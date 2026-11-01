using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// The world map (docs/DESIGN.md §9): World 1's stages as nodes along a dotted path over the
    /// turning Earth, each showing its three medals; locked stages are dimmed. The side panels hold
    /// credits and medal totals, the sandbox switch and a reset, and the chosen stage's details
    /// with the way into the hangar. Stages not built yet say so.
    /// </summary>
    public sealed partial class Hud
    {
        string _mapSelected = "1-1";
        float _resetArmedUntil = -1f;

        // Where each node sits in the field, as fractions (x across, y down): a winding path upwards.
        static readonly Vector2[] NodeAt =
        {
            new Vector2(0.28f, 0.86f), new Vector2(0.68f, 0.74f), new Vector2(0.32f, 0.6f),
            new Vector2(0.7f, 0.46f), new Vector2(0.34f, 0.32f), new Vector2(0.6f, 0.15f),
        };

        WorldStage SelectedWorldStage
        {
            get
            {
                foreach (var st in Stages.World1) if (st.Id == _mapSelected) return st;
                return Stages.World1[0];
            }
        }

        void Map(Rect field, float s, bool panels)
        {
            var nodesArea = field;
            if (panels)
            {
                DrawRect(new Rect(0f, 0f, field.x, Screen.height), Palette.PanelBg);
                DrawRect(new Rect(field.xMax, 0f, Screen.width - field.xMax, Screen.height), Palette.PanelBg);
                float pad = 24f * s;
                MapLeft(new Rect(pad, pad, field.x - pad * 2f, Screen.height - pad * 2f), s);
                MapRight(new Rect(field.xMax + pad, pad, Screen.width - field.xMax - pad * 2f, Screen.height - pad * 2f), s);
            }
            else
            {
                // Phone: nodes in the upper part, the details panel below.
                nodesArea = new Rect(field.x, field.y + 40f * s, field.width, field.height * 0.55f);
                var panel = new Rect(field.x, nodesArea.yMax, field.width, field.yMax - nodesArea.yMax);
                DrawRect(panel, Palette.PanelBg);
                float pad = 8f * s;
                MapRight(new Rect(panel.x + pad, panel.y + pad, panel.width - pad * 2f, panel.height - pad * 2f), s);
                GUI.Label(new Rect(field.x + pad, field.y + pad, field.width, 30f * s), "WORLD 1   " + _runner.Progress.Credits.ToString("N0") + " cr", _title);
                var btn = new GUIStyle(_button) { alignment = TextAnchor.MiddleCenter };
                float bw = 96f * s;
                if (GUI.Button(new Rect(field.xMax - pad - bw, field.y + pad, bw, 32f * s), "SETTINGS", btn)) OpenPanel(Panel.Settings);
                bool vetOpen = _runner.Progress.VeteranUnlocked || _runner.Sandbox;
                if (vetOpen && GUI.Button(new Rect(field.xMax - pad - bw * 2f - 6f * s, field.y + pad, bw, 32f * s), _runner.Veteran ? "VETERAN" : "NORMAL", _runner.Veteran ? _buttonDanger : btn))
                    _runner.SetVeteran(!_runner.Veteran);
            }
            MapNodes(nodesArea, s);
        }

        void MapNodes(Rect area, float s)
        {
            var world = Stages.World1;
            float w = Mathf.Min(170f * s, area.width * 0.42f), h = 58f * s;
            Vector2 At(int i) => new Vector2(area.x + NodeAt[i].x * area.width, area.y + NodeAt[i].y * area.height);

            // Dotted path.
            for (int i = 0; i < world.Length - 1; i++)
            {
                var a = At(i); var b = At(i + 1);
                bool open = _runner.Progress.IsUnlocked(world[i + 1].Id, _runner.Veteran) || _runner.Sandbox;
                int dots = Mathf.CeilToInt(Vector2.Distance(a, b) / (14f * s));
                for (int k = 1; k < dots; k++)
                {
                    var p = Vector2.Lerp(a, b, k / (float)dots);
                    DrawRect(new Rect(p.x - 2f * s, p.y - 2f * s, 4f * s, 4f * s), open ? Palette.HudAccent * 0.8f : Palette.HudBarBg);
                }
            }

            for (int i = 0; i < world.Length; i++)
            {
                var st = world[i];
                var c = At(i);
                var rect = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
                bool unlocked = _runner.Sandbox || _runner.Progress.IsUnlocked(st.Id, _runner.Veteran);
                bool built = st.Def != null;
                var style = st.Id == _mapSelected ? _buttonOn : unlocked && built ? _button : _buttonLocked;
                string sub = !built ? "not built yet" : !unlocked ? "locked" : st.Id == "1-B" ? "BOSS" : "";
                if (GUI.Button(rect, st.Id + "  " + st.Name + (sub.Length > 0 ? "\n" + sub : "\n"), style))
                {
                    _mapSelected = st.Id;
                    if (built && unlocked) _runner.SelectedStage = st.Def;
                }
                if (built) MedalChips(new Rect(rect.x + 10f * s, rect.yMax - 16f * s, rect.width - 20f * s, 10f * s), _runner.Progress.Record(st.Id, _runner.Veteran), s, small: true);
            }
        }

        void MapLeft(Rect r, float s)
        {
            float y = r.y;
            Line(ref y, r, "WORLD 1\nEARTHFALL", _title, 72f * s);
            Line(ref y, r, "The Veil Armada is landing. Fly each stage, earn credits and medals, and upgrade the Vanguard in the hangar.", _small, 64f * s);
            y += 10f * s;
            var p = _runner.Progress;
            Line(ref y, r, "CREDITS", _label, 22f * s);
            Line(ref y, r, p.Credits.ToString("N0"), _gold, 36f * s);
            int medals = 0, possible = 0;
            foreach (var st in Stages.World1) if (st.Def != null) { possible += 3; medals += p.Record(st.Id, _runner.Veteran)?.MedalCount ?? 0; }
            Line(ref y, r, _runner.Veteran ? "MEDALS (VETERAN)" : "MEDALS", _label, 22f * s);
            Line(ref y, r, medals + " / " + possible, _value, 40f * s);
            y += 16f * s;

            Line(ref y, r, "SANDBOX", _label, 22f * s);
            if (GUI.Button(new Rect(r.x, y, r.width, 40f * s), _runner.Sandbox ? "Sandbox ON: everything free, nothing saved" : "Sandbox OFF", _runner.Sandbox ? _buttonOn : _button))
                _runner.SetSandbox(!_runner.Sandbox);
            y += 46f * s;
            Line(ref y, r, "Try any weapon at any level. Credits and medals aren't earned in the sandbox.", _small, 44f * s);
            y += 4f * s;
            Line(ref y, r, "DIFFICULTY", _label, 22f * s);
            bool vetOpen = p.VeteranUnlocked || _runner.Sandbox;
            float half = (r.width - 6f * s) * 0.5f;
            if (GUI.Button(new Rect(r.x, y, half, 36f * s), "Normal", _runner.Veteran ? _button : _buttonOn)) _runner.SetVeteran(false);
            if (GUI.Button(new Rect(r.x + half + 6f * s, y, half, 36f * s), vetOpen ? "Veteran" : "Veteran (locked)", _runner.Veteran ? _buttonDanger : vetOpen ? _button : _buttonLocked) && vetOpen)
                _runner.SetVeteran(true);
            y += 40f * s;
            Line(ref y, r, vetOpen ? "Veteran: faster, denser fire, ×1.5 damage, tougher enemies; ×1.3 credits, ×1.5 score, its own medals."
                                   : "Beat the Tidebreaker to unlock Veteran.", _small, 44f * s);
            y += 8f * s;
            if (GUI.Button(new Rect(r.x, y, r.width * 0.48f, 36f * s), "Armada Sampler", _button)) { _runner.SelectedStage = Stages.ArmadaSampler; _runner.OpenHangar(); }
            if (GUI.Button(new Rect(r.x + r.width * 0.52f, y, r.width * 0.48f, 36f * s), "Practice", _button)) { _runner.SelectedStage = null; _runner.OpenHangar(); }
            y += 42f * s;
            Line(ref y, r, "Test flights: they don't earn anything.", _small, 22f * s);

            float half2 = (r.width - 6f * s) * 0.5f;
            var smallBtn = new GUIStyle(_button) { alignment = TextAnchor.MiddleCenter };
            if (GUI.Button(new Rect(r.x, r.yMax - 112f * s, half2, 36f * s), "< TITLE", smallBtn)) _runner.OpenTitle();
            if (GUI.Button(new Rect(r.x + half2 + 6f * s, r.yMax - 112f * s, half2, 36f * s), "SETTINGS", smallBtn)) OpenPanel(Panel.Settings);

            // Reset: asks twice.
            bool armed = Time.unscaledTime < _resetArmedUntil;
            var resetRect = new Rect(r.x, r.yMax - 70f * s, r.width, 34f * s);
            if (GUI.Button(resetRect, armed ? "Click again to erase all progress" : "Reset progress", armed ? _buttonDanger : _button))
            {
                if (armed) { _resetArmedUntil = -1f; _runner.ResetProgress(); }
                else _resetArmedUntil = Time.unscaledTime + 3f;
            }
            GUI.Label(new Rect(r.x, r.yMax - 26f * s, r.width, 24f * s), "v" + Application.version + (GameSettings.ShowFps ? " · " + Mathf.RoundToInt(_fps) + " fps" : ""), _small);
        }

        void MapRight(Rect r, float s)
        {
            float y = r.y;
            var st = SelectedWorldStage;
            var rec = _runner.Progress.Record(st.Id, _runner.Veteran);
            bool unlocked = _runner.Sandbox || _runner.Progress.IsUnlocked(st.Id, _runner.Veteran);
            Line(ref y, r, (st.Id == "1-B" ? "BOSS STAGE" : "STAGE " + st.Id) + (_runner.Veteran ? " · VETERAN" : ""), _label, 22f * s);
            Line(ref y, r, st.Name, _heading, 34f * s);
            Line(ref y, r, st.Blurb, _body, 30f * s);
            if (st.Def == null)
            {
                Line(ref y, r, "This stage isn't built yet.", _small, 26f * s);
                return;
            }
            if (!unlocked)
            {
                Line(ref y, r, "Clear the stage before it to unlock this one.", _small, 26f * s);
                return;
            }
            Line(ref y, r, "Best score: " + (rec == null || rec.BestScore == 0 ? "—" : rec.BestScore.ToString("N0")), _body, 26f * s);
            y += 6f * s;
            MedalRow(ref y, r, s, "ACE", "score " + Progress.AceScore(st.Def, _runner.Veteran).ToString("N0"), rec?.Ace ?? false, st.Def.MedalBonus[0]);
            MedalRow(ref y, r, s, "UNTOUCHED", "clear with no hull damage", rec?.Untouched ?? false, st.Def.MedalBonus[1]);
            MedalRow(ref y, r, s, "PURGE", "destroy " + Stages.PurgePercent + "% of enemies", rec?.Purge ?? false, st.Def.MedalBonus[2]);
            y += 10f * s;
            float bh = 54f * s;
            if (GUI.Button(new Rect(r.x, Mathf.Max(y, r.yMax - bh - 30f * s), r.width, bh), "TO THE HANGAR", _bigButton))
            {
                _runner.SelectedStage = st.Def;
                _runner.OpenHangar();
            }
            if (!_runner.Controls.UsingTouch)
                GUI.Label(new Rect(r.x, r.yMax - 26f * s, r.width, 24f * s), "Enter: hangar", _small);
        }

        void MedalRow(ref float y, Rect r, float s, string name, string rule, bool earned, int bonus)
        {
            float h = 40f * s;
            DrawRect(new Rect(r.x, y + 6f * s, 14f * s, 14f * s), earned ? Palette.HudGold : Palette.HudBarBg);
            var nameStyle = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = earned ? Palette.HudGold : Palette.HudText } };
            GUI.Label(new Rect(r.x + 22f * s, y, r.width - 22f * s, 22f * s), name + (earned ? "  ✓" : "  +" + bonus + " credits"), nameStyle);
            GUI.Label(new Rect(r.x + 22f * s, y + 18f * s, r.width - 22f * s, 22f * s), rule, _small);
            y += h;
        }

        /// <summary>Three little squares: Ace, Untouched, Purge; gold when earned.</summary>
        void MedalChips(Rect r, StageRecord rec, float s, bool small)
        {
            float gap = 4f * s, w = (r.width - gap * 2f) / 3f;
            bool[] got = { rec?.Ace ?? false, rec?.Untouched ?? false, rec?.Purge ?? false };
            for (int i = 0; i < 3; i++) DrawRect(new Rect(r.x + i * (w + gap), r.y, w, r.height), got[i] ? Palette.HudGold : Palette.HudBarBg);
        }
    }
}
