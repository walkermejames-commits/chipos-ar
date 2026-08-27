package com.chipos.ar.captionlens

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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.chipos.ar.overlay.ContrastMode
import com.chipos.ar.overlay.PanelPosition

private val ChipOsDark = darkColorScheme(
    background = Color(0xFF05070A),
    surface = Color(0xFF10141C),
    primary = Color(0xFFF4E27A),
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
    LaunchedEffect(state.listening) {
        viewModel.refreshDisplays()
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(palette.stage)
            .statusBarsPadding(),
    ) {
        ListeningBanner(
            state = state,
            palette = palette,
            modifier = Modifier
                .align(Alignment.TopStart)
                .padding(12.dp)
                .widthIn(max = 520.dp),
        )

        CaptionCard(
            state = state,
            palette = palette,
            modifier = Modifier
                .align(alignmentFor(state.panelPosition))
                .padding(horizontal = 16.dp, vertical = bottomPaddingFor(state.panelPosition))
                .fillMaxWidth(widthFor(state.panelPosition))
                .widthIn(max = 900.dp),
        )

        ControlDock(
            state = state,
            palette = palette,
            onStart = viewModel::startListening,
            onStop = viewModel::stopListening,
            onSmaller = viewModel::smallerText,
            onLarger = viewModel::largerText,
            onContrast = viewModel::cycleContrast,
            onPosition = viewModel::cyclePosition,
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .navigationBarsPadding(),
        )
    }
}

@Composable
private fun ListeningBanner(
    state: CaptionUiState,
    palette: CaptionPalette,
    modifier: Modifier = Modifier,
) {
    val indicatorColor by animateColorAsState(
        targetValue = when (state.listening) {
            ListeningState.Listening -> Color(0xFF3DDB84)
            ListeningState.Stopped -> Color(0xFFE0A325)
            ListeningState.Idle -> Color(0xFF8A8F99)
        },
        label = "listening-indicator",
    )
    val statusText = when (state.listening) {
        ListeningState.Idle -> "NOT LISTENING"
        ListeningState.Listening -> "LISTENING — SAMPLE TEXT"
        ListeningState.Stopped -> "STOPPED"
    }
    val privacy = when (state.listening) {
        ListeningState.Idle -> "Microphone is off. Sample captions only. Nothing is recorded."
        ListeningState.Listening -> "Listening light is on. Microphone is still off. No audio is captured or sent anywhere."
        ListeningState.Stopped -> "Listening stopped. Microphone is off."
    }

    Column(
        modifier = modifier
            .clip(RoundedCornerShape(14.dp))
            .background(palette.card.copy(alpha = 0.92f))
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
        Text(
            text = privacy,
            color = palette.fg,
            fontSize = 13.sp,
        )
        Text(
            text = "${state.sourceName}  ·  Mic: off  ·  Displays: ${state.displayCount}",
            color = palette.fg.copy(alpha = 0.8f),
            fontSize = 12.sp,
        )
        Text(
            text = state.displaySummary,
            color = palette.fg.copy(alpha = 0.7f),
            fontSize = 11.sp,
        )
    }
}

@Composable
private fun CaptionCard(
    state: CaptionUiState,
    palette: CaptionPalette,
    modifier: Modifier = Modifier,
) {
    val scale = state.textScale.multiplier
    val body = state.currentLine?.text ?: "Captions will appear here."
    val speaker = state.currentLine?.speakerLabel

    Column(
        modifier = modifier
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
                text = speaker,
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
    modifier: Modifier = Modifier,
) {
    Surface(
        color = palette.dock,
        modifier = modifier,
    ) {
        FlowRow(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 10.dp, vertical = 8.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            val listening = state.listening == ListeningState.Listening
            DockButton(
                label = "Start Listening",
                onClick = onStart,
                emphasized = !listening,
                palette = palette,
                enabled = !listening,
            )
            DockButton(
                label = "Stop Listening",
                onClick = onStop,
                emphasized = listening,
                palette = palette,
                enabled = listening,
            )
            DockButton(label = "Text −", onClick = onSmaller, palette = palette)
            DockButton(label = "Text +", onClick = onLarger, palette = palette)
            DockButton(
                label = "Contrast: ${state.contrast.label}",
                onClick = onContrast,
                palette = palette,
            )
            DockButton(
                label = "Panel: ${state.panelPosition.label}",
                onClick = onPosition,
                palette = palette,
            )
            Text(
                text = "Text ${state.textScale.label}",
                color = palette.fg,
                fontSize = 13.sp,
                modifier = Modifier.padding(top = 10.dp),
            )
        }
    }
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
    PanelPosition.BottomCenter, PanelPosition.LowerCenter -> Alignment.BottomCenter
    PanelPosition.BottomLeft -> Alignment.BottomStart
    PanelPosition.BottomRight -> Alignment.BottomEnd
}

private fun widthFor(position: PanelPosition): Float = when (position) {
    PanelPosition.BottomLeft, PanelPosition.BottomRight -> 0.72f
    else -> 0.94f
}

private fun bottomPaddingFor(position: PanelPosition) = when (position) {
    PanelPosition.LowerCenter -> 118.dp
    else -> 92.dp
}
