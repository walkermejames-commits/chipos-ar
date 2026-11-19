using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Persisted runtime snapshot for restoring the local shell on startup.
    /// </summary>
    [Serializable]
    public sealed class ChipOSRuntimeState
    {
        public string layoutMode = LayoutMode.Default.ToString();
        public bool hudVisible = true;
        public string activeTaskTitle = "System boot";
        public int activeTaskIndex;
        public bool debugEventTracingEnabled;
        public bool verboseServiceStatusEnabled = true;
        public string savedAtUtc = "";

        public void Sanitize()
        {
            layoutMode = string.IsNullOrWhiteSpace(layoutMode) ? LayoutMode.Default.ToString() : layoutMode.Trim();
            activeTaskTitle = string.IsNullOrWhiteSpace(activeTaskTitle) ? "System boot" : activeTaskTitle.Trim();
            activeTaskIndex = activeTaskIndex < 0 ? 0 : activeTaskIndex;
            savedAtUtc = string.IsNullOrWhiteSpace(savedAtUtc) ? DateTime.UtcNow.ToString("O") : savedAtUtc;
        }

        public bool TryGetRestorableLayoutMode(out LayoutMode mode)
        {
            mode = LayoutMode.Default;

            if (string.IsNullOrWhiteSpace(layoutMode) || !Enum.TryParse(layoutMode, true, out LayoutMode parsedMode))
            {
                return false;
            }

            if (parsedMode == LayoutMode.Panic)
            {
                return false;
            }

            mode = parsedMode;
            return true;
        }
    }
}
