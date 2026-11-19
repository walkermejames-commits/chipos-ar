using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ChipOSMarkII
{
    public static class ChipOSAndroidBuildConfigurator
    {
        [MenuItem("Tools/ChipOS Mark II/Android Build/Print Android Build Setup Instructions")]
        public static void PrintAndroidBuildSetupInstructions()
        {
            var profile = new ChipOSAndroidBuildProfile();
            var builder = new StringBuilder();
            builder.AppendLine("[ChipOS] First Android Phone Build Attempt");
            builder.AppendLine($"Main Scene: {ChipOSMarkII.Editor.ChipOSSceneBuilder.MainScenePath}");
            builder.AppendLine($"Package Name: {profile.PackageName}");
            builder.AppendLine($"Minimum API: {profile.MinimumAndroidApi}");
            builder.AppendLine($"Target API: {profile.TargetAndroidApi} (or highest installed / automatic)");
            builder.AppendLine($"IL2CPP: {profile.UseIL2CPP}");
            builder.AppendLine($"ARM64: {profile.UseARM64}");
            builder.AppendLine($"Orientation: {(profile.PreferLandscape ? "Landscape" : "Auto")}");
            builder.AppendLine("Keep simulator fallback active for the first Android phone build.");
            builder.AppendLine("Do not enable CHIPOS_XREAL, CHIPOS_NRSDK, or CHIPOS_OPENXR yet.");
            builder.AppendLine("After the APK run, fill in CHIPOS_ANDROID_BUILD_REPORT_TEMPLATE.md.");
            Debug.Log(builder.ToString());
        }

        [MenuItem("Tools/ChipOS Mark II/Android Build/Apply Safe Android Player Settings")]
        public static void ApplySafeAndroidPlayerSettings()
        {
            var profile = new ChipOSAndroidBuildProfile();
            var notes = new StringBuilder();

            try
            {
                ApplyApplicationIdentifier(profile.PackageName);
                notes.AppendLine($"Package Name: {profile.PackageName}");
                PlayerSettings.productName = "ChipOS Mark II";
                notes.AppendLine("Application Name: ChipOS Mark II");
                PlayerSettings.bundleVersion = "0.1.21";
                PlayerSettings.Android.bundleVersionCode = 21;
                notes.AppendLine("Release: 0.1.21 (Android version code 21)");

                ApplyScriptingBackend(ScriptingImplementation.IL2CPP);
                notes.AppendLine("Scripting Backend: IL2CPP");

                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = true;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                notes.AppendLine("Orientation: Auto-rotate between portrait phone UI and landscape glasses UI");

                // XREAL's direct-presentation path uses camera alpha so the real
                // scene remains visible behind the rendered UI. Unity otherwise
                // clears the Android framebuffer opaquely even with a transparent
                // camera background.
                PlayerSettings.preserveFramebufferAlpha = true;
                notes.AppendLine("Framebuffer Alpha: preserved for transparent XREAL presentation");

                TryApplyAndroidArchitecture(notes);
                TryApplyAndroidApiLevels(profile, notes);
                EnableDirectPresent(notes);

                notes.AppendLine("Permissions: left unchanged unless already required by the project.");
                Debug.Log("[ChipOS] Applied safe Android player settings.\n" + notes);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ChipOS] Failed to apply safe Android player settings: " + ex.Message);
                throw;
            }
        }

        private static void EnableDirectPresent(StringBuilder notes)
        {
            // The XREAL loader only starts its direct AR presentation path when this
            // symbol is compiled into the Android player. Without it, the glasses can
            // behave like a large view-fixed display instead of a transparent AR view.
            var symbols = new List<string>(PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            if (!symbols.Contains("DIRECT_PRESENT")) symbols.Add("DIRECT_PRESENT");
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, string.Join(";", symbols));
            notes.AppendLine("XREAL: DIRECT_PRESENT enabled for the Android player.");
        }

        private static void TryApplyAndroidArchitecture(StringBuilder notes)
        {
            try
            {
                var androidPlayerSettingsType = typeof(PlayerSettings).GetNestedType("Android", BindingFlags.Public);
                if (androidPlayerSettingsType == null)
                {
                    notes.AppendLine("ARM64: Android PlayerSettings reflection type was unavailable.");
                    return;
                }

                var architectureType = Type.GetType("UnityEditor.AndroidArchitecture, UnityEditor");
                var targetArchitectures = androidPlayerSettingsType.GetProperty("targetArchitectures", BindingFlags.Public | BindingFlags.Static);
                if (architectureType == null || targetArchitectures == null || !targetArchitectures.CanWrite)
                {
                    notes.AppendLine("ARM64: Android architecture setting was not available in this editor install.");
                    return;
                }

                var arm64Value = Enum.Parse(architectureType, "ARM64", true);
                targetArchitectures.SetValue(null, arm64Value, null);
                notes.AppendLine("Target Architectures: ARM64");
            }
            catch (Exception ex)
            {
                notes.AppendLine("ARM64: Could not apply automatically. " + ex.Message);
            }
        }

        private static void ApplyApplicationIdentifier(string packageName)
        {
            var namedBuildTargetType = FindNamedBuildTargetType();
            var setMethod = namedBuildTargetType == null
                ? null
                : typeof(PlayerSettings).GetMethod("SetApplicationIdentifier", BindingFlags.Public | BindingFlags.Static, null, new[] { namedBuildTargetType, typeof(string) }, null);
            var androidTarget = namedBuildTargetType == null ? null : namedBuildTargetType.GetField("Android", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

            if (setMethod != null && androidTarget != null)
            {
                setMethod.Invoke(null, new[] { androidTarget, packageName });
                return;
            }

#pragma warning disable CS0618
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, packageName);
#pragma warning restore CS0618
        }

        private static void ApplyScriptingBackend(ScriptingImplementation backend)
        {
            var namedBuildTargetType = FindNamedBuildTargetType();
            var setMethod = namedBuildTargetType == null
                ? null
                : typeof(PlayerSettings).GetMethod("SetScriptingBackend", BindingFlags.Public | BindingFlags.Static, null, new[] { namedBuildTargetType, typeof(ScriptingImplementation) }, null);
            var androidTarget = namedBuildTargetType == null ? null : namedBuildTargetType.GetField("Android", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

            if (setMethod != null && androidTarget != null)
            {
                setMethod.Invoke(null, new[] { androidTarget, backend });
                return;
            }

#pragma warning disable CS0618
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, backend);
#pragma warning restore CS0618
        }

        private static Type FindNamedBuildTargetType()
        {
            return Type.GetType("UnityEditor.Build.NamedBuildTarget, UnityEditor.CoreModule")
                   ?? Type.GetType("UnityEditor.Build.NamedBuildTarget, UnityEditor");
        }

        private static void TryApplyAndroidApiLevels(ChipOSAndroidBuildProfile profile, StringBuilder notes)
        {
            try
            {
                var androidPlayerSettingsType = typeof(PlayerSettings).GetNestedType("Android", BindingFlags.Public);
                var sdkEnumType = Type.GetType("UnityEditor.AndroidSdkVersions, UnityEditor");
                if (androidPlayerSettingsType == null || sdkEnumType == null)
                {
                    notes.AppendLine("Android API Levels: Android SDK enum was unavailable in this editor install.");
                    return;
                }

                var minProperty = androidPlayerSettingsType.GetProperty("minSdkVersion", BindingFlags.Public | BindingFlags.Static);
                var targetProperty = androidPlayerSettingsType.GetProperty("targetSdkVersion", BindingFlags.Public | BindingFlags.Static);

                if (minProperty == null || targetProperty == null)
                {
                    notes.AppendLine("Android API Levels: PlayerSettings.Android properties were unavailable.");
                    return;
                }

                // The XREAL and offline speech bridge AARs declare minSdk 31.
                // Keep the app installable on Android 9+ rather than forcing a manifest override.
                var minimumAndroidApi = Math.Max(profile.MinimumAndroidApi, 31);
                var minValue = ResolveSdkEnumValue(sdkEnumType, minimumAndroidApi);
                if (minValue != null)
                {
                    minProperty.SetValue(null, minValue, null);
                    notes.AppendLine($"Minimum API: {minimumAndroidApi}");
                }
                else
                {
                    notes.AppendLine($"Minimum API: Could not map {minimumAndroidApi} automatically.");
                }

                var targetValue = ResolveAutomaticOrHighestSdkEnumValue(sdkEnumType);
                if (targetValue != null)
                {
                    targetProperty.SetValue(null, targetValue, null);
                    notes.AppendLine("Target API: Automatic / highest installed");
                }
                else
                {
                    notes.AppendLine("Target API: Automatic / highest installed could not be resolved automatically.");
                }
            }
            catch (Exception ex)
            {
                notes.AppendLine("Android API Levels: Could not apply automatically. " + ex.Message);
            }
        }

        private static object ResolveSdkEnumValue(Type sdkEnumType, int apiLevel)
        {
            var names = Enum.GetNames(sdkEnumType);
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i].Contains(apiLevel.ToString(), StringComparison.Ordinal))
                {
                    return Enum.Parse(sdkEnumType, names[i], true);
                }
            }

            return null;
        }

        private static object ResolveAutomaticOrHighestSdkEnumValue(Type sdkEnumType)
        {
            var automaticNames = new[]
            {
                "AndroidApiLevelAuto",
                "Auto",
                "Automatic",
                "AndroidApiLevelHighestInstalled"
            };

            for (var i = 0; i < automaticNames.Length; i++)
            {
                try
                {
                    return Enum.Parse(sdkEnumType, automaticNames[i], true);
                }
                catch
                {
                }
            }

            var names = Enum.GetNames(sdkEnumType);
            for (var i = names.Length - 1; i >= 0; i--)
            {
                if (names[i].IndexOf("AndroidApiLevel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return Enum.Parse(sdkEnumType, names[i], true);
                }
            }

            return null;
        }
    }
}
