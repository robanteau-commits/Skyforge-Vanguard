namespace Skyforge.Simulation
{
    /// <summary>
    /// The logical play field, centred on the origin: 18 × 32 units (9:16 portrait) on phones,
    /// 24 × 32 (3:4) on PC and web (Rob's call, v0.3.2). The width is per stage
    /// (<see cref="StageSetup.FieldWidth"/>, <see cref="StageSim.HalfWidth"/>); these constants are
    /// the portrait defaults. Y runs −16..16 (bottom..top of the screen) everywhere.
    /// </summary>
    public static class Field
    {
        /// <summary>Portrait width (phones). Stage waves are authored for this width and spread out on wider fields.</summary>
        public const float Width = 18f;
        /// <summary>Wide width (PC and web): 3:4.</summary>
        public const float WideWidth = 24f;
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

        public static bool IsOutside(Vec2 p, float margin = CullMargin, float halfWidth = HalfWidth) =>
            p.X < -halfWidth - margin || p.X > halfWidth + margin ||
            p.Y < -HalfHeight - margin || p.Y > HalfHeight + margin;
    }
}
