using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Common interface for simulated and future real XR adapters.
    /// Extends IChipOSService so it can be registered in the ServiceRegistry.
    /// </summary>
    public interface IChipOSXRAdapter : IChipOSService
    {
        ChipOSXRAdapterState State { get; }
        ChipOSXRCapabilities Capabilities { get; }
        ChipOSXRFrame LatestFrame { get; }

        event Action<ChipOSXRAdapterState, ChipOSXRAdapterState> StateChanged;
        event Action<ChipOSXRFrame> FrameUpdated;

        void StartAdapter();
        void StopAdapter();
        void ResetAdapter();
        void Tick(float deltaTime);
    }
}