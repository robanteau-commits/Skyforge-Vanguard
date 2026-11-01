using System.Globalization;
using System.Text;
using UnityEngine;

namespace Skyforge.Game
{
    /// <summary>
    /// Player settings (docs/DESIGN.md §9.6), stored apart from the save in PlayerPrefs as
    /// <c>key=value</c> lines: volumes, screen shake, flashes, bullet contrast, touch sensitivity,
    /// graphics quality, and the window on PC. Purely presentation: none of it touches the simulation.
    /// </summary>
    public static class GameSettings
    {
        const string Key = "skyforge.settings.v1";

        public static float MusicVolume = 0.6f;
        public static float SfxVolume = 0.8f;
        /// <summary>Screen shake strength, 0..1 (0 = off).</summary>
        public static float Shake = 1f;
        /// <summary>Big full-bright flashes (explosions, hits, boss phases); off for accessibility.</summary>
        public static bool Flashes = true;
        /// <summary>Enemy bullets drawn larger with a bright white rim.</summary>
        public static bool HighContrastBullets;
        /// <summary>Touch drag multiplier, 0.5..2.</summary>
        public static float TouchSensitivity = 1f;
        /// <summary>High = bloom, MSAA and full resolution; Low = the lighter mobile tier.</summary>
        public static bool HighQuality = !Application.isMobilePlatform;
        public static bool ShowFps = true;

        static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _loaded = false;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            var text = PlayerPrefs.GetString(Key, "");
            foreach (var raw in text.Split('\n'))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                string k = raw.Substring(0, eq).Trim(), v = raw.Substring(eq + 1).Trim();
                float f = float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : -1f;
                switch (k)
                {
                    case "music": if (f >= 0f) MusicVolume = Mathf.Clamp01(f); break;
                    case "sfx": if (f >= 0f) SfxVolume = Mathf.Clamp01(f); break;
                    case "shake": if (f >= 0f) Shake = Mathf.Clamp01(f); break;
                    case "flashes": Flashes = v != "0"; break;
                    case "contrast": HighContrastBullets = v == "1"; break;
                    case "touch": if (f > 0f) TouchSensitivity = Mathf.Clamp(f, 0.5f, 2f); break;
                    case "quality": HighQuality = v == "1"; break;
                    case "fps": ShowFps = v != "0"; break;
                }
            }
            ApplyQuality();
        }

        public static void Save()
        {
            var sb = new StringBuilder();
            void F(string k, float v) => sb.Append(k).Append('=').Append(v.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
            void B(string k, bool v) => sb.Append(k).Append('=').Append(v ? 1 : 0).Append('\n');
            F("music", MusicVolume); F("sfx", SfxVolume); F("shake", Shake); B("flashes", Flashes);
            B("contrast", HighContrastBullets); F("touch", TouchSensitivity); B("quality", HighQuality); B("fps", ShowFps);
            PlayerPrefs.SetString(Key, sb.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>Switch between the project's two quality levels, "PC" (high) and "Mobile" (low).</summary>
        public static void ApplyQuality()
        {
            string want = HighQuality ? "PC" : "Mobile";
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
                if (names[i] == want && QualitySettings.GetQualityLevel() != i) QualitySettings.SetQualityLevel(i, true);
        }
    }
}
