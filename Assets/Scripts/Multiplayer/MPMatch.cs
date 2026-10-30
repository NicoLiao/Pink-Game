using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace LoveShot.Multiplayer
{
    // One player's live team size.
    public struct MPScore : INetworkSerializable, IEquatable<MPScore>
    {
        public ulong ClientId;
        public int Team;
        public int Count;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Team);
            serializer.SerializeValue(ref Count);
        }

        public bool Equals(MPScore other) => ClientId == other.ClientId && Team == other.Team && Count == other.Count;
    }

    // Host-authoritative free-for-all match. The host spawns players and NPCs (once), runs the
    // wait-for-everyone -> countdown -> timed match -> results flow, keeps the scores, and relays projectiles:
    // it simulates the real projectile and tells clients to show a visual copy. Clients only read the state.
    public class MPMatch : NetworkBehaviour
    {
        public enum MatchPhase : byte { Waiting, Countdown, Playing, Ended }

        [SerializeField, Min(1f)] float matchDuration = 180f;
        [SerializeField, Min(0f)] float countdownDuration = 3f;
        [Tooltip("Longest the host waits for every client to load the arena before starting anyway.")]
        [SerializeField, Min(0f)] float loadTimeout = 10f;

        [Header("Spawning")]
        [SerializeField] NetworkObject playerPrefab;
        [SerializeField] NetworkObject npcPrefab;
        [Tooltip("Player spawn per slot (team 0..3).")]
        [SerializeField] Vector2[] playerSpawns =
        {
            new Vector2(-21f, 10f), new Vector2(21f, -10f), new Vector2(21f, 10f), new Vector2(-21f, -10f),
        };
        [SerializeField, Min(0)] int baseNpcCount = 10;
        [SerializeField, Min(0)] int npcsPerPlayer = 3;
        [Tooltip("NPCs spawn inside this half-size around the center.")]
        [SerializeField] Vector2 npcSpawnHalfExtents = new Vector2(26f, 15f);
        [SerializeField, Min(0f)] float npcClearance = 0.8f;
        [Tooltip("NPCs never spawn this close to a player spawn.")]
        [SerializeField, Min(0f)] float playerSpawnClearance = 4f;

        [Header("Projectiles")]
        [SerializeField] MPProjectile projectilePrefab;
        [SerializeField] HeartBurst burstPrefab;

        [SerializeField, Min(0.05f)] float scoreInterval = 0.2f;

        public static MPMatch Instance { get; private set; }
        // True while gameplay should run (between GO and the end of the timer).
        public static bool InProgress => Instance != null && Instance.phase.Value == MatchPhase.Playing;

        readonly NetworkVariable<MatchPhase> phase = new(MatchPhase.Waiting);
        readonly NetworkVariable<double> phaseEndTime = new(0d);
        readonly NetworkList<MPScore> scores = new();

        readonly Dictionary<ushort, MPProjectile> projectiles = new();
        ushort nextProjectileId;
        bool spawned;
        bool everyoneLoaded;
        float waitStartTime;
        float nextScoreTime;

        public MatchPhase Phase => phase.Value;
        public NetworkList<MPScore> Scores => scores;
        public float MatchDuration => matchDuration;
        double Now => NetworkManager.ServerTime.Time;
        public float TimeRemaining => phase.Value switch
        {
            MatchPhase.Playing => Mathf.Max(0f, (float)(phaseEndTime.Value - Now)),
            MatchPhase.Ended => 0f,
            _ => matchDuration,
        };
        public float CountdownRemaining => phase.Value == MatchPhase.Countdown ? Mathf.Max(0f, (float)(phaseEndTime.Value - Now)) : 0f;

        // Raised on every machine.
        public event Action<MatchPhase> PhaseChanged;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            phase.OnValueChanged += OnPhaseValueChanged;
            if (!IsServer) return;

            waitStartTime = Time.time;
            NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            SpawnEverything();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            phase.OnValueChanged -= OnPhaseValueChanged;
            if (IsServer && NetworkManager != null)
            {
                if (NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        void OnPhaseValueChanged(MatchPhase previous, MatchPhase current) => PhaseChanged?.Invoke(current);

        void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut) => everyoneLoaded = true;

        void Update()
        {
            if (!IsServer) return;
            switch (phase.Value)
            {
                case MatchPhase.Waiting:
                    if (everyoneLoaded || Time.time - waitStartTime >= loadTimeout) SetPhase(MatchPhase.Countdown, countdownDuration);
                    break;
                case MatchPhase.Countdown:
                    if (Now >= phaseEndTime.Value) SetPhase(MatchPhase.Playing, matchDuration);
                    break;
                case MatchPhase.Playing:
                    if (Time.time >= nextScoreTime)
                    {
                        nextScoreTime = Time.time + scoreInterval;
                        UpdateScores();
                    }
                    if (Now >= phaseEndTime.Value || scores.Count < 2) EndMatch();
                    break;
            }
        }

        void SetPhase(MatchPhase next, float duration)
        {
            phaseEndTime.Value = Now + duration;
            phase.Value = next;
        }

        void EndMatch()
        {
            UpdateScores();
            phase.Value = MatchPhase.Ended;
        }

        // ---------- Spawning (host, once) ----------

        void SpawnEverything()
        {
            if (spawned) return;
            spawned = true;

            MPLobby lobby = MPLobby.Instance;
            var participants = new List<MPPlayerInfo>();
            if (lobby != null)
                foreach (MPPlayerInfo info in lobby.Players)
                    if (NetworkManager.ConnectedClients.ContainsKey(info.ClientId)) participants.Add(info);

            foreach (MPPlayerInfo info in participants)
            {
                Vector2 position = playerSpawns[info.Slot % playerSpawns.Length];
                NetworkObject player = Instantiate(playerPrefab, position, Quaternion.identity);
                player.GetComponent<MPPlayer>().SetTeam(info.Slot);
                player.SpawnAsPlayerObject(info.ClientId, destroyWithScene: true);
                scores.Add(new MPScore { ClientId = info.ClientId, Team = info.Slot, Count = 0 });
            }

            Physics2D.SyncTransforms();
            int count = baseNpcCount + npcsPerPlayer * participants.Count;
            for (int i = 0; i < count; i++)
            {
                if (!TryFindNpcSpot(out Vector2 position)) continue;
                NetworkObject npc = Instantiate(npcPrefab, position, Quaternion.identity);
                npc.name = $"{npcPrefab.name} {i + 1}";
                npc.Spawn(destroyWithScene: true);
                Physics2D.SyncTransforms();
            }
        }

        bool TryFindNpcSpot(out Vector2 position)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                position = new Vector2(Random.Range(-npcSpawnHalfExtents.x, npcSpawnHalfExtents.x),
                    Random.Range(-npcSpawnHalfExtents.y, npcSpawnHalfExtents.y));
                if (Physics2D.OverlapCircle(position, npcClearance) != null) continue;
                bool nearSpawn = false;
                foreach (Vector2 spawn in playerSpawns)
                    if (Vector2.Distance(spawn, position) < playerSpawnClearance) nearSpawn = true;
                if (!nearSpawn) return true;
            }
            position = default;
            return false;
        }

        // ---------- Scores ----------

        void UpdateScores()
        {
            var counts = new int[MPTeams.MaxTeams];
            foreach (MPNpc npc in MPNpc.All)
                if (MPTeams.IsTeam(npc.Team)) counts[npc.Team]++;
            for (int i = 0; i < scores.Count; i++)
            {
                MPScore score = scores[i];
                int count = counts[score.Team];
                if (score.Count == count) continue;
                score.Count = count;
                scores[i] = score;
            }
        }

        // Highest team size first; equal sizes share a rank (ties are allowed).
        public List<(MPScore score, int rank)> Ranking()
        {
            var list = new List<MPScore>();
            foreach (MPScore score in scores) list.Add(score);
            list.Sort((a, b) => b.Count != a.Count ? b.Count.CompareTo(a.Count) : a.Team.CompareTo(b.Team));
            var ranked = new List<(MPScore, int)>();
            for (int i = 0; i < list.Count; i++)
            {
                int rank = i > 0 && list[i].Count == list[i - 1].Count ? ranked[i - 1].Item2 : i + 1;
                ranked.Add((list[i], rank));
            }
            return ranked;
        }

        void OnClientDisconnected(ulong clientId)
        {
            // A player who leaves mid-match loses their team: their NPCs turn neutral and their score row goes.
            for (int i = scores.Count - 1; i >= 0; i--)
            {
                if (scores[i].ClientId != clientId) continue;
                int team = scores[i].Team;
                if (phase.Value != MatchPhase.Ended)
                {
                    foreach (MPNpc npc in MPNpc.All)
                        if (npc.Team == team) npc.Capture.SetOwner(MPTeams.Neutral);
                    scores.RemoveAt(i);
                }
            }
        }

        // ---------- Projectiles ----------

        // Host only. Fires an authoritative projectile and shows a visual copy on every client.
        public void Fire(int team, Vector2 origin, Vector2 direction, float speed, float power, bool fromPlayer, ulong shooterObjectId)
        {
            if (!IsServer || projectilePrefab == null || direction.sqrMagnitude < 0.0001f) return;
            ushort id = ++nextProjectileId;
            Vector2 velocity = direction.normalized * speed;
            MPProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
            projectile.Launch(this, id, team, velocity, power, fromPlayer, shooterObjectId, authoritative: true);
            projectiles[id] = projectile;
            SpawnProjectileRpc(id, origin, velocity, team);
        }

        [Rpc(SendTo.NotServer)]
        void SpawnProjectileRpc(ushort id, Vector2 origin, Vector2 velocity, int team)
        {
            MPProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
            projectile.Launch(this, id, team, velocity, 0f, false, 0, authoritative: false);
            projectiles[id] = projectile;
        }

        // Host: called once when an authoritative projectile hits something.
        public void OnProjectileRemoved(ushort id, Vector2 position)
        {
            HeartBurst.Play(burstPrefab, position, Color.white, 0.35f, 3);
            RemoveProjectileRpc(id, position);
        }

        [Rpc(SendTo.NotServer)]
        void RemoveProjectileRpc(ushort id, Vector2 position)
        {
            HeartBurst.Play(burstPrefab, position, Color.white, 0.35f, 3);
            if (projectiles.TryGetValue(id, out MPProjectile projectile) && projectile != null) projectile.Remove(false);
        }

        public void ForgetProjectile(MPProjectile projectile)
        {
            if (projectiles.TryGetValue(projectile.Id, out MPProjectile stored) && stored == projectile) projectiles.Remove(projectile.Id);
        }
    }
}
