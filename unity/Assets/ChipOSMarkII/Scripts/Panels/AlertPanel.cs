using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Calm, high-contrast alert surface used for local notices and panic recovery messaging.
    /// </summary>
    public sealed class AlertPanel : HudPanel
    {
        private TextSlot titleLabel;
        private TextSlot messageLabel;
        private TextSlot footerLabel;
        private ChipState latestState;

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
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero,
                28,
                TextAnchor.UpperLeft,
                FontStyle.Bold,
                Color.white);

            messageLabel = CreateLabel(
                "Message",
                contentRoot,
                new Vector2(0f, 0.2f),
                new Vector2(1f, 0.72f),
                Vector2.zero,
                Vector2.zero,
                20,
                TextAnchor.UpperLeft,
                FontStyle.Normal,
                new Color(0.94f, 0.97f, 1f, 1f));

            footerLabel = CreateLabel(
                "Footer",
                contentRoot,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.2f),
                Vector2.zero,
                Vector2.zero,
                16,
                TextAnchor.LowerLeft,
                FontStyle.Italic,
                new Color(0.8f, 0.9f, 1f, 1f));
        }

        private void UpdateDisplay()
        {
            if (latestState == null)
            {
                return;
            }

            var runtime = App != null ? App.Runtime : null;
            var notificationMessage = runtime != null
                ? runtime.NotificationService.GetActiveMessageOrDefault(latestState.systemMessage)
                : latestState.systemMessage;
            var notificationLine = runtime != null ? runtime.GetActiveNotificationSummary() : "None";

            SetBackgroundTint(ResolveTint(latestState));
            titleLabel?.SetText(ResolveTitle(latestState));
            messageLabel?.SetText(
                $"Message: {notificationMessage}\n" +
                $"Notification: {notificationLine}\n" +
                $"Alert: {latestState.alertLevel}\n" +
                $"Mode: {latestState.appMode}  |  Layout: {latestState.layoutMode}");
            footerLabel?.SetText(latestState.panicMode
                ? "Press O to restore the previous layout."
                : "Local-only alert surface // no network services active.");
        }

        private static string ResolveTitle(ChipState state)
        {
            if (state == null)
            {
                return "Alert";
            }

            if (state.panicMode)
            {
                return "Panic Mode";
            }

            switch (state.alertLevel)
            {
                case ChipAlertLevel.Critical:
                    return "Critical Alert";
                case ChipAlertLevel.Warning:
                    return "Warning";
                case ChipAlertLevel.Notice:
                    return "Notice";
                case ChipAlertLevel.Normal:
                default:
                    return "Status";
            }
        }

        private static Color ResolveTint(ChipState state)
        {
            if (state == null)
            {
                return new Color(0.08f, 0.1f, 0.14f, 0.92f);
            }

            if (state.panicMode || state.alertLevel == ChipAlertLevel.Critical)
            {
                return new Color(0.18f, 0.08f, 0.08f, 0.96f);
            }

            if (state.alertLevel == ChipAlertLevel.Warning)
            {
                return new Color(0.2f, 0.13f, 0.08f, 0.94f);
            }

            if (state.alertLevel == ChipAlertLevel.Notice)
            {
                return new Color(0.06f, 0.12f, 0.17f, 0.92f);
            }

            return new Color(0.06f, 0.1f, 0.14f, 0.88f);
        }
    }
}
