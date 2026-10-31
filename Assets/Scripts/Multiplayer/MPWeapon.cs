using UnityEngine;

namespace LoveShot.Multiplayer
{
    public enum ProjectileStyle : byte { Heart, Rocket, Arrow }

    // Everything a single projectile needs to fly and hit. Sent to clients so their visual copies match.
    public readonly struct MPShot
    {
        public readonly ProjectileStyle Style;
        public readonly float Speed, Power, Lifetime, Scale;
        public readonly int Bounces;
        // Explosive shots (ExplosionRadius > 0) burst on impact or at the end of their lifetime, giving
        // Power at the center down to Power * ExplosionEdgePower at the edge.
        public readonly float ExplosionRadius, ExplosionEdgePower;

        public MPShot(ProjectileStyle style, float speed, float power, float lifetime, float scale = 1f, int bounces = 0,
            float explosionRadius = 0f, float explosionEdgePower = 0f)
        {
            Style = style;
            Speed = speed;
            Power = power;
            Lifetime = lifetime;
            Scale = scale;
            Bounces = bounces;
            ExplosionRadius = explosionRadius;
            ExplosionEdgePower = explosionEdgePower;
        }

        public bool Explosive => ExplosionRadius > 0f;
    }

    // A player weapon (multiplayer). Tuning lives in the assets under Assets/Settings/Weapons, which the
    // builder creates only if they're missing, so edit them directly. MPMatch.weapons[0] is the Default Gun.
    [CreateAssetMenu(menuName = "LoveShot/Weapon", fileName = "Weapon")]
    public class MPWeapon : ScriptableObject
    {
        [SerializeField] string displayName = "Love Gun";
        [SerializeField] Sprite icon;
        [Tooltip("Seconds a picked-up weapon lasts before you're back to the Default Gun.")]
        [SerializeField, Min(1f)] float duration = 30f;

        [Header("Firing")]
        [SerializeField, Min(0.02f)] float fireCooldown = 0.25f;
        [Tooltip("Projectiles per shot, spread evenly across spreadAngle.")]
        [SerializeField, Min(1)] int pellets = 1;
        [SerializeField, Range(0f, 120f)] float spreadAngle;

        [Header("Projectile")]
        [SerializeField] ProjectileStyle style = ProjectileStyle.Heart;
        [SerializeField, Min(0.5f)] float projectileSpeed = 12f;
        [Tooltip("Love per projectile (a neutral NPC needs 100, a player 250).")]
        [SerializeField, Min(0f)] float power = 20f;
        [SerializeField, Min(0.05f)] float lifetime = 2f;
        [SerializeField, Min(0.1f)] float projectileScale = 1f;
        [Tooltip("Times a projectile ricochets off walls and obstacles before it's gone.")]
        [SerializeField, Min(0)] int bounces;

        [Header("Explosion (0 radius = none)")]
        [SerializeField, Min(0f)] float explosionRadius;
        [Tooltip("Fraction of the center love that a target at the very edge of the explosion gets.")]
        [SerializeField, Range(0f, 1f)] float explosionEdgePower = 0.35f;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public float Duration => duration;
        public float FireCooldown => fireCooldown;
        public int Pellets => pellets;
        public float SpreadAngle => spreadAngle;
        public MPShot Shot => new(style, projectileSpeed, power, lifetime, projectileScale, bounces, explosionRadius, explosionEdgePower);
    }
}
