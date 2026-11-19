using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.ARCore;
using UnityEngine.XR.Management;
using Unity.XR.XREAL;

namespace ChipOSMarkII.Editor
{
    /// <summary>Assigns the official XREAL loader first and ARCore as a phone fallback.</summary>
    [InitializeOnLoad]
    public static class ChipOSSpatialXRSetup
    {
        private const string XrealSettingsPath = "Assets/XR/Settings/XREALSettings.asset";
        private const string SimulationSettingsPath = "Assets/XR/Settings/XRSimulationSettings.asset";

        static ChipOSSpatialXRSetup()
        {
            EditorApplication.delayCall += ConfigureAndroidSpatialRuntime;
        }

        [MenuItem("Tools/ChipOS Mark II/Configure XREAL Spatial Runtime")]
        public static void ConfigureAndroidSpatialRuntime()
        {
            var xrealSettings = AssetDatabase.LoadAssetAtPath<XREALSettings>(XrealSettingsPath);
            if (xrealSettings == null)
            {
                Debug.LogWarning("[ChipOS XR] XREAL settings asset is missing. AR Foundation will use the phone fallback if configured.");
                return;
            }

            xrealSettings.InitialTrackingType = TrackingType.MODE_6DOF;
            xrealSettings.InitialInputSource = InputSource.ControllerAndHands;
            // Keep the Unity activity as the Android launcher. The XREAL multi-resume
            // wrapper rejects ordinary Android devices before Unity can show the
            // phone fallback UI; the XREAL loader itself remains available on glasses.
            xrealSettings.SupportMultiResume = false;
            if (!xrealSettings.SupportDevices.Contains(XREALDeviceCategory.XREAL_DEVICE_CATEGORY_REALITY))
                xrealSettings.SupportDevices.Add(XREALDeviceCategory.XREAL_DEVICE_CATEGORY_REALITY);
            EditorBuildSettings.AddConfigObject(XREALSettings.k_SettingsKey, xrealSettings, true);

            var arcoreSettings = AssetDatabase.LoadAssetAtPath<ARCoreSettings>("Assets/XR/Settings/ARCoreSettings.asset");
            if (arcoreSettings != null)
            {
                arcoreSettings.requirement = ARCoreSettings.Requirement.Optional;
                arcoreSettings.depth = ARCoreSettings.Requirement.Optional;
            }

            var simulationSettings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SimulationSettingsPath);
            if (simulationSettings != null)
                EditorBuildSettings.AddConfigObject("com.unity.xr.arfoundation.simulation_settings", simulationSettings, true);

            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null || general.Manager == null)
            {
                Debug.LogError("[ChipOS XR] Android XR management settings are unavailable.");
                return;
            }

            var manager = general.Manager;
            // XRGeneralSettings has its own startup switch. If this remains true,
            // Unity calls InitializeLoaderSync from Awake and blocks the first screen
            // even when XRManagerSettings.automaticLoading is false.
            general.InitManagerOnStart = false;
            if (!XRPackageMetadataStore.AssignLoader(manager, "Unity.XR.XREAL.XREALXRLoader", BuildTargetGroup.Android))
            {
                Debug.LogError("[ChipOS XR] Could not assign XREALXRLoader. Check the local XREAL SDK package dependency.");
                return;
            }
            if (!XRPackageMetadataStore.AssignLoader(manager, "Unity.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android))
                Debug.LogWarning("[ChipOS XR] ARCore loader could not be assigned; phone-only spatial fallback will be unavailable.");

            var ordered = new List<XRLoader>();
            var xrealLoader = AssetDatabase.LoadAssetAtPath<XRLoader>("Assets/XR/Loaders/XREALXRLoader.asset");
            var arcoreLoader = AssetDatabase.LoadAssetAtPath<XRLoader>("Assets/XR/Loaders/ARCoreLoader.asset");
            if (xrealLoader != null) ordered.Add(xrealLoader);
            if (arcoreLoader != null) ordered.Add(arcoreLoader);
            if (!manager.TrySetLoaders(ordered))
                Debug.LogError("[ChipOS XR] Could not persist the XREAL and ARCore loader order.");
            else if (ordered.Count != 2)
                Debug.LogWarning($"[ChipOS XR] Expected XREAL and ARCore loaders; found {ordered.Count} loader asset(s).");
            // The XREAL loader can take several seconds to initialize and can fail
            // completely on ordinary phones. Keep the phone studio responsive at
            // launch and load an XR provider only after the user asks to map a room.
            manager.automaticLoading = false;
            manager.automaticRunning = false;

            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            // Unity 6 builds the supported Android player for ARM64. The x86_64 AVD
            // can exercise UI through ARM translation but cannot load Sherpa's ARM64
            // speech runtime; do not label an ARM64 APK as universal.
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(xrealSettings);
            if (arcoreSettings != null) EditorUtility.SetDirty(arcoreSettings);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChipOS XR] Configured standard Unity launcher, 6DoF XREAL first, and ARCore phone AR fallback.");
        }
    }
}
