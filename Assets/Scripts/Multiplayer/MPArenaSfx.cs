using LoveShot.Audio;
using UnityEngine;

namespace LoveShot.Multiplayer
{
    // The arena's sound effects. Most play where they happen, on every machine (shots, impacts, bounces, conversions,
    // pickups, special NPCs, abilities). Charging, the ability-ready ping, the countdown, the last seconds and the
    // result sting are only for the local player. Ability use sounds are set on each MPAbilityDef asset.
    public class MPArenaSfx : MonoBehaviour
    {
        [SerializeField] GameAudio gameAudio;

        [Header("NPC joins a team")]
        [Tooltip("A neutral NPC is recruited.")]
        [SerializeField] Sound recruited;
        [Tooltip("An NPC changes from one team to another.")]
        [SerializeField] Sound stolen;
        [Tooltip("The Celebrity joins a team (recruited or stolen).")]
        [SerializeField] Sound celebrityJoined;

        [Header("Shots")]
        [Tooltip("Each projectile a player (or a zombie) fires; set a voice cap so a shotgun isn't five sounds.")]
        [SerializeField] Sound playerShot;
        [SerializeField] Sound npcShot;
        [SerializeField] Sound cupidArrow;
        [Tooltip("A shot lands on a character.")]
        [SerializeField] Sound impact;
        [Tooltip("A rocket explodes.")]
        [SerializeField] Sound explosion;
        [Tooltip("A shot ricochets off a wall.")]
        [SerializeField] Sound bounce;

        [Header("Bow (local player)")]
        [Tooltip("Loops while you hold fire; its pitch rises with the charge. Needs Loop ticked.")]
        [SerializeField] Sound chargeLoop;
        [Tooltip("Pitch of the charge loop at no charge (x) and at full charge (y).")]
        [SerializeField] Vector2 chargePitch = new Vector2(0.8f, 1.6f);
        [Tooltip("Played once when the charge is full.")]
        [SerializeField] Sound chargeFull;

        [Header("Players")]
        [Tooltip("A player is lovestruck (slowed) by an enemy shot.")]
        [SerializeField] Sound struck;
        [Tooltip("A player is converted to another team.")]
        [SerializeField] Sound converted;
        [Tooltip("A modifier pickup is collected.")]
        [SerializeField] Sound pickup;
        [Tooltip("One of your abilities is ready again.")]
        [SerializeField] Sound abilityReady;

        [Header("Special NPCs")]
        [SerializeField] Sound queenBurst;
        [SerializeField] Sound hug;

        [Header("Match flow (local)")]
        [Tooltip("Once a second during the countdown.")]
        [SerializeField] Sound countdownTick;
        [SerializeField] Sound go;
        [Tooltip("Once a second in the last seconds of the match.")]
        [SerializeField] Sound timeTick;
        [SerializeField, Min(1)] int timeTickFrom = 10;
        [Tooltip("The match ends with your team in first place (ties count).")]
        [SerializeField] Sound win;
        [SerializeField] Sound lose;

        SoundHandle chargeHandle;
        bool wasCharging;
        bool fullPlayed;
        readonly bool[] abilityWasReady = { true, true };
        MPMatch.MatchPhase lastPhase = MPMatch.MatchPhase.Waiting;
        int lastSecond = -1;

        void OnEnable()
        {
            MPCapture.OwnerChangedAnywhere += OnOwnerChanged;
            MPProjectile.Launched += OnLaunched;
            MPProjectile.Bounced += OnBounced;
            MPMatch.ImpactPlayed += OnImpact;
            MPPlayer.AbilityUsedAnywhere += OnAbilityUsed;
            MPPlayer.StruckAnywhere += OnStruck;
            MPPlayer.ConvertedAnywhere += OnConverted;
            MPWeaponPickup.CollectedAnywhere += OnPickup;
            MPNpcFlair.QueenBursted += OnQueenBurst;
            MPNpcFlair.HugLanded += OnHug;
        }

        void OnDisable()
        {
            MPCapture.OwnerChangedAnywhere -= OnOwnerChanged;
            MPProjectile.Launched -= OnLaunched;
            MPProjectile.Bounced -= OnBounced;
            MPMatch.ImpactPlayed -= OnImpact;
            MPPlayer.AbilityUsedAnywhere -= OnAbilityUsed;
            MPPlayer.StruckAnywhere -= OnStruck;
            MPPlayer.ConvertedAnywhere -= OnConverted;
            MPWeaponPickup.CollectedAnywhere -= OnPickup;
            MPNpcFlair.QueenBursted -= OnQueenBurst;
            MPNpcFlair.HugLanded -= OnHug;
            if (chargeHandle != null) chargeHandle.Stop();
        }

