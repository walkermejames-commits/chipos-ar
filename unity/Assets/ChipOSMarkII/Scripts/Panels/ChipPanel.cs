using System;
using UnityEngine;

namespace ChipOSMarkII
{
    public sealed class ChipPanel : HudPanel
    {
        private TextSlot titleLabel;
        private TextSlot clockLabel;
        private TextSlot modeLabel;
        private TextSlot statusLabel;
        private TextSlot taskLabel;
        private ChipState latestState;
        private float nextClockRefresh;

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
                new Vector2(0f, 0.72f),
                new Vector2(0.65f, 1f),
                Vector2.zero,
                Vector2.zero,
                28,
                TextAnchor.UpperLeft,
                FontStyle.Bold,
                AccentColor);

            clockLabel = CreateLabel(
                "Clock",
                contentRoot,
                new Vector2(0.58f, 0.72f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero,
                26,
                TextAnchor.UpperRight,
                FontStyle.Bold,
                Color.white);

            modeLabel = CreateLabel(
                "Mode",
                contentRoot,
                new Vector2(0f, 0.54f),
                new Vector2(1f, 0.72f),
                Vector2.zero,
                Vector2.zero,
                18,
                TextAnchor.UpperLeft,
                FontStyle.Bold,
                new Color(0.76f, 0.89f, 1f, 1f));

            statusLabel = CreateLabel(
                "Status",
                contentRoot,
                new Vector2(0f, 0.2f),
                new Vector2(1f, 0.54f),
                Vector2.zero,
                Vector2.zero,
                19,
                TextAnchor.UpperLeft,
                FontStyle.Normal,
                new Color(0.87f, 0.93f, 0.99f, 1f));

            taskLabel = CreateLabel(
                "Task",
                contentRoot,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.28f),
                Vector2.zero,
                Vector2.zero,
                18,
                TextAnchor.LowerLeft,
                FontStyle.Italic,
                new Color(0.76f, 0.89f, 1f, 1f));
        }

        private void Update()
        {
            if (latestState == null || Time.unscaledTime < nextClockRefresh)
            {
                return;
            }

            nextClockRefresh = Time.unscaledTime + 0.25f;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (latestState == null)
            {
                return;
            }

            SetBackgroundTint(ResolveBackgroundTint(latestState));
            titleLabel?.SetText("ChipOS Mark II");
            clockLabel?.SetText(DateTime.Now.ToString("HH:mm:ss"));
            modeLabel?.SetText(
                $"Mode: {latestState.appMode}  |  Layout: {latestState.layoutMode}  |  Alert: {latestState.alertLevel}");
            statusLabel?.SetText(
                $"Status: {latestState.assistantStatus}\n" +
                $"Message: {latestState.systemMessage}\n" +
                $"Tracking: {latestState.trackingStatus}\n" +
                $"Ready: {latestState.systemReady}  |  HUD: {latestState.hudVisible}");
            taskLabel?.SetText($"Active Task: {latestState.activeTask}  |  Last Input: {latestState.inputStatus}");
        }

        private Color ResolveBackgroundTint(ChipState state)
        {
            if (state == null)
            {
                return BackgroundColor;
            }

            if (state.panicMode || state.alertLevel == ChipAlertLevel.Critical)
            {
                return new Color(0.18f, 0.08f, 0.1f, 0.94f);
            }

            if (state.alertLevel == ChipAlertLevel.Warning)
            {
                return new Color(0.16f, 0.11f, 0.08f, 0.92f);
            }

            if (state.alertLevel == ChipAlertLevel.Notice)
            {
                return new Color(0.05f, 0.09f, 0.14f, 0.9f);
            }

            return BackgroundColor;
        }
    }
}
