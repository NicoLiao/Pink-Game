using UnityEngine;

namespace LoveShot
{
    public enum Team { Neutral, Pink, Blue }

    public static class TeamUtil
    {
        public static readonly Color PinkColor = new Color(1f, 0.45f, 0.72f);
        public static readonly Color BlueColor = new Color(0.38f, 0.66f, 1f);
        public static readonly Color NeutralColor = new Color(0.62f, 0.62f, 0.66f);

        public static Color ColorOf(Team team) => team switch
        {
            Team.Pink => PinkColor,
            Team.Blue => BlueColor,
            _ => NeutralColor,
        };

        // Affection runs from -100 (Blue) to +100 (Pink): Pink love pushes it up, Blue love pushes it down.
        public static float AffectionSign(Team team) => team switch
        {
            Team.Pink => 1f,
            Team.Blue => -1f,
            _ => 0f,
        };

        public static Team Opponent(Team team) => team switch
        {
            Team.Pink => Team.Blue,
            Team.Blue => Team.Pink,
            _ => Team.Neutral,
        };
    }
}
