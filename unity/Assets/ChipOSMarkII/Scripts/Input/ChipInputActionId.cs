namespace ChipOSMarkII
{
    /// <summary>
    /// Logical input actions understood by the ChipOS app layer.
    /// </summary>
    public enum ChipInputActionId
    {
        ToggleHud = 0,
        ResetLayout = 1,
        Panic = 2,
        Restore = 3,
        CycleTask = 4,
        DefaultLayout = 5,
        DeskLayout = 6,
        WalkingLayout = 7,
        FocusLayout = 8,
        MinimalLayout = 9,
        ToggleSimulatedAudio = 10,
        ToggleSimulatedSpeech = 11,
        EmitMockCaption = 12,
        ToggleCreativeStudio = 13
    }
}
