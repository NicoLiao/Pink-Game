using System.Collections.Generic;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // HUD orb for the local player's bow, styled like a skill slot: the bow icon in an orb whose ring sweeps around
    // as you hold fire to charge (the glow lights up and blinks at full charge), with a row of small orbs under it,
    // one per active modifier (instantiated from a prefab), each draining as its timer runs out.
    // Pops when a modifier is picked up.
    public class MPWeaponHUD : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Image icon;
        [Tooltip("Filled (Radial 360) ring; its fill is the current charge.")]
        [SerializeField] UnityEngine.UI.Image chargeRing;
        [Tooltip("Lit while charging, blinks at full charge.")]
        [SerializeField] UnityEngine.UI.Image glow;
        [SerializeField] RectTransform modifierRoot;
        [SerializeField] MPModifierSlot modifierSlotPrefab;
        [SerializeField, Min(0f)] float warningTime = 5f;
        [SerializeField, Range(0f, 1f)] float chargingGlowAlpha = 0.6f;
        [SerializeField, Min(1f)] float popScale = 1.15f;
        [SerializeField, Min(0.01f)] float popDuration = 0.3f;

        readonly List<MPModifierSlot> slots = new();
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
            MPWeapon bow = bound != null ? bound.Bow : null;
            if (bow == null) return;

            if (icon != null) icon.sprite = bow.Icon;
            float charge = bound.ChargeFraction;
            if (chargeRing != null) chargeRing.fillAmount = charge;
            if (glow != null)
            {
                bool full = bound.IsCharging && charge >= 1f;
                glow.enabled = bound.IsCharging && (!full || Mathf.Repeat(Time.unscaledTime * 6f, 1f) < 0.5f);
                glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, full ? 1f : chargingGlowAlpha);
            }

            ShowModifiers();

            float t = (Time.unscaledTime - popStartTime) / popDuration;
            transform.localScale = Vector3.one * (1f + (t < 1f ? Mathf.Sin(t * Mathf.PI) * (popScale - 1f) : 0f));
        }

        void ShowModifiers()
        {
            int count = modifierRoot != null && modifierSlotPrefab != null ? bound.ModifierCount : 0;
            while (slots.Count < count) slots.Add(Instantiate(modifierSlotPrefab, modifierRoot));
            for (int i = 0; i < slots.Count; i++)
            {
                MPWeapon modifier = i < count ? bound.ModifierAt(i) : null;
                slots[i].gameObject.SetActive(modifier != null);
                if (modifier != null) slots[i].Show(modifier, bound.ModifierRemaining(i), warningTime);
            }
        }
    }
}
