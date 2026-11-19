using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChipOSMarkII
{
    /// <summary>
    /// Main ChipOS controller that owns the operating state, boot flow, events, and HUD modules.
    /// </summary>
    public sealed class ChipOSApp : MonoBehaviour, IChipOSService
    {
        private const int MaxWarnings = 8;
        private const float DefaultHudFadeDuration = 0.18f;
        private const string SafeAreaRootName = "SafeAreaRoot";

        [SerializeField] private bool bootOnAwake = true;
        [SerializeField] private ChipState state = new ChipState();
        [SerializeField] private float hudFadeDuration = DefaultHudFadeDuration;
        [SerializeField] private bool respectSafeArea = true;
        [SerializeField] [Min(0.05f)] private float bootStepDelaySeconds = 0.38f;
        [SerializeField] private string[] bootMessages =
        {
            "CHIPOS MARK II // waking interface",
            "loading layout manager",
            "checking input router",
            "diagnostics online",
            "HUD ready"
        };

        private readonly Dictionary<string, HudPanel> panelLookup = new Dictionary<string, HudPanel>(StringComparer.Ordinal);
        private readonly List<HudPanel> panels = new List<HudPanel>();
        private readonly List<string> warnings = new List<string>();
        private readonly ChipEventBus eventBus = new ChipEventBus();
        private readonly ChipTaskBoard taskBoard = new ChipTaskBoard();

        private ChipOSRuntime runtime;
        private LayoutManager layoutManager;
        private InputRouter inputRouter;
        [SerializeField] private Canvas sceneHudCanvas;
        private Canvas hudCanvas;
        private CanvasGroup hudCanvasGroup;
        private GraphicRaycaster graphicRaycaster;
        private RectTransform safeAreaRoot;
        private TaskPanel taskPanel;
        private AlertPanel alertPanel;
        private StatusStrip statusStrip;
        private StudioExperience studioExperience;
        private Coroutine hudVisibilityRoutine;
        private Coroutine bootRoutine;
        private bool hudVisible = true;
        private LayoutMode restoreMode = LayoutMode.Default;
        private Rect lastSafeArea = Rect.zero;
        private Vector2Int lastResolution = Vector2Int.zero;

        public ChipState State => state;
        public ChipEventBus EventBus => eventBus;
        public ChipTaskBoard TaskBoard => taskBoard;
        public ChipOSRuntime Runtime => runtime;
        public IReadOnlyList<HudPanel> Panels => panels;
        public int PanelCount => panels.Count;
        public int RegisteredPanelCount => panelLookup.Count;
        public int WarningCount => warnings.Count;
        public bool HudVisible => hudVisible;
        public bool PanicMode => state.panicMode;
        public LayoutMode ActiveLayout => layoutManager != null ? layoutManager.CurrentMode : LayoutMode.Default;
        public string CurrentLayoutDescription => layoutManager != null ? layoutManager.GetLayoutDescription(ActiveLayout) : "No layout manager";
        public ChipAppMode CurrentAppMode => state.appMode;
        public ChipAlertLevel CurrentAlertLevel => state.alertLevel;
        public TaskPanel TaskPanel => taskPanel;
        public AlertPanel AlertPanel => alertPanel;
        public StatusStrip StatusStrip => statusStrip;
        public string CurrentResolution => $"{Screen.width}x{Screen.height}";
        public string CurrentSafeArea => $"{Mathf.RoundToInt(Screen.safeArea.width)}x{Mathf.RoundToInt(Screen.safeArea.height)}";
        public string CurrentSceneName => SceneManager.GetActiveScene().name;
        public string UnityVersion => Application.unityVersion;
        public string LastEventSummary => state.lastEvent;
        public int CurrentTaskIndex => taskBoard.CurrentIndex;
        public string CurrentTaskTitle => taskBoard.CurrentTask != null ? taskBoard.CurrentTask.title : "No active task";
        public string CurrentTaskDescription => taskBoard.CurrentTask != null ? taskBoard.CurrentTask.description : "No task description";
        public string NextTaskTitle => taskBoard.NextTask != null ? taskBoard.NextTask.title : "None";
        public string ParkedTaskTitle => taskBoard.ParkedTask != null ? taskBoard.ParkedTask.title : "None";
        public string PanelRegistryReport => BuildPanelRegistryReport();
        public string ServiceName => "ChipOS App";
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.UI;
        public bool IsServiceReady => layoutManager != null && inputRouter != null && runtime != null;

        private void Awake()
        {
            EnsureDependencies();

            if (bootOnAwake)
            {
                BootSystem();
            }
        }

        private void Update()
        {
            if (hudCanvas == null || safeAreaRoot == null)
            {
                return;
            }

            if (lastResolution.x != Screen.width || lastResolution.y != Screen.height || lastSafeArea != Screen.safeArea)
            {
                ApplySafeArea();
            }
        }

        /// <summary>
        /// Boots the local ChipOS shell and starts the visible boot sequence.
        /// </summary>
        public void BootSystem()
        {
            EnsureDependencies();
            state.EnsureInitialized();
            taskBoard.Reset();
            runtime?.RestoreTaskSelection(taskBoard);
            hudVisible = runtime != null ? runtime.ResolveStartupHudVisibility(hudVisible) : hudVisible;
            EnsureHudCanvas();
            EnsureStudioExperience();
            DiscoverPanels();
            EnsureRequiredPanels();
            InitializePanels();

            state.systemReady = false;
            state.SetTrackingStatus(GetRuntimeTrackingStatusSummary());
            state.SetHudVisible(hudVisible);
            state.SetPanicMode(false);
            state.SetAlertLevel(ChipAlertLevel.Notice);
            state.SetCurrentMode(layoutManager.LoadStartupLayoutPreview());
            state.SetAppMode(ChipAppMode.Boot, "Booting");
            state.SetActiveTask(CurrentTaskTitle);
            state.SetSystemMessage("CHIPOS MARK II // waking interface");

            layoutManager.ApplyStartupLayout();
            if (runtime != null && runtime.TryGetRestoredLayoutMode(out var restoredLayout))
            {
                layoutManager.ApplyLayout(restoredLayout, true);
            }

            state.SetCurrentMode(layoutManager.CurrentMode);
            restoreMode = layoutManager.CurrentMode == LayoutMode.Panic ? LayoutMode.Default : layoutManager.CurrentMode;

            SetHudVisibility(hudVisible, true);
            RefreshPanels();
            runtime?.MarkStateDirty();

            if (bootRoutine != null)
            {
                StopCoroutine(bootRoutine);
            }

            bootRoutine = StartCoroutine(RunBootSequence());
        }

        /// <summary>
        /// Registers a panel/module with the app and layout system.
        /// </summary>
        public void RegisterPanel(HudPanel panel)
        {
            EnsureDependencies();
            RemoveNullPanels();

            if (panel == null)
            {
                return;
            }

            var panelId = panel.PanelId;
            if (string.IsNullOrWhiteSpace(panelId))
            {
                Warn($"Skipped registering unnamed panel on {panel.gameObject.name}.");
                return;
            }

            if (panelLookup.TryGetValue(panelId, out var existing))
            {
                if (existing == null)
                {
                    panelLookup.Remove(panelId);
                    panels.Remove(existing);
                }

                if (ReferenceEquals(existing, panel))
                {
                    return;
                }

                Warn($"Duplicate panel id '{panelId}' detected. Keeping the first registration.");
                return;
            }

            panelLookup.Add(panelId, panel);
            if (!panels.Contains(panel))
            {
                panels.Add(panel);
            }

            if (panel is TaskPanel discoveredTaskPanel)
            {
                taskPanel = discoveredTaskPanel;
            }
            else if (panel is AlertPanel discoveredAlertPanel)
            {
                alertPanel = discoveredAlertPanel;
            }
            else if (panel is StatusStrip discoveredStatusStrip)
            {
                statusStrip = discoveredStatusStrip;
            }

            if (safeAreaRoot != null)
            {
                panel.InitializeModule(this, safeAreaRoot);
            }

            layoutManager.RegisterPanel(panel);
            RefreshSinglePanel(panel);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ChipOSApp] Registered panel '{panelId}'. Total: {panelLookup.Count}", this);
#endif
        }

        /// <summary>
        /// Safely unregisters a panel/module from the app and layout system.
        /// </summary>
        public void UnregisterPanel(HudPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            var panelId = panel.PanelId;
            if (!string.IsNullOrWhiteSpace(panelId) && panelLookup.TryGetValue(panelId, out var existing) && ReferenceEquals(existing, panel))
            {
                panelLookup.Remove(panelId);
            }

            panels.Remove(panel);
            layoutManager?.UnregisterPanel(panel);

            if (ReferenceEquals(taskPanel, panel))
            {
                taskPanel = FindPanel<TaskPanel>();
            }

            if (ReferenceEquals(alertPanel, panel))
            {
                alertPanel = FindPanel<AlertPanel>();
            }

            if (ReferenceEquals(statusStrip, panel))
            {
                statusStrip = FindPanel<StatusStrip>();
            }
        }

        /// <summary>
        /// Toggles overall HUD visibility.
        /// </summary>
        public void ToggleHud()
        {
            SetHudVisibility(!hudVisible, false);
            PostNotification(hudVisible ? "HUD visible" : "HUD hidden", ChipOSNotificationPriority.Normal, 1.8f);
            state.SetSystemMessage(hudVisible ? "HUD visible" : "HUD hidden");
            PublishEvent(ChipEventType.HudToggled, hudVisible ? "HUD visible" : "HUD hidden");
            RefreshPanels();
        }

        /// <summary>
        /// Switches to panic mode and shows the calm recovery surface.
        /// </summary>
        public void Panic()
        {
            EnsureDependencies();
            SetHudVisibility(true, false);

            if (layoutManager.CurrentMode != LayoutMode.Panic)
            {
                restoreMode = layoutManager.CurrentMode;
            }

            layoutManager.ApplyLayout(LayoutMode.Panic, false);
            state.SetCurrentMode(layoutManager.CurrentMode);
            state.SetPanicMode(true);
            state.SetAlertLevel(ChipAlertLevel.Critical);
            state.SetAppMode(ChipAppMode.Panic, "Panic");
            state.SetSystemMessage("Panic mode active // press O to restore");
            PostNotification("Panic layout engaged", ChipOSNotificationPriority.Critical, 4f);
            PublishEvent(ChipEventType.PanicEntered, "Panic entered");
            PublishEvent(ChipEventType.ModeChanged, $"Mode changed to {state.appMode}");
            PublishEvent(ChipEventType.LayoutChanged, $"Layout changed to {layoutManager.CurrentMode}");
            RefreshPanels();
        }

        /// <summary>
        /// Restores the last non-panic layout and clears the critical panic state.
        /// </summary>
        public void Restore()
        {
            EnsureDependencies();
            if (restoreMode == LayoutMode.Panic)
            {
                restoreMode = LayoutMode.Default;
            }

            layoutManager.ApplyLayout(restoreMode, false);
            state.SetCurrentMode(layoutManager.CurrentMode);
            state.SetPanicMode(false);
            state.SetAlertLevel(ChipAlertLevel.Normal);
            state.SetAppMode(ResolveModeForLayout(layoutManager.CurrentMode), "Ready");
            state.SetSystemMessage($"Restored {layoutManager.CurrentMode} layout");
            PostNotification($"Restored {layoutManager.CurrentMode} layout", ChipOSNotificationPriority.High, 3f);
            restoreMode = layoutManager.CurrentMode;
            PublishEvent(ChipEventType.PanicRestored, $"Panic restored to {layoutManager.CurrentMode}");
            PublishEvent(ChipEventType.ModeChanged, $"Mode restored to {state.appMode}");
            PublishEvent(ChipEventType.LayoutChanged, $"Layout changed to {layoutManager.CurrentMode}");
            RefreshPanels();
        }

        /// <summary>
        /// Clears saved layout data and snaps back to the default layout.
        /// </summary>
        public void ResetLayout()
        {
            EnsureDependencies();
            layoutManager.ResetSavedLayout();
            ApplyStandardLayout(LayoutMode.Default, true, "Layout reset");
        }

        /// <summary>
        /// Applies a named non-panic layout through the layout manager.
        /// </summary>
        public void SetLayout(LayoutMode mode)
        {
            EnsureDependencies();
            ApplyStandardLayout(mode, false, $"Layout changed to {mode}");
        }

        /// <summary>
        /// Cycles the local task list and publishes the update.
        /// </summary>
        public void CycleTask()
        {
            var task = taskBoard.Cycle();
            if (task == null)
            {
                Warn("No local tasks were available to cycle.");
                return;
            }

            state.SetActiveTask(task.title);
            state.SetSystemMessage($"Task ready // {task.title}");
            PostNotification($"Current task: {task.title}", ChipOSNotificationPriority.Normal, 2.4f);
            PublishEvent(ChipEventType.TaskChanged, $"Task changed to {task.title}");
            RefreshPanels();
        }

        public void ToggleSimulatedAudio()
        {
            if (runtime == null)
            {
                Warn("Runtime unavailable; simulated audio could not be toggled.");
                return;
            }

            var running = runtime.ToggleSimulatedAudioSource();
            var message = running ? "Simulated audio running" : "Simulated audio stopped";
            state.SetSystemMessage(message);
            PostNotification(message, ChipOSNotificationPriority.Low, 1.8f);
            RefreshPanels();
        }

        public void ToggleSimulatedSpeechMode()
        {
            if (runtime == null)
            {
                Warn("Runtime unavailable; simulated speech mode could not be toggled.");
                return;
            }

            var speechEnabled = runtime.ToggleSimulatedSpeechMode();
            var message = speechEnabled ? "Simulated speech enabled" : "Simulated silence enabled";
            state.SetSystemMessage(message);
            PostNotification(message, ChipOSNotificationPriority.Low, 1.8f);
            RefreshPanels();
        }

        public void EmitManualMockCaption()
        {
            if (runtime == null)
            {
                Warn("Runtime unavailable; mock caption could not be emitted.");
                return;
            }

            var caption = runtime.EmitManualMockCaption("Manual mock caption emitted");
            var message = caption != null ? $"Mock caption: {caption.text}" : "Mock caption unavailable";
            state.SetSystemMessage(message);
            PostNotification(message, ChipOSNotificationPriority.Low, 1.8f);
            RefreshPanels();
        }

        /// <summary>
        /// Runs the layout self-test manually and records the result in diagnostics.
        /// </summary>
        public bool RunSelfTest()
        {
            EnsureDependencies();
            var passed = layoutManager.RunSelfTest();
            state.SetAppMode(ChipAppMode.Diagnostics, passed ? "Diagnostics OK" : "Diagnostics Failed");
            state.SetSystemMessage(passed ? "Diagnostics online" : "Diagnostics reported failures");
            PublishEvent(ChipEventType.DiagnosticsUpdated, passed ? "Diagnostics self-test passed" : "Diagnostics self-test failed");
            RefreshPanels();
            return passed;
        }

        public bool TryGetPanel(string panelId, out HudPanel panel)
        {
            RemoveNullPanels();
            panel = null;

            if (string.IsNullOrWhiteSpace(panelId))
            {
                return false;
            }

            return panelLookup.TryGetValue(panelId, out panel) && panel != null;
        }

        public Vector2 GetHudReferenceSize()
        {
            if (safeAreaRoot != null && safeAreaRoot.rect.width > 0f && safeAreaRoot.rect.height > 0f)
            {
                return safeAreaRoot.rect.size;
            }

            var scaler = hudCanvas != null ? hudCanvas.GetComponent<CanvasScaler>() : null;
            return scaler != null && scaler.referenceResolution.sqrMagnitude > 0f
                ? scaler.referenceResolution
                : new Vector2(1920f, 1080f);
        }

        public string GetWarningsSummary()
        {
            if (warnings.Count == 0)
            {
                return "None";
            }

            return string.Join(" | ", warnings.ToArray());
        }

        public string GetModuleDiagnosticsReport()
        {
            RemoveNullPanels();
            if (panels.Count == 0)
            {
                return "No modules";
            }

            var report = new List<string>(panels.Count);
            for (var i = 0; i < panels.Count; i++)
            {
                if (panels[i] != null)
                {
                    report.Add(panels[i].GetDiagnosticsSummary());
                }
            }

            return string.Join(" || ", report.ToArray());
        }

        internal void HandleInputCommand(ChipInputCommand command)
        {
            state.SetInput(command.Source, command.Label);
            PublishEvent(ChipEventType.InputReceived, $"Input received: {command.Label}", command.Label);

            switch (command.Action)
            {
                case ChipInputActionId.ToggleHud:
                    ToggleHud();
                    break;
                case ChipInputActionId.ResetLayout:
                    ResetLayout();
                    break;
                case ChipInputActionId.Panic:
                    Panic();
                    break;
                case ChipInputActionId.Restore:
                    Restore();
                    break;
                case ChipInputActionId.CycleTask:
                    CycleTask();
                    break;
                case ChipInputActionId.DefaultLayout:
                    SetLayout(LayoutMode.Default);
                    break;
                case ChipInputActionId.DeskLayout:
                    SetLayout(LayoutMode.Desk);
                    break;
                case ChipInputActionId.WalkingLayout:
                    SetLayout(LayoutMode.Walking);
                    break;
                case ChipInputActionId.FocusLayout:
                    SetLayout(LayoutMode.Focus);
                    break;
                case ChipInputActionId.MinimalLayout:
                    SetLayout(LayoutMode.Minimal);
                    break;
                case ChipInputActionId.ToggleSimulatedAudio:
                    ToggleSimulatedAudio();
                    break;
                case ChipInputActionId.ToggleSimulatedSpeech:
                    ToggleSimulatedSpeechMode();
                    break;
                case ChipInputActionId.EmitMockCaption:
                    EmitManualMockCaption();
                    break;
                case ChipInputActionId.ToggleCreativeStudio:
                    ToggleCreativeStudio();
                    break;
                default:
                    Warn($"Unhandled input action '{command.Action}'.");
                    break;
            }
        }

        public void ToggleCreativeStudio()
        {
            EnsureHudCanvas();
            EnsureStudioExperience();
            studioExperience.Toggle();
            RecordAuxiliaryInput(ChipInputSourceKind.Keyboard, "V Toggle Creative Studio");
        }

        private void EnsureStudioExperience()
        {
            if (studioExperience != null || hudCanvas == null) return;
            studioExperience = hudCanvas.GetComponent<StudioExperience>();
            if (studioExperience == null) studioExperience = hudCanvas.gameObject.AddComponent<StudioExperience>();
        }

        internal void RecordAuxiliaryInput(ChipInputSourceKind source, string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            state.SetInput(source, label);
            RefreshPanels();
        }

        internal void SynchronizeDeviceSnapshot(ChipOSDeviceSnapshot snapshot)
        {
            state.SetTrackingStatus(snapshot != null ? snapshot.GetTrackingSummary() : "Device unavailable");

            if (panels.Count > 0)
            {
                RefreshPanels();
            }
        }

        internal void Warn(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            warnings.Add(message.Trim());
            while (warnings.Count > MaxWarnings)
            {
                warnings.RemoveAt(0);
            }

            PostNotification($"Warning: {message}", ChipOSNotificationPriority.High, 3.5f);
            state.SetAlertLevel(state.panicMode ? ChipAlertLevel.Critical : ChipAlertLevel.Warning);
            state.SetSystemMessage(message);
            PublishEvent(ChipEventType.AlertRaised, $"Warning: {message}");
            Debug.LogWarning($"[ChipOSApp] {message}", this);
            runtime?.MarkStateDirty();
        }

        internal Canvas EnsureHudCanvas()
        {
            if (hudCanvas != null)
            {
                return hudCanvas;
            }

            hudCanvas = sceneHudCanvas != null ? sceneHudCanvas : FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (hudCanvas == null)
            {
                var canvasObject = new GameObject("ChipOSCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
                canvasObject.transform.SetParent(transform, false);
                hudCanvas = canvasObject.GetComponent<Canvas>();
            }

            graphicRaycaster = hudCanvas != null ? hudCanvas.GetComponent<GraphicRaycaster>() : null;
            if (hudCanvas != null && graphicRaycaster == null)
            {
                graphicRaycaster = hudCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            hudCanvasGroup = hudCanvas != null ? hudCanvas.GetComponent<CanvasGroup>() : null;
            if (hudCanvas != null && hudCanvasGroup == null)
            {
                hudCanvasGroup = hudCanvas.gameObject.AddComponent<CanvasGroup>();
            }

            if (hudCanvas != null)
            {
                hudCanvas.name = "ChipOSCanvas";
                hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                hudCanvas.pixelPerfect = false;
                hudCanvas.sortingOrder = 100;
                hudCanvas.enabled = true;
            }

            var scaler = hudCanvas != null ? hudCanvas.GetComponent<CanvasScaler>() : null;
            if (hudCanvas != null && scaler == null)
            {
                scaler = hudCanvas.gameObject.AddComponent<CanvasScaler>();
            }

            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
                scaler.defaultSpriteDPI = 96f;
                scaler.fallbackScreenDPI = 96f;
                scaler.dynamicPixelsPerUnit = 1f;
            }

            var canvasRect = hudCanvas != null ? hudCanvas.GetComponent<RectTransform>() : null;
            if (canvasRect != null)
            {
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.offsetMin = Vector2.zero;
                canvasRect.offsetMax = Vector2.zero;
            }

            safeAreaRoot = EnsureSafeAreaRoot(hudCanvas);
            ApplySafeArea();
            ApplyHudVisibilityInstant(hudVisible ? 1f : 0f);
            return hudCanvas;
        }

        private void EnsureDependencies()
        {
            runtime = GetComponent<ChipOSRuntime>();
            if (runtime == null)
            {
                runtime = gameObject.AddComponent<ChipOSRuntime>();
            }

            layoutManager = GetComponent<LayoutManager>();
            if (layoutManager == null)
            {
                layoutManager = gameObject.AddComponent<LayoutManager>();
            }

            inputRouter = GetComponent<InputRouter>();
            if (inputRouter == null)
            {
                inputRouter = gameObject.AddComponent<InputRouter>();
            }

            layoutManager.Bind(this);
            inputRouter.Bind(this);
            runtime.Bind(this, layoutManager, inputRouter);
            state.EnsureInitialized();
        }

        private void DiscoverPanels()
        {
            var foundPanels = FindObjectsByType<HudPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < foundPanels.Length; i++)
            {
                var panel = foundPanels[i];
                if (panel != null && panel.gameObject != gameObject)
                {
                    RegisterPanel(panel);
                }
            }
        }

        private void EnsureRequiredPanels()
        {
            EnsurePanel<ChipPanel>("ChipPanel");
            EnsurePanel<TaskPanel>("TaskPanel");
            EnsurePanel<CaptionPanel>("CaptionPanel");
            EnsurePanel<DebugPanel>("DebugPanel");
            EnsurePanel<AlertPanel>("AlertPanel");
            EnsurePanel<StatusStrip>("StatusStrip");
        }

        private void EnsurePanel<TPanel>(string objectName) where TPanel : HudPanel
        {
            for (var i = 0; i < panels.Count; i++)
            {
                if (panels[i] is TPanel)
                {
                    return;
                }
            }

            var panelObject = new GameObject(objectName);
            panelObject.transform.SetParent(transform, false);

            var panel = panelObject.AddComponent<TPanel>();
            RegisterPanel(panel);
            PostNotification($"{objectName} created automatically", ChipOSNotificationPriority.Low, 2f);
        }

        private void InitializePanels()
        {
            if (safeAreaRoot == null)
            {
                return;
            }

            for (var i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                if (panel != null)
                {
                    panel.InitializeModule(this, safeAreaRoot);
                }
            }
        }

        private void ApplyStandardLayout(LayoutMode mode, bool immediate, string summary)
        {
            layoutManager.ApplyLayout(mode, immediate);
            state.SetCurrentMode(layoutManager.CurrentMode);
            state.SetPanicMode(false);
            state.SetHudVisible(hudVisible);
            state.SetAlertLevel(ChipAlertLevel.Normal);
            state.SetAppMode(ResolveModeForLayout(layoutManager.CurrentMode), "Ready");
            state.SetSystemMessage($"{layoutManager.CurrentMode} layout // {layoutManager.GetLayoutDescription(layoutManager.CurrentMode)}");
            PostNotification(summary, ChipOSNotificationPriority.High, 2.75f);
            restoreMode = layoutManager.CurrentMode;
            PublishEvent(ChipEventType.ModeChanged, $"Mode changed to {state.appMode}");
            PublishEvent(ChipEventType.LayoutChanged, summary);
            RefreshPanels();
            runtime?.MarkStateDirty();
        }

        private ChipAppMode ResolveModeForLayout(LayoutMode mode)
        {
            switch (mode)
            {
                case LayoutMode.Desk:
                    return ChipAppMode.Desk;
                case LayoutMode.Walking:
                    return ChipAppMode.Walking;
                case LayoutMode.Focus:
                    return ChipAppMode.Focus;
                case LayoutMode.Panic:
                    return ChipAppMode.Panic;
                case LayoutMode.Minimal:
                case LayoutMode.Default:
                default:
                    return ChipAppMode.Idle;
            }
        }

        private void RefreshPanels()
        {
            RemoveNullPanels();

            for (var i = 0; i < panels.Count; i++)
            {
                RefreshSinglePanel(panels[i]);
            }
        }

        private void RefreshSinglePanel(HudPanel panel)
        {
            if (panel != null)
            {
                panel.HandleStateChanged(state);
            }
        }

        private void SetHudVisibility(bool visible, bool immediate)
        {
            EnsureHudCanvas();
            hudVisible = visible;
            state.SetHudVisible(visible);

            if (hudCanvasGroup == null)
            {
                return;
            }

            var targetAlpha = visible ? 1f : 0f;
            if (hudVisibilityRoutine != null)
            {
                StopCoroutine(hudVisibilityRoutine);
                hudVisibilityRoutine = null;
            }

            if (immediate || !isActiveAndEnabled)
            {
                ApplyHudVisibilityInstant(targetAlpha);
                return;
            }

            hudVisibilityRoutine = StartCoroutine(FadeHudVisibility(targetAlpha, Mathf.Max(0.01f, hudFadeDuration)));
        }

        private IEnumerator FadeHudVisibility(float targetAlpha, float duration)
        {
            var startAlpha = hudCanvasGroup != null ? hudCanvasGroup.alpha : 1f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                ApplyHudVisibilityInstant(Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }

            ApplyHudVisibilityInstant(targetAlpha);
            hudVisibilityRoutine = null;
        }

        private void ApplyHudVisibilityInstant(float alpha)
        {
            if (hudCanvasGroup == null)
            {
                return;
            }

            hudCanvasGroup.alpha = Mathf.Clamp01(alpha);
            hudCanvasGroup.interactable = hudCanvasGroup.alpha > 0.95f;
            hudCanvasGroup.blocksRaycasts = hudCanvasGroup.alpha > 0.05f;

            if (graphicRaycaster != null)
            {
                graphicRaycaster.enabled = hudCanvasGroup.alpha > 0.05f;
            }
        }

        private RectTransform EnsureSafeAreaRoot(Canvas canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            var rootTransform = canvas.transform.Find(SafeAreaRootName) as RectTransform;
            if (rootTransform == null)
            {
                var rootObject = new GameObject(SafeAreaRootName, typeof(RectTransform));
                rootTransform = rootObject.GetComponent<RectTransform>();
                rootTransform.SetParent(canvas.transform, false);
                rootTransform.SetAsFirstSibling();
            }

            rootTransform.anchorMin = Vector2.zero;
            rootTransform.anchorMax = Vector2.one;
            rootTransform.pivot = new Vector2(0.5f, 0.5f);
            rootTransform.offsetMin = Vector2.zero;
            rootTransform.offsetMax = Vector2.zero;
            rootTransform.localScale = Vector3.one;
            return rootTransform;
        }

        private void ApplySafeArea()
        {
            if (safeAreaRoot == null)
            {
                return;
            }

            var safeArea = respectSafeArea ? Screen.safeArea : new Rect(0f, 0f, Screen.width, Screen.height);
            var width = Mathf.Max(1f, Screen.width);
            var height = Mathf.Max(1f, Screen.height);

            safeAreaRoot.anchorMin = new Vector2(safeArea.xMin / width, safeArea.yMin / height);
            safeAreaRoot.anchorMax = new Vector2(safeArea.xMax / width, safeArea.yMax / height);
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
            safeAreaRoot.localScale = Vector3.one;

            lastSafeArea = safeArea;
            lastResolution = new Vector2Int(Screen.width, Screen.height);
        }

        private IEnumerator RunBootSequence()
        {
            state.SetAppMode(ChipAppMode.Boot, "Booting");
            state.SetAlertLevel(ChipAlertLevel.Notice);
            state.SetPanicMode(false);
            state.SetHudVisible(hudVisible);
            PublishEvent(ChipEventType.ModeChanged, "Boot sequence started");
            RefreshPanels();

            for (var i = 0; i < bootMessages.Length; i++)
            {
                var message = string.IsNullOrWhiteSpace(bootMessages[i]) ? $"Boot step {i + 1}" : bootMessages[i];
                state.SetSystemMessage(message);
                state.SetLastEvent($"Boot: {message}");
                PostNotification(message, ChipOSNotificationPriority.Low, 1.8f);
                PublishEvent(ChipEventType.BootMessage, message);
                RefreshPanels();
                yield return new WaitForSecondsRealtime(bootStepDelaySeconds);
            }

            state.systemReady = true;
            state.SetAlertLevel(ChipAlertLevel.Normal);
            state.SetCurrentMode(layoutManager.CurrentMode);
            state.SetAppMode(ResolveModeForLayout(layoutManager.CurrentMode), "Ready");
            state.SetActiveTask(CurrentTaskTitle);
            state.SetSystemMessage("HUD ready");
            PublishEvent(ChipEventType.ModeChanged, $"Mode changed to {state.appMode}");
            PublishEvent(ChipEventType.DiagnosticsUpdated, "HUD ready");
            RefreshPanels();
            bootRoutine = null;
            runtime?.MarkStateDirty();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[ChipOSApp] Boot complete.", this);
#endif
        }

        private void PublishEvent(ChipEventType eventType, string summary, string inputLabel = "")
        {
            state.SetLastEvent(summary);
            var chipEvent = new ChipEvent(eventType, state, summary, inputLabel);
            eventBus.Publish(chipEvent);
            runtime?.Publish(chipEvent);
            runtime?.MarkStateDirty();
        }

        public string GetServiceStatus()
        {
            return $"Layout {ActiveLayout} | HUD {(HudVisible ? "On" : "Off")} | Panic {PanicMode} | Task {CurrentTaskTitle}";
        }

        private void PostNotification(string message, ChipOSNotificationPriority priority, float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            state.AddNotification(message);
            runtime?.QueueNotification(message, priority, durationSeconds, nameof(ChipOSApp));
        }

        private string GetRuntimeTrackingStatusSummary()
        {
            var snapshot = runtime != null ? runtime.GetDeviceSnapshot() : null;
            return snapshot != null
                ? snapshot.GetTrackingSummary()
                : (Application.isEditor ? "Editor simulation" : "Standalone");
        }

        private int RemoveNullPanels()
        {
            var removedCount = 0;

            for (var i = panels.Count - 1; i >= 0; i--)
            {
                if (panels[i] == null)
                {
                    panels.RemoveAt(i);
                    removedCount++;
                }
            }

            if (panelLookup.Count == 0)
            {
                return removedCount;
            }

            var staleKeys = new List<string>();
            foreach (var entry in panelLookup)
            {
                if (entry.Value == null)
                {
                    staleKeys.Add(entry.Key);
                }
            }

            for (var i = 0; i < staleKeys.Count; i++)
            {
                if (panelLookup.Remove(staleKeys[i]))
                {
                    removedCount++;
                }
            }

            return removedCount;
        }

        private TPanel FindPanel<TPanel>() where TPanel : HudPanel
        {
            for (var i = 0; i < panels.Count; i++)
            {
                if (panels[i] is TPanel matchedPanel)
                {
                    return matchedPanel;
                }
            }

            return null;
        }

        private string BuildPanelRegistryReport()
        {
            RemoveNullPanels();
            if (panelLookup.Count == 0)
            {
                return "No registered panels";
            }

            var report = new List<string>(panelLookup.Count);
            foreach (var entry in panelLookup)
            {
                report.Add($"{entry.Key}:{(entry.Value != null ? "Live" : "Missing")}");
            }

            return string.Join(", ", report.ToArray());
        }
    }
}
