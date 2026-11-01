# Skyforge Vanguard — Design Document (v0.1, approved)

A vertical-scrolling, low-poly 3D shoot-'em-up. You fly the **Vanguard**, a prototype delta-wing
fighter forged to stop an invading drone armada. Before each stage you fit a weapon to the **left
wing**, the **right wing**, and the **core**. You earn score and credits by shooting the enemy ships
that are shooting back at you.

Inspired by the genre of Ninja Kiwi's *Super Monkey*: separate weapon slots, a hangar shop and a
stage map. Every name, weapon, enemy, stage and piece of art in this game is original.

> Items marked **[TUNE]** are starting numbers I expect to change after you play-test.
> Items marked **[DECIDE]** are open questions for you (collected again at the end).

---

## 1. Decisions locked in (from the Q&A)

| Topic | Decision |
|---|---|
| Engine | Unity 6 (6000.6.4f1, URP), C#. No three.js. Web via Unity's web export. |
| Platforms | Windows / Steam, web browser, Android; iOS later (needs a Mac) |
| Camera | Top-down 3D, tilted ~20°, portrait play field |
| Structure | World map → stages → boss; replay for medals |
| Loadout | Left wing + right wing + core, chosen in the hangar and fixed for the stage |
| Wings | Independent: any wing weapon on either side, duplicates allowed |
| Core | A charged special that you trigger yourself |
| Progression | Credits buy weapons and upgrade them (5 levels); the airframe has upgradeable stats |
| Health | Regenerating shield over a hull bar; hull 0 = stage failed |
| Controls | Auto-fire; you move the ship and press one button for the core |
| Difficulty | Casual-friendly Normal; Veteran unlocks after a world is cleared |
| Scoring | Score with a combo multiplier, plus collectable credits and 3 medals per stage |
| Setting | Near-future Earth → orbit → the invaders' world |
| Art | Dark sci-fi, flat-shaded low poly, neon emissive accents with bloom; models made in Blender (docs/BLENDER.md), with code-built fallbacks |
| Audio | Sound effects generated in code, plus a synth soundtrack |
| Online | None in v1 (offline only) |
| Business | Premium: pay once, no ads, no in-app purchases |
| v1 scope | World 1: 5 stages + boss stage, 8 wing weapons, all 4 core specials, hangar and shop |

---

## 2. Core loop

```
World map ──► pick stage ──► Hangar (fit L wing / R wing / core, buy & upgrade)
    ▲                                   │
    │                                   ▼
    └── results: score, credits, medals ◄── fly stage (~2.5–3.5 min)
```

1. **Fly:** the screen scrolls forward and waves of enemies arrive on scripted paths. You dodge
   their bullets, your wings fire on their own, and you trigger the core when it's charged.
2. **Collect:** destroyed enemies drop credit shards, which drift toward you inside your magnet
   range and otherwise fly off the bottom of the screen.
3. **Results:** score, kill %, damage taken and medals earned. Credits are banked **even if you
   fail the stage** (at 50%, decided) so a struggling player still progresses.
4. **Spend:** buy new weapons, upgrade weapons and the airframe, then try again or go on.

---

## 3. The play field and the ship

### 3.1 Play field
- Logical field **18 × 32 units** (9:16 portrait). Everything in the simulation uses these units.
- **Phone:** the field fills the screen (letterboxed on unusual aspect ratios).
- **PC / web:** the field sits in the centre, with side panels for score, combo, credits, the hull
  and shield bars and the loadout. The side panels do not block or affect play.
- The camera tilts ~20° so the low-poly models read as 3D. Gameplay is strictly 2D on the XZ plane,
  which keeps collisions simple and readable.
- Fixed simulation step of **60 Hz**, with rendering interpolated. The fixed step is independent of
  frame rate (important for web and mobile).

### 3.2 The Vanguard (player ship)
- A sleek delta wing with a hardpoint pod under each wing. **The pod model changes with the weapon
  equipped** (barrel cluster, laser emitter, missile rack, coil…). The **core** is a glowing crystal
  in the fuselage, coloured by the equipped core special. When the core is fully charged it pulses
  so you can see it's ready.
- **Hitbox:** a circle of radius **0.35** around the core, much smaller than the ship model (genre
  standard). Its outline appears faintly whenever enemy bullets are nearby.
- The ship banks (rolls) when moving sideways. This is visual only.

