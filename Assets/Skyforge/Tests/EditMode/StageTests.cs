using NUnit.Framework;

namespace Skyforge.Simulation.Tests
{
    /// <summary>M1: enemies, enemy fire, damage, shield, combo, pickups, stage end.</summary>
    public class StageTests
    {
        static readonly Loadout Unarmed = new Loadout();

        /// <summary>A quiet stage: no practice drones and no enemies. Its one (far-off) order keeps it
        /// from counting as cleared, which would pull every pickup to the ship.</summary>
        static StageSim Empty(Loadout loadout = null) =>
            new StageSim(new StageSetup { Stage = new StageBuilder().Repair(1000f).Build("t", "Test"), Loadout = loadout ?? Unarmed });

        static StageSim With(StageBuilder b, Loadout loadout = null, uint seed = 1) =>
            new StageSim(new StageSetup { Stage = b.Build("t", "Test"), Loadout = loadout ?? Unarmed, Seed = seed });

        static void Run(StageSim sim, float seconds, PlayerInput input = default)
        {
            int steps = (int)(seconds * Field.StepsPerSecond + 0.5f);
            for (int i = 0; i < steps; i++) sim.Step(input);
        }

        static int CountEvents(StageSim sim, float seconds, SimEventType type)
        {
            int n = 0, steps = (int)(seconds * Field.StepsPerSecond + 0.5f);
            for (int i = 0; i < steps; i++)
            {
                sim.Step(PlayerInput.Idle);
                foreach (var e in sim.Events) if (e.Type == type) n++;
            }
            return n;
        }

        static void ShootPlayer(StageSim sim, float damage = 10f) =>
            sim.EnemyShots.Add(new EnemyShot { Pos = sim.Player.Pos, Vel = Vec2.Zero, Radius = 0.2f, Damage = damage });

        // ------------------------------------------------------------------ damage

        [Test]
        public void ShieldSoaksDamageBeforeTheHull()
        {
            var sim = Empty();
            ShootPlayer(sim);
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(30f, sim.Player.Shield, 0.01f);
            Assert.AreEqual(100f, sim.Player.Hull, 0.01f);
            Assert.AreEqual(0f, sim.HullDamageTaken);
            Assert.AreEqual(0, sim.EnemyShots.Count, "the bullet is used up");
        }

        [Test]
        public void DamageBeyondTheShieldHitsTheHull()
        {
            var sim = Empty();
            sim.Player.Shield = 4f;
            sim.Player.ShieldRegenWait = 10f; // no regeneration this step
            ShootPlayer(sim, 10f);
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(0f, sim.Player.Shield, 0.01f);
            Assert.AreEqual(94f, sim.Player.Hull, 0.01f);
            Assert.AreEqual(6f, sim.HullDamageTaken, 0.01f);
        }

        [Test]
        public void AHitGivesBriefInvulnerability()
        {
            var sim = Empty();
            ShootPlayer(sim);
            sim.Step(PlayerInput.Idle);
            ShootPlayer(sim);
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(30f, sim.Player.Shield, 0.01f, "second bullet passes through");
            Assert.AreEqual(1, sim.EnemyShots.Count);
            Run(sim, PlayerShip.HitInvulnerability);
            sim.EnemyShots.Clear();
            ShootPlayer(sim);
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(20f, sim.Player.Shield, 0.01f, "hurt again once invulnerability ends");
        }

        [Test]
        public void ShieldRegeneratesOnlyAfterTheDelay()
        {
            var sim = Empty();
            ShootPlayer(sim);
            sim.Step(PlayerInput.Idle);
            Run(sim, sim.Player.ShieldRegenDelay - 0.1f);
            Assert.AreEqual(30f, sim.Player.Shield, 0.01f);
            Run(sim, 0.6f);
            Assert.Greater(sim.Player.Shield, 30f);
            Run(sim, 2f);
            Assert.AreEqual(sim.Player.MaxShield, sim.Player.Shield, 0.01f);
        }

        [Test]
        public void EmptyHullDestroysTheShipAndFailsTheStage()
        {
            var sim = Empty();
            sim.Player.Shield = 0f;
            sim.Player.Hull = 5f;
            ShootPlayer(sim);
            sim.Step(PlayerInput.Idle);
            Assert.IsFalse(sim.Player.Alive);
            Assert.AreEqual(StageStatus.Failed, sim.Status);
            var pos = sim.Player.Pos;
            sim.Step(PlayerInput.Move(1f, 0f));
            Assert.AreEqual(pos, sim.Player.Pos, "a destroyed ship doesn't move");
        }

        [Test]
        public void AFailedRunBanksHalfTheCredits()
        {
            var sim = Empty();
            sim.Pickups.Add(new Pickup { Kind = PickupKind.Credit, Pos = sim.Player.Pos, Value = 41 });
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(41, sim.Credits);
            sim.Player.Shield = 0f;
            sim.Player.Hull = 1f;
            ShootPlayer(sim);
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(StageStatus.Failed, sim.Status);
            Assert.AreEqual(20, sim.CreditsBanked);
        }

