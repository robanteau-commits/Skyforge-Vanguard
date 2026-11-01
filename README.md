# Skyforge Vanguard

A low-poly 3D vertical shoot-'em-up for PC, web and mobile, built in Unity 6 (URP). Fly the
Vanguard, fit a weapon to each wing and the core, and fight off the Veil Armada.

- **Design:** [docs/DESIGN.md](docs/DESIGN.md) (approved)
- **Models:** made in Blender, see [docs/BLENDER.md](docs/BLENDER.md)
- **Engine:** Unity 6000.6.4f1, Universal Render Pipeline, Input System
- **Status:** milestone M5, sound, settings and builds for Windows, web and Android (v0.7.0)
- **Building:** [docs/BUILDING.md](docs/BUILDING.md) (Windows installer as `.exe` or `.msi`, web, Android)

## Opening the project
1. Get the latest files: in Command Prompt, `cd /d D:\Games\SkyforgeVanguard` then `git pull`.
2. Unity Hub → **Projects** → **Add** → **Add project from disk** → choose
   `D:\Games\SkyforgeVanguard`. The first open takes a few minutes while Unity builds its cache.
3. Open `Assets/Scenes/Main.unity` if it isn't already open, then press **Play**.

## What's in M5 — sound, settings and builds
- **Sound:** every effect and the music are synthesised in code as the game starts: a sound for
  each wing weapon and core, hits, explosions (bigger for big ships, a long one for the boss),
  shield and hull hits, credits (chiming higher with a bigger combo), repairs, boss warnings and
  lasers, menu clicks, purchases, medals, and clear / fail jingles. Synthwave music: a calm track
  for the menus, one per stage and a boss track.
- **Title screen** (Play, Settings, Credits, Quit) and a **pause menu** (Resume, Settings, Restart,
  Hangar, World map).
- **Settings:** music and effects volume, screen shake (off / low / full), screen flashes on/off,
  high-contrast enemy bullets, graphics quality, touch sensitivity, show FPS, and fullscreen /
  resolution on PC. Saved separately from progress.
- **Touch:** a pause button and a core button (showing its charge) during play; any extra finger
  also fires the core. The stage pauses when the app or browser tab loses focus.
- **Gamepad:** A / Start move forward through the menus, B goes back.
- **Builds:** Skyforge → Build menu in Unity for Windows, web and Android, plus the Windows
  installer. Step by step in [docs/BUILDING.md](docs/BUILDING.md).

## What's in M4 — all of World 1
- **Stages 1-2 to 1-5:** Harbor Line (Bastion turrets on drifting asteroids), Stormwall (Spine
  Mine fields and Ram Drones), Foundry Ridge (Wardens, and the **Brood Carrier** mini-boss whose
  two launch bays release Gnats until you shoot them off), Upper Air (everything, two Carriers).
- **1-B The Tidebreaker:** a three-phase boss. Phase 1: two flank cannons sweep streams and a
  missile bay launches homing missiles (shoot them down); the core eye is armoured. Phase 2 (both
  cannons down): the eye opens and fires spirals while Gnat waves launch. Phase 3 (eye below 40%):
  it lunges forward with ring barrages (find the gap) and sweeping lasers (red warning line first).
  A boss health bar shows along the top.
- **Veteran mode** unlocks when you beat the Tidebreaker: switch it on the map. Faster, denser fire,
  ×1.5 damage, ×1.3 enemy HP; ×1.3 credits, ×1.5 score, and its own medals.
- Fix: credits still flying in when a stage is cleared now count (they used to be lost).

## What's in M3 — the full loop
- **World map** (the game starts here): World 1's six stages along a path over the turning Earth,
  each showing its three medals. Clear a stage to unlock the next; 1-2 to the boss arrive in M4.
- **Shop in the hangar:** buy wing weapons and cores with credits, upgrade them to level 5, and
  fit them. The **Airframe** tab upgrades Hull, Shield, Shield regen, Thrusters, Magnet and
  Capacitor (5 levels each, 250 → 2,400 credits).
- **Results:** score, credits (half kept on a failed run), the three medals (Ace, Untouched,
  Purge) with their one-time bonus, total credits, new best, unlocks. CONTINUE goes to the map.
