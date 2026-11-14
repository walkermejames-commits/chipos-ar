package com.chipos.ar.studio

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import org.json.JSONArray
import org.json.JSONObject
import java.util.UUID

data class StudioProject(
    val id: String = UUID.randomUUID().toString(),
    val title: String,
    val area: Int = 0,
    val status: String = "In progress",
    val next: String = "",
    val notes: String = "",
    val provenance: String = "Confirmed by me",
)

class StudioStore(app: Application) : AndroidViewModel(app) {
    private val prefs = app.getSharedPreferences("chipos_studio", 0)
    private val mutableProjects = MutableStateFlow(read())
    val projects = mutableProjects.asStateFlow()
    private fun read(): List<StudioProject> = runCatching {
        val rows = JSONArray(prefs.getString("projects", "[]"))
        (0 until rows.length()).map { index ->
            val row = rows.getJSONObject(index)
            StudioProject(row.getString("id"), row.getString("title"), row.optInt("area").coerceIn(0, 6),
                row.optString("status", "Unknown"), row.optString("next"), row.optString("notes"),
                row.optString("provenance", "Unknown"))
        }
    }.getOrDefault(emptyList())

    fun save(project: StudioProject) {
        if (project.title.isBlank()) return
        val entries = mutableProjects.value.toMutableList()
        val index = entries.indexOfFirst { it.id == project.id }
        if (index < 0) entries.add(project) else entries[index] = project
        persist(entries)
    }

    fun delete(id: String) = persist(mutableProjects.value.filterNot { it.id == id })

    private fun persist(entries: List<StudioProject>) {
        val rows = JSONArray()
        entries.forEach { project ->
            rows.put(JSONObject().put("id", project.id).put("title", project.title).put("area", project.area)
                .put("status", project.status).put("next", project.next).put("notes", project.notes)
                .put("provenance", project.provenance))
        }
        // Commit before emitting so a saved card survives immediate process exit.
        check(prefs.edit().putString("projects", rows.toString()).commit()) { "Could not save this project locally" }
        mutableProjects.value = entries
    }
}
