using System;

namespace ChipOSMarkII
{
    [Serializable]
    public sealed class ChipOSCaptionDiagnostics
    {
        public long framesConsumed;
        public long captionsProduced;
        public string lastCaption = "No captions yet";
        public float lastConfidence;
        public double lastLatencyEstimateSeconds;
        public string latestError = string.Empty;

        public string GetCompactSummary()
        {
            var error = string.IsNullOrWhiteSpace(latestError) ? "OK" : latestError.Trim();
            return $"Captions {captionsProduced} | Frames {framesConsumed} | Conf {lastConfidence:0.00} | {lastCaption} | {error}";
        }

        public string GetDetailedSummary()
        {
            var error = string.IsNullOrWhiteSpace(latestError) ? "None" : latestError.Trim();
            return
                $"Frames Consumed: {framesConsumed}\n" +
                $"Captions Produced: {captionsProduced}\n" +
                $"Last Caption: {lastCaption}\n" +
                $"Last Confidence: {lastConfidence:0.00}\n" +
                $"Last Latency Estimate: {lastLatencyEstimateSeconds:0.000}s\n" +
                $"Latest Error: {error}";
        }
    }
}
