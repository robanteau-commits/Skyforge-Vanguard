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
| `enemy_gnat` | Swarm fighter, small arrowhead. Hit radius 0.55. | code Gnat |
| `enemy_needle` | Diving fighter, long and thin. Hit radius 0.6. | code Needle |
| `enemy_ramDrone` | Kamikaze, spiked wedge. Hit radius 0.6. | code Ram Drone |
| `enemy_spineMine` | Spiked mine. Hit radius 0.6. Spins slowly. | code mine |
| `enemy_warden` | Gunship, broad and armoured. Hit radius 1.5. | code Warden |
| `enemy_bastion` | Turret with one heavy barrel pointing forward (the whole model turns to aim). Hit radius 0.9. | code turret |
| `enemy_broodCarrier` | Mini-boss: a broad armoured wedge, about 3.4 wide. Hit radius 1.7. Its two launch bays are separate models. | code Carrier |
| `enemy_broodBay` | A Carrier's launch bay pod, mounted 1.9 to each side of the Carrier's centre. Hit radius 0.8. | code bay |
| `enemy_tidebreaker` | The boss's hull, a sea-fortress about 15 wide and 6.5 deep (local x ±7.6, z −3.2…+3.3), prow forward. It's never hit itself; its parts sit on it: cannons at local (±5.5, z +1.2), missile bay at z −2.4, eye at z +1.6. | code hull |
| `enemy_tideCannon` | Flank cannon turret with a twin barrel pointing forward (the model turns to aim). Hit radius 1.1. | code cannon |
| `enemy_tideMissileBay` | Armoured block of launch tubes. Hit radius 1.2. | code bay |
| `enemy_tideEye` | The core eye: a big glowing orb in a mount. Hit radius 1.4. Needs a child object named `Shield` (armoured iris plates over the orb), which the game shows only while the eye is shielded. | code eye |
| `enemy_tideMissile` | Small homing missile. Hit radius 0.45. | code missile |
| `prop_asteroid` | The rock a Bastion rides on, about 4–5 across and 2 tall, its top just below the turret's base. Origin at its centre; the game places it 1.4 below the play plane. | code asteroid |

Later ones follow the same pattern. Pods use the weapon ids from `Simulation/Content/Weapons.cs`:
`pod_scatterCannon_lv1`, `pod_lanceDriver_lv1`, … Enemies are `enemy_` plus the kind in camelCase:
`enemy_broodCarrier`, `enemy_tidebreaker` and the boss parts listed above.
Enemy models face forward like the ship (nose to Blender −Y); the game turns them to face where
they're flying, which is usually down the screen.

## 3. Scale and orientation

- **1 Blender metre = 1 game unit.** The play field is 18 × 32 units.
- **Nose points to Blender −Y**: in Front view (numpad 1) the ship faces you. **Top is +Z.**
  The ship's right wing (pilot's right) is at Blender **−X**, the left of the screen in Front
  view. The game camera looks down from above, so the **top surface matters most**.
- Build symmetric ships with a **Mirror** modifier on X.
- Unity's FBX import turns a model built this way to face backwards (−Z). The game corrects this
  for every model by turning it 180° when loading (`ModelLibrary.BlenderToGame`), so keep
  building nose to −Y and **don't** rotate models in Blender to compensate. This was found with the
  first `vanguard` export (v0.1.2 pointed down the screen) and fixed in v0.1.3.
- Left/right hasn't been checked with an asymmetric model yet. The first time you make one, check
  in Unity that its right side is on the ship's right.

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

- **Low poly, flat-shaded** (DESIGN §10.1): facets that stay visible. Shade Flat, no
  subdivision, no textures. Strong, readable silhouettes from above.
