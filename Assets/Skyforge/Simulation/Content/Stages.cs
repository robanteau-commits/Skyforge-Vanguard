using System.Collections.Generic;

namespace Skyforge.Simulation
{
    /// <summary>How a Gnat flies (formations are several Gnats on the same path, a little apart).</summary>
    public enum GnatPath : byte
    {
        /// <summary>Straight down (Param = speed).</summary>
        Dive,
        /// <summary>Down in a snaking weave around X.</summary>
        Weave,
        /// <summary>In from the left edge, across and down in an arc (Y = entry height).</summary>
        SweepRight,
        /// <summary>In from the right edge, across and down in an arc (Y = entry height).</summary>
        SweepLeft,
        /// <summary>Down while looping around X.</summary>
        Swirl,
    }

    /// <summary>One thing the stage spawns at a given time.</summary>
    public struct SpawnOrder
    {
        public float Time;
        public EnemyKind Kind;
        public float X, Y;
        public GnatPath Path;
        /// <summary>Kind-specific: Gnat dive speed, Needle hover height.</summary>
        public float Param;
        /// <summary>Not an enemy: a repair orb drifting down from (X, Y).</summary>
        public bool Repair;
    }

    /// <summary>A stage: its name and its spawn timeline, sorted by time.</summary>
    public sealed class StageDef
    {
        public string Id;
        public string Name;
        public SpawnOrder[] Orders;
        public float LastOrderTime => Orders.Length == 0 ? 0f : Orders[Orders.Length - 1].Time;
    }

    /// <summary>Writes stage timelines in readable steps: <c>b.Gnats(6f, GnatPath.SweepRight, 6, y: 12f)</c>.</summary>
    public sealed class StageBuilder
    {
        public const float SpawnY = Field.HalfHeight + 1.5f;
        readonly List<SpawnOrder> _orders = new List<SpawnOrder>();

        void Add(SpawnOrder o) => _orders.Add(o);

        /// <summary><paramref name="count"/> Gnats on one path, <paramref name="delay"/> seconds apart.</summary>
        public StageBuilder Gnats(float t, GnatPath path, int count, float x = 0f, float y = 12f, float delay = 0.35f, float speed = 6.5f)
        {
            for (int i = 0; i < count; i++)
            {
                float sx = path == GnatPath.SweepRight ? -Field.HalfWidth - 1.5f : path == GnatPath.SweepLeft ? Field.HalfWidth + 1.5f : x;
                float sy = path == GnatPath.SweepRight || path == GnatPath.SweepLeft ? y : SpawnY;
                Add(new SpawnOrder { Time = t + i * delay, Kind = EnemyKind.Gnat, X = sx, Y = sy, Path = path, Param = speed });
            }
            return this;
        }

        /// <summary>Five Gnats in a V, diving slowly.</summary>
        public StageBuilder V(float t, float x, float speed = 4.5f)
        {
            for (int dx = -2; dx <= 2; dx++)
                Add(new SpawnOrder { Time = t, Kind = EnemyKind.Gnat, X = x + dx * 1.3f, Y = SpawnY + (dx < 0 ? -dx : dx) * 1.1f, Path = GnatPath.Dive, Param = speed });
            return this;
        }

        public StageBuilder Needle(float t, float x, float hoverY = 9f) { Add(new SpawnOrder { Time = t, Kind = EnemyKind.Needle, X = x, Y = SpawnY, Param = hoverY }); return this; }
        public StageBuilder Ram(float t, float x) { Add(new SpawnOrder { Time = t, Kind = EnemyKind.RamDrone, X = x, Y = SpawnY }); return this; }
        public StageBuilder Mine(float t, float x) { Add(new SpawnOrder { Time = t, Kind = EnemyKind.SpineMine, X = x, Y = SpawnY }); return this; }
        public StageBuilder Warden(float t, float x = 0f) { Add(new SpawnOrder { Time = t, Kind = EnemyKind.Warden, X = x, Y = SpawnY + 1f }); return this; }
        public StageBuilder Bastion(float t, float x) { Add(new SpawnOrder { Time = t, Kind = EnemyKind.Bastion, X = x, Y = SpawnY + 1f }); return this; }
        public StageBuilder Repair(float t, float x = 0f) { Add(new SpawnOrder { Time = t, X = x, Y = SpawnY, Repair = true }); return this; }

        public StageDef Build(string id, string name)
        {
            // Stable sort by time (insertion sort: List.Sort isn't stable, and order must be deterministic).
            var a = _orders.ToArray();
            for (int i = 1; i < a.Length; i++)
            {
                var o = a[i];
                int j = i - 1;
                while (j >= 0 && a[j].Time > o.Time) { a[j + 1] = a[j]; j--; }
                a[j + 1] = o;
            }
            return new StageDef { Id = id, Name = name, Orders = a };
        }
    }

