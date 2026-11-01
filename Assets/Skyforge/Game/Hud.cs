using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// IMGUI heads-up display. On wide screens (PC, web) the score and loadout sit in side panels
    /// beside the play field; on a phone they overlay its top edge. M0 test-flight version: score,
    /// kills, loadout and control hints.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        GameRunner _runner;
        GUIStyle _title, _label, _value, _small, _centre, _hint;
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
            _small = S(14, Palette.HudDim);
            _centre = S(34, Palette.HudText, FontStyle.Bold, TextAnchor.MiddleCenter);
            _hint = S(16, Palette.HudDim, FontStyle.Normal, TextAnchor.UpperCenter);
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
                DrawPanel(new Rect(0f, 0f, sideW, Screen.height));
                DrawPanel(new Rect(field.xMax, 0f, Screen.width - field.xMax, Screen.height));
                float pad = 24f * scale;
                var left = new Rect(pad, pad, sideW - pad * 2f, Screen.height - pad * 2f);
                var right = new Rect(field.xMax + pad, pad, Screen.width - field.xMax - pad * 2f, Screen.height - pad * 2f);
                LeftPanel(left, sim, scale);
                RightPanel(right, scale);
            }
            else
            {
                Overlay(field, sim, scale);
            }

            if (_runner.Paused)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(field, Texture2D.whiteTexture);
                GUI.color = Color.white;
                var mid = new Rect(field.x, field.center.y - 40f * scale, field.width, 50f * scale);
                GUI.Label(mid, "PAUSED", _centre);
                var hint = new Rect(field.x, mid.yMax, field.width, 30f * scale);
                GUI.Label(hint, _runner.Controls.UsingTouch ? "tap to resume" : "Esc or click to resume", _hint);
            }
        }

        static void DrawPanel(Rect r)
        {
            GUI.color = Palette.PanelBg;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void LeftPanel(Rect r, StageSim sim, float s)
        {
            float y = r.y;
            Line(ref y, r, "SKYFORGE\nVANGUARD", _title, 72f * s);
            y += 20f * s;
            Line(ref y, r, "SCORE", _label, 22f * s);
            Line(ref y, r, sim.Score.ToString("N0"), _value, 44f * s);
            y += 8f * s;
            Line(ref y, r, "KILLS", _label, 22f * s);
            Line(ref y, r, sim.Kills.ToString(), _value, 44f * s);
            y += 24f * s;
            Line(ref y, r, "LOADOUT", _label, 22f * s);
            Line(ref y, r, "Left wing   " + Describe(sim.Loadout.LeftWing), _small, 22f * s);
            Line(ref y, r, "Right wing  " + Describe(sim.Loadout.RightWing), _small, 22f * s);
            Line(ref y, r, "Core        " + (sim.Loadout.Core.IsEmpty ? "— (coming in M2)" : Describe(sim.Loadout.Core)), _small, 22f * s);

            var foot = new Rect(r.x, r.yMax - 44f * s, r.width, 44f * s);
            GUI.Label(foot, "v" + Application.version + " · M0 test flight\n" + Mathf.RoundToInt(_fps) + " fps", _small);
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
                "Wings fire automatically.\n\n" +
                "TEST FLIGHT\n" +
                "1–5 — Pulse Repeater level (restarts)\n" +
                "R — restart\n" +
                "Esc / P — pause", _small, 300f * s);
        }

        void Overlay(Rect field, StageSim sim, float s)
        {
            float pad = 12f * s;
            var r = new Rect(field.x + pad, field.y + pad, field.width - pad * 2f, 80f * s);
            GUI.Label(r, sim.Score.ToString("N0"), _value);
            GUI.Label(new Rect(r.x, r.y + 40f * s, r.width, 24f * s), "KILLS " + sim.Kills, _small);
            var v = new Rect(field.x + pad, field.yMax - 30f * s, field.width - pad * 2f, 24f * s);
            GUI.Label(v, "v" + Application.version + " · M0 test flight · " + Mathf.RoundToInt(_fps) + " fps", _small);
        }

        static void Line(ref float y, Rect r, string text, GUIStyle style, float h)
        {
            GUI.Label(new Rect(r.x, y, r.width, h), text, style);
            y += h;
        }

        static string Describe(Fitted f) => f.IsEmpty ? "—" : f.Weapon.Name + "  Lv " + f.Level;
    }
}
