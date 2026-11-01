using NUnit.Framework;

namespace Skyforge.Simulation.Tests
{
    /// <summary>M4: World 1's stages, the Brood Carrier, the Tidebreaker and Veteran mode.</summary>
    public class WorldTests
    {
        static StageSim With(StageBuilder b, Loadout loadout = null, bool veteran = false, uint seed = 1)
        {
            var sim = new StageSim(new StageSetup { Stage = b.Build("t", "Test"), Loadout = loadout ?? new Loadout(), Seed = seed, Veteran = veteran });
            // Tests watch the enemies, not the player's survival.
            sim.Player.MaxHull = sim.Player.Hull = 1e6f;
            return sim;
        }

        static void Run(StageSim sim, float seconds, PlayerInput input = default)
        {
            int steps = (int)(seconds * Field.StepsPerSecond + 0.5f);
            for (int i = 0; i < steps; i++) sim.Step(input);
        }

        static Enemy Find(StageSim sim, EnemyKind kind)
        {
            foreach (var e in sim.Enemies) if (e.Kind == kind && !e.Dead) return e;
            return null;
        }

        static int Count(StageSim sim, EnemyKind kind)
        {
            int n = 0;
            foreach (var e in sim.Enemies) if (e.Kind == kind && !e.Dead) n++;
            return n;
        }

        static readonly Loadout Strong = new Loadout
        {
            LeftWing = new Fitted(Weapons.PulseRepeater, 5),
            RightWing = new Fitted(Weapons.PulseRepeater, 5),
        };

        // ------------------------------------------------------------------ stages

        [Test]
        public void EveryWorldOneStageIsBuiltAndInTimeOrder()
        {
            foreach (var w in Stages.World1)
            {
                Assert.IsNotNull(w.Def, w.Id + " has a timeline");
                Assert.AreEqual(w.Id, w.Def.Id);
                Assert.Greater(w.Def.Orders.Length, 10, w.Id);
                for (int i = 1; i < w.Def.Orders.Length; i++)
                    Assert.LessOrEqual(w.Def.Orders[i - 1].Time, w.Def.Orders[i].Time, w.Id + " is sorted");
                Assert.AreEqual(3, w.Def.MedalBonus.Length);
            }
        }

        [Test]
        public void StagesIntroduceTheirThreats()
        {
            bool Has(StageDef d, EnemyKind k) { foreach (var o in d.Orders) if (!o.Repair && o.Kind == k) return true; return false; }
            Assert.IsTrue(Has(Stages.HarborLine, EnemyKind.Bastion));
            Assert.IsTrue(Has(Stages.Stormwall, EnemyKind.SpineMine) && Has(Stages.Stormwall, EnemyKind.RamDrone));
            Assert.IsTrue(Has(Stages.FoundryRidge, EnemyKind.Warden) && Has(Stages.FoundryRidge, EnemyKind.BroodCarrier));
            Assert.IsTrue(Has(Stages.TidebreakerStage, EnemyKind.Tidebreaker));
        }

        [Test]
        public void TheBossStageIsDeterministic()
        {
            var a = new StageSim(new StageSetup { Stage = Stages.TidebreakerStage, Loadout = Strong, Seed = 5 });
            var b = new StageSim(new StageSetup { Stage = Stages.TidebreakerStage, Loadout = Strong, Seed = 5 });
            for (int i = 0; i < Field.StepsPerSecond * 50; i++)
            {
                var input = PlayerInput.Towards(new Vec2(4f * (float)System.Math.Sin(i * 0.01), -10f));
                a.Step(input);
                b.Step(input);
            }
            Assert.AreEqual(a.ComputeHash(), b.ComputeHash());
        }

        // ------------------------------------------------------------------ Brood Carrier

        [Test]
        public void TheCarrierCrossesWithTwoBaysThatLaunchGnats()
        {
            var sim = With(new StageBuilder().Carrier(0f, -1f, 10f).Repair(1000f));
            Run(sim, 0.1f);
            var carrier = Find(sim, EnemyKind.BroodCarrier);
            Assert.IsNotNull(carrier);
            Assert.Less(carrier.Pos.X, -sim.HalfWidth, "enters from the left");
            Assert.AreEqual(2, Count(sim, EnemyKind.BroodBay));
            float x0 = carrier.Pos.X;
            Run(sim, 8f);
            Assert.Greater(carrier.Pos.X, x0 + 5f, "crosses to the right");
            foreach (var e in sim.Enemies)
                if (e.Kind == EnemyKind.BroodBay) Assert.AreEqual(carrier.Pos.X + e.Offset.X, e.Pos.X, 0.01f, "bays ride along");
            Assert.Greater(Count(sim, EnemyKind.Gnat), 0, "the bays launch Gnats");
            Assert.AreEqual(1, sim.Spawned, "launched Gnats and bays don't count toward the kill %");
        }

