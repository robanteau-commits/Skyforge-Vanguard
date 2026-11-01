using System.Collections.Generic;

namespace Skyforge.Simulation
{
    /// <summary>Settings for starting a stage.</summary>
    public sealed class StageSetup
    {
        public uint Seed = 1;
        public Loadout Loadout = Loadout.Starter();
        /// <summary>The stage to play; null = the practice flight (unarmed target drones).</summary>
        public StageDef Stage;
        /// <summary>Spawn unarmed target drones. Defaults to on when there's no stage.</summary>
        public bool? PracticeDrones;
        /// <summary>Speeds up the core's charging (the hangar preview uses it so the core fires often).</summary>
        public float CoreChargeMultiplier = 1f;
        /// <summary>Play field width: <see cref="Field.Width"/> (portrait, phones) or <see cref="Field.WideWidth"/> (PC, web).</summary>
        public float FieldWidth = Field.Width;
        /// <summary>Airframe upgrade levels per <see cref="AirframeStat"/> (null = stock ship).</summary>
        public int[] Airframe;
    }

    /// <summary>
    /// One running stage. Plain C#, no engine types: the Unity layer reads this state and draws it,
    /// and only changes it through <see cref="Step"/>. Split into partial files by topic:
    /// Player (movement), Weapons (player fire), Enemies (behaviour), Combat (enemy fire, damage,
    /// pickups), Timeline (spawning and the end of the stage), Practice (target drones).
    /// Same setup + same inputs ⇒ same result (see <see cref="ComputeHash"/>).
    /// </summary>
    public sealed partial class StageSim
    {
        public readonly StageSetup Setup;
        public readonly Loadout Loadout;
        public readonly StageDef Stage;
        public readonly PlayerShip Player = new PlayerShip();
        /// <summary>Half the play field's width for this stage.</summary>
        public readonly float HalfWidth;
        /// <summary>How much wider than the authored portrait field this one is (1 on phones); spawn X is scaled by it.</summary>
        public readonly float XScale;
        public readonly List<Shot> Shots = new List<Shot>(512);
        public readonly List<EnemyShot> EnemyShots = new List<EnemyShot>(512);
        public readonly List<Enemy> Enemies = new List<Enemy>(64);
        public readonly List<Pickup> Pickups = new List<Pickup>(128);
        /// <summary>What happened during the last step (cleared at the start of each step).</summary>
        public readonly List<SimEvent> Events = new List<SimEvent>(64);
        readonly Rng _rng;
        readonly bool _practice;

        /// <summary>Steps taken so far.</summary>
        public int Tick { get; private set; }
        public float Time => Tick * Field.Dt;
        public StageStatus Status { get; private set; } = StageStatus.Playing;
        /// <summary>Seconds since the stage was cleared or failed (0 while playing).</summary>
        public float TimeSinceEnd { get; private set; }
        public long Score { get; private set; }
        public int Kills { get; private set; }
        /// <summary>Stage enemies spawned so far (the kill percentage is out of these).</summary>
        public int Spawned { get; private set; }
        /// <summary>Credits collected this stage.</summary>
        public int Credits { get; private set; }
        /// <summary>Total hull damage taken (the Untouched medal needs 0).</summary>
        public float HullDamageTaken { get; private set; }

        /// <summary>Score multiplier in tenths (10 = ×1.0 … 50 = ×5.0), kept as an integer so it never drifts.</summary>
        public int ComboTenths { get; private set; } = 10;
        public float Combo => ComboTenths / 10f;
        float _comboIdle;

        int _nextEnemyId = 1;
        int _nextPickupId = 1;

        // Combo rules (docs/DESIGN.md §7).
        public const int ComboMaxTenths = 50;
        public const float ComboDecaySeconds = 2.5f;

