using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Skyforge.EditorTools
{
    /// <summary>
    /// Player builds for every v1 platform (docs/DESIGN.md §10.3): Windows, web (WebGL) and Android.
    /// Menu: <b>Skyforge → Build</b>. From the command line:
    /// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod Skyforge.EditorTools.SkyforgeBuild.WebGL</c>
    /// (also <c>Windows</c>, <c>Android</c>, <c>AndroidBundle</c>). Output goes under <c>Builds/</c>.
    /// The platform settings are applied here, in code, so they're the same on every machine.
    /// </summary>
    public sealed class SkyforgeBuildSettings : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        /// <summary>However a build starts (menu, Build Settings window, command line), apply the platform's settings.</summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) SkyforgeBuild.ApplyAndroid(EditorUserBuildSettings.buildAppBundle);
            else if (report.summary.platform == BuildTarget.WebGL) SkyforgeBuild.ApplyWebGL();
        }
    }

    public static class SkyforgeBuild
    {
        public const string AndroidId = "com.robanteau.skyforgevanguard";

        static string Root => Path.GetDirectoryName(Application.dataPath);

        [MenuItem("Skyforge/Build/Windows")]
        public static void Windows() => Run(BuildTarget.StandaloneWindows64, "Builds/Windows/Skyforge Vanguard.exe");

        [MenuItem("Skyforge/Build/Web (WebGL)")]
        public static void WebGL()
        {
            ApplyWebGL();
            Run(BuildTarget.WebGL, "Builds/Web");
        }

        public static void ApplyWebGL()
        {
            // Gzip with the decompression fallback runs on any web host (itch.io, GitHub Pages, a plain
            // server) without special headers; a little bigger to download than Brotli.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
        }

        [MenuItem("Skyforge/Build/Android (APK, to install on a phone)")]
        public static void Android() => BuildAndroid(bundle: false);

        [MenuItem("Skyforge/Build/Android (App Bundle, for Google Play)")]
        public static void AndroidBundle() => BuildAndroid(bundle: true);

        static void BuildAndroid(bool bundle)
        {
            ApplyAndroid(bundle);
            Run(BuildTarget.Android, bundle ? "Builds/Android/SkyforgeVanguard.aab" : "Builds/Android/SkyforgeVanguard.apk");
        }

        public static void ApplyAndroid(bool bundle)
        {
            var target = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(target, AndroidId);
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.bundleVersionCode = VersionCode(PlayerSettings.bundleVersion);
            // Portrait only on phones (the field is 18 × 32).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            EditorUserBuildSettings.buildAppBundle = bundle;

            // Google Play needs a release key. Without these variables the build is signed with the
            // debug key, which is fine for installing on your own phone.
            string keystore = Environment.GetEnvironmentVariable("SKYFORGE_KEYSTORE");
            PlayerSettings.Android.useCustomKeystore = !string.IsNullOrEmpty(keystore);
            if (PlayerSettings.Android.useCustomKeystore)
            {
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("SKYFORGE_KEYSTORE_PASS") ?? "";
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("SKYFORGE_KEY_ALIAS") ?? "skyforge";
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("SKYFORGE_KEY_PASS") ?? PlayerSettings.Android.keystorePass;
            }
            else if (bundle)
            {
                Debug.LogWarning("Building an App Bundle with the debug key: Google Play will refuse it. Set SKYFORGE_KEYSTORE (see docs/BUILDING.md).");
            }
        }

        /// <summary>"0.7.0" → 700: Android needs a version code that only ever goes up.</summary>
        public static int VersionCode(string version)
        {
            var parts = (version ?? "0").Split('.');
            int P(int i) => i < parts.Length && int.TryParse(parts[i], out var v) ? v : 0;
            return Math.Max(1, P(0) * 10000 + P(1) * 100 + P(2));
        }

        static void Run(BuildTarget target, string output)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/Main.unity" };
            string path = Path.Combine(Root, output);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            var sum = report.summary;
            Debug.Log($"[SkyforgeBuild] {target}: {sum.result}, {sum.totalErrors} errors, {sum.totalSize / (1024 * 1024)} MB → {path}");
            if (Application.isBatchMode) EditorApplication.Exit(sum.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
