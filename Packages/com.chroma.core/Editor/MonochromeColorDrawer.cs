using Chroma.Core.Rendering;
using UnityEditor;
using UnityEngine;

namespace Chroma.Core.Editor
{
    // Draws a palette color picker with its Okhsl black-to-white lightness ramp below it, or for a float lightness just the ramp as a draggable bar.
    [CustomPropertyDrawer(typeof(MonochromeColorAttribute))]
    sealed class MonochromeColorDrawer : PropertyDrawer
    {
        const float StripHeight = 12f;
        const float MarkerWidth = 2f;
        const int StripSteps = 64;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.Float) return EditorGUIUtility.singleLineHeight;
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + StripHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.Float)
            {
                DrawLightnessBar(position, label, property);
                return;
            }
            if (property.propertyType != SerializedPropertyType.Color)
            {
                EditorGUI.LabelField(position, label.text, "Use [MonochromeColor] on a Color or float field.");
                return;
            }

            var fieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var picked = EditorGUI.ColorField(fieldRect, label, property.colorValue, true, false, false);
            if (EditorGUI.EndChangeCheck()) property.colorValue = picked;
            EditorGUI.EndProperty();

            var stripRect = new Rect(
                position.x + EditorGUIUtility.labelWidth + 2f,
                fieldRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width - EditorGUIUtility.labelWidth - 2f,
                StripHeight);
            if (!property.hasMultipleDifferentValues) DrawLightnessStrip(stripRect, property.colorValue);
        }

        // Draws the label and a black-to-white bar; clicking or dragging on it sets the float to that lightness.
        static void DrawLightnessBar(Rect position, GUIContent label, SerializedProperty property)
        {
            EditorGUI.BeginProperty(position, label, property);
            var bar = new Rect(position.x + EditorGUIUtility.labelWidth + 2f, position.y + 1f, position.width - EditorGUIUtility.labelWidth - 2f, position.height - 2f);
            EditorGUI.PrefixLabel(position, label);

            int id = GUIUtility.GetControlID(FocusType.Passive, bar);
            var e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when e.button == 0 && bar.Contains(e.mousePosition):
                    GUIUtility.hotControl = id;
                    SetFromMouse(bar, e, property);
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    SetFromMouse(bar, e, property);
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;
            }

            if (!property.hasMultipleDifferentValues) DrawLightnessStrip(bar, 0f, 0f, property.floatValue);
            EditorGUI.EndProperty();
        }

        // Sets the property to the lightness under the mouse.
        static void SetFromMouse(Rect bar, Event e, SerializedProperty property)
        {
            property.floatValue = Mathf.Clamp01((e.mousePosition.x - bar.x) / bar.width);
            GUI.changed = true;
            e.Use();
        }

        // Fills the rect with the color's Okhsl hue and saturation from lightness 0 to 1, marking the picked color.
        static void DrawLightnessStrip(Rect rect, Color color)
        {
            var hsl = Okhsl.FromSrgb(color);
            DrawLightnessStrip(rect, hsl.x, hsl.y, hsl.z);
        }

        // Fills the rect with the hue and saturation from lightness 0 to 1, marking the given lightness.
        static void DrawLightnessStrip(Rect rect, float hue, float saturation, float lightness)
        {
            var hsl = new Vector3(hue, saturation, lightness);
            float step = rect.width / StripSteps;
            for (int i = 0; i < StripSteps; i++)
            {
                var segment = new Rect(rect.x + i * step, rect.y, step + 1f, rect.height);
                EditorGUI.DrawRect(segment, Okhsl.ToSrgb(hsl.x, hsl.y, (i + 0.5f) / StripSteps));
            }

            float markerX = rect.x + hsl.z * (rect.width - MarkerWidth);
            var markerColor = hsl.z > 0.5f ? Color.black : Color.white;
            EditorGUI.DrawRect(new Rect(markerX, rect.y, MarkerWidth, rect.height), markerColor);
        }
    }
}
