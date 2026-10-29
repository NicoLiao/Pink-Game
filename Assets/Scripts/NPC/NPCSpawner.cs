using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LoveShot
{
    // Spawns neutral NPCs of weighted random types at free spots inside an area when the match starts.
    public class NPCSpawner : MonoBehaviour
    {
        [Serializable]
        public struct SpawnOption
        {
            public NPCController prefab;
            [Min(0f)] public float weight;
        }

        [SerializeField] SpawnOption[] options;
        [SerializeField, Min(0)] int minCount = 8;
        [SerializeField, Min(0)] int maxCount = 12;
        [Tooltip("Spawn area on the smallest arena.")]
        [SerializeField] Vector2 areaSize = new Vector2(16f, 12f);
        [Tooltip("When the arena has grown to fit the screen, the spawn area grows by the same amount.")]
        [SerializeField] bool growWithArena = true;
        [Tooltip("Free radius required around a spawn point (keeps NPCs out of walls and each other).")]
        [SerializeField, Min(0f)] float clearance = 0.7f;
        [SerializeField, Min(1)] int attemptsPerNpc = 30;

        void Start()
        {
            int count = Random.Range(minCount, Mathf.Max(minCount, maxCount) + 1);
            Vector2 area = areaSize;
            ArenaBounds bounds = ArenaBounds.Current;
            if (growWithArena && bounds != null) area += (bounds.HalfExtents - bounds.MinHalfExtents) * 2f;
            for (int i = 0; i < count; i++)
            {
                NPCController prefab = PickPrefab();
                if (prefab == null) return;

                for (int attempt = 0; attempt < attemptsPerNpc; attempt++)
                {
                    Vector2 position = (Vector2)transform.position + new Vector2(
                        Random.Range(-0.5f, 0.5f) * area.x,
                        Random.Range(-0.5f, 0.5f) * area.y);
                    if (Physics2D.OverlapCircle(position, clearance) != null) continue;

                    NPCController npc = Instantiate(prefab, position, Quaternion.identity, transform);
                    npc.name = $"{prefab.name} {i + 1}";
                    // Sync transforms so the next OverlapCircle sees this NPC.
                    Physics2D.SyncTransforms();
                    break;
                }
            }
        }

        NPCController PickPrefab()
        {
            float total = 0f;
            foreach (SpawnOption option in options) if (option.prefab != null) total += option.weight;
            if (total <= 0f) return null;

            float roll = Random.value * total;
            foreach (SpawnOption option in options)
            {
                if (option.prefab == null) continue;
                roll -= option.weight;
                if (roll <= 0f) return option.prefab;
            }
            return options[options.Length - 1].prefab;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = TeamUtil.NeutralColor;
            Gizmos.DrawWireCube(transform.position, areaSize);
        }
    }
}
