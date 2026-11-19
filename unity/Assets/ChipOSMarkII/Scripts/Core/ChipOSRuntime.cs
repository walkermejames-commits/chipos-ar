using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Internal runtime kernel with XR Stability Harness.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class ChipOSRuntime : MonoBehaviour, IChipOSService
    {
        private const string RuntimeStatePrefKey = "ChipOSMarkII.RuntimeState";
        private const float AudioFrameEventPublishIntervalSeconds = 1f;

        [Header("Persistence")]
        [SerializeField] private bool restoreStateOnStartup = true;
        [SerializeField] private bool saveStateAutomatically = true;
        [SerializeField] [Min(0.25f)] private float autoSaveIntervalSeconds = 0.75f;

        [Header("XR Adapter")]
        [SerializeField] private XRAdapterConfiguration xrConfiguration = XRAdapterConfiguration.Default;

        [Header("Debug")]
        [SerializeField] private bool eventTracingEnabled;
        [SerializeField] private bool verboseServiceStatus = true;
        [SerializeField] [TextArea(3, 6)] private string inspectorStatus = "ChipOSRuntime awaiting bind.";

        private readonly ServiceRegistry serviceRegistry = new ServiceRegistry();
        private readonly ChipOSEventBus eventBus = new ChipOSEventBus();
        private readonly ChipOSNotificationService notificationService = new ChipOSNotificationService();
        private readonly SimulatedChipOSDeviceService simulatedDeviceService = new SimulatedChipOSDeviceService();
        private readonly SimulatedAudioSource simulatedAudioSource = new SimulatedAudioSource();
        private readonly ChipOSCaptionService captionService = new ChipOSCaptionService();

        // XR Stability Harness
        private IChipOSXRAdapter xrAdapter;
        private XRAdapterDeviceBridge xrAdapterBridge;
        private XRAdapterDiagnostics xrDiagnostics;

        private ChipOSApp app;
        private LayoutManager layoutManager;
        private InputRouter inputRouter;
        private ChipOSContextPolicyService contextPolicyService;
        private ChipOSRuntimeState restoredState;
        private bool initialized;
        private bool deviceServiceSubscribed;
        private bool audioSourceSubscribed;
        private bool runtimeOnlineLogged;
        private bool stateLoaded;
        private bool stateDirty;
        private float nextSaveTime;
        private float startedAtRealtime;
        private float smoothedDeltaTime = 0.016f;
        private float lastDiagnosticsRefreshTime;
        private float lastAudioFrameEventPublishTime = -999f;

        public string ServiceName => "ChipOS Runtime";
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.Runtime;
        public bool IsServiceReady => app != null && layoutManager != null && inputRouter != null;
        public ServiceRegistry Services => serviceRegistry;
        public ChipOSEventBus EventBus => eventBus;
        public ChipOSNotificationService NotificationService => notificationService;
        public SimulatedChipOSDeviceService SimulatedDeviceService => simulatedDeviceService;
        public IChipOSDeviceService DeviceService => serviceRegistry.Get<IChipOSDeviceService>() ?? simulatedDeviceService;
        public IChipOSAudioSource AudioSource => serviceRegistry.Get<IChipOSAudioSource>() ?? simulatedAudioSource;
        public ChipOSCaptionService CaptionService => captionService;
        public ChipOSContextPolicyService ContextPolicyService => contextPolicyService;

        // XR Stability Harness exposure
        public IChipOSXRAdapter XRAdapter => xrAdapter;
        public XRAdapterDeviceBridge XRAdapterBridge => xrAdapterBridge;
        public XRAdapterDiagnostics XRDiagnostics => xrDiagnostics;

        public bool EventTracingEnabled => eventTracingEnabled;
        public bool VerboseServiceStatus => verboseServiceStatus;
        public int PublishedEventCount => eventBus.PublishedEventCount;
        public float UptimeSeconds => Mathf.Max(0f, Time.unscaledTime - startedAtRealtime);
        public float SmoothedFps => smoothedDeltaTime > 0.0001f ? 1f / smoothedDeltaTime : 0f;
        public string InputMode => inputRouter != null ? inputRouter.CurrentInputSource.ToString() : ChipInputSourceKind.Unknown.ToString();
        public string InspectorStatus => inspectorStatus;

        public string GetServiceStatus()
        {
            return $"Ready {IsServiceReady} | Services {serviceRegistry.Count} | Events {PublishedEventCount}";
        }

        public ChipOSDeviceSnapshot GetDeviceSnapshot()
        {
            return DeviceService != null ? DeviceService.CurrentSnapshot : null;
        }

        /// <summary>
        /// Refreshes XRDiagnostics from live adapter and bridge state.
        /// Safe to call frequently (low cost).
        /// </summary>
        public void RefreshXRDiagnostics()
        {
            if (xrDiagnostics == null) return;

            if (xrAdapter != null)
            {
                xrDiagnostics.SelectedAdapterName = xrAdapter.ServiceName;
                xrDiagnostics.AdapterState = xrAdapter.State;

                // Preserve factory-selected kind and fallback info
                // (these are set once at creation and rarely change)
            }

            if (xrAdapterBridge != null)
            {
                xrDiagnostics.BridgeConnected = xrAdapterBridge.IsConnected;
                xrDiagnostics.FramesAppliedCount = xrAdapterBridge.FramesAppliedCount;
            }

            if (xrAdapter == null || xrAdapterBridge == null)
            {
                xrDiagnostics.LatestError = "XR adapter or bridge not available";
            }

            lastDiagnosticsRefreshTime = Time.unscaledTime;
        }

        private void Awake()
        {
            EnsureInitialized();
            UpdateInspectorStatus();
        }

        private void Update()
        {
            smoothedDeltaTime += (Time.unscaledDeltaTime - smoothedDeltaTime) * 0.12f;
            notificationService.Tick(Time.unscaledDeltaTime);

            xrAdapter?.Tick(Time.unscaledDeltaTime);
            AudioSource?.Tick(Time.unscaledDeltaTime);

            // Low-cost diagnostics refresh (~every 0.5s)
            if (Time.unscaledTime - lastDiagnosticsRefreshTime > 0.5f)
            {
                RefreshXRDiagnostics();
            }

            if (!saveStateAutomatically || !stateDirty || Time.unscaledTime < nextSaveTime)
            {
                return;
            }

            SaveRuntimeState();
        }

        private void OnDisable()
        {
            if (saveStateAutomatically) SaveRuntimeState();
            xrAdapter?.StopAdapter();
            AudioSource?.StopSource();
        }

        private void OnApplicationQuit()
        {
            if (saveStateAutomatically) SaveRuntimeState();
            xrAdapter?.StopAdapter();
            AudioSource?.StopSource();
        }

        public void Bind(ChipOSApp owner, LayoutManager layout, InputRouter input)
        {
            EnsureInitialized();
            app = owner;
            layoutManager = layout;
            inputRouter = input;

            RegisterOrReplaceService<ChipOSRuntime>(this);
            RegisterOrReplaceService<ChipOSEventBus>(eventBus);
            RegisterOrReplaceService<ChipOSNotificationService>(notificationService);
            RegisterOrReplaceService<SimulatedChipOSDeviceService>(simulatedDeviceService);
            RegisterOrReplaceService<IChipOSDeviceService>(simulatedDeviceService);
            RegisterOrReplaceService<SimulatedAudioSource>(simulatedAudioSource);
            RegisterOrReplaceService<IChipOSAudioSource>(simulatedAudioSource);

            if (app != null) RegisterOrReplaceService<ChipOSApp>(app);
            if (layoutManager != null) RegisterOrReplaceService<LayoutManager>(layoutManager);
            if (inputRouter != null) RegisterOrReplaceService<InputRouter>(inputRouter);

            // Context Policy
            contextPolicyService = new ChipOSContextPolicyService(DeviceService, layoutManager, notificationService, eventBus);
            RegisterOrReplaceService<ChipOSContextPolicyService>(contextPolicyService);

            captionService.Bind(simulatedAudioSource, eventBus);
            RegisterOrReplaceService<ChipOSCaptionService>(captionService);
            SubscribeToAudioSource();

            // === XR Stability Harness ===
            xrDiagnostics = new XRAdapterDiagnostics();
            xrAdapter = XRAdapterFactory.CreateAdapter(xrConfiguration, out xrDiagnostics);

            if (xrAdapter != null)
            {
                RegisterOrReplaceService<IChipOSXRAdapter>(xrAdapter);

                if (xrAdapter is SimulatedXRAdapter sim)
                    RegisterOrReplaceService<SimulatedXRAdapter>(sim);
                else if (xrAdapter is UnavailableRealXRAdapter unavailable)
                    RegisterOrReplaceService<UnavailableRealXRAdapter>(unavailable);
            }

            xrAdapterBridge = new XRAdapterDeviceBridge(xrAdapter, DeviceService);

            // Initial diagnostics refresh
            RefreshXRDiagnostics();

            // Start adapter
            xrAdapter?.StartAdapter();
            simulatedAudioSource.StartSource();
            RefreshXRDiagnostics();

            app?.SynchronizeDeviceSnapshot(simulatedDeviceService.CurrentSnapshot);
            LogRuntimeOnlineIfReady();
            UpdateInspectorStatus();
        }

        // ... rest of methods unchanged ...

        public bool ResolveStartupHudVisibility(bool fallbackValue)
        {
            return stateLoaded && restoredState != null ? restoredState.hudVisible : fallbackValue;
        }

        public void RestoreTaskSelection(ChipTaskBoard taskBoard)
        {
            if (!stateLoaded || restoredState == null || taskBoard == null) return;
            taskBoard.RestoreSelection(restoredState.activeTaskIndex, restoredState.activeTaskTitle);
        }

        public bool TryGetRestoredLayoutMode(out LayoutMode mode)
        {
            mode = LayoutMode.Default;
            if (!stateLoaded || restoredState == null) return false;
            return restoredState.TryGetRestorableLayoutMode(out mode);
        }

        public void Publish<TEvent>(TEvent eventData) => eventBus.Publish(eventData);

        public void QueueNotification(string message, ChipOSNotificationPriority priority, float durationSeconds, string source)
        {
            notificationService.Enqueue(message, priority, durationSeconds, source);
            MarkStateDirty();
        }

        public void SetEventTracingEnabled(bool enabled)
        {
            if (eventTracingEnabled == enabled) return;
            eventTracingEnabled = enabled;
            eventBus.SetTracingEnabled(enabled);
            eventBus.Publish(new ChipOSDebugSettingsChangedEvent(eventTracingEnabled, verboseServiceStatus));
            MarkStateDirty();
            UpdateInspectorStatus();
        }

        public void SetVerboseServiceStatus(bool enabled)
        {
            if (verboseServiceStatus == enabled) return;
            verboseServiceStatus = enabled;
            eventBus.Publish(new ChipOSDebugSettingsChangedEvent(eventTracingEnabled, verboseServiceStatus));
            MarkStateDirty();
            UpdateInspectorStatus();
        }

        public bool ToggleSimulatedAudioSource()
        {
            var source = AudioSource;
            if (source == null)
            {
                return false;
            }

            if (source.State == ChipOSAudioSourceState.Running || source.State == ChipOSAudioSourceState.Muted)
            {
                source.StopSource();
                return false;
            }

            source.StartSource();
            return true;
        }

        public bool ToggleSimulatedSpeechMode()
        {
            return simulatedAudioSource.ToggleSpeechSimulation();
        }

        public ChipOSCaptionEvent EmitManualMockCaption(string text = null)
        {
            return captionService.EmitManualMockCaption(text);
        }

        public void MarkStateDirty()
        {
            stateDirty = true;
            nextSaveTime = Time.unscaledTime + autoSaveIntervalSeconds;
            UpdateInspectorStatus();
        }

        public string GetServiceReport()
        {
            if (serviceRegistry.Count == 0) return "None";

            var report = new List<string>(serviceRegistry.Count);
            var services = serviceRegistry.RegisteredServices;
            for (var i = 0; i < services.Count; i++)
            {
                if (services[i] == null) continue;
                report.Add(verboseServiceStatus
                    ? $"{services[i].ServiceName} [{services[i].ServiceLayer}] {(services[i].IsServiceReady ? "Ready" : "Waiting")} // {services[i].GetServiceStatus()}"
                    : $"{services[i].ServiceName}:{(services[i].IsServiceReady ? "Ready" : "Wait")}");
            }
            return report.Count == 0 ? "None" : string.Join(" | ", report.ToArray());
        }

        public string GetEventTraceReport(int maxEntries)
        {
            var trace = eventBus.GetTraceSnapshot();
            if (!eventTracingEnabled) return "Tracing off";
            if (trace.Count == 0) return "No traced events";

            var startIndex = trace.Count > maxEntries ? trace.Count - maxEntries : 0;
            var entries = new List<string>(trace.Count - startIndex);
            for (var i = startIndex; i < trace.Count; i++) entries.Add(trace[i].ToString());
            return string.Join(" | ", entries.ToArray());
        }

        public string GetActiveNotificationSummary()
        {
            return notificationService.HasActiveNotification ? notificationService.ActiveNotification.ToString() : "None";
        }

        public string GetDeviceStatusSummary()
        {
            var snapshot = GetDeviceSnapshot();
            return snapshot != null ? snapshot.GetCompactSummary() : "Device unavailable";
        }

        public string GetUptimeSummary()
        {
            var uptime = TimeSpan.FromSeconds(UptimeSeconds);
            return uptime.TotalHours >= 1d ? uptime.ToString(@"hh\:mm\:ss") : uptime.ToString(@"mm\:ss");
        }

        public void SaveRuntimeState()
        {
            if (app == null || layoutManager == null) return;

            var runtimeState = BuildRuntimeState();
            try
            {
                var json = JsonUtility.ToJson(runtimeState);
                PlayerPrefs.SetString(RuntimeStatePrefKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[ChipOSRuntime] Failed to save runtime state: {ex.Message}", this);
#endif
                _ = ex;
                return;
            }

            stateDirty = false;
            restoredState = runtimeState;
            stateLoaded = true;
            eventBus.Publish(new ChipOSRuntimeStateEvent("Saved", runtimeState));
            UpdateInspectorStatus();
        }

        private void LoadRuntimeState()
        {
            if (!PlayerPrefs.HasKey(RuntimeStatePrefKey)) return;

            var json = PlayerPrefs.GetString(RuntimeStatePrefKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return;

            ChipOSRuntimeState loadedState;
            try
            {
                loadedState = JsonUtility.FromJson<ChipOSRuntimeState>(json);
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[ChipOSRuntime] Failed to deserialize runtime state: {ex.Message}", this);
#endif
                _ = ex;
                PlayerPrefs.DeleteKey(RuntimeStatePrefKey);
                return;
            }

            if (loadedState == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[ChipOSRuntime] Deserialized runtime state was null; ignoring saved state.", this);
#endif
                PlayerPrefs.DeleteKey(RuntimeStatePrefKey);
                return;
            }

            loadedState.Sanitize();
            restoredState = loadedState;
            stateLoaded = true;
            eventTracingEnabled = loadedState.debugEventTracingEnabled;
            verboseServiceStatus = loadedState.verboseServiceStatusEnabled;
            eventBus.SetTracingEnabled(eventTracingEnabled);
            eventBus.Publish(new ChipOSRuntimeStateEvent("Restored", loadedState));
        }

        private void EnsureInitialized()
        {
            if (initialized) return;

            initialized = true;
            startedAtRealtime = Time.unscaledTime;
            notificationService.Bind(eventBus);
            eventBus.SetTracingEnabled(eventTracingEnabled);
            SubscribeToDeviceService();

            if (restoreStateOnStartup) LoadRuntimeState();
        }

        private ChipOSRuntimeState BuildRuntimeState()
        {
            var runtimeState = new ChipOSRuntimeState
            {
                layoutMode = layoutManager.CurrentMode.ToString(),
                hudVisible = app.HudVisible,
                activeTaskTitle = app.CurrentTaskTitle,
                activeTaskIndex = app.CurrentTaskIndex < 0 ? 0 : app.CurrentTaskIndex,
                debugEventTracingEnabled = eventTracingEnabled,
                verboseServiceStatusEnabled = verboseServiceStatus,
                savedAtUtc = DateTime.UtcNow.ToString("O")
            };

            runtimeState.Sanitize();
            return runtimeState;
        }

        private void RegisterOrReplaceService<TService>(TService service) where TService : class, IChipOSService
        {
            if (service == null) return;

            if (serviceRegistry.TryGet<TService>(out var existing))
            {
                if (ReferenceEquals(existing, service)) return;
                serviceRegistry.Unregister<TService>();
                eventBus.Publish(new ChipOSServiceRegistryEvent("Unregistered", typeof(TService), existing));
            }

            if (!serviceRegistry.Register(service))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[ChipOSRuntime] Failed to register service '{service.ServiceName}' as {typeof(TService).Name}.", this);
#endif
                return;
            }

            eventBus.Publish(new ChipOSServiceRegistryEvent("Registered", typeof(TService), service));
            MarkStateDirty();
        }

        private void SubscribeToDeviceService()
        {
            if (deviceServiceSubscribed) return;
            deviceServiceSubscribed = true;
            simulatedDeviceService.SnapshotChanged += HandleDeviceSnapshotChanged;
        }

        private void SubscribeToAudioSource()
        {
            if (audioSourceSubscribed)
            {
                return;
            }

            audioSourceSubscribed = true;
            simulatedAudioSource.AudioFrameProduced += HandleAudioFrameProduced;
            simulatedAudioSource.StateChanged += HandleAudioSourceStateChanged;
        }

        private void HandleAudioFrameProduced(ChipOSAudioFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            if (!eventTracingEnabled && Time.unscaledTime - lastAudioFrameEventPublishTime < AudioFrameEventPublishIntervalSeconds)
            {
                return;
            }

            lastAudioFrameEventPublishTime = Time.unscaledTime;
            eventBus.Publish(new ChipOSAudioFrameProducedEvent(frame));
        }

        private void HandleAudioSourceStateChanged(ChipOSAudioSourceState state)
        {
            eventBus.Publish(new ChipOSAudioSourceStateChangedEvent(simulatedAudioSource.SourceName, state));
            UpdateInspectorStatus();
        }

        private void HandleDeviceSnapshotChanged(ChipOSDeviceSnapshot previousSnapshot, ChipOSDeviceSnapshot currentSnapshot)
        {
            if (currentSnapshot == null) return;

            app?.SynchronizeDeviceSnapshot(currentSnapshot);
            eventBus.Publish(new ChipOSDeviceSnapshotChangedEvent(currentSnapshot));

            var resetDetected = simulatedDeviceService.IsDefaultSnapshot(currentSnapshot) && !simulatedDeviceService.IsDefaultSnapshot(previousSnapshot);

            if (previousSnapshot != null && previousSnapshot.trackingState != currentSnapshot.trackingState)
            {
                eventBus.Publish(new ChipOSDeviceTrackingChangedEvent(previousSnapshot.trackingState, currentSnapshot.trackingState));
                if (!resetDetected)
                {
                    if (currentSnapshot.trackingState == ChipOSDeviceTrackingState.Lost)
                        QueueNotification("Tracking state: LOST", ChipOSNotificationPriority.High, 2.6f, nameof(SimulatedChipOSDeviceService));
                    else if (previousSnapshot.trackingState == ChipOSDeviceTrackingState.Lost)
                        QueueNotification($"Tracking state: {currentSnapshot.trackingState.ToString().ToUpperInvariant()}", ChipOSNotificationPriority.Normal, 2.2f, nameof(SimulatedChipOSDeviceService));
                }
            }

            if (previousSnapshot != null && previousSnapshot.userContext != currentSnapshot.userContext)
            {
                eventBus.Publish(new ChipOSDeviceContextChangedEvent(previousSnapshot.userContext, currentSnapshot.userContext, currentSnapshot.movementSpeedMetersPerSecond));
                if (!resetDetected)
                    QueueNotification($"Device context: {currentSnapshot.userContext.ToString().ToUpperInvariant()}", ChipOSNotificationPriority.Normal, 2.2f, nameof(SimulatedChipOSDeviceService));
            }

            if (previousSnapshot != null && (previousSnapshot.batteryLevel != currentSnapshot.batteryLevel || previousSnapshot.isCharging != currentSnapshot.isCharging))
            {
                eventBus.Publish(new ChipOSDeviceBatteryChangedEvent(previousSnapshot.batteryLevel, currentSnapshot.batteryLevel, currentSnapshot.isCharging));
                if (!resetDetected && previousSnapshot.batteryLevel > 20 && currentSnapshot.batteryLevel <= 20)
                    QueueNotification($"Battery low: {currentSnapshot.batteryLevel}%", ChipOSNotificationPriority.High, 3f, nameof(SimulatedChipOSDeviceService));
            }

            if (previousSnapshot != null && previousSnapshot.comfortWarning != currentSnapshot.comfortWarning)
            {
                eventBus.Publish(new ChipOSDeviceComfortWarningChangedEvent(currentSnapshot.comfortWarning));
                if (!resetDetected)
                    QueueNotification(currentSnapshot.comfortWarning ? "Comfort warning enabled" : "Comfort warning cleared", currentSnapshot.comfortWarning ? ChipOSNotificationPriority.High : ChipOSNotificationPriority.Normal, 2.4f, nameof(SimulatedChipOSDeviceService));
            }

            MarkStateDirty();
            UpdateInspectorStatus();
        }

        private void UpdateInspectorStatus()
        {
            var deviceSnapshot = GetDeviceSnapshot();
            var xrStatus = xrAdapter != null ? xrAdapter.GetServiceStatus() : "No XR adapter";

            inspectorStatus =
                $"Runtime Ready: {IsServiceReady}\n" +
                $"Services: {serviceRegistry.Count}\n" +
                $"Event Count: {PublishedEventCount}\n" +
                $"Event Trace: {(eventTracingEnabled ? "On" : "Off")}\n" +
                $"Verbose Status: {verboseServiceStatus}\n" +
                $"Notification: {GetActiveNotificationSummary()}\n" +
                $"Device: {(deviceSnapshot != null ? deviceSnapshot.GetCompactSummary() : "Unavailable")}\n" +
                $"XR Adapter: {xrStatus}\n" +
                $"Audio: {AudioSource?.Diagnostics.GetCompactSummary() ?? "Unavailable"}\n" +
                $"Captions: {captionService.Diagnostics.GetCompactSummary()}";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogRuntimeOnlineIfReady()
        {
            if (runtimeOnlineLogged || !IsServiceReady) return;
            runtimeOnlineLogged = true;
            Debug.Log($"ChipOS Mark II runtime online: services={serviceRegistry.Count}, layout={layoutManager.CurrentMode}, hud={app.HudVisible}", this);
        }
    }
}
