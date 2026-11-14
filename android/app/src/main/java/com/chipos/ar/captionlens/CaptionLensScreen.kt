package com.chipos.ar.captionlens

import android.Manifest
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.animateColorAsState
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.chipos.ar.caption.CaptionMode
import com.chipos.ar.overlay.ContrastMode
import com.chipos.ar.overlay.PanelPosition
import com.chipos.ar.teach.TeachingCard

private val ChipOsDark = darkColorScheme(
    background = Color(0xFF061C20),
    surface = Color(0xFF143739),
    primary = Color(0xFF9BD9C6),
    onPrimary = Color(0xFF141414),
    onBackground = Color(0xFFF5F5F0),
    onSurface = Color(0xFFF5F5F0),
)

@Composable
fun CaptionLensTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = ChipOsDark, content = content)
}

@Composable
fun CaptionLensScreen(
    viewModel: CaptionLensViewModel = viewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val palette = paletteFor(state.contrast)
    val micLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission(),
    ) { granted -> viewModel.onMicPermissionResult(granted) }

    LaunchedEffect(state.listening) {
        viewModel.refreshDisplays()
        viewModel.refreshAudioInputs()
    }
    LaunchedEffect(state.needsMicPermission) {
        if (state.needsMicPermission) {
            micLauncher.launch(Manifest.permission.RECORD_AUDIO)
        }
    }
    val immersiveLive = state.listening == ListeningState.Listening && state.captionMode.recordsAudio

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(palette.stage)
            .statusBarsPadding(),
    ) {
        ListeningBanner(
            state = state,
            palette = palette,
            compact = immersiveLive,
            modifier = Modifier
                .align(Alignment.TopStart)
                .padding(12.dp)
                .widthIn(max = if (immersiveLive) 300.dp else 560.dp),
        )

        if (immersiveLive) {
            BottomSubtitleBand(
                state = state,
                palette = palette,
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .fillMaxWidth()
                    .padding(horizontal = 12.dp, vertical = 16.dp),
            )
            TextButton(
                onClick = viewModel::stopListening,
                modifier = Modifier.align(Alignment.TopEnd).padding(8.dp),
            ) {
                Text("Stop captions", color = palette.fg, fontWeight = FontWeight.Bold)
            }
        } else {
            Column(
                modifier = Modifier
                    .align(alignmentFor(state.panelPosition))
                    .padding(horizontal = 16.dp, vertical = bottomPaddingFor(state.panelPosition))
                    .fillMaxWidth(widthFor(state.panelPosition))
                    .widthIn(max = 900.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                state.teachingCard?.let { card ->
                    TeachingCardView(
                        card = card,
                        palette = palette,
                        scale = state.textScale.multiplier,
                        onHide = viewModel::hideHelp,
                    )
                }
                CaptionCard(
                    state = state,
                    palette = palette,
                )
                if (state.showSession) {
                    SessionPanel(state = state, palette = palette)
                }
            }

            ControlDock(
                state = state,
                palette = palette,
                onStart = viewModel::startListening,
                onStop = viewModel::stopListening,
                onSmaller = viewModel::smallerText,
                onLarger = viewModel::largerText,
                onContrast = viewModel::cycleContrast,
                onPosition = viewModel::cyclePosition,
                onMode = viewModel::openEnginePicker,
                onTeach = viewModel::teachMeThis,
                onHideHelp = viewModel::hideHelp,
                onMore = viewModel::toggleMore,
                onDiarization = viewModel::toggleDiarization,
                onMicGain = viewModel::cycleMicGain,
                onInputPicker = viewModel::openInputPicker,
                onStartSession = viewModel::startTranscriptSession,
                onStopSession = viewModel::stopTranscriptSession,
                onShowSession = viewModel::toggleShowSession,
                onDeleteSession = viewModel::deleteCurrentSession,
                onDeleteSaved = viewModel::deleteSavedTranscript,
                onNameSpeaker = viewModel::openNameSpeakerDialog,
                onTestVoice = viewModel::simulateNextVoiceCommand,
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .fillMaxWidth()
                    .navigationBarsPadding(),
            )
        }
    }

    if (state.nameSpeakerDialog) {
        NameSpeakerDialog(
            currentLabel = state.currentLine?.speakerLabel ?: "Speaker",
            onDismiss = viewModel::dismissNameSpeakerDialog,
            onConfirm = viewModel::nameCurrentSpeaker,
            palette = palette,
        )
    }

    if (state.enginePickerOpen) {
        EngineSelectionDialog(
            state = state,
            palette = palette,
            onSelect = viewModel::chooseEngine,
            onDismiss = viewModel::dismissEnginePicker,
        )
    }
    if (state.inputPickerOpen) {
        AudioInputSelectionDialog(
            state = state,
            palette = palette,
            onSelect = viewModel::selectAudioInput,
            onDismiss = viewModel::dismissInputPicker,
        )
    }
}

