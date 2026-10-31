using UnityEngine;
using UnityEngine.EventSystems;

namespace LoveShot
{
    // Main menu page switching and Quit. The online buttons (Host / Join) are driven by MultiplayerMenu.
    // To add Settings / Credits later: add a panel to `panels` and a button that calls ShowPanel(index), with a
    // Back button on that panel calling ShowPanel(0).
    public class MainMenu : MonoBehaviour
    {
        [Tooltip("Menu pages, shown one at a time. Element 0 is the main page.")]
        [SerializeField] GameObject[] panels;
        [Tooltip("Per page (same index as panels): selected on open so keyboard/gamepad Enter works without the mouse.")]
        [SerializeField] GameObject[] panelFirstSelected;

        void Start() => ShowPanel(0);

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

            GameObject selected = panelFirstSelected != null && index < panelFirstSelected.Length ? panelFirstSelected[index] : null;
            if (selected != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selected);
        }
    }
}
