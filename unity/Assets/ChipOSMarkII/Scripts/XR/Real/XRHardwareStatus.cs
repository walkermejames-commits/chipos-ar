namespace ChipOSMarkII
{
    public struct XRHardwareStatus
    {
        public bool SdkCompiled;
        public bool HardwareAvailable;
        public bool TrackingAvailable;
        public bool PoseAvailable;
        public bool BatteryAvailable;
        public string LatestError;
        public string StatusMessage;

        public static XRHardwareStatus Unavailable => new XRHardwareStatus
        {
            SdkCompiled = false,
            HardwareAvailable = false,
            TrackingAvailable = false,
            PoseAvailable = false,
            BatteryAvailable = false,
            StatusMessage = "No real XR hardware available"
        };
    }
}