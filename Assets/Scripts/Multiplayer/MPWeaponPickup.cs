using Unity.Netcode;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // A weapon lying in the arena. The host decides who collects it (the first player to touch it, once,
    // guarded by `collected`) and despawns it; clients only show it: the weapon's icon on a bobbing, glowing
    // badge that blinks before it disappears.
    [RequireComponent(typeof(NetworkObject))]
    public class MPWeaponPickup : NetworkBehaviour
    {
        [SerializeField] SpriteRenderer icon;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] Transform visualRoot;
        [SerializeField] HeartBurst burstPrefab;
        [SerializeField, Min(0f)] float bobHeight = 0.12f;
        [SerializeField, Min(0f)] float bobSpeed = 3f;
        [Tooltip("Seconds before despawning during which it blinks.")]
        [SerializeField, Min(0f)] float blinkTime = 3f;

        readonly NetworkVariable<byte> weapon = new(1);
        readonly NetworkVariable<double> despawnTime = new(0d);

        bool collected;

        public int WeaponIndex => weapon.Value;
        double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;

        // Host, before spawning.
        public void Init(int weaponIndex, double expireTime)
        {
            weapon.Value = (byte)weaponIndex;
            despawnTime.Value = expireTime;
        }

        public override void OnNetworkSpawn()
        {
            MPWeapon definition = MPMatch.Instance != null ? MPMatch.Instance.WeaponAt(weapon.Value) : null;
            if (icon != null && definition != null) icon.sprite = definition.Icon;
        }

        void Update()
        {
            if (!IsSpawned) return;
            float remaining = (float)(despawnTime.Value - Now);
            if (IsServer && remaining <= 0f)
            {
                NetworkObject.Despawn();
                return;
            }

            if (visualRoot != null)
            {
                visualRoot.localPosition = new Vector3(0f, Mathf.Sin(Time.time * bobSpeed) * bobHeight, 0f);
                bool hidden = remaining < blinkTime && Mathf.Repeat(Time.time * 6f, 1f) < 0.35f;
                visualRoot.gameObject.SetActive(!hidden);
            }
            if (glow != null)
            {
                glow.transform.localScale = Vector3.one * (1.25f + 0.1f * Mathf.Sin(Time.time * bobSpeed * 2f));
                glow.transform.Rotate(0f, 0f, 60f * Time.deltaTime);
            }
        }

        void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        // Also on stay, in case a player was already standing there when the match went live.
        void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        void TryCollect(Collider2D other)
        {
            if (!IsServer || !IsSpawned || collected || !MPMatch.InProgress) return;
            if (other.attachedRigidbody == null || !other.attachedRigidbody.TryGetComponent(out MPPlayer player)) return;
            collected = true;
            player.ServerEquip(weapon.Value);
            CollectedRpc(MPTeams.ColorOf(player.Team));
            NetworkObject.Despawn();
        }

        [Rpc(SendTo.ClientsAndHost)]
        void CollectedRpc(Color color)
        {
            HeartBurst.Play(burstPrefab, transform.position, color, 1.3f, 12);
            HeartBurst.Play(burstPrefab, transform.position, Color.white, 0.7f, 6);
        }
    }
}
