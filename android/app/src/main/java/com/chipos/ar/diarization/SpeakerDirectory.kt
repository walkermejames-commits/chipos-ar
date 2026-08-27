package com.chipos.ar.diarization

import android.content.Context
import com.chipos.ar.modules.SpeakerDiarizationModule

/**
 * Maps speaker slots to labels. Default is "Speaker 1", "Speaker 2", …
 * A personal name is stored only when the wearer types it.
 */
class SpeakerDirectory(
    context: Context,
) : SpeakerDiarizationModule {

    private val prefs = context.applicationContext.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    fun labelFor(speakerId: Int): String {
        val named = prefs.getString(key(speakerId), null)?.trim().orEmpty()
        return named.ifEmpty { "Speaker $speakerId" }
    }

    fun hasWearerName(speakerId: Int): Boolean {
        return !prefs.getString(key(speakerId), null)?.trim().isNullOrEmpty()
    }

    fun nameSpeaker(speakerId: Int, name: String) {
        val trimmed = name.trim()
        if (trimmed.isEmpty()) {
            prefs.edit().remove(key(speakerId)).apply()
        } else {
            prefs.edit().putString(key(speakerId), trimmed).apply()
        }
    }

    fun clearNames() {
        prefs.edit().clear().apply()
    }

    private fun key(speakerId: Int) = "speaker_$speakerId"

    private companion object {
        const val PREFS = "chipos_speaker_names"
    }
}
