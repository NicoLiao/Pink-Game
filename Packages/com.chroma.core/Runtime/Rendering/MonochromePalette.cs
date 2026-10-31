using UnityEngine;

namespace Chroma.Core.Rendering
{
    // Pushes the game's palette from its GameConfig to the monochrome shaders.
    [ExecuteAlways]
    public sealed class MonochromePalette : MonoBehaviour
    {
        static readonly int PrimaryId = Shader.PropertyToID("_ChromaPrimaryHS");
        static readonly int SecondaryId = Shader.PropertyToID("_ChromaSecondaryHS");
        static readonly int FadeId = Shader.PropertyToID("_ChromaFade");

        [SerializeField] GameConfig config;

        GameConfig bound;

        void OnEnable()
        {
            SetFade(0f, 0f);
            Bind(config);
        }

        void OnDisable()
        {
            SetFade(0f, 0f);
            Bind(null);
        }

        // Fades every primary-palette pixel's lightness toward the given Okhsl lightness by amount (0 = none, 1 = flat); Secondary pixels are untouched.
        public void SetFade(float amount, float lightness) => Shader.SetGlobalVector(FadeId, new Vector4(Mathf.Clamp01(amount), Mathf.Clamp01(lightness)));

        void OnValidate()
        {
            if (isActiveAndEnabled) Bind(config);
        }

        // Swaps the listened-to config and applies the new one.
        void Bind(GameConfig next)
        {
            if (bound != null) bound.Changed -= Apply;
            bound = next;
            if (bound == null) return;
            bound.Changed += Apply;
            Apply(bound);
        }

        // Writes the palette hue/saturation pairs to global shader vectors.
        static void Apply(GameConfig source)
        {
            Shader.SetGlobalVector(PrimaryId, source.PrimaryHueSaturation);
            Shader.SetGlobalVector(SecondaryId, source.SecondaryHueSaturation);
        }
    }
}
