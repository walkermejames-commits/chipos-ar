package com.chipos.ar.voice

/**
 * Maps short wearer phrases to Caption Lens actions.
 *
 * Commands are matched against the full utterance, not substrings inside a
 * longer sentence, so ordinary conversation is less likely to fire them.
 */
object VoiceCommandParser {

    fun parse(raw: String): VoiceCommand? {
        val text = normalize(raw)
        if (text.isEmpty()) return null
        return rules.firstOrNull { rule -> rule.phrases.any { it == text } }?.command
    }

    fun isCommand(raw: String): Boolean = parse(raw) != null

    private data class Rule(
        val command: VoiceCommand,
        val phrases: Set<String>,
    )

    private val rules = listOf(
        Rule(
            VoiceCommand.StartCaptions,
            setOf(
                "start captions",
                "start listening",
                "start caption",
                "captions on",
            ),
        ),
        Rule(
            VoiceCommand.StopCaptions,
            setOf(
                "stop captions",
                "stop listening",
                "stop caption",
                "captions off",
            ),
        ),
        Rule(
            VoiceCommand.MakeItBigger,
            setOf(
                "make it bigger",
                "bigger text",
                "increase text",
                "text bigger",
                "make text bigger",
            ),
        ),
        Rule(
            VoiceCommand.MoveSubtitlesDown,
            setOf(
                "move subtitles down",
                "move captions down",
                "subtitles down",
                "captions down",
                "move it down",
            ),
        ),
        Rule(
            VoiceCommand.TeachMeThis,
            setOf(
                "teach me this",
                "teach me",
                "explain this",
                "what is this",
            ),
        ),
        Rule(
            VoiceCommand.HideHelp,
            setOf(
                "hide help",
                "hide teaching",
                "dismiss help",
                "close help",
                "hide the help",
            ),
        ),
    )

    private fun normalize(raw: String): String {
        return raw.lowercase()
            .replace(Regex("[^a-z0-9\\s]"), " ")
            .replace(Regex("\\s+"), " ")
            .trim()
    }
}
