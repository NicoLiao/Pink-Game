using System.Collections.Generic;
using UnityEngine;

namespace LoveShot
{
    // Marks the leader of a team (the player for Pink, the AI mini-boss for Blue). Converted NPCs follow it.
    public class TeamLeader : MonoBehaviour
    {
        static readonly Dictionary<Team, TeamLeader> leaders = new();

        [SerializeField] Team team = Team.Pink;

        Rigidbody2D body;

        public Team Team => team;
        public Vector2 Position => transform.position;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

        public static TeamLeader For(Team team) => leaders.TryGetValue(team, out TeamLeader leader) ? leader : null;

        void Awake() => body = GetComponent<Rigidbody2D>();

        void OnEnable() => leaders[team] = this;

        void OnDisable()
        {
            if (For(team) == this) leaders.Remove(team);
        }
    }
}
