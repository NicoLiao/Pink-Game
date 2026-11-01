using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Screen-edge vignette for the local player, in the attacker's color: its base strength follows how far the
    // player is from being converted (the capture progress against them), and every enemy hit spikes it, fading
    // back to the base; being converted flashes it at full.
    public class MPHitVignette : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Image image;
        [Tooltip("Strength when the player is one hit from being converted; it scales with the capture progress.")]
        [SerializeField, Range(0f, 1f)] float baseStrength = 0.55f;
        [Tooltip("How fast the base strength follows the progress (per second).")]
        [SerializeField, Min(0.1f)] float baseResponse = 4f;
        [Tooltip("Strength added by each enemy hit.")]
        [SerializeField, Range(0f, 1f)] float hitSpike = 0.35f;
        [Tooltip("Seconds a full spike takes to fade.")]
        [SerializeField, Min(0.05f)] float spikeFade = 0.5f;
        [Tooltip("Strength the moment the player is converted.")]
        [SerializeField, Range(0f, 1f)] float convertedSpike = 1f;
        [SerializeField, Range(0f, 1f)] float maxAlpha = 0.9f;

        MPPlayer bound;
        float baseLevel;
        float spike;
        int attacker = MPTeams.Neutral;

        void OnDisable() => Bind(null);

        void Bind(MPPlayer player)
        {
            if (bound == player) return;
            if (bound != null)
            {
                bound.Capture.HitReceived -= OnHit;
                bound.Converted -= OnConverted;
            }
            bound = player;
            baseLevel = spike = 0f;
            if (bound == null) return;
            bound.Capture.HitReceived += OnHit;
            bound.Converted += OnConverted;
        }

        void OnHit(int team)
        {
            if (team == bound.Team) return;
            attacker = team;
            spike = Mathf.Min(1f, spike + hitSpike);
        }

        void OnConverted(int previous, int current)
        {
            spike = convertedSpike;
            baseLevel = 0f;
        }

        void LateUpdate()
        {
            Bind(MPPlayer.Local);
            float dt = Time.unscaledDeltaTime;
            float target = bound != null && MPMatch.InProgress ? bound.Capture.Progress * baseStrength : 0f;
            baseLevel = Mathf.Lerp(baseLevel, target, 1f - Mathf.Exp(-baseResponse * dt));
            spike = Mathf.MoveTowards(spike, 0f, dt / spikeFade);

            int team = bound != null && MPTeams.IsTeam(bound.Capture.Capturer) ? bound.Capture.Capturer : attacker;
            Color color = MPTeams.IsTeam(team) ? MPTeams.ColorOf(team) : Color.white;
            float alpha = Mathf.Min(maxAlpha, baseLevel + spike);
            image.color = new Color(color.r, color.g, color.b, alpha);
            image.enabled = alpha > 0.002f;
        }
    }
}
