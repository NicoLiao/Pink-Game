using UnityEngine;

namespace LoveShot
{
    // Turns objects and components on or off depending on whether this is a web (WebGL) build. Runs before other
    // scripts' Awake, so an object it switches off never starts. In the Editor the web case can be simulated.
    [DefaultExecutionOrder(-1000)]
    public class PlatformSwitch : MonoBehaviour
    {
        [Tooltip("GameObjects or components (scripts) that are on only in a web build.")]
        [SerializeField] Object[] onlyOnWeb;
        [Tooltip("GameObjects or components (scripts) that are on everywhere except a web build.")]
        [SerializeField] Object[] notOnWeb;
        [Tooltip("Editor only: act as if this were a web build.")]
        [SerializeField] bool simulateWebInEditor;

        public static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;

        void Awake()
        {
            bool web = IsWeb || (Application.isEditor && simulateWebInEditor);
            Apply(onlyOnWeb, web);
            Apply(notOnWeb, !web);
        }

        static void Apply(Object[] targets, bool on)
        {
            if (targets == null) return;
            foreach (Object target in targets)
            {
                if (target is GameObject gameObject) gameObject.SetActive(on);
                else if (target is Behaviour behaviour) behaviour.enabled = on;
            }
        }
    }
}
