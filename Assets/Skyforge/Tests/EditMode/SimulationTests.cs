using NUnit.Framework;

namespace Skyforge.Simulation.Tests
{
    public class SimulationTests
    {
        static StageSim Quiet(Loadout loadout = null) =>
            new StageSim(new StageSetup { PracticeDrones = false, Loadout = loadout ?? Loadout.Starter() });

        static void Run(StageSim sim, PlayerInput input, float seconds)
        {
            int steps = (int)(seconds * Field.StepsPerSecond + 0.5f);
            for (int i = 0; i < steps; i++) sim.Step(input);
        }

        // ------------------------------------------------------------------ movement

        [Test]
        public void ShipMovesAtTopSpeedWithFullStick()
        {
            var sim = Quiet();
            var start = sim.Player.Pos;
            Run(sim, PlayerInput.Move(1f, 0f), 0.25f);
            Assert.AreEqual(start.X + sim.Player.MaxSpeed * 0.25f, sim.Player.Pos.X, 0.01f);
        }

        [Test]
        public void DiagonalInputIsNotFasterThanStraight()
        {
            var sim = Quiet();
            var start = sim.Player.Pos;
            Run(sim, PlayerInput.Move(1f, 1f), 0.25f);
            float moved = (sim.Player.Pos - start).Length;
            Assert.AreEqual(sim.Player.MaxSpeed * 0.25f, moved, 0.01f);
        }

        [Test]
        public void PrecisionHalvesSpeed()
        {
            var sim = Quiet();
            var start = sim.Player.Pos;
            var input = PlayerInput.Move(1f, 0f);
            input.Precision = true;
            Run(sim, input, 0.25f);
            Assert.AreEqual(start.X + sim.Player.MaxSpeed * 0.25f * StageSim.PrecisionFactor, sim.Player.Pos.X, 0.01f);
        }

        [Test]
        public void ShipStaysInsideTheField()
        {
            var sim = Quiet();
            Run(sim, PlayerInput.Move(-1f, -1f), 5f);
            Assert.AreEqual(-Field.HalfWidth + Field.ShipMarginX, sim.Player.Pos.X, 1e-4f);
            Assert.AreEqual(Field.ShipMinY, sim.Player.Pos.Y, 1e-4f);
            Run(sim, PlayerInput.Move(1f, 1f), 5f);
            Assert.AreEqual(Field.HalfWidth - Field.ShipMarginX, sim.Player.Pos.X, 1e-4f);
            Assert.AreEqual(Field.ShipMaxY, sim.Player.Pos.Y, 1e-4f);
        }

        [Test]
        public void TargetModeStopsOnTheTarget()
        {
            var sim = Quiet();
            var target = new Vec2(3f, -6f);
            Run(sim, PlayerInput.Towards(target), 2f);
            Assert.AreEqual(target.X, sim.Player.Pos.X, 1e-4f);
            Assert.AreEqual(target.Y, sim.Player.Pos.Y, 1e-4f);
        }

        [Test]
        public void TargetModeIsCappedAtTopSpeed()
        {
            var sim = Quiet();
            var start = sim.Player.Pos;
            sim.Step(PlayerInput.Towards(start + new Vec2(0f, 20f)));
            Assert.AreEqual(sim.Player.MaxSpeed * Field.Dt, sim.Player.Pos.Y - start.Y, 1e-4f);
        }

        [Test]
        public void DragFollowsTheFingerUpToTheCap()
        {
            var sim = Quiet();
            var start = sim.Player.Pos;
            sim.Step(PlayerInput.DragBy(new Vec2(0.1f, 0f)));
            Assert.AreEqual(start.X + 0.1f, sim.Player.Pos.X, 1e-4f, "small drags are followed exactly");
            var mid = sim.Player.Pos;
            sim.Step(PlayerInput.DragBy(new Vec2(5f, 0f)));
            float cap = sim.Player.MaxSpeed * Field.Dt * StageSim.DragSpeedFactor;
            Assert.AreEqual(mid.X + cap, sim.Player.Pos.X, 1e-4f, "big jumps are capped");
        }

        // ------------------------------------------------------------------ weapons

