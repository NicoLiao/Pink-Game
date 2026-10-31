using System;
using Unity.Netcode;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Hugger: melee support that never shoots. Once recruited it hunts nearby opposing NPCs (players too,
    // but less eagerly and for less love), waddles up, charges the last few meters and gives a big hug of
    // love on contact. Then it needs a moment before the next hug and trots back to its owner.
    public class MPHuggerNpc : MPNpc
    {
        [Header("Hug")]
        [Tooltip("Starts the charge from this distance.")]
        [SerializeField, Min(0f)] float chargeRange = 2.6f;
        [Tooltip("Contact distance between body edges that counts as a hug.")]
        [SerializeField, Min(0f)] float hugReach = 0.35f;
        [SerializeField, Min(0f)] float hugPower = 45f;
        [Tooltip("Multiplies hug love on players.")]
        [SerializeField, Range(0f, 1f)] float playerHugMultiplier = 0.45f;
        [SerializeField, Min(0f)] float hugCooldown = 2.5f;
        [Tooltip("Distance multiplier for players as targets (higher = picks them less often).")]
        [SerializeField, Min(1f)] float playerTargetPenalty = 1.6f;

        float nextHugTime;
        float bodyRadius;

        // Raised on every machine for each hug, with the hug point and the hugger's team.
        public event Action<Vector2, int> Hugged;

        protected override void Awake()
        {
            base.Awake();
            bodyRadius = GetComponent<CircleCollider2D>().radius * transform.localScale.x;
        }

        protected override NpcBehavior CurrentBehavior() =>
            Mover.IsDashing ? NpcBehavior.Charge : target != null && Time.time >= nextHugTime ? NpcBehavior.Attack : NpcBehavior.Follow;

        protected override Vector2 Follow(MPPlayer leader)
        {
            if (target == null || Time.time < nextHugTime) return base.Follow(leader);
            Vector2 toTarget = target.transform.position - transform.position;
            if (toTarget.magnitude <= chargeRange) Mover.TryDash(toTarget);
            return toTarget.normalized;
        }

        protected override void UpdateCombat()
        {
            RefreshTarget();
            if (target == null || Time.time < nextHugTime) return;
            float targetRadius = target.TryGetComponent(out CircleCollider2D other) ? other.radius * other.transform.localScale.x : 0.5f;
            float gap = Vector2.Distance(transform.position, target.transform.position) - bodyRadius - targetRadius;
            if (gap > hugReach || MPMatch.Instance == null) return;

            float power = target.Player != null ? hugPower * playerHugMultiplier : hugPower;
            Vector2 point = Vector2.Lerp(transform.position, target.transform.position, 0.5f);
            if (MPMatch.Instance.ApplyLove(target, PersonalOwner, power, fromPlayer: false)) HuggedRpc(point, Team);
            nextHugTime = Time.time + hugCooldown;
            target = null;
        }

        protected override float TargetScore(MPCapture other, float distance)
        {
            if (other.Player != null) return distance * playerTargetPenalty;
            return base.TargetScore(other, distance);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void HuggedRpc(Vector2 point, int team) => Hugged?.Invoke(point, team);
    }
}
