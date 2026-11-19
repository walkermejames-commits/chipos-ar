using System;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Immutable-style snapshot of the current device state exposed to runtime consumers.
    /// </summary>
    [Serializable]
    public sealed class ChipOSDeviceSnapshot
    {
        public string deviceName = "ChipOS Device";
        public ChipOSDeviceMode deviceMode = ChipOSDeviceMode.EditorSimulation;
        public int batteryLevel = 100;
        public bool isCharging;
        public ChipOSDeviceTrackingState trackingState = ChipOSDeviceTrackingState.Tracking;
        public ChipOSDevicePose headPose = new ChipOSDevicePose(Vector3.zero, Vector3.zero);
        public float movementSpeedMetersPerSecond;
        public ChipOSDeviceContext userContext = ChipOSDeviceContext.Desk;
        public bool comfortWarning;
        public string lastUpdatedUtc = string.Empty;

        public ChipOSDeviceSnapshot Clone()
        {
            return new ChipOSDeviceSnapshot
            {
                deviceName = deviceName,
                deviceMode = deviceMode,
                batteryLevel = batteryLevel,
                isCharging = isCharging,
                trackingState = trackingState,
                headPose = headPose,
                movementSpeedMetersPerSecond = movementSpeedMetersPerSecond,
                userContext = userContext,
                comfortWarning = comfortWarning,
                lastUpdatedUtc = lastUpdatedUtc
            };
        }

        public void Sanitize()
        {
            deviceName = string.IsNullOrWhiteSpace(deviceName) ? "ChipOS Device" : deviceName.Trim();
            batteryLevel = Mathf.Clamp(batteryLevel, 0, 100);
            movementSpeedMetersPerSecond = float.IsNaN(movementSpeedMetersPerSecond) || float.IsInfinity(movementSpeedMetersPerSecond)
                ? 0f
                : Mathf.Max(0f, movementSpeedMetersPerSecond);
            headPose.Sanitize();
            lastUpdatedUtc = string.IsNullOrWhiteSpace(lastUpdatedUtc) ? DateTime.UtcNow.ToString("O") : lastUpdatedUtc;
        }

        public string GetBatterySummary()
        {
            return $"{batteryLevel}%{(isCharging ? " charging" : string.Empty)}";
        }

        public string GetComfortSummary()
        {
            return comfortWarning ? "Warning" : "Clear";
        }

        public string GetPoseSummary()
        {
            return headPose.ToCompactSummary();
        }

        public string GetTrackingSummary()
        {
            return $"{trackingState} // {userContext}";
        }

        public string GetCompactSummary()
        {
            return $"{deviceName} [{deviceMode}] // {trackingState} // {userContext} // {GetBatterySummary()}";
        }
    }
}
