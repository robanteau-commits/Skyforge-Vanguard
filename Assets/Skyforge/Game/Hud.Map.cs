using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// The world map, "Theatre Map" (docs/DESIGN.md §9): World 1's stages as tags along a dotted path
    /// over the turning Earth, each with its three medals; locked stages are dimmed, the boss is red.
    /// Left panel: the world, credits, medals, difficulty, test flights, settings and reset. Right
    /// panel: the selected stage's mission briefing and the way into the hangar. The key art,
    /// darkened, sits behind the side panels.
    /// </summary>
    public sealed partial class Hud
    {
        string _mapSelected = "1-1";
        float _resetArmedUntil = -1f;

        // Where each node sits in the field, as fractions (x across, y down): a winding path upwards.
        static readonly Vector2[] NodeAt =
        {
            new Vector2(0.3f, 0.86f), new Vector2(0.68f, 0.74f), new Vector2(0.32f, 0.6f),
            new Vector2(0.7f, 0.46f), new Vector2(0.34f, 0.32f), new Vector2(0.58f, 0.15f),
        };

        WorldStage SelectedWorldStage
        {
            get
            {
                foreach (var st in Stages.World1) if (st.Id == _mapSelected) return st;
                return Stages.World1[0];
            }
        }

        static bool IsBoss(WorldStage st) => st.Id == "1-B";
        static string NodeCode(WorldStage st) => IsBoss(st) ? "BOSS" : "0" + st.Id;

        void Map(Rect field, float s, bool panels)
        {
            var nodesArea = field;
            string diff = _runner.Veteran ? "[Veteran]" : "[Normal]";
            if (panels)
            {
                ArtBehindPanels(field, 0.32f);
                float m = 60f * s, top = 92f * s, bottom = 92f * s;
                MapLeft(new Rect(m, top, field.x - m - 40f * s, Screen.height - top - bottom), s);
                MapRight(new Rect(field.xMax + 40f * s, top, Screen.width - field.xMax - m - 40f * s, Screen.height - top - bottom), s);
                Frame(s, "Vanguard Command  //  [Theatre map]", "Sector Earth  •  " + diff, "[Enter] Hangar  •  [Esc] Title", "Build " + Application.version);
            }
            else
            {
                // Phone: nodes in the upper part, the briefing below.
                nodesArea = new Rect(field.x, field.y + 56f * s, field.width, field.height * 0.5f);
                var panel = new Rect(field.x + 8f * s, nodesArea.yMax + 6f * s, field.width - 16f * s, field.yMax - nodesArea.yMax - 14f * s);
                MapRight(panel, s);
                float pad = 10f * s;
                Tracked(new Rect(field.x + pad, field.y + pad, field.width, 36f * s), "EARTHFALL", _tButton, 4f * s, Cyan);
                Tracked(new Rect(field.x + pad, field.y + pad + 22f * s, field.width, 30f * s), _runner.Progress.Credits.ToString("N0") + " CR", _tLabel, 2.5f * s, Palette.HudGold);
                float bw = 104f * s;
                if (GhostButton(new Rect(field.xMax - pad - bw, field.y + pad, bw, 38f * s), "Settings", s)) OpenPanel(Panel.Settings);
                bool vetOpen = _runner.Progress.VeteranUnlocked || _runner.Sandbox;
                if (vetOpen && GhostButton(new Rect(field.xMax - pad - bw * 2f - 8f * s, field.y + pad, bw, 38f * s), _runner.Veteran ? "Veteran" : "Normal", s, _runner.Veteran))
                    _runner.SetVeteran(!_runner.Veteran);
            }
            MapNodes(nodesArea, s);
        }

        void MapNodes(Rect area, float s)
        {
            var world = Stages.World1;
            float w = Mathf.Min(250f * s, area.width * 0.44f), h = 66f * s;
            Vector2 At(int i) => new Vector2(area.x + NodeAt[i].x * area.width, area.y + NodeAt[i].y * area.height);

            // Dotted path between the nodes.
            for (int i = 0; i < world.Length - 1; i++)
            {
                var a = At(i); var b = At(i + 1);
                bool open = _runner.Progress.IsUnlocked(world[i + 1].Id, _runner.Veteran) || _runner.Sandbox;
                int dots = Mathf.CeilToInt(Vector2.Distance(a, b) / (12f * s));
                for (int k = 1; k < dots; k++)
                {
                    var p = Vector2.Lerp(a, b, k / (float)dots);
                    DrawRect(new Rect(p.x - 1.5f * s, p.y - 1.5f * s, 3f * s, 3f * s), open ? new Color(0.16f, 0.78f, 1f, 0.8f) : new Color(0.3f, 0.4f, 0.48f, 0.5f));
                }
            }

            for (int i = 0; i < world.Length; i++)
            {
                var st = world[i];
                var c = At(i);
                var r = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
                bool unlocked = _runner.Sandbox || _runner.Progress.IsUnlocked(st.Id, _runner.Veteran);
                bool built = st.Def != null;
                bool selected = st.Id == _mapSelected;
                bool boss = IsBoss(st);
                bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
                var accent = boss ? Palette.HudDanger : Cyan;

                if (selected)
                {
                    if (Event.current.type == EventType.Repaint) (boss ? _buttonDanger : _buttonOn).Draw(r, false, false, false, false);
                }
                else
                {
                    DrawRect(r, new Color(0f, 0.04f, 0.07f, hover ? 0.95f : 0.85f));
                    Outline(r, unlocked ? new Color(accent.r, accent.g, accent.b, hover ? 0.9f : 0.45f) : new Color(0.25f, 0.33f, 0.4f, 0.5f));
                    DrawRect(new Rect(r.x, r.y, 4f * s, r.height), unlocked ? accent : new Color(0.2f, 0.29f, 0.35f));
                }
                var ink = selected ? (boss ? Color.white : Ink) : unlocked ? Palette.HudText : new Color(0.45f, 0.53f, 0.6f);
                var sub = selected ? ink : unlocked ? accent : new Color(0.4f, 0.48f, 0.55f);
                float tx = r.x + 16f * s;
                string code = NodeCode(st) + (!built ? "  •  SOON" : !unlocked ? "  •  LOCKED" : "");
                float cw = Tracked(new Rect(tx, r.y + 8f * s, w, 22f * s), code, _tLabel, 3f * s, sub);
                if (built && unlocked)
                {
                    var rec = _runner.Progress.Record(st.Id, _runner.Veteran);
                    bool[] got = { rec?.Ace ?? false, rec?.Untouched ?? false, rec?.Purge ?? false };
                    for (int k = 0; k < 3; k++)
                    {
                        var dc = new Vector2(tx + cw + 16f * s + k * 16f * s, r.y + 19f * s);
                        if (got[k]) Diamond(dc, 8f * s, selected ? ink : Palette.HudGold);
                        else { Diamond(dc, 8f * s, selected ? ink : sub); Diamond(dc, 5f * s, selected ? (boss ? Palette.HudDanger : Cyan) : new Color(0f, 0.04f, 0.07f)); }
                    }
                }
                Tracked(new Rect(tx, r.y + 30f * s, w, 30f * s), st.Name.ToUpperInvariant(), _tButton, 3f * s, ink);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    _mapSelected = st.Id;
                    if (built && unlocked) _runner.SelectedStage = st.Def;
                }
            }
        }

        void MapLeft(Rect box, float s)
        {
            var r = PanelBox(box, s, "World 01", "Earthfall");
            float y = r.y;
            var p = _runner.Progress;
            GUI.Label(new Rect(r.x, y, r.width, 44f * s), "EARTHFALL", new GUIStyle(_heading) { fontSize = Mathf.RoundToInt(32 * s) });
            y += 46f * s;
            GUI.Label(new Rect(r.x, y, r.width, 80f * s), "The Veil Armada is landing. Fly each stage, earn credits and medals, and upgrade the Vanguard in the hangar.", _small);
            y += 82f * s;

            float half = (r.width - 14f * s) * 0.5f, bh = 92f * s;
            var cr = new Rect(r.x, y, half, bh);
            var md = new Rect(r.x + half + 14f * s, y, half, bh);
            Outline(cr, new Color(1f, 0.71f, 0.24f, 0.4f));
            Outline(md, new Color(0.16f, 0.78f, 1f, 0.3f));
            Lbl(new Rect(cr.x + 16f * s, cr.y + 10f * s, half, 22f * s), "Credits", s);
            GUI.Label(new Rect(cr.x + 16f * s, cr.y + 36f * s, half, 50f * s), p.Credits.ToString("N0"), new GUIStyle(_gold) { fontSize = Mathf.RoundToInt(34 * s) });
            int medals = 0, possible = 0;
            foreach (var st in Stages.World1) if (st.Def != null) { possible += 3; medals += p.Record(st.Id, _runner.Veteran)?.MedalCount ?? 0; }
            Lbl(new Rect(md.x + 16f * s, md.y + 10f * s, half, 22f * s), _runner.Veteran ? "Medals · Vet" : "Medals", s);
            GUI.Label(new Rect(md.x + 16f * s, md.y + 36f * s, half, 50f * s), medals + "<size=" + Mathf.RoundToInt(20 * s) + "><color=#6f8496> / " + possible + "</color></size>",
                new GUIStyle(_value) { fontSize = Mathf.RoundToInt(34 * s) });
            y += bh + 28f * s;

            Lbl(new Rect(r.x, y, r.width, 22f * s), "Difficulty", s);
            y += 28f * s;
            bool vetOpen = p.VeteranUnlocked || _runner.Sandbox;
            int pick = Segmented(new Rect(r.x, y, r.width, 42f * s), s, _runner.Veteran ? 1 : 0,
                new[] { "Normal", vetOpen ? "Veteran" : "Veteran · locked" }, new[] { true, vetOpen });
            if (pick != (_runner.Veteran ? 1 : 0)) _runner.SetVeteran(pick == 1);
            y += 48f * s;
            GUI.Label(new Rect(r.x, y, r.width, 46f * s), vetOpen ? "Veteran: faster, denser fire and tougher enemies, for ×1.3 credits, ×1.5 score and its own medals."
                                                                  : "Beat the Tidebreaker to unlock Veteran.", _small);
            y += 54f * s;

            Lbl(new Rect(r.x, y, r.width, 22f * s), "Test flights", s);
            y += 28f * s;
            float third = (r.width - 16f * s) / 3f, th = 44f * s;
            if (GhostButton(new Rect(r.x, y, third, th), "Sandbox", s, _runner.Sandbox)) _runner.SetSandbox(!_runner.Sandbox);
            if (GhostButton(new Rect(r.x + third + 8f * s, y, third, th), "Sampler", s)) { _runner.SelectedStage = Stages.ArmadaSampler; _runner.OpenHangar(); }
            if (GhostButton(new Rect(r.x + (third + 8f * s) * 2f, y, third, th), "Practice", s)) { _runner.SelectedStage = null; _runner.OpenHangar(); }
            y += th + 8f * s;
            GUI.Label(new Rect(r.x, y, r.width, 46f * s), _runner.Sandbox ? "<color=#ffb43c>Sandbox on: every weapon free at any level; nothing is earned or saved.</color>"
                                                                         : "Test flights don't earn anything.", _small);

            // Bottom row: back to the title, settings, and reset (asks twice).
            float by = r.yMax - 44f * s;
            float q = (r.width - 16f * s) / 3f;
            if (GhostButton(new Rect(r.x, by, q, 44f * s), "Title", s)) _runner.OpenTitle();
            if (GhostButton(new Rect(r.x + q + 8f * s, by, q, 44f * s), "Settings", s)) OpenPanel(Panel.Settings);
            bool armed = Time.unscaledTime < _resetArmedUntil;
            var resetRect = new Rect(r.x + (q + 8f * s) * 2f, by, q, 44f * s);
            bool reset = armed ? GUI.Button(resetRect, "SURE?", _buttonDanger) : GhostButton(resetRect, "Reset", s);
            if (reset)
            {
                if (armed) { _resetArmedUntil = -1f; _runner.ResetProgress(); }
                else _resetArmedUntil = Time.unscaledTime + 3f;
            }
            if (armed) GUI.Label(new Rect(r.x, by - 26f * s, r.width, 24f * s), "<color=#ff4f61>Click SURE? to erase all progress.</color>", _small);
            else if (GameSettings.ShowFps) Lbl(new Rect(r.x, by - 28f * s, r.width, 22f * s), Mathf.RoundToInt(_fps) + " FPS", s);
        }

        void MapRight(Rect box, float s)
        {
            var st = SelectedWorldStage;
            var r = PanelBox(box, s, IsBoss(st) ? "Boss briefing" : "Mission briefing", NodeCode(st) + (_runner.Veteran ? " · Vet" : ""),
                IsBoss(st) ? Palette.HudDanger : (Color?)null);
            float y = r.y;
            bool compact = box.height < 520f * s;
            var rec = _runner.Progress.Record(st.Id, _runner.Veteran);
            bool unlocked = _runner.Sandbox || _runner.Progress.IsUnlocked(st.Id, _runner.Veteran);
            GUI.Label(new Rect(r.x, y, r.width, 40f * s), st.Name.ToUpperInvariant(), new GUIStyle(_heading) { fontSize = Mathf.RoundToInt((compact ? 24 : 30) * s), normal = { textColor = IsBoss(st) ? Palette.HudDanger : Palette.HudText } });
            y += (compact ? 32f : 44f) * s;
            GUI.Label(new Rect(r.x, y, r.width, 50f * s), st.Blurb, _small);
            y += (compact ? 30f : 56f) * s;
            if (st.Def == null || !unlocked)
            {
                Lbl(new Rect(r.x, y, r.width, 24f * s), st.Def == null ? "Not built yet" : "Locked", s, Palette.HudDanger);
                y += 28f * s;
                GUI.Label(new Rect(r.x, y, r.width, 50f * s), st.Def == null ? "This stage isn't built yet." : "Clear the stage before it to unlock this one.", _small);
                return;
            }
            if (!compact)
            {
                Lbl(new Rect(r.x, y, r.width, 22f * s), "Best score", s);
                y += 26f * s;
                GUI.Label(new Rect(r.x, y, r.width, 48f * s), rec == null || rec.BestScore == 0 ? "—" : rec.BestScore.ToString("N0"), new GUIStyle(_value) { fontSize = Mathf.RoundToInt(36 * s) });
                y += 58f * s;
                Lbl(new Rect(r.x, y, r.width, 22f * s), "Medals", s);
                y += 30f * s;
            }
            float mh = (compact ? 44f : 62f) * s, gap = 10f * s;
            MedalRow(new Rect(r.x, y, r.width, mh), s, "Ace", "Score " + Progress.AceScore(st.Def, _runner.Veteran).ToString("N0"), rec?.Ace ?? false, st.Def.MedalBonus[0], compact);
            y += mh + gap;
            MedalRow(new Rect(r.x, y, r.width, mh), s, "Untouched", "Clear without being hit", rec?.Untouched ?? false, st.Def.MedalBonus[1], compact);
            y += mh + gap;
            MedalRow(new Rect(r.x, y, r.width, mh), s, "Purge", "Destroy " + Stages.PurgePercent + "% of enemies", rec?.Purge ?? false, st.Def.MedalBonus[2], compact);

            float bh = (compact ? 52f : 66f) * s;
            if (BigButton(new Rect(r.x, r.yMax - bh, r.width, bh), "To the hangar  ›", s))
            {
                _runner.SelectedStage = st.Def;
                _runner.OpenHangar();
            }
        }

        void MedalRow(Rect r, float s, string name, string rule, bool earned, int bonus, bool compact)
        {
            var gold = Palette.HudGold;
            DrawRect(r, earned ? new Color(1f, 0.71f, 0.24f, 0.06f) : new Color(0f, 0.04f, 0.07f, 0.5f));
            Outline(r, earned ? new Color(1f, 0.71f, 0.24f, 0.55f) : new Color(0.16f, 0.62f, 0.85f, 0.3f));
            Diamond(new Vector2(r.x + 24f * s, r.center.y), 13f * s, earned ? gold : new Color(0.2f, 0.29f, 0.35f));
            float tx = r.x + 48f * s;
            if (compact)
            {
                Tracked(new Rect(tx, r.y, r.width, r.height), name.ToUpperInvariant(), _tButton, 3f * s, earned ? gold : Palette.HudText);
            }
            else
            {
                Tracked(new Rect(tx, r.y + 8f * s, r.width, 26f * s), name.ToUpperInvariant(), _tButton, 3f * s, earned ? gold : Palette.HudText);
                GUI.Label(new Rect(tx, r.y + 34f * s, r.width - 150f * s, 24f * s), rule, _small);
            }
            Chip(r.xMax - 14f * s, r.center.y, earned ? "EARNED" : "+" + bonus + " CR", earned ? gold : Cyan, s);
        }
    }
}
