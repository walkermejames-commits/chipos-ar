using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Timed runtime notification used by the local shell queue.
    /// </summary>
    [Serializable]
    public sealed class ChipOSNotification
    {
        public ulong sequenceId;
        public string message;
        public string source;
        public string createdUtc;
        public float durationSeconds;
        public float remainingSeconds;
        public ChipOSNotificationPriority priority;

        public ChipOSNotification(ulong sequenceId, string message, ChipOSNotificationPriority priority, float durationSeconds, string source)
        {
            this.sequenceId = sequenceId;
            this.message = string.IsNullOrWhiteSpace(message) ? "Notification" : message.Trim();
            this.priority = priority;
            this.durationSeconds = durationSeconds;
            remainingSeconds = durationSeconds;
            this.source = string.IsNullOrWhiteSpace(source) ? "Runtime" : source.Trim();
            createdUtc = DateTime.UtcNow.ToString("O");
        }

        public ChipOSNotification Clone()
        {
            return new ChipOSNotification(sequenceId, message, priority, durationSeconds, source)
            {
                createdUtc = createdUtc,
                remainingSeconds = remainingSeconds
            };
        }

        public override string ToString()
        {
            return $"{priority}: {message}";
        }
    }
}
