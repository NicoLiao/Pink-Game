# LoveShot — Progress

Last updated: 2026-10-30. Git checkpoints: `4869fca` (pre-multiplayer), `024c638` (Update 4, before the gameplay expansion).


How to read this file:
- **Verified (automated):** Claude checked it in the live Editor through the Unity CLI, using scripted calls, not a human on keyboard and mouse.
- **Needs playtest:** not yet checked by the user.

## Status overview
| Phase | Scope | Status |
|---|---|---|
| 1 Core conversion | Player movement and shooting, neutral NPCs, affection/conversion, allies follow leader | Implemented, automated checks passed |
| 2 AI mini-boss | Blue AI (Recruit/Steal/Reposition), ally auto-shooting, timer, scoring, HUD | Implemented, automated checks passed |
| 3 Improvements | Lovestruck leader hits, AI Evade/harass/smarter targeting, NPC movement upgrades, Cupid/Shy/Bodyguard, effects and HUD polish | Implemented, automated checks passed; one bug found and fixed |
| Update 1 (2026-10-29) | Main menu with Play, love meter fix (full bar at ±100), larger arena + zoomed-out camera | Implemented, automated checks passed |
| Update 2 (2026-10-29) | Adaptive arena camera, dash rework + effects, Easy/Hard AI difficulty, menu difficulty selector + Quit, M to return to menu | Implemented, automated checks passed; 3 issues found and fixed during testing |
| Update 3 (2026-10-29) | Arena fills the screen: walls flush with the screen edges, border pattern removed | Implemented, automated checks passed |
| Update 4 (2026-10-29) | Online Multiplayer FFA (private Relay rooms, 2–4 players, lobby, 4-team capture, networked NPCs, results/restart), skill system + HUD skill slot, 20s Dash cooldown in both modes, new menu | Implemented; automated multi-instance checks passed (4 Editor instances over real Relay); 5 issues found and fixed |
| Update 5 (2026-10-30) | Multiplayer gameplay expansion: player conversion (identity vs team, personal NPC ownership, 3s immunity), team scoring, 4 weapons + timed pickups + weapon HUD, Drama Queen / Celebrity / Cupid / Hugger, status panel, event feed, +1 popups, two-line team leaderboard | Implemented; automated multi-instance checks passed (4 Editor instances over real Relay); 2 issues found and fixed; single player re-checked |
| Update 6 (2026-10-30) | No white backgrounds behind in-game HUD panels (both modes), crosshair mouse cursor in both arenas (`AimCursor`), all projectiles 1.5x faster with unchanged reach (both modes; builder constant `ProjectileSpeedScale`, weapon assets scaled once) | Implemented, automated checks passed (prefab/asset values, SP and 2-instance MP screenshots, 0 console errors); cursor look and new shot speed need playtest |

No phase has been playtested by the user yet. Git checkpoints: `4869fca` (pre-multiplayer) and `024c638` (Update 4, made before Update 5 started). Update 5 is not committed yet.

## What exists in the project
- **Scenes** (Build Settings order):
  0. `Assets/Scenes/MainMenu.unity`: Main Camera, Global Light 2D, EventSystem (`InputSystemUIInputModule`), MenuCanvas (floating hearts, "LOVESHOT" title, four pages: Main = SINGLE PLAYER / ONLINE MULTIPLAYER / QUIT + controls hint; Single Player = PLAY / DIFFICULTY / BACK; Online = HOST GAME / JOIN GAME / BACK + status; Join = code field / JOIN / BACK + status) and Main Menu (`MainMenu` + `MultiplayerMenu`).
  1. `Assets/Scenes/LoveShotArena.unity`: Main Camera (`CameraShake` + `ArenaCamera`, which fits the view at runtime; clear color = wall lavender), Global Light 2D, Arena (`ArenaBounds`; floor and 4 walls 2 units thick, laid out at the 36×20 minimum and resized at runtime to fill the screen, 9 point-symmetric obstacles on the `Obstacles` layer, 6), PinkLeader at (−15, 0), BlueLeader at (15, 0), NPC Spawner (area 26×16), Match Manager and HUD.
  2. `Assets/Scenes/MPLobby.unity`: camera, EventSystem, LobbyCanvas (`MPLobbyUI`: title, room code + COPY, PLAYERS n/4, 4 slot rows, START GAME (host only), LEAVE, hint).
  3. `Assets/Scenes/MPArena.unity`: Main Camera (`CameraShake` + `MPFollowCamera`), Global Light 2D, Arena (fixed 56×34 interior, 4 walls, 21 obstacles mirrored on both axes, `Obstacles` layer), Match Bootstrap (`MPArenaBootstrap`, spawns the networked `MPMatch` on the host), EventSystem, HUD (`MPHUD`: timer, leaderboard, countdown, skill bar, results panel).
  4. `SampleScene`: untouched.
  Netcode scene sync only works for scenes in this list, and MPPM clones read it from disk (`ProjectSettings/EditorBuildSettings.asset`).
