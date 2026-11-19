using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Diagnostics for the current XR adapter selection and state.
    /// </summary>
    public sealed class XRAdapterDiagnostics
    {
        public string SelectedAdapterName { get; set; } = "None";
        public XRAdapterKind SelectedKind { get; set; } = XRAdapterKind.Unavailable;
        public bool IsRealHardwareRequested { get; set; }
        public bool IsRealHardwareAvailable { get; set; }
        public bool IsSDKCompiled { get; set; }
        public bool IsFallbackActive { get; set; }
        public string FallbackReason { get; set; } = string.Empty;
        public string LatestError { get; set; } = string.Empty;
        public ChipOSXRAdapterState AdapterState { get; set; } = ChipOSXRAdapterState.Stopped;
        public bool BridgeConnected { get; set; }
        public int FramesAppliedCount { get; set; }

        public string GetCompactSummary()
        {
            return $"{SelectedAdapterName} [{SelectedKind}] | State: {AdapterState} | Fallback: {IsFallbackActive}";
        }

        public string GetDetailedSummary()
        {
            return $"Adapter: {SelectedAdapterName}\n" +
                   $"Kind: {SelectedKind}\n" +
                   $"Real Requested: {IsRealHardwareRequested}\n" +
                   $"Real Available: {IsRealHardwareAvailable}\n" +
                   $"SDK Compiled: {IsSDKCompiled}\n" +
                   $"Fallback Active: {IsFallbackActive} ({FallbackReason})\n" +
                   $"State: {AdapterState}\n" +
                   $"Bridge Connected: {BridgeConnected}\n" +
                   $"Frames Applied: {FramesAppliedCount}\n" +
                   $"Last Error: {LatestError}";
        }
    }
}