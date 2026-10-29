using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LoveShot
{
    // Runs the start countdown and match timer, counts each team's NPCs, decides the winner and handles
    // restart (R) or returning to the main menu (M) once the match is over.
    public class MatchManager : MonoBehaviour
    {
        public enum Result { None, PinkWins, BlueWins, Draw }

        [SerializeField, Min(1f)] float matchDuration = 90f;
        [SerializeField] string menuSceneName = "MainMenu";
        [Tooltip("3-2-1 countdown before play starts. 0 skips it.")]
        [SerializeField, Min(0f)] float countdownDuration = 3f;

        public static MatchManager Instance { get; private set; }
        // True while gameplay should run. Scenes without a MatchManager always count as in progress.
        public static bool InProgress => Instance == null || (Instance.MatchResult == Result.None && !Instance.IsCountingDown);

        public float TimeRemaining { get; private set; }
        public float CountdownRemaining { get; private set; }
        public bool IsCountingDown => CountdownRemaining > 0f;
        // Time.time when the countdown finished and play began.
        public float PlayStartTime { get; private set; } = float.NegativeInfinity;
        public int PinkCount { get; private set; }
        public int BlueCount { get; private set; }
        public int NeutralCount { get; private set; }
        public Result MatchResult { get; private set; } = Result.None;

        public event Action<Result> MatchEnded;

        public int CountOf(Team team) => team switch
        {
            Team.Pink => PinkCount,
            Team.Blue => BlueCount,
            _ => NeutralCount,
        };

        void Awake()
        {
            Instance = this;
            TimeRemaining = matchDuration;
            CountdownRemaining = countdownDuration;
            if (!IsCountingDown) PlayStartTime = Time.time;
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (MatchResult != Result.None)
            {
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Restart();
                else if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) ReturnToMenu();
                return;
            }

            CountTeams();
            if (IsCountingDown)
            {
                CountdownRemaining = Mathf.Max(0f, CountdownRemaining - Time.deltaTime);
                if (!IsCountingDown) PlayStartTime = Time.time;
                return;
            }

            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
            if (TimeRemaining <= 0f) EndMatch();
        }

        void CountTeams()
        {
            int pink = 0, blue = 0, neutral = 0;
            foreach (NPCController npc in NPCController.All)
            {
                switch (npc.Team)
                {
                    case Team.Pink: pink++; break;
                    case Team.Blue: blue++; break;
                    default: neutral++; break;
                }
            }
            PinkCount = pink;
            BlueCount = blue;
            NeutralCount = neutral;
        }

        void EndMatch()
        {
            CountTeams();
            MatchResult = PinkCount > BlueCount ? Result.PinkWins
                : BlueCount > PinkCount ? Result.BlueWins
                : Result.Draw;
            // Freeze the arena on the final frame; Update still runs so R can restart.
            Time.timeScale = 0f;
            MatchEnded?.Invoke(MatchResult);
        }

        // The selected difficulty lives in GameSettings, so it carries over to the restarted match.
        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