| Airframe stat | Base | Per upgrade level (5 levels) | Max |
|---|---|---|---|
| Hull | 100 | +20 | 200 |
| Shield | 40 | +12 | 100 |
| Shield regen | 10 /s after 2.5 s without a hit | −0.3 s delay, +2 /s | 1.0 s, 20 /s |
| Thrusters (max speed) | 14 u/s | +1.5 | 21.5 u/s |
| Magnet range | 2.5 u | +1.0 | 7.5 u |
| Capacitor (core charge rate) | 100% | +10% | 150% |

Airframe upgrade costs (each stat): **250 / 500 / 900 / 1500 / 2400** credits. **[TUNE]**

### 3.3 Damage
- Enemy hits drain the **shield** first, then the **hull**.
- After any hit you're invulnerable for **0.8 s** (the ship flickers).
- Contact with an enemy ship deals its ram damage, and the enemy takes 50 damage.
- **Repair orbs** (+15 hull) drop from some tougher enemies and appear at fixed points in each stage.

---

## 4. Weapons

### 4.1 Rules
- Both wings fire on their own the whole stage. The left wing's projectiles start from the left
  pod and the right wing's from the right, so a different weapon on each side looks different.
- Any wing weapon fits either wing. Putting the **same weapon on both wings** is allowed and simply
  doubles it. (Combo synergies were considered and left out in favour of free mixing.)
- Each weapon has **5 levels**. You buy it at Lv 1; upgrades raise its damage and often change its
  pattern (more streams, more missiles, more chain jumps…). The pod model gains detail as it levels.
- Damage figures are **per wing**. A Lv 1 wing does about **20 DPS** against a single target; Lv 5
  does about **2.3×** that. **[TUNE]**
- Upgrade costs for any weapon: **Lv2 300 · Lv3 600 · Lv4 1000 · Lv5 1600**. **[TUNE]**

### 4.2 Wing weapons (8 in v1)

| # | Weapon | Type | What it does | Lv 1 | Lv 5 | Price | Strong vs / weak vs |
|---|---|---|---|---|---|---|---|
| 1 | **Pulse Repeater** | Bullets | Fast stream of cyan bolts straight ahead. | 1 stream, 10 shots/s × 2 dmg | 3 streams, 12/s × 1.3 dmg | Starter (free) | All-rounder |
| 2 | **Scatter Cannon** | Spread | A fan of pellets. | 3 pellets at 15°, 4/s × 1.8 | 7 pellets at 40°, 5/s × 2.4 | 600 | Swarms / single targets far away |
| 3 | **Lance Driver** | Laser bolts | Slow-firing bolts that **pierce** every enemy in a line. | 2/s × 11, pierce ∞ | 3/s × 17, width +50% | 900 | Lines of enemies / swarms off to the sides |
| 4 | **Sunline Beam** | Continuous beam | A beam to the top of the screen. Damage ramps up the longer it stays on one target. | 16 → 28 DPS over 1.5 s | 30 → 60 DPS, wider beam | 1400 | Bosses, gunships / swarms |
| 5 | **Seeker Pods** | Homing missiles | Missiles that lock onto the nearest enemy, with a small blast. | 1 missile / 0.6 s × 13, blast r 0.8 | 3 missiles / 0.5 s × 16, blast r 1.2 | 800 | Enemies off to the sides / fast-moving enemies (slow turn rate) |
| 6 | **Hornet Rack** | Missile swarm | A salvo of many weak micro-missiles that spread out over targets. | 4 missiles every 1.2 s × 6 | 8 missiles every 0.9 s × 7 | 1100 | Swarms, mines / armoured targets |
| 7 | **Arc Coil** | Exotic: chain lightning | Strikes the nearest enemy within 9 u and jumps to others. | 3/s × 6, 2 jumps | 4/s × 9, 5 jumps, range 11 | 1300 | Clusters / short range |
| 8 | **Ricochet Disc** | Exotic: bouncing | Spinning discs that bounce off the sides of the screen and through enemies, each hitting at most 4 times. | 1 disc / 0.8 s × 8 | 2 discs / 0.6 s × 12, 6 hits | 1000 | Crowded screens / skill to aim |

Reserved for a later update (not in v1): **Flak Burster** (shells that burst into shrapnel at a set
distance) and **Plasma Orb** (a slow orb that eats enemy bullets).

### 4.3 Core specials

The core charges from **0 to 100**: **+2 per second** of flight, plus **+1 per kill** (+5 for
gunship-class enemies) **[TUNE]**, multiplied by the Capacitor stat. When it's full, press Core to fire it.

