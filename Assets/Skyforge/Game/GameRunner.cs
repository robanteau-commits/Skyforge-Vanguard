using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Skyforge.Simulation;
// The Input System has its own PlayerInput component; ours is the simulation's input struct.
using PlayerInput = Skyforge.Simulation.PlayerInput;

namespace Skyforge.Game
{
    /// <summary>
    /// Boots the game in whatever scene is open and runs the stage: reads input, steps the
    /// simulation at a fixed 60 Hz, and draws it with interpolation. The Game layer only reads
    /// simulation state; it changes the stage solely through <see cref="StageSim.Step"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameRunner : MonoBehaviour
    {
        /// <summary>Longest frame time simulated at once (avoids a catch-up spiral after a hitch).</summary>
        const float MaxFrameTime = 0.1f;

        public StageSim Sim { get; private set; }
        public PlayfieldCamera View { get; private set; }
        public PlayerControls Controls { get; } = new PlayerControls();
        public bool Paused { get; private set; }

        public enum Screen { Map, Hangar, Flying }
        /// <summary>Which screen is up: the world map, the hangar (live practice preview behind it) or a stage.</summary>
        public Screen Current { get; private set; } = Screen.Map;
        public bool InHangar => Current == Screen.Hangar;
        public bool InMap => Current == Screen.Map;

        /// <summary>The save: credits, owned weapons and levels, airframe upgrades, fit, stage records.</summary>
        public Progress Progress { get; private set; }
        /// <summary>
        /// Sandbox: everything unlocked at any level, nothing saved or earned (the M2 test mode, kept
        /// so weapons can be tried before buying).
        /// </summary>
        public bool Sandbox { get; private set; }
        Loadout _sandboxLoadout = Loadout.Starter();

        /// <summary>The ship's current fit: the save's in a normal game, the free one in the sandbox.</summary>
        public Loadout Loadout => Sandbox ? _sandboxLoadout : Progress.BuildLoadout();
        /// <summary>The stage to fly; null = practice drones.</summary>
        public StageDef SelectedStage { get; set; } = Stages.FirstLight;
        /// <summary>What the last finished stage earned (null for sandbox, practice and test stages).</summary>
        public StageResult LastResult { get; private set; }
        bool _recorded;
        GameObject _globe;

        /// <summary>
        /// Play field width: wide (3:4) on PC and web, portrait (9:16) on phones and tablets
        /// (Rob's call, v0.3.2: the portrait strip felt narrow on a monitor).
        /// </summary>
        public static float FieldWidth => Application.isMobilePlatform ? Field.Width : Field.WideWidth;

        /// <summary>The hangar preview's core charges this many times faster, so you see it fire.</summary>
        const float PreviewCoreCharge = 8f;

        Asteroids _asteroids;
        WeaponViews _weapons;
        Transform _world;
        Backdrop _backdrop;
        ShipView _ship;
        EnemyViews _enemies;
        ShotRenderer _shots;
        Fx _fx;
        float _accumulator;
        Vec2 _prevShipPos;
        Vec2 _pendingDrag;
        bool _pendingCore;

        // ------------------------------------------------------------------ boot

        // RuntimeInitializeOnLoadMethod fires once per play session, so also hook later scene loads.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            EnsureRunner();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureRunner();

        static void EnsureRunner()
        {
            if (FindAnyObjectByType<GameRunner>() == null)
                new GameObject("Skyforge").AddComponent<GameRunner>();
        }

        void Awake()
        {
#if UNITY_ANDROID || UNITY_IOS
            Application.targetFrameRate = 60;
#endif
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            }
            View = PlayfieldCamera.Attach(cam);
            View.FieldWidth = FieldWidth;
            SetupLighting();

            _world = new GameObject("World").transform;
            _backdrop = new GameObject("Backdrop").AddComponent<Backdrop>();
            _backdrop.transform.SetParent(_world, false);
            _enemies = new EnemyViews(_world);
            _asteroids = new Asteroids(_world);
            _weapons = new WeaponViews(_world);
            _shots = new ShotRenderer();
            _fx = new Fx();
            _globe = GlobeModel.Create(_world);
            Progress = SaveStore.Load();
            gameObject.AddComponent<Hud>().Init(this);

            OpenMap();
        }