- **Prefabs** (`Assets/Prefabs`): SkillSlot (HUD skill slot, both modes), PinkLeader (with `DashSkill`, `DashEffects` and a world-space DashCooldown ring), BlueLeader (`DashEffects` without ring or shake; `MiniBossAI.defaultDifficulty` = Easy), NPC, NPC_Cupid, NPC_Shy, NPC_Bodyguard, LoveProjectile, HeartBurst. Leaders use continuous collision detection.
- **Multiplayer prefabs** (`Assets/Prefabs/Multiplayer`): NetworkManager (`NetworkManager` + `UnityTransport` + `MPConnection`; approval on, scene management on, tick 30, no auto player prefab), MPLobby, MPMatch (+ `MPPickupSpawner`, weapon list, NPC spawn table), MPPlayer (with `MPCapture`, love bar, identity heart, immunity shield), MPNpc, MPNpc_Cupid, MPNpc_DramaQueen, MPNpc_Hugger, MPNpc_Celebrity, MPWeaponPickup (all networked, registered in `Assets/Settings/Network/LoveShotNetworkPrefabs.asset`), MPLoveProjectile (not networked; host-simulated with client visual copies; heart/rocket/arrow styles). Shared: `Assets/Prefabs/HeartRing`, `FloatingText`. NGO also auto-generated `Assets/DefaultNetworkPrefabs.asset`; nothing references it.
- **Weapon assets (Update 5):** `Assets/Settings/Weapons/DefaultGun, BounceGun, Shotgun, RocketLauncher.asset` (`MPWeapon`). Created only if missing; tune them directly.
- **Update 5 scripts:** changed `MPCapture`, `MPPlayer`, `MPNpc`, `MPMatch`, `MPProjectile`, `MPHUD`, `MPPlayerVisuals`, `MPNpcVisuals`; new `MPWeapon` (+ `MPShot`), `MPWeaponPickup`, `MPPickupSpawner`, `MPWeaponHUD`, `MPStatusHUD`, `MPFeed`, `MPCaptureBar`, `MPNpcFlair`, `Npcs/MPDramaQueenNpc`, `Npcs/MPCupidNpc`, `Npcs/MPHuggerNpc`, and two effects (`HeartRing`, `FloatingText`). No single-player script was changed (checked with `git diff`). Builder: new generated sprites (Rocket, Arrow, Crown, WeaponBounceIcon, WeaponShotgunIcon) and the multiplayer prefabs above.
- **Networking packages (Update 4):** `com.unity.netcode.gameobjects` 2.13.3, `com.unity.services.multiplayer` 2.4.1 (Sessions + Relay; brings Authentication, Core, Transport 6.6), `com.unity.multiplayer.playmode` 3.0.0 (local multi-instance testing). The project is linked to Unity Cloud project `c476fc30-9a8b-4cac-8850-ffbab9aca8be` (org `applenico0507`); anonymous sign-in and Relay worked with no dashboard changes.
- **Difficulty assets:** `Assets/Settings/Difficulty/Easy.asset` and `Hard.asset` (`AIDifficulty`). The builder creates them only if missing and never overwrites them, so tune them directly.
- **Generated sprites:** `Assets/Art/Generated` (Circle, Ring, Square, Heart, RoundedRect with a 9-slice border, DashIcon).
- **Scripts:** 63 runtime scripts in `Assets/Scripts` (Update 5 details below). Update 4 added `Core/Skills/Skill`, `Core/Skills/DashSkill`, `UI/SkillSlotUI`, `UI/SkillBarUI` and 15 in `Multiplayer/` (`MPTeams`, `MPConnection`, `MPLobby`, `MPLobbyUI`, `MultiplayerMenu`, `MPMatch`, `MPPlayer`, `NetDashSkill`, `MPPlayerVisuals`, `MPNpc`, `MPNpcVisuals`, `MPCapture`, `MPProjectile`, `MPFollowCamera`, `MPArenaBootstrap`). Changed for single player: `PlayerController` (activates skills by key), `Mover` (dashCooldown 20, `DashDuration`), `MainMenu` (per-page default selection). The editor builder is `Assets/Editor/LoveShotBuilder.cs` + `LoveShotBuilder.Multiplayer.cs` (menu **LoveShot → Rebuild Arena + Menu**, builds all four scenes).
- **Packages added:** TextMeshPro Essential Resources were imported into `Assets/TextMesh Pro`. The Pipeline package (`com.unity.pipeline` 0.9.1-exp.1) was already present.
- **Last compile:** 0 errors, 0 warnings (Update 5). Console during the final runs: no errors from game code; Editor-side "Token Exchange failed" errors and one MPPM clone exception ("This cannot be used during play mode", raised by MPPM itself after a scene save) appeared.
- Both scenes and all prefabs were regenerated after every code change listed here, including the Bodyguard fix.

