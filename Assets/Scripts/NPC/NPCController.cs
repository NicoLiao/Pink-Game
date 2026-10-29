using System.Collections.Generic;
using UnityEngine;

namespace LoveShot
{
    // Basic NPC brain: Neutral NPCs wander, owned NPCs follow their team's leader and shoot NPCs not on their team.
    // Special types subclass this and override Wander / Follow / FindTarget / Fire / OnTeamChanged.
    [RequireComponent(typeof(Mover), typeof(Affection))]
    public class NPCController : MonoBehaviour
    {
        static readonly List<NPCController> all = new();
        public static IReadOnlyList<NPCController> All => all;

        [Header("Wander (Neutral)")]
        [SerializeField, Min(0f)] float wanderSpeed = 2f;
        [Tooltip("Random time (seconds) between direction changes.")]
        [SerializeField] Vector2 directionChangeInterval = new Vector2(1.5f, 3.5f);
        [SerializeField, Range(0f, 1f)] float idleChance = 0.2f;
        [Tooltip("Degrees per second the wander heading turns toward a new direction (smooth curves instead of snaps).")]
        [SerializeField, Min(0f)] float wanderTurnSpeed = 240f;

        [Header("Follow (Owned)")]
        [SerializeField, Min(0f)] float followSpeed = 6f;
        [Tooltip("NPCs stop approaching once this close to their follow point.")]
        [SerializeField, Min(0f)] protected float followDistance = 1.8f;
        [Tooltip("How far behind a moving leader (in seconds of its velocity) followers aim, so they trail instead of blocking.")]
        [SerializeField, Min(0f)] float followLag = 0.3f;
        [Tooltip("Beyond this distance followers speed up by catchUpMultiplier.")]
        [SerializeField, Min(0f)] float catchUpDistance = 6f;
        [SerializeField, Min(1f)] float catchUpMultiplier = 1.35f;
        [SerializeField, Min(0f)] float separationRadius = 1.1f;
        [SerializeField, Min(0f)] float separationStrength = 1.5f;

        [Header("Combat (Owned)")]
        [Tooltip("Owned NPCs shoot NPCs of other teams within this range (needs a Shooter).")]
        [SerializeField, Min(0f)] float attackRange = 6f;
        [SerializeField, Min(0.05f)] float retargetInterval = 0.4f;
        [Tooltip("Distance multiplier for enemy-owned targets. Below 1 = allies prefer attacking enemies over recruiting neutrals.")]
        [SerializeField, Min(0.1f)] float enemyTargetPriority = 0.7f;

        [Header("Obstacles")]
        [SerializeField] LayerMask obstacleMask;
        [SerializeField, Min(0f)] float lookAhead = 0.9f;
        [SerializeField, Min(0f)] float bodyRadius = 0.4f;
        [Tooltip("Wanting to move but barely moving for this long triggers a short random sidestep.")]
        [SerializeField, Min(0.1f)] float stuckTime = 0.8f;

        public Mover Mover { get; private set; }
        public Affection Affection { get; private set; }
        public Team Team => Affection.Owner;
        protected Shooter Shooter { get; private set; }

        NPCController target;
        float nextRetargetTime;
        Vector2 wanderDirection;
        Vector2 wanderHeading;
        float nextWanderChange;
        float stuckTimer;
        Vector2 unstuckDirection;
        float unstuckUntil;

        protected virtual void Awake()
        {
            Mover = GetComponent<Mover>();
            Affection = GetComponent<Affection>();
            Shooter = GetComponent<Shooter>();
        }

        protected virtual void OnEnable()
        {
            all.Add(this);
            Affection.TeamChanged += OnTeamChanged;
            OnTeamChanged(Team.Neutral, Team);
        }

        protected virtual void OnDisable()
        {
            all.Remove(this);
            Affection.TeamChanged -= OnTeamChanged;
        }

