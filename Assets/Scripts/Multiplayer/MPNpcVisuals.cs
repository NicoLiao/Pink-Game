using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Colors a multiplayer NPC by its owner (neutral NPCs tint toward the team capturing them), flashes and
    // puffs hearts on every hit, pops and bursts when it changes team, and floats +1/-1 when it joins or leaves
    // your team. The bar itself is MPCaptureBar.
    // Runs on every machine.
    [RequireComponent(typeof(MPCapture))]
    public class MPNpcVisuals : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
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
        [Tooltip("Floating +1/-1 shown when it joins or leaves the local player's team.")]
        [SerializeField] FloatingText popupPrefab;
        [SerializeField] Color lostColor = new Color(0.45f, 0.28f, 0.42f);

        MPCapture capture;
        MPNpc npc;
        Vector3 bodyBaseScale = Vector3.one;
        float popStartTime = float.NegativeInfinity;
        float flashStartTime = float.NegativeInfinity;

        // Extra scale applied on top of the pop (special NPC animations write it every frame).
        public Vector3 ExtraScale { get; set; } = Vector3.one;

        void Awake()
        {
            capture = GetComponent<MPCapture>();
            npc = GetComponent<MPNpc>();
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
            if (!MPTeams.IsTeam(local) || (previous != local && current != local)) return;
            CameraShake.Shake(localTeamShake);
            // "+1" when it joins your team, "-1" when you lose it (Celebrities are worth more).
            int points = npc != null ? npc.ScoreValue : 1;
            bool gained = current == local;
            FloatingText.Play(popupPrefab, (Vector2)transform.position + Vector2.up * 0.6f, gained ? $"+{points}" : $"-{points}",
                gained ? MPTeams.TextColorOf(local) : lostColor, points > 1 ? 1.4f : 1f);
        }

        void OnHit(int team)
        {
            flashStartTime = Time.time;
            HeartBurst.Play(burstPrefab, transform.position, MPTeams.ColorOf(team), 0.5f, 3);
        }

        void LateUpdate()
        {
            if (body == null) return;
            int owner = capture.Owner, capturer = capture.Capturer;
            float progress = capturer == MPTeams.Neutral ? 0f : capture.Progress;
            Color color = owner == MPTeams.Neutral
                ? Color.Lerp(TeamUtil.NeutralColor, MPTeams.ColorOf(capturer), progress * neutralTint)
                : MPTeams.ColorOf(owner);
            if (Time.time - flashStartTime < hitFlashDuration) color = Color.Lerp(color, flashColor, 0.6f);
            body.color = color;

            float t = (Time.time - popStartTime) / popDuration;
            float pop = t < 1f ? Mathf.Sin(t * Mathf.PI) * (popScale - 1f) : 0f;
            body.transform.localScale = Vector3.Scale(bodyBaseScale * (1f + pop), ExtraScale);
        }
    }
}
