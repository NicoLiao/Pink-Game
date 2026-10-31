using System;
using Unity.Netcode;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Drama Queen: whenever she joins a team, she bursts a ring of hearts in her new team's color that
    // pushes every nearby NPC (and, more weakly, every opposing player) toward that team. A burst can
    // convert another Drama Queen, which bursts in turn: a chain reaction.
    // Chains always end: each Queen bursts at most once per cooldown, the burst waits a short delay (so the
    // chain ripples visibly instead of recursing in one frame), and bursts only ever add love for one team.
    public class MPDramaQueenNpc : MPNpc
    {
        [Header("Drama burst")]
        [SerializeField, Min(0f)] float burstRadius = 4.5f;
        [Tooltip("Love given to each NPC in range.")]
        [SerializeField, Min(0f)] float npcBurstPower = 40f;
        [Tooltip("Love given to each opposing player in range.")]
        [SerializeField, Min(0f)] float playerBurstPower = 15f;
        [Tooltip("Seconds between joining a team and bursting.")]
        [SerializeField, Min(0f)] float burstDelay = 0.35f;
        [Tooltip("Minimum seconds between two bursts of the same Queen.")]
        [SerializeField, Min(0f)] float burstCooldown = 4f;

        readonly NetworkVariable<double> burstReadyTime = new(0d);

        float pendingBurstTime = float.NaN;

        public float BurstRadius => burstRadius;
        public bool BurstReady => NetworkManager != null && NetworkManager.ServerTime.Time >= burstReadyTime.Value;

        // Raised on every machine when she bursts, with the team color.
        public event Action<int> Burst;

        protected override void OnTeamChanged(int previous, int current)
        {
            base.OnTeamChanged(previous, current);
            if (!MPTeams.IsTeam(current) || !BurstReady || !float.IsNaN(pendingBurstTime)) return;
            pendingBurstTime = Time.time + burstDelay;
            burstReadyTime.Value = NetworkManager.ServerTime.Time + burstCooldown;
        }

        protected override void Update()
        {
            base.Update();
            if (!IsServer || float.IsNaN(pendingBurstTime) || Time.time < pendingBurstTime) return;
            pendingBurstTime = float.NaN;
            DoBurst();
        }

        void DoBurst()
        {
            MPMatch match = MPMatch.Instance;
            // She may have been stolen again during the delay; the burst is for whoever she's with now.
            if (match == null || !MPTeams.IsTeam(Team) || !MPMatch.InProgress) return;
            int team = Team, credit = PersonalOwner;
            BurstRpc(team);
            Vector2 center = transform.position;
            foreach (MPCapture target in MPCapture.All)
            {
                if (target == Capture || target.Owner == team || Vector2.Distance(center, target.transform.position) > burstRadius) continue;
                match.ApplyLove(target, credit, target.Player != null ? playerBurstPower : npcBurstPower, fromPlayer: false);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        void BurstRpc(int team) => Burst?.Invoke(team);
    }
}
