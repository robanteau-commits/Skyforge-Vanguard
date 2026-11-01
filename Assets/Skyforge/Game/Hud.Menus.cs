using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Skyforge.Simulation;
using Skyforge.Game.Audio;

namespace Skyforge.Game
{
    /// <summary>
    /// The title screen (Rob's key art, Resources/UI/TitleKeyArt.png, with live buttons laid exactly
    /// over the ones painted in it), the pause menu, the settings / credits / how-to-play panels,
    /// and the on-screen touch buttons (docs/DESIGN.md §8–9). Menus move with the arrow keys,
    /// W/S or the d-pad and confirm with Enter, Space or gamepad A, as well as the mouse.
    /// </summary>
    public sealed partial class Hud
    {
        enum Panel { None, Settings, Credits, Help }
        Panel _panel;
        List<Vector2Int> _resolutions;

        bool PanelOpen => _panel != Panel.None;

        // Keyboard / gamepad menu navigation (read in Update, used in OnGUI).
        int _menuSel, _menuCount;
        bool _menuActivate;
        bool MenuVisible => !PanelOpen && (_runner.InTitle || (_runner.Flying && _runner.Paused));

        void OpenPanel(Panel p)
        {
            _panel = p;
            _runner.MenuOpen = p != Panel.None;
            if (p == Panel.None) GameSettings.Save();
        }

        /// <summary>Menu keys, once per frame (OnGUI runs several times a frame).</summary>
        bool _menuWasVisible;

