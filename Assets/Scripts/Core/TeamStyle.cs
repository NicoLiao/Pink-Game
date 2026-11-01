using UnityEngine;

namespace LoveShot
{
    // Every team is a shade of pink, so each also has its own symbol shape (on players, projectiles, NPC badges
    // and HUD swatches) to keep teams apart at a glance. This asset is the one place that pairs a team's symbol,
    // name and colors; it lives in a Resources folder so the static MPTeams helper can find it.
    [CreateAssetMenu(menuName = "LoveShot/Team Style")]
    public class TeamStyle : ScriptableObject
    {
        [Tooltip("Symbol per team slot: 0 Pink (heart), 1 Berry (moon), 2 Coral (flower), 3 Blush (diamond).")]
        [SerializeField] Sprite[] symbols;
        [Tooltip("Team display names, same order as the symbols.")]
        [SerializeField] string[] names;
        [Tooltip("Main team colors (players, projectiles, badges, swatches), same order as the symbols.")]
        [SerializeField] Color[] colors;
        [Tooltip("Darker versions for text on light backgrounds (Blush is unreadable otherwise).")]
        [SerializeField] Color[] textColors;
        [SerializeField] string neutralName = "Neutral";
        [Tooltip("Dusty mauve-gray for characters no team owns.")]
        [SerializeField] Color neutralColor = new Color(0.68f, 0.62f, 0.65f);
        [SerializeField] Color neutralTextColor = new Color(0.45f, 0.28f, 0.42f);

        const string ResourceName = "TeamStyle";
        static TeamStyle active;

        // The shared asset (Assets/Settings/Resources/TeamStyle.asset).
        public static TeamStyle Active
        {
            get
            {
                if (active == null) active = Resources.Load<TeamStyle>(ResourceName);
                return active;
            }
        }

        public Sprite SymbolOf(int slot) => symbols != null && slot >= 0 && slot < symbols.Length ? symbols[slot] : null;
        public string NameOf(int slot) => names != null && slot >= 0 && slot < names.Length ? names[slot] : neutralName;
        public Color ColorOf(int slot) => colors != null && slot >= 0 && slot < colors.Length ? colors[slot] : neutralColor;
        public Color TextColorOf(int slot) => textColors != null && slot >= 0 && slot < textColors.Length ? textColors[slot] : neutralTextColor;
        public Color NeutralColor => neutralColor;
    }
}
