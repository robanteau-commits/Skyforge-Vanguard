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

        public static Material Hull => Lit("hull", Palette.Hull, 0.55f, 0.4f);
        public static Material HullLight => Lit("hullLight", Palette.HullLight, 0.5f, 0.45f);
        public static Material HullDark => Lit("hullDark", Palette.HullDark, 0.4f, 0.3f);
        public static Material Canopy => Lit("canopy", Palette.Canopy, 0.2f, 0.92f);
        public static Material PlayerGlow => Glow("playerGlow", Palette.PlayerGlow, 3f);
        public static Material PlayerGlowSoft => Glow("playerGlowSoft", Palette.PlayerGlow, 1.4f);
        public static Material PlayerShot => Glow("playerShot", Palette.PlayerShot, 4f);

        public static Material EnemyHull => Lit("enemyHull", Palette.EnemyHull, 0.5f, 0.35f);
        public static Material EnemyPlate => Lit("enemyPlate", Palette.EnemyPlate, 0.45f, 0.4f);
        public static Material EnemyGlow => Glow("enemyGlow", Palette.EnemyGlow, 3f);

        public static Material Explosion => Glow("explosion", Palette.Explosion, 5f);
        public static Material Spark => Glow("spark", Palette.Spark, 4f);
        public static Material Debris => Lit("debris", Palette.EnemyPlate, 0.4f, 0.3f);

        public static Material Water => Lit("water", Palette.Water, 0.1f, 0.75f);
        public static Material Land => Lit("land", Palette.Land, 0f, 0.1f);
        public static Material Rock => Lit("rock", Palette.Rock, 0f, 0.15f);
        public static Material CityLight => Glow("cityLight", Palette.CityLight, 2.2f);
    }
}
