using UnityEngine;

namespace LoveShot
{
    // A tiny self-destroying burst of hearts that fly outward and fade. No particle system needed.
    public class HeartBurst : MonoBehaviour
    {
        [SerializeField] Sprite sprite;
        [SerializeField, Min(1)] int count = 8;
        [SerializeField, Min(0f)] float speed = 3.5f;
        [SerializeField, Min(0.05f)] float lifetime = 0.45f;
        [SerializeField, Min(0f)] float heartSize = 0.28f;
        [SerializeField] int sortingOrder = 20;

        SpriteRenderer[] hearts;
        Vector2[] velocities;
        Color color;
        float age;

        public static void Play(HeartBurst prefab, Vector2 position, Color color, float scale = 1f, int count = 0)
        {
            if (prefab == null) return;
            HeartBurst burst = Instantiate(prefab, position, Quaternion.identity);
            burst.Init(color, scale, count > 0 ? count : burst.count);
        }

        void Init(Color color, float scale, int heartCount)
        {
            this.color = color;
            hearts = new SpriteRenderer[heartCount];
            velocities = new Vector2[heartCount];
            float offset = Random.value * 360f;
            for (int i = 0; i < heartCount; i++)
            {
                var heart = new GameObject("Heart").AddComponent<SpriteRenderer>();
                heart.transform.SetParent(transform, false);
                heart.transform.localScale = Vector3.one * heartSize * scale;
                heart.sprite = sprite;
                heart.color = color;
                heart.sortingOrder = sortingOrder;
                hearts[i] = heart;
                float angle = (offset + 360f * i / heartCount) * Mathf.Deg2Rad;
                velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * scale * Random.Range(0.7f, 1.2f);
            }
        }

        void Update()
        {
            if (hearts == null) return;
            age += Time.deltaTime;
            float t = age / lifetime;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            for (int i = 0; i < hearts.Length; i++)
            {
                hearts[i].transform.localPosition += (Vector3)(velocities[i] * (1f - t) * Time.deltaTime);
                hearts[i].color = new Color(color.r, color.g, color.b, 1f - t);
            }
        }
    }
}
