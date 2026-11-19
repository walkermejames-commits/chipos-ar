namespace ChipOSMarkII
{
    /// <summary>
    /// Snapshot-style event payload published through the local ChipOS event bus.
    /// </summary>
    public readonly struct ChipEvent
    {
        public readonly ChipEventType Type;
        public readonly ChipAppMode AppMode;
        public readonly LayoutMode LayoutMode;
        public readonly ChipAlertLevel AlertLevel;
        public readonly ChipInputSourceKind InputSource;
        public readonly bool HudVisible;
        public readonly bool PanicMode;
        public readonly string ActiveTask;
        public readonly string InputLabel;
        public readonly string SystemMessage;
        public readonly string Summary;
        public readonly string TimestampUtc;

        public ChipEvent(
            ChipEventType type,
            ChipState state,
            string summary,
            string inputLabel = "")
        {
            Type = type;
            AppMode = state != null ? state.appMode : ChipAppMode.Boot;
            LayoutMode = state != null ? state.layoutMode : LayoutMode.Default;
            AlertLevel = state != null ? state.alertLevel : ChipAlertLevel.Normal;
            InputSource = state != null ? state.inputSource : ChipInputSourceKind.Unknown;
            HudVisible = state != null && state.hudVisible;
            PanicMode = state != null && state.panicMode;
            ActiveTask = state != null ? state.activeTask : "No active task";
            InputLabel = inputLabel ?? string.Empty;
            SystemMessage = state != null ? state.systemMessage : string.Empty;
            Summary = string.IsNullOrWhiteSpace(summary) ? type.ToString() : summary;
            TimestampUtc = state != null ? state.lastStateChangeUtc : string.Empty;
        }
    }
}
