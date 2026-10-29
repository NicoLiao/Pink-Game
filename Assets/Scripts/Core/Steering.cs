using UnityEngine;

namespace LoveShot
{
    // Shared movement helpers for anything AI-driven (NPCs and the mini-boss).
    public static class Steering
    {
        static readonly float[] avoidAngles = { 35f, -35f, 70f, -70f, 110f, -110f };

        // If the way ahead is blocked, tries angles to either side, then backs off.
        public static Vector2 AvoidObstacles(Vector2 position, Vector2 desired, float bodyRadius, float lookAhead, LayerMask obstacleMask)
        {
            if (desired.sqrMagnitude < 0.0001f || !IsBlocked(position, desired, bodyRadius, lookAhead, obstacleMask))
                return desired;

            foreach (float angle in avoidAngles)
            {
                Vector2 candidate = Quaternion.Euler(0f, 0f, angle) * desired;
                if (!IsBlocked(position, candidate, bodyRadius, lookAhead, obstacleMask)) return candidate;
            }
            return -desired;
        }

        public static bool IsBlocked(Vector2 position, Vector2 direction, float bodyRadius, float lookAhead, LayerMask obstacleMask) =>
            Physics2D.CircleCast(position, bodyRadius, direction.normalized, lookAhead, obstacleMask);

        public static bool HasLineOfSight(Vector2 from, Vector2 to, LayerMask obstacleMask) =>
            !Physics2D.Linecast(from, to, obstacleMask);
    }
}
