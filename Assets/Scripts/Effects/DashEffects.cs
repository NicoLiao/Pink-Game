using System.Collections.Generic;
using UnityEngine;

namespace LoveShot
{
    // Dash feedback: fading afterimages during the dash, a heart burst when it starts, and an optional
    // radial cooldown ring that fills while recharging and pops when dash is ready again.
    [RequireComponent(typeof(Mover))]
    public class DashEffects : MonoBehaviour
    {
        [Header("Afterimages")]
        [Tooltip("The sprite copied for afterimages (the leader's body).")]
        [SerializeField] SpriteRenderer source;
        [SerializeField] Color afterimageColor = new Color(1f, 0.45f, 0.72f, 0.55f);
        [SerializeField, Min(0.005f)] float afterimageInterval = 0.025f;
        [SerializeField, Min(0.05f)] float afterimageLifetime = 0.25f;

        [Header("Burst")]
        [SerializeField] HeartBurst burstPrefab;
        [SerializeField, Min(1)] int burstCount = 6;
        [SerializeField, Min(0f)] float burstScale = 0.8f;
        [SerializeField, Min(0f)] float dashShake = 0.06f;

        [Header("Cooldown indicator (optional)")]
        [Tooltip("A Filled (Radial 360) UI Image around the leader.")]
        [SerializeField] UnityEngine.UI.Image cooldownRing;
        [SerializeField] Color chargingColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] Color readyColor = new Color(1f, 0.45f, 0.72f, 1f);
        [SerializeField, Min(1f)] float readyPopScale = 1.35f;
        [SerializeField, Min(0.01f)] float readyPopDuration = 0.3f;

        struct Afterimage
        {
            public SpriteRenderer Renderer;
            public float BornTime;
        }

        Mover mover;
        readonly List<Afterimage> afterimages = new();
        float nextAfterimageTime;
        float readyPopStartTime = float.NegativeInfinity;
        bool wasReady = true;

        void Awake() => mover = GetComponent<Mover>();

        void OnEnable() => mover.Dashed += OnDashed;

        void OnDisable()
        {
            mover.Dashed -= OnDashed;
            foreach (Afterimage afterimage in afterimages)
                if (afterimage.Renderer != null) Destroy(afterimage.Renderer.gameObject);
            afterimages.Clear();
        }

        void OnDashed(Vector2 direction)
        {
            HeartBurst.Play(burstPrefab, transform.position, new Color(afterimageColor.r, afterimageColor.g, afterimageColor.b), burstScale, burstCount);
            CameraShake.Shake(dashShake);
            nextAfterimageTime = Time.time;
        }

        void LateUpdate()
        {
            if (mover.IsDashing && source != null && Time.time >= nextAfterimageTime)
            {
                SpawnAfterimage();
                nextAfterimageTime = Time.time + afterimageInterval;
            }
            FadeAfterimages();
            UpdateCooldownRing();
        }

        void SpawnAfterimage()
        {
            var renderer = new GameObject("DashAfterimage").AddComponent<SpriteRenderer>();
            renderer.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            renderer.transform.localScale = source.transform.lossyScale;
            renderer.sprite = source.sprite;
            renderer.color = afterimageColor;
            renderer.sortingOrder = source.sortingOrder - 1;
            afterimages.Add(new Afterimage { Renderer = renderer, BornTime = Time.time });
        }

        void FadeAfterimages()
        {
            for (int i = afterimages.Count - 1; i >= 0; i--)
            {
                Afterimage afterimage = afterimages[i];
                float t = (Time.time - afterimage.BornTime) / afterimageLifetime;
                if (afterimage.Renderer == null || t >= 1f)
                {
                    if (afterimage.Renderer != null) Destroy(afterimage.Renderer.gameObject);
                    afterimages.RemoveAt(i);
                    continue;
                }
                Color color = afterimageColor;
                color.a *= 1f - t;
                afterimage.Renderer.color = color;
                afterimage.Renderer.transform.localScale = source.transform.lossyScale * (1f - 0.3f * t);
            }
        }

        void UpdateCooldownRing()
        {
            if (cooldownRing == null) return;

            bool ready = mover.CanDash;
            if (ready && !wasReady) readyPopStartTime = Time.time;
            wasReady = ready;

            Transform ring = cooldownRing.transform;
            if (!ready)
            {
                // Fills clockwise from empty (just dashed) to full (ready).
                float cooldown = Mathf.Max(mover.DashCooldown, 0.0001f);
                cooldownRing.enabled = true;
                cooldownRing.fillAmount = 1f - mover.DashCooldownRemaining / cooldown;
                cooldownRing.color = chargingColor;
                ring.localScale = Vector3.one;
                return;
            }

            float t = (Time.time - readyPopStartTime) / readyPopDuration;
            cooldownRing.enabled = t < 1f;
            if (t >= 1f) return;
            // "Ready!" pop: full ring in the team color that swells and fades.
            cooldownRing.fillAmount = 1f;
            Color color = readyColor;
            color.a *= 1f - t;
            cooldownRing.color = color;
            ring.localScale = Vector3.one * Mathf.Lerp(1f, readyPopScale, t);
        }
    }
}
