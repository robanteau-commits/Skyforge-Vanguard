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

        // ------------------------------------------------------------------ wing weapons
        // Each level adds one projectile you can see (docs/DESIGN.md §4.2 "what each level adds");
        // damage per projectile is set so total damage per second still grows about 2.3x from Lv 1
        // to Lv 5. Level 5 adds a twist.

        /// <summary>Starter: bolt streams, one more per level. DPS per wing 20 → 46; Lv 5 adds a charged bolt every 5th volley.</summary>
        public static readonly WeaponDef PulseRepeater = new WeaponDef
        {
            Id = "pulseRepeater", Name = "Pulse Repeater", Slot = WeaponSlot.Wing, Kind = WeaponKind.PulseRepeater, Price = 0,
            Description = "A fast stream of plasma bolts straight ahead. Reliable against anything.",
            Role = "All-rounder",
            Levels = Levels(lv =>
            {
                float[] dps = { 20f, 25f, 31f, 38f, 46f };
                return new WeaponLevel
                {
                    Count = lv, ShotsPerSecond = 10f, Damage = dps[lv - 1] / (lv * 10f), StreamGap = 0.26f,
                    AngleStep = lv >= 4 ? 2.5f : 0f, ProjectileSpeed = 34f, ProjectileRadius = 0.18f,
                    SpecialEvery = lv == 5 ? 5 : 0, SpecialDamage = 6f, SpecialSize = 2f,
                };
            }),
        };

        public static readonly WeaponDef ScatterCannon = new WeaponDef
        {
            Id = "scatterCannon", Name = "Scatter Cannon", Slot = WeaponSlot.Wing, Kind = WeaponKind.ScatterCannon, Price = 600,
            Description = "A fan of pellets, one more per level. Shreds swarms up close; at level 5 pellets burst into shrapnel at the end of their range.",
            Role = "Swarms, close range",
            Levels = Levels(lv => new WeaponLevel
            {
                Count = lv + 2, Spread = L(15, 40, lv), ShotsPerSecond = L(4, 5, lv), Damage = L(1.8f, 2.4f, lv),
                ProjectileSpeed = 26f, ProjectileRadius = 0.16f, Life = 0.75f, Burst = lv == 5 ? 4 : 0,
            }),
        };

        public static readonly WeaponDef LanceDriver = new WeaponDef
        {
            Id = "lanceDriver", Name = "Lance Driver", Slot = WeaponSlot.Wing, Kind = WeaponKind.LanceDriver, Price = 900,
            Description = "Heavy laser spears that pierce every enemy in their path. Level 2 thickens them, then one more spear per level; at level 5 every third is a giant.",
            Role = "Lines of enemies, armour",
            Levels = Levels(lv =>
            {
                int[] count = { 1, 1, 2, 3, 3 };
                float[] dps = { 22f, 28f, 35f, 42f, 46f };
                float rate = L(2f, 3f, lv);
                return new WeaponLevel
                {
                    Count = count[lv - 1], ShotsPerSecond = rate, Damage = dps[lv - 1] / (count[lv - 1] * rate), Pierce = true,
                    StreamGap = 0.4f, ProjectileSpeed = 40f, ProjectileRadius = lv == 2 ? 0.33f : 0.22f,
                    SpecialEvery = lv == 5 ? 3 : 0, SpecialDamage = 2.5f, SpecialSize = 3f,
                };
            }),
        };

        public static readonly WeaponDef SunlineBeam = new WeaponDef
        {
            Id = "sunlineBeam", Name = "Sunline Beam", Slot = WeaponSlot.Wing, Kind = WeaponKind.SunlineBeam, Price = 1400,
            Description = "A continuous beam that gets stronger the longer it stays on one target. More and wider strands with each level; they burn through to a second enemy from level 4, and pulse at full power at level 5.",
            Role = "Bosses, gunships",
            Levels = Levels(lv =>
            {
                int[] strands = { 1, 1, 2, 2, 3 };
                float[] width = { 0.3f, 0.5f, 0.35f, 0.4f, 0.4f };
                return new WeaponLevel
                {
                    Count = strands[lv - 1], Damage = L(16, 30, lv), DamageMax = L(28, 60, lv), RampTime = 1.5f,
                    Width = width[lv - 1], StreamGap = 0.5f, PassThrough = lv >= 4,
                    PulseDamage = lv == 5 ? 25f : 0f, PulseRadius = 2.2f,
                };
            }),
        };

        public static readonly WeaponDef SeekerPods = new WeaponDef
        {
            Id = "seekerPods", Name = "Seeker Pods", Slot = WeaponSlot.Wing, Kind = WeaponKind.SeekerPods, Price = 800,
            Description = "Missiles that lock onto enemies and burst on impact, one more per level. At level 5 each blast scatters bomblets.",
            Role = "Enemies off to the sides",
            Levels = Levels(lv =>
            {
                float[] dps = { 21.7f, 27f, 33f, 40f, 50f };
                float rate = 1f / L(0.6f, 0.5f, lv);
                return new WeaponLevel
                {
                    Count = lv, ShotsPerSecond = rate, Damage = dps[lv - 1] / (lv * rate), Blast = L(0.8f, 1.2f, lv),
                    Spread = 12f * (lv - 1), ProjectileSpeed = 16f, ProjectileRadius = 0.25f, TurnRate = 220f, Life = 3f,
                    Burst = lv == 5 ? 3 : 0,
                };
            }),
        };

        public static readonly WeaponDef HornetRack = new WeaponDef
        {
            Id = "hornetRack", Name = "Hornet Rack", Slot = WeaponSlot.Wing, Kind = WeaponKind.HornetRack, Price = 1100,
            Description = "Salvos of micro-missiles spread across every target in sight, one more per level. At level 5 darts that miss circle back for another try.",
            Role = "Swarms, mines",
            Levels = Levels(lv =>
            {
                float[] dps = { 20f, 25f, 31f, 38f, 46f };
                float interval = L(1.2f, 0.9f, lv);
                return new WeaponLevel
                {
                    Count = lv + 3, ShotsPerSecond = 1f / interval, Damage = dps[lv - 1] * interval / (lv + 3),
                    Spread = 120f, ProjectileSpeed = 20f, ProjectileRadius = 0.2f,
                    TurnRate = lv == 5 ? 420f : 320f, Life = lv == 5 ? 4f : 2.5f,
                };
            }),
        };

        public static readonly WeaponDef ArcCoil = new WeaponDef
        {
            Id = "arcCoil", Name = "Arc Coil", Slot = WeaponSlot.Wing, Kind = WeaponKind.ArcCoil, Price = 1300,
            Description = "Lightning that strikes the nearest enemy and jumps to others, one more jump per level. At level 5 every strike forks.",
            Role = "Clusters; short range",
            Levels = Levels(lv => new WeaponLevel
            {
                ShotsPerSecond = L(3, 4, lv), Damage = L(6, 9, lv), Jumps = lv, Range = L(9, 11, lv), Fork = lv == 5 ? 2 : 1,
            }),
        };

        public static readonly WeaponDef RicochetDisc = new WeaponDef
        {
            Id = "ricochetDisc", Name = "Ricochet Disc", Slot = WeaponSlot.Wing, Kind = WeaponKind.RicochetDisc, Price = 1000,
            Description = "Spinning discs that bounce off the sides of the screen and cut through enemies, one more per level. At level 5 they split in two on their last hit.",
            Role = "Crowded screens",
            Levels = Levels(lv => new WeaponLevel
            {
                Count = lv, ShotsPerSecond = 1f / L(0.8f, 0.6f, lv), Damage = L(8f, 6f, lv), MaxHits = Li(4, 6, lv),
                Spread = 12f, ProjectileSpeed = 15f, ProjectileRadius = 0.35f, Life = 4f, Split = lv == 5,
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
