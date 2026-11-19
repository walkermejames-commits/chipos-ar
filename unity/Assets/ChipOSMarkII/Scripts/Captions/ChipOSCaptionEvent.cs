using System;

namespace ChipOSMarkII
{
    [Serializable]
    public sealed class ChipOSCaptionEvent
    {
        public string text = "Caption unavailable";
        public float confidence;
        public double startTimestampSeconds;
        public double endTimestampSeconds;
        public string sourceId = "UnknownAudioSource";
        public bool IsFinal = true;
        public bool IsPartial;
        public long sequenceNumber;

        public ChipOSCaptionEvent()
        {
            Sanitize();
        }

        public ChipOSCaptionEvent(
            string text,
            float confidence,
            double startTimestampSeconds,
            double endTimestampSeconds,
            string sourceId,
            bool isFinal,
            bool isPartial,
            long sequenceNumber)
        {
            this.text = text;
            this.confidence = confidence;
            this.startTimestampSeconds = startTimestampSeconds;
            this.endTimestampSeconds = endTimestampSeconds;
            this.sourceId = sourceId;
            IsFinal = isFinal;
            IsPartial = isPartial;
            this.sequenceNumber = sequenceNumber;
            Sanitize();
        }

        public void Sanitize()
        {
            text = string.IsNullOrWhiteSpace(text) ? "Caption unavailable" : text.Trim();
            confidence = UnityEngine.Mathf.Clamp01(confidence);
            startTimestampSeconds = double.IsNaN(startTimestampSeconds) || double.IsInfinity(startTimestampSeconds) ? 0d : Math.Max(0d, startTimestampSeconds);
            endTimestampSeconds = double.IsNaN(endTimestampSeconds) || double.IsInfinity(endTimestampSeconds) ? startTimestampSeconds : Math.Max(startTimestampSeconds, endTimestampSeconds);
            sourceId = string.IsNullOrWhiteSpace(sourceId) ? "UnknownAudioSource" : sourceId.Trim();
            sequenceNumber = Math.Max(0, sequenceNumber);
            if (!IsFinal && !IsPartial)
            {
                IsFinal = true;
            }
        }

        public string GetCompactSummary()
        {
            Sanitize();
            var mode = IsFinal ? "final" : "partial";
            return $"#{sequenceNumber} {mode} {confidence:0.00}: {text}";
        }

        public override string ToString()
        {
            return GetCompactSummary();
        }
    }
}
