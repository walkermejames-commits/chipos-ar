namespace ChipOSMarkII
{
    /// <summary>
    /// Lifecycle state of an XR adapter (simulated or real hardware).
    /// </summary>
    public enum ChipOSXRAdapterState
    {
        Stopped = 0,
        Starting = 1,
        Running = 2,
        Limited = 3,
        Faulted = 4
    }
}