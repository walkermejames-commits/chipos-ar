package com.chipos.ar.xreal

import android.app.Activity
import android.app.Presentation
import android.graphics.Color
import android.hardware.display.DisplayManager
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.view.*
import android.widget.FrameLayout
import android.widget.TextView
import com.chipos.ar.captionlens.CaptionUiState
import com.chipos.ar.captionlens.ListeningState
import com.chipos.ar.overlay.PanelPosition
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow

data class GlassesDisplayState(
    val connected: Boolean = false,
    val enabled: Boolean = false,
    val hidden: Boolean = false,
    val testPattern: Boolean = false,
    val message: String = "No separate display detected. Phone controls remain available.",
)

/** Android external display, not an XREAL tracking implementation. Never opens a camera. */
class GlassesDisplayController(private val activity: Activity) : DisplayManager.DisplayListener {
    private val manager = activity.getSystemService(DisplayManager::class.java)
    private val mutable = MutableStateFlow(GlassesDisplayState())
    val state = mutable.asStateFlow()
    private var presentation: CaptionPresentation? = null
    private var captionState = CaptionUiState()

    init {
        manager.registerDisplayListener(this, Handler(Looper.getMainLooper()))
        refresh()
    }

    fun update(value: CaptionUiState) { captionState = value; render() }
    fun enable() {
        val display = manager.getDisplays(DisplayManager.DISPLAY_CATEGORY_PRESENTATION).firstOrNull()
        if (display == null) { refresh(); return }
        try {
            presentation?.dismiss()
            presentation = CaptionPresentation(activity, display).also { it.show() }
            mutable.value = GlassesDisplayState(true, true, message = "Separate display active. Controls stay on this phone. View-fixed; no spatial tracking.")
            render()
        } catch (error: WindowManager.InvalidDisplayException) {
            presentation = null
            mutable.value = GlassesDisplayState(message = "Display disconnected. Reconnect the glasses and try again.")
        } catch (error: SecurityException) {
            presentation = null
            mutable.value = GlassesDisplayState(message = "Android denied this display. Use phone captions; separate output is unavailable.")
        }
    }
    fun disable() {
        presentation?.dismiss(); presentation = null
        mutable.value = mutable.value.copy(enabled = false, hidden = false, testPattern = false)
        refresh()
    }
    fun toggleHidden() { mutable.value = mutable.value.copy(hidden = !mutable.value.hidden); render() }
    fun toggleTest() { mutable.value = mutable.value.copy(testPattern = !mutable.value.testPattern); render() }
    private fun render() { presentation?.render(captionState, mutable.value) }
    private fun refresh() {
        val available = manager.getDisplays(DisplayManager.DISPLAY_CATEGORY_PRESENTATION)
        if (presentation != null && available.none { it.displayId == presentation?.display?.displayId }) {
            presentation?.dismiss(); presentation = null
            mutable.value = GlassesDisplayState()
        }
        val connected = available.isNotEmpty()
        mutable.value = mutable.value.copy(connected = connected, message = when {
            presentation != null -> "Separate display active. Controls stay on this phone. View-fixed; no spatial tracking."
            connected -> "Separate display detected. Tap Connect glasses display. No audio starts automatically."
            else -> "No separate display detected. Android may be mirroring this phone or using DeX. Separate phone controls require an external presentation display; this app cannot provide tracked AR."
        })
    }
    override fun onDisplayAdded(id: Int) = refresh()
    override fun onDisplayChanged(id: Int) = refresh()
    override fun onDisplayRemoved(id: Int) = refresh()
    fun close() { manager.unregisterDisplayListener(this); presentation?.dismiss(); presentation = null }
}

private class CaptionPresentation(activity: Activity, display: Display) : Presentation(activity, display) {
    private lateinit var root: FrameLayout
    private lateinit var text: TextView
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window?.apply {
            setBackgroundDrawableResource(android.R.color.black)
            addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
            decorView.systemUiVisibility = View.SYSTEM_UI_FLAG_FULLSCREEN or View.SYSTEM_UI_FLAG_HIDE_NAVIGATION or View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
        }
        root = FrameLayout(context).apply { setBackgroundColor(Color.BLACK) }
        text = TextView(context).apply {
            setTextColor(Color.WHITE); gravity = Gravity.CENTER; maxLines = 4
            setShadowLayer(2f, 0f, 0f, Color.BLACK)
            setPadding(16, 8, 16, 8)
        }
        root.addView(text)
        setContentView(root)
    }
    fun render(captions: CaptionUiState, output: GlassesDisplayState) {
        if (!::text.isInitialized) return
        text.visibility = if (output.hidden) View.INVISIBLE else View.VISIBLE
        text.textSize = 26f * captions.textScale.multiplier
        val metrics = context.resources.displayMetrics
        val side = when (captions.panelPosition) {
            PanelPosition.BottomLeft -> Gravity.LEFT
            PanelPosition.BottomRight -> Gravity.RIGHT
            else -> Gravity.CENTER_HORIZONTAL
        }
        text.layoutParams = FrameLayout.LayoutParams((metrics.widthPixels * .68f).toInt(), ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.BOTTOM or side).apply {
            bottomMargin = (metrics.heightPixels * when(captions.panelPosition) {
                PanelPosition.LowerCenter -> .24f
                PanelPosition.LowestCenter -> .06f
                else -> .13f
            }).toInt()
            leftMargin = (metrics.widthPixels * .06f).toInt(); rightMargin = leftMargin
        }
        text.text = when {
            output.testPattern -> "DISPLAY TEST — not live speech\nChange size and position on your phone"
            !captions.captionMode.recordsAudio -> "DEMO MODE · switch to live speech on phone"
            captions.listening != ListeningState.Listening -> "Microphone off · controls on phone"
            captions.currentLine == null -> "Listening…"
            else -> captions.currentLine!!.let { line ->
                val label = if (captions.diarizationEnabled) (line.speakerLabel ?: "Speaker unknown") + if (line.speakerUncertain) " ?" else "" else ""
                listOf(label, line.text).filter { it.isNotBlank() }.joinToString("\n")
            }
        }
    }
}
