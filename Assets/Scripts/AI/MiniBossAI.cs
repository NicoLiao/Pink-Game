using System.Collections.Generic;
using UnityEngine;

namespace LoveShot
{
    // The AI opponent: a second "player" that drives the same Mover, Shooter and Lovestruck as the human.
    // States: Recruit (target a neutral NPC), Steal (target an enemy-owned NPC),
    // Evade (sidestep an incoming leader shot), Reposition (move to a fresh spot).
    // How well it plays (aim, reactions, targeting, dodging) comes from an AIDifficulty profile.
    [RequireComponent(typeof(Mover), typeof(Shooter))]
    public class MiniBossAI : MonoBehaviour
    {
        public enum State { Recruit, Steal, Evade, Reposition }

        [SerializeField] Team team = Team.Blue;
        [Tooltip("Used when no difficulty was chosen in the main menu (e.g. playing the arena scene directly).")]
        [SerializeField] AIDifficulty defaultDifficulty;

        [Header("Reposition / Unstuck")]
        [Tooltip("Half-size of the playable area, centered on the world origin.")]
        [SerializeField] Vector2 arenaHalfExtents = new Vector2(12f, 6f);
        [SerializeField, Min(0.1f)] float repositionDuration = 1.2f;
        [Tooltip("Wanting to move but slower than stuckSpeed for this long triggers a Reposition.")]
        [SerializeField, Min(0.1f)] float stuckTime = 0.6f;
        [SerializeField, Min(0f)] float stuckSpeed = 0.5f;

        [Header("Obstacles")]
        [SerializeField] LayerMask obstacleMask;
        [SerializeField, Min(0f)] float lookAhead = 1f;
        [SerializeField, Min(0f)] float bodyRadius = 0.5f;

        Mover mover;
        Shooter shooter;
        Lovestruck lovestruck;
        AimIndicator aimIndicator;

        readonly HashSet<Projectile> judgedShots = new();
        float nextDecisionTime;
        float strafeSign = 1f;
        float nextStrafeSwitch;
        Vector2 waypoint;
        float repositionEndTime;
        float stuckTimer;
        Vector2 evadeDirection;
        float evadeEndTime;
        bool harassing;
        State resumeState = State.Reposition;
        float targetAcquiredTime;
        float jitterSeed;

        public State CurrentState { get; private set; } = State.Reposition;
        public NPCController Target { get; private set; }
        public AIDifficulty Difficulty { get; private set; }

        void Awake()
        {
            mover = GetComponent<Mover>();
            shooter = GetComponent<Shooter>();
            lovestruck = GetComponent<Lovestruck>();
            aimIndicator = GetComponent<AimIndicator>();
            shooter.Team = team;
            jitterSeed = Random.value * 100f;
            // Never run without a profile: a fresh instance holds the default (Hard) values.
            ApplyDifficulty(GameSettings.SelectedDifficulty != null ? GameSettings.SelectedDifficulty
                : defaultDifficulty != null ? defaultDifficulty
                : ScriptableObject.CreateInstance<AIDifficulty>());
            EnterReposition();
        }

        public void ApplyDifficulty(AIDifficulty difficulty)
        {
            if (difficulty == null) return;
            Difficulty = difficulty;
            shooter.CooldownMultiplier = difficulty.FireCooldownMultiplier;
        }

        void Update()
        {
            if (!MatchManager.InProgress)
            {
                mover.SetMoveInput(Vector2.zero);
                return;
            }

            if (CurrentState == State.Evade && Time.time >= evadeEndTime) CurrentState = resumeState;
            if (CurrentState != State.Evade) CheckThreats();

            bool targetLost = Target != null && (Target.Team == team || !Target.isActiveAndEnabled);
            if (CurrentState != State.Evade && (Time.time >= nextDecisionTime || targetLost)) Decide();

            Vector2 move = CurrentState switch
            {
                State.Evade => evadeDirection,
                State.Reposition => MoveToWaypoint(),
                _ => MoveAroundTarget(),
            };
            if (CurrentState != State.Evade) move = AddJitter(move);
            move = Steering.AvoidObstacles(transform.position, move, bodyRadius, lookAhead, obstacleMask);
            mover.SetMoveInput(move);
            CheckStuck(move);
            Shoot();
        }

        void Decide()
        {
            nextDecisionTime = Time.time + Difficulty.DecisionInterval;
            harassing = Random.value < Difficulty.HarassChance;
            if (CurrentState == State.Reposition && Time.time < repositionEndTime) return;

            // Sloppier difficulties sometimes wander off instead of pursuing anything.
            if (Difficulty.WanderChance > 0f && Random.value < Difficulty.WanderChance)
            {
                EnterReposition();
                return;
            }

            NPCController previous = Target;
            Target = PickTarget();
            if (Target == null)
            {
                if (CurrentState != State.Reposition || Time.time >= repositionEndTime) EnterReposition();
                return;
            }
            if (Target != previous) targetAcquiredTime = Time.time;
            CurrentState = Target.Team == Team.Neutral ? State.Recruit : State.Steal;
        }

