package com.chipos.ar.caption

/**
 * How Caption Lens produces text.
 *
 * User-selectable caption sources. All selectable modes are live and use a
 * local microphone path; there is no demo mode, cloud mode, or silent fallback.
 */
enum class CaptionMode(val label: String, val recordsAudio: Boolean) {
    LiveOnDevice("Live — on-device speech", recordsAudio = true),
    LiveSherpa("Live — offline ASR + acoustic speakers", recordsAudio = true),
    ;

    fun next(): CaptionMode {
        val values = entries
        return values[(ordinal + 1) % values.size]
    }
}
