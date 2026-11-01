namespace Skyforge.Simulation
{
    /// <summary>How this step's movement input should be read.</summary>
    public enum MoveMode : byte
    {
        /// <summary>No movement input.</summary>
        None,
        /// <summary><see cref="PlayerInput.Direction"/> is an analog direction (keys or stick), length ≤ 1.</summary>
        Direction,
        /// <summary>Fly towards <see cref="PlayerInput.Target"/> (mouse cursor) at up to full speed.</summary>
        Target,
        /// <summary>Move by <see cref="PlayerInput.Drag"/> (touch: the finger's movement this step).</summary>
        Drag,
    }

    /// <summary>One simulation step's worth of player input, in play-field units.</summary>
    public struct PlayerInput
    {
        public MoveMode Mode;
        public Vec2 Direction;
        public Vec2 Target;
        public Vec2 Drag;
        /// <summary>Precision flying: half speed.</summary>
        public bool Precision;
        /// <summary>Core button pressed this step.</summary>
        public bool Core;

        public static PlayerInput Idle => default;

        public static PlayerInput Move(float x, float y) =>
            new PlayerInput { Mode = MoveMode.Direction, Direction = new Vec2(x, y) };
        public static PlayerInput Towards(Vec2 target) =>
            new PlayerInput { Mode = MoveMode.Target, Target = target };
        public static PlayerInput DragBy(Vec2 delta) =>
            new PlayerInput { Mode = MoveMode.Drag, Drag = delta };
    }
}
