package com.chipos.ar.studio

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.grid.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.chipos.ar.captionlens.*
import com.chipos.ar.overlay.SpeechOverlayService
import kotlinx.coroutines.launch
import com.chipos.ar.xreal.GlassesDisplayController
import com.chipos.ar.xreal.GlassesControls

private val Ink = Color(0xFF092B2D)
private val Deep = Color(0xFF061C20)
private val Paper = Color(0xFFF5ECD9)
private val Mint = Color(0xFF9BD9C6)
private val Brass = Color(0xFFE5BD78)
private val Muted = Color(0xFFB3C6C1)
private data class Area(val title: String, val subtitle: String, val icon: ImageVector, val tint: Color)
private val areas = listOf(
    Area("Writing desk", "Novels, scenes & the next sentence", Icons.Outlined.EditNote, Brass),
    Area("Sewing & making", "Patterns, materials & works in progress", Icons.Outlined.ContentCut, Color(0xFFEAB7A6)),
    Area("Duck universe", "Characters, canon & new possibilities", Icons.Outlined.AutoStories, Mint),
    Area("Project shelves", "A place for everything underway", Icons.Outlined.Inventory2, Color(0xFFB8C9E8)),
    Area("Emporium", "Find, repair, restore & rehome", Icons.Outlined.Handyman, Brass),
    Area("Computer station", "Creative code & technical projects", Icons.Outlined.Terminal, Mint),
    Area("Ideas board", "Catch a thought before it flies", Icons.Outlined.Lightbulb, Color(0xFFEACBA3)),
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun StudioApp(captions: CaptionLensViewModel = viewModel(), store: StudioStore = viewModel(), glasses: GlassesDisplayController) {
    val state by captions.uiState.collectAsStateWithLifecycle()
    val projects by store.projects.collectAsStateWithLifecycle()
    val overlayActive by SpeechOverlayService.active.collectAsStateWithLifecycle()
    var captionPage by rememberSaveable { mutableStateOf(false) }
    var selected by rememberSaveable { mutableStateOf<Int?>(null) }
    var editing by remember { mutableStateOf<StudioProject?>(null) }
    var overlayInfo by remember { mutableStateOf(false) }
    var glassesPage by rememberSaveable { mutableStateOf(false) }
    var message by remember { mutableStateOf<String?>(null) }
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val mic = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission(), captions::onMicPermissionResult)
    val startOverlay: () -> Unit = {
        scope.launch {
            captions.stopListening().join()
            try { ContextCompat.startForegroundService(context, Intent(context, SpeechOverlayService::class.java)) }
            catch (error: Exception) { message = "Could not open floating captions: ${error.message}" }
        }
    }
    val overlayPermissions = rememberLauncherForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { grants ->
        if (grants[Manifest.permission.RECORD_AUDIO] == true) startOverlay()
        else message = "Microphone permission is needed for live captions. Nothing has started."
    }
    if (!captionPage) LaunchedEffect(state.needsMicPermission) {
        if (state.needsMicPermission) mic.launch(Manifest.permission.RECORD_AUDIO)
    }
    BackHandler(glassesPage || captionPage || selected != null) { if (glassesPage) glassesPage = false else if (captionPage) captionPage = false else selected = null }

    if (glassesPage) {
        GlassesControls(glasses, captions, overlayActive) { glassesPage = false }
    } else if (captionPage && !overlayActive) {
        Box(Modifier.fillMaxSize()) {
            CaptionLensScreen(captions)
            FilledTonalButton(onClick = { captionPage = false }, modifier = Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(top = 8.dp)) {
                Icon(Icons.Outlined.Home, null, Modifier.size(18.dp)); Spacer(Modifier.width(6.dp)); Text("Studio")
            }
        }
    } else {
        Scaffold(containerColor = Deep, bottomBar = {
            Column(Modifier.background(Deep).navigationBarsPadding()) {
                if (state.listening == ListeningState.Listening) {
                    Column(Modifier.fillMaxWidth().background(Ink).padding(horizontal = 20.dp, vertical = 10.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text("● LIVE · ON THIS DEVICE", color = Mint, fontSize = 11.sp, modifier = Modifier.weight(1f))
                            TextButton(onClick = { captions.stopListening() }) { Text("Stop microphone", color = Paper) }
                        }
                        val line = state.currentLine
                        Text(line?.let { (it.speakerLabel ?: "Speaker unknown") + if (it.speakerUncertain && it.speakerLabel != null) " ?" else "" }
                            ?: "Waiting for speech", color = Brass, fontSize = 12.sp)
                        Text(line?.text ?: "Your words will appear here…", color = Paper,
                            fontSize = (18 * state.textScale.multiplier).sp, maxLines = 3, overflow = TextOverflow.Ellipsis)
                    }
                }
                Row(Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 8.dp), horizontalArrangement = Arrangement.SpaceEvenly) {
                    TextButton(onClick = { selected = null }) { Icon(Icons.Outlined.Dashboard, null); Spacer(Modifier.width(6.dp)); Text("Studio") }
                    TextButton(onClick = { if (overlayActive) message = "Stop floating captions first to use the in-app caption controls." else captionPage = true }) {
                        Icon(Icons.Outlined.Subtitles, null); Spacer(Modifier.width(6.dp)); Text("Captions")
                    }
                    TextButton(onClick = {
                        if (overlayActive) context.stopService(Intent(context, SpeechOverlayService::class.java)) else overlayInfo = true
                    }) { Icon(Icons.Outlined.PictureInPictureAlt, null); Spacer(Modifier.width(6.dp)); Text(if (overlayActive) "Stop floating" else "Float") }
                }
            }
        }) { padding ->
            LazyVerticalGrid(
                columns = GridCells.Adaptive(270.dp),
                contentPadding = PaddingValues(20.dp),
                horizontalArrangement = Arrangement.spacedBy(14.dp), verticalArrangement = Arrangement.spacedBy(14.dp),
                modifier = Modifier.fillMaxSize().padding(padding)
                    .background(Brush.verticalGradient(listOf(Color(0xFF123B3B), Deep))),
            ) {
                item(span = { GridItemSpan(maxLineSpan) }) {
                    Row(Modifier.fillMaxWidth().padding(bottom = 16.dp), verticalAlignment = Alignment.CenterVertically) {
                        DuckSeal(); Spacer(Modifier.width(12.dp))
                        Column(Modifier.weight(1f)) {
                            Text("CHIPOS", letterSpacing = 3.sp, color = Paper, fontWeight = FontWeight.Bold, fontSize = 19.sp)
                            Text("THE CREATIVE STUDIO", letterSpacing = 1.5.sp, fontSize = 9.sp, color = Mint)
                        }
                        Surface(color = Color(0xFF214440), shape = CircleShape) {
                            Text("●  Local & private", Modifier.padding(horizontal = 12.dp, vertical = 8.dp), color = Mint, fontSize = 11.sp)
                        }
                    }
                }
                item(span = { GridItemSpan(maxLineSpan) }) {
                    if (selected == null) {
                        Column(Modifier.fillMaxWidth().padding(bottom = 10.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                            Text("A room for your\nnext good idea.", color = Paper, fontFamily = FontFamily.Serif,
                                fontSize = 38.sp, lineHeight = 42.sp)
                            Text("Write a chapter. Make something wonderful.\nKeep every thread within reach.", color = Muted, fontSize = 15.sp, lineHeight = 22.sp)
                            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                                Button(onClick = { editing = StudioProject(title = "", area = 6, provenance = "Suggestion") }, colors = ButtonDefaults.buttonColors(containerColor = Brass, contentColor = Deep)) {
                                    Icon(Icons.Outlined.Add, null, Modifier.size(18.dp)); Text(" Capture an idea")
                                }
                                OutlinedButton(onClick = { glassesPage = true }) { Text("Glasses remote", color = Paper) }
                            }
                        }
                    } else {
                        val area = areas[selected!!]
                        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                            TextButton(onClick = { selected = null }, contentPadding = PaddingValues(0.dp)) { Text("← All studio areas", color = Mint) }
                            Text(area.title, color = Paper, fontFamily = FontFamily.Serif, fontSize = 34.sp)
                            Text(area.subtitle, color = Muted)
                            Button(onClick = { editing = StudioProject(title = "", area = selected!!) }, colors = ButtonDefaults.buttonColors(containerColor = Brass, contentColor = Deep)) { Text("+ Add a project") }
                        }
                    }
                }
                if (selected == null) {
                    item(span = { GridItemSpan(maxLineSpan) }) {
                        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                            Text("YOUR STUDIO", color = Mint, fontSize = 11.sp, letterSpacing = 2.sp, modifier = Modifier.weight(1f))
                            Text("${projects.size} saved projects", color = Muted, fontSize = 12.sp)
                        }
                    }
                    items(areas.size) { index -> AreaCard(index, projects.count { it.area == index }) { selected = index } }
                    item { SpeechCard(state, overlayActive, onStart = { captions.startListening() }, onOpen = { captionPage = true }) }
                }
                val visibleProjects = projects.filter { selected == null || it.area == selected }
                item(span = { GridItemSpan(maxLineSpan) }) {
                    Text(if (selected == null) "ON YOUR WORKBENCH" else "PROJECTS & NOTES", color = Mint, fontSize = 11.sp, letterSpacing = 2.sp, modifier = Modifier.padding(top = 14.dp))
                }
                if (visibleProjects.isEmpty()) item(span = { GridItemSpan(maxLineSpan) }) {
                    Surface(color = Ink, shape = RoundedCornerShape(20.dp)) {
                        Column(Modifier.padding(22.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Text("Make this space yours.", color = Paper, fontFamily = FontFamily.Serif, fontSize = 25.sp)
                            Text("Your project details are unknown until you add them. Notes stay on this device; you can edit or remove them at any time.", color = Muted, lineHeight = 21.sp)
                            TextButton(onClick = { editing = StudioProject(title = "", area = selected ?: 0) }) { Text("Add your first project  →", color = Brass) }
                        }
                    }
                }
                items(visibleProjects, key = { it.id }) { project -> ProjectCard(project) { editing = project } }
                item(span = { GridItemSpan(maxLineSpan) }) {
                    Text("A writing sanctuary. A maker’s workshop. Your own small universe.\nView-fixed phone / glasses display · no room scan in this APK", color = Muted.copy(alpha = .8f), fontSize = 11.sp, lineHeight = 18.sp, modifier = Modifier.padding(vertical = 16.dp))
                }
            }
        }
    }
    editing?.let { project ->
        ProjectEditor(project, onClose = { editing = null }, onSave = {
            try { store.save(it); editing = null } catch (error: Exception) { message = error.message }
        }, onDelete = { store.delete(project.id); editing = null })
    }
    if (overlayInfo) AlertDialog(onDismissRequest = { overlayInfo = false }, title = { Text("Captions that stay with you") }, text = {
        Text("Float local speech captions and anonymous speaker labels over other apps. Android requires ‘Display over other apps’ and microphone access. A microphone notification and Stop button remain visible. Audio is processed on this phone; no audio or transcript is saved. Labels need about 12 seconds and can be uncertain.\n\nIf Android opens overlay settings, allow ChipOS, return here, then tap Float again.")
    }, confirmButton = { TextButton(onClick = {
        overlayInfo = false
        if (!Settings.canDrawOverlays(context)) context.startActivity(Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION, Uri.parse("package:${context.packageName}")))
        else {
            val needed = mutableListOf(Manifest.permission.RECORD_AUDIO)
            if (Build.VERSION.SDK_INT >= 33) needed += Manifest.permission.POST_NOTIFICATIONS
            if (needed.any { ContextCompat.checkSelfPermission(context, it) != PackageManager.PERMISSION_GRANTED }) overlayPermissions.launch(needed.toTypedArray())
            else startOverlay()
        }
    }) { Text("Enable floating captions") } }, dismissButton = { TextButton(onClick = { overlayInfo = false }) { Text("Cancel") } })
    message?.let { text -> AlertDialog(onDismissRequest = { message = null }, title = { Text("ChipOS") }, text = { Text(text) }, confirmButton = { TextButton(onClick = { message = null }) { Text("OK") } }) }
}

