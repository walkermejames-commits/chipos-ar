using System.Collections.Generic;
using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Local task list for the current ChipOS editor shell.
    /// </summary>
    public sealed class ChipTaskBoard
    {
        private readonly List<ChipTaskItem> tasks = new List<ChipTaskItem>
        {
            new ChipTaskItem("Check workspace", "Confirm the HUD layout and base panels are alive."),
            new ChipTaskItem("Review notifications", "Check recent local notices in the shell."),
            new ChipTaskItem("Prepare captions layer", "Reserve space for future text and subtitle modules."),
            new ChipTaskItem("Test walking layout", "Verify readable spacing in motion-oriented mode."),
            new ChipTaskItem("Test focus mode", "Confirm minimal distraction layout behavior.")
        };

        private int currentIndex;

        public IReadOnlyList<ChipTaskItem> Tasks => tasks;
        public int CurrentIndex => tasks.Count == 0 ? -1 : currentIndex;
        public ChipTaskItem CurrentTask => tasks.Count == 0 ? null : tasks[currentIndex];
        public ChipTaskItem NextTask => tasks.Count < 2 ? null : tasks[(currentIndex + 1) % tasks.Count];
        public ChipTaskItem ParkedTask => tasks.Count < 3 ? null : tasks[(currentIndex + 2) % tasks.Count];

        public void Reset()
        {
            currentIndex = 0;
        }

        public bool RestoreSelection(int preferredIndex, string preferredTitle)
        {
            if (tasks.Count == 0)
            {
                currentIndex = 0;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(preferredTitle))
            {
                for (var i = 0; i < tasks.Count; i++)
                {
                    if (string.Equals(tasks[i].title, preferredTitle.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        currentIndex = i;
                        return true;
                    }
                }
            }

            if (preferredIndex >= 0 && preferredIndex < tasks.Count)
            {
                currentIndex = preferredIndex;
                return true;
            }

            currentIndex = 0;
            return false;
        }

        public ChipTaskItem Cycle()
        {
            if (tasks.Count == 0)
            {
                return null;
            }

            currentIndex = (currentIndex + 1) % tasks.Count;
            return CurrentTask;
        }
    }
}