@Composable
private fun ListeningBanner(
    state: CaptionUiState,
    palette: CaptionPalette,
    compact: Boolean = false,
    modifier: Modifier = Modifier,
) {
    val listening = state.listening == ListeningState.Listening
    val indicatorColor by animateColorAsState(
        targetValue = when {
            listening && state.captionMode == CaptionMode.LiveOnDevice -> Color(0xFF3DDB84)
            listening && state.captionMode == CaptionMode.LiveSherpa -> Color(0xFF3DDB84)
            listening -> Color(0xFF3DDB84)
            state.listening == ListeningState.Stopped -> Color(0xFFE0A325)
            else -> Color(0xFF8A8F99)
        },
        label = "listening-indicator",
    )
    val statusText = when {
        compact -> "LIVE · MIC ON"
        !listening && state.listening == ListeningState.Idle -> "NOT LISTENING"
        !listening -> "STOPPED"
        state.captionMode == CaptionMode.LiveOnDevice -> "LISTENING — ON-DEVICE"
        state.captionMode == CaptionMode.LiveSherpa -> "LISTENING — LOCAL ASR + SPEAKERS"
        else -> "LISTENING"
    }
    val privacy = when {
        !listening -> "Microphone is off."
        state.captionMode.recordsAudio ->
            "Microphone is on. On-device speech only. Audio is not uploaded. Cloud speech is not used."
        else -> "Microphone is off. Nothing is recorded."
    }

    Column(
        modifier = modifier
            .clip(RoundedCornerShape(14.dp))
            .background(palette.card.copy(alpha = if (compact) 0.56f else 0.92f))
            .border(2.dp, indicatorColor, RoundedCornerShape(14.dp))
            .padding(horizontal = 12.dp, vertical = 10.dp)
            .semantics {
                contentDescription = "$statusText. $privacy"
            },
        verticalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            Box(
                modifier = Modifier
                    .size(18.dp)
                    .clip(CircleShape)
                    .background(indicatorColor),
            )
            Text(
                text = statusText,
                color = palette.fg,
                fontWeight = FontWeight.Bold,
                fontSize = 16.sp,
            )
        }
        if (compact) {
            Text("On-device only", color = palette.fg.copy(alpha = 0.8f), fontSize = 11.sp)
            Text(
                text = "Input: ${state.audioInputName ?: "waiting for microphone"}",
                color = palette.fg.copy(alpha = 0.76f),
                fontSize = 10.sp,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            Text("Mic signal: ${"%.3f".format(state.micLevel * 100)}% full-scale", color = palette.fg.copy(alpha = 0.82f), fontSize = 10.sp)
            LinearProgressIndicator(
                progress = { state.micLevel },
                modifier = Modifier.fillMaxWidth(),
                color = palette.accent,
                trackColor = palette.fg.copy(alpha = 0.2f),
            )
        } else {
            Text(text = privacy, color = palette.fg, fontSize = 13.sp)
            Text(
                text = "${state.sourceName}  ·  Mic: ${if (state.recordsAudio && listening) "on" else "off"}  ·  Displays: ${state.displayCount}",
                color = palette.fg.copy(alpha = 0.8f),
                fontSize = 12.sp,
            )
            Text(
                text = state.displaySummary,
                color = palette.fg.copy(alpha = 0.7f),
                fontSize = 11.sp,
            )
        }
        if (state.speechError != null) {
            Text(text = state.speechError!!, color = Color(0xFFFF8A80), fontSize = 12.sp)
        }
        if (!compact && state.lastVoiceCommand != null) {
            Text(
                text = "Voice command: ${state.lastVoiceCommand}",
                color = palette.fg,
                fontSize = 12.sp,
                fontWeight = FontWeight.Medium,
            )
        }
        if (!compact) Text(text = state.engineStatus, color = palette.fg, fontSize = 12.sp, fontWeight = FontWeight.Medium)
        if (!compact && state.captionMode == CaptionMode.LiveOnDevice && !state.onDeviceSpeechAvailable) {
            Text(
                text = "Local speech engine unavailable. No cloud fallback.",
                color = Color(0xFFFFB74D),
                fontSize = 12.sp,
            )
        }
    }
}

