using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Lightweight trace line recorded when runtime event tracing is enabled.
    /// </summary>
    [Serializable]
    public readonly struct ChipOSEventTraceEntry
    {
        public readonly string eventTypeName;
        public readonly string summary;
        public readonly string timestampUtc;

        public ChipOSEventTraceEntry(string eventTypeName, string summary, string timestampUtc)
        {
            this.eventTypeName = string.IsNullOrWhiteSpace(eventTypeName) ? "UnknownEvent" : eventTypeName.Trim();
            this.summary = string.IsNullOrWhiteSpace(summary) ? "No summary" : summary.Trim();
            this.timestampUtc = string.IsNullOrWhiteSpace(timestampUtc) ? DateTime.UtcNow.ToString("O") : timestampUtc;
        }

        public override string ToString()
        {
            return $"{eventTypeName} // {summary}";
        }
    }
}
