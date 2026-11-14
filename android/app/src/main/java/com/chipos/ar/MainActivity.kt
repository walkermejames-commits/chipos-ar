package com.chipos.ar

import android.os.Bundle
import android.graphics.PixelFormat
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.viewModels
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import com.chipos.ar.captionlens.CaptionLensScreen
import com.chipos.ar.captionlens.CaptionLensTheme
import com.chipos.ar.captionlens.CaptionLensViewModel
import com.chipos.ar.studio.StudioApp
import com.chipos.ar.xreal.GlassesDisplayController
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch

class MainActivity : ComponentActivity() {
    private val captions: CaptionLensViewModel by viewModels()
    private lateinit var glasses: GlassesDisplayController
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        glasses = GlassesDisplayController(this)
        lifecycleScope.launch { captions.uiState.collect { glasses.update(it) } }
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        enableEdgeToEdge()
        WindowCompat.setDecorFitsSystemWindows(window, false)
        WindowInsetsControllerCompat(window, window.decorView).apply {
            isAppearanceLightStatusBars = false
            isAppearanceLightNavigationBars = false
            hide(WindowInsetsCompat.Type.navigationBars())
            systemBarsBehavior =
                WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
        }
        setContent {
            CaptionLensTheme {
                StudioApp(captions, glasses = glasses)
            }
        }
    }

    override fun onStop() {
        // Only the explicitly started foreground overlay service may listen over other apps.
        if (!isChangingConfigurations) captions.stopListening()
        super.onStop()
    }

    override fun onDestroy() {
        glasses.close()
        super.onDestroy()
    }
}
