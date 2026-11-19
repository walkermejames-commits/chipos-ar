using System;

namespace ChipOSMarkII
{
    [Serializable]
    public sealed class ChipOSAudioDiagnostics
    {
        public string activeSourceName = "No audio source";
        public ChipOSAudioSourceState state = ChipOSAudioSourceState.Stopped;
        public int sampleRateHz = ChipOSAudioFrame.DefaultSampleRateHz;
        public int channels = ChipOSAudioFrame.DefaultChannels;
        public int bitsPerSample = ChipOSAudioFrame.DefaultBitsPerSample;
        public long framesProduced;
        public long droppedFrames;
        public double lastFrameTimestamp;
        public float lastRmsLevel;
        public bool speechLike;
        public string latestError = string.Empty;

        public string GetCompactSummary()
        {
            var error = string.IsNullOrWhiteSpace(latestError) ? "OK" : latestError.Trim();
            return $"{activeSourceName} | {state} | {sampleRateHz}Hz | Frames {framesProduced} | RMS {lastRmsLevel:0.000} | Speech {speechLike} | {error}";
        }

        public string GetDetailedSummary()
        {
            var error = string.IsNullOrWhiteSpace(latestError) ? "None" : latestError.Trim();
            return
                $"Audio Source: {activeSourceName}\n" +
                $"State: {state}\n" +
                $"Format: {sampleRateHz} Hz, {channels} ch, {bitsPerSample}-bit\n" +
                $"Frames Produced: {framesProduced}\n" +
                $"Dropped Frames: {droppedFrames}\n" +
                $"Last Timestamp: {lastFrameTimestamp:0.000}\n" +
                $"Last RMS: {lastRmsLevel:0.000}\n" +
                $"Speech-like: {speechLike}\n" +
                $"Latest Error: {error}";
        }
    }
}
