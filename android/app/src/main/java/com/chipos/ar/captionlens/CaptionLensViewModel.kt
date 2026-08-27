package com.chipos.ar.captionlens

import android.Manifest
import android.app.Application
import android.content.Context
import android.content.pm.PackageManager
import android.hardware.display.DisplayManager
import android.os.Handler
import android.os.Looper
import androidx.core.content.ContextCompat
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.chipos.ar.caption.CaptionLine
import com.chipos.ar.caption.CaptionMode
import com.chipos.ar.caption.CaptionSource
import com.chipos.ar.caption.LocalSpeechUnavailableException
import com.chipos.ar.caption.OnDeviceSpeech
import com.chipos.ar.caption.SampleCaptionSource
import com.chipos.ar.caption.ScriptedConversationSource
import com.chipos.ar.caption.SpeechCaptionSource
import com.chipos.ar.diarization.SpeakerDirectory
import com.chipos.ar.diarization.TurnDiarizer
import com.chipos.ar.teach.MockTeachingEngine
import com.chipos.ar.transcript.TranscriptStore
import com.chipos.ar.voice.VoiceCommand
import com.chipos.ar.voice.VoiceCommandParser
import com.chipos.ar.xreal.probeDisplays
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

class CaptionLensViewModel(
    application: Application,
) : AndroidViewModel(application) {

    private val prefs = application.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
    private val directory = SpeakerDirectory(application)
    private val transcripts = TranscriptStore(application)
    private val teaching = MockTeachingEngine()
    private val turnDiarizer = TurnDiarizer()

    private val displayManager =
        application.getSystemService(Context.DISPLAY_SERVICE) as DisplayManager
    private val displayListener = object : DisplayManager.DisplayListener {
        override fun onDisplayAdded(displayId: Int) = refreshDisplays()
        override fun onDisplayRemoved(displayId: Int) = refreshDisplays()
        override fun onDisplayChanged(displayId: Int) = refreshDisplays()
    }

    private var captionSource: CaptionSource = SampleCaptionSource()
    private var collectJob: Job? = null
    private val sessionLines = mutableListOf<CaptionLine>()

    private val _uiState = MutableStateFlow(
        CaptionUiState(
            captionMode = CaptionMode.TestSample,
            recordsAudio = false,
            sourceName = CaptionMode.TestSample.label,
            onDeviceSpeechAvailable = OnDeviceSpeech.isAvailable(application),
            engineStatus = engineStatusLabel(OnDeviceSpeech.isAvailable(application)),
            diarizationEnabled = prefs.getBoolean(KEY_DIARIZATION, true),
            transcriptSessionOn = false,
            savedTranscriptExists = transcripts.hasSavedTranscript(),
            savedTranscriptPreview = transcripts.savedPreview(),
        ),
    )
    val uiState: StateFlow<CaptionUiState> = _uiState.asStateFlow()

    init {
        displayManager.registerDisplayListener(
            displayListener,
            Handler(Looper.getMainLooper()),
        )
        bindSource()
        refreshDisplays()
    }

    override fun onCleared() {
        displayManager.unregisterDisplayListener(displayListener)
        viewModelScope.launch { captionSource.stop() }
        super.onCleared()
    }

    fun startListening() {
        if (_uiState.value.listening == ListeningState.Listening) return
        val mode = _uiState.value.captionMode
        if (mode == CaptionMode.LiveOnDevice && !OnDeviceSpeech.isAvailable(getApplication())) {
            _uiState.update {
                it.copy(
                    speechError = "On-device speech engine is unavailable on this phone. Cloud speech is not used. Stay on Demo — sample captions.",
                    engineStatus = engineStatusLabel(false),
                    recordsAudio = false,
                )
            }
            return
        }
        if (mode.recordsAudio && !hasMicPermission()) {
            _uiState.update { it.copy(needsMicPermission = true, speechError = null) }
            return
        }
        viewModelScope.launch {
            turnDiarizer.reset()
            try {
                captionSource.start()
            } catch (error: LocalSpeechUnavailableException) {
                _uiState.update {
                    it.copy(
                        listening = ListeningState.Idle,
                        recordsAudio = false,
                        speechError = error.message,
                        engineStatus = engineStatusLabel(false),
                    )
                }
                return@launch
            } catch (error: Exception) {
                _uiState.update {
                    it.copy(
                        listening = ListeningState.Idle,
                        recordsAudio = false,
                        speechError = "Could not start captions: ${error.message ?: error.javaClass.simpleName}",
                    )
                }
                return@launch
            }
            _uiState.update {
                it.copy(
                    listening = ListeningState.Listening,
                    recordsAudio = captionSource.recordsAudio,
                    speechError = null,
                    micPermissionDenied = false,
                    lastVoiceCommand = null,
                    engineStatus = if (captionSource.recordsAudio) {
                        "Engine: on-device SpeechRecognizer (local-first)"
                    } else {
                        "Engine: offline demo — microphone off"
                    },
                )
            }
        }
    }

    fun stopListening() {
        viewModelScope.launch {
            captionSource.stop()
            _uiState.update {
                it.copy(
                    listening = ListeningState.Stopped,
                    recordsAudio = false,
                    engineStatus = if (it.captionMode.recordsAudio) {
                        engineStatusLabel(OnDeviceSpeech.isAvailable(getApplication()))
                    } else {
                        "Engine: offline demo — microphone off"
                    },
                )
            }
        }
    }

    fun onMicPermissionResult(granted: Boolean) {
        _uiState.update {
            it.copy(
                needsMicPermission = false,
                micPermissionDenied = !granted,
                speechError = if (granted) {
                    null
                } else {
                    "Microphone permission denied. Demo modes still work without it. Cloud speech is not used."
                },
            )
        }
        if (granted) startListening()
    }

    fun openEnginePicker() {
        _uiState.update { it.copy(enginePickerOpen = true) }
    }

    fun dismissEnginePicker() {
        _uiState.update { it.copy(enginePickerOpen = false) }
    }

    fun chooseEngine(mode: CaptionMode) {
        setMode(mode)
        _uiState.update { it.copy(enginePickerOpen = false) }
    }

    fun setMode(mode: CaptionMode) {
        viewModelScope.launch {
            captionSource.stop()
            captionSource = createSource(mode)
            bindSource()
            val available = OnDeviceSpeech.isAvailable(getApplication())
            val liveUnavailable = mode == CaptionMode.LiveOnDevice && !available
            _uiState.update {
                it.copy(
                    captionMode = mode,
                    sourceName = mode.label,
                    recordsAudio = false,
                    listening = ListeningState.Idle,
                    speechError = if (liveUnavailable) {
                        "Not yet available: this phone has no on-device speech engine. Cloud speech is not used. Use Demo — sample captions."
                    } else {
                        null
                    },
                    currentLine = null,
                    engineStatus = if (mode.recordsAudio) {
                        engineStatusLabel(available)
                    } else {
                        "Engine: offline demo — microphone off"
                    },
                    enginePickerOpen = false,
                )
            }
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

    fun moveSubtitlesDown() {
        _uiState.update { it.copy(panelPosition = it.panelPosition.moveDown()) }
    }

    fun toggleDiarization() {
        val enabled = !_uiState.value.diarizationEnabled
        prefs.edit().putBoolean(KEY_DIARIZATION, enabled).apply()
        _uiState.update {
            it.copy(
                diarizationEnabled = enabled,
                speakerUncertain = enabled && it.captionMode.recordsAudio,
                speakerNote = speakerNote(enabled, it.captionMode),
            )
        }
    }

    fun startTranscriptSession() {
        _uiState.update { it.copy(transcriptSessionOn = true, showSession = true) }
    }

    fun stopTranscriptSession() {
        if (sessionLines.isNotEmpty()) {
            transcripts.save(sessionLines.toList())
        }
        _uiState.update {
            it.copy(
                transcriptSessionOn = false,
                savedTranscriptExists = transcripts.hasSavedTranscript(),
                savedTranscriptPreview = transcripts.savedPreview(),
            )
        }
    }

    fun toggleShowSession() {
        _uiState.update { it.copy(showSession = !it.showSession) }
    }

    fun deleteCurrentSession() {
        sessionLines.clear()
        _uiState.update {
            it.copy(
                sessionLines = emptyList(),
                currentLine = it.currentLine,
            )
        }
    }

    fun deleteSavedTranscript() {
        transcripts.deleteSaved()
        _uiState.update {
            it.copy(
                savedTranscriptExists = false,
                savedTranscriptPreview = "",
            )
        }
    }

    fun toggleMore() {
        _uiState.update { it.copy(moreOpen = !it.moreOpen) }
    }

    fun teachMeThis() {
        val text = _uiState.value.currentLine?.text.orEmpty()
        val card = teaching.explain(text, cloudOptIn = false)
        _uiState.update { it.copy(teachingCard = card, lastVoiceCommand = VoiceCommand.TeachMeThis.label) }
    }

    fun hideHelp() {
        _uiState.update { it.copy(teachingCard = null, lastVoiceCommand = VoiceCommand.HideHelp.label) }
    }

    fun openNameSpeakerDialog() {
        if (_uiState.value.currentLine?.speakerId == null) return
        _uiState.update { it.copy(nameSpeakerDialog = true) }
    }

    fun dismissNameSpeakerDialog() {
        _uiState.update { it.copy(nameSpeakerDialog = false) }
    }

    fun nameCurrentSpeaker(name: String) {
        val id = _uiState.value.currentLine?.speakerId ?: return
        directory.nameSpeaker(id, name)
        _uiState.update { state ->
            val line = state.currentLine?.copy(speakerLabel = directory.labelFor(id))
            val session = state.sessionLines.map { item ->
                if (item.speakerId == id) item.copy(speakerLabel = directory.labelFor(id)) else item
            }
            sessionLines.clear()
            sessionLines += session
            state.copy(currentLine = line, sessionLines = session, nameSpeakerDialog = false)
        }
    }

    fun simulateNextVoiceCommand() {
        val command = _uiState.value.nextVoiceCommand
        applyVoiceCommand(command)
        _uiState.update {
            it.copy(voiceCommandIndex = (it.voiceCommandIndex + 1) % VoiceCommand.entries.size)
        }
    }

    fun refreshDisplays() {
        val snapshot = probeDisplays(getApplication())
        val available = OnDeviceSpeech.isAvailable(getApplication())
        _uiState.update {
            it.copy(
                displayCount = snapshot.count,
                displaySummary = snapshot.summary,
                onDeviceSpeechAvailable = available,
                engineStatus = if (it.listening == ListeningState.Listening && it.recordsAudio) {
                    "Engine: on-device SpeechRecognizer (local-first)"
                } else if (it.captionMode.recordsAudio) {
                    engineStatusLabel(available)
                } else {
                    "Engine: offline demo — microphone off"
                },
            )
        }
    }

    fun applyVoiceCommand(command: VoiceCommand) {
        _uiState.update { it.copy(lastVoiceCommand = command.label) }
        when (command) {
            VoiceCommand.StartCaptions -> startListening()
            VoiceCommand.StopCaptions -> stopListening()
            VoiceCommand.MakeItBigger -> largerText()
            VoiceCommand.MoveSubtitlesDown -> moveSubtitlesDown()
            VoiceCommand.TeachMeThis -> teachMeThis()
            VoiceCommand.HideHelp -> hideHelp()
        }
    }

    private fun bindSource() {
        collectJob?.cancel()
        collectJob = viewModelScope.launch {
            captionSource.captions().collect { raw -> handleIncoming(raw) }
        }
        _uiState.update {
            it.copy(
                sourceName = it.captionMode.label,
                speakerNote = speakerNote(it.diarizationEnabled, it.captionMode),
            )
        }
    }

    private fun handleIncoming(raw: CaptionLine) {
        if (!raw.isPartial && VoiceCommandParser.isCommand(raw.text)) {
            VoiceCommandParser.parse(raw.text)?.let { applyVoiceCommand(it) }
            return
        }
        if (raw.sourceId.endsWith("-status")) {
            _uiState.update { it.copy(speechError = raw.text, currentLine = raw.copy(speakerLabel = null)) }
            return
        }
        val labeled = label(raw)
        if (!labeled.isPartial && _uiState.value.transcriptSessionOn) {
            sessionLines += labeled
        }
        val liveGuess = labeled.speakerUncertain && _uiState.value.diarizationEnabled
        _uiState.update {
            it.copy(
                currentLine = labeled,
                sessionLines = sessionLines.toList(),
                speakerUncertain = liveGuess,
                speakerNote = speakerNote(it.diarizationEnabled, it.captionMode),
                recordsAudio = captionSource.recordsAudio && it.listening == ListeningState.Listening,
            )
        }
    }

    private fun label(line: CaptionLine): CaptionLine {
        val enabled = _uiState.value.diarizationEnabled
        if (!enabled) {
            return line.copy(
                speakerLabel = null,
                speakerId = null,
                speakerUncertain = false,
                speakerConfidence = null,
            )
        }
        if (line.speakerId != null) {
            return line.copy(speakerLabel = directory.labelFor(line.speakerId))
        }
        if (!_uiState.value.captionMode.recordsAudio) {
            return line.copy(speakerLabel = directory.labelFor(1), speakerId = 1)
        }
        val assignment = if (line.isPartial) turnDiarizer.currentPartial() else turnDiarizer.onFinalUtterance()
        return line.copy(
            speakerId = assignment.speakerId,
            speakerLabel = directory.labelFor(assignment.speakerId),
            speakerConfidence = assignment.confidence,
            speakerUncertain = assignment.uncertain,
        )
    }

    private fun speakerNote(enabled: Boolean, mode: CaptionMode): String? {
        if (!enabled) return "Speaker labels off"
        return when (mode) {
            CaptionMode.TestScripted -> "Test labels: Speaker 1–3. Not real identities."
            CaptionMode.TestSample -> "Demo label: Speaker 1. Not a real identity."
            CaptionMode.LiveOnDevice ->
                "Speaker uncertain. Pause-based guess only. Not face recognition."
        }
    }

    private fun createSource(mode: CaptionMode): CaptionSource = when (mode) {
        CaptionMode.TestSample -> SampleCaptionSource()
        CaptionMode.TestScripted -> ScriptedConversationSource()
        CaptionMode.LiveOnDevice -> SpeechCaptionSource(getApplication())
    }

    private fun hasMicPermission(): Boolean {
        return ContextCompat.checkSelfPermission(
            getApplication(),
            Manifest.permission.RECORD_AUDIO,
        ) == PackageManager.PERMISSION_GRANTED
    }

    private companion object {
        const val PREFS = "chipos_caption_prefs"
        const val KEY_DIARIZATION = "diarization"

        fun engineStatusLabel(available: Boolean): String {
            return if (available) {
                "Engine: on-device SpeechRecognizer ready (not started)"
            } else {
                "Engine: UNAVAILABLE — no local speech on this phone. Cloud is not used."
            }
        }
    }
}
