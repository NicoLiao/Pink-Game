using UnityEngine;

namespace LoveShot
{
    // Leader feedback: flickers and wobbles while lovestruck, and the outline dims while dash is on cooldown.
    [RequireComponent(typeof(Lovestruck), typeof(Mover))]
    public class LeaderVisuals : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer outline;
        [SerializeField] HeartBurst burstPrefab;
        [SerializeField] Color struckTint = Color.white;
        [SerializeField, Min(0f)] float struckWobble = 0.15f;
        [Tooltip("Outline opacity while dash is recharging.")]
        [SerializeField, Range(0f, 1f)] float dashCooldownAlpha = 0.25f;
        [SerializeField, Min(0f)] float shakeOnStruck = 0.25f;

        Lovestruck lovestruck;
        Mover mover;
        Color bodyColor;
        Vector3 bodyScale;

        void Awake()
        {
            lovestruck = GetComponent<Lovestruck>();
            mover = GetComponent<Mover>();
            if (body != null)
            {
                bodyColor = body.color;
                bodyScale = body.transform.localScale;
            }
        }

        void OnEnable() => lovestruck.Struck += OnStruck;

        void OnDisable() => lovestruck.Struck -= OnStruck;

        void OnStruck()
        {
            HeartBurst.Play(burstPrefab, transform.position, struckTint, 1.2f, 10);
            CameraShake.Shake(shakeOnStruck);
        }

        void LateUpdate()
        {
            if (body != null)
            {
                bool struck = lovestruck.IsStruck;
                float pulse = struck ? 0.5f + 0.5f * Mathf.Sin(Time.time * 30f) : 0f;
                body.color = Color.Lerp(bodyColor, struckTint, pulse * 0.7f);
                body.transform.localScale = bodyScale * (1f + (struck ? Mathf.Sin(Time.time * 22f) * struckWobble : 0f));
            }

            if (outline != null)
            {
                Color color = outline.color;
                color.a = mover.CanDash ? 1f : dashCooldownAlpha;
                outline.color = color;
            }
        }
    }
}
