# LoveShot — Progress

Last updated: 2026-10-30. Git checkpoints: `4869fca` (pre-multiplayer), `024c638` (Update 4), `ce98e98` (Updates 5+6; the last version with single player). `30737d2` (Updates 7, 8 + single-player removal), pushed to https://github.com/NicoLiao/Pink-Game (branch `master`).

How to read this file:
- **Verified (automated):** Claude checked it in the live Editor through the Unity CLI, using scripted calls, not a human on keyboard and mouse.
- **Needs playtest:** not yet checked by the user.

## Status overview
| Phase | Scope | Status |
|---|---|---|
| 1 Core conversion | Player movement and shooting, neutral NPCs, affection/conversion, allies follow leader | Single-player only; **removed** 2026-10-30 (in `ce98e98`) |
| 2 AI mini-boss | Blue AI (Recruit/Steal/Reposition), ally auto-shooting, timer, scoring, HUD | **Removed** 2026-10-30 |
| 3 Improvements | Lovestruck leader hits, AI Evade/harass/smarter targeting, NPC movement upgrades, Cupid/Shy/Bodyguard, effects and HUD polish | **Removed** 2026-10-30 (multiplayer has its own versions of lovestruck, Cupid and effects) |
| Update 1 (2026-10-29) | Main menu with Play, love meter fix (full bar at ±100), larger arena + zoomed-out camera | Menu kept and reworked; the rest **removed** with single player |
| Update 2 (2026-10-29) | Adaptive arena camera, dash rework + effects, Easy/Hard AI difficulty, menu difficulty selector + Quit, M to return to menu | Dash rework (`Mover`) and Quit kept; the rest **removed** |
| Update 3 (2026-10-29) | Arena fills the screen: walls flush with the screen edges, border pattern removed | **Removed** (single-player arena) |
| Update 4 (2026-10-29) | Online Multiplayer FFA (private Relay rooms, 2–4 players, lobby, 4-team capture, networked NPCs, results/restart), skill system + HUD skill slot, 20s Dash cooldown, new menu | Implemented; automated multi-instance checks passed (4 Editor instances over real Relay); 5 issues found and fixed |
| Update 5 (2026-10-30) | Multiplayer gameplay expansion: player conversion (identity vs team, personal NPC ownership, 3s immunity), team scoring, 4 weapons + timed pickups + weapon HUD, Drama Queen / Celebrity / Cupid / Hugger, status panel, event feed, +1 popups, two-line team leaderboard | Implemented; automated multi-instance checks passed (4 Editor instances over real Relay); 2 issues found and fixed |
| Update 6 (2026-10-30) | No white backgrounds behind in-game HUD panels, crosshair mouse cursor (`AimCursor`), all projectiles 1.5x faster with unchanged reach (builder constant `ProjectileSpeedScale`, weapon assets scaled once) | Implemented, automated checks passed (prefab/asset values, screenshots, 0 console errors); cursor look and new shot speed need playtest |
| Update 7 (2026-10-30) | Multiplayer lag fix (owner-driven movement with host validation, instant dash, predicted shots reconciled with the host's projectiles, local hit bursts, lighter NetworkTransform sync), toon art (shaded outlined sprites, faces, candy blocks, tiled floor, glows and trails), squash-and-stretch animation, smooth color blends, cleaner menu and lobby | Implemented; automated 2- and 4-instance checks over real Relay passed; look and feel need the user's playtest |
| Update 8 (2026-10-30) | All-pink theme in the style of the two reference images: inked cartoon sprites (thick outline, cel shading, grain, wobble), teams as pink shades + symbols (heart/moon/flower/diamond via `TeamStyle`), team names Pink/Berry/Coral/Blush, NPC team badges, white sticker border on players, paper floor, dusty-rose blocks, plum walls, inked UI buttons | Implemented; compile clean, rebuilt, 2-player MP screenshots checked, 0 game errors; look needs the user's eyes |
| Single player removed (2026-10-30) | User request: "remove the single player mode completely". Deleted the single-player scene, prefabs, scripts (AI, Match, NPC, Player, single-player Core/Effects) and difficulty assets; menu is now HOST GAME / JOIN GAME / QUIT | Done; compile clean (0 errors), rebuilt, menu/host/leave/join-page checked in Play mode (details below) |

No phase has been playtested by the user yet.

## What exists in the project
- **Scenes** (Build Settings order):
  0. `Assets/Scenes/MainMenu.unity`: Main Camera, Global Light 2D, EventSystem (`InputSystemUIInputModule`), MenuCanvas (floating hearts, "LOVESHOT" title, two pages: Main = HOST GAME / JOIN GAME / QUIT + status; Join = code field / JOIN / BACK + status) and Main Menu (`MainMenu` + `MultiplayerMenu`).
  1. `Assets/Scenes/MPLobby.unity`: camera, EventSystem, LobbyCanvas (`MPLobbyUI`: title, room code + COPY, PLAYERS n/4, 4 slot rows, START GAME (host only), LEAVE).
  2. `Assets/Scenes/MPArena.unity`: Main Camera (`CameraShake` + `MPFollowCamera`), Global Light 2D, Arena (fixed 56×34 interior, 4 walls, 21 obstacles mirrored on both axes, `Obstacles` layer), Match Bootstrap (`MPArenaBootstrap`, spawns the networked `MPMatch` on the host), EventSystem, HUD (`MPHUD`: timer, leaderboard, countdown, skill bar, results panel).
  3. `SampleScene`: pre-existing, untouched.
  Netcode scene sync only works for scenes in this list, and MPPM clones read it from disk (`ProjectSettings/EditorBuildSettings.asset`).
- **Prefabs** (`Assets/Prefabs`): SkillSlot (HUD skill slot), HeartBurst, HeartRing, FloatingText.
- **Multiplayer prefabs** (`Assets/Prefabs/Multiplayer`): NetworkManager (`NetworkManager` + `UnityTransport` + `MPConnection`; approval on, scene management on, tick 30, no auto player prefab), MPLobby, MPMatch (+ `MPPickupSpawner`, weapon list, NPC spawn table), MPPlayer (with `MPCapture`, love bar, identity symbol, sticker border, immunity shield), MPNpc, MPNpc_Cupid, MPNpc_DramaQueen, MPNpc_Hugger, MPNpc_Celebrity, MPWeaponPickup (all networked, registered in `Assets/Settings/Network/LoveShotNetworkPrefabs.asset`), MPLoveProjectile (not networked; host-simulated with client visual copies; heart/rocket/arrow styles). NGO also auto-generated `Assets/DefaultNetworkPrefabs.asset`; nothing references it.
- **Settings assets:** `Assets/Settings/Weapons/DefaultGun, BounceGun, Shotgun, RocketLauncher.asset` (`MPWeapon`; created only if missing, tune them directly) and `Assets/Settings/Resources/TeamStyle.asset` (team symbols, names and colors; read by `MPTeams` through `Resources`; edit it directly).
- **Networking packages (Update 4):** `com.unity.netcode.gameobjects` 2.13.3, `com.unity.services.multiplayer` 2.4.1 (Sessions + Relay; brings Authentication, Core, Transport 6.6), `com.unity.multiplayer.playmode` 3.0.0 (local multi-instance testing). The project is linked to Unity Cloud project `c476fc30-9a8b-4cac-8850-ffbab9aca8be` (org `applenico0507`); anonymous sign-in and Relay worked with no dashboard changes.
- **Generated sprites:** `Assets/Art/Generated` (base shapes, inked toon sprites, team symbols Heart/SymbolStar (now a crescent moon)/SymbolFlower/SymbolDiamond, Face, CandyBlock, FloorTile, Backdrop, weapon icons, DashIcon, AimCursor).
- **Scripts:** 44 runtime scripts in `Assets/Scripts`: `Core` (`Mover`, `Steering`, `AimIndicator`, `TeamStyle`, `Skills/Skill`; `NetDashSkill` lives in Multiplayer), `Effects`, `UI` and `Multiplayer` (+ `Multiplayer/Npcs`). The editor builder is `Assets/Editor/LoveShotBuilder.cs` + `LoveShotBuilder.Multiplayer.cs` + `LoveShotBuilder.Art.cs` (menu **LoveShot → Rebuild Arena + Menu**, builds the three scenes).
- **Packages added:** TextMeshPro Essential Resources were imported into `Assets/TextMesh Pro`. The Pipeline package (`com.unity.pipeline` 0.9.1-exp.1) was already present.
- **Last compile:** 0 errors (after the single-player removal).

## Verified (automated, live Editor)
_Phase 1–3 and Updates 1–3 below describe the removed single-player mode; kept as history. Single-player lines inside later updates are history too._

**Phase 1**
- Affection: 4 hits leave an NPC neutral at 80 and the 5th converts it. An owned NPC worn to 0 stays Pink, and healing works. Three full Pink→Blue→Pink cycles switched team 9 times with nothing stuck.
- A real projectile fired through the player's `Shooter` gives +20 per hit, respects the 0.25s cooldown, converts the NPC, updates its color, and the next shot passes through the now fully loyal ally.
- A converted NPC walked from 23.6 to about 1.86 units from the leader, stopping near `followDistance` = 1.8.

**Phase 2**
- The AI recruited all 12 NPCs in about 22s against an idle player, then switched to Reposition when no targets were left.
- Given 4 Pink NPCs, the AI switched to Steal immediately and took all 4 back in about 8s.
- Pink and Blue allies wear down each other's loyalty.
- Match end at 0:00: `BlueWins` with 7–5, gameplay frozen (`timeScale` 0), and the result banner shows the correct text. HUD TMP text has a font, non-zero size and no overflow.
- `MatchManager.Restart()` reloads with a fresh timer, new neutral NPCs and the AI enabled.

**Phase 3**
- Countdown: shows 3 → GO! → hidden; the timer stays at 90 until play starts and `InProgress` is false during the countdown.
- Spawner produces mixed types (one run: 8 Basic, 1 Cupid, 2 Shy).
- Lovestruck on a stationary AI: the first leader hit stunned it (speed ×0.35, Shooter locked); a hit inside the immunity window passed through; a hit after immunity stunned it again. An ally (non-leader) shot passed through without stunning.
- Evade (with `dodgeChance` forced to 1): the AI entered Evade on 3 of 5 shots and was never stunned. The other 2 shots were already missing. Dash-dodge never triggered (threats were seen early).
- Harass: a player parked 3.5 units from the AI was stunned 5 times in about 12s, while the AI kept recruiting.
- Shy: fled from 1.35 to 2.57 units at ×1.6 speed. Sensitivity 0.75 verified.
- Bodyguard: sensitivity 0.5 verified (+20 hit → +10). After the fix, it reaches its guard spot (0.20 away) without shoving the player.
- Cupid: fires a fan of 3 hearts.

**Update 1: menu, love meter, arena**
- Main menu: Play mode starts in MainMenu with PLAY selected. A pointer click delivered through the EventSystem (`ExecuteEvents.pointerClickHandler`) loads LoveShotArena and the countdown runs. The menu was visually checked in a capture (with the canvas switched to camera space at runtime only).
- Love meter, with gameplay paused, through the real `Affection.ApplyLove`: 0 → empty; +50 → 50% pink; +100 → 100% pink (and Pink body); 50 → 50% pink; 0 → empty; −50 → 50% blue (body still Pink because of hysteresis); −100 → 100% blue (and Blue body); 0 → empty; +100 → 100% pink. The fill is always left-anchored inside the bar.
- Arena: leaders spawn at x = ±15; NPCs spawned within x [−9.3, 12.7], y [−7.6, 7.4]; AI reposition extents (17, 9).
- Bounds: the player, under script control, was pushed and dashed (3 dashes each) at all 4 walls and 2 diagonals, and stayed inside every time (it stopped at y = ±9.50 against the top and bottom walls). In a 20s natural run, no leader or NPC left the interior.
- Arena capture with the HUD: the whole arena is visible, the HUD bar covers only the top wall, and full meters show on owned NPCs.

**Update 2: camera, dash, difficulty, menu**
- Camera fit, computed for aspect ratios 2.13 (the user's Game view, 1094×513), 16:9, 16:10, 4:3, 21:9 and 1:1: the full 36×20 interior is visible below the HUD bar every time. Capture at 1094×513: the arena fills the height and the side margins show the heart-pattern border. The same fit results with the HUD in Overlay or Camera mode.
- Dash through **simulated keyboard input** (Input System `QueueStateEvent`, the real `PlayerController` path):
  - D moves right at 7 u/s; Space with no key held dashes in the last movement direction.
  - Space before any input dashes toward the arena center.
  - Space during the cooldown is ignored.
  - From a standstill, a dash covers 4.05 units and ends crisply.
  - A dash into the bottom wall stops at y = −9.495.
  - The 2s cooldown ring fills progressively, and afterimages spawn during the dash.
- Menu flow through EventSystem clicks:
  - A fresh menu shows Easy, and `GameSettings` = Easy.
  - Difficulty cycles Easy → Hard → Easy, and the button tints with each MenuColor.
  - Selecting Hard and pressing Play gives the AI the Hard profile (6° aim error, fire cooldown ×1).
  - `Restart()` keeps Hard.
  - `ReturnToMenu()` shows the menu still on HARD.
  - Switching to Easy and pressing Play gives the AI Easy (14° aim error, fire cooldown ×1.4).
- The Hard asset's values equal the pre-Update-2 AI exactly. All of Easy's "mistake" settings are 0 or 1 on Hard, which switches them off.
- Easy vs Hard against an idle player (one run each, random spawns):
  - Easy: first conversion about 13s after GO; 5/12 NPCs at 24s.
  - Hard: 3 NPCs by 8s; all 9 at 24s.
- Easy, contested, with Pink starting on 3 NPCs and the player idle (after the Easy retune): Blue 4 by 24s, then entered Steal and took all 3 Pink NPCs by 46s. The AI is never inactive or unable to recruit.

**Update 3: arena fills the screen**
- In Play mode at the user's Game view (1094×513, aspect 2.13), the interior grew to 49.5×20. Exactly 0.40 units of wall show at the left, right and bottom screen edges, and 0.40 units of wall show directly under the HUD bar. The top wall extends to the screen top behind the HUD, and NPCs spawned across the wider field (x from −21.9 to 23.9). A capture confirmed this: no margin and no decoration outside the arena.
- Forced camera aspects 16:9, 4:3, 1:1, 21:9 and 16:10 gave the same 0.40-unit wall at every edge, an interior never below 36×20, and the top wall reaching the screen top. After shrinking from 21:9 to 16:10 mid-match, no rigidbody was outside the interior.
- 0 compile errors and warnings; no console errors.

**Update 4: online multiplayer, skills, 20s dash**
Method: the main Editor (host) plus three Multiplayer Play Mode clones (Players 2–4), each driven through the Pipeline CLI. All traffic went through real Unity Relay servers. Inputs were simulated keyboard events or the same input RPC the client sends; no human played.
- **Sign-in and hosting:** anonymous UGS sign-in and a private Relay session worked with no dashboard setup. The host got a code (e.g. `98JWJ8`) and landed in the lobby at 1/4.
- **Joining:** 3 clients joined by code (one typed in lowercase) → every instance showed 4/4 with the same names, slots and colors; lobby screenshots correct for host (START GAME) and client (no start button, "Waiting for the host...").
- **Join errors:** unknown code `ABCDEF` → "No room found with that code."; empty code → "Enter a room code."; joining a running match → "That room's match has already started." (room locked); full room, tested by capping a fresh room at 2 players → third joiner got "That room is full." (the Sessions service enforces the cap; the real cap is 4). A literal fifth instance was not available.
- **Leave/rejoin in lobby:** count dropped to 3/4 for everyone and the leaver returned to the Online page; rejoining took the free slot.
- **Start:** a client calling `StartMatch` was ignored; the host's start moved everyone to the arena. 4 players and 22 NPCs spawned once (same count on every instance), each player at its own corner.
- **Movement authority:** a client's W key moved only its own character (+11.3 units), identical on host and another client. Calling another player's dash RPC from a client is refused ("This RPC can only be sent by its owner").
- **Dash:** Space on a client dashed 4.06 units in the last move direction; the 20s cooldown showed on every instance only for that player; Space again and a direct RPC during the cooldown were both refused by the host.
- **Capture rules** (scripted hit sequence on the host): neutral capture, competing capture with overflow, full capture, loyal NPC ignoring its own team, steal needing 200, defending reduces progress, completed steal — all matched the design. End to end, a client's shots converted an NPC and every instance showed the same owner and Follow behavior; NPC allies then kept recruiting on their own with identical counts everywhere.
- **Leaving mid-match:** that player's NPCs turned neutral and their leaderboard row disappeared; the other three kept playing.
- **Match end:** forced to end early on the host; every instance showed phase Ended, the same ranking (a real tie happened: two players with 5 shared 1st, "IT'S A TIE!"), frozen NPCs, Return to Lobby only on the host. Screenshots checked.
- **Restart:** a client's Return to Lobby did nothing; the host's brought everyone back with the player list intact; a second match spawned 19 NPCs for 3 players with no duplicates.
- **Host leaving:** the host chose Main Menu mid-match → both clients returned to the Online page with "The host left the game."; NetworkManagers destroyed everywhere.
- **Single player still works:** via the new menu, Hard and the default Easy both applied and the AI recruited; player and AI dash cooldowns are 20s; the HUD skill slot counts down ("DASH 17.9s", overlay draining) and Space on cooldown does nothing.
- 0 compile errors and warnings.

**Update 6: HUD backgrounds, cursor, projectile speed**
- Prefab and asset values after the rebuild: SP leaders and AI 18, SP NPCs 15, SP projectile lifetime 1.33s; MP NPCs 15 / 1.33s, Cupid 22.5; weapon assets Default 18 / 1.333s, Bounce 19.5 / 1.733s, Shotgun 22.5 / 0.24s, Rocket 12 / 1.067s (was 12 / 2, 13 / 2.6, 15 / 0.36, 8 / 1.6). Every value is 1.5x the old speed with the same reach.
- Screenshots of a single-player and a 2-instance multiplayer match: no white panels; timer, scores, leaderboard, status, weapon and Dash text stay readable directly on the arena.
- `AimCursor.png` generated as a 48x48 Cursor texture (crosshair); `AimCursor` is active on the HUD in both arenas. Screenshots don't include the OS cursor, so **how it looks in game needs a playtest**. It shows in both arenas, including on the result screen, and the normal arrow returns in menus and the lobby.
- 0 compile errors; 0 console errors.

**Update 5: player conversion, weapons, special NPCs, HUD**
Method: as in Update 4: main Editor (host) + MPPM clones driven through the Pipeline CLI over real Relay, 2 instances for the first conversion run and 4 for everything else. Hits were applied with the host's own `MPMatch.ApplyLove` (the same function projectiles call), client actions through the real client RPCs (`SubmitInputRpc`, `RequestDashRpc`). No human played.

The 16 requested test cases:
| # | Test | Result |
|---|---|---|
| 1 | Pink converts Blue; Blue's NPCs become Pink | **Passed (automated).** 12 hits left Blue at 96%; the 13th (250 love) converted. Blue's 3 NPCs switched to Pink and kept Blue as personal owner. Host and clients agreed. |
| 2 | Converted players keep movement, shooting and dash | **Passed (automated, client RPC path).** A converted client moved (y −10.2 → −7.0), fired, and dashed (cooldown 19.3s on the host). Real keyboard/mouse on a converted client: **needs playtest**. |
| 3 | Converted players shoot in their new team's color | **Passed (automated).** The converted Blue client's projectiles on the host had Pink's exact color. |
| 4 | Friendly projectiles can't convert teammates | **Passed (automated).** 5 hits from the new team and a self-hit were all rejected (progress stayed 0). |
| 5 | Immunity lasts 3 seconds | **Passed (automated).** 3.00s at conversion; all 13 of Purple's hits during it were rejected; the HUD showed "SAFE 2.5s" on the converted client. |
| 6 | Re-conversion after immunity | **Passed (automated).** Once immunity ended, Purple converted the ex-Blue player with 13 hits. |
| 7 | No ownership loops | **Passed (automated).** Blue → Pink → Purple, then the converted Blue converted Pink. Each time only the converted player's personal NPCs moved: Pink's stayed Pink until Pink was converted, then followed Pink. A neutral NPC recruited by converted Blue joined Pink's team with Blue as personal owner. All 4 instances showed the same teams and leaderboard (Purple team = players 0+1+2, 7 pts). |
| 8 | Temporary weapons expire after 30s | **Passed (automated).** Equipped at 29.6s left → back to the Default Gun after it ran out; replacing the Bounce Gun (19.9s left) with a Shotgun restarted the timer at about 30s. |
| 9 | All players see the same pickups | **Passed (automated).** Same pickup IDs, weapons and positions on all 4 instances. Two players placed on one pickup in the same frame: only one got it; it vanished everywhere; every client showed the same weapon and countdown. |
| 10 | Rocket explosions hit each target once | **Passed (automated).** The center NPC got exactly 70 (0.70 of 100); an enemy player 1.5 units away got exactly 47.3 (0.189 of 250). |
| 11 | Drama Queen chains end safely | **Passed (automated).** Queen A's burst pushed Queen B (80% → over 100%), who burst 0.4s later and reached an NPC only she could reach. Both then went on cooldown (3.1s / 3.5s left); no further bursts. |
| 12 | Cupid and Hugger retarget when their team changes | **Passed (automated).** The Hugger charged (Charge state) and hugged; after a swap to Blue it targeted a Pink NPC and hugged it. Cupid, swapped to Purple, targeted a Pink NPC and fired arrows. |
| 13 | Celebrity score updates after conversion | **Passed (automated).** 13 shots to recruit (a basic NPC needs 5), 25 to steal. Blue's score jumped to 3 points for 1 NPC on all 4 instances right after the steal. |
| 14 | All clients see the same leaderboard and timer | **Passed (automated).** Rankings identical on 4 instances after every scripted step and after a 40s natural NPC battle (identical per-NPC owner string); timers within 1s. |
| 15 | Four players at once | **Passed (automated).** 4 Editor instances over Relay through several full matches; results screen identical everywhere. With 4 real humans: **needs playtest**. |
| 16 | Single player still works | **Passed (automated).** Menu → Single Player → Play: simulated D + Space moved and dashed the player (20s cooldown started); the AI had 8 NPCs by 37s; no console errors. |

Other checks:
- **Spawn mix (4 players, 22 NPCs):** first run 13 Basic, 3 Cupid, 2 Drama Queen, 3 Hugger, 1 Celebrity, identical everywhere. The caps were then lowered to 2 per special type, and the rebuilt table was verified in the prefab.
- **Weapons:** a client's Bounce Gun shots ricocheted (velocity reflected, bounce count 3 → 0) on the host and on the client copies. A Shotgun blast fired 5 pellets at −18°, −9°, 0°, 9° and 18°, speed 15.
- **HUD (screenshots at 1094×602):** status panel ("PINK TEAM", "PURPLE 48%" with a split meter), weapon panel ("BOUNCE GUN 29.3s"), Dash slot, two-line team leaderboard, results ("PINK TEAM WINS!", two-line ranked rows, a shared 3rd place). The feed showed "YOU FELL FOR PINK!" on the converted client and "… FELL FOR PINK!" / "… FELL FOR YOUR TEAM!" on the others. Love bars over players, identity hearts on converted players, pickup badges and the rocket were visible in game.
- 0 compile errors and warnings; 0 console errors on all 4 instances during the final session.

### Update 7 (2026-10-30): multiplayer responsiveness + visual/UI polish
**Lag diagnosis (measured, MPPM over real Relay):** RTT 71–83 ms; host and clients ran at 160–280 FPS with 16–22 NPCs, so frame rate, NPC AI and physics were not the cause. The cause was authority: every client input went to the host and the result came back through NetworkTransform interpolation, so a client saw its own movement, dash and shots about RTT + 2–3 ticks late (~150–250 ms).
**Automated checks passed (2 players, then 4 players, real Relay):**
- Client movement: full speed (7) on the next client frame after the key press; at rest the host and every client agree on every player's position within 0.01.
- Client walls/obstacles still block the client's own movement (stopped at the outer wall; the 4 machines report identical obstacle colliders).
- Predicted shots: the projectile exists in the same frame as the click on the client. 3 shots at a neutral NPC → host progress 0.60 (3 × 20); afterwards 0 pending predictions, 0 leftover local impacts, 0 stray projectiles. With 4 players, a client's 3 shots appeared on the host and on both other clients (per-frame watcher).
- Fire-rate cheat: 20 `ShootRpc`s in one frame → the host accepted 1.
- Dash: instant on the client (speed 21), a second dash refused, host cooldown recorded (18.4s vs the client's own 20.0s countdown).
- Teleport cheat (+10 units): the host corrected the client; about 6.8 units of burst allowance still get through once, and the long-run speed stays capped at 1.3× (`speedTolerance`, `burstSeconds` 0.75).
- Conversion of a client player synced to all 4 machines.
- Remote smoothness (host's view of a client moving at 7): exact average speed, no stalls or teleports, mean deviation from a straight line 0.02–0.04 units (~1 px), worst 0.13 units (~4.5 px). Tick rate 60 measured the same, so 30 was kept (half the traffic). Interpolation modes compared: Lerp + smoothing was best; SmoothDampening was clearly worse.
- Single player: match runs, the AI recruits (7 NPCs in ~25 s), 14 animated rigs, ~375 FPS, 0 errors.
- 0 compile errors; 0 game-code console errors on all instances (the only errors came from MPPM's own scene-save hook and a Unity tutorial window).
**Visuals (screenshots):** new menu (gradient, title shadow, outlined hearts, no hint/subtitles), SP arena (tiled floor, mint candy obstacles, lavender candy walls, shaded characters with faces, glowing trails) and MP arena (special NPC accessories still visible). **Needs the user's eyes:** overall look, readability in a 4-player brawl, animation feel (hop/lean/recoil/squish strength), floor pattern contrast.

### Single player removed (2026-10-30)
**Automated checks passed:**
- No script references a removed type: compile clean, 0 errors.
- The builder rebuilt everything: it only builds sprites, multiplayer prefabs, MainMenu, MPLobby and MPArena now. Build Settings = MainMenu (0), MPLobby (1), MPArena (2), plus the old untouched `SampleScene`; entries for missing scene files are dropped.
- The menu screenshot shows the title, HOST GAME / JOIN GAME / QUIT, and no leftover single-player buttons.
- In Play mode, Host Game reached MPLobby with a room code over Relay. Leave returned to the main menu. Join Game and Back switched pages. 0 console errors.

- 2-instance match over real Relay after the removal (host + 1 MPPM clone): lobby → Playing, 2 players + 16 NPCs on both machines, a scripted conversion of the Celebrity to the client's team synced to the client, ~250 FPS. The only console exceptions are MPPM's own scene-save hook ("This cannot be used during play mode"), seen before. The first session attempt failed because Play mode stopped while the clone was restarting; the retry worked.

**Not re-checked:** a 4-player match after the removal. Multiplayer code was only touched in comments and in `SquashStretch`'s recoil hook (now called by `MPProjectile`).

## Bugs
**Fixed (Update 5)**
- The leaderboard row overflowed for 3-member teams (names wrapped into the points). Rows are now two lines: team name, then smaller member names.
- The weapon name overlapped "UNLIMITED" in the weapon panel. The name field is narrower and the time text auto-sizes.

**Fixed (Update 4)**
- Build scene list was only changed in memory, so MPPM clones and Netcode scene sync didn't know the new scenes ("Scene Hash … does not exist in the HashToBuildIndex table"). The builder now saves it to disk.
- Joining players kept the placeholder name "Player N": their name RPC arrived before the host had added them. The host now remembers early names.
- Disconnect messages showed Netcode's internal text ("[Disconnect Event][Client-1]…"); now replaced by "Lost connection to the host." unless it's one of our own messages.
- After the network stopped, the player kept trying to send input RPCs (error spam). Now guarded.
- HUD: the skill label wrapped onto two lines, and the top-left player sat under the leaderboard. Label no longer wraps; spawns moved inward to (±21, ±10); leaderboard, timer and skill bar turn see-through while your character is behind them.

**Fixed (Update 2; mostly single-player, now removed)**
- Menu difficulty list saved as null references: opening the menu scene unloaded the freshly created difficulty assets before they were assigned. The builder now reloads them after opening the scene, and `MainMenu` ignores null entries.
- Dash slid on for about 0.3s (about 3 units) after ending because movement eased down from dash speed. Speed is now capped to move speed when the dash ends.
- `ArenaCamera` misread the HUD bar's position if the HUD canvas was not Screen Space Overlay. It now converts the corners with `RectTransformUtility.WorldToScreenPoint`.
- Easy was too weak in a contested match: it converted 0 NPCs in 47s against 3 Pink allies. Retuned Easy: aim error 18° → 14°, reaction 0.6 → 0.4s, wander chance 0.12 → 0.08.

**Fixed (earlier; single-player, now removed)**
- Love meter only reached half width at ±100 (it grew from the center). It now fills left-to-right across the full bar: pink for positive, blue for negative (`AffectionVisuals`).
- Bodyguard walked straight through its own leader to reach its guard spot, shoving the player forward. Fixed in `BodyguardNPC.AroundLeader` (sidesteps when the leader is in the way). Re-verified. A small initial nudge (about 0.3 units) remains while it rounds the leader.

**Known issues / limitations (not fixed)**
- **Multiplayer not tested by humans, in a build, or between two computers.** All checks ran in one PC's Editors (still through real Relay servers). Real mouse aiming/shooting in multiplayer, the Copy button's clipboard, the countdown and HUD feel, and whether Update 5 is *fun* (conversion cost, weapon balance, chain-reaction frequency) need a playtest.
- **2-player matches end at the first player conversion** (only one team is left). This is a [Proposed] rule (`MPMatch.endWhenOneTeamLeft`), not something you asked for. Turn it off in the MPMatch prefab (or the builder) if you'd rather keep playing until the timer.
- **Readability with 4 teams + 22 NPCs is untested by eye.** Special NPCs, rockets and conversion effects add visual noise; only still screenshots were checked.
- Followers still use steering, not pathfinding. A Hugger that changed owner was seen idling near its new owner in a corner, which was correct, but long cross-map follows can snag on obstacles.
- A local player's own conversion can briefly show "+1"/"−1" popups on their NPCs in the wrong order (team-change messages arrive in either order). Cosmetic.
- Rocket explosions and Drama Queen bursts ignore walls (area effects reach behind cover). Intentional for now.
- Client projectile copies are visual: bounces are deterministic, but small timing differences can make a client copy bounce slightly differently than the host's.
- **Client latency (Update 7):** a client's own movement, dash and shots are now instant. Still delayed by about half a round trip: hits and conversions (the host decides them), other players and NPCs (shown ~100 ms in the past, as interpolation requires). There is no lag compensation, so against fast-moving targets a client may see a predicted hit burst while the host recorded a miss (the shot then simply does nothing).
- **Movement check limits (Update 7):** a modified client can teleport about 7 units once before being corrected, and can move at most 1.3× normal speed over time. A real lag spike longer than ~0.75 s may snap a legitimate player back.
- **Players walk through NPCs in multiplayer (Update 7):** needed for owner-driven movement (otherwise followers would block their own player on the client).
- **Client projectiles are visual copies:** they can look like they pass through an NPC slightly before the host reports the hit.
- MPPM clones render a tiny Game view in this layout (875×88), so the follow camera zooms way in there; screenshots from clones aren't representative.
- The Editor console shows "UnityConnectWebRequestException: Token Exchange failed" a few times per session (Editor services login). Sign-in and Relay still worked.
- **Real input only partly exercised:** keyboard movement and Space dash were driven through simulated Input System events. Mouse aim and click shooting, a real click or Enter on menu buttons, R and M on the result screen, and Quit in a built player (only the editor path stops Play mode) were not.
- **MainMenu scene shows as modified after Play mode** (`isDirty`), apparently from UI layout recalculation. Harmless, but Unity may ask to save it, and the builder refuses to run while it's dirty. Reload the scene without saving (or save it) first.
- **Overlay UI isn't captured in screenshots:** to check it visually, switch the canvas to Screen Space Camera at runtime only. The HUD and menu were checked that way at 1280×720.
- **Rebuild wipes tuning:** **LoveShot → Rebuild Arena + Menu** overwrites all three scenes and all prefabs.
- Editor test helpers (`editor set_auto_tick`, run-in-background override) are per Editor session; they reset when Unity restarts.
- Multiplayer has no host migration, late join or spectating; names are random guest names. There is no pause menu.

## How to test multiplayer
**On one PC (Multiplayer Play Mode):** Window → Multiplayer → Multiplayer Play Mode, tick Player 2 (and 3, 4), wait for the clone windows, press Play. In the main Editor: Host Game, note the code. In each clone: Online Multiplayer → Join Game, type the code, Join. Start Game from the host once 2+ are in. Click into a window before using its keyboard/mouse.

**With a friend on another computer:**
1. File → Build Profiles → Windows, Build (the three scenes are already in the list; MainMenu first). Send the whole build folder (zipped) to your friend, or have them open the project in Unity 6000.6.7f1.
2. Both need internet. Sign-in is anonymous and automatic; no accounts needed. Relay handles NAT, so no port forwarding.
3. You: Host Game → COPY, and send the code. Friend: Join Game → paste/type the code → JOIN.
4. Host presses START GAME. Check: you each control only your own character, NPC colors/bars and the leaderboard match on both screens, the timer matches, Dash shows its own 20s cooldown for each of you, stealing each other's NPCs works, the results match, Return to Lobby → Start again works, and closing the host's game sends the friend back to the menu with "The host left the game."
5. Two copies of the build on one PC also work (each gets its own guest identity).

## Next steps
1. Everything up to the single-player removal is committed (`30737d2`) and pushed to GitHub. Single player can be restored from `ce98e98` if ever wanted.
2. **Update 8 look check (user):** is the all-pink palette readable (especially Pink vs Blush, Blush vs neutral), are the symbols big enough, should the ink outline be thicker or thinner (`MakeToonSprite` outline values in `LoveShotBuilder.Art.cs`)?
3. **User playtest of Updates 5 and 7** (ideally 3–4 people over the internet): do a client's movement, dash and shooting feel as responsive as the host's? Does converting players feel good (250 love, 3s immunity)? Are the weapons balanced?
4. Balance pass from that feedback: weapon assets in `Assets/Settings/Weapons` (survive rebuilds); NPC/player numbers in `LoveShotBuilder.Multiplayer.cs`.
5. The user to confirm or redesign the **[Proposed]** items in `GAME_DESIGN.md` §10.
6. Possible later work: lag compensation for hits, name entry, more skills on the skill bar, a minimap, a pause menu, audio (see `GAME_DESIGN.md` §11).

## Lobby redesign + 2v2 (2026-10-31)
- Implemented, NOT yet compiled or playtested: `MPLobby` mode/order/`TeamOfSlot`/`SwapRpc`/`SetModeRpc`, icon-only `MPLobbyUI`, rebuilt `MPLobby.unity` team row (hand-edited YAML), `MPPlayer.InitSlot(slot, team)`, `MPStatusHUD` team label. Needs: compile check, a 2-player FFA lobby test, a 2v2 test (shared team, no friendly conversion, scores per side, early end), and a look at the layout.

## FFA lead cascade + zombie mode (2026-11-01)
- Implemented (NOT yet compiled or playtested; the Unity CLI wasn't available in that session): `MPPlayer` (`IsLead`, `IsZombie`, `ZombieMove`, `ServerOnConverted` cascade, `ServerJoinTeam`; zombie fields + obstacle mask added to `MPPlayer.prefab`), `MPLobby` (`zombieMode`, `SetZombieModeRpc`, `UsesZombies`), `MPLobbyUI` + a `ZombieButton` cloned next to the FFA button in `MPLobby.unity`.
- To verify: lead conversion converts the whole team and moves their NPCs; zombies follow and shoot; toggle works and hides in 2v2; zombie movement passes the host's movement check; button position looks right.
- Still to do: the end-screen rework (per-player recap) the user originally asked for.

## End screen rework (2026-11-01)
- Implemented, NOT yet compiled or viewed: `MPResultRow` (new, per-row icons + score), `MPHUD` (`resultRows` is now `MPResultRow[]`, new `youArrow`, title strings are serialized fields, ranks/names removed from results), `MPArena.unity` result rows rebuilt by script (old Swatch/Name objects removed; each row has an Icons strip with Original 1-2, Divider, Zombie 1-3; `You Arrow` under the results panel).
- To verify: icon strip spacing and arrow placement in the Editor (rows are 800 wide; the strip leaves 240px for the score), arrow lands on the right icon for host and client, 2v2 shows both teammates under ORIGINAL.

## HUD rows as prefabs + greyed Dash (2026-11-01)
- Implemented, NOT yet compiled or viewed: the leaderboard and result rows are now prefabs (`Assets/Prefabs/Multiplayer/MPLeaderboardRow.prefab`, `MPResultRow.prefab`). `MPHUD` instantiates `MPTeams.MaxTeams` of each into `boardRoot` / `resultRowsRoot` (the `Leaderboard` and `Ranking` objects, which now have a VerticalLayoutGroup); the per-row copies and parallel arrays were removed from the scene and from `MPHUD`. New `MPLeaderboardRow` component. The scene and prefabs were written by one-off Python (outside the project), not in the Editor.
- `Skill.IsDisabled` (virtual); `NetDashSkill` returns `MPPlayer.IsZombie`; `SkillSlotUI` greys the slot (serialized disabled colors, no cooldown text).
- To verify: both lists look like before (leaderboard rows at the old spacing, padding 14), rows appear/disappear per team, the Dash slot greys when you are a zombie and recovers otherwise.

## Weapon + health orbs (2026-11-01)
- Implemented, NOT yet compiled or viewed: `MPWeaponHUD` (icon, radial time overlay, glow, ammo text, `unlimitedSymbol`; new `Assets/Art/Generated/Infinity.png`) and `MPStatusHUD` (orb, damage wedge, symbol, shield glow, attacker icon above; old text/bar fields removed). Both panels in `MPArena.unity` were rebuilt from the `SkillSlot.prefab` pieces by one-off Python (outside the project); old Name/Team/Meter/LoveMeter/bar objects and the panel backgrounds are gone.
- To verify: Infinity.png imports as a Sprite (the meta was written by hand), orb positions (weapon at (24,160), health at (24,286), both 120 wide), the wedge fills the right way round and shows in the attacker's color, attacker icon appears/hides, glow behaviour, and that the ammo text fits the badge.

## Lobby mode arrows (2026-11-01)
- Generated `Assets/Art/Generated/LobbyCross.png` (X: two crossing double-headed arrows, for FFA) and `LobbyHorizontal.png` (double-headed horizontal arrow, for 2v2) in the LobbySwap ink style. Import metas written by hand (Sprite, single). NOT yet wired: the FFA/2v2 text button in `MPLobby.unity` / `MPLobbyUI` still uses text.

## Mode icon prefabs (2026-11-01)
- Implemented, NOT yet compiled or viewed: `Assets/Prefabs/Multiplayer/MPModeIconFreeForAll.prefab` and `MPModeIconTwoVsTwo.prefab` (new `MPModeIcon` component tints the 4 blobs from `MPTeams.ColorOf`); `MPLobbyUI` instantiates both under the mode button's `ModeIcon` object and shows the one for the current mode (the label text and its strings are gone). The ModeButton in `MPLobby.unity` is now square (120x120) with the old text label replaced by the `ModeIcon` object. Prefabs and the scene edit were written by one-off Python/YAML.
- To verify: the prefabs and new metas import cleanly, icon fits the button, FFA shows the X with four colors and 2v2 shows the horizontal arrow with two-and-two colors, toggling switches them. The blobs follow TeamStyle, which currently has greyscale colors.

## Weapon icons (2026-11-01)
- Regenerated in the inked style (by one-off Python, outside the project): `WeaponHeartGun.png` (new; a triangle trail + orb pointing to the top right; now the Default Gun's icon in `DefaultGun.asset`), `WeaponBounceIcon.png` (orb ricocheting along a wide zigzag trail; replaces the flat glyph, same guid) and `WeaponShotgunIcon.png` (a big shell and a fan of five orb pellets; replaces the flat glyph, same guid). Projectiles are plain circles on purpose. All 128px at 128 pixels-per-unit, so pickups keep their old world size. Rocket was already inked and is unchanged.
- The Dash skill icon (`DashIcon.png`) is still the old flat chevrons.

## Charged bow + stacking modifiers (2026-11-01)
- Implemented, NOT yet compiled or viewed: `MPWeapon` now holds the bow stats (asset index 0, `DefaultGun.asset`, icon new `WeaponBowIcon.png`) and modifier fields (multipliers, extra pellets/bounces, explosion, style override); `MPWeapon.Resolve` stacks them. Modifier assets: Bounce, Shotgun, Rocket and the new `Gun.asset` (charge x0.5, added to `MPMatch.weapons` and the pickup weights). `MPPlayer`: `NetworkList<MPModifier>` (index + expiry), charge on hold/release (`UpdateCharge`), `ShootRpc` now carries a charge byte and the host rechecks the elapsed time against recovery + charge time; zombies mirror the same `MPFiring`. `MPMatch.FireWeapon/PredictWeapon` take an `MPFiring`. HUD: `MPWeaponHUD` (charge ring + glow) and the new `MPModifierSlot` prefab (`MPModifierSlot.prefab`, instantiated per modifier under the former ammo badge, now `Modifiers` with a HorizontalLayoutGroup); the ammo text and infinity symbol were removed from `MPArena.unity` (one-off Python).
- To verify: compile; prefab/asset/meta imports (new `MPModifierSlot`, `Gun.asset`, bow icon); charge feel and numbers (tap spam vs full charge, shotgun+rocket stacking); host accepts legit charged shots at any ping; predicted vs host projectiles match; ring/glow/modifier row layout; zombies fire charged stacked shots; pickups with the 5-entry weights.

## Arrow + emblem projectiles (2026-11-01)
- Implemented, NOT yet compiled or viewed: new `ProjectileArrow.png` (shaft + fletching, style slot 0 in `MPLoveProjectile.prefab`), a child `Emblem` SpriteRenderer on the prefab shown by `MPProjectile.ShowEmblem` (arrow tip or rocket badge; offsets/sizes serialized), projectiles now always face their velocity. `SpawnProjectileRpc` now sends `creditSlot` (before, remote visual copies had no shooter symbol). `WeaponBowIcon.png` regenerated: bow bulges toward the target, heart at the arrow tip.
- To verify: emblem size/position on the arrow and rocket (tune the offsets), team tint contrast, the arrow's thin shaft at the 0.45 prefab scale.

## Separate NPC projectile prefab (2026-11-01)
- Implemented, NOT yet compiled or viewed: `Assets/Prefabs/Multiplayer/MPNpcProjectile.prefab` (a copy of `MPLoveProjectile`, slot 0 back to the heart; new guid in its meta) used for every shot with `fromPlayer == false` (NPCs, Cupid). `MPMatch.npcProjectilePrefab` is wired on `MPMatch.prefab`; `SpawnProjectileRpc` now sends `fromPlayer` so client copies use the matching prefab. NPC shots use style `Icon` (just the owner's symbol).

## Shift abilities (2026-11-01)
- Implemented, NOT yet compiled or viewed: `MPAbility` enum + `MPPlayerInfo.Ability` + `MPLobby.SetAbilityRpc`; `MPLobbyUI` ability button (cycles; new `AbilityButton` + `AbilityIcon` in `MPLobby.unity`, cloned from the zombie button at (360,-290)); `NetAbilitySkill` replaces `NetDashSkill` on `MPPlayer.prefab` (key LeftShift = 51, label SHIFT, names/icons per ability); `MPPlayer` ability state/RPC (`UseAbilityRpc`, heal/powerup/duplicate on the host), `MPCapture.ServerHeal/ServerSetHealthMultiplier`, `MPNpc.size/IsRegular/ServerShrink` (+ `MPNpcVisuals` scale), `MPMatch.ServerSpawnNpcLike/Pickups`, `MPPickupSpawner.ServerSpawnAt`. New inked icons: `AbilityHeal/Powerup/Duplicate.png`; `DashIcon.png` redrawn (same guid).
- To verify: compile; Key.LeftShift value (51 written by hand); lobby button layout/labels and that the choice reaches the match; each ability on host and client; duplicate keeps scores/NPC counts sane and clones follow; shrunk NPC collider is unchanged (only the body sprite shrinks); the old Space dash is gone.

- Update (same day): two ability slots. `MPPlayerInfo.AbilitySpace/AbilityShift` (`SetAbilityRpc(slot, ability)` refuses a duplicate), `MPAbilityState` NetworkVariable on `MPPlayer` (`AbilityOf/AbilityCooldown/AbilityCooldownRemaining/AbilityUsedUp(slot)`, `TryUseAbility(slot, dir)`, `UseAbilityRpc(slot)`), two `NetAbilitySkill` components on `MPPlayer.prefab` (Space then Shift, `abilitySlot` 0/1), and a second lobby button (`AbilityButton2`, y -400) with `MPLobbyUI` arrays. NOT yet compiled or viewed.

- Update (same day): the lobby ability buttons were replaced by a picker under your own icon: new `MPAbilityOrb` + `MPAbilityOrb.prefab` (icon orb, selected glow, label), `AbilityPicker` (slot row of 2 orbs + option row of 4, instantiated by `MPLobbyUI`, moved under your icon each frame); `SetAbilityRpc` swaps when the other slot already holds the pick. The two cloned `AbilityButton` objects are gone from `MPLobby.unity` (the editor had attached their pieces to the zombie label object, which I cleaned). NOT yet compiled or viewed; picker offset (0,-150 under the card) and orb sizes need a look.

- Update (same day): abilities are now `MPAbilityDef` ScriptableObjects in `Assets/Settings/Resources/Abilities` (Dash, GroupHeal, Powerup, Duplicate; `MPAbilities.Get(kind)` loads them). The numbers that were on `MPPlayer.prefab` (dash cooldown, heal radius/cooldown, powerup distance/cooldown, duplicate size/health/spread, ring radius) and the name/icon lists on `NetAbilitySkill` and `MPLobbyUI` moved into the assets; the leftover serialized values were removed from the prefab and lobby scene. NOT yet compiled or viewed.

- Update (same day): abilities are now `MPAbilityDef` ScriptableObjects in `Assets/Settings/Resources/Abilities` (Dash, GroupHeal, Powerup, Duplicate; `MPAbilities.Get(kind)` loads them). The numbers that were on `MPPlayer.prefab` (dash cooldown, heal radius/cooldown, powerup distance/cooldown, duplicate size/health/spread, ring radius) and the name/icon lists on `NetAbilitySkill` and `MPLobbyUI` moved into the assets; the leftover serialized values were removed from the prefab and lobby scene. NOT yet compiled or viewed.

## Title card (2026-11-01)
- Implemented, NOT yet compiled or viewed: `Assets/Scripts/UI/TitleCard.cs` (replaces the earlier `TitleFade`, which is gone; the `Title` object is back to how it was): a new `TitleCardCanvas` in `MainMenu.unity` (Screen Space Overlay, sorting order 100, CanvasGroup) with a plain full-screen `Backdrop` image. It shows for `holdSeconds` (1.5), fades out over `fadeSeconds` (0.8) and then deactivates; a static flag makes it play only the first time the menu opens per run of the game. Put the title art inside the canvas.

- End screen names (2026-11-01): `MPResultRow` fills a `Name` TMP under each icon (children of the 5 icon objects in `MPResultRow.prefab`, `originalNames` / `zombieNames`). NOT yet compiled or viewed; check the names fit under the icons (auto-size 10-24) and the row spacing.

- Name setter (2026-11-01): `NameInput` (cloned from the room-code field, at (0,-230) on the main page of `MainMenu.unity`) wired to `MultiplayerMenu.nameInput`; `MPConnection.SetLocalName/SavedName` (PlayerPrefs); `MPLobby.MakeUnique` adds " #N" to a duplicate name. NOT yet compiled or viewed; check the field fits under the buttons and is not overlapped by the Status line (-330).

- Lobby names (2026-11-01): a `PlayerName` TMP under each of the four icons in `MPLobby.unity`, filled by `MPLobbyUI.playerNames` (blank while the slot is empty, in the player's identity text color). NOT yet compiled or viewed; check it against the ability picker under your icon.

- Replace Fonts tool (2026-11-01): `Assets/Editor/ReplaceFontsWindow.cs`, menu **LoveShot > Replace Fonts...**: sets one TMP font asset on every TMP text and input field in all scenes/prefabs under Assets (with a count-only button). NOT yet compiled or run; commit before using it.
- Replace Font Styles tool (2026-11-01): `Assets/Editor/ReplaceFontStylesWindow.cs`, menu **LoveShot > Replace Font Styles...** (Set / Add / Remove Bold and Italic on every TMP text). Both tools now share `Assets/Editor/TmpBatch.cs`. NOT yet compiled or run; commit before using.
