using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ChipOSMarkII
{
    /// <summary>
    /// Runtime service that subscribes to device changes and applies context-aware layout recommendations.
    /// Panic mode from LayoutManager now correctly overrides all context decisions.
    /// </summary>
    public sealed class ChipOSContextPolicyService : IChipOSService, IDisposable
    {
        private readonly IChipOSDeviceService deviceService;
        private readonly LayoutManager layoutManager;
        private readonly ChipOSNotificationService notificationService;
        private readonly ChipOSEventBus eventBus;

        private ChipOSContextDecision lastDecision = ChipOSContextDecision.Default;
        private bool autoSwitchEnabled = true;
        private bool lastPanicState;
        private int lastBatteryLevel = 100;
        private bool lastTrackingLost;
        private bool lastComfortWarning;
        private LayoutMode lastAppliedLayout = LayoutMode.Default;

        public string ServiceName => "Context Policy Service";
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.Runtime;
        public bool IsServiceReady => deviceService != null && layoutManager != null;

        public bool AutoSwitchEnabled => autoSwitchEnabled;
        public ChipOSContextDecision CurrentDecision => lastDecision;
        public bool IsPanicActive => layoutManager != null && layoutManager.IsPanicMode;

        public ChipOSContextPolicyService(
            IChipOSDeviceService deviceService,
            LayoutManager layoutManager,
            ChipOSNotificationService notificationService,
            ChipOSEventBus eventBus)
        {
            this.deviceService = deviceService;
            this.layoutManager = layoutManager;
            this.notificationService = notificationService;
            this.eventBus = eventBus;

            if (deviceService != null)
            {
                deviceService.SnapshotChanged += OnDeviceSnapshotChanged;
            }

            autoSwitchEnabled = Application.isEditor;
        }

        public void ToggleAutoSwitch()
        {
            autoSwitchEnabled = !autoSwitchEnabled;

            string msg = autoSwitchEnabled ? "Auto context layout: ON" : "Auto context layout: OFF";
            notificationService?.Enqueue(msg, ChipOSNotificationPriority.Normal, 2.5f, nameof(ChipOSContextPolicyService));

            LogDebug(msg);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogDebug(string msg)
        {
            Debug.Log($"[ContextPolicy] {msg}");
        }

        private void OnDeviceSnapshotChanged(ChipOSDeviceSnapshot previous, ChipOSDeviceSnapshot current)
        {
            if (current == null)
            {
                Debug.LogWarning("[ContextPolicy] Received null device snapshot; skipping policy evaluation.");
                return;
            }

            if (layoutManager == null)
            {
                Debug.LogWarning("[ContextPolicy] LayoutManager is null; cannot apply context policy.");
                return;
            }

            bool panicActive = layoutManager.IsPanicMode;
            var decision = ChipOSContextPolicy.Evaluate(current, panicActive);

            bool decisionChanged = !AreDecisionsEqual(lastDecision, decision);
            bool layoutChanged = decision.RecommendedLayout != lastAppliedLayout;
            bool significantChange = layoutChanged ||
                                     decision.TrackingSafetyMode != lastDecision.TrackingSafetyMode ||
                                     decision.ComfortModeActive != lastDecision.ComfortModeActive ||
                                     decision.LowPowerSuggestion != lastDecision.LowPowerSuggestion;

            lastDecision = decision;

            // Never override Panic mode
            if (panicActive)
            {
                lastAppliedLayout = LayoutMode.Panic;
                return;
            }

            // Apply layout only if auto-switch is on, decision changed meaningfully, and not fighting manual control
            if (autoSwitchEnabled && significantChange && decision.RecommendedLayout != LayoutMode.Panic)
            {
                bool wasTrackingLost = lastTrackingLost;
                lastTrackingLost = decision.TrackingSafetyMode;

                bool wasComfort = lastComfortWarning;
                lastComfortWarning = decision.ComfortModeActive;

                layoutManager.ApplyLayout(decision.RecommendedLayout, immediate: false);
                lastAppliedLayout = decision.RecommendedLayout;

                // Queue contextual notifications (only on state change)
                if (decision.TrackingSafetyMode && !wasTrackingLost)
                {
                    notificationService?.Enqueue("Tracking safety mode active", ChipOSNotificationPriority.High, 2.8f, nameof(ChipOSContextPolicyService));
                }
                else if (!decision.TrackingSafetyMode && wasTrackingLost)
                {
                    notificationService?.Enqueue("Tracking restored - resuming normal HUD", ChipOSNotificationPriority.Normal, 2.4f, nameof(ChipOSContextPolicyService));
                }

                if (decision.ComfortModeActive && !wasComfort)
                {
                    notificationService?.Enqueue("Comfort mode engaged - reducing visual load", ChipOSNotificationPriority.Normal, 2.4f, nameof(ChipOSContextPolicyService));
                }
                else if (!decision.ComfortModeActive && wasComfort)
                {
                    notificationService?.Enqueue("Comfort warning cleared", ChipOSNotificationPriority.Normal, 2.4f, nameof(ChipOSContextPolicyService));
                }

                if (decision.LowPowerSuggestion && current.batteryLevel <= 20 && lastBatteryLevel > 20)
                {
                    notificationService?.Enqueue($"Low battery ({current.batteryLevel}%) - consider power saving", ChipOSNotificationPriority.Normal, 3.0f, nameof(ChipOSContextPolicyService));
                }

                lastBatteryLevel = current.batteryLevel;
            }
        }

        private static bool AreDecisionsEqual(ChipOSContextDecision a, ChipOSContextDecision b)
        {
            return a.RecommendedLayout == b.RecommendedLayout &&
                   a.SimplifyHUD == b.SimplifyHUD &&
                   a.TrackingSafetyMode == b.TrackingSafetyMode &&
                   a.ComfortModeActive == b.ComfortModeActive &&
                   a.LowPowerSuggestion == b.LowPowerSuggestion;
        }

        public void Dispose()
        {
            if (deviceService != null)
            {
                deviceService.SnapshotChanged -= OnDeviceSnapshotChanged;
            }
        }

        public string GetServiceStatus()
        {
            return GetStatusSummary();
        }

        public string GetStatusSummary()
        {
            return $"{lastDecision.RecommendedLayout} | Auto:{autoSwitchEnabled} | Panic:{IsPanicActive} | Simplify:{lastDecision.SimplifyHUD} | Safety:{lastDecision.TrackingSafetyMode} | Comfort:{lastDecision.ComfortModeActive} | LowPower:{lastDecision.LowPowerSuggestion} | {lastDecision.Reason}";
        }
    }
}
