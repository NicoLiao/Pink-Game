using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Networked NPC brain, run only on the host (clients just see the synced position, owner and capture).
    // Neutral NPCs wander; owned NPCs follow their personal owner (the player who recruited them), keep
    // spacing from teammates and shoot NPCs of every other team (neutral or enemy-owned). Mirrors the
    // single-player NPCController behavior, but works with the 4-team MPCapture instead of Pink/Blue
    // Affection, so the two modes stay independent.
    // Special types subclass this and override Wander / Follow / UpdateCombat / TargetScore / Fire / OnTeamChanged.
    [RequireComponent(typeof(Mover), typeof(MPCapture))]
    public class MPNpc : NetworkBehaviour
    {
        // Shy and Bodyguard are single-player types kept for future use; values stay stable for the network.
        public enum NpcType : byte { Basic, Cupid, Shy, Bodyguard, DramaQueen, Celebrity, Hugger }
        public enum NpcBehavior : byte { Wander, Follow, Attack, Charge }

        static readonly List<MPNpc> all = new();
        public static IReadOnlyList<MPNpc> All => all;

        [SerializeField] NpcType type = NpcType.Basic;
        [Tooltip("Points this NPC is worth to the team that owns it.")]
        [SerializeField, Min(0)] int scoreValue = 1;

        [Header("Wander (Neutral)")]
        [SerializeField, Min(0f)] float wanderSpeed = 2f;
        [SerializeField] Vector2 directionChangeInterval = new Vector2(1.5f, 3.5f);
        [SerializeField, Range(0f, 1f)] float idleChance = 0.2f;
        [SerializeField, Min(0f)] float wanderTurnSpeed = 240f;

        [Header("Follow (Owned)")]
        [SerializeField, Min(0f)] protected float followSpeed = 6f;
        [SerializeField, Min(0f)] protected float followDistance = 1.8f;
        [SerializeField, Min(0f)] float followLag = 0.3f;
        [SerializeField, Min(0f)] float catchUpDistance = 6f;
        [SerializeField, Min(1f)] float catchUpMultiplier = 1.35f;
        [SerializeField, Min(0f)] float separationRadius = 1.1f;
        [SerializeField, Min(0f)] float separationStrength = 1.5f;

        [Header("Combat (Owned)")]
        [SerializeField, Min(0f)] protected float attackRange = 6f;
        [SerializeField, Min(0.05f)] float retargetInterval = 0.4f;
        [Tooltip("Distance multiplier for enemy-owned targets. Below 1 = prefers stealing over recruiting neutrals.")]
        [SerializeField, Min(0.1f)] float enemyTargetPriority = 0.7f;
        [SerializeField, Min(0f)] protected float fireCooldown = 0.9f;
        [SerializeField, Min(0f)] protected float projectileSpeed = 15f;
        [Tooltip("Seconds a shot flies before fading (range = speed x lifetime).")]
        [SerializeField, Min(0.05f)] float projectileLifetime = 1.33f;
        [Tooltip("Love per shot (players' default shots are 20).")]
        [SerializeField, Min(0f)] protected float shotPower = 10f;
        [SerializeField, Min(0f)] protected float spawnOffset = 0.5f;

        [Header("Obstacles")]
        [SerializeField] protected LayerMask obstacleMask;
        [SerializeField, Min(0f)] float lookAhead = 0.9f;
        [SerializeField, Min(0f)] float bodyRadius = 0.4f;
        [SerializeField, Min(0.1f)] float stuckTime = 0.8f;

        readonly NetworkVariable<NpcType> npcType = new(NpcType.Basic);
        readonly NetworkVariable<NpcBehavior> behavior = new(NpcBehavior.Wander);

        public Mover Mover { get; private set; }
        public MPCapture Capture { get; private set; }
        public int Team => Capture.Owner;
        public int PersonalOwner => Capture.PersonalOwner;
        public NpcType Type => npcType.Value;
        public NpcBehavior Behavior => behavior.Value;
        public int ScoreValue => scoreValue;

        protected MPCapture target;
        float nextRetargetTime;
        protected float nextFireTime;
        Vector2 wanderDirection;
        Vector2 wanderHeading;
        float nextWanderChange;
        float stuckTimer;
        Vector2 unstuckDirection;
        float unstuckUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();

        protected virtual void Awake()
        {
            Mover = GetComponent<Mover>();
            Capture = GetComponent<MPCapture>();
        }

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            Mover.enabled = IsServer;
            if (!IsServer) return;
            npcType.Value = type;
            Capture.OwnerChanged += OnTeamChanged;
            OnTeamChanged(MPTeams.Neutral, Team);
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            if (IsServer) Capture.OwnerChanged -= OnTeamChanged;
        }

        protected virtual void Update()
        {
            if (!IsServer) return;
            Mover.SpeedMultiplier = 1f;
            if (!MPMatch.InProgress)
            {
                Mover.SetMoveInput(Vector2.zero);
                return;
            }

            bool neutral = Team == MPTeams.Neutral;
            Vector2 desired = Time.time < unstuckUntil ? unstuckDirection
                : neutral ? Wander()
                : Follow(MPPlayer.ForSlot(PersonalOwner));
            Vector2 steered = Steering.AvoidObstacles(transform.position, desired, bodyRadius, lookAhead, obstacleMask);
            if (neutral && steered != desired) wanderDirection = wanderHeading = steered.normalized;
            Mover.SetMoveInput(steered);
            CheckStuck(steered);

            if (!neutral) UpdateCombat();
            NpcBehavior current = neutral ? NpcBehavior.Wander : CurrentBehavior();
            if (behavior.Value != current) behavior.Value = current;
        }

        protected virtual NpcBehavior CurrentBehavior() => target != null ? NpcBehavior.Attack : NpcBehavior.Follow;

        protected virtual void OnTeamChanged(int previous, int current)
        {
            Mover.MoveSpeed = current == MPTeams.Neutral ? wanderSpeed : followSpeed;
            target = null;
            if (current == MPTeams.Neutral) PickWanderDirection();
        }

        protected virtual Vector2 Wander()
        {
            if (Time.time >= nextWanderChange) PickWanderDirection();
            if (wanderDirection == Vector2.zero) return Vector2.zero;
            if (wanderHeading == Vector2.zero) wanderHeading = wanderDirection;
            wanderHeading = Vector3.RotateTowards(wanderHeading, wanderDirection, wanderTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
            return wanderHeading;
        }

        protected virtual Vector2 Follow(MPPlayer leader)
        {
            if (leader == null) return Wander();
            Vector2 lag = Vector2.ClampMagnitude(leader.Velocity * followLag, followDistance);
            Vector2 followPoint = leader.Position - lag;
            if (Vector2.Distance(transform.position, leader.Position) > catchUpDistance) Mover.SpeedMultiplier = catchUpMultiplier;
            return Vector2.ClampMagnitude(Arrive(followPoint, followDistance) + Separation(), 1f);
        }

        protected Vector2 Arrive(Vector2 point, float stopDistance)
        {
            Vector2 toPoint = point - (Vector2)transform.position;
            float distance = toPoint.magnitude;
            if (distance < 0.001f) return Vector2.zero;
            float easing = Mathf.Max(stopDistance, 0.5f);
            return toPoint / distance * Mathf.Clamp01((distance - stopDistance) / easing);
        }

        protected virtual void UpdateCombat()
        {
            RefreshTarget();
            if (target != null && Time.time >= nextFireTime) Fire(target.transform.position - transform.position);
        }

        protected void RefreshTarget()
        {
            if (Time.time < nextRetargetTime && IsValidTarget(target, attackRange * 1.2f)) return;
            target = FindTarget();
            nextRetargetTime = Time.time + retargetInterval;
        }

        protected virtual MPShot Shot => new(ProjectileStyle.Heart, projectileSpeed, shotPower, projectileLifetime);

        protected virtual void Fire(Vector2 direction)
        {
            if (MPMatch.Instance == null || direction.sqrMagnitude < 0.0001f) return;
            nextFireTime = Time.time + fireCooldown;
            Vector2 origin = (Vector2)transform.position + direction.normalized * spawnOffset;
            MPMatch.Instance.Fire(Team, PersonalOwner, origin, direction, Shot, false, NetworkObjectId);
        }

        // Lowest TargetScore among valid targets in range.
        MPCapture FindTarget()
        {
            MPCapture best = null;
            float bestScore = float.MaxValue;
            foreach (MPCapture other in MPCapture.All)
            {
                if (!IsValidTarget(other, attackRange)) continue;
                float score = TargetScore(other, Vector2.Distance(transform.position, other.transform.position));
                if (score < bestScore)
                {
                    bestScore = score;
                    best = other;
                }
            }
            return best;
        }

        // Lower is better; float.MaxValue skips the target. Basic NPCs only shoot NPCs, preferring enemy-owned ones.
        protected virtual float TargetScore(MPCapture other, float distance)
        {
            if (other.Player != null) return float.MaxValue;
            return other.Owner != MPTeams.Neutral ? distance * enemyTargetPriority : distance;
        }

        protected bool IsValidTarget(MPCapture other, float range)
        {
            if (other == null || other.gameObject == gameObject || !other.IsSpawned || !other.CanBeAffectedBy(Team) || other.Owner == Team) return false;
            if (other.Player != null && other.Player.IsConversionImmune) return false;
            Vector2 from = transform.position, to = other.transform.position;
            return Vector2.Distance(from, to) <= range && Steering.HasLineOfSight(from, to, obstacleMask);
        }

        protected Vector2 Separation()
        {
            Vector2 push = Vector2.zero;
            Vector2 position = transform.position;
            foreach (MPNpc other in all)
            {
                if (other == this || other.Team != Team) continue;
                Vector2 offset = position - (Vector2)other.transform.position;
                float distance = offset.magnitude;
                if (distance > 0.001f && distance < separationRadius)
                    push += offset / distance * (1f - distance / separationRadius);
            }
            return push * separationStrength;
        }

        protected void PickWanderDirection()
        {
            wanderDirection = Random.value < idleChance ? Vector2.zero : Random.insideUnitCircle.normalized;
            nextWanderChange = Time.time + Random.Range(directionChangeInterval.x, directionChangeInterval.y);
        }

        void CheckStuck(Vector2 move)
        {
            bool wantsToMove = move.sqrMagnitude > 0.25f && Time.time >= unstuckUntil;
            stuckTimer = wantsToMove && Mover.Velocity.magnitude < 0.3f ? stuckTimer + Time.deltaTime : 0f;
            if (stuckTimer < stuckTime) return;
            stuckTimer = 0f;
            unstuckDirection = Random.insideUnitCircle.normalized;
            unstuckUntil = Time.time + 0.4f;
            if (Team == MPTeams.Neutral) PickWanderDirection();
        }
    }
}
