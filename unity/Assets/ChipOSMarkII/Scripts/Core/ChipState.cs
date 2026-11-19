using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Central runtime state model for the local ChipOS shell.
    /// </summary>
    [Serializable]
    public sealed class ChipState
    {
        public bool systemReady;
        public bool hudVisible = true;
        public bool panicMode;
        public ChipAppMode appMode = ChipAppMode.Boot;
        public LayoutMode layoutMode = LayoutMode.Default;
        public ChipAlertLevel alertLevel = ChipAlertLevel.Notice;
        public ChipInputSourceKind inputSource = ChipInputSourceKind.Unknown;
        public string assistantStatus = "Booting";
        public string activeTask = "System boot";
        public string currentMode = "Default";
        public string trackingStatus = "Desktop simulation";
        public string inputStatus = "Idle";
        public string systemMessage = "CHIPOS MARK II // waking interface";
        public string lastEvent = "None";
        public string lastStateChangeUtc = "";
        public List<string> notifications = new List<string>();

        public void EnsureInitialized()
        {
            lastStateChangeUtc = string.IsNullOrWhiteSpace(lastStateChangeUtc) ? DateTime.UtcNow.ToString("O") : lastStateChangeUtc;
            systemMessage = string.IsNullOrWhiteSpace(systemMessage) ? "CHIPOS MARK II // waking interface" : systemMessage;
            lastEvent = string.IsNullOrWhiteSpace(lastEvent) ? "None" : lastEvent;
            assistantStatus = string.IsNullOrWhiteSpace(assistantStatus) ? "Booting" : assistantStatus;
            activeTask = string.IsNullOrWhiteSpace(activeTask) ? "System boot" : activeTask;
            currentMode = string.IsNullOrWhiteSpace(currentMode) ? layoutMode.ToString() : currentMode;
            trackingStatus = string.IsNullOrWhiteSpace(trackingStatus) ? "Desktop simulation" : trackingStatus;
            inputStatus = string.IsNullOrWhiteSpace(inputStatus) ? "Idle" : inputStatus;
            notifications ??= new List<string>();
            layoutMode = Enum.TryParse(currentMode, true, out LayoutMode parsedMode) ? parsedMode : layoutMode;
            SyncLegacyStrings();
        }

        public void SetAppMode(ChipAppMode mode, string statusText = "")
        {
            appMode = mode;
            if (!string.IsNullOrWhiteSpace(statusText))
            {
                assistantStatus = statusText;
            }

            Touch();
        }

        public void SetCurrentMode(LayoutMode mode)
        {
            layoutMode = mode;
            Touch();
        }

        public void SetHudVisible(bool visible)
        {
            hudVisible = visible;
            Touch();
        }

        public void SetPanicMode(bool active)
        {
            panicMode = active;
            Touch();
        }

        public void SetAlertLevel(ChipAlertLevel level)
        {
            alertLevel = level;
            Touch();
        }

        public void SetInput(ChipInputSourceKind source, string label)
        {
            inputSource = source;
            inputStatus = string.IsNullOrWhiteSpace(label) ? "Input received" : label;
            Touch();
        }

        public void SetSystemMessage(string message)
        {
            systemMessage = string.IsNullOrWhiteSpace(message) ? systemMessage : message.Trim();
            Touch();
        }

        public void SetActiveTask(string task)
        {
            activeTask = string.IsNullOrWhiteSpace(task) ? activeTask : task.Trim();
            Touch();
        }

        public void SetTrackingStatus(string status)
        {
            trackingStatus = string.IsNullOrWhiteSpace(status) ? trackingStatus : status.Trim();
            Touch();
        }

        public void SetLastEvent(string summary)
        {
            lastEvent = string.IsNullOrWhiteSpace(summary) ? "None" : summary.Trim();
            Touch();
        }

        public void AddNotification(string message, int maxEntries = 8)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            notifications.Add(message.Trim());

            while (notifications.Count > maxEntries)
            {
                notifications.RemoveAt(0);
            }

            Touch();
        }

        private void Touch()
        {
            lastStateChangeUtc = DateTime.UtcNow.ToString("O");
            SyncLegacyStrings();
        }

        private void SyncLegacyStrings()
        {
            currentMode = layoutMode.ToString();
            assistantStatus = string.IsNullOrWhiteSpace(assistantStatus)
                ? appMode.ToString()
                : assistantStatus;
        }
    }
}
