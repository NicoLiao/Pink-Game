using TMPro;
using UnityEngine;

namespace LoveShot
{
    // World-space text that pops, drifts up and fades ("+1", "-3"). Self-destroying.
    [RequireComponent(typeof(TextMeshPro))]
    public class FloatingText : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float lifetime = 0.9f;
        [SerializeField, Min(0f)] float rise = 1.1f;

        TextMeshPro label;
        Color color;
        Vector3 start;
        float age;

        public static void Play(FloatingText prefab, Vector2 position, string text, Color color, float scale = 1f)
        {
            if (prefab == null) return;
            FloatingText popup = Instantiate(prefab, position, Quaternion.identity);
            popup.transform.localScale *= scale;
            popup.label = popup.GetComponent<TextMeshPro>();
            popup.label.text = text;
            popup.color = color;
            popup.start = popup.transform.position;
        }

        void Update()
        {
            if (label == null) return;
            age += Time.deltaTime;
            float t = age / lifetime;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.position = start + Vector3.up * rise * (1f - (1f - t) * (1f - t));
            label.color = new Color(color.r, color.g, color.b, t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
        }
    }
}
