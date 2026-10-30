# LoveShot — Project Guide

Cute, chaotic, pink top-down 2D arena shooter (Unity 6000.6.7f1, URP 2D, C#). Two independent modes:
- **Single Player:** the player (Pink) competes with an AI mini-boss (Blue) that plays by the same rules: both shoot
  love projectiles to convert neutral (Gray) NPCs and steal each other's. Most NPCs when the 90s timer ends wins.
- **Online Multiplayer:** private Relay rooms, 2–4 humans, free-for-all (Pink/Blue/Purple/Yellow), 180s, no AI.
It is **not** a traditional boss fight, and leaders are never eliminated. Multiplayer changes must not break single player.

## Session protocol
- **At the start of a session:** read `GAME_DESIGN.md` (what we agreed) and `PROGRESS.md` (what exists, what's verified, known bugs, next steps).
- **Before major changes and before context compaction:** update `PROGRESS.md` (and `GAME_DESIGN.md` if a design decision changed).
- Only record a feature as done or verified once it actually is. Keep "automated check passed" separate from "needs the user's playtest".
- Work one phase or feature at a time, and only start the next one when the user asks.

## Architecture (`Assets/Scripts`, namespace `LoveShot`)
- **Core (shared by player, AI and NPCs):** `Mover` (movement, dash, `SpeedMultiplier`), `Shooter` (cooldown, spread, `Locked`), `Projectile`, `Affection` (−100..+100 team ownership), `TeamLeader` (static lookup per team), `Lovestruck` (leader stun), `Steering` (wall avoidance, line of sight), `AimIndicator`, `Team`/`TeamUtil`, `ArenaBounds` (on Arena; resizes the floor and outer walls at runtime, `ArenaBounds.Current.HalfExtents` is the live interior size, never below `MinHalfExtents`).
- **Skills (both modes):** `Skill` base class (name, key, icon, cooldown, `TryActivate(direction)`); `DashSkill` wraps `Mover` (single player), `NetDashSkill` asks the host (multiplayer). `SkillBarUI` shows one `SkillSlotUI` per `Skill` on the bound player. A new ability = a new `Skill` subclass.
- **Player:** `PlayerController` reads input only and drives `Mover`/`Shooter` and its `Skill`s by key.
- **AI:** `MiniBossAI` state machine (Recruit / Steal / Evade / Reposition) driving the same `Mover`/`Shooter`/`Lovestruck`. Its tuning comes from an `AIDifficulty` ScriptableObject (`Assets/Settings/Difficulty/Easy.asset`, `Hard.asset`): `GameSettings.SelectedDifficulty` (static, set by the menu), else `defaultDifficulty`. **The builder creates these assets only if they're missing and never overwrites them**, so tune them directly.
- **NPC:** `NPCController` (wander, follow, auto-shoot; virtual hooks `Wander`/`Follow`/`FindTarget`/`Fire`/`OnTeamChanged`), subclasses in `NPC/Types` (`CupidNPC`, `ShyNPC`, `BodyguardNPC`), `AffectionVisuals`, `NPCSpawner` (weighted types).
- **Match:** `MatchManager` (countdown, timer, counts, result, R to restart; `MatchManager.InProgress` gates all gameplay), `MatchHUD` (uGUI + TextMeshPro).
- **UI:** `MainMenu` (panel switching, `Play()`, `CycleDifficulty()`, `Quit()`), `UIFloat`, `ButtonBounce`. `MatchManager.ReturnToMenu()` handles the M key on the result screen.
- **Effects:** `HeartBurst`, `CameraShake` (shakes around `BasePosition`), `ArenaCamera` (every LateUpdate: fits the minimum interior below the HUD bar, then calls `ArenaBounds.Resize` so the walls sit at the screen edges), `LeaderVisuals`, `DashEffects`.
- **Multiplayer (`Scripts/Multiplayer`, namespace `LoveShot.Multiplayer`; Netcode for GameObjects 2.13 + Multiplayer Services 2.4 sessions over Relay; host-authoritative):**
  - `MPConnection` (on the NetworkManager prefab, created by `MultiplayerMenu` on demand, destroyed when the session closes): UGS anonymous sign-in (one auth profile per process), `HostAsync`/`JoinAsync` (return an error string or null), connection approval (max players, no join during a match), disconnect → main menu with `PendingMessage`, `Close()`.
  - `MPLobby` (spawned by the host, DontDestroyOnLoad, lives for the whole session): `NetworkList<MPPlayerInfo>` (clientId, slot = team = color, name), phase, room code; `StartMatch`/`ReturnToLobby` (host) load scenes through Netcode's SceneManager.
  - `MPMatch` (spawned by `MPArenaBootstrap` on the host in MPArena): spawns players/NPCs once, phases Waiting → Countdown → Playing → Ended on server time, `NetworkList<MPScore>`, `Ranking()`, and projectile relay (host simulates `MPProjectile`, clients get visual copies via RPC; hits processed once on the host).
  - `MPPlayer`: owner sends input RPCs (unreliable, 30/s + on change) and `RequestDashRpc`; the host moves it with `Mover`, fires, enforces cooldowns, applies lovestruck. `MPNpc`: host-only AI mirroring `NPCController` with virtual hooks. `MPCapture`: 4-team owner/capturer/progress (rules in `GAME_DESIGN.md` §10). Visuals: `MPPlayerVisuals`, `MPNpcVisuals`. UI: `MultiplayerMenu`, `MPLobbyUI`, `MPHUD`. Camera: `MPFollowCamera` (local only).
  - Movement sync: `NetworkTransform` (2D position only) + `NetworkRigidbody2D`; `Mover` is enabled only on the host. Every networked prefab must be in `Assets/Settings/Network/LoveShotNetworkPrefabs.asset` (the builder regenerates it).
- **Scenes and prefabs are generated** by `Assets/Editor/LoveShotBuilder.cs` + `LoveShotBuilder.Multiplayer.cs` (menu **LoveShot → Rebuild Arena + Menu**): `Assets/Scenes/MainMenu.unity` (build index 0), `Assets/Scenes/LoveShotArena.unity` (1), `Assets/Scenes/MPLobby.unity` (2), `Assets/Scenes/MPArena.unity` (3), `Assets/Prefabs/` and `Assets/Prefabs/Multiplayer/`. **Rebuilding overwrites all four scenes, all prefabs and generated sprites**, so put tuning that must survive into the builder. Arena size, spawns, camera and AI bounds all derive from the builder's `ArenaHalfWidth/ArenaHalfHeight/LeaderSpawnX` constants. `CameraSize/OffsetY` only set the edit-mode framing; `ArenaCamera` takes over at runtime.

## Conventions
- Private `[SerializeField]` fields with `Min`/`Range`/`Tooltip` for every tunable value; no public fields.
- New gameplay goes through the shared Core components; the AI must not get unfair advantages unless the user decides otherwise.
- Multiplayer: clients only send inputs/requests; the host decides movement, hits, capture, ownership, scores and timers. Validate RPC senders (`InvokePermission = RpcInvokePermission.Owner` or check `RpcParams.Receive.SenderClientId`). Keep multiplayer code in `Scripts/Multiplayer` and don't change single-player classes' behavior for it.
- Gameplay `Update`s check `MatchManager.InProgress`.
- Fully qualified uGUI types (`UnityEngine.UI.Image`). Use TMP for text; never set TMP outline/material properties from editor code, because that changes the shared font material.
- Match the existing comment style: a short header comment per class, comments only where the reasoning isn't obvious.

## Working with the Editor (Unity CLI + Pipeline package)
- Drive the live Editor with `unity command ... --project-path "C:\Users\User\Pink Game"`. Never hand-edit `.unity`/`.prefab` YAML.
- Check compiles with `unity recompile --project-path ...`, then run `unity status --until-ready` (the connection drops during domain reload).
- For `script eval` code, put C# in a quoted heredoc variable: a `'` char literal inside single-quoted bash breaks the command.
- Before Play-mode tests: `unity command editor set_auto_tick` and `unity command editor.playmode set_run_in_background` (otherwise the game freezes at frame 1 while the Editor is unfocused). Wait on time with `job wait` (`op` is `greaterThan`, not `gt`).
- Test pitfalls already hit: the match ends at about t=93s and freezes time, teleports can land inside obstacles, and moving targets dodge scripted shots. Hold gameplay still for deterministic checks by setting `MatchManager.CountdownRemaining` high through reflection.
- Screenshots don't include Screen Space Overlay canvases. To see the HUD or menu, switch the canvas to `ScreenSpaceCamera` at runtime (never save that). `ArenaCamera` handles either mode.
- Real-input path: simulate keys with `InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.D, Key.Space))`, and queue an empty `KeyboardState` to release them. Click menu buttons with `ExecuteEvents.Execute(button, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler)`.
- After Play mode, `MainMenu.unity` can show as dirty (layout recalculation only). The builder refuses to run on a dirty scene, so reopen it without saving first.
- **Multiplayer testing:** Multiplayer Play Mode clones are activated through internal API by reflection: `Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode.PlayerTwo/Three/Four` (in assembly `UnityEditor.MultiplayerModule`) → `UnityPlayer.Activate(out ActivationError, List<string>)` / `Deactivate`. `unity command list_instances` lists them; target a clone with `--project-path "<project>\Library\VP\<vpId>"` (`--instance` isn't accepted by these commands). Entering Play in the main Editor starts Play in the clones. Clones read `ProjectSettings` from disk and need a restart after build-settings changes. Clone game views are tiny, so take screenshots from the main Editor.
- **Never edit scripts while a multiplayer Play session runs:** the Editor's domain reload killed the host's NetworkManager mid-match once.
- Simulated mouse input is unreliable in clones (real OS mouse events override it). For shooting tests, call the owner's `SubmitInputRpc` by reflection with the player's `enabled` set to false so `ReadOwnerInput` doesn't override it.
- In `script eval`, call `SessionOptionsExtensions.WithRelayNetwork(options)` statically (extension `using`s aren't available).
- Git: the repo was created 2026-10-29 with a Unity `.gitignore`; checkpoint commit `4869fca` is the pre-multiplayer state.

## Docs
- `GAME_DESIGN.md`: confirmed mechanics, controls, scoring, AI and NPC design, and open ideas.
- `PROGRESS.md`: implementation status, verification record, known issues and next steps.
