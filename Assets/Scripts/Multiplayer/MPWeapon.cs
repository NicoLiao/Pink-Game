using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Heart = the bow's arrow with the shooter's symbol as its head; Icon = just the shooter's symbol (normal NPC shots).
    public enum ProjectileStyle : byte { Heart, Rocket, Arrow, Icon }

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

    // One shot as it leaves the player: the stacked bow + modifiers, resolved for a given charge.
    public readonly struct MPFiring
    {
        public readonly MPShot Shot;
        public readonly int Pellets;
        public readonly float SpreadAngle;
        // Seconds to charge fully (the bow's, shortened by modifiers).
        public readonly float ChargeTime;

        public MPFiring(MPShot shot, int pellets, float spreadAngle, float chargeTime)
        {
            Shot = shot;
            Pellets = pellets;
            SpreadAngle = spreadAngle;
            ChargeTime = chargeTime;
        }
    }

    // A player's two abilities (Space, Shift), when each cooldown ability is ready again (server time) and which are spent.
    public struct MPAbilityState : Unity.Netcode.INetworkSerializable, System.IEquatable<MPAbilityState>
    {
        public MPAbility Ability0, Ability1;
        public double Ready0, Ready1;
        public byte UsedMask;

        public void NetworkSerialize<T>(Unity.Netcode.BufferSerializer<T> serializer) where T : Unity.Netcode.IReaderWriter
        {
            serializer.SerializeValue(ref Ability0);
            serializer.SerializeValue(ref Ability1);
            serializer.SerializeValue(ref Ready0);
            serializer.SerializeValue(ref Ready1);
            serializer.SerializeValue(ref UsedMask);
        }

        public bool Equals(MPAbilityState other) =>
            Ability0 == other.Ability0 && Ability1 == other.Ability1 && Ready0 == other.Ready0 && Ready1 == other.Ready1 && UsedMask == other.UsedMask;
    }

    // A modifier a player holds: its MPMatch weapon index and when it runs out (server time).
    public struct MPModifier : Unity.Netcode.INetworkSerializable, System.IEquatable<MPModifier>
    {
        public byte Index;
        public double Expire;

        public void NetworkSerialize<T>(Unity.Netcode.BufferSerializer<T> serializer) where T : Unity.Netcode.IReaderWriter
        {
            serializer.SerializeValue(ref Index);
            serializer.SerializeValue(ref Expire);
        }

        public bool Equals(MPModifier other) => Index == other.Index && Expire == other.Expire;
    }

    // Weapon data (multiplayer). MPMatch.weapons[0] is the Bow every player always holds: a charged shot, so
    // holding fire charges it and releasing shoots, harder and faster the longer it was held. Every other asset is
    // a timed modifier picked up in the arena: they stack with the bow and with each other, each on its own timer
    // (picking one up again restarts its timer). Tuning lives in the assets under Assets/Settings/Weapons.
    [CreateAssetMenu(menuName = "LoveShot/Weapon", fileName = "Weapon")]
    public class MPWeapon : ScriptableObject
    {
        [SerializeField] string displayName = "Love Bow";
        [SerializeField] Sprite icon;
        [Tooltip("Modifiers only: seconds a picked-up modifier lasts.")]
        [SerializeField, Min(1f)] float duration = 30f;

        [Header("Bow (index 0 only)")]
        [Tooltip("Seconds of holding fire for a full charge.")]
        [SerializeField, Min(0.05f)] float chargeTime = 1.2f;
        [Tooltip("Seconds after a shot before charging can start again.")]
        [SerializeField, Min(0f)] float recovery = 0.15f;
        [Tooltip("Releasing before this fraction of a full charge fires nothing.")]
        [SerializeField, Range(0f, 0.9f)] float minCharge = 0.3f;
        [Tooltip("Speed and love at zero charge, as a fraction of a full charge (they grow linearly up to it).")]
        [SerializeField, Range(0.05f, 1f)] float tapSpeed = 0.5f;
        [SerializeField, Range(0.05f, 1f)] float tapPower = 0.15f;
        [SerializeField] ProjectileStyle style = ProjectileStyle.Heart;
        [SerializeField, Min(0.5f)] float projectileSpeed = 18f;
        [Tooltip("Love per projectile at full charge (a neutral NPC needs 100, a player 250).")]
        [SerializeField, Min(0f)] float power = 60f;
        [SerializeField, Min(0.05f)] float lifetime = 1.4f;
        [SerializeField, Min(0.1f)] float projectileScale = 1f;

        [Header("Modifier (multiplies / adds to the bow; 1 / 0 = no change)")]
        [SerializeField, Min(0.05f)] float chargeTimeMultiplier = 1f;
        [SerializeField, Min(0.1f)] float speedMultiplier = 1f;
        [SerializeField, Min(0.05f)] float powerMultiplier = 1f;
        [SerializeField, Min(0.1f)] float lifetimeMultiplier = 1f;
        [SerializeField, Min(0.1f)] float scaleMultiplier = 1f;
        [Tooltip("Extra projectiles per shot, spread evenly across spreadAngle.")]
        [SerializeField, Min(0)] int extraPellets;
        [SerializeField, Range(0f, 120f)] float spreadAngle;
        [Tooltip("Extra ricochets off walls and obstacles.")]
        [SerializeField, Min(0)] int extraBounces;
        [Tooltip("Above 0 the shot explodes (the largest radius of the stack counts).")]
        [SerializeField, Min(0f)] float explosionRadius;
        [Tooltip("Fraction of the center love that a target at the very edge of the explosion gets.")]
        [SerializeField, Range(0f, 1f)] float explosionEdgePower = 0.35f;
        [Tooltip("Draw the projectile in another style (the rocket) while this modifier is active.")]
        [SerializeField] bool changeStyle;
        [SerializeField] ProjectileStyle styleOverride = ProjectileStyle.Rocket;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public float Duration => duration;
        public float Recovery => recovery;
        public float MinCharge => minCharge;

        // The bow's shot at `charge` (0..1) with the given modifiers stacked on top.
        public static MPFiring Resolve(MPWeapon bow, System.Collections.Generic.IReadOnlyList<MPWeapon> modifiers, float charge)
        {
            charge = Mathf.Clamp01(charge);
            float speed = bow.projectileSpeed * Mathf.Lerp(bow.tapSpeed, 1f, charge);
            float power = bow.power * Mathf.Lerp(bow.tapPower, 1f, charge);
            float life = bow.lifetime, scale = bow.projectileScale, time = bow.chargeTime;
            ProjectileStyle shotStyle = bow.style;
            int pellets = 1, bounces = 0;
            float spread = 0f, radius = 0f, edge = bow.explosionEdgePower;
            foreach (MPWeapon modifier in modifiers)
            {
                if (modifier == null) continue;
                time *= modifier.chargeTimeMultiplier;
                speed *= modifier.speedMultiplier;
                power *= modifier.powerMultiplier;
                life *= modifier.lifetimeMultiplier;
                scale *= modifier.scaleMultiplier;
                pellets += modifier.extraPellets;
                spread = Mathf.Max(spread, modifier.spreadAngle);
                bounces += modifier.extraBounces;
                if (modifier.explosionRadius > radius)
                {
                    radius = modifier.explosionRadius;
                    edge = modifier.explosionEdgePower;
                }
                if (modifier.changeStyle) shotStyle = modifier.styleOverride;
            }
            return new MPFiring(new MPShot(shotStyle, speed, power, life, scale, bounces, radius, edge), pellets, spread, time);
        }
    }
}
