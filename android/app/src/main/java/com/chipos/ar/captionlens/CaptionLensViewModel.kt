package com.chipos.ar.captionlens

import android.app.Application
import android.content.Context
import android.hardware.display.DisplayManager
import android.os.Handler
import android.os.Looper
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.chipos.ar.caption.CaptionSource
import com.chipos.ar.caption.SampleCaptionSource
import com.chipos.ar.xreal.probeDisplays
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

class CaptionLensViewModel(
    application: Application,
) : AndroidViewModel(application) {

    private val captionSource: CaptionSource = SampleCaptionSource()
    private val displayManager =
        getApplication<Application>().getSystemService(Context.DISPLAY_SERVICE) as DisplayManager
    private val displayListener = object : DisplayManager.DisplayListener {
        override fun onDisplayAdded(displayId: Int) = refreshDisplays()
        override fun onDisplayRemoved(displayId: Int) = refreshDisplays()
        override fun onDisplayChanged(displayId: Int) = refreshDisplays()
    }

    private val _uiState = MutableStateFlow(
        CaptionUiState(
            recordsAudio = captionSource.recordsAudio,
            sourceName = captionSource.displayName,
        ),
    )
    val uiState: StateFlow<CaptionUiState> = _uiState.asStateFlow()

    init {
        displayManager.registerDisplayListener(
            displayListener,
            Handler(Looper.getMainLooper()),
        )
        viewModelScope.launch {
            captionSource.captions().collect { line ->
                _uiState.update { it.copy(currentLine = line) }
            }
        }
        refreshDisplays()
    }

    override fun onCleared() {
        displayManager.unregisterDisplayListener(displayListener)
        super.onCleared()
    }

    fun startListening() {
        viewModelScope.launch {
            captionSource.start()
            _uiState.update { it.copy(listening = ListeningState.Listening) }
        }
    }

    fun stopListening() {
        viewModelScope.launch {
            captionSource.stop()
            _uiState.update { it.copy(listening = ListeningState.Stopped) }
        }
    }

    fun smallerText() {
        _uiState.update { it.copy(textScale = it.textScale.smaller()) }
    }

    fun largerText() {
        _uiState.update { it.copy(textScale = it.textScale.larger()) }
    }

    fun cycleContrast() {
        _uiState.update { it.copy(contrast = it.contrast.next()) }
    }

    fun cyclePosition() {
        _uiState.update { it.copy(panelPosition = it.panelPosition.next()) }
    }

    fun refreshDisplays() {
        val snapshot = probeDisplays(getApplication())
        _uiState.update {
            it.copy(
                displayCount = snapshot.count,
                displaySummary = snapshot.summary,
            )
        }
    }
}
