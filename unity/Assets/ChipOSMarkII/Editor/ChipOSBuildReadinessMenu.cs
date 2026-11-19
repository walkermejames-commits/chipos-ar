using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ChipOSMarkII
{
    public static class ChipOSBuildReadinessMenu
    {
        [MenuItem("Tools/ChipOS Mark II/Build Readiness/Run Android/XR Readiness Check")]
        public static void RunReadinessCheck()
        {
            var report = ChipOSBuildReadinessCheck.RunCheck();
            Debug.Log($"[ChipOS] Android/XR Readiness: {report.GetCompactSummary()}");
        }

        [MenuItem("Tools/ChipOS Mark II/Build Readiness/Write Android/XR Readiness Report")]
        public static void WriteReadinessReport()
        {
            var report = ChipOSBuildReadinessCheck.RunCheck();
            var text = ChipOSBuildReadinessCheck.GenerateReportText(report);
            var path = Path.Combine(ChipOSMarkII.Editor.ChipOSSceneBuilder.GetRepoRootPath(), "ChipOS_AndroidXR_ReadinessReport.txt");

            File.WriteAllText(path, text, new UTF8Encoding(false));
            Debug.Log($"[ChipOS] Android/XR readiness report written to: {path}");
        }

        [MenuItem("Tools/ChipOS Mark II/Build Readiness/Print Recommended Android Settings")]
        public static void PrintRecommendedSettings()
        {
            var profile = new ChipOSAndroidBuildProfile();
            Debug.Log($"[ChipOS] Recommended Package: {profile.PackageName}");
            Debug.Log($"[ChipOS] Min API: {profile.MinimumAndroidApi} | Target API: {profile.TargetAndroidApi}");
            Debug.Log($"[ChipOS] IL2CPP: {profile.UseIL2CPP} | ARM64: {profile.UseARM64}");
            Debug.Log($"[ChipOS] Landscape: {profile.PreferLandscape}");
            Debug.Log($"[ChipOS] Expected Symbols: CHIPOS_XREAL / CHIPOS_NRSDK / CHIPOS_OPENXR");
        }
    }
}
