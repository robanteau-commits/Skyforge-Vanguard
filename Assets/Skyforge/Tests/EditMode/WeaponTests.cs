using NUnit.Framework;

namespace Skyforge.Simulation.Tests
{
    /// <summary>M2: the eight wing weapons and the four core specials.</summary>
    public class WeaponTests
    {
        static Loadout RightOnly(WeaponDef w, int level = 1, WeaponDef core = null) =>
            new Loadout { RightWing = new Fitted(w, level), Core = core == null ? default : new Fitted(core, 1) };

        static StageSim With(StageBuilder b, Loadout loadout, uint seed = 1) =>
            new StageSim(new StageSetup { Stage = b.Repair(1000f).Build("t", "Test"), Loadout = loadout, Seed = seed });

        /// <summary>Step until the first volley is in the air (the right wing fires half a beat late).</summary>
        static void FirstVolley(StageSim sim)
        {
            for (int i = 0; i < Field.StepsPerSecond * 2 && sim.Shots.Count == 0; i++) sim.Step(PlayerInput.Idle);
        }

        static void Run(StageSim sim, float seconds, PlayerInput input = default)
        {
            int steps = (int)(seconds * Field.StepsPerSecond + 0.5f);
            for (int i = 0; i < steps; i++) sim.Step(input);
        }

        /// <summary>Gnats diving down two lanes, into and beside the right wing's line of fire.</summary>
        static StageBuilder GnatLanes() =>
            new StageBuilder().Gnats(0.5f, GnatPath.Dive, 5, x: 1f, delay: 0.8f).Gnats(0.9f, GnatPath.Dive, 5, x: 4f, delay: 0.8f);

        // ------------------------------------------------------------------ every wing weapon

        static readonly string[] WingIds =
            { "pulseRepeater", "scatterCannon", "lanceDriver", "sunlineBeam", "seekerPods", "hornetRack", "arcCoil", "ricochetDisc" };

        [TestCaseSource(nameof(WingIds))]
        public void EveryWingWeaponKillsGnats(string id)
        {
            var sim = With(GnatLanes(), RightOnly(Weapons.ById(id)));
            Run(sim, 10f);
            Assert.GreaterOrEqual(sim.Kills, 3, id);
        }

        [TestCaseSource(nameof(WingIds))]
        public void HigherLevelsAreStronger(string id)
        {
            var w = Weapons.ById(id);
            int kills1 = 0, kills5 = 0;
            for (int lv = 1; lv <= 5; lv += 4)
            {
                var sim = new StageSim(new StageSetup { Stage = Stages.ArmadaSampler, Loadout = new Loadout { LeftWing = new Fitted(w, lv), RightWing = new Fitted(w, lv) }, Seed = 5 });
                Run(sim, 60f, PlayerInput.Towards(new Vec2(0f, -11f)));
                if (lv == 1) kills1 = sim.Kills; else kills5 = sim.Kills;
            }
            Assert.GreaterOrEqual(kills5, kills1, id);
        }

        [Test]
        public void ScatterCannonFiresAFan()
        {
            var sim = With(new StageBuilder(), RightOnly(Weapons.ScatterCannon, 5));
            FirstVolley(sim);
            Assert.AreEqual(Weapons.ScatterCannon.At(5).Count, sim.Shots.Count);
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var s in sim.Shots) { minX = System.Math.Min(minX, s.Vel.X); maxX = System.Math.Max(maxX, s.Vel.X); }
            Assert.Less(minX, 0f);
            Assert.Greater(maxX, 0f);
        }

        [Test]
        public void ScatterPelletsHaveLimitedRange()
        {
            var sim = With(new StageBuilder(), RightOnly(Weapons.ScatterCannon));
            FirstVolley(sim);
            var startY = sim.Shots[0].Pos.Y;
            Run(sim, 2f);
            foreach (var s in sim.Shots) Assert.Less(s.Pos.Y - startY, 26f * 0.8f);
        }

        [Test]
        public void LanceBoltsPierceThroughEnemies()
        {
            var sim = With(new StageBuilder().Gnats(0f, GnatPath.Dive, 4, x: 1f, delay: 0.12f), RightOnly(Weapons.LanceDriver));
            for (int i = 0; i < Field.StepsPerSecond * 6; i++)
            {
                sim.Step(PlayerInput.Idle);
                foreach (var e in sim.Events)
                {
                    if (e.Type != SimEventType.Destroyed) continue;
                    bool boltStillFlying = false;
                    foreach (var s in sim.Shots) if (s.Visual == ShotVisual.LanceBolt && s.LastHit == e.EnemyId) boltStillFlying = true;
                    Assert.IsTrue(boltStillFlying, "the bolt carries on after a kill");
                    return;
                }
            }
            Assert.Fail("no kill");
        }

