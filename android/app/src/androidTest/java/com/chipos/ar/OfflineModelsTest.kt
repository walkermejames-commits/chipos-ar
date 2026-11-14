package com.chipos.ar

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.k2fsa.sherpa.onnx.*
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import java.nio.ByteBuffer
import java.nio.ByteOrder

/** Runs the APK's actual native models on the upstream two-speaker English fixture. */
@RunWith(AndroidJUnit4::class)
class OfflineModelsTest {
    @Test fun bundledModelsRecognizeSpeechAndSeparateTwoFixtureVoices() {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val assets = instrumentation.targetContext.assets
        val bytes = instrumentation.context.assets.open("two-speakers-en.wav").use { it.readBytes() }
        val buffer = ByteBuffer.wrap(bytes).order(ByteOrder.LITTLE_ENDIAN)
        assertEquals("RIFF", String(bytes.copyOfRange(0, 4)))
        var offset = 12
        var pcm: FloatArray? = null
        while (offset + 8 <= bytes.size) {
            val size = buffer.getInt(offset + 4)
            when (String(bytes.copyOfRange(offset, offset + 4))) {
                "fmt " -> {
                    assertEquals(1, buffer.getShort(offset + 8).toInt())
                    assertEquals(1, buffer.getShort(offset + 10).toInt())
                    assertEquals(16000, buffer.getInt(offset + 12))
                    assertEquals(16, buffer.getShort(offset + 22).toInt())
                }
                "data" -> pcm = FloatArray(size / 2) { buffer.getShort(offset + 8 + it * 2) / 32768f }
            }
            offset += 8 + size + size % 2
        }
        val samples = requireNotNull(pcm)
        val recognizer = OnlineRecognizer(assets, OnlineRecognizerConfig(
            featConfig = FeatureConfig(sampleRate = 16000, featureDim = 80),
            modelConfig = OnlineModelConfig(transducer = OnlineTransducerModelConfig(
                encoder = "models/asr/encoder-epoch-99-avg-1.int8.onnx",
                decoder = "models/asr/decoder-epoch-99-avg-1.onnx",
                joiner = "models/asr/joiner-epoch-99-avg-1.int8.onnx"),
                tokens = "models/asr/tokens.txt", numThreads = 2, provider = "cpu", modelType = "zipformer"),
            enableEndpoint = false))
        val stream = recognizer.createStream()
        try {
            stream.acceptWaveform(samples + FloatArray(16000), 16000)
            stream.inputFinished()
            while (recognizer.isReady(stream)) recognizer.decode(stream)
            val text = recognizer.getResult(stream).text.trim()
            android.util.Log.i("ChipOSModelTest", "ASR fixture result: $text")
            assertTrue("Bundled ASR must recognize multiple words", text.split(" ").size >= 5)
        } finally { stream.release(); recognizer.release() }
        val diarizer = OfflineSpeakerDiarization(assets, OfflineSpeakerDiarizationConfig(
            segmentation = OfflineSpeakerSegmentationModelConfig(
                pyannote = OfflineSpeakerSegmentationPyannoteModelConfig(model = "models/diarization/segmentation.int8.onnx", windowShiftRatio = .1f),
                numThreads = 2, provider = "cpu"),
            embedding = SpeakerEmbeddingExtractorConfig(model = "models/diarization/eres2net-en.onnx", numThreads = 2, provider = "cpu"),
            clustering = FastClusteringConfig(numClusters = -1, threshold = .5f, computeConfidence = true),
            minDurationOn = .35f, minDurationOff = .4f))
        try {
            val segments = diarizer.process(samples)
            android.util.Log.i("ChipOSModelTest", "Diarization segments: " + segments.joinToString { "${it.start}-${it.end}: speaker ${it.speaker}" })
            assertTrue("Must find speech segments", segments.isNotEmpty())
            assertEquals("Upstream fixture contains two voices", 2, segments.map { it.speaker }.distinct().size)
            assertTrue(segments.all { it.start >= 0f && it.end > it.start && it.end <= samples.size / 16000f + .2f })
        } finally { diarizer.release() }
    }
}
