using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Read-only device boundary that future XR adapters can implement without coupling UI to SDKs.
    /// </summary>
    public interface IChipOSDeviceService : IChipOSService
    {
        string DeviceName { get; }
        ChipOSDeviceMode DeviceMode { get; }
        ChipOSDeviceSnapshot CurrentSnapshot { get; }

        event Action<ChipOSDeviceSnapshot, ChipOSDeviceSnapshot> SnapshotChanged;
    }
}