@Composable
private fun BottomSubtitleBand(
    state: CaptionUiState,
    palette: CaptionPalette,
    modifier: Modifier = Modifier,
) {
    val transcriptScroll = rememberScrollState()
    val lines = buildList {
        addAll(state.recentCaptions.takeLast(2))
        val partial = state.currentLine?.takeIf { it.isPartial }
        if (partial != null && none { it.atMillis == partial.atMillis && it.text == partial.text }) {
            add(partial)
        }
    }
    Column(
        modifier = modifier
            .clip(RoundedCornerShape(16.dp))
            .background(Color(0x990A0D12))
            .border(2.dp, palette.border, RoundedCornerShape(16.dp))
            .padding(horizontal = 18.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        Text(
            text = "LIVE CAPTIONS · SPEAKER COLOURS",
            color = palette.fg.copy(alpha = 0.72f),
            fontSize = 12.sp,
            fontWeight = FontWeight.SemiBold,
        )
        LaunchedEffect(lines.lastOrNull()?.text, lines.size) {
            transcriptScroll.scrollTo(transcriptScroll.maxValue)
        }
        Column(
            modifier = Modifier
                .heightIn(max = 148.dp)
                .verticalScroll(transcriptScroll),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            if (lines.isEmpty()) {
                Text(
                    text = "Listening for speech…",
                    color = palette.fg,
                    fontSize = (22 * state.textScale.multiplier).sp,
                    fontWeight = FontWeight.Medium,
                )
            } else {
                lines.forEach { line ->
                    Row(horizontalArrangement = Arrangement.spacedBy(12.dp), verticalAlignment = Alignment.Top) {
                        Text(
                            text = line.speakerLabel ?: "Speaker ?",
                            color = speakerColor(line.speakerId),
                            fontSize = (15 * state.textScale.multiplier).sp,
                            fontWeight = FontWeight.Bold,
                        )
                        Text(
                            text = line.text,
                            color = palette.fg,
                            fontSize = (22 * state.textScale.multiplier).sp,
                            lineHeight = (27 * state.textScale.multiplier).sp,
                            modifier = Modifier.weight(1f),
                        )
                    }
                }
            }
        }
    }
}

private fun speakerColor(speakerId: Int?): Color = when (speakerId?.let { (it - 1).mod(6) }) {
    0 -> Color(0xFF5EDCFF)
    1 -> Color(0xFFFFB86B)
    2 -> Color(0xFF8FE3A2)
    3 -> Color(0xFFFF87B2)
    4 -> Color(0xFFB8A0FF)
    5 -> Color(0xFFFFE16B)
    else -> Color(0xFFCFD4DC)
}

@Composable
private fun CaptionCard(
    state: CaptionUiState,
    palette: CaptionPalette,
) {
    val scale = state.textScale.multiplier
    val body = state.currentLine?.text ?: "Captions will appear here."
    val speaker = state.currentLine?.speakerLabel

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(18.dp))
            .background(palette.card)
            .border(2.dp, palette.border, RoundedCornerShape(18.dp))
            .padding(horizontal = 18.dp, vertical = 14.dp)
            .heightIn(min = 96.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        Text(
            text = "ChipOS AR — Caption Lens",
            color = palette.fg,
            fontWeight = FontWeight.SemiBold,
            fontSize = (16 * scale).sp,
        )
        if (speaker != null) {
            Text(
                text = speaker + if (state.currentLine?.isPartial == true) " …" else "",
                color = palette.fg.copy(alpha = 0.85f),
                fontSize = (14 * scale).sp,
                fontWeight = FontWeight.Medium,
            )
        }
        Text(
            text = body,
            color = palette.fg,
            fontSize = (26 * scale).sp,
            fontWeight = FontWeight.Bold,
            lineHeight = (32 * scale).sp,
        )
        if (state.diarizationEnabled && state.speakerNote != null) {
            Text(
                text = state.speakerNote!!,
                color = if (state.speakerUncertain) Color(0xFFFFB74D) else palette.fg.copy(alpha = 0.8f),
                fontSize = (13 * scale).sp,
                fontWeight = if (state.speakerUncertain) FontWeight.SemiBold else FontWeight.Normal,
            )
        }
        Text(
            text = sessionFooter(state),
            color = palette.fg.copy(alpha = 0.75f),
            fontSize = 12.sp,
        )
    }
}

