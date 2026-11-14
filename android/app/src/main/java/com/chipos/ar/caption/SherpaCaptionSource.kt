package com.chipos.ar.caption

import android.content.Context
import android.Manifest
import android.content.pm.PackageManager
import androidx.core.content.ContextCompat
import android.media.AudioFormat
import android.media.AudioRecord
import android.media.MediaRecorder
import android.media.AudioManager
import android.media.audiofx.AutomaticGainControl
import android.os.SystemClock
import android.util.Log
import com.chipos.ar.BuildConfig
import com.chipos.ar.modules.SpeechToTextModule
import com.k2fsa.sherpa.onnx.FastClusteringConfig
import com.k2fsa.sherpa.onnx.FeatureConfig
import com.k2fsa.sherpa.onnx.OfflineSpeakerDiarization
import com.k2fsa.sherpa.onnx.OfflineSpeakerDiarizationConfig
import com.k2fsa.sherpa.onnx.OfflineSpeakerSegmentationModelConfig
import com.k2fsa.sherpa.onnx.OfflineSpeakerSegmentationPyannoteModelConfig
import com.k2fsa.sherpa.onnx.OnlineModelConfig
import com.k2fsa.sherpa.onnx.OnlineRecognizer
import com.k2fsa.sherpa.onnx.OnlineRecognizerConfig
import com.k2fsa.sherpa.onnx.OnlineTransducerModelConfig
import com.k2fsa.sherpa.onnx.SpeakerEmbeddingExtractor
import com.k2fsa.sherpa.onnx.SpeakerEmbeddingExtractorConfig
import com.k2fsa.sherpa.onnx.OnlineStream
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.withContext
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.math.sqrt

/**
 * Fully local microphone pipeline. A single AudioRecord stream feeds both the
 * streaming recognizer and the acoustic speaker diarizer. Speaker vectors and
 * labels exist only for this source/session and are discarded on stop.
 */
class SherpaCaptionSource(context: Context) : CaptionSource, SpeechToTextModule {
    private val lifecycleMutex = Mutex()
    private val appContext = context.applicationContext
    private val lines = MutableSharedFlow<CaptionLine>(extraBufferCapacity = 64)
    private val audioRing = FloatAudioRing(MAX_AUDIO_SECONDS * SAMPLE_RATE)
    private val speakerBank = SessionSpeakerBank()
    private val diarizationBusy = AtomicBoolean(false)
    private val diarizedSegments = mutableListOf<TimedSpeakerSegment>()

    @Volatile private var running = false
    @Volatile private var sampleCount = 0L
    @Volatile private var sessionStartedAtMillis = 0L
    @Volatile private var speakerAnalysisEnabled = true
    @Volatile private var inputGainMultiplier = 1.0f
    @Volatile private var audioWarningSent = false
    @Volatile private var preferredInputDeviceId: Int? = null
    @Volatile var inputDeviceName: String = "System-selected microphone"
        private set
    private var recorder: AudioRecord? = null
    private var automaticGainControl: AutomaticGainControl? = null
    private var captureThread: Thread? = null
    private var diarizationExecutor: ExecutorService? = null
    private var recognizer: OnlineRecognizer? = null
    private var diarizer: OfflineSpeakerDiarization? = null
    private var embeddingExtractor: SpeakerEmbeddingExtractor? = null

    override val id: String = SOURCE_ID
    override val displayName: String = "Offline ASR + acoustic diarization"
    override val recordsAudio: Boolean = true
    override fun captions(): Flow<CaptionLine> = lines.asSharedFlow()

    @Synchronized
    fun setSpeakerAnalysisEnabled(enabled: Boolean) {
        speakerAnalysisEnabled = enabled
        if (!enabled) {
            speakerBank.reset()
            synchronized(diarizedSegments) { diarizedSegments.clear() }
        }
    }

    fun setInputGain(multiplier: Float) {
        inputGainMultiplier = multiplier.coerceIn(1.0f, 3.0f)
    }

