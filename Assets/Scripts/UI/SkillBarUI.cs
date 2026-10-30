using System.Collections.Generic;
using UnityEngine;

namespace LoveShot
{
    // HUD row of skill slots, one per Skill on the bound player. Single player binds to `player` on start;
    // multiplayer binds at runtime once the local player spawns.
    public class SkillBarUI : MonoBehaviour
    {
        [SerializeField] SkillSlotUI slotPrefab;
        [Tooltip("Optional: player whose skills are shown from the start.")]
        [SerializeField] GameObject player;

        readonly List<SkillSlotUI> slots = new();

        void Start()
        {
            if (player != null) Bind(player);
        }

        public void Bind(GameObject owner)
        {
            foreach (SkillSlotUI slot in slots) if (slot != null) Destroy(slot.gameObject);
            slots.Clear();
            if (owner == null || slotPrefab == null) return;

            foreach (Skill skill in owner.GetComponents<Skill>())
            {
                SkillSlotUI slot = Instantiate(slotPrefab, transform);
                slot.gameObject.SetActive(true);
                slot.Bind(skill);
                slots.Add(slot);
            }
        }
    }
}