        NPCController PickTarget()
        {
            NPCController best = null;
            float bestScore = float.MaxValue;
            foreach (NPCController npc in NPCController.All)
            {
                if (npc.Team == team) continue;
                float score = Score(npc);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = npc;
                }
            }

            // Stick with the current target unless the new one is clearly better.
            if (Target != null && Target != best && Target.Team != team && Target.isActiveAndEnabled
                && Score(Target) <= bestScore + Difficulty.SwitchMargin)
                return Target;
            return best;
        }

        // Lower is better: distance, adjusted for ownership, progress, the opponent's progress and visibility.
        float Score(NPCController npc)
        {
            Vector2 from = transform.position, to = npc.transform.position;
            float score = Vector2.Distance(from, to);
            if (npc.Team != Team.Neutral) score *= StealFactor();

            float leaning = npc.Affection.Value * TeamUtil.AffectionSign(team) / Affection.Max;
            score -= Difficulty.ProgressBonus * Mathf.Clamp01(leaning);
            if (npc.Team == Team.Neutral) score -= Difficulty.DenyBonus * Mathf.Clamp01(-leaning);

            if (!Steering.HasLineOfSight(from, to, obstacleMask)) score += Difficulty.NoLineOfSightPenalty;
            return score;
        }

        float StealFactor()
        {
            MatchManager match = MatchManager.Instance;
            bool losing = match != null && match.CountOf(team) < match.CountOf(TeamUtil.Opponent(team));
            return losing ? Difficulty.StealDistanceFactor * Difficulty.LosingStealMultiplier : Difficulty.StealDistanceFactor;
        }

        // Looks for enemy leader shots on a collision course and, sometimes, sidesteps them.
        void CheckThreats()
        {
            judgedShots.RemoveWhere(shot => shot == null);
            if (lovestruck != null && !lovestruck.CanBeStruck) return; // immune: no need to dodge

            Vector2 position = transform.position;
            foreach (Projectile shot in Projectile.All)
            {
                if (shot.Team == team || !shot.FromLeader || judgedShots.Contains(shot)) continue;

                Vector2 velocity = shot.Velocity;
                if (velocity.sqrMagnitude < 0.01f) continue;
                Vector2 toMe = position - (Vector2)shot.transform.position;
                float timeToClosest = Vector2.Dot(toMe, velocity) / velocity.sqrMagnitude;
                if (timeToClosest < 0f || timeToClosest > Difficulty.ThreatHorizon) continue;
                Vector2 missOffset = toMe - velocity * timeToClosest;
                if (missOffset.magnitude > Difficulty.DangerRadius) continue;

                // Each shot gets one roll, so dodging stays occasional rather than perfect.
                judgedShots.Add(shot);
                if (Random.value > Difficulty.DodgeChance) continue;

                Vector2 away = missOffset.sqrMagnitude > 0.0001f ? missOffset.normalized : new Vector2(-velocity.y, velocity.x).normalized;
                if (Steering.IsBlocked(position, away, bodyRadius, lookAhead, obstacleMask)) away = -away;
                StartEvade(away, timeToClosest);
                return;
            }
        }

        void StartEvade(Vector2 direction, float timeToImpact)
        {
            if (CurrentState != State.Evade) resumeState = CurrentState;
            CurrentState = State.Evade;
            evadeDirection = direction;
            evadeEndTime = Time.time + Difficulty.EvadeDuration;
            if (timeToImpact < Difficulty.DashDodgeTime) mover.TryDash(direction);
        }

        // Smoothly wandering wobble on the movement direction, scaled by the difficulty's MoveJitter.
        Vector2 AddJitter(Vector2 move)
        {
            if (Difficulty.MoveJitter <= 0f || move.sqrMagnitude < 0.0001f) return move;
            float t = Time.time * 0.8f;
            var wobble = new Vector2(Mathf.PerlinNoise(t, jitterSeed) * 2f - 1f, Mathf.PerlinNoise(jitterSeed, t) * 2f - 1f);
            return (move.normalized + wobble * (Difficulty.MoveJitter * 2f)).normalized * move.magnitude;
        }