        [Test]
        public void DestroyingTheCarrierTakesItsBaysWithIt()
        {
            var sim = With(new StageBuilder().Carrier(0f, -1f, 10f).Repair(1000f), Strong);
            Run(sim, 0.1f);
            var carrier = Find(sim, EnemyKind.BroodCarrier);
            carrier.Hp = 0.5f; // one hit
            for (int i = 0; i < Field.StepsPerSecond * 15 && !carrier.Dead; i++)
                sim.Step(PlayerInput.Towards(new Vec2(carrier.Pos.X, -10f)));
            Assert.IsTrue(carrier.Dead);
            sim.Step(PlayerInput.Idle);
            Assert.AreEqual(0, Count(sim, EnemyKind.BroodBay));
            Assert.AreEqual(100, sim.KillPercent);
            Assert.GreaterOrEqual(sim.Score, EnemyDefs.BroodCarrier.Score);
        }

        // ------------------------------------------------------------------ Tidebreaker

        static StageSim Boss(Loadout loadout = null, bool veteran = false)
        {
            var sim = With(new StageBuilder().Tidebreaker(0f), loadout, veteran);
            Run(sim, 12f); // rises into place
            return sim;
        }

        [Test]
        public void TheTidebreakerRisesInWithItsParts()
        {
            var sim = Boss();
            var hull = Find(sim, EnemyKind.Tidebreaker);
            Assert.IsNotNull(hull);
            Assert.IsTrue(hull.Intangible);
            Assert.AreEqual(StageSim.BossStationY, hull.Pos.Y, 0.2f);
            Assert.AreEqual(1, hull.Mode, "phase 1");
            Assert.AreEqual(2, Count(sim, EnemyKind.TideCannon));
            Assert.AreEqual(1, Count(sim, EnemyKind.TideMissileBay));
            Assert.IsTrue(Find(sim, EnemyKind.TideEye).Shielded, "the eye is shielded in phase 1");
            Assert.AreSame(hull, sim.Boss);
            Assert.AreEqual(1f, sim.BossHealth(hull), 0.001f);
        }

        [Test]
        public void PhaseOneFiresStreamsAndMissiles()
        {
            var sim = Boss();
            int missiles = 0, maxShots = 0;
            for (int i = 0; i < Field.StepsPerSecond * 10; i++)
            {
                sim.Step(PlayerInput.Idle);
                missiles = System.Math.Max(missiles, Count(sim, EnemyKind.TideMissile));
                maxShots = System.Math.Max(maxShots, sim.EnemyShots.Count);
            }
            Assert.Greater(missiles, 0, "the missile bay launches missiles");
            Assert.Greater(maxShots, 10, "the cannons fire streams");
        }

        [Test]
        public void TheShieldedEyeTakesNoDamage()
        {
            var sim = Boss(Strong);
            var eye = Find(sim, EnemyKind.TideEye);
            int deflected = 0;
            for (int i = 0; i < Field.StepsPerSecond * 3; i++)
            {
                sim.Step(PlayerInput.Towards(new Vec2(eye.Pos.X, -10f)));
                foreach (var e in sim.Events) if (e.Type == SimEventType.Deflected) deflected++;
            }
            Assert.AreEqual(eye.MaxHp, eye.Hp);
            Assert.Greater(deflected, 0);
        }

        [Test]
        public void TheHullCantBeHit()
        {
            var sim = Boss(Strong);
            var hull = Find(sim, EnemyKind.Tidebreaker);
            foreach (var e in sim.Enemies) if (e.ParentId == hull.Id) e.Shielded = true; // nothing else to hit
            Run(sim, 3f, PlayerInput.Towards(new Vec2(hull.Pos.X, -10f)));
            Assert.IsFalse(hull.Dead);
            Assert.AreEqual(1, Count(sim, EnemyKind.Tidebreaker));
        }

        [Test]
        public void LosingBothCannonsOpensTheEyeAndStartsPhaseTwo()
        {
            var sim = Boss();
            foreach (var e in sim.Enemies) if (e.Kind == EnemyKind.TideCannon) e.Dead = true;
            Run(sim, 0.1f);
            var hull = Find(sim, EnemyKind.Tidebreaker);
            Assert.AreEqual(2, hull.Mode);
            Assert.IsFalse(Find(sim, EnemyKind.TideEye).Shielded);
            Run(sim, StageSim.BossGnatInterval);
            Assert.Greater(Count(sim, EnemyKind.Gnat), 0, "phase 2 launches Gnat waves");
            Assert.Greater(sim.EnemyShots.Count, 0, "the eye fires its spiral");
        }

