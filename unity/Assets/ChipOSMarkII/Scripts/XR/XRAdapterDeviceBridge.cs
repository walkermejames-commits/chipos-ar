using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Bridges IChipOSXRAdapter output into the existing IChipOSDeviceService layer.
    /// Now with frame counting and live diagnostics support.
    /// </summary>
    public sealed class XRAdapterDeviceBridge : IDisposable
    {
        private readonly IChipOSXRAdapter adapter;
        private readonly IChipOSDeviceService deviceService;
        private SimulatedChipOSDeviceService simulatedDeviceService;

        public bool IsConnected => adapter != null && deviceService != null;
        public string LastBridgeStatus { get; private set; } = "Not connected";
        public DateTime? LastFrameAppliedUtc { get; private set; }
        public int FramesAppliedCount { get; private set; }
        public float LastFrameConfidence { get; private set; }
        public ChipOSDeviceContext LastFrameContext { get; private set; }
        public ChipOSDeviceTrackingState LastFrameTrackingState { get; private set; }

        public XRAdapterDeviceBridge(IChipOSXRAdapter adapter, IChipOSDeviceService deviceService)
        {
            this.adapter = adapter;
            this.deviceService = deviceService;

            if (deviceService is SimulatedChipOSDeviceService sim)
            {
                simulatedDeviceService = sim;
            }

            if (adapter != null)
            {
                adapter.FrameUpdated += OnFrameUpdated;
            }

            LastBridgeStatus = IsConnected ? "Connected" : "No adapter or device service";
        }

        private void OnFrameUpdated(ChipOSXRFrame frame)
        {
            if (frame == null || deviceService == null) return;

            bool applied = false;

            if (simulatedDeviceService != null)
            {
                applied = simulatedDeviceService.ApplyXRFrame(frame);
            }
            else
            {
                LastBridgeStatus = "Cannot apply frame: no SimulatedChipOSDeviceService available";
                return;
            }

            if (applied)
            {
                FramesAppliedCount++;
                LastFrameAppliedUtc = DateTime.UtcNow;
                LastFrameConfidence = frame.Confidence;
                LastFrameContext = frame.InferredContext;
                LastFrameTrackingState = frame.TrackingState;

                LastBridgeStatus = $"Frame applied | Context: {frame.InferredContext} | Confidence: {frame.Confidence:0.00}";
            }
            else
            {
                LastBridgeStatus = $"Frame rejected by device service (sequence {FramesAppliedCount})";
            }
        }

        public void Dispose()
        {
            if (adapter != null)
            {
                adapter.FrameUpdated -= OnFrameUpdated;
            }
        }
    }
}