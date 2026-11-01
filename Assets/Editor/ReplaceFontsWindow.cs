using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
                if (GUILayout.Button("Replace all")) Replace();
            }
            if (result.Length > 0) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        void Replace()
        {
            if (!EditorUtility.DisplayDialog("Replace fonts", $"Set {target.name} on every text in all {(includeScenes ? "scenes" : "")}{(includeScenes && includePrefabs ? " and " : "")}{(includePrefabs ? "prefabs" : "")}? Commit first.", "Replace", "Cancel")) return;
            if (includeScenes && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            result = Run(apply: true);
            AssetDatabase.SaveAssets();
        }

        // Returns a summary. Without `apply` nothing is changed, only counted.
        string Run(bool apply)
        {
            int texts = 0, changed = 0, assets = 0;
            if (includePrefabs)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        int before = changed;
                        Visit(root.GetComponentsInChildren<TMP_Text>(true), root.GetComponentsInChildren<TMP_InputField>(true), apply, ref texts, ref changed);
                        if (apply && changed > before)
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            assets++;
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
            if (includeScenes)
            {
                var openScenes = new List<string>();
                for (int i = 0; i < SceneManager.sceneCount; i++) openScenes.Add(SceneManager.GetSceneAt(i).path);
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    int before = changed;
                    foreach (GameObject root in scene.GetRootGameObjects())
                        Visit(root.GetComponentsInChildren<TMP_Text>(true), root.GetComponentsInChildren<TMP_InputField>(true), apply, ref texts, ref changed);
                    if (apply && changed > before)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        assets++;
                    }
                    if (openScenes.Contains(path)) continue;
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            return apply ? $"Changed {changed} of {texts} texts in {assets} assets." : $"{texts} texts found, {changed} would change.";
        }

        void Visit(TMP_Text[] allTexts, TMP_InputField[] inputs, bool apply, ref int texts, ref int changed)
        {
            foreach (TMP_Text text in allTexts)
            {
                texts++;
                if (text.font == target) continue;
                changed++;
                if (!apply) continue;
                text.font = target;
                EditorUtility.SetDirty(text);
            }
            if (!apply) return;
            foreach (TMP_InputField input in inputs)
            {
                if (input.fontAsset == target) continue;
                input.fontAsset = target;
                EditorUtility.SetDirty(input);
            }
        }
    }
}
