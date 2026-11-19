using System;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Small in-process event bus for ChipOS modules.
    /// </summary>
    public sealed class ChipEventBus
    {
        private event Action<ChipEvent> EventPublished;

        public ChipEvent LastEvent { get; private set; }
        public bool HasPublishedEvent { get; private set; }

        public void Subscribe(Action<ChipEvent> listener)
        {
            if (listener == null)
            {
                return;
            }

            EventPublished -= listener;
            EventPublished += listener;
        }

        public void Unsubscribe(Action<ChipEvent> listener)
        {
            if (listener == null)
            {
                return;
            }

            EventPublished -= listener;
        }

        public void Publish(ChipEvent chipEvent)
        {
            LastEvent = chipEvent;
            HasPublishedEvent = true;

            var handler = EventPublished;
            if (handler == null)
            {
                return;
            }

            foreach (var invocation in handler.GetInvocationList())
            {
                try
                {
                    ((Action<ChipEvent>)invocation)(chipEvent);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ChipEventBus] Subscriber threw on event '{chipEvent.Type}': {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }
}
