using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Multiplayer Dash: dashes at once on the owner; the host confirms the cooldown (MPPlayer.TryDash).
    [RequireComponent(typeof(MPPlayer))]
    public class NetDashSkill : Skill
    {
        MPPlayer player;

        void Awake() => player = GetComponent<MPPlayer>();

        public override float Cooldown => player.DashCooldown;
        public override float CooldownRemaining => player.DashCooldownRemaining;
        public override bool IsReady => CooldownRemaining <= 0f;
        // Zombies follow their converter and can't dash.
        public override bool IsDisabled => player.IsZombie;

        public override bool TryActivate(Vector2 direction) => player.TryDash(direction);
    }
}
