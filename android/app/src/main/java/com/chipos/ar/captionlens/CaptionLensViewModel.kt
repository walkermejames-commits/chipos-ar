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
import com.chipos.ar.caption.OnDeviceSpeech
import com.chipos.ar.caption.SampleCaptionSource
import com.chipos.ar.caption.ScriptedConversationSource
import com.chipos.ar.caption.SpeechCaptionSource
import com.chipos.ar.diarization.SpeakerDirectory
import com.chipos.ar.diarization.TurnDiarizer
import com.chipos.ar.overlay.PanelPosition
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

    private var captionSource: CaptionSource = ScriptedConversationSource()
    private var collectJob: Job? = null
    private val sessionLines = mutableListOf<CaptionLine>()

    private val _uiState = MutableStateFlow(
        CaptionUiState(
            captionMode = CaptionMode.TestScripted,
            recordsAudio = false,
            sourceName = CaptionMode.TestScripted.label,
            onDeviceSpeechAvailable = OnDeviceSpeech.isAvailable(application),
            cloudSpeechOptIn = prefs.getBoolean(KEY_CLOUD_SPEECH, false),
            cloudAiOptIn = prefs.getBoolean(KEY_CLOUD_AI, false),
            diarizationEnabled = prefs.getBoolean(KEY_DIARIZATION, true),
            saveTranscript = prefs.getBoolean(KEY_SAVE_TRANSCRIPT, false),
            savedTranscriptExists = transcripts.hasSavedTranscript(),
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
        val state = _uiState.value
        val mode = state.captionMode
        if (mode.recordsAudio && !hasMicPermission()) {
            _uiState.update { it.copy(needsMicPermission = true, speechError = null) }
            return
        }
        if (mode == CaptionMode.LiveOnDevice && !OnDeviceSpeech.isAvailable(getApplication())) {
            _uiState.update {
                it.copy(
                    speechError = "On-device speech is not available on this phone. Use Test mode, or opt in to cloud speech.",
                )
            }
            return
        }
        if (mode == CaptionMode.LiveCloud && !state.cloudSpeechOptIn) {
            _uiState.update {
                it.copy(speechError = "Cloud speech stays off until you opt in.")
            }
            return
        }
        if (mode == CaptionMode.LiveCloud &&
            !OnDeviceSpeech.isLegacyRecognizerAvailable(getApplication())
        ) {
            _uiState.update {
                it.copy(speechError = "No system speech recognizer is installed on this phone.")
            }
            return
        }
        viewModelScope.launch {
            turnDiarizer.reset()
            try {
                captionSource.start()
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
                )
            }
        }
    }

    fun stopListening() {
        viewModelScope.launch {
            captionSource.stop()
            if (_uiState.value.saveTranscript && sessionLines.isNotEmpty()) {
                transcripts.save(sessionLines.toList())
            }
            _uiState.update {
                it.copy(
                    listening = ListeningState.Stopped,
                    savedTranscriptExists = transcripts.hasSavedTranscript(),
                    recordsAudio = false,
                )
            }
        }
    }

    fun onMicPermissionResult(granted: Boolean) {
        _uiState.update {
            it.copy(
                needsMicPermission = false,
                micPermissionDenied = !granted,
                speechError = if (granted) null else "Microphone permission denied. Test mode still works without it.",
            )
        }
        if (granted) startListening()
    }

    fun cycleMode() {
        val current = _uiState.value.captionMode
        var next = current.next()
        if (next == CaptionMode.LiveCloud && !_uiState.value.cloudSpeechOptIn) {
            next = CaptionMode.TestScripted
        }
        setMode(next)
    }

    fun setMode(mode: CaptionMode) {
        viewModelScope.launch {
            val wasListening = _uiState.value.listening == ListeningState.Listening
            captionSource.stop()
            captionSource = createSource(mode)
            bindSource()
            _uiState.update {
                it.copy(
                    captionMode = mode,
                    sourceName = mode.label,
                    recordsAudio = false,
                    listening = if (wasListening) ListeningState.Stopped else it.listening,
                    speechError = null,
                    currentLine = null,
                )
            }
            if (wasListening) startListening()
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

    fun toggleSaveTranscript() {
        val enabled = !_uiState.value.saveTranscript
        prefs.edit().putBoolean(KEY_SAVE_TRANSCRIPT, enabled).apply()
        _uiState.update { it.copy(saveTranscript = enabled) }
    }

    fun toggleCloudSpeechOptIn() {
        val enabled = !_uiState.value.cloudSpeechOptIn
        prefs.edit().putBoolean(KEY_CLOUD_SPEECH, enabled).apply()
        _uiState.update { it.copy(cloudSpeechOptIn = enabled) }
        if (!enabled && _uiState.value.captionMode == CaptionMode.LiveCloud) {
            setMode(CaptionMode.TestScripted)
        }
    }

    fun toggleCloudAiOptIn() {
        val enabled = !_uiState.value.cloudAiOptIn
        prefs.edit().putBoolean(KEY_CLOUD_AI, enabled).apply()
        _uiState.update { it.copy(cloudAiOptIn = enabled) }
    }

    fun toggleMore() {
        _uiState.update { it.copy(moreOpen = !it.moreOpen) }
    }

    fun teachMeThis() {
        val text = _uiState.value.currentLine?.text.orEmpty()
        val card = teaching.explain(text, _uiState.value.cloudAiOptIn)
        _uiState.update { it.copy(teachingCard = card, lastVoiceCommand = VoiceCommand.TeachMeThis.label) }
    }

    fun hideHelp() {
        _uiState.update { it.copy(teachingCard = null, lastVoiceCommand = VoiceCommand.HideHelp.label) }
    }

    fun deleteCurrentSession() {
        sessionLines.clear()
        _uiState.update {
            it.copy(
                currentLine = null,
                sessionLineCount = 0,
                teachingCard = null,
            )
        }
    }

    fun deleteSavedTranscript() {
        transcripts.deleteSaved()
        _uiState.update { it.copy(savedTranscriptExists = false) }
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
            state.copy(currentLine = line, nameSpeakerDialog = false)
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
        _uiState.update {
            it.copy(
                displayCount = snapshot.count,
                displaySummary = snapshot.summary,
                onDeviceSpeechAvailable = OnDeviceSpeech.isAvailable(getApplication()),
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
                recordsAudio = captionSource.recordsAudio && it.listening == ListeningState.Listening,
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
        if (!labeled.isPartial) {
            sessionLines += labeled
            if (_uiState.value.saveTranscript) {
                transcripts.save(sessionLines.toList())
            }
        }
        val liveGuess = labeled.speakerUncertain && _uiState.value.diarizationEnabled
        _uiState.update {
            it.copy(
                currentLine = labeled,
                sessionLineCount = sessionLines.size,
                speakerUncertain = liveGuess,
                speakerNote = speakerNote(it.diarizationEnabled, it.captionMode),
                recordsAudio = captionSource.recordsAudio && it.listening == ListeningState.Listening,
                savedTranscriptExists = transcripts.hasSavedTranscript(),
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
            CaptionMode.TestSample -> "Test label: Speaker 1. Not a real identity."
            CaptionMode.LiveOnDevice, CaptionMode.LiveCloud ->
                "Speaker guess uncertain. Pause-based only. Not face recognition."
        }
    }

    private fun createSource(mode: CaptionMode): CaptionSource = when (mode) {
        CaptionMode.TestScripted -> ScriptedConversationSource()
        CaptionMode.TestSample -> SampleCaptionSource()
        CaptionMode.LiveOnDevice -> SpeechCaptionSource(getApplication(), onDevice = true)
        CaptionMode.LiveCloud -> SpeechCaptionSource(getApplication(), onDevice = false)
    }

    private fun hasMicPermission(): Boolean {
        return ContextCompat.checkSelfPermission(
            getApplication(),
            Manifest.permission.RECORD_AUDIO,
        ) == PackageManager.PERMISSION_GRANTED
    }

    private companion object {
        const val PREFS = "chipos_caption_prefs"
        const val KEY_CLOUD_SPEECH = "cloud_speech_opt_in"
        const val KEY_CLOUD_AI = "cloud_ai_opt_in"
        const val KEY_DIARIZATION = "diarization"
        const val KEY_SAVE_TRANSCRIPT = "save_transcript"
    }
}