        // Stay inside a distance band around the target, strafing sideways while in it.
        Vector2 MoveAroundTarget()
        {
            if (Target == null) return Vector2.zero;

            Vector2 toTarget = Target.transform.position - transform.position;
            float distance = toTarget.magnitude;
            if (distance < 0.001f) return Vector2.zero;
            Vector2 direction = toTarget / distance;

            if (!Steering.HasLineOfSight(transform.position, Target.transform.position, obstacleMask))
                return direction;

            if (Time.time >= nextStrafeSwitch) FlipStrafe();
            float preferredMinDistance = Difficulty.PreferredMinDistance, preferredMaxDistance = Difficulty.PreferredMaxDistance;
            Vector2 strafe = new Vector2(-direction.y, direction.x) * strafeSign;
            if (Steering.IsBlocked(transform.position, strafe, bodyRadius, lookAhead, obstacleMask))
            {
                FlipStrafe();
                strafe = -strafe;
            }

            Vector2 move;
            if (distance > preferredMaxDistance) move = direction + strafe * 0.3f;
            else if (distance < preferredMinDistance) move = -direction + strafe * 0.5f;
            else move = strafe * 0.8f;
            return move.normalized;
        }

        Vector2 MoveToWaypoint()
        {
            Vector2 toWaypoint = waypoint - (Vector2)transform.position;
            if (toWaypoint.magnitude < 0.5f) repositionEndTime = Time.time;
            return toWaypoint.normalized;
        }

        void EnterReposition()
        {
            CurrentState = State.Reposition;
            Target = null;
            repositionEndTime = Time.time + repositionDuration;
            waypoint = transform.position;
            // Use the live arena size when it has grown to fit the screen (same 1-unit wall clearance).
            Vector2 extents = ArenaBounds.Current != null
                ? arenaHalfExtents + ArenaBounds.Current.HalfExtents - ArenaBounds.Current.MinHalfExtents
                : arenaHalfExtents;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                Vector2 candidate = new Vector2(
                    Random.Range(-extents.x, extents.x),
                    Random.Range(-extents.y, extents.y));
                if (Physics2D.OverlapCircle(candidate, bodyRadius + 0.3f, obstacleMask) != null) continue;
                waypoint = candidate;
                break;
            }
        }

        void CheckStuck(Vector2 move)
        {
            bool wantsToMove = move.sqrMagnitude > 0.25f && CurrentState != State.Evade;
            stuckTimer = wantsToMove && mover.Velocity.magnitude < stuckSpeed ? stuckTimer + Time.deltaTime : 0f;
            if (stuckTimer < stuckTime) return;
            stuckTimer = 0f;
            EnterReposition();
        }

        void Shoot()
        {
            Vector2 from = transform.position;
            Transform aimTarget = null;
            Vector2 aimVelocity = Vector2.zero;

            TeamLeader enemy = TeamLeader.For(TeamUtil.Opponent(team));
            bool reacting = false;
            if (harassing && enemy != null && CanHarass(enemy, from))
            {
                aimTarget = enemy.transform;
                aimVelocity = enemy.Velocity;
            }
            else if (Target != null)
            {
                aimTarget = Target.transform;
                aimVelocity = Target.Mover.Velocity;
                reacting = Time.time - targetAcquiredTime < Difficulty.TargetReactionTime;
            }
            if (aimTarget == null) return;

            Vector2 aimPoint = aimTarget.position;
            float distance = Vector2.Distance(from, aimPoint);
            if (Difficulty.LeadTargets && shooter.ProjectileSpeed > 0f) aimPoint += aimVelocity * (distance / shooter.ProjectileSpeed);

            Vector2 aim = aimPoint - from;
            if (aimIndicator != null) aimIndicator.Point(aim);
            if (reacting || distance > Difficulty.ShootRange || !Steering.HasLineOfSight(from, aimTarget.position, obstacleMask)) return;

            aim = Quaternion.Euler(0f, 0f, Random.Range(-Difficulty.AimSpread, Difficulty.AimSpread)) * aim;
            shooter.TryShoot(aim);
        }

        bool CanHarass(TeamLeader enemy, Vector2 from)
        {
            if (Difficulty.HarassRange <= 0f || Vector2.Distance(from, enemy.Position) > Difficulty.HarassRange) return false;
            if (enemy.TryGetComponent(out Lovestruck enemyLovestruck) && !enemyLovestruck.CanBeStruck) return false;
            return Steering.HasLineOfSight(from, enemy.Position, obstacleMask);
        }

        void FlipStrafe()
        {
            strafeSign = -strafeSign;
            nextStrafeSwitch = Time.time + Random.Range(Difficulty.StrafeSwitchInterval.x, Difficulty.StrafeSwitchInterval.y);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = TeamUtil.ColorOf(team);
            if (Target != null) Gizmos.DrawLine(transform.position, Target.transform.position);
            if (CurrentState == State.Reposition) Gizmos.DrawWireSphere(waypoint, 0.4f);
        }
    }
}
