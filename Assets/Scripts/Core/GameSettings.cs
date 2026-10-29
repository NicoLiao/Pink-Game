using UnityEngine;

namespace LoveShot
{
    // Choices made in the main menu that carry into the match (and survive restarts) for this play session.
    public static class GameSettings
    {
        // Null until the menu sets it; the arena then falls back to MiniBossAI's default difficulty.
        public static AIDifficulty SelectedDifficulty { get; set; }

        // Statics survive between Editor play sessions when domain reload is off, so clear them on start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForNewSession() => SelectedDifficulty = null;
    }
}
