using System;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Abstract base for real XR adapters (XREAL, NRSDK, OpenXR).
    /// Provides safe defaults and common behaviour.
    /// </summary>
    public abstract class RealXRAdapterBase : IChipOSXRAdapter, IDisposable
    {
        protected ChipOSXRAdapterState currentState = ChipOSXRAdapterState.Stopped;
        protected ChipOSXRFrame latestFrame = ChipOSXRFrame.Default;
        protected ChipOSXRCapabilities capabilities = new ChipOSXRCapabilities { SupportsRealHardware = true };

        public abstract string AdapterName { get; }

        public ChipOSXRAdapterState State => currentState;
        public ChipOSXRCapabilities Capabilities => capabilities;
        public ChipOSXRFrame LatestFrame => latestFrame;

        public event Action<ChipOSXRAdapterState, ChipOSXRAdapterState> StateChanged;
        public event Action<ChipOSXRFrame> FrameUpdated;

        public string ServiceName => AdapterName;
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.XR;
        public bool IsServiceReady => currentState == ChipOSXRAdapterState.Running;

        public virtual void StartAdapter()
        {
            if (currentState == ChipOSXRAdapterState.Running) return;
            var previous = currentState;
            currentState = ChipOSXRAdapterState.Running;
            StateChanged?.Invoke(previous, currentState);
        }

        public virtual void StopAdapter()
        {
            if (currentState == ChipOSXRAdapterState.Stopped) return;
            var previous = currentState;
            currentState = ChipOSXRAdapterState.Stopped;
            StateChanged?.Invoke(previous, currentState);
        }

        public virtual void ResetAdapter()
        {
            StopAdapter();
            latestFrame = ChipOSXRFrame.Default;
            StartAdapter();
        }

        public virtual void Tick(float deltaTime)
        {
            // Real adapters override this to read real hardware
        }

        public virtual string GetServiceStatus()
        {
            return $"{currentState} | Real Hardware Path";
        }

        public void Dispose()
        {
            StopAdapter();
        }

        protected void UpdateFrame(ChipOSXRFrame newFrame)
        {
            latestFrame = newFrame;
            FrameUpdated?.Invoke(latestFrame);
        }

        protected void SetState(ChipOSXRAdapterState newState)
        {
            if (currentState == newState) return;
            var previous = currentState;
            currentState = newState;
            StateChanged?.Invoke(previous, currentState);
        }
    }
}