package com.chipos.ar.caption

/**
 * One caption line shown in Caption Lens.
 *
 * [speakerLabel] is a provisional label such as "Speaker 1". Names are never
 * invented; later diarization must keep labels unnamed until the wearer
 * explicitly names a speaker.
 */
data class CaptionLine(
    val text: String,
    val speakerLabel: String? = null,
    val isPartial: Boolean = false,
    val sourceId: String,
)
