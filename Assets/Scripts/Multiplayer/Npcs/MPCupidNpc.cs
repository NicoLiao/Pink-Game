using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Cupid: ranged support. Hangs back behind its owner and shoots fast love arrows from long range at
    // neutral and enemy NPCs, and now and then at an opposing player (players only win when they're
    // clearly the closest target). Weaker per second than it looks, so it helps without dominating.
    public class MPCupidNpc : MPNpc
    {
        [Header("Cupid")]
        [Tooltip("Distance multiplier for opposing players as targets (higher = picks them less often).")]
        [SerializeField, Min(1f)] float playerTargetPenalty = 1.8f;
        [SerializeField, Min(0.1f)] float arrowScale = 1.6f;

        protected override MPShot Shot =>
            new(ProjectileStyle.Arrow, projectileSpeed, shotPower, attackRange / Mathf.Max(projectileSpeed, 0.1f) + 0.25f, arrowScale);

        protected override float TargetScore(MPCapture other, float distance)
        {
            if (other.Player != null) return distance * playerTargetPenalty;
            return base.TargetScore(other, distance);
        }
    }
}
