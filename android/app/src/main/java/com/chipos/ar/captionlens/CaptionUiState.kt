package com.chipos.ar.captionlens

import com.chipos.ar.caption.CaptionLine
import com.chipos.ar.caption.CaptionMode
import com.chipos.ar.overlay.ContrastMode
import com.chipos.ar.overlay.PanelPosition
import com.chipos.ar.overlay.TextScale
import com.chipos.ar.teach.TeachingCard
import com.chipos.ar.voice.VoiceCommand

enum class ListeningState {
    Idle,
    Listening,
    Stopped,
}

data class CaptionUiState(
    val listening: ListeningState = ListeningState.Idle,
    val currentLine: CaptionLine? = null,
    val textScale: TextScale = TextScale.Large,
    val contrast: ContrastMode = ContrastMode.High,
    val panelPosition: PanelPosition = PanelPosition.BottomCenter,
    val captionMode: CaptionMode = CaptionMode.TestScripted,
    val recordsAudio: Boolean = false,
    val sourceName: String = CaptionMode.TestScripted.label,
    val displayCount: Int = 1,
    val displaySummary: String = "not probed",
    val onDeviceSpeechAvailable: Boolean = false,
    val cloudSpeechOptIn: Boolean = false,
    val cloudAiOptIn: Boolean = false,
    val diarizationEnabled: Boolean = true,
    val speakerUncertain: Boolean = false,
    val speakerNote: String? = null,
    val saveTranscript: Boolean = false,
    val sessionLineCount: Int = 0,
    val savedTranscriptExists: Boolean = false,
    val teachingCard: TeachingCard? = null,
    val lastVoiceCommand: String? = null,
    val needsMicPermission: Boolean = false,
    val micPermissionDenied: Boolean = false,
    val speechError: String? = null,
    val moreOpen: Boolean = false,
    val nameSpeakerDialog: Boolean = false,
    val voiceCommandIndex: Int = 0,
) {
    val nextVoiceCommand: VoiceCommand
        get() = VoiceCommand.entries[voiceCommandIndex % VoiceCommand.entries.size]
}
