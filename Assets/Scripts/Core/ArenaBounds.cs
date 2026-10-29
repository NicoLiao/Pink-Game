using UnityEngine;

namespace LoveShot
{
    // Owns the arena's floor and outer walls and resizes them at runtime, so the playable field always
    // reaches the edges of the camera view (ArenaCamera calls Resize). Obstacles keep their fixed layout;
    // the interior never shrinks below minHalfExtents, which is the layout they were designed for.
    public class ArenaBounds : MonoBehaviour
    {
        [Tooltip("Smallest allowed interior half-size. The arena only grows beyond this on screens with spare room.")]
        [SerializeField] Vector2 minHalfExtents = new Vector2(18f, 10f);
        [SerializeField, Min(0.1f)] float wallThickness = 2f;
        [SerializeField] Transform floor;
        [SerializeField] Transform wallTop;
        [SerializeField] Transform wallBottom;
        [SerializeField] Transform wallLeft;
        [SerializeField] Transform wallRight;
        [Tooltip("Characters left outside the interior after it shrinks (window resize) are moved this far inside.")]
        [SerializeField, Min(0f)] float clampInset = 0.6f;

        public static ArenaBounds Current { get; private set; }
        public Vector2 MinHalfExtents => minHalfExtents;
        public float WallThickness => wallThickness;
        public Vector2 HalfExtents { get; private set; }

        void Awake()
        {
            Current = this;
            // ArenaCamera may already have called Resize from its own Awake.
            if (HalfExtents == Vector2.zero) HalfExtents = minHalfExtents;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // halfExtents: interior half-size. topExtra: how far above the top wall's outer edge the view reaches
        // (the strip behind the HUD), so the top wall can be thickened to cover it.
        public void Resize(Vector2 halfExtents, float topExtra = 0f)
        {
            halfExtents = Vector2.Max(halfExtents, minHalfExtents);
            Vector2 previous = HalfExtents;
            HalfExtents = halfExtents;

            float w = halfExtents.x, h = halfExtents.y, t = wallThickness;
            float topT = t + Mathf.Max(0f, topExtra);
            Place(floor, Vector2.zero, new Vector2(w * 2f, h * 2f));
            Place(wallTop, new Vector2(0f, h + topT / 2f), new Vector2(w * 2f + t * 2f, topT));
            Place(wallBottom, new Vector2(0f, -h - t / 2f), new Vector2(w * 2f + t * 2f, t));
            Place(wallLeft, new Vector2(-w - t / 2f, 0f), new Vector2(t, h * 2f));
            Place(wallRight, new Vector2(w + t / 2f, 0f), new Vector2(t, h * 2f));

            if (halfExtents.x < previous.x - 0.001f || halfExtents.y < previous.y - 0.001f)
                PullBodiesInside();
        }

        static void Place(Transform part, Vector2 position, Vector2 size)
        {
            if (part == null) return;
            part.localPosition = position;
            part.localScale = new Vector3(size.x, size.y, 1f);
        }

        // Walls that moved inward could otherwise leave characters stranded outside the field.
        void PullBodiesInside()
        {
            Physics2D.SyncTransforms();
            Vector2 limit = HalfExtents - Vector2.one * clampInset;
            foreach (Rigidbody2D body in FindObjectsByType<Rigidbody2D>())
            {
                Vector2 p = body.position;
                if (Mathf.Abs(p.x) <= HalfExtents.x && Mathf.Abs(p.y) <= HalfExtents.y) continue;
                body.position = new Vector2(Mathf.Clamp(p.x, -limit.x, limit.x), Mathf.Clamp(p.y, -limit.y, limit.y));
            }
        }
    }
}