## Verified (automated, live Editor)
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

**Fixed (Update 2)**
- Menu difficulty list saved as null references: opening the menu scene unloaded the freshly created difficulty assets before they were assigned. The builder now reloads them after opening the scene, and `MainMenu` ignores null entries.
- Dash slid on for about 0.3s (about 3 units) after ending because movement eased down from dash speed. Speed is now capped to move speed when the dash ends.
- `ArenaCamera` misread the HUD bar's position if the HUD canvas was not Screen Space Overlay. It now converts the corners with `RectTransformUtility.WorldToScreenPoint`.
- Easy was too weak in a contested match: it converted 0 NPCs in 47s against 3 Pink allies. Retuned Easy: aim error 18° → 14°, reaction 0.6 → 0.4s, wander chance 0.12 → 0.08.

**Fixed (earlier)**
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
- **Input latency on clients:** movement is host-authoritative with no client-side prediction, so clients see their own movement, shots and dash after a round trip plus interpolation (~100–250 ms over Relay). The host has none, which is a small host advantage.
- **Client projectiles are visual copies:** they can look like they pass through an NPC slightly before the host reports the hit.
- Multiplayer has no Shy/Bodyguard, no host migration, no late join or spectating; names are random guest names.
- The AI mini-boss now also has the 20s dash cooldown (Hard's dash-dodges are rare).
- MPPM clones render a tiny Game view in this layout (875×88), so the follow camera zooms way in there; screenshots from clones aren't representative.
- The Editor console shows "UnityConnectWebRequestException: Token Exchange failed" a few times per session (Editor services login). Sign-in and Relay still worked.
- **Balance unknown:** Hard snowballs quickly against a passive player. Easy is slower but its allies still snowball, because difficulty only affects the mini-boss, not Blue's allies. Neither has been tested against a real player, and the single-run measurements vary a lot with random spawns.
- **Real input only partly exercised:** keyboard movement and Space dash were driven through simulated Input System events. Mouse aim and click shooting, a real click or Enter on menu buttons, R and M on the result screen, and Quit in a built player (only the editor path stops Play mode) were not.
- **MainMenu scene shows as modified after Play mode** (`isDirty`), apparently from UI layout recalculation. Harmless, but Unity may ask to save it, and the builder refuses to run while it's dirty. Reload the scene without saving (or save it) first.
- **Dash values changed for both leaders:** 3× speed, 0.2s, now a 20s cooldown (Update 4; previously 2s, originally 18 u/s, 0.15s, 1s). Hard's dash-dodges are therefore rare, a deviation from "preserve Hard" that follows the user's 20s request.
- **Overlay UI isn't captured in screenshots:** to check it visually, switch the canvas to Screen Space Camera at runtime only. The HUD and menu were checked that way at 1280×720.
- **Rebuild wipes tuning:** **LoveShot → Rebuild Arena + Menu** overwrites both scenes and all prefabs.
- **Sparser arena:** the arena roughly doubled in area, but the NPC count is still 8–12 (confirmed spec), so NPCs are more spread out. Characters also appear about 30% smaller on screen at the new zoom.
- The menu is only reachable at match end (M). There's no pause menu during a match.
- Followers use steering, not pathfinding, so they can snag on obstacles when the leader goes around them.
- Teammates whose loyalty is worn down absorb (and are healed by) friendly shots, so they can block your aim.
- The arena size lives in `LoveShotBuilder` constants (`ArenaHalfWidth/Height`, `LeaderSpawnX`). The builder derives the spawner area, `MiniBossAI.arenaHalfExtents` and `ArenaCamera.interiorHalfExtents` from them, so change the size there, not on the components. `CameraSize/OffsetY` are only the edit-mode framing.
- Restart reloads by build index, so the arena scene must stay in Build Settings.
- Editor test helpers (`editor set_auto_tick`, run-in-background override) are per Editor session; they reset when Unity restarts.
- Evade at the default 50% chance, and AI behavior over a full natural match against a human, haven't been measured.

## How to test multiplayer
**On one PC (Multiplayer Play Mode):** Window → Multiplayer → Multiplayer Play Mode, tick Player 2 (and 3, 4), wait for the clone windows, press Play. In the main Editor: Online Multiplayer → Host Game, note the code. In each clone: Online Multiplayer → Join Game, type the code, Join. Start Game from the host once 2+ are in. Click into a window before using its keyboard/mouse.

**With a friend on another computer:**
1. File → Build Profiles → Windows, Build (the four scenes are already in the list; MainMenu first). Send the whole build folder (zipped) to your friend, or have them open the project in Unity 6000.6.7f1.
2. Both need internet. Sign-in is anonymous and automatic; no accounts needed. Relay handles NAT, so no port forwarding.
3. You: Online Multiplayer → Host Game → COPY, and send the code. Friend: Join Game → paste/type the code → JOIN.
4. Host presses START GAME. Check: you each control only your own character, NPC colors/bars and the leaderboard match on both screens, the timer matches, Dash shows its own 20s cooldown for each of you, stealing each other's NPCs works, the results match, Return to Lobby → Start again works, and closing the host's game sends the friend back to the menu with "The host left the game."
5. Two copies of the build on one PC also work (each gets its own guest identity).

## Next steps
1. **User playtest of Update 5** (ideally 3–4 people over the internet, or Multiplayer Play Mode on one PC): does converting players feel good (250 love, 3s immunity), are the weapons balanced (especially the Rocket and Shotgun), are Drama Queen chains frequent and readable, is the Celebrity worth fighting over, is the screen readable with 4 teams. Also decide on the 2-player early end (see Known issues).
2. Balance pass from that feedback: weapon assets in `Assets/Settings/Weapons` (survive rebuilds); NPC/player numbers in `LoveShotBuilder.Multiplayer.cs` (`CreateMPNpcPrefabs`, `CreateMPPlayerPrefab`, `EnsureWeaponAssets` only seeds new assets).
3. The user to confirm or redesign the **[Proposed]** items in `GAME_DESIGN.md` §10 (player conversion numbers, scoring, weapon and NPC numbers), and the single-player ones (steal cost, Lovestruck, Cupid/Shy/Bodyguard).
4. Commit Update 5 to git once reviewed.
5. Single player is unchanged; testing there is still pending from earlier updates (difficulty balance, dash feel, HUD readability).
6. Possible later work: client-side prediction, name entry, more skills on the skill bar, Shy/Bodyguard online, a minimap, a pause menu, audio, real art (see `GAME_DESIGN.md` §11).
