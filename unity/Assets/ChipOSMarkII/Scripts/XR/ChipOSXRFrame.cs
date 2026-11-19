using System;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Immutable snapshot of one XR frame from the adapter.
    /// </summary>
    public sealed class ChipOSXRFrame
    {
        public DateTime Timestamp { get; }
        public Vector3 HeadPosition { get; }
        public Quaternion HeadRotation { get; }
        public float MovementSpeed { get; }
        public ChipOSDeviceContext InferredContext { get; }
        public ChipOSDeviceTrackingState TrackingState { get; }
        public bool ComfortWarning { get; }
        public int BatteryLevel { get; }
        public bool IsCharging { get; }
        public float Confidence { get; }
        public string StatusMessage { get; }

        public ChipOSXRFrame(
            Vector3 headPosition,
            Quaternion headRotation,
            float movementSpeed,
            ChipOSDeviceContext inferredContext,
            ChipOSDeviceTrackingState trackingState,
            bool comfortWarning,
            int batteryLevel,
            bool isCharging,
            float confidence,
            string statusMessage = null)
        {
            Timestamp = DateTime.UtcNow;
            HeadPosition = headPosition;
            HeadRotation = headRotation;
            MovementSpeed = Mathf.Max(0f, movementSpeed);
            InferredContext = inferredContext;
            TrackingState = trackingState;
            ComfortWarning = comfortWarning;
            BatteryLevel = Mathf.Clamp(batteryLevel, 0, 100);
            IsCharging = isCharging;
            Confidence = Mathf.Clamp01(confidence);
            StatusMessage = statusMessage ?? string.Empty;
        }

        public static ChipOSXRFrame Default => new ChipOSXRFrame(
            Vector3.zero,
            Quaternion.identity,
            0f,
            ChipOSDeviceContext.Desk,
            ChipOSDeviceTrackingState.Tracking,
            false,
            100,
            false,
            1.0f,
            "Default simulated frame");
    }
}