package com.chipos.ar.captionlens

import com.chipos.ar.caption.CaptionLine
import com.chipos.ar.overlay.ContrastMode
import com.chipos.ar.overlay.PanelPosition
import com.chipos.ar.overlay.TextScale

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
    val recordsAudio: Boolean = false,
    val sourceName: String = "Sample captions",
    val displayCount: Int = 1,
    val displaySummary: String = "not probed",
)
