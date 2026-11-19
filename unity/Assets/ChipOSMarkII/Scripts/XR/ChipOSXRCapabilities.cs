namespace ChipOSMarkII
{
    /// <summary>
    /// Declares what capabilities an XR adapter (simulated or real) supports.
    /// </summary>
    public sealed class ChipOSXRCapabilities
    {
        public bool SupportsHeadPose { get; set; } = true;
        public bool SupportsBattery { get; set; } = true;
        public bool SupportsTrackingState { get; set; } = true;
        public bool SupportsContextInference { get; set; } = true;
        public bool SupportsComfortSignals { get; set; } = true;
        public bool SupportsHandInput { get; set; } = false;
        public bool SupportsVoiceInput { get; set; } = false;
        public bool SupportsSceneUnderstanding { get; set; } = false;
        public bool SupportsRealHardware { get; set; } = false;
        public string AdapterLabel { get; set; } = "Simulated XR Adapter";

        public static ChipOSXRCapabilities SimulatedDefault => new ChipOSXRCapabilities
        {
            SupportsHeadPose = true,
            SupportsBattery = true,
            SupportsTrackingState = true,
            SupportsContextInference = true,
            SupportsComfortSignals = true,
            SupportsHandInput = false,
            SupportsVoiceInput = false,
            SupportsSceneUnderstanding = false,
            SupportsRealHardware = false,
            AdapterLabel = "Simulated XR Adapter (Editor)"
        };
    }
}