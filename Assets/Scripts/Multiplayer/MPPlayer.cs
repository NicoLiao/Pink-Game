using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LoveShot.Multiplayer
{
    // A networked player. Movement is owner-driven so it responds instantly: the owning machine moves its
    // own character with the shared Mover (walls and obstacles included) and its NetworkTransform (Owner
    // authority) streams the position to everyone. Dash and shots also start at once on the owner. The host
    // stays the judge of everything that matters: it checks each player's speed against a movement budget
    // and snaps a player back if they moved farther than possible, enforces the dash and shot cooldowns,
    // fires the authoritative projectiles and resolves every hit, lovestruck slow and conversion.
    //
    // Identity vs allegiance: Slot is permanent (lobby slot, name, identity color). Team is the team the
    // player currently fights for (its MPCapture owner); it starts as the lobby team (own slot in FFA, side 0 or 1 in 2v2) and changes when another
    // team converts the player. A converted player takes their own NPCs along. In FFA, converting a team's
    // lead converts the whole team, and with zombie mode on every converted player walks beside their team's
    // lead (the owner's movement input is replaced) while still aiming and shooting.
    [RequireComponent(typeof(Mover), typeof(MPCapture))]
    public class MPPlayer : NetworkBehaviour
    {
        [Header("Shooting (weapon stats come from the MPWeapon assets on MPMatch)")]
        [SerializeField, Min(0f)] float spawnOffset = 0.6f;
        [Tooltip("Host: a shot may arrive this fraction of its charge time early (network jitter bunches messages).")]
        [SerializeField, Range(0f, 0.9f)] float fireTimingGrace = 0.4f;
        [Tooltip("Host: the owner's muzzle position is trusted within this distance of where the host sees the player.")]
        [SerializeField, Min(0f)] float maxOriginError = 3.5f;

        [Header("Dash")]
        [Tooltip("Host: seconds of clock error tolerated when checking the dash cooldown.")]
        [SerializeField, Min(0f)] float dashGrace = 0.3f;

        [Header("Abilities (numbers and icons are the MPAbilityDef assets in Assets/Settings/Resources/Abilities)")]
        [Tooltip("Ring shown around a player using an ability.")]
        [SerializeField] HeartRing abilityRingPrefab;

        [Header("Lovestruck (hit by an enemy player)")]
        [SerializeField, Min(0f)] float struckDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] float struckSpeedMultiplier = 0.35f;
        [SerializeField, Min(0f)] float struckImmunity = 1f;

        [Header("Conversion")]
        [Tooltip("Seconds a freshly converted player can't be converted again (shots pass through).")]
        [SerializeField, Min(0f)] float conversionImmunity = 3f;

        [Header("Zombie (converted player in FFA zombie mode: walks beside the team lead, still aims and shoots)")]
        [Tooltip("How far to the side of the lead a zombie walks.")]
        [SerializeField, Min(0f)] float zombieSideDistance = 1.6f;
        [Tooltip("Stops this far from its walking spot.")]
        [SerializeField, Min(0f)] float zombieStopDistance = 0.3f;
        [Tooltip("Beyond this distance from the lead a zombie speeds up to catch up.")]
        [SerializeField, Min(0f)] float zombieCatchUpDistance = 6f;
        [SerializeField, Range(1f, 1.3f)] float zombieCatchUpMultiplier = 1.25f;
        [SerializeField] LayerMask obstacleMask;
        [SerializeField, Min(0f)] float lookAhead = 0.9f;
        [SerializeField, Min(0f)] float bodyRadius = 0.4f;

        [Header("Movement check (host)")]
        [Tooltip("Allowed speed as a multiple of the move speed (positions arrive unevenly).")]
        [SerializeField, Min(1f)] float speedTolerance = 1.3f;
        [Tooltip("Seconds of full-speed movement that may arrive in one burst (after a lag spike).")]
        [SerializeField, Min(0.1f)] float burstSeconds = 0.75f;

        [Header("Aim")]
        [Tooltip("The aim direction is shared with everyone when it turns more than this many degrees.")]
        [SerializeField, Min(0f)] float aimSyncAngle = 2f;
        [SerializeField] AimIndicator aimIndicator;

        static readonly Dictionary<int, MPPlayer> bySlot = new();
        static readonly List<MPPlayer> all = new();
        public static IReadOnlyList<MPPlayer> All => all;
        public static MPPlayer Local { get; private set; }
        public static event Action<MPPlayer> LocalPlayerSpawned;

        readonly NetworkVariable<int> slot = new(MPTeams.Neutral);
        readonly NetworkVariable<Vector2> aim = new(Vector2.up, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<double> dashReadyTime = new(0d);
        // The two abilities (set at spawn), when each cooldown ability is ready again, and whether Duplicate was spent.
        readonly NetworkVariable<MPAbilityState> abilities = new(new MPAbilityState { Ability1 = MPAbility.Heal });
        readonly NetworkVariable<double> struckUntil = new(0d);
        readonly NetworkVariable<double> conversionImmuneUntil = new(0d);
        // Slot of the player who converted this one (a zombie walks beside them).
        readonly NetworkVariable<int> followSlot = new(MPTeams.Neutral);
        // Timed modifiers stacked on the bow (MPMatch weapon index + when each runs out); the host adds and removes them.
        readonly NetworkList<MPModifier> modifiers = new();
        readonly List<MPWeapon> activeModifiers = new();

        Mover mover;
        Rigidbody2D body;
        MPCapture capture;
        NetworkTransform networkTransform;
        Camera aimCamera;
        Skill[] skills;

        // Owner-side.
        Vector2 lastMoveDirection = Vector2.right;
        float speedBoost = 1f;
        float nextFireTime;
        bool charging;
        float chargeStart;
        ushort shotSequence;
        float predictedDashReady;
        readonly float[] predictedAbilityReady = new float[2];

        // Host-side.
        float serverNextShotTime;
        double immuneUntil;
        float moveBudget;
        Vector2 lastCheckedPosition;
        Vector2 lastValidPosition;
        Vector2 observedVelocity;
        float checkResumeTime;

        public static MPPlayer ForSlot(int slot) => bySlot.TryGetValue(slot, out MPPlayer player) ? player : null;

        // True if any player currently fights for `team`.
        public static bool TeamHasPlayers(int team)
        {
            foreach (MPPlayer player in all)
                if (player.Team == team) return true;
            return false;
        }

        public int Slot => slot.Value;
        public int Team => capture.Owner;
        // FFA: a team's lead is the player it started with. Converting the lead converts the whole team.
        public bool IsLead => Team == Slot;
        // FFA zombie mode: a converted player (on any team but their own) follows their team's lead.
        public bool IsZombie => MPLobby.Instance != null && MPLobby.Instance.UsesZombies && !IsLead;
        public Vector2 AimDirection => aim.Value;

        // The player a zombie walks beside: its converter, or the team's lead if the converter has since left the team.
        public MPPlayer FollowTarget
        {
            get
            {
                MPPlayer converter = ForSlot(followSlot.Value);
                return converter != null && converter != this && converter.Team == Team ? converter : ForSlot(Team);
            }
        }
        public MPCapture Capture => capture;
        public bool IsConversionImmune => Now < conversionImmuneUntil.Value;
        public float ConversionImmunityRemaining => Mathf.Max(0f, (float)(conversionImmuneUntil.Value - Now));
        // The bow every player holds (MPMatch weapon 0).
        public MPWeapon Bow => MPMatch.Instance != null ? MPMatch.Instance.WeaponAt(0) : null;
        public int ModifierCount => modifiers.Count;
        public MPWeapon ModifierAt(int i) => MPMatch.Instance != null ? MPMatch.Instance.WeaponAt(modifiers[i].Index) : null;
        public float ModifierRemaining(int i) => Mathf.Max(0f, (float)(modifiers[i].Expire - Now));
        // Seconds of holding fire for a full charge with the current modifiers.
        public float FullChargeTime => Bow != null ? Firing(1f).ChargeTime : 1f;
        // 0..1 while the owner holds fire, else 0.
        public float ChargeFraction => charging ? Mathf.Clamp01((Time.time - chargeStart) / FullChargeTime) : 0f;
        public bool IsCharging => charging;
        // The charge below which releasing fires nothing.
        public float MinCharge => Bow != null ? Bow.MinCharge : 0f;
        public Vector2 Position => transform.position;
        // Meaningful on the host (NPC following): the real velocity for its own player, a measured one for remote players.
        public Vector2 Velocity => IsOwner ? mover.Velocity : observedVelocity;
        // Ability slot 0 is Space, slot 1 is Shift.
        public MPAbility AbilityOf(int abilitySlot) => abilitySlot == 0 ? abilities.Value.Ability0 : abilities.Value.Ability1;
        public bool AbilityUsedUp(int abilitySlot) => (abilities.Value.UsedMask & (1 << abilitySlot)) != 0;
        public float AbilityCooldown(int abilitySlot) => Def(AbilityOf(abilitySlot)).Cooldown;
        public float AbilityCooldownRemaining(int abilitySlot)
        {
            if (AbilityOf(abilitySlot) == MPAbility.Dash) return DashCooldownRemaining;
            double ready = abilitySlot == 0 ? abilities.Value.Ready0 : abilities.Value.Ready1;
            return Mathf.Max((float)(ready - Now), IsOwner ? predictedAbilityReady[abilitySlot] - Time.time : 0f, 0f);
        }
        public float DashCooldown => Def(MPAbility.Dash).Cooldown;
        static MPAbilityDef Def(MPAbility kind) => MPAbilities.Get(kind);
        // The owner counts down from its own dash at once, without waiting for the host's confirmation.
        public float DashCooldownRemaining => Mathf.Max((float)(dashReadyTime.Value - Now), IsOwner ? predictedDashReady - Time.time : 0f, 0f);
        public bool IsStruck => Now < struckUntil.Value;
        public string DisplayName => MPLobby.Instance != null ? MPLobby.Instance.NameOf(slot.Value) : MPTeams.NameOf(slot.Value);
        double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;

        // Raised on every machine (immediately on the owner).
        public event Action<Vector2> Dashed;
        public event Action Struck;
        // (previous team, new team), raised on every machine when this player is converted.
        public event Action<int, int> Converted;
        // Weapon index, raised on every machine when this player picks up a modifier.
        public event Action<int> Equipped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            bySlot.Clear();
            all.Clear();
            Local = null;
            LocalPlayerSpawned = null;
        }

        // Players walk through NPCs. Each owner simulates only its own body (everyone else is kinematic there),
        // so solid NPCs would block a client's player on its screen while the host pushed them aside.
        public static void IgnoreBodies(Component a, Component b)
        {
            foreach (Collider2D first in a.GetComponents<Collider2D>())
                foreach (Collider2D second in b.GetComponents<Collider2D>())
                    Physics2D.IgnoreCollision(first, second);
        }

        void Awake()
        {
            mover = GetComponent<Mover>();
            body = GetComponent<Rigidbody2D>();
            capture = GetComponent<MPCapture>();
            networkTransform = GetComponent<NetworkTransform>();
            skills = GetComponents<Skill>();
        }

        // Host, before spawning: the abilities chosen in the lobby.
        public void InitAbilities(MPAbility space, MPAbility shift) => abilities.Value = new MPAbilityState { Ability0 = space, Ability1 = shift };

        // Host, before spawning: the permanent slot (identity) and the team the lobby assigned it.
        public void InitSlot(int playerSlot, int startTeam)
        {
            slot.Value = playerSlot;
            capture.InitOwner(startTeam, playerSlot);
        }

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            bySlot[slot.Value] = this;
            capture.OwnerChanged += OnTeamChanged;
            foreach (MPNpc npc in MPNpc.All) IgnoreBodies(this, npc);
            // Only the owner simulates movement; everyone else follows the NetworkTransform.
            mover.enabled = IsOwner;
            lastCheckedPosition = lastValidPosition = transform.position;
            moveBudget = MaxBudget;
            if (!IsOwner) return;

            Local = this;
            aimCamera = Camera.main;
            // Before any input, "last direction" points toward the arena center.
            lastMoveDirection = -((Vector2)transform.position).normalized;
            if (lastMoveDirection == Vector2.zero) lastMoveDirection = Vector2.right;
            LocalPlayerSpawned?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            capture.OwnerChanged -= OnTeamChanged;
            if (ForSlot(slot.Value) == this) bySlot.Remove(slot.Value);
            if (Local == this) Local = null;
        }

        void OnTeamChanged(int previous, int current)
        {
            if (MPTeams.IsTeam(previous) && MPTeams.IsTeam(current)) Converted?.Invoke(previous, current);
        }

        void Update()
        {
            if (!IsSpawned || NetworkManager == null || !NetworkManager.IsListening) return;
            if (IsOwner) ReadOwnerInput();
            else if (aimIndicator != null) aimIndicator.Point(aim.Value);
            if (IsServer) Simulate();
        }

        // ---------- Owner ----------

        void ReadOwnerInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (aimCamera == null) aimCamera = Camera.main;
            bool live = MPMatch.InProgress && keyboard != null && mouse != null && aimCamera != null;

            Vector2 move = Vector2.zero;
            Vector2 aimDirection = aim.Value;
            bool fire = false;
            speedBoost = 1f;
            bool zombie = IsZombie;
            if (live)
            {
                if (zombie) move = ZombieMove();
                else
                {
                    if (keyboard.aKey.isPressed) move.x -= 1f;
                    if (keyboard.dKey.isPressed) move.x += 1f;
                    if (keyboard.sKey.isPressed) move.y -= 1f;
                    if (keyboard.wKey.isPressed) move.y += 1f;
                    move = move.normalized;
                }
                if (move != Vector2.zero) lastMoveDirection = move;

                if (zombie)
                {
                    // A zombie's aim mirrors the player it follows, and it fires when they do (host side).
                    MPPlayer leader = FollowTarget;
                    if (leader != null && leader != this) aimDirection = leader.AimDirection;
                }
                else
                {
                    Vector2 mouseWorld = aimCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                    Vector2 toMouse = mouseWorld - (Vector2)transform.position;
                    if (toMouse.sqrMagnitude > 0.0001f) aimDirection = toMouse.normalized;
                    fire = mouse.leftButton.isPressed;
                }

                // Skills go where you're moving; if standing still, in the last direction you moved.
                if (!zombie)
                    foreach (Skill skill in skills)
                        if (keyboard[skill.Key].wasPressedThisFrame) skill.TryActivate(lastMoveDirection);
            }
            OwnerTick(move, aimDirection, fire);
        }

        // Owner, zombie: walk to a spot beside the lead (alternating sides among the team's zombies), around obstacles.
        Vector2 ZombieMove()
        {
            MPPlayer lead = FollowTarget;
            if (lead == null || lead == this) return Vector2.zero;
            int index = 0;
            foreach (MPPlayer other in MPMatch.MembersOf(Team))
            {
                if (other == this) break;
                if (other != lead && other.IsZombie && other.FollowTarget == lead) index++;
            }
            Vector2 facing = lead.Velocity.sqrMagnitude > 0.25f ? lead.Velocity.normalized : lead.AimDirection;
            float side = (index % 2 == 0 ? 1f : -1f) * (1 + index / 2);
            Vector2 spot = lead.Position + Vector2.Perpendicular(facing) * side * zombieSideDistance;

            Vector2 toSpot = spot - (Vector2)transform.position;
            float distance = toSpot.magnitude;
            if (Vector2.Distance(transform.position, lead.Position) > zombieCatchUpDistance) speedBoost = zombieCatchUpMultiplier;
            if (distance < 0.001f) return Vector2.zero;
            float easing = Mathf.Max(zombieStopDistance, 0.5f);
            Vector2 desired = toSpot / distance * Mathf.Clamp01((distance - zombieStopDistance) / easing);
            return Steering.AvoidObstacles(transform.position, desired, bodyRadius, lookAhead, obstacleMask);
        }

        // One frame of owner input: moves locally, shares the aim, and fires (predicted, then confirmed by the host).
        void OwnerTick(Vector2 move, Vector2 aimDirection, bool fire)
        {
            bool live = MPMatch.InProgress;
            bool struck = IsStruck;
            mover.SpeedMultiplier = struck ? struckSpeedMultiplier : speedBoost;
            mover.SetMoveInput(live ? Vector2.ClampMagnitude(move, 1f) : Vector2.zero);
            if (aimDirection.sqrMagnitude > 0.0001f)
            {
                aimDirection.Normalize();
                if (Vector2.Angle(aim.Value, aimDirection) > aimSyncAngle) aim.Value = aimDirection;
            }
            if (aimIndicator != null) aimIndicator.Point(aimDirection);
            if (live && !struck && !IsZombie) UpdateCharge(fire, aimDirection);
            else charging = false;
        }

        // Holding fire charges the bow; releasing shoots (a release before the minimum charge cancels).
        void UpdateCharge(bool fire, Vector2 direction)
        {
            if (Bow == null)
            {
                charging = false;
                return;
            }
            if (fire)
            {
                if (!charging)
                {
                    charging = true;
                    chargeStart = Mathf.Max(Time.time, nextFireTime);
                }
                return;
            }
            if (!charging) return;
            float charge = ChargeFraction;
            charging = false;
            if (charge >= Bow.MinCharge) TryFire(direction, charge);
        }

        void TryFire(Vector2 direction, float charge)
        {
            MPWeapon bow = Bow;
            if (bow == null || Time.time < nextFireTime || direction.sqrMagnitude < 0.0001f) return;
            byte sentCharge = (byte)Mathf.RoundToInt(Mathf.Clamp01(charge) * 255f);
            MPFiring firing = Firing(sentCharge / 255f);
            nextFireTime = Time.time + bow.Recovery;
            Vector2 origin = (Vector2)transform.position + direction * spawnOffset;
            if (++shotSequence == 0) shotSequence = 1;
            // A client shows its shot right away; the host's projectiles take over these copies when they arrive.
            if (!IsServer && MPMatch.Instance != null) MPMatch.Instance.PredictWeapon(firing, Team, Slot, origin, direction, NetworkObjectId, shotSequence);
            ShootRpc(origin, direction, shotSequence, sentCharge);
        }

        // The bow with every running modifier stacked on it, at `charge` (0..1).
        MPFiring Firing(float charge)
        {
            activeModifiers.Clear();
            for (int i = 0; i < modifiers.Count; i++)
                if (Now < modifiers[i].Expire) activeModifiers.Add(MPMatch.Instance.WeaponAt(modifiers[i].Index));
            return MPWeapon.Resolve(Bow, activeModifiers, charge);
        }

        // Owner: dashes at once. The host checks the cooldown and shows the dash to everyone else.
        public bool TryDash(Vector2 direction)
        {
            if (!IsOwner || IsZombie || !MPMatch.InProgress || direction.sqrMagnitude < 0.0001f || DashCooldownRemaining > 0f) return false;
            direction.Normalize();
            if (!mover.TryDash(direction)) return false;
            predictedDashReady = Time.time + DashCooldown;
            Dashed?.Invoke(direction);
            RequestDashRpc(direction);
            return true;
        }

        // Owner: uses the ability in a slot (Dash happens at once; the others are decided by the host).
        public bool TryUseAbility(int abilitySlot, Vector2 direction)
        {
            MPAbility current = AbilityOf(abilitySlot);
            if (current == MPAbility.Dash) return TryDash(direction);
            if (!IsOwner || IsZombie || !MPMatch.InProgress || AbilityUsedUp(abilitySlot) || AbilityCooldownRemaining(abilitySlot) > 0f) return false;
            // Shows the cooldown at once; Duplicate is only guarded briefly (it may do nothing if you have no followers).
            predictedAbilityReady[abilitySlot] = Time.time + (Def(current).OncePerMatch ? 0.5f : AbilityCooldown(abilitySlot));
            UseAbilityRpc((byte)abilitySlot);
            return true;
        }

        // Owner: the host moved us back (we moved farther than the movement check allows).
        [Rpc(SendTo.Owner)]
        void CorrectPositionRpc(Vector2 position)
        {
            networkTransform.Teleport(position, transform.rotation, transform.localScale);
            body.position = position;
            body.linearVelocity = Vector2.zero;
        }

        [Rpc(SendTo.Owner)]
        void RejectShotRpc(ushort sequence)
        {
            if (MPMatch.Instance != null) MPMatch.Instance.CancelPrediction(sequence);
        }

        // ---------- Host ----------

        float MaxBudget => mover.MoveSpeed * speedTolerance * burstSeconds;

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void ShootRpc(Vector2 origin, Vector2 direction, ushort sequence, byte chargeByte)
        {
            MPWeapon bow = Bow;
            float charge = chargeByte / 255f;
            bool accepted = MPMatch.InProgress && !IsStruck && bow != null && direction.sqrMagnitude > 0.0001f && charge >= bow.MinCharge * (1f - fireTimingGrace);
            MPFiring firing = default;
            float needed = 0f;
            if (accepted)
            {
                // The charge took real time: the host never sees it held, so it checks that long enough has passed.
                firing = Firing(charge);
                needed = bow.Recovery + firing.ChargeTime * charge;
                accepted = Time.time >= serverNextShotTime - needed * fireTimingGrace;
            }
            if (!accepted)
            {
                if (!IsOwnedByServer) RejectShotRpc(sequence);
                return;
            }
            // Bunched messages may come a little early, but the long-run rate never beats the charge time.
            serverNextShotTime = Mathf.Max(Time.time, serverNextShotTime) + needed;
            direction.Normalize();
            // The owner is ahead of the host's view of it, so its muzzle position is used if it's plausible.
            Vector2 expected = (Vector2)transform.position + direction * spawnOffset;
            if (Vector2.Distance(origin, expected) > maxOriginError) origin = expected;
            MPMatch.Instance.FireWeapon(firing, Team, Slot, origin, direction, NetworkObjectId, IsOwnedByServer ? (ushort)0 : sequence);

            // Zombies following this player attack with them: same shot, same direction, from their own position.
            foreach (MPPlayer zombie in all)
                if (zombie != this && zombie.IsZombie && !zombie.IsStruck && zombie.FollowTarget == this)
                    zombie.ServerMirrorShot(firing, direction);
        }

        // Host: fires `firing` in `direction` from this zombie, because the player it follows just fired.
        void ServerMirrorShot(MPFiring firing, Vector2 direction)
        {
            Vector2 origin = (Vector2)transform.position + direction * spawnOffset;
            MPMatch.Instance.FireWeapon(firing, Team, Slot, origin, direction, NetworkObjectId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void UseAbilityRpc(byte abilitySlot)
        {
            if (abilitySlot > 1) return;
            MPAbility current = AbilityOf(abilitySlot);
            MPAbilityState state = abilities.Value;
            double ready = abilitySlot == 0 ? state.Ready0 : state.Ready1;
            if (current == MPAbility.Dash || !MPMatch.InProgress || IsZombie || AbilityUsedUp(abilitySlot) || Now < ready - dashGrace) return;
            switch (current)
            {
                case MPAbility.Heal:
                    ServerHeal();
                    break;
                case MPAbility.Powerup:
                    ServerSpawnPowerup();
                    break;
                case MPAbility.Duplicate:
                    if (!ServerDuplicate()) return;
                    break;
            }
            if (Def(current).OncePerMatch) state.UsedMask |= (byte)(1 << abilitySlot);
            if (abilitySlot == 0) state.Ready0 = Now + AbilityCooldown(abilitySlot);
            else state.Ready1 = Now + AbilityCooldown(abilitySlot);
            abilities.Value = state;
            AbilityUsedRpc(Def(current).Radius);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void AbilityUsedRpc(float radius) => HeartRing.Play(abilityRingPrefab, transform.position, MPTeams.ColorOf(Team), radius, 18);

        // Host: everything on this team in range gets its loyalty back.
        void ServerHeal()
        {
            foreach (MPCapture member in MPCapture.All)
                if (member.Owner == Team && Vector2.Distance(member.transform.position, transform.position) <= Def(MPAbility.Heal).Radius) member.ServerHeal();
        }

        // Host: a random modifier pickup appears in front of the player.
        void ServerSpawnPowerup()
        {
            Vector2 spot = (Vector2)transform.position + AimDirection * Def(MPAbility.Powerup).Distance;
            if (Physics2D.OverlapCircle(spot, 0.5f, obstacleMask) != null) spot = transform.position;
            if (MPMatch.Instance != null && MPMatch.Instance.Pickups != null) MPMatch.Instance.Pickups.ServerSpawnAt(spot);
        }

        // Host: copies every follower (except the Celebrity) that hasn't been duplicated yet; originals and copies shrink and get weaker.
        bool ServerDuplicate()
        {
            var followers = new List<MPNpc>();
            foreach (MPNpc npc in MPNpc.All)
                if (npc.CanDuplicate && !npc.IsShrunk && npc.PersonalOwner == Slot && npc.Team == Team) followers.Add(npc);
            if (followers.Count == 0) return false;
            MPAbilityDef duplicate = Def(MPAbility.Duplicate);
            foreach (MPNpc npc in followers)
            {
                Vector2 spot = (Vector2)npc.transform.position + UnityEngine.Random.insideUnitCircle.normalized * duplicate.Spread;
                if (Physics2D.OverlapCircle(spot, 0.4f, obstacleMask) != null) spot = npc.transform.position;
                MPNpc copy = MPMatch.Instance.ServerSpawnNpcLike(npc, spot, Team, Slot);
                npc.ServerShrink(duplicate.Size, duplicate.HealthMultiplier);
                if (copy != null) copy.ServerShrink(duplicate.Size, duplicate.HealthMultiplier);
            }
            return true;
        }

        // The host is the only judge of the cooldown; a dash it doesn't grant gets no movement allowance.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void RequestDashRpc(Vector2 direction)
        {
            if (!MPMatch.InProgress || direction.sqrMagnitude < 0.0001f || Now < dashReadyTime.Value - dashGrace) return;
            dashReadyTime.Value = Now + DashCooldown;
            moveBudget += mover.DashDistance;
            DashedRpc(direction.normalized);
        }

        [Rpc(SendTo.NotOwner)]
        void DashedRpc(Vector2 direction) => Dashed?.Invoke(direction);

        void Simulate()
        {
            for (int i = modifiers.Count - 1; i >= 0; i--)
                if (Now >= modifiers[i].Expire) modifiers.RemoveAt(i);
            if (!IsOwner) CheckMovement();
        }

        // Host, for remote players: movement allowance refills at (a bit over) full speed; moving farther than
        // the allowance means a cheat or a broken client, so the owner is put back at its last valid position.
        void CheckMovement()
        {
            Vector2 position = transform.position;
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector2 step = position - lastCheckedPosition;
            lastCheckedPosition = position;
            observedVelocity = Vector2.Lerp(observedVelocity, step / deltaTime, 1f - Mathf.Exp(-12f * deltaTime));

            float cap = MaxBudget;
            moveBudget = moveBudget > cap
                ? Mathf.MoveTowards(moveBudget, cap, cap * deltaTime)
                : Mathf.Min(cap, moveBudget + mover.MoveSpeed * speedTolerance * deltaTime);
            if (Time.time < checkResumeTime)
            {
                lastValidPosition = position;
                return;
            }
            moveBudget -= step.magnitude;
            if (moveBudget >= 0f)
            {
                lastValidPosition = position;
                return;
            }
            // No allowance left after a correction, so repeating the trick gains nothing. The check pauses for a
            // round trip while the correction arrives.
            moveBudget = 0f;
            checkResumeTime = Time.time + 0.5f;
            CorrectPositionRpc(lastValidPosition);
        }

        // Host: adds a picked-up modifier to the stack. Picking up one already held restarts its timer.
        public void ServerEquip(int weaponIndex)
        {
            MPWeapon picked = MPMatch.Instance != null ? MPMatch.Instance.WeaponAt(weaponIndex) : null;
            if (!IsServer || picked == null || weaponIndex <= 0) return;
            var modifier = new MPModifier { Index = (byte)weaponIndex, Expire = Now + picked.Duration };
            bool refreshed = false;
            for (int i = 0; i < modifiers.Count; i++)
                if (modifiers[i].Index == weaponIndex)
                {
                    modifiers[i] = modifier;
                    refreshed = true;
                }
            if (!refreshed) modifiers.Add(modifier);
            EquippedRpc(weaponIndex);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void EquippedRpc(int weaponIndex) => Equipped?.Invoke(weaponIndex);

        // Host: an enemy player's shot slows this player briefly, then it is immune for a moment.
        public bool TryStrike()
        {
            if (!IsServer || struckDuration <= 0f || Now < immuneUntil) return false;
            struckUntil.Value = Now + struckDuration;
            immuneUntil = struckUntil.Value + struckImmunity;
            StruckRpc();
            return true;
        }

        [Rpc(SendTo.ClientsAndHost)]
        void StruckRpc() => Struck?.Invoke();

        // Host: a hit by `team`. Returns false if the shot passes
        // through: own team (no friendly fire) or conversion immunity. Player shots also lovestruck.
        public bool ServerReceiveLove(int team, int converterSlot, float power, bool fromPlayer)
        {
            if (!IsServer || !MPTeams.IsTeam(team) || team == Team || IsConversionImmune) return false;
            if (fromPlayer) TryStrike();
            int previousTeam = Team;
            if (capture.ApplyHit(team, power, Slot)) ServerOnConverted(previousTeam, team, converterSlot);
            return true;
        }

        // Host: this player now fights for `team`. In free-for-all the lead is privileged: when the lead falls,
        // everyone on their team falls with them (and their NPCs), so a team never outlives its starting player.
        void ServerOnConverted(int previousTeam, int team, int converterSlot)
        {
            ServerJoinTeam(team, converterSlot);
            bool leadFell = previousTeam == Slot && MPLobby.Instance != null && MPLobby.Instance.Mode == MPLobby.TeamMode.FreeForAll;
            if (leadFell)
                foreach (MPPlayer member in all)
                {
                    if (member == this || member.Team != previousTeam) continue;
                    member.capture.SetOwner(team, member.Slot);
                    member.ServerJoinTeam(team, converterSlot);
                }
            if (MPMatch.Instance != null) MPMatch.Instance.OnPlayerConverted();
        }

        // Host: starts the conversion immunity (so later hits in the same frame, like simultaneous projectiles
        // and explosions, can't convert again) and moves everything this player personally owns to `team`.
        void ServerJoinTeam(int team, int converterSlot)
        {
            followSlot.Value = converterSlot;
            conversionImmuneUntil.Value = Now + conversionImmunity;
            foreach (MPNpc npc in MPNpc.All)
                if (npc.PersonalOwner == Slot && npc.Team != team) npc.Capture.SetOwner(team, Slot);
        }
    }
}
