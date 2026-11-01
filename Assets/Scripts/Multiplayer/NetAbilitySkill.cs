using UnityEngine;

namespace LoveShot.Multiplayer
{
    // One ability slot (Space or Shift): whichever ability the player chose for it in the lobby (Dash, Group Heal, Spawn Powerup or Duplicate).
    // Name, icon and numbers come from the MPAbilityDef assets; the rules live in MPPlayer (the host confirms every use).
    [RequireComponent(typeof(MPPlayer))]
    public class NetAbilitySkill : Skill
    {
        [Tooltip("0 = the Space slot, 1 = the Shift slot (matches the lobby choice).")]
        [SerializeField, Range(0, 1)] int abilitySlot;

        MPPlayer player;

        void Awake() => player = GetComponent<MPPlayer>();

        MPAbilityDef Current => player != null ? MPAbilities.Get(player.AbilityOf(abilitySlot)) : null;

        public override string DisplayName => Current != null ? Current.DisplayName : base.DisplayName;
        public override Sprite Icon => Current != null && Current.Icon != null ? Current.Icon : base.Icon;
        public override float Cooldown => player.AbilityCooldown(abilitySlot);
        public override float CooldownRemaining => player.AbilityCooldownRemaining(abilitySlot);
        // Zombies follow their converter and can't use abilities; Duplicate works once per match.
        public override bool IsDisabled => player.IsZombie || player.AbilityUsedUp(abilitySlot);

        public override bool TryActivate(Vector2 direction) => player.TryUseAbility(abilitySlot, direction);
    }
}
