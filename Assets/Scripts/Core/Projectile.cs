using System.Collections.Generic;
using UnityEngine;

namespace LoveShot
{
    // A love projectile. Pushes an NPC's affection toward its team, lovestrucks an enemy leader
    // (leader shots only), and stops on walls.
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        static readonly List<Projectile> all = new();
        public static IReadOnlyList<Projectile> All => all;

        [SerializeField, Min(0.1f)] float lifetime = 2f;

        Rigidbody2D body;
        Team team;
        float affectionAmount;
        GameObject owner;

        public Team Team => team;
        public Vector2 Velocity => body.linearVelocity;
        // Only shots fired by a leader can lovestruck the enemy leader.
        public bool FromLeader { get; private set; }

        void Awake() => body = GetComponent<Rigidbody2D>();

        void OnEnable() => all.Add(this);

        void OnDisable() => all.Remove(this);

        public void Launch(Team team, Vector2 velocity, float affectionAmount, GameObject owner)
        {
            this.team = team;
            this.affectionAmount = affectionAmount;
            this.owner = owner;
            FromLeader = owner != null && owner.GetComponent<TeamLeader>() != null;
            body.linearVelocity = velocity;
            if (TryGetComponent(out SpriteRenderer sprite)) sprite.color = TeamUtil.ColorOf(team);
            Destroy(gameObject, lifetime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            GameObject hit = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (hit == owner || hit.GetComponent<Projectile>() != null) return;

            if (hit.TryGetComponent(out TeamLeader leader))
            {
                // Otherwise shots pass through leaders (teammates, ally shots, or a leader that is immune).
                if (FromLeader && leader.Team != team && hit.TryGetComponent(out Lovestruck lovestruck) && lovestruck.TryStrike())
                    Destroy(gameObject);
                return;
            }

            if (hit.TryGetComponent(out Affection target))
            {
                // Fully loyal teammates don't absorb friendly shots, so allies never block your aim.
                if (!target.CanBeAffectedBy(team)) return;
                target.ApplyLove(team, affectionAmount);
                Destroy(gameObject);
                return;
            }

            if (!other.isTrigger) Destroy(gameObject);
        }
    }
}
