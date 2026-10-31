using UnityEngine;

namespace LoveShot
{
    // Swaps the mouse cursor for a crosshair while this object is active (it sits in both arena scenes),
    // and restores the system cursor when the scene unloads, so menus keep the normal arrow.
    public class AimCursor : MonoBehaviour
    {
        [Tooltip("Texture imported as type Cursor. The hotspot is its center.")]
        [SerializeField] Texture2D cursorTexture;

        void OnEnable()
        {
            if (cursorTexture == null) return;
            Cursor.SetCursor(cursorTexture, new Vector2(cursorTexture.width, cursorTexture.height) * 0.5f, CursorMode.Auto);
        }

        void OnDisable() => Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