        /// <summary>
        /// Space lighting over a black background: a cool white key light from the upper left that
        /// picks out the ships' facets, a dark blue ambient, no fog, and a dim cyan rim from ahead.
        /// </summary>
        static void SetupLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.18f, 0.2f, 0.28f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.085f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.03f, 0.04f);
            RenderSettings.fog = false;

            Light key = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { key = l; break; }
            if (key == null) key = new GameObject("Key Light").AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.9f, 0.94f, 1f);
            key.intensity = 1.2f;
            key.shadows = LightShadows.None;
            key.transform.rotation = Quaternion.Euler(55f, 140f, 0f);
            RenderSettings.sun = key;

            var rim = new GameObject("Rim Light").AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.35f, 0.75f, 1f);
            rim.intensity = 0.45f;
            rim.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.Euler(25f, 170f, 0f);
        }

        // ------------------------------------------------------------------ stage

        int[] AirframeLevels => Sandbox ? null : Progress.AirframeLevels;

        /// <summary>Show the world map (a slowly turning Earth behind it; no ship).</summary>
        public void OpenMap()
        {
            Current = Screen.Map;
            Begin(new StageSetup { Seed = 7, Loadout = new Loadout(), Stage = null, PracticeDrones = false, FieldWidth = FieldWidth });
        }

        /// <summary>Show the hangar, with a practice flight of the current loadout behind it.</summary>
        public void OpenHangar()
        {
            Current = Screen.Hangar;
            Begin(new StageSetup { Seed = 7, Loadout = Loadout, Stage = null, CoreChargeMultiplier = PreviewCoreCharge, FieldWidth = FieldWidth, Airframe = AirframeLevels });
        }

        /// <summary>Fly the selected stage with the current loadout.</summary>
        public void Launch()
        {
            Current = Screen.Flying;
            Begin(new StageSetup { Seed = (uint)System.Environment.TickCount | 1u, Loadout = Loadout, Stage = SelectedStage, FieldWidth = FieldWidth, Airframe = AirframeLevels });
        }

        public void SetSandbox(bool on)
        {
            Sandbox = on;
            if (InHangar) OpenHangar();
        }

        /// <summary>Fit a weapon to a slot (hangar). Restarts the preview so the change shows at once.
        /// In a normal game only owned weapons fit, at their owned level.</summary>
        public void Fit(WeaponSlot slot, Side side, WeaponDef weapon, int level)
        {
            if (Sandbox)
            {
                var l = _sandboxLoadout;
                var fitted = new Fitted(weapon, level);
                _sandboxLoadout = new Loadout
                {
                    LeftWing = slot == WeaponSlot.Wing && side == Side.Left ? fitted : l.LeftWing,
                    RightWing = slot == WeaponSlot.Wing && side == Side.Right ? fitted : l.RightWing,
                    Core = slot == WeaponSlot.Core ? fitted : l.Core,
                };
            }
            else if (!Progress.Fit(slot, side, weapon)) return;
            else SaveStore.Save(Progress);
            if (InHangar) OpenHangar();
        }

        /// <summary>Buy a weapon and fit it to the slot being looked at.</summary>
        public bool Buy(WeaponDef weapon, Side side)
        {
            if (Sandbox || !Progress.Buy(weapon)) return false;
            Progress.Fit(weapon.Slot, side, weapon);
            SaveStore.Save(Progress);
            if (InHangar) OpenHangar();
            return true;
        }

        public bool Upgrade(WeaponDef weapon)
        {
            if (Sandbox || !Progress.Upgrade(weapon)) return false;
            SaveStore.Save(Progress);
            if (InHangar) OpenHangar();
            return true;
        }

        public bool UpgradeAirframe(AirframeStat stat)
        {
            if (Sandbox || !Progress.UpgradeAirframe(stat)) return false;
            SaveStore.Save(Progress);
            if (InHangar) OpenHangar();
            return true;
        }

        /// <summary>Wipe the save and start over (asked twice on the map).</summary>
        public void ResetProgress()
        {
            Progress = Progress.NewGame();
            SaveStore.Save(Progress);
            SelectedStage = Stages.FirstLight;
            OpenMap();
        }

        /// <summary>Bank a finished stage into the save, once, when it ends (not in the sandbox,
        /// practice or the test stage).</summary>
        void RecordIfFinished()
        {
            if (_recorded || Current != Screen.Flying || Sim.Status == StageStatus.Playing) return;
            _recorded = true;
            if (Sandbox || Sim.Stage == null || Sim.Stage == Stages.ArmadaSampler) { LastResult = null; return; }
            LastResult = Progress.RecordStage(Sim.Stage, Sim);
            SaveStore.Save(Progress);
        }

        void Begin(StageSetup setup)
        {
            Sim = new StageSim(setup);
            _recorded = false;
            LastResult = null;
            _prevShipPos = Sim.Player.Pos;
            _accumulator = 0f;
            _pendingDrag = Vec2.Zero;
            _pendingCore = false;
            _enemies.Clear();
            _asteroids.Clear();
            _weapons.Clear();
            _shots.Clear();
            _fx.Clear();
            if (_ship == null) _ship = new ShipView(_world, Loadout);
            else _ship.RefitPods(Loadout);
            Paused = false;
        }

        /// <summary>The hangar preview flies itself: it hovers mid-screen, weaving a little, and fires its core whenever it's ready.</summary>
        PlayerInput PreviewInput()
        {
            var input = PlayerInput.Towards(new Vec2(3f * Mathf.Sin(Sim.Time * 0.6f), -4f));
            input.Core = true;
            return input;
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            int stageKey = PlayerControls.StageKeyPressed();
            if (stageKey > 0)
            {
                SelectedStage = stageKey == 6 ? Stages.FirstLight : stageKey == 7 ? Stages.ArmadaSampler : null;
                Launch();
            }
            else if (InMap)
            {
                if (PlayerControls.LaunchPressed()) OpenHangar();
            }
            else if (InHangar)
            {
                if (PlayerControls.LaunchPressed()) Launch();
                else if (PlayerControls.PausePressed()) OpenMap();
            }
            else
            {
                if (PlayerControls.PausePressed()) Paused = !Paused;
                else if (Paused && ResumeClicked()) Paused = false;
                if (PlayerControls.RestartPressed()) Launch();
                if (PlayerControls.HangarPressed()) OpenHangar();
                if (PlayerControls.MapPressed()) OpenMap();
                // On the results screen, confirm continues to the map.
                if (Sim.Status != StageStatus.Playing && Sim.TimeSinceEnd >= Hud.ResultsDelay + 0.3f && PlayerControls.ConfirmPressed())
                    OpenMap();
            }

            float dt = Mathf.Min(Time.deltaTime, MaxFrameTime);
            if (!Paused)
            {
                var input = InHangar ? PreviewInput() : InMap ? PlayerInput.Idle : Controls.Read(View);
                // Touch movement and core presses are per frame; hold them until a step uses them.
                _pendingDrag += input.Drag;
                _pendingCore |= input.Core;

                _accumulator += dt;
                while (_accumulator >= Field.Dt)
                {
                    var step = input;
                    step.Drag = _pendingDrag;
                    step.Core = _pendingCore;
                    _pendingDrag = Vec2.Zero;
                    _pendingCore = false;

                    _prevShipPos = Sim.Player.Pos;
                    Sim.Step(step);
                    _enemies.AfterStep(Sim);
                    _fx.Consume(Sim.Events);
                    _weapons.Consume(Sim.Events);
                    _asteroids.Consume(Sim.Events, Sim.Time);
                    foreach (var ev in Sim.Events)
                    {
                        if (ev.Type == SimEventType.HullHit) View.Shake(0.35f);
                        else if (ev.Type == SimEventType.PlayerDestroyed) View.Shake(0.8f);
                        else if (ev.Type == SimEventType.Destroyed && ev.EnemyKind == EnemyKind.Warden) View.Shake(0.5f);
                        else if (ev.Type == SimEventType.CoreActivated && ev.EnemyId == (int)WeaponKind.PulseNova && !InHangar) View.Shake(0.45f);
                    }
                    _accumulator -= Field.Dt;
                    RecordIfFinished();
                }
            }

            float alpha = _accumulator / Field.Dt;
            // Everything is drawn as of (alpha) between the previous and the latest step.
            float renderTime = Sim.Time - (1f - alpha) * Field.Dt;
            var shipPos = new Vec2(
                Mathf.Lerp(_prevShipPos.X, Sim.Player.Pos.X, alpha),
                Mathf.Lerp(_prevShipPos.Y, Sim.Player.Pos.Y, alpha));
            _ship.Update(shipPos, Sim.Player.Velocity, Sim.Player.MaxSpeed, Paused ? 0f : dt, renderTime);
            // Hidden once destroyed; flickers while invulnerable after a hit.
            bool flicker = Sim.Player.Invulnerable > 0f && Mathf.FloorToInt(renderTime * 20f) % 2 == 0;
            _ship.SetVisible(Sim.Player.Alive && !flicker && !InMap);
            GlobeModel.Show(_globe, InMap, renderTime);
            _backdrop.SetTime(renderTime);
            _asteroids.Render(renderTime);
            _enemies.Render(alpha, Paused ? 0f : dt);
            _weapons.Render(Sim, shipPos, renderTime, Paused ? 0f : dt);
            _shots.Draw(Sim, (1f - alpha) * Field.Dt, renderTime, Paused ? 0f : dt);
            _fx.Tick(Paused ? 0f : dt);
        }

        static bool ResumeClicked()
        {
            var touch = Touchscreen.current;
            var mouse = Mouse.current;
            return (touch != null && touch.primaryTouch.press.wasPressedThisFrame) ||
                   (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        void OnApplicationFocus(bool focused)
        {
            // Phones and browser tabs: losing focus pauses the stage (the hangar keeps running).
            if (!focused && Sim != null && Current == Screen.Flying) Paused = true;
        }

        void OnDestroy()
        {
            _shots?.Dispose();
            _fx?.Dispose();
            _enemies?.Dispose();
            _asteroids?.Dispose();
            _weapons?.Dispose();
        }
    }
}
