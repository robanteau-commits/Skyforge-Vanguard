using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// IMGUI heads-up display. On wide screens (PC, web) everything sits in side panels beside the
    /// play field; on a phone a compact version overlays the field's top and bottom edges.
    /// Shows score, combo, credits, kills, hull and shield, stage progress, and the results screen.
    /// </summary>
    public sealed partial class Hud : MonoBehaviour
    {
        /// <summary>Seconds after the stage ends before the results appear.</summary>
        public const float ResultsDelay = 1.5f;

        GameRunner _runner;
        GUIStyle _title, _label, _value, _combo, _small, _centre, _hint, _resultLine, _resultValue, _gold;
        GUIStyle _button, _buttonOn, _buttonView, _buttonLocked, _buttonDanger, _bigButton, _body, _heading;
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
            _body = S(15, Palette.HudText);
            _heading = S(22, Palette.HudText, FontStyle.Bold);
            _button = ButtonStyle(15, scale, new Color(0.09f, 0.11f, 0.15f), new Color(0.14f, 0.17f, 0.23f), Palette.HudText);
            _buttonOn = ButtonStyle(15, scale, new Color(0.07f, 0.3f, 0.38f), new Color(0.09f, 0.36f, 0.45f), Color.white);
            _buttonView = ButtonStyle(15, scale, new Color(0.12f, 0.17f, 0.24f), new Color(0.16f, 0.22f, 0.3f), Color.white);
            _buttonLocked = ButtonStyle(15, scale, new Color(0.06f, 0.07f, 0.09f), new Color(0.08f, 0.09f, 0.12f), Palette.HudDim);
            _buttonDanger = ButtonStyle(15, scale, new Color(0.4f, 0.09f, 0.08f), new Color(0.52f, 0.12f, 0.1f), Color.white);
            _bigButton = ButtonStyle(22, scale, new Color(0.1f, 0.55f, 0.68f), new Color(0.15f, 0.65f, 0.8f), Color.white);
            _bigButton.fontStyle = FontStyle.Bold;
            _bigButton.alignment = TextAnchor.MiddleCenter;
        }

        static readonly System.Collections.Generic.Dictionary<Color, Texture2D> Swatches = new System.Collections.Generic.Dictionary<Color, Texture2D>();

        static Texture2D Swatch(Color c)
        {
            if (Swatches.TryGetValue(c, out var t) && t != null) return t;
            t = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            Swatches[c] = t;
            return t;
        }

        static GUIStyle ButtonStyle(int size, float scale, Color bg, Color hover, Color text)
        {
            return new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(9, Mathf.RoundToInt(size * scale)),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                padding = new RectOffset(Mathf.RoundToInt(10 * scale), Mathf.RoundToInt(8 * scale), Mathf.RoundToInt(4 * scale), Mathf.RoundToInt(4 * scale)),
                normal = { background = Swatch(bg), textColor = text },
                hover = { background = Swatch(hover), textColor = text },
                active = { background = Swatch(hover), textColor = Color.white },
                focused = { background = Swatch(bg), textColor = text },
            };
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
            if (_runner.InMap)
            {
                Map(field, scale, panels);
                return;
            }
            if (_runner.InHangar)
            {
                Hangar(field, scale, panels);
                return;
            }
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
            y += 8f * s;
            CoreMeter(ref y, r, sim, s);
            y += 16f * s;

            Line(ref y, r, "LOADOUT", _label, 22f * s);
            Line(ref y, r, "Wings  " + Describe(sim.Loadout.LeftWing) + (Same(sim.Loadout) ? "" : " / " + Describe(sim.Loadout.RightWing)), _small, 22f * s);
            Line(ref y, r, "Core  " + (sim.Loadout.Core.IsEmpty ? "—" : Describe(sim.Loadout.Core)), _small, 22f * s);

            var foot = new Rect(r.x, r.yMax - 44f * s, r.width, 44f * s);
            GUI.Label(foot, "v" + Application.version + " · M3\n" + Mathf.RoundToInt(_fps) + " fps", _small);
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
                "Space, left click, gamepad A or a second finger — fire the core when it's charged\n\n" +
                "Wings fire automatically. Dodge the orange bullets; a red glow means an enemy is about to fire. " +
                "Your shield takes hits first and recharges; hull damage resets the combo. Fly near gold shards to collect them.\n\n" +
                "H — back to the hangar\n" +
                "R — restart · Esc / P — pause\n" +
                "6 / 7 / 8 — fly 1-1 / Armada Sampler / practice", _small, 440f * s);
        }

        void Overlay(Rect field, StageSim sim, float s)
        {
            float pad = 10f * s;
            float w = field.width - pad * 2f;
            var top = new Rect(field.x + pad, field.y + pad, w, 30f * s);
            GUI.Label(top, sim.Score.ToString("N0"), _value);
            var comboStyle = new GUIStyle(_combo) { alignment = TextAnchor.UpperRight };
            GUI.Label(top, "×" + sim.Combo.ToString("0.0"), comboStyle);
            if (sim.CoreWeapon != null)
            {
                var core = new Rect(field.x + pad, top.yMax + 4f * s, w, 24f * s);
                var coreStyle = new GUIStyle(_small) { alignment = TextAnchor.UpperRight };
                string text = sim.Player.CoreActive ? sim.CoreWeapon.Name + " " + Mathf.CeilToInt(sim.Player.CoreTimer) + "s"
                    : sim.Player.CoreReady ? "CORE READY: tap with a second finger" : "core " + Mathf.FloorToInt(sim.Player.CoreCharge) + "%";
                GUI.Label(core, text, coreStyle);
            }
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
            var res = _runner.LastResult;
            ResultRow(ref y, x, w, s, cleared ? "Credits" : "Credits kept (half)", sim.CreditsBanked.ToString("N0"));
            if (res != null)
            {
                if (cleared && sim.Stage != null)
                {
                    MedalResult(ref y, x, w, s, "Ace", res.Ace, res.NewAce, sim.Stage.MedalBonus[0]);
                    MedalResult(ref y, x, w, s, "Untouched", res.Untouched, res.NewUntouched, sim.Stage.MedalBonus[1]);
                    MedalResult(ref y, x, w, s, "Purge", res.Purge, res.NewPurge, sim.Stage.MedalBonus[2]);
                }
                ResultRow(ref y, x, w, s, "Total credits", res.TotalCredits.ToString("N0"));
                string news = (res.NewBest ? "New best score! " : "") + (res.UnlockedStageId != null ? "Stage " + res.UnlockedStageId + " unlocked." : "");
                if (news.Length > 0) { GUI.Label(new Rect(field.x, y, field.width, 26f * s), news, new GUIStyle(_hint) { normal = { textColor = Palette.HudGold } }); y += 26f * s; }
            }
            else if (_runner.Sandbox || sim.Stage == null || sim.Stage == Stages.ArmadaSampler)
            {
                GUI.Label(new Rect(field.x, y, field.width, 26f * s), "Test flight: nothing is saved.", _hint);
                y += 26f * s;
            }
            y += 18f * s;

            float bh = 48f * s, gap = 10f * s;
            if (GUI.Button(new Rect(x, y, w, bh), "CONTINUE", _bigButton)) _runner.OpenMap();
            y += bh + gap;
            float bw = (w - gap) * 0.5f;
            if (GUI.Button(new Rect(x, y, bw, 40f * s), "FLY AGAIN", _buttonOn)) _runner.Launch();
            if (GUI.Button(new Rect(x + bw + gap, y, bw, 40f * s), "HANGAR", _buttonOn)) _runner.OpenHangar();
            y += 48f * s;
            if (!_runner.Controls.UsingTouch)
                GUI.Label(new Rect(field.x, y, field.width, 30f * s), "Enter: continue · R: fly again · H: hangar", _hint);
        }

        void MedalResult(ref float y, float x, float w, float s, string name, bool earned, bool isNew, int bonus)
        {
            string v = !earned ? "—" : isNew ? "NEW  +" + bonus + " cr" : "earned";
            var r = new Rect(x, y, w, 30f * s);
            GUI.Label(r, "Medal: " + name, _resultLine);
            GUI.Label(r, v, earned ? new GUIStyle(_resultValue) { normal = { textColor = Palette.HudGold } } : _resultValue);
            y += 32f * s;
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
