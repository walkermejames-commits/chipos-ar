using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChipOSMarkII.Editor
{
    public static class ChipOSProjectHealthCheck
    {
        private const string ExpectedUnityVersion = "6000.4.8f1";
        private const string ReportFileName = "ChipOS_HealthReport.txt";

        private sealed class HealthCheckReport
        {
            private readonly List<string> passes = new List<string>();
            private readonly List<string> warnings = new List<string>();
            private readonly List<string> failures = new List<string>();
            private readonly List<string> notes = new List<string>();

            public string ReportPath { get; set; }
            public int PassCount => passes.Count;
            public int WarningCount => warnings.Count;
            public int FailureCount => failures.Count;
            public string Status => failures.Count > 0 ? "FAIL" : warnings.Count > 0 ? "WARNING" : "PASS";

            public void Pass(string message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    passes.Add(message.Trim());
                }
            }

            public void Warn(string message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    warnings.Add(message.Trim());
                }
            }

            public void Fail(string message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    failures.Add(message.Trim());
                }
            }

            public void Note(string message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    notes.Add(message.Trim());
                }
            }

            public string BuildText()
            {
                var builder = new StringBuilder();
                builder.AppendLine("ChipOS Mark II Project Health Check");
                builder.AppendLine($"Status: {Status}");
                builder.AppendLine($"PASS: {PassCount} | WARNING: {WarningCount} | FAIL: {FailureCount}");
                builder.AppendLine($"Generated: {DateTime.UtcNow:O}");
                if (!string.IsNullOrWhiteSpace(ReportPath))
                {
                    builder.AppendLine($"Report Path: {ReportPath}");
                }

                AppendSection(builder, "Notes", notes);
                AppendSection(builder, "Passes", passes);
                AppendSection(builder, "Warnings", warnings);
                AppendSection(builder, "Failures", failures);
                return builder.ToString();
            }

            private static void AppendSection(StringBuilder builder, string title, List<string> items)
            {
                if (items.Count == 0)
                {
                    return;
                }

                builder.AppendLine();
                builder.AppendLine(title + ":");
                for (var i = 0; i < items.Count; i++)
                {
                    builder.AppendLine($"- {items[i]}");
                }
            }
        }

        [MenuItem("Tools/ChipOS Mark II/Run Project Health Check", false, 110)]
        public static void RunProjectHealthCheckMenu()
        {
            RunProjectHealthCheck();
        }

        public static void RunProjectHealthCheck()
        {
            var report = RunProjectHealthCheckInternal();
            var text = report.BuildText();

            try
            {
                File.WriteAllText(report.ReportPath, text, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                report.Warn($"Could not write report file: {ex.Message}");
                text = report.BuildText();
            }

            if (report.FailureCount > 0)
            {
                Debug.LogError(text);
                throw new InvalidOperationException("ChipOS Mark II health check failed. See ChipOS_HealthReport.txt.");
            }

            if (report.WarningCount > 0)
            {
                Debug.LogWarning(text);
                return;
            }

            Debug.Log(text);
        }

        private static HealthCheckReport RunProjectHealthCheckInternal()
        {
            var report = new HealthCheckReport();
            report.ReportPath = Path.Combine(ChipOSSceneBuilder.GetRepoRootPath(), ReportFileName);
            report.Note($"Target scene: {ChipOSSceneBuilder.MainScenePath}");
            report.Note($"Expected Unity version: {ExpectedUnityVersion}");

            ValidateProjectVersion(report);
            ValidateMainSceneExists(report);
            ValidateRequiredScripts(report);

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                report.Fail("Health check cancelled because modified scenes were not saved.");
                return report;
            }

            var scene = OpenMainScene(report);
            if (!scene.IsValid())
            {
                return report;
            }

            ValidateSceneObjects(report);
            ValidateCriticalReferences(report);
            ValidateMissingReferences(report);
            ValidateRuntimeBinding(report);
            return report;
        }

        private static void ValidateProjectVersion(HealthCheckReport report)
        {
            var versionPath = Path.Combine(ChipOSSceneBuilder.GetRepoRootPath(), "ProjectSettings", "ProjectVersion.txt");
            if (!File.Exists(versionPath))
            {
                report.Fail("ProjectSettings/ProjectVersion.txt is missing.");
                return;
            }

            var versionText = File.ReadAllText(versionPath);
            if (versionText.Contains($"m_EditorVersion: {ExpectedUnityVersion}"))
            {
                report.Pass($"ProjectVersion matches Unity {ExpectedUnityVersion}.");
                return;
            }

            report.Warn($"ProjectVersion does not match Unity {ExpectedUnityVersion}. Found: {ExtractEditorVersion(versionText)}");
        }

        private static string ExtractEditorVersion(string versionText)
        {
            if (string.IsNullOrWhiteSpace(versionText))
            {
                return "Unknown";
            }

            var lines = versionText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("m_EditorVersion: ", StringComparison.Ordinal))
                {
                    return lines[i].Substring("m_EditorVersion: ".Length).Trim();
                }
            }

            return "Unknown";
        }

        private static void ValidateMainSceneExists(HealthCheckReport report)
        {
            var scenePath = ChipOSSceneBuilder.MainScenePath;
            var absoluteScenePath = Path.Combine(ChipOSSceneBuilder.GetRepoRootPath(), scenePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absoluteScenePath))
            {
                report.Fail($"Main scene file is missing: {scenePath}");
                return;
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset == null)
            {
                report.Fail($"Main scene asset could not be loaded: {scenePath}");
                return;
            }

            report.Pass($"Main scene exists: {scenePath}");
        }

        private static void ValidateRequiredScripts(HealthCheckReport report)
        {
            var assemblies = CompilationPipeline.GetAssemblies();
            if (assemblies.Any(assembly => assembly.name == "Assembly-CSharp"))
            {
                report.Pass("Assembly-CSharp is compiled and discoverable.");
            }
            else
            {
                report.Fail("Assembly-CSharp is not discoverable through the compilation pipeline.");
            }

            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Core/ChipOSApp.cs", typeof(ChipOSApp));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Layout/LayoutManager.cs", typeof(LayoutManager));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Input/InputRouter.cs", typeof(InputRouter));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Core/ChipOSRuntime.cs", typeof(ChipOSRuntime));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Panels/ChipPanel.cs", typeof(ChipPanel));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Panels/TaskPanel.cs", typeof(TaskPanel));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Debug/DebugPanel.cs", typeof(DebugPanel));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Core/IChipOSService.cs", typeof(IChipOSService));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Core/ServiceRegistry.cs", typeof(ServiceRegistry));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Core/ChipOSEventBus.cs", typeof(ChipOSEventBus));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Core/ChipOSRuntimeState.cs", typeof(ChipOSRuntimeState));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Core/ChipOSNotificationService.cs", typeof(ChipOSNotificationService));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Device/IChipOSDeviceService.cs", typeof(IChipOSDeviceService));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Device/ChipOSDeviceMode.cs", typeof(ChipOSDeviceMode));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Device/ChipOSDeviceTrackingState.cs", typeof(ChipOSDeviceTrackingState));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Device/ChipOSDeviceContext.cs", typeof(ChipOSDeviceContext));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Device/ChipOSDevicePose.cs", typeof(ChipOSDevicePose));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Device/ChipOSDeviceSnapshot.cs", typeof(ChipOSDeviceSnapshot));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Device/SimulatedChipOSDeviceService.cs", typeof(SimulatedChipOSDeviceService));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Audio/ChipOSAudioFrame.cs", typeof(ChipOSAudioFrame));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Audio/IChipOSAudioSource.cs", typeof(IChipOSAudioSource));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Audio/ChipOSAudioSourceState.cs", typeof(ChipOSAudioSourceState));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Audio/ChipOSAudioDiagnostics.cs", typeof(ChipOSAudioDiagnostics));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Audio/SimulatedAudioSource.cs", typeof(SimulatedAudioSource));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Captions/ChipOSCaptionEvent.cs", typeof(ChipOSCaptionEvent));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Captions/ChipOSCaptionDiagnostics.cs", typeof(ChipOSCaptionDiagnostics));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Captions/ChipOSCaptionService.cs", typeof(ChipOSCaptionService));
            ValidateScript(report, "Assets/ChipOSMarkII/Scripts/Captions/CaptionPanel.cs", typeof(CaptionPanel));
        }

        private static void ValidateScript(HealthCheckReport report, string assetPath, Type expectedType)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (script == null)
            {
                report.Fail($"Required script asset is missing: {assetPath}");
                return;
            }

            var compiledType = script.GetClass();
            if (compiledType == null)
            {
                if (expectedType.IsInterface || expectedType.IsEnum || (expectedType.IsValueType && !expectedType.IsPrimitive))
                {
                    report.Pass($"Contract asset is present and discoverable: {expectedType.Name}");
                    return;
                }

                report.Fail($"Script compiles to no discoverable type: {assetPath}");
                return;
            }

            if (compiledType != expectedType)
            {
                report.Fail($"Script type mismatch for {assetPath}. Expected {expectedType.Name}, found {compiledType.Name}.");
                return;
            }

            report.Pass($"Script is compiled and discoverable: {expectedType.Name}");
        }

        private static Scene OpenMainScene(HealthCheckReport report)
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(ChipOSSceneBuilder.MainScenePath, OpenSceneMode.Single);
                if (scene.IsValid())
                {
                    report.Pass("Main scene opened successfully in editor mode.");
                    return scene;
                }

                report.Fail("Main scene did not open as a valid scene.");
            }
            catch (Exception ex)
            {
                report.Fail($"Main scene could not be opened: {ex.Message}");
            }

            return default;
        }

        private static void ValidateSceneObjects(HealthCheckReport report)
        {
            RequireNamedObject<ChipOSApp>(ChipOSSceneBuilder.RootObjectName, report, out var app);
            RequireNamedComponent<LayoutManager>(app != null ? app.gameObject : null, ChipOSSceneBuilder.RootObjectName, report);
            RequireNamedComponent<InputRouter>(app != null ? app.gameObject : null, ChipOSSceneBuilder.RootObjectName, report);
            RequireNamedComponent<ChipOSRuntime>(app != null ? app.gameObject : null, ChipOSSceneBuilder.RootObjectName, report);
            RequireNamedObject<Canvas>(ChipOSSceneBuilder.CanvasObjectName, report, out _);
            RequireNamedObject<ChipPanel>("ChipPanel", report, out _);
            RequireNamedObject<TaskPanel>("TaskPanel", report, out _);
            RequireOptionalNamedObject<CaptionPanel>("CaptionPanel", report);
            RequireNamedObject<DebugPanel>("DebugPanel", report, out _);
            RequireAnyObject<EventSystem>(report, out _);
        }

        private static void ValidateCriticalReferences(HealthCheckReport report)
        {
            var app = UnityEngine.Object.FindFirstObjectByType<ChipOSApp>(FindObjectsInactive.Include);
            var canvas = FindNamedObject<Canvas>(ChipOSSceneBuilder.CanvasObjectName);

            if (app == null || canvas == null)
            {
                return;
            }

            var serializedApp = new SerializedObject(app);
            var sceneHudCanvasProperty = serializedApp.FindProperty("sceneHudCanvas");
            if (sceneHudCanvasProperty == null || sceneHudCanvasProperty.objectReferenceValue == null)
            {
                report.Fail("ChipOSApp.sceneHudCanvas is null after scene build.");
            }
            else if (!ReferenceEquals(sceneHudCanvasProperty.objectReferenceValue, canvas))
            {
                report.Fail("ChipOSApp.sceneHudCanvas does not point to ChipOSCanvas.");
            }
            else
            {
                report.Pass("ChipOSApp.sceneHudCanvas is wired to ChipOSCanvas.");
            }

            var safeAreaRoot = canvas.transform.Find("SafeAreaRoot");
            if (safeAreaRoot == null)
            {
                report.Fail("SafeAreaRoot is missing under ChipOSCanvas.");
            }
            else
            {
                report.Pass("SafeAreaRoot exists under ChipOSCanvas.");
            }

            ValidatePanelDefinitions(report);
        }

        private static void ValidatePanelDefinitions(HealthCheckReport report)
        {
            var panels = UnityEngine.Object.FindObjectsByType<HudPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (panels.Length == 0)
            {
                report.Fail("No HudPanel components were found in the main scene.");
                return;
            }

            var seenIds = new HashSet<string>();
            for (var i = 0; i < panels.Length; i++)
            {
                var panel = panels[i];
                if (panel == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(panel.PanelId))
                {
                    report.Fail($"Panel '{panel.gameObject.name}' is missing a stable PanelId.");
                    continue;
                }

                if (!seenIds.Add(panel.PanelId))
                {
                    report.Fail($"Duplicate panel id detected: '{panel.PanelId}'.");
                }
            }

            if (seenIds.Count > 0)
            {
                report.Pass($"Validated {seenIds.Count} unique panel id(s).");
            }
        }

        private static void ValidateMissingReferences(HealthCheckReport report)
        {
            var missingScriptCount = 0;
            var missingReferenceCount = 0;
            var invalidTmpReferenceCount = 0;
            var components = UnityEngine.Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    missingScriptCount++;
                    continue;
                }

                var serializedObject = new SerializedObject(component);
                var iterator = serializedObject.GetIterator();
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

