using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // Big-moment announcements under the timer, on every machine: players being converted (with extra
    // emphasis when it's you or your team), the Celebrity changing hands and Drama Queen bursts.
    // Messages queue up and show one at a time with a pop; a long queue plays faster.
    public class MPFeed : MonoBehaviour
    {
        [SerializeField] TMP_Text text;
        [SerializeField, Min(0.2f)] float messageDuration = 2.2f;
        [SerializeField, Min(0.2f)] float hurriedDuration = 1.1f;
        [SerializeField, Min(1f)] float popScale = 1.3f;
        [SerializeField, Min(0.01f)] float popDuration = 0.25f;

        struct Message
        {
            public string Text;
            public Color Color;
        }

        readonly Queue<Message> queue = new();
        readonly HashSet<MPPlayer> watchedPlayers = new();
        readonly HashSet<MPNpc> watchedNpcs = new();
        float shownAt = float.NegativeInfinity;
        float showFor;

        void Start()
        {
            if (text != null) text.gameObject.SetActive(false);
        }

        void Post(string message, Color color) => queue.Enqueue(new Message { Text = message, Color = color });

        // Subscribes to anything new (players and NPCs spawn after the HUD; they're destroyed with the scene).
        void Watch()
        {
            foreach (MPPlayer player in MPPlayer.All)
                if (watchedPlayers.Add(player)) player.Converted += (previous, current) => Announce(player, previous, current);
            foreach (MPNpc npc in MPNpc.All)
            {
                if (!watchedNpcs.Add(npc)) continue;
                if (npc.Type == MPNpc.NpcType.Celebrity) npc.Capture.OwnerChanged += (_, team) => OnCelebrity(team);
                if (npc is MPDramaQueenNpc queen) queen.Burst += OnDramaBurst;
            }
        }

        void Announce(MPPlayer player, int previous, int current)
        {
            MPPlayer local = MPPlayer.Local;
            string team = MPTeams.NameOf(current).ToUpperInvariant();
            if (player == local) Post($"YOU FELL FOR {team}!\n<size=55%>You now play for {team}. Your NPCs came with you.</size>", MPTeams.TextColorOf(current));
            else if (local != null && current == local.Team) Post($"{player.DisplayName} FELL FOR YOUR TEAM!", MPTeams.TextColorOf(current));
            else if (local != null && previous == local.Team) Post($"YOUR TEAMMATE {player.DisplayName} FELL FOR {team}!", MPTeams.TextColorOf(current));
            else Post($"{player.DisplayName} FELL FOR {team}!", MPTeams.TextColorOf(current));
        }

        void OnCelebrity(int team)
        {
            if (!MPTeams.IsTeam(team)) return;
            Post($"{MPTeams.NameOf(team).ToUpperInvariant()} WON THE CELEBRITY! <size=70%>(+3)</size>", MPTeams.TextColorOf(team));
        }

        void OnDramaBurst(int team) => Post($"DRAMA! <size=70%>{MPTeams.NameOf(team).ToUpperInvariant()} HEART BURST</size>", MPTeams.TextColorOf(team));

        void Update()
        {
            Watch();
            if (text == null) return;
            if (Time.unscaledTime - shownAt >= showFor)
            {
                if (queue.Count == 0)
                {
                    text.gameObject.SetActive(false);
                    return;
                }
                Message next = queue.Dequeue();
                text.text = next.Text;
                text.color = next.Color;
                text.gameObject.SetActive(true);
                shownAt = Time.unscaledTime;
                showFor = queue.Count > 0 ? hurriedDuration : messageDuration;
            }
            float t = (Time.unscaledTime - shownAt) / popDuration;
            text.transform.localScale = Vector3.one * (1f + (t < 1f ? Mathf.Sin(t * Mathf.PI) * (popScale - 1f) : 0f));
        }
    }
}
