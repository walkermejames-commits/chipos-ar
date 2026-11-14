package com.chipos.ar

import android.graphics.PixelFormat
import android.hardware.display.DisplayManager
import android.media.ImageReader
import android.view.View
import android.view.ViewGroup
import android.view.inspector.WindowInspector
import android.widget.TextView
import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.chipos.ar.caption.CaptionLine
import com.chipos.ar.captionlens.CaptionUiState
import com.chipos.ar.captionlens.ListeningState
import com.chipos.ar.xreal.GlassesDisplayController
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class GlassesDisplayTest {
    private fun texts(view: View): List<TextView> = when(view) {
        is TextView -> listOf(view)
        is ViewGroup -> (0 until view.childCount).flatMap { texts(view.getChildAt(it)) }
        else -> emptyList()
    }
    @Test fun externalCaptionWindowIsIndependentAndCanHideResizeAndDisconnect() {
        ActivityScenario.launch(MainActivity::class.java).use { scenario ->
            lateinit var reader: ImageReader
            lateinit var display: android.hardware.display.VirtualDisplay
            lateinit var controller: GlassesDisplayController
            scenario.onActivity { activity ->
                reader = ImageReader.newInstance(1280, 720, PixelFormat.RGBA_8888, 2)
                val manager = activity.getSystemService(DisplayManager::class.java)
                display = manager.createVirtualDisplay("ChipOS instrumented glasses", 1280, 720, 160, reader.surface,
                    DisplayManager.VIRTUAL_DISPLAY_FLAG_PRESENTATION or DisplayManager.VIRTUAL_DISPLAY_FLAG_OWN_CONTENT_ONLY)
                controller = GlassesDisplayController(activity)
                    assertTrue(controller.state.value.connected)
                    controller.enable()
                    assertTrue(controller.state.value.enabled)
                    controller.update(CaptionUiState(listening = ListeningState.Listening,
                        currentLine = CaptionLine("External caption verification", sourceId = "test", speakerLabel = "Speaker A")))
            }
            InstrumentationRegistry.getInstrumentation().waitForIdleSync()
            scenario.onActivity { activity ->
                try {
                    val captionView = WindowInspector.getGlobalWindowViews().flatMap(::texts).single { it.text.contains("External caption verification") }
                    assertTrue(captionView.text.contains("Speaker A"))
                    assertNotEquals(activity.window.decorView, captionView.rootView)
                    assertEquals(display.display.displayId, captionView.display.displayId)
                    val originalSize = captionView.textSize
                    controller.update(CaptionUiState(listening = ListeningState.Listening, textScale = com.chipos.ar.overlay.TextScale.Huge,
                        currentLine = CaptionLine("External caption verification", sourceId = "test")))
                    assertTrue(captionView.textSize > originalSize)
                    controller.toggleHidden()
                    assertEquals(View.INVISIBLE, captionView.visibility)
                    controller.toggleHidden()
                    controller.toggleTest()
                    assertTrue(captionView.text.contains("DISPLAY TEST"))
                    controller.toggleTest()
                    controller.update(CaptionUiState())
                    assertTrue(captionView.text.contains("Microphone off"))
                    controller.disable()
                    assertFalse(controller.state.value.enabled)
                } finally { controller.close(); display.release(); reader.close() }
            }
            InstrumentationRegistry.getInstrumentation().waitForIdleSync()
        }
    }
}
