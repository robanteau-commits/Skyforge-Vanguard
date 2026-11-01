namespace Skyforge.Simulation
{
    public enum WeaponSlot : byte { Wing, Core }

    /// <summary>How a weapon behaves. Each kind has its own code in StageSim.Weapons / StageSim.Core.</summary>
    public enum WeaponKind : byte
    {
        // Wing weapons (docs/DESIGN.md §4.2)
        PulseRepeater,
        ScatterCannon,
        LanceDriver,
        SunlineBeam,
        SeekerPods,
        HornetRack,
        ArcCoil,
        RicochetDisc,
        // Core specials (docs/DESIGN.md §4.3)
        PulseNova,
        SolarLance,
        AegisField,
        Wingmates,
    }

    /// <summary>
    /// Stats for one upgrade level of a weapon. Each kind uses the fields that make sense for it;
    /// the rest stay 0.
    /// </summary>
    public sealed class WeaponLevel
    {
        /// <summary>Streams, pellets, missiles or discs per volley; Wingmates: drones.</summary>
        public int Count = 1;
        /// <summary>Volleys per second (beams: unused).</summary>
        public float ShotsPerSecond;
        /// <summary>Damage per hit; beams: damage per second at the start of the ramp; cores: damage (Nova) or per second (Lance).</summary>
        public float Damage;
        /// <summary>Sideways gap between parallel streams (Pulse Repeater).</summary>
        public float StreamGap;
        /// <summary>Fan angle in degrees across the whole volley.</summary>
        public float Spread;
        public float ProjectileSpeed;
        public float ProjectileRadius;
        /// <summary>Seconds a projectile lives (0 = until it leaves the screen).</summary>
        public float Life;
        /// <summary>Passes through enemies, hitting each once.</summary>
        public bool Pierce;
        /// <summary>Discs: hits before it's used up.</summary>
        public int MaxHits;
        /// <summary>Missiles: blast radius on impact (0 = single target).</summary>
        public float Blast;
        /// <summary>Homing projectiles: degrees per second they can turn.</summary>
        public float TurnRate;
        /// <summary>Arc Coil: extra targets the arc jumps to.</summary>
        public int Jumps;
        /// <summary>Arc Coil: reach of the first strike.</summary>
        public float Range;
        /// <summary>Beams: width.</summary>
        public float Width;
        /// <summary>Sunline Beam: damage per second after <see cref="RampTime"/> on one target.</summary>
        public float DamageMax;
        public float RampTime;
        /// <summary>Cores: seconds the effect lasts.</summary>
        public float Duration;
        /// <summary>Wingmates: share of your wing weapons' damage each drone deals.</summary>
        public float Factor;

        /// <summary>Single-target damage per second if every projectile hits (wing guns).</summary>
        public float Dps => Count * ShotsPerSecond * Damage;
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
        /// <summary>One line for the hangar list: what it's strong against.</summary>
        public string Role;
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
