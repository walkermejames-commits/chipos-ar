namespace ChipOSMarkII
{
    /// <summary>
    /// Current or future input origin for ChipOS commands.
    /// </summary>
    public enum ChipInputSourceKind
    {
        Unknown = 0,
        Keyboard = 1,
        XrealController = 2,
        Gaze = 3,
        Voice = 4,
        Touch = 5
    }
}
