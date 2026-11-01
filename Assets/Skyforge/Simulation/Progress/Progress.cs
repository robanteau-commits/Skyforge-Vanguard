using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Skyforge.Simulation
{
    /// <summary>One stage's record in the save.</summary>
    public sealed class StageRecord
    {
        public string Id;
        public bool Cleared;
        public long BestScore;
        public bool Ace, Untouched, Purge;
        public int MedalCount => (Ace ? 1 : 0) + (Untouched ? 1 : 0) + (Purge ? 1 : 0);
    }

    /// <summary>What finishing a stage earned (for the results screen).</summary>
    public sealed class StageResult
    {
        public bool Cleared;
        public int CreditsBanked;
        public bool Ace, Untouched, Purge;          // medals earned this run
        public bool NewAce, NewUntouched, NewPurge; // ... for the first time
        public int MedalBonus;                      // credits from first-time medals
        public bool NewBest;
        public string UnlockedStageId;              // the next stage, if this clear opened it
        public int TotalCredits;
    }

    /// <summary>
    /// The player's progress (docs/DESIGN.md §2, §7): credits, owned weapons and their levels,
    /// airframe upgrades, the fitted loadout, and each stage's record and medals. All the shop and
    /// medal rules live here, engine-free and tested; the Game layer only shows it and stores the
    /// text from <see cref="Save"/>. Stored ids are content ids (stable), never display names.
    /// </summary>
    public sealed class Progress
    {
        public const int SaveVersion = 1;

        public int Credits;
        /// <summary>Owned weapons and their level (1..5), in the order they were bought.</summary>
        readonly List<(string Id, int Level)> _owned = new List<(string, int)>();
        public readonly int[] AirframeLevels = new int[Airframe.StatCount];
        public readonly List<StageRecord> Stages = new List<StageRecord>();
        public string LeftWing = "pulseRepeater", RightWing = "pulseRepeater", Core = "pulseNova";

        /// <summary>A fresh save: the starter Pulse Repeater and Pulse Nova, no credits.</summary>
        public static Progress NewGame()
        {
            var p = new Progress();
            p._owned.Add((Weapons.PulseRepeater.Id, 1));
            p._owned.Add((Weapons.PulseNova.Id, 1));
            return p;
        }

        // ------------------------------------------------------------------ weapons

        /// <summary>The owned level of a weapon (0 = not owned).</summary>
        public int LevelOf(string id)
        {
            foreach (var o in _owned) if (o.Id == id) return o.Level;
            return 0;
        }

        public bool Owns(WeaponDef w) => LevelOf(w.Id) > 0;

        public bool CanBuy(WeaponDef w) => !Owns(w) && Credits >= w.Price;

        public bool Buy(WeaponDef w)
        {
            if (!CanBuy(w)) return false;
            Credits -= w.Price;
            _owned.Add((w.Id, 1));
            return true;
        }

        /// <summary>Price of the next level (−1 if not owned or already level 5).</summary>
        public int UpgradeCost(WeaponDef w)
        {
            int lv = LevelOf(w.Id);
            if (lv <= 0 || lv >= w.MaxLevel) return -1;
            var costs = w.Slot == WeaponSlot.Core ? Weapons.CoreUpgradeCosts : Weapons.WingUpgradeCosts;
            return costs[lv - 1];
        }

        public bool CanUpgrade(WeaponDef w)
        {
            int cost = UpgradeCost(w);
            return cost >= 0 && Credits >= cost;
        }

        public bool Upgrade(WeaponDef w)
        {
            if (!CanUpgrade(w)) return false;
            Credits -= UpgradeCost(w);
            for (int i = 0; i < _owned.Count; i++)
                if (_owned[i].Id == w.Id) _owned[i] = (w.Id, _owned[i].Level + 1);
            return true;
        }

        // ------------------------------------------------------------------ airframe

        public int AirframeCost(AirframeStat s)
        {
            int lv = AirframeLevels[(int)s];
            return lv >= Airframe.MaxLevel ? -1 : Airframe.Costs[lv];
        }

        public bool CanUpgradeAirframe(AirframeStat s)
        {
            int cost = AirframeCost(s);
            return cost >= 0 && Credits >= cost;
        }

        public bool UpgradeAirframe(AirframeStat s)
        {
            if (!CanUpgradeAirframe(s)) return false;
            Credits -= AirframeCost(s);
            AirframeLevels[(int)s]++;
            return true;
        }

        // ------------------------------------------------------------------ loadout

        /// <summary>Fit an owned weapon to a slot. False if it isn't owned or doesn't fit that slot.</summary>
        public bool Fit(WeaponSlot slot, Side side, WeaponDef w)
        {
            if (w == null || !Owns(w) || w.Slot != slot) return false;
            if (slot == WeaponSlot.Core) Core = w.Id;
            else if (side == Side.Left) LeftWing = w.Id;
            else RightWing = w.Id;
            return true;
        }

        /// <summary>The fitted loadout at the owned levels.</summary>
        public Loadout BuildLoadout()
        {
            Fitted F(string id)
            {
                var w = Weapons.ById(id);
                return w == null || !Owns(w) ? default : new Fitted(w, LevelOf(id));
            }
            return new Loadout { LeftWing = F(LeftWing), RightWing = F(RightWing), Core = F(Core) };
        }

        // ------------------------------------------------------------------ stages and medals

        public StageRecord Record(string stageId)
        {
            foreach (var r in Stages) if (r.Id == stageId) return r;
            return null;
        }

        StageRecord RecordOrNew(string stageId)
        {
            var r = Record(stageId);
            if (r == null) Stages.Add(r = new StageRecord { Id = stageId });
            return r;
        }

        /// <summary>A world-map stage is open if it's the first, or the one before it has been cleared.</summary>
        public bool IsUnlocked(string stageId)
        {
            var order = Simulation.Stages.World1;
            for (int i = 0; i < order.Length; i++)
            {
                if (order[i].Id != stageId) continue;
                return i == 0 || (Record(order[i - 1].Id)?.Cleared ?? false);
            }
            return false;
        }

        /// <summary>
        /// Bank a finished stage: credits (half on a failed run), best score, and on a clear the
        /// medals, each paying its one-time bonus the first time. Call once, when the stage ends.
        /// </summary>
        public StageResult RecordStage(StageDef stage, StageSim sim)
        {
            var res = new StageResult { Cleared = sim.Status == StageStatus.Cleared, CreditsBanked = sim.CreditsBanked };
            Credits += res.CreditsBanked;
            if (stage != null)
            {
                var rec = RecordOrNew(stage.Id);
                if (sim.Score > rec.BestScore) { rec.BestScore = sim.Score; res.NewBest = true; }
                if (res.Cleared)
                {
                    bool wasCleared = rec.Cleared;
                    rec.Cleared = true;
                    res.Ace = sim.Score >= stage.AceScore;
                    res.Untouched = sim.HullDamageTaken <= 0f;
                    res.Purge = sim.KillPercent >= Simulation.Stages.PurgePercent;
                    if (res.Ace && !rec.Ace) { rec.Ace = res.NewAce = true; res.MedalBonus += stage.MedalBonus[0]; }
                    if (res.Untouched && !rec.Untouched) { rec.Untouched = res.NewUntouched = true; res.MedalBonus += stage.MedalBonus[1]; }
                    if (res.Purge && !rec.Purge) { rec.Purge = res.NewPurge = true; res.MedalBonus += stage.MedalBonus[2]; }
                    Credits += res.MedalBonus;
                    if (!wasCleared)
                    {
                        var order = Simulation.Stages.World1;
                        for (int i = 0; i < order.Length - 1; i++)
                            if (order[i].Id == stage.Id) res.UnlockedStageId = order[i + 1].Id;
                    }
                }
            }
            res.TotalCredits = Credits;
            return res;
        }

        // ------------------------------------------------------------------ save text

        /// <summary>Plain text, one "key=value" per line, so it's readable and forgiving to load.</summary>
        public string Save()
        {
            var sb = new StringBuilder();
            sb.Append("version=").Append(SaveVersion).Append('\n');
            sb.Append("credits=").Append(Credits).Append('\n');
            foreach (var o in _owned) sb.Append("weapon=").Append(o.Id).Append(':').Append(o.Level).Append('\n');
            sb.Append("airframe=").Append(string.Join(",", AirframeLevels)).Append('\n');
            sb.Append("fit=").Append(LeftWing).Append(',').Append(RightWing).Append(',').Append(Core).Append('\n');
            foreach (var r in Stages)
            {
                sb.Append("stage=").Append(r.Id).Append(':').Append(r.Cleared ? 1 : 0).Append(':')
                  .Append(r.BestScore.ToString(CultureInfo.InvariantCulture)).Append(':')
                  .Append(r.Ace ? 1 : 0).Append(r.Untouched ? 1 : 0).Append(r.Purge ? 1 : 0).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Read a save; anything missing or unreadable falls back to a new game's value.</summary>
        public static Progress Load(string text)
        {
            var p = NewGame();
            if (string.IsNullOrEmpty(text)) return p;
            bool sawWeapons = false;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq), val = line.Substring(eq + 1);
                try
                {
                    switch (key)
                    {
                        case "credits":
                            p.Credits = Math.Max(0, int.Parse(val, CultureInfo.InvariantCulture));
                            break;
                        case "weapon":
                        {
                            var parts = val.Split(':');
                            var w = Weapons.ById(parts[0]);
                            if (w == null) break; // a weapon that no longer exists
                            if (!sawWeapons) { p._owned.Clear(); sawWeapons = true; }
                            int lv = Math.Max(1, Math.Min(w.MaxLevel, int.Parse(parts[1], CultureInfo.InvariantCulture)));
                            p._owned.RemoveAll(o => o.Id == w.Id);
                            p._owned.Add((w.Id, lv));
                            break;
                        }
                        case "airframe":
                        {
                            var parts = val.Split(',');
                            for (int i = 0; i < parts.Length && i < Airframe.StatCount; i++)
                                p.AirframeLevels[i] = Math.Max(0, Math.Min(Airframe.MaxLevel, int.Parse(parts[i], CultureInfo.InvariantCulture)));
                            break;
                        }
                        case "fit":
                        {
                            var parts = val.Split(',');
                            if (parts.Length == 3) { p.LeftWing = parts[0]; p.RightWing = parts[1]; p.Core = parts[2]; }
                            break;
                        }
                        case "stage":
                        {
                            var parts = val.Split(':');
                            var r = p.RecordOrNew(parts[0]);
                            r.Cleared = parts[1] == "1";
                            r.BestScore = long.Parse(parts[2], CultureInfo.InvariantCulture);
                            r.Ace = parts[3][0] == '1'; r.Untouched = parts[3][1] == '1'; r.Purge = parts[3][2] == '1';
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    // Skip a damaged line rather than losing the whole save.
                }
            }
            // The starters are always owned, and the fit must be owned weapons in the right slots.
            if (p.LevelOf(Weapons.PulseRepeater.Id) == 0) p._owned.Insert(0, (Weapons.PulseRepeater.Id, 1));
            if (p.LevelOf(Weapons.PulseNova.Id) == 0) p._owned.Insert(1, (Weapons.PulseNova.Id, 1));
            if (!Valid(p, p.LeftWing, WeaponSlot.Wing)) p.LeftWing = Weapons.PulseRepeater.Id;
            if (!Valid(p, p.RightWing, WeaponSlot.Wing)) p.RightWing = Weapons.PulseRepeater.Id;
            if (!Valid(p, p.Core, WeaponSlot.Core)) p.Core = Weapons.PulseNova.Id;
            return p;
        }

        static bool Valid(Progress p, string id, WeaponSlot slot)
        {
            var w = Weapons.ById(id);
            return w != null && w.Slot == slot && p.Owns(w);
        }
    }
}
