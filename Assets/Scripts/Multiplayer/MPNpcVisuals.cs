using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Colors a multiplayer NPC by its owner and draws its capture bar: the owner's remaining loyalty fills
    // from the left in the owner's color, the capturing team's progress fills from the right in its color.
    // Flashes and puffs hearts on every hit, pops and bursts when it changes team. Runs on every machine.
    [RequireComponent(typeof(MPCapture))]
    public class MPNpcVisuals : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer loyaltyFill;
        [SerializeField] SpriteRenderer captureFill;
        [Tooltip("World width of the full bar; must match the bar background.")]
        [SerializeField, Min(0f)] float barWidth = 1f;
        [Tooltip("How strongly a neutral NPC tints toward the team capturing it.")]
        [SerializeField, Range(0f, 1f)] float neutralTint = 0.45f;

        [Header("Feedback")]
        [SerializeField] Color flashColor = Color.white;
        [SerializeField, Min(0f)] float hitFlashDuration = 0.08f;
        [SerializeField, Min(1f)] float popScale = 1.5f;
        [SerializeField, Min(0.01f)] float popDuration = 0.3f;
        [SerializeField] HeartBurst burstPrefab;
        [Tooltip("Screen shake when an NPC joins or leaves the local player's team.")]
        [SerializeField, Min(0f)] float localTeamShake = 0.08f;

        MPCapture capture;
        Vector3 bodyBaseScale = Vector3.one;
        float popStartTime = float.NegativeInfinity;
        float flashStartTime = float.NegativeInfinity;

        void Awake()
        {
            capture = GetComponent<MPCapture>();
            if (body != null) bodyBaseScale = body.transform.localScale;
        }

        void OnEnable()
        {
            capture.OwnerChanged += OnOwnerChanged;
            capture.HitReceived += OnHit;
        }

        void OnDisable()
        {
            capture.OwnerChanged -= OnOwnerChanged;
            capture.HitReceived -= OnHit;
        }

        void OnOwnerChanged(int previous, int current)
        {
            popStartTime = Time.time;
            HeartBurst.Play(burstPrefab, transform.position, MPTeams.ColorOf(current));
            int local = MPPlayer.Local != null ? MPPlayer.Local.Team : MPTeams.Neutral;
            if (MPTeams.IsTeam(local) && (previous == local || current == local)) CameraShake.Shake(localTeamShake);
        }

        void OnHit(int team)
        {
            flashStartTime = Time.time;
            HeartBurst.Play(burstPrefab, transform.position, MPTeams.ColorOf(team), 0.5f, 3);
        }

        void LateUpdate()
        {
            int owner = capture.Owner, capturer = capture.Capturer;
            float progress = capturer == MPTeams.Neutral ? 0f : capture.Progress;

            if (body != null)
            {
                Color color = owner == MPTeams.Neutral
                    ? Color.Lerp(TeamUtil.NeutralColor, MPTeams.ColorOf(capturer), progress * neutralTint)
                    : MPTeams.ColorOf(owner);
                if (Time.time - flashStartTime < hitFlashDuration) color = Color.Lerp(color, flashColor, 0.6f);
                body.color = color;

                float t = (Time.time - popStartTime) / popDuration;
                float pop = t < 1f ? Mathf.Sin(t * Mathf.PI) * (popScale - 1f) : 0f;
                body.transform.localScale = bodyBaseScale * (1f + pop);
            }

            float loyalty = owner == MPTeams.Neutral ? 0f : 1f - progress;
            SetFill(loyaltyFill, loyalty, fromLeft: true, MPTeams.ColorOf(owner));
            SetFill(captureFill, progress, fromLeft: false, MPTeams.ColorOf(capturer));
        }

        void SetFill(SpriteRenderer fill, float fraction, bool fromLeft, Color color)
        {
            if (fill == null) return;
            float width = Mathf.Clamp01(fraction) * barWidth;
            float x = (barWidth - width) * 0.5f * (fromLeft ? -1f : 1f);
            Transform t = fill.transform;
            t.localPosition = new Vector3(x, t.localPosition.y, 0f);
            t.localScale = new Vector3(width, t.localScale.y, 1f);
            fill.color = color;
        }
    }
}
