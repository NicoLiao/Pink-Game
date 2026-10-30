using UnityEngine;

namespace LoveShot.Multiplayer
{
    // A heart projectile in multiplayer. The host's copy is authoritative: it alone detects hits, applies
    // capture progress and lovestrucks players, exactly once (`consumed`). Clients get a visual-only copy
    // (spawned by MPMatch) that flies the same path, stops on walls, and is removed when the host reports a hit.
    [RequireComponent(typeof(Rigidbody2D))]
    public class MPProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float lifetime = 2f;
        [SerializeField] SpriteRenderer sprite;

        MPMatch match;
        int team;
        float power;
        bool fromPlayer;
        ulong shooterObjectId;
        bool authoritative;
        bool consumed;

        public ushort Id { get; private set; }

        public void Launch(MPMatch owner, ushort id, int team, Vector2 velocity, float power, bool fromPlayer, ulong shooterObjectId, bool authoritative)
        {
            match = owner;
            Id = id;
            this.team = team;
            this.power = power;
            this.fromPlayer = fromPlayer;
            this.shooterObjectId = shooterObjectId;
            this.authoritative = authoritative;
            GetComponent<Rigidbody2D>().linearVelocity = velocity;
            if (sprite != null) sprite.color = MPTeams.ColorOf(team);
            Destroy(gameObject, lifetime);
        }

        void OnDestroy()
        {
            if (match != null) match.ForgetProjectile(this);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (consumed) return;
            GameObject hit = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (hit.GetComponent<MPProjectile>() != null) return;

            if (!authoritative)
            {
                // Visual copy: characters are the host's business; only walls and obstacles stop it.
                if (!other.isTrigger && hit.GetComponent<Unity.Netcode.NetworkObject>() == null) Remove(false);
                return;
            }

            if (hit.TryGetComponent(out Unity.Netcode.NetworkObject networkObject) && networkObject.NetworkObjectId == shooterObjectId) return;

            if (hit.TryGetComponent(out MPPlayer player))
            {
                // Like single player: only a player's own shots lovestruck an enemy player; everything else passes through.
                if (fromPlayer && player.Team != team && player.TryStrike()) Remove(true);
                return;
            }

            if (hit.TryGetComponent(out MPCapture capture))
            {
                // Fully loyal teammates don't absorb friendly shots, so allies never block your aim.
                if (!capture.CanBeAffectedBy(team)) return;
                capture.ApplyHit(team, power);
                Remove(true);
                return;
            }

            if (!other.isTrigger) Remove(true);
        }

        // Host: removes this projectile everywhere. Clients: removes the local visual.
        public void Remove(bool notifyClients)
        {
            if (consumed) return;
            consumed = true;
            if (authoritative && notifyClients && match != null) match.OnProjectileRemoved(Id, transform.position);
            Destroy(gameObject);
        }
    }
}
