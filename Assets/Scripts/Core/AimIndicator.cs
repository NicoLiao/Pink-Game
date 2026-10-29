using UnityEngine;

namespace LoveShot
{
    // Places a small marker around a leader in the direction it is aiming.
    public class AimIndicator : MonoBehaviour
    {
        [SerializeField] Transform marker;
        [SerializeField, Min(0f)] float radius = 0.85f;

        public void Point(Vector2 direction)
        {
            if (marker == null || direction.sqrMagnitude < 0.0001f) return;
            direction.Normalize();
            marker.position = (Vector2)transform.position + direction * radius;
            marker.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        }
    }
}
