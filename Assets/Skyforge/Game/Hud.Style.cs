using System.Collections.Generic;
using UnityEngine;

namespace Skyforge.Game
{
    /// <summary>
    /// The HUD's look, taken from the title key art (Resources/UI/TitleKeyArt.png): Orbitron for
    /// titles and numbers, Rajdhani for labels and buttons (both OFL, docs/licenses), cyan on deep
    /// navy, chamfered buttons with a cyan edge strip, framed panels with a header strip, corner
    /// brackets round the screen and small letter-spaced status text. Textures are generated at the
    /// current UI scale so edges stay crisp; IMGUI has no letter spacing, so tracked text is drawn a
    /// character at a time (<see cref="Tracked"/>).
    /// </summary>
    public sealed partial class Hud
    {
        // Colours sampled from the key art.
        static readonly Color Cyan = new Color(0.16f, 0.78f, 1f);
        static readonly Color CyanDeep = new Color(0.04f, 0.4f, 0.58f);
        static readonly Color Ink = new Color(0.01f, 0.07f, 0.11f);
        static readonly Color Line1 = new Color(0.16f, 0.78f, 1f, 0.35f);
        static readonly Color Fill = new Color(0.0f, 0.04f, 0.07f, 0.9f);

        static Font _fDisplay, _fDisplayBold, _fUi, _fUiBold, _fBody;
        static Texture2D _keyArt;

        GUIStyle _title, _label, _value, _combo, _small, _centre, _hint, _resultLine, _resultValue, _gold;
        GUIStyle _button, _buttonOn, _buttonView, _buttonLocked, _buttonDanger, _bigButton, _body, _heading;
        /// <summary>Plain text styles for <see cref="Tracked"/> (left-aligned, no padding).</summary>
        GUIStyle _tTag, _tLabel, _tButton, _tBig, _tIndex;
        GUIStyle _panelStyle, _ghostStyle;
        float _styleScale = -1f;
        readonly List<Texture2D> _made = new List<Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStyleStatics()
        {
            _fDisplay = _fDisplayBold = _fUi = _fUiBold = _fBody = null;
            _keyArt = null;
        }

        static void LoadAssets()
        {
            if (_fUi != null) return;
            _fDisplay = Resources.Load<Font>("Fonts/Orbitron-Black");
            _fDisplayBold = Resources.Load<Font>("Fonts/Orbitron-Bold");
            _fUi = Resources.Load<Font>("Fonts/Rajdhani-SemiBold");
            _fUiBold = Resources.Load<Font>("Fonts/Rajdhani-Bold");
            _fBody = Resources.Load<Font>("Fonts/Rajdhani-Medium");
            _keyArt = Resources.Load<Texture2D>("UI/TitleKeyArt");
        }

        void Styles(float scale)
        {
            if (Mathf.Abs(scale - _styleScale) < 0.01f && _title != null) return;
            _styleScale = scale;
            LoadAssets();
            foreach (var t in _made) if (t != null) Destroy(t);
            _made.Clear();
            _charWidths.Clear();

            GUIStyle S(Font f, int size, Color c, TextAnchor a = TextAnchor.UpperLeft, bool wrap = true) =>
                new GUIStyle(GUI.skin.label)
                {
                    font = f,
                    fontSize = Mathf.Max(9, Mathf.RoundToInt(size * scale)),
                    alignment = a,
                    wordWrap = wrap,
                    richText = true,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = c },
                };
            _title = S(_fDisplay, 30, Cyan);
            _heading = S(_fDisplay, 26, Palette.HudText);
            _value = S(_fDisplayBold, 32, Palette.HudText);
            _combo = S(_fDisplayBold, 26, Cyan);
            _gold = S(_fDisplayBold, 26, Palette.HudGold);
            _label = S(_fUi, 16, Palette.HudDim);
            _small = S(_fBody, 17, Palette.HudDim);
            _body = S(_fBody, 19, Palette.HudText);
            _centre = S(_fDisplay, 42, Palette.HudText, TextAnchor.MiddleCenter);
            _hint = S(_fUi, 17, Palette.HudDim, TextAnchor.UpperCenter);
            _resultLine = S(_fUi, 19, Palette.HudDim, TextAnchor.MiddleLeft);
            _resultValue = S(_fDisplayBold, 21, Palette.HudText, TextAnchor.MiddleRight);

