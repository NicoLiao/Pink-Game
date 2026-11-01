using TMPro;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // The main menu's online pages: Host Game and Join Game on the main page, then room code entry.
    // Creates the NetworkManager (with MPConnection) on demand and shows progress and error messages.
    // Runs after MainMenu so reopening this page on return from a session isn't overridden by its default page.
    [DefaultExecutionOrder(100)]
    public class MultiplayerMenu : MonoBehaviour
    {
        [SerializeField] MainMenu mainMenu;
        [Tooltip("NetworkManager prefab with UnityTransport and MPConnection.")]
        [SerializeField] MPConnection networkPrefab;
        [SerializeField, Min(0)] int multiplayerPanel = 0;
        [SerializeField, Min(0)] int joinPanel = 1;
        [SerializeField] TMP_Text multiplayerStatus;
        [SerializeField] TMP_Text joinStatus;
        [SerializeField] TMP_InputField codeInput;
        [Tooltip("Player name field on the main page; remembered between sessions.")]
        [SerializeField] TMP_InputField nameInput;
        [Tooltip("Buttons disabled while a request is running.")]
        [SerializeField] UnityEngine.UI.Selectable[] busyDisabled;
        [SerializeField] Color errorColor = new Color(0.9f, 0.2f, 0.35f);
        [SerializeField] Color infoColor = new Color(0.45f, 0.28f, 0.42f);

        bool busy;

        void Start()
        {
            SetStatus(multiplayerStatus, "", false);
            SetStatus(joinStatus, "", false);
            if (nameInput != null) nameInput.text = MPConnection.SavedName;
            if (!MPConnection.ReturnToMultiplayerMenu) return;

            // Back from a session (left, host quit, kicked...): reopen this page with the reason.
            string message = MPConnection.PendingMessage;
            MPConnection.ReturnToMultiplayerMenu = false;
            MPConnection.PendingMessage = null;
            mainMenu.ShowPanel(multiplayerPanel);
            if (!string.IsNullOrEmpty(message)) SetStatus(multiplayerStatus, message, true);
        }

        public async void Host()
        {
            if (busy) return;
            SetBusy(true);
            SetStatus(multiplayerStatus, "Creating a private room...", false);
            MPConnection connection = MPConnection.Ensure(networkPrefab);
            connection.SetLocalName(nameInput != null ? nameInput.text : "");
            string error = await connection.HostAsync();
            if (this == null) return;
            if (error != null) Fail(multiplayerStatus, error);
        }

        public void OpenJoin()
        {
            if (busy) return;
            SetStatus(joinStatus, "", false);
            mainMenu.ShowPanel(joinPanel);
            if (codeInput != null)
            {
                codeInput.text = "";
                codeInput.ActivateInputField();
            }
        }

        public async void Join()
        {
            if (busy) return;
            string code = codeInput != null ? codeInput.text : "";
            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatus(joinStatus, "Enter a room code.", true);
                return;
            }
            SetBusy(true);
            SetStatus(joinStatus, "Joining...", false);
            MPConnection connection = MPConnection.Ensure(networkPrefab);
            connection.SetLocalName(nameInput != null ? nameInput.text : "");
            string error = await connection.JoinAsync(code);
            if (this == null) return;
            if (error != null) Fail(joinStatus, error);
            else SetStatus(joinStatus, "Connected! Loading the lobby...", false);
        }

        public void BackToMultiplayer()
        {
            if (!busy) mainMenu.ShowPanel(multiplayerPanel);
        }

        void Fail(TMP_Text label, string error)
        {
            // Drop the half-started connection so the next attempt starts clean.
            if (MPConnection.Instance != null) Destroy(MPConnection.Instance.gameObject);
            SetBusy(false);
            SetStatus(label, error, true);
        }

        void SetBusy(bool value)
        {
            busy = value;
            if (busyDisabled == null) return;
            foreach (UnityEngine.UI.Selectable selectable in busyDisabled)
                if (selectable != null) selectable.interactable = !value;
        }

        void SetStatus(TMP_Text label, string text, bool error)
        {
            if (label == null) return;
            label.text = text;
            label.color = error ? errorColor : infoColor;
        }
    }
}
