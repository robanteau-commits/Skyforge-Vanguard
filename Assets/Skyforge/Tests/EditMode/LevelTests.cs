using NUnit.Framework;

namespace Skyforge.Simulation.Tests
{
    /// <summary>What each weapon level adds (docs/DESIGN.md §4.2): one more projectile per level, and a twist at level 5.</summary>
    public class LevelTests
    {
        static StageSim Sim(WeaponDef w, int level, StageBuilder b = null) => new StageSim(new StageSetup
        {
            Stage = (b ?? new StageBuilder()).Repair(1000f).Build("t", "Test"),
            Loadout = new Loadout { LeftWing = new Fitted(w, level) },
        });

        /// <summary>Shots in the air after the left wing's first volley.</summary>
        static int FirstVolley(WeaponDef w, int level)
        {
            var sim = Sim(w, level);
            sim.Step(PlayerInput.Idle);
            return sim.Shots.Count;
        }

        [TestCase("pulseRepeater", 1, 1)]
        [TestCase("pulseRepeater", 5, 5)]
        [TestCase("scatterCannon", 1, 3)]
        [TestCase("scatterCannon", 2, 4)]
        [TestCase("scatterCannon", 5, 7)]
        [TestCase("lanceDriver", 1, 1)]
        [TestCase("lanceDriver", 2, 1)]
        [TestCase("lanceDriver", 3, 2)]
        [TestCase("lanceDriver", 4, 3)]
        [TestCase("seekerPods", 1, 1)]
        [TestCase("seekerPods", 4, 4)]
        [TestCase("seekerPods", 5, 5)]
        [TestCase("hornetRack", 1, 4)]
        [TestCase("hornetRack", 5, 8)]
        [TestCase("ricochetDisc", 1, 1)]
        [TestCase("ricochetDisc", 3, 3)]
        [TestCase("ricochetDisc", 5, 5)]
        public void EachLevelAddsAProjectile(string id, int level, int shots)
        {
            Assert.AreEqual(shots, FirstVolley(Weapons.ById(id), level));
        }

        [Test]
        public void LanceLevel2IsAThickerSpear()
        {
            Assert.Greater(Weapons.LanceDriver.At(2).ProjectileRadius, Weapons.LanceDriver.At(1).ProjectileRadius);
        }

        [TestCaseSource(nameof(Wing))]
        public void TotalDamageGrowsAboutTwoAndAHalfTimes(string id)
        {
            var w = Weapons.ById(id);
            if (w.Kind == WeaponKind.SunlineBeam || w.Kind == WeaponKind.ArcCoil || w.Kind == WeaponKind.ScatterCannon || w.Kind == WeaponKind.RicochetDisc)
                Assert.Pass("damage depends on how many targets it reaches");
            float ratio = w.At(5).Dps / w.At(1).Dps;
            Assert.That(ratio, Is.InRange(2.0f, 2.6f), id);
            for (int lv = 2; lv <= 5; lv++) Assert.Greater(w.At(lv).Dps, w.At(lv - 1).Dps, id + " level " + lv);
        }

        static readonly string[] Wing = { "pulseRepeater", "scatterCannon", "lanceDriver", "sunlineBeam", "seekerPods", "hornetRack", "arcCoil", "ricochetDisc" };

        // ------------------------------------------------------------------ level-5 twists

        static int CountShots(StageSim sim, ShotVisual visual)
        {
            int n = 0;
            foreach (var s in sim.Shots) if (s.Visual == visual) n++;
            return n;
        }

        [Test]
        public void PulseLevel5FiresAChargedBoltEveryFifthVolley()
        {
            var sim = Sim(Weapons.PulseRepeater, 5);
            int charged = 0;
            for (int i = 0; i < Field.StepsPerSecond; i++)
            {
                int before = CountShots(sim, ShotVisual.ChargedBolt);
                sim.Step(PlayerInput.Idle);
                if (CountShots(sim, ShotVisual.ChargedBolt) > before) charged++;
            }
            Assert.AreEqual(2, charged, "10 volleys a second → 2 charged bolts");
            Assert.AreEqual(0, CountShots(Sim(Weapons.PulseRepeater, 4), ShotVisual.ChargedBolt));
        }

        [Test]
        public void ScatterLevel5PelletsBurstIntoShrapnel()
        {
            var sim = Sim(Weapons.ScatterCannon, 5);
            sim.Step(PlayerInput.Idle);
            bool shrapnel = false;
            for (int i = 0; i < Field.StepsPerSecond && !shrapnel; i++)
            {
                sim.Step(PlayerInput.Idle);
                shrapnel = CountShots(sim, ShotVisual.Shrapnel) > 0;
            }
            Assert.IsTrue(shrapnel);
            var lv4 = Sim(Weapons.ScatterCannon, 4);
            for (int i = 0; i < Field.StepsPerSecond; i++) lv4.Step(PlayerInput.Idle);
            Assert.AreEqual(0, CountShots(lv4, ShotVisual.Shrapnel));
        }

