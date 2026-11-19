namespace ChipOSMarkII
{
    /// <summary>
    /// Compile-safe placeholder for future OpenXR integration.
    /// </summary>
    public sealed class OpenXRAdapterStub : RealXRAdapterBase
    {
        public override string AdapterName => "OpenXR Adapter (Stub)";

#if CHIPOS_OPENXR
        public OpenXRAdapterStub()
        {
            capabilities = new ChipOSXRCapabilities
            {
                SupportsRealHardware = true,
                AdapterLabel = "OpenXR compile doorway"
            };
        }

        public override void StartAdapter()
        {
            SetState(ChipOSXRAdapterState.Limited);
        }

        public override void Tick(float deltaTime)
        {
            // TODO: Implement real OpenXR pose reading when package is present
        }

        public override string GetServiceStatus()
        {
            return "OpenXR compile doorway is enabled, but real SDK integration is still TODO.";
        }
#else
        public OpenXRAdapterStub()
        {
            capabilities = new ChipOSXRCapabilities
            {
                SupportsRealHardware = false,
                AdapterLabel = "OpenXR (SDK not compiled)"
            };
        }

        public override void StartAdapter()
        {
            SetState(ChipOSXRAdapterState.Faulted);
        }

        public override string GetServiceStatus()
        {
            return "OpenXR path - SDK not compiled (CHIPOS_OPENXR missing)";
        }
#endif
    }
}
