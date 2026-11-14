package com.chipos.ar.captionlens

import android.Manifest
import android.app.Application
import android.content.Context
import android.content.pm.PackageManager
import android.hardware.display.DisplayManager
import android.media.AudioDeviceInfo
import android.media.AudioDeviceCallback
import android.media.AudioManager
import android.os.Handler
import android.os.Looper
import androidx.core.content.ContextCompat
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.chipos.ar.caption.CaptionLine
import com.chipos.ar.caption.AudioInputOption
import com.chipos.ar.caption.CaptionMode
import com.chipos.ar.caption.CaptionSource
import com.chipos.ar.caption.LocalSpeechUnavailableException
import com.chipos.ar.caption.OnDeviceSpeech
import com.chipos.ar.caption.SpeechCaptionSource
import com.chipos.ar.caption.SherpaCaptionSource
import com.chipos.ar.diarization.SpeakerDirectory
import com.chipos.ar.teach.MockTeachingEngine
import com.chipos.ar.transcript.TranscriptStore
import com.chipos.ar.voice.VoiceCommand
import com.chipos.ar.voice.VoiceCommandParser
import com.chipos.ar.xreal.probeDisplays
import kotlinx.coroutines.Job
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
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

    private val displayManager =
        application.getSystemService(Context.DISPLAY_SERVICE) as DisplayManager
    private val audioManager = application.getSystemService(Context.AUDIO_SERVICE) as AudioManager
    private val displayListener = object : DisplayManager.DisplayListener {
        override fun onDisplayAdded(displayId: Int) = refreshDisplays()
        override fun onDisplayRemoved(displayId: Int) = refreshDisplays()
        override fun onDisplayChanged(displayId: Int) = refreshDisplays()
    }
    private val audioDeviceCallback = object : AudioDeviceCallback() {
        override fun onAudioDevicesAdded(addedDevices: Array<out AudioDeviceInfo>) = refreshAudioInputs()
        override fun onAudioDevicesRemoved(removedDevices: Array<out AudioDeviceInfo>) = refreshAudioInputs()
    }

    private var captionSource: CaptionSource = SherpaCaptionSource(application)
    private var collectJob: Job? = null
    private var startInProgress = false
    private var startJob: Job? = null
    private val sessionLines = mutableListOf<CaptionLine>()

    private val _uiState = MutableStateFlow(
        CaptionUiState(
            captionMode = CaptionMode.LiveSherpa,
            recordsAudio = false,
            sourceName = CaptionMode.LiveSherpa.label,
            onDeviceSpeechAvailable = OnDeviceSpeech.isAvailable(application),
            engineStatus = "Engine: offline sherpa-onnx ASR + acoustic diarization",
            diarizationEnabled = prefs.getBoolean(KEY_DIARIZATION, true),
            micGain = prefs.getFloat(KEY_MIC_GAIN, DEFAULT_MIC_GAIN),
            selectedAudioInputId = prefs.getInt(KEY_AUDIO_INPUT_ID, DEFAULT_AUDIO_INPUT_ID).takeIf { it >= 0 },
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
        audioManager.registerAudioDeviceCallback(audioDeviceCallback, Handler(Looper.getMainLooper()))
        bindSource()
        refreshDisplays()
        refreshAudioInputs()
    }

    override fun onCleared() {
        displayManager.unregisterDisplayListener(displayListener)
        audioManager.unregisterAudioDeviceCallback(audioDeviceCallback)
        val source = captionSource
        CoroutineScope(SupervisorJob() + Dispatchers.IO).launch { runCatching { source.stop() } }
        super.onCleared()
    }

    fun startListening() {
        if (_uiState.value.listening == ListeningState.Listening || startInProgress) return
        val mode = _uiState.value.captionMode
        if (mode == CaptionMode.LiveOnDevice && !OnDeviceSpeech.isAvailable(getApplication())) {
            _uiState.update {
                it.copy(
                    speechError = "Android's speech engine is unavailable here. Select the bundled offline Sherpa engine; cloud speech is not used.",
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
        startInProgress = true
        startJob = viewModelScope.launch {
            (captionSource as? SherpaCaptionSource)?.setSpeakerAnalysisEnabled(
                _uiState.value.diarizationEnabled,
            )
            (captionSource as? SherpaCaptionSource)?.setInputGain(_uiState.value.micGain)
            (captionSource as? SherpaCaptionSource)?.setPreferredInputDevice(_uiState.value.selectedAudioInputId)
            try {
                captionSource.start()
            } catch (error: kotlinx.coroutines.CancellationException) {
                startInProgress = false
                throw error
            } catch (error: LocalSpeechUnavailableException) {
                _uiState.update {
                    it.copy(
                        listening = ListeningState.Idle,
                        recordsAudio = false,
                        speechError = error.message,
                        engineStatus = engineStatusLabel(false),
                    )
                }
                startInProgress = false
                return@launch
            } catch (error: Exception) {
                runCatching { captionSource.stop() }
                _uiState.update {
                    it.copy(
                        listening = ListeningState.Idle,
                        recordsAudio = false,
                        speechError = "Could not start captions: ${error.message ?: error.javaClass.simpleName}",
                    )
                }
                startInProgress = false
                return@launch
            }
            startInProgress = false
            _uiState.update {
                it.copy(
                    listening = ListeningState.Listening,
                    recordsAudio = captionSource.recordsAudio,
                    audioInputName = (captionSource as? SherpaCaptionSource)?.inputDeviceName,
                    speechError = null,
                    micPermissionDenied = false,
                    lastVoiceCommand = null,
                    engineStatus = if (captionSource is SherpaCaptionSource) {
                        "Engine: offline sherpa-onnx ASR + acoustic diarization"
                    } else if (captionSource.recordsAudio) {
                        "Engine: Android on-device SpeechRecognizer"
                    } else {
                        "Engine unavailable — select a live speech engine"
                    },
                )
            }
        }
    }

    fun stopListening(): Job {
        startJob?.cancel()
        return viewModelScope.launch {
            captionSource.stop()
            _uiState.update {
                it.copy(
                    listening = ListeningState.Stopped,
                    recordsAudio = false,
                    engineStatus = if (it.captionMode == CaptionMode.LiveSherpa) {
                        "Engine: offline sherpa-onnx ASR + acoustic diarization (stopped)"
                    } else if (it.captionMode == CaptionMode.LiveOnDevice) {
                        engineStatusLabel(OnDeviceSpeech.isAvailable(getApplication()))
                    } else {
                        "Engine unavailable — select a live speech engine"
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
                    "Microphone permission denied. Allow microphone access to use live captions. Audio stays on this device."
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
                        "Android's speech engine is not available on this phone. Bundled offline Sherpa remains available; cloud speech is not used."
                    } else {
                        null
                    },
                    currentLine = null,
                    recentCaptions = emptyList(),
                    audioInputName = null,
                    engineStatus = if (mode == CaptionMode.LiveSherpa) {
                        "Engine: offline sherpa-onnx ASR + acoustic diarization"
                    } else if (mode.recordsAudio) {
                        engineStatusLabel(available)
                    } else {
                        "Engine unavailable — select a live speech engine"
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
        (captionSource as? SherpaCaptionSource)?.setSpeakerAnalysisEnabled(enabled)
        prefs.edit().putBoolean(KEY_DIARIZATION, enabled).apply()
        _uiState.update {
            it.copy(
                diarizationEnabled = enabled,
                speakerUncertain = enabled && it.captionMode.recordsAudio,
                speakerNote = speakerNote(enabled, it.captionMode),
                currentLine = if (!enabled && it.captionMode == CaptionMode.LiveSherpa) {
                    it.currentLine?.copy(
                        speakerLabel = null,
                        speakerId = null,
                        speakerConfidence = null,
                        speakerUncertain = false,
                    )
                } else {
                    it.currentLine
                },
            )
        }
    }

    fun cycleMicGain() {
        val levels = floatArrayOf(1.0f, 1.5f, 2.0f, 3.0f)
        val current = _uiState.value.micGain
        val next = levels.firstOrNull { it > current + 0.01f } ?: levels.first()
        (captionSource as? SherpaCaptionSource)?.setInputGain(next)
        prefs.edit().putFloat(KEY_MIC_GAIN, next).apply()
        _uiState.update { it.copy(micGain = next) }
    }

    fun refreshAudioInputs() {
        val inputs = audioManager.getDevices(AudioManager.GET_DEVICES_INPUTS)
            .filter { isUsableInputType(it.type) }
            .map { device ->
            val label = device.productName.toString().takeIf { it.isNotBlank() } ?: "Audio input ${device.id}"
            AudioInputOption(device.id, label, audioInputKind(device.type))
        }
        val selectedId = _uiState.value.selectedAudioInputId
        if (selectedId != null && inputs.none { it.id == selectedId }) {
            prefs.edit().putInt(KEY_AUDIO_INPUT_ID, DEFAULT_AUDIO_INPUT_ID).apply()
            (captionSource as? SherpaCaptionSource)?.setPreferredInputDevice(null)
            _uiState.update { it.copy(audioInputs = inputs, selectedAudioInputId = null) }
        } else {
            _uiState.update { it.copy(audioInputs = inputs) }
        }
    }

    fun openInputPicker() {
        refreshAudioInputs()
        _uiState.update { it.copy(inputPickerOpen = true) }
    }

    fun dismissInputPicker() {
        _uiState.update { it.copy(inputPickerOpen = false) }
    }

    fun selectAudioInput(deviceId: Int?) {
        prefs.edit().putInt(KEY_AUDIO_INPUT_ID, deviceId ?: DEFAULT_AUDIO_INPUT_ID).apply()
        (captionSource as? SherpaCaptionSource)?.setPreferredInputDevice(deviceId)
        _uiState.update {
            val chosen = deviceId?.let { id -> it.audioInputs.firstOrNull { input -> input.id == id } }
            it.copy(
                selectedAudioInputId = deviceId,
                audioInputName = chosen?.name ?: if (it.listening == ListeningState.Listening) it.audioInputName else "System default",
                inputPickerOpen = false,
                speechError = null,
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
        if (_uiState.value.captionMode == CaptionMode.LiveSherpa) return
        if (_uiState.value.currentLine?.speakerId == null) return
        _uiState.update { it.copy(nameSpeakerDialog = true) }
    }

    fun dismissNameSpeakerDialog() {
        _uiState.update { it.copy(nameSpeakerDialog = false) }
    }

    fun nameCurrentSpeaker(name: String) {
        if (_uiState.value.captionMode == CaptionMode.LiveSherpa) return
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
                engineStatus = if (it.listening == ListeningState.Listening && it.captionMode == CaptionMode.LiveSherpa) {
                    "Engine: offline sherpa-onnx ASR + acoustic diarization"
                } else if (it.listening == ListeningState.Listening && it.recordsAudio) {
                    "Engine: on-device SpeechRecognizer (local-first)"
                } else if (it.captionMode == CaptionMode.LiveSherpa) {
                    "Engine: offline sherpa-onnx ASR + acoustic diarization (ready; microphone off)"
                } else if (it.captionMode == CaptionMode.LiveOnDevice) {
                    engineStatusLabel(available)
                } else {
                    "Engine unavailable — select a live speech engine"
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
        if (raw.sourceId == SherpaCaptionSource.DIARIZATION_STATUS_SOURCE_ID) {
            _uiState.update {
                it.copy(speechError = raw.text, engineStatus = "Offline captions active · speaker analysis unavailable")
            }
            return
        }
        if (raw.sourceId == SherpaCaptionSource.AUDIO_WARNING_SOURCE_ID) {
            _uiState.update { it.copy(speechError = raw.text) }
            return
        }
        if (raw.sourceId == SherpaCaptionSource.INPUT_ROUTE_SOURCE_ID) {
            _uiState.update { it.copy(audioInputName = raw.text) }
            return
        }
        if (raw.sourceId == SherpaCaptionSource.MIC_LEVEL_SOURCE_ID) {
            _uiState.update { it.copy(micLevel = raw.audioLevel ?: 0f) }
            return
        }
        if (raw.sourceId == SherpaCaptionSource.SPEAKER_UPDATE_SOURCE_ID) {
            if (!_uiState.value.diarizationEnabled) return
            _uiState.update { state ->
                val current = state.currentLine?.let { line ->
                    if (line.atMillis == raw.atMillis && line.text == raw.text) raw else line
                }
                val session = state.sessionLines.map { line ->
                    if (line.atMillis == raw.atMillis && line.text == raw.text) raw else line
                }
                val recent = state.recentCaptions.map { line ->
                    if (line.atMillis == raw.atMillis && line.text == raw.text) raw else line
                }
                sessionLines.clear()
                sessionLines += session
                state.copy(
                    currentLine = current,
                    sessionLines = session,
                    recentCaptions = recent,
                    speakerUncertain = raw.speakerUncertain,
                )
            }
            return
        }
        if (!raw.isPartial && VoiceCommandParser.isCommand(raw.text)) {
            VoiceCommandParser.parse(raw.text)?.let { applyVoiceCommand(it) }
            return
        }
        if (raw.sourceId.endsWith("-status")) {
            _uiState.update {
                it.copy(
                    listening = ListeningState.Stopped,
                    recordsAudio = false,
                    speechError = raw.text,
                    currentLine = raw.copy(speakerLabel = null),
                    engineStatus = "Engine stopped after an audio or model error",
                )
            }
            viewModelScope.launch { captionSource.stop() }
            return
        }
        val labeled = label(raw)
        if (!labeled.isPartial && _uiState.value.transcriptSessionOn) {
            sessionLines += labeled
        }
        val liveGuess = labeled.speakerUncertain && _uiState.value.diarizationEnabled
        _uiState.update {
            val recent = if (labeled.isPartial) {
                it.recentCaptions
            } else {
                (it.recentCaptions.filterNot { old ->
                    old.atMillis == labeled.atMillis && old.text == labeled.text
                } + labeled).takeLast(MAX_RECENT_CAPTIONS)
            }
            it.copy(
                currentLine = labeled,
                recentCaptions = recent,
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
            if (_uiState.value.captionMode == CaptionMode.LiveSherpa) return line
            return line.copy(speakerLabel = directory.labelFor(line.speakerId))
        }
        if (_uiState.value.captionMode == CaptionMode.LiveSherpa) {
            return line.copy(speakerLabel = null, speakerId = null, speakerUncertain = true)
        }
        if (!_uiState.value.captionMode.recordsAudio) {
            return line.copy(speakerLabel = null, speakerId = null, speakerUncertain = true)
        }
        return line.copy(
            speakerId = null,
            speakerLabel = null,
            speakerConfidence = null,
            speakerUncertain = true,
        )
    }

    private fun speakerNote(enabled: Boolean, mode: CaptionMode): String? {
        if (!enabled) return "Speaker labels off"
        return when (mode) {
            CaptionMode.LiveSherpa ->
                "Offline acoustic diarization. Anonymous labels are session-only and may be uncertain."
            CaptionMode.LiveOnDevice ->
                "Speaker unknown. Real speaker diarization is not available yet."
        }
    }

    private fun createSource(mode: CaptionMode): CaptionSource = when (mode) {
        CaptionMode.LiveOnDevice -> SpeechCaptionSource(getApplication())
        CaptionMode.LiveSherpa -> SherpaCaptionSource(getApplication())
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
        const val KEY_MIC_GAIN = "mic_input_gain"
        const val KEY_AUDIO_INPUT_ID = "audio_input_device_id"
        const val DEFAULT_AUDIO_INPUT_ID = -1
        const val DEFAULT_MIC_GAIN = 2.0f
        const val MAX_RECENT_CAPTIONS = 3

        fun engineStatusLabel(available: Boolean): String {
            return if (available) {
                "Engine: on-device SpeechRecognizer ready (not started)"
            } else {
                "Engine: UNAVAILABLE — no local speech on this phone. Cloud is not used."
            }
        }

        fun audioInputKind(type: Int): String = when (type) {
            AudioDeviceInfo.TYPE_BUILTIN_MIC -> "Phone microphone"
            AudioDeviceInfo.TYPE_BLUETOOTH_SCO -> "Bluetooth microphone"
            AudioDeviceInfo.TYPE_BLE_HEADSET -> "Bluetooth LE microphone"
            AudioDeviceInfo.TYPE_WIRED_HEADSET -> "Wired headset microphone"
            AudioDeviceInfo.TYPE_USB_DEVICE, AudioDeviceInfo.TYPE_USB_HEADSET -> "USB microphone"
            AudioDeviceInfo.TYPE_TELEPHONY -> "Telephony input"
            else -> "External microphone"
        }

        fun isUsableInputType(type: Int): Boolean = type != AudioDeviceInfo.TYPE_TELEPHONY &&
            type != AudioDeviceInfo.TYPE_FM_TUNER && type != AudioDeviceInfo.TYPE_REMOTE_SUBMIX
    }
}
