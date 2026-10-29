using System;
using UnityEngine;

namespace LoveShot
{
    // Affection runs from -100 (Blue) to +100 (Pink). Reaching an extreme converts the NPC to that team.
    // An owned NPC keeps its team while an opponent wears its loyalty down, and only switches when the
    // opposite extreme is reached, so ownership never flickers at a threshold and can switch any number of times.
    public class Affection : MonoBehaviour
    {
        public const float Max = 100f;

        [SerializeField, Range(-Max, Max)] float value;
        [Tooltip("Multiplies incoming love. Lets special NPC types (e.g. Shy) convert slower or faster.")]
        [SerializeField, Min(0f)] float sensitivity = 1f;
        [Tooltip("If on, an owned NPC whose affection falls back to 0 turns Neutral before it can be stolen.")]
        [SerializeField] bool becomeNeutralAtZero;

        Team owner = Team.Neutral;

        // (previous, current)
        public event Action<Team, Team> TeamChanged;
        // Raised on every accepted hit, with the shooter's team.
        public event Action<Team> LoveReceived;

        public float Value => value;
        public float Normalized => value / Max;
        public Team Owner => owner;

        void Awake()
        {
            if (value >= Max) owner = Team.Pink;
            else if (value <= -Max) owner = Team.Blue;
        }

        // False only when this NPC already fully belongs to that team (a hit would change nothing).
        public bool CanBeAffectedBy(Team source)
        {
            float sign = TeamUtil.AffectionSign(source);
            return sign != 0f && value * sign < Max;
        }

        public void ApplyLove(Team source, float amount)
        {
            float sign = TeamUtil.AffectionSign(source);
            if (sign == 0f || amount <= 0f) return;

            value = Mathf.Clamp(value + sign * amount * sensitivity, -Max, Max);
            LoveReceived?.Invoke(source);
            UpdateOwner();
        }

        // Instantly sets ownership, e.g. for NPCs that start on a team.
        public void SetOwner(Team team)
        {
            value = TeamUtil.AffectionSign(team) * Max;
            UpdateOwner();
        }

        void UpdateOwner()
        {
            Team next = owner;
            if (value >= Max) next = Team.Pink;
            else if (value <= -Max) next = Team.Blue;
            else if (becomeNeutralAtZero && owner != Team.Neutral && value * TeamUtil.AffectionSign(owner) <= 0f) next = Team.Neutral;

            if (next == owner) return;
            Team previous = owner;
            owner = next;
            TeamChanged?.Invoke(previous, next);
        }
    }
}
