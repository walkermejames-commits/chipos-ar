namespace ChipOSMarkII
{
    /// <summary>
    /// One logical input command produced by an input source.
    /// </summary>
    public readonly struct ChipInputCommand
    {
        public readonly ChipInputActionId Action;
        public readonly ChipInputSourceKind Source;
        public readonly string Label;

        public ChipInputCommand(ChipInputActionId action, ChipInputSourceKind source, string label)
        {
            Action = action;
            Source = source;
            Label = string.IsNullOrWhiteSpace(label) ? action.ToString() : label;
        }
    }
}
