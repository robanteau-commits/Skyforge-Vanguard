using NUnit.Framework;

namespace Skyforge.Simulation.Tests
{
    /// <summary>The wide (PC/web) field: 24 units across instead of 18, waves spread to fill it.</summary>
    public class FieldTests
    {
        static StageSim Wide(StageBuilder b = null, Loadout loadout = null) => new StageSim(new StageSetup
        {
            Stage = (b ?? new StageBuilder()).Repair(1000f).Build("t", "Test"),
            Loadout = loadout ?? new Loadout(),
            FieldWidth = Field.WideWidth,
        });

        static void Run(StageSim sim, float seconds, PlayerInput input = default)
        {
            int steps = (int)(seconds * Field.StepsPerSecond + 0.5f);
            for (int i = 0; i < steps; i++) sim.Step(input);
        }

        [Test]
        public void ShipReachesFurtherOnAWideField()
        {
            var sim = Wide();
            Run(sim, 3f, PlayerInput.Move(1f, 0f));
            Assert.AreEqual(Field.WideWidth / 2f - Field.ShipMarginX, sim.Player.Pos.X, 1e-4f);
        }

        [Test]
        public void WavesSpreadAcrossAWideField()
        {
            var sim = Wide(new StageBuilder().Needle(0f, 6f));
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(6f * Field.WideWidth / Field.Width, sim.Enemies[0].Pos.X, 1e-3f);
        }

        [Test]
        public void SweepsEnterFromTheWideEdges()
        {
            var sim = Wide(new StageBuilder().Gnats(0f, GnatPath.SweepRight, 1, y: 12f));
            sim.Step(PlayerInput.Idle);
            Assert.Less(sim.Enemies[0].Pos.X, -Field.WideWidth / 2f);
            Run(sim, 8f);
            Assert.AreEqual(0, sim.Enemies.Count, "it crossed the whole field and left");
        }

        [Test]
        public void DiscsBounceOffTheWideEdges()
        {
            var sim = Wide(null, new Loadout { RightWing = new Fitted(Weapons.RicochetDisc, 1) });
            float maxX = 0f;
            for (int i = 0; i < Field.StepsPerSecond * 3; i++)
            {
                sim.Step(PlayerInput.Idle);
                foreach (var s in sim.Shots) if (s.Pos.X > maxX) maxX = s.Pos.X;
            }
            Assert.Greater(maxX, Field.HalfWidth + 1f, "past where the portrait edge would be");
            Assert.LessOrEqual(maxX, Field.WideWidth / 2f + 0.5f);
        }

        [Test]
        public void FirstLightRunsOnTheWideField()
        {
            var sim = new StageSim(new StageSetup { Stage = Stages.FirstLight, FieldWidth = Field.WideWidth, Seed = 2 });
            for (int i = 0; i < Field.StepsPerSecond * 170 && sim.Status == StageStatus.Playing; i++)
                sim.Step(PlayerInput.Move((i / 150) % 2 == 0 ? 1f : -1f, 0f));
            Assert.AreEqual(StageStatus.Cleared, sim.Status);
        }
    }
}
