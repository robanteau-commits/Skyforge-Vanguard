using System.Collections.Generic;

namespace Skyforge.Simulation
{
    /// <summary>Settings for starting a stage.</summary>
    public sealed class StageSetup
    {
        public uint Seed = 1;
        public Loadout Loadout = Loadout.Starter();
        /// <summary>Spawn unarmed target drones (the M0 test flight).</summary>
        public bool PracticeDrones = true;
    }

    /// <summary>
    /// One running stage. Plain C#, no engine types: the Unity layer reads this state and draws it,
    /// and only changes it through <see cref="Step"/>. Split into partial files by topic.
    /// Same setup + same inputs ⇒ same result (see <see cref="ComputeHash"/>).
    /// </summary>
    public sealed partial class StageSim
    {
        public readonly StageSetup Setup;
        public readonly Loadout Loadout;
        public readonly PlayerShip Player = new PlayerShip();
        public readonly List<Shot> Shots = new List<Shot>(512);
        public readonly List<Enemy> Enemies = new List<Enemy>(64);
        /// <summary>What happened during the last step (cleared at the start of each step).</summary>
        public readonly List<SimEvent> Events = new List<SimEvent>(64);
        readonly Rng _rng;

        /// <summary>Steps taken so far.</summary>
        public int Tick { get; private set; }
        public float Time => Tick * Field.Dt;
        public long Score { get; private set; }
        public int Kills { get; private set; }

        int _nextEnemyId = 1;

        public StageSim(StageSetup setup = null)
        {
            Setup = setup ?? new StageSetup();
            Loadout = Setup.Loadout ?? Loadout.Starter();
            _rng = new Rng(Setup.Seed);
            Player.Pos = new Vec2(0f, -10f);
            // Offset the right wing by half a shot so the two wings alternate.
            var right = Loadout.RightWing.Stats;
            if (right != null) Player.RightCooldown = 0.5f / right.ShotsPerSecond;
        }

        /// <summary>Advance the stage by one fixed step (<see cref="Field.Dt"/>).</summary>
        public void Step(PlayerInput input)
        {
            Events.Clear();
            Tick++;
            MovePlayer(input);
            FireWings();
            if (Setup.PracticeDrones) UpdatePracticeDrones();
            MoveEnemies();
            MoveShots();
        }

        void AddScore(int points) => Score += points;

        Enemy SpawnEnemy(EnemyKind kind, Vec2 pos, Vec2 vel, float hp, float radius, int score)
        {
            var e = new Enemy
            {
                Id = _nextEnemyId++,
                Kind = kind,
                Pos = pos,
                BaseX = pos.X,
                Vel = vel,
                Hp = hp,
                MaxHp = hp,
                Radius = radius,
                Score = score,
                Phase = _rng.Range(0f, 6.2831853f),
            };
            Enemies.Add(e);
            return e;
        }

        void MoveEnemies()
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                e.Age += Field.Dt;
                switch (e.Kind)
                {
                    case EnemyKind.PracticeDrone: MovePracticeDrone(e); break;
                }
            }
            // Drop enemies that left the field (no score).
            Enemies.RemoveAll(e => e.Pos.Y < -Field.HalfHeight - Field.CullMargin || Field.IsOutside(e.Pos, 6f));
        }

        void Kill(Enemy e)
        {
            Kills++;
            AddScore(e.Score);
            Events.Add(new SimEvent { Type = SimEventType.Destroyed, Pos = e.Pos, EnemyKind = e.Kind, EnemyId = e.Id });
        }

        /// <summary>
        /// A checksum of all gameplay state. Two runs with the same setup and inputs must produce the
        /// same hash every step; tests use it to catch accidental non-determinism.
        /// Add any new gameplay state here.
        /// </summary>
        public ulong ComputeHash()
        {
            var h = new Hasher();
            h.Add(Tick); h.Add(Score); h.Add(Kills); h.Add(_rng.State); h.Add(_nextEnemyId);
            h.Add(Player.Pos); h.Add(Player.LeftCooldown); h.Add(Player.RightCooldown);
            h.Add(Shots.Count);
            foreach (var s in Shots) { h.Add(s.Pos); h.Add(s.Vel); h.Add(s.Damage); }
            h.Add(Enemies.Count);
            foreach (var e in Enemies) { h.Add(e.Id); h.Add((int)e.Kind); h.Add(e.Pos); h.Add(e.Hp); h.Add(e.Age); }
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
