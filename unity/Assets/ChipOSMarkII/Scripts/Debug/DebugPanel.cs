using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Editor-friendly diagnostics overlay. Shows essential runtime + XR Stability Harness status.
    /// Automatically hidden in release (non-debug) builds to prevent information disclosure.
    /// </summary>
    public sealed class DebugPanel : HudPanel
    {
        private TextSlot titleLabel;
        private TextSlot detailsLabel;
        private ChipState latestState;
        private float nextRefreshTime;
        private float smoothedDeltaTime = 0.016f;

        private void Awake()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
            {
                gameObject.SetActive(false);
            }
        }

        public override void Refresh(ChipState state)
        {
            latestState = state;
            UpdateDisplay(true);
        }

        protected override void BuildContent(RectTransform contentRoot)
        {
            titleLabel = CreateLabel(
                "Title",
                contentRoot,
                new Vector2(0f, 0.78f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero,
                24,
                TextAnchor.UpperLeft,
                FontStyle.Bold,
                AccentColor);

            detailsLabel = CreateLabel(
                "Details",
                contentRoot,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.78f),
                Vector2.zero,
                Vector2.zero,
                18,
                TextAnchor.UpperLeft,
                FontStyle.Normal,
                Color.white);
        }

        private void Update()
        {
            smoothedDeltaTime += (Time.unscaledDeltaTime - smoothedDeltaTime) * 0.12f;

            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.2f;
            UpdateDisplay(false);
        }

        private void UpdateDisplay(bool force)
        {
            if (latestState == null && !force) return;

            var state = latestState;
            if (state == null) return;

            var runtime = App != null ? App.Runtime : null;

            var fps = runtime != null ? runtime.SmoothedFps : (smoothedDeltaTime > 0.0001f ? 1f / smoothedDeltaTime : 0f);
            var uptime = runtime != null ? runtime.GetUptimeSummary() : "00:00";
            var layout = App != null ? App.ActiveLayout.ToString() : state.layoutMode.ToString();
            var device = runtime != null ? runtime.GetDeviceSnapshot() : null;
            var deviceContext = device != null ? device.userContext.ToString() : "Unknown";
            var tracking = device != null ? device.trackingState.ToString() : "?";
            var battery = device != null ? device.GetBatterySummary() : "n/a";
            var comfort = device != null ? device.GetComfortSummary() : "?";
            var notification = runtime != null ? runtime.GetActiveNotificationSummary() : "None";
            var serviceCount = runtime != null ? runtime.Services.Count : 0;
            var eventCount = runtime != null ? runtime.PublishedEventCount : 0;
            var audio = runtime != null ? runtime.AudioSource : null;
            var audioDiagnostics = audio != null ? audio.Diagnostics : null;
            var captionDiagnostics = runtime != null && runtime.CaptionService != null ? runtime.CaptionService.Diagnostics : null;

            // XR Stability Harness
            string xrLine = "XR: unavailable";
            if (runtime != null && runtime.XRDiagnostics != null)
            {
                var d = runtime.XRDiagnostics;
                xrLine = $"XR: {d.SelectedAdapterName} [{d.SelectedKind}] | {d.AdapterState} | Fallback:{d.IsFallbackActive} | Frames:{d.FramesAppliedCount}";
            }

            // Context Policy (if available)
            string policyLine = "Policy: n/a";
            if (runtime != null && runtime.ContextPolicyService != null)
            {
                policyLine = $"Policy: {runtime.ContextPolicyService.GetStatusSummary()}";
            }

            var audioLine = audioDiagnostics != null
                ? $"Audio: {audioDiagnostics.activeSourceName} | {audioDiagnostics.state} | {audioDiagnostics.sampleRateHz}Hz | Frames:{audioDiagnostics.framesProduced} | RMS:{audioDiagnostics.lastRmsLevel:0.000} | Speech:{audioDiagnostics.speechLike}{FormatError(audioDiagnostics.latestError)}"
                : "Audio: unavailable";

            var captionLine = captionDiagnostics != null
                ? $"Captions: {captionDiagnostics.captionsProduced} | {captionDiagnostics.lastConfidence:0.00} | {captionDiagnostics.lastCaption}{FormatError(captionDiagnostics.latestError)}"
                : "Captions: unavailable";

            titleLabel?.SetText("Runtime Status");
            detailsLabel?.SetText(
                $"FPS: {fps:0.0} | Uptime: {uptime}\n" +
                $"Layout: {layout} | Context: {deviceContext} | Track: {tracking}\n" +
                $"Battery: {battery} | Comfort: {comfort}\n" +
                $"Notification: {notification}\n" +
                $"{xrLine}\n" +
                $"{policyLine}\n" +
                $"{audioLine}\n" +
                $"{captionLine}\n" +
                $"Services: {serviceCount} | Events: {eventCount}");
        }

        private static string FormatError(string latestError)
        {
            return string.IsNullOrWhiteSpace(latestError) ? string.Empty : $" | Err:{latestError.Trim()}";
        }
    }
}
