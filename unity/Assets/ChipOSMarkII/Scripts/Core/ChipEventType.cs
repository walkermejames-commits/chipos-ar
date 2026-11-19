namespace ChipOSMarkII
{
    /// <summary>
    /// Lightweight event ids for the local message router.
    /// </summary>
    public enum ChipEventType
    {
        HudToggled = 0,
        LayoutChanged = 1,
        PanicEntered = 2,
        PanicRestored = 3,
        TaskChanged = 4,
        AlertRaised = 5,
        DiagnosticsUpdated = 6,
        InputReceived = 7,
        BootMessage = 8,
        ModeChanged = 9,
        SystemMessageChanged = 10
    }
}
