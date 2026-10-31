using System;
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
    // the lobby scene and the 4-player arena scene, plus the HUD skill slot.
    public static partial class LoveShotBuilder
    {
        const string MPPrefabDir = "Assets/Prefabs/Multiplayer";
        const string NetworkSettingsDir = "Assets/Settings/Network";
        const string WeaponSettingsDir = "Assets/Settings/Weapons";
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
            Directory.CreateDirectory(WeaponSettingsDir);

            HeartRing ring = CreateHeartRingPrefab(sprites);
            MPWeapon[] weapons = EnsureWeaponAssets(sprites);
            MPProjectile projectile = CreateMPProjectilePrefab(sprites, burst, obstacleLayer);
            GameObject player = CreateMPPlayerPrefab(sprites, burst);
            FloatingText popup = CreateFloatingTextPrefab();
            MPNpcType[] npcTypes = CreateMPNpcPrefabs(sprites, burst, ring, popup, obstacleLayer);
            GameObject pickup = CreatePickupPrefab(sprites, burst);
            GameObject match = CreateMPMatchPrefab(player, npcTypes, projectile, burst, ring, weapons, pickup);
            GameObject lobby = SaveMPPrefab(new GameObject("MPLobby", typeof(NetworkObject), typeof(MPLobby)), "MPLobby");

            var networked = new System.Collections.Generic.List<GameObject> { player, match, lobby, pickup };
            foreach (MPNpcType npcType in npcTypes) networked.Add(npcType.Prefab);
            NetworkPrefabsList prefabList = CreateNetworkPrefabList(networked.ToArray());
            GameObject networkManager = CreateNetworkManagerPrefab(prefabList, lobby);

            string slotPath = AssetDatabase.GetAssetPath(skillSlot);
            string matchPath = AssetDatabase.GetAssetPath(match);
            BuildMPLobbyScene(sprites);
            BuildMPArenaScene(sprites, obstacleLayer, matchPath, slotPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(networkManager));
        }

        // ---------- Weapons ----------

        // Created only if missing (like the difficulty assets), so tuning done in the assets survives a rebuild.
        // Icons are generated sprites, so they're re-assigned every time.
        static MPWeapon[] EnsureWeaponAssets(Sprites sprites)
        {
            MPWeapon LoadOrCreate(string name, Sprite icon, Action<MPWeapon> configure)
            {
                string path = $"{WeaponSettingsDir}/{name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<MPWeapon>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<MPWeapon>();
                    AssetDatabase.CreateAsset(asset, path);
                    configure(asset);
                }
                SetField(asset, "icon", icon);
                EditorUtility.SetDirty(asset);
                return asset;
            }

            var weapons = new[]
            {
                LoadOrCreate("DefaultGun", sprites.Heart, w =>
                {
                    SetField(w, "displayName", "Love Gun");
                    SetField(w, "fireCooldown", 0.25f);
                    SetField(w, "projectileSpeed", 12f * ProjectileSpeedScale);
                    SetField(w, "power", 20f);
                    SetField(w, "lifetime", 2f / ProjectileSpeedScale);
                }),
                LoadOrCreate("BounceGun", sprites.BounceIcon, w =>
                {
                    SetField(w, "displayName", "Bounce Gun");
                    SetField(w, "fireCooldown", 0.3f);
                    SetField(w, "projectileSpeed", 13f * ProjectileSpeedScale);
                    SetField(w, "power", 16f);
                    SetField(w, "lifetime", 2.6f / ProjectileSpeedScale);
                    SetField(w, "bounces", 3);
                }),
                LoadOrCreate("Shotgun", sprites.ShotgunIcon, w =>
                {
                    SetField(w, "displayName", "Shotgun");
                    SetField(w, "fireCooldown", 0.8f);
                    SetField(w, "pellets", 5);
                    SetField(w, "spreadAngle", 36f);
                    SetField(w, "projectileSpeed", 15f * ProjectileSpeedScale);
                    SetField(w, "power", 9f);
                    SetField(w, "lifetime", 0.36f / ProjectileSpeedScale);
                    SetField(w, "projectileScale", 0.8f);
                }),
                LoadOrCreate("RocketLauncher", sprites.Rocket, w =>
                {
                    SetField(w, "displayName", "Rocket Launcher");
                    SetField(w, "fireCooldown", 1.3f);
                    SetField(w, "style", (int)ProjectileStyle.Rocket);
                    SetField(w, "projectileSpeed", 8f * ProjectileSpeedScale);
                    SetField(w, "power", 70f);
                    SetField(w, "lifetime", 1.6f / ProjectileSpeedScale);
                    SetField(w, "projectileScale", 1.7f);
                    SetField(w, "explosionRadius", 3f);
                    SetField(w, "explosionEdgePower", 0.35f);
                }),
            };
            AssetDatabase.SaveAssets();
            return weapons;
        }

        static GameObject CreatePickupPrefab(Sprites sprites, HeartBurst burst)
        {
            var go = new GameObject("MPWeaponPickup", typeof(NetworkObject));
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.65f;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            AddSprite(go.transform, "Shadow", sprites.Shadow, ShadowColor, new Vector2(0f, -0.62f), new Vector2(1f, 0.38f), 1);
            AddSprite(visual, "Aura", sprites.Glow, new Color(1f, 0.6f, 0.72f, 0.7f), Vector2.zero, Vector2.one * 2.3f, 2);
            SpriteRenderer glow = AddSprite(visual, "Glow", sprites.Ring, new Color(0.95f, 0.45f, 0.62f, 0.9f), Vector2.zero, Vector2.one * 1.25f, 3);
            AddSprite(visual, "Badge", sprites.Blob, Color.white, Vector2.zero, Vector2.one * 1.05f, 4);
            AddSprite(visual, "Shine", sprites.Shine, new Color(1f, 1f, 1f, 0.7f), new Vector2(-0.22f, 0.26f), new Vector2(0.4f, 0.24f), 6);
            SpriteRenderer icon = AddSprite(visual, "Icon", sprites.Heart, DeepPink, Vector2.zero, Vector2.one * 0.75f, 5);
            var pickup = go.AddComponent<MPWeaponPickup>();
            SetField(pickup, "icon", icon);
            SetField(pickup, "glow", glow);
            SetField(pickup, "visualRoot", visual);
            SetField(pickup, "burstPrefab", burst);
            return SaveMPPrefab(go, "MPWeaponPickup");
        }

        static FloatingText CreateFloatingTextPrefab()
        {
            var label = new GameObject("FloatingText").AddComponent<TMPro.TextMeshPro>();
            label.rectTransform.sizeDelta = new Vector2(3f, 1f);
            label.fontSize = 5f;
            label.fontStyle = TMPro.FontStyles.Bold;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.sortingOrder = 40;
            label.gameObject.AddComponent<FloatingText>();
            return SavePrefab(label.gameObject, "FloatingText").GetComponent<FloatingText>();
        }

        static HeartRing CreateHeartRingPrefab(Sprites sprites)
        {
            var ring = new GameObject("HeartRing").AddComponent<HeartRing>();
            SetField(ring, "heartSprite", sprites.Heart);
            SetField(ring, "ringSprite", sprites.Ring);
            return SavePrefab(ring.gameObject, "HeartRing").GetComponent<HeartRing>();
        }

        // ---------- Prefabs ----------

        static GameObject SaveMPPrefab(GameObject go, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{MPPrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // Smooth, light position sync. NPCs: the host simulates and everyone interpolates. Players
        // (ownerAuthority): the owner simulates its own character for instant response (MPPlayer validates it).
        // Lerp interpolation buffers by the measured tick latency; unreliable deltas don't stall behind a lost
        // packet; half floats and a small threshold cut the traffic for slow or resting characters.
        static void AddNetworkMotion(GameObject go, bool ownerAuthority)
        {
            go.AddComponent<NetworkObject>();
            var transform = go.AddComponent<NetworkTransform>();
            transform.AuthorityMode = ownerAuthority ? NetworkTransform.AuthorityModes.Owner : NetworkTransform.AuthorityModes.Server;
            transform.SyncPositionZ = false;
            transform.SyncRotAngleX = transform.SyncRotAngleY = transform.SyncRotAngleZ = false;
            transform.SyncScaleX = transform.SyncScaleY = transform.SyncScaleZ = false;
            transform.Interpolate = true;
            transform.PositionInterpolationType = NetworkTransform.InterpolationTypes.Lerp;
            transform.UseUnreliableDeltas = true;
            transform.UseHalfFloatPrecision = true;
            transform.PositionThreshold = 0.01f;
            go.AddComponent<NetworkRigidbody2D>();
        }

        static MPProjectile CreateMPProjectilePrefab(Sprites sprites, HeartBurst burst, int obstacleLayer)
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
            AddProjectileGlow(go, sprites, renderer);
            SetField(projectile, "teamStyle", sprites.TeamStyle);
            var serialized = new SerializedObject(projectile);
            SetArray(serialized.FindProperty("styleSprites"), sprites.Heart, sprites.Rocket, sprites.Arrow);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetField(projectile, "wallMask", 1 << obstacleLayer);
            SetField(projectile, "trailPrefab", burst);
            return SaveMPPrefab(go, "MPLoveProjectile").GetComponent<MPProjectile>();
        }

        static GameObject CreateMPPlayerPrefab(Sprites sprites, HeartBurst burst)
        {
            var go = new GameObject("MPPlayer");
            AddBody(go, 0.5f);
            go.GetComponent<Rigidbody2D>().collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            AddNetworkMotion(go, ownerAuthority: true);

            // Colors are applied at runtime: body = current team, heart mark = the player's own slot.
            SpriteRenderer localRing = AddSprite(go.transform, "LocalRing", sprites.Ring, Color.white, Vector2.zero, Vector2.one * 1.6f, 1);
            SpriteRenderer body = AddCharacterBody(go, sprites, Color.white, 1f, out Transform rig);
            // White sticker border behind the inked body, so players stand out from NPCs.
            AddSprite(rig, "Outline", sprites.Circle, Color.white, Vector2.zero, Vector2.one * 1.24f, 1);
            SpriteRenderer identityBack = AddSprite(rig, "IdentityBack", sprites.Heart, Color.white, Vector2.zero, Vector2.one * 0.6f, 3);
            SpriteRenderer identity = AddSprite(rig, "IdentityMark", sprites.Heart, Color.white, Vector2.zero, Vector2.one * 0.4f, 4);
            SpriteRenderer aimMarker = AddSprite(go.transform, "AimMarker", sprites.Heart, Color.white, new Vector2(0f, 0.85f), Vector2.one * 0.3f, 4);
            SpriteRenderer shield = AddSprite(go.transform, "ImmunityShield", sprites.Ring, Color.white, Vector2.zero, Vector2.one * 1.55f, 5);
            shield.enabled = false;

            // Love meter: wider and thicker than an NPC's, so players read as the important characters.
            const float barY = 0.82f, barWidth = 1.3f;
            AddSprite(go.transform, "LoveBarBack", sprites.Square, new Color(0.3f, 0.25f, 0.3f, 0.5f), new Vector2(0f, barY), new Vector2(barWidth, 0.2f), 6);
            SpriteRenderer loyalty = AddSprite(go.transform, "LoyaltyFill", sprites.Square, Color.white, new Vector2(0f, barY), new Vector2(0f, 0.14f), 7);
            SpriteRenderer captureFill = AddSprite(go.transform, "CaptureFill", sprites.Square, Color.white, new Vector2(0f, barY), new Vector2(0f, 0.14f), 7);

            var tag = new GameObject("NameTag").AddComponent<TMPro.TextMeshPro>();
            tag.transform.SetParent(go.transform, false);
            tag.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            tag.rectTransform.sizeDelta = new Vector2(6f, 0.8f);
            tag.text = "Player";
            tag.fontSize = 3.8f;
            tag.fontStyle = TMPro.FontStyles.Bold;
            tag.alignment = TMPro.TextAlignmentOptions.Center;
            tag.sortingOrder = 30;

            // The host enforces the dash cooldown (MPPlayer.dashCooldown), so the Mover itself has none.
            var mover = go.AddComponent<Mover>();
            SetField(mover, "dashCooldown", 0f);
            // A player takes 2.5x a neutral NPC's love to convert; their own team's shots pass through.
            var capture = go.AddComponent<MPCapture>();
            SetField(capture, "captureAmount", 250f);
            SetField(capture, "stealAmount", 250f);
            SetField(capture, "ownTeamHeals", false);
            SetField(capture, "recoveryDelay", 3f);
            SetField(capture, "recoveryRate", 30f);
            var bar = go.AddComponent<MPCaptureBar>();
            SetField(bar, "loyaltyFill", loyalty);
            SetField(bar, "captureFill", captureFill);
            SetField(bar, "barWidth", barWidth);
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
            SetField(visuals, "identityMark", identity);
            SetField(visuals, "identityBack", identityBack);
            SetField(visuals, "teamStyle", sprites.TeamStyle);
            SetField(visuals, "immunityShield", shield);
            SetField(visuals, "nameTag", tag);
            SetField(visuals, "burstPrefab", burst);
            return SaveMPPrefab(go, "MPPlayer");
        }

        // One multiplayer NPC type: its prefab plus its spawn weight and per-match limits (0 max = no limit).
        struct MPNpcType
        {
            public GameObject Prefab;
            public float Weight;
            public int Min, Max;
        }

        // The basic NPC plus the special types. Same body for all; each type has its own brain, a few tuned
        // values and an accessory so it reads at a glance. Normal NPCs keep most of the spawn weight.
        static MPNpcType[] CreateMPNpcPrefabs(Sprites sprites, HeartBurst burst, HeartRing ring, FloatingText popup, int obstacleLayer)
        {
            var gold = new Color(0.98f, 0.7f, 0.58f);
            var wingColor = new Color(1f, 0.86f, 0.9f);
            var bowColor = new Color(1f, 0.35f, 0.65f);

            GameObject basic = CreateMPNpcPrefab<MPNpc>("MPNpc", MPNpc.NpcType.Basic, 0.8f, sprites, burst, ring, popup, obstacleLayer, null, null);

            GameObject cupid = CreateMPNpcPrefab<MPCupidNpc>("MPNpc_Cupid", MPNpc.NpcType.Cupid, 0.75f, sprites, burst, ring, popup, obstacleLayer,
                (body, flair) =>
                {
                    // Wings sit behind the body and flap.
                    SpriteRenderer left = AddSprite(body, "WingL", sprites.Heart, wingColor, new Vector2(-0.62f, 0.12f), new Vector2(0.55f, 0.42f), 1);
                    SpriteRenderer right = AddSprite(body, "WingR", sprites.Heart, wingColor, new Vector2(0.62f, 0.12f), new Vector2(0.55f, 0.42f), 1);
                    SetArray(flair, "wings", left.transform, right.transform);
                },
                go =>
                {
                    MPCupidNpc brain = go.GetComponent<MPCupidNpc>();
                    SetField(brain, "wanderSpeed", 2.8f);
                    SetField(brain, "followDistance", 3.2f);
                    SetField(brain, "attackRange", 8f);
                    SetField(brain, "fireCooldown", 1.5f);
                    SetField(brain, "projectileSpeed", 15f * ProjectileSpeedScale);
                    SetField(brain, "shotPower", 12f);
                });

            GameObject queen = CreateMPNpcPrefab<MPDramaQueenNpc>("MPNpc_DramaQueen", MPNpc.NpcType.DramaQueen, 0.85f, sprites, burst, ring, popup, obstacleLayer,
                (body, flair) =>
                {
                    // A big bow on top, and an aura that glows while her burst is ready.
                    SpriteRenderer aura = AddSprite(body, "Aura", sprites.Ring, Color.white, Vector2.zero, Vector2.one * 1.5f, 1);
                    Transform bowLeft = AddSprite(body, "BowL", sprites.Heart, bowColor, new Vector2(-0.2f, 0.52f), new Vector2(0.42f, 0.36f), 4).transform;
                    bowLeft.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Transform bowRight = AddSprite(body, "BowR", sprites.Heart, bowColor, new Vector2(0.2f, 0.52f), new Vector2(0.42f, 0.36f), 4).transform;
                    bowRight.localRotation = Quaternion.Euler(0f, 0f, -90f);
                    AddSprite(body, "BowKnot", sprites.Circle, Color.white, new Vector2(0f, 0.52f), Vector2.one * 0.16f, 5);
                    SetField(flair, "aura", aura);
                },
                go => SetField(go.GetComponent<MPDramaQueenNpc>(), "wanderSpeed", 2.2f));

            GameObject celebrity = CreateMPNpcPrefab<MPNpc>("MPNpc_Celebrity", MPNpc.NpcType.Celebrity, 0.95f, sprites, burst, ring, popup, obstacleLayer,
                (body, flair) =>
                {
                    Transform spinner = AddSprite(body, "Glow", sprites.Ring, new Color(gold.r, gold.g, gold.b, 0.85f), Vector2.zero, Vector2.one * 1.35f, 1).transform;
                    AddSprite(spinner, "SparkleA", sprites.Circle, Color.white, new Vector2(0.45f, 0f), Vector2.one * 0.1f, 2);
                    AddSprite(spinner, "SparkleB", sprites.Circle, Color.white, new Vector2(-0.45f, 0f), Vector2.one * 0.1f, 2);
                    AddSprite(body, "Crown", sprites.Crown, gold, new Vector2(0f, 0.62f), new Vector2(0.62f, 0.5f), 4);
                    SetField(flair, "spinner", spinner);
                },
                go =>
                {
                    // Worth 3 points and 2.5x harder to win over (recruit 250 love, steal 500).
                    SetField(go.GetComponent<MPNpc>(), "scoreValue", 3);
                    SetField(go.GetComponent<MPNpc>(), "wanderSpeed", 2.6f);
                    SetField(go.GetComponent<MPCapture>(), "sensitivity", 0.4f);
                });

            GameObject hugger = CreateMPNpcPrefab<MPHuggerNpc>("MPNpc_Hugger", MPNpc.NpcType.Hugger, 0.9f, sprites, burst, ring, popup, obstacleLayer,
                (body, flair) =>
                {
                    Color armColor = new Color(1f, 1f, 1f, 0.9f);
                    Transform left = AddSprite(body, "ArmL", sprites.Circle, armColor, new Vector2(-0.52f, -0.08f), Vector2.one * 0.3f, 3).transform;
                    Transform right = AddSprite(body, "ArmR", sprites.Circle, armColor, new Vector2(0.52f, -0.08f), Vector2.one * 0.3f, 3).transform;
                    AddSprite(body, "Blush", sprites.Heart, new Color(1f, 0.5f, 0.6f, 0.9f), new Vector2(0f, -0.2f), Vector2.one * 0.2f, 4);
                    SetArray(flair, "arms", left, right);
                    SetField(flair, "waddle", true);
                },
                go =>
                {
                    MPHuggerNpc brain = go.GetComponent<MPHuggerNpc>();
                    SetField(brain, "followSpeed", 6.5f);
                    SetField(brain, "attackRange", 7f);
                    // The charge is a Mover dash; the hug cooldown limits it, not the Mover.
                    Mover mover = go.GetComponent<Mover>();
                    SetField(mover, "dashSpeedMultiplier", 2.6f);
                    SetField(mover, "dashDuration", 0.35f);
                    SetField(mover, "dashCooldown", 0f);
                });

            return new[]
            {
                new MPNpcType { Prefab = basic, Weight = 6f },
                new MPNpcType { Prefab = cupid, Weight = 1.2f, Max = 2 },
                new MPNpcType { Prefab = queen, Weight = 1f, Max = 2 },
                new MPNpcType { Prefab = hugger, Weight = 1.2f, Max = 2 },
                // Exactly one per match: a single prize everyone fights over.
                new MPNpcType { Prefab = celebrity, Weight = 0f, Min = 1, Max = 1 },
            };
        }

        static GameObject CreateMPNpcPrefab<T>(string name, MPNpc.NpcType type, float bodyScale, Sprites sprites, HeartBurst burst, HeartRing ring, FloatingText popup,
            int obstacleLayer, Action<Transform, MPNpcFlair> decorate, Action<GameObject> configure) where T : MPNpc
        {
            var go = new GameObject(name);
            AddBody(go, bodyScale / 2f);
            AddNetworkMotion(go, ownerAuthority: false);

            SpriteRenderer body = AddCharacterBody(go, sprites, MPTeams.NeutralColor, bodyScale, out _);
            // The face and accessories are children of Body so they pop with it (positions are in body units).
            AddSprite(body.transform, "Face", sprites.Face, Color.white, Vector2.zero, Vector2.one, 3);

            float barY = bodyScale / 2f + 0.22f;
            AddSprite(go.transform, "CaptureBarBack", sprites.Square, new Color(0.3f, 0.25f, 0.3f, 0.45f), new Vector2(0f, barY), new Vector2(1f, 0.14f), 5);
            SpriteRenderer loyalty = AddSprite(go.transform, "LoyaltyFill", sprites.Square, Color.white, new Vector2(0f, barY), new Vector2(0f, 0.1f), 6);
            SpriteRenderer capture = AddSprite(go.transform, "CaptureFill", sprites.Square, Color.white, new Vector2(0f, barY), new Vector2(0f, 0.1f), 6);

            var mover = go.AddComponent<Mover>();
            SetField(mover, "moveSpeed", 2f);
            SetField(mover, "acceleration", 25f);
            go.AddComponent<MPCapture>();
            var bar = go.AddComponent<MPCaptureBar>();
            SetField(bar, "loyaltyFill", loyalty);
            SetField(bar, "captureFill", capture);
            var brain = go.AddComponent<T>();
            SetField(brain, "type", (int)type);
            SetField(brain, "obstacleMask", 1 << obstacleLayer);
            SetField(brain, "spawnOffset", bodyScale / 2f + 0.1f);
            SetField(brain, "bodyRadius", bodyScale / 2f);
            SetField(brain, "projectileSpeed", 10f * ProjectileSpeedScale);
            SetField(brain, "projectileLifetime", 2f / ProjectileSpeedScale);
            var visuals = go.AddComponent<MPNpcVisuals>();
            SetField(visuals, "body", body);
            SetField(visuals, "badge", AddTeamBadge(body.transform, sprites));
            SetField(visuals, "teamStyle", sprites.TeamStyle);
            SetField(visuals, "burstPrefab", burst);
            SetField(visuals, "popupPrefab", popup);

            if (decorate != null)
            {
                var flair = go.AddComponent<MPNpcFlair>();
                SetField(flair, "visuals", visuals);
                SetField(flair, "ringPrefab", ring);
                SetField(flair, "burstPrefab", burst);
                decorate(body.transform, flair);
            }
            configure?.Invoke(go);
            return SaveMPPrefab(go, name);
        }

        static void SetArray(Object target, string field, params Object[] values)
        {
            var serialized = new SerializedObject(target);
            SetArray(serialized.FindProperty(field), values);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CreateMPMatchPrefab(GameObject player, MPNpcType[] npcTypes, MPProjectile projectile, HeartBurst burst, HeartRing ring,
            MPWeapon[] weapons, GameObject pickup)
        {
            var go = new GameObject("MPMatch", typeof(NetworkObject));
            var match = go.AddComponent<MPMatch>();
            SetField(match, "playerPrefab", player.GetComponent<NetworkObject>());
            SetField(match, "projectilePrefab", projectile);
            SetField(match, "burstPrefab", burst);
            SetField(match, "ringPrefab", ring);
            var serialized = new SerializedObject(match);
            SerializedProperty list = serialized.FindProperty("playerSpawns");
            Vector2[] points = { new(-MPSpawnX, MPSpawnY), new(MPSpawnX, -MPSpawnY), new(MPSpawnX, MPSpawnY), new(-MPSpawnX, -MPSpawnY) };
            list.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) list.GetArrayElementAtIndex(i).vector2Value = points[i];
            Vector2 spawnArea = new Vector2(MPHalfWidth - 2f, MPHalfHeight - 2f);
            serialized.FindProperty("npcSpawnHalfExtents").vector2Value = spawnArea;
            SetArray(serialized.FindProperty("weapons"), weapons);
            SerializedProperty table = serialized.FindProperty("npcTypes");
            table.arraySize = npcTypes.Length;
            for (int i = 0; i < npcTypes.Length; i++)
            {
                SerializedProperty entry = table.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("prefab").objectReferenceValue = npcTypes[i].Prefab.GetComponent<NetworkObject>();
                entry.FindPropertyRelative("weight").floatValue = npcTypes[i].Weight;
                entry.FindPropertyRelative("minCount").intValue = npcTypes[i].Min;
                entry.FindPropertyRelative("maxCount").intValue = npcTypes[i].Max;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var spawner = go.AddComponent<MPPickupSpawner>();
            SetField(spawner, "pickupPrefab", pickup.GetComponent<NetworkObject>());
            SetField(spawner, "areaHalfExtents", spawnArea);
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

        // ---------- Skill HUD ----------

        // [icon with radial cooldown overlay + SPACE badge]  DASH  14.5s
        static SkillSlotUI CreateSkillSlotPrefab(Sprites sprites)
        {
            var root = new GameObject("SkillSlot", typeof(RectTransform)).GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(470f, 120f);
            var layout = root.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredWidth = 470f;
            layout.preferredHeight = 120f;

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

            AddBackdrop(root, sprites);
            CreateTitle(root, "PRIVATE ROOM", 120f, new Vector2(0f, 420f), new Vector2(1400f, 150f));

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
            (UnityEngine.UI.Button copy, TMPro.TextMeshProUGUI copyLabel) = CreateMenuButton(copyHolder, "CopyButton", "COPY", sprites.RoundedRect, Berry, new Vector2(220f, 90f), 40f);

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
                swatch.sprite = sprites.TeamStyle.SymbolOf(slot);
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
            AddSizedSprite(arena, "Floor", sprites.FloorTile, Color.white, Vector2.zero, new Vector2(w * 2f, h * 2f), -10, SpriteDrawMode.Tiled);

            var walls = new GameObject("Walls").transform;
            walls.SetParent(arena);
            AddBlock(walls, "Wall Top", sprites, obstacleLayer, new Vector2(0f, h + t / 2f), new Vector2(w * 2f + t * 2f, t), color: WallColor);
            AddBlock(walls, "Wall Bottom", sprites, obstacleLayer, new Vector2(0f, -h - t / 2f), new Vector2(w * 2f + t * 2f, t), color: WallColor);
            AddBlock(walls, "Wall Left", sprites, obstacleLayer, new Vector2(-w - t / 2f, 0f), new Vector2(t, h * 2f), color: WallColor);
            AddBlock(walls, "Wall Right", sprites, obstacleLayer, new Vector2(w + t / 2f, 0f), new Vector2(t, h * 2f), color: WallColor);

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
            TMPro.TextMeshProUGUI timer = CreateText("Timer", bar, "3:00", 64f, new Color(0.35f, 0.25f, 0.35f));
            Stretch(timer.rectTransform);

            // Top-left live leaderboard.
            RectTransform board = CreateRect("Leaderboard", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(430f, 312f));
            (Object[] boardRows, Object[] boardSwatches, Object[] boardNames, Object[] boardCounts) =
                CreateScoreRows(board, sprites, new Vector2(0f, -14f), 72f, 390f, 30f);

            TMPro.TextMeshProUGUI countdown = CreateText("Countdown", root, "3", 220f, DeepPink);
            countdown.rectTransform.sizeDelta = new Vector2(1400f, 300f);

            SkillBarUI skillBar = CreateSkillBar(root, slotPrefab);
            RectTransform weaponPanel = CreateWeaponPanel(root, sprites);
            RectTransform statusPanel = CreateStatusPanel(root, sprites);
            CreateFeed(root);

            // Results: dim + banner with ranking and buttons. Hidden until the match ends.
            RectTransform panel = CreateRect("ResultPanel", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddImage(panel, new Color(0.3f, 0.15f, 0.25f, 0.4f)).raycastTarget = true;
            RectTransform banner = CreateRect("Banner", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 840f));
            UnityEngine.UI.Image bannerImage = AddImage(banner, new Color(1f, 0.97f, 0.99f, 0.97f));
            bannerImage.sprite = sprites.RoundedRect;
            bannerImage.type = UnityEngine.UI.Image.Type.Sliced;
            TMPro.TextMeshProUGUI resultTitle = CreateText("Title", banner, "PINK WINS!", 96f, DeepPink);
            resultTitle.rectTransform.anchoredPosition = new Vector2(0f, 330f);
            resultTitle.rectTransform.sizeDelta = new Vector2(940f, 130f);
            TMPro.TextMeshProUGUI subtitle = CreateText("Subtitle", banner, "FINAL TEAM SCORES", 34f, SoftPlum);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, 240f);
            subtitle.rectTransform.sizeDelta = new Vector2(900f, 50f);
            RectTransform rowsRoot = CreateRect("Ranking", banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 200f), new Vector2(800f, 380f));
            (Object[] resultRows, Object[] resultSwatches, Object[] resultNames, Object[] resultCounts) =
                CreateScoreRows(rowsRoot, sprites, Vector2.zero, 94f, 800f, 42f);

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

            SetField(root.gameObject.AddComponent<AimCursor>(), "cursorTexture", sprites.AimCursor);
            var hud = root.gameObject.AddComponent<MPHUD>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("timerText").objectReferenceValue = timer;
            serialized.FindProperty("countdownText").objectReferenceValue = countdown;
            serialized.FindProperty("teamStyle").objectReferenceValue = sprites.TeamStyle;
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
                skillBar.gameObject.AddComponent<CanvasGroup>(), bar.gameObject.AddComponent<CanvasGroup>(),
                weaponPanel.gameObject.AddComponent<CanvasGroup>(), statusPanel.gameObject.AddComponent<CanvasGroup>());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(lobby.onClick, hud.ReturnToLobby);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(menuButton.onClick, hud.ReturnToMainMenu);
        }

        // Bottom-left, above the skill bar: [icon]  WEAPON NAME   12.3s  + a draining time bar.
        static RectTransform CreateWeaponPanel(Transform root, Sprites sprites)
        {
            RectTransform panel = CreateRect("WeaponPanel", root, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24f, 160f), new Vector2(470f, 110f));

            Vector2 iconCenter = new Vector2(62f, 55f);
            RectTransform iconBack = CreateRect("IconBack", panel, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), iconCenter, Vector2.one * 84f);
            AddImage(iconBack, SoftPlum).sprite = sprites.Circle;
            RectTransform iconRect = CreateRect("Icon", panel, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), iconCenter, Vector2.one * 56f);
            UnityEngine.UI.Image icon = AddImage(iconRect, Color.white);
            icon.sprite = sprites.Heart;
            icon.preserveAspect = true;

            TMPro.TextMeshProUGUI name = CreateText("Name", panel, "LOVE GUN", 34f, DeepPink);
            name.alignment = TMPro.TextAlignmentOptions.Left;
            name.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            name.enableAutoSizing = true;
            name.fontSizeMin = 22f;
            name.fontSizeMax = 34f;
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            name.rectTransform.anchoredPosition = new Vector2(120f, -10f);
            name.rectTransform.sizeDelta = new Vector2(185f, 50f);
            TMPro.TextMeshProUGUI time = CreateText("Time", panel, "UNLIMITED", 28f, SoftPlum);
            time.alignment = TMPro.TextAlignmentOptions.Right;
            time.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            time.rectTransform.anchorMin = time.rectTransform.anchorMax = new Vector2(1f, 1f);
            time.rectTransform.pivot = new Vector2(1f, 1f);
            time.rectTransform.anchoredPosition = new Vector2(-18f, -14f);
            time.rectTransform.sizeDelta = new Vector2(130f, 44f);
            time.enableAutoSizing = true;
            time.fontSizeMin = 18f;
            time.fontSizeMax = 26f;

            RectTransform barBack = CreateRect("TimeBarBack", panel, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(120f, 22f), new Vector2(330f, 22f));
            UnityEngine.UI.Image barBackImage = AddImage(barBack, new Color(0.45f, 0.28f, 0.42f, 0.2f));
            barBackImage.sprite = sprites.RoundedRect;
            barBackImage.type = UnityEngine.UI.Image.Type.Sliced;
            RectTransform barRect = CreateRect("TimeBar", barBack, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UnityEngine.UI.Image bar = AddImage(barRect, DeepPink);
            bar.sprite = sprites.Square;
            bar.type = UnityEngine.UI.Image.Type.Filled;
            bar.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            bar.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;

            var hud = panel.gameObject.AddComponent<MPWeaponHUD>();
            SetField(hud, "icon", icon);
            SetField(hud, "nameText", name);
            SetField(hud, "timeText", time);
            SetField(hud, "timeBar", bar);
            return panel;
        }

        // Bottom-left, above the weapon panel: YOU: name / (o) PINK TEAM / your love meter + LOYAL / SAFE 2.1s / BLUE 40%.
        static RectTransform CreateStatusPanel(Transform root, Sprites sprites)
        {
            RectTransform panel = CreateRect("StatusPanel", root, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24f, 286f), new Vector2(470f, 128f));

            TMPro.TextMeshProUGUI name = CreateText("Name", panel, "YOU", 30f, SoftPlum);
            name.alignment = TMPro.TextAlignmentOptions.Left;
            name.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            name.rectTransform.anchoredPosition = new Vector2(20f, -8f);
            name.rectTransform.sizeDelta = new Vector2(430f, 40f);

            RectTransform dot = CreateRect("TeamSwatch", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(36f, -66f), Vector2.one * 30f);
            UnityEngine.UI.Image swatch = AddImage(dot, Color.white);
            swatch.sprite = sprites.Circle;
            TMPro.TextMeshProUGUI team = CreateText("Team", panel, "PINK TEAM", 32f, DeepPink);
            team.alignment = TMPro.TextAlignmentOptions.Left;
            team.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            team.rectTransform.anchorMin = team.rectTransform.anchorMax = new Vector2(0f, 1f);
            team.rectTransform.pivot = new Vector2(0f, 0.5f);
            team.rectTransform.anchoredPosition = new Vector2(62f, -66f);
            team.rectTransform.sizeDelta = new Vector2(250f, 40f);
            TMPro.TextMeshProUGUI meterText = CreateText("MeterText", panel, "LOYAL", 26f, SoftPlum);
            meterText.alignment = TMPro.TextAlignmentOptions.Right;
            meterText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            meterText.rectTransform.anchorMin = meterText.rectTransform.anchorMax = new Vector2(1f, 1f);
            meterText.rectTransform.pivot = new Vector2(1f, 0.5f);
            meterText.rectTransform.anchoredPosition = new Vector2(-20f, -66f);
            meterText.rectTransform.sizeDelta = new Vector2(170f, 40f);

            // Love meter: loyalty from the left in your team's color, the capturer's progress from the right.
            RectTransform meter = CreateRect("LoveMeter", panel, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(20f, 16f), new Vector2(430f, 24f));
            UnityEngine.UI.Image meterBack = AddImage(meter, new Color(0.45f, 0.28f, 0.42f, 0.2f));
            meterBack.sprite = sprites.RoundedRect;
            meterBack.type = UnityEngine.UI.Image.Type.Sliced;
            UnityEngine.UI.Image Fill(string fillName, UnityEngine.UI.Image.OriginHorizontal origin)
            {
                RectTransform rect = CreateRect(fillName, meter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                UnityEngine.UI.Image image = AddImage(rect, Color.white);
                image.sprite = sprites.Square;
                image.type = UnityEngine.UI.Image.Type.Filled;
                image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                image.fillOrigin = (int)origin;
                return image;
            }
            UnityEngine.UI.Image loyalty = Fill("LoyaltyFill", UnityEngine.UI.Image.OriginHorizontal.Left);
            UnityEngine.UI.Image capture = Fill("CaptureFill", UnityEngine.UI.Image.OriginHorizontal.Right);
            capture.fillAmount = 0f;

            var status = panel.gameObject.AddComponent<MPStatusHUD>();
            SetField(status, "teamStyle", sprites.TeamStyle);
            SetField(status, "nameText", name);
            SetField(status, "teamText", team);
            SetField(status, "teamSwatch", swatch);
            SetField(status, "loyaltyFill", loyalty);
            SetField(status, "captureFill", capture);
            SetField(status, "meterText", meterText);
            return panel;
        }

        // Announcements under the timer ("BLUE FELL FOR PINK!").
        static void CreateFeed(Transform root)
        {
            TMPro.TextMeshProUGUI text = CreateText("Feed", root, "", 60f, DeepPink);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(0f, -130f);
            text.rectTransform.sizeDelta = new Vector2(1300f, 170f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 30f;
            text.fontSizeMax = 60f;
            text.raycastTarget = false;
            SetField(root.gameObject.AddComponent<MPFeed>(), "text", text);
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