@Composable
private fun TeachingCardView(
    card: TeachingCard,
    palette: CaptionPalette,
    scale: Float,
    onHide: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(palette.card)
            .border(2.dp, palette.accent, RoundedCornerShape(16.dp))
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        Text(
            text = card.title,
            color = palette.fg,
            fontWeight = FontWeight.Bold,
            fontSize = (16 * scale).sp,
        )
        Text(
            text = card.body,
            color = palette.fg,
            fontSize = (16 * scale).sp,
            lineHeight = (22 * scale).sp,
        )
        Text(
            text = "About: ${card.sourceLine}",
            color = palette.fg.copy(alpha = 0.8f),
            fontSize = 12.sp,
        )
        Text(
            text = card.uncertainty,
            color = palette.fg.copy(alpha = 0.85f),
            fontSize = 12.sp,
        )
        DockButton(label = "Hide help", onClick = onHide, palette = palette)
    }
}

@Composable
private fun SessionPanel(
    state: CaptionUiState,
    palette: CaptionPalette,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .heightIn(max = 140.dp)
            .clip(RoundedCornerShape(14.dp))
            .background(palette.card.copy(alpha = 0.95f))
            .border(1.dp, palette.border, RoundedCornerShape(14.dp))
            .padding(12.dp)
            .verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(4.dp),
    ) {
        Text(
            text = if (state.transcriptSessionOn) "Current session (recording locally)" else "Current session (not recording)",
            color = palette.fg,
            fontWeight = FontWeight.SemiBold,
            fontSize = 13.sp,
        )
        if (state.sessionLines.isEmpty()) {
            Text(text = "No session lines yet.", color = palette.fg.copy(alpha = 0.75f), fontSize = 12.sp)
        } else {
            state.sessionLines.takeLast(12).forEach { line ->
                val who = line.speakerLabel ?: "Caption"
                Text(text = "$who: ${line.text}", color = palette.fg, fontSize = 12.sp)
            }
        }
        if (state.savedTranscriptExists) {
            Text(
                text = "Saved file on this phone:\n${state.savedTranscriptPreview}",
                color = palette.fg.copy(alpha = 0.8f),
                fontSize = 11.sp,
            )
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun ControlDock(
    state: CaptionUiState,
    palette: CaptionPalette,
    onStart: () -> Unit,
    onStop: () -> Unit,
    onSmaller: () -> Unit,
    onLarger: () -> Unit,
    onContrast: () -> Unit,
    onPosition: () -> Unit,
    onMode: () -> Unit,
    onTeach: () -> Unit,
    onHideHelp: () -> Unit,
    onMore: () -> Unit,
    onDiarization: () -> Unit,
    onMicGain: () -> Unit,
    onInputPicker: () -> Unit,
    onStartSession: () -> Unit,
    onStopSession: () -> Unit,
    onShowSession: () -> Unit,
    onDeleteSession: () -> Unit,
    onDeleteSaved: () -> Unit,
    onNameSpeaker: () -> Unit,
    onTestVoice: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val listening = state.listening == ListeningState.Listening
    Surface(color = palette.dock, modifier = modifier) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 10.dp, vertical = 8.dp)
                .heightIn(max = 240.dp)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            FlowRow(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                DockButton(
                    "Start live listening",
                    onStart,
                    palette,
                    emphasized = !listening,
                    enabled = !listening,
                )
                DockButton("Stop Listening", onStop, palette, emphasized = listening, enabled = listening)
                DockButton("Choose engine: ${state.captionMode.label}", onMode, palette)
                if (state.captionMode == CaptionMode.LiveSherpa) {
                    DockButton("Choose microphone", onInputPicker, palette)
                }
                DockButton("Text −", onSmaller, palette)
                DockButton("Text +", onLarger, palette)
                DockButton("Contrast: ${state.contrast.label}", onContrast, palette)
                DockButton("Panel: ${state.panelPosition.label}", onPosition, palette)
                DockButton("Teach me this", onTeach, palette)
                DockButton("Hide help", onHideHelp, palette)
                DockButton(if (state.moreOpen) "Less" else "More", onMore, palette)
            }
            if (state.moreOpen) {
                FlowRow(
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    if (state.captionMode == CaptionMode.LiveSherpa) {
                        DockButton("Mic boost: ${state.micGain}×", onMicGain, palette)
                    }
                    DockButton(
                        if (state.diarizationEnabled) "Speakers: on" else "Speakers: off",
                        onDiarization,
                        palette,
                    )
                    DockButton(
                        "Start session",
                        onStartSession,
                        palette,
                        emphasized = !state.transcriptSessionOn,
                        enabled = !state.transcriptSessionOn,
                    )
                    DockButton(
                        "Stop session",
                        onStopSession,
                        palette,
                        emphasized = state.transcriptSessionOn,
                        enabled = state.transcriptSessionOn,
                    )
                    DockButton(
                        if (state.showSession) "Hide session" else "Show session",
                        onShowSession,
                        palette,
                    )
                    DockButton("Delete session", onDeleteSession, palette)
                    DockButton(
                        "Delete saved transcript",
                        onDeleteSaved,
                        palette,
                        enabled = state.savedTranscriptExists,
                    )
                    DockButton(
                        "Name this speaker",
                        onNameSpeaker,
                        palette,
                        enabled = state.currentLine?.speakerId != null &&
                            state.captionMode != CaptionMode.LiveSherpa,
                    )
                    DockButton("Test voice: ${state.nextVoiceCommand.label}", onTestVoice, palette)
                }
            }
        }
    }
}

