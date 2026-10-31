using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LoveShot.Multiplayer
{
    // A networked player. Host-authoritative: the owning client only reads its keyboard/mouse and sends
    // inputs and skill requests; the host moves the character with the shared Mover, fires projectiles,
    // enforces the shot and dash cooldowns, and applies lovestruck slows and conversion.
    // NetworkTransform replicates the position to everyone with interpolation.
    //
    // Identity vs allegiance: Slot is permanent (lobby slot, name, identity color). Team is the team the
    // player currently fights for (its MPCapture owner); it starts equal to Slot and changes when another
    // team converts the player. A converted player keeps full control and takes their own NPCs along.
    [RequireComponent(typeof(Mover), typeof(MPCapture))]
    public class MPPlayer : NetworkBehaviour
    {
        [Header("Shooting (weapon stats come from the MPWeapon assets on MPMatch)")]
        [SerializeField, Min(0f)] float spawnOffset = 0.6f;
        [SerializeField] bool holdToFire = true;

        [Header("Dash")]
        [Tooltip("Seconds between dashes. Enforced by the host.")]
        [SerializeField, Min(0f)] float dashCooldown = 20f;

        [Header("Lovestruck (hit by an enemy player)")]
        [SerializeField, Min(0f)] float struckDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] float struckSpeedMultiplier = 0.35f;
        [SerializeField, Min(0f)] float struckImmunity = 1f;

        [Header("Conversion")]
        [Tooltip("Seconds a freshly converted player can't be converted again (shots pass through).")]
        [SerializeField, Min(0f)] float conversionImmunity = 3f;

        [Header("Input")]
        [Tooltip("Input messages per second sent to the host (plus one immediately whenever keys change).")]
        [SerializeField, Min(1f)] float inputSendRate = 30f;
        [SerializeField] AimIndicator aimIndicator;

        static readonly Dictionary<int, MPPlayer> bySlot = new();
        static readonly List<MPPlayer> all = new();
        public static IReadOnlyList<MPPlayer> All => all;
        public static MPPlayer Local { get; private set; }
        public static event Action<MPPlayer> LocalPlayerSpawned;

        readonly NetworkVariable<int> slot = new(MPTeams.Neutral);
        readonly NetworkVariable<Vector2> aim = new(Vector2.up);
        readonly NetworkVariable<double> dashReadyTime = new(0d);
        readonly NetworkVariable<double> struckUntil = new(0d);
        readonly NetworkVariable<double> conversionImmuneUntil = new(0d);
        // Index into MPMatch weapons (0 = Default Gun) and when a picked-up weapon runs out.
        readonly NetworkVariable<byte> weapon = new(0);
        readonly NetworkVariable<double> weaponExpireTime = new(0d);

        Mover mover;
        MPCapture capture;
        Camera aimCamera;
        Skill[] skills;

        // Host-side input state, as last received from the owner.
        Vector2 moveInput;
        Vector2 aimInput = Vector2.up;
        bool firing;
        float nextFireTime;
        double immuneUntil;

        // Owner-side.
        Vector2 lastMoveDirection = Vector2.right;
        Vector2 sentMove;
        bool sentFire;
        float nextSendTime;

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
        public MPCapture Capture => capture;
        public bool IsConversionImmune => Now < conversionImmuneUntil.Value;
        public float ConversionImmunityRemaining => Mathf.Max(0f, (float)(conversionImmuneUntil.Value - Now));
        public int WeaponIndex => weapon.Value;
        public MPWeapon Weapon => MPMatch.Instance != null ? MPMatch.Instance.WeaponAt(weapon.Value) : null;
        // Seconds left on a picked-up weapon (0 for the Default Gun).
        public float WeaponRemaining => weapon.Value == 0 ? 0f : Mathf.Max(0f, (float)(weaponExpireTime.Value - Now));
        public Vector2 Position => transform.position;
        // Meaningful on the host, which is the only machine that uses it (NPC following).
        public Vector2 Velocity => mover.Velocity;
        public float DashCooldown => dashCooldown;
        public float DashCooldownRemaining => Mathf.Max(0f, (float)(dashReadyTime.Value - Now));
        public bool IsStruck => Now < struckUntil.Value;
        public string DisplayName => MPLobby.Instance != null ? MPLobby.Instance.NameOf(slot.Value) : MPTeams.NameOf(slot.Value);
        double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;

        // Raised on every machine.
        public event Action<Vector2> Dashed;
        public event Action Struck;
        // (previous team, new team), raised on every machine when this player is converted.
        public event Action<int, int> Converted;
        // Weapon index, raised on every machine when this player picks up a weapon.
        public event Action<int> Equipped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            bySlot.Clear();
            all.Clear();
            Local = null;
            LocalPlayerSpawned = null;
        }

        void Awake()
        {
            mover = GetComponent<Mover>();
            capture = GetComponent<MPCapture>();
            skills = GetComponents<Skill>();
        }

        // Host, before spawning: the permanent slot, which is also the starting team.
        public void InitSlot(int playerSlot)
        {
            slot.Value = playerSlot;
            capture.InitOwner(playerSlot, playerSlot);
        }

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            bySlot[slot.Value] = this;
            capture.OwnerChanged += OnTeamChanged;
            // Only the host simulates movement; everyone else follows the NetworkTransform.
            mover.enabled = IsServer;
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
            if (IsServer) Simulate();
            if (!IsOwner && aimIndicator != null) aimIndicator.Point(aim.Value);
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
            if (live)
            {
                if (keyboard.aKey.isPressed) move.x -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.wKey.isPressed) move.y += 1f;
                move = move.normalized;
                if (move != Vector2.zero) lastMoveDirection = move;

                Vector2 mouseWorld = aimCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                Vector2 toMouse = mouseWorld - (Vector2)transform.position;
                if (toMouse.sqrMagnitude > 0.0001f) aimDirection = toMouse.normalized;
                fire = holdToFire ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;

                // Skills go where you're moving; if standing still, in the last direction you moved.
                foreach (Skill skill in skills)
                    if (keyboard[skill.Key].wasPressedThisFrame) skill.TryActivate(lastMoveDirection);
            }
            if (aimIndicator != null) aimIndicator.Point(aimDirection);

            // Steady stream (unreliable, so a lost packet is replaced by the next), plus an immediate send on changes.
            bool changed = move != sentMove || fire != sentFire;
            if (!changed && Time.unscaledTime < nextSendTime) return;
            nextSendTime = Time.unscaledTime + 1f / inputSendRate;
            sentMove = move;
            sentFire = fire;
            SubmitInputRpc(move, aimDirection, fire);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        void SubmitInputRpc(Vector2 move, Vector2 aimDirection, bool fire)
        {
            moveInput = Vector2.ClampMagnitude(move, 1f);
            if (aimDirection.sqrMagnitude > 0.0001f) aimInput = aimDirection.normalized;
            firing = fire;
            if ((aim.Value - aimInput).sqrMagnitude > 0.0004f) aim.Value = aimInput;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestDashRpc(Vector2 direction) => ServerTryDash(direction);

        // ---------- Host ----------

        void Simulate()
        {
            bool live = MPMatch.InProgress;
            bool struck = IsStruck;
            mover.SpeedMultiplier = struck ? struckSpeedMultiplier : 1f;
            mover.SetMoveInput(live ? moveInput : Vector2.zero);
            if (weapon.Value != 0 && Now >= weaponExpireTime.Value) weapon.Value = 0;

            MPWeapon current = Weapon;
            if (!live || !firing || struck || Time.time < nextFireTime || current == null) return;
            nextFireTime = Time.time + current.FireCooldown;
            Vector2 origin = (Vector2)transform.position + aimInput * spawnOffset;
            MPMatch.Instance.FireWeapon(current, Team, Slot, origin, aimInput, NetworkObjectId);
        }

        // Host: equips a picked-up weapon. Another pickup replaces it and restarts the timer.
        public void ServerEquip(int weaponIndex)
        {
            MPWeapon picked = MPMatch.Instance != null ? MPMatch.Instance.WeaponAt(weaponIndex) : null;
            if (!IsServer || picked == null) return;
            weaponExpireTime.Value = Now + picked.Duration;
            weapon.Value = (byte)weaponIndex;
            // Fire right away with the new weapon instead of waiting out the old one's cooldown.
            nextFireTime = Mathf.Min(nextFireTime, Time.time + 0.1f);
            EquippedRpc(weaponIndex);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void EquippedRpc(int weaponIndex) => Equipped?.Invoke(weaponIndex);

        // The host is the only judge of the cooldown, so a modified client can't dash more often.
        bool ServerTryDash(Vector2 direction)
        {
            if (!MPMatch.InProgress || direction.sqrMagnitude < 0.0001f || Now < dashReadyTime.Value || mover.IsDashing) return false;
            direction.Normalize();
            if (!mover.TryDash(direction)) return false;
            dashReadyTime.Value = Now + dashCooldown;
            DashedRpc(direction);
            return true;
        }

        [Rpc(SendTo.ClientsAndHost)]
        void DashedRpc(Vector2 direction) => Dashed?.Invoke(direction);

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
        public bool ServerReceiveLove(int team, float power, bool fromPlayer)
        {
            if (!IsServer || !MPTeams.IsTeam(team) || team == Team || IsConversionImmune) return false;
            if (fromPlayer) TryStrike();
            if (capture.ApplyHit(team, power, Slot)) ServerOnConverted(team);
            return true;
        }

        // Host: this player now fights for `team`. Immunity starts at once, so later hits in the same
        // frame (simultaneous projectiles, explosions) can't convert them again.
        void ServerOnConverted(int team)
        {
            conversionImmuneUntil.Value = Now + conversionImmunity;
            // Everything this player personally owns changes team with them (personal ownership stays).
            foreach (MPNpc npc in MPNpc.All)
                if (npc.PersonalOwner == Slot && npc.Team != team) npc.Capture.SetOwner(team, Slot);
            if (MPMatch.Instance != null) MPMatch.Instance.OnPlayerConverted();
        }
    }
}
