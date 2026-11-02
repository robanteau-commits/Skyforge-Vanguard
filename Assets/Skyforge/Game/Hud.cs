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
        float _fps, _fpsTimer;
        int _fpsFrames;

        public void Init(GameRunner runner) => _runner = runner;

        void Update()
        {
            if (_runner != null) MenuKeys();
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps = _fpsFrames / _fpsTimer;
                _fpsFrames = 0;
                _fpsTimer = 0f;
            }
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
            bool flyingLive = _runner.Flying && !_runner.Paused && sim.Status == StageStatus.Playing;
            if (Event.current.type == EventType.MouseDown && !flyingLive) _runner.Audio.Play(Audio.Sfx.Click);

            // Under an open settings/credits panel nothing else takes clicks.
            GUI.enabled = !PanelOpen;
            if (_runner.InTitle) Title(field, scale);
            else if (_runner.InMap) Map(field, scale, panels);
            else if (_runner.InHangar) Hangar(field, scale, panels);
            else Flight(field, sim, scale, panels);
            GUI.enabled = true;
            if (!_runner.Flying) TouchButtons(field, sim, scale, false);
            Panels(scale);
        }

        void Flight(Rect field, StageSim sim, float scale, bool panels)
        {
            if (panels)
            {
                ArtBehindPanels(field, 0.16f);
                float m = 52f * scale, top = 56f * scale;
                LeftPanel(new Rect(m, top, field.x - m - 44f * scale, Screen.height - top - 50f * scale), sim, scale);
                RightPanel(new Rect(field.xMax + 44f * scale, top, Screen.width - field.xMax - m - 44f * scale, Screen.height - top - 50f * scale), sim, scale);
                Frame(scale, null, null, null, null);
            }
            else
            {
                Overlay(field, sim, scale);
            }

            BossBar(field, sim, scale, panels);
            bool playing = sim.Status == StageStatus.Playing;
            TouchButtons(field, sim, scale, playing && !_runner.Paused && (TouchUi && (_runner.Controls.UsingTouch || Application.isMobilePlatform)));

            if (!playing && sim.TimeSinceEnd >= ResultsDelay && _runner.ResultsReady)
                Results(field, sim, scale);
            else if (_runner.Paused && !PanelOpen)
                PauseMenu(field, scale);
        }

        // ------------------------------------------------------------------ flight panels

        void LeftPanel(Rect r, StageSim sim, float s)
        {
            float y = r.y;
            Glow(new Rect(r.x, y, r.width, 36f * s), "SKYFORGE", new GUIStyle(_title) { fontSize = Mathf.RoundToInt(30 * s) }, Cyan, Cyan, 2f * s);
            y += 34f * s;
            GUI.Label(new Rect(r.x, y, r.width, 36f * s), "VANGUARD", new GUIStyle(_heading) { fontSize = Mathf.RoundToInt(30 * s) });
            y += 46f * s;
            DrawRect(new Rect(r.x, y, 70f * s, 4f * s), Cyan);
            y += 22f * s;
            FrameTag(new Rect(r.x, y, r.width, 22f * s), StageTag(sim), s, TextAnchor.MiddleLeft);
            y += 30f * s;
            StatBar(new Rect(r.x, y, r.width, 4f * s), sim.Progress, Cyan);
            y += 34f * s;

            Lbl(new Rect(r.x, y, r.width, 22f * s), "Score", s);
            y += 24f * s;
            GUI.Label(new Rect(r.x, y, r.width, 56f * s), sim.Score.ToString("N0"), new GUIStyle(_value) { fontSize = Mathf.RoundToInt(44 * s) });
            y += 64f * s;

            float half = (r.width - 14f * s) * 0.5f, bh = 78f * s;
            var combo = new Rect(r.x, y, half, bh);
            var cred = new Rect(r.x + half + 14f * s, y, half, bh);
            Outline(combo, new Color(0.16f, 0.78f, 1f, 0.3f));
            Outline(cred, new Color(1f, 0.71f, 0.24f, 0.4f));
            Lbl(new Rect(combo.x + 14f * s, combo.y + 8f * s, half, 20f * s), "Combo", s);
            GUI.Label(new Rect(combo.x + 14f * s, combo.y + 32f * s, half, 40f * s), "×" + sim.Combo.ToString("0.0"), _combo);
            Lbl(new Rect(cred.x + 14f * s, cred.y + 8f * s, half, 20f * s), "Credits", s);
            GUI.Label(new Rect(cred.x + 14f * s, cred.y + 32f * s, half, 40f * s), sim.Credits.ToString("N0"), _gold);
            y += bh + 22f * s;

            var p = sim.Player;
            Meter(ref y, r, s, "Shield", Mathf.CeilToInt(p.Shield) + " / " + Mathf.RoundToInt(p.MaxShield), p.Shield / p.MaxShield, Cyan, Palette.HudText);
            bool low = p.Hull < p.MaxHull * 0.3f;
            Meter(ref y, r, s, "Hull", Mathf.CeilToInt(p.Hull) + " / " + Mathf.RoundToInt(p.MaxHull), p.Hull / p.MaxHull, low ? Palette.HudDanger : Palette.HudHull, low ? Palette.HudDanger : Palette.HudText);
            CoreMeter(ref y, r, sim, s);
            y += 12f * s;

            Lbl(new Rect(r.x, y, r.width, 22f * s), "Kills", s);
            Tracked(new Rect(r.x, y, r.width, 22f * s), sim.Kills + (sim.Spawned > 0 ? "   " + sim.KillPercent + "%" : ""), _tButton, 2f * s, Palette.HudText, TextAnchor.MiddleRight);
            y += 34f * s;
            Lbl(new Rect(r.x, y, r.width, 22f * s), "Loadout", s);
            y += 26f * s;
            GUI.Label(new Rect(r.x, y, r.width, 24f * s), "Wings  <color=#dbe8f4>" + Describe(sim.Loadout.LeftWing) + (Same(sim.Loadout) ? "" : " / " + Describe(sim.Loadout.RightWing)) + "</color>", _small);
            y += 24f * s;
            GUI.Label(new Rect(r.x, y, r.width, 24f * s), "Core  <color=#dbe8f4>" + (sim.Loadout.Core.IsEmpty ? "—" : Describe(sim.Loadout.Core)) + "</color>", _small);

            Lbl(new Rect(r.x, r.yMax - 22f * s, r.width, 22f * s), "Build " + Application.version + (GameSettings.ShowFps ? "   •   " + Mathf.RoundToInt(_fps) + " fps" : ""), s);
        }

        /// <summary>A labelled bar: "SHIELD ........ 40 / 40" over a thick bar.</summary>
        void Meter(ref float y, Rect r, float s, string label, string value, float fraction, Color fill, Color valueColour)
        {
            Lbl(new Rect(r.x, y, r.width, 20f * s), label, s);
            Tracked(new Rect(r.x, y, r.width, 20f * s), value, _tLabel, 2.5f * s, valueColour, TextAnchor.MiddleRight);
            y += 26f * s;
            StatBar(new Rect(r.x, y, r.width, 12f * s), fraction, fill);
            y += 26f * s;
        }

        /// <summary>The core's charge, or the time left on its effect, and READY when it can fire.</summary>
        void CoreMeter(ref float y, Rect r, StageSim sim, float s)
        {
            if (sim.CoreWeapon == null) return;
            var p = sim.Player;
            string value;
            float fill;
            Color colour, valueColour = Cyan;
            if (p.CoreActive)
            {
                value = Mathf.CeilToInt(p.CoreTimer) + "s";
                fill = p.CoreTimer / Mathf.Max(0.01f, sim.CoreStats.Duration);
                colour = Color.white;
                valueColour = Color.white;
            }
            else if (p.CoreReady)
            {
                bool blink = Mathf.FloorToInt(Time.unscaledTime * 3f) % 2 == 0;
                value = _runner.Controls.UsingTouch ? "READY" : "READY · SPACE";
                fill = 1f;
                colour = blink ? new Color(0.6f, 0.92f, 1f) : Cyan;
            }
            else
            {
                value = Mathf.FloorToInt(p.CoreCharge) + "%";
                fill = p.CoreCharge / PlayerShip.CoreChargeMax;
                colour = new Color(0.04f, 0.5f, 0.7f);
                valueColour = Palette.HudText;
            }
            Meter(ref y, r, s, "Core · " + sim.CoreWeapon.Name, value, fill, colour, valueColour);
        }

        void RightPanel(Rect r, StageSim sim, float s)
        {
            float y = r.y;
            Lbl(new Rect(r.x, y, r.width, 22f * s), "Flight controls", s, Cyan);
            y += 36f * s;
            var rows = new[]
            {
                ("Mouse", "Ship follows the cursor"), ("WASD", "Fly"), ("Shift", "Precision (half speed)"),
                ("Space", "Fire the core when charged"), ("Gamepad", "Stick, LT precision, A core"),
                ("Esc", "Pause menu"), ("R", "Restart"), ("H / M", "Hangar / map"),
            };
            foreach (var (key, what) in rows)
            {
                Tracked(new Rect(r.x, y, 130f * s, 28f * s), key.ToUpperInvariant(), _tButton, 2.2f * s, Palette.HudText);
                GUI.Label(new Rect(r.x + 130f * s, y + 3f * s, r.width - 130f * s, 28f * s), what, _small);
                y += 32f * s;
            }
            y += 26f * s;
            Lbl(new Rect(r.x, y, r.width, 22f * s), "Field guide", s, Cyan);
            y += 32f * s;
            GUI.Label(new Rect(r.x, y, r.width, 200f * s),
                "Wings fire on their own. Dodge the orange bullets: a red glow means an enemy is about to fire. " +
                "Your shield takes hits first and recharges; hull damage resets the combo. Fly near gold shards to collect them.", _small);

            if (sim.Veteran)
                Tracked(new Rect(r.x, r.yMax - 22f * s, r.width, 22f * s), "VETERAN", _tLabel, 3.4f * s, Palette.HudDanger, TextAnchor.MiddleRight);
        }

        string StageTag(StageSim sim) =>
            sim.Stage == null ? "[Practice] flight" : sim.Stage.Id == "sampler" ? "[Test]  //  " + sim.Stage.Name
            : "[" + (sim.Stage.Id == "1-B" ? "Boss" : "Stage 0" + sim.Stage.Id) + "]  //  " + sim.Stage.Name;

        void Overlay(Rect field, StageSim sim, float s)
        {
            float pad = 10f * s;
            float w = field.width - pad * 2f;
            var top = new Rect(field.x + pad, field.y + pad, w, 34f * s);
            GUI.Label(top, sim.Score.ToString("N0"), _value);
            var comboStyle = new GUIStyle(_combo) { alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(top.x, top.y, top.width - 50f * s, top.height), "×" + sim.Combo.ToString("0.0"), comboStyle);
            Tracked(new Rect(field.x + pad, top.yMax + 6f * s, w, 22f * s), sim.Credits.ToString("N0") + " CR", _tLabel, 2.5f * s, Palette.HudGold);

            // Bars along the bottom edge.
            var p = sim.Player;
            float barH = 8f * s;
            float y = field.yMax - pad - barH * 2f - 6f * s;
            StatBar(new Rect(field.x + pad, y, w, barH), p.Shield / p.MaxShield, Cyan);
            StatBar(new Rect(field.x + pad, y + barH + 4f * s, w, barH), p.Hull / p.MaxHull, p.Hull < p.MaxHull * 0.3f ? Palette.HudDanger : Palette.HudHull);
        }

        /// <summary>The boss's name and health along the top of the field.</summary>
        void BossBar(Rect field, StageSim sim, float s, bool panels)
        {
            var boss = sim.Boss;
            if (boss == null || sim.Status != StageStatus.Playing || boss.Pos.Y > Field.HalfHeight + 2f) return;
            float pad = 18f * s;
            float y = field.y + (panels ? pad : 72f * s);
            var r = new Rect(field.x + pad * 2f, y, field.width - pad * 4f, 26f * s);
            string phase = boss.Kind == EnemyKind.Tidebreaker && boss.Mode >= 1 ? "PHASE " + boss.Mode : "";
            Tracked(r, boss.Def.Name.ToUpperInvariant(), _tButton, 4f * s, Palette.HudDanger);
            if (phase.Length > 0) Tracked(r, phase, _tLabel, 3f * s, new Color(1f, 0.6f, 0.65f), TextAnchor.MiddleRight);
            var bar = new Rect(r.x, r.yMax + 4f * s, r.width, 10f * s);
            StatBar(bar, sim.BossHealth(boss), Palette.HudDanger);
            Outline(new Rect(bar.x - 1f, bar.y - 1f, bar.width + 2f, bar.height + 2f), new Color(1f, 0.31f, 0.38f, 0.5f));
        }

        // ------------------------------------------------------------------ results

        void Results(Rect field, StageSim sim, float s)
        {
            DrawRect(field, new Color(0f, 0.01f, 0.03f, 0.72f));
            bool cleared = sim.Status == StageStatus.Cleared;
            var res = _runner.LastResult;
            bool medals = res != null && cleared && sim.Stage != null;
            float w = Mathf.Min(field.width - 24f * s, 560f * s);
            float h = (medals ? 760f : 600f) * s;
            var box = new Rect(field.center.x - w * 0.5f, Mathf.Max(field.y + 12f * s, field.center.y - h * 0.5f), w, Mathf.Min(h, field.height - 24f * s));
            var inner = PanelBox(box, s, "Mission report", sim.Stage == null ? "Practice" : StageCode(sim) + "  //  " + sim.Stage.Name);
            float y = inner.y;
            var headColour = cleared ? Cyan : Palette.HudDanger;
            Glow(new Rect(inner.x, y, inner.width, 60f * s), cleared ? "STAGE CLEAR" : "SHIP DOWN",
                new GUIStyle(_centre) { fontSize = Mathf.RoundToInt(46 * s) }, headColour, headColour, 3f * s);
            y += 68f * s;
            DrawRect(new Rect(inner.center.x - 45f * s, y, 90f * s, 4f * s), headColour);
            y += 26f * s;

            ResultRow(ref y, inner, s, "Score", sim.Score.ToString("N0"), Palette.HudText);
            ResultRow(ref y, inner, s, "Enemies destroyed", sim.Spawned > 0 ? sim.KillPercent + "%" : sim.Kills.ToString(), Palette.HudText);
            ResultRow(ref y, inner, s, "Hits taken", sim.HitsTaken == 0 ? "NONE" : sim.HitsTaken.ToString(), Palette.HudText);
            ResultRow(ref y, inner, s, cleared ? "Credits banked" : "Credits kept (half)", sim.CreditsBanked.ToString("N0"), Palette.HudGold);
            if (res != null) ResultRow(ref y, inner, s, "Total credits", res.TotalCredits.ToString("N0"), Palette.HudGold);
            y += 10f * s;

            if (medals)
            {
                float gap = 12f * s, mw = (inner.width - gap * 2f) / 3f, mh = 96f * s;
                MedalTile(new Rect(inner.x, y, mw, mh), s, "Ace", res.Ace, res.NewAce, sim.Stage.MedalBonus[0], "Score " + Progress.AceScore(sim.Stage, sim.Veteran).ToString("N0"));
                MedalTile(new Rect(inner.x + mw + gap, y, mw, mh), s, "Untouched", res.Untouched, res.NewUntouched, sim.Stage.MedalBonus[1], "No hits taken");
                MedalTile(new Rect(inner.x + (mw + gap) * 2f, y, mw, mh), s, "Purge", res.Purge, res.NewPurge, sim.Stage.MedalBonus[2], Stages.PurgePercent + "% needed");
                y += mh + 16f * s;
            }
            string news = res == null ? (_runner.Sandbox || sim.Stage == null || sim.Stage == Stages.ArmadaSampler ? "Test flight: nothing is saved" : "")
                : (res.NewBest ? "New best score   " : "") + (res.UnlockedStageId != null ? "Stage " + res.UnlockedStageId + " unlocked   " : "") + (res.UnlockedVeteran ? "Veteran unlocked" : "");
            if (news.Length > 0)
                Tracked(new Rect(inner.x, y, inner.width, 24f * s), news.Trim().ToUpperInvariant(), _tLabel, 2.6f * s, res == null ? Palette.HudDim : Palette.HudGold, TextAnchor.MiddleCenter);

            float bh = 62f * s, gh = 50f * s;
            float by = inner.yMax - bh - 12f * s - gh;
            if (BigButton(new Rect(inner.x, by, inner.width, bh), "Continue  ›", s)) _runner.OpenMap();
            float bw = (inner.width - 12f * s) * 0.5f;
            if (GhostButton(new Rect(inner.x, by + bh + 12f * s, bw, gh), "Fly again", s)) _runner.Launch();
            if (GhostButton(new Rect(inner.x + bw + 12f * s, by + bh + 12f * s, bw, gh), "Hangar", s)) _runner.OpenHangar();
            if (!_runner.Controls.UsingTouch)
                Tracked(new Rect(box.x, box.yMax + 8f * s, box.width, 22f * s), "ENTER  CONTINUE   •   R  FLY AGAIN   •   H  HANGAR", _tTag, 2.6f * s, Palette.HudDim, TextAnchor.MiddleCenter);
        }

        void ResultRow(ref float y, Rect r, float s, string label, string value, Color valueColour)
        {
            var row = new Rect(r.x, y, r.width, 36f * s);
            Lbl(row, label, s);
            var st = new GUIStyle(_resultValue) { normal = { textColor = valueColour } };
            GUI.Label(row, value, st);
            DrawRect(new Rect(r.x, row.yMax, r.width, 1f), new Color(0.16f, 0.62f, 0.85f, 0.18f));
            y += 42f * s;
        }

        void MedalTile(Rect r, float s, string name, bool earned, bool isNew, int bonus, string rule)
        {
            var gold = Palette.HudGold;
            DrawRect(r, earned ? new Color(1f, 0.71f, 0.24f, 0.08f) : new Color(0f, 0.04f, 0.07f, 0.6f));
            Outline(r, earned ? gold : new Color(0.16f, 0.62f, 0.85f, 0.25f));
            Diamond(new Vector2(r.center.x, r.y + 24f * s), 15f * s, earned ? gold : new Color(0.2f, 0.29f, 0.35f));
            Tracked(new Rect(r.x, r.y + 42f * s, r.width, 24f * s), name.ToUpperInvariant(), _tButton, 2.6f * s, earned ? gold : Palette.HudDim, TextAnchor.MiddleCenter);
            string sub = !earned ? rule.ToUpperInvariant() : isNew ? "NEW  +" + bonus : "EARNED";
            Tracked(new Rect(r.x, r.y + 68f * s, r.width, 20f * s), sub, _tLabel, 1.6f * s, earned ? gold : Palette.HudDim, TextAnchor.MiddleCenter);
        }

        // ------------------------------------------------------------------ helpers

        static void DrawRect(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
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
