namespace Skyforge.Simulation
{
    /// <summary>The player's ship (docs/DESIGN.md §3.2–3.3).</summary>
    public sealed class PlayerShip
    {
        public Vec2 Pos;
        /// <summary>Movement over the last step, per second (the view banks the ship with it).</summary>
        public Vec2 Velocity;
        public float MaxSpeed = 14f;
        /// <summary>Seconds until each wing may fire again.</summary>
        public float LeftCooldown, RightCooldown;

        public bool Alive = true;
        public float Hull = 100f, MaxHull = 100f;
        public float Shield = 40f, MaxShield = 40f;
        /// <summary>Shield regeneration per second, after <see cref="ShieldRegenDelay"/> without a hit.</summary>
        public float ShieldRegenRate = 4f;
        public float ShieldRegenDelay = 4f;
        /// <summary>Seconds left before the shield starts regenerating.</summary>
        public float ShieldRegenWait;
        /// <summary>Seconds of invulnerability left after a hit.</summary>
        public float Invulnerable;
        /// <summary>Pickups inside this distance fly to the ship.</summary>
        public float MagnetRange = 2.5f;

        /// <summary>Volleys each wing has fired (every Nth can be special: charged bolts, giant spears).</summary>
        public readonly int[] VolleyCount = new int[2];

        // Sunline Beam, per wing (index 0 = left, 1 = right), with up to 3 strands each.
        public const int MaxStrands = 3;
        /// <summary>The beam is firing from this wing.</summary>
        public readonly bool[] BeamOn = new bool[2];
        /// <summary>Strands this wing's beam has (its level).</summary>
        public readonly int[] BeamStrands = new int[2];
        /// <summary>Strand <c>wing * MaxStrands + i</c>: its X offset from the muzzle and where it ends (field Y).</summary>
        public readonly float[] BeamStrandX = new float[2 * MaxStrands];
        public readonly float[] BeamEndY = new float[2 * MaxStrands];
        /// <summary>Seconds until the next full-power pulse (level 5).</summary>
        public readonly float[] BeamPulseTimer = new float[2];
        /// <summary>Enemy the beam is on (0 = none) and how long it's been on it (the damage ramps up).</summary>
        public readonly int[] BeamTarget = new int[2];
        public readonly float[] BeamTime = new float[2];

        // Core special.
        /// <summary>0..<see cref="CoreChargeMax"/>; the core can fire when full.</summary>
        public float CoreCharge;
        /// <summary>Charge rate multiplier (the Capacitor airframe upgrade).</summary>
        public float Capacitor = 1f;
        /// <summary>A lasting core effect is running (Solar Lance, Aegis Field, Wingmates).</summary>
        public bool CoreActive;
        /// <summary>Seconds left on the running core effect.</summary>
        public float CoreTimer;
        /// <summary>Wingmates: how many drones are flying, and each one's fire cooldowns (left/right weapon).</summary>
        public int WingmateCount;
        public readonly float[] WingmateCooldown = new float[3];

        public const float CoreChargeMax = 100f;
        public bool CoreReady => CoreCharge >= CoreChargeMax;

        /// <summary>Wingmate drone <paramref name="i"/>'s position relative to the ship.</summary>
        public static Vec2 WingmateOffset(int i) =>
            i == 0 ? new Vec2(-2.6f, -0.8f) : i == 1 ? new Vec2(2.6f, -0.8f) : new Vec2(0f, -2.4f);

        /// <summary>The hitbox: a small circle around the core, much smaller than the model.</summary>
        public const float HitRadius = 0.35f;
        /// <summary>Radius used for ramming enemies (the airframe, not just the core).</summary>
        public const float BodyRadius = 0.7f;
        /// <summary>Invulnerability after any hit.</summary>
        public const float HitInvulnerability = 0.8f;

        /// <summary>Where a wing's projectiles start, relative to the ship's centre.</summary>
        public static Vec2 PodOffset(Side side) => new Vec2(side == Side.Left ? -1.0f : 1.0f, 0.6f);
    }

    public enum ShotVisual : byte { PulseBolt, Pellet, LanceBolt, Missile, MicroMissile, Disc, ChargedBolt, Shrapnel, Bomblet }

    /// <summary>A projectile fired by the player (or a wingmate).</summary>
    public struct Shot
    {
        public Vec2 Pos;
        public Vec2 Vel;
        public float Damage;
        public float Radius;
        public Side Wing;
        public ShotVisual Visual;
        /// <summary>Seconds left to live (0 = until it leaves the screen).</summary>
        public float Life;
        /// <summary>Passes through enemies, hitting each once.</summary>
        public bool Pierce;
        /// <summary>Discs: hits left before it's used up (0 = not a disc).</summary>
        public int HitsLeft;
        /// <summary>The enemy it last hit (piercing shots and discs skip it).</summary>
        public int LastHit;
        /// <summary>Blast radius on impact (0 = single target).</summary>
        public float Blast;
        /// <summary>Homing: degrees per second it can turn (0 = flies straight), and its target.</summary>
        public float TurnRate;
        public int TargetId;
        /// <summary>Bounces off the sides of the screen.</summary>
        public bool Bounce;
        /// <summary>Scatter pellets: shards released when it reaches the end of its range. Seekers: bomblets per blast.</summary>
        public int Burst;
        /// <summary>Bomblets: explode (with <see cref="Blast"/>) when their life runs out.</summary>
        public bool BlastOnExpire;
        /// <summary>Discs: split in two on their last hit.</summary>
        public bool Split;
    }