- **Player ship style: "faceted alloy"** (Rob's pick, 2026-11-01, from an AI-generated concept
  sheet; recreate the look as our own model, don't trace the image). It's a clean, studio-model
  feel: a light matte alloy airframe built from large crisp facets, a faceted teal canopy,
  darker graphite engine nacelles and intakes, a row of small dark vent slots along the spine,
  and small peach panels on the wingtips. See §8 for the Vanguard brief.
- **Enemies** keep the dark hull with red-magenta glow for now; Rob decides their restyle after
  seeing the new Vanguard in the game.
- Use only these **material names**. The colours you set in Blender are just for preview, because
  the game swaps each slot for its own material by name:

| Material name | Used for | Game colour |
|---|---|---|
| `Hull` | Player: main airframe | Light matte alloy |
| `HullLight` | Player: brighter panels, trim | Near-white alloy |
| `HullDark` | Player: nacelles, nozzles, intakes, vent slots | Dark graphite |
| `Canopy` | Cockpit glass | Teal glass |
| `Accent` | Player: small wingtip panels only | Muted peach (matte; keep it small so it never reads as an enemy bullet) |
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

1. **Rebuild `vanguard` in the Kestrel style (§8).** Rob's current priority.
2. `vanguard_core`, `vanguard_flame`.
3. `pod_pulseRepeater_lv1` … `_lv5`, styled to match: graphite nacelle pods with glowing muzzles.
   Then the other seven weapons' pods (all in the game with code-built stand-ins since v0.3.0):
   `pod_scatterCannon` (short, wide, flared muzzle), `pod_lanceDriver` (long twin-rail gun),
   `pod_sunlineBeam` (lens emitter), `pod_seekerPods` (box launcher, one tube per missile),
   `pod_hornetRack` (wide block with a grid of small tubes), `pod_arcCoil` (glowing coil rings and
   two prongs), `pod_ricochetDisc` (launcher with a disc on top). A plain `pod_<id>` covers every
   level; add `_lv<N>` versions where the level changes the look (barrel or tube counts).
   `wingmate`: the Wingmates core's escort drone, a small delta about 1.4 across (not loaded yet;
   ask the cloud session to hook it up when it exists).
4. `enemy_practiceDrone`.
5. The M1 enemies, now in the game with code-built stand-ins: `enemy_gnat`, `enemy_needle`,
   `enemy_ramDrone`, `enemy_spineMine`, `enemy_warden`, `enemy_bastion`, `prop_asteroid`.
   Press **7** in the game for the Armada Sampler, which shows every enemy.
6. Then each new enemy or weapon as the milestones need it.

## 8. The Vanguard, Kestrel style (current task)

Rebuild `vanguard` (airframe only, as before) as a **delta interceptor** in the faceted-alloy
style. Keep the size and mount points of §4 so the game's pods, core, flames and hitbox still fit:
nose at Y −1.75, tail at Y +1.5, wingtips at X ±1.6, about 0.6 tall, pod mounts under the wings
at X ±1.0, Z −0.13.

- **Wings:** a big, clean delta (or cranked delta) with a sharp leading edge, built from a few
  large flat facets, `Hull` on top with a `HullLight` panel inset. Small **`Accent` peach panels
  on each wingtip** (a few faces, not the whole tip).
- **Fuselage:** a faceted, slightly flattened body with a pointed nose; the **teal `Canopy`**
  sits forward as a faceted, gem-like bubble.
- **Tail:** two vertical or slightly canted fins in `HullLight`.
- **Engines:** dark graphite (`HullDark`) nacelle or nacelles at the back with visible intakes;
  the exhausts line up with the flame mounts at X ±0.2, Y +1.5.
- **Details:** a short row of small `HullDark` vent slots along the spine behind the canopy; a
  thin `Glow` strip on each wing's leading edge (it's the player's cyan identity in play).
- **Budget:** ≤ 1,500 triangles. Read it from above first: the delta and the canopy should be
  clear at the game's size (the ship is about 3 units long on an 18-unit-wide screen).
- Show Rob a top view and a 3/4 view before exporting; then export, check it in the game
  (it should point up the screen), and commit the `.blend`, `.fbx` and Unity's `.fbx.meta`.

