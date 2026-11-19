namespace ChipOSMarkII
{
    /// <summary>
    /// High-level runtime modes for the ChipOS shell.
    /// </summary>
    public enum ChipAppMode
    {
        Boot = 0,
        Idle = 1,
        Focus = 2,
        Walking = 3,
        Desk = 4,
        Panic = 5,
        Diagnostics = 6
    }
}
