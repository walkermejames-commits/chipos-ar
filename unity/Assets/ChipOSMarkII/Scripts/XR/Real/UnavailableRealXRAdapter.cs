using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Safe unavailable stub for when real XR SDK is not compiled.
    /// </summary>
    public sealed class UnavailableRealXRAdapter : IChipOSXRAdapter, IDisposable
    {
        public string AdapterName => "Unavailable Real XR Adapter";
        public ChipOSXRAdapterState State => ChipOSXRAdapterState.Faulted;
        public ChipOSXRCapabilities Capabilities => new ChipOSXRCapabilities { AdapterLabel = "No SDK compiled" };
        public ChipOSXRFrame LatestFrame => ChipOSXRFrame.Default;

        public event Action<ChipOSXRAdapterState, ChipOSXRAdapterState> StateChanged;
        public event Action<ChipOSXRFrame> FrameUpdated;

        public string ServiceName => "Unavailable Real XR Adapter";
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.XR;
        public bool IsServiceReady => false;

        public void StartAdapter() { }
        public void StopAdapter() { }
        public void ResetAdapter() { }
        public void Tick(float deltaTime) { }

        public string GetServiceStatus() => "SDK not compiled - using simulated fallback";

        public void Dispose() { }
    }
}