@Composable
private fun DuckSeal() {
    Canvas(Modifier.size(48.dp).background(Brass.copy(alpha = .12f), CircleShape).border(1.dp, Brass.copy(alpha = .4f), CircleShape)) {
        val w = size.width
        drawOval(Brass, Offset(w * .20f, w * .48f), Size(w * .54f, w * .28f))
        drawCircle(Brass, w * .14f, Offset(w * .62f, w * .36f))
        drawPath(Path().apply { moveTo(w * .74f, w * .32f); lineTo(w * .90f, w * .40f); lineTo(w * .74f, w * .44f); close() }, Brass)
        drawCircle(Deep, w * .022f, Offset(w * .66f, w * .33f))
        drawLine(Mint, Offset(w * .18f, w * .82f), Offset(w * .78f, w * .82f), w * .02f)
    }
}

@Composable
private fun AreaCard(index: Int, count: Int, onClick: () -> Unit) {
    val area = areas[index]
    Card(onClick = onClick, colors = CardDefaults.cardColors(containerColor = Ink), shape = RoundedCornerShape(22.dp),
        modifier = Modifier.fillMaxWidth().heightIn(min = 172.dp).border(1.dp, area.tint.copy(alpha = .15f), RoundedCornerShape(22.dp))) {
        Column(Modifier.padding(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Icon(area.icon, null, tint = area.tint, modifier = Modifier.size(29.dp))
                Spacer(Modifier.weight(1f)); Text(if (count == 0) "READY FOR YOU" else "$count PROJECT${if (count == 1) "" else "S"}", color = Muted, fontSize = 9.sp, letterSpacing = 1.sp)
            }
            Text(area.title, color = Paper, fontFamily = FontFamily.Serif, fontSize = 24.sp)
            Text(area.subtitle, color = Muted, fontSize = 13.sp)
        }
    }
}

