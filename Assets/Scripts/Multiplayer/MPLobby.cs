using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoveShot.Multiplayer
{
    // An ability a player takes into the match (two slots, Space and Shift, chosen in the lobby; the two differ).
    public enum MPAbility : byte { Dash, Heal, Powerup, Duplicate }

    // One connected player as the lobby knows them. Slot is also the player's team and color.
    public struct MPPlayerInfo : INetworkSerializable, IEquatable<MPPlayerInfo>
    {
        public ulong ClientId;
        public int Slot;
        public FixedString32Bytes Name;
        public MPAbility AbilitySpace;
        public MPAbility AbilityShift;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref AbilitySpace);
            serializer.SerializeValue(ref AbilityShift);
        }

        public bool Equals(MPPlayerInfo other) => ClientId == other.ClientId && Slot == other.Slot && Name.Equals(other.Name) && AbilitySpace == other.AbilitySpace && AbilityShift == other.AbilityShift;
    }

    // Session-wide networked state, spawned by the host and kept across scene loads: who is connected,
    // their slot (team/color) and name, the room code, and whether a match is running. The host starts
    // matches and brings everyone back to the lobby; clients only submit their own name.
    public class MPLobby : NetworkBehaviour
    {
        public enum LobbyPhase : byte { Lobby, InMatch }
        // FreeForAll: every player is their own team. TwoVsTwo: the first two icons in the row play against the last two.
        public enum TeamMode : byte { FreeForAll, TwoVsTwo }

        [SerializeField, Range(2, MPTeams.MaxTeams)] int minPlayersToStart = 2;

        public static MPLobby Instance { get; private set; }

        readonly NetworkList<MPPlayerInfo> players = new();
        readonly NetworkVariable<LobbyPhase> phase = new(LobbyPhase.Lobby);
        readonly NetworkVariable<FixedString32Bytes> roomCode = new();
        readonly NetworkVariable<TeamMode> mode = new(TeamMode.FreeForAll);
        // FFA only: converted players walk beside their team's lead (and still shoot) instead of keeping full control.
        readonly NetworkVariable<bool> zombieMode = new(true);
        // The icon row: order[position] is the slot shown there. Positions 0-1 and 2-3 are the two sides in 2v2.
        readonly NetworkList<int> order = new();
        // Host only: names received per client, including ones that arrived before the client was added.
        readonly System.Collections.Generic.Dictionary<ulong, string> submittedNames = new();

        public NetworkList<MPPlayerInfo> Players => players;
        public LobbyPhase Phase => phase.Value;
        public string RoomCode => roomCode.Value.ToString();
        public int MinPlayersToStart => minPlayersToStart;
        public TeamMode Mode => mode.Value;
        public bool ZombieMode => zombieMode.Value;
        // Zombies exist only in free-for-all; 2v2 keeps full control for converted players.
        public bool UsesZombies => mode.Value == TeamMode.FreeForAll && zombieMode.Value;
        public bool CanStart => IsServer && phase.Value == LobbyPhase.Lobby && players.Count >= minPlayersToStart && HasValidTeams;

        // FFA needs nothing beyond the player minimum; 2v2 needs at least one player on each side.
        public bool HasValidTeams
        {
            get
            {
                if (mode.Value == TeamMode.FreeForAll) return true;
                var sideHasPlayer = new bool[2];
                foreach (MPPlayerInfo player in players) sideHasPlayer[TeamOfSlot(player.Slot)] = true;
                return sideHasPlayer[0] && sideHasPlayer[1];
            }
        }

        // The slot shown at a position in the icon row.
        public int SlotAt(int position) => position >= 0 && position < order.Count ? order[position] : position;

        public int PositionOf(int slot)
        {
            for (int i = 0; i < order.Count; i++)
                if (order[i] == slot) return i;
            return slot;
        }

        // The team a slot plays for at match start: its own slot in FFA, side 0 or 1 in 2v2.
        public int TeamOfSlot(int slot) => mode.Value == TeamMode.FreeForAll ? slot : (PositionOf(slot) < MPTeams.MaxTeams / 2 ? 0 : 1);

        public override void OnNetworkSpawn()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (IsServer)
            {
                if (order.Count == 0)
                    for (int slot = 0; slot < MPTeams.MaxTeams; slot++) order.Add(slot);
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
            name = MakeUnique(name, clientId);
            players.Add(new MPPlayerInfo { ClientId = clientId, Slot = slot, Name = name, AbilitySpace = MPAbility.Dash, AbilityShift = MPAbility.Heal });
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

        // A name someone else already has gets " #2", " #3"... so the first player keeps the plain name.
        string MakeUnique(string name, ulong clientId)
        {
            string candidate = name;
            for (int n = 2; NameTaken(candidate, clientId); n++) candidate = $"{name} #{n}";
            return candidate;
        }

        bool NameTaken(string name, ulong clientId)
        {
            foreach (MPPlayerInfo player in players)
                if (player.ClientId != clientId && string.Equals(player.Name.ToString(), name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
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
                info.Name = MakeUnique(clean, sender);
                players[i] = info;
            }
        }

        // Any player picks their own abilities while in the lobby (slot 0 = Space, 1 = Shift). The two always differ.
        [Rpc(SendTo.Server)]
        public void SetAbilityRpc(int abilitySlot, MPAbility ability, RpcParams rpcParams = default)
        {
            if (phase.Value != LobbyPhase.Lobby || abilitySlot < 0 || abilitySlot > 1 || (int)ability >= Enum.GetValues(typeof(MPAbility)).Length) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].ClientId != sender) continue;
                MPPlayerInfo info = players[i];
                // Picking the one the other slot holds swaps the two.
                if (abilitySlot == 0)
                {
                    if (ability == info.AbilityShift) info.AbilityShift = info.AbilitySpace;
                    info.AbilitySpace = ability;
                }
                else
                {
                    if (ability == info.AbilitySpace) info.AbilitySpace = info.AbilityShift;
                    info.AbilityShift = ability;
                }
                players[i] = info;
            }
        }

        // Host only: lobby controls (a client's call is ignored).
        [Rpc(SendTo.Server)]
        public void SetModeRpc(TeamMode newMode, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId || phase.Value != LobbyPhase.Lobby) return;
            mode.Value = newMode;
        }

        [Rpc(SendTo.Server)]
        public void SetZombieModeRpc(bool enabled, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId || phase.Value != LobbyPhase.Lobby) return;
            zombieMode.Value = enabled;
        }

        // Swaps the icons at `position` and `position + 1` (connected or not).
        [Rpc(SendTo.Server)]
        public void SwapRpc(int position, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId || phase.Value != LobbyPhase.Lobby) return;
            if (position < 0 || position + 1 >= order.Count) return;
            (order[position], order[position + 1]) = (order[position + 1], order[position]);
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