@Composable
private fun EngineSelectionDialog(
    state: CaptionUiState,
    palette: CaptionPalette,
    onSelect: (CaptionMode) -> Unit,
    onDismiss: () -> Unit,
) {
    val liveReady = state.onDeviceSpeechAvailable
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Caption engine") },
        text = {
            Column(
                verticalArrangement = Arrangement.spacedBy(10.dp),
                modifier = Modifier.verticalScroll(rememberScrollState()),
            ) {
                Text("Live engines only. Speech is processed on this phone; audio is never sent to a service.")
                EngineChoiceRow(
                    title = CaptionMode.LiveOnDevice.label,
                    detail = if (liveReady) {
                        "Ready on this phone. Microphone is requested only after Start Listening. Audio stays on-device. Not Google cloud."
                    } else {
                        "Not yet available on this phone. No local speech engine. Cloud speech will not be used."
                    },
                    selected = state.captionMode == CaptionMode.LiveOnDevice,
                    available = liveReady,
                    palette = palette,
                    onClick = { onSelect(CaptionMode.LiveOnDevice) },
                )
                EngineChoiceRow(
                    title = CaptionMode.LiveSherpa.label,
                    detail = "Bundled offline English speech and acoustic-speaker models. Labels are anonymous and session-only; the first label needs a 12-second audio window.",
                    selected = state.captionMode == CaptionMode.LiveSherpa,
                    available = true,
                    palette = palette,
                    onClick = { onSelect(CaptionMode.LiveSherpa) },
                )
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) { Text("Close") }
        },
        containerColor = palette.card,
        titleContentColor = palette.fg,
        textContentColor = palette.fg,
    )
}

@Composable
private fun AudioInputSelectionDialog(
    state: CaptionUiState,
    palette: CaptionPalette,
    onSelect: (Int?) -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Microphone input") },
        text = {
            Column(
                verticalArrangement = Arrangement.spacedBy(8.dp),
                modifier = Modifier.verticalScroll(rememberScrollState()),
            ) {
                Text("Choose an input for the offline Sherpa microphone capture. Android may use the phone mic if a connected route is unavailable.")
                EngineChoiceRow(
                    title = "System default microphone",
                    detail = "Let Android choose the active microphone.",
                    selected = state.selectedAudioInputId == null,
                    available = true,
                    palette = palette,
                    onClick = { onSelect(null) },
                )
                state.audioInputs.forEach { input ->
                    EngineChoiceRow(
                        title = input.name,
                        detail = input.kind,
                        selected = state.selectedAudioInputId == input.id,
                        available = true,
                        palette = palette,
                        onClick = { onSelect(input.id) },
                    )
                }
                if (state.audioInputs.isEmpty()) Text("Android reports no microphone inputs.")
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text("Close") } },
        containerColor = palette.card,
        titleContentColor = palette.fg,
        textContentColor = palette.fg,
    )
}

@Composable
private fun EngineChoiceRow(
    title: String,
    detail: String,
    selected: Boolean,
    available: Boolean,
    palette: CaptionPalette,
    onClick: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(if (selected) palette.button else palette.card)
            .border(2.dp, if (selected) palette.accent else palette.border, RoundedCornerShape(12.dp))
            .padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(4.dp),
    ) {
        Text(
            text = title + if (selected) "  (selected)" else "",
            color = palette.fg,
            fontWeight = FontWeight.Bold,
            fontSize = 15.sp,
        )
        Text(
            text = if (available) "Available" else "NOT YET AVAILABLE",
            color = if (available) palette.fg else Color(0xFFFFB74D),
            fontWeight = FontWeight.SemiBold,
            fontSize = 13.sp,
        )
        Text(text = detail, color = palette.fg, fontSize = 13.sp)
        Button(
            onClick = onClick,
            colors = ButtonDefaults.buttonColors(
                containerColor = palette.accent,
                contentColor = palette.accentFg,
            ),
        ) {
            Text(if (selected) "Keep this engine" else "Use this engine")
        }
    }
}

