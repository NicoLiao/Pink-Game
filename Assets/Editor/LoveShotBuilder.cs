using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LoveShot.EditorTools
{
    // Generates placeholder sprites, prefabs, the main menu scene, and the arena scene with its HUD.
    // Re-running regenerates the generated sprites/prefabs and overwrites both scenes.
    public static partial class LoveShotBuilder
    {
        const string ArtDir = "Assets/Art/Generated";
        const string PrefabDir = "Assets/Prefabs";
        const string ScenePath = "Assets/Scenes/LoveShotArena.unity";
        const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string SceneTemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
        const string ObstacleLayerName = "Obstacles";

        // Playable interior is (2 * ArenaHalfWidth) x (2 * ArenaHalfHeight), centered on the origin.
        // Everything that depends on the arena size (walls, camera, spawns, AI reposition area) derives from these.
        const float ArenaHalfWidth = 18f, ArenaHalfHeight = 10f, WallThickness = 2f;
        const float LeaderSpawnX = 15f;
        // Every projectile flies this much faster than the original tuning; lifetimes are divided by it so reach stays the same.
        const float ProjectileSpeedScale = 1.5f;
        // Initial camera framing for the Scene/Game view in edit mode; ArenaCamera refits it at runtime
        // for the actual screen size and HUD.
        const float CameraSize = 12.2f, CameraOffsetY = 1.2f;
        const string DifficultyDir = "Assets/Settings/Difficulty";

        static readonly Color FloorColor = new Color(1f, 0.97f, 0.98f);
        // Lavender, so walls never read as Pink team.
        static readonly Color WallColor = new Color(0.8f, 0.74f, 0.9f);
        static readonly Color MenuBackgroundColor = new Color(1f, 0.85f, 0.92f);
        static readonly Color DeepPink = new Color(0.9f, 0.25f, 0.55f);
        static readonly Color SoftPlum = new Color(0.45f, 0.28f, 0.42f);

        struct Sprites
        {
            public Sprite Circle, Ring, Square, Heart, RoundedRect, DashIcon, Rocket, Arrow, Crown, BounceIcon, ShotgunIcon;
            public Texture2D AimCursor;
        }

        [MenuItem("LoveShot/Rebuild Arena + Menu")]
        static void BuildFromMenu()
        {
            if (EditorUtility.DisplayDialog("LoveShot", $"Regenerate prefabs and overwrite {ScenePath} and {MenuScenePath}?", "Rebuild", "Cancel"))
                Debug.Log(Build());
        }

        public static string Build()
        {
            if (SceneManager.GetActiveScene().isDirty)
                return "Aborted: the active scene has unsaved changes. Save it first.";

            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(PrefabDir);

            // Start from the project's URP 2D scene template (camera + global 2D light).
            AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SceneTemplatePath, ScenePath))
                return $"Failed to copy {SceneTemplatePath}";
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int obstacleLayer = EnsureLayer(ObstacleLayerName);
            Sprites sprites = CreateSprites();
            AIDifficulty[] difficulties = EnsureDifficultyAssets();
            GameObject projectile = CreateProjectilePrefab(sprites);
            HeartBurst burst = CreateBurstPrefab(sprites);
            GameObject pinkLeader = CreateLeaderPrefab(Team.Pink, sprites, projectile, burst, obstacleLayer, difficulties[0]);
            GameObject blueLeader = CreateLeaderPrefab(Team.Blue, sprites, projectile, burst, obstacleLayer, difficulties[0]);
            var npcs = new NpcPrefabs(sprites, projectile, burst, obstacleLayer);

            SkillSlotUI skillSlot = CreateSkillSlotPrefab(sprites);

            BuildArena(sprites, obstacleLayer);
            // Clear color = wall color, so a camera-shake jolt past the walls reads as more wall, never as empty background.
            Camera camera = SetUpCamera(WallColor, CameraSize, CameraOffsetY);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(pinkLeader, scene);
            player.transform.position = new Vector3(-LeaderSpawnX, 0f, 0f);
            ((GameObject)PrefabUtility.InstantiatePrefab(blueLeader, scene)).transform.position = new Vector3(LeaderSpawnX, 0f, 0f);
            CreateSpawner((npcs.Basic, 5f), (npcs.Cupid, 1.5f), (npcs.Shy, 2f), (npcs.Bodyguard, 1.5f));
            RectTransform hudTopBar = CreateHud(new GameObject("Match Manager").AddComponent<MatchManager>());
            SkillBarUI skillBar = CreateSkillBar(GameObject.Find("HUD").transform, skillSlot);
            SetField(skillBar, "player", player);
            SetField(GameObject.Find("HUD").AddComponent<AimCursor>(), "cursorTexture", sprites.AimCursor);
            var arenaCamera = camera.gameObject.AddComponent<ArenaCamera>();
            SetField(arenaCamera, "interiorHalfExtents", new Vector2(ArenaHalfWidth, ArenaHalfHeight));
            SetField(arenaCamera, "arenaBounds", Object.FindAnyObjectByType<ArenaBounds>());
            SetField(arenaCamera, "hudTopBar", hudTopBar);
            EditorSceneManager.SaveScene(scene);

            // Online multiplayer: prefabs, network setup, lobby and arena scenes.
            GameObject networkManager = BuildMultiplayer(sprites, obstacleLayer, burst, skillSlot);

            BuildMenuScene(sprites, networkManager);
            SetBuildScenes(MenuScenePath, ScenePath, MPLobbyScenePath, MPArenaScenePath);
            return $"Built {MenuScenePath}, {ScenePath}, {MPLobbyScenePath} and {MPArenaScenePath}";
        }

        // ---------- Sprites ----------

        static Sprites CreateSprites()
        {
            return new Sprites
            {
                Circle = MakeSprite("Circle", 64, (x, y) => x * x + y * y <= 1f),
                Ring = MakeSprite("Ring", 64, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return r <= 1f && r >= 0.8f; }),
                Square = MakeSprite("Square", 8, (x, y) => true),
                // Classic implicit heart curve: (x² + y² - 1)³ - x²y³ <= 0
                Heart = MakeSprite("Heart", 64, (x, y) =>
                {
                    float hx = x * 1.25f, hy = y * 1.25f + 0.15f;
                    float a = hx * hx + hy * hy - 1f;
                    return a * a * a - hx * hx * hy * hy * hy <= 0f;
                }),
                // Rounded box (corner radius 0.5 in [-1, 1] space = 16px), 9-sliced so UI can stretch it.
                RoundedRect = MakeSprite("RoundedRect", 64, (x, y) =>
                {
                    const float radius = 0.5f;
                    float qx = Mathf.Max(Mathf.Abs(x) - (1f - radius), 0f), qy = Mathf.Max(Mathf.Abs(y) - (1f - radius), 0f);
                    return qx * qx + qy * qy <= radius * radius;
                }, new Vector4(18f, 18f, 18f, 18f)),
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
                Rocket = MakeSprite("Rocket", 64, (x, y) =>
                {
                    float ay = Mathf.Abs(y);
                    if (x >= -0.55f && x <= 0.35f && ay < 0.26f) return true;
                    if (x > 0.35f && x <= 0.9f && ay < 0.26f * (0.9f - x) / 0.55f) return true;
                    return x >= -0.85f && x < -0.35f && ay < Mathf.Min(0.62f, 0.26f + 0.36f * (-0.35f - x) / 0.5f);
                }),
                Arrow = MakeSprite("Arrow", 64, (x, y) =>
                {
                    float ay = Mathf.Abs(y);
                    if (x >= -0.85f && x <= 0.45f && ay < 0.07f) return true;
                    if (x > 0.3f && x <= 0.95f && ay < 0.38f * (0.95f - x) / 0.65f) return true;
                    return x >= -0.98f && x < -0.55f && ay < 0.06f + 0.26f * (-0.55f - x) / 0.43f && ay > 0.04f;
                }),
                Crown = MakeSprite("Crown", 64, (x, y) =>
                {
                    if (Mathf.Abs(x) < 0.78f && y >= -0.6f && y <= -0.25f) return true;
                    foreach (float cx in new[] { -0.58f, 0f, 0.58f })
                    {
                        if (y > -0.25f && y < 0.5f && Mathf.Abs(x - cx) < 0.3f * (0.5f - y) / 0.75f) return true;
                        if ((x - cx) * (x - cx) + (y - 0.56f) * (y - 0.56f) < 0.13f * 0.13f) return true;
                    }
                    return false;
                }),
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

        static GameObject CreateProjectilePrefab(Sprites sprites)
        {
            var go = new GameObject("LoveProjectile");
            go.transform.localScale = Vector3.one * 0.45f;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites.Heart;
            renderer.sortingOrder = 10;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.4f;

            SetField(go.AddComponent<Projectile>(), "lifetime", 2f / ProjectileSpeedScale);
            return SavePrefab(go, "LoveProjectile");
        }

        // Both leaders share the same body, Mover and Shooter defaults; only the controller differs.
        static HeartBurst CreateBurstPrefab(Sprites sprites)
        {
            var burst = new GameObject("HeartBurst").AddComponent<HeartBurst>();
            SetField(burst, "sprite", sprites.Heart);
            return SavePrefab(burst.gameObject, "HeartBurst").GetComponent<HeartBurst>();
        }

        static GameObject CreateLeaderPrefab(Team team, Sprites sprites, GameObject projectile, HeartBurst burst, int obstacleLayer,
            AIDifficulty defaultDifficulty)
        {
            string name = $"{team}Leader";
            Color color = TeamUtil.ColorOf(team);
            var go = new GameObject(name);
            AddBody(go, 0.5f);
            // Leaders dash fast; continuous collision guarantees they can never tunnel through a wall.
            go.GetComponent<Rigidbody2D>().collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            SpriteRenderer body = AddSprite(go.transform, "Body", sprites.Circle, color, Vector2.zero, Vector2.one, 2);
            SpriteRenderer outline = AddSprite(go.transform, "Outline", sprites.Ring, Color.white, Vector2.zero, Vector2.one * 1.12f, 3);
            AddSprite(go.transform, "LeaderMark", sprites.Heart, Color.white, Vector2.zero, Vector2.one * 0.45f, 3);
            SpriteRenderer aimMarker = AddSprite(go.transform, "AimMarker", sprites.Heart, color, new Vector2(0f, 0.85f), Vector2.one * 0.3f, 4);

            go.AddComponent<Mover>();
            var shooter = go.AddComponent<Shooter>();
            SetField(shooter, "team", (int)team);
            SetField(shooter, "projectilePrefab", projectile.GetComponent<Projectile>());
            SetField(shooter, "projectileSpeed", 12f * ProjectileSpeedScale);

            var leader = go.AddComponent<TeamLeader>();
            SetField(leader, "team", (int)team);

            var aim = go.AddComponent<AimIndicator>();
            SetField(aim, "marker", aimMarker.transform);

            go.AddComponent<Lovestruck>();
            var visuals = go.AddComponent<LeaderVisuals>();
            SetField(visuals, "body", body);
            SetField(visuals, "outline", outline);
            SetField(visuals, "burstPrefab", burst);

            var dash = go.AddComponent<DashEffects>();
            SetField(dash, "source", body);
            SetField(dash, "burstPrefab", burst);
            SetField(dash, "afterimageColor", new Color(color.r, color.g, color.b, 0.55f));
            SetField(dash, "readyColor", color);

            if (team == Team.Pink)
            {
                var dashSkill = go.AddComponent<DashSkill>();
                SetField(dashSkill, "displayName", "Dash");
                SetField(dashSkill, "icon", sprites.DashIcon);
                go.AddComponent<PlayerController>();
                SetField(visuals, "shakeOnStruck", 0.3f);
                SetField(dash, "cooldownRing", CreateCooldownRing(go.transform, sprites.Ring));
            }
            else
            {
                // The AI's dashes shouldn't shake the player's screen.
                SetField(dash, "dashShake", 0f);
                var ai = go.AddComponent<MiniBossAI>();
                SetField(ai, "team", (int)team);
                SetField(ai, "defaultDifficulty", defaultDifficulty);
                SetField(ai, "obstacleMask", 1 << obstacleLayer);
                // Reposition waypoints stay a body-width inside the walls.
                SetField(ai, "arenaHalfExtents", new Vector2(ArenaHalfWidth - 1f, ArenaHalfHeight - 1f));
            }
            return SavePrefab(go, name);
        }

        // A world-space radial ring just outside the leader's outline, filled by DashEffects while dash recharges.
        static UnityEngine.UI.Image CreateCooldownRing(Transform leader, Sprite ringSprite)
        {
            const float diameter = 1.45f, pixels = 100f;
            var canvasObject = new GameObject("DashCooldown", typeof(RectTransform));
            var rect = (RectTransform)canvasObject.transform;
            rect.SetParent(leader, false);
            rect.sizeDelta = Vector2.one * pixels;
            rect.localScale = Vector3.one * (diameter / pixels);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;

            RectTransform ringRect = CreateRect("Ring", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UnityEngine.UI.Image ring = AddImage(ringRect, Color.white);
            ring.sprite = ringSprite;
            ring.type = UnityEngine.UI.Image.Type.Filled;
            ring.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            ring.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.enabled = false;
            return ring;
        }

        // Difficulty profiles are created once and never overwritten, so balancing done in them survives rebuilds.
        // Index 0 is the default (Easy).
        static AIDifficulty[] EnsureDifficultyAssets()
        {
            Directory.CreateDirectory(DifficultyDir);
            AIDifficulty hard = LoadOrCreate($"{DifficultyDir}/Hard.asset", null);
            AIDifficulty easy = LoadOrCreate($"{DifficultyDir}/Easy.asset", asset =>
            {
                SetAuto(asset, "DisplayName", "Easy");
                SetAuto(asset, "MenuColor", new Color(0.95f, 0.45f, 0.68f));
                // Slow, sticky, purely distance-based targeting.
                SetAuto(asset, "DecisionInterval", 0.8f);
                SetAuto(asset, "SwitchMargin", 5f);
                SetAuto(asset, "TargetReactionTime", 0.4f);
                SetAuto(asset, "StealDistanceFactor", 1f);
                SetAuto(asset, "LosingStealMultiplier", 1f);
                SetAuto(asset, "ProgressBonus", 0f);
                SetAuto(asset, "DenyBonus", 0f);
                SetAuto(asset, "NoLineOfSightPenalty", 2f);
                // Sloppy movement.
                SetAuto(asset, "WanderChance", 0.08f);
                SetAuto(asset, "MoveJitter", 0.45f);
                // Inaccurate, slower shooting; rarely picks on the player.
                SetAuto(asset, "ShootRange", 8f);
                SetAuto(asset, "AimSpread", 14f);
                SetAuto(asset, "LeadTargets", false);
                SetAuto(asset, "FireCooldownMultiplier", 1.4f);
                SetAuto(asset, "HarassRange", 4f);
                SetAuto(asset, "HarassChance", 0.1f);
                // Notices shots late, dodges rarely, never dash-dodges.
                SetAuto(asset, "DodgeChance", 0.15f);
                SetAuto(asset, "ThreatHorizon", 0.45f);
                SetAuto(asset, "DashDodgeTime", 0f);
            });
            return new[] { easy, hard };
        }

        static AIDifficulty LoadOrCreate(string path, Action<AIDifficulty> configure)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AIDifficulty>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<AIDifficulty>();
            AssetDatabase.CreateAsset(asset, path);
            configure?.Invoke(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // Sets an auto-property declared with [field: SerializeField].
        static void SetAuto(Object target, string property, object value) => SetField(target, $"<{property}>k__BackingField", value);

        // The basic NPC plus the special types. Each type is the same body with a different controller,
        // a few tuned values and a small accessory so it reads at a glance.
        class NpcPrefabs
        {
            public readonly NPCController Basic, Cupid, Shy, Bodyguard;

            public NpcPrefabs(Sprites sprites, GameObject projectile, HeartBurst burst, int obstacleLayer)
            {
                var wingColor = new Color(1f, 0.88f, 0.5f);
                var shadesColor = new Color(0.15f, 0.12f, 0.18f);
                var blushColor = new Color(1f, 0.55f, 0.55f, 0.85f);

                Basic = CreateNpcPrefab<NPCController>("NPC", 0.8f, sprites, projectile, burst, obstacleLayer, null, null);

                Cupid = CreateNpcPrefab<CupidNPC>("NPC_Cupid", 0.75f, sprites, projectile, burst, obstacleLayer,
                    body =>
                    {
                        // Wings sit behind the body.
                        AddSprite(body, "WingL", sprites.Heart, wingColor, new Vector2(-0.6f, 0.1f), new Vector2(0.5f, 0.4f), 1);
                        AddSprite(body, "WingR", sprites.Heart, wingColor, new Vector2(0.6f, 0.1f), new Vector2(0.5f, 0.4f), 1);
                    },
                    go =>
                    {
                        SetField(go.GetComponent<CupidNPC>(), "wanderSpeed", 3.2f);
                        Shooter shooter = go.GetComponent<Shooter>();
                        SetField(shooter, "fireCooldown", 1.1f);
                        SetField(shooter, "affectionPerHit", 6f);
                    });

                Shy = CreateNpcPrefab<ShyNPC>("NPC_Shy", 0.65f, sprites, projectile, burst, obstacleLayer,
                    body =>
                    {
                        AddSprite(body, "BlushL", sprites.Circle, blushColor, new Vector2(-0.3f, -0.12f), new Vector2(0.2f, 0.1f), 3);
                        AddSprite(body, "BlushR", sprites.Circle, blushColor, new Vector2(0.3f, -0.12f), new Vector2(0.2f, 0.1f), 3);
                    },
                    go =>
                    {
                        ShyNPC shy = go.GetComponent<ShyNPC>();
                        SetField(shy, "followDistance", 3f);
                        SetField(shy, "attackRange", 4.5f);
                        SetField(go.GetComponent<Affection>(), "sensitivity", 0.75f);
                    });

                Bodyguard = CreateNpcPrefab<BodyguardNPC>("NPC_Bodyguard", 1.05f, sprites, projectile, burst, obstacleLayer,
                    body => AddSprite(body, "Shades", sprites.Square, shadesColor, new Vector2(0f, 0.1f), new Vector2(0.62f, 0.15f), 4),
                    go =>
                    {
                        BodyguardNPC guard = go.GetComponent<BodyguardNPC>();
                        SetField(guard, "wanderSpeed", 1.6f);
                        SetField(guard, "followSpeed", 6.5f);
                        SetField(guard, "bodyRadius", 0.5f);
                        SetField(go.GetComponent<Affection>(), "sensitivity", 0.5f);
                        SetField(go.GetComponent<Shooter>(), "fireCooldown", 1.4f);
                    });
            }
        }

        static NPCController CreateNpcPrefab<T>(string name, float bodyScale, Sprites sprites, GameObject projectile, HeartBurst burst,
            int obstacleLayer, Action<Transform> decorate, Action<GameObject> configure) where T : NPCController
        {
            var go = new GameObject(name);
            AddBody(go, bodyScale / 2f);
            SpriteRenderer body = AddSprite(go.transform, "Body", sprites.Circle, TeamUtil.NeutralColor, Vector2.zero, Vector2.one * bodyScale, 2);
            Color eyeColor = new Color(0.25f, 0.18f, 0.25f);
            // Eyes and accessories are children of Body so they pop with it (positions are in body units).
            AddSprite(body.transform, "EyeL", sprites.Circle, eyeColor, new Vector2(-0.17f, 0.1f), Vector2.one * 0.15f, 3);
            AddSprite(body.transform, "EyeR", sprites.Circle, eyeColor, new Vector2(0.17f, 0.1f), Vector2.one * 0.15f, 3);
            decorate?.Invoke(body.transform);

            float barY = bodyScale / 2f + 0.22f;
            AddSprite(go.transform, "AffectionBarBack", sprites.Square, new Color(0.3f, 0.25f, 0.3f, 0.45f), new Vector2(0f, barY), new Vector2(1f, 0.14f), 5);
            SpriteRenderer fill = AddSprite(go.transform, "AffectionBarFill", sprites.Square, TeamUtil.PinkColor, new Vector2(0f, barY), new Vector2(0f, 0.1f), 6);

            var mover = go.AddComponent<Mover>();
            SetField(mover, "moveSpeed", 2f);
            SetField(mover, "acceleration", 25f);
            go.AddComponent<Affection>();

            // Team is set at runtime when the NPC is converted; allies shoot slower and weaker than leaders.
            var shooter = go.AddComponent<Shooter>();
            SetField(shooter, "team", (int)Team.Neutral);
            SetField(shooter, "projectilePrefab", projectile.GetComponent<Projectile>());
            SetField(shooter, "fireCooldown", 0.9f);
            SetField(shooter, "projectileSpeed", 10f * ProjectileSpeedScale);
            SetField(shooter, "affectionPerHit", 10f);
            SetField(shooter, "spawnOffset", bodyScale / 2f + 0.1f);

            var controller = go.AddComponent<T>();
            SetField(controller, "obstacleMask", 1 << obstacleLayer);

            var visuals = go.AddComponent<AffectionVisuals>();
            SetField(visuals, "body", body);
            SetField(visuals, "barFill", fill);
            SetField(visuals, "burstPrefab", burst);

            configure?.Invoke(go);
            return SavePrefab(go, name).GetComponent<NPCController>();
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

        static void BuildArena(Sprites sprites, int obstacleLayer)
        {
            const float halfWidth = ArenaHalfWidth, halfHeight = ArenaHalfHeight, thickness = WallThickness;
            var arena = new GameObject("Arena").transform;

            SpriteRenderer floor = AddSprite(arena, "Floor", sprites.Square, FloorColor, Vector2.zero, new Vector2(halfWidth * 2f, halfHeight * 2f), -10);

            // The floor and outer walls are resized at runtime by ArenaBounds so the field reaches the screen
            // edges; they are laid out here at the minimum size, and not marked static because they move.
            var walls = new GameObject("Walls").transform;
            walls.SetParent(arena);
            float outerW = halfWidth * 2f + thickness * 2f;
            Transform top = AddBlock(walls, "Wall Top", sprites, obstacleLayer, new Vector2(0f, halfHeight + thickness / 2f), new Vector2(outerW, thickness), false);
            Transform bottom = AddBlock(walls, "Wall Bottom", sprites, obstacleLayer, new Vector2(0f, -halfHeight - thickness / 2f), new Vector2(outerW, thickness), false);
            Transform left = AddBlock(walls, "Wall Left", sprites, obstacleLayer, new Vector2(-halfWidth - thickness / 2f, 0f), new Vector2(thickness, halfHeight * 2f), false);
            Transform right = AddBlock(walls, "Wall Right", sprites, obstacleLayer, new Vector2(halfWidth + thickness / 2f, 0f), new Vector2(thickness, halfHeight * 2f), false);

            var bounds = arena.gameObject.AddComponent<ArenaBounds>();
            SetField(bounds, "minHalfExtents", new Vector2(halfWidth, halfHeight));
            SetField(bounds, "wallThickness", thickness);
            SetField(bounds, "floor", floor.transform);
            SetField(bounds, "wallTop", top);
            SetField(bounds, "wallBottom", bottom);
            SetField(bounds, "wallLeft", left);
            SetField(bounds, "wallRight", right);

            // Point-symmetric layout (every block has a mirror at -position) so neither side has an advantage.
            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(arena);
            AddBlock(obstacles, "Center", sprites, obstacleLayer, Vector2.zero, new Vector2(2f, 2f));
            AddMirroredBlocks(obstacles, "Bar", sprites, obstacleLayer, new Vector2(-7f, 4.5f), new Vector2(4f, 0.9f));
            AddMirroredBlocks(obstacles, "Pillar", sprites, obstacleLayer, new Vector2(-7f, -5f), new Vector2(1f, 2.6f));
            AddMirroredBlocks(obstacles, "Block", sprites, obstacleLayer, new Vector2(-12.5f, 6.5f), new Vector2(1.6f, 1.6f));
            AddMirroredBlocks(obstacles, "Ledge", sprites, obstacleLayer, new Vector2(2f, 7.5f), new Vector2(3f, 0.9f));
        }

        static void AddMirroredBlocks(Transform parent, string name, Sprites sprites, int layer, Vector2 position, Vector2 size)
        {
            AddBlock(parent, $"{name} A", sprites, layer, position, size);
            AddBlock(parent, $"{name} B", sprites, layer, -position, size);
        }

        static Transform AddBlock(Transform parent, string name, Sprites sprites, int layer, Vector2 position, Vector2 size, bool isStatic = true)
        {
            SpriteRenderer block = AddSprite(parent, name, sprites.Square, WallColor, position, size, 0);
            block.gameObject.layer = layer;
            block.gameObject.isStatic = isStatic;
            block.gameObject.AddComponent<BoxCollider2D>();
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

        static void CreateSpawner(params (NPCController prefab, float weight)[] options)
        {
            Camera camera = Camera.main;
            if (camera != null && camera.GetComponent<CameraShake>() == null) camera.gameObject.AddComponent<CameraShake>();

            var spawner = new GameObject("NPC Spawner").AddComponent<NPCSpawner>();
            // Keep spawns clear of the walls and of the leaders' spawn columns.
            SetField(spawner, "areaSize", new Vector2((LeaderSpawnX - 2f) * 2f, (ArenaHalfHeight - 2f) * 2f));
            var serialized = new SerializedObject(spawner);
            SerializedProperty array = serialized.FindProperty("options");
            array.arraySize = options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("prefab").objectReferenceValue = options[i].prefab;
                element.FindPropertyRelative("weight").floatValue = options[i].weight;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Returns the top bar so the camera can keep the arena out from under it.
        static RectTransform CreateHud(MatchManager match)
        {
            GameObject canvasObject = CreateCanvas("HUD").gameObject;
            Transform root = canvasObject.transform;

            // Top-center bar: PINK n | m:ss | n BLUE
            RectTransform bar = CreateRect("TopBar", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(760f, 100f));
            var layout = bar.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(24, 24, 8, 8);
            TMPro.TextMeshProUGUI pink = CreateText("PinkCount", bar, "PINK 0", 54f, new Color(0.9f, 0.25f, 0.55f));
            TMPro.TextMeshProUGUI timer = CreateText("Timer", bar, "1:30", 64f, new Color(0.35f, 0.25f, 0.35f));
            TMPro.TextMeshProUGUI blue = CreateText("BlueCount", bar, "0 BLUE", 54f, new Color(0.2f, 0.45f, 0.9f));

            // Full-screen dim + centered banner, hidden until the match ends.
            RectTransform panel = CreateRect("ResultPanel", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddImage(panel, new Color(0.3f, 0.15f, 0.25f, 0.35f));
            RectTransform banner = CreateRect("Banner", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 380f));
            AddImage(banner, new Color(1f, 0.97f, 0.99f, 0.97f));
            TMPro.TextMeshProUGUI result = CreateText("ResultText", banner, "PINK WINS!", 110f, new Color(0.9f, 0.25f, 0.55f));
            var resultRect = result.rectTransform;
            resultRect.anchorMin = Vector2.zero;
            resultRect.anchorMax = Vector2.one;
            resultRect.offsetMin = new Vector2(30f, 30f);
            resultRect.offsetMax = new Vector2(-30f, -30f);
            panel.gameObject.SetActive(false);

            // Big 3-2-1-GO in the middle of the screen; the HUD hides it once play starts.
            TMPro.TextMeshProUGUI countdown = CreateText("Countdown", root, "3", 220f, new Color(0.9f, 0.25f, 0.55f));
            countdown.rectTransform.sizeDelta = new Vector2(800f, 300f);

            var hud = canvasObject.AddComponent<MatchHUD>();
            SetField(hud, "match", match);
            SetField(hud, "pinkCountText", pink);
            SetField(hud, "blueCountText", blue);
            SetField(hud, "timerText", timer);
            SetField(hud, "resultPanel", panel.gameObject);
            SetField(hud, "resultText", result);
            SetField(hud, "countdownText", countdown);
            return bar;
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

        // Puts these scenes first, in order (the first one is what a build launches into); keeps any others after them.
        static void SetBuildScenes(params string[] orderedPaths)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => Array.IndexOf(orderedPaths, s.path) >= 0);
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
            // Opening a scene unloads unreferenced assets, so load the difficulty profiles after it.
            AIDifficulty[] difficulties = EnsureDifficultyAssets();
            SetUpCamera(MenuBackgroundColor, 5f, 0f);
            CreateEventSystem();

            Transform root = CreateCanvas("MenuCanvas").transform;

            // Floating hearts behind everything.
            Transform decor = CreateRect("Decor", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var hearts = new (Vector2 position, float size, Color color)[]
            {
                (new Vector2(-760f, 330f), 140f, new Color(1f, 1f, 1f, 0.7f)),
                (new Vector2(-560f, -260f), 90f, new Color(1f, 0.45f, 0.72f, 0.45f)),
                (new Vector2(-820f, -40f), 70f, new Color(0.38f, 0.66f, 1f, 0.45f)),
                (new Vector2(700f, 360f), 100f, new Color(1f, 0.45f, 0.72f, 0.5f)),
                (new Vector2(820f, 20f), 150f, new Color(1f, 1f, 1f, 0.7f)),
                (new Vector2(600f, -300f), 80f, new Color(0.38f, 0.66f, 1f, 0.45f)),
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
            TMPro.TextMeshProUGUI title = CreateText("Title", root, "LOVESHOT", 190f, DeepPink);
            title.rectTransform.anchoredPosition = new Vector2(0f, 300f);
            title.rectTransform.sizeDelta = new Vector2(1400f, 240f);
            AddFloat(title.rectTransform, 10f, 1.6f, 0f, 2f);

            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            var online = menu.gameObject.AddComponent<Multiplayer.MultiplayerMenu>();
            Color plum = new Color(0.62f, 0.42f, 0.58f);
            Color violet = new Color(0.68f, 0.48f, 0.95f);

            // 0: Main page.
            (RectTransform mainPanel, RectTransform mainButtons) = CreateMenuPage(root, "MainPanel", "Win more hearts than anyone else!");
            (UnityEngine.UI.Button single, _) = CreateMenuButton(mainButtons, "SinglePlayerButton", "SINGLE PLAYER", sprites.RoundedRect, DeepPink, new Vector2(560f, 120f), 56f);
            (UnityEngine.UI.Button multi, _) = CreateMenuButton(mainButtons, "OnlineButton", "ONLINE MULTIPLAYER", sprites.RoundedRect, violet, new Vector2(560f, 110f), 46f);
            (UnityEngine.UI.Button quit, _) = CreateMenuButton(mainButtons, "QuitButton", "QUIT", sprites.RoundedRect, plum, new Vector2(460f, 100f), 46f);
            TMPro.TextMeshProUGUI hint = CreateText("ControlsHint", mainPanel, "WASD move  ·  Mouse aim  ·  Click shoot  ·  Space dash", 34f, SoftPlum);
            hint.fontStyle = TMPro.FontStyles.Normal;
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, 48f);
            hint.rectTransform.sizeDelta = new Vector2(1600f, 60f);

            // 1: Single player (Pink vs the Blue AI mini-boss).
            (RectTransform singlePanel, RectTransform singleButtons) = CreateMenuPage(root, "SinglePlayerPanel", "Win their hearts before the mini-boss does!");
            (UnityEngine.UI.Button play, _) = CreateMenuButton(singleButtons, "PlayButton", "PLAY", sprites.RoundedRect, DeepPink, new Vector2(460f, 130f), 68f);
            // Cycles Easy -> Hard -> ...; MainMenu writes the label and tints it with the difficulty's MenuColor.
            (UnityEngine.UI.Button difficulty, TMPro.TextMeshProUGUI difficultyLabel) = CreateMenuButton(singleButtons, "DifficultyButton", "DIFFICULTY: EASY",
                sprites.RoundedRect, difficulties[0].MenuColor, new Vector2(540f, 100f), 42f);
            (UnityEngine.UI.Button singleBack, _) = CreateMenuButton(singleButtons, "BackButton", "BACK", sprites.RoundedRect, plum, new Vector2(360f, 90f), 42f);

            // 2: Online multiplayer (private rooms, up to 4 players, free-for-all).
            (RectTransform onlinePanel, RectTransform onlineButtons) = CreateMenuPage(root, "OnlinePanel", "Private rooms · 2-4 players · Free-for-all");
            (UnityEngine.UI.Button host, _) = CreateMenuButton(onlineButtons, "HostButton", "HOST GAME", sprites.RoundedRect, DeepPink, new Vector2(500f, 120f), 56f);
            (UnityEngine.UI.Button join, _) = CreateMenuButton(onlineButtons, "JoinButton", "JOIN GAME", sprites.RoundedRect, violet, new Vector2(500f, 110f), 52f);
            (UnityEngine.UI.Button onlineBack, _) = CreateMenuButton(onlineButtons, "BackButton", "BACK", sprites.RoundedRect, plum, new Vector2(360f, 90f), 42f);
            TMPro.TextMeshProUGUI onlineStatus = CreateStatusText(onlinePanel);

            // 3: Join with a room code.
            (RectTransform joinPanel, RectTransform joinButtons) = CreateMenuPage(root, "JoinPanel", "Enter the room code from your host");
            TMPro.TMP_InputField codeInput = CreateCodeInput(joinButtons, sprites.RoundedRect);
            (UnityEngine.UI.Button joinGo, _) = CreateMenuButton(joinButtons, "JoinRoomButton", "JOIN", sprites.RoundedRect, DeepPink, new Vector2(420f, 110f), 56f);
            (UnityEngine.UI.Button joinBack, _) = CreateMenuButton(joinButtons, "BackButton", "BACK", sprites.RoundedRect, plum, new Vector2(360f, 90f), 42f);
            TMPro.TextMeshProUGUI joinStatus = CreateStatusText(joinPanel);

            var serialized = new SerializedObject(menu);
            SetArray(serialized.FindProperty("panels"), mainPanel.gameObject, singlePanel.gameObject, onlinePanel.gameObject, joinPanel.gameObject);
            SetArray(serialized.FindProperty("panelFirstSelected"), single.gameObject, play.gameObject, host.gameObject, codeInput.gameObject);
            serialized.FindProperty("gameSceneName").stringValue = Path.GetFileNameWithoutExtension(ScenePath);
            SetArray(serialized.FindProperty("difficulties"), difficulties);
            serialized.FindProperty("difficultyLabel").objectReferenceValue = difficultyLabel;
            serialized.FindProperty("difficultyBackground").objectReferenceValue = difficulty.targetGraphic;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetField(online, "mainMenu", menu);
            SetField(online, "networkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(networkManagerPath).GetComponent<Multiplayer.MPConnection>());
            SetField(online, "multiplayerStatus", onlineStatus);
            SetField(online, "joinStatus", joinStatus);
            SetField(online, "codeInput", codeInput);
            var onlineSerialized = new SerializedObject(online);
            SetArray(onlineSerialized.FindProperty("busyDisabled"), host, join, onlineBack, joinGo, joinBack, codeInput);
            onlineSerialized.ApplyModifiedPropertiesWithoutUndo();

            AddClick(single, menu.ShowPanel, 1);
            AddClick(multi, menu.ShowPanel, 2);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(play.onClick, menu.Play);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(difficulty.onClick, menu.CycleDifficulty);
            AddClick(singleBack, menu.ShowPanel, 0);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(host.onClick, online.Host);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(join.onClick, online.OpenJoin);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(onlineBack.onClick, online.BackToMain);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(joinGo.onClick, online.Join);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(joinBack.onClick, online.BackToMultiplayer);

            EditorSceneManager.SaveScene(scene);
        }

        // A full-screen menu page with a subtitle and a vertical button stack; returns (page, stack).
        static (RectTransform, RectTransform) CreateMenuPage(Transform root, string name, string subtitleText)
        {
            RectTransform panel = CreateRect(name, root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            TMPro.TextMeshProUGUI subtitle = CreateText("Subtitle", panel, subtitleText, 46f, SoftPlum);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, 160f);
            subtitle.rectTransform.sizeDelta = new Vector2(1500f, 80f);

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

        static void AddClick(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction<int> action, int argument) =>
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(button.onClick, action, argument);

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