        [Test]
        public void PhaseThreeLungesAndFiresLasersThatHurt()
        {
            var sim = Boss();
            foreach (var e in sim.Enemies) if (e.Kind == EnemyKind.TideCannon) e.Dead = true;
            Run(sim, 0.1f);
            var eye = Find(sim, EnemyKind.TideEye);
            eye.Hp = eye.MaxHp * 0.3f;
            Run(sim, 0.1f);
            var hull = Find(sim, EnemyKind.Tidebreaker);
            Assert.AreEqual(3, hull.Mode);
            Run(sim, 3.5f);
            Assert.Less(hull.Pos.Y, StageSim.BossStationY - 1.5f, "the fortress lunges forward");
            Assert.Greater(sim.Lasers.Count, 0, "lasers sweep in phase 3");
            Assert.IsFalse(sim.Lasers[0].Firing, "a warning line first");

            var down = new HazardBeam { Origin = new Vec2(0f, 10f), Heading = 180f };
            Assert.IsTrue(StageSim.InLaser(down, new Vec2(0.3f, -5f), PlayerShip.HitRadius));
            Assert.IsFalse(StageSim.InLaser(down, new Vec2(2f, -5f), PlayerShip.HitRadius));
            Assert.IsFalse(StageSim.InLaser(down, new Vec2(0f, 12f), PlayerShip.HitRadius), "nothing behind the emitter");
        }

        [Test]
        public void DestroyingTheEyeDefeatsTheBossAndClearsTheStage()
        {
            var sim = With(new StageBuilder().Tidebreaker(0f));
            Run(sim, 12f);
            long before = sim.Score;
            foreach (var e in sim.Enemies) if (e.Kind == EnemyKind.TideEye) e.Dead = true;
            Run(sim, 2f);
            Assert.AreEqual(0, Count(sim, EnemyKind.Tidebreaker));
            Assert.AreEqual(0, Count(sim, EnemyKind.TideMissileBay), "the other parts go with it");
            Assert.GreaterOrEqual(sim.Score - before, EnemyDefs.Tidebreaker.Score);
            Run(sim, 4f);
            Assert.AreEqual(StageStatus.Cleared, sim.Status);
            Assert.AreEqual(100, sim.KillPercent);
            Assert.GreaterOrEqual(sim.Credits, EnemyDefs.Tidebreaker.Credits, "the boss's credits are pulled in");
        }

        // ------------------------------------------------------------------ Veteran

        [Test]
        public void VeteranEnemiesAreTougherAndShootFaster()
        {
            StageSim Make(bool vet) => With(new StageBuilder().Needle(0f, 0f).Warden(0f, 4f).Repair(1000f), veteran: vet);
            var normal = Make(false);
            var vet = Make(true);
            normal.Step(PlayerInput.Idle);
            vet.Step(PlayerInput.Idle);
            Assert.AreEqual(Find(normal, EnemyKind.Warden).MaxHp * StageSim.VeteranHp, Find(vet, EnemyKind.Warden).MaxHp, 0.01f);
            for (int i = 0; i < Field.StepsPerSecond * 6 && (normal.EnemyShots.Count == 0 || vet.EnemyShots.Count == 0); i++)
            {
                if (normal.EnemyShots.Count == 0) normal.Step(PlayerInput.Idle);
                if (vet.EnemyShots.Count == 0) vet.Step(PlayerInput.Idle);
            }
            Assert.AreEqual(normal.EnemyShots[0].Vel.Length * StageSim.VeteranBulletSpeed, vet.EnemyShots[0].Vel.Length, 0.01f);
        }

        [Test]
        public void VeteranPatternsAreDenser()
        {
            int RingSize(bool vet)
            {
                // A Spine Mine drifting into the guns bursts into a ring when it's shot.
                var sim = With(new StageBuilder().Mine(0f, 0f).Repair(1000f), Strong, vet);
                for (int i = 0; i < Field.StepsPerSecond * 6 && sim.EnemyShots.Count == 0; i++) sim.Step(PlayerInput.Idle);
                return sim.EnemyShots.Count;
            }
            Assert.AreEqual(StageSim.MineRingBullets, RingSize(false));
            Assert.AreEqual((int)System.Math.Round(StageSim.MineRingBullets * StageSim.VeteranDensity), RingSize(true));
        }

