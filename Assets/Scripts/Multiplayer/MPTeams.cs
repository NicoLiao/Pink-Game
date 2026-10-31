using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Free-for-all teams: one per player slot (0..3). -1 means Neutral.
    public static class MPTeams
    {
        public const int Neutral = -1;
        public const int MaxTeams = 4;

        // Dusty mauve-gray for characters no team owns.
        public static readonly Color NeutralColor = new Color(0.68f, 0.62f, 0.65f);

        static readonly Color[] colors =
        {
            new Color(0.96f, 0.4f, 0.62f),
            new Color(0.68f, 0.2f, 0.47f),
            new Color(1f, 0.55f, 0.46f),
            new Color(1f, 0.78f, 0.86f),
        };

        // Darker versions for text on light backgrounds (Blush is unreadable otherwise).
        static readonly Color[] textColors =
        {
            new Color(0.86f, 0.2f, 0.47f),
            new Color(0.55f, 0.1f, 0.36f),
            new Color(0.86f, 0.33f, 0.24f),
            new Color(0.78f, 0.38f, 0.55f),
        };

        // Every team is a shade of pink; TeamStyle gives each its own symbol (heart, moon, flower, diamond).
        static readonly string[] names = { "Pink", "Berry", "Coral", "Blush" };

        public static bool IsTeam(int team) => team >= 0 && team < MaxTeams;
        public static Color ColorOf(int team) => IsTeam(team) ? colors[team] : NeutralColor;
        public static Color TextColorOf(int team) => IsTeam(team) ? textColors[team] : new Color(0.45f, 0.28f, 0.42f);
        public static string NameOf(int team) => IsTeam(team) ? names[team] : "Neutral";
    }
}
