using System;
using UnityEngine;

namespace ChipOSMarkII
{
    public sealed class SimulatedAudioSource : IChipOSAudioSource
    {
        private const string DefaultSourceName = "Simulated Audio Source";
        private const string DefaultSourceId = "SimulatedAudio";
        private const float DefaultFrameIntervalSeconds = 0.25f;
        private const float SpeechAmplitude = 0.35f;
        private const int MaxFramesPerTick = 4;

        private readonly ChipOSAudioDiagnostics diagnostics = new ChipOSAudioDiagnostics();
        private ChipOSAudioSourceState state = ChipOSAudioSourceState.Stopped;
        private float frameIntervalSeconds = DefaultFrameIntervalSeconds;
        private float accumulatedTime;
        private long nextSequenceNumber;
        private bool speechSimulationEnabled = true;

        public string SourceName => DefaultSourceName;
        public ChipOSAudioSourceState State => state;
        public ChipOSAudioDiagnostics Diagnostics => diagnostics;
        public bool IsSourceReady => state == ChipOSAudioSourceState.Running || state == ChipOSAudioSourceState.Muted;
        public bool SpeechSimulationEnabled => speechSimulationEnabled;

        public string ServiceName => SourceName;
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.Runtime;
        public bool IsServiceReady => IsSourceReady;

        public event Action<ChipOSAudioFrame> AudioFrameProduced;
        public event Action<ChipOSAudioSourceState> StateChanged;

        public SimulatedAudioSource()
        {
            RefreshDiagnostics();
        }

        public void StartSource()
        {
            if (state == ChipOSAudioSourceState.Running)
            {
                return;
            }

            SetState(ChipOSAudioSourceState.Starting);
            accumulatedTime = 0f;
            diagnostics.latestError = string.Empty;
            SetState(ChipOSAudioSourceState.Running);
        }

        public void StopSource()
        {
            if (state == ChipOSAudioSourceState.Stopped)
            {
                return;
            }

            accumulatedTime = 0f;
            SetState(ChipOSAudioSourceState.Stopped);
        }

        public void ResetSource()
        {
            var wasRunning = state == ChipOSAudioSourceState.Running || state == ChipOSAudioSourceState.Muted;
            accumulatedTime = 0f;
            nextSequenceNumber = 0;
            diagnostics.framesProduced = 0;
            diagnostics.droppedFrames = 0;
            diagnostics.lastFrameTimestamp = 0d;
            diagnostics.lastRmsLevel = 0f;
            diagnostics.speechLike = false;
            diagnostics.latestError = string.Empty;
            SetState(wasRunning ? ChipOSAudioSourceState.Running : ChipOSAudioSourceState.Stopped);
        }

        public void Tick(float deltaTime)
        {
            if (state != ChipOSAudioSourceState.Running && state != ChipOSAudioSourceState.Muted)
            {
                return;
            }

            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            accumulatedTime += deltaTime;
            var producedThisTick = 0;

            while (accumulatedTime >= frameIntervalSeconds && producedThisTick < MaxFramesPerTick)
            {
                accumulatedTime -= frameIntervalSeconds;
                ProduceFrame();
                producedThisTick++;
            }

            if (accumulatedTime >= frameIntervalSeconds)
            {
                diagnostics.droppedFrames++;
                accumulatedTime = 0f;
            }
        }

        public void SetSpeechSimulationEnabled(bool enabled)
        {
            if (speechSimulationEnabled == enabled)
            {
                return;
            }

            speechSimulationEnabled = enabled;
            diagnostics.speechLike = false;
        }

        public bool ToggleSpeechSimulation()
        {
            SetSpeechSimulationEnabled(!speechSimulationEnabled);
            return speechSimulationEnabled;
        }

        public ChipOSAudioFrame ProduceFrameForTest(bool speechLike)
        {
            return CreateFrame(speechLike, nextSequenceNumber++);
        }

        public string GetServiceStatus()
        {
            return diagnostics.GetCompactSummary();
        }

        private void ProduceFrame()
        {
            var sequence = nextSequenceNumber++;
            var speechLike = state != ChipOSAudioSourceState.Muted && speechSimulationEnabled && ((sequence / 4) % 2 == 0);
            var frame = CreateFrame(speechLike, sequence);

            diagnostics.framesProduced++;
            diagnostics.lastFrameTimestamp = frame.TimestampSeconds;
            diagnostics.lastRmsLevel = frame.RmsLevel;
            diagnostics.speechLike = frame.IsSpeechLike;
            diagnostics.latestError = string.Empty;

            AudioFrameProduced?.Invoke(frame);
        }

        private ChipOSAudioFrame CreateFrame(bool speechLike, long sequenceNumber)
        {
            var sampleRate = ChipOSAudioFrame.DefaultSampleRateHz;
            var channels = ChipOSAudioFrame.DefaultChannels;
            var bitsPerSample = ChipOSAudioFrame.DefaultBitsPerSample;
            var sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * frameIntervalSeconds));
            var data = new byte[sampleCount * channels * (bitsPerSample / 8)];
            var sumSquares = 0d;

            for (var i = 0; i < sampleCount; i++)
            {
                short sample = 0;
                if (speechLike)
                {
                    var phase = (sequenceNumber * sampleCount + i) * 0.043f;
                    sample = (short)(Mathf.Sin(phase) * short.MaxValue * SpeechAmplitude);
                }

                var byteIndex = i * 2;
                data[byteIndex] = (byte)(sample & 0xff);
                data[byteIndex + 1] = (byte)((sample >> 8) & 0xff);
                var normalized = sample / (float)short.MaxValue;
                sumSquares += normalized * normalized;
            }

            var rms = sampleCount > 0 ? Mathf.Sqrt((float)(sumSquares / sampleCount)) : 0f;
            return new ChipOSAudioFrame(
                data,
                sampleRate,
                channels,
                bitsPerSample,
                sequenceNumber,
                Time.realtimeSinceStartupAsDouble,
                DefaultSourceId,
                rms,
                speechLike && rms > 0.02f);
        }

        private void SetState(ChipOSAudioSourceState nextState)
        {
            if (state == nextState)
            {
                RefreshDiagnostics();
                return;
            }

            state = nextState;
            RefreshDiagnostics();
            StateChanged?.Invoke(state);
        }

        private void RefreshDiagnostics()
        {
            diagnostics.activeSourceName = SourceName;
            diagnostics.state = state;
            diagnostics.sampleRateHz = ChipOSAudioFrame.DefaultSampleRateHz;
            diagnostics.channels = ChipOSAudioFrame.DefaultChannels;
            diagnostics.bitsPerSample = ChipOSAudioFrame.DefaultBitsPerSample;
        }
    }
}
