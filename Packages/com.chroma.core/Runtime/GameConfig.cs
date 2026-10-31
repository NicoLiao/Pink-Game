using System;
using Chroma.Core.Rendering;
using UnityEngine;

namespace Chroma.Core
{
    // Palette settings for the monochrome look (trimmed from Chroma's GameConfig: no audio or timing).
    [CreateAssetMenu(menuName = "Chroma/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Monochrome palette")]
        [SerializeField, MonochromeColor] Color primaryColor = Color.white;
        [SerializeField, MonochromeColor] Color secondaryColor = Color.white;

        // Raised when any value changes in the Inspector.
        public event Action<GameConfig> Changed;

        public Vector2 PrimaryHueSaturation => HueSaturation(primaryColor);
        public Vector2 SecondaryHueSaturation => HueSaturation(secondaryColor);

        protected virtual void OnValidate() => Changed?.Invoke(this);

        // Extracts Okhsl hue and saturation from an sRGB color, discarding its lightness.
        static Vector2 HueSaturation(Color color) => Okhsl.FromSrgb(color);
    }
}
