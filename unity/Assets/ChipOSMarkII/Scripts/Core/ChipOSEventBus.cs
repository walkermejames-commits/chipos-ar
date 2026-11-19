using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Typed in-process event bus used by the internal runtime kernel.
    /// </summary>
    public sealed class ChipOSEventBus : IChipOSService
    {
        private const int MaxTraceEntries = 32;

        private readonly Dictionary<Type, Delegate> handlersByType = new Dictionary<Type, Delegate>();
        private readonly List<ChipOSEventTraceEntry> recentTrace = new List<ChipOSEventTraceEntry>(MaxTraceEntries);

        public string ServiceName => "Runtime Event Bus";
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.Runtime;
        public bool IsServiceReady => true;
        public int PublishedEventCount { get; private set; }
        public int SubscriptionTypeCount => handlersByType.Count;
        public bool TraceEnabled { get; private set; }

        public string GetServiceStatus()
        {
            return $"Events {PublishedEventCount} | Trace {(TraceEnabled ? "On" : "Off")} | Topics {SubscriptionTypeCount}";
        }

        public void SetTracingEnabled(bool enabled)
        {
            TraceEnabled = enabled;
        }

        public void Subscribe<TEvent>(Action<TEvent> listener)
        {
            if (listener == null)
            {
                return;
            }

            var eventType = typeof(TEvent);
            if (handlersByType.TryGetValue(eventType, out var existing))
            {
                handlersByType[eventType] = ((Action<TEvent>)existing - listener) + listener;
                return;
            }

            handlersByType.Add(eventType, listener);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> listener)
        {
            if (listener == null)
            {
                return;
            }

            var eventType = typeof(TEvent);
            if (!handlersByType.TryGetValue(eventType, out var existing))
            {
                return;
            }

            var combined = (Action<TEvent>)existing - listener;
            if (combined == null)
            {
                handlersByType.Remove(eventType);
                return;
            }

            handlersByType[eventType] = combined;
        }

        public void Publish<TEvent>(TEvent eventData)
        {
            PublishedEventCount++;

            if (TraceEnabled)
            {
                AppendTrace(new ChipOSEventTraceEntry(typeof(TEvent).Name, DescribeEvent(eventData), DateTime.UtcNow.ToString("O")));
            }

            if (!handlersByType.TryGetValue(typeof(TEvent), out var existing))
            {
                return;
            }

            var handler = existing as Action<TEvent>;
            if (handler == null)
            {
                return;
            }

            foreach (var invocation in handler.GetInvocationList())
            {
                try
                {
                    ((Action<TEvent>)invocation)(eventData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ChipOSEventBus] Subscriber threw on {typeof(TEvent).Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        public IReadOnlyList<ChipOSEventTraceEntry> GetTraceSnapshot()
        {
            return recentTrace;
        }

        private void AppendTrace(ChipOSEventTraceEntry entry)
        {
            recentTrace.Add(entry);
            while (recentTrace.Count > MaxTraceEntries)
            {
                recentTrace.RemoveAt(0);
            }
        }

        private static string DescribeEvent<TEvent>(TEvent eventData)
        {
            if (eventData is null)
            {
                return "Null payload";
            }

            if (eventData is ChipEvent chipEvent)
            {
                return $"{chipEvent.Type}: {chipEvent.Summary}";
            }

            return eventData.ToString();
        }
    }
}
