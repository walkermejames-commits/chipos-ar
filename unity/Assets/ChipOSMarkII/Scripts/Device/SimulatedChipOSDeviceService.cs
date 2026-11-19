using System;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Editor-safe simulated device adapter that stands in for future XR hardware integrations.
    /// Now supports direct XR frame application from XRAdapterDeviceBridge.
    /// </summary>
    public sealed class SimulatedChipOSDeviceService : IChipOSDeviceService
    {
        private const int MinimumBatteryLevel = 0;
        private const int MaximumBatteryLevel = 100;
        private const float MaximumPitchDegrees = 60f;

        private ChipOSDeviceSnapshot currentSnapshot;

        public SimulatedChipOSDeviceService()
        {
            currentSnapshot = CreateDefaultSnapshot();
        }

        public string ServiceName => "Simulated Device Service";
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.XR;
        public bool IsServiceReady => true;
        public string DeviceName => currentSnapshot.deviceName;
        public ChipOSDeviceMode DeviceMode => currentSnapshot.deviceMode;
        public ChipOSDeviceSnapshot CurrentSnapshot => currentSnapshot.Clone();

        public event Action<ChipOSDeviceSnapshot, ChipOSDeviceSnapshot> SnapshotChanged;

        public string GetServiceStatus()
        {
            return $"{currentSnapshot.deviceMode} | {currentSnapshot.trackingState} | {currentSnapshot.userContext} | {currentSnapshot.GetBatterySummary()}";
        }

        // === NEW: XR Frame Application ===
        public bool ApplyXRFrame(ChipOSXRFrame frame)
        {
            if (frame == null) return false;

            var next = currentSnapshot.Clone();

            next.headPose = new ChipOSDevicePose(frame.HeadPosition, frame.HeadRotation.eulerAngles);
            next.movementSpeedMetersPerSecond = frame.MovementSpeed;
            next.userContext = frame.InferredContext;
            next.trackingState = frame.TrackingState;
            next.comfortWarning = frame.ComfortWarning;
            next.batteryLevel = frame.BatteryLevel;
            next.isCharging = frame.IsCharging;

            return ApplySnapshot(next);
        }

        public bool SetContext(ChipOSDeviceContext context)
        {
            var next = currentSnapshot.Clone();
            next.userContext = context;
            next.movementSpeedMetersPerSecond = ResolveMovementSpeed(context);
            return ApplySnapshot(next);
        }

        public bool AdjustBattery(int deltaPercent)
        {
            if (deltaPercent == 0) return false;

            var next = currentSnapshot.Clone();
            next.batteryLevel = Mathf.Clamp(next.batteryLevel + deltaPercent, MinimumBatteryLevel, MaximumBatteryLevel);
            return ApplySnapshot(next);
        }

        public bool ToggleCharging()
        {
            return SetCharging(!currentSnapshot.isCharging);
        }

        public bool SetCharging(bool isCharging)
        {
            var next = currentSnapshot.Clone();
            next.isCharging = isCharging;
            return ApplySnapshot(next);
        }

        public ChipOSDeviceTrackingState CycleTrackingState()
        {
            var nextState = currentSnapshot.trackingState switch
            {
                ChipOSDeviceTrackingState.Unavailable => ChipOSDeviceTrackingState.Limited,
                ChipOSDeviceTrackingState.Limited => ChipOSDeviceTrackingState.Tracking,
                ChipOSDeviceTrackingState.Tracking => ChipOSDeviceTrackingState.Lost,
                _ => ChipOSDeviceTrackingState.Unavailable
            };

            SetTrackingState(nextState);
            return nextState;
        }

        public bool SetTrackingState(ChipOSDeviceTrackingState state)
        {
            var next = currentSnapshot.Clone();
            next.trackingState = state;
            return ApplySnapshot(next);
        }

        public bool ToggleComfortWarning()
        {
            return SetComfortWarning(!currentSnapshot.comfortWarning);
        }

        public bool SetComfortWarning(bool enabled)
        {
            var next = currentSnapshot.Clone();
            next.comfortWarning = enabled;
            return ApplySnapshot(next);
        }

        public bool AdjustHeadPose(float yawDeltaDegrees, float pitchDeltaDegrees)
        {
            if (Mathf.Approximately(yawDeltaDegrees, 0f) && Mathf.Approximately(pitchDeltaDegrees, 0f)) return false;

            var next = currentSnapshot.Clone();
            var pitch = Mathf.Clamp(next.headPose.Pitch + pitchDeltaDegrees, -MaximumPitchDegrees, MaximumPitchDegrees);
            var yaw = WrapAngle(next.headPose.Yaw + yawDeltaDegrees);
            next.headPose.eulerAngles = new Vector3(pitch, yaw, 0f);
            return ApplySnapshot(next);
        }

        public void ResetState()
        {
            ApplySnapshot(CreateDefaultSnapshot(), true);
        }

        public bool IsDefaultSnapshot(ChipOSDeviceSnapshot snapshot)
        {
            if (snapshot == null) return false;

            var baseline = CreateDefaultSnapshot();
            return AreSnapshotsEquivalent(snapshot, baseline);
        }

        private bool ApplySnapshot(ChipOSDeviceSnapshot nextSnapshot, bool forceDispatch = false)
        {
            if (nextSnapshot == null) return false;

            nextSnapshot.Sanitize();
            nextSnapshot.lastUpdatedUtc = DateTime.UtcNow.ToString("O");

            if (!forceDispatch && AreSnapshotsEquivalent(currentSnapshot, nextSnapshot)) return false;

            var previous = currentSnapshot.Clone();
            currentSnapshot = nextSnapshot;
            SnapshotChanged?.Invoke(previous, currentSnapshot.Clone());
            return true;
        }

        private ChipOSDeviceSnapshot CreateDefaultSnapshot()
        {
            var defaultContext = ChipOSDeviceContext.Desk;
            var snapshot = new ChipOSDeviceSnapshot
            {
                deviceName = Application.isEditor ? "ChipOS Editor Rig" : "ChipOS Glasses Sim",
                deviceMode = Application.isEditor ? ChipOSDeviceMode.EditorSimulation : ChipOSDeviceMode.GlassesSimulation,
                batteryLevel = 100,
                isCharging = false,
                trackingState = ChipOSDeviceTrackingState.Tracking,
                headPose = new ChipOSDevicePose(Vector3.zero, Vector3.zero),
                movementSpeedMetersPerSecond = ResolveMovementSpeed(defaultContext),
                userContext = defaultContext,
                comfortWarning = false,
                lastUpdatedUtc = DateTime.UtcNow.ToString("O")
            };

            snapshot.Sanitize();
            return snapshot;
        }

        private static bool AreSnapshotsEquivalent(ChipOSDeviceSnapshot a, ChipOSDeviceSnapshot b)
        {
            if (a == null || b == null) return false;

            return string.Equals(a.deviceName, b.deviceName, StringComparison.Ordinal) &&
                   a.deviceMode == b.deviceMode &&
                   a.batteryLevel == b.batteryLevel &&
                   a.isCharging == b.isCharging &&
                   a.trackingState == b.trackingState &&
                   a.userContext == b.userContext &&
                   a.comfortWarning == b.comfortWarning &&
                   Mathf.Approximately(a.movementSpeedMetersPerSecond, b.movementSpeedMetersPerSecond) &&
                   Vector3.Distance(a.headPose.position, b.headPose.position) < 0.0001f &&
                   Vector3.Distance(a.headPose.eulerAngles, b.headPose.eulerAngles) < 0.0001f;
        }

        private static float ResolveMovementSpeed(ChipOSDeviceContext context)
        {
            return context switch
            {
                ChipOSDeviceContext.Walking => 1.25f,
                ChipOSDeviceContext.Focus => 0.05f,
                ChipOSDeviceContext.Unknown => 0.1f,
                _ => 0f
            };
        }

        private static float WrapAngle(float degrees)
        {
            var wrapped = degrees;
            while (wrapped > 180f) wrapped -= 360f;
            while (wrapped < -180f) wrapped += 360f;
            return wrapped;
        }
    }
}