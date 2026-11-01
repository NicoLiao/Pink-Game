using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoveShot.Editor
{
    // Runs an edit over every TMP text and input field in all scenes and prefabs under Assets (used by the
    // Replace Fonts and Replace Font Styles windows). Each edit callback changes the component when `apply` is true
    // and returns whether it did (or would) change anything.
    public static class TmpBatch
    {
        public static string Run(bool scenes, bool prefabs, bool apply, Func<TMP_Text, bool> editText, Func<TMP_InputField, bool> editInput = null)
        {
            int texts = 0, changed = 0, assets = 0;
            if (prefabs)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        int before = changed;
                        Visit(root, apply, editText, editInput, ref texts, ref changed);
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
            if (scenes)
            {
                var openScenes = new List<string>();
                for (int i = 0; i < SceneManager.sceneCount; i++) openScenes.Add(SceneManager.GetSceneAt(i).path);
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    int before = changed;
                    foreach (GameObject root in scene.GetRootGameObjects()) Visit(root, apply, editText, editInput, ref texts, ref changed);
                    if (apply && changed > before)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        assets++;
                    }
                    if (!openScenes.Contains(path)) EditorSceneManager.CloseScene(scene, true);
                }
            }
            if (apply) AssetDatabase.SaveAssets();
            return apply ? $"Changed {changed} of {texts} texts in {assets} assets." : $"{texts} texts found, {changed} would change.";
        }

        static void Visit(GameObject root, bool apply, Func<TMP_Text, bool> editText, Func<TMP_InputField, bool> editInput, ref int texts, ref int changed)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                texts++;
                if (!editText(text)) continue;
                changed++;
                if (apply) EditorUtility.SetDirty(text);
            }
            if (editInput == null) return;
            foreach (TMP_InputField input in root.GetComponentsInChildren<TMP_InputField>(true))
                if (editInput(input) && apply) EditorUtility.SetDirty(input);
        }
    }
}