#if UNITY_6000_6_OR_NEWER
                    if (iterator.objectReferenceValue != null || iterator.objectReferenceEntityIdValue == EntityId.None)
#else
                    if (iterator.objectReferenceValue != null || iterator.objectReferenceInstanceIDValue == 0)
#endif
                    {
                        continue;
                    }

                    missingReferenceCount++;
                    if (component.GetType().FullName != null && component.GetType().FullName.Contains("TMPro", StringComparison.Ordinal))
                    {
                        invalidTmpReferenceCount++;
                    }
                }
            }

            if (missingScriptCount == 0)
            {
                report.Pass("No missing script components were found in the main scene.");
            }
            else
            {
                report.Fail($"Missing script components found in the main scene: {missingScriptCount}");
            }

            if (missingReferenceCount == 0)
            {
                report.Pass("No serialized missing references were found in the main scene.");
            }
            else
            {
                report.Fail($"Serialized missing references found in the main scene: {missingReferenceCount}");
            }

            if (invalidTmpReferenceCount == 0)
            {
                report.Pass("No invalid TextMeshPro references were found.");
            }
            else
            {
                report.Warn($"Invalid TextMeshPro references found: {invalidTmpReferenceCount}");
            }
        }

        private static void ValidateRuntimeBinding(HealthCheckReport report)
        {
            var temporaryRoot = new GameObject("ChipOSHealthCheck_TempRuntime");
            temporaryRoot.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                var app = temporaryRoot.AddComponent<ChipOSApp>();
                var runtime = temporaryRoot.AddComponent<ChipOSRuntime>();
                var layoutManager = temporaryRoot.AddComponent<LayoutManager>();
                var inputRouter = temporaryRoot.AddComponent<InputRouter>();

                layoutManager.Bind(app);
                inputRouter.Bind(app);
                runtime.Bind(app, layoutManager, inputRouter);

                var servicesReady =
                    runtime.Services.Get<ChipOSRuntime>() != null &&
                    runtime.Services.Get<ChipOSEventBus>() != null &&
                    runtime.Services.Get<ChipOSNotificationService>() != null &&
                    runtime.Services.Get<IChipOSDeviceService>() != null &&
                    runtime.Services.Get<IChipOSAudioSource>() != null &&
                    runtime.Services.Get<SimulatedAudioSource>() != null &&
                    runtime.Services.Get<ChipOSCaptionService>() != null &&
                    runtime.Services.Get<ChipOSApp>() != null &&
                    runtime.Services.Get<LayoutManager>() != null &&
                    runtime.Services.Get<InputRouter>() != null;

                if (!servicesReady)
                {
                    report.Fail("Runtime services could not be instantiated and bound safely.");
                    return;
                }

                report.Pass($"Runtime services instantiated and bound safely. Service count: {runtime.Services.Count}");
                report.Pass("Simulated audio/caption services registered safely. Real ASR and microphone capture are not required yet.");
                if (runtime.AudioSource == null)
                {
                    report.Fail("Runtime AudioSource property is null.");
                    return;
                }

                if (runtime.CaptionService == null)
                {
                    report.Fail("Runtime CaptionService property is null.");
                    return;
                }

                report.Pass($"Runtime exposes AudioSource: {runtime.AudioSource.SourceName}");
                report.Pass("Runtime exposes CaptionService.");
                report.Pass("No Android RECORD_AUDIO permission is required for the simulated audio/caption layer.");

                var canvasObject = new GameObject("ChipOSHealthCheck_Canvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.hideFlags = HideFlags.HideAndDontSave;
                var safeAreaRoot = new GameObject("SafeAreaRoot", typeof(RectTransform));
                safeAreaRoot.hideFlags = HideFlags.HideAndDontSave;
                safeAreaRoot.transform.SetParent(canvasObject.transform, false);

                var debugPanelObject = new GameObject("DebugPanel", typeof(DebugPanel));
                debugPanelObject.hideFlags = HideFlags.HideAndDontSave;
                var debugPanel = debugPanelObject.GetComponent<DebugPanel>();
                debugPanel.InitializeModule(app, safeAreaRoot.transform);
                debugPanel.HandleStateChanged(app.State);
                report.Pass("DebugPanel can display runtime and device status without null exceptions.");

                UnityEngine.Object.DestroyImmediate(debugPanelObject);

                var captionPanelObject = new GameObject("CaptionPanel", typeof(CaptionPanel));
                captionPanelObject.hideFlags = HideFlags.HideAndDontSave;
                var captionPanel = captionPanelObject.GetComponent<CaptionPanel>();
                captionPanel.InitializeModule(app, safeAreaRoot.transform);
                captionPanel.HandleStateChanged(app.State);
                report.Pass("CaptionPanel can display mock caption status without null exceptions.");

                UnityEngine.Object.DestroyImmediate(captionPanelObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
            catch (Exception ex)
            {
                var rootException = ex.InnerException ?? ex;
                report.Fail(
                    $"Runtime services threw during editor-time binding: {rootException.GetType().Name}: {rootException.Message}\n{rootException.StackTrace}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporaryRoot);
            }
        }

        private static void RequireNamedObject<T>(string objectName, HealthCheckReport report, out T component) where T : Component
        {
            component = FindNamedObject<T>(objectName);
            if (component == null)
            {
                report.Fail($"Expected object '{objectName}' with component {typeof(T).Name}.");
                return;
            }

            report.Pass($"Found {typeof(T).Name} on '{objectName}'.");
        }

        private static void RequireNamedComponent<T>(GameObject gameObject, string objectName, HealthCheckReport report) where T : Component
        {
            if (gameObject == null)
            {
                report.Fail($"Could not validate component {typeof(T).Name} because '{objectName}' was missing.");
                return;
            }

            var component = gameObject.GetComponent<T>();
            if (component == null)
            {
                report.Fail($"Expected component {typeof(T).Name} on '{objectName}'.");
                return;
            }

            report.Pass($"Found {typeof(T).Name} on '{objectName}'.");
        }

        private static void RequireOptionalNamedObject<T>(string objectName, HealthCheckReport report) where T : Component
        {
            var component = FindNamedObject<T>(objectName);
            if (component == null)
            {
                report.Warn($"Optional object '{objectName}' with component {typeof(T).Name} is not serialized in the current scene. ChipOSApp and the scene builder can create it safely.");
                return;
            }

            report.Pass($"Found optional {typeof(T).Name} on '{objectName}'.");
        }

        private static void RequireAnyObject<T>(HealthCheckReport report, out T component) where T : Component
        {
            component = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (component == null)
            {
                report.Fail($"Expected a scene object with component {typeof(T).Name}.");
                return;
            }

            report.Pass($"Found scene object with component {typeof(T).Name}.");
        }

        private static T FindNamedObject<T>(string objectName) where T : Component
        {
            var instances = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < instances.Length; i++)
            {
                if (instances[i] != null && instances[i].gameObject.name == objectName)
                {
                    return instances[i];
                }
            }

            return null;
        }
    }
}
