using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoveShot.Editor
{
    // Saves the Game view exactly as shown (UI included) to Screenshots/, in or out of Play mode.
    public static class ScreenshotTaker
    {
        const string Folder = "Screenshots";
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [MenuItem("LoveShot/Screenshot/Game View _F12")]
        static void TakeGameView()
        {
            var source = GetGameViewTexture();
            if (source == null)
            {
                Debug.LogError("Screenshot: the Game view is not open or has not rendered yet.");
                return;
            }

            var copy = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previousActive = RenderTexture.active;
            try
            {
                Graphics.Blit(source, copy, new Vector2(1f, -1f), new Vector2(0f, 1f));
                RenderTexture.active = copy;
                var tex = new Texture2D(copy.width, copy.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, copy.width, copy.height), 0, 0);
                tex.Apply();

                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, $"{SceneManager.GetActiveScene().name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                Debug.Log($"Screenshot saved: {Path.GetFullPath(path)}");
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(copy);
            }
        }

        // Returns the render texture the Game view draws into, or null.
        static RenderTexture GetGameViewTexture()
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return null;

            var view = gameViewType.GetMethod("GetMainGameView", Any)?.Invoke(null, null) as EditorWindow;
            if (view == null)
            {
                var all = Resources.FindObjectsOfTypeAll(gameViewType);
                if (all.Length > 0) view = all[0] as EditorWindow;
            }
            if (view == null) return null;

            for (var type = gameViewType; type != null; type = type.BaseType)
            {
                var field = type.GetField("m_TargetTexture", Any);
                if (field != null) return field.GetValue(view) as RenderTexture;
            }
            return null;
        }
    }
}
