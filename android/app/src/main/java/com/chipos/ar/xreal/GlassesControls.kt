package com.chipos.ar.xreal

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.chipos.ar.captionlens.CaptionLensViewModel
import com.chipos.ar.captionlens.ListeningState
import com.chipos.ar.caption.CaptionMode

@Composable
fun GlassesControls(controller: GlassesDisplayController, captions: CaptionLensViewModel, microphoneBusy: Boolean, onClose: () -> Unit) {
    val display by controller.state.collectAsStateWithLifecycle()
    val speech by captions.uiState.collectAsStateWithLifecycle()
    Column(Modifier.fillMaxSize().background(Color(0xFF061C20)).safeDrawingPadding().verticalScroll(rememberScrollState()).padding(20.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
        TextButton(onClick = onClose) { Text("← Studio") }
        Text("Glasses remote", fontSize = 30.sp, color = Color.White)
        Text("Phone = controls. Glasses = captions.", fontSize = 20.sp, color = Color(0xFF9BD9C6))
        Text(display.message, color = Color.White, fontSize = 17.sp)
        Button(onClick = { if (display.enabled) controller.disable() else controller.enable() }, enabled = display.connected, modifier = Modifier.fillMaxWidth().heightIn(min = 56.dp)) {
            Text(if (display.enabled) "Disconnect glasses display" else "Connect glasses display")
        }
        Text("This is an optical caption display, not room-anchored AR. Black pixels emit no light on optical glasses; dimming and the environment still affect visibility. No camera or room scan is used.", color = Color(0xFFB3C6C1))
        if (microphoneBusy) Text("Floating captions are running. Stop Float in Studio before using these microphone controls.", color = Color(0xFFE5BD78))
        Button(onClick = {
            if (speech.listening == ListeningState.Listening) captions.stopListening()
            else captions.startListening()
        }, enabled = !microphoneBusy && speech.captionMode.recordsAudio, modifier = Modifier.fillMaxWidth().heightIn(min = 60.dp)) {
            Text(if (speech.listening == ListeningState.Listening) "Stop microphone" else "Start live captions", fontSize = 20.sp)
        }
        if (speech.captionMode != CaptionMode.LiveSherpa) OutlinedButton(onClick = { captions.chooseEngine(CaptionMode.LiveSherpa) }) { Text("Use bundled offline speech") }
        Text(speech.speechError ?: speech.engineStatus, color = Color.White)
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            OutlinedButton(onClick = captions::smallerText, modifier = Modifier.weight(1f).heightIn(min = 56.dp)) { Text("A− Smaller") }
            OutlinedButton(onClick = captions::largerText, modifier = Modifier.weight(1f).heightIn(min = 56.dp)) { Text("A+ Larger") }
        }
        OutlinedButton(onClick = captions::cyclePosition, modifier = Modifier.fillMaxWidth().heightIn(min = 56.dp)) { Text("Move captions · ${speech.panelPosition.label}") }
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            OutlinedButton(onClick = controller::toggleHidden, enabled = display.enabled, modifier = Modifier.weight(1f)) { Text(if (display.hidden) "Show captions" else "Hide captions") }
            OutlinedButton(onClick = controller::toggleTest, enabled = display.enabled, modifier = Modifier.weight(1f)) { Text(if (display.testPattern) "End display test" else "Test display") }
        }
        Text("PHONE PREVIEW · ${speech.textScale.label}", color = Color(0xFF9BD9C6))
        Text(speech.currentLine?.text ?: "Live words appear here after you start the microphone. Use Test display to check the glasses without listening.", color = Color.White, fontSize = (22 * speech.textScale.multiplier).sp)
        Text("ChipOS Studio 0.3.1 · If you see ‘ChipOS Mark II’ and ‘Caption frame received’, close the older prototype and open ChipOS Studio.", color = Color(0xFFB3C6C1))
    }
}
