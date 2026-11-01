using TMPro;
using UnityEditor;
using UnityEngine;

namespace LoveShot.Editor
{
    // LoveShot > Replace Fonts: sets one TMP font asset on every TextMeshPro text (and input field) in all scenes
    // and prefabs under Assets. Commit first: this rewrites the assets and can't be undone with Ctrl+Z.
    public class ReplaceFontsWindow : EditorWindow
    {
        TMP_FontAsset target;
        bool includeScenes = true;
        bool includePrefabs = true;
        string result = "";

        [MenuItem("LoveShot/Replace Fonts...")]
        static void Open() => GetWindow<ReplaceFontsWindow>("Replace Fonts");

        void OnGUI()
        {
            target = (TMP_FontAsset)EditorGUILayout.ObjectField("New font", target, typeof(TMP_FontAsset), false);
            includeScenes = EditorGUILayout.Toggle("Scenes", includeScenes);
            includePrefabs = EditorGUILayout.Toggle("Prefabs", includePrefabs);

            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button("Count texts")) result = Run(apply: false);
                if (GUILayout.Button("Replace all") && Confirm()) result = Run(apply: true);
            }
            if (result.Length > 0) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        bool Confirm()
        {
            if (!EditorUtility.DisplayDialog("Replace fonts", $"Set {target.name} on every text in the chosen scenes and prefabs? Commit first.", "Replace", "Cancel")) return false;
            return !includeScenes || UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        string Run(bool apply) => TmpBatch.Run(includeScenes, includePrefabs, apply,
            text =>
            {
                if (text.font == target) return false;
                if (apply) text.font = target;
                return true;
            },
            input =>
            {
                if (input.fontAsset == target) return false;
                if (apply) input.fontAsset = target;
                return true;
            });
    }
}