    @Synchronized
    fun setPreferredInputDevice(deviceId: Int?) {
        preferredInputDeviceId = deviceId
        val audioRecord = recorder ?: return
        val device = deviceId?.let { id ->
            (appContext.getSystemService(Context.AUDIO_SERVICE) as AudioManager)
                .getDevices(AudioManager.GET_DEVICES_INPUTS).firstOrNull { it.id == id }
        }
        val accepted = audioRecord.setPreferredDevice(device)
        Log.i("ChipMicInput", "preferred input=${device?.productName ?: "System default"} accepted=$accepted")
        if (!accepted) lines.tryEmit(CaptionLine(text = "Android could not select that microphone; using its default route.", sourceId = AUDIO_WARNING_SOURCE_ID))
    }

    override suspend fun start() = lifecycleMutex.withLock {
        try { startInternal() } catch (error: Throwable) {
            withContext(kotlinx.coroutines.NonCancellable) { stopInternal() }
            throw error
        }
    }

    override suspend fun stop() = lifecycleMutex.withLock { stopInternal() }

    private suspend fun startInternal() = withContext(Dispatchers.IO) {
        if (running) return@withContext
        if (ContextCompat.checkSelfPermission(appContext, Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            throw SecurityException("Microphone permission is required before starting live captions.")
        }
        speakerBank.reset()
        synchronized(diarizedSegments) { diarizedSegments.clear() }
        synchronized(lastCaptionLines) { lastCaptionLines.clear() }
        audioRing.clear()
        sampleCount = 0L
        audioWarningSent = false
        diarizationBusy.set(false)
        sessionStartedAtMillis = System.currentTimeMillis()

        val assets = appContext.assets
        val asrDir = "models/asr"
        val diarizationDir = "models/diarization"
        val asr = OnlineRecognizer(
            assetManager = assets,
            config = OnlineRecognizerConfig(
                featConfig = FeatureConfig(sampleRate = SAMPLE_RATE, featureDim = 80),
                modelConfig = OnlineModelConfig(
                    transducer = OnlineTransducerModelConfig(
                        encoder = "$asrDir/encoder-epoch-99-avg-1.int8.onnx",
                        decoder = "$asrDir/decoder-epoch-99-avg-1.onnx",
                        joiner = "$asrDir/joiner-epoch-99-avg-1.int8.onnx",
                    ),
                    tokens = "$asrDir/tokens.txt",
                    numThreads = 2,
                    provider = "cpu",
                    modelType = "zipformer",
                ),
                enableEndpoint = true,
            ),
        )
        recognizer = asr
        var sd: OfflineSpeakerDiarization? = null
        var extractor: SpeakerEmbeddingExtractor? = null
        if (speakerAnalysisEnabled) try {
        sd = OfflineSpeakerDiarization(
            assetManager = assets,
            config = OfflineSpeakerDiarizationConfig(
                segmentation = OfflineSpeakerSegmentationModelConfig(
                    pyannote = OfflineSpeakerSegmentationPyannoteModelConfig(
                        model = "$diarizationDir/segmentation.int8.onnx",
                        windowShiftRatio = 0.1f,
                    ),
                    numThreads = 2,
                    provider = "cpu",
                ),
                embedding = SpeakerEmbeddingExtractorConfig(
                    model = "$diarizationDir/eres2net-en.onnx",
                    numThreads = 2,
                    provider = "cpu",
                ),
                clustering = FastClusteringConfig(
                    numClusters = -1,
                    threshold = 0.5f,
                    computeConfidence = true,
                ),
                minDurationOn = 0.35f,
                minDurationOff = 0.4f,
            ),
        )
        extractor = SpeakerEmbeddingExtractor(
            assetManager = assets,
            config = SpeakerEmbeddingExtractorConfig(
                model = "$diarizationDir/eres2net-en.onnx",
                numThreads = 2,
                provider = "cpu",
            ),
        )
        } catch (error: Throwable) {
            runCatching { extractor?.release() }
            runCatching { sd?.release() }
            extractor = null
            sd = null
            speakerAnalysisEnabled = false
            lines.tryEmit(CaptionLine(
                text = "Speaker analysis unavailable; offline captions will continue. ${error.message ?: error.javaClass.simpleName}",
                sourceId = DIARIZATION_STATUS_SOURCE_ID,
            ))
        }
        diarizer = sd
        embeddingExtractor = extractor

        val minBufferBytes = AudioRecord.getMinBufferSize(
            SAMPLE_RATE,
            AudioFormat.CHANNEL_IN_MONO,
            AudioFormat.ENCODING_PCM_16BIT,
        )
        check(minBufferBytes > 0) { "This phone cannot open a 16 kHz microphone input." }
        currentCoroutineContext().ensureActive()
        val audioRecord = AudioRecord(
            MediaRecorder.AudioSource.MIC,
            SAMPLE_RATE,
            AudioFormat.CHANNEL_IN_MONO,
            AudioFormat.ENCODING_PCM_16BIT,
            maxOf(minBufferBytes * 2, CHUNK_SAMPLES * 4),
        )
        check(audioRecord.state == AudioRecord.STATE_INITIALIZED) {
            audioRecord.release()
            "The microphone could not be initialized."
        }
        val selectedDevice = preferredInputDeviceId?.let { id ->
            (appContext.getSystemService(Context.AUDIO_SERVICE) as AudioManager)
                .getDevices(AudioManager.GET_DEVICES_INPUTS).firstOrNull { it.id == id }
        }
        val preferredAccepted = audioRecord.setPreferredDevice(selectedDevice)
        if (preferredInputDeviceId != null && selectedDevice == null) {
            lines.tryEmit(CaptionLine(text = "Selected microphone is no longer connected. Using Android's default input.", sourceId = AUDIO_WARNING_SOURCE_ID))
        } else if (selectedDevice != null && !preferredAccepted) {
            lines.tryEmit(CaptionLine(text = "Android rejected the selected microphone. Using its default input.", sourceId = AUDIO_WARNING_SOURCE_ID))
        }
        automaticGainControl = if (AutomaticGainControl.isAvailable()) {
            runCatching {
                AutomaticGainControl.create(audioRecord.audioSessionId)?.apply { enabled = true }
            }.getOrNull()
        } else {
            null
        }

        recognizer = asr
        recorder = audioRecord
        diarizationExecutor = Executors.newSingleThreadExecutor { runnable ->
            Thread(runnable, "chipos-diarization").apply { isDaemon = true }
        }
        running = true
        try {
            currentCoroutineContext().ensureActive()
            audioRecord.startRecording()
            check(audioRecord.recordingState == AudioRecord.RECORDSTATE_RECORDING) {
                "The microphone did not enter the recording state."
            }
            inputDeviceName = audioRecord.routedDevice?.productName?.toString()
                ?.takeIf { it.isNotBlank() } ?: "System-selected microphone"
        } catch (error: Throwable) {
            running = false
            automaticGainControl?.release()
            automaticGainControl = null
            audioRecord.release()
            recorder = null
            extractor?.release()
            sd?.release()
            asr.release()
            embeddingExtractor = null
            diarizer = null
            recognizer = null
            throw error
        }
        captureThread = Thread(::captureLoop, "chipos-audio-capture").apply {
            isDaemon = true
            start()
        }
    }

    private suspend fun stopInternal() = withContext(Dispatchers.IO) {
        if (!running && recorder == null && recognizer == null && diarizer == null &&
            embeddingExtractor == null
        ) return@withContext
        running = false
        try {
            recorder?.stop()
        } catch (_: IllegalStateException) {
            // It may already have stopped after a capture error.
        }
        captureThread?.join()
        captureThread = null
        automaticGainControl?.release()
        automaticGainControl = null
        recorder?.release()
        recorder = null
        diarizationExecutor?.shutdown()
        while (diarizationExecutor?.awaitTermination(30, TimeUnit.SECONDS) == false) {
            // Do not release the native diarizer while its worker is inside JNI.
        }
        diarizationExecutor = null
        diarizationBusy.set(false)
        embeddingExtractor?.release()
        embeddingExtractor = null
        diarizer?.release()
        diarizer = null
        recognizer?.release()
        recognizer = null
        speakerBank.reset()
        audioRing.clear()
        synchronized(diarizedSegments) { diarizedSegments.clear() }
    }

    private fun captureLoop() {
        val audioRecord = recorder ?: return
        val onlineRecognizer = recognizer ?: return
        var stream: OnlineStream? = null
        val pcm = ShortArray(CHUNK_SAMPLES)
        var lastDiarizationQueuedAt = 0L
        var lastCaptionText = ""
        var utteranceAtMillis = 0L
        var lastAudioDiagnosticAt = 0L
        var lastMicLevelUpdateAt = 0L
        var silentSince = 0L
        var lastRoute = audioRecord.routedDevice?.productName?.toString()
        try {
            stream = onlineRecognizer.createStream()
            while (running) {
                val count = audioRecord.read(pcm, 0, pcm.size, AudioRecord.READ_BLOCKING)
                if (count < 0) throw IllegalStateException("Microphone read failed ($count).")
                if (count == 0) continue
                val nowElapsed = SystemClock.elapsedRealtime()
                if (nowElapsed - lastMicLevelUpdateAt >= MIC_LEVEL_UPDATE_MS) {
                    var levelSquares = 0.0
                    for (index in 0 until count) {
                        val sample = pcm[index].toDouble() / Short.MAX_VALUE
                        levelSquares += sample * sample
                    }
                    lines.tryEmit(CaptionLine(
                        text = "",
                        sourceId = MIC_LEVEL_SOURCE_ID,
                        audioLevel = sqrt(levelSquares / count).toFloat().coerceIn(0f, 1f),
                    ))
                    lastMicLevelUpdateAt = nowElapsed
                }
                val route = audioRecord.routedDevice?.productName?.toString()
                    ?.takeIf { it.isNotBlank() } ?: "System-selected microphone"
                if (route != inputDeviceName) {
                    inputDeviceName = route
                    lines.tryEmit(CaptionLine(text = route, sourceId = INPUT_ROUTE_SOURCE_ID))
                }
                if (route != lastRoute) {
                    Log.i("ChipMicInput", "input route changed: $lastRoute -> $route")
                    lastRoute = route
                }
                var sumSquares = 0.0
                var peak = 0
                for (index in 0 until count) {
                    val sample = pcm[index].toInt()
                    peak = maxOf(peak, kotlin.math.abs(sample))
                    val normalized = sample.toDouble() / Short.MAX_VALUE
                    sumSquares += normalized * normalized
                }
                val rms = sqrt(sumSquares / count).toFloat()
                val hasSignal = rms >= MIN_SIGNAL_RMS
                if (hasSignal) {
                    silentSince = 0L
                    audioWarningSent = false
                } else {
                    if (silentSince == 0L) silentSince = nowElapsed
                    if (!audioWarningSent && nowElapsed - silentSince >= SILENT_INPUT_WARNING_MS) {
                        audioWarningSent = true
                        lines.tryEmit(CaptionLine(
                            text = "Microphone input is silent. Check the phone or glasses microphone route and Android microphone access.",
                            sourceId = AUDIO_WARNING_SOURCE_ID,
                        ))
                    }
                }
                if (BuildConfig.DEBUG) {
                    val now = android.os.SystemClock.elapsedRealtime()
                    if (now - lastAudioDiagnosticAt >= 2_000L) {
                        Log.i("ChipMicInput", "samples=$count rms=$rms peak=${peak / Short.MAX_VALUE.toFloat()} route=${audioRecord.routedDevice?.productName}")
                        lastAudioDiagnosticAt = now
                    }
                }
            val gain = inputGainMultiplier
            val floats = FloatArray(count) {
                (pcm[it] / 32768.0f * gain).coerceIn(-1.0f, 1.0f)
            }
                audioRing.append(floats)
                val totalSamples = sampleCount + count
                sampleCount = totalSamples
                stream.acceptWaveform(floats, SAMPLE_RATE)
                while (onlineRecognizer.isReady(stream)) onlineRecognizer.decode(stream)

                val endpoint = onlineRecognizer.isEndpoint(stream)
                val text = onlineRecognizer.getResult(stream).text.trim()
                if (text.isNotEmpty() && (text != lastCaptionText || endpoint)) {
                    if (utteranceAtMillis == 0L) {
                        utteranceAtMillis = sessionStartedAtMillis + totalSamples * 1_000L / SAMPLE_RATE
                    }
                    val atMillis = utteranceAtMillis
                    val speaker = speakerFor(atMillis)
                    val caption = CaptionLine(
                            text = text,
                            speakerLabel = speaker?.label,
                            isPartial = !endpoint,
                            sourceId = SOURCE_ID,
                            speakerId = speaker?.id,
                            speakerConfidence = speaker?.confidence,
                            speakerUncertain = speaker == null || speaker.confidence < MIN_CONFIDENT_LABEL,
                            atMillis = atMillis,
                        )
                    synchronized(lastCaptionLines) {
                        if (!caption.isPartial) lastCaptionLines.addLast(caption)
                        while (lastCaptionLines.size > MAX_RECENT_CAPTIONS) lastCaptionLines.removeFirst()
                    }
                    lines.tryEmit(caption)
                    lastCaptionText = text
                }
                if (endpoint) {
                    onlineRecognizer.reset(stream)
                    lastCaptionText = ""
                    utteranceAtMillis = 0L
                }

                if (speakerAnalysisEnabled && totalSamples >= DIARIZATION_WINDOW_SAMPLES &&
                    totalSamples - lastDiarizationQueuedAt >= DIARIZATION_STEP_SAMPLES
                ) {
                    queueDiarization(totalSamples)
                    lastDiarizationQueuedAt = totalSamples
                }
            }
        } catch (error: Throwable) {
            if (running) {
                lines.tryEmit(
                    CaptionLine(
                        text = "Offline caption and speaker analysis stopped: ${error.message ?: error.javaClass.simpleName}",
                        sourceId = "$SOURCE_ID-status",
                    ),
                )
                running = false
            }
        } finally {
            stream?.release()
        }
    }

    private fun queueDiarization(endSample: Long) {
        if (!speakerAnalysisEnabled || !diarizationBusy.compareAndSet(false, true)) return
        val source = diarizer ?: run { diarizationBusy.set(false); return }
        val extractor = embeddingExtractor ?: run { diarizationBusy.set(false); return }
        val executor = diarizationExecutor ?: run { diarizationBusy.set(false); return }
        val samples = audioRing.snapshot(DIARIZATION_WINDOW_SAMPLES) ?: run {
            diarizationBusy.set(false)
            return
        }
        val startSample = endSample - samples.size
        executor.execute {
            if (!running) {
                diarizationBusy.set(false)
                return@execute
            }
            try {
                val segments = source.process(samples)
                val localVectors = LinkedHashMap<Int, MutableList<Pair<FloatArray, Float>>>()
                for (segment in segments) {
                    val from = (segment.start * SAMPLE_RATE).toInt().coerceIn(0, samples.size)
                    val to = (segment.end * SAMPLE_RATE).toInt().coerceIn(from, samples.size)
                    if (to - from < MIN_EMBEDDING_SAMPLES) continue
                    val embedding = extractEmbedding(extractor, samples.copyOfRange(from, to)) ?: continue
                    localVectors.getOrPut(segment.speaker) { mutableListOf() }
                        .add(embedding to (to - from).toFloat())
                }
                val localEmbeddings = localVectors.mapValues { (_, values) ->
                    weightedAverage(values)
                }
                synchronized(this@SherpaCaptionSource) {
                    if (!speakerAnalysisEnabled || !running) return@execute
                    val localToSession = speakerBank.assignWindow(localEmbeddings)
                    val updated = segments.mapNotNull { segment ->
                        val id = localToSession[segment.speaker] ?: return@mapNotNull null
                        TimedSpeakerSegment(
                            startMillis = sessionStartedAtMillis +
                                (startSample + (segment.start * SAMPLE_RATE).toLong()) * 1_000L / SAMPLE_RATE,
                            endMillis = sessionStartedAtMillis +
                                (startSample + (segment.end * SAMPLE_RATE).toLong()) * 1_000L / SAMPLE_RATE,
                            id = id,
                            confidence = segment.confidence.coerceIn(0f, 1f),
                        )
                    }
                    synchronized(diarizedSegments) {
                        diarizedSegments.removeAll { it.endMillis >= sessionStartedAtMillis +
                            startSample * 1_000L / SAMPLE_RATE }
                        diarizedSegments.addAll(updated)
                        diarizedSegments.sortBy { it.startMillis }
                    }
                }
                emitRecentSpeakerCorrections()
            } catch (error: Throwable) {
                if (running) {
                    lines.tryEmit(
                        CaptionLine(
                            text = "Acoustic speaker analysis error: ${error.message ?: error.javaClass.simpleName}",
                            sourceId = DIARIZATION_STATUS_SOURCE_ID,
                        ),
                    )
                }
            } finally {
                diarizationBusy.set(false)
            }
        }
    }

    private fun extractEmbedding(extractor: SpeakerEmbeddingExtractor, samples: FloatArray): FloatArray? {
        val stream = extractor.createStream()
        return try {
            stream.acceptWaveform(samples, SAMPLE_RATE)
            stream.inputFinished()
            var result: FloatArray? = null
            while (extractor.isReady(stream)) result = extractor.compute(stream)
            result?.takeIf { vector -> vector.isNotEmpty() && vector.all { it.isFinite() } }
        } finally {
            stream.release()
        }
    }

    private fun weightedAverage(embeddings: List<Pair<FloatArray, Float>>): FloatArray {
        val dimension = embeddings.first().first.size
        val average = FloatArray(dimension)
        var totalWeight = 0f
        for ((vector, weight) in embeddings) {
            if (vector.size != dimension) continue
            for (i in average.indices) average[i] += vector[i] * weight
            totalWeight += weight
        }
        if (totalWeight > 0f) for (i in average.indices) average[i] /= totalWeight
        normalize(average)
        return average
    }

    private fun emitRecentSpeakerCorrections() {
        val now = sessionStartedAtMillis + sampleCount * 1_000L / SAMPLE_RATE
        val cutoff = now - RECENT_CORRECTION_WINDOW_MS
        val recent = synchronized(diarizedSegments) { diarizedSegments.toList() }
        val captions = synchronized(lastCaptionLines) { lastCaptionLines.toList() }
        for (line in captions) {
            if (line.atMillis < cutoff) continue
            val match = recent.filter { line.atMillis >= it.startMillis &&
                line.atMillis <= it.endMillis + SPEAKER_END_TOLERANCE_MS }
                .maxByOrNull { it.confidence } ?: continue
            lines.tryEmit(
                line.copy(
                    sourceId = SPEAKER_UPDATE_SOURCE_ID,
                    speakerId = match.id,
                    speakerLabel = speakerBank.labelFor(match.id),
                    speakerConfidence = match.confidence.takeIf { it >= 0f },
                    speakerUncertain = match.confidence < MIN_CONFIDENT_LABEL,
                ),
            )
        }
    }

    private val lastCaptionLines = ArrayDeque<CaptionLine>()

    private fun speakerFor(atMillis: Long): SessionSpeakerBank.Speaker? {
        if (!speakerAnalysisEnabled) return null
        val match = synchronized(diarizedSegments) {
            diarizedSegments.filter {
                atMillis >= it.startMillis - SPEAKER_START_TOLERANCE_MS &&
                    atMillis <= it.endMillis + SPEAKER_END_TOLERANCE_MS
            }.maxByOrNull { it.confidence }
        } ?: return null
        return speakerBank.speakerFor(match.id, match.confidence)
    }

    private data class TimedSpeakerSegment(
        val startMillis: Long,
        val endMillis: Long,
        val id: Int,
        val confidence: Float,
    )

    private class FloatAudioRing(private val capacity: Int) {
        private val values = FloatArray(capacity)
        private var totalWritten = 0L

        @Synchronized fun clear() {
            values.fill(0f)
            totalWritten = 0L
        }

        @Synchronized fun append(samples: FloatArray) {
            for (sample in samples) {
                values[(totalWritten % capacity).toInt()] = sample
                totalWritten++
            }
        }

        @Synchronized fun snapshot(count: Int): FloatArray? {
            if (totalWritten < count) return null
            val output = FloatArray(count)
            val start = totalWritten - count
            for (i in 0 until count) output[i] = values[((start + i) % capacity).toInt()]
            return output
        }
    }

    private class SessionSpeakerBank {
        data class Speaker(val id: Int, val label: String, val confidence: Float)
        private data class Entry(val id: Int, var vector: FloatArray, var count: Int)
        private val entries = mutableListOf<Entry>()
        private var nextId = 1

        @Synchronized fun reset() {
            entries.clear()
            nextId = 1
        }

        @Synchronized fun assignWindow(embeddings: Map<Int, FloatArray>): Map<Int, Int> {
            val used = mutableSetOf<Int>()
            val result = mutableMapOf<Int, Int>()
            for ((localId, embedding) in embeddings) {
                val best = entries.asSequence()
                    .filter { it.id !in used && it.vector.size == embedding.size }
                    .map { it to cosine(it.vector, embedding) }
                    .maxByOrNull { it.second }
                val entry = if (best != null && best.second >= MATCH_THRESHOLD) {
                    best.first.also { update(it, embedding) }
                } else {
                    Entry(nextId++, embedding.copyOf(), 1).also(entries::add)
                }
                used += entry.id
                result[localId] = entry.id
            }
            return result
        }

        @Synchronized fun labelFor(id: Int): String = "Speaker ${speakerLetter(id)}"

        @Synchronized fun speakerFor(id: Int, confidence: Float): Speaker? {
            if (entries.none { it.id == id }) return null
            return Speaker(id, labelFor(id), confidence)
        }

        private fun update(entry: Entry, vector: FloatArray) {
            val total = (entry.count + 1).toFloat()
            for (i in entry.vector.indices) entry.vector[i] += (vector[i] - entry.vector[i]) / total
            normalize(entry.vector)
            entry.count++
        }

        private fun speakerLetter(id: Int): String {
            var value = id
            val result = StringBuilder()
            while (value > 0) {
                value--
                result.insert(0, ('A'.code + value % 26).toChar())
                value /= 26
            }
            return result.toString()
        }
    }

    companion object {
        const val SOURCE_ID = "speech-sherpa-offline"
        const val SPEAKER_UPDATE_SOURCE_ID = "speech-sherpa-speaker-update"
        const val DIARIZATION_STATUS_SOURCE_ID = "speech-sherpa-diarization-status"
        const val AUDIO_WARNING_SOURCE_ID = "speech-sherpa-audio-warning"
        const val INPUT_ROUTE_SOURCE_ID = "speech-sherpa-input-route"
        const val MIC_LEVEL_SOURCE_ID = "speech-sherpa-mic-level"
        private const val SAMPLE_RATE = 16_000
        private const val CHUNK_SAMPLES = 1_600
        private const val MAX_AUDIO_SECONDS = 20
        private const val DIARIZATION_WINDOW_SECONDS = 12
        private const val DIARIZATION_STEP_SECONDS = 4
        private const val DIARIZATION_WINDOW_SAMPLES = SAMPLE_RATE * DIARIZATION_WINDOW_SECONDS
        private const val DIARIZATION_STEP_SAMPLES = SAMPLE_RATE * DIARIZATION_STEP_SECONDS
        private const val MIN_EMBEDDING_SAMPLES = SAMPLE_RATE * 13 / 10
        private const val MATCH_THRESHOLD = 0.68f
        private const val MIN_CONFIDENT_LABEL = 0.45f
        private const val SPEAKER_START_TOLERANCE_MS = 250L
        private const val SPEAKER_END_TOLERANCE_MS = 1_800L
        private const val RECENT_CORRECTION_WINDOW_MS = 16_000L
        private const val MAX_RECENT_CAPTIONS = 32
        private const val SILENT_INPUT_WARNING_MS = 5_000L
        private const val MIC_LEVEL_UPDATE_MS = 150L
        private const val MIN_SIGNAL_RMS = 0.0005f
    }
}

private fun normalize(vector: FloatArray) {
    val norm = sqrt(vector.sumOf { (it * it).toDouble() }).toFloat()
    if (norm > 0f) for (i in vector.indices) vector[i] /= norm
}

private fun cosine(left: FloatArray, right: FloatArray): Float {
    if (left.size != right.size) return -1f
    var dot = 0f
    for (i in left.indices) dot += left[i] * right[i]
    return dot
}
