using System;
using Unity.Netcode;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Free-for-all allegiance for one NPC: an owner team (or Neutral), the team currently capturing it,
    // and capture progress 0..1. Only the host changes it; every client reads the synced values.
    //
    // Rules for a hit by team T (all deterministic, applied in hit order on the host):
    //  - T owns it: T's shots wear down whoever is capturing it (loyalty is restored). A fully loyal NPC
    //    ignores its own team's shots, so they pass through.
    //  - Nobody, or T, is capturing it: T's progress grows. At 1 the NPC joins T and progress resets.
    //  - Another team C is capturing it: T first wears C's progress down to 0; the overflow becomes T's progress.
    // Capturing a neutral takes captureAmount; stealing an owned NPC takes stealAmount (it breaks allegiance first).
    public class MPCapture : NetworkBehaviour
    {
        [Tooltip("Shot power needed to recruit a neutral NPC.")]
        [SerializeField, Min(1f)] float captureAmount = 100f;
        [Tooltip("Shot power needed to steal an NPC another team owns.")]
        [SerializeField, Min(1f)] float stealAmount = 200f;
        [Tooltip("Multiplies incoming shot power. For special NPC types that convert slower or faster.")]
        [SerializeField, Min(0f)] float sensitivity = 1f;

        readonly NetworkVariable<int> owner = new(MPTeams.Neutral);
        readonly NetworkVariable<int> capturer = new(MPTeams.Neutral);
        readonly NetworkVariable<float> progress = new(0f);
        // Declared before `hits` so clients have it updated when the hit counter's callback runs.
        readonly NetworkVariable<int> lastHitTeam = new(MPTeams.Neutral);
        // Increments on every accepted hit, so clients can flash even when progress lands on the same value.
        readonly NetworkVariable<int> hits = new(0);

        public int Owner => owner.Value;
        public int Capturer => capturer.Value;
        public float Progress => progress.Value;

        // (previous, current) owner, raised on every machine.
        public event Action<int, int> OwnerChanged;
        // Raised on every machine for each accepted hit, with the shooter's team.
        public event Action<int> HitReceived;

        public override void OnNetworkSpawn()
        {
            owner.OnValueChanged += OnOwnerValueChanged;
            hits.OnValueChanged += OnHitsChanged;
        }

        public override void OnNetworkDespawn()
        {
            owner.OnValueChanged -= OnOwnerValueChanged;
            hits.OnValueChanged -= OnHitsChanged;
        }

        void OnOwnerValueChanged(int previous, int current) => OwnerChanged?.Invoke(previous, current);

        void OnHitsChanged(int previous, int current) => HitReceived?.Invoke(lastHitTeam.Value);

        // False only when the shot could change nothing: a fully loyal NPC hit by its own team.
        public bool CanBeAffectedBy(int team) =>
            MPTeams.IsTeam(team) && !(owner.Value == team && capturer.Value == MPTeams.Neutral);

        // Host only. power is the shot's strength in the same units as captureAmount/stealAmount.
        public void ApplyHit(int team, float power)
        {
            if (!IsServer || !CanBeAffectedBy(team) || power <= 0f) return;

            float required = owner.Value == MPTeams.Neutral ? captureAmount : stealAmount;
            float delta = power * sensitivity / required;
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

            lastHitTeam.Value = team;
            hits.Value++;
            if (currentCapturer == team && current >= 1f)
            {
                SetOwner(team);
                return;
            }
            capturer.Value = currentCapturer;
            progress.Value = Mathf.Clamp01(current);
        }

        // Host only: instantly sets the owner and clears any capture in progress.
        public void SetOwner(int team)
        {
            if (!IsServer) return;
            capturer.Value = MPTeams.Neutral;
            progress.Value = 0f;
            owner.Value = MPTeams.IsTeam(team) ? team : MPTeams.Neutral;
        }
    }
}
