using NUnit.Framework;

namespace Skyforge.Simulation.Tests
{
    /// <summary>M3: the shop, airframe upgrades, medals, unlocks and the save file.</summary>
    public class ProgressTests
    {
        [Test]
        public void NewGameOwnsOnlyTheStarters()
        {
            var p = Progress.NewGame();
            Assert.AreEqual(0, p.Credits);
            Assert.IsTrue(p.Owns(Weapons.PulseRepeater));
            Assert.IsTrue(p.Owns(Weapons.PulseNova));
            Assert.IsFalse(p.Owns(Weapons.ScatterCannon));
            var l = p.BuildLoadout();
            Assert.AreEqual(Weapons.PulseRepeater, l.LeftWing.Weapon);
            Assert.AreEqual(Weapons.PulseNova, l.Core.Weapon);
        }

        [Test]
        public void BuyingNeedsTheCredits()
        {
            var p = Progress.NewGame();
            p.Credits = 599;
            Assert.IsFalse(p.Buy(Weapons.ScatterCannon));
            p.Credits = 650;
            Assert.IsTrue(p.Buy(Weapons.ScatterCannon));
            Assert.AreEqual(50, p.Credits);
            Assert.AreEqual(1, p.LevelOf("scatterCannon"));
            Assert.IsFalse(p.Buy(Weapons.ScatterCannon), "can't buy twice");
        }

        [Test]
        public void UpgradesFollowTheCostTable()
        {
            var p = Progress.NewGame();
            p.Credits = 300 + 600 + 1000 + 1600;
            for (int i = 0; i < 4; i++) Assert.IsTrue(p.Upgrade(Weapons.PulseRepeater));
            Assert.AreEqual(5, p.LevelOf("pulseRepeater"));
            Assert.AreEqual(0, p.Credits);
            Assert.AreEqual(-1, p.UpgradeCost(Weapons.PulseRepeater), "level 5 is the top");
            Assert.AreEqual(400, p.UpgradeCost(Weapons.PulseNova), "cores have their own costs");
            Assert.AreEqual(-1, p.UpgradeCost(Weapons.LanceDriver), "not owned");
        }

        [Test]
        public void OnlyOwnedWeaponsCanBeFittedInTheirOwnSlot()
        {
            var p = Progress.NewGame();
            Assert.IsFalse(p.Fit(WeaponSlot.Wing, Side.Left, Weapons.LanceDriver));
            Assert.IsFalse(p.Fit(WeaponSlot.Wing, Side.Left, Weapons.PulseNova), "a core isn't a wing gun");
            p.Credits = 900;
            p.Buy(Weapons.LanceDriver);
            Assert.IsTrue(p.Fit(WeaponSlot.Wing, Side.Right, Weapons.LanceDriver));
            Assert.AreEqual(Weapons.LanceDriver, p.BuildLoadout().RightWing.Weapon);
        }

        [Test]
        public void AirframeUpgradesChangeTheShip()
        {
            var p = Progress.NewGame();
            p.Credits = 250 + 500;
            Assert.IsTrue(p.UpgradeAirframe(AirframeStat.Hull));
            Assert.IsTrue(p.UpgradeAirframe(AirframeStat.Hull));
            Assert.IsFalse(p.UpgradeAirframe(AirframeStat.Hull), "out of credits");
            var sim = new StageSim(new StageSetup { Airframe = p.AirframeLevels });
            Assert.AreEqual(140f, sim.Player.MaxHull);
            Assert.AreEqual(140f, sim.Player.Hull, "starts full");
            Assert.AreEqual(40f, sim.Player.MaxShield, "other stats unchanged");
        }

        [Test]
        public void StockShipMatchesTheDesign()
        {
            var sim = new StageSim(new StageSetup());
            Assert.AreEqual(100f, sim.Player.MaxHull);
            Assert.AreEqual(40f, sim.Player.MaxShield);
            Assert.AreEqual(14f, sim.Player.MaxSpeed);
            Assert.AreEqual(4f, sim.Player.MagnetRange);
            Assert.AreEqual(4f, sim.Player.ShieldRegenDelay);
        }

        [Test]
        public void MaxedAirframeMatchesTheDesign()
        {
            var sim = new StageSim(new StageSetup { Airframe = new[] { 5, 5, 5, 5, 5, 5 } });
            Assert.AreEqual(200f, sim.Player.MaxHull);
            Assert.AreEqual(100f, sim.Player.MaxShield);
            Assert.AreEqual(21.5f, sim.Player.MaxSpeed, 1e-4f);
            Assert.AreEqual(9f, sim.Player.MagnetRange);
            Assert.AreEqual(2f, sim.Player.ShieldRegenDelay, 1e-4f);
            Assert.AreEqual(10f, sim.Player.ShieldRegenRate, 1e-4f);
            Assert.AreEqual(1.5f, sim.Player.Capacitor, 1e-4f);
        }

        // ------------------------------------------------------------------ stages and medals

