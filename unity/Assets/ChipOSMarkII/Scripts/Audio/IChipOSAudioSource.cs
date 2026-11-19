using System;

namespace ChipOSMarkII
{
    public interface IChipOSAudioSource : IChipOSService
    {
        string SourceName { get; }
        ChipOSAudioSourceState State { get; }
        ChipOSAudioDiagnostics Diagnostics { get; }
        bool IsSourceReady { get; }

        event Action<ChipOSAudioFrame> AudioFrameProduced;
        event Action<ChipOSAudioSourceState> StateChanged;

        void StartSource();
        void StopSource();
        void ResetSource();
        void Tick(float deltaTime);
    }
}
