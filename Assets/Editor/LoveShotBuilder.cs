using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LoveShot.EditorTools
{
    // Generates the art, prefabs and scenes: the main menu here, the online lobby and arena in
    // LoveShotBuilder.Multiplayer.cs. Re-running regenerates the generated sprites/prefabs and overwrites the scenes.
    public static partial class LoveShotBuilder
    {
        const string ArtDir = "Assets/Art/Generated";
        const string PrefabDir = "Assets/Prefabs";
        const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string SceneTemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
        const string ObstacleLayerName = "Obstacles";

        const float WallThickness = 2f;
        // Every projectile flies this much faster than the original tuning; lifetimes are divided by it so reach stays the same.
        const float ProjectileSpeedScale = 1.5f;

        // Dusty plum-rose: darker and grayer than any team, so walls never read as a team.
        static readonly Color WallColor = new Color(0.6f, 0.38f, 0.47f);
        static readonly Color MenuBackgroundColor = new Color(0.96f, 0.8f, 0.83f);
        static readonly Color DeepPink = new Color(0.86f, 0.22f, 0.48f);
        static readonly Color SoftPlum = new Color(0.4f, 0.2f, 0.32f);
        static readonly Color Berry = new Color(0.68f, 0.2f, 0.47f);

        struct Sprites
        {
            public Sprite Circle, Ring, Square, Heart, RoundedRect, DashIcon, Rocket, Arrow, Crown, BounceIcon, ShotgunIcon;
            // Toon art (LoveShotBuilder.Art.cs).
            public Sprite Blob, Shine, Shadow, Glow, CandyBlock, Face, Sparkle, FloorTile, Backdrop, Star, Flower, Diamond;
            public TeamStyle TeamStyle;
            public Texture2D AimCursor;
        }

        [MenuItem("LoveShot/Rebuild Arena + Menu")]
        static void BuildFromMenu()
        {
            if (EditorUtility.DisplayDialog("LoveShot", $"Regenerate prefabs and overwrite {MenuScenePath}, {MPLobbyScenePath} and {MPArenaScenePath}?", "Rebuild", "Cancel"))
                Debug.Log(Build());
        }

        public static string Build()
        {
            if (SceneManager.GetActiveScene().isDirty)
                return "Aborted: the active scene has unsaved changes. Save it first.";

            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(PrefabDir);

            int obstacleLayer = EnsureLayer(ObstacleLayerName);
            Sprites sprites = CreateSprites();
            HeartBurst burst = CreateBurstPrefab(sprites);
            SkillSlotUI skillSlot = CreateSkillSlotPrefab(sprites);

            // Online multiplayer: prefabs, network setup, lobby and arena scenes.
            GameObject networkManager = BuildMultiplayer(sprites, obstacleLayer, burst, skillSlot);

            BuildMenuScene(sprites, networkManager);
            SetBuildScenes(MenuScenePath, MPLobbyScenePath, MPArenaScenePath);
            return $"Built {MenuScenePath}, {MPLobbyScenePath} and {MPArenaScenePath}";
        }

        // ---------- Sprites ----------

        static Sprites CreateSprites()
        {
            var sprites = new Sprites
            {
                Circle = MakeSprite("Circle", 64, (x, y) => x * x + y * y <= 1f),
                Ring = MakeSprite("Ring", 64, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return r <= 1f && r >= 0.8f; }),
                Square = MakeSprite("Square", 8, (x, y) => true),
                // Classic implicit heart curve: (x² + y² - 1)³ - x²y³ <= 0
                Heart = MakeToonSprite("Heart", 96, (x, y) =>
                {
                    float hx = x * 1.25f, hy = y * 1.25f + 0.15f;
                    float a = hx * hx + hy * hy - 1f;
                    return a * a * a - hx * hx * hy * hy * hy <= 0f;
                }, 0.14f),
                // Rounded box (corner radius 0.5 in [-1, 1] space = 16px), 9-sliced so UI can stretch it.
                // Inked like the characters, so buttons and cards share the cartoon look.
                RoundedRect = MakeToonSprite("RoundedRect", 64, (x, y) =>
                {
                    const float radius = 0.5f;
                    float qx = Mathf.Max(Mathf.Abs(x) - (1f - radius), 0f), qy = Mathf.Max(Mathf.Abs(y) - (1f - radius), 0f);
                    return qx * qx + qy * qy <= radius * radius;
                }, 0.08f, 0f, new Vector4(18f, 18f, 18f, 18f)),
                // Two right-pointing chevrons ">>" for the Dash skill icon.
                DashIcon = MakeSprite("DashIcon", 64, (x, y) =>
                {
                    float ay = Mathf.Abs(y);
                    if (ay > 0.62f) return false;
                    foreach (float cx in new[] { -0.42f, 0.18f })
                        if (Mathf.Abs(x - cx - (0.5f - ay) * 0.8f) < 0.16f) return true;
                    return false;
                }),
                // Multiplayer: projectiles point right (+x) and are rotated to their velocity.
                Rocket = MakeToonSprite("Rocket", 96, (x, y) =>
                {
                    float ay = Mathf.Abs(y);
                    if (x >= -0.55f && x <= 0.35f && ay < 0.26f) return true;
                    if (x > 0.35f && x <= 0.9f && ay < 0.26f * (0.9f - x) / 0.55f) return true;
                    return x >= -0.85f && x < -0.35f && ay < Mathf.Min(0.62f, 0.26f + 0.36f * (-0.35f - x) / 0.5f);
                }, 0.11f, 0.15f),
                Arrow = MakeToonSprite("Arrow", 96, (x, y) =>
                {
                    float ay = Mathf.Abs(y);
                    if (x >= -0.85f && x <= 0.45f && ay < 0.07f) return true;
                    if (x > 0.3f && x <= 0.95f && ay < 0.38f * (0.95f - x) / 0.65f) return true;
                    return x >= -0.98f && x < -0.55f && ay < 0.06f + 0.26f * (-0.55f - x) / 0.43f && ay > 0.04f;
                }, 0.05f, 0f),
                Crown = MakeToonSprite("Crown", 96, (x, y) =>
                {
                    if (Mathf.Abs(x) < 0.78f && y >= -0.6f && y <= -0.25f) return true;
                    foreach (float cx in new[] { -0.58f, 0f, 0.58f })
                    {
                        if (y > -0.25f && y < 0.5f && Mathf.Abs(x - cx) < 0.3f * (0.5f - y) / 0.75f) return true;
                        if ((x - cx) * (x - cx) + (y - 0.56f) * (y - 0.56f) < 0.13f * 0.13f) return true;
                    }
                    return false;
                }, 0.1f, 0.12f),
                // Weapon icons (the Default Gun uses Heart).
                BounceIcon = MakeSprite("WeaponBounceIcon", 64, (x, y) =>
                {
                    Vector2[] path = { new(-0.85f, 0.45f), new(-0.4f, -0.55f), new(0.1f, 0.45f), new(0.55f, -0.55f) };
                    for (int i = 0; i < path.Length - 1; i++)
                        if (SegmentDistance(new Vector2(x, y), path[i], path[i + 1]) < 0.12f) return true;
                    return (x - 0.72f) * (x - 0.72f) + (y + 0.12f) * (y + 0.12f) < 0.2f * 0.2f;
                }),
                ShotgunIcon = MakeSprite("WeaponShotgunIcon", 64, (x, y) =>
                {
                    if (x >= -0.95f && x <= -0.4f && Mathf.Abs(y) < 0.17f) return true;
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = Mathf.Lerp(-38f, 38f, i / 4f) * Mathf.Deg2Rad;
                        float cx = -0.55f + 1.25f * Mathf.Cos(angle), cy = 1.25f * Mathf.Sin(angle);
                        if ((x - cx) * (x - cx) + (y - cy) * (y - cy) < 0.16f * 0.16f) return true;
                    }
                    return false;
                }),
                AimCursor = MakeCursor("AimCursor", 48),
            };
            CreateArtSprites(ref sprites);
            sprites.TeamStyle = CreateTeamStyle(sprites);
            return sprites;
        }

        // Crosshair cursor for the arenas: ring, four ticks and a center dot in hot pink with a dark outline,
        // so it reads on the light floor and over pink characters. Imported as a Cursor texture.
        static Texture2D MakeCursor(string name, int size)
        {
            bool Shape(float x, float y)
            {
                float r = Mathf.Sqrt(x * x + y * y);
                if (r >= 0.5f && r <= 0.64f) return true;
                if (r <= 0.1f) return true;
                float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                return (ax < 0.07f && ay > 0.28f && ay < 0.86f) || (ay < 0.07f && ax > 0.28f && ax < 0.86f);
            }
            bool Outline(float x, float y)
            {
                const float d = 0.09f;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    if (Shape(x + Mathf.Cos(angle) * d, y + Mathf.Sin(angle) * d)) return true;
                }
                return false;
            }

            const int samples = 4;
            var fill = new Color(1f, 0.3f, 0.62f);
            var edge = new Color(0.28f, 0.12f, 0.24f);
            var pixels = new Color[size * size];
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                int inside = 0, outline = 0;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float x = (px + (sx + 0.5f) / samples) / size * 2f - 1f;
                    float y = (py + (sy + 0.5f) / samples) / size * 2f - 1f;
                    if (Shape(x, y)) inside++;
                    else if (Outline(x, y)) outline++;
                }
                float total = samples * samples;
                float alpha = (inside + outline) / total;
                Color color = alpha > 0f ? Color.Lerp(edge, fill, inside / (float)(inside + outline)) : Color.clear;
                pixels[py * size + px] = new Color(color.r, color.g, color.b, alpha);
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            string path = $"{ArtDir}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Cursor;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(point, a + ab * t);
        }

        // inside(x, y) is sampled over [-1, 1]²; 4x4 supersampling gives soft edges.
        // border is the 9-slice border in pixels (left, bottom, right, top), for UI sprites.
        static Sprite MakeSprite(string name, int size, Func<float, float, bool> inside, Vector4 border = default)
        {
            const int samples = 4;
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                int hits = 0;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float x = (px + (sx + 0.5f) / samples) / size * 2f - 1f;
                    float y = (py + (sy + 0.5f) / samples) / size * 2f - 1f;
                    if (inside(x, y)) hits++;
                }
                pixels[py * size + px] = new Color32(255, 255, 255, (byte)(255 * hits / (samples * samples)));
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            string path = $"{ArtDir}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------- Prefabs ----------

        static HeartBurst CreateBurstPrefab(Sprites sprites)
        {
            var burst = new GameObject("HeartBurst").AddComponent<HeartBurst>();
            SetField(burst, "sprite", sprites.Heart);
            return SavePrefab(burst.gameObject, "HeartBurst").GetComponent<HeartBurst>();
        }

        static void AddBody(GameObject go, float radius)
        {
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            go.AddComponent<CircleCollider2D>().radius = radius;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------- Scene ----------

        // A candy block (walls: plum-rose, obstacles: dusty rose by default) with a matching box collider.
        static Transform AddBlock(Transform parent, string name, Sprites sprites, int layer, Vector2 position, Vector2 size, bool isStatic = true,
            Color? color = null)
        {
            SpriteRenderer block = AddSizedSprite(parent, name, sprites.CandyBlock, color ?? ObstacleColor, position, size, 0, SpriteDrawMode.Tiled);
            // Tiling (not stretching) the middle keeps the paper grain even on long walls.
            block.tileMode = SpriteTileMode.Continuous;
            block.gameObject.layer = layer;
            block.gameObject.isStatic = isStatic;
            block.gameObject.AddComponent<BoxCollider2D>().size = size;
            return block.transform;
        }

        static Camera SetUpCamera(Color background, float size, float offsetY)
        {
            Camera camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (camera == null) return null;
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.transform.position = new Vector3(0f, offsetY, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            return camera;
        }
        static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        static UnityEngine.UI.Image AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TMPro.TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false);
            label.text = text;
            label.fontSize = size;
            label.fontStyle = TMPro.FontStyles.Bold;
            label.color = color;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        // Puts these scenes first, in order (the first one is what a build launches into); keeps any others that still
        // exist after them.
        static void SetBuildScenes(params string[] orderedPaths)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => Array.IndexOf(orderedPaths, s.path) >= 0 || !File.Exists(s.path));
            for (int i = 0; i < orderedPaths.Length; i++)
                scenes.Insert(i, new EditorBuildSettingsScene(orderedPaths[i], true));
            EditorBuildSettings.scenes = scenes.ToArray();
            // Write ProjectSettings/EditorBuildSettings.asset now: Multiplayer Play Mode clones and Netcode's scene
            // sync read the list from disk, and a list that only exists in memory breaks scene synchronization.
            AssetDatabase.SaveAssets();
        }

        // ---------- Main menu ----------

        static void BuildMenuScene(Sprites sprites, GameObject networkManagerPrefab)
        {
            string networkManagerPath = AssetDatabase.GetAssetPath(networkManagerPrefab);
            AssetDatabase.DeleteAsset(MenuScenePath);
            if (!AssetDatabase.CopyAsset(SceneTemplatePath, MenuScenePath))
                throw new InvalidOperationException($"Failed to copy {SceneTemplatePath}");
            Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            SetUpCamera(MenuBackgroundColor, 5f, 0f);
            CreateEventSystem();

            Transform root = CreateCanvas("MenuCanvas").transform;
            AddBackdrop(root, sprites);

            // Floating hearts behind everything.
            Transform decor = CreateRect("Decor", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var hearts = new (Vector2 position, float size, Color color)[]
            {
                (new Vector2(-760f, 330f), 140f, new Color(1f, 1f, 1f, 0.7f)),
                (new Vector2(-560f, -260f), 90f, new Color(1f, 0.45f, 0.72f, 0.45f)),
                (new Vector2(-820f, -40f), 70f, new Color(1f, 0.55f, 0.46f, 0.55f)),
                (new Vector2(700f, 360f), 100f, new Color(1f, 0.45f, 0.72f, 0.5f)),
                (new Vector2(820f, 20f), 150f, new Color(1f, 1f, 1f, 0.7f)),
                (new Vector2(600f, -300f), 80f, new Color(1f, 0.55f, 0.46f, 0.55f)),
                (new Vector2(-300f, 400f), 60f, new Color(1f, 1f, 1f, 0.6f)),
                (new Vector2(330f, -420f), 70f, new Color(1f, 1f, 1f, 0.6f)),
            };
            for (int i = 0; i < hearts.Length; i++)
            {
                RectTransform heart = CreateRect($"Heart {i + 1}", decor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    hearts[i].position, Vector2.one * hearts[i].size);
                AddImage(heart, hearts[i].color).sprite = sprites.Heart;
                AddFloat(heart, 18f, 1.1f + 0.15f * i, i * 0.9f, 8f);
            }

            // The title stays on every page; each page is a sibling panel listed in MainMenu.panels.
            CreateTitle(root, "LOVESHOT", 190f, new Vector2(0f, 300f), new Vector2(1400f, 240f));

            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            var online = menu.gameObject.AddComponent<Multiplayer.MultiplayerMenu>();
            Color plum = new Color(0.62f, 0.42f, 0.58f);

            // 0: Main page (online rooms, up to 4 players, free-for-all).
            (RectTransform mainPanel, RectTransform mainButtons) = CreateMenuPage(root, "MainPanel");
            (UnityEngine.UI.Button host, _) = CreateMenuButton(mainButtons, "HostButton", "HOST GAME", sprites.RoundedRect, DeepPink, new Vector2(520f, 120f), 56f);
            (UnityEngine.UI.Button join, _) = CreateMenuButton(mainButtons, "JoinButton", "JOIN GAME", sprites.RoundedRect, Berry, new Vector2(520f, 110f), 52f);
            (UnityEngine.UI.Button quit, _) = CreateMenuButton(mainButtons, "QuitButton", "QUIT", sprites.RoundedRect, plum, new Vector2(400f, 100f), 46f);
            TMPro.TextMeshProUGUI onlineStatus = CreateStatusText(mainPanel);

            // 1: Join with a room code.
            (RectTransform joinPanel, RectTransform joinButtons) = CreateMenuPage(root, "JoinPanel");
            TMPro.TMP_InputField codeInput = CreateCodeInput(joinButtons, sprites.RoundedRect);
            (UnityEngine.UI.Button joinGo, _) = CreateMenuButton(joinButtons, "JoinRoomButton", "JOIN", sprites.RoundedRect, DeepPink, new Vector2(420f, 110f), 56f);
            (UnityEngine.UI.Button joinBack, _) = CreateMenuButton(joinButtons, "BackButton", "BACK", sprites.RoundedRect, plum, new Vector2(360f, 90f), 42f);
            TMPro.TextMeshProUGUI joinStatus = CreateStatusText(joinPanel);

            var serialized = new SerializedObject(menu);
            SetArray(serialized.FindProperty("panels"), mainPanel.gameObject, joinPanel.gameObject);
            SetArray(serialized.FindProperty("panelFirstSelected"), host.gameObject, codeInput.gameObject);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetField(online, "mainMenu", menu);
            SetField(online, "networkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(networkManagerPath).GetComponent<Multiplayer.MPConnection>());
            SetField(online, "multiplayerStatus", onlineStatus);
            SetField(online, "joinStatus", joinStatus);
            SetField(online, "codeInput", codeInput);
            var onlineSerialized = new SerializedObject(online);
            SetArray(onlineSerialized.FindProperty("busyDisabled"), host, join, quit, joinGo, joinBack, codeInput);
            onlineSerialized.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(host.onClick, online.Host);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(join.onClick, online.OpenJoin);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(joinGo.onClick, online.Join);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(joinBack.onClick, online.BackToMultiplayer);

            EditorSceneManager.SaveScene(scene);
        }

        // A full-screen menu page with a vertical button stack; returns (page, stack).
        static (RectTransform, RectTransform) CreateMenuPage(Transform root, string name)
        {
            RectTransform panel = CreateRect(name, root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            // Buttons stack downward from here; new buttons just get added as children.
            RectTransform buttons = CreateRect("Buttons", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                new Vector2(0f, 80f), new Vector2(620f, 440f));
            var stack = buttons.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            stack.spacing = 26f;
            stack.childAlignment = TextAnchor.UpperCenter;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;
            return (panel, buttons);
        }

        // Error/progress line under a page's buttons.
        static TMPro.TextMeshProUGUI CreateStatusText(Transform panel)
        {
            TMPro.TextMeshProUGUI status = CreateText("Status", panel, "", 38f, SoftPlum);
            status.rectTransform.anchoredPosition = new Vector2(0f, -330f);
            status.rectTransform.sizeDelta = new Vector2(1500f, 110f);
            return status;
        }

        static TMPro.TMP_InputField CreateCodeInput(Transform parent, Sprite background)
        {
            GameObject go = TMPro.TMP_DefaultControls.CreateInputField(new TMPro.TMP_DefaultControls.Resources());
            go.name = "CodeInput";
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = background;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = Color.white;
            var size = go.AddComponent<UnityEngine.UI.LayoutElement>();
            size.preferredWidth = 520f;
            size.preferredHeight = 110f;

            var input = go.GetComponent<TMPro.TMP_InputField>();
            input.characterLimit = 12;
            input.characterValidation = TMPro.TMP_InputField.CharacterValidation.Alphanumeric;
            foreach (TMPro.TMP_Text text in go.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                text.fontSize = 56f;
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.fontStyle = TMPro.FontStyles.Bold | TMPro.FontStyles.UpperCase;
            }
            input.textComponent.color = DeepPink;
            var placeholder = (TMPro.TMP_Text)input.placeholder;
            placeholder.text = "ROOM CODE";
            placeholder.color = new Color(0.45f, 0.28f, 0.42f, 0.4f);
            return input;
        }

        static void SetArray(SerializedProperty array, params Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // The project uses the new Input System only, so the UI needs its input module.
        static void CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
        }

        static (UnityEngine.UI.Button, TMPro.TextMeshProUGUI) CreateMenuButton(Transform parent, string name, string label, Sprite sprite,
            Color color, Vector2 size, float fontSize)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = color;
            var shadow = rect.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0.55f, 0.12f, 0.32f, 0.6f);
            shadow.effectDistance = new Vector2(0f, -8f);

            var layoutSize = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layoutSize.preferredWidth = size.x;
            layoutSize.preferredHeight = size.y;

            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.highlightedColor = colors.selectedColor = new Color(1f, 0.88f, 0.94f);
            colors.pressedColor = new Color(0.82f, 0.72f, 0.8f);
            button.colors = colors;
            rect.gameObject.AddComponent<ButtonBounce>();

            TMPro.TextMeshProUGUI text = CreateText("Label", rect, label, fontSize, Color.white);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;
            return (button, text);
        }

        static void AddFloat(RectTransform rect, float amplitude, float speed, float phase, float swayAngle)
        {
            var bob = rect.gameObject.AddComponent<UIFloat>();
            SetField(bob, "amplitude", amplitude);
            SetField(bob, "speed", speed);
            SetField(bob, "phase", phase);
            SetField(bob, "swayAngle", swayAngle);
        }

        static Canvas CreateCanvas(string name)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return canvas;
        }

        // ---------- Helpers ----------

        static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, Color color, Vector2 localPosition, Vector2 scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 6; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }
            throw new InvalidOperationException("No free user layer for " + layerName);
        }

        // Sets a private [SerializeField] so prefabs keep the encapsulation of the runtime scripts.
        static void SetField(Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            switch (value)
            {
                case Object reference: property.objectReferenceValue = reference; break;
                case float f: property.floatValue = f; break;
                case Vector2 v: property.vector2Value = v; break;
                case Color c: property.colorValue = c; break;
                case bool b: property.boolValue = b; break;
                case string s: property.stringValue = s; break;
                case int i when property.propertyType == SerializedPropertyType.LayerMask: property.intValue = i; break;
                case int i when property.propertyType == SerializedPropertyType.Enum: property.enumValueIndex = i; break;
                case int i: property.intValue = i; break;
                default: throw new ArgumentException($"Unsupported value type for '{field}'");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
