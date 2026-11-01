using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoveShot.Multiplayer
{
    // Owns the online session: signs in to Unity Gaming Services, hosts or joins a private Relay session
    // (the Multiplayer Services SDK then starts Netcode as host or client), approves connections, and
    // sends everyone back to the menu with a message when the session ends. Lives on the NetworkManager prefab,
    // which the menu instantiates on demand and which is destroyed whenever the session closes.
    [RequireComponent(typeof(NetworkManager))]
    public class MPConnection : MonoBehaviour
    {
        [SerializeField, Range(2, MPTeams.MaxTeams)] int maxPlayers = 4;
        [SerializeField] string menuSceneName = "MainMenu";
        [SerializeField] string lobbySceneName = "MPLobby";
        [SerializeField] string arenaSceneName = "MPArena";
        [Tooltip("Networked lobby state (MPLobby), spawned by the host and kept for the whole session.")]
        [SerializeField] NetworkObject lobbyPrefab;
        [Tooltip("Seconds the host waits after telling clients it is leaving, so the message reaches them.")]
        [SerializeField, Min(0f)] float hostLeaveDelay = 0.3f;

        public static MPConnection Instance { get; private set; }
        // Read by the main menu after a session ends: open the multiplayer page and show this message.
        public static bool ReturnToMultiplayerMenu { get; set; }
        public static string PendingMessage { get; set; }

        public ISession Session { get; private set; }
        public string LocalPlayerName { get; private set; }
        const string NameKey = "LoveShot.PlayerName";
        const int MaxNameLength = 16;

        // The name saved from the last time the player typed one (empty if none).
        public static string SavedName => PlayerPrefs.GetString(NameKey, "");

        // Takes the typed name (empty keeps the current one) and remembers it for next time.
        public void SetLocalName(string typed)
        {
            string clean = (typed ?? "").Trim();
            if (clean.Length == 0) return;
            if (clean.Length > MaxNameLength) clean = clean.Substring(0, MaxNameLength);
            LocalPlayerName = clean;
            PlayerPrefs.SetString(NameKey, clean);
        }
        public int MaxPlayers => maxPlayers;
        public string LobbySceneName => lobbySceneName;
        public string ArenaSceneName => arenaSceneName;
        public bool IsClosing => closing;

        NetworkManager network;
        bool closing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            ReturnToMultiplayerMenu = false;
            PendingMessage = null;
        }

        public static MPConnection Ensure(MPConnection prefab) => Instance != null ? Instance : Instantiate(prefab);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            network = GetComponent<NetworkManager>();
            LocalPlayerName = SavedName.Length > 0 ? SavedName : $"Guest{UnityEngine.Random.Range(1000, 10000)}";
            network.ConnectionApprovalCallback = ApproveConnection;
            network.OnServerStarted += OnServerStarted;
            network.OnClientStopped += OnClientStopped;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (network == null) return;
            network.ConnectionApprovalCallback = null;
            network.OnServerStarted -= OnServerStarted;
            network.OnClientStopped -= OnClientStopped;
        }

        // ---------- Host / join ----------

        // Each returns null on success, or a message for the player.
        public async Task<string> HostAsync()
        {
            try
            {
                await SignInAsync();
                SessionOptions options = new SessionOptions
                {
                    MaxPlayers = maxPlayers,
                    IsPrivate = true,
                    Name = $"{LocalPlayerName}'s room",
                }.WithRelayNetwork();
                Session = await MultiplayerService.Instance.CreateSessionAsync(options);
                WatchSession();
                // Netcode now runs as host; OnServerStarted spawned the lobby state. Bring everyone to the lobby.
                network.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LoveShot] Hosting failed: {e}");
                return Describe(e, hosting: true);
            }
        }

        public async Task<string> JoinAsync(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) return "Enter a room code.";
            try
            {
                await SignInAsync();
                Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                WatchSession();
                // The host's Netcode scene sync moves this client into the lobby scene.
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LoveShot] Joining {code} failed: {e}");
                return Describe(e, hosting: false);
            }
        }

        static async Task SignInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                // One profile per running game process, so several copies on one machine (Multiplayer Play Mode
                // virtual players, two builds) sign in as different anonymous players.
                var options = new InitializationOptions();
                options.SetProfile($"p{System.Diagnostics.Process.GetCurrentProcess().Id}");
                await UnityServices.InitializeAsync(options);
            }
            while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Yield();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static string Describe(Exception e, bool hosting)
        {
            string text = "";
            for (Exception x = e; x != null; x = x.InnerException) text += " " + x.Message;
            text = text.ToLowerInvariant();
            SessionError error = e is SessionException session ? session.Error : SessionError.None;

            if (text.Contains("full")) return "That room is full.";
            if (text.Contains("locked")) return "That room's match has already started.";
            if (!hosting && (error == SessionError.SessionNotFound || error == SessionError.InvalidSessionIdentifier ||
                             error == SessionError.InvalidParameter || text.Contains("not found") || text.Contains("invalid")))
                return "No room found with that code. Check it and try again.";
            if (e is AuthenticationException || e is RequestFailedException || text.Contains("network") || text.Contains("connect"))
                return hosting ? "Couldn't create a room. Check your internet connection and try again."
                               : "Couldn't connect to that room. Check your internet connection and try again.";
            return (hosting ? "Couldn't create a room: " : "Couldn't join: ") + e.Message;
        }

        void WatchSession()
        {
            if (Session == null) return;
            Session.RemovedFromSession += () => OnSessionLost("You were removed from the room.");
            Session.Deleted += () => OnSessionLost("The host closed the room.");
        }

        void OnSessionLost(string message)
        {
            if (closing || network.IsServer) return;
            Close(message, toMultiplayerMenu: true);
        }

        // Locks the room while a match runs, so the join screen reports it instead of connecting.
        public async void SetRoomLocked(bool locked)
        {
            if (Session == null || !Session.IsHost) return;
            try
            {
                IHostSession host = Session.AsHost();
                host.IsLocked = locked;
                await host.SavePropertiesAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LoveShot] Couldn't {(locked ? "lock" : "unlock")} the room: {e.Message}");
            }
        }

        // ---------- Netcode callbacks ----------

        void OnServerStarted()
        {
            if (lobbyPrefab == null) return;
            NetworkObject lobby = Instantiate(lobbyPrefab);
            DontDestroyOnLoad(lobby.gameObject);
            lobby.Spawn(destroyWithScene: false);
        }

        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                return;
            }

            if (network.ConnectedClientsIds.Count >= maxPlayers)
                Reject(response, $"That room is full ({maxPlayers}/{maxPlayers}).");
            else if (MPLobby.Instance != null && MPLobby.Instance.Phase != MPLobby.LobbyPhase.Lobby)
                Reject(response, "That room's match has already started.");
            else
                response.Approved = true;
        }

        static void Reject(NetworkManager.ConnectionApprovalResponse response, string reason)
        {
            response.Approved = false;
            response.Reason = reason;
        }

        void OnClientStopped(bool wasHost)
        {
            if (closing) return;
            // Our own reasons (room full, host left...) are plain sentences; Netcode's generated ones
            // ("[Disconnect Event][Client-1]...") are replaced with something readable.
            string reason = network.DisconnectReason;
            if (string.IsNullOrEmpty(reason) || reason.StartsWith("["))
                reason = wasHost ? "The session ended." : "Lost connection to the host.";
            Close(reason, toMultiplayerMenu: true);
        }

        // ---------- Leaving ----------

        // Leaves the session (as host this ends it for everyone) and loads the main menu.
        public void Close(string message = null, bool toMultiplayerMenu = true) => _ = CloseAsync(message, toMultiplayerMenu);

        async Task CloseAsync(string message, bool toMultiplayerMenu)
        {
            if (closing) return;
            closing = true;

            if (network.IsServer && network.IsListening)
            {
                foreach (ulong id in new List<ulong>(network.ConnectedClientsIds))
                    if (id != NetworkManager.ServerClientId) network.DisconnectClient(id, "The host left the game.");
                if (hostLeaveDelay > 0f) await Task.Delay(Mathf.RoundToInt(hostLeaveDelay * 1000f));
            }

            try
            {
                if (Session != null) await Session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LoveShot] Leaving the session failed: {e.Message}");
            }
            Session = null;
            if (network != null && network.IsListening) network.Shutdown(true);

            Time.timeScale = 1f;
            ReturnToMultiplayerMenu = toMultiplayerMenu;
            PendingMessage = message;
            Destroy(gameObject);
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
