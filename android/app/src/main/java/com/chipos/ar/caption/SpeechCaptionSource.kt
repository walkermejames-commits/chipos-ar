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
 * Live captions using Android's **on-device** speech recognizer only.
 *
 * This class never constructs [SpeechRecognizer.createSpeechRecognizer], which
 * may send audio to Google. If on-device recognition is missing, [start]
 * fails with [LocalSpeechUnavailableException] and the microphone is not used.
 */
class SpeechCaptionSource(
    context: Context,
) : CaptionSource, SpeechToTextModule {

    private val appContext = context.applicationContext
    private val mainHandler = Handler(Looper.getMainLooper())
    private val lines = MutableSharedFlow<CaptionLine>(extraBufferCapacity = 32)

    @Volatile
    private var running = false
    private var recognizer: SpeechRecognizer? = null

    override val id: String = "speech-on-device"
    override val displayName: String = "On-device speech"
    override val recordsAudio: Boolean = true

    override fun captions(): Flow<CaptionLine> = lines.asSharedFlow()

    override suspend fun start() {
        withContext(Dispatchers.Main) {
            if (!OnDeviceSpeech.isAvailable(appContext)) {
                throw LocalSpeechUnavailableException()
            }
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
        recognizer = try {
            SpeechRecognizer.createOnDeviceSpeechRecognizer(appContext)
        } catch (error: Exception) {
            throw LocalSpeechUnavailableException(error)
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
            putExtra(RecognizerIntent.EXTRA_PREFER_OFFLINE, true)
        }
        try {
            recognizer?.startListening(intent)
        } catch (error: Exception) {
            emitStatus("On-device recognizer failed to start: ${error.message ?: error.javaClass.simpleName}")
            running = false
            destroyRecognizer()
        }
    }

    private fun destroyRecognizer() {
        try {
            recognizer?.cancel()
            recognizer?.destroy()
        } catch (_: Exception) {
            // Already torn down.
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
            emitStatus("On-device speech unavailable: ${error.message ?: error.javaClass.simpleName}")
            running = false
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
                SpeechRecognizer.ERROR_NETWORK,
                SpeechRecognizer.ERROR_NETWORK_TIMEOUT,
                -> {
                    emitStatus("On-device speech reported a network error. Cloud speech is not enabled. Stopping.")
                    running = false
                    destroyRecognizer()
                }
                SpeechRecognizer.ERROR_CLIENT,
                SpeechRecognizer.ERROR_RECOGNIZER_BUSY,
                SpeechRecognizer.ERROR_NO_MATCH,
                SpeechRecognizer.ERROR_SPEECH_TIMEOUT,
                SpeechRecognizer.ERROR_AUDIO,
                -> scheduleRestart(250L)
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

class LocalSpeechUnavailableException(
    cause: Throwable? = null,
) : IllegalStateException(
    "On-device speech is not available on this phone. Cloud speech is not used.",
    cause,
)
