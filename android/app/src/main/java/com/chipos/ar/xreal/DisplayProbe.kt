package com.chipos.ar.xreal

import android.content.Context
import android.hardware.display.DisplayManager
import android.view.Display

data class DisplaySnapshot(
    val count: Int,
    val summary: String,
)

fun probeDisplays(context: Context): DisplaySnapshot {
    val manager = context.getSystemService(Context.DISPLAY_SERVICE) as DisplayManager
    val displays = manager.displays
    val parts = displays.map { display ->
        val name = display.name ?: "unnamed"
        val flags = buildList {
            if (display.displayId == Display.DEFAULT_DISPLAY) add("default")
            if (display.flags and Display.FLAG_PRESENTATION != 0) add("presentation")
            if (display.flags and Display.FLAG_SECURE != 0) add("secure")
        }.joinToString("+").ifEmpty { "flags=${display.flags}" }
        "#${display.displayId} $name ${display.mode.physicalWidth}x${display.mode.physicalHeight} $flags"
    }
    return DisplaySnapshot(
        count = displays.size,
        summary = parts.joinToString(" | ").ifEmpty { "none" },
    )
}
