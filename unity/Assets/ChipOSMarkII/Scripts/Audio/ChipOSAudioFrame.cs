using System;
using UnityEngine;

namespace ChipOSMarkII
{
    [Serializable]
    public sealed class ChipOSAudioFrame
    {
        public const int DefaultSampleRateHz = 16000;
        public const int DefaultChannels = 1;
        public const int DefaultBitsPerSample = 16;

        public byte[] PcmData = Array.Empty<byte>();
        public int SampleRateHz = DefaultSampleRateHz;
        public int Channels = DefaultChannels;
        public int BitsPerSample = DefaultBitsPerSample;
        public long SequenceNumber;
        public double TimestampSeconds;
        public string SourceId = "SimulatedAudio";
        public float RmsLevel;
        public bool IsSpeechLike;

        public bool IsValid =>
            PcmData != null &&
            PcmData.Length > 0 &&
            SampleRateHz > 0 &&
            Channels > 0 &&
            BitsPerSample > 0 &&
            !string.IsNullOrWhiteSpace(SourceId);

        public ChipOSAudioFrame()
        {
            Sanitize();
        }

        public ChipOSAudioFrame(
            byte[] pcmData,
            int sampleRateHz,
            int channels,
            int bitsPerSample,
            long sequenceNumber,
            double timestampSeconds,
            string sourceId,
            float rmsLevel,
            bool isSpeechLike)
        {
            PcmData = pcmData ?? Array.Empty<byte>();
            SampleRateHz = sampleRateHz;
            Channels = channels;
            BitsPerSample = bitsPerSample;
            SequenceNumber = sequenceNumber;
            TimestampSeconds = timestampSeconds;
            SourceId = sourceId;
            RmsLevel = rmsLevel;
            IsSpeechLike = isSpeechLike;
            Sanitize();
        }

        public void Sanitize()
        {
            PcmData ??= Array.Empty<byte>();
            SampleRateHz = SampleRateHz <= 0 ? DefaultSampleRateHz : SampleRateHz;
            Channels = Channels <= 0 ? DefaultChannels : Channels;
            BitsPerSample = BitsPerSample <= 0 ? DefaultBitsPerSample : BitsPerSample;
            SequenceNumber = Math.Max(0, SequenceNumber);
            TimestampSeconds = double.IsNaN(TimestampSeconds) || double.IsInfinity(TimestampSeconds) ? 0d : Math.Max(0d, TimestampSeconds);
            SourceId = string.IsNullOrWhiteSpace(SourceId) ? "UnknownAudioSource" : SourceId.Trim();
            RmsLevel = float.IsNaN(RmsLevel) || float.IsInfinity(RmsLevel) ? 0f : Mathf.Clamp01(RmsLevel);
        }

        public string GetCompactSummary()
        {
            Sanitize();
            return $"{SourceId} #{SequenceNumber} | {SampleRateHz}Hz {Channels}ch {BitsPerSample}bit | RMS {RmsLevel:0.000} | Speech {IsSpeechLike}";
        }
    }
}
