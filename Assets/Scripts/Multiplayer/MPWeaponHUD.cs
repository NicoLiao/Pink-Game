using TMPro;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // HUD orb for the local player's weapon, styled like a skill slot: the weapon icon in an orb whose dark
    // overlay fills as a picked-up weapon runs out, with the ammo underneath: seconds left, or an infinity
    // symbol for the Default Gun. The glow marks a timed weapon and blinks when it is about to run out.
    // Pops when a weapon is picked up.
    public class MPWeaponHUD : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Image icon;
        [Tooltip("Filled (Radial 360) dark overlay; its fill is the fraction of the weapon's time already used.")]
        [SerializeField] UnityEngine.UI.Image timeOverlay;
        [Tooltip("Lit while a timed weapon is equipped; blinks in the last seconds.")]
        [SerializeField] UnityEngine.UI.Image glow;
        [Tooltip("Seconds left under the orb (timed weapons).")]
        [SerializeField] TMP_Text ammoText;
        [Tooltip("Shown instead of the ammo text for the Default Gun.")]
        [SerializeField] GameObject unlimitedSymbol;
        [Tooltip("{0} = seconds left.")]
        [SerializeField] string timedFormat = "{0:0.0}s";
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

            bool timed = bound.WeaponIndex != 0;
            float remaining = bound.WeaponRemaining;
            if (icon != null) icon.sprite = weapon.Icon;
            if (timeOverlay != null) timeOverlay.fillAmount = timed ? 1f - Mathf.Clamp01(remaining / weapon.Duration) : 0f;
            if (ammoText != null)
            {
                ammoText.gameObject.SetActive(timed);
                if (timed) ammoText.text = string.Format(timedFormat, remaining);
            }
            if (unlimitedSymbol != null) unlimitedSymbol.SetActive(!timed);
            if (glow != null)
            {
                bool warning = timed && remaining <= warningTime;
                glow.enabled = timed && (!warning || Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.5f);
            }

            float t = (Time.unscaledTime - popStartTime) / popDuration;
            transform.localScale = Vector3.one * (1f + (t < 1f ? Mathf.Sin(t * Mathf.PI) * (popScale - 1f) : 0f));
        }
    }
}
