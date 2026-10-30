using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Local (never networked) camera for the multiplayer arena: follows this machine's own player and is
    // clamped so the view never leaves the arena. If the screen is wider or taller than the arena allows,
    // it zooms in until it fits, so no space outside the map is ever visible.
    [RequireComponent(typeof(Camera))]
    public class MPFollowCamera : MonoBehaviour
    {
        [Tooltip("Half-size of the arena interior, centered on the world origin.")]
        [SerializeField] Vector2 arenaHalfExtents = new Vector2(28f, 17f);
        [Tooltip("How much wall may show at the arena edge, in world units.")]
        [SerializeField, Min(0f)] float wallMargin = 0.4f;
        [Tooltip("Preferred orthographic size (half the visible height in world units).")]
        [SerializeField, Min(1f)] float preferredSize = 12.5f;
        [SerializeField, Min(0f)] float smoothTime = 0.12f;

        Camera cam;
        CameraShake shake;
        Vector2 position;
        Vector2 velocity;
        bool snapped;

        void Awake()
        {
            cam = GetComponent<Camera>();
            shake = GetComponent<CameraShake>();
        }

        void LateUpdate()
        {
            Vector2 limit = arenaHalfExtents + Vector2.one * wallMargin;
            // Largest view that still fits inside the arena on both axes.
            float size = Mathf.Min(preferredSize, limit.y, limit.x / cam.aspect);
            cam.orthographicSize = size;

            MPPlayer target = MPPlayer.Local;
            Vector2 goal = target != null ? (Vector2)target.transform.position : Vector2.zero;
            Vector2 halfView = new Vector2(size * cam.aspect, size);
            goal.x = Mathf.Clamp(goal.x, -limit.x + halfView.x, limit.x - halfView.x);
            goal.y = Mathf.Clamp(goal.y, -limit.y + halfView.y, limit.y - halfView.y);

            if (!snapped && target != null)
            {
                // Jump straight to the player the first time instead of gliding in from the center.
                position = goal;
                snapped = true;
            }
            else
            {
                position = Vector2.SmoothDamp(position, goal, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            }

            var final = new Vector3(position.x, position.y, transform.position.z);
            if (shake != null) shake.BasePosition = final;
            else transform.position = final;
        }
    }
}
