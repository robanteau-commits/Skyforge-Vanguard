namespace Skyforge.Simulation
{
    /// <summary>The airframe's upgradeable stats (docs/DESIGN.md §3.2).</summary>
    public enum AirframeStat : byte { Hull, Shield, Regen, Thrusters, Magnet, Capacitor }

    /// <summary>
    /// Airframe upgrades: five levels per stat, bought with credits. Level 0 is the stock ship.
    /// <see cref="Apply"/> sets a ship's stats from a set of levels.
    /// </summary>
    public static class Airframe
    {
        public const int StatCount = 6;
        public const int MaxLevel = 5;

        /// <summary>Price to reach level 1, 2, 3, 4, 5 of any stat.</summary>
        public static readonly int[] Costs = { 250, 500, 900, 1500, 2400 };

        public static readonly string[] Names = { "Hull", "Shield", "Shield regen", "Thrusters", "Magnet", "Capacitor" };

        public static readonly string[] Descriptions =
        {
            "Hull points: what you have left once the shield is down.",
            "Shield points: soak hits first and recharge on their own.",
            "How soon and how fast the shield recharges after a hit.",
            "Top speed.",
            "How far away gold shards and repair orbs fly to you.",
            "How fast the core special charges.",
        };

        public static float Hull(int lv) => 100f + 20f * lv;
        public static float Shield(int lv) => 40f + 12f * lv;
        public static float RegenDelay(int lv) => 4f - 0.4f * lv;
        public static float RegenRate(int lv) => 4f + 1.2f * lv;
        public static float Speed(int lv) => 14f + 1.5f * lv;
        public static float Magnet(int lv) => 4f + 1f * lv;
        public static float Capacitor(int lv) => 1f + 0.1f * lv;

        /// <summary>Set the ship's stats (and fill hull and shield) for these upgrade levels; null = stock.</summary>
        public static void Apply(PlayerShip p, int[] levels)
        {
            int L(AirframeStat s) => levels == null || levels.Length <= (int)s ? 0 : Clamp(levels[(int)s]);
            p.MaxHull = p.Hull = Hull(L(AirframeStat.Hull));
            p.MaxShield = p.Shield = Shield(L(AirframeStat.Shield));
            p.ShieldRegenDelay = RegenDelay(L(AirframeStat.Regen));
            p.ShieldRegenRate = RegenRate(L(AirframeStat.Regen));
            p.MaxSpeed = Speed(L(AirframeStat.Thrusters));
            p.MagnetRange = Magnet(L(AirframeStat.Magnet));
            p.Capacitor = Capacitor(L(AirframeStat.Capacitor));
        }

        static int Clamp(int lv) => lv < 0 ? 0 : lv > MaxLevel ? MaxLevel : lv;

        /// <summary>The stat's value at a level, in words ("120 hull").</summary>
        public static string Value(AirframeStat s, int lv)
        {
            switch (s)
            {
                case AirframeStat.Hull: return $"{Hull(lv):0} hull";
                case AirframeStat.Shield: return $"{Shield(lv):0} shield";
                case AirframeStat.Regen: return $"{RegenRate(lv):0.#}/s after {RegenDelay(lv):0.#} s";
                case AirframeStat.Thrusters: return $"speed {Speed(lv):0.#}";
                case AirframeStat.Magnet: return $"range {Magnet(lv):0}";
                default: return $"{Capacitor(lv) * 100f:0}% charge rate";
            }
        }
    }
}