    public enum EnemyShotVisual : byte
    {
        /// <summary>Round glowing orb.</summary>
        Orb,
        /// <summary>Elongated dart, drawn along its velocity.</summary>
        Dart,
    }

    /// <summary>A projectile fired by an enemy.</summary>
    public struct EnemyShot
    {
        public Vec2 Pos;
        public Vec2 Vel;
        public float Radius;
        public float Damage;
        public EnemyShotVisual Visual;
    }

    /// <summary>An enemy ship. Behaviour fields are interpreted per kind (StageSim.Enemies.cs).</summary>
    public sealed class Enemy
    {
        /// <summary>Unique for the stage; views use it to track the enemy.</summary>
        public int Id;
        public EnemyKind Kind;
        public EnemyDef Def;
        public Vec2 Pos;
        public Vec2 Vel;
        public float Hp;
        public float MaxHp;
        public float Radius;
        public int Score;
        /// <summary>Seconds since it spawned.</summary>
        public float Age;
        /// <summary>Per-enemy random phase (wobbles, twinkles).</summary>
        public float Phase;
        public float BaseX;
        /// <summary>Where it's pointing, in degrees clockwise from up the screen (180 = down).</summary>
        public float Facing = 180f;
        /// <summary>0..1: an attack is winding up (the view shows a muzzle glow).</summary>
        public float Telegraph;

        // Behaviour state.
        public int Mode;
        public float Timer;
        public int Counter;
        public GnatPath Path;
        public float StartY;
        public float Param;
        public Vec2 Target;
        /// <summary>Gnats: seconds after spawning at which it fires its one shot (0 = never).</summary>
        public float ShotAt;
        /// <summary>Counts toward the stage's kill percentage (stage spawns only).</summary>
        public bool Counted;
        /// <summary>Destroyed this step; removed at the end of the step.</summary>
        public bool Dead;
    }

    public enum PickupKind : byte
    {
        /// <summary>A gold shard worth <see cref="Pickup.Value"/> credits.</summary>
        Credit,
        /// <summary>A green orb that repairs <see cref="Pickup.Value"/> hull.</summary>
        Repair,
    }

    /// <summary>Something dropped for the player to collect.</summary>
    public struct Pickup
    {
        public int Id;
        public PickupKind Kind;
        public Vec2 Pos;
        public Vec2 Vel;
        public int Value;
        /// <summary>Caught by the magnet: flies to the ship from now on.</summary>
        public bool Homing;
    }

    public enum StageStatus : byte { Playing, Cleared, Failed }

    public enum SimEventType : byte
    {
        /// <summary>A wing fired a volley (Pos = muzzle).</summary>
        Fired,
        /// <summary>A player shot hit an enemy without killing it.</summary>
        Hit,
        /// <summary>An enemy was destroyed by the player (Value = score awarded).</summary>
        Destroyed,
        /// <summary>An enemy fired (Pos = muzzle).</summary>
        EnemyFired,
        /// <summary>A spine mine burst into a ring.</summary>
        MineBurst,
        /// <summary>The shield absorbed a hit (Value = damage absorbed).</summary>
        ShieldHit,
        /// <summary>The hull took damage (Value = damage).</summary>
        HullHit,
        /// <summary>The player's ship was destroyed.</summary>
        PlayerDestroyed,
        /// <summary>A pickup was collected (EnemyId = pickup id, Value = its value).</summary>
        Collected,
        /// <summary>A Bastion turret appeared (the view adds its asteroid).</summary>
        BastionSpawned,
        /// <summary>The stage was cleared.</summary>
        StageCleared,
        /// <summary>An Arc Coil bolt from Pos to To.</summary>
        Arc,
        /// <summary>The core special fired (EnemyId = (int)WeaponKind of the core).</summary>
        CoreActivated,
        /// <summary>A lasting core effect ran out.</summary>
        CoreEnded,
        /// <summary>The Aegis Field absorbed a hit.</summary>
        AegisBlock,
        /// <summary>A Sunline Beam at full power pulsed (Pos = where, Value = radius).</summary>
        BeamPulse,
        /// <summary>A small blast went off (Pos = where, Value = radius): missiles, bomblets.</summary>
        Blast,
    }

    /// <summary>Something that happened this step, for effects and sound. Gameplay never reads these.</summary>
    public struct SimEvent
    {
        public SimEventType Type;
        public Vec2 Pos;
        public Side Wing;
        public EnemyKind EnemyKind;
        public int EnemyId;
        public float Value;
        public PickupKind PickupKind;
        /// <summary>Second point (Arc: where the bolt ends).</summary>
        public Vec2 To;
    }
}
