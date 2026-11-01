using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Skyforge.Simulation;
using Skyforge.Game.Audio;

namespace Skyforge.Game
{
    /// <summary>
    /// The title screen, the settings and credits panels, the pause menu, and the on-screen touch
    /// buttons (docs/DESIGN.md §8–9).
    /// </summary>
    public sealed partial class Hud
    {
        enum Panel { None, Settings, Credits }
        Panel _panel;
        List<Vector2Int> _resolutions;

        bool PanelOpen => _panel != Panel.None;

        void OpenPanel(Panel p)
        {
            _panel = p;
            _runner.MenuOpen = p != Panel.None;
            if (p == Panel.None) GameSettings.Save();
        }

        // ------------------------------------------------------------------ title

        void Title(Rect field, float s)
        {
            float w = Mathf.Min(Screen.width - 32f * s, 520f * s);
            float x = Screen.width * 0.5f - w * 0.5f;
            float y = Screen.height * 0.16f;
            var big = new GUIStyle(_centre) { fontSize = Mathf.RoundToInt(64 * s), normal = { textColor = Palette.HudAccent } };
            GUI.Label(new Rect(0f, y, Screen.width, 80f * s), "SKYFORGE", big);
            y += 72f * s;
            GUI.Label(new Rect(0f, y, Screen.width, 80f * s), "VANGUARD", new GUIStyle(big) { normal = { textColor = Palette.HudText } });
            y += 92f * s;
            GUI.Label(new Rect(0f, y, Screen.width, 30f * s), "The Veil Armada is landing. Hold the line.", _hint);

            float by = Screen.height * 0.52f, bh = 56f * s, gap = 12f * s;
            if (GUI.Button(new Rect(x, by, w, bh), "PLAY", _bigButton)) _runner.OpenMap();
            by += bh + gap;
            var mid = new GUIStyle(_button) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(19 * s) };
            if (GUI.Button(new Rect(x, by, w, 46f * s), "SETTINGS", mid)) OpenPanel(Panel.Settings);
            by += 46f * s + gap;
            if (GUI.Button(new Rect(x, by, w, 46f * s), "CREDITS", mid)) OpenPanel(Panel.Credits);
            by += 46f * s + gap;
            if (GameRunner.CanQuit && GUI.Button(new Rect(x, by, w, 46f * s), "QUIT", mid)) _runner.Quit();

            var foot = new GUIStyle(_small) { alignment = TextAnchor.LowerCenter };
            GUI.Label(new Rect(0f, Screen.height - 34f * s, Screen.width, 26f * s),
                "v" + Application.version + (_runner.Controls.UsingTouch ? "" : "   ·   Enter / gamepad A: play"), foot);
        }

        // ------------------------------------------------------------------ pause

