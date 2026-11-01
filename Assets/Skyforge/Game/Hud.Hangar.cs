using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// The hangar and shop (docs/DESIGN.md §9), styled after the title key art. Left panel: tabs for
    /// the left wing, right wing, core and airframe, and a numbered list (level pips, FITTED / OWNED
    /// / price tags). Right panel: the selected weapon's or airframe stat's data, with buy, upgrade
    /// and fit buttons. Under the field: back to the map and LAUNCH. Behind it the ship flies a live
    /// practice preview of the current loadout. In the sandbox everything is free at any level and
    /// nothing is saved. Phones get one compact panel over the lower field.
    /// </summary>
    public sealed partial class Hud
    {
        enum Tab { LeftWing, RightWing, Core, Airframe }

        Tab _tab = Tab.LeftWing;
        WeaponDef _viewing;
        AirframeStat _stat = AirframeStat.Hull;

        WeaponSlot TabSlot => _tab == Tab.Core ? WeaponSlot.Core : WeaponSlot.Wing;
        Side TabSide => _tab == Tab.RightWing ? Side.Right : Side.Left;

        Fitted TabFit
        {
            get
            {
                var l = _runner.Loadout;
                return _tab == Tab.Core ? l.Core : _tab == Tab.RightWing ? l.RightWing : l.LeftWing;
            }
        }

        /// <summary>The weapon the details panel shows: the one clicked, else the one fitted.</summary>
        WeaponDef Viewing
        {
            get
            {
                if (_viewing != null && _viewing.Slot == TabSlot) return _viewing;
                return TabFit.Weapon;
            }
        }

        int OwnedLevel(WeaponDef w)
        {
            if (_runner.Sandbox)
            {
                var f = TabFit;
                return f.Weapon == w ? f.Level : 1;
            }
            return _runner.Progress.LevelOf(w.Id);
        }

        string NextSortie
        {
            get
            {
                var st = _runner.SelectedStage;
                return st == null ? "Practice" : st == Stages.ArmadaSampler ? "Armada Sampler" : "0" + st.Id + " " + st.Name;
            }
        }

        string TabName() => _tab == Tab.Core ? "Core" : _tab == Tab.RightWing ? "Right wing" : _tab == Tab.LeftWing ? "Left wing" : "Airframe";

        void Hangar(Rect field, float s, bool panels)
        {
            if (panels)
            {
                ArtBehindPanels(field, 0.28f);
                float m = 60f * s, top = 92f * s, bottom = 92f * s;
                HangarList(new Rect(m, top, field.x - m - 40f * s, Screen.height - top - bottom), s);
                HangarDetails(new Rect(field.xMax + 40f * s, top, Screen.width - field.xMax - m - 40f * s, Screen.height - top - bottom), s);
                // Back and launch, under the live preview.
                float bh = 66f * s, y = Screen.height - bottom - bh;
                float bw = Mathf.Min(150f * s, field.width * 0.25f);
                if (GhostButton(new Rect(field.x + 30f * s, y, bw, bh), "‹ Map", s)) _runner.OpenMap();
                if (BigButton(new Rect(field.x + 30f * s + bw + 12f * s, y, field.width - 60f * s - bw - 12f * s, bh), "Launch", s)) _runner.Launch();
                Frame(s, "Vanguard Command  //  [Skyforge hangar]", "Next sortie  •  [" + NextSortie + "]", "[Enter] Launch  •  [Esc] Map", "Build " + Application.version);
            }
            else
            {
                HangarCompact(field, s);
            }
        }

        // ------------------------------------------------------------------ wide layout

        void HangarList(Rect box, float s)
        {
            string credits = _runner.Sandbox ? "Sandbox" : _runner.Progress.Credits.ToString("N0") + " cr";
            var r = PanelBox(box, s, "Hangar", credits, Palette.HudGold);
            float y = r.y;
            Tabs(ref y, r, s);
            y += 18f * s;
            if (_tab == Tab.Airframe) AirframeList(ref y, r, s, 60f * s);
            else WeaponList(ref y, r, s, 56f * s);
            if (_runner.Sandbox)
            {
                GUI.Label(new Rect(r.x, r.yMax - 46f * s, r.width, 46f * s), "<color=#ffb43c>Sandbox: every weapon free at any level; nothing is saved.</color>", _small);
            }
        }

        void HangarDetails(Rect box, float s)
        {
            var r = PanelBox(box, s, _tab == Tab.Airframe ? "Airframe data" : "Weapon data", TabName());
            float y = r.y;
            if (_tab == Tab.Airframe) AirframeDetails(ref y, r, s);
            else WeaponDetails(ref y, r, s);

            if (_runner.Sandbox)
            {
                y = Mathf.Max(y + 10f * s, r.yMax - 76f * s);
                Lbl(new Rect(r.x, y, r.width, 22f * s), "Test flight", s);
                StageButtons(y + 28f * s, r, s);
            }
        }

        void WeaponDetails(ref float y, Rect r, float s)
        {
            var w = Viewing;
            if (w == null) return;
            bool owned = _runner.Sandbox || _runner.Progress.Owns(w);
            int level = owned ? OwnedLevel(w) : 1;
            GUI.Label(new Rect(r.x, y, r.width, 42f * s), w.Name.ToUpperInvariant(), new GUIStyle(_heading) { fontSize = Mathf.RoundToInt(30 * s) });
            y += 46f * s;
            Lbl(new Rect(r.x, y, r.width, 22f * s), (owned ? "Level " + level + " of " + w.MaxLevel : "Not owned") + "   •   " + w.Role, s, Cyan);
            y += 32f * s;
            GUI.Label(new Rect(r.x, y, r.width, 60f * s), w.Description, _small);
            y += 64f * s;

            if (_runner.Sandbox)
            {
                Lbl(new Rect(r.x, y, r.width, 22f * s), "Level", s);
                y += 28f * s;
                LevelButtons(ref y, r, s, new Fitted(w, level));
                y += 14f * s;
            }

            Lbl(new Rect(r.x, y, r.width, 22f * s), "Performance", s);
            y += 30f * s;
            StatLines(ref y, r, s, StatsText(w, level));
            y += 12f * s;

            if (!_runner.Sandbox)
            {
                var p = _runner.Progress;
                float bh = 58f * s;
                if (!owned)
                {
                    if (BigButton(new Rect(r.x, y, r.width, bh), "Buy", s, p.CanBuy(w), w.Price.ToString("N0") + " CR")) _runner.Buy(w, TabSide);
                    y += bh + 10f * s;
                    if (!p.CanBuy(w)) { Lbl(new Rect(r.x, y, r.width, 22f * s), "Need " + (w.Price - p.Credits).ToString("N0") + " more credits", s, Palette.HudDanger); y += 30f * s; }
                }
                else if (p.UpgradeCost(w) >= 0)
                {
                    Lbl(new Rect(r.x, y, r.width, 22f * s), "Next level", s);
                    y += 26f * s;
                    GUI.Label(new Rect(r.x, y, r.width, 46f * s), StatsText(w, level + 1).Split('\n')[0], _body);
                    y += 34f * s;
                    int cost = p.UpgradeCost(w);
                    if (BigButton(new Rect(r.x, y, r.width, bh), "Upgrade to Lv " + (level + 1), s, p.CanUpgrade(w), cost.ToString("N0") + " CR")) _runner.Upgrade(w);
                    y += bh + 10f * s;
                }
                else
                {
                    Lbl(new Rect(r.x, y, r.width, 22f * s), "Fully upgraded", s, Palette.HudGold);
                    y += 32f * s;
                }
                if (owned && TabFit.Weapon != w)
                {
                    if (GhostButton(new Rect(r.x, y, r.width, 50f * s), "Fit to " + TabName(), s)) _runner.Fit(TabSlot, TabSide, w, level);
                    y += 60f * s;
                }
            }
            if (TabSlot == WeaponSlot.Wing && owned && TabFit.Weapon == w)
            {
                var other = TabSide == Side.Left ? Side.Right : Side.Left;
                if (GhostButton(new Rect(r.x, y, r.width, 50f * s), "Fit the same to " + (other == Side.Left ? "left" : "right") + " wing", s))
                    _runner.Fit(WeaponSlot.Wing, other, w, level);
                y += 60f * s;
            }
        }

        /// <summary>Each line of a stats description as a row with a cyan tick at the left.</summary>
        void StatLines(ref float y, Rect r, float s, string text)
        {
            foreach (var line in text.Split('\n'))
            {
                if (line.Length == 0) continue;
                DrawRect(new Rect(r.x, y + 9f * s, 10f * s, 3f * s), line.StartsWith("Lv 5") ? Palette.HudGold : Cyan);
                GUI.Label(new Rect(r.x + 20f * s, y, r.width - 20f * s, 46f * s), line, _body);
                y += (line.Length > 46 ? 48f : 28f) * s;
            }
        }

        void AirframeDetails(ref float y, Rect r, float s)
        {
            int lv = _runner.Sandbox ? 0 : _runner.Progress.AirframeLevels[(int)_stat];
            GUI.Label(new Rect(r.x, y, r.width, 42f * s), Airframe.Names[(int)_stat].ToUpperInvariant(), new GUIStyle(_heading) { fontSize = Mathf.RoundToInt(30 * s) });
            y += 46f * s;
            Lbl(new Rect(r.x, y, r.width, 22f * s), "Level " + lv + " of " + Airframe.MaxLevel, s, Cyan);
            Pips(r.x + 200f * s, y + 11f * s, lv, Airframe.MaxLevel, s, Cyan);
            y += 34f * s;
            GUI.Label(new Rect(r.x, y, r.width, 50f * s), Airframe.Descriptions[(int)_stat], _small);
            y += 56f * s;
            Lbl(new Rect(r.x, y, r.width, 22f * s), "Now", s);
            Tracked(new Rect(r.x + 90f * s, y, r.width, 22f * s), Airframe.Value(_stat, lv).ToUpperInvariant(), _tButton, 1.5f * s, Palette.HudText);
            y += 34f * s;
            if (_runner.Sandbox)
            {
                GUI.Label(new Rect(r.x, y, r.width, 40f * s), "Upgrades are bought outside the sandbox.", _small);
                y += 40f * s;
                return;
            }
            var p = _runner.Progress;
            int cost = p.AirframeCost(_stat);
            if (cost < 0)
            {
                Lbl(new Rect(r.x, y, r.width, 22f * s), "Fully upgraded", s, Palette.HudGold);
                y += 32f * s;
                return;
            }
            Lbl(new Rect(r.x, y, r.width, 22f * s), "Next", s);
            Tracked(new Rect(r.x + 90f * s, y, r.width, 22f * s), Airframe.Value(_stat, lv + 1).ToUpperInvariant(), _tButton, 1.5f * s, Cyan);
            y += 44f * s;
            if (BigButton(new Rect(r.x, y, r.width, 58f * s), "Upgrade to Lv " + (lv + 1), s, p.CanUpgradeAirframe(_stat), cost.ToString("N0") + " CR")) _runner.UpgradeAirframe(_stat);
            y += 68f * s;
        }

        // ------------------------------------------------------------------ compact (phone) layout

        void HangarCompact(Rect field, float s)
        {
            float pad = 8f * s;
            var box = new Rect(field.x + pad, field.y + field.height * 0.36f, field.width - pad * 2f, field.height * 0.64f - pad);
            string credits = _runner.Sandbox ? "Sandbox" : _runner.Progress.Credits.ToString("N0") + " cr";
            var r = PanelBox(box, s, "Hangar", credits, Palette.HudGold);
            float y = r.y;
            Tabs(ref y, r, s);
            y += 10f * s;
            if (_tab == Tab.Airframe)
            {
                AirframeList(ref y, r, s, 40f * s, columns: 2);
                AirframeDetails(ref y, r, s);
            }
            else
            {
                WeaponList(ref y, r, s, 40f * s, columns: 2);
                var w = Viewing;
                if (w != null)
                {
                    bool owned = _runner.Sandbox || _runner.Progress.Owns(w);
                    int level = owned ? OwnedLevel(w) : 1;
                    if (_runner.Sandbox) LevelButtons(ref y, r, s, new Fitted(w, level));
                    GUI.Label(new Rect(r.x, y + 4f * s, r.width, 44f * s), StatsText(w, level).Replace("\n", " · "), _small);
                    y += 48f * s;
                    if (!_runner.Sandbox)
                    {
                        var p = _runner.Progress;
                        var br = new Rect(r.x, y, r.width, 46f * s);
                        if (!owned) { if (BigButton(br, "Buy", s, p.CanBuy(w), w.Price.ToString("N0"))) _runner.Buy(w, TabSide); }
                        else if (p.UpgradeCost(w) >= 0 && BigButton(br, "Lv " + (level + 1), s, p.CanUpgrade(w), p.UpgradeCost(w).ToString("N0"))) _runner.Upgrade(w);
                    }
                }
            }
            float launchH = 52f * s;
            if (GhostButton(new Rect(r.x, r.yMax - launchH, r.width * 0.3f, launchH), "‹ Map", s)) _runner.OpenMap();
            if (BigButton(new Rect(r.x + r.width * 0.32f, r.yMax - launchH, r.width * 0.68f, launchH), "Launch", s)) _runner.Launch();
        }

        // ------------------------------------------------------------------ pieces

        void Tabs(ref float y, Rect r, float s)
        {
            var tabs = new[] { Tab.LeftWing, Tab.RightWing, Tab.Core, Tab.Airframe };
            int pick = Segmented(new Rect(r.x, y, r.width, 44f * s), s, (int)_tab, new[] { "Left wing", "Right wing", "Core", "Airframe" });
            if (pick != (int)_tab) { _tab = tabs[pick]; _viewing = null; }
            y += 44f * s;
        }

        /// <summary>A list row: accent strip, index, name, then tags right-aligned.</summary>
        bool Row(Rect rect, float s, string index, string name, bool fitted, bool viewing, bool dim)
        {
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            if (fitted)
            {
                DrawRect(rect, new Color(0.16f, 0.78f, 1f, 0.18f));
                Outline(rect, new Color(0.5f, 0.88f, 1f));
            }
            else
            {
                DrawRect(rect, new Color(0f, 0.04f, 0.07f, hover || viewing ? 0.95f : 0.7f));
                Outline(rect, viewing ? new Color(0.5f, 0.88f, 1f, 0.8f) : hover ? new Color(0.16f, 0.78f, 1f, 0.7f) : new Color(0.16f, 0.62f, 0.85f, dim ? 0.15f : 0.3f));
            }
            DrawRect(new Rect(rect.x, rect.y, 5f * s, rect.height), fitted ? Cyan : dim ? new Color(0.15f, 0.2f, 0.24f) : new Color(0.11f, 0.37f, 0.49f));
            var text = dim ? new Color(0.45f, 0.53f, 0.6f) : Palette.HudText;
            float x = rect.x + 18f * s;
            if (index != null)
            {
                Tracked(new Rect(x, rect.y, 40f * s, rect.height), index, _tIndex, 1f * s, dim ? new Color(0.4f, 0.48f, 0.55f) : new Color(0.75f, 0.83f, 0.9f));
                x += 42f * s;
            }
            Tracked(new Rect(x, rect.y, rect.width, rect.height), name.ToUpperInvariant(), _tButton, 2.6f * s, text);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        void WeaponList(ref float y, Rect r, float s, float rowHeight, int columns = 1)
        {
            var list = _tab == Tab.Core ? Weapons.CoreWeapons : Weapons.WingWeapons;
            var fitted = TabFit.Weapon;
            var viewing = Viewing;
            float gap = 8f * s;
            float w = (r.width - gap * (columns - 1)) / columns;
            var p = _runner.Progress;
            for (int i = 0; i < list.Length; i++)
            {
                var weapon = list[i];
                int col = i % columns, row = i / columns;
                var rect = new Rect(r.x + col * (w + gap), y + row * (rowHeight + gap), w, rowHeight);
                bool owned = _runner.Sandbox || p.Owns(weapon);
                bool affordable = owned || p.Credits >= weapon.Price;
                bool clicked = Row(rect, s, columns == 1 ? (i + 1).ToString("00") : null, weapon.Name, weapon == fitted, weapon == viewing, !affordable);
                if (columns == 1)
                {
                    float right = rect.xMax - 14f * s;
                    if (weapon == fitted) right -= Chip(right, rect.center.y, "FITTED", Cyan, s) + 10f * s;
                    else if (owned && !_runner.Sandbox) right -= Chip(right, rect.center.y, "OWNED", Palette.HudDim, s) + 10f * s;
                    else if (!owned) right -= Chip(right, rect.center.y, weapon.Price.ToString("N0") + " CR", affordable ? Palette.HudGold : new Color(0.45f, 0.53f, 0.6f), s) + 10f * s;
                    if (owned && !_runner.Sandbox)
                    {
                        int lv = p.LevelOf(weapon.Id);
                        Pips(right - PipsWidth(weapon.MaxLevel, s), rect.center.y, lv, weapon.MaxLevel, s, Cyan);
                    }
                }
                if (clicked)
                {
                    _viewing = weapon;
                    // Owned weapons fit straight away (so the preview shows them); others just open their details.
                    if (owned && weapon != fitted) _runner.Fit(TabSlot, TabSide, weapon, OwnedLevel(weapon));
                }
            }
            y += (list.Length + columns - 1) / columns * (rowHeight + gap);
        }

        void AirframeList(ref float y, Rect r, float s, float rowHeight, int columns = 1)
        {
            float gap = 8f * s;
            float w = (r.width - gap * (columns - 1)) / columns;
            var p = _runner.Progress;
            for (int i = 0; i < Airframe.StatCount; i++)
            {
                var stat = (AirframeStat)i;
                int lv = _runner.Sandbox ? 0 : p.AirframeLevels[i];
                int col = i % columns, row = i / columns;
                var rect = new Rect(r.x + col * (w + gap), y + row * (rowHeight + gap), w, rowHeight);
                if (Row(rect, s, columns == 1 ? (i + 1).ToString("00") : null, Airframe.Names[i], false, _stat == stat, false)) _stat = stat;
                if (columns == 1)
                {
                    float right = rect.xMax - 14f * s;
                    int cost = p.AirframeCost(stat);
                    if (!_runner.Sandbox)
                        right -= Chip(right, rect.center.y, cost < 0 ? "MAX" : cost.ToString("N0") + " CR", cost < 0 ? Cyan : p.Credits >= cost ? Palette.HudGold : Palette.HudDim, s) + 10f * s;
                    Pips(right - PipsWidth(Airframe.MaxLevel, s), rect.center.y, lv, Airframe.MaxLevel, s, Cyan);
                }
            }
            y += (Airframe.StatCount + columns - 1) / columns * (rowHeight + gap) + 6f * s;
        }

        void LevelButtons(ref float y, Rect r, float s, Fitted fit)
        {
            var labels = new string[fit.Weapon.MaxLevel];
            for (int i = 0; i < labels.Length; i++) labels[i] = "Lv " + (i + 1);
            int pick = Segmented(new Rect(r.x, y, r.width, 40f * s), s, fit.Level - 1, labels);
            if (pick != fit.Level - 1) _runner.Fit(TabSlot, TabSide, fit.Weapon, pick + 1);
            y += 40f * s;
        }

        void StageButtons(float y, Rect r, float s)
        {
            var options = new[] { (Stages.FirstLight, "1-1"), (Stages.ArmadaSampler, "Sampler"), ((StageDef)null, "Practice") };
            int current = -1;
            for (int i = 0; i < options.Length; i++) if (_runner.SelectedStage == options[i].Item1) current = i;
            var labels = new string[options.Length];
            for (int i = 0; i < options.Length; i++) labels[i] = options[i].Item2;
            int pick = Segmented(new Rect(r.x, y, r.width, 40f * s), s, current, labels);
            if (pick != current && pick >= 0) _runner.SelectedStage = options[pick].Item1;
        }

        /// <summary>The numbers that matter for this weapon at this level, in plain words.</summary>
        static string StatsText(WeaponDef w, int level)
        {
            var st = w.At(level);
            switch (w.Kind)
            {
                case WeaponKind.PulseRepeater:
                    return $"{st.Count} stream{(st.Count > 1 ? "s" : "")}, {st.ShotsPerSecond:0.#} volleys/s · {st.Dps:0} damage/s" +
                           (st.SpecialEvery > 0 ? $"\nLv 5: a charged, piercing bolt every {st.SpecialEvery}th volley" : st.AngleStep > 0f ? "\nOuter streams fan out" : "");
                case WeaponKind.ScatterCannon:
                    return $"{st.Count} pellets over {st.Spread:0}°, {st.ShotsPerSecond:0.#} volleys/s\n{st.Damage:0.##} damage per pellet · short range" +
                           (st.Burst > 0 ? $"\nLv 5: pellets burst into {st.Burst} shards" : "");
                case WeaponKind.LanceDriver:
                    return $"{st.Count} spear{(st.Count > 1 ? "s" : "")}, {st.ShotsPerSecond:0.##} volleys/s, {st.Damage:0.#} damage each\nPierces every enemy in its path" +
                           (st.SpecialEvery > 0 ? $"\nLv 5: every {st.SpecialEvery}rd volley fires a giant spear" : "");
                case WeaponKind.SunlineBeam:
                    return $"{st.Count} strand{(st.Count > 1 ? "s" : "")}, {st.Damage:0} → {st.DamageMax:0} damage/s over {st.RampTime:0.#} s" +
                           (st.PassThrough ? "\nBurns through to a second enemy" : "") + (st.PulseDamage > 0f ? "\nLv 5: pulses at full power" : "");
                case WeaponKind.SeekerPods:
                    return $"{st.Count} missile{(st.Count > 1 ? "s" : "")} every {1f / st.ShotsPerSecond:0.##} s, {st.Damage:0.#} damage\nBlast radius {st.Blast:0.#}" +
                           (st.Burst > 0 ? $"\nLv 5: each blast scatters {st.Burst} bomblets" : "");
                case WeaponKind.HornetRack:
                    return $"{st.Count} micro-missiles every {1f / st.ShotsPerSecond:0.##} s\n{st.Damage:0.##} damage each, spread over targets" +
                           (st.Life > 3f ? "\nLv 5: darts that miss circle back" : "");
                case WeaponKind.ArcCoil:
                    return $"{st.ShotsPerSecond:0.##} strikes/s, {st.Damage:0.#} damage\nJumps to {st.Jumps} more · reach {st.Range:0.#}" +
                           (st.Fork > 1 ? "\nLv 5: every strike forks to two targets" : "");
                case WeaponKind.RicochetDisc:
                    return $"{st.Count} disc{(st.Count > 1 ? "s" : "")} every {1f / st.ShotsPerSecond:0.##} s, {st.Damage:0.#} damage\nUp to {st.MaxHits} hits each, bounces off the sides" +
                           (st.Split ? "\nLv 5: discs split in two on their last hit" : "");
                case WeaponKind.PulseNova:
                    return $"Clears every bullet on screen\n{st.Damage:0} damage to every enemy in view";
                case WeaponKind.SolarLance:
                    return $"{st.Damage:0} damage per second for {st.Duration:0.#} s\nBeam {st.Width:0.#} wide, cancels bullets";
                case WeaponKind.AegisField:
                    return $"Absorbs every hit for {st.Duration:0.#} s\nNearby bullets slow to {st.Factor * 100f:0}%";
                case WeaponKind.Wingmates:
                    return $"{st.Count} drones for {st.Duration:0.#} s\nEach copies your wings at {st.Factor * 100f:0}% damage";
                default:
                    return "";
            }
        }
    }
}
