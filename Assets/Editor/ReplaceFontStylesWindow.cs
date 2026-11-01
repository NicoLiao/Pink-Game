using TMPro;
using UnityEditor;
using UnityEngine;

namespace LoveShot.Editor
{
    // LoveShot > Replace Font Styles: turns Bold and/or Italic on or off (or sets them exactly) on every TextMeshPro
    // text in all scenes and prefabs under Assets. Other styles (underline, small caps...) are left alone.
    // Commit first: this rewrites the assets and can't be undone with Ctrl+Z.
    public class ReplaceFontStylesWindow : EditorWindow
    {
        enum Mode { Set, Add, Remove }

        Mode mode = Mode.Set;
        bool bold;
        bool italic;
        bool includeScenes = true;
        bool includePrefabs = true;
        string result = "";

        [MenuItem("LoveShot/Replace Font Styles...")]
        static void Open() => GetWindow<ReplaceFontStylesWindow>("Replace Font Styles");

        void OnGUI()
        {
            mode = (Mode)EditorGUILayout.EnumPopup(new GUIContent("Mode", "Set: Bold/Italic become exactly as ticked. Add: ticked ones are turned on. Remove: ticked ones are turned off."), mode);
            bold = EditorGUILayout.Toggle("Bold", bold);
            italic = EditorGUILayout.Toggle("Italic", italic);
            includeScenes = EditorGUILayout.Toggle("Scenes", includeScenes);
            includePrefabs = EditorGUILayout.Toggle("Prefabs", includePrefabs);

            if (GUILayout.Button("Count texts")) result = Run(apply: false);
            if (GUILayout.Button("Replace all") && Confirm()) result = Run(apply: true);
            if (result.Length > 0) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        bool Confirm()
        {
            if (!EditorUtility.DisplayDialog("Replace font styles", "Change the style of every text in the chosen scenes and prefabs? Commit first.", "Replace", "Cancel")) return false;
            return !includeScenes || UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        string Run(bool apply) => TmpBatch.Run(includeScenes, includePrefabs, apply, text =>
        {
            FontStyles style = text.fontStyle;
            FontStyles next = style;
            switch (mode)
            {
                case Mode.Set:
                    next = With(With(next, FontStyles.Bold, bold), FontStyles.Italic, italic);
                    break;
                case Mode.Add:
                    next = With(With(next, FontStyles.Bold, bold || (style & FontStyles.Bold) != 0), FontStyles.Italic, italic || (style & FontStyles.Italic) != 0);
                    break;
                case Mode.Remove:
                    next = With(With(next, FontStyles.Bold, !bold && (style & FontStyles.Bold) != 0), FontStyles.Italic, !italic && (style & FontStyles.Italic) != 0);
                    break;
            }
            if (next == style) return false;
            if (apply) text.fontStyle = next;
            return true;
        });

        static FontStyles With(FontStyles styles, FontStyles flag, bool on) => on ? styles | flag : styles & ~flag;
    }
}