        [Test]
        public void LanceLevel5FiresAGiantEveryThirdVolley()
        {
            var sim = Sim(Weapons.LanceDriver, 5);
            float biggest = 0f;
            for (int i = 0; i < Field.StepsPerSecond * 2; i++)
            {
                sim.Step(PlayerInput.Idle);
                foreach (var s in sim.Shots) if (s.Radius > biggest) biggest = s.Radius;
            }
            Assert.AreEqual(Weapons.LanceDriver.At(5).ProjectileRadius * 3f, biggest, 1e-4f);
        }

        [TestCase(1, 1)]
        [TestCase(3, 2)]
        [TestCase(5, 3)]
        public void SunlineStrandsPerLevel(int level, int strands)
        {
            var sim = Sim(Weapons.SunlineBeam, level);
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(strands, sim.Player.BeamStrands[0]);
        }

        [Test]
        public void SunlineLevel4BurnsThroughToASecondEnemy()
        {
            // Two Needles hovering in one column above the left wing (pod at X = -1); the far one is behind the near one.
            var b = new StageBuilder().Needle(0f, -1f, 6f).Needle(0f, -1f, 11f);
            bool farBurned = false;
            for (int level = 3; level <= 4; level++)
            {
                var sim = Sim(Weapons.SunlineBeam, level, b);
                farBurned = false;
                for (int i = 0; i < Field.StepsPerSecond * 3; i++)
                {
                    sim.Step(PlayerInput.Idle);
                    // Only while the near one is still alive and blocking the column.
                    bool nearAlive = sim.Enemies.Exists(e => e.Target.Y < 7f && !e.Dead);
                    foreach (var e in sim.Enemies)
                        if (nearAlive && e.Target.Y > 10f && e.Hp < e.MaxHp) farBurned = true;
                }
                if (level == 3) Assert.IsFalse(farBurned, "level 3 stops at the first enemy");
            }
            Assert.IsTrue(farBurned, "level 4 burns the one behind too");
        }

        [Test]
        public void SunlineLevel5PulsesAtFullPower()
        {
            var sim = Sim(Weapons.SunlineBeam, 5, new StageBuilder().Warden(0f, -1f));
            int pulses = 0;
            for (int i = 0; i < Field.StepsPerSecond * 10; i++)
            {
                sim.Step(PlayerInput.Idle);
                foreach (var e in sim.Events) if (e.Type == SimEventType.BeamPulse) pulses++;
            }
            Assert.Greater(pulses, 0);
        }

        [Test]
        public void SeekerLevel5BlastsScatterBomblets()
        {
            var sim = Sim(Weapons.SeekerPods, 5, new StageBuilder().Warden(0f, 0f));
            bool bomblets = false;
            for (int i = 0; i < Field.StepsPerSecond * 10 && !bomblets; i++)
            {
                sim.Step(PlayerInput.Idle);
                bomblets = CountShots(sim, ShotVisual.Bomblet) > 0;
            }
            Assert.IsTrue(bomblets);
        }

        [Test]
        public void HornetLevel5DartsLiveLongerAndTurnHarder()
        {
            Assert.Greater(Weapons.HornetRack.At(5).Life, Weapons.HornetRack.At(4).Life);
            Assert.Greater(Weapons.HornetRack.At(5).TurnRate, Weapons.HornetRack.At(4).TurnRate);
        }

        [Test]
        public void ArcLevel5ForksIntoMoreStrikes()
        {
            int Strikes(int level)
            {
                var sim = Sim(Weapons.ArcCoil, level, new StageBuilder().V(0f, -1f, 2.5f).V(0.2f, -1f, 2.5f));
                int best = 0;
                for (int i = 0; i < Field.StepsPerSecond * 8; i++)
                {
                    sim.Step(PlayerInput.Idle);
                    int n = 0;
                    foreach (var e in sim.Events) if (e.Type == SimEventType.Arc) n++;
                    if (n > best) best = n;
                }
                return best;
            }
            Assert.LessOrEqual(Strikes(1), 2, "level 1: a strike and one jump");
            Assert.Greater(Strikes(5), Strikes(4), "forks reach more targets");
        }

        [Test]
        public void RicochetLevel5DiscsSplitOnTheirLastHit()
        {
            var sim = Sim(Weapons.RicochetDisc, 5, new StageBuilder().Warden(0f, 4f));
            for (int i = 0; i < Field.StepsPerSecond * 4; i++) sim.Step(PlayerInput.Idle);
            var warden = sim.Enemies.Find(e => e.Kind == EnemyKind.Warden);
            Assert.IsNotNull(warden);
            sim.Shots.Clear();
            sim.Shots.Add(new Shot { Pos = warden.Pos, Vel = new Vec2(0f, 15f), Damage = 1f, Radius = 0.35f, Visual = ShotVisual.Disc, HitsLeft = 1, Split = true, Bounce = true, Life = 4f });
            sim.Step(PlayerInput.Idle);
            int small = sim.Shots.FindAll(s => s.Visual == ShotVisual.Disc && s.Radius < 0.3f).Count;
            Assert.AreEqual(2, small);
        }
    }
}
