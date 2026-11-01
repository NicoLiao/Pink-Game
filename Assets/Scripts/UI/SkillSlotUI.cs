using TMPro;
using UnityEngine;

namespace LoveShot
{
    // One HUD skill slot: icon, key badge, "DASH 14.5s" label and a radial cooldown overlay that drains
    // as the skill recharges. When ready the icon lights up fully and pops once.
    public class SkillSlotUI : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Image icon;
        [Tooltip("Filled (Radial 360) dark overlay; its fill is the fraction of cooldown left.")]
        [SerializeField] UnityEngine.UI.Image cooldownOverlay;
        [SerializeField] UnityEngine.UI.Image readyGlow;
        [SerializeField] TMP_Text keyText;
        [SerializeField] TMP_Text labelText;
        [SerializeField] Color readyIconColor = Color.white;
        [SerializeField] Color chargingIconColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] Color readyLabelColor = new Color(0.9f, 0.25f, 0.55f);
        [SerializeField] Color chargingLabelColor = new Color(0.45f, 0.28f, 0.42f);
        [Tooltip("Icon and label colors while the skill can't be used at all (e.g. a zombie's Dash).")]
        [SerializeField] Color disabledIconColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);
        [SerializeField] Color disabledLabelColor = new Color(0.55f, 0.5f, 0.55f);
        [SerializeField, Min(1f)] float readyPopScale = 1.25f;
        [SerializeField, Min(0.01f)] float readyPopDuration = 0.35f;

        Skill skill;
        bool wasReady = true;
        float readyPopStart = float.NegativeInfinity;

        public Skill Skill => skill;

        public void Bind(Skill target)
        {
            skill = target;
            if (skill == null) return;
            if (icon != null && skill.Icon != null) icon.sprite = skill.Icon;
            if (keyText != null) keyText.text = skill.KeyLabel;
            wasReady = skill.IsReady;
        }

        void LateUpdate()
        {
            if (skill == null) return;

            float cooldown = Mathf.Max(skill.Cooldown, 0.0001f);
            float remaining = Mathf.Max(0f, skill.CooldownRemaining);
            bool disabled = skill.IsDisabled;
            bool ready = skill.IsReady && !disabled;
            if (ready && !wasReady) readyPopStart = Time.unscaledTime;
            wasReady = ready;

            if (cooldownOverlay != null) cooldownOverlay.fillAmount = ready || disabled ? 0f : Mathf.Clamp01(remaining / cooldown);
            if (icon != null) icon.color = disabled ? disabledIconColor : ready ? readyIconColor : chargingIconColor;

            if (labelText != null)
            {
                string name = skill.DisplayName.ToUpperInvariant();
                labelText.text = disabled ? name : ready ? $"{name}  READY" : $"{name}  {remaining:0.0}s";
                labelText.color = disabled ? disabledLabelColor : ready ? readyLabelColor : chargingLabelColor;
            }

            float t = (Time.unscaledTime - readyPopStart) / readyPopDuration;
            float pop = t < 1f ? Mathf.Sin(t * Mathf.PI) * (readyPopScale - 1f) : 0f;
            transform.localScale = Vector3.one * (1f + pop);
            if (readyGlow != null) readyGlow.enabled = ready;
        }
    }
}