@Composable
private fun NameSpeakerDialog(
    currentLabel: String,
    onDismiss: () -> Unit,
    onConfirm: (String) -> Unit,
    palette: CaptionPalette,
) {
    var name by remember { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Name $currentLabel") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("Only type a name you already know. The app will not identify strangers.")
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("Name for this speaker") },
                    singleLine = true,
                )
            }
        },
        confirmButton = {
            TextButton(onClick = { onConfirm(name) }) { Text("Save") }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Cancel") }
        },
        containerColor = palette.card,
        titleContentColor = palette.fg,
        textContentColor = palette.fg,
    )
}

@Composable
private fun DockButton(
    label: String,
    onClick: () -> Unit,
    palette: CaptionPalette,
    emphasized: Boolean = false,
    enabled: Boolean = true,
) {
    Button(
        onClick = onClick,
        enabled = enabled,
        colors = ButtonDefaults.buttonColors(
            containerColor = if (emphasized) palette.accent else palette.button,
            contentColor = if (emphasized) palette.accentFg else palette.fg,
            disabledContainerColor = palette.button.copy(alpha = 0.4f),
            disabledContentColor = palette.fg.copy(alpha = 0.4f),
        ),
    ) {
        Text(text = label, fontSize = 15.sp, fontWeight = FontWeight.SemiBold)
    }
}

private data class CaptionPalette(
    val stage: Color,
    val card: Color,
    val dock: Color,
    val fg: Color,
    val border: Color,
    val button: Color,
    val accent: Color,
    val accentFg: Color,
)

private fun paletteFor(mode: ContrastMode): CaptionPalette = when (mode) {
    ContrastMode.Standard -> CaptionPalette(
        stage = Color(0xFF05070A),
        card = Color(0xCC10141C),
        dock = Color(0xE610141C),
        fg = Color(0xFFF5F5F0),
        border = Color(0xFFD0D0C8),
        button = Color(0xFF2A3140),
        accent = Color(0xFF3DDB84),
        accentFg = Color(0xFF05210F),
    )
    ContrastMode.High -> CaptionPalette(
        stage = Color(0xFF000000),
        card = Color(0xF2000000),
        dock = Color(0xF2000000),
        fg = Color(0xFFFFF176),
        border = Color(0xFFFFF176),
        button = Color(0xFF1A1A00),
        accent = Color(0xFFFFF176),
        accentFg = Color(0xFF111100),
    )
    ContrastMode.Inverse -> CaptionPalette(
        stage = Color(0xFFFFF8C0),
        card = Color(0xFFFFF176),
        dock = Color(0xFFFFF176),
        fg = Color(0xFF111111),
        border = Color(0xFF111111),
        button = Color(0xFFFFE082),
        accent = Color(0xFF111111),
        accentFg = Color(0xFFFFF176),
    )
}

private fun alignmentFor(position: PanelPosition): Alignment = when (position) {
    PanelPosition.BottomCenter,
    PanelPosition.LowerCenter,
    PanelPosition.LowestCenter,
    -> Alignment.BottomCenter
    PanelPosition.BottomLeft -> Alignment.BottomStart
    PanelPosition.BottomRight -> Alignment.BottomEnd
}

private fun widthFor(position: PanelPosition): Float = when (position) {
    PanelPosition.BottomLeft, PanelPosition.BottomRight -> 0.72f
    else -> 0.94f
}

private fun bottomPaddingFor(position: PanelPosition) = when (position) {
    PanelPosition.LowerCenter -> 132.dp
    PanelPosition.BottomCenter, PanelPosition.BottomLeft, PanelPosition.BottomRight -> 96.dp
    PanelPosition.LowestCenter -> 72.dp
}

private fun sessionFooter(state: CaptionUiState): String {
    val rec = if (state.transcriptSessionOn) "session ON" else "session off"
    val saved = if (state.savedTranscriptExists) "saved file yes" else "saved file no"
    return "Session lines: ${state.sessionLineCount}  ·  $rec  ·  $saved"
}
