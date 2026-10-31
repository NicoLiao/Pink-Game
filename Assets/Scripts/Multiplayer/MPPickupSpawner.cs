using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Host only (sits on the MPMatch prefab): while the match runs, drops a random temporary weapon at a
    // free spot every few seconds, up to a cap. Spots avoid walls, obstacles, characters and players.
    public class MPPickupSpawner : MonoBehaviour
    {
        [SerializeField] NetworkObject pickupPrefab;
        [Tooltip("Spawn weight per weapon index (index 0, the Default Gun, is never dropped).")]
        [SerializeField] float[] weaponWeights = { 0f, 1f, 1f, 0.7f };
        [SerializeField, Min(0f)] float firstSpawnDelay = 5f;
        [SerializeField, Min(0.5f)] float spawnInterval = 9f;
        [SerializeField, Min(1)] int maxActive = 3;
        [Tooltip("Seconds an uncollected pickup stays before it disappears.")]
        [SerializeField, Min(1f)] float pickupLifetime = 20f;
        [SerializeField] Vector2 areaHalfExtents = new Vector2(26f, 15f);
        [SerializeField, Min(0f)] float clearance = 1f;
        [SerializeField, Min(0f)] float minPlayerDistance = 4f;

        readonly List<NetworkObject> active = new();
        float nextSpawnTime = float.NaN;

        void Update()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsServer || !MPMatch.InProgress) return;
            if (float.IsNaN(nextSpawnTime)) nextSpawnTime = Time.time + firstSpawnDelay;
            if (Time.time < nextSpawnTime) return;
            nextSpawnTime = Time.time + spawnInterval;

            active.RemoveAll(pickup => pickup == null || !pickup.IsSpawned);
            if (active.Count >= maxActive || pickupPrefab == null) return;
            int weapon = PickWeapon();
            if (weapon <= 0 || !TryFindSpot(out Vector2 position)) return;

            NetworkObject pickup = Instantiate(pickupPrefab, position, Quaternion.identity);
            pickup.GetComponent<MPWeaponPickup>().Init(weapon, network.ServerTime.Time + pickupLifetime);
            pickup.Spawn(destroyWithScene: true);
            active.Add(pickup);
        }

        int PickWeapon()
        {
            int count = MPMatch.Instance != null ? Mathf.Min(MPMatch.Instance.WeaponCount, weaponWeights.Length) : 0;
            float total = 0f;
            for (int i = 1; i < count; i++) total += Mathf.Max(0f, weaponWeights[i]);
            if (total <= 0f) return 0;
            float roll = Random.value * total;
            for (int i = 1; i < count; i++)
            {
                roll -= Mathf.Max(0f, weaponWeights[i]);
                if (roll <= 0f) return i;
            }
            return count - 1;
        }

        bool TryFindSpot(out Vector2 position)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                position = new Vector2(Random.Range(-areaHalfExtents.x, areaHalfExtents.x), Random.Range(-areaHalfExtents.y, areaHalfExtents.y));
                if (Physics2D.OverlapCircle(position, clearance) != null) continue;
                bool nearPlayer = false;
                foreach (MPPlayer player in MPPlayer.All)
                    if (Vector2.Distance(player.Position, position) < minPlayerDistance) nearPlayer = true;
                if (!nearPlayer) return true;
            }
            position = default;
            return false;
        }
    }
}