        // ------------------------------------------------------------------ pickups

        [Test]
        public void MagnetPullsInNearbyShardsOnly()
        {
            var sim = Empty();
            var p = sim.Player.Pos;
            sim.Pickups.Add(new Pickup { Id = 1, Kind = PickupKind.Credit, Pos = p + new Vec2(2f, 0f), Value = 5 });
            sim.Pickups.Add(new Pickup { Id = 2, Kind = PickupKind.Credit, Pos = p + new Vec2(6f, 4f), Value = 7 });
            Run(sim, 0.5f);
            Assert.AreEqual(5, sim.Credits);
            Assert.AreEqual(1, sim.Pickups.Count, "the far shard keeps drifting");
            Run(sim, 6f);
            Assert.AreEqual(0, sim.Pickups.Count, "it drifted off the bottom");
            Assert.AreEqual(5, sim.Credits);
        }

        [Test]
        public void RepairOrbsMendTheHullUpToMax()
        {
            var sim = Empty();
            sim.Player.Hull = 90f;
            sim.Pickups.Add(new Pickup { Kind = PickupKind.Repair, Pos = sim.Player.Pos, Value = StageSim.RepairAmount });
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(sim.Player.MaxHull, sim.Player.Hull, 0.01f);
        }

        // ------------------------------------------------------------------ combo and score

        /// <summary>Gnats diving straight into the right wing's stream (pod at X = +1).</summary>
        static StageSim GnatsIntoTheGuns(int count, float delay = 0.6f) =>
            With(new StageBuilder().Gnats(0.5f, GnatPath.Dive, count, x: 1f, delay: delay), Loadout.Starter());

        [Test]
        public void EachKillRaisesTheComboAndScoresWithIt()
        {
            var sim = GnatsIntoTheGuns(3);
            var points = new System.Collections.Generic.List<float>();
            for (int i = 0; i < Field.StepsPerSecond * 6 && points.Count < 3; i++)
            {
                sim.Step(PlayerInput.Idle);
                foreach (var e in sim.Events) if (e.Type == SimEventType.Destroyed) points.Add(e.Value);
            }
            Assert.AreEqual(3, points.Count);
            Assert.AreEqual(50f, points[0]);  // ×1.0
            Assert.AreEqual(55f, points[1]);  // ×1.1
            Assert.AreEqual(60f, points[2]);  // ×1.2
            Assert.AreEqual(13, sim.ComboTenths);
        }

        [Test]
        public void ComboFallsBackWithoutKills()
        {
            var sim = GnatsIntoTheGuns(3, delay: 0.3f);
            Run(sim, 4f);
            Assert.AreEqual(13, sim.ComboTenths);
            Run(sim, StageSim.ComboDecaySeconds + 0.1f);
            Assert.AreEqual(10, sim.ComboTenths, "drops by 1.0, not below ×1.0");
        }

        [Test]
        public void HullDamageResetsTheCombo()
        {
            var sim = GnatsIntoTheGuns(3, delay: 0.3f);
            Run(sim, 3f);
            Assert.Greater(sim.ComboTenths, 10);
            sim.Player.Shield = 0f;
            ShootPlayer(sim);
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(10, sim.ComboTenths);
        }

        [Test]
        public void KillsDropCreditShardsThatTheMagnetCollects()
        {
            var sim = GnatsIntoTheGuns(3);
            Run(sim, 10f);
            Assert.AreEqual(3, sim.Kills);
            // The shards drift down onto the ship (it sits right under the kills): 3 Gnats × 2 credits.
            Assert.AreEqual(3 * EnemyDefs.Gnat.Credits, sim.Credits);
        }

        // ------------------------------------------------------------------ enemies

        [Test]
        public void NeedleFiresAThreeShotBurst()
        {
            var sim = With(new StageBuilder().Needle(0f, 5f, 9f));
            Assert.AreEqual(3, CountEvents(sim, 6f, SimEventType.EnemyFired));
        }

        [Test]
        public void NeedleWindsUpBeforeFiring()
        {
            var sim = With(new StageBuilder().Needle(0f, 5f, 9f));
            float maxTelegraph = 0f;
            for (int i = 0; i < Field.StepsPerSecond * 6; i++)
            {
                sim.Step(PlayerInput.Idle);
                bool fired = false;
                foreach (var e in sim.Events) if (e.Type == SimEventType.EnemyFired) fired = true;
                if (fired) break;
                foreach (var e in sim.Enemies) if (e.Telegraph > maxTelegraph) maxTelegraph = e.Telegraph;
            }
            Assert.Greater(maxTelegraph, 0.8f);
        }

        [Test]
        public void RamDroneChargesTheShip()
        {
            var sim = With(new StageBuilder().Ram(0f, 0f));
            Run(sim, StageSim.RamLockTime + 0.1f);
            var ram = sim.Enemies[0];
            Assert.AreEqual(1, ram.Mode, "locked on");
            Assert.AreEqual(StageSim.RamSpeed, ram.Vel.Length, 0.01f);
            Assert.Less(ram.Vel.Y, 0f);
            Run(sim, 3f);
            Assert.Less(sim.Player.Shield + sim.Player.Hull, 140f, "it hit");
        }

