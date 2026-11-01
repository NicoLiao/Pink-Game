using System;
using TMPro;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // One small clickable orb in the lobby's ability picker (a prefab MPLobbyUI instantiates): the ability's icon in an
    // orb with a label underneath, a glow while selected, and a dimmed look for an ability the other slot already holds.
    public class MPAbilityOrb : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Button button;
        [SerializeField] UnityEngine.UI.Image icon;
        [Tooltip("Lit while this orb is the selected one.")]
        [SerializeField] UnityEngine.UI.Image glow;
        [SerializeField] TMP_Text label;
        [SerializeField, Range(0f, 1f)] float dimmedAlpha = 0.5f;

        public void Bind(Action onClick) => button.onClick.AddListener(() => onClick());

        public void Show(Sprite sprite, string text, bool selected, bool dimmed)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = new Color(1f, 1f, 1f, dimmed ? dimmedAlpha : 1f);
            }
            if (glow != null) glow.enabled = selected;
            if (label != null) label.text = text;
        }
    }
}
