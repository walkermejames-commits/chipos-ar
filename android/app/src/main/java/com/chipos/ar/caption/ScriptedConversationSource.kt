package com.chipos.ar.caption

import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.emptyFlow
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.isActive

/**
 * Test mode: a scripted three-speaker conversation. No microphone. Speaker
 * numbers are part of the script, not identified people.
 */
class ScriptedConversationSource : CaptionSource {
    override val id: String = SOURCE_ID
    override val displayName: String = "Scripted conversation"
    override val recordsAudio: Boolean = false

    private val running = MutableStateFlow(false)

    @OptIn(ExperimentalCoroutinesApi::class)
    override fun captions(): Flow<CaptionLine> = running.flatMapLatest { isRunning ->
        if (!isRunning) {
            emptyFlow()
        } else {
            flow {
                var index = 0
                while (currentCoroutineContext().isActive) {
                    val beat = SCRIPT[index % SCRIPT.size]
                    emit(
                        CaptionLine(
                            text = beat.text,
                            speakerLabel = "Speaker ${beat.speaker}",
                            speakerId = beat.speaker,
                            speakerConfidence = 1.0f,
                            speakerUncertain = false,
                            isPartial = false,
                            sourceId = SOURCE_ID,
                        ),
                    )
                    index += 1
                    delay(beat.holdMs)
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

    private data class Beat(
        val speaker: Int,
        val text: String,
        val holdMs: Long = 2600L,
    )

    private companion object {
        const val SOURCE_ID = "scripted"
        val SCRIPT = listOf(
            Beat(1, "The board says platform 4."),
            Beat(2, "Is that the train to Canterbury?"),
            Beat(1, "Yes. It leaves in twelve minutes."),
            Beat(3, "I'll get the tickets."),
            Beat(2, "The print is tiny on this leaflet."),
            Beat(1, "We can read it together."),
            Beat(3, "Platform 4 is to the left of the cafe."),
            Beat(2, "Thank you. I will wait by the bench."),
        )
    }
}