            _tTag = S(_fUi, 15, new Color(0.62f, 0.7f, 0.77f), TextAnchor.MiddleLeft, false);
            _tLabel = S(_fUi, 16, Palette.HudDim, TextAnchor.MiddleLeft, false);
            _tButton = S(_fUiBold, 20, Palette.HudText, TextAnchor.MiddleLeft, false);
            _tBig = S(_fUiBold, 24, Ink, TextAnchor.MiddleLeft, false);
            _tIndex = S(_fUi, 20, new Color(0.8f, 0.88f, 0.94f), TextAnchor.MiddleLeft, false);

            int cut = Mathf.Max(6, Mathf.RoundToInt(12f * scale));
            int bar = Mathf.Max(3, Mathf.RoundToInt(5f * scale));
            var idle = Chamfer(64, cut, Fill, new Color(0.16f, 0.62f, 0.85f, 0.75f), bar, Cyan);
            var hover = Chamfer(64, cut, new Color(0.03f, 0.13f, 0.2f, 0.95f), Cyan, bar, Cyan);
            var view = Chamfer(64, cut, new Color(0.03f, 0.1f, 0.16f, 0.95f), new Color(0.5f, 0.88f, 1f), bar, Cyan);
            var locked = Chamfer(64, cut, new Color(0.01f, 0.03f, 0.05f, 0.88f), new Color(0.2f, 0.3f, 0.38f, 0.8f), bar, new Color(0.15f, 0.25f, 0.32f));
            var on = Gradient(256, cut, CyanDeep * 1.6f, Cyan, new Color(0.05f, 0.42f, 0.62f), new Color(0.6f, 0.92f, 1f));
            var onHover = Gradient(256, cut, CyanDeep * 1.9f, new Color(0.4f, 0.86f, 1f), new Color(0.08f, 0.5f, 0.72f), Color.white);
            var big = Gradient(256, cut, new Color(0.04f, 0.56f, 0.77f), Cyan, new Color(0.31f, 0.83f, 1f), new Color(0.6f, 0.92f, 1f));
            var bigHover = Gradient(256, cut, new Color(0.1f, 0.65f, 0.86f), new Color(0.45f, 0.88f, 1f), new Color(0.55f, 0.9f, 1f), Color.white);
            var danger = Gradient(256, cut, new Color(0.45f, 0.06f, 0.1f), new Color(0.85f, 0.2f, 0.28f), new Color(0.5f, 0.08f, 0.12f), new Color(1f, 0.6f, 0.65f));
            var ghost = Chamfer(64, cut, new Color(0.0f, 0.04f, 0.07f, 0.8f), new Color(0.16f, 0.62f, 0.85f, 0.7f), 0, Cyan);
            var ghostHover = Chamfer(64, cut, new Color(0.03f, 0.13f, 0.2f, 0.95f), Cyan, 0, Cyan);
            var panel = Chamfer(64, 0, Fill, new Color(0.16f, 0.62f, 0.85f, 0.35f), 0, Cyan);

            GUIStyle B(Texture2D bg, Texture2D bgHover, Color text, int size, TextAnchor a, int leftPad, Font f)
            {
                var b = new RectOffset(Mathf.Max(bar + 2, 3), cut + 2, 2, cut + 2);
                return new GUIStyle(GUI.skin.button)
                {
                    font = f,
                    fontSize = Mathf.Max(9, Mathf.RoundToInt(size * scale)),
                    alignment = a,
                    wordWrap = true,
                    richText = true,
                    border = b,
                    padding = new RectOffset(Mathf.RoundToInt(leftPad * scale), Mathf.RoundToInt(12 * scale), Mathf.RoundToInt(4 * scale), Mathf.RoundToInt(4 * scale)),
                    normal = { background = bg, textColor = text },
                    hover = { background = bgHover, textColor = text },
                    active = { background = bgHover, textColor = text },
                    focused = { background = bg, textColor = text },
                    onNormal = { background = bg, textColor = text },
                };
            }
            _button = B(idle, hover, Palette.HudText, 18, TextAnchor.MiddleLeft, 18, _fUiBold);
            _buttonView = B(view, hover, Color.white, 18, TextAnchor.MiddleLeft, 18, _fUiBold);
            _buttonLocked = B(locked, locked, new Color(0.42f, 0.5f, 0.57f), 18, TextAnchor.MiddleLeft, 18, _fUiBold);
            _buttonOn = B(on, onHover, Ink, 18, TextAnchor.MiddleLeft, 18, _fUiBold);
            _buttonDanger = B(danger, danger, Color.white, 18, TextAnchor.MiddleCenter, 12, _fUiBold);
            _bigButton = B(big, bigHover, Ink, 24, TextAnchor.MiddleCenter, 12, _fUiBold);
            _ghostStyle = B(ghost, ghostHover, Palette.HudText, 18, TextAnchor.MiddleCenter, 12, _fUiBold);
            _panelStyle = new GUIStyle { normal = { background = panel }, border = new RectOffset(2, 2, 2, 2) };
        }

