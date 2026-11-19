namespace ChipOSMarkII
{
    /// <summary>
    /// Shared lifecycle contract for HUD-facing ChipOS modules.
    /// </summary>
    public interface IChipHudModule
    {
        string ModuleId { get; }

        void InitializeModule(ChipOSApp app, UnityEngine.Transform canvasParent);
        void HandleStateChanged(ChipState state);
        void HandleChipEvent(ChipEvent chipEvent);
        void ShowModule();
        void HideModule();
        void UpdateDisplayedContent();
        string GetDiagnosticsSummary();
    }
}
