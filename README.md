# Skyforge Vanguard

A low-poly 3D vertical shoot-'em-up for PC, web and mobile, built in Unity 6 (URP). Fly the
Vanguard, fit a weapon to each wing and the core, and fight off the Veil Armada.

- **Design:** [docs/DESIGN.md](docs/DESIGN.md) (approved)
- **Models:** made in Blender, see [docs/BLENDER.md](docs/BLENDER.md)
- **Engine:** Unity 6000.6.4f1, Universal Render Pipeline, Input System
- **Status:** milestone M0, the test flight (v0.1.1)

## Opening the project
1. Get the latest files: in Command Prompt, `cd /d D:\Games\SkyforgeVanguard` then `git pull`.
2. Unity Hub → **Projects** → **Add** → **Add project from disk** → choose
   `D:\Games\SkyforgeVanguard`. The first open takes a few minutes while Unity builds its cache.
3. Open `Assets/Scenes/Main.unity` if it isn't already open, then press **Play**.

## What's in M0 — test flight
- The Vanguard (a delta-wing fighter built from code) flying over an endless, scrolling,
  low-poly coastline at night.
- Pulse Repeater on both wings, firing automatically; its pods change shape with the level.
- Unarmed practice drones to shoot, with explosions, a score and a kill count.
- Controls: the ship follows the mouse; WASD / arrows; gamepad stick; touch drag. Shift / right
  mouse / LT = precision (half speed). Esc / P = pause.
- Test keys: **1–5** set the Pulse Repeater level (restarts the flight), **R** restarts.
- Portrait 9:16 play field; on wide screens the score and controls sit in side panels.

## Milestones (docs/DESIGN.md §12)
| # | Milestone | Status |
|---|---|---|
| M0 | Project scaffold, ship flying, Pulse Repeater | **done** |
| M1 | Enemies that shoot back, damage, shield and hull, stage timeline, stage 1-1 | next |
| M2 | All 8 wing weapons, the 4 core specials, hangar loadout screen | |
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
