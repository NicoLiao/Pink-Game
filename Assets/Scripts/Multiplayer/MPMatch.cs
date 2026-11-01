using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace LoveShot.Multiplayer
{
    // One team's live score: weighted NPC points (Celebrities count more) and plain NPC count.
    public struct MPScore : INetworkSerializable, IEquatable<MPScore>
    {
        public int Team;
        public int Points;
        public int Npcs;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Team);
            serializer.SerializeValue(ref Points);
            serializer.SerializeValue(ref Npcs);
        }

        public bool Equals(MPScore other) => Team == other.Team && Points == other.Points && Npcs == other.Npcs;
    }

    // One NPC type in the match's spawn table.
    [Serializable]
    public class NpcSpawnOption
    {
        [SerializeField] NetworkObject prefab;
        [SerializeField, Min(0f)] float weight = 1f;
        [Tooltip("Always spawn at least this many (if the total allows).")]
        [SerializeField, Min(0)] int minCount;
        [Tooltip("Never spawn more than this many. 0 = no limit.")]
        [SerializeField, Min(0)] int maxCount;

        public NetworkObject Prefab => prefab;
        public float Weight => weight;
        public int MinCount => minCount;
        public int MaxCount => maxCount;
        public bool IsFull(int spawned) => maxCount > 0 && spawned >= maxCount;
    }

    // Host-authoritative free-for-all match. The host spawns players and NPCs (once), runs the
    // wait-for-everyone -> countdown -> timed match -> results flow, resolves every love hit (ApplyLove), keeps
    // the team scores (ends early once every player is on one team), and relays projectiles:
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
        [Tooltip("NPC types with spawn weights and per-match limits. Normal NPCs should keep most of the weight.")]
        [SerializeField] NpcSpawnOption[] npcTypes;
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
        [Tooltip("Shots fired by NPCs (a separate look and size from the players' shots).")]
        [SerializeField] MPProjectile npcProjectilePrefab;
        [SerializeField] HeartBurst burstPrefab;
        [SerializeField] HeartRing ringPrefab;
        [SerializeField, Min(0f)] float explosionShake = 0.25f;

        [Header("Weapons")]
        [Tooltip("Index 0 is the Default Gun every player starts with; the rest can be picked up.")]
        [SerializeField] MPWeapon[] weapons;

        [Header("Rules")]
        [Tooltip("End the match as soon as every remaining player is on the same team.")]
        [SerializeField] bool endWhenOneTeamLeft = true;
        [SerializeField, Min(0.05f)] float scoreInterval = 0.2f;

        public static MPMatch Instance { get; private set; }
        // True while gameplay should run (between GO and the end of the timer).
        public static bool InProgress => Instance != null && Instance.phase.Value == MatchPhase.Playing;

        readonly NetworkVariable<MatchPhase> phase = new(MatchPhase.Waiting);
        readonly NetworkVariable<double> phaseEndTime = new(0d);
        readonly NetworkList<MPScore> scores = new();

        readonly Dictionary<ushort, MPProjectile> projectiles = new();
        // Client-side: own shots shown before the host confirms them (null once they hit), and ids whose
        // copy already burst locally.
        readonly Dictionary<uint, MPProjectile> predicted = new();
        readonly HashSet<ushort> resolvedLocally = new();
        readonly Dictionary<ulong, int> slotsByClient = new();
        ushort nextProjectileId;
        bool spawned;
        bool everyoneLoaded;
        float waitStartTime;
        float nextScoreTime;

        public MatchPhase Phase => phase.Value;
        public int WeaponCount => weapons != null ? weapons.Length : 0;
        public MPWeapon WeaponAt(int index) => weapons != null && index >= 0 && index < weapons.Length ? weapons[index] : null;
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
                    if (Now >= phaseEndTime.Value || (endWhenOneTeamLeft && scores.Count < 2)) EndMatch();
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
                MPPlayer spawnedPlayer = player.GetComponent<MPPlayer>();
                spawnedPlayer.InitSlot(info.Slot, lobby.TeamOfSlot(info.Slot));
                spawnedPlayer.InitAbilities(info.AbilitySpace, info.AbilityShift);
                player.SpawnAsPlayerObject(info.ClientId, destroyWithScene: true);
                slotsByClient[info.ClientId] = info.Slot;
            }

            Physics2D.SyncTransforms();
            int count = baseNpcCount + npcsPerPlayer * participants.Count;
            var perType = new int[npcTypes.Length];
            for (int i = 0; i < count; i++)
            {
                int type = PickNpcType(perType);
                if (type < 0 || !TryFindNpcSpot(out Vector2 position)) continue;
                perType[type]++;
                NetworkObject prefab = npcTypes[type].Prefab;
                NetworkObject npc = Instantiate(prefab, position, Quaternion.identity);
                npc.name = $"{prefab.name} {i + 1}";
                npc.Spawn(destroyWithScene: true);
                Physics2D.SyncTransforms();
            }
            UpdateScores();
        }

        // Host only: spawns another NPC of the same kind as `template` for a team and player (Duplicate).
        public MPNpc ServerSpawnNpcLike(MPNpc template, Vector2 position, int team, int slot)
        {
            if (!IsServer || template == null || npcTypes == null) return null;
            foreach (NpcSpawnOption option in npcTypes)
            {
                NetworkObject prefab = option.Prefab;
                if (prefab == null || !prefab.TryGetComponent(out MPNpc candidate)
                    || candidate.GetType() != template.GetType() || candidate.DefinedType != template.DefinedType) continue;
                NetworkObject copy = Instantiate(prefab, position, Quaternion.identity);
                copy.GetComponent<MPCapture>().InitOwner(team, slot);
                copy.Spawn(destroyWithScene: true);
                Physics2D.SyncTransforms();
                return copy.GetComponent<MPNpc>();
            }
            return null;
        }

        // The pickup spawner on the same prefab (host).
        public MPPickupSpawner Pickups => pickups != null ? pickups : pickups = GetComponent<MPPickupSpawner>();
        MPPickupSpawner pickups;

        // Types still under their minimum come first; then a weighted pick among types not at their maximum.
        int PickNpcType(int[] spawned)
        {
            for (int i = 0; i < npcTypes.Length; i++)
                if (npcTypes[i].Prefab != null && spawned[i] < npcTypes[i].MinCount && !npcTypes[i].IsFull(spawned[i])) return i;
            float total = 0f;
            for (int i = 0; i < npcTypes.Length; i++)
                if (npcTypes[i].Prefab != null && !npcTypes[i].IsFull(spawned[i])) total += npcTypes[i].Weight;
            if (total <= 0f) return -1;
            float roll = Random.value * total;
            int last = -1;
            for (int i = 0; i < npcTypes.Length; i++)
            {
                if (npcTypes[i].Prefab == null || npcTypes[i].IsFull(spawned[i]) || npcTypes[i].Weight <= 0f) continue;
                last = i;
                roll -= npcTypes[i].Weight;
                if (roll <= 0f) return i;
            }
            return last;
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

        // ---------- Love and scores ----------

        // Host: one hit of `power` love on `target`, credited to the player in `creditSlot`. The hit counts
        // for that player's team at this moment (a shot still in flight when its shooter is converted
        // already counts for the new team). Returns false if the shot passes through without effect.
        public bool ApplyLove(MPCapture target, int creditSlot, float power, bool fromPlayer)
        {
            if (!IsServer || phase.Value != MatchPhase.Playing || target == null) return false;
            MPPlayer credit = MPPlayer.ForSlot(creditSlot);
            if (credit == null) return false;
            int team = credit.Team;
            if (target.Player != null) return target.Player.ServerReceiveLove(team, creditSlot, power, fromPlayer);
            if (!target.CanBeAffectedBy(team)) return false;
            target.ApplyHit(team, power, creditSlot);
            return true;
        }

        // Host: called right after a player changed team.
        public void OnPlayerConverted() => UpdateScores();

        // Team scores, for teams that still have players. Also cleans up after players who left:
        // their NPCs turn neutral, and captures in progress by teams with no players are dropped.
        void UpdateScores()
        {
            var points = new int[MPTeams.MaxTeams];
            var npcs = new int[MPTeams.MaxTeams];
            foreach (MPNpc npc in MPNpc.All)
            {
                if (!MPTeams.IsTeam(npc.Team)) continue;
                if (MPPlayer.ForSlot(npc.PersonalOwner) == null)
                {
                    npc.Capture.SetOwner(MPTeams.Neutral, MPTeams.Neutral);
                    continue;
                }
                points[npc.Team] += npc.ScoreValue;
                npcs[npc.Team]++;
            }

            var current = new List<MPScore>();
            for (int team = 0; team < MPTeams.MaxTeams; team++)
            {
                if (MPPlayer.TeamHasPlayers(team))
                {
                    current.Add(new MPScore { Team = team, Points = points[team], Npcs = npcs[team] });
                    continue;
                }
                foreach (MPCapture capture in MPCapture.All) capture.ClearCapture(team);
            }

            bool same = current.Count == scores.Count;
            for (int i = 0; same && i < current.Count; i++) same = current[i].Equals(scores[i]);
            if (same) return;
            scores.Clear();
            foreach (MPScore score in current) scores.Add(score);
        }

        // Highest score first; equal scores share a rank (ties are allowed).
        public List<(MPScore score, int rank)> Ranking()
        {
            var list = new List<MPScore>();
            foreach (MPScore score in scores) list.Add(score);
            list.Sort((a, b) => b.Points != a.Points ? b.Points.CompareTo(a.Points) : a.Team.CompareTo(b.Team));
            var ranked = new List<(MPScore, int)>();
            for (int i = 0; i < list.Count; i++)
            {
                int rank = i > 0 && list[i].Points == list[i - 1].Points ? ranked[i - 1].Item2 : i + 1;
                ranked.Add((list[i], rank));
            }
            return ranked;
        }

        // Players currently on `team`, by slot (works on every machine).
        public static List<MPPlayer> MembersOf(int team)
        {
            var members = new List<MPPlayer>();
            foreach (MPPlayer player in MPPlayer.All)
                if (player.Team == team) members.Add(player);
            members.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            return members;
        }

        void OnClientDisconnected(ulong clientId)
        {
            // A player who leaves mid-match takes nothing along: the NPCs they personally own turn neutral.
            if (phase.Value == MatchPhase.Ended || !slotsByClient.TryGetValue(clientId, out int slot)) return;
            foreach (MPNpc npc in MPNpc.All)
                if (npc.PersonalOwner == slot) npc.Capture.SetOwner(MPTeams.Neutral, MPTeams.Neutral);
        }

        // ---------- Projectiles ----------

        // Host only. Fires one resolved shot (all its pellets, spread evenly across the cone).
        // predictSequence (non-zero) is the owner's shot number: the owner already shows these pellets.
        public void FireWeapon(MPFiring firing, int team, int creditSlot, Vector2 origin, Vector2 direction, ulong shooterObjectId,
            ushort predictSequence = 0)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            for (int i = 0; i < firing.Pellets; i++)
                Fire(team, creditSlot, origin, PelletDirection(firing, direction, i), firing.Shot, true, shooterObjectId, PredictKey(predictSequence, i));
        }

        static Vector2 PelletDirection(MPFiring firing, Vector2 direction, int pellet)
        {
            int pellets = firing.Pellets;
            float angle = pellets == 1 ? 0f : Mathf.Lerp(-firing.SpreadAngle / 2f, firing.SpreadAngle / 2f, pellet / (pellets - 1f));
            return Quaternion.Euler(0f, 0f, angle) * direction;
        }

        // Identifies one predicted pellet: the owner's shot number and the pellet's index. 0 = not predicted.
        static uint PredictKey(ushort sequence, int pellet) => sequence == 0 ? 0u : ((uint)sequence << 5) | (uint)(pellet & 31);

        // Host only. Fires an authoritative projectile and shows a visual copy on every client.
        // creditSlot is the player credited with its hits (an NPC's personal owner for NPC shots).
        public void Fire(int team, int creditSlot, Vector2 origin, Vector2 direction, MPShot shot, bool fromPlayer, ulong shooterObjectId,
            uint predictKey = 0)
        {
            if (!IsServer || projectilePrefab == null || direction.sqrMagnitude < 0.0001f) return;
            if (++nextProjectileId == 0) nextProjectileId = 1;
            ushort id = nextProjectileId;
            Vector2 velocity = direction.normalized * shot.Speed;
            MPProjectile projectile = Instantiate(PrefabFor(fromPlayer), origin, Quaternion.identity);
            projectile.Launch(this, id, team, creditSlot, velocity, shot, fromPlayer, shooterObjectId, authoritative: true);
            projectiles[id] = projectile;
            SpawnProjectileRpc(id, origin, velocity, team, creditSlot, (byte)shot.Style, shot.Scale, (byte)shot.Bounces, shot.Lifetime, shot.ExplosionRadius,
                shooterObjectId, predictKey, fromPlayer);
        }

        MPProjectile PrefabFor(bool fromPlayer) => fromPlayer || npcProjectilePrefab == null ? projectilePrefab : npcProjectilePrefab;

        [Rpc(SendTo.NotServer)]
        void SpawnProjectileRpc(ushort id, Vector2 origin, Vector2 velocity, int team, int creditSlot, byte style, float scale, byte bounces, float lifetime,
            float explosionRadius, ulong shooterObjectId, uint predictKey, bool fromPlayer)
        {
            // Our own predicted shot: the copy already flying becomes this projectile (or already hit something).
            if (predictKey != 0 && MPPlayer.Local != null && MPPlayer.Local.NetworkObjectId == shooterObjectId
                && predicted.Remove(predictKey, out MPProjectile own))
            {
                if (own != null)
                {
                    own.AssignId(id);
                    projectiles[id] = own;
                }
                else MarkResolved(id);
                return;
            }
            // Visual copy: power doesn't matter, the rest must match the host's flight exactly.
            var shot = new MPShot((ProjectileStyle)style, velocity.magnitude, 0f, lifetime, scale, bounces, explosionRadius);
            MPProjectile projectile = Instantiate(PrefabFor(fromPlayer), origin, Quaternion.identity);
            projectile.Launch(this, id, team, creditSlot, velocity, shot, fromPlayer, shooterObjectId, authoritative: false);
            projectiles[id] = projectile;
        }

        // Client, owner: shows its own weapon shot immediately, before the host confirms it.
        public void PredictWeapon(MPFiring firing, int team, int creditSlot, Vector2 origin, Vector2 direction, ulong shooterObjectId, ushort sequence)
        {
            if (IsServer || projectilePrefab == null) return;
            // Unconfirmed leftovers (e.g. a modifier ran out and changed the pellet count) are cosmetic; don't let them pile up.
            if (predicted.Count > 128) predicted.Clear();
            MPShot shot = firing.Shot;
            for (int i = 0; i < firing.Pellets; i++)
            {
                Vector2 velocity = PelletDirection(firing, direction, i).normalized * shot.Speed;
                MPProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
                projectile.Launch(this, 0, team, creditSlot, velocity, shot, true, shooterObjectId, authoritative: false);
                uint key = PredictKey(sequence, i);
                projectile.PredictKey = key;
                predicted[key] = projectile;
            }
        }

        // Client, owner: the host refused this shot (cooldown, lovestruck), so its predicted copies vanish.
        public void CancelPrediction(ushort sequence)
        {
            for (int i = 0; i < 32; i++)
                if (predicted.Remove(PredictKey(sequence, i), out MPProjectile projectile) && projectile != null) projectile.Remove(false);
        }

        // Client: a visual copy touched something it will very likely hit, so it bursts now instead of
        // flying through until the host's word arrives. The host's later report of the same hit then stays quiet.
        public void OnLocalImpact(MPProjectile projectile)
        {
            if (projectile.Id != 0) MarkResolved(projectile.Id);
            else if (projectile.PredictKey != 0 && predicted.ContainsKey(projectile.PredictKey)) predicted[projectile.PredictKey] = null;
            PlayImpact(projectile.transform.position, MPTeams.Neutral, 0f);
        }

        void MarkResolved(ushort id)
        {
            // Copies whose host shot missed are never reported; drop old ids before they could collide after wrapping.
            if (resolvedLocally.Count > 256) resolvedLocally.Clear();
            resolvedLocally.Add(id);
        }

        // Client: whether a copy flying for `team` would plausibly affect `target` (the host decides for real).
        public bool LooksHittable(MPCapture target, int team)
        {
            if (target == null || !MPTeams.IsTeam(team) || target.Owner == team) return false;
            if (target.Player != null) return !target.Player.IsConversionImmune;
            return target.CanBeAffectedBy(team);
        }

        // Host: whether a hit credited to `creditSlot` would do anything to `target` (rockets only burst on those).
        public bool CanTakeLove(MPCapture target, int creditSlot)
        {
            MPPlayer credit = MPPlayer.ForSlot(creditSlot);
            if (credit == null || target == null || target.Owner == credit.Team) return false;
            if (target.Player != null) return !target.Player.IsConversionImmune;
            return target.CanBeAffectedBy(credit.Team);
        }

        // Host: an explosion. Every character in the radius gets love once (a set, however many colliders
        // it has), more the closer it is to the center.
        public void Explode(Vector2 position, MPShot shot, int creditSlot, bool fromPlayer)
        {
            var targets = new HashSet<MPCapture>();
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(position, shot.ExplosionRadius))
            {
                GameObject hit = collider.attachedRigidbody != null ? collider.attachedRigidbody.gameObject : collider.gameObject;
                if (hit.TryGetComponent(out MPCapture capture)) targets.Add(capture);
            }
            foreach (MPCapture target in targets)
            {
                float t = Mathf.Clamp01(Vector2.Distance(position, target.transform.position) / shot.ExplosionRadius);
                ApplyLove(target, creditSlot, shot.Power * Mathf.Lerp(1f, shot.ExplosionEdgePower, t), fromPlayer);
            }
        }

        // Host: called once when an authoritative projectile hits something (or a rocket explodes).
        public void OnProjectileRemoved(ushort id, Vector2 position, int team, float explosionRadius)
        {
            PlayImpact(position, team, explosionRadius);
            RemoveProjectileRpc(id, position, team, explosionRadius);
        }

        [Rpc(SendTo.NotServer)]
        void RemoveProjectileRpc(ushort id, Vector2 position, int team, float explosionRadius)
        {
            // Explosions always play here; a heart that already burst locally doesn't burst twice.
            if (!resolvedLocally.Remove(id) || explosionRadius > 0f) PlayImpact(position, team, explosionRadius);
            if (projectiles.TryGetValue(id, out MPProjectile projectile) && projectile != null) projectile.Remove(false);
        }

        // (position, explosion radius or 0 for a plain hit), raised on every machine whenever a shot lands.
        public static event Action<Vector2, float> ImpactPlayed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetImpactEvent() => ImpactPlayed = null;

        void PlayImpact(Vector2 position, int team, float explosionRadius)
        {
            ImpactPlayed?.Invoke(position, explosionRadius);
            if (explosionRadius <= 0f)
            {
                HeartBurst.Play(burstPrefab, position, Color.white, 0.35f, 3);
                return;
            }
            HeartRing.Play(ringPrefab, position, MPTeams.ColorOf(team), explosionRadius, 18);
            HeartBurst.Play(burstPrefab, position, Color.white, 1.4f, 10);
            if (MPPlayer.Local != null && Vector2.Distance(MPPlayer.Local.Position, position) < explosionRadius * 2.5f) CameraShake.Shake(explosionShake);
        }

        public void ForgetProjectile(MPProjectile projectile)
        {
            if (projectiles.TryGetValue(projectile.Id, out MPProjectile stored) && stored == projectile) projectiles.Remove(projectile.Id);
        }
    }
}
