package com.chipos.ar.teach

/**
 * One short on-demand explanation. No model is bundled yet. Cloud AI is
 * accepted as a flag only; this engine still returns local mock copy so we
 * never upload captions by surprise.
 */
class MockTeachingEngine : TeachingEngine {

    override fun explain(captionText: String, cloudOptIn: Boolean): TeachingCard {
        val line = captionText.trim().ifEmpty { "No caption yet." }
        val topic = topicFor(line)
        val cloudNote = if (cloudOptIn) {
            "Cloud AI is switched on, but no remote model is connected yet, so this card is still a local mock. Nothing was uploaded."
        } else {
            "Local mock only. Cloud AI is off. Nothing was uploaded."
        }
        return TeachingCard(
            title = "Teach me this",
            body = topic,
            sourceLine = line,
            isMock = true,
            uncertainty = "$cloudNote This is a brief guess from the last caption, not a verified fact.",
        )
    }

    private fun topicFor(line: String): String {
        val t = line.lowercase()
        return when {
            listOf("platform", "train", "bus", "timetable", "ticket").any { it in t } ->
                "This sounds like travel information. Read the platform or stop, the destination, and the time as three separate facts. If any of those is missing, treat the plan as incomplete."
            listOf("leaflet", "print", "tiny", "sign", "board").any { it in t } ->
                "The last caption is about something hard to read. Caption Lens can enlarge the spoken words. It cannot yet see the printed sign. If the text matters, ask someone nearby or move closer."
            listOf("minute", "o'clock", "hour", "time").any { it in t } ->
                "A time was mentioned. Confirm whether it is a departure, arrival, or waiting time before acting on it. I cannot see a clock from here."
            else ->
                "From the last caption, the useful next step is to repeat the concrete nouns (place, object, time) and ignore filler. I do not know the scene beyond these words."
        }
    }
}