        void At(Sound sound, Vector3 position)
        {
            if (gameAudio != null) gameAudio.PlayAt(sound, position);
        }

        void OnOwnerChanged(MPCapture character, int previous, int current)
        {
            if (character.Npc == null || !MPTeams.IsTeam(current)) return;
            Sound sound = character.Npc.Type == MPNpc.NpcType.Celebrity ? celebrityJoined : MPTeams.IsTeam(previous) ? stolen : recruited;
            At(sound, character.transform.position);
        }

        void OnLaunched(MPProjectile projectile, bool fromPlayer) =>
            At(projectile.Style == ProjectileStyle.Arrow ? cupidArrow : fromPlayer ? playerShot : npcShot, projectile.transform.position);

        void OnBounced(MPProjectile projectile) => At(bounce, projectile.transform.position);

        void OnImpact(Vector2 position, float explosionRadius) => At(explosionRadius > 0f ? explosion : impact, position);

        void OnAbilityUsed(MPPlayer player, MPAbility ability)
        {
            MPAbilityDef def = MPAbilities.Get(ability);
            if (def != null) At(def.UseSound, player.Position);
        }

        void OnStruck(MPPlayer player) => At(struck, player.Position);

        void OnConverted(MPPlayer player) => At(converted, player.Position);

        void OnPickup(Vector2 position) => At(pickup, position);

        void OnQueenBurst(Vector2 position) => At(queenBurst, position);

        void OnHug(Vector2 position) => At(hug, position);

        void Update()
        {
            if (gameAudio == null) return;
            UpdateCharge(MPPlayer.Local);
            UpdateAbilityReady(MPPlayer.Local);
            UpdateFlow(MPMatch.Instance);
        }

        // A looping sound while the local player holds fire, rising in pitch, and a ping at full charge.
        void UpdateCharge(MPPlayer local)
        {
            bool charging = local != null && local.IsCharging && MPMatch.InProgress;
            if (charging && !wasCharging) chargeHandle = gameAudio.Hold(chargeLoop);
            if (!charging)
            {
                if (chargeHandle != null) chargeHandle.Stop();
                chargeHandle = null;
                fullPlayed = false;
            }
            wasCharging = charging;
            if (!charging) return;

            float charge = local.ChargeFraction;
            if (chargeHandle != null) chargeHandle.Pitch = Mathf.Lerp(chargePitch.x, chargePitch.y, charge);
            if (charge >= 1f && !fullPlayed)
            {
                fullPlayed = true;
                gameAudio.Play(chargeFull);
            }
        }

        // A ping when one of the local player's abilities comes off cooldown.
        void UpdateAbilityReady(MPPlayer local)
        {
            if (local == null) return;
            for (int slot = 0; slot < abilityWasReady.Length; slot++)
            {
                bool ready = MPMatch.InProgress && !local.IsZombie && !local.AbilityUsedUp(slot) && local.AbilityCooldownRemaining(slot) <= 0f;
                if (ready && !abilityWasReady[slot]) gameAudio.Play(abilityReady);
                abilityWasReady[slot] = ready || !MPMatch.InProgress;
            }
        }

        // Countdown ticks, the go, the last seconds, and the result sting.
        void UpdateFlow(MPMatch match)
        {
            if (match == null) return;
            MPMatch.MatchPhase phase = match.Phase;
            if (phase != lastPhase)
            {
                if (phase == MPMatch.MatchPhase.Playing) gameAudio.Play(go);
                else if (phase == MPMatch.MatchPhase.Ended) gameAudio.Play(LocalWon(match) ? win : lose);
                lastPhase = phase;
                lastSecond = -1;
            }

            int second = -1;
            Sound tick = null;
            if (phase == MPMatch.MatchPhase.Countdown)
            {
                second = Mathf.CeilToInt(match.CountdownRemaining);
                tick = countdownTick;
            }
            else if (phase == MPMatch.MatchPhase.Playing)
            {
                second = Mathf.CeilToInt(match.TimeRemaining);
                if (second > timeTickFrom) second = -1;
                tick = timeTick;
            }
            if (second <= 0 || second == lastSecond) return;
            lastSecond = second;
            gameAudio.Play(tick);
        }

        // Whether the local player's team finished first (ties count).
        static bool LocalWon(MPMatch match)
        {
            MPPlayer local = MPPlayer.Local;
            if (local == null) return false;
            foreach ((MPScore score, int rank) in match.Ranking())
                if (score.Team == local.Team) return rank == 1;
            return false;
        }
    }
}
