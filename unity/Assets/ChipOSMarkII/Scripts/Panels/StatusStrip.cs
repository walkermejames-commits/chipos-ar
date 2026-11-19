using System;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Thin always-on status module that keeps key state readable without taking much visual space.
    /// </summary>
    public sealed class StatusStrip : HudPanel
    {
        private TextSlot statusLabel;
        private ChipState latestState;
        private float nextRefreshTime;

        public override void Refresh(ChipState state)
        {
            latestState = state;
            UpdateDisplay();
        }

        protected override void BuildContent(RectTransform contentRoot)
        {
            statusLabel = CreateLabel(
                "StatusLine",
                contentRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                18,
                TextAnchor.MiddleLeft,
                FontStyle.Normal,
                new Color(0.92f, 0.96f, 1f, 1f));
        }

        private void Update()
        {
            if (latestState == null || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.25f;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (latestState == null)
            {
                return;
            }

            var runtime = App != null ? App.Runtime : null;
            SetBackgroundTint(latestState.panicMode
                ? new Color(0.12f, 0.05f, 0.05f, 0.94f)
                : new Color(0.03f, 0.06f, 0.09f, 0.84f));

            var task = App != null ? App.CurrentTaskTitle : latestState.activeTask;
            var notification = runtime != null
                ? runtime.NotificationService.GetActiveMessageOrDefault(latestState.systemMessage)
                : latestState.systemMessage;
            statusLabel?.SetText(
                $"[{DateTime.Now:HH:mm:ss}] " +
                $"Mode {latestState.appMode} | " +
                $"Layout {latestState.layoutMode} | " +
                $"HUD {latestState.hudVisible} | " +
                $"Panic {latestState.panicMode} | " +
                $"Task {task} | " +
                $"Input {latestState.inputStatus} | " +
                $"Notice {notification}");
        }
    }
}
