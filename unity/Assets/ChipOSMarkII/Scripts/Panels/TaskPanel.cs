using UnityEngine;

namespace ChipOSMarkII
{
    public sealed class TaskPanel : HudPanel
    {
        private TextSlot titleLabel;
        private TextSlot tasksLabel;
        private TextSlot footerLabel;
        private ChipState latestState;

        public string CurrentTask => App != null ? App.CurrentTaskTitle : "No active task";
        public int CurrentTaskIndex => App != null ? App.CurrentTaskIndex : -1;

        public void CycleTask()
        {
            App?.CycleTask();
        }

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
                new Vector2(0f, 0.78f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero,
                26,
                TextAnchor.UpperLeft,
                FontStyle.Bold,
                AccentColor);

            tasksLabel = CreateLabel(
                "Tasks",
                contentRoot,
                new Vector2(0f, 0.14f),
                new Vector2(1f, 0.78f),
                Vector2.zero,
                Vector2.zero,
                20,
                TextAnchor.UpperLeft,
                FontStyle.Normal,
                Color.white);

            footerLabel = CreateLabel(
                "Footer",
                contentRoot,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.14f),
                Vector2.zero,
                Vector2.zero,
                16,
                TextAnchor.LowerLeft,
                FontStyle.Italic,
                new Color(0.74f, 0.86f, 1f, 1f));
        }

        protected override void OnPanelInitialized()
        {
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            var currentTask = App != null ? App.TaskBoard.CurrentTask : null;
            var nextTask = App != null ? App.TaskBoard.NextTask : null;
            var parkedTask = App != null ? App.TaskBoard.ParkedTask : null;
            var taskIndex = App != null ? App.CurrentTaskIndex : -1;
            var displayIndex = taskIndex >= 0 ? taskIndex + 1 : 0;

            titleLabel?.SetText("Tasks");
            tasksLabel?.SetText(
                $"Now: {(currentTask != null ? currentTask.title : "No active task")}\n" +
                $"Next: {(nextTask != null ? nextTask.title : "None")}\n" +
                $"Parked: {(parkedTask != null ? parkedTask.title : "None")}\n" +
                $"Mode: {(latestState != null ? latestState.appMode.ToString() : "Boot")}");
            footerLabel?.SetText(
                $"Task #{displayIndex}  |  {(currentTask != null ? currentTask.description : "Press T to cycle tasks")}");
        }
    }
}
