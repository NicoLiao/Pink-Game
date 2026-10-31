using UnityEngine;

namespace LoveShot
{
    // Gives a projectile a soft glow and a fading trail in its own color. The projectile scripts color the main
    // sprite when they launch it (before Start), so this only copies that color; it has no gameplay effect.
    public class ProjectileGlow : MonoBehaviour
    {
        [SerializeField] SpriteRenderer source;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] TrailRenderer trail;
        [SerializeField, Range(0f, 1f)] float glowAlpha = 0.45f;
        [SerializeField, Range(0f, 1f)] float trailAlpha = 0.6f;
        [Tooltip("Glow size pulse (fraction of its size) and speed.")]
        [SerializeField, Range(0f, 0.5f)] float pulse = 0.12f;
        [SerializeField, Min(0f)] float pulseSpeed = 14f;

        Vector3 glowScale;

        void Start()
        {
            Color color = source != null ? source.color : Color.white;
            if (glow != null)
            {
                glow.color = new Color(color.r, color.g, color.b, glowAlpha);
                glowScale = glow.transform.localScale;
            }
            if (trail != null)
            {
                trail.startColor = new Color(color.r, color.g, color.b, trailAlpha);
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
                trail.Clear();
            }
        }

        void Update()
        {
            if (glow != null) glow.transform.localScale = glowScale * (1f + pulse * Mathf.Sin(Time.time * pulseSpeed));
        }
    }
}
