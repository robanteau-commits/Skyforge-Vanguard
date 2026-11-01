# Blender models — guide

Rob decided (2026-11-01) that the game's models are made in **Blender** and replace the
code-built ones. The work runs in a Claude Code session on Rob's PC, in
`D:\Games\SkyforgeVanguard`, with the Blender MCP ("MCP for Blender") connected. This file is
that session's brief. Read `CLAUDE.md` and `docs/DESIGN.md` first.

The game already loads these models when they exist (`Game/Art/ModelLibrary.cs`). Any model not
made yet falls back to its code-built version, so models can be replaced **one at a time** and
the game always runs.

---

## 1. Workflow for each model

1. `git pull` first. Another session may have pushed changes.
2. Build the model in Blender over the MCP, following §3–§5.
3. **Show Rob before exporting**: take a viewport screenshot (top view and a 3/4 view) and ask
   for his OK. He decides the look.
4. Save the Blender source to `Art/Blender/<name>.blend`, outside `Assets/`, so Unity doesn't try
   to import .blend files.
5. Export the FBX to `Assets/Skyforge/Resources/Models/<name>.fbx` (§6).
6. Check it in the game: open Unity, press **Play**, and look at it from the game camera. Fix
   the scale or orientation if it's off.
7. Commit the `.blend`, the `.fbx` **and** the `.fbx.meta` that **Unity** creates when it imports
   the model, then push to `main`. Don't write that `.meta` by hand or with
   `Tools/claude/make_metas.py`, which skips models on purpose. Its first line under the GUID must
   read `ModelImporter:`. If it says `DefaultImporter:`, let Unity reimport the model
   (right-click it → Reimport) and commit the rewritten file.

Terrain, bullets and explosion particles stay code-built: they're tiny or endless.

## 2. Model names

The file name is the model's id. The game looks for exactly these names.

| File (`<name>.fbx`) | What it is | Replaces |
|---|---|---|
| `vanguard` | Player ship airframe: fuselage, wings, canopy, tail fins, engine nozzles. **No** core, flames or weapon pods, which are separate files. | code airframe |
| `vanguard_core` | The core crystal in the spine. Spins slowly and pulses. | code octahedron |
| `vanguard_flame` | One engine flame (used twice). The game stretches it along its length. | code flame |
| `pod_pulseRepeater_lv1` … `_lv5` | Pulse Repeater wing pod at each level: 1, 2, 2, 3, 3 barrels. A missing level uses the nearest lower one; `pod_pulseRepeater` with no level works for all. | code pod |
| `enemy_practiceDrone` | Unarmed target drone (M0). | code drone |

Later ones follow the same pattern. Pods use the weapon ids from `Simulation/Content/Weapons.cs`:
`pod_scatterCannon_lv1`, `pod_lanceDriver_lv1`, … Enemies are `enemy_` plus the kind in camelCase:
`enemy_gnat`, `enemy_needle`, `enemy_ramDrone`, `enemy_spineMine`, `enemy_warden`,
`enemy_bastion`, `enemy_broodCarrier`, `enemy_tidebreaker` (boss parts to be agreed when we get
there).

## 3. Scale and orientation

- **1 Blender metre = 1 game unit.** The play field is 18 × 32 units.
- **Nose points to Blender −Y**: in Front view (numpad 1) the ship faces you. **Top is +Z.**
  The ship's right wing (pilot's right) is at Blender **−X**, the left of the screen in Front
  view. The game camera looks down from above, so the **top surface matters most**.
- Build symmetric ships with a **Mirror** modifier on X.
- After the first export, check once in Unity that the nose points up the screen and the model
  isn't mirrored or rotated. If it is, fix the export settings (§6), not the game code, and
  update this section.

## 4. Sizes and mount points

The object origin of each file is its mount point; the game places the origin there. Positions
are given in Blender coordinates relative to the ship's origin (in game units).

| Model | Origin | Size |
|---|---|---|
| `vanguard` | Ship centre, the point the game moves. | Nose at Y −1.75, tail at Y +1.5, wingtips at X ±1.6, about 0.6 tall. Leave a mount under each wing at X ±1.0, Z −0.13 for the pods. |
| `vanguard_core` | Crystal centre. Sits at (0, +0.35, +0.27) on the ship. | About 0.26 wide, 0.44 long, 0.38 tall. |
| `vanguard_flame` | Engine exit. Sits at (±0.2, +1.5, +0.02). | Points backwards (+Y) from the origin, about 0.55 long and 0.18 wide. |
| `pod_*` | Pod centre. Sits at (±1.0, 0, −0.13). | About 1.3 long. **Muzzles at Y −0.6** (forward), because that's where the bullets start. Model the **right** pod, with its outboard side towards Blender −X; the game mirrors it for the left. |
| `enemy_*` | Centre of the ship. | The enemy's hit radius (from the simulation) is the outline seen from above. `enemy_practiceDrone`: radius 0.75. |

## 5. Look

- **Dark sci-fi low poly** (DESIGN §10.1): flat-shaded facets that stay visible. Shade Flat,
  no subdivision, no textures. Strong, readable silhouettes from above.
- Use only these **material names**. The colours you set in Blender are just for preview, because
  the game swaps each slot for its own material by name:

| Material name | Used for | Game colour |
|---|---|---|
| `Hull` | Main armour | Gunmetal |
| `HullLight` | Panels, trim | Light grey metal |
| `HullDark` | Nozzles, intakes, recesses | Near-black |
| `Canopy` | Cockpit glass | Dark blue glass |
| `Glow` | Player glow: edge strips, core, flames, muzzles | Bright cyan (blooms) |
| `GlowSoft` | Dimmer player glow | Soft cyan |
| `EnemyHull` | Enemy armour | Dark purple-black |
| `EnemyPlate` | Enemy panels | Muted plum |
| `EnemyGlow` | Enemy seams, eyes | Hot red-magenta (blooms) |

- Glow sparingly: thin strips and small shapes. Big glowing areas wash out the bloom.
- Player = cyan and white, enemies = red and magenta. Never mix them.
- **Triangle budgets** (phones run this too): ship airframe ≤ 1,500; core, flame and pod ≤ 300
  each; small enemies ≤ 400; gunships and turrets ≤ 1,200; boss ≤ 6,000.

## 6. FBX export settings

File → Export → FBX (.fbx):
- **Include:** Limit to *Selected Objects*; Object Types: **Mesh** only.
- **Transform:** Scale 1.0, Apply Scalings **FBX All**, Forward **−Z Forward**, Up **Y Up**,
  **Apply Unit** on, **Apply Transform** on.
- **Geometry:** Smoothing **Face**, **Apply Modifiers** on.
- **Animation:** off.
- Join each model into **one object** before exporting (Ctrl+J), unless it needs moving parts.

Unity applies the matching import settings automatically to anything in
`Assets/Skyforge/Resources/Models/` (`Editor/ModelImportSettings.cs`).

## 7. Order of work

1. `vanguard`: the player ship. Keep the current look (sleek delta wing, canards, canted twin
   fins, twin nozzles, glowing leading edges) and refine it.
2. `vanguard_core`, `vanguard_flame`.
3. `pod_pulseRepeater_lv1` … `_lv5`.
4. `enemy_practiceDrone`.
5. Then each new enemy or weapon as the milestones need it (M1: Gnat, Needle, Ram Drone, Spine
   Mine, Warden, Bastion).
