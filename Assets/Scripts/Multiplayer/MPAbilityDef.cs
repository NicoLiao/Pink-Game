using System.Collections.Generic;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // One player ability (Space / Shift slot): its name, icon, cooldown and numbers. The assets live in
    // Assets/Settings/Resources/Abilities (one per MPAbility); MPAbilities finds them. The rules are in MPPlayer.
    [CreateAssetMenu(menuName = "LoveShot/Ability", fileName = "Ability")]
    public class MPAbilityDef : ScriptableObject
    {
        [Tooltip("Which behavior this asset configures (one asset per kind).")]
        [SerializeField] MPAbility kind;
        [SerializeField] string displayName = "Ability";
        [SerializeField] Sprite icon;
        [Tooltip("Seconds before it can be used again.")]
        [SerializeField, Min(0f)] float cooldown = 20f;
        [Tooltip("Once the first use has gone through, it can't be used again this match.")]
        [SerializeField] bool oncePerMatch;
        [Tooltip("Heal: how far teammates are healed. Others: the size of the ring shown when used.")]
        [SerializeField, Min(0.5f)] float radius = 2.5f;
        [Tooltip("Powerup: how far ahead of your aim the pickup drops.")]
        [SerializeField, Min(0f)] float distance = 2f;
        [Tooltip("Duplicate: the size (1 = normal) of originals and copies.")]
        [SerializeField, Range(0.3f, 1f)] float size = 0.75f;
        [Tooltip("Duplicate: love per hit that originals and copies take, as a multiple (2 = half the health).")]
        [SerializeField, Min(1f)] float healthMultiplier = 2f;
        [Tooltip("Duplicate: how far from its original a copy appears.")]
        [SerializeField, Min(0f)] float spread = 1f;

        public MPAbility Kind => kind;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public float Cooldown => cooldown;
        public bool OncePerMatch => oncePerMatch;
        public float Radius => radius;
        public float Distance => distance;
        public float Size => size;
        public float HealthMultiplier => healthMultiplier;
        public float Spread => spread;
    }

    // Finds the ability assets by kind.
    public static class MPAbilities
    {
        static Dictionary<MPAbility, MPAbilityDef> byKind;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => byKind = null;

        public static MPAbilityDef Get(MPAbility kind) => Get((int)kind);

        public static MPAbilityDef Get(int kind)
        {
            if (byKind == null)
            {
                byKind = new Dictionary<MPAbility, MPAbilityDef>();
                foreach (MPAbilityDef def in Resources.LoadAll<MPAbilityDef>("Abilities")) byKind[def.Kind] = def;
            }
            return byKind.TryGetValue((MPAbility)kind, out MPAbilityDef found) ? found : null;
        }
    }
}
