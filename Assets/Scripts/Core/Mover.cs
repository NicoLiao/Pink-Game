using System;
using UnityEngine;

namespace LoveShot
{
    // Shared top-down movement for leaders and NPCs. Controllers only call SetMoveInput / TryDash.
    [RequireComponent(typeof(Rigidbody2D))]
    public class Mover : MonoBehaviour
    {
        [SerializeField, Min(0f)] float moveSpeed = 7f;
        [Tooltip("How quickly velocity reaches the target speed (units/s²). Higher = snappier.")]
        [SerializeField, Min(0f)] float acceleration = 70f;

        [Header("Dash")]
        [Tooltip("Dash speed as a multiple of moveSpeed.")]
        [SerializeField, Min(0f)] float dashSpeedMultiplier = 3f;
        [SerializeField, Min(0f)] float dashDuration = 0.2f;
        [Tooltip("Seconds from the start of one dash until the next is allowed.")]
        [SerializeField, Min(0f)] float dashCooldown = 2f;

        Rigidbody2D body;
        Vector2 moveInput;
        Vector2 dashVelocity;
        float dashEndTime;
        float nextDashTime;
        bool wasDashing;

        // Raised when a dash starts, with its normalized direction.
        public event Action<Vector2> Dashed;

        public float MoveSpeed { get => moveSpeed; set => moveSpeed = Mathf.Max(0f, value); }
        // Temporary speed scaling (slows, catch-up boosts). Owners reset it to 1 when done.
        public float SpeedMultiplier { get; set; } = 1f;
        public bool IsDashing => Time.time < dashEndTime;
        public bool CanDash => !IsDashing && Time.time >= nextDashTime;
        public float DashCooldown => dashCooldown;
        public float DashCooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);
        public Vector2 Velocity => body.linearVelocity;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }

        // input is a direction with magnitude 0..1 (fraction of moveSpeed).
        public void SetMoveInput(Vector2 input) => moveInput = Vector2.ClampMagnitude(input, 1f);

        public bool TryDash(Vector2 direction)
        {
            if (!CanDash || direction.sqrMagnitude < 0.0001f) return false;
            direction.Normalize();
            // Rigidbody collisions still apply during the dash, so walls and obstacles stop it.
            dashVelocity = direction * moveSpeed * dashSpeedMultiplier;
            body.linearVelocity = dashVelocity;
            dashEndTime = Time.time + dashDuration;
            nextDashTime = Time.time + dashCooldown;
            Dashed?.Invoke(direction);
            return true;
        }

        void FixedUpdate()
        {
            if (IsDashing)
            {
                body.linearVelocity = dashVelocity;
                wasDashing = true;
                return;
            }
            if (wasDashing)
            {
                // End crisply: drop straight back to normal speed instead of sliding down from dash speed.
                wasDashing = false;
                body.linearVelocity = Vector2.ClampMagnitude(body.linearVelocity, moveSpeed);
            }
            Vector2 target = moveInput * moveSpeed * SpeedMultiplier;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, target, acceleration * Time.fixedDeltaTime);
        }
    }
}
