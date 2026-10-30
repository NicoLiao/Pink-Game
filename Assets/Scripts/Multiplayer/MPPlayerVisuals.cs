using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Look of a networked player on every machine: team colors, a name tag ("YOU" for the local player),
    // a ring marking your own character, flicker while lovestruck, and dash afterimages and burst.
    [RequireComponent(typeof(MPPlayer))]
    public class MPPlayerVisuals : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer aimMarker;
        [SerializeField] SpriteRenderer localRing;
        [SerializeField] TMP_Text nameTag;
        [SerializeField] HeartBurst burstPrefab;
        [SerializeField] Color struckTint = Color.white;
        [SerializeField, Min(0f)] float struckWobble = 0.15f;

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
        }

        void OnDisable()
        {
            player.Dashed -= OnDashed;
            player.Struck -= OnStruck;
            foreach (Afterimage afterimage in afterimages)
                if (afterimage.Renderer != null) Destroy(afterimage.Renderer.gameObject);
            afterimages.Clear();
        }

        void Start() => ApplyTeam();

        void ApplyTeam()
        {
            teamColor = MPTeams.ColorOf(player.Team);
            if (body != null) body.color = teamColor;
            if (aimMarker != null) aimMarker.color = teamColor;
            if (localRing != null)
            {
                localRing.enabled = player.IsOwner;
                localRing.color = new Color(teamColor.r, teamColor.g, teamColor.b, 0.5f);
            }
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
            if (nameTag != null)
            {
                nameTag.text = player.IsOwner ? $"{player.DisplayName} (YOU)" : player.DisplayName;
                nameTag.color = MPTeams.TextColorOf(player.Team);
            }

            if (body != null)
            {
                bool struck = player.IsStruck;
                float pulse = struck ? 0.5f + 0.5f * Mathf.Sin(Time.time * 30f) : 0f;
                body.color = Color.Lerp(teamColor, struckTint, pulse * 0.7f);
                body.transform.localScale = bodyScale * (1f + (struck ? Mathf.Sin(Time.time * 22f) * struckWobble : 0f));
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
