using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Free-for-all allegiance of one character (NPC or player): the team it fights for (or Neutral), its
    // personal owner (the player slot credited with it), the team currently capturing it, and capture
    // progress 0..1. Only the host changes it; every client reads the synced values.
    //
    // Rules for a hit by team T (all deterministic, applied in hit order on the host):
    //  - T owns it: T's shots wear down whoever is capturing it (loyalty is restored). A fully loyal NPC
    //    ignores its own team's shots, so they pass through. Players never take shots from their own team.
    //  - Nobody, or T, is capturing it: T's progress grows. At 1 it joins T and progress resets.
    //  - Another team C is capturing it: T first wears C's progress down to 0; the overflow becomes T's progress.
    // Capturing a neutral takes captureAmount; stealing an owned one takes stealAmount (it breaks allegiance first).
    public class MPCapture : NetworkBehaviour
    {
        [Tooltip("Love needed to recruit a neutral NPC.")]
        [SerializeField, Min(1f)] float captureAmount = 100f;
        [Tooltip("Love needed to steal one another team owns (or to convert a player).")]
        [SerializeField, Min(1f)] float stealAmount = 200f;
        [Tooltip("Multiplies incoming love. For special NPC types that convert slower or faster.")]
        [SerializeField, Min(0f)] float sensitivity = 1f;
        [Tooltip("Whether its own team's shots restore worn-down loyalty (NPCs). Off: own-team shots always pass through (players).")]
        [SerializeField] bool ownTeamHeals = true;
        [Tooltip("Seconds without hits before capture progress starts fading. 0 disables recovery.")]
        [SerializeField, Min(0f)] float recoveryDelay;
        [Tooltip("Love per second removed from capture progress while recovering.")]
        [SerializeField, Min(0f)] float recoveryRate;

        static readonly List<MPCapture> all = new();
        public static IReadOnlyList<MPCapture> All => all;

        readonly NetworkVariable<int> owner = new(MPTeams.Neutral);
        readonly NetworkVariable<int> personalOwner = new(MPTeams.Neutral);
        readonly NetworkVariable<int> capturer = new(MPTeams.Neutral);
        readonly NetworkVariable<float> progress = new(0f);
        // Declared before `hits` so clients have it updated when the hit counter's callback runs.
        readonly NetworkVariable<int> lastHitTeam = new(MPTeams.Neutral);
        // Increments on every accepted hit, so clients can flash even when progress lands on the same value.
        readonly NetworkVariable<int> hits = new(0);

        float lastHitTime;

        public int Owner => owner.Value;
        // Player slot this character is credited to (NPCs: who recruited it; players: themselves). Neutral if none.
        public int PersonalOwner => personalOwner.Value;
        public int Capturer => capturer.Value;
        public float Progress => progress.Value;
        public MPPlayer Player { get; private set; }
        public MPNpc Npc { get; private set; }

        // (previous, current) owner team, raised on every machine.
        public event Action<int, int> OwnerChanged;
        // Raised on every machine for each accepted hit, with the shooter's team.
        public event Action<int> HitReceived;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();

        void Awake()
        {
            Player = GetComponent<MPPlayer>();
            Npc = GetComponent<MPNpc>();
        }

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            owner.OnValueChanged += OnOwnerValueChanged;
            hits.OnValueChanged += OnHitsChanged;
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            owner.OnValueChanged -= OnOwnerValueChanged;
            hits.OnValueChanged -= OnHitsChanged;
        }

        void OnOwnerValueChanged(int previous, int current) => OwnerChanged?.Invoke(previous, current);

        void OnHitsChanged(int previous, int current) => HitReceived?.Invoke(lastHitTeam.Value);

        void Update()
        {
            if (!IsServer || recoveryDelay <= 0f || capturer.Value == MPTeams.Neutral || Time.time - lastHitTime < recoveryDelay) return;
            float next = progress.Value - recoveryRate / RequiredAmount * Time.deltaTime;
            if (next > 0f)
            {
                progress.Value = next;
                return;
            }
            progress.Value = 0f;
            capturer.Value = MPTeams.Neutral;
        }

        float RequiredAmount => owner.Value == MPTeams.Neutral ? captureAmount : stealAmount;

        // False when the shot could change nothing: its own team's shot on a fully loyal NPC, or on a player.
        public bool CanBeAffectedBy(int team) =>
            MPTeams.IsTeam(team) && !(owner.Value == team && (!ownTeamHeals || capturer.Value == MPTeams.Neutral));

        // Host only. power is the shot's strength in the same units as captureAmount/stealAmount.
        // creditSlot is the player who becomes its personal owner if this hit completes the capture.
        // Returns true if the hit converted it.
        public bool ApplyHit(int team, float power, int creditSlot)
        {
            if (!IsServer || !CanBeAffectedBy(team) || power <= 0f) return false;

            float delta = power * sensitivity / RequiredAmount;
            int currentCapturer = capturer.Value;
            float current = progress.Value;

            if (owner.Value == team)
            {
                current -= delta;
                if (current <= 0f)
                {
                    current = 0f;
                    currentCapturer = MPTeams.Neutral;
                }
            }
            else if (currentCapturer == MPTeams.Neutral || currentCapturer == team)
            {
                currentCapturer = team;
                current += delta;
            }
            else
            {
                current -= delta;
                if (current <= 0f)
                {
                    currentCapturer = team;
                    current = -current;
                }
            }

            lastHitTime = Time.time;
            lastHitTeam.Value = team;
            hits.Value++;
            if (currentCapturer == team && current >= 1f)
            {
                SetOwner(team, creditSlot);
                return true;
            }
            capturer.Value = currentCapturer;
            progress.Value = Mathf.Clamp01(current);
            return false;
        }

        // Host only: instantly sets the team and personal owner and clears any capture in progress.
        public void SetOwner(int team, int slot)
        {
            if (!IsServer) return;
            capturer.Value = MPTeams.Neutral;
            progress.Value = 0f;
            bool owned = MPTeams.IsTeam(team);
            // Personal owner first, so anything reacting to the team change already sees who it belongs to.
            personalOwner.Value = owned && MPTeams.IsTeam(slot) ? slot : MPTeams.Neutral;
            owner.Value = owned ? team : MPTeams.Neutral;
        }

        // Host, before spawning: the starting allegiance (becomes the initial synced value).
        public void InitOwner(int team, int slot)
        {
            owner.Value = team;
            personalOwner.Value = slot;
        }

        // Host only: drops a capture in progress by `team` (used when a team has no players left).
        public void ClearCapture(int team)
        {
            if (!IsServer || capturer.Value != team) return;
            capturer.Value = MPTeams.Neutral;
            progress.Value = 0f;
        }
    }
}
