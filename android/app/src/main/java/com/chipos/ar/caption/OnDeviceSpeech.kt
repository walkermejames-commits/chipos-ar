package com.chipos.ar.caption

import android.content.Context
import android.os.Build
import android.speech.SpeechRecognizer

object OnDeviceSpeech {
    fun isAvailable(context: Context): Boolean {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.S) return false
        return try {
            SpeechRecognizer.isOnDeviceRecognitionAvailable(context)
        } catch (_: Exception) {
            false
        }
    }

    fun isLegacyRecognizerAvailable(context: Context): Boolean {
        return SpeechRecognizer.isRecognitionAvailable(context)
    }
}
