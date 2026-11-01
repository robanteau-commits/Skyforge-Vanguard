namespace Skyforge.Simulation
{
    public enum EnemyKind : byte
    {
        /// <summary>Unarmed target drone for the practice flight.</summary>
        PracticeDrone,
        /// <summary>Swarm fighter: flies in formations, rare single aimed shots.</summary>
        Gnat,
        /// <summary>Diving fighter: enters, hovers, fires a 3-shot burst, dives off.</summary>
        Needle,
        /// <summary>Kamikaze: flashes, locks on and charges.</summary>
        RamDrone,
        /// <summary>Drifting mine: bursts into a ring of bullets when shot or approached.</summary>
        SpineMine,
        /// <summary>Armoured gunship: stays a while firing rotating rings and aimed fans.</summary>
        Warden,
        /// <summary>Turret on a drifting asteroid: tracks the player and fires aimed bursts.</summary>
        Bastion,
        /// <summary>Mini-boss: crosses the screen slowly firing spread bursts; its two bays launch Gnats.</summary>
        BroodCarrier,
        /// <summary>A Brood Carrier's launch bay (a part: moves with the carrier).</summary>
        BroodBay,
        /// <summary>World 1 boss, the controller and hull: it can't be hit, its parts can.</summary>
        Tidebreaker,
        /// <summary>Tidebreaker flank cannon: sweeping aimed streams (phase 1).</summary>
        TideCannon,
        /// <summary>Tidebreaker missile bay: launches slow homing missiles.</summary>
        TideMissileBay,
        /// <summary>Tidebreaker core eye: shielded until both cannons fall; spirals, then ring barrages.</summary>
        TideEye,
        /// <summary>A slow homing missile from the missile bay; can be shot down.</summary>
        TideMissile,
    }

    /// <summary>
    /// An enemy type's stats (docs/DESIGN.md §5). Behaviour lives in StageSim.Enemies.cs.
    /// </summary>
    public sealed class EnemyDef
    {
        public EnemyKind Kind;
        public string Name;
        public float Hp;
        /// <summary>Hit radius (shots and contact).</summary>
        public float Radius;
        public int Score;
        public int Credits;
        /// <summary>Damage to the player on contact; the enemy takes <see cref="StageSim.ContactSelfDamage"/>.</summary>
        public float ContactDamage;
        /// <summary>Chance (0..1) to drop a repair orb when destroyed.</summary>
        public float RepairChance;
    }

    /// <summary>Every enemy type's stats. Credits were tripled from the first numbers in v0.5.0 (Rob's
    /// call) so a good run of stage 1-1 pays roughly the design's target.</summary>
    public static class EnemyDefs
    {
        static EnemyDef D(EnemyKind k, string name, float hp, float radius, int score, int credits, float contact, float repair = 0f) =>
            new EnemyDef { Kind = k, Name = name, Hp = hp, Radius = radius, Score = score, Credits = credits, ContactDamage = contact, RepairChance = repair };

        public static readonly EnemyDef PracticeDrone = D(EnemyKind.PracticeDrone, "Practice Drone", 12f, 0.75f, 50, 0, 10f);
        public static readonly EnemyDef Gnat = D(EnemyKind.Gnat, "Gnat", 6f, 0.55f, 50, 6, 15f);
        public static readonly EnemyDef Needle = D(EnemyKind.Needle, "Needle", 20f, 0.6f, 120, 15, 20f, 0.04f);
        public static readonly EnemyDef RamDrone = D(EnemyKind.RamDrone, "Ram Drone", 14f, 0.6f, 100, 12, 25f);
        public static readonly EnemyDef SpineMine = D(EnemyKind.SpineMine, "Spine Mine", 10f, 0.6f, 60, 6, 10f);
        public static readonly EnemyDef Warden = D(EnemyKind.Warden, "Warden", 160f, 1.5f, 600, 60, 30f, 1f);
        public static readonly EnemyDef Bastion = D(EnemyKind.Bastion, "Bastion", 90f, 0.9f, 400, 36, 20f, 0.25f);

        public static readonly EnemyDef BroodCarrier = D(EnemyKind.BroodCarrier, "Brood Carrier", 700f, 1.7f, 2500, 240, 30f, 1f);
        public static readonly EnemyDef BroodBay = D(EnemyKind.BroodBay, "Launch Bay", 150f, 0.8f, 400, 30, 20f);
        public static readonly EnemyDef Tidebreaker = D(EnemyKind.Tidebreaker, "The Tidebreaker", 1f, 0f, 5000, 400, 0f, 1f);
        public static readonly EnemyDef TideCannon = D(EnemyKind.TideCannon, "Flank Cannon", 400f, 1.1f, 800, 45, 30f, 1f);
        public static readonly EnemyDef TideMissileBay = D(EnemyKind.TideMissileBay, "Missile Bay", 500f, 1.2f, 1000, 60, 30f, 1f);
        public static readonly EnemyDef TideEye = D(EnemyKind.TideEye, "Core Eye", 1500f, 1.4f, 2000, 90, 30f);
        public static readonly EnemyDef TideMissile = D(EnemyKind.TideMissile, "Missile", 8f, 0.45f, 30, 0, 15f);

        /// <summary>The big ones: a heavier core charge, a bigger blast and a screen shake.</summary>
        public static bool IsBig(EnemyKind k) => k == EnemyKind.Warden || k == EnemyKind.Bastion || k == EnemyKind.BroodCarrier ||
            k == EnemyKind.BroodBay || k == EnemyKind.Tidebreaker || k == EnemyKind.TideCannon || k == EnemyKind.TideMissileBay || k == EnemyKind.TideEye;

        public static EnemyDef Of(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Gnat: return Gnat;
                case EnemyKind.Needle: return Needle;
                case EnemyKind.RamDrone: return RamDrone;
                case EnemyKind.SpineMine: return SpineMine;
                case EnemyKind.Warden: return Warden;
                case EnemyKind.Bastion: return Bastion;
                case EnemyKind.BroodCarrier: return BroodCarrier;
                case EnemyKind.BroodBay: return BroodBay;
                case EnemyKind.Tidebreaker: return Tidebreaker;
                case EnemyKind.TideCannon: return TideCannon;
                case EnemyKind.TideMissileBay: return TideMissileBay;
                case EnemyKind.TideEye: return TideEye;
                case EnemyKind.TideMissile: return TideMissile;
                default: return PracticeDrone;
            }
        }
    }
}
