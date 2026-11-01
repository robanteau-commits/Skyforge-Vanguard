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

    /// <summary>A stage: its name, spawn timeline (sorted by time) and medal rules.</summary>
    public sealed class StageDef
    {
        public string Id;
        public string Name;
        public SpawnOrder[] Orders;
        /// <summary>Score for the Ace medal.</summary>
        public long AceScore = 20000;
        /// <summary>One-time credit bonus for each medal: Ace, Untouched, Purge.</summary>
        public int[] MedalBonus = { 250, 200, 150 };
        public float LastOrderTime => Orders.Length == 0 ? 0f : Orders[Orders.Length - 1].Time;
    }

    /// <summary>A stage's place on the world map.</summary>
    public sealed class WorldStage
    {
        public string Id;
        public string Name;
        public string Blurb;
        /// <summary>The playable stage, or null if it isn't built yet.</summary>
        public StageDef Def;
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
        /// <summary>A Brood Carrier crossing from the side <paramref name="x"/> is on, at height <paramref name="y"/>.</summary>
        public StageBuilder Carrier(float t, float x, float y = 10f) { Add(new SpawnOrder { Time = t, Kind = EnemyKind.BroodCarrier, X = x, Y = SpawnY, Param = y }); return this; }
        /// <summary>The Tidebreaker rises in.</summary>
        public StageBuilder Tidebreaker(float t) { Add(new SpawnOrder { Time = t, Kind = EnemyKind.Tidebreaker, X = 0f, Y = SpawnY }); return this; }
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
        public static readonly StageDef HarborLine = BuildHarborLine();
        public static readonly StageDef Stormwall = BuildStormwall();
        public static readonly StageDef FoundryRidge = BuildFoundryRidge();
        public static readonly StageDef UpperAir = BuildUpperAir();
        public static readonly StageDef TidebreakerStage = BuildTidebreaker();
        public static readonly StageDef ArmadaSampler = BuildSampler();

        /// <summary>Kill percentage for the Purge medal.</summary>
        public const int PurgePercent = 90;

        /// <summary>World 1 "Earthfall" in map order (docs/DESIGN.md §6).</summary>
        public static readonly WorldStage[] World1 =
        {
            new WorldStage { Id = "1-1", Name = "First Light", Def = FirstLight, Blurb = "Gnats and Needles; the first Warden." },
            new WorldStage { Id = "1-2", Name = "Harbor Line", Def = HarborLine, Blurb = "Bastion turrets on drifting asteroids." },
            new WorldStage { Id = "1-3", Name = "Stormwall", Def = Stormwall, Blurb = "Spine Mines and Ram Drones." },
            new WorldStage { Id = "1-4", Name = "Foundry Ridge", Def = FoundryRidge, Blurb = "Wardens and the first Brood Carrier." },
            new WorldStage { Id = "1-5", Name = "Upper Air", Def = UpperAir, Blurb = "Everything at once." },
            new WorldStage { Id = "1-B", Name = "The Tidebreaker", Def = TidebreakerStage, Blurb = "Boss: the Armada's sea-fortress." },
        };

        public static readonly StageDef[] All = { FirstLight, HarborLine, Stormwall, FoundryRidge, UpperAir, TidebreakerStage, ArmadaSampler };

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
            var def = b.Build("1-1", "First Light");
            def.AceScore = 18000;
            def.MedalBonus = new[] { 250, 200, 150 };
            return def;
        }

        static StageDef Finish(StageBuilder b, string id, string name, long ace, int a, int u, int p)
        {
            var def = b.Build(id, name);
            def.AceScore = ace;
            def.MedalBonus = new[] { a, u, p };
            return def;
        }

        /// <summary>1-2 "Harbor Line": about 2.5 minutes. Bastion turrets ride drifting asteroids through Gnat and Needle waves.</summary>
        static StageDef BuildHarborLine()
        {
            var b = new StageBuilder();
            b.Gnats(2f, GnatPath.SweepRight, 6, y: 12f).Gnats(4f, GnatPath.SweepLeft, 6, y: 10f);
            b.V(8f, 0f);
            // The first Bastion, on its own.
            b.Bastion(12f, -4f);
            b.Gnats(16f, GnatPath.Weave, 5, x: 4f);
            b.Bastion(21f, 5f);
            b.Needle(24f, -5f).Needle(25f, 0f, 10f);
            b.Gnats(29f, GnatPath.Swirl, 8, x: 0f, delay: 0.3f);
            b.Bastion(33f, -6f).Bastion(35f, 2f);
            b.V(38f, 5f).V(40f, -5f);
            b.Repair(44f, 0f);
            b.Needle(46f, -6f).Needle(46.5f, 6f).Needle(48f, 0f, 11f);
            b.Gnats(51f, GnatPath.Dive, 5, x: -3f, speed: 7f).Gnats(52f, GnatPath.Dive, 5, x: 3f, speed: 7f);
            b.Bastion(56f, 0f);
            b.Gnats(59f, GnatPath.SweepRight, 8, y: 13f).Gnats(61f, GnatPath.SweepLeft, 8, y: 9f);
            b.Bastion(66f, -5f).Bastion(66f, 5f);
            b.Gnats(70f, GnatPath.Weave, 6, x: 0f);
            b.Needle(74f, -4f, 8f).Needle(74f, 4f, 8f);
            b.Repair(79f, 4f);
            b.Gnats(81f, GnatPath.Swirl, 6, x: -4f, delay: 0.3f).Gnats(83f, GnatPath.Swirl, 6, x: 4f, delay: 0.3f);
            b.Bastion(87f, -3f).Bastion(90f, 4f).Bastion(93f, -6f);
            b.V(96f, 0f);
            b.Needle(99f, -6f).Needle(99.5f, -2f, 10f).Needle(100f, 2f, 10f).Needle(100.5f, 6f);
            b.Gnats(105f, GnatPath.SweepLeft, 8, y: 12f).Gnats(107f, GnatPath.SweepRight, 8, y: 8f);
            b.Bastion(111f, 0f).Bastion(113f, -6f).Bastion(113f, 6f);
            b.Repair(118f, 0f);
            // Finale: a Warden escorted by Bastions.
            b.Warden(122f, 0f);
            b.Bastion(128f, -6f).Bastion(132f, 6f);
            b.Gnats(136f, GnatPath.Weave, 6, x: 0f);
            b.Repair(140f, 0f);
            return Finish(b, "1-2", "Harbor Line", 20000, 275, 225, 175);
        }

        /// <summary>1-3 "Stormwall": about 3 minutes. Spine Mine fields and Ram Drone charges.</summary>
        static StageDef BuildStormwall()
        {
            var b = new StageBuilder();
            b.Gnats(2f, GnatPath.Weave, 5, x: 0f);
            // Spine Mines drift in: shoot them from a distance.
            b.Mine(6f, -4f).Mine(7f, 4f).Mine(9f, 0f);
            b.Gnats(12f, GnatPath.SweepRight, 6, y: 12f);
            b.Mine(15f, -6f).Mine(15.5f, -2f).Mine(16f, 2f).Mine(16.5f, 6f);
            // Ram Drones: watch for the red flash and sidestep.
            b.Ram(21f, 0f);
            b.Ram(24f, -5f).Ram(25f, 5f);
            b.Gnats(28f, GnatPath.Swirl, 8, x: 0f, delay: 0.3f);
            b.Mine(32f, -3f).Mine(32f, 3f).Ram(34f, 0f);
            b.Needle(37f, -5f).Needle(38f, 5f);
            b.Ram(41f, -6f).Ram(41.5f, 6f).Ram(42f, 0f);
            b.Repair(46f, 0f);
            b.V(48f, -4f).V(50f, 4f);
            b.Mine(53f, -7f).Mine(53.5f, -3.5f).Mine(54f, 0f).Mine(54.5f, 3.5f).Mine(55f, 7f);
            b.Gnats(58f, GnatPath.SweepLeft, 8, y: 11f);
            b.Ram(62f, -4f).Ram(63f, 4f).Ram(64f, -1f).Ram(65f, 1f);
            b.Needle(69f, -6f, 8f).Needle(69f, 6f, 8f);
            b.Mine(72f, 0f).Gnats(73f, GnatPath.Weave, 6, x: -4f).Gnats(73f, GnatPath.Weave, 6, x: 4f);
            b.Bastion(78f, -5f);
            b.Ram(81f, 5f).Ram(82f, 2f);
            b.Repair(86f, 4f);
            b.Mine(89f, -5f).Mine(89f, 5f).Mine(91f, -1.5f).Mine(91f, 1.5f);
            b.Gnats(94f, GnatPath.Swirl, 6, x: -3.5f, delay: 0.3f).Gnats(96f, GnatPath.Swirl, 6, x: 3.5f, delay: 0.3f);
            b.Ram(100f, -6f).Ram(100.5f, -2f).Ram(101f, 2f).Ram(101.5f, 6f);
            b.Needle(105f, -3f, 10f).Needle(105f, 3f, 10f);
            b.Mine(109f, -6f).Mine(109.5f, 0f).Mine(110f, 6f).Ram(111f, -3f).Ram(112f, 3f);
            b.V(115f, 0f);
            b.Bastion(118f, 4f).Bastion(121f, -4f);
            b.Repair(125f, 0f);
            b.Gnats(127f, GnatPath.SweepRight, 8, y: 13f).Gnats(129f, GnatPath.SweepLeft, 8, y: 9f);
            b.Ram(133f, 0f).Ram(134f, -5f).Ram(135f, 5f);
            b.Mine(138f, -4f).Mine(138f, 4f).Mine(140f, 0f);
            b.Needle(143f, -6f).Needle(143.5f, -2f, 10f).Needle(144f, 2f, 10f).Needle(144.5f, 6f);
            // Finale: a Warden inside a mine field.
            b.Warden(150f, 0f);
            b.Mine(154f, -6f).Mine(158f, 6f).Mine(162f, -3f).Mine(166f, 3f);
            b.Ram(160f, -6f).Ram(168f, 6f);
            b.Repair(170f, 0f);
            return Finish(b, "1-3", "Stormwall", 20000, 300, 250, 200);
        }

        /// <summary>1-4 "Foundry Ridge": about 3 minutes. Wardens, and the first Brood Carrier halfway.</summary>
        static StageDef BuildFoundryRidge()
        {
            var b = new StageBuilder();
            b.Gnats(2f, GnatPath.SweepRight, 6, y: 12f).Gnats(3f, GnatPath.SweepLeft, 6, y: 10f);
            b.Needle(7f, -4f).Needle(8f, 4f);
            b.Warden(12f, -3f);
            b.Gnats(18f, GnatPath.Weave, 5, x: 5f);
            b.Bastion(22f, 5f);
            b.Ram(26f, -5f).Ram(27f, 5f);
            b.V(31f, 0f);
            b.Warden(35f, 4f);
            b.Mine(40f, -5f).Mine(41f, 0f);
            b.Gnats(44f, GnatPath.Swirl, 8, x: -2f, delay: 0.3f);
            b.Repair(50f, 0f);
            b.Needle(53f, -6f).Needle(53.5f, 6f).Needle(55f, 0f, 11f);
            b.Gnats(58f, GnatPath.Dive, 5, x: -5f, speed: 7f).Gnats(59f, GnatPath.Dive, 5, x: 5f, speed: 7f);
            b.Bastion(63f, -4f).Bastion(65f, 4f);
            b.Repair(72f, -4f);
            // Mid-boss: the Brood Carrier crosses from the left. Shoot its launch bays to stop the Gnats.
            b.Carrier(75f, -1f, 10.5f);
            b.Gnats(92f, GnatPath.SweepLeft, 6, y: 12f);
            b.Repair(100f, 0f);
            b.Warden(104f, 0f);
            b.Gnats(110f, GnatPath.Weave, 6, x: -5f).Gnats(110f, GnatPath.Weave, 6, x: 5f);
            b.Ram(116f, 0f).Ram(117f, -4f).Ram(118f, 4f);
            b.V(122f, -4f).V(123f, 4f);
            b.Bastion(127f, 0f);
            b.Needle(131f, -5f, 8f).Needle(131f, 5f, 8f);
            b.Repair(136f, 0f);
            b.Warden(139f, -4f).Warden(145f, 4f);
            b.Gnats(150f, GnatPath.SweepRight, 8, y: 13f).Gnats(152f, GnatPath.SweepLeft, 8, y: 9f);
            b.Mine(157f, -6f).Mine(157f, 6f);
            b.Needle(162f, -3f, 10f).Needle(162f, 3f, 10f);
            b.Gnats(167f, GnatPath.Swirl, 10, x: 0f, delay: 0.25f);
            b.Repair(172f, 0f);
            return Finish(b, "1-4", "Foundry Ridge", 22000, 325, 275, 225);
        }

        /// <summary>1-5 "Upper Air": about 3.5 minutes. Everything at once, the hardest regular stage.</summary>
        static StageDef BuildUpperAir()
        {
            var b = new StageBuilder();
            b.Gnats(2f, GnatPath.SweepRight, 8, y: 12f).Gnats(3f, GnatPath.SweepLeft, 8, y: 9f);
            b.Needle(7f, -6f).Needle(7.5f, 0f, 11f).Needle(8f, 6f);
            b.Ram(12f, -4f).Ram(12.5f, 4f);
            b.Bastion(15f, -5f).Bastion(17f, 5f);
            b.Gnats(20f, GnatPath.Swirl, 8, x: 0f, delay: 0.3f);
            b.Mine(24f, -6f).Mine(24.5f, -2f).Mine(25f, 2f).Mine(25.5f, 6f);
            b.Warden(29f, 0f);
            b.Ram(34f, -6f).Ram(36f, 6f);
            b.V(39f, -5f).V(40f, 5f);
            b.Repair(45f, 0f);
            b.Needle(47f, -5f, 8f).Needle(47f, 5f, 8f).Needle(49f, -2f, 11f).Needle(49f, 2f, 11f);
            b.Gnats(53f, GnatPath.Weave, 6, x: -4f).Gnats(53f, GnatPath.Weave, 6, x: 4f);
            b.Bastion(57f, 0f).Mine(59f, -5f).Mine(59f, 5f);
            b.Ram(63f, -3f).Ram(63.5f, 3f).Ram(64f, 0f);
            b.Repair(70f, 4f);
            // A Brood Carrier from the right, with Needles.
            b.Carrier(72f, 1f, 11f);
            b.Needle(80f, -6f, 8f).Needle(86f, 6f, 8f);
            b.Repair(96f, -4f);
            b.Gnats(98f, GnatPath.SweepRight, 8, y: 13f).Gnats(99f, GnatPath.SweepLeft, 8, y: 10f);
            b.Warden(104f, -4f);
            b.Bastion(108f, 5f).Ram(110f, 0f);
            b.Mine(113f, -3f).Mine(113f, 3f);
            b.Warden(117f, 4f);
            b.Gnats(122f, GnatPath.Dive, 6, x: -6f, speed: 8f).Gnats(122f, GnatPath.Dive, 6, x: 6f, speed: 8f);
            b.Repair(128f, 0f);
            b.Needle(131f, -6f).Needle(131.5f, -2f, 10f).Needle(132f, 2f, 10f).Needle(132.5f, 6f);
            b.Ram(136f, -5f).Ram(136.5f, 5f).Ram(137f, -1f).Ram(137.5f, 1f);
            b.Bastion(141f, -5f).Bastion(141f, 5f);
            b.Gnats(145f, GnatPath.Swirl, 6, x: -3.5f, delay: 0.3f).Gnats(147f, GnatPath.Swirl, 6, x: 3.5f, delay: 0.3f);
            b.Mine(151f, -7f).Mine(151.5f, -3.5f).Mine(152f, 0f).Mine(152.5f, 3.5f).Mine(153f, 7f);
            b.Repair(158f, 0f);
            // Finale: a second Carrier from the left and two Wardens.
            b.Carrier(161f, -1f, 10f);
            b.Warden(178f, -4f).Warden(180f, 4f);
            b.Gnats(188f, GnatPath.SweepRight, 8, y: 6f).Gnats(194f, GnatPath.SweepLeft, 8, y: 6f);
            b.Ram(198f, 0f);
            b.Repair(200f, 0f);
            return Finish(b, "1-5", "Upper Air", 30000, 350, 300, 250);
        }

        /// <summary>1-B "The Tidebreaker": a short escort wave, then the boss (docs/DESIGN.md §6.1).</summary>
        static StageDef BuildTidebreaker()
        {
            var b = new StageBuilder();
            b.Gnats(2f, GnatPath.SweepRight, 6, y: 12f).Gnats(4f, GnatPath.SweepLeft, 6, y: 10f);
            b.Needle(8f, -5f).Needle(8.5f, 5f);
            b.V(12f, 0f);
            b.Ram(16f, -4f).Ram(16.5f, 4f);
            b.Repair(20f, 0f);
            b.Tidebreaker(22f);
            return Finish(b, "1-B", "The Tidebreaker", 18000, 400, 350, 300);
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
            b.Carrier(68f, -1f, 10.5f);
            return b.Build("sampler", "Armada Sampler");
        }
    }
}
