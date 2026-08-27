package com.chipos.ar.diarization

/**
 * Pause-based turn taking for live speech. This is not voice printing and
 * not identity. Confidence stays low so the UI can show uncertainty.
 */
class TurnDiarizer {

    private var currentSpeaker = 1
    private var lastFinalAt = 0L
    private var announced = 0

    fun reset() {
        currentSpeaker = 1
        lastFinalAt = 0L
        announced = 0
    }

    fun onFinalUtterance(nowMillis: Long = System.currentTimeMillis()): Assignment {
        if (lastFinalAt != 0L && nowMillis - lastFinalAt >= TURN_GAP_MS) {
            currentSpeaker += 1
            if (currentSpeaker > MAX_SPEAKERS) currentSpeaker = 1
        }
        lastFinalAt = nowMillis
        announced = currentSpeaker
        return Assignment(
            speakerId = currentSpeaker,
            confidence = 0.28f,
            uncertain = true,
        )
    }

    fun currentPartial(): Assignment {
        return Assignment(
            speakerId = currentSpeaker,
            confidence = 0.18f,
            uncertain = true,
        )
    }

    data class Assignment(
        val speakerId: Int,
        val confidence: Float,
        val uncertain: Boolean,
    )

    private companion object {
        const val TURN_GAP_MS = 1800L
        const val MAX_SPEAKERS = 4
    }
}