| Core | Effect at Lv 1 | Lv 5 | Price |
|---|---|---|---|
| **Pulse Nova** (screen bomb / EMP) | Clears **all enemy bullets** on screen and deals 120 damage to every enemy on screen. 1 s of invulnerability. | 300 damage; also stuns enemies for 1.5 s | Starter (free) |
| **Solar Lance** (mega beam) | A 3-unit-wide beam for 3 s at 250 DPS, cancelling bullets inside it. The ship can still move. | 4.5 s, 450 DPS, 4.5 units wide | 1500 |
| **Aegis Field** (shield / time-slow) | A bubble for 5 s that absorbs all hits; enemy bullets within 6 u move at 40% speed. | 8 s; bullets it absorbs are fired back at enemies | 1200 |
| **Wingmates** (drones) | 2 drones fly alongside you for 10 s, each firing a copy of your wing weapons at 40% damage. | 3 drones, 15 s, 60% damage | 1300 |

**v1 cores:** all four ship in v1 (decided).

Core upgrade costs: **Lv2 400 · Lv3 800 · Lv4 1300 · Lv5 2000**. **[TUNE]**

### 4.4 Starting loadout
Pulse Repeater on both wings, Pulse Nova in the core, and 0 credits. Stage 1-1 is tuned to pay
out ~600–800 credits on a decent run, so the first purchase (Scatter Cannon or a Seeker Pod) comes
after one or two runs.

---

## 5. Enemies — the Veil Armada

An alien drone armada. Its ships are angular and dark, with **red and magenta** glowing seams so
they never get mistaken for the player (who is **cyan and white**). Enemy bullets are **hot
orange/pink with a dark outline**, the brightest things on screen, so they always read.

