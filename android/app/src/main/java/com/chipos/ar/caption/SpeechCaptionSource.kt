package com.chipos.ar.caption

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.emptyFlow

/**
 * Reserved seam for on-device speech-to-text.
 *
 * Not used in Phase 1. Android's default [android.speech.SpeechRecognizer]
 * may send audio to Google servers on many phones. That is not allowed here
 * unless the wearer makes an explicit, informed choice and we have verified
 * an on-device engine on the test device.
 *
 * Do not request RECORD_AUDIO until this source is actually wired and local.
 */
class SpeechCaptionSource : CaptionSource {
    override val id: String = "speech-on-device-unwired"
    override val displayName: String = "On-device speech (not wired)"
    override val recordsAudio: Boolean = true

    override fun captions(): Flow<CaptionLine> = emptyFlow()

    override suspend fun start() {
        error("SpeechCaptionSource is not wired in Phase 1")
    }

    override suspend fun stop() = Unit
}
