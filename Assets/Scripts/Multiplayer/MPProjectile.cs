using UnityEngine;

namespace LoveShot.Multiplayer
{
    // A love projectile in multiplayer (heart, rocket or arrow). The host's copy is authoritative: it alone
    // detects character hits and hands them to MPMatch (ApplyLove, or Explode for rockets), exactly once
    // (`consumed`). Clients get a visual-only copy (spawned by MPMatch) that flies the same path, bounces off
    // the same walls, and is removed when the host reports the hit.
    // Walls are found by a swept circle cast each physics step, so ricochets are identical on every machine.
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class MPProjectile : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [Tooltip("Sprite per ProjectileStyle (Heart, Rocket, Arrow).")]
        [SerializeField] Sprite[] styleSprites;
        [SerializeField] LayerMask wallMask;
        [SerializeField] HeartBurst trailPrefab;
        [SerializeField, Min(0.01f)] float trailInterval = 0.05f;

        MPMatch match;
        Rigidbody2D body;
        float radius;
        int team;
        int creditSlot;
        MPShot shot;
        int bouncesLeft;
        bool fromPlayer;
        ulong shooterObjectId;
        bool authoritative;
        bool consumed;
        float dieTime;
        float nextTrailTime;

        public ushort Id { get; private set; }

        // team only colors it; on a hit the host uses the credited player's team at that moment.
        public void Launch(MPMatch owner, ushort id, int team, int creditSlot, Vector2 velocity, MPShot shot, bool fromPlayer,
            ulong shooterObjectId, bool authoritative)
        {
            match = owner;
            Id = id;
            this.team = team;
            this.creditSlot = creditSlot;
            this.shot = shot;
            bouncesLeft = shot.Bounces;
            this.fromPlayer = fromPlayer;
            this.shooterObjectId = shooterObjectId;
            this.authoritative = authoritative;
            transform.localScale *= shot.Scale;
            body = GetComponent<Rigidbody2D>();
            radius = GetComponent<CircleCollider2D>().radius * transform.localScale.x;
            body.linearVelocity = velocity;
            dieTime = Time.time + shot.Lifetime;
            if (sprite != null)
            {
                sprite.color = MPTeams.ColorOf(team);
                int style = (int)shot.Style;
                if (styleSprites != null && style < styleSprites.Length && styleSprites[style] != null) sprite.sprite = styleSprites[style];
            }
            FaceVelocity();
        }

        void OnDestroy()
        {
            if (match != null) match.ForgetProjectile(this);
        }

        void Update()
        {
            if (consumed) return;
            if (Time.time >= dieTime)
            {
                // Hearts just fade out everywhere. Rockets burst; clients wait for the host's word on where.
                if (authoritative && shot.Explosive) Finish();
                else if (authoritative || !shot.Explosive || Time.time >= dieTime + 1f) Remove(false);
                return;
            }
            if (shot.Style == ProjectileStyle.Rocket && Time.time >= nextTrailTime && trailPrefab != null)
            {
                nextTrailTime = Time.time + trailInterval;
                HeartBurst.Play(trailPrefab, transform.position - (Vector3)(body.linearVelocity.normalized * radius), MPTeams.ColorOf(team), 0.35f, 1);
            }
        }

        void FixedUpdate()
        {
            if (consumed) return;
            Vector2 velocity = body.linearVelocity;
            float step = velocity.magnitude * Time.fixedDeltaTime;
            if (step <= 0f) return;
            RaycastHit2D hit = Physics2D.CircleCast(body.position, radius, velocity, step, wallMask);
            if (hit.collider == null) return;

            if (bouncesLeft <= 0)
            {
                body.position = hit.centroid;
                if (authoritative) Finish();
                else if (!shot.Explosive) Remove(false);
                else body.linearVelocity = Vector2.zero;
                return;
            }
            bouncesLeft--;
            body.position = hit.centroid + hit.normal * 0.01f;
            body.linearVelocity = Vector2.Reflect(velocity, hit.normal);
            FaceVelocity();
        }

        void FaceVelocity()
        {
            if (shot.Style == ProjectileStyle.Heart || body.linearVelocity.sqrMagnitude < 0.0001f) return;
            float angle = Mathf.Atan2(body.linearVelocity.y, body.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (consumed || !authoritative) return;
            GameObject hit = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (!hit.TryGetComponent(out MPCapture capture) || match == null) return;
            if (hit.TryGetComponent(out Unity.Netcode.NetworkObject networkObject) && networkObject.NetworkObjectId == shooterObjectId) return;

            if (shot.Explosive)
            {
                // Rockets burst on anything that could take their love; teammates and immune players let them through.
                if (match.CanTakeLove(capture, creditSlot)) Finish();
                return;
            }
            // Shots their target can't take pass through, so allies never block your aim.
            if (match.ApplyLove(capture, creditSlot, shot.Power, fromPlayer)) Remove(true);
        }

        // Host: end of flight (wall, lifetime, or a rocket's target): explode if explosive, else just vanish.
        void Finish()
        {
            if (shot.Explosive && match != null)
            {
                match.Explode(transform.position, shot, creditSlot, fromPlayer);
                Remove(true, shot.ExplosionRadius);
            }
            else Remove(true);
        }

        // Host: removes this projectile everywhere. Clients: removes the local visual.
        public void Remove(bool notifyClients, float explosionRadius = 0f)
        {
            if (consumed) return;
            consumed = true;
            if (authoritative && notifyClients && match != null) match.OnProjectileRemoved(Id, transform.position, team, explosionRadius);
            Destroy(gameObject);
        }
    }
}
