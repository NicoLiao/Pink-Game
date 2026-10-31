# LoveShot — Game Design

Status labels used below:
- **[Confirmed]**: the user specified or explicitly chose it.
- **[Proposed]**: Claude designed it to fill a gap. It is implemented, but the user hasn't explicitly approved it, so it's open to change.
- **[Idea]**: not decided or not built.

Numbers are current Inspector defaults (tunable), not final balance.

## 1. Core concept
- **[Confirmed]** Cute, chaotic, pink top-down 2D arena shooter, **online multiplayer only**: 2–4 players in a private-room free-for-all (section 10). Players shoot love projectiles to convert neutral NPCs, steal each other's, and can convert each other. Converted NPCs join the shooter's team and fight for it.
- **[Confirmed]** (2026-10-30) The single-player mode (Pink vs the Blue AI mini-boss, with Easy/Hard difficulty and the Shy/Bodyguard NPCs) was **removed completely** at the user's request. Its design and code are in git commit `ce98e98` if it's ever wanted back. Former sections 3–7 (single-player affection, lovestruck, NPC, mini-boss AI, difficulty and arena rules) were dropped from this document (the remaining section numbers are kept so references stay valid); the multiplayer rules are in section 10.
- **[Confirmed]** Keep the conversion mechanic. **Do not** turn this into a traditional boss fight. Players are never eliminated, only converted.

## 2. Controls
- **[Confirmed]** WASD move, mouse aim, left-click shoot, Space dash (with cooldown).
- **[Proposed]** Holding left-click auto-fires at the weapon's fire rate.
- **[Confirmed]** Dash: Space, **20-second cooldown**, in the current movement direction or else the last one, can't be used on cooldown, respects walls and obstacles. Cooldown configurable in the Inspector (`MPPlayer.dashCooldown`).
- **[Proposed]** Dash details: 3× move speed for 0.2s, about 4 units, cooldown counted from the start of the dash. It ends crisply at normal speed.
- **[Confirmed]** A reusable skill system with a HUD skill slot. Dash is the only skill for now. The slot shows the Dash icon, a SPACE key badge, a countdown, a radial cooldown overlay that drains as it recharges, and a fully lit icon with a pop when ready.
- **[Proposed]** Skills are `Skill` components on the player (`NetDashSkill`); the skill bar shows one slot per `Skill`, so a new ability is a new `Skill` subclass with its own key.

## 2b. Main menu
- **[Confirmed]** The game opens on a main menu in the cute pink look, kept simple but structured so Settings, Credits and other options can be added later. Since 2026-10-30 (single player removed): **HOST GAME**, **JOIN GAME**, **QUIT**, plus a status line for progress and errors.
- **[Proposed]** Big bobbing "LoveShot" title, rounded buttons (grow on hover or selection, squish when pressed), floating hearts. HOST GAME is pre-selected, so Enter works too.
- **[Proposed]** Join page: a room-code field (letters/digits, case-insensitive), JOIN, BACK, and a status line.
- **[Idea]** Settings and Credits panels; a pause menu during the match.