        [Test]
        public void PulseRepeaterFiresTenVolleysPerSecondPerWing()
        {
            var sim = Quiet();
            int left = 0, right = 0;
            for (int i = 0; i < Field.StepsPerSecond * 2; i++)
            {
                sim.Step(PlayerInput.Idle);
                foreach (var e in sim.Events)
                    if (e.Type == SimEventType.Fired) { if (e.Wing == Side.Left) left++; else right++; }
            }
            Assert.AreEqual(20, left, 1);
            Assert.AreEqual(20, right, 1);
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        [TestCase(5, 5)]
        public void PulseRepeaterStreamsPerLevel(int level, int streams)
        {
            var loadout = new Loadout { LeftWing = new Fitted(Weapons.PulseRepeater, level) };
            var sim = Quiet(loadout);
            sim.Step(PlayerInput.Idle); // left wing fires on the first step
            Assert.AreEqual(streams, sim.Shots.Count);
        }

        [Test]
        public void PulseRepeaterDpsRisesEveryLevelToAbout2Point3x()
        {
            var w = Weapons.PulseRepeater;
            Assert.AreEqual(20f, w.At(1).Dps, 0.01f);
            for (int lv = 2; lv <= w.MaxLevel; lv++)
                Assert.Greater(w.At(lv).Dps, w.At(lv - 1).Dps, "level " + lv);
            Assert.AreEqual(2.3f, w.At(5).Dps / w.At(1).Dps, 0.1f);
        }

        [Test]
        public void ShotsComeFromTheirOwnWing()
        {
            var sim = Quiet();
            Run(sim, PlayerInput.Idle, 0.2f);
            foreach (var s in sim.Shots)
            {
                if (s.Wing == Side.Left) Assert.Less(s.Pos.X, sim.Player.Pos.X);
                else Assert.Greater(s.Pos.X, sim.Player.Pos.X);
            }
        }

        [Test]
        public void ShotsLeavingTheTopAreRemoved()
        {
            var sim = Quiet();
            Run(sim, PlayerInput.Idle, 5f);
            // 34 u/s over ~28 u of field ≈ 0.8 s alive; 20 volleys/s → ~17 in flight, never thousands.
            Assert.Less(sim.Shots.Count, 25);
            foreach (var s in sim.Shots) Assert.LessOrEqual(s.Pos.Y, Field.HalfHeight + 1f);
        }

        [Test]
        public void EmptyWingDoesNotFire()
        {
            var sim = Quiet(new Loadout());
            Run(sim, PlayerInput.Idle, 1f);
            Assert.AreEqual(0, sim.Shots.Count);
        }

        // ------------------------------------------------------------------ practice drones

        [Test]
        public void ShootingPracticeDronesScores()
        {
            var sim = new StageSim(new StageSetup { Seed = 7 });
            // Sweep left and right under the drones for a while.
            for (int i = 0; i < Field.StepsPerSecond * 30; i++)
            {
                float x = (i / 90) % 2 == 0 ? 1f : -1f;
                sim.Step(PlayerInput.Move(x, 0f));
            }
            Assert.Greater(sim.Kills, 5);
            // Each kill scores the drone's 50 points times the combo (at least ×1.0).
            Assert.GreaterOrEqual(sim.Score, sim.Kills * (long)EnemyDefs.PracticeDrone.Score);
        }

        [Test]
        public void PracticeDronesAreCapped()
        {
            var sim = new StageSim(new StageSetup { Loadout = new Loadout() }); // unarmed: nothing dies
            for (int i = 0; i < Field.StepsPerSecond * 20; i++)
            {
                sim.Step(PlayerInput.Idle);
                Assert.LessOrEqual(sim.Enemies.Count, StageSim.PracticeDroneMax);
            }
        }

        // ------------------------------------------------------------------ determinism

        [Test]
        public void SameSeedAndInputsGiveTheSameStage()
        {
            var a = new StageSim(new StageSetup { Seed = 42 });
            var b = new StageSim(new StageSetup { Seed = 42 });
            for (int i = 0; i < Field.StepsPerSecond * 20; i++)
            {
                var input = PlayerInput.Move((i / 50) % 2 == 0 ? 1f : -1f, (i / 170) % 2 == 0 ? 0.3f : -0.3f);
                a.Step(input);
                b.Step(input);
                Assert.AreEqual(a.ComputeHash(), b.ComputeHash(), "diverged at step " + i);
            }
        }

        [Test]
        public void DifferentSeedsGiveDifferentStages()
        {
            var a = new StageSim(new StageSetup { Seed = 1 });
            var b = new StageSim(new StageSetup { Seed = 2 });
            for (int i = 0; i < Field.StepsPerSecond * 5; i++) { a.Step(PlayerInput.Idle); b.Step(PlayerInput.Idle); }
            Assert.AreNotEqual(a.ComputeHash(), b.ComputeHash());
        }
    }
}
