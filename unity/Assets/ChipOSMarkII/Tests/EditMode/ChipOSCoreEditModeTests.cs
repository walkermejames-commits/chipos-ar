using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using ChipOSMarkII;

namespace ChipOSMarkII.Tests.EditMode
{
    public sealed class ChipOSCoreEditModeTests
    {
        private object capturedRuntimeEvent;
        private int capturedRuntimeEventCount;

        [Test]
        public void CreativeStudioStartsVisibleAndItsControlsRespondToClicks()
        {
            var host = new GameObject("StudioExperience_EditModeTest");
            try
            {
                var studio = host.AddComponent<StudioExperience>();
                typeof(StudioExperience).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(studio, null);

                var dock = (RectTransform)typeof(StudioExperience).GetField("window", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(studio);
                Assert.That(dock.name, Is.EqualTo("QuickActionDock"));
                Assert.That(dock.sizeDelta, Is.EqualTo(new Vector2(1420, 400)));
                Assert.That(dock.gameObject.activeInHierarchy, Is.True, "The launch dock must be visible beneath an active overlay root.");

                var body = dock.Find("StudioBody");
                foreach (var actionName in new[] { "StartSpatial", "LiveCaptions", "AnchorCaptions" })
                {
                    var action = body.Find(actionName).GetComponent<Button>();
                    Assert.That(action.gameObject.activeInHierarchy, Is.True, actionName + " must be visible before opening the workspace.");
                    Assert.That(action.interactable, Is.True, actionName + " must accept input.");
                }
                Assert.That(body.Find("LiveCaptions").GetComponentInChildren<Text>().text,
                    Does.Contain("microphone"), "The primary action must say that it starts microphone capture.");

                Assert.That(body.Find("AreaCards").gameObject.activeSelf, Is.False, "Workspace cards must not cover the quick actions on launch.");
                var rootImage = dock.parent.GetComponent<Image>();
                Assert.That(rootImage.color.a, Is.GreaterThan(0.9f), "The phone fallback needs a readable background when no AR camera passthrough is active.");
                Assert.That(rootImage.raycastTarget, Is.False, "Empty space around the dock must not intercept pointer input.");

                var areasButton = dock.Find("StudioHeader/Areas").GetComponent<Button>();
                areasButton.onClick.Invoke();
                Assert.That(body.Find("AreaCards").gameObject.activeInHierarchy, Is.True, "The Areas click must open the workspace.");
                Assert.That(dock.sizeDelta.y, Is.EqualTo(760));
                areasButton.onClick.Invoke();
                Assert.That(body.Find("AreaCards").gameObject.activeInHierarchy, Is.False, "The close click must return to quick actions.");
                Assert.That(dock.sizeDelta.y, Is.EqualTo(400));

                body.Find("LiveCaptions").GetComponent<Button>().onClick.Invoke();
                var microphoneStatus = (Text)typeof(StudioExperience).GetField("microphoneStatus", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(studio);
                Assert.That(microphoneStatus.text, Does.Contain("require the Android app"), "The microphone button must execute its platform-aware action.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SpatialMapPrefabsAreActiveAndRenderDetectedSurfaces()
        {
            var host = new GameObject("SpatialStudioController_EditModeTest");
            GameObject meshPrefab = null;
            GameObject planePrefab = null;
            try
            {
                var controller = host.AddComponent<SpatialStudioController>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var meshMethod = typeof(SpatialStudioController).GetMethod("CreateSpatialMeshPrefab", flags);
                var planeMethod = typeof(SpatialStudioController).GetMethod("CreateSpatialPlanePrefab", flags);
                Assert.That(meshMethod, Is.Not.Null);
                Assert.That(planeMethod, Is.Not.Null);

                var mesh = (MeshFilter)meshMethod.Invoke(controller, null);
                meshPrefab = mesh.gameObject;
                planePrefab = (GameObject)planeMethod.Invoke(controller, null);

                Assert.That(meshPrefab.activeSelf, Is.True, "Mesh trackables must not inherit an inactive template state.");
                Assert.That(meshPrefab.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(planePrefab.activeSelf, Is.True, "Plane trackables must be visible when the provider adds them.");
                Assert.That(planePrefab.GetComponent("ARPlaneMeshVisualizer"), Is.Not.Null,
                    "Tracked plane polygons need a renderer for visible scan feedback.");
            }
            finally
            {
                if (meshPrefab != null) UnityEngine.Object.DestroyImmediate(meshPrefab);
                if (planePrefab != null) UnityEngine.Object.DestroyImmediate(planePrefab);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ServiceRegistryRegistersService()
        {
            var registry = CreateRuntimeInstance("ServiceRegistry");
            var service = CreateRuntimeInstance("ChipOSEventBus");

            var result = InvokeGeneric(registry, "Register", service.GetType(), service);

            Assert.That(result, Is.EqualTo(true));
            Assert.That(GetPropertyValue<int>(registry, "Count"), Is.EqualTo(1));
            Assert.That(InvokeGeneric(registry, "Contains", service.GetType()), Is.EqualTo(true));
        }

        [Test]
        public void ServiceRegistryPreventsDuplicateServiceRegistration()
        {
            var registry = CreateRuntimeInstance("ServiceRegistry");
            var primary = CreateRuntimeInstance("ChipOSEventBus");
            var duplicate = CreateRuntimeInstance("ChipOSEventBus");

            Assert.That(InvokeGeneric(registry, "Register", primary.GetType(), primary), Is.EqualTo(true));

            var duplicateResult = InvokeGeneric(registry, "Register", duplicate.GetType(), duplicate);
            var resolved = InvokeGeneric(registry, "Get", primary.GetType());

            Assert.That(duplicateResult, Is.EqualTo(false));
            Assert.That(GetPropertyValue<int>(registry, "Count"), Is.EqualTo(1));
            Assert.That(resolved, Is.SameAs(primary));
        }

        [Test]
        public void ServiceRegistryUnregistersService()
        {
            var registry = CreateRuntimeInstance("ServiceRegistry");
            var service = CreateRuntimeInstance("ChipOSEventBus");
            InvokeGeneric(registry, "Register", service.GetType(), service);

            var result = InvokeGeneric(registry, "Unregister", service.GetType());

            Assert.That(result, Is.EqualTo(true));
            Assert.That(GetPropertyValue<int>(registry, "Count"), Is.EqualTo(0));
            Assert.That(InvokeGeneric(registry, "Contains", service.GetType()), Is.EqualTo(false));
        }

        [Test]
        public void ServiceRegistryTryGetReturnsExpectedService()
        {
            var registry = CreateRuntimeInstance("ServiceRegistry");
            var service = CreateRuntimeInstance("ChipOSEventBus");
            InvokeGeneric(registry, "Register", service.GetType(), service);

            var arguments = new object[] { null };
            var found = InvokeGenericWithArguments(registry, "TryGet", service.GetType(), arguments);

            Assert.That(found, Is.EqualTo(true));
            Assert.That(arguments[0], Is.SameAs(service));
        }

        [Test]
        public void ServiceRegistryGetReturnsNullSafelyForMissingService()
        {
            var registry = CreateRuntimeInstance("ServiceRegistry");
            var missingType = GetRuntimeType("ChipOSNotificationService");

            var resolved = InvokeGeneric(registry, "Get", missingType);

            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void EventBusSubscribesAndReceivesTypedEvent()
        {
            var bus = CreateRuntimeInstance("ChipOSEventBus");
            var received = 0;
            Action<DummyEvent> listener = _ => received++;

            InvokeGeneric(bus, "Subscribe", typeof(DummyEvent), listener);
            InvokeGeneric(bus, "Publish", typeof(DummyEvent), new DummyEvent(7));

            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void EventBusUnsubscribePreventsFutureCalls()
        {
            var bus = CreateRuntimeInstance("ChipOSEventBus");
            var received = 0;
            Action<DummyEvent> listener = _ => received++;

            InvokeGeneric(bus, "Subscribe", typeof(DummyEvent), listener);
            InvokeGeneric(bus, "Unsubscribe", typeof(DummyEvent), listener);
            InvokeGeneric(bus, "Publish", typeof(DummyEvent), new DummyEvent(7));

            Assert.That(received, Is.EqualTo(0));
        }

        [Test]
        public void EventBusPublishIncrementsEventCount()
        {
            var bus = CreateRuntimeInstance("ChipOSEventBus");

            InvokeGeneric(bus, "Publish", typeof(DummyEvent), new DummyEvent(1));
            InvokeGeneric(bus, "Publish", typeof(DummyEvent), new DummyEvent(2));

            Assert.That(GetPropertyValue<int>(bus, "PublishedEventCount"), Is.EqualTo(2));
        }

        [Test]
        public void EventBusTracingRecordsRecentEventsWhenEnabled()
        {
            var bus = CreateRuntimeInstance("ChipOSEventBus");
            Invoke(bus, "SetTracingEnabled", true);
            InvokeGeneric(bus, "Publish", typeof(DummyEvent), new DummyEvent(42));

            var snapshot = (IEnumerable)Invoke(bus, "GetTraceSnapshot");
            var entries = snapshot.Cast<object>().ToList();
            var summary = GetFieldValue<string>(entries[0], "summary");

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(summary, Does.Contain("DummyEvent"));
        }

        [Test]
        public void EventBusNullListenerSubscribeUnsubscribeDoesNotThrow()
        {
            var bus = CreateRuntimeInstance("ChipOSEventBus");

            Assert.DoesNotThrow(() => InvokeGeneric(bus, "Subscribe", typeof(DummyEvent), new object[] { null }));
            Assert.DoesNotThrow(() => InvokeGeneric(bus, "Unsubscribe", typeof(DummyEvent), new object[] { null }));
        }

        [Test]
        public void NotificationServiceQueuesNotification()
        {
            var service = CreateRuntimeInstance("ChipOSNotificationService");

            Invoke(service, "Enqueue", "Primary", GetEnumValue("ChipOSNotificationPriority", "Low"), 5f, "Test");
            Invoke(service, "Enqueue", "Queued", GetEnumValue("ChipOSNotificationPriority", "Low"), 5f, "Test");

            Assert.That(GetPropertyValue<bool>(service, "HasActiveNotification"), Is.True);
            Assert.That(GetPropertyValue<int>(service, "PendingCount"), Is.EqualTo(1));
        }

        [Test]
        public void NotificationServiceActivatesFirstNotification()
        {
            var service = CreateRuntimeInstance("ChipOSNotificationService");

            Invoke(service, "Enqueue", "Primary", GetEnumValue("ChipOSNotificationPriority", "Normal"), 5f, "Test");

            var activeNotification = GetPropertyValue<object>(service, "ActiveNotification");
            Assert.That(GetPropertyValue<bool>(service, "HasActiveNotification"), Is.True);
            Assert.That(GetFieldValue<string>(activeNotification, "message"), Is.EqualTo("Primary"));
        }

        [Test]
        public void NotificationServicePriorityNotificationPreemptsLowerPriorityNotification()
        {
            var service = CreateRuntimeInstance("ChipOSNotificationService");
            Invoke(service, "Enqueue", "Low", GetEnumValue("ChipOSNotificationPriority", "Low"), 5f, "Test");
            Invoke(service, "Enqueue", "Critical", GetEnumValue("ChipOSNotificationPriority", "Critical"), 5f, "Test");

            var activeNotification = GetPropertyValue<object>(service, "ActiveNotification");
            Assert.That(GetFieldValue<string>(activeNotification, "message"), Is.EqualTo("Critical"));
            Assert.That(GetPropertyValue<int>(service, "PendingCount"), Is.EqualTo(1));
        }

        [Test]
        public void NotificationServiceNotificationExpiresAfterTick()
        {
            var service = CreateRuntimeInstance("ChipOSNotificationService");
            Invoke(service, "Enqueue", "Short", GetEnumValue("ChipOSNotificationPriority", "Normal"), 0.25f, "Test");

            Invoke(service, "Tick", 0.3f);

            Assert.That(GetPropertyValue<bool>(service, "HasActiveNotification"), Is.False);
        }

        [Test]
        public void NotificationServiceHistoryIsCappedSafely()
        {
            var service = CreateRuntimeInstance("ChipOSNotificationService");

            for (var i = 0; i < 16; i++)
            {
                Invoke(service, "Enqueue", $"Message {i}", GetEnumValue("ChipOSNotificationPriority", "Low"), 0.25f, "Test");
                Invoke(service, "Tick", 0.3f);
            }

            var history = (IEnumerable)GetPropertyValue<object>(service, "History");
            Assert.That(history.Cast<object>().Count(), Is.LessThanOrEqualTo(12));
        }

        [Test]
        public void RuntimeStateSanitizesInvalidState()
        {
            var state = CreateRuntimeInstance("ChipOSRuntimeState");
            SetFieldValue(state, "layoutMode", "   ");
            SetFieldValue(state, "activeTaskTitle", string.Empty);
            SetFieldValue(state, "activeTaskIndex", -9);
            SetFieldValue(state, "savedAtUtc", string.Empty);

            Invoke(state, "Sanitize");

            Assert.That(GetFieldValue<string>(state, "layoutMode"), Is.EqualTo("Default"));
            Assert.That(GetFieldValue<string>(state, "activeTaskTitle"), Is.EqualTo("System boot"));
            Assert.That(GetFieldValue<int>(state, "activeTaskIndex"), Is.EqualTo(0));
            Assert.That(GetFieldValue<string>(state, "savedAtUtc"), Is.Not.Empty);
        }

        [Test]
        public void RuntimeStateSerializesAndDeserializesWithJsonUtility()
        {
            var runtimeStateType = GetRuntimeType("ChipOSRuntimeState");
            var original = Activator.CreateInstance(runtimeStateType);
            SetFieldValue(original, "layoutMode", "Focus");
            SetFieldValue(original, "hudVisible", false);
            SetFieldValue(original, "activeTaskTitle", "Verify runtime");
            SetFieldValue(original, "activeTaskIndex", 2);
            SetFieldValue(original, "debugEventTracingEnabled", true);
            SetFieldValue(original, "verboseServiceStatusEnabled", false);
            SetFieldValue(original, "savedAtUtc", "2026-05-28T01:02:03.0000000Z");

            var json = JsonUtility.ToJson(original);
            var fromJson = typeof(JsonUtility)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(method => method.Name == "FromJson" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1)
                .MakeGenericMethod(runtimeStateType);
            var clone = fromJson.Invoke(null, new object[] { json });

            Assert.That(clone, Is.Not.Null);
            Assert.That(GetFieldValue<string>(clone, "layoutMode"), Is.EqualTo("Focus"));
            Assert.That(GetFieldValue<bool>(clone, "hudVisible"), Is.False);
            Assert.That(GetFieldValue<string>(clone, "activeTaskTitle"), Is.EqualTo("Verify runtime"));
            Assert.That(GetFieldValue<int>(clone, "activeTaskIndex"), Is.EqualTo(2));
            Assert.That(GetFieldValue<bool>(clone, "debugEventTracingEnabled"), Is.True);
        }

        [Test]
        public void RuntimeStateRefusesInvalidPanicLayoutRestore()
        {
            var state = CreateRuntimeInstance("ChipOSRuntimeState");
            SetFieldValue(state, "layoutMode", "Panic");

            var arguments = new object[] { null };
            var canRestore = InvokeWithArguments(state, "TryGetRestorableLayoutMode", arguments);

            Assert.That(canRestore, Is.EqualTo(false));
            Assert.That(arguments[0]?.ToString(), Is.EqualTo("Default"));
        }

        [Test]
        public void SimulatedDeviceServiceDefaultSnapshotIsSafe()
        {
            var service = CreateRuntimeInstance("SimulatedChipOSDeviceService");
            var snapshot = GetPropertyValue<object>(service, "CurrentSnapshot");

            Assert.That(GetFieldValue<string>(snapshot, "deviceName"), Is.Not.Empty);
            Assert.That(GetFieldValue<object>(snapshot, "deviceMode").ToString(), Is.EqualTo("EditorSimulation"));
            Assert.That(GetFieldValue<int>(snapshot, "batteryLevel"), Is.EqualTo(100));
            Assert.That(GetFieldValue<object>(snapshot, "trackingState").ToString(), Is.EqualTo("Tracking"));
            Assert.That(GetFieldValue<object>(snapshot, "userContext").ToString(), Is.EqualTo("Desk"));
            Assert.That(GetFieldValue<bool>(snapshot, "comfortWarning"), Is.False);
        }

        [Test]
        public void SimulatedDeviceContextChangesPublishCorrectState()
        {
            ResetCapturedRuntimeEvents();
            var graph = CreateBoundRuntimeGraph();

            try
            {
                var bus = GetPropertyValue<object>(graph.runtime, "EventBus");
                var eventType = GetRuntimeType("ChipOSDeviceContextChangedEvent");
                var listener = CreateRuntimeEventListener(eventType);
                InvokeGeneric(bus, "Subscribe", eventType, listener);

                var deviceService = GetPropertyValue<object>(graph.runtime, "SimulatedDeviceService");
                Invoke(deviceService, "SetContext", GetEnumValue("ChipOSDeviceContext", "Walking"));

                Assert.That(capturedRuntimeEventCount, Is.EqualTo(1));
                Assert.That(GetFieldValue<object>(capturedRuntimeEvent, "currentContext").ToString(), Is.EqualTo("Walking"));

                InvokeGeneric(bus, "Unsubscribe", eventType, listener);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph.root);
            }
        }

        [Test]
        public void SimulatedDeviceBatteryClampsBetweenZeroAndOneHundred()
        {
            var service = CreateRuntimeInstance("SimulatedChipOSDeviceService");

            Invoke(service, "AdjustBattery", -200);
            var snapshot = GetPropertyValue<object>(service, "CurrentSnapshot");
            Assert.That(GetFieldValue<int>(snapshot, "batteryLevel"), Is.EqualTo(0));

            Invoke(service, "AdjustBattery", 250);
            snapshot = GetPropertyValue<object>(service, "CurrentSnapshot");
            Assert.That(GetFieldValue<int>(snapshot, "batteryLevel"), Is.EqualTo(100));
        }

        [Test]
        public void SimulatedDeviceTrackingCyclesCorrectly()
        {
            var service = CreateRuntimeInstance("SimulatedChipOSDeviceService");

            var state = Invoke(service, "CycleTrackingState");
            Assert.That(state.ToString(), Is.EqualTo("Lost"));

            state = Invoke(service, "CycleTrackingState");
            Assert.That(state.ToString(), Is.EqualTo("Unavailable"));

            state = Invoke(service, "CycleTrackingState");
            Assert.That(state.ToString(), Is.EqualTo("Limited"));

            state = Invoke(service, "CycleTrackingState");
            Assert.That(state.ToString(), Is.EqualTo("Tracking"));
        }

        [Test]
        public void SimulatedDeviceComfortWarningTogglesCorrectly()
        {
            var service = CreateRuntimeInstance("SimulatedChipOSDeviceService");

            Invoke(service, "ToggleComfortWarning");
            var snapshot = GetPropertyValue<object>(service, "CurrentSnapshot");
            Assert.That(GetFieldValue<bool>(snapshot, "comfortWarning"), Is.True);

            Invoke(service, "ToggleComfortWarning");
            snapshot = GetPropertyValue<object>(service, "CurrentSnapshot");
            Assert.That(GetFieldValue<bool>(snapshot, "comfortWarning"), Is.False);
        }

        [Test]
        public void SimulatedDeviceResetReturnsSafeDefaults()
        {
            var service = CreateRuntimeInstance("SimulatedChipOSDeviceService");
            Invoke(service, "SetContext", GetEnumValue("ChipOSDeviceContext", "Walking"));
            Invoke(service, "AdjustBattery", -60);
            Invoke(service, "SetCharging", true);
            Invoke(service, "CycleTrackingState");
            Invoke(service, "ToggleComfortWarning");
            Invoke(service, "AdjustHeadPose", 12f, -8f);

            Invoke(service, "ResetState");
            var snapshot = GetPropertyValue<object>(service, "CurrentSnapshot");
            var pose = GetFieldValue<object>(snapshot, "headPose");

            Assert.That(GetFieldValue<object>(snapshot, "userContext").ToString(), Is.EqualTo("Desk"));
            Assert.That(GetFieldValue<int>(snapshot, "batteryLevel"), Is.EqualTo(100));
            Assert.That(GetFieldValue<bool>(snapshot, "isCharging"), Is.False);
            Assert.That(GetFieldValue<object>(snapshot, "trackingState").ToString(), Is.EqualTo("Tracking"));
            Assert.That(GetFieldValue<bool>(snapshot, "comfortWarning"), Is.False);
            Assert.That(GetFieldValue<Vector3>(pose, "eulerAngles"), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void SimulatedDeviceServiceCanBeRegisteredAndQueriedThroughServiceRegistry()
        {
            var registry = CreateRuntimeInstance("ServiceRegistry");
            var service = CreateRuntimeInstance("SimulatedChipOSDeviceService");
            var interfaceType = GetRuntimeType("IChipOSDeviceService");

            var registered = InvokeGeneric(registry, "Register", interfaceType, service);
            var resolved = InvokeGeneric(registry, "Get", interfaceType);

            Assert.That(registered, Is.EqualTo(true));
            Assert.That(resolved, Is.SameAs(service));
        }

        [Test]
        public void AndroidBuildProfileDefaultsAreSafe()
        {
            var profile = CreateRuntimeInstance("ChipOSAndroidBuildProfile");
            var packageName = GetFieldValue<string>(profile, "PackageName");

            Assert.That(packageName, Is.EqualTo("com.walkermejames.chiposmarkii"));
            Assert.That(packageName, Does.Match(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$"));
            Assert.That(GetFieldValue<bool>(profile, "UseIL2CPP"), Is.True);
            Assert.That(GetFieldValue<bool>(profile, "UseARM64"), Is.True);
            Assert.That(GetFieldValue<bool>(profile, "InternetPermissionRequired"), Is.False);
            Assert.That(GetFieldValue<bool>(profile, "CameraPermissionRequired"), Is.True);
            Assert.That(GetFieldValue<bool>(profile, "MicrophonePermissionRequired"), Is.True);
        }

        [Test]
        public void XRReadinessReportCompactSummaryIsNotEmpty()
        {
            var report = CreateRuntimeInstance("ChipOSXRReadinessReport");
            SetFieldValue(report, "OverallStatus", GetEnumValue("ChipOSBuildReadinessStatus", "Warning"));
            SetFieldValue(report, "UnityVersion", "6000.4.8f1");
            SetFieldValue(report, "ActiveBuildTarget", "StandaloneWindows64");
            SetFieldValue(report, "SimulatorFallbackAvailable", true);
            SetFieldValue(report, "EditorSimulationReadyStatus", GetEnumValue("ChipOSBuildReadinessStatus", "Pass"));
            SetFieldValue(report, "AndroidPhoneSimulatorBuildReadyStatus", GetEnumValue("ChipOSBuildReadinessStatus", "Warning"));
            SetFieldValue(report, "RealXrealSdkReadyStatus", GetEnumValue("ChipOSBuildReadinessStatus", "Warning"));

            var summary = (string)Invoke(report, "GetCompactSummary");
            Assert.That(summary, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void AndroidReadinessCheckRunsWithoutThrowing()
        {
            var readinessType = GetRuntimeType("ChipOSBuildReadinessCheck");
            var runCheck = readinessType.GetMethod("RunCheck", BindingFlags.Public | BindingFlags.Static);
            Assert.That(runCheck, Is.Not.Null);

            Assert.DoesNotThrow(() => runCheck.Invoke(null, null));
        }

        [Test]
        public void AndroidReadinessCheckKeepsMissingRealSdkAsWarning()
        {
            var report = InvokeStatic("ChipOSBuildReadinessCheck", "RunCheck");

            Assert.That(GetFieldValue<object>(report, "RealXrealSdkReadyStatus").ToString(), Is.EqualTo("Warning"));
            Assert.That(GetFieldValue<bool>(report, "XrealSdkInstalled"), Is.False);

            var warnings = (string[])GetFieldValue<object>(report, "Warnings");
            Assert.That(warnings, Is.Not.Null);
            Assert.That(warnings.Any(entry => entry.Contains("CHIPOS_XREAL", StringComparison.Ordinal)), Is.True);
            Assert.That(warnings.Any(entry => entry.Contains("CHIPOS_NRSDK", StringComparison.Ordinal)), Is.True);
            Assert.That(warnings.Any(entry => entry.Contains("CHIPOS_OPENXR", StringComparison.Ordinal)), Is.True);

            var failures = (string[])GetFieldValue<object>(report, "Failures");
            Assert.That(failures.Any(entry => entry.Contains("CHIPOS_XREAL", StringComparison.Ordinal)), Is.False);
            Assert.That(failures.Any(entry => entry.Contains("CHIPOS_NRSDK", StringComparison.Ordinal)), Is.False);
            Assert.That(failures.Any(entry => entry.Contains("CHIPOS_OPENXR", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void AndroidReadinessCheckReportsMainScenePath()
        {
            var report = InvokeStatic("ChipOSBuildReadinessCheck", "RunCheck");

            Assert.That(GetFieldValue<string>(report, "MainScenePath"), Is.EqualTo("Assets/ChipOSMarkII/Scenes/ChipOSMarkII_Main.unity"));
            Assert.That(GetFieldValue<bool>(report, "MainSceneExists"), Is.True);
        }

        [Test]
        public void AndroidBuildReportTemplateExists()
        {
            var path = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "CHIPOS_ANDROID_BUILD_REPORT_TEMPLATE.md");
            Assert.That(File.Exists(path), Is.True, $"Expected report template at '{path}'.");
        }

        private static Assembly GetRuntimeAssembly()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assembly = assemblies.FirstOrDefault(candidate => candidate.GetName().Name == "ChipOSMarkII.Runtime")
                ?? assemblies.FirstOrDefault(candidate => candidate.GetName().Name == "Assembly-CSharp");
            Assert.That(assembly, Is.Not.Null, "ChipOSMarkII.Runtime or Assembly-CSharp must be loaded for reflection-based runtime tests.");
            return assembly;
        }

        private static Type GetRuntimeType(string typeName)
        {
            var fullName = $"ChipOSMarkII.{typeName}";
            var runtimeType = GetRuntimeAssembly().GetType(fullName, false);
            Assert.That(runtimeType, Is.Not.Null, $"Could not resolve runtime type '{fullName}'.");
            return runtimeType;
        }

        private static object CreateRuntimeInstance(string typeName)
        {
            return Activator.CreateInstance(GetRuntimeType(typeName));
        }

        private static object InvokeStatic(string typeName, string methodName, params object[] arguments)
        {
            var runtimeType = GetRuntimeType(typeName);
            var method = runtimeType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, $"Missing static method '{methodName}' on {runtimeType.Name}.");
            return method.Invoke(null, arguments);
        }

        private (GameObject root, object app, object runtime) CreateBoundRuntimeGraph()
        {
            var root = new GameObject("ChipOSRuntimeGraph");
            var app = root.AddComponent(GetRuntimeType("ChipOSApp"));
            var runtime = root.AddComponent(GetRuntimeType("ChipOSRuntime"));
            var layoutManager = root.AddComponent(GetRuntimeType("LayoutManager"));
            var inputRouter = root.AddComponent(GetRuntimeType("InputRouter"));

            Invoke(layoutManager, "Bind", app);
            Invoke(inputRouter, "Bind", app);
            Invoke(runtime, "Bind", app, layoutManager, inputRouter);
            return (root, app, runtime);
        }

        private static object GetEnumValue(string typeName, string memberName)
        {
            return Enum.Parse(GetRuntimeType(typeName), memberName);
        }

        private static T GetPropertyValue<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing property '{propertyName}' on {target.GetType().Name}.");
            return (T)property.GetValue(target, null);
        }

        private static T GetFieldValue<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static void SetFieldValue(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = FindBestMethod(target.GetType(), methodName, arguments);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on {target.GetType().Name}.");
            return method.Invoke(target, arguments);
        }

        private static object InvokeWithArguments(object target, string methodName, object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on {target.GetType().Name}.");
            return method.Invoke(target, arguments);
        }

        private static object InvokeGeneric(object target, string methodName, Type genericType, params object[] arguments)
        {
            var method = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .First(candidate => candidate.Name == methodName && candidate.IsGenericMethodDefinition && candidate.GetGenericArguments().Length == 1 && candidate.GetParameters().Length == arguments.Length);
            return method.MakeGenericMethod(genericType).Invoke(target, arguments);
        }

        private static object InvokeGenericWithArguments(object target, string methodName, Type genericType, object[] arguments)
        {
            var method = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .First(candidate => candidate.Name == methodName && candidate.IsGenericMethodDefinition && candidate.GetGenericArguments().Length == 1 && candidate.GetParameters().Length == arguments.Length);
            return method.MakeGenericMethod(genericType).Invoke(target, arguments);
        }

        private static MethodInfo FindBestMethod(Type targetType, string methodName, object[] arguments)
        {
            return targetType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != methodName)
                    {
                        return false;
                    }

                    var parameters = candidate.GetParameters();
                    if (parameters.Length != arguments.Length)
                    {
                        return false;
                    }

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (arguments[i] == null)
                        {
                            continue;
                        }

                        if (!parameters[i].ParameterType.IsInstanceOfType(arguments[i]) &&
                            parameters[i].ParameterType != arguments[i].GetType())
                        {
                            return false;
                        }
                    }

                    return true;
                });
        }

        private Delegate CreateRuntimeEventListener(Type eventType)
        {
            var captureMethod = GetType()
                .GetMethod(nameof(CaptureRuntimeEvent), BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(eventType);
            var delegateType = typeof(Action<>).MakeGenericType(eventType);
            return Delegate.CreateDelegate(delegateType, this, captureMethod);
        }

        private void ResetCapturedRuntimeEvents()
        {
            capturedRuntimeEvent = null;
            capturedRuntimeEventCount = 0;
        }

        private void CaptureRuntimeEvent<TEvent>(TEvent eventData)
        {
            capturedRuntimeEvent = eventData;
            capturedRuntimeEventCount++;
        }
    }

    internal readonly struct DummyEvent
    {
        public DummyEvent(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public override string ToString()
        {
            return $"DummyEvent:{Value}";
        }
    }
}