        /// <summary>A short cleared stage, or a failed one, to bank.</summary>
        static StageSim Finish(StageDef stage, bool fail = false)
        {
            var sim = new StageSim(new StageSetup { Stage = stage, Loadout = new Loadout { LeftWing = new Fitted(Weapons.PulseRepeater, 5), RightWing = new Fitted(Weapons.PulseRepeater, 5) } });
            if (fail) { sim.Player.Shield = 0f; sim.Player.Hull = 1f; sim.EnemyShots.Add(new EnemyShot { Pos = sim.Player.Pos, Radius = 0.3f, Damage = 10f }); }
            for (int i = 0; i < Field.StepsPerSecond * 30 && sim.Status == StageStatus.Playing; i++) sim.Step(PlayerInput.Idle);
            return sim;
        }

        static StageDef Tiny(long ace) =>
            new StageBuilder().Gnats(0.5f, GnatPath.Dive, 3, x: 1f, delay: 0.6f).Gnats(0.5f, GnatPath.Dive, 3, x: -1f, delay: 0.6f)
                .Build("1-1", "Test") is var d ? Set(d, ace) : null;

        static StageDef Set(StageDef d, long ace) { d.AceScore = ace; d.MedalBonus = new[] { 250, 200, 150 }; return d; }

        [Test]
        public void ClearingAwardsMedalsAndTheirBonusOnce()
        {
            var stage = Tiny(ace: 1);
            var p = Progress.NewGame();
            var sim = Finish(stage);
            Assert.AreEqual(StageStatus.Cleared, sim.Status);
            var r = p.RecordStage(stage, sim);
            Assert.IsTrue(r.NewAce && r.NewUntouched && r.NewPurge);
            Assert.AreEqual(600, r.MedalBonus);
            Assert.AreEqual(sim.Credits + 600, p.Credits);
            Assert.AreEqual(3, p.Record("1-1").MedalCount);
            Assert.AreEqual("1-2", r.UnlockedStageId);

            var again = p.RecordStage(stage, Finish(stage));
            Assert.AreEqual(0, again.MedalBonus, "medal bonuses are paid once");
            Assert.IsNull(again.UnlockedStageId);
        }

        [Test]
        public void AceNeedsTheScore()
        {
            var stage = Tiny(ace: 999999);
            var r = Progress.NewGame().RecordStage(stage, Finish(stage));
            Assert.IsFalse(r.Ace);
            Assert.IsTrue(r.Untouched);
        }

        [Test]
        public void FailingBanksHalfTheCreditsAndNoMedals()
        {
            var stage = Tiny(ace: 1);
            var p = Progress.NewGame();
            var sim = Finish(stage, fail: true);
            Assert.AreEqual(StageStatus.Failed, sim.Status);
            var r = p.RecordStage(stage, sim);
            Assert.IsFalse(r.Cleared);
            Assert.AreEqual(0, r.MedalBonus);
            Assert.AreEqual(sim.Credits / 2, p.Credits);
            Assert.IsFalse(p.IsUnlocked("1-2"));
        }

        [Test]
        public void StagesUnlockInOrder()
        {
            var p = Progress.NewGame();
            Assert.IsTrue(p.IsUnlocked("1-1"));
            Assert.IsFalse(p.IsUnlocked("1-2"));
            var stage = Tiny(ace: 1);
            p.RecordStage(stage, Finish(stage));
            Assert.IsTrue(p.IsUnlocked("1-2"));
            Assert.IsFalse(p.IsUnlocked("1-3"));
        }

        // ------------------------------------------------------------------ save file

        [Test]
        public void SaveRoundTrips()
        {
            var p = Progress.NewGame();
            p.Credits = 5000;
            p.Buy(Weapons.SeekerPods);
            p.Upgrade(Weapons.SeekerPods);
            p.UpgradeAirframe(AirframeStat.Magnet);
            p.Fit(WeaponSlot.Wing, Side.Right, Weapons.SeekerPods);
            var stage = Tiny(ace: 1);
            p.RecordStage(stage, Finish(stage));

            var q = Progress.Load(p.Save());
            Assert.AreEqual(p.Credits, q.Credits);
            Assert.AreEqual(2, q.LevelOf("seekerPods"));
            Assert.AreEqual(1, q.AirframeLevels[(int)AirframeStat.Magnet]);
            Assert.AreEqual("seekerPods", q.RightWing);
            Assert.AreEqual(3, q.Record("1-1").MedalCount);
            Assert.AreEqual(p.Record("1-1").BestScore, q.Record("1-1").BestScore);
            Assert.AreEqual(p.Save(), q.Save());
        }

        [Test]
        public void EmptyOrDamagedSavesFallBackSafely()
        {
            Assert.AreEqual(Progress.NewGame().Save(), Progress.Load(null).Save());
            var q = Progress.Load("credits=abc\nweapon=noSuchGun:3\nfit=lanceDriver,pulseRepeater,pulseNova\nweapon=pulseRepeater:9\n");
            Assert.AreEqual(0, q.Credits);
            Assert.AreEqual(5, q.LevelOf("pulseRepeater"), "clamped to the top level");
            Assert.AreEqual("pulseRepeater", q.LeftWing, "an unowned weapon can't stay fitted");
            Assert.IsTrue(q.Owns(Weapons.PulseNova), "starters are always owned");
        }
    }
}
