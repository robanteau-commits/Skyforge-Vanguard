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
        /// <summary>Plain black space.</summary>
        public static readonly Color Clear = Color.black;
        public static readonly Color PanelBg = new Color(0.03f, 0.04f, 0.06f, 0.92f);

        // Player ship: "faceted alloy" (Rob's pick, the Kestrel concept). Light matte alloy hull,
        // brighter panels, dark graphite nacelles, teal canopy, small peach wingtip accents.
        public static readonly Color Hull = new Color(0.66f, 0.68f, 0.71f);
        public static readonly Color HullLight = new Color(0.84f, 0.85f, 0.86f);
        public static readonly Color HullDark = new Color(0.12f, 0.14f, 0.17f);
        public static readonly Color Canopy = new Color(0.05f, 0.36f, 0.4f);
        public static readonly Color Accent = new Color(0.86f, 0.6f, 0.47f);
        public static readonly Color PlayerGlow = new Color(0.3f, 0.9f, 1f);
        public static readonly Color PlayerShot = new Color(0.45f, 0.95f, 1f);

        // Weapon effects (designed on the "Weapon effects" render page): each wing weapon has its own
        // hue, all cool (cyan, sky, ice, aqua, mint, violet) and well clear of enemy orange/red,
        // credit gold and repair green.
        public static readonly Color PulseCore = new Color(0.85f, 1f, 1f);
        public static readonly Color Pulse = new Color(0.25f, 0.9f, 1f);
        public static readonly Color ScatterCore = new Color(0.85f, 0.95f, 1f);
        public static readonly Color Scatter = new Color(0.35f, 0.65f, 1f);
        public static readonly Color LanceCore = Color.white;
        public static readonly Color Lance = new Color(0.65f, 0.85f, 1f);
        public static readonly Color BeamCore = Color.white;
        public static readonly Color Beam = new Color(0.3f, 0.95f, 1f);
        public static readonly Color BeamOuter = new Color(0.1f, 0.5f, 0.75f);
        public static readonly Color Exhaust = new Color(0.75f, 0.95f, 1f);
        public static readonly Color Smoke = new Color(0.46f, 0.48f, 0.53f);
        public static readonly Color Hornet = new Color(0.25f, 1f, 0.85f);
        public static readonly Color HornetTrail = new Color(0.15f, 0.7f, 0.62f);
        public static readonly Color ArcCore = new Color(0.95f, 0.9f, 1f);
        public static readonly Color Arc = new Color(0.55f, 0.45f, 1f);
        public static readonly Color Disc = new Color(0.3f, 1f, 1f);
        public static readonly Color DiscPlate = new Color(0.1f, 0.23f, 0.27f);

        // Enemies
        public static readonly Color EnemyHull = new Color(0.14f, 0.1f, 0.13f);
        public static readonly Color EnemyPlate = new Color(0.28f, 0.2f, 0.25f);
        public static readonly Color EnemyGlow = new Color(1f, 0.12f, 0.28f);
        public static readonly Color EnemyBullet = new Color(1f, 0.45f, 0.3f);
        /// <summary>Dark maroon rim behind enemy bullets, so they read over explosions.</summary>
        public static readonly Color BulletRim = new Color(0.3f, 0.03f, 0.08f);
        /// <summary>The wind-up glow before an enemy fires.</summary>
        public static readonly Color Telegraph = new Color(1f, 0.35f, 0.45f);

        // Effects
        public static readonly Color Explosion = new Color(1f, 0.55f, 0.18f);
        public static readonly Color Spark = new Color(1f, 0.85f, 0.6f);
        public static readonly Color Credit = new Color(1f, 0.78f, 0.25f);
        public static readonly Color Repair = new Color(0.35f, 1f, 0.5f);
        public static readonly Color Asteroid = new Color(0.2f, 0.185f, 0.18f);
        public static readonly Color AsteroidDark = new Color(0.12f, 0.11f, 0.11f);

        // Background: black space with small stars (see Backdrop).
        public static readonly Color Star = new Color(0.78f, 0.84f, 1f);

        // HUD
        public static readonly Color HudText = new Color(0.82f, 0.9f, 0.95f);
        public static readonly Color HudDim = new Color(0.45f, 0.53f, 0.6f);
        public static readonly Color HudAccent = new Color(0.4f, 0.9f, 1f);
        public static readonly Color HudHull = new Color(0.86f, 0.9f, 0.95f);
        public static readonly Color HudDanger = new Color(1f, 0.35f, 0.3f);
        public static readonly Color HudGold = new Color(1f, 0.8f, 0.35f);
        public static readonly Color HudBarBg = new Color(1f, 1f, 1f, 0.1f);
    }
}
