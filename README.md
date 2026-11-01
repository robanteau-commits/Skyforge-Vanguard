# Skyforge Vanguard

A low-poly 3D vertical shoot-'em-up for PC, web and mobile, built in Unity 6 (URP). Fly the
Vanguard, fit a weapon to each wing and the core, and fight off the Veil Armada.

- **Design:** [docs/DESIGN.md](docs/DESIGN.md) (approved)
- **Models:** made in Blender, see [docs/BLENDER.md](docs/BLENDER.md)
- **Engine:** Unity 6000.6.4f1, Universal Render Pipeline, Input System
- **Status:** milestone M1, the first stage (v0.2.0)

## Opening the project
1. Get the latest files: in Command Prompt, `cd /d D:\Games\SkyforgeVanguard` then `git pull`.
2. Unity Hub → **Projects** → **Add** → **Add project from disk** → choose
   `D:\Games\SkyforgeVanguard`. The first open takes a few minutes while Unity builds its cache.
3. Open `Assets/Scenes/Main.unity` if it isn't already open, then press **Play**.

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
- Test keys: **6** stage 1-1, **7** Armada Sampler (every enemy type), **8** practice drones,
  **1–5** Pulse Repeater level (restarts), **R** restarts.
- Portrait 9:16 play field; on wide screens the score and controls sit in side panels.

## Milestones (docs/DESIGN.md §12)
| # | Milestone | Status |
|---|---|---|
| M0 | Project scaffold, ship flying, Pulse Repeater | **done** |
| M1 | Enemies that shoot back, damage, shield and hull, stage timeline, stage 1-1 | **done** |
| M2 | All 8 wing weapons, the 4 core specials, hangar loadout screen | next |
| M3 | Credits, shop, upgrades, results, medals, saves, world map | |
| M4 | Stages 1-2 to 1-5, the Tidebreaker boss, Veteran mode | |
| M5 | Audio, polish, settings, touch, mobile and web builds | |

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
