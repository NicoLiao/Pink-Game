using UnityEngine;

namespace LoveShot.Multiplayer
{
    // One small orb in the weapon HUD's modifier row (a prefab MPWeaponHUD instantiates per active modifier):
    // the modifier's icon with a dark overlay that fills as its timer runs out, blinking in the last seconds.
    public class MPModifierSlot : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Image icon;
        [Tooltip("Filled (Radial 360) dark overlay; its fill is the fraction of the time already used.")]
        [SerializeField] UnityEngine.UI.Image timeOverlay;
        [SerializeField, Range(0f, 1f)] float blinkAlpha = 0.35f;
        [SerializeField, Min(0.1f)] float blinkSpeed = 4f;

        public void Show(MPWeapon modifier, float remaining, float warningTime)
        {
            if (icon != null)
            {
                icon.sprite = modifier.Icon;
                bool dim = remaining <= warningTime && Mathf.Repeat(Time.unscaledTime * blinkSpeed, 1f) >= 0.5f;
                icon.color = new Color(1f, 1f, 1f, dim ? blinkAlpha : 1f);
            }
            if (timeOverlay != null) timeOverlay.fillAmount = 1f - Mathf.Clamp01(remaining / modifier.Duration);
        }
    }
}
