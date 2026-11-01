namespace Skyforge.Simulation
{
    public enum WeaponSlot : byte { Wing, Core }

    /// <summary>How a wing weapon behaves. Each kind has its own firing code in StageSim.Weapons.</summary>
    public enum WeaponKind : byte
    {
        PulseRepeater,
        // Planned (docs/DESIGN.md §4.2): ScatterCannon, LanceDriver, SunlineBeam, SeekerPods,
        // HornetRack, ArcCoil, RicochetDisc.
    }

    /// <summary>Stats for one upgrade level of a weapon.</summary>
    public sealed class WeaponLevel
    {
        /// <summary>Parallel streams fired side by side.</summary>
        public int Streams;
        public float ShotsPerSecond;
        public float Damage;
        /// <summary>Sideways gap between neighbouring streams.</summary>
        public float StreamGap;
        public float ProjectileSpeed;
        public float ProjectileRadius;

        /// <summary>Single-target damage per second if every stream hits.</summary>
        public float Dps => Streams * ShotsPerSecond * Damage;
    }

    /// <summary>
    /// A weapon's static definition. <see cref="Id"/> is internal and stable (it goes into save
    /// files); <see cref="Name"/> is the display name and may change.
    /// </summary>
    public sealed class WeaponDef
    {
        public string Id;
        public string Name;
        public string Description;
        public WeaponSlot Slot;
        public WeaponKind Kind;
        /// <summary>Shop price in credits; 0 = owned from the start.</summary>
        public int Price;
        /// <summary>Levels 1..5 are entries 0..4.</summary>
        public WeaponLevel[] Levels;

        public int MaxLevel => Levels.Length;

        /// <summary>Stats for <paramref name="level"/> (1-based, clamped to the valid range).</summary>
        public WeaponLevel At(int level)
        {
            int i = level < 1 ? 0 : level > Levels.Length ? Levels.Length - 1 : level - 1;
            return Levels[i];
        }
    }
}