@Composable
private fun SpeechCard(state: CaptionUiState, floating: Boolean, onStart: () -> Unit, onOpen: () -> Unit) {
    Card(colors = CardDefaults.cardColors(containerColor = Color(0xFF24443B)), shape = RoundedCornerShape(22.dp)) {
        Column(Modifier.padding(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Icon(Icons.Outlined.GraphicEq, null, tint = Mint, modifier = Modifier.size(29.dp))
            Text("Let the words come to you.", color = Paper, fontFamily = FontFamily.Serif, fontSize = 24.sp)
            Text("Offline captions + acoustic speaker labels. Microphone starts only when you choose.", color = Muted, fontSize = 13.sp)
            if (state.speechError != null) Text(state.speechError, color = Brass, fontSize = 12.sp)
            Row {
                TextButton(onClick = onStart, enabled = !floating && state.listening != ListeningState.Listening) { Text("Start listening", color = if (floating) Muted else Mint) }
                TextButton(onClick = onOpen, enabled = !floating) { Text("Controls", color = Paper) }
            }
        }
    }
}

@Composable
private fun ProjectCard(project: StudioProject, onClick: () -> Unit) {
    Card(onClick = onClick, colors = CardDefaults.cardColors(containerColor = Color(0xFF143739)), shape = RoundedCornerShape(20.dp)) {
        Column(Modifier.padding(20.dp), verticalArrangement = Arrangement.spacedBy(9.dp)) {
            Text("${areas[project.area].title.uppercase()} · ${project.status}", color = Mint, fontSize = 10.sp, letterSpacing = 1.sp)
            Text(project.title, color = Paper, fontFamily = FontFamily.Serif, fontSize = 24.sp)
            Text("NEXT  ${project.next.ifBlank { "Unknown — add a next step" }}", color = Muted, fontSize = 13.sp, maxLines = 3, overflow = TextOverflow.Ellipsis)
            Text(project.provenance, color = if (project.provenance == "Suggestion") Brass else Mint, fontSize = 11.sp)
            Text("Open & edit  →", color = Brass, fontSize = 12.sp)
        }
    }
}

@Composable
private fun ProjectEditor(project: StudioProject, onClose: () -> Unit, onSave: (StudioProject) -> Unit, onDelete: () -> Unit) {
    var title by remember(project.id) { mutableStateOf(project.title) }
    var next by remember(project.id) { mutableStateOf(project.next) }
    var notes by remember(project.id) { mutableStateOf(project.notes) }
    var area by remember(project.id) { mutableIntStateOf(project.area) }
    var status by remember(project.id) { mutableStateOf(project.status) }
    var provenance by remember(project.id) { mutableStateOf(project.provenance) }
    AlertDialog(onDismissRequest = onClose, title = { Text(if (project.title.isBlank()) "Make room for a project" else "Your project") }, text = {
        Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            OutlinedTextField(title, { title = it }, label = { Text("Project or idea") }, modifier = Modifier.fillMaxWidth())
            Text("File it in", color = Mint)
            Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                areas.forEachIndexed { index, item -> FilterChip(area == index, { area = index }, label = { Text(item.title) }) }
            }
            OutlinedTextField(next, { next = it }, label = { Text("Next action") }, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(notes, { notes = it }, label = { Text("Notes, scenes, materials or references") }, minLines = 4, modifier = Modifier.fillMaxWidth())
            Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                listOf("In progress", "Planned", "Parked", "Complete", "Unknown").forEach { FilterChip(status == it, { status = it }, label = { Text(it) }) }
            }
            Text("Keep facts and possibilities clear", color = Mint)
            Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                listOf("Confirmed by me", "Suggestion", "Unknown").forEach { FilterChip(provenance == it, { provenance = it }, label = { Text(it) }) }
            }
            Text("Saved privately on this device. No sharing or cloud sync.", fontSize = 12.sp, color = Muted)
            if (project.title.isNotBlank()) TextButton(onClick = onDelete) { Text("Delete this project", color = Color(0xFFFFB4A8)) }
        }
    }, confirmButton = { TextButton(onClick = { onSave(project.copy(title = title.trim(), area = area, next = next, notes = notes, status = status, provenance = provenance)) }, enabled = title.isNotBlank()) { Text("Save project") } },
        dismissButton = { TextButton(onClick = onClose) { Text("Close") } })
}