        // ------------------------------------------------------------------ generated textures

        Texture2D Made(Texture2D t)
        {
            t.hideFlags = HideFlags.DontSave;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.Apply(false, true);
            _made.Add(t);
            return t;
        }

        /// <summary>A box with a 1-px border and its bottom-right corner cut off; an optional accent strip down the left.</summary>
        Texture2D Chamfer(int size, int cut, Color fill, Color border, int bar, Color barColour)
        {
            int w = size + cut, h = size + cut;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Texture rows go bottom-up; the cut is at the bottom-right of the drawn box.
                    int fromRight = w - 1 - x, fromBottom = y;
                    float d = fromRight + fromBottom - (cut - 1); // < 0 inside the cut-off corner
                    Color c;
                    if (cut > 0 && d < 0f) c = Color.clear;
                    else if (cut > 0 && d < 1.2f) c = border;                       // the diagonal edge
                    else if (x == 0 || y == 0 || x == w - 1 || y == h - 1) c = border;
                    else c = fill;
                    if (bar > 0 && x < bar && c.a > 0f) c = barColour;
                    px[y * w + x] = c;
                }
            t.SetPixels(px);
            return Made(t);
        }

        /// <summary>A horizontal gradient (a → b at a quarter → c) with a lit top edge and the bottom-right corner cut.</summary>
        Texture2D Gradient(int width, int cut, Color a, Color b, Color c, Color edge)
        {
            int h = 32 + cut;
            var t = new Texture2D(width, h, TextureFormat.RGBA32, false);
            var px = new Color[width * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    var col = u < 0.25f ? Color.Lerp(a, b, u / 0.25f) : Color.Lerp(b, c, (u - 0.25f) / 0.75f);
                    col.a = 1f;
                    int fromRight = width - 1 - x;
                    float d = fromRight + y - (cut - 1);
                    if (d < 0f) col = Color.clear;
                    else if (d < 1.2f || y == h - 1 || x == width - 1) col = Color.Lerp(col, edge, 0.6f);
                    px[y * width + x] = col;
                }
            t.SetPixels(px);
            return Made(t);
        }

        // ------------------------------------------------------------------ text

        readonly Dictionary<long, float> _charWidths = new Dictionary<long, float>();
        static readonly GUIContent Scratch = new GUIContent();

        float CharWidth(GUIStyle st, char ch)
        {
            long key = ((long)st.GetHashCode() << 20) ^ ((long)st.fontSize << 16) ^ ch;
            if (_charWidths.TryGetValue(key, out var w)) return w;
            Scratch.text = ch == ' ' ? " " : ch.ToString();
            w = st.CalcSize(Scratch).x;
            if (ch == ' ') w = Mathf.Max(w, st.fontSize * 0.28f);
            _charWidths[key] = w;
            return w;
        }

        /// <summary>Width of <paramref name="text"/> with <paramref name="spacing"/> pixels between characters.</summary>
        float TrackedWidth(string text, GUIStyle st, float spacing)
        {
            float w = 0f;
            for (int i = 0; i < text.Length; i++) w += CharWidth(st, text[i]) + (i > 0 ? spacing : 0f);
            return w;
        }

        /// <summary>One line of letter-spaced text in <paramref name="r"/>, aligned left, centre or right; returns its width.</summary>
        float Tracked(Rect r, string text, GUIStyle st, float spacing, Color colour, TextAnchor align = TextAnchor.MiddleLeft)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float total = TrackedWidth(text, st, spacing);
            float x = align == TextAnchor.MiddleCenter || align == TextAnchor.UpperCenter ? r.center.x - total * 0.5f
                    : align == TextAnchor.MiddleRight || align == TextAnchor.UpperRight ? r.xMax - total : r.x;
            var old = st.normal.textColor;
            st.normal.textColor = colour;
            for (int i = 0; i < text.Length; i++)
            {
                float cw = CharWidth(st, text[i]);
                if (text[i] != ' ')
                {
                    Scratch.text = text[i].ToString();
                    GUI.Label(new Rect(x, r.y, cw + 4f, r.height), Scratch, st);
                }
                x += cw + spacing;
            }
            st.normal.textColor = old;
            return total;
        }

        /// <summary>Text with a soft glow: the same text drawn a few times, faint and offset, under it.</summary>
        void Glow(Rect r, string text, GUIStyle st, Color colour, Color glow, float radius)
        {
            var old = st.normal.textColor;
            st.normal.textColor = new Color(glow.r, glow.g, glow.b, glow.a * 0.16f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                GUI.Label(new Rect(r.x + Mathf.Cos(a) * radius, r.y + Mathf.Sin(a) * radius, r.width, r.height), text, st);
            }
            st.normal.textColor = colour;
            GUI.Label(r, text, st);
            st.normal.textColor = old;
        }

        /// <summary>Small letter-spaced uppercase label, e.g. "CREDITS".</summary>
        void Lbl(Rect r, string text, float s, Color? colour = null, TextAnchor align = TextAnchor.MiddleLeft) =>
            Tracked(r, text.ToUpperInvariant(), _tLabel, 3.2f * s, colour ?? Palette.HudDim, align);

        // ------------------------------------------------------------------ widgets

        static void Line(Rect r, Color c) => DrawRect(r, c);

        /// <summary>A framed panel with a header strip ("WORLD 01 ........ EARTHFALL"); returns the area under the header.</summary>
        Rect PanelBox(Rect r, float s, string header, string right = null, Color? rightColour = null)
        {
            if (Event.current.type == EventType.Repaint) _panelStyle.Draw(r, false, false, false, false);
            if (header == null) return new Rect(r.x + 20f * s, r.y + 18f * s, r.width - 40f * s, r.height - 36f * s);
            float hh = 46f * s;
            var head = new Rect(r.x + 20f * s, r.y, r.width - 40f * s, hh);
            Tracked(head, header.ToUpperInvariant(), _tLabel, 3.4f * s, Cyan);
            if (right != null) Tracked(head, right.ToUpperInvariant(), _tLabel, 3.4f * s, rightColour ?? Palette.HudDim, TextAnchor.MiddleRight);
            DrawRect(new Rect(r.x + 1f, r.y + hh, r.width - 2f, 1f), Line1);
            return new Rect(r.x + 22f * s, r.y + hh + 20f * s, r.width - 44f * s, r.height - hh - 40f * s);
        }

        /// <summary>The title-screen button: index box, letter-spaced label, chevron; cyan gradient when selected.</summary>
        bool MenuButton(Rect r, string index, string label, bool selected, float s, out bool hovered)
        {
            hovered = GUI.enabled && r.Contains(Event.current.mousePosition);
            bool lit = selected || hovered;
            // A solid backing (the chamfered corner left open) so nothing behind shows through.
            float cut = Mathf.Max(6f, 12f * _styleScale);
            DrawRect(new Rect(r.x, r.y, r.width, r.height - cut), new Color(0f, 0.035f, 0.055f));
            DrawRect(new Rect(r.x, r.yMax - cut, r.width - cut, cut), new Color(0f, 0.035f, 0.055f));
            if (Event.current.type == EventType.Repaint)
                (lit ? _buttonOn : _button).Draw(r, false, false, false, false);
            var ink = lit ? Ink : new Color(0.89f, 0.93f, 0.97f);
            float bar = 5f * s, box = 82f * s;
            DrawRect(new Rect(r.x + bar + box, r.y + 1f, 1f, r.height - 2f), lit ? new Color(0f, 0.1f, 0.16f, 0.35f) : new Color(0.16f, 0.62f, 0.85f, 0.4f));
            Tracked(new Rect(r.x + bar, r.y, box, r.height), index, _tIndex, 1.5f * s, lit ? Ink : new Color(0.8f, 0.88f, 0.94f), TextAnchor.MiddleCenter);
            Tracked(new Rect(r.x + bar + box + 23f * s, r.y, r.width, r.height), label.ToUpperInvariant(), _tButton, 5f * s, ink);
            Chevron(new Rect(r.xMax - 40f * s, r.y, 16f * s, r.height), ink, s);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>A big call-to-action button (cyan gradient, dark letter-spaced label).</summary>
        bool BigButton(Rect r, string label, float s, bool enabled = true, string price = null)
        {
            if (!enabled)
            {
                if (Event.current.type == EventType.Repaint) _buttonLocked.Draw(r, false, false, false, false);
                Tracked(r, label.ToUpperInvariant(), _tButton, 4f * s, new Color(0.42f, 0.5f, 0.57f), TextAnchor.MiddleCenter);
                return false;
            }
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint) _bigButton.Draw(r, hover, hover, false, false);
            var st = r.height > 52f * s ? _tBig : _tButton;
            float spacing = (r.height > 52f * s ? 6.5f : 4.5f) * s;
            if (price == null)
            {
                Tracked(r, label.ToUpperInvariant(), st, spacing, Ink, TextAnchor.MiddleCenter);
            }
            else
            {
                // Label then a gold price tag, centred together.
                string text = label.ToUpperInvariant();
                float lw = TrackedWidth(text, st, spacing), pw = TrackedWidth(price, _tButton, 2f * s) + 20f * s;
                float x = r.center.x - (lw + 16f * s + pw) * 0.5f;
                Tracked(new Rect(x, r.y, lw + 4f, r.height), text, st, spacing, Ink);
                var tag = new Rect(x + lw + 16f * s, r.center.y - 15f * s, pw, 30f * s);
                DrawRect(tag, Palette.HudGold);
                Tracked(tag, price, _tButton, 2f * s, new Color(0.23f, 0.14f, 0f), TextAnchor.MiddleCenter);
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>A secondary button: outline only, letter-spaced label.</summary>
        bool GhostButton(Rect r, string label, float s, bool on = false)
        {
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint) (on ? _buttonOn : _ghostStyle).Draw(r, hover, hover, false, false);
            Tracked(r, label.ToUpperInvariant(), _tButton, 4f * s, on ? Ink : new Color(0.82f, 0.89f, 0.94f), TextAnchor.MiddleCenter);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>A row of options joined into one bar; returns the chosen index (or the current one).</summary>
        int Segmented(Rect r, float s, int current, string[] options, bool[] enabled = null)
        {
            DrawRect(r, new Color(0f, 0.04f, 0.07f, 0.8f));
            Outline(r, new Color(0.16f, 0.62f, 0.85f, 0.6f));
            float w = r.width / options.Length;
            int chosen = current;
            for (int i = 0; i < options.Length; i++)
            {
                var cell = new Rect(r.x + i * w, r.y, w, r.height);
                bool ok = enabled == null || enabled[i];
                bool hover = ok && GUI.enabled && cell.Contains(Event.current.mousePosition);
                if (i == current) DrawRect(new Rect(cell.x + 1f, cell.y + 1f, cell.width - 2f, cell.height - 2f), Cyan);
                else if (hover) DrawRect(new Rect(cell.x + 1f, cell.y + 1f, cell.width - 2f, cell.height - 2f), new Color(0.16f, 0.78f, 1f, 0.15f));
                if (i > 0) DrawRect(new Rect(cell.x, cell.y + 4f * s, 1f, cell.height - 8f * s), new Color(0.16f, 0.62f, 0.85f, 0.35f));
                var c = i == current ? Ink : ok ? new Color(0.68f, 0.76f, 0.83f) : new Color(0.3f, 0.38f, 0.45f);
                Tracked(cell, options[i].ToUpperInvariant(), _tLabel, 2.6f * s, c, TextAnchor.MiddleCenter);
                if (ok && GUI.Button(cell, GUIContent.none, GUIStyle.none)) chosen = i;
            }
            return chosen;
        }

        /// <summary>A small outlined tag, right-aligned at <paramref name="right"/>: "FITTED", "900 CR".</summary>
        float Chip(float right, float centreY, string text, Color colour, float s)
        {
            float w = TrackedWidth(text, _tLabel, 2.2f * s) + 18f * s, h = 26f * s;
            var r = new Rect(right - w, centreY - h * 0.5f, w, h);
            Outline(r, colour);
            Tracked(r, text, _tLabel, 2.2f * s, colour, TextAnchor.MiddleCenter);
            return w;
        }

        static void Outline(Rect r, Color c)
        {
            DrawRect(new Rect(r.x, r.y, r.width, 1f), c);
            DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            DrawRect(new Rect(r.x, r.y, 1f, r.height), c);
            DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        /// <summary>A diamond (medals): a square turned 45°.</summary>
        static void Diamond(Vector2 centre, float size, Color c)
        {
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, centre);
            DrawRect(new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), c);
            GUI.matrix = m;
        }

        /// <summary>A "›" chevron drawn from two short strokes.</summary>
        static void Chevron(Rect r, Color c, float s)
        {
            var m = GUI.matrix;
            float len = 11f * s, th = Mathf.Max(2f, 2.5f * s);
            var p = new Vector2(r.center.x + 2f * s, r.center.y);
            GUIUtility.RotateAroundPivot(45f, p);
            DrawRect(new Rect(p.x - len, p.y - th * 0.5f, len, th), c);
            GUI.matrix = m;
            GUIUtility.RotateAroundPivot(-45f, p);
            DrawRect(new Rect(p.x - len, p.y - th * 0.5f, len, th), c);
            GUI.matrix = m;
        }

        /// <summary>Level pips: filled squares up to the level.</summary>
        static void Pips(float x, float centreY, int level, int max, float s, Color on)
        {
            float size = 9f * s, gap = 4f * s;
            for (int i = 0; i < max; i++)
            {
                var r = new Rect(x + i * (size + gap), centreY - size * 0.5f, size, size);
                if (i < level) DrawRect(r, on);
                else Outline(r, new Color(on.r, on.g, on.b, 0.5f));
            }
        }

        static float PipsWidth(int max, float s) => max * 13f * s - 4f * s;

        /// <summary>A thin stat bar.</summary>
        static void StatBar(Rect r, float fraction, Color c)
        {
            DrawRect(r, Palette.HudBarBg);
            DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), c);
        }

        // ------------------------------------------------------------------ the frame

        /// <summary>
        /// The console frame from the key art: corner brackets, thin lines along the top and bottom
        /// with a centre notch, and letter-spaced status text in the four corners. Text segments
        /// wrapped in [brackets] are drawn in cyan.
        /// </summary>
        void Frame(float s, string topLeft, string topRight, string bottomLeft, string bottomRight)
        {
            float m = 28f * s, len = 44f * s, th = Mathf.Max(2f, 3f * s);
            var r = new Rect(m, m, Screen.width - m * 2f, Screen.height - m * 2f);
            // Brackets.
            DrawRect(new Rect(r.x, r.y, len, th), Cyan); DrawRect(new Rect(r.x, r.y, th, len), Cyan);
            DrawRect(new Rect(r.xMax - len, r.y, len, th), Cyan); DrawRect(new Rect(r.xMax - th, r.y, th, len), Cyan);
            DrawRect(new Rect(r.x, r.yMax - th, len, th), Cyan); DrawRect(new Rect(r.x, r.yMax - len, th, len), Cyan);
            DrawRect(new Rect(r.xMax - len, r.yMax - th, len, th), Cyan); DrawRect(new Rect(r.xMax - th, r.yMax - len, th, len), Cyan);
            // Lines, fading at the ends, with a notch in the middle.
            for (int edge = 0; edge < 2; edge++)
            {
                float y = edge == 0 ? r.y : r.yMax - 1f;
                float x0 = r.x + len + 16f * s, x1 = r.xMax - len - 16f * s;
                DrawRect(new Rect(x0, y, x1 - x0, 1f), Line1);
                float nw = 220f * s, nh = 8f * s, nx = r.center.x - nw * 0.5f;
                float ny = edge == 0 ? y : y - nh;
                DrawRect(new Rect(nx, ny, 1f, nh), Line1); DrawRect(new Rect(nx + nw, ny, 1f, nh), Line1);
                DrawRect(new Rect(nx, edge == 0 ? ny + nh : ny, nw, 1f), Line1);
                Tracked(new Rect(nx, edge == 0 ? y - 9f * s : y - 9f * s, nw, 18f * s), "//", _tTag, 1f * s, Cyan, TextAnchor.MiddleCenter);
            }
            float tagH = 20f * s;
            FrameTag(new Rect(r.x + 52f * s, r.y + 12f * s, r.width * 0.48f, tagH), topLeft, s, TextAnchor.MiddleLeft);
            FrameTag(new Rect(r.center.x, r.y + 12f * s, r.width * 0.5f - 52f * s, tagH), topRight, s, TextAnchor.MiddleRight);
            FrameTag(new Rect(r.x + 52f * s, r.yMax - 12f * s - tagH, r.width * 0.48f, tagH), bottomLeft, s, TextAnchor.MiddleLeft);
            FrameTag(new Rect(r.center.x, r.yMax - 12f * s - tagH, r.width * 0.5f - 52f * s, tagH), bottomRight, s, TextAnchor.MiddleRight);
        }

        void FrameTag(Rect r, string text, float s, TextAnchor align)
        {
            if (string.IsNullOrEmpty(text)) return;
            // Split into plain and [cyan] segments, then lay them out in a row.
            var parts = new List<(string, bool)>();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('[', i);
                if (open < 0) { parts.Add((text.Substring(i), false)); break; }
                if (open > i) parts.Add((text.Substring(i, open - i), false));
                int close = text.IndexOf(']', open);
                if (close < 0) close = text.Length;
                parts.Add((text.Substring(open + 1, close - open - 1), true));
                i = close + 1;
            }
            float spacing = 3.4f * s, total = 0f;
            foreach (var (t, _) in parts) total += TrackedWidth(t.ToUpperInvariant(), _tTag, spacing) + spacing;
            float x = align == TextAnchor.MiddleRight ? r.xMax - total : r.x;
            foreach (var (t, cyan) in parts)
            {
                string u = t.ToUpperInvariant();
                float w = Tracked(new Rect(x, r.y, 4000f, r.height), u, _tTag, spacing, cyan ? Cyan : new Color(0.62f, 0.7f, 0.77f));
                x += w + spacing;
            }
        }

        /// <summary>The key art, darkened, behind the side panels of the menu screens (the field shows the live scene).</summary>
        void ArtBehindPanels(Rect field, float brightness)
        {
            if (_keyArt == null) return;
            var cover = CoverRect(_keyArt, new Rect(0f, 0f, Screen.width, Screen.height));
            GUI.color = new Color(brightness, brightness, brightness, 1f);
            void Part(Rect screen)
            {
                if (screen.width <= 0f) return;
                var uv = new Rect((screen.x - cover.x) / cover.width, 1f - (screen.yMax - cover.y) / cover.height,
                                  screen.width / cover.width, screen.height / cover.height);
                GUI.DrawTextureWithTexCoords(screen, _keyArt, uv);
            }
            Part(new Rect(0f, 0f, field.x, Screen.height));
            Part(new Rect(field.xMax, 0f, Screen.width - field.xMax, Screen.height));
            GUI.color = Color.white;
            // A cyan hairline down each edge of the field.
            DrawRect(new Rect(field.x - 1f, 0f, 1f, Screen.height), new Color(0.16f, 0.78f, 1f, 0.22f));
            DrawRect(new Rect(field.xMax, 0f, 1f, Screen.height), new Color(0.16f, 0.78f, 1f, 0.22f));
        }

        static Rect CoverRect(Texture t, Rect area)
        {
            float k = Mathf.Max(area.width / t.width, area.height / t.height);
            float w = t.width * k, h = t.height * k;
            return new Rect(area.center.x - w * 0.5f, area.center.y - h * 0.5f, w, h);
        }

        void OnDestroy()
        {
            foreach (var t in _made) if (t != null) Destroy(t);
        }
    }
}
