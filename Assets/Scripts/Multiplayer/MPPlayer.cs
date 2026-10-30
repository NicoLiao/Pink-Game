using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LoveShot.Multiplayer
{
    // A networked player (leader of one FFA team). Host-authoritative: the owning client only reads its
    // keyboard/mouse and sends inputs and skill requests; the host moves the character with the shared
    // Mover, fires projectiles, enforces the shot and dash cooldowns, and applies lovestruck slows.
    // NetworkTransform replicates the position to everyone with interpolation.
    [RequireComponent(typeof(Mover))]
    public class MPPlayer : NetworkBehaviour
    {
        [Header("Shooting")]
        [SerializeField, Min(0f)] float fireCooldown = 0.25f;
        [SerializeField, Min(0f)] float projectileSpeed = 12f;
        [Tooltip("Capture power of one shot (a neutral NPC needs 100, stealing needs 200).")]
        [SerializeField, Min(0f)] float shotPower = 20f;
        [SerializeField, Min(0f)] float spawnOffset = 0.6f;
        [SerializeField] bool holdToFire = true;

        [Header("Dash")]
        [Tooltip("Seconds between dashes. Enforced by the host.")]
        [SerializeField, Min(0f)] float dashCooldown = 20f;

        [Header("Lovestruck (hit by an enemy player)")]
        [SerializeField, Min(0f)] float struckDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] float struckSpeedMultiplier = 0.35f;
        [SerializeField, Min(0f)] float struckImmunity = 1f;

        [Header("Input")]
        [Tooltip("Input messages per second sent to the host (plus one immediately whenever keys change).")]
        [SerializeField, Min(1f)] float inputSendRate = 30f;
        [SerializeField] AimIndicator aimIndicator;

        static readonly Dictionary<int, MPPlayer> byTeam = new();
        static readonly List<MPPlayer> all = new();
        public static IReadOnlyList<MPPlayer> All => all;
        public static MPPlayer Local { get; private set; }
        public static event Action<MPPlayer> LocalPlayerSpawned;

        readonly NetworkVariable<int> team = new(MPTeams.Neutral);
        readonly NetworkVariable<Vector2> aim = new(Vector2.up);
        readonly NetworkVariable<double> dashReadyTime = new(0d);
        readonly NetworkVariable<double> struckUntil = new(0d);

        Mover mover;
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

        public static MPPlayer ForTeam(int team) => byTeam.TryGetValue(team, out MPPlayer player) ? player : null;

        public int Team => team.Value;
        public Vector2 Position => transform.position;
        // Meaningful on the host, which is the only machine that uses it (NPC following).
        public Vector2 Velocity => mover.Velocity;
        public float DashCooldown => dashCooldown;
        public float DashCooldownRemaining => Mathf.Max(0f, (float)(dashReadyTime.Value - Now));
        public bool IsStruck => Now < struckUntil.Value;
        public string DisplayName => MPLobby.Instance != null ? MPLobby.Instance.NameOf(team.Value) : MPTeams.NameOf(team.Value);
        double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;

        // Raised on every machine.
        public event Action<Vector2> Dashed;
        public event Action Struck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            byTeam.Clear();
            all.Clear();
            Local = null;
            LocalPlayerSpawned = null;
        }

        void Awake()
        {
            mover = GetComponent<Mover>();
            skills = GetComponents<Skill>();
        }

        // Host, before spawning.
        public void SetTeam(int slot) => team.Value = slot;

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            byTeam[team.Value] = this;
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
            if (ForTeam(team.Value) == this) byTeam.Remove(team.Value);
            if (Local == this) Local = null;
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

            if (!live || !firing || struck || Time.time < nextFireTime || MPMatch.Instance == null) return;
            nextFireTime = Time.time + fireCooldown;
            Vector2 origin = (Vector2)transform.position + aimInput * spawnOffset;
            MPMatch.Instance.Fire(team.Value, origin, aimInput, projectileSpeed, shotPower, true, NetworkObjectId);
        }

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
    }
}
