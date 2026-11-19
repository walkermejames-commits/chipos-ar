namespace ChipOSMarkII
{
    /// <summary>
    /// Reports which real XR SDK compile symbols are defined.
    /// </summary>
    public static class XRSDKAvailability
    {
        public static bool IsXrealSymbolDefined =>
#if CHIPOS_XREAL || CHIPOS_NRSDK
            true;
#else
            false;
#endif

        public static bool IsNRSDKSymbolDefined =>
#if CHIPOS_NRSDK
            true;
#else
            false;
#endif

        public static bool IsOpenXRSymbolDefined =>
#if CHIPOS_OPENXR
            true;
#else
            false;
#endif

        public static bool HasAnyRealXRCompiled => IsXrealSymbolDefined || IsNRSDKSymbolDefined || IsOpenXRSymbolDefined;

        public static string GetAvailabilitySummary()
        {
            if (HasAnyRealXRCompiled)
                return "Real XR SDK compiled";
            return "No real XR SDK compiled (simulated fallback active)";
        }
    }
}