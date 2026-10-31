using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Look of a networked player on every machine: body in the current team's color (blending over when
    // converted), a heart mark in the player's own identity color, a name tag ("YOU" for the local player),
    // a ring marking your own character, a pulsing shield while conversion-immune, flicker while lovestruck,
    // a big heart burst on conversion, and dash afterimages.
    [RequireComponent(typeof(MPPlayer))]
    public class MPPlayerVisuals : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer aimMarker;
        [SerializeField] SpriteRenderer localRing;
        [Tooltip("Heart in the player's own (identity) color, so a converted player is still recognizable.")]
        [SerializeField] SpriteRenderer identityMark;
        [Tooltip("Ring shown while the player is conversion-immune.")]
        [SerializeField] SpriteRenderer immunityShield;
        [SerializeField] TMP_Text nameTag;
        [SerializeField] HeartBurst burstPrefab;
        [SerializeField] Color struckTint = Color.white;
        [SerializeField, Min(0f)] float struckWobble = 0.15f;

        [Header("Conversion")]
        [SerializeField, Min(0.01f)] float colorBlendDuration = 0.6f;
        [SerializeField, Min(1f)] float convertPopScale = 1.6f;
        [SerializeField, Min(0f)] float convertShake = 0.45f;

        [Header("Dash")]
        [SerializeField, Min(0.05f)] float dashVisualDuration = 0.2f;
        [SerializeField, Min(0.005f)] float afterimageInterval = 0.025f;
        [SerializeField, Min(0.05f)] float afterimageLifetime = 0.25f;
        [SerializeField, Min(0f)] float dashShake = 0.06f;
        [SerializeField, Min(0f)] float struckShake = 0.3f;

        struct Afterimage
        {
            public SpriteRenderer Renderer;
            public float BornTime;
        }

        MPPlayer player;
        Color teamColor;
        Color previousTeamColor;
        float convertedTime = float.NegativeInfinity;
        Vector3 bodyScale;
        float dashVisualUntil = float.NegativeInfinity;
        float nextAfterimageTime;
        readonly List<Afterimage> afterimages = new();

        void Awake()
        {
            player = GetComponent<MPPlayer>();
            if (body != null) bodyScale = body.transform.localScale;
        }

        void OnEnable()
        {
            player.Dashed += OnDashed;
            player.Struck += OnStruck;
            player.Converted += OnConverted;
        }

        void OnDisable()
        {
            player.Dashed -= OnDashed;
            player.Struck -= OnStruck;
            player.Converted -= OnConverted;
            foreach (Afterimage afterimage in afterimages)
                if (afterimage.Renderer != null) Destroy(afterimage.Renderer.gameObject);
            afterimages.Clear();
        }

        void Start()
        {
            teamColor = previousTeamColor = MPTeams.ColorOf(player.Team);
            if (localRing != null) localRing.enabled = player.IsOwner;
            if (identityMark != null) identityMark.color = MPTeams.ColorOf(player.Slot);
        }

        void OnConverted(int previous, int current)
        {
            previousTeamColor = MPTeams.ColorOf(previous);
            convertedTime = Time.time;
            HeartBurst.Play(burstPrefab, transform.position, MPTeams.ColorOf(current), 2f, 18);
            HeartBurst.Play(burstPrefab, transform.position, previousTeamColor, 1.2f, 8);
            if (player.IsOwner) CameraShake.Shake(convertShake);
        }

        void OnDashed(Vector2 direction)
        {
            dashVisualUntil = Time.time + dashVisualDuration;
            nextAfterimageTime = Time.time;
            HeartBurst.Play(burstPrefab, transform.position, teamColor, 0.8f, 6);
            if (player.IsOwner) CameraShake.Shake(dashShake);
        }

        void OnStruck()
        {
            HeartBurst.Play(burstPrefab, transform.position, struckTint, 1.2f, 10);
            if (player.IsOwner) CameraShake.Shake(struckShake);
        }

        void LateUpdate()
        {
            float blend = Mathf.Clamp01((Time.time - convertedTime) / colorBlendDuration);
            teamColor = Color.Lerp(previousTeamColor, MPTeams.ColorOf(player.Team), blend);
            if (aimMarker != null) aimMarker.color = teamColor;
            if (localRing != null) localRing.color = new Color(teamColor.r, teamColor.g, teamColor.b, 0.5f);

            if (nameTag != null)
            {
                nameTag.text = player.IsOwner ? $"{player.DisplayName} (YOU)" : player.DisplayName;
                nameTag.color = MPTeams.TextColorOf(player.Team);
            }

            if (immunityShield != null)
            {
                bool immune = player.IsConversionImmune;
                immunityShield.enabled = immune;
                if (immune)
                {
                    // Pulses faster in the last second, so everyone can see it running out.
                    float speed = player.ConversionImmunityRemaining < 1f ? 22f : 9f;
                    immunityShield.color = new Color(1f, 1f, 1f, 0.45f + 0.35f * Mathf.Sin(Time.time * speed));
                    immunityShield.transform.localScale = Vector3.one * (1.55f + 0.08f * Mathf.Sin(Time.time * speed * 0.5f));
                }
            }

            if (body != null)
            {
                bool struck = player.IsStruck;
                float pulse = struck ? 0.5f + 0.5f * Mathf.Sin(Time.time * 30f) : 0f;
                body.color = Color.Lerp(teamColor, struckTint, pulse * 0.7f);
                float t = (Time.time - convertedTime) / colorBlendDuration;
                float pop = t < 1f ? Mathf.Sin(t * Mathf.PI) * (convertPopScale - 1f) : 0f;
                body.transform.localScale = bodyScale * (1f + pop + (struck ? Mathf.Sin(Time.time * 22f) * struckWobble : 0f));
            }

            if (Time.time < dashVisualUntil && body != null && Time.time >= nextAfterimageTime)
            {
                SpawnAfterimage();
                nextAfterimageTime = Time.time + afterimageInterval;
            }
            FadeAfterimages();
        }

        void SpawnAfterimage()
        {
            var renderer = new GameObject("DashAfterimage").AddComponent<SpriteRenderer>();
            renderer.transform.SetPositionAndRotation(body.transform.position, body.transform.rotation);
            renderer.transform.localScale = body.transform.lossyScale;
            renderer.sprite = body.sprite;
            renderer.color = new Color(teamColor.r, teamColor.g, teamColor.b, 0.55f);
            renderer.sortingOrder = body.sortingOrder - 1;
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
                afterimage.Renderer.color = new Color(teamColor.r, teamColor.g, teamColor.b, 0.55f * (1f - t));
            }
        }
    }
}
