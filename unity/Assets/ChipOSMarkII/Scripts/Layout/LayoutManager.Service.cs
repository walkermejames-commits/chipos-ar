namespace ChipOSMarkII
{
    public sealed partial class LayoutManager : IChipOSService
    {
        public string ServiceName => "Layout Manager";
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.UI;
        public bool IsServiceReady => app != null;

        public string GetServiceStatus()
        {
            return $"Current {CurrentMode} | Last Stable {LastStableMode} | Panels {RegisteredPanelCount}";
        }
    }
}