| Enemy | Role | HP | Behaviour | Bullets | Score | Credits |
|---|---|---|---|---|---|---|
| **Gnat** | Swarm fighter | 6 | Flies in formations (V, line, snake, swirl) along spline paths | Rare single aimed shots | 50 | 2 |
| **Needle** | Diving fighter | 20 | Enters, pauses, fires, then dives off-screen | 3-shot aimed burst | 120 | 5 |
| **Ram Drone** | Kamikaze | 14 | Locks onto you after 0.7 s and charges (telegraphed by a red flash) | None; 25 ram damage | 100 | 4 |
| **Spine Mine** | Mine | 10 | Drifts with the scroll; when shot or approached within 2 u it bursts into an 8-bullet ring | Ring on death | 60 | 2 |
| **Warden** | Gunship | 160 | Slow, armoured, stays on screen for a while | Rotating rings and fans | 600 | 20 |
| **Bastion** | Turret on a drifting asteroid (Rob's call, 2026-11-01: the background is space, so no ground) | 90 | Drifts down the screen with its asteroid and turns to track you | Aimed 3-shot bursts | 400 | 12 |
| **Brood Carrier** | Mini-boss | 700 | Crosses slowly, launching Gnats; it has 2 destructible launch bays | Spread bursts | 2500 | 80 |

**Bullet rules (Normal):** enemy bullet speed 6–10 u/s, at most ~250 enemy bullets on screen in
World 1, and every attack is telegraphed (a muzzle glow 0.3 s before firing). **[TUNE]**

### 5.1 Difficulty modes
| | Normal | Veteran (unlocks after the World 1 boss) |
|---|---|---|
| Enemy bullet speed | ×1.0 | ×1.25 |
| Bullet density | ×1.0 | ×1.35 (more bullets per pattern) |
| Damage taken | ×1.0 | ×1.5 |
| Enemy HP | ×1.0 | ×1.3 |
| Credits / score | ×1.0 | ×1.3 / ×1.5 |

Medals are earned separately for each difficulty.

---

## 6. World 1 — "Earthfall" (v1)

The Veil Armada has broken through the orbital defence grid and is landing on Earth. The Vanguard
launches from **Skyforge**, a mountain-top aerospace foundry. (Worlds 2–3 are planned to move into
orbit and then onto the invaders' world, see §11.)

| Stage | Name | Backdrop (scrolling ground) | New threat introduced | Length |
|---|---|---|---|---|
| 1-1 | **First Light** | Black space with a scrolling starfield (see §10.1) | Gnats, Needles; the first Warden closes the stage | 2.5 min |
| 1-2 | **Harbor Line** | Docks, cargo ships, cranes | Bastion turrets on drifting asteroids | 2.5 min |
| 1-3 | **Stormwall** | Ocean in a storm, lightning flashes | Spine Mines, Ram Drones | 3 min |
| 1-4 | **Foundry Ridge** | Mountain refinery, smoke stacks | Wardens; first Brood Carrier as mid-boss | 3 min |
| 1-5 | **Upper Air** | Above the clouds at dusk, the Armada's mothership on the horizon | Mixed waves, the hardest regular stage | 3.5 min |
| 1-B | **The Tidebreaker** (boss) | Night ocean | Boss fight | ~3 min |

Stages are unlocked in order. The boss stage unlocks once 1-5 is cleared.

### 6.1 Boss: The Tidebreaker
An armoured Armada sea-fortress that rises out of the ocean. It fills the top third of the screen.

- **Parts:** 2 **flank cannons** (400 HP each), a **missile bay** (500 HP) and the **core eye**
  (1500 HP). The eye is shielded until both flank cannons are destroyed.
- **Phase 1:** the flank cannons sweep aimed streams, and the missile bay launches slow homing
  missiles (which can be shot down).
- **Phase 2** (both cannons down): the eye opens and fires rotating spiral patterns; Gnat waves
  launch from the hull.
- **Phase 3** (eye below 40%): the fortress lunges forward and fires dense ring barrages with gaps
  to slip through, plus sweeping laser telegraphs.
- Each destroyed part drops credits and a repair orb. Defeat = **5000 score, 400 credits**.

### 6.2 Stage scripting
Each stage is a **timeline** defined in C# content files: at time *t*, spawn *enemy type* in
*formation* along *path*, plus backdrop sections and music cues. That keeps stages easy to tweak
and easy for me to write and balance.

---

## 7. Scoring, credits, medals

- **Score:** each enemy's base score × the **combo multiplier**.
- **Combo:** starts at ×1.0, +0.1 per kill, capped at **×5.0**. It drops back by 1.0 if you go
  2.5 s without a kill, and **resets to ×1.0 on hull damage** (shield hits don't reset it).
- **Credits:** collected shards only (the magnet stat matters). Any shards you missed are shown on
  the results screen.
- **Medals (3 per stage, per difficulty):**
  1. **Ace:** reach the stage's score threshold.
  2. **Untouched:** finish without any **hull** damage (shield damage allowed).
  3. **Purge:** destroy ≥ 90% of the stage's enemies.
- Each medal pays a **one-time credit bonus** (stage-dependent, ~150–400) the first time it's earned.
  Medal totals show on the world map. **[DECIDE]** Should medals also unlock anything (e.g. pod
  colour schemes, or Veteran early)?

---

## 8. Controls

| Action | PC (mouse) | PC (keyboard) | Gamepad | Touch |
|---|---|---|---|---|
| Move | Ship follows the cursor (capped at the thruster speed) | WASD / arrows | Left stick | **Relative drag:** the ship moves by your finger's movement, offset so your finger doesn't cover it |
| Precision (half speed) | Hold right button | Hold Shift | Hold LT | — (a slow drag is already precise) |
| Core | Left click / Space | Space | A / RT | Tap the core button (bottom corner) or with a second finger |
| Pause | Esc | Esc / P | Start | Pause button; also pauses when the app loses focus |

Firing is always automatic. Menus work with mouse, touch and gamepad from day one so a console
port stays possible.

---

## 9. Screens and flow

1. **Title:** Play, Settings, Credits, Quit (on PC).
2. **World map:** stage nodes along a path over a low-poly globe of Earth, showing medals per stage
   and the difficulty toggle.
3. **Hangar:** the Vanguard on a rotating platform with three slots (left wing, right wing, core).
   Tapping a slot opens its weapon list: owned and locked weapons, a stat preview and a **live
   test-fire preview** of the pattern. Upgrade buttons show their cost. There's an Airframe tab
   for stat upgrades. Launch button.
4. **Stage:** the HUD (score, combo, credits, hull and shield bars, core meter) and boss health bar.
5. **Results:** score, kills %, credits, medals earned, then Retry / Hangar / Next.
6. **Settings:** volume (music and sound effects), screen shake, screen flashes (on/off, for
   accessibility), bullet-contrast mode, touch sensitivity, resolution / fullscreen on PC.

---

## 10. Art, audio, tech

### 10.1 Art direction — dark sci-fi low poly
- Flat-shaded, faceted meshes **made in Blender** (decided 2026-11-01, replacing the original
  "built in code" plan). They're exported as FBX into `Assets/Skyforge/Resources/Models/`; the
  brief, names, sizes and export settings are in **docs/BLENDER.md**. Each ship part keeps a
  code-built fallback (`*Model.cs`) used until its Blender model exists. Terrain, bullets and
  particles stay code-built.
- **Palette:** gunmetal, slate and deep navy for ships and terrain; desaturated, darkened
  backgrounds so the action stands out. Emissive accents: **player cyan/white**, **enemy
  red/magenta**, **enemy bullets orange/pink**, **credits gold**, **repair orbs green**.
- **Player ship look (decided by Rob, 2026-11-01):** "faceted alloy" after the Kestrel concept:
  a light matte alloy delta interceptor with a teal canopy, graphite nacelles and small peach
  wingtip panels (brief in docs/BLENDER.md §8). Enemies keep the dark hull and red-magenta glow
  until Rob reviews the new ship.
- **Background (decided by Rob, 2026-11-01):** plain black with small, scattered stars that stream
  from the top of the screen to the bottom in three parallax layers, giving the sense of flying
  forward. This replaced the earlier scrolling terrain and ocean backdrops. The stage table's
  backdrop column is the planned theming; each stage can later add its own distant elements.
- URP bloom on emissive materials only; a little fog and parallax on the scrolling ground; simple
  particle sparks, explosions and debris chunks.
- **Readability rule:** nothing in the background may be as bright or saturated as enemy bullets.

### 10.2 Audio
- **Sound effects** synthesized in code at startup (sfxr-style: shots per weapon type, hits,
  explosions, pickups, warnings, menu clicks), then cached as AudioClips.
- **Music:** a synth soundtrack of one track per stage plus a boss track. I'll make generated
  synthwave loops in code for v1; that's quick and fits the style, but it won't match a composed
  score. **[DECIDE]** later whether to swap in royalty-free or commissioned tracks for release.

### 10.3 Technical plan
- **Unity 6000.6.4f1, URP**, the same editor and package versions as Banners of the Realm.
- **Simulation separate from presentation:** the shooter logic (movement, bullets, collisions,
  waves, scoring) is plain C# that runs on a fixed 60 Hz step and doesn't depend on Unity's
  physics. That means:
  - EditMode tests can play a stage headlessly (e.g. "an auto-pilot with weapon X clears 1-1",
    "the boss phases trigger at the right HP").
  - The same stage, inputs and seed give the same result, so replays or leaderboards stay possible
    later.
- **Performance budget** (phones and web are the bottleneck): ≤ 600 bullets, ≤ 80 enemies,
  GPU-instanced bullets and shards, pooled objects (no garbage generated per frame), 60 fps
  target, with a reduced quality tier for low-end phones.
- **Collision:** circles against circles and against capsules (for beams), using a uniform spatial
  grid.
- **Saves:** a JSON file in `persistentDataPath` (works on PC, mobile and web, which uses
  IndexedDB). It's versioned so later updates won't break saves.
- **Builds:** Windows (with an installer script like Banners), web and Android. iOS waits until
  a Mac with Xcode is available (decided).

---

## 11. Beyond v1 (roadmap, not committed)
- **World 2 — Orbital Siege:** satellite fields, a broken defence grid, asteroid debris; the boss
  is a space-station turret ring.
- **World 3 — The Veil:** the invaders' biomechanical world; the final boss is the Armada's mind.
- More wing weapons (Flak Burster, Plasma Orb…), more cores, more airframe upgrades.
- Endless mode, Steam achievements, leaderboards, cloud saves.

---

## 12. Milestones

| # | Milestone | You can play… |
|---|---|---|
| M0 | Project scaffold, scene, camera, ship flying, Pulse Repeater | Fly and shoot in an empty sky |
| M1 | Enemies, bullets, damage, shield and hull, stage timeline; stage 1-1 (done in v0.2.0) | A full first stage |
| M2 | All 8 wing weapons and the core specials, the hangar loadout screen | Try every loadout on 1-1 |
| M3 | Credits, shop, upgrades, results screen, medals, saves, world map | The full loop |
| M4 | Stages 1-2 to 1-5, the Tidebreaker boss, Veteran mode | All of World 1 |
| M5 | Audio, polish, settings, touch controls, mobile and web builds | v1 on all platforms |

---

## 13. Open questions for Rob ([DECIDE])
Answered (2026-11-01): **all four cores** in v1; failed runs **keep 50%** of credits; **no Mac**,
so iOS waits (Windows, web and Android first). Approved as written.

Still open:
1. **Medal rewards:** credits only, or cosmetic unlocks too?
2. **Music:** generated in code for now; real tracks before release?
