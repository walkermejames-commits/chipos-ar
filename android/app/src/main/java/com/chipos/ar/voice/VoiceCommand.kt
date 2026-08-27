package com.chipos.ar.voice

enum class VoiceCommand(val spoken: String, val label: String) {
    StartCaptions("start captions", "Start captions"),
    StopCaptions("stop captions", "Stop captions"),
    MakeItBigger("make it bigger", "Make it bigger"),
    MoveSubtitlesDown("move subtitles down", "Move subtitles down"),
    TeachMeThis("teach me this", "Teach me this"),
    HideHelp("hide help", "Hide help"),
}
