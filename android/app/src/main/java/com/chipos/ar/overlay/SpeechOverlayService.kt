package com.chipos.ar.overlay

import android.app.*
import android.content.Intent
import android.content.pm.ServiceInfo
import android.graphics.Color
import android.graphics.PixelFormat
import android.graphics.drawable.GradientDrawable
import android.os.IBinder
import android.provider.Settings
import android.view.*
import android.widget.*
import androidx.core.app.NotificationCompat
import com.chipos.ar.MainActivity
import com.chipos.ar.R
import com.chipos.ar.caption.CaptionLine
import com.chipos.ar.caption.SherpaCaptionSource
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow

/** Explicitly started, local-only captions above other apps. No transcript is saved. */
class SpeechOverlayService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private lateinit var source: SherpaCaptionSource
    private lateinit var windows: WindowManager
    private var panel: LinearLayout? = null
    private lateinit var status: TextView
    private lateinit var captions: TextView
    private var fontSize = 22f
    private var current: CaptionLine? = null
    private var previous: CaptionLine? = null
    private var started = false
    private var startupJob: Job? = null

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        source = SherpaCaptionSource(applicationContext)
        windows = getSystemService(WINDOW_SERVICE) as WindowManager
        val channel = NotificationChannel(CHANNEL, "Live captions", NotificationManager.IMPORTANCE_LOW)
        channel.description = "Visible while ChipOS uses the microphone for local speech captions"
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == STOP) { stopSelf(); return START_NOT_STICKY }
        if (started) return START_NOT_STICKY
        if (!Settings.canDrawOverlays(this)) { stopSelf(); return START_NOT_STICKY }
        try {
            val open = PendingIntent.getActivity(this, 0, Intent(this, MainActivity::class.java), PendingIntent.FLAG_IMMUTABLE)
            val stop = PendingIntent.getService(this, 1, Intent(this, javaClass).setAction(STOP), PendingIntent.FLAG_IMMUTABLE)
            val notification = NotificationCompat.Builder(this, CHANNEL)
                .setSmallIcon(R.drawable.ic_launcher_foreground)
                .setContentTitle("ChipOS microphone is on")
                .setContentText("Offline captions + provisional speaker labels. Tap Stop to end.")
                .setContentIntent(open).setOngoing(true).setSilent(true)
                .addAction(0, "Stop", stop).build()
            startForeground(31, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_MICROPHONE)
            showPanel()
            started = true
            mutableActive.value = true
            scope.launch {
                source.captions().collect { receive(it) }
            }
            startupJob = scope.launch {
                try {
                    val prefs = getSharedPreferences("chipos_caption_prefs", MODE_PRIVATE)
                    source.setSpeakerAnalysisEnabled(prefs.getBoolean("diarization", true))
                    source.setInputGain(prefs.getFloat("mic_input_gain", 2f))
                    source.setPreferredInputDevice(prefs.getInt("audio_input_device_id", -1).takeIf { it >= 0 })
                    source.start()
                    status.text = "MIC ON · LOCAL SPEECH · DRAG TO MOVE"
                } catch (error: CancellationException) {
                    throw error
                } catch (error: Exception) {
                    Toast.makeText(this@SpeechOverlayService, "Captions could not start: ${error.message}", Toast.LENGTH_LONG).show()
                    stopSelf()
                }
            }
        } catch (error: Exception) {
            Toast.makeText(this, "Overlay unavailable: ${error.message}", Toast.LENGTH_LONG).show()
            stopSelf()
        }
        return START_NOT_STICKY
    }

    private fun showPanel() {
        val density = resources.displayMetrics.density
        fun dp(value: Int) = (value * density).toInt()
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(16), dp(10), dp(16), dp(12))
            background = GradientDrawable().apply {
                setColor(Color.argb(240, 7, 29, 31)); cornerRadius = dp(20).toFloat()
                setStroke(dp(1), Color.rgb(194, 165, 108))
            }
        }
        val header = LinearLayout(this).apply { gravity = Gravity.CENTER_VERTICAL }
        status = TextView(this).apply {
            text = "STARTING LOCAL MODELS… · DRAG TO MOVE"
            textSize = 11f; setTextColor(Color.rgb(144, 223, 204))
            contentDescription = "Microphone status. Drag this header to move captions."
        }
        header.addView(status, LinearLayout.LayoutParams(0, dp(48), 1f))
        fun button(label: String, description: String, action: () -> Unit) = Button(this).apply {
            text = label; contentDescription = description; textSize = 14f
            setTextColor(Color.WHITE); minimumWidth = dp(48); minWidth = dp(48)
            setOnClickListener { action() }
        }
        header.addView(button("A−", "Smaller captions") { fontSize = (fontSize - 2).coerceAtLeast(16f); captions.textSize = fontSize })
        header.addView(button("A+", "Larger captions") { fontSize = (fontSize + 2).coerceAtMost(36f); captions.textSize = fontSize })
        header.addView(button("Stop", "Stop microphone and close overlay") { stopSelf() })
        root.addView(header)
        captions = TextView(this).apply {
            text = "Listening for speech. Speaker labels need about 12 seconds."
            textSize = fontSize; setTextColor(Color.rgb(249, 240, 221))
            maxLines = 7; setPadding(0, dp(6), 0, 0)
            accessibilityLiveRegion = View.ACCESSIBILITY_LIVE_REGION_POLITE
        }
        root.addView(captions)
        val metrics = resources.displayMetrics
        val params = WindowManager.LayoutParams(
            (metrics.widthPixels - dp(24)).coerceAtMost(dp(760)), WindowManager.LayoutParams.WRAP_CONTENT,
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL,
            PixelFormat.TRANSLUCENT,
        ).apply { gravity = Gravity.TOP or Gravity.START; x = dp(12); y = (metrics.heightPixels * .65f).toInt() }
        var downX = 0f; var downY = 0f; var originX = 0; var originY = 0
        status.setOnTouchListener { _, event ->
            when (event.action) {
                MotionEvent.ACTION_DOWN -> { downX = event.rawX; downY = event.rawY; originX = params.x; originY = params.y; true }
                MotionEvent.ACTION_MOVE -> {
                    params.x = (originX + event.rawX - downX).toInt().coerceIn(0, (resources.displayMetrics.widthPixels - root.width).coerceAtLeast(0))
                    params.y = (originY + event.rawY - downY).toInt().coerceIn(0, (resources.displayMetrics.heightPixels - root.height).coerceAtLeast(0))
                    windows.updateViewLayout(root, params); true
                }
                MotionEvent.ACTION_UP -> { status.performClick(); true }
                else -> false
            }
        }
        windows.addView(root, params)
        panel = root
    }

    private fun receive(line: CaptionLine) {
        when (line.sourceId) {
            SherpaCaptionSource.MIC_LEVEL_SOURCE_ID, SherpaCaptionSource.INPUT_ROUTE_SOURCE_ID -> return
            SherpaCaptionSource.SPEAKER_UPDATE_SOURCE_ID -> {
                if (current?.atMillis == line.atMillis && current?.text == line.text) current = line
                if (previous?.atMillis == line.atMillis && previous?.text == line.text) previous = line
            }
            SherpaCaptionSource.DIARIZATION_STATUS_SOURCE_ID, SherpaCaptionSource.AUDIO_WARNING_SOURCE_ID -> { status.text = line.text; return }
            else -> {
                if (line.sourceId.endsWith("-status")) {
                    Toast.makeText(this, line.text, Toast.LENGTH_LONG).show(); stopSelf(); return
                }
                if (current?.isPartial == false && current?.atMillis != line.atMillis) previous = current
                current = line
            }
        }
        captions.text = listOfNotNull(previous, current).joinToString("\n\n") {
            val speaker = it.speakerLabel?.let { name -> name + if (it.speakerUncertain) " ?" else "" } ?: "Speaker unknown"
            "$speaker  ·  ${it.text}"
        }
    }

    override fun onDestroy() {
        startupJob?.cancel()
        panel?.let { runCatching { windows.removeView(it) } }
        panel = null
        // Stop capture promptly, then let the native inference worker finish before releasing JNI objects.
        scope.launch { try { source.stop() } finally { mutableActive.value = false; scope.cancel() } }
        stopForeground(STOP_FOREGROUND_REMOVE)
        super.onDestroy()
    }

    companion object {
        const val STOP = "com.chipos.ar.STOP_OVERLAY"
        private const val CHANNEL = "chipos_live_captions"
        private val mutableActive = MutableStateFlow(false)
        val active = mutableActive.asStateFlow()
    }
}
