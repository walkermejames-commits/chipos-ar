namespace ChipOSMarkII
{
    /// <summary>
    /// Input providers translate a concrete source into ChipOS commands.
    /// </summary>
    public interface IChipInputProvider
    {
        ChipInputSourceKind SourceKind { get; }

        bool TryReadCommand(float currentTime, out ChipInputCommand command);
    }
}
