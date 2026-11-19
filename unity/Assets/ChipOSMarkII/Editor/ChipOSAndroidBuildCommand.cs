using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChipOSMarkII.Editor
{
    /// <summary>Command-line entry point for producing the first Android APK.</summary>
    public static class ChipOSAndroidBuildCommand
    {
        public static void BuildAndroidApk()
        {
            BuildAndroidApk(false);
        }

        /// <summary>Builds the ARM64 APK for emulator UI/fallback checks. Unity 6 no longer builds Android x86_64.</summary>
        public static void BuildAndroidEmulatorApk()
        {
            BuildAndroidApk(true);
        }

        private static void BuildAndroidApk(bool emulatorTarget)
        {
            ConfigureAndroidToolPaths();
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not determine the Unity project root.");
            var outputPath = Environment.GetEnvironmentVariable("CHIPOS_APK_PATH");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.Combine(projectRoot, "Builds", "Android", "ChipOSMarkII.apk");
            }

            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)
                ?? throw new InvalidOperationException("APK output path has no parent directory."));

            ChipOSSpatialXRSetup.ConfigureAndroidSpatialRuntime();
            ChipOSAndroidBuildConfigurator.ApplySafeAndroidPlayerSettings();
            // Unity 6.0+ no longer supports Android x86_64. Keep this as a real
            // ARM64-only APK; an x86_64 AVD may run its UI through ARM translation,
            // but the offline Sherpa native runtime is intentionally unavailable there.
            if (emulatorTarget)
                Debug.Log("[ChipOS] Emulator check build is ARM64-only: UI/fallback can be checked, but offline speech is unavailable on the x86_64 AVD.");
            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                scenes = new[] { new EditorBuildSettingsScene(ChipOSSceneBuilder.MainScenePath, true) };
            }

            var options = new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(scenes, scene => scene.path),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[ChipOS] Building Android APK to {outputPath}");
            var report = BuildPipeline.BuildPlayer(options);
            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type == LogType.Error || message.type == LogType.Exception)
                        Debug.LogError($"[ChipOS] Build error ({step.name}): {message.content}");
                }
            }
            Debug.Log($"[ChipOS] Build result: {report.summary.result}; errors: {report.summary.totalErrors}; warnings: {report.summary.totalWarnings}; APK: {outputPath}");
            ChipOSAndroidBuildConfigurator.ApplySafeAndroidPlayerSettings();
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Android APK build failed with result {report.summary.result} and {report.summary.totalErrors} errors.");
            }
        }

        private static void ConfigureAndroidToolPaths()
        {
            var sdkRoot = Environment.GetEnvironmentVariable("CHIPOS_ANDROID_SDK_ROOT");
            var ndkRoot = Environment.GetEnvironmentVariable("CHIPOS_ANDROID_NDK_ROOT");
            var jdkRoot = Environment.GetEnvironmentVariable("CHIPOS_ANDROID_JDK_ROOT");
            if (string.IsNullOrWhiteSpace(jdkRoot))
                jdkRoot = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (string.IsNullOrWhiteSpace(sdkRoot) || !Directory.Exists(sdkRoot))
                throw new InvalidOperationException("Set CHIPOS_ANDROID_SDK_ROOT to an installed Android SDK before building.");
            if (string.IsNullOrWhiteSpace(ndkRoot) || !File.Exists(Path.Combine(ndkRoot, "source.properties")))
                throw new InvalidOperationException("Set CHIPOS_ANDROID_NDK_ROOT to an installed Android NDK before building.");
            if (string.IsNullOrWhiteSpace(jdkRoot) || !File.Exists(Path.Combine(jdkRoot, "bin", "java.exe")))
                throw new InvalidOperationException("Set CHIPOS_ANDROID_JDK_ROOT to an installed JDK 17 before building.");

            EditorPrefs.SetString("AndroidSdkRoot", Path.GetFullPath(sdkRoot));
            EditorPrefs.SetString("AndroidNdkRoot", Path.GetFullPath(ndkRoot));
            EditorPrefs.SetString("JdkPath", Path.GetFullPath(jdkRoot));
            EditorPrefs.SetString("AndroidSdkPath", Path.GetFullPath(sdkRoot));
            EditorPrefs.SetString("AndroidNdkPath", Path.GetFullPath(ndkRoot));
            EditorPrefs.SetString("SdkPath", Path.GetFullPath(sdkRoot));
            EditorPrefs.SetString("NdkPath", Path.GetFullPath(ndkRoot));
            EditorPrefs.SetBool("SdkUseEmbedded", false);
            EditorPrefs.SetBool("NdkUseEmbedded", false);
            EditorPrefs.SetBool("JdkUseEmbedded", false);
            Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT", Path.GetFullPath(sdkRoot));
            Environment.SetEnvironmentVariable("ANDROID_HOME", Path.GetFullPath(sdkRoot));
            Environment.SetEnvironmentVariable("ANDROID_NDK_ROOT", Path.GetFullPath(ndkRoot));
            Environment.SetEnvironmentVariable("ANDROID_NDK_HOME", Path.GetFullPath(ndkRoot));
            Environment.SetEnvironmentVariable("JAVA_HOME", Path.GetFullPath(jdkRoot));
            SetAndroidExternalToolPath("sdkRootPath", sdkRoot);
            SetAndroidExternalToolPath("ndkRootPath", ndkRoot);
            SetAndroidExternalToolPath("jdkRootPath", jdkRoot);
            Debug.Log($"[ChipOS] Android SDK: {Path.GetFullPath(sdkRoot)}\n[ChipOS] Android NDK: {Path.GetFullPath(ndkRoot)}\n[ChipOS] Android JDK: {Path.GetFullPath(jdkRoot)}");
        }

        private static void SetAndroidExternalToolPath(string propertyName, string path)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("UnityEditor.Android.AndroidExternalToolsSettings", false);
                if (type == null) continue;
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (property == null || !property.CanWrite)
                {
                    Debug.LogWarning($"[ChipOS] Unity Android external-tools property {propertyName} is unavailable.");
                    return;
                }
                property.SetValue(null, Path.GetFullPath(path), null);
                Debug.Log($"[ChipOS] Unity Android {propertyName}: {property.GetValue(null, null)}");
                return;
            }
            Debug.LogWarning("[ChipOS] Unity AndroidExternalToolsSettings API is unavailable; using EditorPrefs and environment paths.");
        }
    }
}
