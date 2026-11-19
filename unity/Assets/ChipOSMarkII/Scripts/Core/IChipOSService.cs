namespace ChipOSMarkII
{
    /// <summary>
    /// Minimal contract for services that participate in the internal ChipOS runtime kernel.
    /// </summary>
    public interface IChipOSService
    {
        string ServiceName { get; }
        ChipOSServiceLayer ServiceLayer { get; }
        bool IsServiceReady { get; }

        string GetServiceStatus();
    }
}
