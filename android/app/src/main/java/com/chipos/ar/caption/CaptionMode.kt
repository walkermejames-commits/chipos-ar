package com.chipos.ar.caption

/**
 * How Caption Lens produces text.
 *
 * Demo modes never open the microphone. Live on-device speech is the only
 * real-audio path. There is no cloud speech mode and no silent fallback.
 */
enum class CaptionMode(val label: String, val recordsAudio: Boolean) {
    TestSample("Demo — sample captions", recordsAudio = false),
    TestScripted("Demo — several speakers", recordsAudio = false),
    LiveOnDevice("Live — on-device speech", recordsAudio = true),
    ;

    fun next(): CaptionMode {
        val values = entries
        return values[(ordinal + 1) % values.size]
    }
}