## 8. Visual style
- **[Confirmed]** Cute arcade look, simple characters, heart projectiles, small effects on team switches, a clear friendly/enemy/neutral distinction. Readability and gameplay come before polished art.
- **[Proposed]** A love meter above each character (rules in section 10). Effects: white hit flash, pop and heart burst on conversion, flickering stunned players, light camera shake.
- **[Confirmed]** (Update 6) No backgrounds behind in-game HUD panels (timer, leaderboard, status, weapon, skill slots), so the arena stays visible; the results banner and lobby cards keep theirs. A custom crosshair mouse cursor (pink with a dark outline).
- **[Confirmed]** (Update 6) All projectiles fly 1.5x faster (players, NPCs, every weapon). **[Proposed]** Their flight times were divided by 1.5, so each shot's reach is unchanged.
- **[Confirmed]** (Update 7) Cute, colorful, polished arcade look, readable in chaos. Menus, lobby and HUD without tutorial text, hints or placeholders; love meters, weapon timers, Dash cooldown, leaderboard and timer stay.
- **[Proposed]** (Update 7) Characters with a drop shadow and a highlight; NPCs with eyes, catchlights, a smile and blush; candy-block walls and obstacles with a darker front lip. Projectiles glow and leave a short trail in their team color.
- **[Proposed]** (Update 7) Animation: characters hop with squash and stretch and lean while moving, breathe when idle, recoil when they shoot and squish when hit; team color changes blend over a few frames instead of snapping.
- **[Confirmed]** (Update 8) Everything is pinkish, in the style of the two reference images (hand-drawn cartoon: thick dark outlines, flat cel shading, muted dusty colors, paper texture, soft ground shadows, white sticker border around the heroes). Teams are different shades of pink and are also told apart by shape.
- **[Proposed]** (Update 8) Teams: Pink (hot pink, heart), Berry (deep magenta, moon), Coral (salmon pink, flower), Blush (pale pink, diamond). The symbol is the player's emblem (their own, kept after a conversion), the shape of heart-style shots, a pin on owned NPCs and the HUD/lobby swatch. Neutrals are dusty mauve-gray. Arena: pale pink paper floor with faint hearts, dusty-rose obstacles, plum-rose walls; accents in rose gold and pale pink. Players have a white sticker border so they stand out from NPCs.

## 9. Technical requirements
- **[Confirmed]** Unity 2D + C#, modular components, Inspector-adjustable values (speed, cooldowns, projectile strength, NPC behavior), simple understandable code, kept at game-jam scope.

## 10. Online Multiplayer (free-for-all)
**Rooms and lobby**
- **[Confirmed]** Private rooms only (no public matchmaking), 2–4 human players, no AI mini-bosses. Unity Multiplayer Services sessions over Relay, Netcode for GameObjects, host-authoritative.
- **[Confirmed]** Host Game creates a room with a join code (shown with a Copy button). Join Game takes a code and shows errors for invalid codes, full rooms and failed connections. The lobby shows the code, player count (e.g. 2/4), names and colors; Start Game is host-only and needs 2+ players; anyone can Leave. Only the host can start a match.
- **[Confirmed]** If the host disconnects, everyone else gets a clear message and returns to the multiplayer menu. No host migration.
- **[Proposed]** Temporary guest names ("Guest1234"). Slot = team = color in join order: Pink, Berry, Coral, Blush (renamed from Pink, Blue, Purple, Yellow in Update 8); a freed slot is reused. Joining is refused while a match is running (the room is locked).

**Gameplay**
- **[Proposed]** (Update 7) Responsiveness: each player's own movement, Dash and shots react instantly on their own screen (owner-driven movement, predicted shots); the host still decides every hit, conversion, weapon, score and timer, enforces cooldowns and rejects impossible movement. As a side effect, players walk through NPCs in multiplayer (they still collide with walls, obstacles and each other).
- **[Confirmed]** Each player controls only their own character with the same controls and rules: move, aim, shoot, Dash, recruit neutrals, steal from anyone. Recruited NPCs follow their player, keep spacing, and shoot neutral NPCs and other teams' NPCs.
- **[Confirmed]** 180-second match. Results show the winner (or "IT'S A TIE!"), final scores and ranks (tied teams share a rank), Return to Lobby (host only; others see "Waiting for the host...") and Main Menu.
- **[Proposed]** 4-way conversion (`MPCapture`): each character has an owner team (or Neutral), the team currently capturing it, and progress 0–100%. A hit by team T: if T owns it, T's shots wear down whoever is capturing it (a fully loyal NPC ignores its own team's shots); if nobody or T is capturing, T's progress grows and at 100% it joins T; if another team is capturing, T first wears that progress to 0 and the overflow becomes T's progress. Recruiting a neutral NPC takes 100 love, stealing an owned NPC takes 200 (default player shots 20, NPC shots 10). This is also the rule when several teams shoot the same player.
- **[Proposed]** Capture bar: the owner's remaining loyalty fills from the left in the owner's color; the capturer's progress fills from the right in its color. Neutral NPCs tint toward their capturer.
- **[Proposed]** A player hit by another player's shot is briefly slowed and can't shoot (0.6s, then 1s immune). NPC shots don't stun.
- **[Proposed]** A player who leaves mid-match: the NPCs they personally own turn neutral.
- **[Proposed]** 3-2-1-GO countdown after everyone has loaded the arena (or after 10s).

