using System;
using UnityEngine;

namespace LoveShot
{
    // A leader hit by the enemy leader is briefly slowed and can't shoot, then immune for a moment
    // so it can never be stun-locked. Leaders are never eliminated.
    [RequireComponent(typeof(Mover), typeof(Shooter))]
    public class Lovestruck : MonoBehaviour
    {
        [Tooltip("Seconds the leader stays lovestruck. 0 disables leader hits.")]
        [SerializeField, Min(0f)] float duration = 0.6f;
        [SerializeField, Range(0f, 1f)] float speedMultiplier = 0.35f;
        [SerializeField] bool blockShooting = true;
        [Tooltip("Seconds after recovering during which further hits pass through.")]
        [SerializeField, Min(0f)] float immunityDuration = 1f;

        Mover mover;
        Shooter shooter;
        float struckUntil = float.NegativeInfinity;
        float immuneUntil = float.NegativeInfinity;

        public event Action Struck;

        public bool IsStruck => Time.time < struckUntil;
        public bool CanBeStruck => isActiveAndEnabled && duration > 0f && Time.time >= immuneUntil;

        void Awake()
        {
            mover = GetComponent<Mover>();
            shooter = GetComponent<Shooter>();
        }

        public bool TryStrike()
        {
            if (!CanBeStruck) return false;
            struckUntil = Time.time + duration;
            immuneUntil = struckUntil + immunityDuration;
            Struck?.Invoke();
            return true;
        }

        void Update()
        {
            bool struck = IsStruck;
            mover.SpeedMultiplier = struck ? speedMultiplier : 1f;
            shooter.Locked = struck && blockShooting;
        }
    }
}