    /// <summary>
    /// Stage timelines. Stage 1-1 introduces Gnats and Needles (docs/DESIGN.md §6) and ends with
    /// the first Warden. Ids are stable (saves will use them); names can change.
    /// </summary>
    public static class Stages
    {
        public static readonly StageDef FirstLight = BuildFirstLight();
        public static readonly StageDef ArmadaSampler = BuildSampler();

        public static readonly StageDef[] All = { FirstLight, ArmadaSampler };

        public static StageDef ById(string id)
        {
            foreach (var s in All) if (s.Id == id) return s;
            return null;
        }

        /// <summary>1-1 "First Light": about 2.5 minutes. Gnats, then Needles, both together, a Warden to finish.</summary>
        static StageDef BuildFirstLight()
        {
            var b = new StageBuilder();
            // Opening: Gnats only.
            b.Gnats(2f, GnatPath.Weave, 5, x: 0f);
            b.Gnats(7f, GnatPath.SweepRight, 6, y: 12f);
            b.Gnats(11f, GnatPath.SweepLeft, 6, y: 10f);
            b.V(15f, -4f).V(17f, 4f);
            b.Gnats(21f, GnatPath.Swirl, 8, x: 0f, delay: 0.3f);
            // Needles arrive.
            b.Needle(27f, -4f).Needle(28f, 4f);
            b.Gnats(32f, GnatPath.Dive, 4, x: -6f).Gnats(32.5f, GnatPath.Dive, 4, x: 6f);
            b.Needle(36f, -6f).Needle(36.6f, 0f, 10f).Needle(37.2f, 6f);
            b.Gnats(42f, GnatPath.SweepRight, 8, y: 13f).Gnats(44f, GnatPath.SweepLeft, 8, y: 11f);
            b.Repair(48f, 0f);
            // Mixed waves.
            b.V(51f, 0f);
            b.Needle(53f, -5f, 8f).Needle(53f, 5f, 8f);
            b.Gnats(57f, GnatPath.Weave, 6, x: -4f).Gnats(57f, GnatPath.Weave, 6, x: 4f);
            b.Gnats(63f, GnatPath.Swirl, 6, x: -3.5f, delay: 0.3f).Gnats(65f, GnatPath.Swirl, 6, x: 3.5f, delay: 0.3f);
            b.Needle(70f, -6f).Needle(70.5f, -2f, 10f).Needle(71f, 2f, 10f).Needle(71.5f, 6f);
            b.V(76f, -4f).V(77f, 4f);
            b.Gnats(81f, GnatPath.SweepRight, 7, y: 12f).Gnats(81f, GnatPath.SweepLeft, 7, y: 8f);
            b.Needle(86f, 0f, 7f);
            b.Gnats(88f, GnatPath.Dive, 5, x: -7f, speed: 8f).Gnats(88f, GnatPath.Dive, 5, x: 7f, speed: 8f);
            b.Repair(92f, -4f);
            b.Gnats(95f, GnatPath.Weave, 8, x: 0f, delay: 0.3f);
            b.Needle(99f, -6f, 8f).Needle(99f, 6f, 8f).Needle(101f, -3f, 11f).Needle(101f, 3f, 11f);
            b.Gnats(106f, GnatPath.Swirl, 10, x: 0f, delay: 0.25f);
            b.V(112f, -5f).V(112f, 5f).V(115f, 0f);
            b.Needle(118f, -4f).Needle(118f, 4f);
            // Finale: the first Warden, with Gnat escorts.
            b.Warden(124f, 0f);
            b.Gnats(131f, GnatPath.SweepRight, 6, y: 6f).Gnats(137f, GnatPath.SweepLeft, 6, y: 6f);
            b.Repair(140f, 0f);
            return b.Build("1-1", "First Light");
        }

        /// <summary>A test stage that shows every enemy type in turn (not part of the campaign).</summary>
        static StageDef BuildSampler()
        {
            var b = new StageBuilder();
            b.Gnats(1f, GnatPath.Weave, 5, x: 0f);
            b.Gnats(5f, GnatPath.SweepRight, 5, y: 12f);
            b.Gnats(8f, GnatPath.Swirl, 6, x: 0f, delay: 0.3f);
            b.V(12f, 0f);
            b.Needle(15f, -4f).Needle(16f, 4f);
            b.Ram(22f, -5f).Ram(23f, 5f).Ram(24f, 0f);
            b.Mine(29f, -5f).Mine(29.5f, 0f).Mine(30f, 5f).Mine(32f, -2.5f).Mine(32.5f, 2.5f);
            b.Bastion(37f, -5f).Bastion(41f, 5f);
            b.Repair(46f, 0f);
            b.Warden(50f, 0f);
            return b.Build("sampler", "Armada Sampler");
        }
    }
}
