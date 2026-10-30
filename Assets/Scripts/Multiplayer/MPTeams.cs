using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Free-for-all teams: one per player slot (0..3). -1 means Neutral.
    public static class MPTeams
    {
        public const int Neutral = -1;
        public const int MaxTeams = 4;

        static readonly Color[] colors =
        {
            TeamUtil.PinkColor,
            TeamUtil.BlueColor,
            new Color(0.68f, 0.48f, 0.95f),
            new Color(1f, 0.8f, 0.22f),
        };

        // Darker versions for text on light backgrounds (yellow is unreadable otherwise).
        static readonly Color[] textColors =
        {
            new Color(0.9f, 0.25f, 0.55f),
            new Color(0.2f, 0.45f, 0.9f),
            new Color(0.5f, 0.28f, 0.82f),
            new Color(0.8f, 0.55f, 0.02f),
        };

        static readonly string[] names = { "Pink", "Blue", "Purple", "Yellow" };

        public static bool IsTeam(int team) => team >= 0 && team < MaxTeams;
        public static Color ColorOf(int team) => IsTeam(team) ? colors[team] : TeamUtil.NeutralColor;
        public static Color TextColorOf(int team) => IsTeam(team) ? textColors[team] : new Color(0.45f, 0.28f, 0.42f);
        public static string NameOf(int team) => IsTeam(team) ? names[team] : "Neutral";
    }
}
