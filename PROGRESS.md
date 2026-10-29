# LoveShot — Progress

Last updated: 2026-10-29. The project is **not** under version control (no git repository).

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

No phase has been playtested by the user yet.

## What exists in the project
- **Scenes** (Build Settings order):
  0. `Assets/Scenes/MainMenu.unity`: Main Camera, Global Light 2D, EventSystem (`InputSystemUIInputModule`), MenuCanvas (floating hearts, "LOVESHOT" title, subtitle, Buttons stack: PLAY / DIFFICULTY: EASY|HARD / QUIT, controls hint) and Main Menu (`MainMenu`).
  1. `Assets/Scenes/LoveShotArena.unity`: Main Camera (`CameraShake` + `ArenaCamera`, which fits the view at runtime; clear color = wall lavender), Global Light 2D, Arena (`ArenaBounds`; floor and 4 walls 2 units thick, laid out at the 36×20 minimum and resized at runtime to fill the screen, 9 point-symmetric obstacles on the `Obstacles` layer, 6), PinkLeader at (−15, 0), BlueLeader at (15, 0), NPC Spawner (area 26×16), Match Manager and HUD.
  2. `SampleScene`: untouched.
- **Prefabs** (`Assets/Prefabs`): PinkLeader (with `DashEffects` and a world-space DashCooldown ring), BlueLeader (`DashEffects` without ring or shake; `MiniBossAI.defaultDifficulty` = Easy), NPC, NPC_Cupid, NPC_Shy, NPC_Bodyguard, LoveProjectile, HeartBurst. Leaders use continuous collision detection.
- **Difficulty assets:** `Assets/Settings/Difficulty/Easy.asset` and `Hard.asset` (`AIDifficulty`). The builder creates them only if missing and never overwrites them, so tune them directly.
- **Generated sprites:** `Assets/Art/Generated` (Circle, Ring, Square, Heart, RoundedRect with a 9-slice border).
- **Scripts:** 30 runtime scripts (Update 3 added `Core/ArenaBounds`) in `Assets/Scripts` (new in Update 2: `AI/AIDifficulty`, `Core/GameSettings`, `Effects/ArenaCamera`, `Effects/DashEffects`), plus the editor builder `Assets/Editor/LoveShotBuilder.cs` (menu **LoveShot → Rebuild Arena + Menu**).
- **Packages added:** TextMeshPro Essential Resources were imported into `Assets/TextMesh Pro`. The Pipeline package (`com.unity.pipeline` 0.9.1-exp.1) was already present.
- **Last compile:** 0 errors, 0 warnings. Console: 0 errors and 0 warnings through the final Play-mode runs.
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

## Bugs
**Fixed (Update 2)**
- Menu difficulty list saved as null references: opening the menu scene unloaded the freshly created difficulty assets before they were assigned. The builder now reloads them after opening the scene, and `MainMenu` ignores null entries.
- Dash slid on for about 0.3s (about 3 units) after ending because movement eased down from dash speed. Speed is now capped to move speed when the dash ends.
- `ArenaCamera` misread the HUD bar's position if the HUD canvas was not Screen Space Overlay. It now converts the corners with `RectTransformUtility.WorldToScreenPoint`.
- Easy was too weak in a contested match: it converted 0 NPCs in 47s against 3 Pink allies. Retuned Easy: aim error 18° → 14°, reaction 0.6 → 0.4s, wander chance 0.12 → 0.08.

**Fixed (earlier)**
- Love meter only reached half width at ±100 (it grew from the center). It now fills left-to-right across the full bar: pink for positive, blue for negative (`AffectionVisuals`).
- Bodyguard walked straight through its own leader to reach its guard spot, shoving the player forward. Fixed in `BodyguardNPC.AroundLeader` (sidesteps when the leader is in the way). Re-verified. A small initial nudge (about 0.3 units) remains while it rounds the leader.

**Known issues / limitations (not fixed)**
- **Balance unknown:** Hard snowballs quickly against a passive player. Easy is slower but its allies still snowball, because difficulty only affects the mini-boss, not Blue's allies. Neither has been tested against a real player, and the single-run measurements vary a lot with random spawns.
- **Real input only partly exercised:** keyboard movement and Space dash were driven through simulated Input System events. Mouse aim and click shooting, a real click or Enter on menu buttons, R and M on the result screen, and Quit in a built player (only the editor path stops Play mode) were not.
- **MainMenu scene shows as modified after Play mode** (`isDirty`), apparently from UI layout recalculation. Harmless, but Unity may ask to save it, and the builder refuses to run while it's dirty. Reload the scene without saving (or save it) first.
- **Dash values changed for both leaders:** 3× speed, 0.2s, 2s cooldown (previously 18 u/s, 0.15s, 1s). Hard's dash-dodges therefore happen at most every 2s instead of every 1s, a small deviation from "preserve Hard".
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

## Next steps
1. **User playtest** of the full loop, starting from the main menu, on both Easy and Hard. Focus on difficulty balance, dash feel, the feel of being stunned, HUD readability, the special NPCs, and the camera fit at different Game view sizes.
2. Balance pass based on that feedback (see `GAME_DESIGN.md` §10), keeping tuning in `LoveShotBuilder.cs` or retiring the builder for hand-tuned prefabs.
3. The user to confirm or redesign the **[Proposed]** items in `GAME_DESIGN.md`, especially steal cost, the Lovestruck details, and the special NPC behaviors.
4. Consider adding version control (git with a Unity `.gitignore`, or Unity Version Control) before further changes.
5. Possible later work: Settings/Credits panels (`MainMenu` is structured for them), a Normal difficulty (new `AIDifficulty` asset added to `MainMenu.difficulties`), difficulty affecting Blue's allies, a pause menu, audio, real art, more NPC types.
