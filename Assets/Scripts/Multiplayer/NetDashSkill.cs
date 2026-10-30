using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Multiplayer Dash: asks the host to dash (the host checks the cooldown) and shows the host's cooldown.
    [RequireComponent(typeof(MPPlayer))]
    public class NetDashSkill : Skill
    {
        [Tooltip("After a request, the slot stays unavailable this long while waiting for the host's answer.")]
        [SerializeField, Min(0f)] float requestTimeout = 0.5f;

        MPPlayer player;
        float pendingUntil = float.NegativeInfinity;

        void Awake() => player = GetComponent<MPPlayer>();

        public override float Cooldown => player.DashCooldown;
        public override float CooldownRemaining => player.DashCooldownRemaining;
        public override bool IsReady => CooldownRemaining <= 0f && Time.unscaledTime >= pendingUntil;

        public override bool TryActivate(Vector2 direction)
        {
            if (!player.IsOwner || !IsReady || !MPMatch.InProgress || direction.sqrMagnitude < 0.0001f) return false;
            pendingUntil = Time.unscaledTime + requestTimeout;
            player.RequestDashRpc(direction);
            return true;
        }
    }
}
