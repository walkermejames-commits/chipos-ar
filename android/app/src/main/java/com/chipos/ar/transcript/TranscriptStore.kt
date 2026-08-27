package com.chipos.ar.transcript

import android.content.Context
import com.chipos.ar.caption.CaptionLine
import java.io.File
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/**
 * Optional on-device transcript. Nothing is written unless the wearer turns
 * saving on. Files never leave app-private storage from this class.
 */
class TranscriptStore(context: Context) {

    private val dir = File(context.applicationContext.filesDir, "transcripts").apply { mkdirs() }
    private val savedFile = File(dir, "saved-session.txt")

    fun hasSavedTranscript(): Boolean = savedFile.exists() && savedFile.length() > 0L

    fun savedPreview(): String {
        if (!hasSavedTranscript()) return ""
        return savedFile.readText().take(400)
    }

    fun save(lines: List<CaptionLine>) {
        val body = buildString {
            appendLine("# ChipOS AR local transcript")
            appendLine("# Saved ${dateStamp()}")
            appendLine("# This file is on this phone only. It was not uploaded.")
            appendLine()
            lines.filter { !it.isPartial && it.text.isNotBlank() }.forEach { line ->
                val who = line.speakerLabel ?: "Speaker"
                appendLine("$who: ${line.text}")
            }
        }
        savedFile.writeText(body)
    }

    fun deleteSaved() {
        if (savedFile.exists()) savedFile.delete()
    }

    private fun dateStamp(): String {
        return SimpleDateFormat("yyyy-MM-dd HH:mm", Locale.UK).format(Date())
    }
}
