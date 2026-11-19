using System.Text;
using UnityEngine;

namespace ChipOSMarkII
{
    public sealed class EnvironmentPanel : HudPanel
    {
        private const float SampleInterval = 0.2f;

        private TextSlot titleLabel;
        private TextSlot summaryLabel;
        private TextSlot notificationsLabel;
        private ChipState latestState;
        private float smoothedFrameMs = 16.67f;
        private float nextSampleTime;

        public override void Refresh(ChipState state)
        {
            latestState = state;
            UpdateDisplay();
        }

        protected override void BuildContent(RectTransform contentRoot)
        {
            titleLabel = CreateLabel(
                "Title",
                contentRoot,
                new Vector2(0f, 0.8f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero,
                24,
                TextAnchor.UpperLeft,
                FontStyle.Bold,
                AccentColor);

            summaryLabel = CreateLabel(
                "Summary",
                contentRoot,
                new Vector2(0f, 0.32f),
                new Vector2(1f, 0.8f),
                Vector2.zero,
                Vector2.zero,
                17,
                TextAnchor.UpperLeft,
                FontStyle.Normal,
                Color.white);

            notificationsLabel = CreateLabel(
                "Notifications",
                contentRoot,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.32f),
                Vector2.zero,
                Vector2.zero,
                15,
                TextAnchor.LowerLeft,
                FontStyle.Italic,
                new Color(0.74f, 0.86f, 1f, 1f));
        }

        private void Update()
        {
            smoothedFrameMs = Mathf.Lerp(smoothedFrameMs, Time.unscaledDeltaTime * 1000f, 0.15f);

            if (latestState == null || Time.unscaledTime < nextSampleTime)
            {
                return;
            }

            nextSampleTime = Time.unscaledTime + SampleInterval;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (latestState == null)
            {
                return;
            }

            titleLabel?.SetText("Environment");
            summaryLabel?.SetText(BuildSummary());
            notificationsLabel?.SetText(BuildNotifications());
        }

        private string BuildSummary()
        {
            var fps = smoothedFrameMs <= 0.01f ? 0f : 1000f / smoothedFrameMs;
            var width = Screen.width;
            var height = Screen.height;
            var targetRate = Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() : "Platform default";
            var headsetMode = Application.isEditor ? "Editor HUD sim" : "Standalone runtime";

            return
                $"Display: {width} x {height}\n" +
                $"Frame: {fps:0} fps | {smoothedFrameMs:0.0} ms\n" +
                $"Target Rate: {targetRate}\n" +
                $"Mode: {headsetMode}\n" +
                $"Tracking: {latestState.trackingStatus}\n" +
                $"Device: {SystemInfo.deviceModel}";
        }

        private string BuildNotifications()
        {
            if (latestState.notifications == null || latestState.notifications.Count == 0)
            {
                return "Recent: No notifications yet";
            }

            var builder = new StringBuilder("Recent:");
            var startIndex = Mathf.Max(0, latestState.notifications.Count - 3);

            for (var i = startIndex; i < latestState.notifications.Count; i++)
            {
                builder.Append("\n- ");
                builder.Append(latestState.notifications[i]);
            }

            return builder.ToString();
        }
    }
}
