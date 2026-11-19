using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Safe factory that selects XR adapter (simulated or real) with automatic fallback.
    /// </summary>
    public static class XRAdapterFactory
    {
        public static IChipOSXRAdapter CreateAdapter(
            XRAdapterConfiguration config,
            out XRAdapterDiagnostics diagnostics)
        {
            diagnostics = new XRAdapterDiagnostics();

            bool isEditor = Application.isEditor;
            bool forceSim = config.ForceSimulationInEditor && isEditor;

            if (forceSim || config.SelectedAdapterKind == XRAdapterKind.Simulated)
            {
                diagnostics.SelectedAdapterName = "Simulated XR Adapter";
                diagnostics.SelectedKind = XRAdapterKind.Simulated;
                diagnostics.IsFallbackActive = false;
                return new SimulatedXRAdapter();
            }

            // Real hardware paths
            if (config.SelectedAdapterKind == XRAdapterKind.Xreal ||
                (config.SelectedAdapterKind == XRAdapterKind.Auto && XRSDKAvailability.HasAnyRealXRCompiled))
            {
                diagnostics.IsRealHardwareRequested = true;
                diagnostics.IsSDKCompiled = XRSDKAvailability.HasAnyRealXRCompiled;

                if (XRSDKAvailability.IsXrealSymbolDefined || XRSDKAvailability.IsNRSDKSymbolDefined)
                {
                    var adapter = new XrealXRAdapter();
                    diagnostics.SelectedAdapterName = adapter.AdapterName;
                    diagnostics.SelectedKind = XRAdapterKind.Xreal;
                    diagnostics.IsRealHardwareAvailable = false;
                    diagnostics.IsFallbackActive = false;
                    diagnostics.FallbackReason = "Compile doorway enabled; real XREAL/NRSDK hardware integration is still TODO.";
                    return adapter;
                }

                // Fallback if no real SDK
                if (config.AllowSimulationFallback)
                {
                    diagnostics.SelectedAdapterName = "Simulated XR Adapter (Fallback)";
                    diagnostics.SelectedKind = XRAdapterKind.Simulated;
                    diagnostics.IsFallbackActive = true;
                    diagnostics.FallbackReason = "XREAL/NRSDK requested but SDK symbols not defined (CHIPOS_XREAL / CHIPOS_NRSDK missing)";
                    return new SimulatedXRAdapter();
                }

                diagnostics.SelectedAdapterName = "Unavailable Real XR Adapter";
                diagnostics.SelectedKind = XRAdapterKind.Unavailable;
                diagnostics.IsFallbackActive = false;
                return new UnavailableRealXRAdapter();
            }

            if (config.SelectedAdapterKind == XRAdapterKind.OpenXR)
            {
                diagnostics.IsRealHardwareRequested = true;
                diagnostics.IsSDKCompiled = XRSDKAvailability.IsOpenXRSymbolDefined;

                if (XRSDKAvailability.IsOpenXRSymbolDefined)
                {
                    var adapter = new OpenXRAdapterStub();
                    diagnostics.SelectedAdapterName = adapter.AdapterName;
                    diagnostics.SelectedKind = XRAdapterKind.OpenXR;
                    diagnostics.IsRealHardwareAvailable = false;
                    diagnostics.FallbackReason = "Compile doorway enabled; real OpenXR hardware integration is still TODO.";
                    return adapter;
                }

                if (config.AllowSimulationFallback)
                {
                    diagnostics.SelectedAdapterName = "Simulated XR Adapter (Fallback)";
                    diagnostics.SelectedKind = XRAdapterKind.Simulated;
                    diagnostics.IsFallbackActive = true;
                    diagnostics.FallbackReason = "OpenXR requested but CHIPOS_OPENXR not defined";
                    return new SimulatedXRAdapter();
                }

                diagnostics.SelectedAdapterName = "Unavailable Real XR Adapter";
                diagnostics.SelectedKind = XRAdapterKind.Unavailable;
                return new UnavailableRealXRAdapter();
            }

            // Default to simulated
            diagnostics.SelectedAdapterName = "Simulated XR Adapter";
            diagnostics.SelectedKind = XRAdapterKind.Simulated;
            return new SimulatedXRAdapter();
        }
    }
}
