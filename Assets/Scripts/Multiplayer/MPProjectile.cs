using UnityEngine;

namespace LoveShot.Multiplayer
{
    // A love projectile in multiplayer (heart, rocket or arrow). The host's copy is authoritative: it alone
    // detects character hits and hands them to MPMatch (ApplyLove, or Explode for rockets), exactly once
    // (`consumed`). Clients get a visual-only copy (spawned by MPMatch) that flies the same path and bounces off
    // the same walls; it bursts on a likely hit right away and is removed when the host reports the real one.
    // A shooting client's own copies are spawned before the host's projectile exists (predicted) and take
    // over its id when the host confirms the shot.
    // Walls are found by a swept circle cast each physics step, so ricochets are identical on every machine.
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class MPProjectile : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [Tooltip("Sprite per ProjectileStyle (Heart = the plain arrow shaft, Rocket, Arrow = Cupid's arrow; Icon uses the shooter's symbol).")]
        [SerializeField] Sprite[] styleSprites;
        [Tooltip("Shooter's symbol sprites.")]
        [SerializeField] TeamStyle teamStyle;
        [Tooltip("Child sprite showing the shooter's symbol: the head of a bow arrow, or the badge on a rocket.")]
        [SerializeField] SpriteRenderer emblem;
        [Tooltip("Where the emblem sits on the arrow (the tip) and how big it is, in the projectile's own units.")]
        [SerializeField] Vector2 arrowEmblemOffset = new Vector2(0.45f, 0f);
        [SerializeField, Min(0.05f)] float arrowEmblemSize = 0.8f;
        [SerializeField] Vector2 rocketEmblemOffset = new Vector2(-0.05f, 0f);
        [SerializeField, Min(0.05f)] float rocketEmblemSize = 0.6f;
        [Tooltip("The badge on a rocket stays this color (the rocket itself takes the team color).")]
        [SerializeField] Color rocketEmblemColor = Color.white;
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

        // Raised on every machine for each projectile that appears (projectile, whether a player fired it).
        public static event System.Action<MPProjectile, bool> Launched;
        // Raised on every machine when a projectile ricochets off a wall.
        public static event System.Action<MPProjectile> Bounced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Launched = null;
            Bounced = null;
        }

        public ushort Id { get; private set; }
        public ProjectileStyle Style => shot.Style;
        // Client: identifies an own shot shown before the host confirmed it (0 otherwise).
        public uint PredictKey { get; set; }

        // Client: the host confirmed this predicted shot under its projectile id.
        public void AssignId(ushort id) => Id = id;

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
                // Icon shots are just the shooter's own symbol (the prefab's default heart if it has none).
                Sprite symbol = shot.Style == ProjectileStyle.Icon && teamStyle != null ? teamStyle.SymbolOf(creditSlot) : null;
                if (symbol != null) sprite.sprite = symbol;
            }
            ShowEmblem();
            FaceVelocity();
            KickShooter();
            Launched?.Invoke(this, fromPlayer);
        }

        // Arrows and rockets carry the shooter's own symbol (identity); an arrow's tip takes the team color.
        void ShowEmblem()
        {
            if (emblem == null) return;
            bool carries = shot.Style == ProjectileStyle.Heart || shot.Style == ProjectileStyle.Rocket;
            Sprite symbol = carries && teamStyle != null ? teamStyle.SymbolOf(creditSlot) : null;
            emblem.gameObject.SetActive(symbol != null);
            if (symbol == null) return;
            bool rocket = shot.Style == ProjectileStyle.Rocket;
            Vector3 size = symbol.bounds.size;
            float fit = (rocket ? rocketEmblemSize : arrowEmblemSize) / Mathf.Max(size.x, size.y, 0.0001f);
            emblem.sprite = symbol;
            emblem.color = rocket ? rocketEmblemColor : MPTeams.ColorOf(team);
            emblem.transform.localPosition = rocket ? rocketEmblemOffset : arrowEmblemOffset;
            emblem.transform.localScale = Vector3.one * fit;
        }

        // The shooter recoils on every machine that shows this shot.
        void KickShooter()
        {
            Unity.Netcode.NetworkManager network = Unity.Netcode.NetworkManager.Singleton;
            if (shooterObjectId == 0 || network == null || network.SpawnManager == null) return;
            if (!network.SpawnManager.SpawnedObjects.TryGetValue(shooterObjectId, out Unity.Netcode.NetworkObject shooter) || shooter == null) return;
            SquashStretch squash = shooter.GetComponentInChildren<SquashStretch>();
            if (squash != null) squash.Kick();
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
            Bounced?.Invoke(this);
        }

        void FaceVelocity()
        {
            if (shot.Style == ProjectileStyle.Icon || body.linearVelocity.sqrMagnitude < 0.0001f) return;
            float angle = Mathf.Atan2(body.linearVelocity.y, body.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (consumed || match == null) return;
            GameObject hit = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (!hit.TryGetComponent(out MPCapture capture)) return;
            if (hit.TryGetComponent(out Unity.Netcode.NetworkObject networkObject) && networkObject.NetworkObjectId == shooterObjectId) return;

            if (!authoritative)
            {
                // Visual copy: react to a likely hit right away. Rockets stop and wait for the host's explosion.
                if (!match.LooksHittable(capture, team)) return;
                if (shot.Explosive) body.linearVelocity = Vector2.zero;
                else
                {
                    match.OnLocalImpact(this);
                    Remove(false);
                }
                return;
            }

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