        [Test]
        public void VeteranHitsHarderAndPaysMore()
        {
            StageSim Make(bool vet) => With(new StageBuilder().Repair(1000f), veteran: vet);
            var normal = Make(false);
            var vet = Make(true);
            foreach (var s in new[] { normal, vet })
            {
                s.Player.Shield = 0f;
                s.Player.ShieldRegenWait = 10f; // no regeneration this step
                s.EnemyShots.Add(new EnemyShot { Pos = s.Player.Pos, Radius = 0.2f, Damage = 10f });
                s.Step(PlayerInput.Idle);
            }
            Assert.AreEqual(10f, normal.HullDamageTaken, 0.01f);
            Assert.AreEqual(10f * StageSim.VeteranDamage, vet.HullDamageTaken, 0.01f);

            long ScoreFor(bool v)
            {
                var sim = With(new StageBuilder().Gnats(0f, GnatPath.Dive, 1, x: 0f, speed: 3f).Repair(1000f), Strong, v);
                for (int i = 0; i < Field.StepsPerSecond * 5 && sim.Kills == 0; i++) sim.Step(PlayerInput.Idle);
                return sim.Score;
            }
            Assert.AreEqual(ScoreFor(false) * 3 / 2, ScoreFor(true));
        }

        // ------------------------------------------------------------------ Veteran progress

        static StageDef Tiny(string id) =>
            new StageBuilder().Gnats(0.5f, GnatPath.Dive, 2, x: 0f, delay: 0.6f).Build(id, "Test") is var d ? Medals(d) : null;

        static StageDef Medals(StageDef d) { d.AceScore = 1; d.MedalBonus = new[] { 250, 200, 150 }; return d; }

        static StageSim Clear(StageDef stage, bool veteran)
        {
            var sim = new StageSim(new StageSetup { Stage = stage, Veteran = veteran, Loadout = Strong });
            for (int i = 0; i < Field.StepsPerSecond * 30 && sim.Status == StageStatus.Playing; i++) sim.Step(PlayerInput.Idle);
            Assert.AreEqual(StageStatus.Cleared, sim.Status);
            return sim;
        }

        [Test]
        public void VeteranUnlocksAfterTheBossAndKeepsItsOwnMedals()
        {
            var p = Progress.NewGame();
            Assert.IsFalse(p.VeteranUnlocked);
            Assert.IsFalse(p.IsUnlocked("1-1", veteran: true));
            var boss = Tiny("1-B");
            var r = p.RecordStage(boss, Clear(boss, false));
            Assert.IsTrue(r.UnlockedVeteran);
            Assert.IsTrue(p.VeteranUnlocked);
            Assert.IsTrue(p.IsUnlocked("1-1", veteran: true));
            Assert.IsFalse(p.IsUnlocked("1-2", veteran: true), "Veteran stages open in order too");

            var first = Tiny("1-1");
            var v = p.RecordStage(first, Clear(first, true));
            Assert.IsTrue(v.Veteran && v.NewAce, "Veteran medals are separate, and pay their bonus again");
            Assert.AreEqual(3, p.Record("1-1", veteran: true).MedalCount);
            Assert.IsNull(p.Record("1-1"), "the Normal record is untouched");
            Assert.IsTrue(p.IsUnlocked("1-2", veteran: true));
            Assert.IsFalse(p.RecordStage(boss, Clear(boss, false)).UnlockedVeteran, "only the first time");
        }

        [Test]
        public void VeteranAceNeedsOneAndAHalfTimesTheScore()
        {
            Assert.AreEqual(Stages.FirstLight.AceScore * 3 / 2, Progress.AceScore(Stages.FirstLight, true));
            Assert.AreEqual(Stages.FirstLight.AceScore, Progress.AceScore(Stages.FirstLight, false));
        }

        [Test]
        public void TheDifficultyAndVeteranRecordsSurviveASave()
        {
            var p = Progress.NewGame();
            var boss = Tiny("1-B");
            p.RecordStage(boss, Clear(boss, false));
            var first = Tiny("1-1");
            p.RecordStage(first, Clear(first, true));
            p.Veteran = true;
            var q = Progress.Load(p.Save());
            Assert.IsTrue(q.Veteran);
            Assert.AreEqual(3, q.Record("1-1", veteran: true).MedalCount);

            // A save that claims Veteran without the boss cleared falls back to Normal.
            Assert.IsFalse(Progress.Load("veteran=1\n").Veteran);
        }
    }
}
