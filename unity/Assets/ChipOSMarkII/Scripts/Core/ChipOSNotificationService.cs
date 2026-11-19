using System.Collections.Generic;

namespace ChipOSMarkII
{
    /// <summary>
    /// Timed notification queue with priority ordering and optional pre-emption.
    /// </summary>
    public sealed class ChipOSNotificationService : IChipOSService
    {
        private const int MaxHistoryEntries = 12;
        private const float MinimumDurationSeconds = 0.25f;

        private readonly List<ChipOSNotification> pendingNotifications = new List<ChipOSNotification>();
        private readonly List<string> history = new List<string>(MaxHistoryEntries);

        private ChipOSEventBus eventBus;
        private ulong nextSequenceId;

        public string ServiceName => "Notification Queue";
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.Runtime;
        public bool IsServiceReady => true;
        public ChipOSNotification ActiveNotification { get; private set; }
        public bool HasActiveNotification => ActiveNotification != null;
        public int PendingCount => pendingNotifications.Count;
        public IReadOnlyList<string> History => history;

        public string GetServiceStatus()
        {
            var activeLabel = HasActiveNotification ? ActiveNotification.ToString() : "Idle";
            return $"{activeLabel} | Pending {PendingCount}";
        }

        public void Bind(ChipOSEventBus bus)
        {
            eventBus = bus;
        }

        public void Enqueue(string message, ChipOSNotificationPriority priority, float durationSeconds, string source)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var notification = new ChipOSNotification(
                ++nextSequenceId,
                message,
                priority,
                durationSeconds < MinimumDurationSeconds ? MinimumDurationSeconds : durationSeconds,
                source);

            if (HasActiveNotification && notification.priority > ActiveNotification.priority)
            {
                var suspendedNotification = ActiveNotification.Clone();
                suspendedNotification.remainingSeconds = suspendedNotification.remainingSeconds < MinimumDurationSeconds
                    ? MinimumDurationSeconds
                    : suspendedNotification.remainingSeconds;
                InsertPending(suspendedNotification);
                Activate(notification, "Preempted");
                return;
            }

            InsertPending(notification);
            RecordHistory($"Queued {notification.priority}: {notification.message}");
            eventBus?.Publish(new ChipOSNotificationEvent("Queued", notification, PendingCount));

            if (!HasActiveNotification)
            {
                ActivateNext();
            }
        }

        public void Tick(float deltaTime)
        {
            if (!HasActiveNotification || deltaTime <= 0f)
            {
                if (!HasActiveNotification)
                {
                    ActivateNext();
                }

                return;
            }

            ActiveNotification.remainingSeconds -= deltaTime;
            if (ActiveNotification.remainingSeconds > 0f)
            {
                return;
            }

            var completed = ActiveNotification;
            ActiveNotification = null;
            RecordHistory($"Dismissed {completed.priority}: {completed.message}");
            eventBus?.Publish(new ChipOSNotificationEvent("Dismissed", completed, PendingCount));
            ActivateNext();
        }

        public string GetActiveMessageOrDefault(string fallback)
        {
            return HasActiveNotification ? ActiveNotification.message : fallback;
        }

        public string GetHistorySummary(int maxEntries)
        {
            if (history.Count == 0)
            {
                return "None";
            }

            var startIndex = history.Count > maxEntries ? history.Count - maxEntries : 0;
            return string.Join(" | ", history.GetRange(startIndex, history.Count - startIndex).ToArray());
        }

        private void ActivateNext()
        {
            if (HasActiveNotification || pendingNotifications.Count == 0)
            {
                return;
            }

            var next = pendingNotifications[0];
            pendingNotifications.RemoveAt(0);
            Activate(next, "Activated");
        }

        private void Activate(ChipOSNotification notification, string action)
        {
            ActiveNotification = notification;
            ActiveNotification.remainingSeconds = ActiveNotification.remainingSeconds < MinimumDurationSeconds
                ? MinimumDurationSeconds
                : ActiveNotification.remainingSeconds;
            RecordHistory($"{action} {notification.priority}: {notification.message}");
            eventBus?.Publish(new ChipOSNotificationEvent(action, notification, PendingCount));
        }

        private void InsertPending(ChipOSNotification notification)
        {
            if (notification == null)
            {
                return;
            }

            var insertIndex = pendingNotifications.Count;
            for (var i = 0; i < pendingNotifications.Count; i++)
            {
                var existing = pendingNotifications[i];
                if (notification.priority > existing.priority)
                {
                    insertIndex = i;
                    break;
                }

                if (notification.priority == existing.priority && notification.sequenceId < existing.sequenceId)
                {
                    insertIndex = i;
                    break;
                }
            }

            pendingNotifications.Insert(insertIndex, notification);
        }

        private void RecordHistory(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            history.Add(line);
            while (history.Count > MaxHistoryEntries)
            {
                history.RemoveAt(0);
            }
        }
    }
}