        [Test]
        public void SunlineBeamDamageRampsUpOnOneTarget()
        {
            var sim = With(new StageBuilder().Warden(0f, 1f), RightOnly(Weapons.SunlineBeam));
            // Wait for the Warden to reach its station (and the beam to find it).
            for (int i = 0; i < Field.StepsPerSecond * 10 && !sim.Player.BeamOn[1] || sim.Player.BeamTarget[1] == 0; i++) sim.Step(PlayerInput.Idle);
            var w = sim.Enemies.Find(e => e.Kind == EnemyKind.Warden);
            Assert.IsNotNull(w);
            float hp0 = w.Hp;
            Run(sim, 0.25f);
            float early = hp0 - w.Hp;
            Run(sim, 1.5f);
            float hp1 = w.Hp;
            Run(sim, 0.25f);
            float late = hp1 - w.Hp;
            Assert.Greater(late, early * 1.4f, "damage per second rises the longer the beam stays on");
            Assert.Less(sim.Player.BeamEndY[1], Field.HalfHeight, "the beam stops at its target");
        }

        [Test]
        public void SeekersHomeOnEnemiesOffToTheSide()
        {
            var sim = With(new StageBuilder().Gnats(0.5f, GnatPath.Dive, 3, x: -6f, delay: 0.8f), RightOnly(Weapons.SeekerPods));
            Run(sim, 8f);
            Assert.GreaterOrEqual(sim.Kills, 2);
        }

        [Test]
        public void HornetRackLaunchesASalvo()
        {
            var sim = With(new StageBuilder(), RightOnly(Weapons.HornetRack, 5));
            FirstVolley(sim);
            Assert.AreEqual(8, sim.Shots.Count);
            foreach (var s in sim.Shots) Assert.AreEqual(ShotVisual.MicroMissile, s.Visual);
        }

        [Test]
        public void ArcCoilJumpsBetweenEnemies()
        {
            var sim = With(new StageBuilder().V(0f, 1f, speed: 3f), RightOnly(Weapons.ArcCoil, 5));
            int maxArcs = 0;
            for (int i = 0; i < Field.StepsPerSecond * 8; i++)
            {
                sim.Step(PlayerInput.Idle);
                int arcs = 0;
                foreach (var e in sim.Events) if (e.Type == SimEventType.Arc) arcs++;
                if (arcs > maxArcs) maxArcs = arcs;
            }
            Assert.GreaterOrEqual(maxArcs, 3, "one strike chained to at least two more");
        }

        [Test]
        public void ArcCoilNeedsATargetInRange()
        {
            var sim = With(new StageBuilder(), RightOnly(Weapons.ArcCoil));
            int arcs = 0;
            for (int i = 0; i < Field.StepsPerSecond * 2; i++)
            {
                sim.Step(PlayerInput.Idle);
                foreach (var e in sim.Events) if (e.Type == SimEventType.Arc) arcs++;
            }
            Assert.AreEqual(0, arcs);
        }

        [Test]
        public void RicochetDiscsBounceOffTheSides()
        {
            var sim = With(new StageBuilder(), RightOnly(Weapons.RicochetDisc));
            FirstVolley(sim);
            Assert.Greater(sim.Shots[0].Vel.X, 0f, "the right wing throws to the right");
            bool bounced = false;
            for (int i = 0; i < Field.StepsPerSecond * 2 && !bounced; i++)
            {
                sim.Step(PlayerInput.Idle);
                foreach (var s in sim.Shots) if (s.Visual == ShotVisual.Disc && s.Vel.X < 0f) bounced = true;
            }
            Assert.IsTrue(bounced);
        }

        // ------------------------------------------------------------------ cores

        [Test]
        public void CoreChargesOverTimeAndWithKills()
        {
            var sim = With(new StageBuilder(), new Loadout { Core = new Fitted(Weapons.PulseNova, 1) });
            Run(sim, 10f);
            Assert.AreEqual(20f, sim.Player.CoreCharge, 0.2f);
            Run(sim, 60f);
            Assert.AreEqual(PlayerShip.CoreChargeMax, sim.Player.CoreCharge, 0.01f);
        }

        [Test]
        public void CoreWontFireUntilFull()
        {
            var sim = With(new StageBuilder(), new Loadout { Core = new Fitted(Weapons.PulseNova, 1) });
            sim.EnemyShots.Add(new EnemyShot { Pos = new Vec2(0f, 10f), Vel = Vec2.Zero, Radius = 0.2f, Damage = 10f });
            sim.Step(new PlayerInput { Core = true });
            Assert.AreEqual(1, sim.EnemyShots.Count);
        }

        [Test]
        public void PulseNovaClearsBulletsAndDamagesEverything()
        {
            var sim = With(new StageBuilder().Warden(0f, 0f), new Loadout { Core = new Fitted(Weapons.PulseNova, 1) });
            Run(sim, 6f);
            var w = sim.Enemies.Find(e => e.Kind == EnemyKind.Warden);
            float hp = w.Hp;
            Assert.Greater(sim.EnemyShots.Count, 0);
            sim.Player.CoreCharge = PlayerShip.CoreChargeMax;
            sim.Step(new PlayerInput { Core = true });
            Assert.AreEqual(0, sim.EnemyShots.Count);
            Assert.AreEqual(hp - Weapons.PulseNova.At(1).Damage, w.Hp, 0.5f);
            Assert.AreEqual(0f, sim.Player.CoreCharge);
            Assert.Greater(sim.Player.Invulnerable, 0.9f);
        }

