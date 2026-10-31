using UnityEngine;

namespace Chroma.Core.Rendering
{
    // Marks a Color field as a palette hue/saturation, drawn with its value range below; on a float it is an Okhsl lightness, drawn as a draggable black-to-white bar.
    public sealed class MonochromeColorAttribute : PropertyAttribute { }
}