        void MenuKeys()
        {
            bool visible = MenuVisible;
            if (visible && !_menuWasVisible) { _menuSel = 0; _menuActivate = false; }
            _menuWasVisible = visible;
            if (!visible || _menuCount <= 0) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame);
            if (up) { _menuSel = (_menuSel + _menuCount - 1) % _menuCount; _runner.Audio.Play(Sfx.Click); }
            if (down) { _menuSel = (_menuSel + 1) % _menuCount; _runner.Audio.Play(Sfx.Click); }
            if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) ||
                (pad != null && pad.buttonSouth.wasPressedThisFrame))
                _menuActivate = true;
        }

        /// <summary>A menu entry: clicked, or chosen with the keys; hovering selects it.</summary>
        bool MenuEntry(Rect r, int i, string label, float s)
        {
            bool clicked = MenuButton(r, (i + 1).ToString("00"), label, _menuSel == i, s, out bool hovered);
            if (hovered && (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDown)) _menuSel = i;
            if (_menuActivate && _menuSel == i && Event.current.type == EventType.Repaint) { _menuActivate = false; return true; }
            return clicked;
        }

        // ------------------------------------------------------------------ title

        // Where things sit in the 1920 × 1080 key art.
        const float ArtW = 1920f, ArtH = 1080f;
        static readonly float[] ArtButtonY = { 565f, 646f, 726f, 805f };
        const float ArtButtonX = 87f, ArtButtonW = 412f, ArtButtonH = 65f;

        /// <summary>Where the key art goes: full height, centred on wide screens (dark bars beside it),
        /// left-anchored on narrower ones (the right side is cropped), across the top on portrait screens.</summary>
        static Rect TitleArtRect()
        {
            float sw = Screen.width, sh = Screen.height;
            if (sw < sh) return new Rect(0f, sh * 0.08f, sw, sw * ArtH / ArtW);
            float w = sh * ArtW / ArtH;
            return new Rect(w <= sw ? (sw - w) * 0.5f : 0f, 0f, w, sh);
        }

        static Rect ArtToScreen(Rect art, float x, float y, float w, float h) =>
            new Rect(art.x + x / ArtW * art.width, art.y + y / ArtH * art.height, w / ArtW * art.width, h / ArtH * art.height);

        string FourthEntry =>
            GameRunner.CanQuit ? "Quit" : Application.platform == RuntimePlatform.WebGLPlayer ? "Fullscreen" : "How to play";

        void Title(Rect field, float s)
        {
            float sw = Screen.width, sh = Screen.height;
            DrawRect(new Rect(0f, 0f, sw, sh), new Color(0f, 0.02f, 0.04f, 1f));
            var art = TitleArtRect();
            if (_keyArt != null) GUI.DrawTexture(art, _keyArt, ScaleMode.StretchToFill);
            float k = art.height / ArtH; // the art's own scale
            bool portrait = sw < sh;

            string[] entries = { "Play", "Settings", "Credits", FourthEntry };
            _menuCount = entries.Length;
            if (!portrait)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    var r = ArtToScreen(art, ArtButtonX, ArtButtonY[i], ArtButtonW, ArtButtonH);
                    if (MenuEntry(r, i, entries[i], k)) TitleAction(i);
                }
                // The real version, where the art's placeholder was.
                var build = ArtToScreen(art, 1300f, 1009f, 312f, 28f);
                Tracked(build, "BUILD " + Application.version, _tTag, 3.4f * k, new Color(0.68f, 0.78f, 0.86f), TextAnchor.MiddleRight);
            }
            else
            {
                // Phones: the painted buttons are too small to tap, so cover them and put full-size ones below.
                var painted = ArtToScreen(art, ArtButtonX - 4f, ArtButtonY[0] - 4f, ArtButtonW + 8f, ArtButtonY[3] + ArtButtonH - ArtButtonY[0] + 8f);
                DrawRect(painted, new Color(0f, 0.03f, 0.05f, 0.92f));
                float w = sw - 48f * s, h = 64f * s, y = art.yMax + 40f * s;
                for (int i = 0; i < entries.Length; i++)
                    if (MenuEntry(new Rect(24f * s, y + i * (h + 14f * s), w, h), i, entries[i], s)) TitleAction(i);
                Tracked(new Rect(0f, sh - 50f * s, sw, 24f * s), "BUILD " + Application.version, _tTag, 3.4f * s, Palette.HudDim, TextAnchor.MiddleCenter);
            }
        }

        void TitleAction(int i)
        {
            switch (i)
            {
                case 0: _runner.OpenMap(); break;
                case 1: OpenPanel(Panel.Settings); break;
                case 2: OpenPanel(Panel.Credits); break;
                default:
                    if (GameRunner.CanQuit) _runner.Quit();
                    else if (Application.platform == RuntimePlatform.WebGLPlayer) Screen.fullScreen = !Screen.fullScreen;
                    else OpenPanel(Panel.Help);
                    break;
            }
        }

        // ------------------------------------------------------------------ pause

        void PauseMenu(Rect field, float s)
        {
            DrawRect(field, new Color(0f, 0.01f, 0.03f, 0.72f));
            float w = Mathf.Min(field.width - 32f * s, 470f * s);
            string[] entries = { "Resume", "Settings", "Restart stage", "Hangar", "World map" };
            _menuCount = entries.Length;
            float bh = 58f * s, gap = 12f * s;
            float h = 46f * s + 110f * s + entries.Length * (bh + gap) + 50f * s;
            var box = new Rect(field.center.x - w * 0.5f, field.center.y - h * 0.5f, w, h);
            var inner = PanelBox(box, s, "Flight paused", StageCode(_runner.Sim));
            Glow(new Rect(inner.x, inner.y, inner.width, 56f * s), "PAUSED", new GUIStyle(_centre) { fontSize = Mathf.RoundToInt(44 * s) }, Cyan, Cyan, 3f * s);
            DrawRect(new Rect(inner.center.x - 45f * s, inner.y + 66f * s, 90f * s, 4f * s), Cyan);
            float y = inner.y + 96f * s;
            for (int i = 0; i < entries.Length; i++)
            {
                if (MenuEntry(new Rect(inner.x, y, inner.width, bh), i, entries[i], s))
                {
                    switch (i)
                    {
                        case 0: _runner.Resume(); break;
                        case 1: OpenPanel(Panel.Settings); break;
                        case 2: _runner.Launch(); break;
                        case 3: _runner.OpenHangar(); break;
                        default: _runner.OpenMap(); break;
                    }
                }
                y += bh + gap;
            }
            Tracked(new Rect(inner.x, y, inner.width, 24f * s), _runner.Controls.UsingTouch ? "LEAVING A STAGE BANKS NOTHING" : "ESC  RESUME   •   LEAVING A STAGE BANKS NOTHING",
                _tTag, 2.6f * s, Palette.HudDim, TextAnchor.MiddleCenter);
        }

        static string StageCode(StageSim sim) =>
            sim?.Stage == null ? "Practice" : sim.Stage.Id == "sampler" ? "Test" : (sim.Stage.Id.StartsWith("1-B") ? "Boss" : "0" + sim.Stage.Id);

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
            DrawRect(pause, Fill);
            Outline(pause, new Color(0.16f, 0.62f, 0.85f, 0.7f));
            float bw = 6f * s, bh = 20f * s;
            DrawRect(new Rect(pause.center.x - bw - 3f * s, pause.center.y - bh * 0.5f, bw, bh), Cyan);
            DrawRect(new Rect(pause.center.x + 3f * s, pause.center.y - bh * 0.5f, bw, bh), Cyan);

            float size = 108f * s;
            var core = new Rect(field.xMax - pad - size, field.yMax - pad - size - 34f * s, size, size);
            if (sim.CoreWeapon != null)
            {
                var p = sim.Player;
                float charge = Mathf.Clamp01(p.CoreCharge / PlayerShip.CoreChargeMax);
                DrawRect(core, Fill);
                DrawRect(new Rect(core.x, core.yMax - core.height * charge, core.width, core.height * charge),
                    p.CoreReady ? new Color(0.16f, 0.78f, 1f, 0.85f) : new Color(0.04f, 0.4f, 0.58f, 0.6f));
                Outline(core, p.CoreReady ? Cyan : new Color(0.16f, 0.62f, 0.85f, 0.7f));
                var c = p.CoreReady ? Ink : Palette.HudText;
                Tracked(new Rect(core.x, core.y + core.height * 0.3f, core.width, 22f * s), "CORE", _tLabel, 3f * s, c, TextAnchor.MiddleCenter);
                Tracked(new Rect(core.x, core.y + core.height * 0.52f, core.width, 26f * s),
                    p.CoreActive ? Mathf.CeilToInt(p.CoreTimer) + "S" : p.CoreReady ? "READY" : Mathf.FloorToInt(charge * 100f) + "%", _tButton, 2f * s, c, TextAnchor.MiddleCenter);
            }
            else core = Rect.zero;

            // GUI rects are top-left based; the controls want screen pixels from the bottom-left.
            Rect ToScreen(Rect r) => r.width <= 0f ? Rect.zero : new Rect(r.x, Screen.height - r.yMax, r.width, r.height);
            PlayerControls.PauseButton = ToScreen(pause);
            PlayerControls.CoreButton = ToScreen(core);
        }

        static bool TouchUi => Application.isMobilePlatform || Touchscreen.current != null;

        // ------------------------------------------------------------------ settings, credits, how to play

        void Panels(float s)
        {
            if (!PanelOpen) return;
            if (PlayerControls.BackPressed()) { OpenPanel(Panel.None); return; }
            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0.01f, 0.03f, 0.8f));
            float w = Mathf.Min(Screen.width - 24f * s, 680f * s);
            float h = Mathf.Min(Screen.height - 24f * s, (_panel == Panel.Settings ? 760f : 600f) * s);
            var box = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);
            string head = _panel == Panel.Settings ? "Settings" : _panel == Panel.Credits ? "Credits" : "How to play";
            var inner = PanelBox(box, s, head, "Vanguard Command");
            if (_panel == Panel.Settings) Settings(inner, s);
            else if (_panel == Panel.Credits) Credits(inner, s);
            else Help(inner, s);
            if (BigButton(new Rect(inner.x, inner.yMax - 56f * s, inner.width, 56f * s), "Done", s)) OpenPanel(Panel.None);
        }

        void Settings(Rect r, float s)
        {
            float y = r.y;
            float labelW = r.width * 0.4f, ctrlX = r.x + labelW, ctrlW = r.width - labelW;
            float rowH = 50f * s;

            void Label(string text)
            {
                Lbl(new Rect(r.x, y, labelW, rowH - 8f * s), text, s, Palette.HudText);
            }

            float Slider(string text, float v, float min, float max, string shown)
            {
                Label(text);
                var track = new Rect(ctrlX, y + rowH * 0.5f - 7f * s, ctrlW - 80f * s, 6f * s);
                float f = Mathf.InverseLerp(min, max, v);
                DrawRect(track, Palette.HudBarBg);
                DrawRect(new Rect(track.x, track.y, track.width * f, track.height), Cyan);
                var thumb = new GUIStyle { fixedWidth = 14f * s, fixedHeight = 24f * s, normal = { background = Texture2D.whiteTexture } };
                var none = new GUIStyle { fixedHeight = 24f * s };
                GUI.color = new Color(0.86f, 0.95f, 1f);
                float nv = GUI.HorizontalSlider(new Rect(track.x, track.center.y - 12f * s, track.width, 24f * s), v, min, max, none, thumb);
                GUI.color = Color.white;
                Tracked(new Rect(r.xMax - 70f * s, y, 70f * s, rowH - 8f * s), shown, _tButton, 1.5f * s, Palette.HudText, TextAnchor.MiddleRight);
                y += rowH;
                return nv;
            }

            int Choice(string text, int current, params string[] options)
            {
                Label(text);
                int chosen = Segmented(new Rect(ctrlX, y + 2f * s, ctrlW, rowH - 14f * s), s, current, options);
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
            bool quality = Choice("Graphics", GameSettings.HighQuality ? 0 : 1, "High", "Low") == 0;
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
            Lbl(new Rect(r.x, y, labelW, rowH - 8f * s), "Resolution", s, Palette.HudText);
            if (_resolutions.Count == 0) { y += rowH; return; }
            int cur = _resolutions.IndexOf(new Vector2Int(Screen.width, Screen.height));
            float x = r.x + labelW, w = r.width - labelW, bw = 52f * s, bh = rowH - 14f * s;
            int next = cur;
            if (GhostButton(new Rect(x, y + 2f * s, bw, bh), "<", s)) next = cur <= 0 ? _resolutions.Count - 1 : cur - 1;
            if (GhostButton(new Rect(x + w - bw, y + 2f * s, bw, bh), ">", s)) next = (cur + 1) % _resolutions.Count;
            Tracked(new Rect(x + bw, y + 2f * s, w - bw * 2f, bh), Screen.width + " × " + Screen.height, _tButton, 2f * s, Palette.HudText, TextAnchor.MiddleCenter);
            if (next != cur && next >= 0)
                Screen.SetResolution(_resolutions[next].x, _resolutions[next].y, Screen.fullScreenMode);
            y += rowH;
        }

        void Credits(Rect r, float s)
        {
            float y = r.y;
            Glow(new Rect(r.x, y, r.width, 44f * s), "SKYFORGE", new GUIStyle(_title) { fontSize = Mathf.RoundToInt(40 * s) }, Cyan, Cyan, 2f * s);
            y += 46f * s;
            GUI.Label(new Rect(r.x, y, r.width, 50f * s), "VANGUARD", new GUIStyle(_heading) { fontSize = Mathf.RoundToInt(46 * s) });
            y += 62f * s;
            DrawRect(new Rect(r.x, y, 90f * s, 4f * s), Cyan);
            y += 22f * s;
            Lbl(new Rect(r.x, y, r.width, 24f * s), "Build " + Application.version, s);
            y += 40f * s;
            Line(ref y, r,
                "Created by Rob Anteau.\n\n" +
                "Code, sound effects and music written with Claude (Anthropic); every sound and music " +
                "loop is synthesised in code as the game runs.\n\n" +
                "Ships modelled in Blender. Built with Unity 6 and the Universal Render Pipeline.\n" +
                "Fonts: Orbitron and Rajdhani (SIL Open Font License).\n\n" +
                "Thanks for playing.", _body, r.height - 260f * s);
        }

        void Help(Rect r, float s)
        {
            float y = r.y;
            var rows = TouchUi && Application.isMobilePlatform
                ? new[] { ("Move", "Drag anywhere: the ship follows your finger"), ("Core", "Tap CORE, or tap with a second finger"), ("Pause", "The pause button at the top"), ("Fire", "Automatic") }
                : new[] { ("Mouse", "The ship follows the cursor"), ("WASD", "Fly"), ("Shift", "Precision (half speed)"), ("Space", "Fire the core when it's charged"), ("Esc", "Pause menu"), ("Gamepad", "Stick to fly, A for the core") };
            foreach (var (key, what) in rows)
            {
                Tracked(new Rect(r.x, y, 140f * s, 32f * s), key.ToUpperInvariant(), _tButton, 2.5f * s, Palette.HudText);
                GUI.Label(new Rect(r.x + 150f * s, y + 4f * s, r.width - 150f * s, 32f * s), what, _body);
                y += 38f * s;
            }
            y += 14f * s;
            Line(ref y, r, "Your wings fire on their own. Dodge the orange bullets: a red glow means an enemy is about to fire. " +
                           "The shield takes hits first and recharges; hull damage resets your combo. Fly near gold shards to collect credits, " +
                           "then spend them in the hangar.", _body, 140f * s);
        }
    }
}