        public StageSim(StageSetup setup = null)
        {
            Setup = setup ?? new StageSetup();
            Loadout = Setup.Loadout ?? Loadout.Starter();
            Stage = Setup.Stage;
            HalfWidth = (Setup.FieldWidth > 0f ? Setup.FieldWidth : Field.Width) * 0.5f;
            XScale = HalfWidth / Field.HalfWidth;
            _practice = Setup.PracticeDrones ?? Stage == null;
            _rng = new Rng(Setup.Seed);
            Simulation.Airframe.Apply(Player, Setup.Airframe);
            Player.Pos = new Vec2(0f, -10f);
            // Offset the right wing by half a shot so the two wings alternate.
            var right = Loadout.RightWing.Stats;
            if (right != null) Player.RightCooldown = 0.5f / right.ShotsPerSecond;
        }

        /// <summary>Kill percentage (0..100) of the stage enemies spawned so far.</summary>
        public int KillPercent => Spawned == 0 ? 0 : (int)(Kills * 100L / Spawned);

        /// <summary>Credits kept: all of them on a clear, half on a failed run (decided by Rob).</summary>
        public int CreditsBanked => Status == StageStatus.Failed ? Credits / 2 : Credits;

        /// <summary>Advance the stage by one fixed step (<see cref="Field.Dt"/>).</summary>
        public void Step(PlayerInput input)
        {
            Events.Clear();
            Tick++;
            if (Status != StageStatus.Playing) TimeSinceEnd += Field.Dt;

            if (Player.Alive)
            {
                MovePlayer(input);
                FireWings();
            }
            else
            {
                Player.BeamOn[0] = Player.BeamOn[1] = false;
            }
            UpdateCore(input);
            UpdateDefences();
            if (Status == StageStatus.Playing) RunTimeline();
            if (_practice) UpdatePracticeDrones();
            UpdateEnemies();
            MoveShots();
            MoveEnemyShots();
            UpdatePickups();
            UpdateCombo();
            Enemies.RemoveAll(e => e.Dead || Outside(e.Pos, 6f));
            CheckStageEnd();
        }

        /// <summary>Past the edge of this stage's field by more than <paramref name="margin"/>.</summary>
        bool Outside(Vec2 p, float margin) => Field.IsOutside(p, margin, HalfWidth);

        Enemy SpawnEnemy(EnemyDef def, Vec2 pos, bool counted)
        {
            var e = new Enemy
            {
                Id = _nextEnemyId++,
                Kind = def.Kind,
                Def = def,
                Pos = pos,
                BaseX = pos.X,
                StartY = pos.Y,
                Hp = def.Hp,
                MaxHp = def.Hp,
                Radius = def.Radius,
                Score = def.Score,
                Phase = _rng.Range(0f, 6.2831853f),
                Counted = counted,
            };
            Enemies.Add(e);
            if (counted) Spawned++;
            return e;
        }

        /// <summary>Damage an enemy; destroys it (with score, drops, effects) at 0 HP. True if it died.
        /// Continuous damage (beams) passes <paramref name="hitEvent"/> = false on most steps.</summary>
        bool DamageEnemy(Enemy e, float damage, Vec2 at, bool hitEvent = true)
        {
            if (e.Dead) return false;
            e.Hp -= damage;
            if (e.Hp > 0f)
            {
                if (hitEvent) Events.Add(new SimEvent { Type = SimEventType.Hit, Pos = at, EnemyKind = e.Kind, EnemyId = e.Id });
                return false;
            }
            Kill(e);
            return true;
        }

        void Kill(Enemy e)
        {
            e.Dead = true;
            Kills++;
            long points = (long)e.Score * ComboTenths / 10;
            Score += points;
            ComboTenths = ComboTenths + 1 > ComboMaxTenths ? ComboMaxTenths : ComboTenths + 1;
            _comboIdle = 0f;
            Events.Add(new SimEvent { Type = SimEventType.Destroyed, Pos = e.Pos, EnemyKind = e.Kind, EnemyId = e.Id, Value = points });
            DropLoot(e);
            ChargeCore(e.Kind == EnemyKind.Warden || e.Kind == EnemyKind.Bastion ? CoreChargePerBigKill : CoreChargePerKill);
            if (e.Kind == EnemyKind.SpineMine) MineBurst(e);
        }

