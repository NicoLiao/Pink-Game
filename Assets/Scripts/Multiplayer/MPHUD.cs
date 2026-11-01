using TMPro;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Multiplayer HUD: timer, 3-2-1-GO, a live team leaderboard (members + points), your skill bar, and the results
    // screen (winning team; one ORIGINAL | ZOMBIES + score row per remaining team with an arrow over you; Return
    // to Lobby for the host, Main Menu for everyone). Row i of the leaderboard arrays belongs together.
    public class MPHUD : MonoBehaviour
    {
        [SerializeField] TMP_Text timerText;
        [SerializeField] TMP_Text countdownText;
        [SerializeField] SkillBarUI skillBar;

        [Header("Leaderboard")]
        [Tooltip("One row per possible team is instantiated under Board Root (give it a layout group).")]
        [SerializeField] MPLeaderboardRow boardRowPrefab;
        [SerializeField] Transform boardRoot;
        [Tooltip("Team symbols for the leaderboard swatches and result icons.")]
        [SerializeField] TeamStyle teamStyle;

        [Header("Results")]
        [SerializeField] GameObject resultPanel;
        [SerializeField] TMP_Text resultTitle;
        [Tooltip("One row per possible team is instantiated under Result Rows Root (give it a layout group).")]
        [SerializeField] MPResultRow resultRowPrefab;
        [SerializeField] Transform resultRowsRoot;
        [Tooltip("Arrow shown above your own icon on the results screen (pivot at its bottom).")]
        [SerializeField] RectTransform youArrow;
        [SerializeField] GameObject lobbyButton;
        [SerializeField] TMP_Text waitingForHostText;
        [SerializeField] GameObject firstSelectedOnResults;

        [Header("Text")]
        [SerializeField] string matchOverText = "MATCH OVER";
        [SerializeField] string tieText = "IT'S A TIE!";
        [Tooltip("{0} = winning team name.")]
        [SerializeField] string winsFormat = "{0} WINS!";

        [Header("Readability")]
        [Tooltip("HUD panels that turn see-through while your own character is behind them.")]
        [SerializeField] CanvasGroup[] fadeWhenCovering;
        [SerializeField, Range(0f, 1f)] float coveredAlpha = 0.3f;
        [SerializeField, Min(0f)] float fadeSpeed = 6f;

        [Header("Style")]
        [SerializeField] Color timerColor = new Color(0.35f, 0.25f, 0.35f);
        [SerializeField] Color timerWarningColor = new Color(0.95f, 0.2f, 0.35f);
        [SerializeField, Min(0f)] float warningTime = 10f;
        [SerializeField, Min(0f)] float goDisplayTime = 0.7f;
        [SerializeField, Min(0f)] float resultFadeTime = 0.35f;

        float playStartTime = float.NegativeInfinity;
        float resultShownTime = float.NegativeInfinity;
        MPMatch boundMatch;
        MPLeaderboardRow[] boardRows;
        MPResultRow[] resultRows;

        void OnEnable() => MPPlayer.LocalPlayerSpawned += BindSkills;

        void OnDisable() => MPPlayer.LocalPlayerSpawned -= BindSkills;

        void Start()
        {
            boardRows = new MPLeaderboardRow[MPTeams.MaxTeams];
            resultRows = new MPResultRow[MPTeams.MaxTeams];
            for (int i = 0; i < MPTeams.MaxTeams; i++)
            {
                if (boardRowPrefab != null && boardRoot != null) boardRows[i] = Instantiate(boardRowPrefab, boardRoot);
                if (resultRowPrefab != null && resultRowsRoot != null) resultRows[i] = Instantiate(resultRowPrefab, resultRowsRoot);
            }
            if (resultPanel != null) resultPanel.SetActive(false);
            if (MPPlayer.Local != null) BindSkills(MPPlayer.Local);
        }

        void BindSkills(MPPlayer player)
        {
            if (skillBar != null) skillBar.Bind(player.gameObject);
        }

        void Update()
        {
            MPMatch match = MPMatch.Instance;
            if (match != boundMatch)
            {
                if (boundMatch != null) boundMatch.PhaseChanged -= OnPhaseChanged;
                boundMatch = match;
                if (match != null) match.PhaseChanged += OnPhaseChanged;
            }

            UpdateTimer(match);
            UpdateCountdown(match);
            UpdateLeaderboard(match);
            if (match != null && match.Phase == MPMatch.MatchPhase.Ended) ShowResults(match);
            FadeCoveringPanels();
        }

        void FadeCoveringPanels()
        {
            if (fadeWhenCovering == null) return;
            Camera world = Camera.main;
            Vector2? screen = MPPlayer.Local != null && world != null ? world.WorldToScreenPoint(MPPlayer.Local.transform.position) : null;
            foreach (CanvasGroup group in fadeWhenCovering)
            {
                if (group == null) continue;
                Canvas canvas = group.GetComponentInParent<Canvas>();
                Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                bool covering = screen.HasValue &&
                                RectTransformUtility.RectangleContainsScreenPoint((RectTransform)group.transform, screen.Value, uiCamera);
                group.alpha = Mathf.MoveTowards(group.alpha, covering ? coveredAlpha : 1f, fadeSpeed * Time.unscaledDeltaTime);
            }
        }

        void OnPhaseChanged(MPMatch.MatchPhase phase)
        {
            if (phase == MPMatch.MatchPhase.Playing) playStartTime = Time.unscaledTime;
        }

        void UpdateTimer(MPMatch match)
        {
            if (timerText == null) return;
            float remaining = match != null ? match.TimeRemaining : 0f;
            int seconds = Mathf.CeilToInt(remaining);
            timerText.text = $"{seconds / 60}:{seconds % 60:00}";
            bool warning = match != null && match.Phase == MPMatch.MatchPhase.Playing && remaining <= warningTime;
            timerText.color = warning ? timerWarningColor : timerColor;
            float tick = warning ? 1f - Mathf.Repeat(remaining, 1f) : 1f;
            timerText.transform.localScale = Vector3.one * (1f + 0.2f * (1f - tick));
        }

        void UpdateCountdown(MPMatch match)
        {
            if (countdownText == null) return;
            string text = null;
            if (match == null || match.Phase == MPMatch.MatchPhase.Waiting) text = "<size=40%>Waiting for players...</size>";
            else if (match.Phase == MPMatch.MatchPhase.Countdown) text = Mathf.Max(1, Mathf.CeilToInt(match.CountdownRemaining)).ToString();
            else if (match.Phase == MPMatch.MatchPhase.Playing && Time.unscaledTime - playStartTime < goDisplayTime) text = "GO!";
            countdownText.gameObject.SetActive(text != null);
            if (text != null) countdownText.text = text;
        }

        void UpdateLeaderboard(MPMatch match)
        {
            var ranking = match != null ? match.Ranking() : null;
            if (boardRows == null) return;
            for (int i = 0; i < boardRows.Length; i++)
            {
                if (boardRows[i] == null) continue;
                bool used = ranking != null && i < ranking.Count;
                boardRows[i].gameObject.SetActive(used);
                if (!used) continue;
                MPScore score = ranking[i].score;
                // Team on the first line, its players (converted ones included) smaller underneath.
                boardRows[i].Show(teamStyle != null ? teamStyle.SymbolOf(score.Team) : null, MPTeams.ColorOf(score.Team),
                    $"{TeamName(score.Team).ToUpperInvariant()}\n<size=72%>{MembersText(score.Team)}</size>",
                    score.Points.ToString(), MPTeams.TextColorOf(score.Team));
            }
        }

        void ShowResults(MPMatch match)
        {
            if (resultPanel == null) return;
            if (!resultPanel.activeSelf)
            {
                resultPanel.SetActive(true);
                resultShownTime = Time.unscaledTime;
                if (firstSelectedOnResults != null && UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(firstSelectedOnResults);
            }

            var ranking = match.Ranking();
            FillResultRows(ranking);

            if (resultTitle != null)
            {
                int winners = 0;
                foreach (var (_, rank) in ranking) if (rank == 1) winners++;
                if (ranking.Count == 0) resultTitle.text = matchOverText;
                else if (winners > 1)
                {
                    resultTitle.text = tieText;
                    resultTitle.color = timerColor;
                }
                else
                {
                    MPScore top = ranking[0].score;
                    resultTitle.text = string.Format(winsFormat, TeamName(top.Team).ToUpperInvariant());
                    resultTitle.color = MPTeams.TextColorOf(top.Team);
                }
            }

            bool host = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer;
            if (lobbyButton != null) lobbyButton.SetActive(host);
            if (waitingForHostText != null) waitingForHostText.gameObject.SetActive(!host);

            // Fade/scale the banner in.
            float t = Mathf.Clamp01((Time.unscaledTime - resultShownTime) / Mathf.Max(resultFadeTime, 0.01f));
            resultPanel.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, 1f - (1f - t) * (1f - t));
        }

        void FillResultRows(System.Collections.Generic.List<(MPScore score, int rank)> ranking)
        {
            if (resultRows == null) return;
            UnityEngine.UI.Image local = null;
            for (int i = 0; i < resultRows.Length; i++)
            {
                if (resultRows[i] == null) continue;
                bool used = i < ranking.Count;
                resultRows[i].gameObject.SetActive(used);
                if (!used) continue;
                UnityEngine.UI.Image icon = resultRows[i].Fill(ranking[i].score, teamStyle);
                if (icon != null) local = icon;
            }
            if (youArrow == null) return;
            youArrow.gameObject.SetActive(local != null);
            if (local == null) return;
            // Just above the top edge of your icon.
            var corners = new Vector3[4];
            ((RectTransform)local.transform).GetWorldCorners(corners);
            youArrow.position = (corners[1] + corners[2]) * 0.5f;
        }

        // "Alice" for a solo team, "Alice + Bob" once players have been converted into it.
        static string MembersText(int team)
        {
            var members = MPMatch.MembersOf(team);
            if (members.Count == 0) return TeamName(team);
            var text = new System.Text.StringBuilder();
            foreach (MPPlayer member in members)
            {
                if (text.Length > 0) text.Append(" + ");
                text.Append(member.DisplayName);
                if (member == MPPlayer.Local) text.Append(" (you)");
            }
            return text.ToString();
        }

        static string TeamName(int team) => $"{MPTeams.NameOf(team)} team";

        // Buttons.
        public void ReturnToLobby()
        {
            if (MPLobby.Instance != null) MPLobby.Instance.ReturnToLobby();
        }

        public void ReturnToMainMenu()
        {
            if (MPConnection.Instance != null) MPConnection.Instance.Close(null, toMultiplayerMenu: false);
            else UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }
}
