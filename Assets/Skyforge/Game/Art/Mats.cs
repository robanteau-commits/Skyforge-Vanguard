using System.Collections.Generic;
using UnityEngine;

namespace Skyforge.Game
{
    /// <summary>
    /// Shared materials, created once from URP's Lit and Unlit shaders (both are in Graphics →
    /// Always Included Shaders, so Shader.Find works in player builds). Glow materials use HDR
    /// colours above 1 so the bloom picks them up. No transparency anywhere: effects fade by
    /// shrinking, which keeps every shader variant opaque.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        // Play mode starts without a domain reload, so statics survive between sessions while the
        // materials they point at are destroyed. Start each session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => Cache.Clear();

        static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        static Shader UnlitShader => Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

        /// <summary>Flat-coloured lit surface.</summary>
        public static Material Lit(string key, Color color, float metallic = 0.3f, float smoothness = 0.35f)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(LitShader) { name = key, enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            Cache[key] = m;
            return m;
        }

        /// <summary>Unlit emissive colour; <paramref name="intensity"/> above 1 blooms.</summary>
        public static Material Glow(string key, Color color, float intensity)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(UnlitShader) { name = key, enableInstancing = true };
            var c = new Color(color.r * intensity, color.g * intensity, color.b * intensity, 1f);
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            Cache[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ the game's materials

        // Matte, faceted alloy: low metallic so the facets read from their lighting, not reflections.
        public static Material Hull => Lit("hull", Palette.Hull, 0.15f, 0.35f);
        public static Material HullLight => Lit("hullLight", Palette.HullLight, 0.1f, 0.4f);
        public static Material HullDark => Lit("hullDark", Palette.HullDark, 0.3f, 0.35f);
        public static Material Canopy => Lit("canopy", Palette.Canopy, 0.2f, 0.9f);
        public static Material Accent => Lit("accent", Palette.Accent, 0f, 0.3f);
        public static Material PlayerGlow => Glow("playerGlow", Palette.PlayerGlow, 3f);
        public static Material PlayerGlowSoft => Glow("playerGlowSoft", Palette.PlayerGlow, 1.4f);
        public static Material PlayerShot => Glow("playerShot", Palette.PlayerShot, 4f);

        // Weapon effects: a bright core, a coloured body, a dimmer trail per weapon.
        public static Material PulseCore => Glow("pulseCore", Palette.PulseCore, 4.5f);
        public static Material Pulse => Glow("pulse", Palette.Pulse, 2.6f);
        public static Material PulseTrail => Glow("pulseTrail", Palette.Pulse, 1.2f);
        public static Material ScatterCore => Glow("scatterCore", Palette.ScatterCore, 4f);
        public static Material Scatter => Glow("scatter", Palette.Scatter, 2.6f);
        public static Material ScatterTrail => Glow("scatterTrail", Palette.Scatter, 1.1f);
        public static Material LanceCore => Glow("lanceCore", Palette.LanceCore, 5f);
        public static Material Lance => Glow("lance", Palette.Lance, 2.4f);
        public static Material LanceTrail => Glow("lanceTrail", Palette.Lance, 1.3f);
        public static Material BeamCore => Glow("beamCore", Palette.BeamCore, 4.5f);
        public static Material Beam => Glow("beam", Palette.Beam, 2.6f);
        public static Material BeamOuter => Glow("beamOuter", Palette.BeamOuter, 1.4f);
        public static Material Exhaust => Glow("exhaust", Palette.Exhaust, 4f);
        public static Material Smoke => Lit("smoke", Palette.Smoke, 0f, 0.05f);
        public static Material Hornet => Glow("hornet", Palette.Hornet, 3.2f);
        public static Material HornetTrail => Glow("hornetTrail", Palette.HornetTrail, 1.8f);
        public static Material ArcCore => Glow("arcCore", Palette.ArcCore, 5f);
        public static Material Arc => Glow("arc", Palette.Arc, 2.5f);
        public static Material Disc => Glow("disc", Palette.Disc, 3f);
        public static Material DiscHub => Glow("discHub", Color.white, 4f);
        public static Material DiscPlate => Lit("discPlate", Palette.DiscPlate, 0.3f, 0.5f);

        public static Material EnemyHull => Lit("enemyHull", Palette.EnemyHull, 0.5f, 0.35f);
        public static Material EnemyPlate => Lit("enemyPlate", Palette.EnemyPlate, 0.45f, 0.4f);
        public static Material EnemyGlow => Glow("enemyGlow", Palette.EnemyGlow, 3f);

        public static Material Explosion => Glow("explosion", Palette.Explosion, 5f);
        public static Material Spark => Glow("spark", Palette.Spark, 4f);
        public static Material Debris => Lit("debris", Palette.EnemyPlate, 0.4f, 0.3f);
        public static Material EnemyBullet => Glow("enemyBullet", Palette.EnemyBullet, 3.5f);
        public static Material BulletRim => Glow("bulletRim", Palette.BulletRim, 1f);
        public static Material Telegraph => Glow("telegraph", Palette.Telegraph, 4f);
        public static Material Credit => Glow("credit", Palette.Credit, 2.5f);
        public static Material Repair => Glow("repair", Palette.Repair, 2.5f);
        public static Material Asteroid => Lit("asteroid", Palette.Asteroid, 0f, 0.1f);
        public static Material AsteroidDark => Lit("asteroidDark", Palette.AsteroidDark, 0f, 0.1f);

        // Background stars: far ones dim, near ones just bright enough to bloom faintly.
        public static Material StarFar => Glow("starFar", Palette.Star, 0.45f);
        public static Material StarMid => Glow("starMid", Palette.Star, 0.8f);
        public static Material StarNear => Glow("starNear", Palette.Star, 1.3f);
    }
}
