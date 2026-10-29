using TMPro;
using UnityEngine;

namespace LoveShot
{
    // Shows the Pink vs Blue counter, the timer, the start countdown, and the result banner.
    public class MatchHUD : MonoBehaviour
    {
        [SerializeField] MatchManager match;
        [SerializeField] TMP_Text pinkCountText;
        [SerializeField] TMP_Text blueCountText;
        [SerializeField] TMP_Text timerText;
        [SerializeField] TMP_Text countdownText;
        [SerializeField] GameObject resultPanel;
        [SerializeField] TMP_Text resultText;

        [Header("Style")]
        [SerializeField] Color pinkTextColor = new Color(0.9f, 0.25f, 0.55f);
        [SerializeField] Color blueTextColor = new Color(0.2f, 0.45f, 0.9f);
        [SerializeField] Color timerColor = new Color(0.35f, 0.25f, 0.35f);
        [SerializeField] Color timerWarningColor = new Color(0.95f, 0.2f, 0.35f);
        [SerializeField, Min(0f)] float warningTime = 10f;
        [Tooltip("How much a team's counter grows when its number changes.")]
        [SerializeField, Min(0f)] float scorePunch = 0.35f;
        [SerializeField, Min(0.01f)] float punchDuration = 0.25f;
        [SerializeField, Min(0f)] float goDisplayTime = 0.7f;

        int shownPink = -1, shownBlue = -1;
        float pinkPunchTime = float.NegativeInfinity, bluePunchTime = float.NegativeInfinity;

        void Awake()
        {
            if (match == null) match = FindAnyObjectByType<MatchManager>();
            if (resultPanel != null) resultPanel.SetActive(false);
        }

        void OnEnable()
        {
            if (match != null) match.MatchEnded += ShowResult;
        }

        void OnDisable()
        {
            if (match != null) match.MatchEnded -= ShowResult;
        }

        void Update()
        {
            if (match == null) return;

            UpdateCounter(pinkCountText, match.PinkCount, ref shownPink, ref pinkPunchTime, n => $"PINK {n}");
            UpdateCounter(blueCountText, match.BlueCount, ref shownBlue, ref bluePunchTime, n => $"{n} BLUE");

            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(match.TimeRemaining);
                bool warning = match.TimeRemaining <= warningTime && match.MatchResult == MatchManager.Result.None;
                timerText.text = $"{seconds / 60}:{seconds % 60:00}";
                timerText.color = warning ? timerWarningColor : timerColor;
                // Tick pulse once per second during the final seconds.
                float tick = warning ? 1f - Mathf.Repeat(match.TimeRemaining, 1f) : 1f;
                timerText.transform.localScale = Vector3.one * (1f + 0.2f * (1f - tick));
            }

            if (countdownText != null)
            {
                bool showGo = !match.IsCountingDown && Time.time - match.PlayStartTime < goDisplayTime;
                countdownText.gameObject.SetActive(match.IsCountingDown || showGo);
                countdownText.text = match.IsCountingDown ? Mathf.CeilToInt(match.CountdownRemaining).ToString() : "GO!";
            }
        }

        void UpdateCounter(TMP_Text label, int value, ref int shown, ref float punchTime, System.Func<int, string> format)
        {
            if (label == null) return;
            if (value != shown)
            {
                if (shown >= 0) punchTime = Time.unscaledTime;
                shown = value;
                label.text = format(value);
            }
            float t = (Time.unscaledTime - punchTime) / punchDuration;
            label.transform.localScale = Vector3.one * (1f + (t < 1f ? scorePunch * (1f - t) : 0f));
        }

        void ShowResult(MatchManager.Result result)
        {
            if (resultPanel == null || resultText == null) return;

            (string title, Color color) = result switch
            {
                MatchManager.Result.PinkWins => ("PINK WINS!", pinkTextColor),
                MatchManager.Result.BlueWins => ("BLUE WINS!", blueTextColor),
                _ => ("DRAW!", timerColor),
            };
            resultText.color = color;
            resultText.text = $"{title}\n<size=45%><color=#{ColorUtility.ToHtmlStringRGB(timerColor)}>" +
                              $"Pink {match.PinkCount} - {match.BlueCount} Blue\nR: play again   M: main menu</color></size>";
            resultPanel.SetActive(true);
        }
    }
}
