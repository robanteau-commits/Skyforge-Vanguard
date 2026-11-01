using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Skyforge.Simulation;

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

        Loadout _loadout = Loadout.Starter();
        /// <summary>The stage to fly; null = practice drones.</summary>
        StageDef _stage = Stages.FirstLight;
        Asteroids _asteroids;
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
            SetupLighting();

            _world = new GameObject("World").transform;
            _backdrop = new GameObject("Backdrop").AddComponent<Backdrop>();
            _backdrop.transform.SetParent(_world, false);
            _enemies = new EnemyViews(_world);
            _asteroids = new Asteroids(_world);
            _shots = new ShotRenderer();
            _fx = new Fx();
            gameObject.AddComponent<Hud>().Init(this);

            StartStage();
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

        void StartStage()
        {
            Sim = new StageSim(new StageSetup { Seed = (uint)System.Environment.TickCount | 1u, Loadout = _loadout, Stage = _stage });
            _prevShipPos = Sim.Player.Pos;
            _accumulator = 0f;
            _pendingDrag = Vec2.Zero;
            _pendingCore = false;
            _enemies.Clear();
            _asteroids.Clear();
            _fx.Clear();
            if (_ship == null) _ship = new ShipView(_world, _loadout);
            else _ship.RefitPods(_loadout);
            Paused = false;
        }

        void SetWeaponLevel(int level)
        {
            _loadout = new Loadout
            {
                LeftWing = new Fitted(Weapons.PulseRepeater, level),
                RightWing = new Fitted(Weapons.PulseRepeater, level),
                Core = _loadout.Core,
            };
            StartStage();
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            if (PlayerControls.PausePressed()) Paused = !Paused;
            else if (Paused && ResumeClicked()) Paused = false;
            if (PlayerControls.RestartPressed()) StartStage();
            int level = PlayerControls.LevelKeyPressed();
            if (level > 0) SetWeaponLevel(level);
            int stageKey = PlayerControls.StageKeyPressed();
            if (stageKey > 0)
            {
                _stage = stageKey == 6 ? Stages.FirstLight : stageKey == 7 ? Stages.ArmadaSampler : null;
                StartStage();
            }
            // On the results screen, any confirm flies again.
            if (Sim.Status != StageStatus.Playing && Sim.TimeSinceEnd >= Hud.ResultsDelay + 0.3f && PlayerControls.ConfirmPressed())
                StartStage();

            float dt = Mathf.Min(Time.deltaTime, MaxFrameTime);
            if (!Paused)
            {
                var input = Controls.Read(View);
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
                    _asteroids.Consume(Sim.Events, Sim.Time);
                    foreach (var ev in Sim.Events)
                    {
                        if (ev.Type == SimEventType.HullHit) View.Shake(0.35f);
                        else if (ev.Type == SimEventType.PlayerDestroyed) View.Shake(0.8f);
                        else if (ev.Type == SimEventType.Destroyed && ev.EnemyKind == EnemyKind.Warden) View.Shake(0.5f);
                    }
                    _accumulator -= Field.Dt;
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
            _ship.SetVisible(Sim.Player.Alive && !flicker);
            _backdrop.SetTime(renderTime);
            _asteroids.Render(renderTime);
            _enemies.Render(alpha, Paused ? 0f : dt);
            _shots.Draw(Sim, (1f - alpha) * Field.Dt, renderTime);
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
            // Phones and browser tabs: losing focus pauses the stage.
            if (!focused && Sim != null) Paused = true;
        }

        void OnDestroy()
        {
            _shots?.Dispose();
            _fx?.Dispose();
            _enemies?.Dispose();
            _asteroids?.Dispose();
        }
    }
}