**Player conversion (Update 5)**
- **[Confirmed]** Players can be converted. Each player has a love meter shown above their character (and in the HUD); enemy love fills it, and when it's full the player joins the attacking team. They keep full control (move, Dash, shoot, recruit), but now play for the new team: their body and shots take the new team's color.
- **[Confirmed]** Four separate ideas: **player identity** (permanent lobby slot, name, identity color), **current team** (can change), an NPC's **personal owner** (the player credited with it) and an NPC's **current team**. A converted player's personally owned NPCs change team with them; NPCs they recruit later are still personally theirs but count for the new team. Stealing an NPC directly makes the capturing player its personal owner. A converted player simply joins the attacker's *current* team; no parent/child chains, and teammates of the converted player are not converted with them.
- **[Confirmed]** 3 seconds of conversion immunity after being converted, with a visible pulsing shield (enemy shots pass through). No self-conversion or friendly fire: a player's own team's shots always pass through them. After immunity, any other team can steal the player.
- **[Proposed]** A player takes 250 love to convert (2.5× a neutral NPC: 13 default shots). Progress fades after 3s without hits (30 love/s), so stray hits don't add up forever.
- **[Proposed]** Hits count for the shooter's team *at impact* (a shot in flight when its shooter is converted counts for the new team). Every hit is resolved once on the host (`MPMatch.ApplyLove`); a converted player's immunity starts in the same frame, so simultaneous hits can't convert twice.
- **[Proposed]** Team scoring: a team's score is the sum of its NPCs' points (Normal 1, Celebrity 3), shared by all its players. The leaderboard lists teams that still have players: team color + name, its players underneath, points. Highest points wins; ties share a rank.
- **[Proposed]** The match ends early once every remaining player is on the same team (`MPMatch.endWhenOneTeamLeft`, on). With 2 players, the first player conversion therefore wins the match.
- **[Proposed]** Visual identity: the body shows the current team's color; a small heart in the middle keeps the player's own identity color, so a converted player is still recognizable. Conversion plays a big two-color heart burst, a color blend and a pop.

