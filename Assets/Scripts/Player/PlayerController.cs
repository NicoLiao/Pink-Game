using UnityEngine;
using UnityEngine.InputSystem;

namespace LoveShot
{
    // Reads keyboard/mouse and drives the shared Mover and Shooter. WASD move, mouse aim, LMB shoot,
    // and each Skill on this object by its key (Space = Dash).
    [RequireComponent(typeof(Mover), typeof(Shooter))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] Camera aimCamera;
        [Tooltip("Hold the button to keep firing at the Shooter's fire rate.")]
        [SerializeField] bool holdToFire = true;

        Mover mover;
        Shooter shooter;
        AimIndicator aimIndicator;
        Skill[] skills;
        Vector2 lastMoveDirection;

        void Awake()
        {
            mover = GetComponent<Mover>();
            skills = GetComponents<Skill>();
            shooter = GetComponent<Shooter>();
            aimIndicator = GetComponent<AimIndicator>();
            if (aimCamera == null) aimCamera = Camera.main;
            // Before the first input, "last direction" points toward the arena center.
            lastMoveDirection = transform.position.x > 0f ? Vector2.left : Vector2.right;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (!MatchManager.InProgress || keyboard == null || mouse == null || aimCamera == null)
            {
                mover.SetMoveInput(Vector2.zero);
                return;
            }

            Vector2 move = Vector2.zero;
            if (keyboard.aKey.isPressed) move.x -= 1f;
            if (keyboard.dKey.isPressed) move.x += 1f;
            if (keyboard.sKey.isPressed) move.y -= 1f;
            if (keyboard.wKey.isPressed) move.y += 1f;
            move = move.normalized;
            mover.SetMoveInput(move);
            if (move != Vector2.zero) lastMoveDirection = move;

            Vector2 mouseWorld = aimCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 aim = (mouseWorld - (Vector2)transform.position).normalized;
            if (aimIndicator != null) aimIndicator.Point(aim);

            bool fire = holdToFire ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
            if (fire) shooter.TryShoot(aim);

            // Skills (Dash) go where you're moving; if standing still, in the last direction you moved.
            foreach (Skill skill in skills)
                if (keyboard[skill.Key].wasPressedThisFrame) skill.TryActivate(lastMoveDirection);
        }
    }
}
