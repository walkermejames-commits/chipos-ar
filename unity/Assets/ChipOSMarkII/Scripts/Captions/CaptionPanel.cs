using UnityEngine;

namespace ChipOSMarkII
{
    public sealed class CaptionPanel : HudPanel
    {
        private TextSlot titleLabel;
        private TextSlot captionLabel;
        private TextSlot statusLabel;
        private ChipOSCaptionService subscribedService;
        private ChipOSCaptionEvent latestCaption;

        protected override void BuildContent(RectTransform contentRoot)
        {
            titleLabel = CreateLabel(
                "Title",
                contentRoot,
                new Vector2(0f, 0.76f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero,
                20,
                TextAnchor.UpperLeft,
                FontStyle.Bold,
                AccentColor);

            captionLabel = CreateLabel(
                "Caption",
                contentRoot,
                new Vector2(0f, 0.22f),
                new Vector2(1f, 0.78f),
                Vector2.zero,
                Vector2.zero,
                24,
                TextAnchor.MiddleLeft,
                FontStyle.Normal,
                Color.white);

            statusLabel = CreateLabel(
                "Status",
                contentRoot,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.22f),
                Vector2.zero,
                Vector2.zero,
                15,
                TextAnchor.LowerLeft,
                FontStyle.Normal,
                new Color(0.72f, 0.88f, 0.95f, 1f));
        }

        public override void Refresh(ChipState state)
        {
            EnsureServiceSubscription();
            UpdateDisplay();
        }

        public override string GetDiagnosticsSummary()
        {
            var caption = latestCaption != null ? latestCaption.GetCompactSummary() : "No caption";
            return $"{PanelId} | Visible: {IsRuntimeVisible} | Ready: {HasRuntimeView} | {caption}";
        }

        protected override void OnPanelInitialized()
        {
            EnsureServiceSubscription();
            UpdateDisplay();
        }

        protected override void OnPanelDisabled()
        {
            UnsubscribeFromCaptionService();
        }

        protected override void OnPanelDestroyed()
        {
            UnsubscribeFromCaptionService();
        }

        private void EnsureServiceSubscription()
        {
            var runtime = App != null ? App.Runtime : null;
            var service = runtime != null ? runtime.CaptionService : null;
            if (ReferenceEquals(subscribedService, service))
            {
                return;
            }

            UnsubscribeFromCaptionService();
            subscribedService = service;

            if (subscribedService != null)
            {
                subscribedService.CaptionProduced += HandleCaptionProduced;
                latestCaption = subscribedService.LastCaption;
            }
        }

        private void UnsubscribeFromCaptionService()
        {
            if (subscribedService != null)
            {
                subscribedService.CaptionProduced -= HandleCaptionProduced;
            }

            subscribedService = null;
        }

        private void HandleCaptionProduced(ChipOSCaptionEvent caption)
        {
            latestCaption = caption;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            titleLabel?.SetText("Captions");

            if (subscribedService == null)
            {
                captionLabel?.SetText("Caption service unavailable");
                statusLabel?.SetText("Audio layer waiting");
                return;
            }

            var captionText = latestCaption != null ? latestCaption.text : "Live captions start from Creative Studio.";
#if UNITY_ANDROID && !UNITY_EDITOR
            var mode = latestCaption == null ? "microphone off · offline speech available in Studio" : latestCaption.IsFinal ? "final" : "partial";
#else
            var mode = latestCaption == null ? "editor simulation" : latestCaption.IsFinal ? "simulated final" : "simulated partial";
#endif
            captionLabel?.SetText(captionText);
            statusLabel?.SetText($"{mode} | {subscribedService.Diagnostics.captionsProduced} captions | {subscribedService.Diagnostics.lastConfidence:0.00} confidence");
        }
    }
}
