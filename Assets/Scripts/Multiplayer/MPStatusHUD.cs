using UnityEngine;

namespace LoveShot.Multiplayer
{
    // HUD orb about you, styled like a skill slot: a circle in the color of the team you fight for, your own
    // symbol in the middle, and a wedge in the attacker's color that grows as another team gets closer to
    // converting you (it flashes when you're nearly gone). A small icon of the attacking team floats above
    // the orb while someone is converting you, and the glow shows while you're conversion-immune. Once you've
    // been converted, the team you now fight for is shown to the right of the orb.
    public class MPStatusHUD : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Image orb;
        [Tooltip("Filled (Radial 360) image: the capturing team's progress, in its color.")]
        [SerializeField] UnityEngine.UI.Image wedge;
        [Tooltip("Your own symbol, in the middle.")]
        [SerializeField] UnityEngine.UI.Image symbol;
        [Tooltip("Lit while you are conversion-immune.")]
        [SerializeField] UnityEngine.UI.Image shieldGlow;
        [Tooltip("The attacking team's symbol, shown above the orb while someone is converting you.")]
        [SerializeField] UnityEngine.UI.Image attackerIcon;
        [Tooltip("The team you were converted to, shown to the right of the orb (hidden while you're on your starting team).")]
        [SerializeField] UnityEngine.UI.Image teamIcon;
        [SerializeField] TeamStyle teamStyle;
        [SerializeField] Color symbolColor = Color.white;
        [SerializeField, Range(0f, 1f)] float dangerThreshold = 0.6f;

        void LateUpdate()
        {
            MPPlayer player = MPPlayer.Local;
            if (player == null) return;
            MPCapture capture = player.Capture;
            int team = player.Team, capturer = capture.Capturer;
            float progress = capturer == MPTeams.Neutral ? 0f : capture.Progress;
            bool attacked = progress > 0f && !player.IsConversionImmune;

            if (orb != null) orb.color = MPTeams.ColorOf(team);
            if (symbol != null)
            {
                symbol.color = symbolColor;
                Sprite own = teamStyle != null ? teamStyle.SymbolOf(player.Slot) : null;
                if (own != null) symbol.sprite = own;
            }
            if (wedge != null)
            {
                wedge.fillAmount = progress;
                // Flashes when you're close to being converted.
                bool danger = progress >= dangerThreshold && Mathf.Repeat(Time.unscaledTime * 5f, 1f) < 0.5f;
                wedge.color = danger ? Color.white : MPTeams.ColorOf(capturer);
            }
            if (teamIcon != null)
            {
                int start = MPLobby.Instance != null ? MPLobby.Instance.TeamOfSlot(player.Slot) : player.Slot;
                bool converted = start != team;
                teamIcon.gameObject.SetActive(converted);
                if (converted)
                {
                    teamIcon.color = MPTeams.ColorOf(team);
                    Sprite current = teamStyle != null ? teamStyle.SymbolOf(team) : null;
                    if (current != null) teamIcon.sprite = current;
                }
            }
            if (shieldGlow != null) shieldGlow.enabled = player.IsConversionImmune;
            if (attackerIcon != null)
            {
                attackerIcon.gameObject.SetActive(attacked);
                if (attacked)
                {
                    attackerIcon.color = MPTeams.ColorOf(capturer);
                    Sprite theirs = teamStyle != null ? teamStyle.SymbolOf(capturer) : null;
                    if (theirs != null) attackerIcon.sprite = theirs;
                }
            }
        }
    }
}
