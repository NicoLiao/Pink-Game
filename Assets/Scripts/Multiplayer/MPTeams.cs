using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Free-for-all teams: one per player slot (0..3). -1 means Neutral.
    // Names and colors live in the TeamStyle asset (next to each team's symbol); this is the static lookup.
    public static class MPTeams
    {
        public const int Neutral = -1;
        public const int MaxTeams = 4;

        public static Color NeutralColor => TeamStyle.Active.NeutralColor;

        public static bool IsTeam(int team) => team >= 0 && team < MaxTeams;
        public static Color ColorOf(int team) => IsTeam(team) ? TeamStyle.Active.ColorOf(team) : NeutralColor;
        public static Color TextColorOf(int team) => TeamStyle.Active.TextColorOf(IsTeam(team) ? team : Neutral);
        public static string NameOf(int team) => TeamStyle.Active.NameOf(IsTeam(team) ? team : Neutral);
    }
}
