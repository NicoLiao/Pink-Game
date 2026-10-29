using UnityEngine;

namespace LoveShot
{
    // Gently bobs (and optionally sways) a UI element, for decoration.
    [RequireComponent(typeof(RectTransform))]
    public class UIFloat : MonoBehaviour
    {
        [SerializeField] float amplitude = 12f;
        [SerializeField, Min(0f)] float speed = 1.5f;
        [Tooltip("Offset in radians, so several floating elements don't move in sync.")]
        [SerializeField] float phase;
        [Tooltip("Sway in degrees.")]
        [SerializeField] float swayAngle;

        RectTransform rect;
        Vector2 basePosition;

        void Awake()
        {
            rect = (RectTransform)transform;
            basePosition = rect.anchoredPosition;
        }

        void Update()
        {
            float wave = Mathf.Sin(Time.unscaledTime * speed + phase);
            rect.anchoredPosition = basePosition + Vector2.up * wave * amplitude;
            rect.localRotation = Quaternion.Euler(0f, 0f, wave * swayAngle);
        }
    }
}
