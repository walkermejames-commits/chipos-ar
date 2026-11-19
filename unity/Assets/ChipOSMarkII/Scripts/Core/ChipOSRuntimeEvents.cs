using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Typed events published through the internal runtime event bus.
    /// </summary>
    public readonly struct ChipOSServiceRegistryEvent
    {
        public readonly string action;
        public readonly string serviceName;
        public readonly string serviceTypeName;
        public readonly string status;
        public readonly string timestampUtc;

        public ChipOSServiceRegistryEvent(string action, Type serviceType, IChipOSService service)
        {
            this.action = string.IsNullOrWhiteSpace(action) ? "Updated" : action.Trim();
            serviceName = service != null ? service.ServiceName : "Unknown Service";
            serviceTypeName = serviceType != null ? serviceType.Name : "UnknownType";
            status = service != null ? service.GetServiceStatus() : "Unavailable";
            timestampUtc = DateTime.UtcNow.ToString("O");
        }

        public override string ToString()
        {
            return $"{action}: {serviceName} ({serviceTypeName})";
        }
    }

    public readonly struct ChipOSNotificationEvent
    {
        public readonly string action;
        public readonly string message;
        public readonly ChipOSNotificationPriority priority;
        public readonly int pendingCount;
        public readonly string timestampUtc;

        public ChipOSNotificationEvent(string action, ChipOSNotification notification, int pendingCount)
        {
            this.action = string.IsNullOrWhiteSpace(action) ? "Updated" : action.Trim();
            message = notification != null ? notification.message : "Notification";
            priority = notification != null ? notification.priority : ChipOSNotificationPriority.Normal;
            this.pendingCount = pendingCount;
            timestampUtc = DateTime.UtcNow.ToString("O");
        }

        public override string ToString()
        {
            return $"{action}: {priority} // {message}";
        }
    }

    public readonly struct ChipOSRuntimeStateEvent
    {
        public readonly string action;
        public readonly string layoutMode;
        public readonly bool hudVisible;
        public readonly string activeTaskTitle;
        public readonly string timestampUtc;

        public ChipOSRuntimeStateEvent(string action, ChipOSRuntimeState state)
        {
            this.action = string.IsNullOrWhiteSpace(action) ? "Saved" : action.Trim();
            layoutMode = state != null ? state.layoutMode : LayoutMode.Default.ToString();
            hudVisible = state != null && state.hudVisible;
            activeTaskTitle = state != null ? state.activeTaskTitle : "No active task";
            timestampUtc = DateTime.UtcNow.ToString("O");
        }

        public override string ToString()
        {
            return $"{action}: {layoutMode} // HUD {hudVisible}";
        }
    }

    public readonly struct ChipOSDebugSettingsChangedEvent
    {
        public readonly bool eventTracingEnabled;
        public readonly bool verboseServiceStatusEnabled;
        public readonly string timestampUtc;

        public ChipOSDebugSettingsChangedEvent(bool eventTracingEnabled, bool verboseServiceStatusEnabled)
        {
            this.eventTracingEnabled = eventTracingEnabled;
            this.verboseServiceStatusEnabled = verboseServiceStatusEnabled;
            timestampUtc = DateTime.UtcNow.ToString("O");
        }

        public override string ToString()
        {
            return $"Trace {(eventTracingEnabled ? "On" : "Off")} // Services {(verboseServiceStatusEnabled ? "Verbose" : "Compact")}";
        }
    }

    public readonly struct ChipOSDeviceSnapshotChangedEvent
    {
        public readonly string deviceName;
        public readonly ChipOSDeviceMode deviceMode;
        public readonly ChipOSDeviceTrackingState trackingState;
        public readonly ChipOSDeviceContext context;
        public readonly int batteryLevel;
        public readonly bool isCharging;
        public readonly bool comfortWarning;
        public readonly string poseSummary;
        public readonly string timestampUtc;

        public ChipOSDeviceSnapshotChangedEvent(ChipOSDeviceSnapshot snapshot)
        {
            deviceName = snapshot != null ? snapshot.deviceName : "Unknown Device";
            deviceMode = snapshot != null ? snapshot.deviceMode : ChipOSDeviceMode.EditorSimulation;
            trackingState = snapshot != null ? snapshot.trackingState : ChipOSDeviceTrackingState.Unavailable;
            context = snapshot != null ? snapshot.userContext : ChipOSDeviceContext.Unknown;
            batteryLevel = snapshot != null ? snapshot.batteryLevel : 0;
            isCharging = snapshot != null && snapshot.isCharging;
            comfortWarning = snapshot != null && snapshot.comfortWarning;
            poseSummary = snapshot != null ? snapshot.GetPoseSummary() : "Pose unavailable";
            timestampUtc = DateTime.UtcNow.ToString("O");
        }

        public override string ToString()
        {
            return $"{deviceMode} // {trackingState} // {context} // {batteryLevel}%";
        }
    }

    public readonly struct ChipOSDeviceTrackingChangedEvent
    {
        public readonly ChipOSDeviceTrackingState previousState;
        public readonly ChipOSDeviceTrackingState currentState;
        public readonly string timestampUtc;

        public ChipOSDeviceTrackingChangedEvent(ChipOSDeviceTrackingState previousState, ChipOSDeviceTrackingState currentState)
        {
            this.previousState = previousState;
            this.currentState = currentState;
            timestampUtc = DateTime.UtcNow.ToString("O");
        }

        public override string ToString()
        {
            return $"Tracking {previousState} -> {currentState}";
        }
    }

    public readonly struct ChipOSDeviceContextChangedEvent
    {
        public readonly ChipOSDeviceContext previousContext;
        public readonly ChipOSDeviceContext currentContext;
        public readonly float movementSpeedMetersPerSecond;
        public readonly string timestampUtc;

        public ChipOSDeviceContextChangedEvent(ChipOSDeviceContext previousContext, ChipOSDeviceContext currentContext, float movementSpeedMetersPerSecond)
        {
            this.previousContext = previousContext;
            this.currentContext = currentContext;
            this.movementSpeedMetersPerSecond = movementSpeedMetersPerSecond;
            timestampUtc = DateTime.UtcNow.ToString("O");
        }

        public override string ToString()
        {
            return $"Context {previousContext} -> {currentContext} // {movementSpeedMetersPerSecond:0.00} m/s";
        }
    }

    public readonly struct ChipOSDeviceBatteryChangedEvent
    {
        public readonly int previousBatteryLevel;
        public readonly int currentBatteryLevel;
        public readonly bool isCharging;
        public readonly string timestampUtc;

        public ChipOSDeviceBatteryChangedEvent(int previousBatteryLevel, int currentBatteryLevel, bool isCharging)
        {
            this.previousBatteryLevel = previousBatteryLevel;
            this.currentBatteryLevel = currentBatteryLevel;
            this.isCharging = isCharging;
            timestampUtc = DateTime.UtcNow.ToString("O");
        }

        public override string ToString()
        {
            return $"Battery {previousBatteryLevel}% -> {currentBatteryLevel}% // Charging {isCharging}";
        }
    }

    public readonly struct ChipOSDeviceComfortWarningChangedEvent
    {
        public readonly bool comfortWarningEnabled;
        public readonly string timestampUtc;

        public ChipOSDeviceComfortWarningChangedEvent(bool comfortWarningEnabled)
        {
            this.comfortWarningEnabled = comfortWarningEnabled;
            timestampUtc = DateTime.UtcNow.ToString("O");
        }

        public override string ToString()
        {
            return comfortWarningEnabled ? "Comfort warning enabled" : "Comfort warning cleared";
        }
    }

    public readonly struct ChipOSAudioFrameProducedEvent
    {
        public readonly string sourceId;
        public readonly long sequenceNumber;
        public readonly int sampleRateHz;
        public readonly int channels;
        public readonly float rmsLevel;
        public readonly bool isSpeechLike;
        public readonly string timestampUtc;

        public ChipOSAudioFrameProducedEvent(ChipOSAudioFrame frame)
        {
            sourceId = frame != null ? frame.SourceId : "UnknownAudioSource";
            sequenceNumber = frame != null ? frame.SequenceNumber : 0;
            sampleRateHz = frame != null ? frame.SampleRateHz : ChipOSAudioFrame.DefaultSampleRateHz;
            channels = frame != null ? frame.Channels : ChipOSAudioFrame.DefaultChannels;
            rmsLevel = frame != null ? frame.RmsLevel : 0f;
            isSpeechLike = frame != null && frame.IsSpeechLike;
            timestampUtc = DateTime.UtcNow.ToString("O");
        }

        public override string ToString()
        {
            return $"{sourceId} #{sequenceNumber} // {sampleRateHz}Hz {channels}ch // RMS {rmsLevel:0.000} // Speech {isSpeechLike}";
        }
    }

    public readonly struct ChipOSCaptionProducedEvent
    {
        public readonly string text;
        public readonly float confidence;
        public readonly bool isFinal;
        public readonly bool isPartial;
        public readonly long sequenceNumber;
        public readonly string sourceId;
        public readonly string timestampUtc;

        public ChipOSCaptionProducedEvent(ChipOSCaptionEvent caption)
        {
            text = caption != null ? caption.text : "Caption unavailable";
            confidence = caption != null ? caption.confidence : 0f;
            isFinal = caption == null || caption.IsFinal;
            isPartial = caption != null && caption.IsPartial;
            sequenceNumber = caption != null ? caption.sequenceNumber : 0;
            sourceId = caption != null ? caption.sourceId : "UnknownAudioSource";
            timestampUtc = DateTime.UtcNow.ToString("O");
        }

        public override string ToString()
        {
            return $"Caption #{sequenceNumber} {(isFinal ? "final" : "partial")} // {confidence:0.00} // {text}";
        }
    }

    public readonly struct ChipOSAudioSourceStateChangedEvent
    {
        public readonly string sourceName;
        public readonly ChipOSAudioSourceState state;
        public readonly string timestampUtc;

        public ChipOSAudioSourceStateChangedEvent(string sourceName, ChipOSAudioSourceState state)
        {
            this.sourceName = string.IsNullOrWhiteSpace(sourceName) ? "Unknown Audio Source" : sourceName.Trim();
            this.state = state;
            timestampUtc = DateTime.UtcNow.ToString("O");
        }

        public override string ToString()
        {
            return $"{sourceName} -> {state}";
        }
    }
}
