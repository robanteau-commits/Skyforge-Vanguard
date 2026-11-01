using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// IMGUI heads-up display. On wide screens (PC, web) everything sits in side panels beside the
    /// play field; on a phone a compact version overlays the field's top and bottom edges.
    /// Shows score, combo, credits, kills, hull and shield, stage progress, and the results screen.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        /// <summary>Seconds after the stage ends before the results appear.</summary>
        public const float ResultsDelay = 1.5f;

        GameRunner _runner;
        GUIStyle _title, _label, _value, _combo, _small, _centre, _hint, _resultLine, _resultValue, _gold;
        float _styleScale = -1f;
        float _fps, _fpsTimer;
        int _fpsFrames;

        public void Init(GameRunner runner) => _runner = runner;

        void Update()
        {
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps = _fpsFrames / _fpsTimer;
                _fpsFrames = 0;
                _fpsTimer = 0f;
            }
        }

        void Styles(float scale)
        {
            if (Mathf.Abs(scale - _styleScale) < 0.01f && _title != null) return;
            _styleScale = scale;
            GUIStyle S(int size, Color c, FontStyle f = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft) =>
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(9, Mathf.RoundToInt(size * scale)),
                    fontStyle = f,
                    alignment = a,
                    wordWrap = true,
                    normal = { textColor = c },
                };
            _title = S(26, Palette.HudAccent, FontStyle.Bold);
            _label = S(15, Palette.HudDim);
            _value = S(30, Palette.HudText, FontStyle.Bold);
            _combo = S(24, Palette.HudAccent, FontStyle.Bold);
            _gold = S(24, Palette.HudGold, FontStyle.Bold);
            _small = S(14, Palette.HudDim);
            _centre = S(34, Palette.HudText, FontStyle.Bold, TextAnchor.MiddleCenter);
            _hint = S(16, Palette.HudDim, FontStyle.Normal, TextAnchor.UpperCenter);
            _resultLine = S(18, Palette.HudDim, FontStyle.Normal, TextAnchor.MiddleLeft);
            _resultValue = S(20, Palette.HudText, FontStyle.Bold, TextAnchor.MiddleRight);
        }

        void OnGUI()
        {
            if (_runner == null || _runner.Sim == null || _runner.View == null) return;
            var sim = _runner.Sim;
            // IMGUI's origin is top-left; the camera's pixel rect is bottom-left.
            var pr = _runner.View.PixelRect;
            var field = new Rect(pr.x, Screen.height - pr.yMax, pr.width, pr.height);
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
            Styles(scale);

            float sideW = field.x;
            bool panels = sideW >= 170f * scale;
            if (panels)
            {
                DrawRect(new Rect(0f, 0f, sideW, Screen.height), Palette.PanelBg);
                DrawRect(new Rect(field.xMax, 0f, Screen.width - field.xMax, Screen.height), Palette.PanelBg);
                float pad = 24f * scale;
                LeftPanel(new Rect(pad, pad, sideW - pad * 2f, Screen.height - pad * 2f), sim, scale);
                RightPanel(new Rect(field.xMax + pad, pad, Screen.width - field.xMax - pad * 2f, Screen.height - pad * 2f), scale);
            }
            else
            {
                Overlay(field, sim, scale);
            }

            if (sim.Status != StageStatus.Playing && sim.TimeSinceEnd >= ResultsDelay)
                Results(field, sim, scale);
            else if (_runner.Paused)
            {
                DrawRect(field, new Color(0f, 0f, 0f, 0.55f));
                var mid = new Rect(field.x, field.center.y - 40f * scale, field.width, 50f * scale);
                GUI.Label(mid, "PAUSED", _centre);
                GUI.Label(new Rect(field.x, mid.yMax, field.width, 30f * scale),
                    _runner.Controls.UsingTouch ? "tap to resume" : "Esc or click to resume", _hint);
            }
        }

        // ------------------------------------------------------------------ panels

        void LeftPanel(Rect r, StageSim sim, float s)
        {
            float y = r.y;
            Line(ref y, r, "SKYFORGE\nVANGUARD", _title, 72f * s);
            y += 8f * s;
            Line(ref y, r, StageTitle(sim), _small, 22f * s);
            Bar(ref y, r, sim.Progress, Palette.HudDim, 6f * s, s);
            y += 16f * s;

            Line(ref y, r, "SCORE", _label, 22f * s);
            Line(ref y, r, sim.Score.ToString("N0"), _value, 42f * s);
            Line(ref y, r, "COMBO", _label, 22f * s);
            Line(ref y, r, "×" + sim.Combo.ToString("0.0"), _combo, 36f * s);
            Line(ref y, r, "CREDITS", _label, 22f * s);
            Line(ref y, r, sim.Credits.ToString("N0"), _gold, 36f * s);
            Line(ref y, r, "KILLS", _label, 22f * s);
            Line(ref y, r, sim.Kills + (sim.Spawned > 0 ? "   (" + sim.KillPercent + "%)" : ""), _value, 42f * s);
            y += 12f * s;

            var p = sim.Player;
            Line(ref y, r, "SHIELD  " + Mathf.CeilToInt(p.Shield) + " / " + Mathf.RoundToInt(p.MaxShield), _label, 22f * s);
            Bar(ref y, r, p.Shield / p.MaxShield, Palette.HudAccent, 12f * s, s);
            y += 8f * s;
            Line(ref y, r, "HULL  " + Mathf.CeilToInt(p.Hull) + " / " + Mathf.RoundToInt(p.MaxHull), _label, 22f * s);
            Bar(ref y, r, p.Hull / p.MaxHull, p.Hull < p.MaxHull * 0.3f ? Palette.HudDanger : Palette.HudHull, 12f * s, s);
            y += 16f * s;

            Line(ref y, r, "LOADOUT", _label, 22f * s);
            Line(ref y, r, "Wings  " + Describe(sim.Loadout.LeftWing) + (Same(sim.Loadout) ? "" : " / " + Describe(sim.Loadout.RightWing)), _small, 22f * s);
            Line(ref y, r, "Core  " + (sim.Loadout.Core.IsEmpty ? "— (M2)" : Describe(sim.Loadout.Core)), _small, 22f * s);

            var foot = new Rect(r.x, r.yMax - 44f * s, r.width, 44f * s);
            GUI.Label(foot, "v" + Application.version + " · M1\n" + Mathf.RoundToInt(_fps) + " fps", _small);
        }

        void RightPanel(Rect r, float s)
        {
            float y = r.y;
            Line(ref y, r, "CONTROLS", _label, 26f * s);
            Line(ref y, r,
                "Mouse — the ship follows the cursor\n" +
                "WASD / arrows — fly\n" +
                "Shift or right mouse — precision (half speed)\n" +
                "Gamepad — left stick, LT precision\n" +
                "Touch — drag anywhere\n\n" +
                "Wings fire automatically. Dodge the orange bullets; a red glow means an enemy is about to fire. " +
                "Your shield takes hits first and recharges; hull damage resets the combo. Fly near gold shards to collect them.\n\n" +
                "TEST KEYS\n" +
                "6 — stage 1-1 First Light\n" +
                "7 — Armada Sampler (every enemy)\n" +
                "8 — practice drones\n" +
                "1–5 — Pulse Repeater level (restarts)\n" +
                "R — restart · Esc / P — pause", _small, 420f * s);
        }

        void Overlay(Rect field, StageSim sim, float s)
        {
            float pad = 10f * s;
            float w = field.width - pad * 2f;
            var top = new Rect(field.x + pad, field.y + pad, w, 30f * s);
            GUI.Label(top, sim.Score.ToString("N0"), _value);
            var comboStyle = new GUIStyle(_combo) { alignment = TextAnchor.UpperRight };
            GUI.Label(top, "×" + sim.Combo.ToString("0.0"), comboStyle);
            var credits = new Rect(field.x + pad, top.yMax + 4f * s, w, 24f * s);
            GUI.Label(credits, "CR " + sim.Credits, new GUIStyle(_gold) { fontSize = Mathf.RoundToInt(18 * s) });

            // Bars along the bottom edge.
            var p = sim.Player;
            float barH = 8f * s;
            float y = field.yMax - pad - barH * 2f - 6f * s;
            var row = new Rect(field.x + pad, y, w, 0f);
            Bar(ref y, row, p.Shield / p.MaxShield, Palette.HudAccent, barH, s);
            y += 4f * s;
            Bar(ref y, row, p.Hull / p.MaxHull, p.Hull < p.MaxHull * 0.3f ? Palette.HudDanger : Palette.HudHull, barH, s);
        }

        // ------------------------------------------------------------------ results

        void Results(Rect field, StageSim sim, float s)
        {
            DrawRect(field, new Color(0f, 0f, 0f, 0.7f));
            bool cleared = sim.Status == StageStatus.Cleared;
            float w = Mathf.Min(field.width - 32f * s, 420f * s);
            float x = field.center.x - w * 0.5f;
            float y = field.y + field.height * 0.24f;

            var head = new GUIStyle(_centre) { normal = { textColor = cleared ? Palette.HudAccent : Palette.HudDanger } };
            GUI.Label(new Rect(field.x, y, field.width, 50f * s), cleared ? "STAGE CLEAR" : "SHIP DESTROYED", head);
            y += 50f * s;
            GUI.Label(new Rect(field.x, y, field.width, 28f * s), StageTitle(sim), _hint);
            y += 50f * s;

            ResultRow(ref y, x, w, s, "Score", sim.Score.ToString("N0"));
            ResultRow(ref y, x, w, s, "Enemies destroyed", sim.Spawned > 0 ? sim.KillPercent + "%" : sim.Kills.ToString());
            ResultRow(ref y, x, w, s, "Hull damage", sim.HullDamageTaken <= 0f ? "none" : Mathf.CeilToInt(sim.HullDamageTaken).ToString());
            ResultRow(ref y, x, w, s, cleared ? "Credits" : "Credits kept (half)", sim.CreditsBanked.ToString("N0"));
            y += 24f * s;

            string again = _runner.Controls.UsingTouch ? "Tap to fly again" : "R, click or Space to fly again";
            GUI.Label(new Rect(field.x, y, field.width, 30f * s), again, _hint);
            GUI.Label(new Rect(field.x, y + 30f * s, field.width, 30f * s), "6 First Light · 7 Armada Sampler · 8 Practice", _hint);
        }

        void ResultRow(ref float y, float x, float w, float s, string label, string value)
        {
            var r = new Rect(x, y, w, 34f * s);
            DrawRect(new Rect(x, r.yMax - 1f, w, 1f), Palette.HudBarBg);
            GUI.Label(r, label, _resultLine);
            GUI.Label(r, value, _resultValue);
            y += 38f * s;
        }

        // ------------------------------------------------------------------ helpers

        static string StageTitle(StageSim sim) =>
            sim.Stage == null ? "Practice flight" : (sim.Stage.Id == "sampler" ? "" : "Stage " + sim.Stage.Id + " · ") + sim.Stage.Name;

        static void DrawRect(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        static void Bar(ref float y, Rect r, float fraction, Color fill, float h, float s)
        {
            var back = new Rect(r.x, y, r.width, h);
            DrawRect(back, Palette.HudBarBg);
            DrawRect(new Rect(back.x, back.y, back.width * Mathf.Clamp01(fraction), h), fill);
            y += h;
        }

        static void Line(ref float y, Rect r, string text, GUIStyle style, float h)
        {
            GUI.Label(new Rect(r.x, y, r.width, h), text, style);
            y += h;
        }

        static bool Same(Loadout l) => l.LeftWing.Weapon == l.RightWing.Weapon && l.LeftWing.Level == l.RightWing.Level;

        static string Describe(Fitted f) => f.IsEmpty ? "—" : f.Weapon.Name + " Lv " + f.Level;
    }
}
