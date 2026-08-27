package com.chipos.ar.caption

import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.emptyFlow
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.isActive

/**
 * Fake captions for the first build. Does not open the microphone, does not
 * record, and does not contact a network.
 */
class SampleCaptionSource : CaptionSource {
    override val id: String = SOURCE_ID
    override val displayName: String = "Sample captions"
    override val recordsAudio: Boolean = false

    private val running = MutableStateFlow(false)

    override fun captions(): Flow<CaptionLine> = running.flatMapLatest { isRunning ->
        if (!isRunning) {
            emptyFlow()
        } else {
            flow {
                var index = 0
                while (currentCoroutineContext().isActive) {
                    val sample = SAMPLE_LINES[index % SAMPLE_LINES.size]
                    emit(
                        CaptionLine(
                            text = sample,
                            speakerLabel = "Speaker 1",
                            isPartial = false,
                            sourceId = SOURCE_ID,
                        ),
                    )
                    index += 1
                    delay(LINE_INTERVAL_MS)
                }
            }
        }
    }

    override suspend fun start() {
        running.value = true
    }

    override suspend fun stop() {
        running.value = false
    }

    private companion object {
        const val SOURCE_ID = "sample"
        const val LINE_INTERVAL_MS = 2800L
        val SAMPLE_LINES = listOf(
            "ChipOS AR — Caption Lens",
            "This is sample caption text for the first glasses test.",
            "The listening light is on. The microphone is still off.",
            "Nothing is recorded. Nothing is sent to the cloud.",
            "Use Text size, Contrast, and Panel position to make this readable.",
            "Keep this panel low, outside the centre of your view.",
        )
    }
}
