using UnityEngine;

namespace Skyforge.Game
{
    /// <summary>
    /// The dark sci-fi palette (docs/DESIGN.md §10.1). Player = cyan/white, enemies = red/magenta,
    /// enemy bullets = orange/pink, credits = gold, repairs = green. Nothing in the background may
    /// be as bright or saturated as enemy bullets.
    /// </summary>
    public static class Palette
    {
        // Screen
        public static readonly Color Clear = new Color(0.02f, 0.025f, 0.04f);
        /// <summary>Sea haze: the far (top) part of the sea fades into a dark blue-violet.</summary>
        public static readonly Color Fog = new Color(0.04f, 0.045f, 0.07f);
        public static readonly Color PanelBg = new Color(0.03f, 0.04f, 0.06f, 0.92f);

        // Player ship
        public static readonly Color Hull = new Color(0.21f, 0.23f, 0.27f);
        public static readonly Color HullLight = new Color(0.44f, 0.47f, 0.52f);
        public static readonly Color HullDark = new Color(0.07f, 0.08f, 0.1f);
        public static readonly Color Canopy = new Color(0.03f, 0.09f, 0.14f);
        public static readonly Color PlayerGlow = new Color(0.3f, 0.9f, 1f);
        public static readonly Color PlayerShot = new Color(0.45f, 0.95f, 1f);

        // Enemies
        public static readonly Color EnemyHull = new Color(0.14f, 0.1f, 0.13f);
        public static readonly Color EnemyPlate = new Color(0.28f, 0.2f, 0.25f);
        public static readonly Color EnemyGlow = new Color(1f, 0.12f, 0.28f);
        public static readonly Color EnemyBullet = new Color(1f, 0.5f, 0.2f);

        // Effects
        public static readonly Color Explosion = new Color(1f, 0.55f, 0.18f);
        public static readonly Color Spark = new Color(1f, 0.85f, 0.6f);
        public static readonly Color Credit = new Color(1f, 0.78f, 0.25f);

        // Stage 1-1 backdrop: B · Open Ocean, darker (Rob's pick). Kept darker and less saturated
        // than anything the player has to read; no point lights.
        public static readonly Color Sea = new Color(0.018f, 0.045f, 0.07f);
        public static readonly Color IsletSand = new Color(0.2f, 0.17f, 0.13f);
        public static readonly Color IsletRock = new Color(0.12f, 0.115f, 0.13f);
        public static readonly Color IsletGreen = new Color(0.07f, 0.1f, 0.075f);

        // HUD
        public static readonly Color HudText = new Color(0.82f, 0.9f, 0.95f);
        public static readonly Color HudDim = new Color(0.45f, 0.53f, 0.6f);
        public static readonly Color HudAccent = new Color(0.4f, 0.9f, 1f);
    }
}
