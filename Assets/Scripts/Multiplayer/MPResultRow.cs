using TMPro;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // One team's row on the results screen: ORIGINAL | ZOMBIES, then the score. Every player shows as their own
    // symbol in their identity color. "Original" players are the ones who started on this team; the rest
    // were converted into it (they are shown the same way whether or not zombie mode is on).
    public class MPResultRow : MonoBehaviour
    {
        [Tooltip("Parent of the icons (a layout group), rebuilt after filling so the arrow can find its place.")]
        [SerializeField] RectTransform iconsRoot;
        [SerializeField] UnityEngine.UI.Image[] originalIcons;
        [SerializeField] UnityEngine.UI.Image[] zombieIcons;
        [Tooltip("The bar between ORIGINAL and ZOMBIES; shown only when the team has zombies.")]
        [SerializeField] GameObject divider;
        [SerializeField] TMP_Text scoreText;

        // Fills the row for `score.Team`. Returns the icon of the local player if they are on this team, else null.
        public UnityEngine.UI.Image Fill(MPScore score, TeamStyle teamStyle)
        {
            int team = score.Team;
            int originals = 0, zombies = 0;
            UnityEngine.UI.Image local = null;
            foreach (MPPlayer member in MPMatch.MembersOf(team))
            {
                bool original = StartTeamOf(member.Slot) == team;
                UnityEngine.UI.Image[] pool = original ? originalIcons : zombieIcons;
                int index = original ? originals++ : zombies++;
                if (index >= pool.Length) continue;
                UnityEngine.UI.Image icon = pool[index];
                icon.gameObject.SetActive(true);
                icon.color = MPTeams.ColorOf(member.Slot);
                Sprite symbol = teamStyle != null ? teamStyle.SymbolOf(member.Slot) : null;
                if (symbol != null) icon.sprite = symbol;
                if (member == MPPlayer.Local) local = icon;
            }
            HideFrom(originalIcons, originals);
            HideFrom(zombieIcons, zombies);
            if (divider != null) divider.SetActive(zombies > 0);

            if (scoreText != null)
            {
                scoreText.text = score.Points.ToString();
                scoreText.color = MPTeams.TextColorOf(team);
            }
            if (iconsRoot != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(iconsRoot);
            return local;
        }

        static int StartTeamOf(int slot) => MPLobby.Instance != null ? MPLobby.Instance.TeamOfSlot(slot) : slot;

        static void HideFrom(UnityEngine.UI.Image[] icons, int used)
        {
            for (int i = used; i < icons.Length; i++)
                if (icons[i] != null) icons[i].gameObject.SetActive(false);
        }
    }
}
