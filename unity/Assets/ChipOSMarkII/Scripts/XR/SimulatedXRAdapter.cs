using System;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Editor-safe simulated XR adapter. Behaves like a real hardware adapter without any real XR SDK.
    /// Now properly implements IChipOSService so it can be registered in ServiceRegistry.
    /// </summary>
    public sealed class SimulatedXRAdapter : IChipOSXRAdapter, IDisposable
    {
        private ChipOSXRAdapterState currentState = ChipOSXRAdapterState.Stopped;
        private ChipOSXRFrame latestFrame = ChipOSXRFrame.Default;
        private ChipOSXRCapabilities capabilities = ChipOSXRCapabilities.SimulatedDefault;
        private float timeSinceLastFrame;
        private bool isSimulatingMotion;
        private float simulatedConfidence = 1.0f;

        public string AdapterName => "Simulated XR Adapter (Editor)";
        public ChipOSXRAdapterState State => currentState;
        public ChipOSXRCapabilities Capabilities => capabilities;
        public ChipOSXRFrame LatestFrame => latestFrame;

        // IChipOSService implementation
        public string ServiceName => "Simulated XR Adapter";
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.XR;
        public bool IsServiceReady => currentState == ChipOSXRAdapterState.Running;

        public event Action<ChipOSXRAdapterState, ChipOSXRAdapterState> StateChanged;
        public event Action<ChipOSXRFrame> FrameUpdated;

        public void StartAdapter()
        {
            if (currentState == ChipOSXRAdapterState.Running) return;

            var previous = currentState;
            currentState = ChipOSXRAdapterState.Running;
            StateChanged?.Invoke(previous, currentState);

            latestFrame = CreateStableFrame();
            FrameUpdated?.Invoke(latestFrame);
        }

        public void StopAdapter()
        {
            if (currentState == ChipOSXRAdapterState.Stopped) return;

            var previous = currentState;
            currentState = ChipOSXRAdapterState.Stopped;
            StateChanged?.Invoke(previous, currentState);
        }

        public void ResetAdapter()
        {
            StopAdapter();
            latestFrame = ChipOSXRFrame.Default;
            simulatedConfidence = 1.0f;
            isSimulatingMotion = false;
            StartAdapter();
        }

        public void Tick(float deltaTime)
        {
            if (currentState != ChipOSXRAdapterState.Running) return;

            timeSinceLastFrame += deltaTime;

            if (timeSinceLastFrame >= 0.05f)
            {
                timeSinceLastFrame = 0f;
                latestFrame = CreateStableFrame();
                FrameUpdated?.Invoke(latestFrame);
            }
        }

        public string GetServiceStatus()
        {
            return $"{currentState} | Confidence: {simulatedConfidence:0.00} | Motion: {isSimulatingMotion}";
        }

        private ChipOSXRFrame CreateStableFrame()
        {
            var context = isSimulatingMotion ? ChipOSDeviceContext.Walking : ChipOSDeviceContext.Desk;
            var speed = isSimulatingMotion ? 1.2f : 0f;

            return new ChipOSXRFrame(
                Vector3.zero,
                Quaternion.identity,
                speed,
                context,
                ChipOSDeviceTrackingState.Tracking,
                false,
                85,
                false,
                simulatedConfidence,
                "Simulated XR frame");
        }

        public void SetSimulatingMotion(bool enabled)
        {
            isSimulatingMotion = enabled;
        }

        public void SetConfidence(float confidence)
        {
            simulatedConfidence = Mathf.Clamp01(confidence);
        }

        public void Dispose()
        {
            StopAdapter();
        }
    }
}