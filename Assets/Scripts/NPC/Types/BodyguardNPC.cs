using UnityEngine;

namespace LoveShot
{
    // Bodyguard: once recruited, stands between its leader and the enemy leader, soaking up the
    // hearts that would lovestruck its leader. (Low Affection sensitivity on the prefab makes it tanky.)
    public class BodyguardNPC : NPCController
    {
        [Header("Bodyguard")]
        [Tooltip("How far in front of its leader (toward the enemy leader) the bodyguard stands.")]
        [SerializeField, Min(0f)] float guardDistance = 1.5f;
        [Tooltip("How wide a berth to give its own leader when walking around it to the guard spot.")]
        [SerializeField, Min(0f)] float leaderClearance = 1.2f;

        protected override Vector2 Follow(TeamLeader leader)
        {
            TeamLeader enemy = TeamLeader.For(TeamUtil.Opponent(Team));
            if (leader == null || enemy == null) return base.Follow(leader);

            Vector2 toEnemy = enemy.Position - leader.Position;
            if (toEnemy.sqrMagnitude < 0.0001f) return base.Follow(leader);
            Vector2 guardPoint = leader.Position + toEnemy.normalized * guardDistance;
            Vector2 move = Arrive(guardPoint, 0.2f);
            return Vector2.ClampMagnitude(move + AroundLeader(leader, guardPoint, move.magnitude) + Separation() * 0.5f, 1f);
        }

        // When the leader stands between us and the guard spot, sidestep around it instead of shoving it.
        Vector2 AroundLeader(TeamLeader leader, Vector2 guardPoint, float strength)
        {
            Vector2 position = transform.position;
            Vector2 toPoint = guardPoint - position;
            float pathLength = toPoint.magnitude;
            if (pathLength < 0.001f) return Vector2.zero;

            Vector2 path = toPoint / pathLength;
            float along = Vector2.Dot(leader.Position - position, path);
            if (along <= 0f || along >= pathLength) return Vector2.zero;

            Vector2 closest = position + path * along;
            Vector2 away = closest - leader.Position;
            if (away.magnitude >= leaderClearance) return Vector2.zero;
            Vector2 side = away.sqrMagnitude > 0.0001f ? away.normalized : new Vector2(-path.y, path.x);
            return side * 1.5f * Mathf.Max(strength, 0.5f);
        }
    }
}
