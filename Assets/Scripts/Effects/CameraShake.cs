using UnityEngine;

namespace LoveShot
{
    // Small decaying screen shake. Call CameraShake.Shake(strength) from anywhere.
    // Other camera scripts (ArenaCamera) move the camera by setting BasePosition; the shake is added on top.
    public class CameraShake : MonoBehaviour
    {
        [SerializeField, Min(0f)] float maxStrength = 0.4f;
        [Tooltip("Shake strength lost per second.")]
        [SerializeField, Min(0.01f)] float decay = 1.5f;

        static CameraShake instance;
        float trauma;

        public Vector3 BasePosition { get; set; }

        public static void Shake(float strength)
        {
            if (instance != null) instance.trauma = Mathf.Min(instance.maxStrength, instance.trauma + strength);
        }

        void Awake()
        {
            instance = this;
            BasePosition = transform.localPosition;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void LateUpdate()
        {
            trauma = Mathf.Max(0f, trauma - decay * Time.unscaledDeltaTime);
            transform.localPosition = BasePosition + (Vector3)(Random.insideUnitCircle * trauma);
        }
    }
}
