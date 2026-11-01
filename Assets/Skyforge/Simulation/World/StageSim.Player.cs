namespace Skyforge.Simulation
{
    public sealed partial class StageSim
    {
        /// <summary>Speed multiplier while precision flying.</summary>
        public const float PrecisionFactor = 0.5f;
        /// <summary>Touch drag may move the ship up to this many times its normal top speed, so the
        /// ship keeps up with a quick finger without teleporting.</summary>
        public const float DragSpeedFactor = 3f;

        void MovePlayer(PlayerInput input)
        {
            var p = Player;
            float speed = p.MaxSpeed * (input.Precision ? PrecisionFactor : 1f);
            float maxStep = speed * Field.Dt;
            Vec2 delta;
            switch (input.Mode)
            {
                case MoveMode.Direction:
                    delta = input.Direction.ClampLength(1f) * maxStep;
                    break;
                case MoveMode.Target:
                    // Head for the cursor at full speed, stopping exactly on it.
                    delta = (input.Target - p.Pos).ClampLength(maxStep);
                    break;
                case MoveMode.Drag:
                    delta = input.Drag.ClampLength(maxStep * DragSpeedFactor);
                    break;
                default:
                    delta = Vec2.Zero;
                    break;
            }

            var before = p.Pos;
            p.Pos = ClampToField(p.Pos + delta);
            p.Velocity = (p.Pos - before) / Field.Dt;
        }

        public Vec2 ClampToField(Vec2 pos)
        {
            float minX = -HalfWidth + Field.ShipMarginX, maxX = HalfWidth - Field.ShipMarginX;
            float x = pos.X < minX ? minX : pos.X > maxX ? maxX : pos.X;
            float y = pos.Y < Field.ShipMinY ? Field.ShipMinY : pos.Y > Field.ShipMaxY ? Field.ShipMaxY : pos.Y;
            return new Vec2(x, y);
        }
    }
}
