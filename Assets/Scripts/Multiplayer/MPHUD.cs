using TMPro;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Multiplayer HUD: timer, 3-2-1-GO, a live team leaderboard (members + points), your skill bar, and the results
    // screen (winning team, final points and ranks, Return to Lobby for the host, Main Menu for everyone).
    // Row i of each array belongs together (root, color swatch, name label, count label).
    public class MPHUD : MonoBehaviour
    {
        [SerializeField] TMP_Text timerText;
        [SerializeField] TMP_Text countdownText;
        [SerializeField] SkillBarUI skillBar;

        [Header("Leaderboard")]
        [SerializeField] GameObject[] boardRows;
        [SerializeField] UnityEngine.UI.Image[] boardSwatches;
        [Tooltip("Team symbols for the leaderboard swatches.")]
        [SerializeField] TeamStyle teamStyle;
        [SerializeField] TMP_Text[] boardNames;
        [SerializeField] TMP_Text[] boardCounts;

        [Header("Results")]
        [SerializeField] GameObject resultPanel;
        [SerializeField] TMP_Text resultTitle;
        [SerializeField] GameObject[] resultRows;
        [SerializeField] UnityEngine.UI.Image[] resultSwatches;
        [SerializeField] TMP_Text[] resultNames;
        [SerializeField] TMP_Text[] resultCounts;
        [SerializeField] GameObject lobbyButton;
        [SerializeField] TMP_Text waitingForHostText;
        [SerializeField] GameObject firstSelectedOnResults;

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

        static readonly string[] ordinals = { "1st", "2nd", "3rd", "4th" };

        void OnEnable() => MPPlayer.LocalPlayerSpawned += BindSkills;

        void OnDisable() => MPPlayer.LocalPlayerSpawned -= BindSkills;

        void Start()
        {
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
            FillRows(ranking, boardRows, boardSwatches, boardNames, boardCounts, withRank: false);
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
            FillRows(ranking, resultRows, resultSwatches, resultNames, resultCounts, withRank: true);

            if (resultTitle != null)
            {
                int winners = 0;
                foreach (var (_, rank) in ranking) if (rank == 1) winners++;
                if (ranking.Count == 0) resultTitle.text = "MATCH OVER";
                else if (winners > 1)
                {
                    resultTitle.text = "IT'S A TIE!";
                    resultTitle.color = timerColor;
                }
                else
                {
                    MPScore top = ranking[0].score;
                    resultTitle.text = $"{TeamName(top.Team).ToUpperInvariant()} WINS!";
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

        void FillRows(System.Collections.Generic.List<(MPScore score, int rank)> ranking, GameObject[] rows,
            UnityEngine.UI.Image[] swatches, TMP_Text[] names, TMP_Text[] counts, bool withRank)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                bool used = ranking != null && i < ranking.Count;
                if (rows[i] != null) rows[i].SetActive(used);
                if (!used) continue;

                (MPScore score, int rank) = ranking[i];
                if (i < swatches.Length && swatches[i] != null)
                {
                    swatches[i].color = MPTeams.ColorOf(score.Team);
                    Sprite symbol = teamStyle != null ? teamStyle.SymbolOf(score.Team) : null;
                    if (symbol != null) swatches[i].sprite = symbol;
                }
                if (i < names.Length && names[i] != null)
                {
                    string prefix = withRank ? $"{ordinals[Mathf.Clamp(rank - 1, 0, ordinals.Length - 1)]}  " : "";
                    // Team on the first line, its players (converted ones included) smaller underneath.
                    names[i].text = $"{prefix}{TeamName(score.Team).ToUpperInvariant()}\n<size=72%>{MembersText(score.Team)}</size>";
                    names[i].color = MPTeams.TextColorOf(score.Team);
                }
                if (i < counts.Length && counts[i] != null)
                {
                    counts[i].text = withRank ? $"{score.Points} pt{(score.Points == 1 ? "" : "s")}" : score.Points.ToString();
                    counts[i].color = MPTeams.TextColorOf(score.Team);
                }
            }
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
