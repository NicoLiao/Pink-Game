using UnityEngine;

namespace LoveShot
{
    // A shockwave: a ring that grows to `radius` plus a circle of hearts flying out to it, both fading.
    // Shows the exact reach of an area effect (rocket explosions, Drama Queen bursts). Self-destroying.
    public class HeartRing : MonoBehaviour
    {
        [SerializeField] Sprite heartSprite;
        [SerializeField] Sprite ringSprite;
        [SerializeField, Min(0.05f)] float duration = 0.45f;
        [SerializeField, Min(0f)] float heartSize = 0.4f;
        [SerializeField] int sortingOrder = 19;

        SpriteRenderer ring;
        SpriteRenderer[] hearts;
        Vector2[] directions;
        Color color;
        float radius;
        float age;

        public static void Play(HeartRing prefab, Vector2 position, Color color, float radius, int count = 16)
        {
            if (prefab == null || radius <= 0f) return;
            HeartRing effect = Instantiate(prefab, position, Quaternion.identity);
            effect.Init(color, radius, Mathf.Max(1, count));
        }

        void Init(Color tint, float reach, int count)
        {
            color = tint;
            radius = reach;
            ring = new GameObject("Ring").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(transform, false);
            ring.sprite = ringSprite;
            ring.sortingOrder = sortingOrder;

            hearts = new SpriteRenderer[count];
            directions = new Vector2[count];
            float offset = Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                var heart = new GameObject("Heart").AddComponent<SpriteRenderer>();
                heart.transform.SetParent(transform, false);
                heart.transform.localScale = Vector3.one * heartSize;
                heart.sprite = heartSprite;
                heart.sortingOrder = sortingOrder + 1;
                hearts[i] = heart;
                float angle = (offset + 360f * i / count) * Mathf.Deg2Rad;
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            Update();
        }

        void Update()
        {
            if (hearts == null) return;
            age += Time.deltaTime;
            float t = age / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            float eased = 1f - (1f - t) * (1f - t);
            float alpha = 1f - t * t;
            // The ring sprite's outer edge is at half its scale.
            ring.transform.localScale = Vector3.one * (radius * 2f * eased);
            ring.color = new Color(color.r, color.g, color.b, 0.6f * alpha);
            for (int i = 0; i < hearts.Length; i++)
            {
                hearts[i].transform.localPosition = directions[i] * radius * eased;
                hearts[i].color = new Color(color.r, color.g, color.b, alpha);
            }
        }
    }
}
