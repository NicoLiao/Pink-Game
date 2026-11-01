using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // The private-room lobby screen: room code with Copy, player count, the row of four team icons (washed out
    // while nobody holds them, an arrow marks yours), separator lines between teams, the host's FFA/2v2 button and
    // swap arrows, Start Game (host only, needs valid teams) and Leave. Reads the networked MPLobby.
    public class MPLobbyUI : MonoBehaviour
    {
        [SerializeField] TMP_Text codeText;
        [SerializeField] TMP_Text copyLabel;
        [SerializeField] TMP_Text countText;
        [SerializeField] TeamStyle teamStyle;
        [Tooltip("The four icons, left to right.")]
        [SerializeField] UnityEngine.UI.Image[] icons;
        [Tooltip("The player name under each icon position (empty while nobody holds it).")]
        [SerializeField] TMP_Text[] playerNames;
        [Tooltip("Arrow above each icon position; shown over your own icon.")]
        [SerializeField] GameObject[] youMarkers;
        [Tooltip("Lines between neighbouring icons (3): all show in FFA, only the middle one in 2v2.")]
        [SerializeField] GameObject[] separators;
        [Tooltip("Host-only buttons between neighbouring icons (3) that swap the two.")]
        [SerializeField] UnityEngine.UI.Button[] swapButtons;
        [SerializeField] UnityEngine.UI.Button modeButton;
        [Tooltip("Inside the mode button: the icon for the current mode is instantiated here, one per mode.")]
        [SerializeField] RectTransform modeIconRoot;
        [SerializeField] MPModeIcon freeForAllIconPrefab;
        [SerializeField] MPModeIcon twoVsTwoIconPrefab;
        [Tooltip("Host toggle for zombie mode (FFA only: converted players walk beside their lead).")]
        [SerializeField] UnityEngine.UI.Button zombieButton;
        [SerializeField] TMP_Text zombieLabel;
        [Tooltip("Your ability picker: sits under your own icon in the row (only you see it). Two orbs (Space, Shift); clicking one opens a row of every ability below.")]
        [SerializeField] RectTransform abilityPicker;
        [SerializeField] RectTransform abilitySlotRoot;
        [SerializeField] RectTransform abilityOptionRoot;
        [SerializeField] MPAbilityOrb abilityOrbPrefab;
        [Tooltip("Label under each of your two orbs.")]
        [SerializeField] string[] abilityKeyLabels = { "SPACE", "SHIFT" };
        [SerializeField] UnityEngine.UI.Button startButton;
        [SerializeField] TMP_Text startLabel;
        [SerializeField, Range(0f, 1f)] float emptyIconAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] float emptyIconWash = 0.6f;
        [SerializeField, Min(0f)] float copiedFeedbackTime = 1.5f;

        [Header("Text")]
        [SerializeField] string codePlaceholder = "......";
        [SerializeField] string copyText = "COPY";
        [SerializeField] string copiedText = "COPIED!";
        [Tooltip("{0} = players, {1} = max players.")]
        [SerializeField] string countFormat = "PLAYERS {0}/{1}";
        [SerializeField] string zombieOnText = "ZOMBIES: ON";
        [SerializeField] string zombieOffText = "ZOMBIES: OFF";
        [SerializeField] string startText = "START GAME";
        [Tooltip("{0} = players needed.")]
        [SerializeField] string needPlayersFormat = "NEED {0}+ PLAYERS";
        [SerializeField] string needBothSidesText = "NEED BOTH SIDES";

        float copiedUntil = float.NegativeInfinity;
        MPModeIcon freeForAllIcon;
        MPModeIcon twoVsTwoIcon;
        readonly System.Collections.Generic.List<MPAbilityOrb> slotOrbs = new();
        readonly System.Collections.Generic.List<MPAbilityOrb> optionOrbs = new();
        int openAbilitySlot = -1;

        void Awake()
        {
            for (int i = 0; i < swapButtons.Length; i++)
            {
                int position = i;
                swapButtons[i].onClick.AddListener(() => Swap(position));
            }
            if (modeButton != null) modeButton.onClick.AddListener(ToggleMode);
            if (modeIconRoot != null)
            {
                if (freeForAllIconPrefab != null) freeForAllIcon = Instantiate(freeForAllIconPrefab, modeIconRoot);
                if (twoVsTwoIconPrefab != null) twoVsTwoIcon = Instantiate(twoVsTwoIconPrefab, modeIconRoot);
                foreach (MPModeIcon icon in new[] { freeForAllIcon, twoVsTwoIcon })
                {
                    if (icon == null) continue;
                    var rect = (RectTransform)icon.transform;
                    rect.anchoredPosition = Vector2.zero;
                    rect.localScale = Vector3.one * (modeIconRoot.rect.width > 0f ? Mathf.Min(modeIconRoot.rect.width, modeIconRoot.rect.height) / 100f : 1f);
                }
            }
            if (zombieButton != null) zombieButton.onClick.AddListener(ToggleZombies);
            if (abilityOrbPrefab != null && abilitySlotRoot != null && abilityOptionRoot != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    int abilitySlot = i;
                    MPAbilityOrb orb = Instantiate(abilityOrbPrefab, abilitySlotRoot);
                    orb.Bind(() => openAbilitySlot = openAbilitySlot == abilitySlot ? -1 : abilitySlot);
                    slotOrbs.Add(orb);
                }
                for (int i = 0; i < System.Enum.GetValues(typeof(MPAbility)).Length; i++)
                {
                    int option = i;
                    MPAbilityOrb orb = Instantiate(abilityOrbPrefab, abilityOptionRoot);
                    orb.Bind(() => ChooseAbility(option));
                    optionOrbs.Add(orb);
                }
            }
        }

        void Update()
        {
            MPLobby lobby = MPLobby.Instance;
            NetworkManager network = NetworkManager.Singleton;
            bool host = network != null && network.IsServer;
            int max = MPConnection.Instance != null ? MPConnection.Instance.MaxPlayers : MPTeams.MaxTeams;

            string code = lobby != null ? lobby.RoomCode : "";
            if (codeText != null) codeText.text = string.IsNullOrEmpty(code) ? codePlaceholder : code;
            if (copyLabel != null) copyLabel.text = Time.unscaledTime < copiedUntil ? copiedText : copyText;

            int count = lobby != null ? lobby.Players.Count : 0;
            if (countText != null) countText.text = string.Format(countFormat, count, max);

            bool twoVsTwo = lobby != null && lobby.Mode == MPLobby.TeamMode.TwoVsTwo;
            for (int position = 0; position < icons.Length; position++) FillIcon(lobby, network, position);
            for (int gap = 0; gap < separators.Length; gap++)
                if (separators[gap] != null) separators[gap].SetActive(!twoVsTwo || gap == 1);
            foreach (UnityEngine.UI.Button swap in swapButtons) swap.gameObject.SetActive(host);
            if (modeButton != null) modeButton.interactable = host;
            if (freeForAllIcon != null) freeForAllIcon.gameObject.SetActive(!twoVsTwo);
            if (twoVsTwoIcon != null) twoVsTwoIcon.gameObject.SetActive(twoVsTwo);
            if (zombieButton != null)
            {
                zombieButton.gameObject.SetActive(!twoVsTwo);
                zombieButton.interactable = host;
            }
            if (zombieLabel != null) zombieLabel.text = lobby != null && lobby.ZombieMode ? zombieOnText : zombieOffText;

            ShowAbility(lobby, network);

            bool canStart = lobby != null && lobby.CanStart;
            int needed = lobby != null ? lobby.MinPlayersToStart : 2;
            if (startButton != null)
            {
                startButton.gameObject.SetActive(host);
                startButton.interactable = canStart;
            }
            if (startLabel != null)
            {
                bool enoughPlayers = count >= needed;
                startLabel.text = canStart ? startText : enoughPlayers ? needBothSidesText : string.Format(needPlayersFormat, needed);
            }
        }

        void FillIcon(MPLobby lobby, NetworkManager network, int position)
        {
            int slot = lobby != null ? lobby.SlotAt(position) : position;
            bool taken = false;
            MPPlayerInfo info = default;
            if (lobby != null) taken = lobby.TryGetPlayerBySlot(slot, out info);

            UnityEngine.UI.Image icon = icons[position];
            if (icon != null)
            {
                icon.sprite = teamStyle != null ? teamStyle.SymbolOf(slot) : icon.sprite;
                Color color = MPTeams.ColorOf(slot);
                icon.color = taken ? color : Color.Lerp(color, Color.white, emptyIconWash) * new Color(1f, 1f, 1f, emptyIconAlpha);
            }
            if (playerNames != null && position < playerNames.Length && playerNames[position] != null)
            {
                playerNames[position].text = taken ? info.Name.ToString() : "";
                playerNames[position].color = MPTeams.TextColorOf(slot);
            }
            if (position < youMarkers.Length && youMarkers[position] != null)
                youMarkers[position].SetActive(taken && network != null && info.ClientId == network.LocalClientId);
        }

        // Buttons.
        void Swap(int position)
        {
            if (MPLobby.Instance != null) MPLobby.Instance.SwapRpc(position);
        }

        void ToggleMode()
        {
            MPLobby lobby = MPLobby.Instance;
            if (lobby == null) return;
            lobby.SetModeRpc(lobby.Mode == MPLobby.TeamMode.FreeForAll ? MPLobby.TeamMode.TwoVsTwo : MPLobby.TeamMode.FreeForAll);
        }

        // Two small orbs under your own icon (Space, Shift); the open slot shows every ability as a row of orbs below.
        void ShowAbility(MPLobby lobby, NetworkManager network)
        {
            MPPlayerInfo me = default;
            bool known = lobby != null && network != null && lobby.TryGetPlayer(network.LocalClientId, out me);
            if (abilityPicker != null) abilityPicker.gameObject.SetActive(known);
            if (!known)
            {
                openAbilitySlot = -1;
                return;
            }

            int position = lobby.PositionOf(me.Slot);
            if (abilityPicker != null && position >= 0 && position < icons.Length && icons[position] != null)
            {
                Vector3 at = abilityPicker.position;
                at.x = icons[position].transform.position.x;
                abilityPicker.position = at;
            }

            int[] held = { (int)me.AbilitySpace, (int)me.AbilityShift };
            for (int i = 0; i < slotOrbs.Count; i++)
                slotOrbs[i].Show(IconOf(held[i]), i < abilityKeyLabels.Length ? abilityKeyLabels[i] : "", openAbilitySlot == i, false);

            abilityOptionRoot.gameObject.SetActive(openAbilitySlot >= 0);
            if (openAbilitySlot < 0) return;
            for (int option = 0; option < optionOrbs.Count; option++)
                optionOrbs[option].Show(IconOf(option), NameOf(option),
                    held[openAbilitySlot] == option, held[1 - openAbilitySlot] == option);
        }

        static Sprite IconOf(int ability) => MPAbilities.Get(ability)?.Icon;
        static string NameOf(int ability) => MPAbilities.Get(ability)?.DisplayName ?? "";

        // Takes an ability for the open slot (the host swaps it if the other slot has it).
        void ChooseAbility(int ability)
        {
            if (MPLobby.Instance != null && openAbilitySlot >= 0) MPLobby.Instance.SetAbilityRpc(openAbilitySlot, (MPAbility)ability);
            openAbilitySlot = -1;
        }

        void ToggleZombies()
        {
            MPLobby lobby = MPLobby.Instance;
            if (lobby != null) lobby.SetZombieModeRpc(!lobby.ZombieMode);
        }

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
