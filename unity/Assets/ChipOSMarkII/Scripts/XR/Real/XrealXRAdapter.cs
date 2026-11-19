using System;

namespace ChipOSMarkII
{
    public sealed class XrealXRAdapter : RealXRAdapterBase
    {
        public override string AdapterName => "XREAL XR Adapter";

#if CHIPOS_XREAL || CHIPOS_NRSDK
        public XrealXRAdapter()
        {
            capabilities = new ChipOSXRCapabilities { SupportsRealHardware = true, AdapterLabel = "XREAL compile doorway" };
        }

        public override void StartAdapter()
        {
            SetState(ChipOSXRAdapterState.Limited);
        }

        public override void Tick(float deltaTime) { }

        public override string GetServiceStatus() => "XREAL compile doorway is enabled, but real SDK integration is still TODO.";
#else
        public XrealXRAdapter()
        {
            capabilities = new ChipOSXRCapabilities { SupportsRealHardware = false, AdapterLabel = "XREAL (SDK not compiled)" };
        }

        public override void StartAdapter() { SetState(ChipOSXRAdapterState.Faulted); }
        public override void Tick(float deltaTime) { }
        public override string GetServiceStatus() => "XREAL path - SDK not compiled (CHIPOS_XREAL / CHIPOS_NRSDK missing)";
#endif
    }
}
