using UnityEngine;

namespace LoveShot.Multiplayer
{
    // World-space love meter over an NPC or player: the owner's remaining loyalty fills from the left in
    // the owner's color, the capturing team's progress fills from the right in its color. Runs on every machine.
    [RequireComponent(typeof(MPCapture))]
    public class MPCaptureBar : MonoBehaviour
    {
        [SerializeField] SpriteRenderer loyaltyFill;
        [SerializeField] SpriteRenderer captureFill;
        [Tooltip("World width of the full bar; must match the bar background.")]
        [SerializeField, Min(0f)] float barWidth = 1f;

        MPCapture capture;

        void Awake() => capture = GetComponent<MPCapture>();

        void LateUpdate()
        {
            int owner = capture.Owner, capturer = capture.Capturer;
            float progress = capturer == MPTeams.Neutral ? 0f : capture.Progress;
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
