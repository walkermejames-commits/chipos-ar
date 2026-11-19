using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Routes local input providers into ChipOS commands while keeping the app core input-agnostic.
    /// </summary>
    public sealed class InputRouter : MonoBehaviour, IChipOSService
    {
        private const float SimulatedYawStepDegrees = 6f;
        private const float SimulatedPitchStepDegrees = 4f;

        public enum RouterState
        {
            Unbound = 0,
            Idle = 1,
            InputReceived = 2
        }

        [Serializable]
        public struct KeyBinding
        {
            public ChipInputActionId action;
            public KeyCode key;
            public string label;

            public KeyBinding(ChipInputActionId action, KeyCode key, string label)
            {
                this.action = action;
                this.key = key;
                this.label = label;
            }
        }

        [SerializeField] private bool autoFindApp = true;
        [SerializeField] private bool verboseWarnings = true;
        [SerializeField] [TextArea(2, 5)] private string inspectorWarning = "InputRouter awaiting app binding.";
        [SerializeField] [Min(0.01f)] private float repeatCooldownSeconds = 0.12f;
        [SerializeField] private List<KeyBinding> bindings = new List<KeyBinding>();

        private readonly Dictionary<ChipInputActionId, float> actionCooldowns = new Dictionary<ChipInputActionId, float>();

        private ChipOSApp app;
        private KeyboardChipInputProvider keyboardProvider;
        private bool missingAppWarningIssued;
        private float nextAllowedInputTime;

        public string LastInput { get; private set; } = "Idle";
        public RouterState CurrentInputState { get; private set; } = RouterState.Unbound;
        public ChipInputSourceKind CurrentInputSource { get; private set; } = ChipInputSourceKind.Unknown;
        public IReadOnlyList<KeyBinding> CurrentBindings => bindings;
        public string InspectorWarning => inspectorWarning;
        public string ServiceName => "Input Router";
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.Runtime;
        public bool IsServiceReady => app != null && keyboardProvider != null;

        private void Reset()
        {
            EnsureDefaultBindings();
        }

        private void Awake()
        {
            EnsureDefaultBindings();
            RebuildProviders();
            TryAutoBind();
        }

        private void OnValidate()
        {
            EnsureDefaultBindings();
            RebuildProviders();
            UpdateInspectorWarning();
        }

        /// <summary>
        /// Binds the router to the owning app controller.
        /// </summary>
        public void Bind(ChipOSApp owner)
        {
            app = owner;
            CurrentInputState = app == null ? RouterState.Unbound : RouterState.Idle;
            missingAppWarningIssued = false;
            UpdateInspectorWarning();
        }

        public string GetBindingsSummary()
        {
            var summary = new StringBuilder();
            for (var i = 0; i < bindings.Count; i++)
            {
                if (summary.Length > 0)
                {
                    summary.Append(" | ");
                }

                summary.Append(bindings[i].key);
                summary.Append(": ");
                summary.Append(bindings[i].label);
            }

            return summary.ToString();
        }

        private void Update()
        {
            if (app == null)
            {
                TryAutoBind();
                return;
            }

            CurrentInputState = RouterState.Idle;
            if (HandleDeviceSimulationInput())
            {
                UpdateInspectorWarning();
                return;
            }

            if (Time.unscaledTime < nextAllowedInputTime)
            {
                UpdateInspectorWarning();
                return;
            }

            if (keyboardProvider != null && keyboardProvider.TryReadCommand(Time.unscaledTime, out var command))
            {
                Consume(command);
            }

            UpdateInspectorWarning();
        }

        private void Consume(ChipInputCommand command)
        {
            LastInput = command.Label;
            CurrentInputState = RouterState.InputReceived;
            CurrentInputSource = command.Source;
            nextAllowedInputTime = Time.unscaledTime + repeatCooldownSeconds;

            if (app != null)
            {
                app.HandleInputCommand(command);
            }
        }

        private void TryAutoBind()
        {
            if (!autoFindApp)
            {
                WarnOnce("InputRouter is not bound to ChipOSApp and auto-find is disabled.");
                UpdateInspectorWarning();
                return;
            }

            if (app == null)
            {
                app = GetComponent<ChipOSApp>();
            }

            if (app == null)
            {
                app = FindFirstObjectByType<ChipOSApp>();
            }

            if (app == null)
            {
                CurrentInputState = RouterState.Unbound;
                CurrentInputSource = ChipInputSourceKind.Unknown;
                WarnOnce("InputRouter could not find ChipOSApp yet. Input providers are waiting for a valid app.");
                UpdateInspectorWarning();
                return;
            }

            Bind(app);
        }

        private void RebuildProviders()
        {
            keyboardProvider = new KeyboardChipInputProvider(bindings, actionCooldowns, repeatCooldownSeconds);
        }

        private void EnsureDefaultBindings()
        {
            var sanitizedBindings = new List<KeyBinding>();
            var seenActions = new HashSet<ChipInputActionId>();

            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.ToggleHud, FindBindingKey(ChipInputActionId.ToggleHud, KeyCode.H), "Toggle HUD");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.ResetLayout, FindBindingKey(ChipInputActionId.ResetLayout, KeyCode.R), "Reset Layout");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.Panic, FindBindingKey(ChipInputActionId.Panic, KeyCode.P), "Panic");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.Restore, FindBindingKey(ChipInputActionId.Restore, KeyCode.O), "Restore");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.CycleTask, FindBindingKey(ChipInputActionId.CycleTask, KeyCode.T), "Cycle Task");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.DefaultLayout, FindBindingKey(ChipInputActionId.DefaultLayout, KeyCode.F1), "Default Layout");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.DeskLayout, FindBindingKey(ChipInputActionId.DeskLayout, KeyCode.F2), "Desk Layout");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.WalkingLayout, FindBindingKey(ChipInputActionId.WalkingLayout, KeyCode.F3), "Walking Layout");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.FocusLayout, FindBindingKey(ChipInputActionId.FocusLayout, KeyCode.F4), "Focus Layout");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.MinimalLayout, FindBindingKey(ChipInputActionId.MinimalLayout, KeyCode.F5), "Minimal Layout");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.ToggleSimulatedAudio, FindBindingKey(ChipInputActionId.ToggleSimulatedAudio, KeyCode.A), "Toggle Sim Audio");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.ToggleSimulatedSpeech, FindBindingKey(ChipInputActionId.ToggleSimulatedSpeech, KeyCode.S), "Toggle Sim Speech");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.EmitMockCaption, FindBindingKey(ChipInputActionId.EmitMockCaption, KeyCode.K), "Mock Caption");
            AddBinding(sanitizedBindings, seenActions, ChipInputActionId.ToggleCreativeStudio, FindBindingKey(ChipInputActionId.ToggleCreativeStudio, KeyCode.V), "Toggle Creative Studio");

            bindings = sanitizedBindings;
        }

        private KeyCode FindBindingKey(ChipInputActionId action, KeyCode fallbackKey)
        {
            for (var i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].action == action)
                {
                    return bindings[i].key;
                }
            }

            return fallbackKey;
        }

        private static void AddBinding(List<KeyBinding> target, HashSet<ChipInputActionId> seenActions, ChipInputActionId action, KeyCode key, string label)
        {
            if (seenActions.Contains(action))
            {
                return;
            }

            seenActions.Add(action);
            target.Add(new KeyBinding(action, key, label));
        }

        private void UpdateInspectorWarning()
        {
            if (app == null)
            {
                inspectorWarning = "ChipOSApp binding missing.\nInput is disabled until a ChipOSApp is found.";
                return;
            }

            inspectorWarning =
                $"Bound to ChipOSApp.\n" +
                $"State: {CurrentInputState}\n" +
                $"Source: {CurrentInputSource}\n" +
                $"Last Input: {LastInput}\n" +
                $"Bindings: {GetBindingsSummary()}\n" +
                $"Device Sim: {GetDeviceSimulationSummary()}\n" +
                $"Audio Sim: A Audio | S Speech/Silence | K Mock Caption | V Studio\n" +
                $"L: Toggle Auto Context Layout";
        }

        private bool HandleDeviceSimulationInput()
        {
            var runtime = app != null ? app.Runtime : null;
            var deviceService = runtime != null ? runtime.SimulatedDeviceService : null;
            if (deviceService == null)
            {
                return false;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                return ApplyDeviceSimulation("1 Device Context Desk", () => deviceService.SetContext(ChipOSDeviceContext.Desk));
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                return ApplyDeviceSimulation("2 Device Context Walking", () => deviceService.SetContext(ChipOSDeviceContext.Walking));
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                return ApplyDeviceSimulation("3 Device Context Focus", () => deviceService.SetContext(ChipOSDeviceContext.Focus));
            }

            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                return ApplyDeviceSimulation("4 Device Context Unknown", () => deviceService.SetContext(ChipOSDeviceContext.Unknown));
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                return ApplyDeviceSimulation("B Battery -10%", () => deviceService.AdjustBattery(-10));
            }

            if (Input.GetKeyDown(KeyCode.N))
            {
                return ApplyDeviceSimulation("N Battery +10%", () => deviceService.AdjustBattery(10));
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                return ApplyDeviceSimulation("C Toggle Charging", deviceService.ToggleCharging);
            }

            if (Input.GetKeyDown(KeyCode.Y))
            {
                return ApplyDeviceSimulation("Y Cycle Tracking", () =>
                {
                    deviceService.CycleTrackingState();
                    return true;
                });
            }

            if (Input.GetKeyDown(KeyCode.U))
            {
                return ApplyDeviceSimulation("U Toggle Comfort Warning", deviceService.ToggleComfortWarning);
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                return ApplyDeviceSimulation("LeftArrow Head Yaw -6", () => deviceService.AdjustHeadPose(-SimulatedYawStepDegrees, 0f));
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                return ApplyDeviceSimulation("RightArrow Head Yaw +6", () => deviceService.AdjustHeadPose(SimulatedYawStepDegrees, 0f));
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                return ApplyDeviceSimulation("UpArrow Head Pitch -4", () => deviceService.AdjustHeadPose(0f, -SimulatedPitchStepDegrees));
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                return ApplyDeviceSimulation("DownArrow Head Pitch +4", () => deviceService.AdjustHeadPose(0f, SimulatedPitchStepDegrees));
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                deviceService.ResetState();
                runtime.QueueNotification("Simulated device reset", ChipOSNotificationPriority.Normal, 2.4f, nameof(SimulatedChipOSDeviceService));
                RecordDeviceSimulationInput("Backspace Device Reset");
                return true;
            }

            // L key - Toggle Auto Context Layout
            if (Input.GetKeyDown(KeyCode.L))
            {
                var policyService = runtime?.ContextPolicyService;
                if (policyService != null)
                {
                    policyService.ToggleAutoSwitch();
                    RecordDeviceSimulationInput("L Toggle Auto Context Layout");
                    return true;
                }
            }

            return false;
        }

        private bool ApplyDeviceSimulation(string label, Func<bool> action)
        {
            if (action == null || !action())
            {
                return false;
            }

            RecordDeviceSimulationInput(label);
            return true;
        }

        private void RecordDeviceSimulationInput(string label)
        {
            LastInput = label;
            CurrentInputState = RouterState.InputReceived;
            CurrentInputSource = ChipInputSourceKind.Keyboard;
            app?.RecordAuxiliaryInput(ChipInputSourceKind.Keyboard, label);
        }

        private static string GetDeviceSimulationSummary()
        {
            return "1-4 Context | B/N Battery | C Charging | Y Tracking | U Comfort | Arrows Pose | Backspace Reset | L Auto-Context";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void Warn(string message)
        {
            if (!verboseWarnings || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Debug.LogWarning($"[InputRouter] {message}", this);
        }

        private void WarnOnce(string message)
        {
            if (missingAppWarningIssued)
            {
                return;
            }

            missingAppWarningIssued = true;
            Warn(message);
        }

        public string GetServiceStatus()
        {
            return $"State {CurrentInputState} | Source {CurrentInputSource} | Last {LastInput}";
        }
    }
}