        protected virtual void Update()
        {
            Mover.SpeedMultiplier = 1f;
            if (!MatchManager.InProgress)
            {
                Mover.SetMoveInput(Vector2.zero);
                return;
            }

            Vector2 desired = Time.time < unstuckUntil ? unstuckDirection
                : Team == Team.Neutral ? Wander()
                : Follow(TeamLeader.For(Team));
            Vector2 steered = Steering.AvoidObstacles(transform.position, desired, bodyRadius, lookAhead, obstacleMask);
            if (Team == Team.Neutral && steered != desired) wanderDirection = wanderHeading = steered.normalized;
            Mover.SetMoveInput(steered);
            CheckStuck(steered);

            if (Team != Team.Neutral) UpdateCombat();
        }

        protected virtual void OnTeamChanged(Team previous, Team current)
        {
            Mover.MoveSpeed = current == Team.Neutral ? wanderSpeed : followSpeed;
            if (Shooter != null) Shooter.Team = current;
            target = null;
            if (current == Team.Neutral) PickWanderDirection();
        }

        protected virtual Vector2 Wander()
        {
            if (Time.time >= nextWanderChange) PickWanderDirection();
            if (wanderDirection == Vector2.zero) return Vector2.zero;
            if (wanderHeading == Vector2.zero) wanderHeading = wanderDirection;
            wanderHeading = Vector3.RotateTowards(wanderHeading, wanderDirection, wanderTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
            return wanderHeading;
        }

        protected virtual Vector2 Follow(TeamLeader leader)
        {
            if (leader == null) return Wander();

            // Aim a little behind where the leader is heading.
            Vector2 lag = Vector2.ClampMagnitude(leader.Velocity * followLag, followDistance);
            Vector2 followPoint = leader.Position - lag;
            if (Vector2.Distance(transform.position, leader.Position) > catchUpDistance)
                Mover.SpeedMultiplier = catchUpMultiplier;
            return Vector2.ClampMagnitude(Arrive(followPoint, followDistance) + Separation(), 1f);
        }

        // Full speed when far from point, easing to a stop at stopDistance.
        protected Vector2 Arrive(Vector2 point, float stopDistance)
        {
            Vector2 toPoint = point - (Vector2)transform.position;
            float distance = toPoint.magnitude;
            if (distance < 0.001f) return Vector2.zero;
            float easing = Mathf.Max(stopDistance, 0.5f);
            return toPoint / distance * Mathf.Clamp01((distance - stopDistance) / easing);
        }

        void UpdateCombat()
        {
            if (Shooter == null) return;
            if (Time.time >= nextRetargetTime || !IsValidTarget(target, attackRange * 1.2f))
            {
                target = FindTarget();
                nextRetargetTime = Time.time + retargetInterval;
            }
            if (target != null) Fire(target.transform.position - transform.position);
        }

        protected virtual void Fire(Vector2 direction) => Shooter.TryShoot(direction);

        // Nearest visible NPC that isn't on our team; enemy-owned NPCs count as closer than neutrals.
        protected virtual NPCController FindTarget()
        {
            NPCController best = null;
            float bestScore = float.MaxValue;
            foreach (NPCController other in all)
            {
                if (!IsValidTarget(other, attackRange)) continue;
                float score = Vector2.Distance(transform.position, other.transform.position);
                if (other.Team != Team.Neutral) score *= enemyTargetPriority;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = other;
                }
            }
            return best;
        }

        bool IsValidTarget(NPCController other, float range)
        {
            if (other == null || other == this || !other.isActiveAndEnabled || other.Team == Team) return false;
            Vector2 from = transform.position, to = other.transform.position;
            return Vector2.Distance(from, to) <= range && Steering.HasLineOfSight(from, to, obstacleMask);
        }

        // Pushes away from nearby teammates so a following group spreads out.
        protected Vector2 Separation()
        {
            Vector2 push = Vector2.zero;
            Vector2 position = transform.position;
            foreach (NPCController other in all)
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
            if (Team == Team.Neutral) PickWanderDirection();
        }
    }
}