        [Test]
        public void MineBurstsWhenTheShipComesClose()
        {
            var sim = With(new StageBuilder().Mine(0f, 0f));
            int bursts = CountEvents(sim, 12f, SimEventType.MineBurst);
            Assert.AreEqual(1, bursts);
            Assert.AreEqual(0, sim.Kills, "a proximity burst isn't a kill");
        }

        [Test]
        public void ShootingAMineAlsoBurstsIt()
        {
            var sim = With(new StageBuilder().Mine(0f, 1f), Loadout.Starter());
            int bursts = CountEvents(sim, 6f, SimEventType.MineBurst);
            Assert.AreEqual(1, bursts);
            Assert.AreEqual(1, sim.Kills);
        }

        [Test]
        public void WardenAlternatesRingsAndFans()
        {
            var sim = With(new StageBuilder().Warden(0f, 0f));
            var volleys = new System.Collections.Generic.List<int>();
            int before = 0;
            for (int i = 0; i < Field.StepsPerSecond * 14 && volleys.Count < 2; i++)
            {
                before = sim.EnemyShots.Count;
                sim.Step(PlayerInput.Idle);
                foreach (var e in sim.Events)
                    if (e.Type == SimEventType.EnemyFired && e.EnemyKind == EnemyKind.Warden) volleys.Add(sim.EnemyShots.Count - before);
            }
            Assert.AreEqual(2, volleys.Count);
            Assert.AreEqual(12, volleys[0], "ring");
            Assert.AreEqual(5, volleys[1], "fan");
        }

        [Test]
        public void BastionArrivesOnItsAsteroidAndFiresBursts()
        {
            var sim = With(new StageBuilder().Bastion(0f, 4f));
            int spawned = 0, shots = 0;
            for (int i = 0; i < Field.StepsPerSecond * 8; i++)
            {
                sim.Step(PlayerInput.Idle);
                foreach (var e in sim.Events)
                {
                    if (e.Type == SimEventType.BastionSpawned) spawned++;
                    if (e.Type == SimEventType.EnemyFired) shots++;
                }
            }
            Assert.AreEqual(1, spawned);
            Assert.GreaterOrEqual(shots, StageSim.BastionBurst);
        }

        // ------------------------------------------------------------------ stage flow

        [Test]
        public void StageClearsOnceTheTimelineIsDoneAndTheSkyIsEmpty()
        {
            var sim = GnatsIntoTheGuns(3);
            Run(sim, 10f);
            Assert.AreEqual(StageStatus.Cleared, sim.Status);
            Assert.AreEqual(3, sim.Spawned);
            Assert.AreEqual(100, sim.KillPercent);
            Assert.AreEqual(sim.Credits, sim.CreditsBanked);
        }

        [Test]
        public void EnemiesThatEscapeLowerTheKillPercentage()
        {
            var sim = With(new StageBuilder().Gnats(0f, GnatPath.Dive, 4, x: -6f)); // unarmed: all escape
            Run(sim, 12f);
            Assert.AreEqual(StageStatus.Cleared, sim.Status);
            Assert.AreEqual(0, sim.KillPercent);
        }

        [Test]
        public void FirstLightTimelineIsSortedAndAbout150Seconds()
        {
            var o = Stages.FirstLight.Orders;
            for (int i = 1; i < o.Length; i++) Assert.LessOrEqual(o[i - 1].Time, o[i].Time);
            Assert.That(Stages.FirstLight.LastOrderTime, Is.InRange(120f, 160f));
            Assert.AreEqual("1-1", Stages.FirstLight.Id);
        }

        [TestCase("1-1")]
        [TestCase("sampler")]
        public void WholeStagesRunWithoutErrors(string id)
        {
            var sim = new StageSim(new StageSetup { Stage = Stages.ById(id), Seed = 3 });
            for (int i = 0; i < Field.StepsPerSecond * 170; i++)
            {
                // Weave back and forth near the bottom.
                sim.Step(PlayerInput.Move((i / 120) % 2 == 0 ? 1f : -1f, 0f));
            }
            Assert.AreNotEqual(StageStatus.Playing, sim.Status, "the stage ended one way or the other");
            Assert.Greater(sim.Kills, 0);
        }

        [Test]
        public void StagesAreDeterministic()
        {
            var a = new StageSim(new StageSetup { Stage = Stages.FirstLight, Seed = 9 });
            var b = new StageSim(new StageSetup { Stage = Stages.FirstLight, Seed = 9 });
            for (int i = 0; i < Field.StepsPerSecond * 60; i++)
            {
                var input = PlayerInput.Move((i / 70) % 2 == 0 ? 1f : -1f, (i / 200) % 2 == 0 ? 0.4f : -0.4f);
                a.Step(input);
                b.Step(input);
                Assert.AreEqual(a.ComputeHash(), b.ComputeHash(), "diverged at step " + i);
            }
        }
    }
}
