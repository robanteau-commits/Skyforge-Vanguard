using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// The hangar and shop (docs/DESIGN.md §9): tabs for the left wing, right wing, core and the
    /// airframe. Owned weapons fit with a click; others show their price and a BUY button; owned
    /// ones show UPGRADE with the next level's cost; the airframe tab upgrades the ship's stats.
    /// Behind it the ship flies a live practice preview of the current loadout.
    /// In the sandbox everything is free at any level and nothing is saved.
    /// Wide screens use both side panels; phones get a compact panel over the lower field.
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

        void Hangar(Rect field, float s, bool panels)
        {
            if (panels)
            {
                DrawRect(new Rect(0f, 0f, field.x, Screen.height), Palette.PanelBg);
                DrawRect(new Rect(field.xMax, 0f, Screen.width - field.xMax, Screen.height), Palette.PanelBg);
                float pad = 24f * s;
                HangarList(new Rect(pad, pad, field.x - pad * 2f, Screen.height - pad * 2f), s);
                HangarDetails(new Rect(field.xMax + pad, pad, Screen.width - field.xMax - pad * 2f, Screen.height - pad * 2f), s);
            }
            else
            {
                HangarCompact(field, s);
            }
        }

        // ------------------------------------------------------------------ wide layout

        void HangarList(Rect r, float s)
        {
            float y = r.y;
            Line(ref y, r, "HANGAR", _title, 38f * s);
            HangarHeader(ref y, r, s);
            y += 8f * s;
            Tabs(ref y, r, s, vertical: true);
            y += 12f * s;
            if (_tab == Tab.Airframe) AirframeList(ref y, r, s, 44f * s);
            else WeaponList(ref y, r, s, columns: 1, rowHeight: 46f * s);
        }

        /// <summary>Credits and where you're going (or the sandbox notice).</summary>
        void HangarHeader(ref float y, Rect r, float s)
        {
            if (_runner.Sandbox)
            {
                Line(ref y, r, "SANDBOX: everything free, nothing saved", new GUIStyle(_small) { normal = { textColor = Palette.HudGold } }, 22f * s);
            }
            else
            {
                Line(ref y, r, _runner.Progress.Credits.ToString("N0") + " credits", _gold, 32f * s);
            }
            var st = _runner.SelectedStage;
            Line(ref y, r, "Next: " + (st == null ? "practice" : st == Stages.ArmadaSampler ? st.Name + " (test)" : "Stage " + st.Id + " " + st.Name), _small, 22f * s);
        }

        void HangarDetails(Rect r, float s)
        {
            float y = r.y;
            if (_tab == Tab.Airframe) AirframeDetails(ref y, r, s);
            else WeaponDetails(ref y, r, s);

            if (_runner.Sandbox)
            {
                y += 10f * s;
                Line(ref y, r, "STAGE", _label, 22f * s);
                StageButtons(ref y, r, s);
            }

            float launchH = 60f * s;
            float by = r.yMax - launchH - 30f * s;
            if (GUI.Button(new Rect(r.x, by - 44f * s, r.width * 0.4f, 36f * s), "< MAP", _button)) _runner.OpenMap();
            if (GUI.Button(new Rect(r.x, by, r.width, launchH), "LAUNCH", _bigButton)) _runner.Launch();
            GUI.Label(new Rect(r.x, r.yMax - 26f * s, r.width, 24f * s), "Enter: launch · Esc: map · v" + Application.version, _small);
        }

        void WeaponDetails(ref float y, Rect r, float s)
        {
            var w = Viewing;
            if (w == null) return;
            bool owned = _runner.Sandbox || _runner.Progress.Owns(w);
            int level = owned ? OwnedLevel(w) : 1;
            Line(ref y, r, w.Name, _heading, 34f * s);
            Line(ref y, r, w.Role, _label, 24f * s);
            Line(ref y, r, w.Description, _body, 66f * s);
            y += 4f * s;

            if (_runner.Sandbox)
            {
                Line(ref y, r, "LEVEL", _label, 22f * s);
                LevelButtons(ref y, r, s, new Fitted(w, level));
            }
            else
            {
                Line(ref y, r, owned ? "LEVEL " + level + " OF " + w.MaxLevel : "NOT OWNED", _label, 22f * s);
                LevelPips(ref y, r, s, owned ? level : 0, w.MaxLevel);
            }
            y += 6f * s;
            Line(ref y, r, StatsText(w, level), _body, 112f * s);

            if (!_runner.Sandbox)
            {
                var p = _runner.Progress;
                if (!owned)
                {
                    bool can = p.CanBuy(w);
                    if (ShopButton(new Rect(r.x, y, r.width, 44f * s), "BUY · " + w.Price.ToString("N0") + " credits", can)) _runner.Buy(w, TabSide);
                    y += 50f * s;
                }
                else if (p.UpgradeCost(w) >= 0)
                {
                    int cost = p.UpgradeCost(w);
                    if (ShopButton(new Rect(r.x, y, r.width, 44f * s), "UPGRADE TO LV " + (level + 1) + " · " + cost.ToString("N0") + " credits", p.CanUpgrade(w))) _runner.Upgrade(w);
                    y += 50f * s;
                    GUI.Label(new Rect(r.x, y, r.width, 40f * s), "Lv " + (level + 1) + ": " + StatsText(w, level + 1).Split('\n')[0], _small);
                    y += 40f * s;
                }
                else
                {
                    Line(ref y, r, "Fully upgraded", _label, 24f * s);
                }
                if (owned && TabFit.Weapon != w)
                {
                    if (GUI.Button(new Rect(r.x, y, r.width, 36f * s), "FIT TO " + TabName(), _button)) _runner.Fit(TabSlot, TabSide, w, level);
                    y += 42f * s;
                }
            }
            if (TabSlot == WeaponSlot.Wing && owned && TabFit.Weapon == w)
            {
                var other = TabSide == Side.Left ? Side.Right : Side.Left;
                if (GUI.Button(new Rect(r.x, y, r.width, 34f * s), "Fit the same to the " + (other == Side.Left ? "left" : "right") + " wing", _button))
                    _runner.Fit(WeaponSlot.Wing, other, w, level);
                y += 40f * s;
            }
        }

        string TabName() => _tab == Tab.Core ? "CORE" : _tab == Tab.RightWing ? "RIGHT WING" : "LEFT WING";

        void AirframeDetails(ref float y, Rect r, float s)
        {
            int lv = _runner.Sandbox ? 0 : _runner.Progress.AirframeLevels[(int)_stat];
            Line(ref y, r, Airframe.Names[(int)_stat], _heading, 34f * s);
            Line(ref y, r, Airframe.Descriptions[(int)_stat], _body, 48f * s);
            Line(ref y, r, "LEVEL " + lv + " OF " + Airframe.MaxLevel, _label, 22f * s);
            LevelPips(ref y, r, s, lv, Airframe.MaxLevel);
            y += 6f * s;
            Line(ref y, r, "Now: " + Airframe.Value(_stat, lv), _body, 26f * s);
            if (_runner.Sandbox)
            {
                Line(ref y, r, "Upgrades are bought outside the sandbox.", _small, 26f * s);
                return;
            }
            var p = _runner.Progress;
            int cost = p.AirframeCost(_stat);
            if (cost < 0)
            {
                Line(ref y, r, "Fully upgraded", _label, 24f * s);
                return;
            }
            Line(ref y, r, "Next: " + Airframe.Value(_stat, lv + 1), _body, 26f * s);
            y += 4f * s;
            if (ShopButton(new Rect(r.x, y, r.width, 44f * s), "UPGRADE · " + cost.ToString("N0") + " credits", p.CanUpgradeAirframe(_stat))) _runner.UpgradeAirframe(_stat);
            y += 50f * s;
        }

        // ------------------------------------------------------------------ compact (phone) layout

        void HangarCompact(Rect field, float s)
        {
            float pad = 8f * s;
            var panel = new Rect(field.x, field.y + field.height * 0.36f, field.width, field.height * 0.64f);
            DrawRect(panel, Palette.PanelBg);
            var r = new Rect(panel.x + pad, panel.y + pad, panel.width - pad * 2f, panel.height - pad * 2f);
            float y = r.y;
            HangarHeader(ref y, r, s);
            Tabs(ref y, r, s, vertical: false);
            y += 6f * s;
            if (_tab == Tab.Airframe)
            {
                AirframeList(ref y, r, s, 34f * s);
                AirframeDetails(ref y, r, s);
            }
            else
            {
                WeaponList(ref y, r, s, columns: 2, rowHeight: 38f * s);
                var w = Viewing;
                if (w != null)
                {
                    bool owned = _runner.Sandbox || _runner.Progress.Owns(w);
                    int level = owned ? OwnedLevel(w) : 1;
                    if (_runner.Sandbox) LevelButtons(ref y, r, s, new Fitted(w, level));
                    GUI.Label(new Rect(r.x, y, r.width, 40f * s), StatsText(w, level).Replace("\n", " · "), _small);
                    y += 42f * s;
                    if (!_runner.Sandbox)
                    {
                        var p = _runner.Progress;
                        if (!owned && ShopButton(new Rect(r.x, y, r.width, 38f * s), "BUY · " + w.Price.ToString("N0"), p.CanBuy(w))) _runner.Buy(w, TabSide);
                        else if (owned && p.UpgradeCost(w) >= 0 && ShopButton(new Rect(r.x, y, r.width, 38f * s), "UPGRADE TO LV " + (level + 1) + " · " + p.UpgradeCost(w).ToString("N0"), p.CanUpgrade(w))) _runner.Upgrade(w);
                    }
                }
            }
            float launchH = 48f * s;
            if (GUI.Button(new Rect(r.x, r.yMax - launchH, r.width * 0.3f, launchH), "< MAP", _button)) _runner.OpenMap();
            if (GUI.Button(new Rect(r.x + r.width * 0.32f, r.yMax - launchH, r.width * 0.68f, launchH), "LAUNCH", _bigButton)) _runner.Launch();
            GUI.Label(new Rect(field.x + pad, field.y + pad, field.width, 30f * s), "HANGAR", _title);
        }

        // ------------------------------------------------------------------ pieces

        void Tabs(ref float y, Rect r, float s, bool vertical)
        {
            var l = _runner.Loadout;
            var tabs = new[]
            {
                (Tab.LeftWing, "LEFT WING", l.LeftWing),
                (Tab.RightWing, "RIGHT WING", l.RightWing),
                (Tab.Core, "CORE", l.Core),
                (Tab.Airframe, "AIRFRAME", default(Fitted)),
            };
            float gap = 4f * s;
            float h = vertical ? 44f * s : 40f * s;
            float w = vertical ? r.width : (r.width - gap * 3f) / 4f;
            for (int i = 0; i < tabs.Length; i++)
            {
                var (tab, label, fit) = tabs[i];
                var rect = vertical ? new Rect(r.x, y + i * (h + gap), w, h) : new Rect(r.x + i * (w + gap), y, w, h);
                string text = tab == Tab.Airframe ? label + (vertical ? "\nhull, shield, speed, magnet…" : "")
                    : label + "\n" + (fit.IsEmpty ? "empty" : fit.Weapon.Name + (vertical ? "  Lv " + fit.Level : ""));
                if (GUI.Button(rect, text, _tab == tab ? _buttonOn : _button)) { _tab = tab; _viewing = null; }
            }
            y += vertical ? tabs.Length * (h + gap) : h;
        }

        void WeaponList(ref float y, Rect r, float s, int columns, float rowHeight)
        {
            var list = _tab == Tab.Core ? Weapons.CoreWeapons : Weapons.WingWeapons;
            var fitted = TabFit.Weapon;
            var viewing = Viewing;
            float gap = 4f * s;
            float w = (r.width - gap * (columns - 1)) / columns;
            int small = Mathf.RoundToInt(12 * s);
            for (int i = 0; i < list.Length; i++)
            {
                var weapon = list[i];
                int col = i % columns, row = i / columns;
                var rect = new Rect(r.x + col * (w + gap), y + row * (rowHeight + gap), w, rowHeight);
                bool owned = _runner.Sandbox || _runner.Progress.Owns(weapon);
                string status = _runner.Sandbox ? "" : owned ? "Lv " + _runner.Progress.LevelOf(weapon.Id) : weapon.Price.ToString("N0") + " cr";
                string text = columns == 1
                    ? weapon.Name + "   <size=" + small + ">" + status + "</size>\n<size=" + small + ">" + weapon.Role + "</size>"
                    : weapon.Name + (status.Length > 0 ? "  <size=" + small + ">" + status + "</size>" : "");
                var style = weapon == fitted ? _buttonOn : weapon == viewing ? _buttonView : owned ? _button : _buttonLocked;
                style.richText = true;
                if (GUI.Button(rect, text, style))
                {
                    _viewing = weapon;
                    // Owned weapons fit straight away (so the preview shows them); others just open their details.
                    if (owned && weapon != fitted) _runner.Fit(TabSlot, TabSide, weapon, OwnedLevel(weapon));
                }
            }
            y += (list.Length + columns - 1) / columns * (rowHeight + gap);
        }

        void AirframeList(ref float y, Rect r, float s, float rowHeight)
        {
            float gap = 4f * s;
            int columns = rowHeight < 40f * s ? 2 : 1;
            float w = (r.width - gap * (columns - 1)) / columns;
            for (int i = 0; i < Airframe.StatCount; i++)
            {
                var stat = (AirframeStat)i;
                int lv = _runner.Sandbox ? 0 : _runner.Progress.AirframeLevels[i];
                int col = i % columns, row = i / columns;
                var rect = new Rect(r.x + col * (w + gap), y + row * (rowHeight + gap), w, rowHeight);
                if (GUI.Button(rect, Airframe.Names[i] + "   Lv " + lv + "/" + Airframe.MaxLevel, _stat == stat ? _buttonOn : _button)) _stat = stat;
            }
            y += (Airframe.StatCount + columns - 1) / columns * (rowHeight + gap) + 6f * s;
        }

        void LevelButtons(ref float y, Rect r, float s, Fitted fit)
        {
            float gap = 4f * s;
            float w = (r.width - gap * 4f) / 5f;
            float h = 34f * s;
            var centred = new GUIStyle(_button) { alignment = TextAnchor.MiddleCenter };
            var centredOn = new GUIStyle(_buttonOn) { alignment = TextAnchor.MiddleCenter };
            for (int lv = 1; lv <= fit.Weapon.MaxLevel; lv++)
            {
                var rect = new Rect(r.x + (lv - 1) * (w + gap), y, w, h);
                if (GUI.Button(rect, "Lv " + lv, lv == fit.Level ? centredOn : centred) && lv != fit.Level)
                    _runner.Fit(TabSlot, TabSide, fit.Weapon, lv);
            }
            y += h;
        }

        /// <summary>Five little boxes, filled up to the level.</summary>
        void LevelPips(ref float y, Rect r, float s, int level, int max)
        {
            float gap = 4f * s, h = 10f * s;
            float w = (r.width - gap * (max - 1)) / max;
            for (int i = 0; i < max; i++)
                DrawRect(new Rect(r.x + i * (w + gap), y, w, h), i < level ? Palette.HudAccent : Palette.HudBarBg);
            y += h + 4f * s;
        }

        /// <summary>A shop button that's dimmed (and does nothing) when you can't afford it.</summary>
        bool ShopButton(Rect rect, string text, bool enabled)
        {
            if (!enabled)
            {
                GUI.Label(rect, text, _buttonLocked);
                return false;
            }
            return GUI.Button(rect, text, _bigButton);
        }

        void StageButtons(ref float y, Rect r, float s)
        {
            var options = new[] { (Stages.FirstLight, "1-1 First Light"), (Stages.ArmadaSampler, "Armada Sampler"), ((StageDef)null, "Practice") };
            float gap = 4f * s;
            float w = (r.width - gap * 2f) / 3f;
            float h = 38f * s;
            var centred = new GUIStyle(_button) { alignment = TextAnchor.MiddleCenter };
            var centredOn = new GUIStyle(_buttonOn) { alignment = TextAnchor.MiddleCenter };
            for (int i = 0; i < options.Length; i++)
            {
                var (stage, label) = options[i];
                if (GUI.Button(new Rect(r.x + i * (w + gap), y, w, h), label, _runner.SelectedStage == stage ? centredOn : centred))
                    _runner.SelectedStage = stage;
            }
            y += h;
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

        /// <summary>In flight: the core's charge, or the time left on its effect, and READY when it can fire.</summary>
        void CoreMeter(ref float y, Rect r, StageSim sim, float s)
        {
            if (sim.CoreWeapon == null) return;
            var p = sim.Player;
            string label;
            float fill;
            Color colour;
            if (p.CoreActive)
            {
                label = sim.CoreWeapon.Name.ToUpperInvariant() + "  " + Mathf.CeilToInt(p.CoreTimer) + "s";
                fill = p.CoreTimer / Mathf.Max(0.01f, sim.CoreStats.Duration);
                colour = Color.white;
            }
            else if (p.CoreReady)
            {
                bool blink = Mathf.FloorToInt(Time.unscaledTime * 3f) % 2 == 0;
                label = "CORE READY — " + (_runner.Controls.UsingTouch ? "second finger" : "Space / click");
                fill = 1f;
                colour = blink ? Color.white : Palette.HudAccent;
            }
            else
            {
                label = "CORE  " + sim.CoreWeapon.Name + "  " + Mathf.FloorToInt(p.CoreCharge) + "%";
                fill = p.CoreCharge / PlayerShip.CoreChargeMax;
                colour = Palette.HudAccent * 0.7f;
            }
            Line(ref y, r, label, _label, 22f * s);
            Bar(ref y, r, fill, colour, 12f * s, s);
        }
    }
}
