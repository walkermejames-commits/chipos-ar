package com.chipos.ar.caption

/**
 * How Caption Lens produces text.
 *
 * Test modes never open the microphone. Live on-device is the default real
 * speech path. Cloud speech is off until the wearer opts in.
 */
enum class CaptionMode(val label: String, val recordsAudio: Boolean) {
    TestScripted("Test — several speakers", recordsAudio = false),
    TestSample("Test — Phase 1 sample", recordsAudio = false),
    LiveOnDevice("Live — on-device speech", recordsAudio = true),
    LiveCloud("Live — cloud speech (opt-in)", recordsAudio = true),
    ;

    fun next(): CaptionMode {
        val values = entries
        return values[(ordinal + 1) % values.size]
    }
}
