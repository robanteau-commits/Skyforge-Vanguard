namespace Skyforge.Simulation
{
    /// <summary>
    /// Every weapon in the game. Numbers come from docs/DESIGN.md §4; change them there first.
    /// </summary>
    public static class Weapons
    {
        /// <summary>Upgrade price to reach level 2, 3, 4, 5 (wing weapons).</summary>
        public static readonly int[] WingUpgradeCosts = { 300, 600, 1000, 1600 };
        /// <summary>Upgrade price to reach level 2, 3, 4, 5 (core specials).</summary>
        public static readonly int[] CoreUpgradeCosts = { 400, 800, 1300, 2000 };

        static WeaponLevel Pulse(int streams, float rate, float damage) => new WeaponLevel
        {
            Streams = streams,
            ShotsPerSecond = rate,
            Damage = damage,
            StreamGap = 0.28f,
            ProjectileSpeed = 34f,
            ProjectileRadius = 0.18f,
        };

        /// <summary>The starter wing gun: a fast stream of cyan bolts straight ahead.
        /// DPS per wing 20 → 25 → 31 → 38 → 47 (≈2.3× at level 5).</summary>
        public static readonly WeaponDef PulseRepeater = new WeaponDef
        {
            Id = "pulseRepeater",
            Name = "Pulse Repeater",
            Description = "A fast stream of plasma bolts straight ahead. Reliable against anything.",
            Slot = WeaponSlot.Wing,
            Kind = WeaponKind.PulseRepeater,
            Price = 0,
            Levels = new[]
            {
                Pulse(1, 10f, 2.0f),
                Pulse(2, 10f, 1.25f),
                Pulse(2, 11f, 1.4f),
                Pulse(3, 11f, 1.15f),
                Pulse(3, 12f, 1.3f),
            },
        };

        public static readonly WeaponDef[] All = { PulseRepeater };

        public static WeaponDef ById(string id)
        {
            foreach (var w in All)
                if (w.Id == id) return w;
            return null;
        }
    }
}
