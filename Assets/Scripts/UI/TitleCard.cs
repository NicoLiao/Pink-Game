using UnityEngine;

namespace LoveShot
{
    // Intro card on its own overlay canvas: shows for `holdSeconds`, then fades out over `fadeSeconds`, and only the
    // first time the main menu opens in this run of the game (coming back to the menu skips it).
    [RequireComponent(typeof(CanvasGroup))]
    public class TitleCard : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField, Min(0f)] float holdSeconds = 1.5f;
        [SerializeField, Min(0.01f)] float fadeSeconds = 0.8f;

        static bool played;
        float elapsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => played = false;

        void Reset() => group = GetComponent<CanvasGroup>();

        void Awake()
        {
            if (played)
            {
                gameObject.SetActive(false);
                return;
            }
            played = true;
            group.alpha = 1f;
            group.blocksRaycasts = true;
        }

        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float fade = Mathf.Clamp01((elapsed - holdSeconds) / fadeSeconds);
            group.alpha = 1f - fade;
            if (fade < 1f) return;
            gameObject.SetActive(false);
        }
    }
}