**Weapons (Update 5)**
- **[Confirmed]** Default Gun (unlimited, everyone's starting weapon), Bounce Gun, Shotgun, Rocket Launcher. Temporary weapons spawn as pickups at random free spots (never in walls/obstacles or on players), are collected by walking over them (one player only), last 30s, then you're back to the Default Gun; another pickup replaces the current one and restarts the 30s. Uncollected pickups disappear after a while. Weapon HUD: icon, name, seconds left and a draining bar.
- **[Proposed]** Numbers (assets in `Assets/Settings/Weapons`, tunable):
  - Default Gun ("Love Gun"): 20 love, 0.25s, speed 18, 1.33s flight.
  - Bounce Gun: 16 love, 0.3s, speed 19.5, 1.73s flight, up to 3 ricochets off walls and obstacles.
  - Shotgun: 5 pellets of 9 love in a 36° cone, 0.8s, short range (0.24s at speed 22.5, about 5 units). A point-blank blast is 45 love, 18% of a player.
  - Rocket Launcher: 1.3s, slow heart rocket (speed 12, 1.07s), explodes on impact with anything it can affect, on walls or at the end of its flight; radius 3, 70 love at the center down to 35% at the edge. Each character is hit once per explosion. Teammates and immune players don't trigger it.
- **[Proposed]** Pickups: first after 5s, then every 9s, at most 3 at once, each lasts 20s (blinks in the last 3s). Weights: Bounce 1, Shotgun 1, Rocket 0.7.

**Special NPCs (Update 5)**
- **[Confirmed]** Normal NPCs stay the majority; special types are rarer, the Celebrity rarest. Spawn weights and limits are Inspector values on `MPMatch` (`npcTypes`).
- **[Confirmed] Drama Queen:** whenever she joins a team she bursts a ring of hearts in the new team's color: nearby NPCs get capture progress toward that team, opposing players a smaller amount. Bursts can convert other NPCs, including other Queens (chain reactions). Each Queen has a cooldown.
- **[Proposed]** Drama Queen numbers: radius 4.5, 40 love to NPCs, 15 to players, burst 0.35s after joining (so chains ripple visibly), 4s cooldown per Queen. Chains always end: each Queen bursts at most once per cooldown, and bursts only push toward one team. Look: a pink bow and an aura that glows while her burst is ready.
- **[Confirmed] Celebrity:** counts 3 points, 2–3× harder to capture, wanders independently, follows its owner once recruited, score updates immediately when stolen. Crown and glowing outline.
- **[Proposed]** Celebrity numbers: love sensitivity 0.4 (recruit 250, steal 500: 13 / 25 default shots). Exactly one per match.
- **[Confirmed] Cupid:** ranged support; follows its owner at a distance and shoots love arrows at neutral and enemy NPCs, occasionally at opposing players; never at teammates; retargets when its team changes.
- **[Proposed]** Cupid numbers: range 8, arrow speed 22.5, 12 love, 1.5s cooldown, follow distance 3.2; players count as 1.8× farther away when it picks targets. Look: flapping gold wings.
- **[Confirmed] Hugger:** melee support that never shoots. It hunts nearby opposing NPCs (players at reduced effect), charges when close and gives a big hug of love on contact, with a cooldown between hugs. Distinct waddle and charge; retargets when its team changes.
- **[Proposed]** Hugger numbers: seeks within 7, charges from 2.6 (a 0.35s dash at 2.6× speed), 45 love per hug (×0.45 on players), 2.5s between hugs; players count as 1.6× farther away. Look: white arms that open when it charges, squash-and-stretch waddle.
- **[Proposed]** Counts: 10 + 3 per player (16/19/22). Weights Normal 6, Cupid 1.2 (max 2), Drama Queen 1 (max 2), Hugger 1.2 (max 2), Celebrity exactly 1.

**HUD and feedback (Update 5)**
- **[Proposed]** Bottom-left stack: your status (name, current team with its color, your love meter split between your loyalty and the capturer's progress, "LOYAL" / "BERRY 40%" / "SAFE 2.1s"), your weapon, your Dash slot. Top-left: team leaderboard. Top-center: timer. Under it, a feed for big moments ("YOU FELL FOR BERRY!", "Guest12 FELL FOR YOUR TEAM!", "PINK WON THE CELEBRITY! (+3)", "DRAMA!"). NPCs joining or leaving your team pop a floating "+1" / "−1" ("+3" for the Celebrity).

**Arena and camera**
- **[Confirmed]** Larger arena for 4 players in the same style; each client has its own local camera; no empty space outside the map is visible.
- **[Proposed]** Fixed 56×34 interior (the same for every client, so it doesn't resize per screen), mirrored on both axes, players spawn at (±21, ±10). The camera follows your own player (orthographic size 12.5) and is clamped so it never shows beyond the walls; on very wide or tall screens it zooms in until it fits. HUD panels turn see-through while your character is behind them.

## 11. Open questions and future ideas
- **[Idea]** Not started: audio, pause, real art.
- **[Idea]** Multiplayer follow-ups: player-chosen names, more special NPC types, spectating or late join, host migration, public matchmaking, a minimap for the larger arena, more player skills (the skill bar supports them).
- **[Idea]** Multiplayer balance after playtests: player conversion cost (250), whether 2-player matches should really end at the first conversion, weapon numbers, special NPC caps, Drama Queen burst strength.

## Lobby and 2v2 (2026-10-31)
- **[Confirmed]** The lobby shows only icons (no names): a row of the 4 team symbols, washed out while nobody holds that slot, with an arrow over your own. Separator lines split teams: 3 in FFA, 1 (middle) in 2v2. The host has an FFA/2v2 button and swap arrows between neighbouring icons (empty icons can be swapped too). Start needs valid teams: FFA 2+ players; 2v2 at least one player on each side.
- **[Confirmed]** 2v2: the first two icons play the last two. Teammates share one team (Pink side vs Berry side): shared NPCs, score and colour, no conversion between teammates; each keeps their own symbol as identity. A player can still be converted to the other side.
