using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Pure policy logic for deciding HUD behaviour from device snapshot.
    /// No side effects, fully testable.
    /// </summary>
    public static class ChipOSContextPolicy
    {
        public static ChipOSContextDecision Evaluate(ChipOSDeviceSnapshot snapshot, bool panicActive)
        {
            if (snapshot == null)
                return ChipOSContextDecision.Default;

            // Panic always wins
            if (panicActive)
            {
                return new ChipOSContextDecision(
                    LayoutMode.Panic,
                    simplifyHUD: true,
                    highPriorityNotifications: true,
                    comfortModeActive: false,
                    trackingSafetyMode: false,
                    lowPowerSuggestion: false,
                    reason: "Panic mode active - safety override");
            }

            bool trackingLost = snapshot.trackingState == ChipOSDeviceTrackingState.Lost ||
                               snapshot.trackingState == ChipOSDeviceTrackingState.Unavailable;

            bool comfortWarning = snapshot.comfortWarning;
            bool lowBattery = snapshot.batteryLevel <= 20;

            // Priority: Tracking Lost > Comfort Warning > Context rules
            if (trackingLost)
            {
                return new ChipOSContextDecision(
                    LayoutMode.Minimal,
                    simplifyHUD: true,
                    highPriorityNotifications: true,
                    comfortModeActive: false,
                    trackingSafetyMode: true,
                    lowPowerSuggestion: lowBattery,
                    reason: $"Tracking {snapshot.trackingState} - safe fallback");
            }

            if (comfortWarning)
            {
                return new ChipOSContextDecision(
                    LayoutMode.Minimal,
                    simplifyHUD: true,
                    highPriorityNotifications: false,
                    comfortModeActive: true,
                    trackingSafetyMode: false,
                    lowPowerSuggestion: lowBattery,
                    reason: "Comfort warning active - reduce visual load");
            }

            // Normal context rules
            switch (snapshot.userContext)
            {
                case ChipOSDeviceContext.Desk:
                    return new ChipOSContextDecision(
                        LayoutMode.Desk,
                        simplifyHUD: false,
                        highPriorityNotifications: false,
                        comfortModeActive: false,
                        trackingSafetyMode: false,
                        lowPowerSuggestion: lowBattery,
                        reason: "Desk context active - full workstation layout");

                case ChipOSDeviceContext.Walking:
                    return new ChipOSContextDecision(
                        LayoutMode.Walking,
                        simplifyHUD: true,
                        highPriorityNotifications: false,
                        comfortModeActive: false,
                        trackingSafetyMode: false,
                        lowPowerSuggestion: lowBattery,
                        reason: "Walking context active - safer simpler HUD");

                case ChipOSDeviceContext.Focus:
                    return new ChipOSContextDecision(
                        LayoutMode.Focus,
                        simplifyHUD: true,
                        highPriorityNotifications: false,
                        comfortModeActive: false,
                        trackingSafetyMode: false,
                        lowPowerSuggestion: lowBattery,
                        reason: "Focus context active - quiet interface, task prominent");

                case ChipOSDeviceContext.Unknown:
                default:
                    return new ChipOSContextDecision(
                        LayoutMode.Default,
                        simplifyHUD: false,
                        highPriorityNotifications: false,
                        comfortModeActive: false,
                        trackingSafetyMode: false,
                        lowPowerSuggestion: lowBattery,
                        reason: "Unknown context - conservative safe behaviour");
            }
        }
    }
}