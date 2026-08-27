package com.chipos.ar.caption

/**
 * One caption line shown in Caption Lens.
 *
 * [speakerLabel] is a provisional label such as "Speaker 1", or a name the
 * wearer typed. Names are never invented. [speakerUncertain] must be true
 * whenever the speaker assignment is a guess.
 */
data class CaptionLine(
    val text: String,
    val speakerLabel: String? = null,
    val isPartial: Boolean = false,
    val sourceId: String,
    val speakerId: Int? = null,
    val speakerConfidence: Float? = null,
    val speakerUncertain: Boolean = false,
    val atMillis: Long = System.currentTimeMillis(),
)