        void PauseMenu(Rect field, float s)
        {
            DrawRect(field, new Color(0f, 0f, 0f, 0.6f));
            float w = Mathf.Min(field.width - 32f * s, 360f * s);
            float x = field.center.x - w * 0.5f;
            float y = field.y + field.height * 0.25f;
            GUI.Label(new Rect(field.x, y, field.width, 50f * s), "PAUSED", _centre);
            y += 70f * s;
            float bh = 48f * s, gap = 10f * s;
            if (GUI.Button(new Rect(x, y, w, bh + 6f * s), "RESUME", _bigButton)) _runner.Resume();
            y += bh + 6f * s + gap;
            var mid = new GUIStyle(_button) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(18 * s) };
            if (GUI.Button(new Rect(x, y, w, bh), "SETTINGS", mid)) OpenPanel(Panel.Settings);
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, w, bh), "RESTART STAGE", mid)) _runner.Launch();
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, w, bh), "HANGAR", mid)) _runner.OpenHangar();
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, w, bh), "WORLD MAP", mid)) _runner.OpenMap();
            y += bh + gap;
            GUI.Label(new Rect(field.x, y, field.width, 40f * s),
                _runner.Controls.UsingTouch ? "Leaving a stage banks nothing." : "Esc / P: resume   ·   leaving a stage banks nothing", _hint);
        }

        // ------------------------------------------------------------------ touch buttons

        /// <summary>The pause and core buttons for touch play; their screen rects go to the controls.</summary>
        void TouchButtons(Rect field, StageSim sim, float s, bool show)
        {
            if (!show)
            {
                PlayerControls.CoreButton = PlayerControls.PauseButton = Rect.zero;
                return;
            }
            float pad = 10f * s;
            var pause = new Rect(field.center.x - 26f * s, field.y + pad, 52f * s, 44f * s);
            DrawRect(pause, new Color(0.09f, 0.11f, 0.15f, 0.85f));
            float bw = 7f * s, bh = 22f * s;
            DrawRect(new Rect(pause.center.x - bw - 3f * s, pause.center.y - bh * 0.5f, bw, bh), Palette.HudText);
            DrawRect(new Rect(pause.center.x + 3f * s, pause.center.y - bh * 0.5f, bw, bh), Palette.HudText);

            float size = 104f * s;
            var core = new Rect(field.xMax - pad - size, field.yMax - pad - size - 34f * s, size, size);
            if (sim.CoreWeapon != null)
            {
                var p = sim.Player;
                float charge = Mathf.Clamp01(p.CoreCharge / PlayerShip.CoreChargeMax);
                DrawRect(core, new Color(0.06f, 0.08f, 0.12f, 0.8f));
                DrawRect(new Rect(core.x, core.yMax - core.height * charge, core.width, core.height * charge),
                    p.CoreReady ? new Color(0.1f, 0.55f, 0.68f, 0.9f) : new Color(0.1f, 0.3f, 0.38f, 0.7f));
                var label = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = p.CoreReady ? Color.white : Palette.HudDim } };
                GUI.Label(core, p.CoreActive ? Mathf.CeilToInt(p.CoreTimer) + "s" : p.CoreReady ? "CORE\nREADY" : "CORE\n" + Mathf.FloorToInt(charge * 100f) + "%", label);
            }
            else core = Rect.zero;

            // GUI rects are top-left based; the controls want screen pixels from the bottom-left.
            Rect ToScreen(Rect r) => r.width <= 0f ? Rect.zero : new Rect(r.x, Screen.height - r.yMax, r.width, r.height);
            PlayerControls.PauseButton = ToScreen(pause);
            PlayerControls.CoreButton = ToScreen(core);
        }

        static bool TouchUi => Application.isMobilePlatform || Touchscreen.current != null;

        // ------------------------------------------------------------------ settings and credits

        void Panels(float s)
        {
            if (!PanelOpen) return;
            if (PlayerControls.BackPressed()) { OpenPanel(Panel.None); return; }
            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.75f));
            float w = Mathf.Min(Screen.width - 24f * s, 600f * s);
            float h = Mathf.Min(Screen.height - 24f * s, (_panel == Panel.Settings ? 660f : 520f) * s);
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);
            DrawRect(r, Palette.PanelBg);
            var inner = new Rect(r.x + 24f * s, r.y + 20f * s, r.width - 48f * s, r.height - 40f * s);
            if (_panel == Panel.Settings) Settings(inner, s);
            else Credits(inner, s);
            var mid = new GUIStyle(_bigButton) { fontSize = Mathf.RoundToInt(18 * s) };
            if (GUI.Button(new Rect(inner.x, inner.yMax - 46f * s, inner.width, 46f * s), "DONE", mid)) OpenPanel(Panel.None);
        }

        void Settings(Rect r, float s)
        {
            float y = r.y;
            Line(ref y, r, "SETTINGS", _title, 40f * s);
            float labelW = r.width * 0.42f, ctrlX = r.x + labelW, ctrlW = r.width - labelW;
            float rowH = 44f * s;

            void Label(string text) => GUI.Label(new Rect(r.x, y + 8f * s, labelW, rowH), text, _body);

            float Slider(string text, float v, float min, float max, string shown)
            {
                Label(text);
                float nv = GUI.HorizontalSlider(new Rect(ctrlX, y + 16f * s, ctrlW - 70f * s, 20f * s), v, min, max);
                GUI.Label(new Rect(r.xMax - 60f * s, y + 8f * s, 60f * s, rowH), shown, _body);
                y += rowH;
                return nv;
            }

            int Choice(string text, int current, params string[] options)
            {
                Label(text);
                float bw = (ctrlW - 6f * s * (options.Length - 1)) / options.Length;
                int chosen = current;
                for (int i = 0; i < options.Length; i++)
                {
                    var style = new GUIStyle(i == current ? _buttonOn : _button) { alignment = TextAnchor.MiddleCenter };
                    if (GUI.Button(new Rect(ctrlX + i * (bw + 6f * s), y + 4f * s, bw, rowH - 10f * s), options[i], style)) chosen = i;
                }
                y += rowH;
                return chosen;
            }

            GameSettings.MusicVolume = Slider("Music volume", GameSettings.MusicVolume, 0f, 1f, Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%");
            float sfx = Slider("Effects volume", GameSettings.SfxVolume, 0f, 1f, Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%");
            if (Mathf.Abs(sfx - GameSettings.SfxVolume) > 0.001f)
            {
                GameSettings.SfxVolume = sfx;
                _runner.Audio.Play(Sfx.Credit);
            }
            int shake = GameSettings.Shake <= 0f ? 0 : GameSettings.Shake < 0.75f ? 1 : 2;
            int newShake = Choice("Screen shake", shake, "Off", "Low", "Full");
            GameSettings.Shake = newShake == 0 ? 0f : newShake == 1 ? 0.5f : 1f;
            GameSettings.Flashes = Choice("Screen flashes", GameSettings.Flashes ? 0 : 1, "On", "Off") == 0;
            GameSettings.HighContrastBullets = Choice("Enemy bullets", GameSettings.HighContrastBullets ? 1 : 0, "Normal", "High contrast") == 1;
            bool quality = Choice("Graphics", GameSettings.HighQuality ? 0 : 1, "High", "Low (faster)") == 0;
            if (quality != GameSettings.HighQuality) { GameSettings.HighQuality = quality; GameSettings.ApplyQuality(); }
            if (TouchUi)
                GameSettings.TouchSensitivity = Slider("Touch sensitivity", GameSettings.TouchSensitivity, 0.5f, 2f, GameSettings.TouchSensitivity.ToString("0.0") + "×");
            GameSettings.ShowFps = Choice("Show FPS", GameSettings.ShowFps ? 0 : 1, "On", "Off") == 0;

            if (!Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer)
            {
                bool full = Screen.fullScreen;
                bool newFull = Choice("Display", full ? 0 : 1, "Fullscreen", "Window") == 0;
                if (newFull != full)
                    Screen.SetResolution(Screen.width, Screen.height, newFull ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
                ResolutionRow(ref y, r, labelW, rowH, s);
            }
        }

        void ResolutionRow(ref float y, Rect r, float labelW, float rowH, float s)
        {
            if (_resolutions == null)
            {
                _resolutions = new List<Vector2Int>();
                foreach (var res in Screen.resolutions)
                {
                    var v = new Vector2Int(res.width, res.height);
                    if (v.x >= 800 && !_resolutions.Contains(v)) _resolutions.Add(v);
                }
            }
            GUI.Label(new Rect(r.x, y + 8f * s, labelW, rowH), "Resolution", _body);
            if (_resolutions.Count == 0) { y += rowH; return; }
            int cur = _resolutions.IndexOf(new Vector2Int(Screen.width, Screen.height));
            float x = r.x + labelW, w = r.width - labelW, bw = 44f * s;
            var style = new GUIStyle(_button) { alignment = TextAnchor.MiddleCenter };
            int next = cur;
            if (GUI.Button(new Rect(x, y + 4f * s, bw, rowH - 10f * s), "<", style)) next = cur <= 0 ? _resolutions.Count - 1 : cur - 1;
            if (GUI.Button(new Rect(x + w - bw, y + 4f * s, bw, rowH - 10f * s), ">", style)) next = (cur + 1) % _resolutions.Count;
            GUI.Label(new Rect(x + bw, y + 8f * s, w - bw * 2f, rowH), Screen.width + " × " + Screen.height,
                new GUIStyle(_body) { alignment = TextAnchor.UpperCenter });
            if (next != cur && next >= 0)
                Screen.SetResolution(_resolutions[next].x, _resolutions[next].y, Screen.fullScreenMode);
            y += rowH;
        }

        void Credits(Rect r, float s)
        {
            float y = r.y;
            Line(ref y, r, "CREDITS", _title, 44f * s);
            Line(ref y, r, "SKYFORGE VANGUARD", _heading, 34f * s);
            Line(ref y, r, "v" + Application.version, _small, 28f * s);
            Line(ref y, r,
                "Created by Rob Anteau.\n\n" +
                "Code, sound effects and music written with Claude (Anthropic); every sound and music " +
                "loop is synthesised in code as the game runs.\n\n" +
                "Ships modelled in Blender. Built with Unity 6 and the Universal Render Pipeline.\n\n" +
                "Thanks for playing.", _body, r.height - 150f * s);
        }
    }
}
