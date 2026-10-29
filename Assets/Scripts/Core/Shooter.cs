using UnityEngine;

namespace LoveShot
{
    // Shared shooting for leaders and converted NPCs. Team can change at runtime.
    public class Shooter : MonoBehaviour
    {
        [SerializeField] Team team = Team.Pink;
        [SerializeField] Projectile projectilePrefab;
        [SerializeField, Min(0f)] float fireCooldown = 0.25f;
        [SerializeField, Min(0f)] float projectileSpeed = 12f;
        [Tooltip("Affection each hit moves an NPC toward this team (affection range is -100..100).")]
        [SerializeField, Min(0f)] float affectionPerHit = 20f;
        [Tooltip("Distance from the shooter's center where projectiles spawn.")]
        [SerializeField, Min(0f)] float spawnOffset = 0.6f;

        float nextFireTime;

        public Team Team { get => team; set => team = value; }
        // Set while the shooter must not fire (e.g. a lovestruck leader).
        public bool Locked { get; set; }
        // Scales fireCooldown at runtime (e.g. AI difficulty). 1 = as configured.
        public float CooldownMultiplier { get; set; } = 1f;
        public bool CanShoot => !Locked && Time.time >= nextFireTime;
        public float ProjectileSpeed => projectileSpeed;

        public bool TryShoot(Vector2 direction) => TryShootSpread(direction, 1, 0f);

        // Fires `count` projectiles fanned evenly across `spreadAngle` degrees, for one cooldown.
        public bool TryShootSpread(Vector2 direction, int count, float spreadAngle)
        {
            if (!CanShoot || projectilePrefab == null || team == Team.Neutral || count < 1 || direction.sqrMagnitude < 0.0001f)
                return false;

            direction.Normalize();
            nextFireTime = Time.time + fireCooldown * CooldownMultiplier;
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : Mathf.Lerp(-spreadAngle / 2f, spreadAngle / 2f, i / (count - 1f));
                Vector2 shotDirection = Quaternion.Euler(0f, 0f, angle) * direction;
                Vector2 spawnPos = (Vector2)transform.position + shotDirection * spawnOffset;
                Projectile projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
                projectile.Launch(team, shotDirection * projectileSpeed, affectionPerHit, gameObject);
            }
            return true;
        }
    }
}
