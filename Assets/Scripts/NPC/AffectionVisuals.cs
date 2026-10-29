using UnityEngine;

namespace LoveShot
{
    // Colors an NPC by team, shows an affection bar (fills left to right: pink for positive affection,
    // blue for negative, full at ±100), flashes on hit and pops when the NPC switches teams.
    [RequireComponent(typeof(Affection))]
    public class AffectionVisuals : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer barFill;
        [Tooltip("World width of the full bar; must match the bar background.")]
        [SerializeField, Min(0f)] float barWidth = 1f;
        [Tooltip("How strongly a Neutral NPC tints toward the team that is winning it over.")]
        [SerializeField, Range(0f, 1f)] float neutralTint = 0.45f;

        [Header("Feedback")]
        [SerializeField] Color flashColor = Color.white;
        [SerializeField, Min(0f)] float hitFlashDuration = 0.08f;
        [SerializeField, Min(1f)] float popScale = 1.5f;
        [SerializeField, Min(0.01f)] float popDuration = 0.3f;
        [SerializeField] HeartBurst burstPrefab;
        [Tooltip("Screen shake when an NPC joins or leaves the Pink (player) team.")]
        [SerializeField, Min(0f)] float playerTeamShake = 0.08f;

        Affection affection;
        Vector3 bodyBaseScale = Vector3.one;
        float popStartTime = float.NegativeInfinity;
        float flashStartTime = float.NegativeInfinity;

        void Awake()
        {
            affection = GetComponent<Affection>();
            if (body != null) bodyBaseScale = body.transform.localScale;
        }

        void OnEnable()
        {
            affection.TeamChanged += OnTeamChanged;
            affection.LoveReceived += OnLoveReceived;
        }

        void OnDisable()
        {
            affection.TeamChanged -= OnTeamChanged;
            affection.LoveReceived -= OnLoveReceived;
        }

        void OnTeamChanged(Team previous, Team current)
        {
            popStartTime = Time.time;
            HeartBurst.Play(burstPrefab, transform.position, TeamUtil.ColorOf(current));
            if (previous == Team.Pink || current == Team.Pink) CameraShake.Shake(playerTeamShake);
        }

        void OnLoveReceived(Team source)
        {
            flashStartTime = Time.time;
            HeartBurst.Play(burstPrefab, transform.position, TeamUtil.ColorOf(source), 0.5f, 3);
        }

        void LateUpdate()
        {
            float normalized = affection.Normalized;
            Team leaning = normalized >= 0f ? Team.Pink : Team.Blue;

            if (body != null)
            {
                Color color = affection.Owner == Team.Neutral
                    ? Color.Lerp(TeamUtil.NeutralColor, TeamUtil.ColorOf(leaning), Mathf.Abs(normalized) * neutralTint)
                    : TeamUtil.ColorOf(affection.Owner);
                if (Time.time - flashStartTime < hitFlashDuration) color = Color.Lerp(color, flashColor, 0.6f);
                body.color = color;

                float t = (Time.time - popStartTime) / popDuration;
                float pop = t < 1f ? Mathf.Sin(t * Mathf.PI) * (popScale - 1f) : 0f;
                body.transform.localScale = bodyBaseScale * (1f + pop);
            }

            if (barFill != null)
            {
                // Left-anchored: the fill grows from the bar's left edge, |affection| / 100 of the way across.
                float width = Mathf.Abs(normalized) * barWidth;
                Transform fill = barFill.transform;
                fill.localPosition = new Vector3((width - barWidth) * 0.5f, fill.localPosition.y, 0f);
                fill.localScale = new Vector3(width, fill.localScale.y, 1f);
                barFill.color = TeamUtil.ColorOf(leaning);
            }
        }
    }
}
