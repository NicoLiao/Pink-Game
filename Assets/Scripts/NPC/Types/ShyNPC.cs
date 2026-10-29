using UnityEngine;

namespace LoveShot
{
    // Shy: runs away from leaders while Neutral, and hangs back behind the group once recruited.
    // (Its Affection sensitivity is lowered on the prefab, so it also takes more hits to convert.)
    public class ShyNPC : NPCController
    {
        [Header("Shy")]
        [SerializeField, Min(0f)] float fleeRadius = 3.5f;
        [SerializeField, Min(1f)] float fleeSpeedMultiplier = 1.6f;

        protected override Vector2 Wander()
        {
            Vector2 flee = Vector2.zero;
            Vector2 position = transform.position;
            foreach (Team team in new[] { Team.Pink, Team.Blue })
            {
                TeamLeader leader = TeamLeader.For(team);
                if (leader == null) continue;
                Vector2 away = position - leader.Position;
                float distance = away.magnitude;
                if (distance > 0.001f && distance < fleeRadius) flee += away / distance * (1f - distance / fleeRadius);
            }

            if (flee == Vector2.zero) return base.Wander();
            Mover.SpeedMultiplier = fleeSpeedMultiplier;
            return flee.normalized;
        }
    }
}
