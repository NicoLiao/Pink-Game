using UnityEngine;

namespace LoveShot
{
    // Tuning profile for MiniBossAI. One asset per difficulty (Easy, Hard, later Normal...).
    // Defaults equal the original AI, which is the Hard profile; the "mistake" settings default to off.
    [CreateAssetMenu(fileName = "AIDifficulty", menuName = "LoveShot/AI Difficulty")]
    public class AIDifficulty : ScriptableObject
    {
        [field: SerializeField, Tooltip("Shown in the main menu.")]
        public string DisplayName { get; private set; } = "Hard";
        [field: SerializeField, Tooltip("Main menu difficulty button color.")]
        public Color MenuColor { get; private set; } = new Color(0.75f, 0.15f, 0.4f);

        [field: Header("Targeting")]
        [field: SerializeField, Min(0.05f), Tooltip("Seconds between target decisions. Higher = slower to react and to switch.")]
        public float DecisionInterval { get; private set; } = 0.3f;
        [field: SerializeField, Min(0f), Tooltip("A new target must score this much better to replace the current one.")]
        public float SwitchMargin { get; private set; } = 2f;
        [field: SerializeField, Min(0f), Tooltip("Seconds after picking a new target before the AI starts shooting at it.")]
        public float TargetReactionTime { get; private set; }
        [field: SerializeField, Min(0.1f), Tooltip("Distance multiplier for enemy-owned NPCs. Below 1 = prefers stealing, 1 = pure distance.")]
        public float StealDistanceFactor { get; private set; } = 1.2f;
        [field: SerializeField, Min(0.1f), Tooltip("Multiplies StealDistanceFactor while losing, so a losing AI steals more. 1 = off.")]
        public float LosingStealMultiplier { get; private set; } = 0.7f;
        [field: SerializeField, Min(0f), Tooltip("Distance discount for NPCs already leaning toward the AI. 0 = off.")]
        public float ProgressBonus { get; private set; } = 4f;
        [field: SerializeField, Min(0f), Tooltip("Distance discount for neutrals the player is about to convert. 0 = off.")]
        public float DenyBonus { get; private set; } = 3f;
        [field: SerializeField, Min(0f), Tooltip("Extra distance added to targets behind walls.")]
        public float NoLineOfSightPenalty { get; private set; } = 4f;

        [field: Header("Movement")]
        [field: SerializeField, Min(0f)]
        public float PreferredMinDistance { get; private set; } = 3f;
        [field: SerializeField, Min(0f)]
        public float PreferredMaxDistance { get; private set; } = 6f;
        [field: SerializeField, Tooltip("Random seconds between strafe direction flips.")]
        public Vector2 StrafeSwitchInterval { get; private set; } = new Vector2(1f, 2.5f);
        [field: SerializeField, Range(0f, 1f), Tooltip("Chance per decision to drift off to a random spot instead of pursuing a target.")]
        public float WanderChance { get; private set; }
        [field: SerializeField, Range(0f, 1f), Tooltip("Random wobble added to the movement direction. 0 = efficient, 1 = very sloppy.")]
        public float MoveJitter { get; private set; }

        [field: Header("Shooting")]
        [field: SerializeField, Min(0f)]
        public float ShootRange { get; private set; } = 9f;
        [field: SerializeField, Range(0f, 45f), Tooltip("Random aim error in degrees.")]
        public float AimSpread { get; private set; } = 6f;
        [field: SerializeField, Tooltip("Aim ahead of moving targets.")]
        public bool LeadTargets { get; private set; } = true;
        [field: SerializeField, Min(0.1f), Tooltip("Multiplies the leader's fire cooldown. Above 1 = shoots slower.")]
        public float FireCooldownMultiplier { get; private set; } = 1f;
        [field: SerializeField, Min(0f), Tooltip("Within this range the AI may shoot the player to lovestruck them. 0 disables.")]
        public float HarassRange { get; private set; } = 5f;
        [field: SerializeField, Range(0f, 1f), Tooltip("Chance, rolled each decision, to go for the player when in range.")]
        public float HarassChance { get; private set; } = 0.35f;

        [field: Header("Evade")]
        [field: SerializeField, Range(0f, 1f), Tooltip("Chance to react to each incoming player shot.")]
        public float DodgeChance { get; private set; } = 0.5f;
        [field: SerializeField, Min(0f), Tooltip("Only shots arriving within this many seconds are noticed.")]
        public float ThreatHorizon { get; private set; } = 0.6f;
        [field: SerializeField, Min(0f), Tooltip("A shot counts as a threat if it will pass this close.")]
        public float DangerRadius { get; private set; } = 0.9f;
        [field: SerializeField, Min(0.05f)]
        public float EvadeDuration { get; private set; } = 0.35f;
        [field: SerializeField, Min(0f), Tooltip("Dash out of the way if impact is sooner than this. 0 = never dash-dodge.")]
        public float DashDodgeTime { get; private set; } = 0.25f;
    }
}
