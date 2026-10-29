using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LoveShot
{
    // Main menu. Buttons call these public methods from their OnClick.
    // To add Settings / Credits later: add a panel to `panels` and a button that calls ShowPanel(index),
    // with a Back button on that panel calling ShowPanel(0).
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] string gameSceneName = "LoveShotArena";
        [Tooltip("Menu pages, shown one at a time. Element 0 is the main page.")]
        [SerializeField] GameObject[] panels;
        [Tooltip("Selected on open so keyboard/gamepad Enter works without the mouse.")]
        [SerializeField] GameObject firstSelected;

        [Header("Difficulty")]
        [Tooltip("Selectable difficulties in cycling order. Element 0 is the default. Add Normal etc. here.")]
        [SerializeField] AIDifficulty[] difficulties;
        [SerializeField] TMP_Text difficultyLabel;
        [Tooltip("Optional: tinted with the selected difficulty's MenuColor.")]
        [SerializeField] UnityEngine.UI.Graphic difficultyBackground;

        int difficultyIndex;

        void Start()
        {
            Time.timeScale = 1f;
            // Coming back from a match keeps the last choice; a fresh launch starts on the default.
            int remembered = System.Array.IndexOf(difficulties, GameSettings.SelectedDifficulty);
            SelectDifficulty(remembered >= 0 ? remembered : 0);
            ShowPanel(0);
        }

        public void Play()
        {
            if (difficulties.Length > 0 && difficulties[difficultyIndex] != null)
                GameSettings.SelectedDifficulty = difficulties[difficultyIndex];
            SceneManager.LoadScene(gameSceneName);
        }

        public void CycleDifficulty() => SelectDifficulty(difficultyIndex + 1);

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ShowPanel(int index)
        {
            for (int i = 0; i < panels.Length; i++)
                if (panels[i] != null) panels[i].SetActive(i == index);

            if (index == 0 && firstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstSelected);
        }

        void SelectDifficulty(int index)
        {
            if (difficulties == null || difficulties.Length == 0) return;
            difficultyIndex = (index % difficulties.Length + difficulties.Length) % difficulties.Length;
            AIDifficulty difficulty = difficulties[difficultyIndex];
            if (difficulty == null) return;
            GameSettings.SelectedDifficulty = difficulty;
            if (difficultyLabel != null) difficultyLabel.text = $"DIFFICULTY: {difficulty.DisplayName.ToUpperInvariant()}";
            if (difficultyBackground != null) difficultyBackground.color = difficulty.MenuColor;
        }
    }
}
