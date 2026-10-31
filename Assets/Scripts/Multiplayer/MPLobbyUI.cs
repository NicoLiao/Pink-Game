using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // The private-room lobby screen: room code with Copy, player count, one row per slot (color, name,
    // HOST/YOU tags), Start Game (host only, from 2 players) and Leave. Reads the networked MPLobby.
    public class MPLobbyUI : MonoBehaviour
    {
        [SerializeField] TMP_Text codeText;
        [SerializeField] TMP_Text copyLabel;
        [SerializeField] TMP_Text countText;
        [SerializeField] UnityEngine.UI.Image[] slotSwatches;
        [SerializeField] TMP_Text[] slotNames;
        [SerializeField] TMP_Text[] slotTags;
        [SerializeField] UnityEngine.UI.Button startButton;
        [SerializeField] TMP_Text startLabel;
        [SerializeField] Color emptySlotColor = new Color(0.85f, 0.8f, 0.86f);
        [SerializeField] Color emptyTextColor = new Color(0.6f, 0.52f, 0.6f);
        [SerializeField, Min(0f)] float copiedFeedbackTime = 1.5f;

        float copiedUntil = float.NegativeInfinity;

        void Update()
        {
            MPLobby lobby = MPLobby.Instance;
            NetworkManager network = NetworkManager.Singleton;
            bool host = network != null && network.IsServer;
            int max = MPConnection.Instance != null ? MPConnection.Instance.MaxPlayers : MPTeams.MaxTeams;

            string code = lobby != null ? lobby.RoomCode : "";
            if (codeText != null) codeText.text = string.IsNullOrEmpty(code) ? "......" : code;
            if (copyLabel != null) copyLabel.text = Time.unscaledTime < copiedUntil ? "COPIED!" : "COPY";

            int count = lobby != null ? lobby.Players.Count : 0;
            if (countText != null) countText.text = $"PLAYERS {count}/{max}";

            for (int slot = 0; slot < slotNames.Length; slot++) FillSlot(lobby, network, slot);

            bool canStart = lobby != null && lobby.CanStart;
            int needed = lobby != null ? lobby.MinPlayersToStart : 2;
            if (startButton != null)
            {
                startButton.gameObject.SetActive(host);
                startButton.interactable = canStart;
            }
            if (startLabel != null) startLabel.text = canStart ? "START GAME" : $"NEED {needed}+ PLAYERS";
        }

        void FillSlot(MPLobby lobby, NetworkManager network, int slot)
        {
            bool taken = false;
            MPPlayerInfo info = default;
            if (lobby != null) taken = lobby.TryGetPlayerBySlot(slot, out info);

            if (slot < slotSwatches.Length && slotSwatches[slot] != null)
                slotSwatches[slot].color = taken ? MPTeams.ColorOf(slot) : emptySlotColor;
            TMP_Text nameLabel = slotNames[slot];
            if (nameLabel != null)
            {
                nameLabel.text = taken ? info.Name.ToString() : "Waiting for player...";
                nameLabel.color = taken ? MPTeams.TextColorOf(slot) : emptyTextColor;
                nameLabel.fontStyle = taken ? FontStyles.Bold : FontStyles.Italic;
            }
            if (slot < slotTags.Length && slotTags[slot] != null)
            {
                string tags = "";
                if (taken && info.ClientId == NetworkManager.ServerClientId) tags += "HOST ";
                if (taken && network != null && info.ClientId == network.LocalClientId) tags += "YOU";
                slotTags[slot].text = taken ? $"{MPTeams.NameOf(slot).ToUpperInvariant()}  {tags}".Trim() : "";
                slotTags[slot].color = MPTeams.TextColorOf(slot);
            }
        }

        // Buttons.
        public void CopyCode()
        {
            if (MPLobby.Instance == null || string.IsNullOrEmpty(MPLobby.Instance.RoomCode)) return;
            GUIUtility.systemCopyBuffer = MPLobby.Instance.RoomCode;
            copiedUntil = Time.unscaledTime + copiedFeedbackTime;
        }

        public void StartGame()
        {
            if (MPLobby.Instance != null) MPLobby.Instance.StartMatch();
        }

        public void Leave()
        {
            if (MPConnection.Instance != null) MPConnection.Instance.Close(null, toMultiplayerMenu: true);
        }
    }
}
