using TMPro;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // HUD panel about you: your name, the team you currently fight for (with its color), your own love meter
    // (how close another team is to converting you) and the conversion-immunity countdown.
    public class MPStatusHUD : MonoBehaviour
    {
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text teamText;
        [SerializeField] UnityEngine.UI.Image teamSwatch;
        [SerializeField] TeamStyle teamStyle;
        [Tooltip("Filled (Horizontal, from the left): your remaining loyalty, in your team's color.")]
        [SerializeField] UnityEngine.UI.Image loyaltyFill;
        [Tooltip("Filled (Horizontal, from the right): the capturing team's progress, in its color.")]
        [SerializeField] UnityEngine.UI.Image captureFill;
        [SerializeField] TMP_Text meterText;
        [SerializeField, Range(0f, 1f)] float dangerThreshold = 0.6f;

        void LateUpdate()
        {
            MPPlayer player = MPPlayer.Local;
            if (player == null) return;
            MPCapture capture = player.Capture;
            int team = player.Team, capturer = capture.Capturer;
            float progress = capturer == MPTeams.Neutral ? 0f : capture.Progress;

            if (nameText != null) nameText.text = $"YOU: {player.DisplayName}";
            if (teamText != null)
            {
                teamText.text = (MPLobby.Instance != null ? MPLobby.Instance.TeamOfSlot(player.Slot) : player.Slot) == team ? $"{MPTeams.NameOf(team).ToUpperInvariant()} TEAM" : $"NOW {MPTeams.NameOf(team).ToUpperInvariant()} TEAM";
                teamText.color = MPTeams.TextColorOf(team);
            }
            if (teamSwatch != null)
            {
                teamSwatch.color = MPTeams.ColorOf(team);
                Sprite symbol = teamStyle != null ? teamStyle.SymbolOf(team) : null;
                if (symbol != null) teamSwatch.sprite = symbol;
            }
            if (loyaltyFill != null)
            {
                loyaltyFill.fillAmount = 1f - progress;
                loyaltyFill.color = MPTeams.ColorOf(team);
            }
            if (captureFill != null)
            {
                captureFill.fillAmount = progress;
                // Flashes when you're close to being converted.
                bool danger = progress >= dangerThreshold && Mathf.Repeat(Time.unscaledTime * 5f, 1f) < 0.5f;
                captureFill.color = danger ? Color.white : MPTeams.ColorOf(capturer);
            }
            if (meterText != null)
            {
                if (player.IsConversionImmune) meterText.text = $"SAFE {player.ConversionImmunityRemaining:0.0}s";
                else if (progress > 0f) meterText.text = $"{MPTeams.NameOf(capturer).ToUpperInvariant()} {Mathf.RoundToInt(progress * 100f)}%";
                else meterText.text = "LOYAL";
                meterText.color = progress > 0f && !player.IsConversionImmune ? MPTeams.TextColorOf(capturer) : MPTeams.TextColorOf(team);
            }
        }
    }
}
