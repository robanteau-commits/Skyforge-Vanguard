namespace Skyforge.Simulation
{
    /// <summary>
    /// The logical play field: 18 × 32 units (9:16 portrait), centred on the origin.
    /// X runs −9..9 (left..right), Y runs −16..16 (bottom..top of the screen).
    /// </summary>
    public static class Field
    {
        public const float Width = 18f;
        public const float Height = 32f;
        public const float HalfWidth = Width / 2f;
        public const float HalfHeight = Height / 2f;

        /// <summary>Simulation rate. Rendering interpolates between steps.</summary>
        public const int StepsPerSecond = 60;
        public const float Dt = 1f / StepsPerSecond;

        /// <summary>How far the ship's centre stays from the left/right edges (half its wingspan).</summary>
        public const float ShipMarginX = 1.4f;
        /// <summary>The ship's centre stays between these Y values (keeps the nose and tail on screen).</summary>
        public const float ShipMinY = -14.5f;
        public const float ShipMaxY = 13.5f;

        /// <summary>How fast the world drifts down the screen: mines and asteroid turrets move with it.</summary>
        public const float ScrollSpeed = 3.5f;

        /// <summary>Anything this far past an edge is gone for good.</summary>
        public const float CullMargin = 3f;

        public static bool IsOutside(Vec2 p, float margin = CullMargin) =>
            p.X < -HalfWidth - margin || p.X > HalfWidth + margin ||
            p.Y < -HalfHeight - margin || p.Y > HalfHeight + margin;
    }
}
