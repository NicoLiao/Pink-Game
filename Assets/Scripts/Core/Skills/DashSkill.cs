using UnityEngine;

namespace LoveShot
{
    // Single-player Dash: a thin skill wrapper around the Mover's dash, which owns speed and cooldown.
    [RequireComponent(typeof(Mover))]
    public class DashSkill : Skill
    {
        Mover mover;

        void Awake() => mover = GetComponent<Mover>();

        public override float Cooldown => mover.DashCooldown;
        public override float CooldownRemaining => mover.DashCooldownRemaining;
        public override bool IsReady => mover.CanDash;

        public override bool TryActivate(Vector2 direction) => mover.TryDash(direction);
    }
}
