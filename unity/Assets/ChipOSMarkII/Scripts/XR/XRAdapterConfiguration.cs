namespace ChipOSMarkII
{
    public enum XRAdapterKind
    {
        Auto = 0,
        Simulated = 1,
        Xreal = 2,
        OpenXR = 3,
        Unavailable = 4
    }

    /// <summary>
    /// Configuration for XR adapter selection and fallback behaviour.
    /// </summary>
    public sealed class XRAdapterConfiguration
    {
        public XRAdapterKind SelectedAdapterKind { get; set; } = XRAdapterKind.Auto;
        public bool PreferRealHardware { get; set; } = false;
        public bool AllowSimulationFallback { get; set; } = true;
        public bool ForceSimulationInEditor { get; set; } = true;
        public bool EnableVerboseDiagnostics { get; set; } = false;

        public static XRAdapterConfiguration Default => new XRAdapterConfiguration();
    }
}