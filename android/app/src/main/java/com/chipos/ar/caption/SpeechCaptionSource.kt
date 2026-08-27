package com.chipos.ar.caption

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.speech.RecognitionListener
import android.speech.RecognizerIntent
import android.speech.SpeechRecognizer
import com.chipos.ar.modules.SpeechToTextModule
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.withContext

/**
 * Live captions through Android's speech recognizer.
 *
 * [onDevice] true uses [SpeechRecognizer.createOnDeviceSpeechRecognizer].
 * [onDevice] false uses the default recognizer and may send audio to Google.
 * Callers must only use the cloud path after an explicit opt-in.
 */
class SpeechCaptionSource(
    context: Context,
    private val onDevice: Boolean,
) : CaptionSource, SpeechToTextModule {

    private val appContext = context.applicationContext
    private val mainHandler = Handler(Looper.getMainLooper())
    private val lines = MutableSharedFlow<CaptionLine>(extraBufferCapacity = 32)

    @Volatile
    private var running = false
    private var recognizer: SpeechRecognizer? = null

    override val id: String = if (onDevice) "speech-on-device" else "speech-cloud"
    override val displayName: String = if (onDevice) "On-device speech" else "Cloud speech"
    override val recordsAudio: Boolean = true

    override fun captions(): Flow<CaptionLine> = lines.asSharedFlow()

    override suspend fun start() {
        withContext(Dispatchers.Main) {
            running = true
            ensureRecognizer()
            startListening()
        }
    }

    override suspend fun stop() {
        withContext(Dispatchers.Main) {
            running = false
            mainHandler.removeCallbacks(restartRunnable)
            destroyRecognizer()
        }
    }

    private fun ensureRecognizer() {
        if (recognizer != null) return
        recognizer = if (onDevice) {
            SpeechRecognizer.createOnDeviceSpeechRecognizer(appContext)
        } else {
            SpeechRecognizer.createSpeechRecognizer(appContext)
        }
        recognizer?.setRecognitionListener(listener)
    }

    private fun startListening() {
        if (!running) return
        val intent = Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH).apply {
            putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
            putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, true)
            putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 3)
            putExtra(RecognizerIntent.EXTRA_CALLING_PACKAGE, appContext.packageName)
            putExtra(RecognizerIntent.EXTRA_PREFER_OFFLINE, onDevice)
        }
        try {
            recognizer?.startListening(intent)
        } catch (error: Exception) {
            emitStatus("Speech recognizer failed to start: ${error.message ?: error.javaClass.simpleName}")
            scheduleRestart(800L)
        }
    }

    private fun destroyRecognizer() {
        try {
            recognizer?.cancel()
            recognizer?.destroy()
        } catch (_: Exception) {
            // Recognizer may already be torn down.
        }
        recognizer = null
    }

    private fun scheduleRestart(delayMs: Long) {
        mainHandler.removeCallbacks(restartRunnable)
        if (running) mainHandler.postDelayed(restartRunnable, delayMs)
    }

    private val restartRunnable = Runnable {
        if (!running) return@Runnable
        try {
            ensureRecognizer()
            startListening()
        } catch (error: Exception) {
            emitStatus("Could not restart listening: ${error.message ?: error.javaClass.simpleName}")
        }
    }

    private val listener = object : RecognitionListener {
        override fun onReadyForSpeech(params: Bundle?) = Unit
        override fun onBeginningOfSpeech() = Unit
        override fun onRmsChanged(rmsdB: Float) = Unit
        override fun onBufferReceived(buffer: ByteArray?) = Unit
        override fun onEndOfSpeech() = Unit

        override fun onError(error: Int) {
            when (error) {
                SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS -> {
                    emitStatus("Microphone permission is missing.")
                    running = false
                }
                SpeechRecognizer.ERROR_CLIENT,
                SpeechRecognizer.ERROR_RECOGNIZER_BUSY,
                SpeechRecognizer.ERROR_NO_MATCH,
                SpeechRecognizer.ERROR_SPEECH_TIMEOUT,
                SpeechRecognizer.ERROR_AUDIO,
                -> scheduleRestart(250L)
                SpeechRecognizer.ERROR_NETWORK,
                SpeechRecognizer.ERROR_NETWORK_TIMEOUT,
                -> {
                    if (onDevice) {
                        scheduleRestart(400L)
                    } else {
                        emitStatus("Cloud speech lost the network.")
                        scheduleRestart(1200L)
                    }
                }
                else -> scheduleRestart(500L)
            }
        }

        override fun onResults(results: Bundle?) {
            emitFrom(results, partial = false)
            scheduleRestart(120L)
        }

        override fun onPartialResults(partialResults: Bundle?) {
            emitFrom(partialResults, partial = true)
        }

        override fun onEvent(eventType: Int, params: Bundle?) = Unit
    }

    private fun emitFrom(bundle: Bundle?, partial: Boolean) {
        val text = bundle
            ?.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION)
            ?.firstOrNull()
            ?.trim()
            .orEmpty()
        if (text.isEmpty()) return
        lines.tryEmit(
            CaptionLine(
                text = text,
                isPartial = partial,
                sourceId = id,
            ),
        )
    }

    private fun emitStatus(message: String) {
        lines.tryEmit(
            CaptionLine(
                text = message,
                isPartial = false,
                sourceId = "$id-status",
            ),
        )
    }
}
