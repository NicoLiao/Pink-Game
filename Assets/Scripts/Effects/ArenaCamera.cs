using UnityEngine;

namespace LoveShot
{
    // Fits the orthographic camera to the arena at any resolution or aspect ratio. The minimum interior
    // (plus a sliver of wall) always fits below the HUD bar; on the looser axis, ArenaBounds then grows the
    // field so its walls sit exactly at the screen edges and no space outside the arena is visible.
    [RequireComponent(typeof(Camera))]
    public class ArenaCamera : MonoBehaviour
    {
        [Tooltip("Resized to fill the view. Without it, the camera just fits interiorHalfExtents.")]
        [SerializeField] ArenaBounds arenaBounds;
        [Tooltip("Half-size of the playable interior when there is no ArenaBounds, centered on the world origin.")]
        [SerializeField] Vector2 interiorHalfExtents = new Vector2(18f, 10f);
        [Tooltip("How much wall shows along the screen edges, in world units.")]
        [SerializeField, Min(0f)] float wallMargin = 0.4f;
        [Tooltip("HUD element at the top of the screen; the arena is fitted below it. Optional.")]
        [SerializeField] RectTransform hudTopBar;
        [Tooltip("Fraction of the screen height reserved at the top when there is no HUD reference.")]
        [SerializeField, Range(0f, 0.5f)] float fallbackTopReserve = 0.1f;

        Camera cam;
        CameraShake shake;
        readonly Vector3[] corners = new Vector3[4];
        Vector2 lastInterior;

        void Awake()
        {
            cam = GetComponent<Camera>();
            shake = GetComponent<CameraShake>();
            Fit();
        }

        void LateUpdate() => Fit();

        void Fit()
        {
            float topReserve = TopReserveFraction();
            float aspect = cam.aspect;
            Vector2 minInterior = arenaBounds != null ? arenaBounds.MinHalfExtents : interiorHalfExtents;
            Vector2 needed = minInterior + Vector2.one * wallMargin;

            // Size needed so the interior fits vertically in the area below the HUD, and horizontally.
            float sizeForHeight = needed.y / (1f - topReserve);
            float sizeForWidth = needed.x / aspect;
            float size = Mathf.Max(sizeForHeight, sizeForWidth);
            cam.orthographicSize = size;

            // Shift up so the interior is centered in the area below the HUD, not under it.
            var position = new Vector3(0f, size * topReserve, transform.position.z);
            if (shake != null) shake.BasePosition = position;
            else transform.position = position;

            if (arenaBounds == null) return;
            // Visible area below the HUD, centered on the origin; the interior fills it minus the wall sliver.
            var visibleHalf = new Vector2(size * aspect, size * (1f - topReserve));
            Vector2 interior = visibleHalf - Vector2.one * wallMargin;
            if ((interior - lastInterior).sqrMagnitude < 0.0001f) return;
            lastInterior = interior;
            // The strip behind the HUD, above the top wall, is covered by thickening that wall.
            float screenTop = size * (1f + topReserve);
            arenaBounds.Resize(interior, screenTop - (interior.y + arenaBounds.WallThickness));
        }

        // Screen-height fraction covered by the HUD bar, whatever the HUD canvas's render mode.
        float TopReserveFraction()
        {
            if (hudTopBar == null || !hudTopBar.gameObject.activeInHierarchy || Screen.height <= 0)
                return fallbackTopReserve;
            Canvas canvas = hudTopBar.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            hudTopBar.GetWorldCorners(corners);
            float bottom = Mathf.Min(RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]).y,
                RectTransformUtility.WorldToScreenPoint(uiCamera, corners[3]).y);
            return Mathf.Clamp(1f - bottom / Screen.height, 0f, 0.5f);
        }
    }
}
