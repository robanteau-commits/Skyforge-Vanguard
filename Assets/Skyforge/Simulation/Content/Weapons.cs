namespace Skyforge.Simulation
{
    /// <summary>
    /// Every weapon in the game. Level 1 and level 5 numbers come from docs/DESIGN.md §4; levels 2–4
    /// sit evenly between them. Change the design doc first, then these.
    /// </summary>
    public static class Weapons
    {
        /// <summary>Upgrade price to reach level 2, 3, 4, 5 (wing weapons).</summary>
        public static readonly int[] WingUpgradeCosts = { 300, 600, 1000, 1600 };
        /// <summary>Upgrade price to reach level 2, 3, 4, 5 (core specials).</summary>
        public static readonly int[] CoreUpgradeCosts = { 400, 800, 1300, 2000 };

        static float L(float a, float b, int lv) => a + (b - a) * (lv - 1) / 4f;
        static int Li(float a, float b, int lv) => (int)(L(a, b, lv) + 0.5f);

        static WeaponLevel[] Levels(System.Func<int, WeaponLevel> make)
        {
            var l = new WeaponLevel[5];
            for (int i = 0; i < 5; i++) l[i] = make(i + 1);
            return l;
        }

        static WeaponLevel Pulse(int streams, float rate, float damage) => new WeaponLevel
        {
            Count = streams, ShotsPerSecond = rate, Damage = damage, StreamGap = 0.28f,
            ProjectileSpeed = 34f, ProjectileRadius = 0.18f,
        };

        // ------------------------------------------------------------------ wing weapons

        /// <summary>Starter: a fast stream of bolts. DPS per wing 20 → 47 (≈2.3× at level 5).</summary>
        public static readonly WeaponDef PulseRepeater = new WeaponDef
        {
            Id = "pulseRepeater", Name = "Pulse Repeater", Slot = WeaponSlot.Wing, Kind = WeaponKind.PulseRepeater, Price = 0,
            Description = "A fast stream of plasma bolts straight ahead. Reliable against anything.",
            Role = "All-rounder",
            Levels = new[] { Pulse(1, 10f, 2.0f), Pulse(2, 10f, 1.25f), Pulse(2, 11f, 1.4f), Pulse(3, 11f, 1.15f), Pulse(3, 12f, 1.3f) },
        };

        public static readonly WeaponDef ScatterCannon = new WeaponDef
        {
            Id = "scatterCannon", Name = "Scatter Cannon", Slot = WeaponSlot.Wing, Kind = WeaponKind.ScatterCannon, Price = 600,
            Description = "A fan of pellets. Shreds swarms up close; weak against single targets far away.",
            Role = "Swarms, close range",
            Levels = Levels(lv => new WeaponLevel
            {
                Count = Li(3, 7, lv), Spread = L(15, 40, lv), ShotsPerSecond = L(4, 5, lv), Damage = L(1.8f, 2.4f, lv),
                ProjectileSpeed = 26f, ProjectileRadius = 0.16f, Life = 0.75f,
            }),
        };

        public static readonly WeaponDef LanceDriver = new WeaponDef
        {
            Id = "lanceDriver", Name = "Lance Driver", Slot = WeaponSlot.Wing, Kind = WeaponKind.LanceDriver, Price = 900,
            Description = "Slow, heavy laser bolts that pierce every enemy in their path.",
            Role = "Lines of enemies, armour",
            Levels = Levels(lv => new WeaponLevel
            {
                ShotsPerSecond = L(2, 3, lv), Damage = L(11, 17, lv), Pierce = true,
                ProjectileSpeed = 40f, ProjectileRadius = L(0.22f, 0.33f, lv),
            }),
        };

        public static readonly WeaponDef SunlineBeam = new WeaponDef
        {
            Id = "sunlineBeam", Name = "Sunline Beam", Slot = WeaponSlot.Wing, Kind = WeaponKind.SunlineBeam, Price = 1400,
            Description = "A continuous beam up the screen. Its damage builds the longer it stays on one target.",
            Role = "Bosses, gunships",
            Levels = Levels(lv => new WeaponLevel
            {
                Damage = L(16, 30, lv), DamageMax = L(28, 60, lv), RampTime = 1.5f, Width = L(0.35f, 0.6f, lv),
            }),
        };

        public static readonly WeaponDef SeekerPods = new WeaponDef
        {
            Id = "seekerPods", Name = "Seeker Pods", Slot = WeaponSlot.Wing, Kind = WeaponKind.SeekerPods, Price = 800,
            Description = "Missiles that lock onto the nearest enemy and burst on impact.",
            Role = "Enemies off to the sides",
            Levels = Levels(lv => new WeaponLevel
            {
                Count = Li(1, 3, lv), ShotsPerSecond = 1f / L(0.6f, 0.5f, lv), Damage = L(13, 16, lv), Blast = L(0.8f, 1.2f, lv),
                Spread = 30f, ProjectileSpeed = 16f, ProjectileRadius = 0.25f, TurnRate = 220f, Life = 3f,
            }),
        };

        public static readonly WeaponDef HornetRack = new WeaponDef
        {
            Id = "hornetRack", Name = "Hornet Rack", Slot = WeaponSlot.Wing, Kind = WeaponKind.HornetRack, Price = 1100,
            Description = "A salvo of micro-missiles that spread out across every target in sight.",
            Role = "Swarms, mines",
            Levels = Levels(lv => new WeaponLevel
            {
                Count = Li(4, 8, lv), ShotsPerSecond = 1f / L(1.2f, 0.9f, lv), Damage = L(6, 7, lv),
                Spread = 120f, ProjectileSpeed = 20f, ProjectileRadius = 0.2f, TurnRate = 320f, Life = 2.5f,
            }),
        };

        public static readonly WeaponDef ArcCoil = new WeaponDef
        {
            Id = "arcCoil", Name = "Arc Coil", Slot = WeaponSlot.Wing, Kind = WeaponKind.ArcCoil, Price = 1300,
            Description = "Lightning that strikes the nearest enemy and jumps to others nearby.",
            Role = "Clusters; short range",
            Levels = Levels(lv => new WeaponLevel
            {
                ShotsPerSecond = L(3, 4, lv), Damage = L(6, 9, lv), Jumps = Li(2, 5, lv), Range = L(9, 11, lv),
            }),
        };

        public static readonly WeaponDef RicochetDisc = new WeaponDef
        {
            Id = "ricochetDisc", Name = "Ricochet Disc", Slot = WeaponSlot.Wing, Kind = WeaponKind.RicochetDisc, Price = 1000,
            Description = "Spinning discs that bounce off the sides of the screen and cut through enemies.",
            Role = "Crowded screens",
            Levels = Levels(lv => new WeaponLevel
            {
                Count = lv >= 3 ? 2 : 1, ShotsPerSecond = 1f / L(0.8f, 0.6f, lv), Damage = L(8, 12, lv), MaxHits = Li(4, 6, lv),
                ProjectileSpeed = 15f, ProjectileRadius = 0.35f, Life = 4f,
            }),
        };

        // ------------------------------------------------------------------ core specials

        public static readonly WeaponDef PulseNova = new WeaponDef
        {
            Id = "pulseNova", Name = "Pulse Nova", Slot = WeaponSlot.Core, Kind = WeaponKind.PulseNova, Price = 0,
            Description = "Clears every enemy bullet on screen and damages every enemy in view.",
            Role = "Screen bomb",
            Levels = Levels(lv => new WeaponLevel { Damage = L(120, 300, lv), Duration = 1f }),
        };

        public static readonly WeaponDef SolarLance = new WeaponDef
        {
            Id = "solarLance", Name = "Solar Lance", Slot = WeaponSlot.Core, Kind = WeaponKind.SolarLance, Price = 1500,
            Description = "A huge beam straight ahead that burns through enemies and cancels bullets inside it.",
            Role = "Mega beam",
            Levels = Levels(lv => new WeaponLevel { Damage = L(250, 450, lv), Duration = L(3f, 4.5f, lv), Width = L(3f, 4.5f, lv) }),
        };

        public static readonly WeaponDef AegisField = new WeaponDef
        {
            Id = "aegisField", Name = "Aegis Field", Slot = WeaponSlot.Core, Kind = WeaponKind.AegisField, Price = 1200,
            Description = "A bubble that absorbs every hit, and slows enemy bullets that come near.",
            Role = "Shield, bullet slow",
            Levels = Levels(lv => new WeaponLevel { Duration = L(5f, 8f, lv), Range = 6f, Factor = 0.4f }),
        };

        public static readonly WeaponDef Wingmates = new WeaponDef
        {
            Id = "wingmates", Name = "Wingmates", Slot = WeaponSlot.Core, Kind = WeaponKind.Wingmates, Price = 1300,
            Description = "Drones that fly beside you and copy your wing weapons for a while.",
            Role = "Extra firepower",
            Levels = Levels(lv => new WeaponLevel { Count = lv >= 4 ? 3 : 2, Duration = L(10f, 15f, lv), Factor = L(0.4f, 0.6f, lv) }),
        };

        public static readonly WeaponDef[] WingWeapons = { PulseRepeater, ScatterCannon, LanceDriver, SunlineBeam, SeekerPods, HornetRack, ArcCoil, RicochetDisc };
        public static readonly WeaponDef[] CoreWeapons = { PulseNova, SolarLance, AegisField, Wingmates };
        public static readonly WeaponDef[] All =
        {
            PulseRepeater, ScatterCannon, LanceDriver, SunlineBeam, SeekerPods, HornetRack, ArcCoil, RicochetDisc,
            PulseNova, SolarLance, AegisField, Wingmates,
        };

        public static WeaponDef ById(string id)
        {
            foreach (var w in All)
                if (w.Id == id) return w;
            return null;
        }
    }
}
