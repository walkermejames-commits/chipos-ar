using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Immutable decision output from the context policy layer.
    /// </summary>
    public readonly struct ChipOSContextDecision
    {
        public LayoutMode RecommendedLayout { get; }
        public bool SimplifyHUD { get; }
        public bool HighPriorityNotifications { get; }
        public bool ComfortModeActive { get; }
        public bool TrackingSafetyMode { get; }
        public bool LowPowerSuggestion { get; }
        public string Reason { get; }

        public ChipOSContextDecision(
            LayoutMode recommendedLayout,
            bool simplifyHUD,
            bool highPriorityNotifications,
            bool comfortModeActive,
            bool trackingSafetyMode,
            bool lowPowerSuggestion,
            string reason)
        {
            RecommendedLayout = recommendedLayout;
            SimplifyHUD = simplifyHUD;
            HighPriorityNotifications = highPriorityNotifications;
            ComfortModeActive = comfortModeActive;
            TrackingSafetyMode = trackingSafetyMode;
            LowPowerSuggestion = lowPowerSuggestion;
            Reason = reason ?? "No specific reason";
        }

        public static ChipOSContextDecision Default => new ChipOSContextDecision(
            LayoutMode.Default, false, false, false, false, false, "Default safe state");
    }
}