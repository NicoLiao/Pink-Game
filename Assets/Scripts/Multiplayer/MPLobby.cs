using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoveShot.Multiplayer
{
    // One connected player as the lobby knows them. Slot is also the player's team and color.
    public struct MPPlayerInfo : INetworkSerializable, IEquatable<MPPlayerInfo>
    {
        public ulong ClientId;
        public int Slot;
        public FixedString32Bytes Name;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref Name);
        }

        public bool Equals(MPPlayerInfo other) => ClientId == other.ClientId && Slot == other.Slot && Name.Equals(other.Name);
    }

    // Session-wide networked state, spawned by the host and kept across scene loads: who is connected,
    // their slot (team/color) and name, the room code, and whether a match is running. The host starts
    // matches and brings everyone back to the lobby; clients only submit their own name.
    public class MPLobby : NetworkBehaviour
    {
        public enum LobbyPhase : byte { Lobby, InMatch }

        [SerializeField, Range(2, MPTeams.MaxTeams)] int minPlayersToStart = 2;

        public static MPLobby Instance { get; private set; }

        readonly NetworkList<MPPlayerInfo> players = new();
        readonly NetworkVariable<LobbyPhase> phase = new(LobbyPhase.Lobby);
        readonly NetworkVariable<FixedString32Bytes> roomCode = new();
        // Host only: names received per client, including ones that arrived before the client was added.
        readonly System.Collections.Generic.Dictionary<ulong, string> submittedNames = new();

        public NetworkList<MPPlayerInfo> Players => players;
        public LobbyPhase Phase => phase.Value;
        public string RoomCode => roomCode.Value.ToString();
        public int MinPlayersToStart => minPlayersToStart;
        public bool CanStart => IsServer && phase.Value == LobbyPhase.Lobby && players.Count >= minPlayersToStart;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += AddPlayer;
                NetworkManager.OnClientDisconnectCallback += RemovePlayer;
                foreach (ulong id in NetworkManager.ConnectedClientsIds) AddPlayer(id);
            }
            if (MPConnection.Instance != null) SubmitNameRpc(MPConnection.Instance.LocalPlayerName);
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= AddPlayer;
                NetworkManager.OnClientDisconnectCallback -= RemovePlayer;
            }
        }

        void Update()
        {
            // The join code becomes available once the session exists, slightly after spawning.
            if (!IsServer || roomCode.Value.Length > 0) return;
            string code = MPConnection.Instance != null ? MPConnection.Instance.Session?.Code : null;
            if (!string.IsNullOrEmpty(code)) roomCode.Value = code;
        }

        public bool TryGetPlayer(ulong clientId, out MPPlayerInfo info)
        {
            foreach (MPPlayerInfo player in players)
            {
                if (player.ClientId != clientId) continue;
                info = player;
                return true;
            }
            info = default;
            return false;
        }

        public bool TryGetPlayerBySlot(int slot, out MPPlayerInfo info)
        {
            foreach (MPPlayerInfo player in players)
            {
                if (player.Slot != slot) continue;
                info = player;
                return true;
            }
            info = default;
            return false;
        }

        public string NameOf(int slot) => TryGetPlayerBySlot(slot, out MPPlayerInfo info) ? info.Name.ToString() : MPTeams.NameOf(slot);

        // ---------- Server ----------

        void AddPlayer(ulong clientId)
        {
            if (TryGetPlayer(clientId, out _)) return;
            int slot = FreeSlot();
            if (slot < 0)
            {
                NetworkManager.DisconnectClient(clientId, "That room is full.");
                return;
            }
            string name = submittedNames.TryGetValue(clientId, out string submitted) ? submitted : $"Player {slot + 1}";
            players.Add(new MPPlayerInfo { ClientId = clientId, Slot = slot, Name = name });
        }

        void RemovePlayer(ulong clientId)
        {
            submittedNames.Remove(clientId);
            for (int i = players.Count - 1; i >= 0; i--)
                if (players[i].ClientId == clientId) players.RemoveAt(i);
        }

        int FreeSlot()
        {
            for (int slot = 0; slot < MPTeams.MaxTeams; slot++)
                if (!TryGetPlayerBySlot(slot, out _)) return slot;
            return -1;
        }

        [Rpc(SendTo.Server)]
        void SubmitNameRpc(FixedString32Bytes playerName, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            string clean = playerName.ToString().Trim();
            if (clean.Length == 0) return;
            if (clean.Length > 16) clean = clean.Substring(0, 16);
            // A joining client sends this during its initial sync, which can arrive before the host's connect
            // callback has added it to the list; remember it so AddPlayer uses it.
            submittedNames[sender] = clean;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].ClientId != sender) continue;
                MPPlayerInfo info = players[i];
                info.Name = clean;
                players[i] = info;
            }
        }

        public void StartMatch()
        {
            if (!CanStart) return;
            phase.Value = LobbyPhase.InMatch;
            MPConnection.Instance?.SetRoomLocked(true);
            NetworkManager.SceneManager.LoadScene(MPConnection.Instance != null ? MPConnection.Instance.ArenaSceneName : "MPArena", LoadSceneMode.Single);
        }

        public void ReturnToLobby()
        {
            if (!IsServer || phase.Value != LobbyPhase.InMatch) return;
            phase.Value = LobbyPhase.Lobby;
            MPConnection.Instance?.SetRoomLocked(false);
            NetworkManager.SceneManager.LoadScene(MPConnection.Instance != null ? MPConnection.Instance.LobbySceneName : "MPLobby", LoadSceneMode.Single);
        }
    }
}
