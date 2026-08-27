package com.chipos.ar.overlay

/**
 * Caption card placement. All options stay in the lower portion of the
 * display so the centre of vision stays clear.
 */
enum class PanelPosition(val label: String) {
    BottomCenter("Bottom centre"),
    BottomLeft("Bottom left"),
    BottomRight("Bottom right"),
    LowerCenter("Lower centre"),
    ;

    fun next(): PanelPosition {
        val values = entries
        return values[(ordinal + 1) % values.size]
    }
}

enum class ContrastMode(val label: String) {
    Standard("White on black"),
    High("Yellow on black"),
    Inverse("Black on yellow"),
    ;

    fun next(): ContrastMode {
        val values = entries
        return values[(ordinal + 1) % values.size]
    }
}

enum class TextScale(val label: String, val multiplier: Float) {
    Medium("M", 1.0f),
    Large("L", 1.25f),
    ExtraLarge("XL", 1.55f),
    Huge("XXL", 1.9f),
    ;

    fun smaller(): TextScale = entries[kotlin.math.max(0, ordinal - 1)]

    fun larger(): TextScale = entries[kotlin.math.min(entries.lastIndex, ordinal + 1)]
}
