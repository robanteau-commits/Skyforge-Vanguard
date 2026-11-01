# Skyforge Vanguard — notes for Claude

Unity 6.6 (6000.6.4f1, URP) low-poly vertical shoot-'em-up, built for Rob. Project folder on his PC:
`D:\Games\SkyforgeVanguard`. Read `docs/DESIGN.md` (the approved spec) and README.md first.

## Rules sources
1. `docs/DESIGN.md` — the approved design: weapons, cores, enemies, stages, scoring, controls,
   milestones (§12). **Authoritative** for numbers. Items marked **[TUNE]** are starting values;
   **[DECIDE]** marks questions still open for Rob.
2. Design changes Rob asks for override the doc. Update the doc in the same commit.
This is an original game in the genre of Ninja Kiwi's Super Monkey; don't copy that game's names,
stages, enemies or art.

## Hard rules
- `Assets/Skyforge/Simulation` is engine-free (asmdef `noEngineReferences: true`): plain C#, its own
  `Vec2`, seeded `Rng` (never System.Random / UnityEngine.Random), no wall-clock time, no
  Dictionary/HashSet iteration. It runs at a fixed 60 Hz (`Field.Dt`); same setup + inputs ⇒ same
  result. It uses `float` (single-player; exact cross-machine replays aren't a goal yet).
- The Game layer reads simulation state but never writes it; the only way in is
  `StageSim.Step(PlayerInput)`. Loadout changes start a new `StageSim`.
- Any new simulation state that affects gameplay goes into `StageSim.ComputeHash()`.
- Content ids are internal and stable (they go into saves): rename via `Name`, never `Id`.
- No transparent materials: effects fade by shrinking, and every material is URP Lit or Unlit
  (both in Graphics → Always Included Shaders, so `Shader.Find` works in builds). Glow = Unlit
  with an HDR colour above 1, which the bloom picks up.
- Play mode runs without a domain reload: reset statics in a
  `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` method (see `Mats.ResetCache`).
- Every new file under `Assets/` needs its `.meta` committed: run
  `python Tools/claude/make_metas.py` before committing (Rob's editor would otherwise invent its
  own GUIDs).

## Layout
- `Simulation/World/StageSim*.cs` — one partial class split by topic: Player (movement), Weapons
  (all 8 wing weapons: projectiles, homing, piercing, bouncing, blasts, the beam, chain lightning),
  Core (charge and the 4 core specials), Enemies (per-kind behaviour, telegraphs, contact), Combat (enemy fire, shield/hull
  damage, pickups), Timeline (spawning, cleared/failed), Practice (target drones).
  `Field` = the play field (X right, Y up the screen): 18 × 32 portrait on phones, 24 × 32 on
  PC/web (`StageSetup.FieldWidth`, `StageSim.HalfWidth`/`XScale`; use those, not the constants,
  for edges). Stages are authored for 18 wide and spread out. `Simulation/Content/` — weapons
  (`Weapons.cs`), enemy stats (`Enemies.cs`, `EnemyDefs`), stage timelines (`Stages.cs`, written
  with `StageBuilder`: 1-1 First Light and the Armada Sampler test stage).
  `Simulation/Progress/` — `Progress` (credits, owned weapons and levels, airframe levels, fit,
  stage records and medals; all shop rules; `Save`/`Load` text) and `Airframe` (upgrade costs and
  stat formulas, applied through `StageSetup.Airframe`). `Stages.World1` is the map order.
- `Game/` — Unity presentation. `GameRunner` boots in any scene, owns the stage and the fixed-step
  loop with render interpolation. `PlayfieldCamera` fits the field in a 9:16 viewport (side panels
  on wide screens). `Input/PlayerControls` = keyboard/mouse/gamepad/touch → `PlayerInput`.
  `GameRunner` has three screens (Map, Hangar, Flying), owns the `Progress` (loaded and saved by
  `SaveStore`, PlayerPrefs) and banks each finished stage (`RecordIfFinished`; not in sandbox,
  practice or the sampler). `Hud` is IMGUI (partial: `Hud.Map.cs` is the world map, where the
  game starts, over `Art/GlobeModel`; `Hud.Hangar.cs` is the shop and loadout screen with a live
  practice preview behind it; results in `Hud.cs`). `WeaponViews` draws beams, the Solar Lance, the Aegis ring, arcs and
  wingmates. Field (X, Y) maps to world (x, 0, z). The background (`Game/Art/Backdrop.cs`)
  is black space with a parallax starfield at y = −14 (Rob's call, replacing the terrain and
  ocean backdrops).
- `Game/Art/` — models. Ships and enemies come from **Blender** (decided by Rob): FBX files in
  `Assets/Skyforge/Resources/Models/`, loaded by `ModelLibrary` (wraps each in a mount object and
  swaps materials by name for `Mats`). **docs/BLENDER.md** is the modelling brief: names, scale,
  orientation, mount points, materials, budgets, export settings. Until a Blender model exists the
  code-built fallback is used: `MeshBuilder` (flat-shaded facets, auto-wound faces),
  `VanguardModel` (player ship, nose +Z, pods change with the weapon), `EnemyModels`. Also
  `Backdrop` (starfield), `InstancedBatch` (GPU-instanced bullets and particles),
  `Mats` + `Palette` (the dark sci-fi colours). Blender sources live in `Art/Blender/`.
- `Editor/ModelImportSettings.cs` — fixes the import settings of everything in the Models folder.
- `Tests/EditMode/` — NUnit tests: `SimulationTests` (movement, weapons, determinism),
  `StageTests` (enemies, damage, combo, pickups, stage flow), `WeaponTests` (every wing weapon
  and core), `LevelTests` (what each weapon level adds), `FieldTests` (the wide PC field) and
  `ProgressTests` (shop, airframe, medals, unlocks, save text).

## Verifying changes
- **From a cloud session (no Unity):** the simulation and its tests compile under plain .NET:
  make a net8.0 test project that includes `Assets/Skyforge/Simulation/**/*.cs` and
  `Assets/Skyforge/Tests/EditMode/**/*.cs` with NUnit 3, then `dotnet test`. The Game layer can be
  compile-checked against the `UnityEngine.Modules` NuGet reference assemblies plus small stubs
  for the Input System / URP members it uses. Neither replaces playing it in Unity. Stubs must
  also declare the other public types in those namespaces (e.g. the Input System's own
  `PlayerInput` component), or name clashes slip through: ours is aliased in `PlayerControls.cs`.
- **On Rob's PC (Editor open):** `bash Tools/claude/test.sh` runs the EditMode tests;
  `bash Tools/claude/test.sh '"TestA","TestB"'` runs named `SimulationTests`. `unity command
  --project-path "D:\Games\SkyforgeVanguard" <cmd>` drives the Editor (`recompile`, `eval`,
  `editor_play`, `capture_game_view`, `build`…). `eval` has a 5 s main-thread limit.
- `bash Tools/claude/build.sh` builds `Builds/Windows/Skyforge Vanguard.exe` through the open Editor.

## Working with Rob
- He's new to Unity and Git; give click-by-click / command-by-command steps for anything he does
  (he uses Command Prompt, where drive changes need `cd /d`).
- He plays each build and reports what feels wrong; confirm a bug with a test or measurement
  before changing anything, and say plainly what you found.
- Design questions with several reasonable answers: offer 2–3 options with a recommendation.
- Commit to `main`, one commit per finished change. Bump `bundleVersion` in
  `ProjectSettings/ProjectSettings.asset` for every build he'll play: patch for fixes and tweaks,
  minor for new features or milestones.
- Decided so far: all four core specials in v1; failed runs keep 50% of credits; no Mac, so iOS
  waits (Windows, web, Android first); models are made in Blender (a Claude Code session on Rob's
  PC with the "MCP for Blender" server, following docs/BLENDER.md). That session and cloud
  sessions both push to `main`: always `git pull` before starting work.