- **Saves** automatically after every stage. **Sandbox** on the map makes everything free for
  testing and saves nothing; **Reset progress** (click twice) starts over.
- Enemy credits are ~3× M2's and the magnet reaches 4 cells, so a good 1-1 run banks ~600.

## What's in M2 — weapons and the hangar
- **The hangar** (the game starts here): fit any of the 8 wing weapons to each wing and any of
  the 4 core specials, at any level 1–5, pick a stage and launch. Your ship flies a live preview
  with the loadout behind the menu. Everything is unlocked for testing until M3's shop.
- **Wing weapons:** Pulse Repeater, Scatter Cannon (fan), Lance Driver (piercing), Sunline Beam
  (continuous, ramps up), Seeker Pods (homing, blast), Hornet Rack (micro-missile salvos), Arc Coil
  (chain lightning), Ricochet Disc (bounces off the sides). Each has its own pod on the wing.
  Every level adds one more projectile (or strand, jump, disc); level 5 adds a twist
  (docs/DESIGN.md §4.2a).
- **Core specials,** charged by time and kills, fired with Space / left click / gamepad A / a
  second finger: Pulse Nova (clears bullets, hits everything), Solar Lance (mega beam), Aegis Field
  (absorbs hits, slows bullets), Wingmates (escort drones copying your wings).

## What's in M1 — the first stage
- **Stage 1-1 "First Light"** (about 2½ minutes): waves of Gnats and Needles, closed by the first
  Warden gunship. It ends in a results screen (score, kill %, hull damage, credits).
- **The Veil Armada:** Gnats (formations), Needles (hover, aimed burst, dive), Ram Drones (lock on
  and charge), Spine Mines (burst into a ring), Wardens (rotating rings and aimed fans) and
  Bastion turrets on drifting asteroids. Every attack is telegraphed by a red glow.
- **Damage:** a regenerating shield over the hull, a moment of invulnerability after each hit,
  repair orbs, and the ship's destruction at 0 hull (a failed run keeps half its credits).
- **Scoring:** a combo multiplier (+0.1 per kill up to ×5, falls back without kills, resets on
  hull damage); gold credit shards that the ship's magnet pulls in.
- The Vanguard (Blender model) flying through black space with a parallax starfield; Pulse
  Repeater on both wings, firing automatically.
- Controls: the ship follows the mouse; WASD / arrows; gamepad stick; touch drag. Shift / right
  mouse / LT = precision (half speed). Esc / P = pause.
- Keys: **H** hangar, **R** restart, **6 / 7 / 8** fly stage 1-1 / Armada Sampler / practice.
- Play field: 3:4 on PC and web (24 × 32), 9:16 portrait on phones; the score and controls sit
  in side panels beside it.

## Milestones (docs/DESIGN.md §12)
| # | Milestone | Status |
|---|---|---|
| M0 | Project scaffold, ship flying, Pulse Repeater | **done** |
| M1 | Enemies that shoot back, damage, shield and hull, stage timeline, stage 1-1 | **done** |
| M2 | All 8 wing weapons, the 4 core specials, hangar loadout screen | **done** |
| M3 | Credits, shop, upgrades, results, medals, saves, world map | **done** |
| M4 | Stages 1-2 to 1-5, the Tidebreaker boss, Veteran mode | **done** |
| M5 | Audio, polish, settings, touch, mobile and web builds | **done** (needs trying on a phone and in a browser) |

## Architecture
- `Assets/Skyforge/Simulation` — the game rules as plain, engine-free C# at a fixed 60 Hz
  (movement, weapons, enemies, collisions, scoring). Deterministic for a given seed and inputs,
  and testable without Unity.
- `Assets/Skyforge/Game` — Unity presentation: camera, input, procedural models, effects, HUD.
  It reads the simulation and draws it with interpolation; it never changes it directly.
- `Assets/Skyforge/Tests/EditMode` — NUnit tests for the simulation
  (Unity: Window → General → Test Runner → EditMode).
- `Assets/Skyforge/Resources/Models` — Blender models (FBX) for ships and enemies; anything not
  modelled yet uses a code-built stand-in. Blender sources are in `Art/Blender`.
- `Assets/Settings` — URP render pipeline assets and post-processing (bloom, vignette, tonemapping).
