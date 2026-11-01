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

    /// <summary>Every enemy type's stats.</summary>
    public static class EnemyDefs
    {
        static EnemyDef D(EnemyKind k, string name, float hp, float radius, int score, int credits, float contact, float repair = 0f) =>
            new EnemyDef { Kind = k, Name = name, Hp = hp, Radius = radius, Score = score, Credits = credits, ContactDamage = contact, RepairChance = repair };

        public static readonly EnemyDef PracticeDrone = D(EnemyKind.PracticeDrone, "Practice Drone", 12f, 0.75f, 50, 0, 10f);
        public static readonly EnemyDef Gnat = D(EnemyKind.Gnat, "Gnat", 6f, 0.55f, 50, 2, 15f);
        public static readonly EnemyDef Needle = D(EnemyKind.Needle, "Needle", 20f, 0.6f, 120, 5, 20f, 0.04f);
        public static readonly EnemyDef RamDrone = D(EnemyKind.RamDrone, "Ram Drone", 14f, 0.6f, 100, 4, 25f);
        public static readonly EnemyDef SpineMine = D(EnemyKind.SpineMine, "Spine Mine", 10f, 0.6f, 60, 2, 10f);
        public static readonly EnemyDef Warden = D(EnemyKind.Warden, "Warden", 160f, 1.5f, 600, 20, 30f, 1f);
        public static readonly EnemyDef Bastion = D(EnemyKind.Bastion, "Bastion", 90f, 0.9f, 400, 12, 20f, 0.25f);

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
                default: return PracticeDrone;
            }
        }
    }
}
