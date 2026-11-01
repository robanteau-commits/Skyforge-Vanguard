using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game.Audio
{
    /// <summary>
    /// Plays the game's sound: effects for this step's simulation events (synthesised once at start,
    /// see <see cref="Sounds"/>), the Sunline Beam's hum while a beam fires, and the music loops
    /// (<see cref="Music"/>), cross-fading between tracks. Each effect has a minimum gap so a
    /// barrage doesn't turn into a wall of noise. Purely presentation.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        const int Voices = 24;
        const float FadeTime = 1.2f;

        AudioClip[] _clips;
        readonly float[] _lastPlayed = new float[Sounds.Count];
        AudioSource[] _voices;
        int _nextVoice;
        AudioSource _beam;
        readonly AudioSource[] _music = new AudioSource[2];
        int _musicOn;
        float _fade = 1f;
        Track _track, _pending;
        Task<float[]> _rendering;
        readonly Dictionary<string, AudioClip> _musicCache = new Dictionary<string, AudioClip>();

        /// <summary>Effects volume for the current screen (the hangar preview plays quietly).</summary>
        public float SfxScale = 1f;
        /// <summary>Music volume factor (lowered on the results screen and while paused).</summary>
        public float MusicDuck = 1f;

        bool _coreWasReady, _wasPlaying = true;
        int _firingLasers;

        void Awake()
        {
            GameSettings.Load();
            _clips = new AudioClip[Sounds.Count];
            for (int i = 0; i < Sounds.Count; i++)
            {
                var data = Synth.Render(Sounds.Design((Sfx)i), Synth.SfxRate, 0.9f, (uint)(i + 1));
                var clip = AudioClip.Create(((Sfx)i).ToString(), data.Length, 1, Synth.SfxRate, false);
                clip.SetData(data, 0);
                _clips[i] = clip;
            }
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++) _voices[i] = NewSource(false);
            _beam = NewSource(true);
            _beam.clip = _clips[(int)Sfx.BeamHum];
            for (int i = 0; i < 2; i++) _music[i] = NewSource(true);
            for (int i = 0; i < _lastPlayed.Length; i++) _lastPlayed[i] = -10f;
        }

        AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        // ------------------------------------------------------------------ effects

        public void Play(Sfx s, float volume = 1f, float pitch = 1f)
        {
            if (_clips == null) return;
            float now = Time.unscaledTime;
            if (now - _lastPlayed[(int)s] < Sounds.MinGap(s)) return;
            _lastPlayed[(int)s] = now;
            float v = Sounds.Gain(s) * volume * GameSettings.SfxVolume * SfxScale;
            if (v <= 0.001f) return;
            var src = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % Voices;
            src.pitch = pitch * Random.Range(0.96f, 1.04f);
            src.PlayOneShot(_clips[(int)s], v);
        }

        static Sfx ShotSound(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.ScatterCannon: return Sfx.ShotScatter;
                case WeaponKind.LanceDriver: return Sfx.ShotLance;
                case WeaponKind.SeekerPods: return Sfx.ShotSeeker;
                case WeaponKind.HornetRack: return Sfx.ShotHornet;
                case WeaponKind.ArcCoil: return Sfx.ShotArc;
                case WeaponKind.RicochetDisc: return Sfx.ShotDisc;
                default: return Sfx.ShotPulse;
            }
        }

        /// <summary>Sounds for this step's events. Call right after each simulation step.</summary>
        public void AfterStep(StageSim sim)
        {
            foreach (var e in sim.Events)
            {
                switch (e.Type)
                {
                    case SimEventType.Fired:
                    {
                        var w = (e.Wing == Side.Left ? sim.Loadout.LeftWing : sim.Loadout.RightWing).Weapon;
                        if (w != null && w.Kind != WeaponKind.SunlineBeam) Play(ShotSound(w.Kind));
                        break;
                    }
                    case SimEventType.Hit: Play(Sfx.Hit); break;
                    case SimEventType.Deflected: case SimEventType.AegisBlock: Play(Sfx.Deflect); break;
                    case SimEventType.Destroyed:
                        if (e.EnemyKind == EnemyKind.Tidebreaker) Play(Sfx.ExplodeBoss);
                        else if (EnemyDefs.IsBig(e.EnemyKind)) Play(Sfx.ExplodeBig);
                        else Play(Sfx.ExplodeSmall, 1f, e.EnemyKind == EnemyKind.Gnat ? 1.15f : 1f);
                        break;
                    case SimEventType.EnemyFired: Play(Sfx.EnemyShot); break;
                    case SimEventType.MineBurst: Play(Sfx.MineBurst); break;
                    case SimEventType.Blast: case SimEventType.BeamPulse: Play(Sfx.Blast); break;
                    case SimEventType.ShieldHit: Play(Sfx.ShieldHit); break;
                    case SimEventType.HullHit: Play(Sfx.HullHit); break;
                    case SimEventType.PlayerDestroyed: Play(Sfx.PlayerDown); break;
                    case SimEventType.Collected:
                        // Credits chime a little higher with a bigger combo.
                        if (e.PickupKind == PickupKind.Credit) Play(Sfx.Credit, 1f, 1f + (sim.ComboTenths - 10) * 0.006f);
                        else Play(Sfx.Repair);
                        break;
                    case SimEventType.CoreActivated:
                        switch ((WeaponKind)e.EnemyId)
                        {
                            case WeaponKind.SolarLance: Play(Sfx.CoreLance); break;
                            case WeaponKind.AegisField: Play(Sfx.CoreAegis); break;
                            case WeaponKind.Wingmates: Play(Sfx.CoreWingmates); break;
                            default: Play(Sfx.CoreNova); break;
                        }
                        break;
                    case SimEventType.BossPhase: if (e.Value >= 1f) Play(Sfx.Warning); break;
                    case SimEventType.StageCleared: Play(Sfx.Clear); break;
                }
            }

            bool ready = sim.Player.CoreReady && !sim.Player.CoreActive;
            if (ready && !_coreWasReady && sim.Stage != null) Play(Sfx.CoreReady);
            _coreWasReady = ready;

            int firing = 0;
            foreach (var l in sim.Lasers) if (l.Firing) firing++;
            if (firing > _firingLasers) Play(Sfx.Laser);
            _firingLasers = firing;

            bool playing = sim.Status == StageStatus.Playing;
            if (_wasPlaying && sim.Status == StageStatus.Failed && sim.Stage != null) Play(Sfx.Fail);
            _wasPlaying = playing;
        }

        /// <summary>A new stage started: forget the last one's state.</summary>
        public void Reset()
        {
            _coreWasReady = false;
            _wasPlaying = true;
            _firingLasers = 0;
        }

        // ------------------------------------------------------------------ music

        /// <summary>Cross-fade to a track (rendered on first use, on a worker thread where there is one).</summary>
        public void PlayMusic(Track t)
        {
            if (t == null || t == _track || t == _pending) return;
            if (_musicCache.ContainsKey(t.Id)) { StartTrack(t); return; }
            _pending = t;
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // No threads on the web: render now (a short pause the first time a track plays).
                _rendering = Task.FromResult(Music.Render(t));
            }
            else
            {
                var track = t;
                _rendering = Task.Run(() => Music.Render(track));
            }
        }

        void StartTrack(Track t)
        {
            _pending = null;
            _track = t;
            _musicOn = 1 - _musicOn;
            var src = _music[_musicOn];
            src.clip = _musicCache[t.Id];
            src.volume = 0f;
            src.Play();
            _fade = 0f;
        }

        void Update()
        {
            if (_pending != null && _rendering != null && _rendering.IsCompleted)
            {
                if (!_rendering.IsFaulted)
                {
                    var data = _rendering.Result;
                    var clip = AudioClip.Create("Music_" + _pending.Id, data.Length, 1, Music.Rate, false);
                    clip.SetData(data, 0);
                    _musicCache[_pending.Id] = clip;
                    StartTrack(_pending);
                }
                else _pending = null;
                _rendering = null;
            }

            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / FadeTime);
            float vol = GameSettings.MusicVolume * 0.55f * MusicDuck;
            _music[_musicOn].volume = vol * _fade;
            var old = _music[1 - _musicOn];
            old.volume = vol * (1f - _fade);
            if (_fade >= 1f && old.isPlaying) old.Stop();
        }

        /// <summary>Keep the beam hum going while a Sunline Beam fires.</summary>
        public void Beam(bool on, bool paused)
        {
            float v = Sounds.Gain(Sfx.BeamHum) * GameSettings.SfxVolume * SfxScale;
            if (on && !paused && v > 0.001f)
            {
                _beam.volume = v;
                if (!_beam.isPlaying) _beam.Play();
            }
            else if (_beam.isPlaying) _beam.Stop();
        }

        void OnDestroy()
        {
            if (_clips != null) foreach (var c in _clips) if (c != null) Destroy(c);
            foreach (var c in _musicCache.Values) if (c != null) Destroy(c);
        }
    }
}
