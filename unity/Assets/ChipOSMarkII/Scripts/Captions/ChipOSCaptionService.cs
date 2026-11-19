using System;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Consumes simulated audio frames and emits mock caption events for HUD validation.
    /// </summary>
    public sealed class ChipOSCaptionService : IChipOSService
    {
        private static readonly string[] MockCaptions =
        {
            "Simulated caption stream active",
            "ChipOS audio pipeline online",
            "Caption frame received"
        };

        private readonly ChipOSCaptionDiagnostics diagnostics = new ChipOSCaptionDiagnostics();
        private IChipOSAudioSource audioSource;
        private ChipOSEventBus eventBus;
        private long nextCaptionSequence;
        private bool subscribed;

        public ChipOSCaptionDiagnostics Diagnostics => diagnostics;
        public ChipOSCaptionEvent LastCaption { get; private set; }

        public string ServiceName => "ChipOS Caption Service";
        public ChipOSServiceLayer ServiceLayer => ChipOSServiceLayer.Runtime;
        public bool IsServiceReady => audioSource != null && audioSource.IsSourceReady;

        public event Action<ChipOSCaptionEvent> CaptionProduced;

        public void Bind(IChipOSAudioSource source, ChipOSEventBus bus)
        {
            if (ReferenceEquals(audioSource, source) && ReferenceEquals(eventBus, bus) && subscribed)
            {
                return;
            }

            Unsubscribe();
            audioSource = source;
            eventBus = bus;
            Subscribe();

            if (audioSource == null)
            {
                diagnostics.latestError = "Audio source missing";
                Debug.LogWarning("[ChipOSCaptionService] Bound with null audio source; captions will not be produced.");
            }
            else
            {
                diagnostics.latestError = string.Empty;
            }
        }

        public void Unbind()
        {
            Unsubscribe();
            audioSource = null;
            eventBus = null;
        }

        public void ConsumeFrame(ChipOSAudioFrame frame)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // The Android player must never present generated demo strings as live captions.
            // Live captions arrive through ChipOSSpeechBridge after an explicit mic action.
            return;
#else
            if (frame == null)
            {
                diagnostics.latestError = "Null audio frame";
                return;
            }

            frame.Sanitize();
            diagnostics.framesConsumed++;

            if (!frame.IsValid)
            {
                diagnostics.latestError = "Invalid audio frame";
                return;
            }

            diagnostics.latestError = string.Empty;

            if (!frame.IsSpeechLike)
            {
                return;
            }

            ProduceMockCaption(frame);
#endif
        }

        public ChipOSCaptionEvent EmitManualMockCaption(string text = null)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            var caption = new ChipOSCaptionEvent(
                string.IsNullOrWhiteSpace(text) ? "Manual mock caption emitted" : text,
                1f,
                now,
                now,
                audioSource != null ? audioSource.SourceName : "ManualCaption",
                true,
                false,
                nextCaptionSequence++);

            PublishCaption(caption, 0d);
            return caption;
        }

        public string GetServiceStatus()
        {
            return diagnostics.GetCompactSummary();
        }

        private void Subscribe()
        {
            if (subscribed || audioSource == null)
            {
                return;
            }

            audioSource.AudioFrameProduced += ConsumeFrame;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || audioSource == null)
            {
                subscribed = false;
                return;
            }

            audioSource.AudioFrameProduced -= ConsumeFrame;
            subscribed = false;
        }

        private void ProduceMockCaption(ChipOSAudioFrame frame)
        {
            var captionIndex = (int)(nextCaptionSequence % MockCaptions.Length);
            var isPartial = nextCaptionSequence % 2 == 0;
            var now = Time.realtimeSinceStartupAsDouble;
            var caption = new ChipOSCaptionEvent(
                MockCaptions[captionIndex],
                0.72f + (captionIndex * 0.08f),
                frame.TimestampSeconds,
                now,
                frame.SourceId,
                !isPartial,
                isPartial,
                nextCaptionSequence++);

            PublishCaption(caption, Math.Max(0d, now - frame.TimestampSeconds));
        }

        private void PublishCaption(ChipOSCaptionEvent caption, double latencyEstimate)
        {
            if (caption == null)
            {
                diagnostics.latestError = "Null caption";
                return;
            }

            caption.Sanitize();
            LastCaption = caption;
            diagnostics.captionsProduced++;
            diagnostics.lastCaption = caption.text;
            diagnostics.lastConfidence = caption.confidence;
            diagnostics.lastLatencyEstimateSeconds = latencyEstimate;
            diagnostics.latestError = string.Empty;

            CaptionProduced?.Invoke(caption);
            eventBus?.Publish(new ChipOSCaptionProducedEvent(caption));
        }
    }
}
