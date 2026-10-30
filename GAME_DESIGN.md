# LoveShot — Game Design

Status labels used below:
- **[Confirmed]**: the user specified or explicitly chose it.
- **[Proposed]**: Claude designed it to fill a gap. It is implemented, but the user hasn't explicitly approved it, so it's open to change.
- **[Idea]**: not decided or not built.

Numbers are current Inspector defaults (tunable), not final balance.

## 1. Core concept
- **[Confirmed]** Cute, chaotic, pastel pink and blue top-down 2D arena shooter. Two separate modes: **Single Player** (Pink vs the Blue AI mini-boss, sections 2–9) and **Online Multiplayer** (2–4 player private-room free-for-all, section 10, added 2026-10-29 at the user's request; the original spec said "no online play"). Multiplayer must never break single player.
- **[Confirmed]** Human player = **Pink** leader. AI mini-boss = **Blue** leader, "a second player controlled by AI" that follows the same rules. Neutral NPCs = **Gray**.
- **[Confirmed]** Both leaders shoot love projectiles to convert NPCs. Converted NPCs join the shooter's team and fight for it. Either side can steal the other's NPCs by converting them again.
- **[Confirmed]** Keep the teammate-conversion mechanic. **Do not** turn this into a traditional boss fight.
- **[Confirmed]** For now, leaders cannot be eliminated. Their role is to recruit and command.
- **[Confirmed]** The AI gets no unfair advantages "for now": same movement, projectile, cooldown and conversion systems as the player.

## 2. Controls (player)
- **[Confirmed]** WASD move, mouse aim, left-click shoot, Space dash (with cooldown).
- **[Proposed]** Holding left-click auto-fires at the fire rate (`holdToFire`).
- **[Confirmed]** Dash: Space, **20-second cooldown** (both modes), in the current movement direction or else the last one, can't be used on cooldown, respects walls and obstacles. Cooldown configurable in the Inspector (`Mover.dashCooldown` in single player, `MPPlayer.dashCooldown` in multiplayer).
- **[Proposed]** Dash details (shared by both leaders, so the AI mini-boss also has the 20s cooldown): 3× move speed (21 u/s) for 0.2s, about 4 units, cooldown counted from the start of the dash. Before any input it goes toward the arena center. It ends crisply at normal speed.
- **[Confirmed]** A reusable skill system with a HUD skill slot. Dash is the only skill for now. The slot shows the Dash icon, a SPACE key badge, "DASH 14.5s" countdown, a radial cooldown overlay that drains as it recharges, and a fully lit icon with a pop when ready.
- **[Proposed]** Skills are `Skill` components on the player (`DashSkill` in single player, `NetDashSkill` in multiplayer); the skill bar shows one slot per `Skill`, so a new ability is a new `Skill` subclass with its own key.
- **[Proposed]** Dash feedback: afterimages, a small heart burst and light screen shake on the player's dash, plus a radial cooldown ring under the player that fills while recharging and pops in team color when ready. The AI's dash shows afterimages and the burst, but no shake or ring.
- **[Proposed]** After the match ends: R plays again, M returns to the main menu.

## 2b. Main menu
- **[Confirmed]** The game opens on a main menu with a **Play** button that starts the match. Cute pink arcade look. Keep it simple, but structured so Settings, Credits and other options can be added later.
- **[Proposed]** Layout: big bobbing "LoveShot" title, the subtitle "Win their hearts before the mini-boss does!", a rounded pink PLAY button (grows on hover or selection, squishes when pressed), floating pink/blue/white hearts, and a controls hint at the bottom. PLAY is pre-selected, so Enter works too.
- **[Confirmed]** Difficulty selection (Easy / Hard) and a Quit button on the menu.
- **[Confirmed]** Main page: **SINGLE PLAYER**, **ONLINE MULTIPLAYER**, **QUIT**. Online Multiplayer opens **HOST GAME**, **JOIN GAME**, **BACK**.
- **[Proposed]** Single Player page: PLAY, DIFFICULTY: EASY|HARD (a click cycles it, tinted per difficulty), BACK. Easy is the default on launch, and the choice is remembered when returning from a match in the same session. To add a difficulty, create an `AIDifficulty` asset and append it to `MainMenu.difficulties`.
- **[Proposed]** Join page: a room-code field (letters/digits, case-insensitive), JOIN, BACK, and a status line for progress and errors.
- **[Idea]** Settings and Credits panels; a pause menu during the match.

## 3. Affection and conversion
- **[Confirmed]** Every NPC has affection from −100 to +100. Pink hits raise it and Blue hits lower it. +100 = Pink, −100 = Blue.
- **[Confirmed]** Hitting an enemy-owned NPC gradually wears down its loyalty before converting it. NPCs can switch teams repeatedly and never get stuck at a threshold.
- **[Confirmed]** Appearance and behavior update immediately on a team change.
- **[Proposed] Hysteresis rule:** an owned NPC keeps its team until affection reaches the *opposite* extreme. So recruiting a neutral NPC takes 100 points (5 leader hits), while stealing takes 200 (10 leader hits). An Inspector option, `becomeNeutralAtZero` (off), makes stolen NPCs turn neutral at 0 instead. **Not decided:** whether stealing should be cheaper.
- **[Proposed]** Shots pass through teammates that are already fully loyal (so allies don't block your aim), but they hit and heal teammates whose loyalty is worn down.
- **[Proposed]** Leader hit = +/−20 affection. Ally hit = +/−10.
- **[Confirmed] Love meter display:** the bar shows the affection value, not the owner. At +100 the whole bar is pink, at −100 the whole bar is blue, and at 0 it's empty. In between, it fills proportionally (left to right) in pink for positive values and blue for negative ones. Because of the hysteresis rule, a Pink-owned NPC worn down past 0 shows a blue bar while still being Pink (its body color shows the owner).

## 4. Leader hits ("Lovestruck")
- **[Confirmed]** (user chose the "brief slow" option) A leader hit by an enemy heart is lovestruck: it moves slower and can't shoot for about 0.6s. Same rule for both sides. It never eliminates anyone.
- **[Proposed]** Values: 35% speed, shooting blocked, then 1s immunity so a leader can't be stun-locked.
- **[Proposed]** Only **leader** shots stun leaders. Ally shots pass through leaders, so a big army can't spam-stun the player.
- **[Proposed]** Dashing is allowed while stunned (a skillful escape).

## 5. NPC behavior
- **[Confirmed]** Neutral: wander, change direction occasionally, avoid obstacles.
- **[Confirmed]** Owned: follow their leader, keep spacing from teammates, auto-shoot opposing NPCs, help recruit neutrals, and switch behavior immediately if stolen.
- **[Proposed]** Allies prefer enemy-owned targets over neutral ones (distance × 0.7), need line of sight, and have a 6-unit range. Followers aim slightly behind a moving leader, speed up ×1.35 when more than 6 units away, and sidestep randomly if stuck.

### Special NPC types
- **[Confirmed]** The design must support special types. The user named **Cupid, Shy, and Bodyguard**.
- **[Proposed]** What each one does (user hasn't reviewed it yet):
  - **Cupid** (gold wings): wanders faster (3.2), and once recruited fires a 3-heart fan (24°) at 6 affection per heart, every 1.1s.
  - **Shy** (small, blushing): flees nearby leaders (3.5 radius, ×1.6 speed) while neutral, takes 0.75× love, and hangs back once recruited (follow distance 3, attack range 4.5).
  - **Bodyguard** (big, sunglasses): takes 0.5× love and is slow when neutral. Once recruited it stands between its leader and the enemy leader (1.5 units out), soaking up shots that would stun its leader. It fires every 1.4s.
- **[Proposed]** Spawn weights: Basic 5, Cupid 1.5, Shy 2, Bodyguard 1.5.

## 6. Mini-boss AI (Blue)
- **[Confirmed]** Moves independently, recruits neutrals, steals Pink NPCs, occasionally dodges, keeps a sensible distance, avoids getting stuck on walls, and switches targets by proximity and ownership. Simple state machine: Recruit, Steal, Evade, Reposition.
- **[Proposed] Target scoring** (lower is better): distance; ×1.2 for enemy-owned NPCs (×0.84 while the AI is losing); −4 × how far the NPC already leans Blue; −3 × how far a neutral leans Pink ("deny" targets the player is about to convert); +4 if a wall is in the way. It only switches target when the new one is better by more than 2.
- **[Proposed] Positioning:** stays 3–6 units from its target and strafes, flipping direction every 1–2.5s or when blocked. It repositions to a random open spot when it has no target or has been stuck for 0.6s.
- **[Proposed] Shooting:** range 9, leads moving targets, ±6° random aim error.
- **[Proposed] Evade:** each incoming player shot that will pass within 0.9 units in the next 0.6s gets one 50% roll to dodge. It sidesteps for 0.35s, and dashes if impact is under 0.25s away and dash is ready. It doesn't dodge while immune.
- **[Proposed] Harass:** each decision (every 0.3s) there's a 35% chance it shoots the player instead, if the player is within 5 units, visible, and not immune.
- The numbers above are the **Hard** profile, which is identical to the original AI.

## 6b. AI difficulty
- **[Confirmed]** Easy and Hard difficulties. Easy still plays the full game: it moves, recruits and steals, and it is never invincible, inactive or unable to recruit. The love meter and stealing rules are the same on both. It should be easy to add more levels (e.g. Normal).
- **[Proposed]** Each difficulty is an `AIDifficulty` asset in `Assets/Settings/Difficulty/`. Easy differs from Hard only in the quality of the AI's decisions:
  - slower decisions (0.8s) and target reactions (0.4s)
  - occasional wandering (8% per decision) and wobbly movement
  - no leading of moving targets, ±14° aim error, range 8, fire cooldown ×1.4
  - less harassment (10%, within 4 units)
  - rarer dodges (15%), and it never dashes to dodge
  - no extra preference for stealing or for nearly-converted NPCs
- **[Proposed]** Difficulty affects only the mini-boss, not Blue's converted NPCs.

## 7. Arena and match rules
- **[Confirmed]** Small top-down arena with walls and simple obstacles. 8–12 neutral NPCs. Player and mini-boss spawn on opposite sides.
- **[Confirmed]** 90-second match, Pink vs Blue counter and timer on screen. The team with more NPCs at the end wins; a tie is a draw.
- **[Confirmed]** A larger arena with a zoomed-out camera (more room to move, more visible at once), with characters still clearly visible and unable to leave the arena.
- **[Proposed]** Arena interior is 36×20 units (previously 26×14), with walls 2 units thick and 9 point-symmetric obstacles (center block plus mirrored pairs of bars, pillars, blocks and ledges). Leaders spawn at x = ±15. NPCs spawn in a 26×16 area.
- **[Confirmed]** The whole arena stays visible at any screen size or aspect ratio, and the HUD doesn't hide any of it.
- **[Confirmed]** The playable field reaches the screen edges: no margin, border padding or decoration outside the arena. The HUD stays at the top and the field fills the rest.
- **[Proposed]** 36×20 is the *minimum* interior. The camera fits that below the HUD bar, then the floor and outer walls grow on the looser axis so the walls sit at the screen edges, with a 0.4-unit wall line visible along each edge. On 16:9 the interior is about 41×20; on the user's 2.13:1 Game view it's about 49×20; on 4:3 it's about 36×24. Obstacles, leader spawns (x = ±15) and the obstacle layout stay fixed; the NPC spawn area and the AI's reposition area grow with the field.
- **[Idea]** Raise the NPC count for the bigger map. The 8–12 count is confirmed spec, so it's unchanged for now.
- **[Proposed]** A 3-2-1-GO countdown before the timer starts. The arena freezes when time runs out, and a result banner shows the score.

## 8. Visual style
- **[Confirmed]** Cute pastel pink and blue arcade look, gray neutrals, simple placeholder characters, heart projectiles, small effects on team switches, a clear friendly/enemy/neutral distinction. Readability and gameplay come before polished art.
- **[Proposed]** Lavender walls (so they never read as Pink team). An affection bar above each NPC (see the love meter rule in §3). Neutral NPCs tint toward whoever is winning them. Effects: white hit flash, pop and heart burst on conversion, flickering stunned leaders, a leader outline that dims while dash recharges, light camera shake.

## 9. Technical requirements
- **[Confirmed]** Unity 2D + C#, modular components, gameplay systems shared between player and AI, Inspector-adjustable values (speed, cooldowns, projectile strength, AI behavior), simple understandable code, kept at game-jam scope.

## 10. Online Multiplayer (free-for-all)
**Rooms and lobby**
- **[Confirmed]** Private rooms only (no public matchmaking), 2–4 human players, no AI mini-bosses. Unity Multiplayer Services sessions over Relay, Netcode for GameObjects, host-authoritative.
- **[Confirmed]** Host Game creates a room with a join code (shown with a Copy button). Join Game takes a code and shows errors for invalid codes, full rooms and failed connections. The lobby shows the code, player count (e.g. 2/4), names and colors; Start Game is host-only and needs 2+ players; anyone can Leave. Only the host can start a match.
- **[Confirmed]** If the host disconnects, everyone else gets a clear message and returns to the multiplayer menu. No host migration.
- **[Proposed]** Temporary guest names ("Guest1234"). Slot = team = color in join order: Pink, Blue, Purple, Yellow; a freed slot is reused. Joining is refused while a match is running (the room is locked).

**Gameplay**
- **[Confirmed]** Each player controls only their own character with the same controls and rules: move, aim, shoot, Dash, recruit neutrals, steal from anyone. Recruited NPCs follow their player, keep spacing, and shoot neutral NPCs and other teams' NPCs.
- **[Confirmed]** 180-second match; live leaderboard of team sizes; most NPCs wins; ties allowed. Results show the winner (or "IT'S A TIE!"), final team sizes and ranks (tied players share a rank), Return to Lobby (host only; others see "Waiting for the host...") and Main Menu.
- **[Proposed]** 4-way conversion (`MPCapture`): each NPC has an owner (or Neutral), the team currently capturing it, and progress 0–100%. A hit by team T: if T owns it, T's shots wear down whoever is capturing it (a fully loyal NPC ignores its own team's shots); if nobody or T is capturing, T's progress grows and at 100% the NPC joins T; if another team is capturing, T first wears that progress to 0 and the overflow becomes T's progress. Recruiting a neutral takes 100 shot power, stealing an owned NPC takes 200 (player shots 20, NPC shots 10, as in single player).
- **[Proposed]** Capture bar: the owner's remaining loyalty fills from the left in the owner's color; the capturer's progress fills from the right in its color. Neutral NPCs tint toward their capturer.
- **[Proposed]** A player hit by another player's shot is briefly slowed and can't shoot (0.6s, then 1s immune), like single player's Lovestruck.
- **[Proposed]** NPCs: 10 + 3 per player (16/19/22), only the basic type for now; `MPNpc` has the same virtual hooks as the single-player NPC for Cupid/Shy/Bodyguard later.
- **[Proposed]** A player who leaves mid-match loses their team: their NPCs turn neutral. The match ends early if fewer than 2 players remain.
- **[Proposed]** 3-2-1-GO countdown after everyone has loaded the arena (or after 10s).

**Arena and camera**
- **[Confirmed]** Larger arena for 4 players in the same style; each client has its own local camera; no empty space outside the map is visible.
- **[Proposed]** Fixed 56×34 interior (it can't resize per screen like single player, because every client must see the same walls), mirrored on both axes, players spawn at (±21, ±10). The camera follows your own player (orthographic size 12.5) and is clamped so it never shows beyond the walls; on very wide or tall screens it zooms in until it fits. HUD panels turn see-through while your character is behind them.

## 11. Open questions and future ideas
- **[Idea]** Balance: against a passive player, the AI plus its allies snowballs to all 12 NPCs in about 22s. Needs real playtesting before choosing fixes (aim spread, ally fire rate, steal cost, harass chance).
- **[Idea]** Make stealing cheaper (`becomeNeutralAtZero`, or a different hysteresis).
- **[Idea]** Leader elimination or knockouts ("for now" they can't be eliminated).
- **[Idea]** A Normal difficulty, or difficulty that also affects Blue's allies. Deliberate AI advantages remain excluded ("no unfair advantages *for now*").
- **[Idea]** More NPC types; the user to confirm or redesign Cupid, Shy and Bodyguard behavior.
- **[Idea]** Not started: audio, pause, real art.
- **[Idea]** Multiplayer follow-ups: client-side prediction for your own movement (clients currently feel their round-trip latency), player-chosen names, Cupid/Shy/Bodyguard in multiplayer, spectating or late join, host migration, public matchmaking, a minimap for the larger arena.
