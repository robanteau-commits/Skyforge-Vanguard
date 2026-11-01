using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// The hangar: pick a weapon and level for the left wing, the right wing and the core, choose a
    /// stage, launch. Behind it the ship flies a live practice preview with the current loadout.
    /// In M2 everything is unlocked for testing; M3 adds prices and the shop.
    /// Wide screens use both side panels; phones get a compact panel over the lower field.
    /// </summary>
    public sealed partial class Hud
    {
        WeaponSlot _slot = WeaponSlot.Wing;
        Side _side = Side.Left;

        Fitted SelectedFit
        {
            get
            {
                var l = _runner.Loadout;
                return _slot == WeaponSlot.Core ? l.Core : l.Wing(_side);
            }
        }

        void Hangar(Rect field, float s, bool panels)
        {
            if (panels)
            {
                DrawRect(new Rect(0f, 0f, field.x, Screen.height), Palette.PanelBg);
                DrawRect(new Rect(field.xMax, 0f, Screen.width - field.xMax, Screen.height), Palette.PanelBg);
                float pad = 24f * s;
                var left = new Rect(pad, pad, field.x - pad * 2f, Screen.height - pad * 2f);
                var right = new Rect(field.xMax + pad, pad, Screen.width - field.xMax - pad * 2f, Screen.height - pad * 2f);
                HangarSlotsAndList(left, s);
                HangarDetails(right, s);
            }
            else
            {
                HangarCompact(field, s);
            }
        }

        // ------------------------------------------------------------------ wide layout

        void HangarSlotsAndList(Rect r, float s)
        {
            float y = r.y;
            Line(ref y, r, "HANGAR", _title, 40f * s);
            Line(ref y, r, "Fit a weapon to each wing and the core. Everything is unlocked for testing; prices arrive with the shop.", _small, 44f * s);
            y += 8f * s;
            SlotButtons(ref y, r, s, vertical: true);
            y += 14f * s;
            Line(ref y, r, _slot == WeaponSlot.Core ? "CORE SPECIALS" : (_side == Side.Left ? "LEFT WING" : "RIGHT WING") + " WEAPONS", _label, 24f * s);
            WeaponList(ref y, r, s, columns: 1, rowHeight: 44f * s);
        }

        void HangarDetails(Rect r, float s)
        {
            float y = r.y;
            var fit = SelectedFit;
            if (!fit.IsEmpty)
            {
                Line(ref y, r, fit.Weapon.Name, _heading, 34f * s);
                Line(ref y, r, fit.Weapon.Role, _label, 24f * s);
                Line(ref y, r, fit.Weapon.Description, _body, 50f * s);
                y += 6f * s;
                Line(ref y, r, "LEVEL", _label, 22f * s);
                LevelButtons(ref y, r, s, fit);
                y += 8f * s;
                Line(ref y, r, StatsText(fit.Weapon, fit.Level), _body, 96f * s);
                if (_slot == WeaponSlot.Wing)
                {
                    if (GUI.Button(new Rect(r.x, y, r.width, 34f * s), "Fit the same to the " + (_side == Side.Left ? "right" : "left") + " wing", _button))
                        _runner.Fit(WeaponSlot.Wing, _side == Side.Left ? Side.Right : Side.Left, fit.Weapon, fit.Level);
                    y += 40f * s;
                }
            }
            y += 12f * s;
            Line(ref y, r, "STAGE", _label, 22f * s);
            StageButtons(ref y, r, s);

            float launchH = 64f * s;
            if (GUI.Button(new Rect(r.x, r.yMax - launchH - 30f * s, r.width, launchH), "LAUNCH", _bigButton)) _runner.Launch();
            GUI.Label(new Rect(r.x, r.yMax - 26f * s, r.width, 24f * s), "Enter to launch · v" + Application.version, _small);
        }

        // ------------------------------------------------------------------ compact (phone) layout

        void HangarCompact(Rect field, float s)
        {
            float pad = 8f * s;
            var panel = new Rect(field.x, field.y + field.height * 0.4f, field.width, field.height * 0.6f);
            DrawRect(panel, Palette.PanelBg);
            var r = new Rect(panel.x + pad, panel.y + pad, panel.width - pad * 2f, panel.height - pad * 2f);
            float y = r.y;
            SlotButtons(ref y, r, s, vertical: false);
            y += 6f * s;
            WeaponList(ref y, r, s, columns: 2, rowHeight: 40f * s);
            y += 4f * s;
            var fit = SelectedFit;
            if (!fit.IsEmpty)
            {
                LevelButtons(ref y, r, s, fit);
                y += 4f * s;
                GUI.Label(new Rect(r.x, y, r.width, 40f * s), StatsText(fit.Weapon, fit.Level).Replace("\n", " · "), _small);
                y += 42f * s;
            }
            StageButtons(ref y, r, s);
            float launchH = 52f * s;
            if (GUI.Button(new Rect(r.x, r.yMax - launchH, r.width, launchH), "LAUNCH", _bigButton)) _runner.Launch();
            GUI.Label(new Rect(field.x + pad, field.y + pad, field.width, 30f * s), "HANGAR", _title);
        }

        // ------------------------------------------------------------------ pieces

        void SlotButtons(ref float y, Rect r, float s, bool vertical)
        {
            var l = _runner.Loadout;
            var slots = new[]
            {
                (WeaponSlot.Wing, Side.Left, "LEFT WING", l.LeftWing),
                (WeaponSlot.Wing, Side.Right, "RIGHT WING", l.RightWing),
                (WeaponSlot.Core, Side.Left, "CORE", l.Core),
            };
            float h = vertical ? 48f * s : 44f * s;
            float w = vertical ? r.width : (r.width - 8f * s) / 3f;
            for (int i = 0; i < slots.Length; i++)
            {
                var (slot, side, label, fit) = slots[i];
                bool on = _slot == slot && (slot == WeaponSlot.Core || _side == side);
                var rect = vertical ? new Rect(r.x, y + i * (h + 6f * s), w, h) : new Rect(r.x + i * (w + 4f * s), y, w, h);
                string text = label + "\n" + (fit.IsEmpty ? "empty" : fit.Weapon.Name + "  Lv " + fit.Level);
                if (GUI.Button(rect, text, on ? _buttonOn : _button)) { _slot = slot; _side = side; }
            }
            y += vertical ? slots.Length * (h + 6f * s) : h;
        }

        void WeaponList(ref float y, Rect r, float s, int columns, float rowHeight)
        {
            var list = _slot == WeaponSlot.Core ? Weapons.CoreWeapons : Weapons.WingWeapons;
            var current = SelectedFit;
            float gap = 4f * s;
            float w = (r.width - gap * (columns - 1)) / columns;
            for (int i = 0; i < list.Length; i++)
            {
                var weapon = list[i];
                int col = i % columns, row = i / columns;
                var rect = new Rect(r.x + col * (w + gap), y + row * (rowHeight + gap), w, rowHeight);
                bool on = current.Weapon == weapon;
                string text = columns == 1 ? weapon.Name + "\n<size=" + Mathf.RoundToInt(12 * s) + ">" + weapon.Role + "</size>" : weapon.Name;
                var style = on ? _buttonOn : _button;
                style.richText = true;
                if (GUI.Button(rect, text, style) && !on)
                    _runner.Fit(_slot, _side, weapon, current.IsEmpty ? 1 : current.Level);
            }
            int rows = (list.Length + columns - 1) / columns;
            y += rows * (rowHeight + gap);
        }

        void LevelButtons(ref float y, Rect r, float s, Fitted fit)
        {
            float gap = 4f * s;
            float w = (r.width - gap * 4f) / 5f;
            float h = 36f * s;
            var centred = new GUIStyle(_button) { alignment = TextAnchor.MiddleCenter };
            var centredOn = new GUIStyle(_buttonOn) { alignment = TextAnchor.MiddleCenter };
            for (int lv = 1; lv <= fit.Weapon.MaxLevel; lv++)
            {
                var rect = new Rect(r.x + (lv - 1) * (w + gap), y, w, h);
                if (GUI.Button(rect, "Lv " + lv, lv == fit.Level ? centredOn : centred) && lv != fit.Level)
                    _runner.Fit(_slot, _side, fit.Weapon, lv);
            }
            y += h;
        }

        void StageButtons(ref float y, Rect r, float s)
        {
            var options = new[] { (Stages.FirstLight, "1-1 First Light"), (Stages.ArmadaSampler, "Armada Sampler"), ((StageDef)null, "Practice") };
            float gap = 4f * s;
            float w = (r.width - gap * 2f) / 3f;
            float h = 40f * s;
            var centred = new GUIStyle(_button) { alignment = TextAnchor.MiddleCenter };
            var centredOn = new GUIStyle(_buttonOn) { alignment = TextAnchor.MiddleCenter };
            for (int i = 0; i < options.Length; i++)
            {
                var (stage, label) = options[i];
                bool on = _runner.SelectedStage == stage;
                if (GUI.Button(new Rect(r.x + i * (w + gap), y, w, h), label, on ? centredOn : centred))
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
                    return $"{st.Count} stream{(st.Count > 1 ? "s" : "")}, {st.ShotsPerSecond:0.#} shots/s\n{st.Damage:0.##} damage per bolt · {st.Dps:0} per second";
                case WeaponKind.ScatterCannon:
                    return $"{st.Count} pellets over {st.Spread:0}°, {st.ShotsPerSecond:0.#} volleys/s\n{st.Damage:0.##} damage per pellet · short range";
                case WeaponKind.LanceDriver:
                    return $"{st.ShotsPerSecond:0.##} bolts/s, {st.Damage:0.#} damage each\nPierces every enemy in its path";
                case WeaponKind.SunlineBeam:
                    return $"{st.Damage:0} → {st.DamageMax:0} damage per second\nRamps up over {st.RampTime:0.#} s on one target";
                case WeaponKind.SeekerPods:
                    return $"{st.Count} missile{(st.Count > 1 ? "s" : "")} every {1f / st.ShotsPerSecond:0.##} s, {st.Damage:0.#} damage\nBlast radius {st.Blast:0.#}";
                case WeaponKind.HornetRack:
                    return $"{st.Count} micro-missiles every {1f / st.ShotsPerSecond:0.##} s\n{st.Damage:0.##} damage each, spread over targets";
                case WeaponKind.ArcCoil:
                    return $"{st.ShotsPerSecond:0.##} strikes/s, {st.Damage:0.#} damage\nJumps to {st.Jumps} more · reach {st.Range:0.#}";
                case WeaponKind.RicochetDisc:
                    return $"{st.Count} disc{(st.Count > 1 ? "s" : "")} every {1f / st.ShotsPerSecond:0.##} s, {st.Damage:0.#} damage\nUp to {st.MaxHits} hits each, bounces off the sides";
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
