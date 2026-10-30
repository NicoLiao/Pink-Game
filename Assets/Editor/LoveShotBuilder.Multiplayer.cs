using System.IO;
using LoveShot.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LoveShot.EditorTools
{
    // Online multiplayer part of the builder: networked prefabs, the NetworkManager prefab and its prefab list,
    // the lobby scene and the 4-player arena scene, plus the HUD skill slot shared with single player.
    public static partial class LoveShotBuilder
    {
        const string MPPrefabDir = "Assets/Prefabs/Multiplayer";
        const string NetworkSettingsDir = "Assets/Settings/Network";
        const string MPLobbyScenePath = "Assets/Scenes/MPLobby.unity";
        const string MPArenaScenePath = "Assets/Scenes/MPArena.unity";

        // Fixed 4-player arena (every client must simulate/see the same walls, so it never resizes).
        const float MPHalfWidth = 28f, MPHalfHeight = 17f;
        const float MPSpawnX = 21f, MPSpawnY = 10f;

        // Returns the NetworkManager prefab.
        static GameObject BuildMultiplayer(Sprites sprites, int obstacleLayer, HeartBurst burst, SkillSlotUI skillSlot)
        {
            Directory.CreateDirectory(MPPrefabDir);
            Directory.CreateDirectory(NetworkSettingsDir);

            MPProjectile projectile = CreateMPProjectilePrefab(sprites);
            GameObject player = CreateMPPlayerPrefab(sprites, burst);
            GameObject npc = CreateMPNpcPrefab(sprites, burst, obstacleLayer);
            GameObject match = CreateMPMatchPrefab(player, npc, projectile, burst);
            GameObject lobby = SaveMPPrefab(new GameObject("MPLobby", typeof(NetworkObject), typeof(MPLobby)), "MPLobby");

            NetworkPrefabsList prefabList = CreateNetworkPrefabList(player, npc, match, lobby);
            GameObject networkManager = CreateNetworkManagerPrefab(prefabList, lobby);

            string slotPath = AssetDatabase.GetAssetPath(skillSlot);
            string matchPath = AssetDatabase.GetAssetPath(match);
            BuildMPLobbyScene(sprites);
            BuildMPArenaScene(sprites, obstacleLayer, matchPath, slotPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(networkManager));
        }

        // ---------- Prefabs ----------

        static GameObject SaveMPPrefab(GameObject go, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{MPPrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // Host-authoritative movement: the host simulates the Rigidbody; everyone else gets interpolated 2D positions.
        static void AddNetworkMotion(GameObject go)
        {
            go.AddComponent<NetworkObject>();
            var transform = go.AddComponent<NetworkTransform>();
            transform.SyncPositionZ = false;
            transform.SyncRotAngleX = transform.SyncRotAngleY = transform.SyncRotAngleZ = false;
            transform.SyncScaleX = transform.SyncScaleY = transform.SyncScaleZ = false;
            transform.Interpolate = true;
            go.AddComponent<NetworkRigidbody2D>();
        }

        static MPProjectile CreateMPProjectilePrefab(Sprites sprites)
        {
            var go = new GameObject("MPLoveProjectile");
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
            var projectile = go.AddComponent<MPProjectile>();
            SetField(projectile, "sprite", renderer);
            return SaveMPPrefab(go, "MPLoveProjectile").GetComponent<MPProjectile>();
        }

        static GameObject CreateMPPlayerPrefab(Sprites sprites, HeartBurst burst)
        {
            var go = new GameObject("MPPlayer");
            AddBody(go, 0.5f);
            go.GetComponent<Rigidbody2D>().collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            AddNetworkMotion(go);

            // Colors are applied at runtime from the player's slot.
            SpriteRenderer localRing = AddSprite(go.transform, "LocalRing", sprites.Ring, Color.white, Vector2.zero, Vector2.one * 1.6f, 1);
            SpriteRenderer body = AddSprite(go.transform, "Body", sprites.Circle, Color.white, Vector2.zero, Vector2.one, 2);
            AddSprite(go.transform, "Outline", sprites.Ring, Color.white, Vector2.zero, Vector2.one * 1.12f, 3);
            AddSprite(go.transform, "LeaderMark", sprites.Heart, Color.white, Vector2.zero, Vector2.one * 0.45f, 3);
            SpriteRenderer aimMarker = AddSprite(go.transform, "AimMarker", sprites.Heart, Color.white, new Vector2(0f, 0.85f), Vector2.one * 0.3f, 4);

            var tag = new GameObject("NameTag").AddComponent<TMPro.TextMeshPro>();
            tag.transform.SetParent(go.transform, false);
            tag.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            tag.rectTransform.sizeDelta = new Vector2(6f, 0.8f);
            tag.text = "Player";
            tag.fontSize = 3.8f;
            tag.fontStyle = TMPro.FontStyles.Bold;
            tag.alignment = TMPro.TextAlignmentOptions.Center;
            tag.sortingOrder = 30;

            // The host enforces the dash cooldown (MPPlayer.dashCooldown), so the Mover itself has none.
            var mover = go.AddComponent<Mover>();
            SetField(mover, "dashCooldown", 0f);
            var aim = go.AddComponent<AimIndicator>();
            SetField(aim, "marker", aimMarker.transform);
            var player = go.AddComponent<MPPlayer>();
            SetField(player, "aimIndicator", aim);
            var dash = go.AddComponent<NetDashSkill>();
            SetField(dash, "displayName", "Dash");
            SetField(dash, "icon", sprites.DashIcon);
            var visuals = go.AddComponent<MPPlayerVisuals>();
            SetField(visuals, "body", body);
            SetField(visuals, "aimMarker", aimMarker);
            SetField(visuals, "localRing", localRing);
            SetField(visuals, "nameTag", tag);
            SetField(visuals, "burstPrefab", burst);
            return SaveMPPrefab(go, "MPPlayer");
        }

        static GameObject CreateMPNpcPrefab(Sprites sprites, HeartBurst burst, int obstacleLayer)
        {
            const float bodyScale = 0.8f;
            var go = new GameObject("MPNpc");
            AddBody(go, bodyScale / 2f);
            AddNetworkMotion(go);

            SpriteRenderer body = AddSprite(go.transform, "Body", sprites.Circle, TeamUtil.NeutralColor, Vector2.zero, Vector2.one * bodyScale, 2);
            Color eyeColor = new Color(0.25f, 0.18f, 0.25f);
            AddSprite(body.transform, "EyeL", sprites.Circle, eyeColor, new Vector2(-0.17f, 0.1f), Vector2.one * 0.15f, 3);
            AddSprite(body.transform, "EyeR", sprites.Circle, eyeColor, new Vector2(0.17f, 0.1f), Vector2.one * 0.15f, 3);

            float barY = bodyScale / 2f + 0.22f;
            AddSprite(go.transform, "CaptureBarBack", sprites.Square, new Color(0.3f, 0.25f, 0.3f, 0.45f), new Vector2(0f, barY), new Vector2(1f, 0.14f), 5);
            SpriteRenderer loyalty = AddSprite(go.transform, "LoyaltyFill", sprites.Square, Color.white, new Vector2(0f, barY), new Vector2(0f, 0.1f), 6);
            SpriteRenderer capture = AddSprite(go.transform, "CaptureFill", sprites.Square, Color.white, new Vector2(0f, barY), new Vector2(0f, 0.1f), 6);

            var mover = go.AddComponent<Mover>();
            SetField(mover, "moveSpeed", 2f);
            SetField(mover, "acceleration", 25f);
            go.AddComponent<MPCapture>();
            var brain = go.AddComponent<MPNpc>();
            SetField(brain, "obstacleMask", 1 << obstacleLayer);
            SetField(brain, "spawnOffset", bodyScale / 2f + 0.1f);
            var visuals = go.AddComponent<MPNpcVisuals>();
            SetField(visuals, "body", body);
            SetField(visuals, "loyaltyFill", loyalty);
            SetField(visuals, "captureFill", capture);
            SetField(visuals, "burstPrefab", burst);
            return SaveMPPrefab(go, "MPNpc");
        }

        static GameObject CreateMPMatchPrefab(GameObject player, GameObject npc, MPProjectile projectile, HeartBurst burst)
        {
            var go = new GameObject("MPMatch", typeof(NetworkObject));
            var match = go.AddComponent<MPMatch>();
            SetField(match, "playerPrefab", player.GetComponent<NetworkObject>());
            SetField(match, "npcPrefab", npc.GetComponent<NetworkObject>());
            SetField(match, "projectilePrefab", projectile);
            SetField(match, "burstPrefab", burst);
            var spawns = new SerializedObject(match);
            SerializedProperty list = spawns.FindProperty("playerSpawns");
            Vector2[] points = { new(-MPSpawnX, MPSpawnY), new(MPSpawnX, -MPSpawnY), new(MPSpawnX, MPSpawnY), new(-MPSpawnX, -MPSpawnY) };
            list.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) list.GetArrayElementAtIndex(i).vector2Value = points[i];
            spawns.FindProperty("npcSpawnHalfExtents").vector2Value = new Vector2(MPHalfWidth - 2f, MPHalfHeight - 2f);
            spawns.ApplyModifiedPropertiesWithoutUndo();
            return SaveMPPrefab(go, "MPMatch");
        }

        // Every networked prefab must be registered with the NetworkManager, identically on host and clients.
        static NetworkPrefabsList CreateNetworkPrefabList(params GameObject[] prefabs)
        {
            string path = $"{NetworkSettingsDir}/LoveShotNetworkPrefabs.asset";
            AssetDatabase.DeleteAsset(path);
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            foreach (GameObject prefab in prefabs) list.Add(new NetworkPrefab { Prefab = prefab });
            AssetDatabase.CreateAsset(list, path);
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            return list;
        }

        static GameObject CreateNetworkManagerPrefab(NetworkPrefabsList prefabList, GameObject lobby)
        {
            var go = new GameObject("NetworkManager");
            var network = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                // Players are spawned by MPMatch at their slot's corner, not automatically on connect.
                PlayerPrefab = null,
                ConnectionApproval = true,
                EnableSceneManagement = true,
                TickRate = 30,
            };
            network.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
            var connection = go.AddComponent<MPConnection>();
            SetField(connection, "lobbyPrefab", lobby.GetComponent<NetworkObject>());
            SetField(connection, "menuSceneName", Path.GetFileNameWithoutExtension(MenuScenePath));
            SetField(connection, "lobbySceneName", Path.GetFileNameWithoutExtension(MPLobbyScenePath));
            SetField(connection, "arenaSceneName", Path.GetFileNameWithoutExtension(MPArenaScenePath));
            return SaveMPPrefab(go, "NetworkManager");
        }

        // ---------- Skill HUD (shared with single player) ----------

        // [icon with radial cooldown overlay + SPACE badge]  DASH  14.5s
        static SkillSlotUI CreateSkillSlotPrefab(Sprites sprites)
        {
            var root = new GameObject("SkillSlot", typeof(RectTransform)).GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(470f, 120f);
            var layout = root.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredWidth = 470f;
            layout.preferredHeight = 120f;
            UnityEngine.UI.Image frame = AddImage(root, new Color(1f, 1f, 1f, 0.85f));
            frame.sprite = sprites.RoundedRect;
            frame.type = UnityEngine.UI.Image.Type.Sliced;

            Vector2 iconCenter = new Vector2(66f, 66f);
            RectTransform iconBack = CreateRect("IconBack", root, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), iconCenter, Vector2.one * 92f);
            AddImage(iconBack, DeepPink).sprite = sprites.Circle;
            RectTransform glowRect = CreateRect("ReadyGlow", root, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), iconCenter, Vector2.one * 108f);
            UnityEngine.UI.Image glow = AddImage(glowRect, new Color(1f, 0.45f, 0.72f));
            glow.sprite = sprites.Ring;
            RectTransform iconRect = CreateRect("Icon", root, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), iconCenter, Vector2.one * 64f);
            UnityEngine.UI.Image icon = AddImage(iconRect, Color.white);
            icon.sprite = sprites.DashIcon;
            RectTransform overlayRect = CreateRect("CooldownOverlay", root, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), iconCenter, Vector2.one * 92f);
            UnityEngine.UI.Image overlay = AddImage(overlayRect, new Color(0.25f, 0.12f, 0.22f, 0.7f));
            overlay.sprite = sprites.Circle;
            overlay.type = UnityEngine.UI.Image.Type.Filled;
            overlay.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            overlay.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            overlay.fillClockwise = false;
            overlay.fillAmount = 0f;

            RectTransform badge = CreateRect("KeyBadge", root, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(66f, 16f), new Vector2(104f, 34f));
            UnityEngine.UI.Image badgeImage = AddImage(badge, SoftPlum);
            badgeImage.sprite = sprites.RoundedRect;
            badgeImage.type = UnityEngine.UI.Image.Type.Sliced;
            TMPro.TextMeshProUGUI key = CreateText("Key", badge, "SPACE", 24f, Color.white);
            Stretch(key.rectTransform);

            TMPro.TextMeshProUGUI label = CreateText("Label", root, "DASH  READY", 40f, DeepPink);
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            label.alignment = TMPro.TextAlignmentOptions.Left;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(132f, 10f);
            label.rectTransform.offsetMax = new Vector2(-16f, -10f);

            var slot = root.gameObject.AddComponent<SkillSlotUI>();
            SetField(slot, "icon", icon);
            SetField(slot, "cooldownOverlay", overlay);
            SetField(slot, "readyGlow", glow);
            SetField(slot, "keyText", key);
            SetField(slot, "labelText", label);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, $"{PrefabDir}/SkillSlot.prefab");
            Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<SkillSlotUI>();
        }

        // Bottom-left row of skill slots.
        static SkillBarUI CreateSkillBar(Transform hudRoot, SkillSlotUI slotPrefab)
        {
            RectTransform bar = CreateRect("SkillBar", hudRoot, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24f, 24f), new Vector2(900f, 120f));
            var row = bar.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.spacing = 16f;
            row.childAlignment = TextAnchor.LowerLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            // Shrink-wrap the slots, so the bar's rect is only as big as what it shows.
            var fit = bar.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var skillBar = bar.gameObject.AddComponent<SkillBarUI>();
            SetField(skillBar, "slotPrefab", slotPrefab);
            return skillBar;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // ---------- Lobby scene ----------

        static void BuildMPLobbyScene(Sprites sprites)
        {
            Scene scene = NewSceneFromTemplate(MPLobbyScenePath);
            SetUpCamera(MenuBackgroundColor, 5f, 0f);
            CreateEventSystem();
            Transform root = CreateCanvas("LobbyCanvas").transform;

            TMPro.TextMeshProUGUI title = CreateText("Title", root, "PRIVATE ROOM", 120f, DeepPink);
            title.rectTransform.anchoredPosition = new Vector2(0f, 420f);
            title.rectTransform.sizeDelta = new Vector2(1400f, 150f);
            AddFloat(title.rectTransform, 8f, 1.6f, 0f, 1.5f);

            // Room code card with Copy.
            RectTransform codeCard = CreateRect("CodeCard", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 270f), new Vector2(900f, 130f));
            UnityEngine.UI.Image cardImage = AddImage(codeCard, new Color(1f, 1f, 1f, 0.9f));
            cardImage.sprite = sprites.RoundedRect;
            cardImage.type = UnityEngine.UI.Image.Type.Sliced;
            TMPro.TextMeshProUGUI codeLabel = CreateText("CodeLabel", codeCard, "ROOM CODE", 30f, SoftPlum);
            codeLabel.rectTransform.anchoredPosition = new Vector2(-210f, 32f);
            codeLabel.rectTransform.sizeDelta = new Vector2(400f, 40f);
            TMPro.TextMeshProUGUI code = CreateText("Code", codeCard, "......", 70f, DeepPink);
            code.rectTransform.anchoredPosition = new Vector2(-150f, -18f);
            code.rectTransform.sizeDelta = new Vector2(560f, 80f);
            code.characterSpacing = 12f;
            RectTransform copyHolder = CreateRect("CopyHolder", codeCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300f, 0f), new Vector2(220f, 90f));
            (UnityEngine.UI.Button copy, TMPro.TextMeshProUGUI copyLabel) = CreateMenuButton(copyHolder, "CopyButton", "COPY", sprites.RoundedRect, new Color(0.68f, 0.48f, 0.95f), new Vector2(220f, 90f), 40f);

            TMPro.TextMeshProUGUI count = CreateText("PlayerCount", root, "PLAYERS 1/4", 44f, SoftPlum);
            count.rectTransform.anchoredPosition = new Vector2(0f, 165f);
            count.rectTransform.sizeDelta = new Vector2(900f, 60f);

            // One row per slot: color dot, name, team + HOST/YOU tags.
            var swatches = new Object[MPTeams.MaxTeams];
            var names = new Object[MPTeams.MaxTeams];
            var tags = new Object[MPTeams.MaxTeams];
            for (int slot = 0; slot < MPTeams.MaxTeams; slot++)
            {
                RectTransform row = CreateRect($"Slot {slot + 1}", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 75f - slot * 92f), new Vector2(900f, 80f));
                UnityEngine.UI.Image rowImage = AddImage(row, new Color(1f, 1f, 1f, 0.75f));
                rowImage.sprite = sprites.RoundedRect;
                rowImage.type = UnityEngine.UI.Image.Type.Sliced;
                RectTransform dot = CreateRect("Swatch", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), Vector2.one * 50f);
                UnityEngine.UI.Image swatch = AddImage(dot, MPTeams.ColorOf(slot));
                swatch.sprite = sprites.Circle;
                swatches[slot] = swatch;
                TMPro.TextMeshProUGUI name = CreateText("Name", row, "Waiting for player...", 40f, SoftPlum);
                name.alignment = TMPro.TextAlignmentOptions.Left;
                name.rectTransform.anchorMin = new Vector2(0f, 0f);
                name.rectTransform.anchorMax = new Vector2(1f, 1f);
                name.rectTransform.offsetMin = new Vector2(95f, 0f);
                name.rectTransform.offsetMax = new Vector2(-260f, 0f);
                names[slot] = name;
                TMPro.TextMeshProUGUI tag = CreateText("Tags", row, "", 30f, SoftPlum);
                tag.alignment = TMPro.TextAlignmentOptions.Right;
                tag.rectTransform.anchorMin = new Vector2(1f, 0f);
                tag.rectTransform.anchorMax = new Vector2(1f, 1f);
                tag.rectTransform.pivot = new Vector2(1f, 0.5f);
                tag.rectTransform.anchoredPosition = new Vector2(-28f, 0f);
                tag.rectTransform.sizeDelta = new Vector2(320f, 0f);
                tags[slot] = tag;
            }

            RectTransform buttons = CreateRect("Buttons", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -340f), new Vector2(1000f, 120f));
            var row2 = buttons.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row2.spacing = 40f;
            row2.childAlignment = TextAnchor.MiddleCenter;
            row2.childControlWidth = row2.childControlHeight = true;
            row2.childForceExpandWidth = row2.childForceExpandHeight = false;
            (UnityEngine.UI.Button start, TMPro.TextMeshProUGUI startLabel) = CreateMenuButton(buttons, "StartButton", "START GAME", sprites.RoundedRect, DeepPink, new Vector2(480f, 115f), 48f);
            (UnityEngine.UI.Button leave, _) = CreateMenuButton(buttons, "LeaveButton", "LEAVE", sprites.RoundedRect, new Color(0.62f, 0.42f, 0.58f), new Vector2(320f, 100f), 44f);

            TMPro.TextMeshProUGUI hint = CreateText("Hint", root, "", 36f, SoftPlum);
            hint.fontStyle = TMPro.FontStyles.Normal;
            hint.rectTransform.anchoredPosition = new Vector2(0f, -450f);
            hint.rectTransform.sizeDelta = new Vector2(1500f, 60f);

            var ui = root.gameObject.AddComponent<MPLobbyUI>();
            var serialized = new SerializedObject(ui);
            serialized.FindProperty("codeText").objectReferenceValue = code;
            serialized.FindProperty("copyLabel").objectReferenceValue = copyLabel;
            serialized.FindProperty("countText").objectReferenceValue = count;
            SetArray(serialized.FindProperty("slotSwatches"), swatches);
            SetArray(serialized.FindProperty("slotNames"), names);
            SetArray(serialized.FindProperty("slotTags"), tags);
            serialized.FindProperty("startButton").objectReferenceValue = start;
            serialized.FindProperty("startLabel").objectReferenceValue = startLabel;
            serialized.FindProperty("hintText").objectReferenceValue = hint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(copy.onClick, ui.CopyCode);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(start.onClick, ui.StartGame);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(leave.onClick, ui.Leave);

            EditorSceneManager.SaveScene(scene);
        }

        static Scene NewSceneFromTemplate(string path)
        {
            AssetDatabase.DeleteAsset(path);
            if (!AssetDatabase.CopyAsset(SceneTemplatePath, path))
                throw new System.InvalidOperationException($"Failed to copy {SceneTemplatePath}");
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        // ---------- Arena scene ----------

        static void BuildMPArenaScene(Sprites sprites, int obstacleLayer, string matchPath, string slotPath)
        {
            Scene scene = NewSceneFromTemplate(MPArenaScenePath);

            // Each client renders its own local camera that follows its own player; nothing about it is networked.
            Camera camera = SetUpCamera(WallColor, 12.5f, 0f);
            camera.gameObject.AddComponent<CameraShake>();
            var follow = camera.gameObject.AddComponent<MPFollowCamera>();
            SetField(follow, "arenaHalfExtents", new Vector2(MPHalfWidth, MPHalfHeight));

            BuildMPArena(sprites, obstacleLayer);

            var bootstrap = new GameObject("Match Bootstrap").AddComponent<MPArenaBootstrap>();
            SetField(bootstrap, "matchPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(matchPath).GetComponent<NetworkObject>());
            SetField(bootstrap, "menuSceneName", Path.GetFileNameWithoutExtension(MenuScenePath));

            CreateEventSystem();
            CreateMPHud(sprites, AssetDatabase.LoadAssetAtPath<GameObject>(slotPath).GetComponent<SkillSlotUI>());
            EditorSceneManager.SaveScene(scene);
        }

        static void BuildMPArena(Sprites sprites, int obstacleLayer)
        {
            const float w = MPHalfWidth, h = MPHalfHeight, t = WallThickness;
            var arena = new GameObject("Arena").transform;
            AddSprite(arena, "Floor", sprites.Square, FloorColor, Vector2.zero, new Vector2(w * 2f, h * 2f), -10);

            var walls = new GameObject("Walls").transform;
            walls.SetParent(arena);
            AddBlock(walls, "Wall Top", sprites, obstacleLayer, new Vector2(0f, h + t / 2f), new Vector2(w * 2f + t * 2f, t));
            AddBlock(walls, "Wall Bottom", sprites, obstacleLayer, new Vector2(0f, -h - t / 2f), new Vector2(w * 2f + t * 2f, t));
            AddBlock(walls, "Wall Left", sprites, obstacleLayer, new Vector2(-w - t / 2f, 0f), new Vector2(t, h * 2f));
            AddBlock(walls, "Wall Right", sprites, obstacleLayer, new Vector2(w + t / 2f, 0f), new Vector2(t, h * 2f));

            // Mirrored on both axes, so all four corner spawns are equivalent.
            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(arena);
            AddBlock(obstacles, "Center", sprites, obstacleLayer, Vector2.zero, new Vector2(3f, 3f));
            AddQuadBlocks(obstacles, "Bar", sprites, obstacleLayer, new Vector2(10f, 8f), new Vector2(5f, 1f));
            AddQuadBlocks(obstacles, "Pillar", sprites, obstacleLayer, new Vector2(16f, 5f), new Vector2(1f, 4f));
            AddQuadBlocks(obstacles, "Block", sprites, obstacleLayer, new Vector2(6f, 12.5f), new Vector2(1.8f, 1.8f));
            AddQuadBlocks(obstacles, "Cover", sprites, obstacleLayer, new Vector2(20f, 9f), new Vector2(1.6f, 1.6f));
            AddBlock(obstacles, "Mid Bar Top", sprites, obstacleLayer, new Vector2(0f, 10f), new Vector2(5f, 1f));
            AddBlock(obstacles, "Mid Bar Bottom", sprites, obstacleLayer, new Vector2(0f, -10f), new Vector2(5f, 1f));
            AddBlock(obstacles, "Side Wall Left", sprites, obstacleLayer, new Vector2(-22f, 0f), new Vector2(1f, 5f));
            AddBlock(obstacles, "Side Wall Right", sprites, obstacleLayer, new Vector2(22f, 0f), new Vector2(1f, 5f));
        }

        static void AddQuadBlocks(Transform parent, string name, Sprites sprites, int layer, Vector2 position, Vector2 size)
        {
            AddBlock(parent, $"{name} TR", sprites, layer, new Vector2(position.x, position.y), size);
            AddBlock(parent, $"{name} TL", sprites, layer, new Vector2(-position.x, position.y), size);
            AddBlock(parent, $"{name} BR", sprites, layer, new Vector2(position.x, -position.y), size);
            AddBlock(parent, $"{name} BL", sprites, layer, new Vector2(-position.x, -position.y), size);
        }

        static void CreateMPHud(Sprites sprites, SkillSlotUI slotPrefab)
        {
            Transform root = CreateCanvas("HUD").transform;

            // Top-center timer.
            RectTransform bar = CreateRect("TopBar", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(300f, 100f));
            UnityEngine.UI.Image barImage = AddImage(bar, new Color(1f, 1f, 1f, 0.85f));
            barImage.sprite = sprites.RoundedRect;
            barImage.type = UnityEngine.UI.Image.Type.Sliced;
            TMPro.TextMeshProUGUI timer = CreateText("Timer", bar, "3:00", 64f, new Color(0.35f, 0.25f, 0.35f));
            Stretch(timer.rectTransform);

            // Top-left live leaderboard.
            RectTransform board = CreateRect("Leaderboard", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(430f, 300f));
            UnityEngine.UI.Image boardImage = AddImage(board, new Color(1f, 1f, 1f, 0.8f));
            boardImage.sprite = sprites.RoundedRect;
            boardImage.type = UnityEngine.UI.Image.Type.Sliced;
            TMPro.TextMeshProUGUI you = CreateText("You", board, "YOU", 30f, SoftPlum);
            you.alignment = TMPro.TextAlignmentOptions.Left;
            you.rectTransform.anchorMin = you.rectTransform.anchorMax = new Vector2(0f, 1f);
            you.rectTransform.pivot = new Vector2(0f, 1f);
            you.rectTransform.anchoredPosition = new Vector2(22f, -12f);
            you.rectTransform.sizeDelta = new Vector2(390f, 44f);
            (Object[] boardRows, Object[] boardSwatches, Object[] boardNames, Object[] boardCounts) =
                CreateScoreRows(board, sprites, new Vector2(0f, -88f), 52f, 390f, 36f);

            TMPro.TextMeshProUGUI countdown = CreateText("Countdown", root, "3", 220f, DeepPink);
            countdown.rectTransform.sizeDelta = new Vector2(1400f, 300f);

            SkillBarUI skillBar = CreateSkillBar(root, slotPrefab);

            // Results: dim + banner with ranking and buttons. Hidden until the match ends.
            RectTransform panel = CreateRect("ResultPanel", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddImage(panel, new Color(0.3f, 0.15f, 0.25f, 0.4f)).raycastTarget = true;
            RectTransform banner = CreateRect("Banner", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 760f));
            UnityEngine.UI.Image bannerImage = AddImage(banner, new Color(1f, 0.97f, 0.99f, 0.97f));
            bannerImage.sprite = sprites.RoundedRect;
            bannerImage.type = UnityEngine.UI.Image.Type.Sliced;
            TMPro.TextMeshProUGUI resultTitle = CreateText("Title", banner, "PINK WINS!", 96f, DeepPink);
            resultTitle.rectTransform.anchoredPosition = new Vector2(0f, 290f);
            resultTitle.rectTransform.sizeDelta = new Vector2(940f, 130f);
            TMPro.TextMeshProUGUI subtitle = CreateText("Subtitle", banner, "FINAL TEAM SIZES", 34f, SoftPlum);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, 205f);
            subtitle.rectTransform.sizeDelta = new Vector2(900f, 50f);
            RectTransform rowsRoot = CreateRect("Ranking", banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 170f), new Vector2(800f, 340f));
            (Object[] resultRows, Object[] resultSwatches, Object[] resultNames, Object[] resultCounts) =
                CreateScoreRows(rowsRoot, sprites, Vector2.zero, 78f, 800f, 46f);

            RectTransform buttons = CreateRect("Buttons", banner, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(940f, 120f));
            var row = buttons.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.spacing = 30f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            (UnityEngine.UI.Button lobby, _) = CreateMenuButton(buttons, "LobbyButton", "RETURN TO LOBBY", sprites.RoundedRect, DeepPink, new Vector2(470f, 105f), 40f);
            TMPro.TextMeshProUGUI waiting = CreateText("WaitingForHost", buttons, "Waiting for the host...", 34f, SoftPlum);
            var waitingSize = waiting.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            waitingSize.preferredWidth = 470f;
            waitingSize.preferredHeight = 105f;
            (UnityEngine.UI.Button menuButton, _) = CreateMenuButton(buttons, "MenuButton", "MAIN MENU", sprites.RoundedRect, new Color(0.62f, 0.42f, 0.58f), new Vector2(340f, 100f), 40f);
            panel.gameObject.SetActive(false);

            var hud = root.gameObject.AddComponent<MPHUD>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("timerText").objectReferenceValue = timer;
            serialized.FindProperty("countdownText").objectReferenceValue = countdown;
            serialized.FindProperty("youText").objectReferenceValue = you;
            serialized.FindProperty("skillBar").objectReferenceValue = skillBar;
            SetArray(serialized.FindProperty("boardRows"), boardRows);
            SetArray(serialized.FindProperty("boardSwatches"), boardSwatches);
            SetArray(serialized.FindProperty("boardNames"), boardNames);
            SetArray(serialized.FindProperty("boardCounts"), boardCounts);
            serialized.FindProperty("resultPanel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("resultTitle").objectReferenceValue = resultTitle;
            SetArray(serialized.FindProperty("resultRows"), resultRows);
            SetArray(serialized.FindProperty("resultSwatches"), resultSwatches);
            SetArray(serialized.FindProperty("resultNames"), resultNames);
            SetArray(serialized.FindProperty("resultCounts"), resultCounts);
            serialized.FindProperty("lobbyButton").objectReferenceValue = lobby.gameObject;
            serialized.FindProperty("waitingForHostText").objectReferenceValue = waiting;
            serialized.FindProperty("firstSelectedOnResults").objectReferenceValue = menuButton.gameObject;
            SetArray(serialized.FindProperty("fadeWhenCovering"), board.gameObject.AddComponent<CanvasGroup>(),
                skillBar.gameObject.AddComponent<CanvasGroup>(), bar.gameObject.AddComponent<CanvasGroup>());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(lobby.onClick, hud.ReturnToLobby);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(menuButton.onClick, hud.ReturnToMainMenu);
        }

        // MPTeams.MaxTeams rows (dot, name, count) stacked downward from `start` inside `parent` (anchored top-center).
        static (Object[], Object[], Object[], Object[]) CreateScoreRows(RectTransform parent, Sprites sprites, Vector2 start, float spacing, float width, float fontSize)
        {
            var rows = new Object[MPTeams.MaxTeams];
            var swatches = new Object[MPTeams.MaxTeams];
            var names = new Object[MPTeams.MaxTeams];
            var counts = new Object[MPTeams.MaxTeams];
            for (int i = 0; i < MPTeams.MaxTeams; i++)
            {
                RectTransform row = CreateRect($"Row {i + 1}", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    start + new Vector2(0f, -i * spacing), new Vector2(width, spacing - 6f));
                RectTransform dot = CreateRect("Swatch", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(fontSize * 0.6f, 0f), Vector2.one * fontSize * 0.8f);
                UnityEngine.UI.Image swatch = AddImage(dot, MPTeams.ColorOf(i));
                swatch.sprite = sprites.Circle;
                TMPro.TextMeshProUGUI name = CreateText("Name", row, "Player", fontSize, SoftPlum);
                name.alignment = TMPro.TextAlignmentOptions.Left;
                name.enableAutoSizing = true;
                name.fontSizeMin = fontSize * 0.6f;
                name.fontSizeMax = fontSize;
                name.rectTransform.anchorMin = Vector2.zero;
                name.rectTransform.anchorMax = Vector2.one;
                name.rectTransform.offsetMin = new Vector2(fontSize * 1.3f, 0f);
                name.rectTransform.offsetMax = new Vector2(-width * 0.28f, 0f);
                TMPro.TextMeshProUGUI count = CreateText("Count", row, "0", fontSize, SoftPlum);
                count.alignment = TMPro.TextAlignmentOptions.Right;
                count.rectTransform.anchorMin = new Vector2(1f, 0f);
                count.rectTransform.anchorMax = new Vector2(1f, 1f);
                count.rectTransform.pivot = new Vector2(1f, 0.5f);
                count.rectTransform.anchoredPosition = Vector2.zero;
                count.rectTransform.sizeDelta = new Vector2(width * 0.28f, 0f);
                rows[i] = row.gameObject;
                swatches[i] = swatch;
                names[i] = name;
                counts[i] = count;
            }
            return (rows, swatches, names, counts);
        }
    }
}