        void UpdateCombo()
        {
            if (ComboTenths <= 10) { _comboIdle = 0f; return; }
            _comboIdle += Field.Dt;
            if (_comboIdle >= ComboDecaySeconds)
            {
                ComboTenths = ComboTenths - 10 < 10 ? 10 : ComboTenths - 10;
                _comboIdle = 0f;
            }
        }

        /// <summary>
        /// A checksum of all gameplay state. Two runs with the same setup and inputs must produce the
        /// same hash every step; tests use it to catch accidental non-determinism.
        /// Add any new gameplay state here.
        /// </summary>
        public ulong ComputeHash()
        {
            var h = new Hasher();
            h.Add(Tick); h.Add(Score); h.Add(Kills); h.Add(Spawned); h.Add(Credits); h.Add(ComboTenths); h.Add(_comboIdle);
            h.Add(HullDamageTaken); h.Add((int)Status); h.Add(_rng.State); h.Add(_nextEnemyId); h.Add(_nextPickupId);
            h.Add(_nextOrder); h.Add(_droneTimer);
            var p = Player;
            h.Add(p.Pos); h.Add(p.LeftCooldown); h.Add(p.RightCooldown); h.Add(p.Alive ? 1 : 0);
            h.Add(p.Hull); h.Add(p.Shield); h.Add(p.ShieldRegenWait); h.Add(p.Invulnerable);
            h.Add(p.CoreCharge); h.Add(p.CoreActive ? 1 : 0); h.Add(p.CoreTimer); h.Add(p.WingmateCount);
            for (int i = 0; i < 2; i++)
            {
                h.Add(p.BeamOn[i] ? 1 : 0); h.Add(p.BeamStrands[i]); h.Add(p.BeamTarget[i]); h.Add(p.BeamTime[i]);
                h.Add(p.BeamPulseTimer[i]); h.Add(p.VolleyCount[i]);
            }
            foreach (var y in p.BeamEndY) h.Add(y);
            foreach (var c in p.WingmateCooldown) h.Add(c);
            h.Add(Shots.Count);
            foreach (var s in Shots)
            {
                h.Add(s.Pos); h.Add(s.Vel); h.Add(s.Damage); h.Add(s.Life); h.Add(s.HitsLeft); h.Add(s.LastHit); h.Add(s.TargetId);
                h.Add(s.Burst); h.Add(s.BlastOnExpire ? 1 : 0); h.Add(s.Split ? 1 : 0);
            }
            h.Add(EnemyShots.Count);
            foreach (var s in EnemyShots) { h.Add(s.Pos); h.Add(s.Vel); h.Add(s.Damage); }
            h.Add(Enemies.Count);
            foreach (var e in Enemies)
            {
                h.Add(e.Id); h.Add((int)e.Kind); h.Add(e.Pos); h.Add(e.Vel); h.Add(e.Hp); h.Add(e.Age);
                h.Add(e.Mode); h.Add(e.Timer); h.Add(e.Counter); h.Add(e.Target); h.Add(e.Facing);
            }
            h.Add(Pickups.Count);
            foreach (var k in Pickups) { h.Add(k.Id); h.Add(k.Pos); h.Add(k.Vel); h.Add(k.Value); h.Add(k.Homing ? 1 : 0); }
            return h.Value;
        }

        /// <summary>FNV-1a over raw bits.</summary>
        struct Hasher
        {
            ulong _h;
            public ulong Value => _h == 0 ? 14695981039346656037UL : _h;

            public void Add(ulong v)
            {
                if (_h == 0) _h = 14695981039346656037UL;
                for (int i = 0; i < 8; i++)
                {
                    _h ^= (v >> (i * 8)) & 0xFF;
                    _h *= 1099511628211UL;
                }
            }

            public void Add(long v) => Add((ulong)v);
            public void Add(int v) => Add((ulong)(uint)v);
            public void Add(uint v) => Add((ulong)v);
            public void Add(float v) => Add((ulong)(uint)System.BitConverter.SingleToInt32Bits(v));
            public void Add(Vec2 v) { Add(v.X); Add(v.Y); }
        }
    }
}
