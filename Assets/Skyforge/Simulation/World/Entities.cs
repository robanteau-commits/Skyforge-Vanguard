namespace Skyforge.Simulation
{
    /// <summary>The player's ship.</summary>
    public sealed class PlayerShip
    {
        public Vec2 Pos;
        /// <summary>Movement over the last step, per second (the view banks the ship with it).</summary>
        public Vec2 Velocity;
        public float MaxSpeed = 14f;
        /// <summary>Seconds until each wing may fire again.</summary>
        public float LeftCooldown, RightCooldown;

        /// <summary>Where a wing's projectiles start, relative to the ship's centre.</summary>
        public static Vec2 PodOffset(Side side) => new Vec2(side == Side.Left ? -1.0f : 1.0f, 0.6f);
    }

    public enum ShotVisual : byte { PulseBolt }

    /// <summary>A projectile fired by the player.</summary>
    public struct Shot
    {
        public Vec2 Pos;
        public Vec2 Vel;
        public float Damage;
        public float Radius;
        public Side Wing;
        public ShotVisual Visual;
    }

    public enum EnemyKind : byte
    {
        /// <summary>Unarmed target drone for the M0 test flight.</summary>
        PracticeDrone,
    }

    /// <summary>An enemy ship.</summary>
    public sealed class Enemy
    {
        /// <summary>Unique for the stage; views use it to track the enemy.</summary>
        public int Id;
        public EnemyKind Kind;
        public Vec2 Pos;
        public Vec2 Vel;
        public float Hp;
        public float MaxHp;
        public float Radius;
        public int Score;
        /// <summary>Seconds since it spawned.</summary>
        public float Age;
        /// <summary>Per-enemy random phase for its movement wobble.</summary>
        public float Phase;
        public float BaseX;
    }

    public enum SimEventType : byte
    {
        /// <summary>A wing fired a volley (Pos = muzzle).</summary>
        Fired,
        /// <summary>A shot hit an enemy without killing it.</summary>
        Hit,
        /// <summary>An enemy was destroyed.</summary>
        Destroyed,
    }

    /// <summary>Something that happened this step, for effects and sound. Gameplay never reads these.</summary>
    public struct SimEvent
    {
        public SimEventType Type;
        public Vec2 Pos;
        public Side Wing;
        public EnemyKind EnemyKind;
        public int EnemyId;
    }
}