        [Test]
        public void SolarLanceBurnsWhatsAheadAndCancelsBullets()
        {
            var sim = With(new StageBuilder().Warden(0f, 0f), new Loadout { Core = new Fitted(Weapons.SolarLance, 1) });
            Run(sim, 4f);
            sim.Player.CoreCharge = PlayerShip.CoreChargeMax;
            sim.Step(new PlayerInput { Core = true });
            Assert.IsTrue(sim.SolarLanceUp);
            sim.EnemyShots.Add(new EnemyShot { Pos = sim.Player.Pos + new Vec2(0.5f, 6f), Vel = Vec2.Zero, Radius = 0.2f, Damage = 10f });
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(0, sim.EnemyShots.FindAll(s => s.Pos.Y > sim.Player.Pos.Y + 5f && System.Math.Abs(s.Pos.X - sim.Player.Pos.X) < 1f).Count);
            Run(sim, 1.5f);
            Assert.IsNull(sim.Enemies.Find(e => e.Kind == EnemyKind.Warden), "250/s for 1.5 s melts a 160 HP Warden");
        }

        [Test]
        public void AegisFieldAbsorbsHitsAndSlowsBullets()
        {
            var sim = With(new StageBuilder(), new Loadout { Core = new Fitted(Weapons.AegisField, 1) });
            sim.Player.CoreCharge = PlayerShip.CoreChargeMax;
            sim.Step(new PlayerInput { Core = true });
            Assert.IsTrue(sim.AegisUp);
            for (int i = 0; i < 5; i++)
            {
                sim.EnemyShots.Add(new EnemyShot { Pos = sim.Player.Pos, Vel = Vec2.Zero, Radius = 0.2f, Damage = 10f });
                sim.Step(PlayerInput.Idle);
            }
            Assert.AreEqual(sim.Player.MaxShield, sim.Player.Shield, 0.01f);
            Assert.AreEqual(sim.Player.MaxHull, sim.Player.Hull, 0.01f);
            var start = sim.Player.Pos + new Vec2(0f, 4f);
            sim.EnemyShots.Add(new EnemyShot { Pos = start, Vel = new Vec2(0f, -6f), Radius = 0.2f, Damage = 10f });
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(6f * Field.Dt * Weapons.AegisField.At(1).Factor, start.Y - sim.EnemyShots[0].Pos.Y, 1e-4f);
            Run(sim, Weapons.AegisField.At(1).Duration);
            Assert.IsFalse(sim.AegisUp, "it runs out");
        }

        [Test]
        public void WingmatesFireAlongsideAndThenLeave()
        {
            var sim = With(new StageBuilder(), new Loadout
            {
                LeftWing = new Fitted(Weapons.PulseRepeater, 1),
                RightWing = new Fitted(Weapons.PulseRepeater, 1),
                Core = new Fitted(Weapons.Wingmates, 1),
            });
            sim.Player.CoreCharge = PlayerShip.CoreChargeMax;
            sim.Step(new PlayerInput { Core = true });
            Assert.AreEqual(2, sim.Player.WingmateCount);
            int farShots = 0;
            for (int i = 0; i < Field.StepsPerSecond; i++)
            {
                sim.Step(PlayerInput.Idle);
                foreach (var e in sim.Events)
                    if (e.Type == SimEventType.Fired && System.Math.Abs(e.Pos.X - sim.Player.Pos.X) > 2f) farShots++;
            }
            Assert.Greater(farShots, 15, "both drones fire about 10 volleys a second");
            Run(sim, Weapons.Wingmates.At(1).Duration);
            Assert.AreEqual(0, sim.Player.WingmateCount);
        }

        [Test]
        public void CoreDoesNotChargeWhileItsEffectRuns()
        {
            var sim = With(new StageBuilder(), new Loadout { Core = new Fitted(Weapons.AegisField, 1) });
            sim.Player.CoreCharge = PlayerShip.CoreChargeMax;
            sim.Step(new PlayerInput { Core = true });
            Run(sim, 3f);
            Assert.AreEqual(0f, sim.Player.CoreCharge);
        }

        [Test]
        public void StagesWithEveryWeaponStayDeterministic()
        {
            foreach (var w in Weapons.WingWeapons)
            {
                var loadout = new Loadout { LeftWing = new Fitted(w, 3), RightWing = new Fitted(w, 3), Core = new Fitted(Weapons.Wingmates, 1) };
                var a = new StageSim(new StageSetup { Stage = Stages.ArmadaSampler, Loadout = loadout, Seed = 4 });
                var b = new StageSim(new StageSetup { Stage = Stages.ArmadaSampler, Loadout = loadout, Seed = 4 });
                for (int i = 0; i < Field.StepsPerSecond * 30; i++)
                {
                    var input = new PlayerInput { Mode = MoveMode.Direction, Direction = new Vec2((i / 90) % 2 == 0 ? 1f : -1f, 0f), Core = i % 600 == 599 };
                    a.Step(input);
                    b.Step(input);
                }
                Assert.AreEqual(a.ComputeHash(), b.ComputeHash(), w.Id);
            }
        }
    }
}
