using TMPro;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // HUD panel for the local player's weapon: icon, name, seconds left and a bar that drains until a
    // picked-up weapon runs out (the Default Gun shows "UNLIMITED"). Pops when a weapon is picked up.
    public class MPWeaponHUD : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Image icon;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text timeText;
        [Tooltip("Filled (Horizontal) image: the fraction of the weapon's time left.")]
        [SerializeField] UnityEngine.UI.Image timeBar;
        [SerializeField] Color barColor = new Color(0.9f, 0.25f, 0.55f);
        [SerializeField] Color barWarningColor = new Color(0.95f, 0.55f, 0.2f);
        [SerializeField, Min(0f)] float warningTime = 5f;
        [SerializeField, Min(1f)] float popScale = 1.15f;
        [SerializeField, Min(0.01f)] float popDuration = 0.3f;

        MPPlayer bound;
        float popStartTime = float.NegativeInfinity;

        void OnDisable() => Bind(null);

        void Bind(MPPlayer player)
        {
            if (bound == player) return;
            if (bound != null) bound.Equipped -= OnEquipped;
            bound = player;
            if (bound != null) bound.Equipped += OnEquipped;
        }

        void OnEquipped(int weaponIndex) => popStartTime = Time.unscaledTime;

        void LateUpdate()
        {
            Bind(MPPlayer.Local);
            MPWeapon weapon = bound != null ? bound.Weapon : null;
            if (weapon == null) return;

            if (icon != null) icon.sprite = weapon.Icon;
            if (nameText != null) nameText.text = weapon.DisplayName.ToUpperInvariant();
            bool timed = bound.WeaponIndex != 0;
            float remaining = bound.WeaponRemaining;
            if (timeText != null) timeText.text = timed ? $"{remaining:0.0}s" : "UNLIMITED";
            if (timeBar != null)
            {
                timeBar.fillAmount = timed ? Mathf.Clamp01(remaining / weapon.Duration) : 1f;
                bool warning = timed && remaining <= warningTime;
                timeBar.color = warning && Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.5f ? barWarningColor : barColor;
            }

            float t = (Time.unscaledTime - popStartTime) / popDuration;
            transform.localScale = Vector3.one * (1f + (t < 1f ? Mathf.Sin(t * Mathf.PI) * (popScale - 1f) : 0f));
        }
    }
}
