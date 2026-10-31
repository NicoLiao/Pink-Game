using UnityEngine;

namespace LoveShot
{
    // Cartoon motion for a character's visual pivot ("Rig", parent of the body sprites): little hops with
    // squash and stretch while moving, a lean into the direction of travel, a slow breath when idle, and a
    // quick squish for shots and hits. It reads motion from the character's transform, so it works the same
    // for simulated, AI and networked copies, and it never touches gameplay (colliders stay on the root).
    public class SquashStretch : MonoBehaviour
    {
        [Tooltip("The moving character root. Defaults to the parent.")]
        [SerializeField] Transform motionSource;
        [Tooltip("Speed at which the hop animation is at full strength.")]
        [SerializeField, Min(0.1f)] float referenceSpeed = 6f;
        [SerializeField, Min(0f)] float hopHeight = 0.07f;
        [Tooltip("Hops per second at the reference speed.")]
        [SerializeField, Min(0f)] float hopRate = 4.5f;
        [SerializeField, Range(0f, 0.4f)] float stretch = 0.09f;
        [SerializeField, Min(0f)] float leanAngle = 7f;
        [SerializeField, Range(0f, 0.2f)] float breathe = 0.025f;
        [SerializeField, Min(0f)] float breatheSpeed = 2.2f;
        [SerializeField, Min(0.01f)] float squishDuration = 0.18f;
        [Tooltip("Squish when the character fires (positive = taller and thinner).")]
        [SerializeField, Range(-0.4f, 0.4f)] float fireSquish = 0.12f;

        Vector3 lastPosition;
        Vector2 velocity;
        float phase;
        float breathOffset;
        float squishAmount;
        float squishStartTime = float.NegativeInfinity;

        void Awake()
        {
            if (motionSource == null) motionSource = transform.parent != null ? transform.parent : transform;
            breathOffset = Random.value * 10f;
        }

        void OnEnable() => lastPosition = motionSource.position;

        // A shot's recoil (MPProjectile calls it on the shooter wherever the shot is shown).
        public void Kick() => Squish(fireSquish);

        // A quick squash (negative: wider) or stretch (positive: taller) that springs back.
        public void Squish(float amount)
        {
            squishAmount = amount;
            squishStartTime = Time.time;
        }

        void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            Vector3 position = motionSource.position;
            // Teleports and network corrections would read as huge speeds; cap them.
            Vector2 frameVelocity = Vector2.ClampMagnitude((position - lastPosition) / deltaTime, referenceSpeed * 3f);
            lastPosition = position;
            velocity = Vector2.Lerp(velocity, frameVelocity, 1f - Mathf.Exp(-15f * deltaTime));

            float moving = Mathf.Clamp01(velocity.magnitude / referenceSpeed);
            phase += deltaTime * hopRate * Mathf.PI * Mathf.Lerp(0.5f, 1f, moving);
            float hop = Mathf.Abs(Mathf.Sin(phase)) * hopHeight * moving;
            // Tall in the air, squashed on landing.
            float amount = -Mathf.Cos(phase * 2f) * stretch * moving;
            amount += Mathf.Sin((Time.time + breathOffset) * breatheSpeed) * breathe * (1f - moving);
            float squish = 1f - (Time.time - squishStartTime) / squishDuration;
            if (squish > 0f) amount += squishAmount * squish * squish;

            transform.localPosition = new Vector3(0f, hop, 0f);
            transform.localScale = new Vector3(1f - amount, 1f + amount, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, -velocity.x / referenceSpeed * leanAngle);
        }
    }
}
