package com.chipos.ar

import android.app.Application
import android.content.Intent
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import androidx.test.uiautomator.By
import androidx.test.uiautomator.UiDevice
import androidx.test.uiautomator.Until
import com.chipos.ar.overlay.SpeechOverlayService
import com.chipos.ar.studio.StudioStore
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class StudioFlowTest {
    private val instrumentation = InstrumentationRegistry.getInstrumentation()
    private val context = instrumentation.targetContext
    private val device = UiDevice.getInstance(instrumentation)

    private fun openStudio() {
        context.startActivity(Intent(context, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK))
        assertNotNull(device.wait(Until.findObject(By.text("CHIPOS")), 15000))
        device.waitForIdle()
    }

    @Test fun projectCanBeCreatedThroughUiAndReloadedFromPrivateStorage() {
        openStudio()
        device.findObject(By.textContains("Capture an idea")).click()
        val title = device.wait(Until.findObject(By.clazz("android.widget.EditText")), 5000)
        assertNotNull(title)
        title.text = "Emulator verification project"
        device.waitForIdle()
        device.findObject(By.text("Save project")).click()
        assertTrue(device.wait(Until.gone(By.text("Make room for a project")), 5000))
        val store = StudioStore(context.applicationContext as Application)
        val project = store.projects.value.single { it.title == "Emulator verification project" }
        assertEquals(6, project.area)
        assertEquals("Suggestion", project.provenance)
        store.save(project.copy(next = "Verify persistence"))
        assertEquals("Verify persistence", StudioStore(context.applicationContext as Application).projects.value.single { it.id == project.id }.next)
        store.delete(project.id)
        assertFalse(StudioStore(context.applicationContext as Application).projects.value.any { it.id == project.id })
    }

    @Test fun floatingMicrophoneOverlaySurvivesHomeAndStopsFromItsButton() {
        device.executeShellCommand("pm grant ${context.packageName} android.permission.RECORD_AUDIO")
        device.executeShellCommand("pm grant ${context.packageName} android.permission.POST_NOTIFICATIONS")
        device.executeShellCommand("appops set ${context.packageName} SYSTEM_ALERT_WINDOW allow")
        openStudio()
        device.findObject(By.text("Float")).click()
        val enable = device.wait(Until.findObject(By.text("Enable floating captions")), 5000)
        assertNotNull(enable)
        enable.click()
        assertNotNull(device.wait(Until.findObject(By.textContains("MIC ON · LOCAL SPEECH")), 20000))
        assertTrue(SpeechOverlayService.active.value)
        device.pressHome()
        assertNotNull(device.wait(Until.findObject(By.desc("Stop microphone and close overlay")), 5000))
        assertTrue(SpeechOverlayService.active.value)
        device.findObject(By.desc("Stop microphone and close overlay")).click()
        assertTrue(device.wait(Until.gone(By.desc("Stop microphone and close overlay")), 5000))
        val deadline = System.currentTimeMillis() + 5000
        while (SpeechOverlayService.active.value && System.currentTimeMillis() < deadline) Thread.sleep(50)
        assertFalse(SpeechOverlayService.active.value)
    }
}
