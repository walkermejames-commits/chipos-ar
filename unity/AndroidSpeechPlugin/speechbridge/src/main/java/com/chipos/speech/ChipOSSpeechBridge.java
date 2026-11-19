package com.chipos.speech;

import android.Manifest;
import android.content.Context;
import android.content.pm.PackageManager;
import android.media.AudioDeviceInfo;
import android.media.AudioManager;
import android.media.AudioFormat;
import android.media.AudioRecord;
import android.media.MediaRecorder;
import android.media.audiofx.AutomaticGainControl;
import android.media.audiofx.NoiseSuppressor;
import android.os.Build;
import android.os.SystemClock;

import com.k2fsa.sherpa.onnx.FastClusteringConfig;
import com.k2fsa.sherpa.onnx.FeatureConfig;
import com.k2fsa.sherpa.onnx.OfflineSpeakerDiarization;
import com.k2fsa.sherpa.onnx.OfflineSpeakerDiarizationConfig;
import com.k2fsa.sherpa.onnx.OfflineSpeakerDiarizationSegment;
import com.k2fsa.sherpa.onnx.OfflineSpeakerSegmentationModelConfig;
import com.k2fsa.sherpa.onnx.OfflineSpeakerSegmentationPyannoteModelConfig;
import com.k2fsa.sherpa.onnx.OnlineModelConfig;
import com.k2fsa.sherpa.onnx.OnlineRecognizer;
import com.k2fsa.sherpa.onnx.OnlineRecognizerConfig;
import com.k2fsa.sherpa.onnx.OnlineRecognizerResult;
import com.k2fsa.sherpa.onnx.OnlineStream;
import com.k2fsa.sherpa.onnx.OnlineTransducerModelConfig;
import com.k2fsa.sherpa.onnx.SpeakerEmbeddingExtractor;
import com.k2fsa.sherpa.onnx.SpeakerEmbeddingExtractorConfig;
import org.json.JSONObject;
import org.json.JSONArray;

import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.zip.ZipFile;

/** Offline streaming speech and acoustic speaker diarization bridge for Unity. */
public final class ChipOSSpeechBridge {
    private static Engine engine;
    private static volatile String checkedAvailability = "Microphone off · offline speech has not been checked";
    private static volatile String checkedSpeakerStatus = "Speaker analysis is off until the microphone starts";

    private ChipOSSpeechBridge() { }

    public static synchronized boolean start(Context context) {
        if (engine == null) engine = new Engine(context.getApplicationContext());
        return engine.start();
    }

    /** Checks the native model ABI before asking the user to grant microphone access. */
    public static String getAvailabilityStatus(Context context) {
        if (context == null) {
            checkedAvailability = "Offline speech is unavailable because Android did not provide an app context. No microphone was opened.";
            checkedSpeakerStatus = "Speaker analysis is unavailable because Android did not provide an app context.";
            return checkedAvailability;
        }
        String abi = findPackagedRuntimeAbi(context);
        if (abi == null) {
            String deviceAbi = Build.SUPPORTED_ABIS.length == 0 ? "unknown" : Build.SUPPORTED_ABIS[0];
            checkedAvailability = "Offline speech is unavailable: this APK has no native speech runtime for " + deviceAbi +
                ". No microphone was opened.";
            checkedSpeakerStatus = "Speaker analysis is unavailable because this APK has no native runtime for " + deviceAbi + ".";
            return checkedAvailability;
        }
        String[] requiredAsrAssets = {
            "models/asr/encoder-epoch-99-avg-1.int8.onnx",
            "models/asr/decoder-epoch-99-avg-1.onnx",
            "models/asr/joiner-epoch-99-avg-1.int8.onnx",
            "models/asr/tokens.txt"
        };
        for (String asset : requiredAsrAssets) {
            if (!hasPackagedAsset(context, asset)) {
                checkedAvailability = "Offline speech is unavailable: this APK is missing " + asset + ". No microphone was opened.";
                checkedSpeakerStatus = "Speaker analysis is unavailable because the offline speech model assets are incomplete.";
                return checkedAvailability;
            }
        }
        boolean diarizationAssetsPresent = hasPackagedAsset(context, "models/diarization/segmentation.int8.onnx") &&
            hasPackagedAsset(context, "models/diarization/eres2net-en.onnx");
        checkedAvailability = "Offline English speech is available for this device (" + abi + "). Microphone permission is still required when you start.";
        checkedSpeakerStatus = diarizationAssetsPresent
            ? "Speaker analysis is off until the microphone starts"
            : "Speaker analysis unavailable: this APK is missing the diarization model assets.";
        return checkedAvailability;
    }

    private static boolean hasPackagedAsset(Context context, String path) {
        try (java.io.InputStream stream = context.getAssets().open(path)) {
            return stream.read() >= 0;
        } catch (Exception ignored) {
            return false;
        }
    }

    /** Only report speech as available when the APK actually ships native libraries for this device ABI. */
    private static String findPackagedRuntimeAbi(Context context) {
        boolean x86Emulator = isX86AndroidEmulatorUsingArmTranslation();
        String apkPath = context.getApplicationInfo().sourceDir;
        try (ZipFile apk = new ZipFile(apkPath)) {
            for (String abi : Build.SUPPORTED_ABIS) {
                String prefix = "lib/" + abi + "/";
                if (apk.getEntry(prefix + "libsherpa-onnx-jni.so") != null &&
                    apk.getEntry(prefix + "libonnxruntime.so") != null) {
                    return abi;
                }
                // Do not fall back to ARM translation on the emulator. A native
                // x86 runtime is safe and is packaged in the emulator-test APK.
                if (x86Emulator && abi.startsWith("x86")) return null;
            }
        } catch (Exception error) {
            return null;
        }
        return null;
    }

    /** The x86_64 AVD advertises ARM64 as a secondary ABI, but its native bridge crashes in ONNX Runtime. */
    private static boolean isX86AndroidEmulatorUsingArmTranslation() {
        if (Build.SUPPORTED_ABIS.length == 0 || !Build.SUPPORTED_ABIS[0].startsWith("x86")) return false;
        String fingerprint = Build.FINGERPRINT == null ? "" : Build.FINGERPRINT;
        String hardware = Build.HARDWARE == null ? "" : Build.HARDWARE;
        return "ranchu".equals(hardware) || "goldfish".equals(hardware) ||
            fingerprint.startsWith("google/sdk_gphone") ||
            (Build.MODEL != null && Build.MODEL.toLowerCase(java.util.Locale.ROOT).contains("sdk_gphone"));
    }

    public static synchronized void stop() {
        if (engine != null) engine.stop();
    }

    /** Cycles the preferred input route without opening the microphone. */
    public static synchronized String cycleInputDevice(Context context) {
        if (context == null) return "Android did not provide an app context; microphone route was not changed.";
        if (engine == null) engine = new Engine(context.getApplicationContext());
        return engine.cycleInputDevice();
    }

    /** Cycles a modest on-device gain multiplier for a quiet microphone route. */
    public static synchronized String cycleInputGain(Context context) {
        if (context == null) return "Android did not provide an app context; microphone gain was not changed.";
        if (engine == null) engine = new Engine(context.getApplicationContext());
        return engine.cycleInputGain();
    }

    public static synchronized String getStateJson() {
        if (engine != null) return engine.getStateJson();
        try {
            JSONObject json = new JSONObject();
            json.put("active", false);
            json.put("starting", false);
            json.put("running", false);
            json.put("status", checkedAvailability);
            json.put("speakerStatus", checkedSpeakerStatus);
            return json.toString();
        } catch (Throwable ignored) {
            return "{\"active\":false,\"starting\":false,\"running\":false,\"status\":\"Microphone off\",\"speakerStatus\":\"Speaker analysis is off until the microphone starts\"}";
        }
    }

    private static final class Engine {
        private static final int RATE = 16000;
        private static final int CHUNK = 1600;
        private static final int WINDOW_SECONDS = 12;
        private static final int STEP_SECONDS = 4;
        private static final int WINDOW = RATE * WINDOW_SECONDS;
        private static final int STEP = RATE * STEP_SECONDS;
        private static final int MIN_EMBED = RATE * 13 / 10;
        private static final float MATCH_THRESHOLD = 0.68f;
        private static final float CERTAIN_THRESHOLD = 0.45f;
        private static final long SPEAKER_START_TOLERANCE_MS = 250L;
        private static final long SPEAKER_END_TOLERANCE_MS = 1800L;

        private final Context context;
        private final AudioManager audioManager;
        private final float[] ring = new float[RATE * 20];
        private final Object ringLock = new Object();
        private final Object stateLock = new Object();
        private final ArrayDeque<Caption> recent = new ArrayDeque<>();
        private final ArrayList<TimedSegment> segments = new ArrayList<>();
        private final SpeakerBank speakers = new SpeakerBank();
        private final AtomicBoolean diarizationBusy = new AtomicBoolean();
        private ExecutorService diarizationQueue;

        private volatile String status = "Microphone off";
        private volatile boolean starting;
        private volatile boolean running;
        private volatile boolean stopRequested;
        private volatile long sessionGeneration;
        private volatile float inputLevel;
        private volatile float inputGain = 1.0f;
        private volatile String inputRoute = "Waiting for Android microphone route";
        private volatile String speakerStatus = "Speaker analysis is off until the microphone starts";
        private volatile Caption latest;
        private volatile long sampleCount;
        private long sessionStartMillis;
        private long lastDiarizationSample;
        private AudioRecord recorder;
        private AutomaticGainControl automaticGainControl;
        private NoiseSuppressor noiseSuppressor;
        private volatile int preferredInputDeviceId = -1;
        private OnlineRecognizer recognizer;
        private OfflineSpeakerDiarization diarizer;
        private SpeakerEmbeddingExtractor embeddingExtractor;
        private Thread startupThread;
        private Thread captureThread;

        Engine(Context context) {
            this.context = context;
            this.audioManager = (AudioManager) context.getSystemService(Context.AUDIO_SERVICE);
        }

        synchronized String cycleInputDevice() {
            if (audioManager == null) return "Android audio routing is unavailable; the microphone was not opened.";
            try {
                ArrayList<AudioDeviceInfo> candidates = new ArrayList<>();
                for (AudioDeviceInfo device : audioManager.getDevices(AudioManager.GET_DEVICES_INPUTS))
                    if (device != null && device.isSource()) candidates.add(device);
                if (candidates.isEmpty()) return "Android reports no microphone inputs; the microphone was not opened.";

                int currentId = preferredInputDeviceId;
                if (currentId < 0 && recorder != null && recorder.getRoutedDevice() != null)
                    currentId = recorder.getRoutedDevice().getId();
                int currentIndex = -1;
                for (int i = 0; i < candidates.size(); i++)
                    if (candidates.get(i).getId() == currentId) { currentIndex = i; break; }
                AudioDeviceInfo selected = candidates.get((currentIndex + 1) % candidates.size());
                preferredInputDeviceId = selected.getId();

                AudioRecord currentRecorder = recorder;
                if (currentRecorder != null && !currentRecorder.setPreferredDevice(selected))
                    return "Android could not switch to " + describeInputDevice(selected) + "; keeping the current microphone route.";
                if (currentRecorder != null) updateInputRoute(currentRecorder);
                else inputRoute = describeInputDevice(selected);
                return "Selected microphone input: " + inputRoute;
            } catch (Throwable error) {
                return "Android could not change the microphone route: " + error.getClass().getSimpleName() + ".";
            }
        }

        synchronized String cycleInputGain() {
            inputGain = inputGain < 1.25f ? 1.5f : inputGain < 1.75f ? 2.0f : 1.0f;
            return "Microphone software gain set to " + inputGain + "×. Increase only if the input meter is quiet.";
        }

        synchronized boolean start() {
            if (running || starting) return true;
            String availability = getAvailabilityStatus(context);
            if (!availability.startsWith("Offline speech is available")) {
                status = availability;
                speakerStatus = "Unavailable on this device architecture";
                return false;
            }
            if (context.checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
                status = "Microphone permission is required";
                return false;
            }
            long generation = ++sessionGeneration;
            stopRequested = false;
            starting = true;
            if (diarizationQueue == null || diarizationQueue.isShutdown()) {
                diarizationQueue = Executors.newSingleThreadExecutor(r -> {
                    Thread thread = new Thread(r, "chipos-speaker-analysis");
                    thread.setDaemon(true);
                    return thread;
                });
            }
            status = "Loading the offline English speech model · microphone remains off until it is ready";
            startupThread = new Thread(() -> initialize(generation), "chipos-offline-speech-start");
            startupThread.setDaemon(true);
            startupThread.start();
            return true;
        }

        private boolean isCurrentSession(long generation) {
            return generation == sessionGeneration && !stopRequested;
        }

        private void initialize(long generation) {
            AutomaticGainControl localGainControl = null;
            NoiseSuppressor localNoiseSuppressor = null;
            OnlineRecognizer localRecognizer = null;
            AudioRecord localRecorder = null;
            try {
                OnlineTransducerModelConfig transducer = new OnlineTransducerModelConfig(
                    "models/asr/encoder-epoch-99-avg-1.int8.onnx",
                    "models/asr/decoder-epoch-99-avg-1.onnx",
                    "models/asr/joiner-epoch-99-avg-1.int8.onnx",
                    new com.k2fsa.sherpa.onnx.QnnConfig());
                OnlineModelConfig model = new OnlineModelConfig();
                model.setTransducer(transducer);
                model.setTokens("models/asr/tokens.txt");
                model.setNumThreads(2);
                model.setProvider("cpu");
                model.setModelType("zipformer");
                OnlineRecognizerConfig recognition = new OnlineRecognizerConfig();
                recognition.setFeatConfig(new FeatureConfig(RATE, 80, 0.0f));
                recognition.setModelConfig(model);
                recognition.setEnableEndpoint(true);
                // Use a wider beam than the default for fewer low-confidence word
                // substitutions. This costs some CPU but the S26 has headroom.
                recognition.setDecodingMethod("modified_beam_search");
                recognition.setMaxActivePaths(16);
                localRecognizer = new OnlineRecognizer(context.getAssets(), recognition);

                if (!isCurrentSession(generation)) {
                    localRecognizer.release();
                    return;
                }
                int minBuffer = AudioRecord.getMinBufferSize(RATE, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT);
                if (minBuffer <= 0) throw new IllegalStateException("This device cannot open a 16 kHz microphone.");
                localRecorder = new AudioRecord(MediaRecorder.AudioSource.VOICE_RECOGNITION, RATE,
                    AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT, Math.max(minBuffer * 2, CHUNK * 4));
                if (localRecorder.getState() != AudioRecord.STATE_INITIALIZED) {
                    localRecorder.release();
                    throw new IllegalStateException("Android could not initialize the microphone.");
                }
                if (preferredInputDeviceId >= 0) {
                    AudioDeviceInfo preferred = findInputDevice(preferredInputDeviceId);
                    if (preferred != null && !localRecorder.setPreferredDevice(preferred))
                        inputRoute = "Android could not select the requested input; using its default microphone";
                }
                localRecorder.startRecording();
                if (localRecorder.getRecordingState() != AudioRecord.RECORDSTATE_RECORDING) {
                    localRecorder.release();
                    throw new IllegalStateException("Android did not start microphone capture.");
                }
                if (AutomaticGainControl.isAvailable()) {
                    try {
                        localGainControl = AutomaticGainControl.create(localRecorder.getAudioSessionId());
                        if (localGainControl != null) localGainControl.setEnabled(true);
                    } catch (Throwable ignored) {
                        localGainControl = null;
                    }
                }
                if (NoiseSuppressor.isAvailable()) {
                    try {
                        localNoiseSuppressor = NoiseSuppressor.create(localRecorder.getAudioSessionId());
                        if (localNoiseSuppressor != null) localNoiseSuppressor.setEnabled(true);
                    } catch (Throwable ignored) {
                        localNoiseSuppressor = null;
                    }
                }
                synchronized (this) {
                    if (!isCurrentSession(generation)) {
                        localRecorder.stop(); localRecorder.release();
                        if (localGainControl != null) localGainControl.release();
                        if (localNoiseSuppressor != null) localNoiseSuppressor.release();
                        localRecognizer.release();
                        return;
                    }
                    recorder = localRecorder;
                    recognizer = localRecognizer;
                    diarizer = null;
                    embeddingExtractor = null;
                    automaticGainControl = localGainControl;
                    noiseSuppressor = localNoiseSuppressor;
                    sessionStartMillis = SystemClock.elapsedRealtime();
                    sampleCount = 0;
                    lastDiarizationSample = 0;
                    speakers.reset();
                    synchronized (ringLock) { for (int i = 0; i < ring.length; i++) ring[i] = 0f; }
                    synchronized (stateLock) { recent.clear(); segments.clear(); latest = null; }
                    running = true;
                    starting = false;
                    updateInputRoute(localRecorder);
                    speakerStatus = "Loading offline acoustic speaker models; speaker labels remain unknown until analysis completes";
                    status = "Microphone on · offline English speech is active · audio stays on this phone · speaker analysis is loading · input: " + inputRoute;
                    AudioRecord captureRecorder = localRecorder;
                    OnlineRecognizer captureRecognizer = localRecognizer;
                    captureThread = new Thread(() -> capture(generation, captureRecorder, captureRecognizer), "chipos-live-microphone");
                    captureThread.setDaemon(true);
                    captureThread.start();
                    localRecorder = null;
                    localRecognizer = null;
                    localGainControl = null;
                    localNoiseSuppressor = null;
                }

                // Start captions as soon as the ASR model and microphone are ready.
                // Speaker models load separately so a missing or slow diarization model
                // cannot prevent live captions from working.
                OfflineSpeakerDiarization localDiarizer = null;
                SpeakerEmbeddingExtractor localExtractor = null;
                try {
                    SpeakerEmbeddingExtractorConfig embedding = new SpeakerEmbeddingExtractorConfig(
                        "models/diarization/eres2net-en.onnx", 2, false, "cpu");
                    OfflineSpeakerSegmentationPyannoteModelConfig pyannote =
                        new OfflineSpeakerSegmentationPyannoteModelConfig("models/diarization/segmentation.int8.onnx", 0.1f);
                    OfflineSpeakerSegmentationModelConfig segmentation = new OfflineSpeakerSegmentationModelConfig();
                    segmentation.setPyannote(pyannote);
                    segmentation.setNumThreads(2);
                    segmentation.setProvider("cpu");
                    FastClusteringConfig clustering = new FastClusteringConfig(-1, 0.5f, true);
                    OfflineSpeakerDiarizationConfig diarization = new OfflineSpeakerDiarizationConfig();
                    diarization.setSegmentation(segmentation);
                    diarization.setEmbedding(embedding);
                    diarization.setClustering(clustering);
                    diarization.setMinDurationOn(0.35f);
                    diarization.setMinDurationOff(0.4f);
                    localDiarizer = new OfflineSpeakerDiarization(context.getAssets(), diarization);
                    localExtractor = new SpeakerEmbeddingExtractor(context.getAssets(), embedding);
                    synchronized (this) {
                        if (!isCurrentSession(generation)) {
                            localExtractor.release();
                            localDiarizer.release();
                            return;
                        }
                        diarizer = localDiarizer;
                        embeddingExtractor = localExtractor;
                        localDiarizer = null;
                        localExtractor = null;
                    speakerStatus = "Acoustic speaker analysis loaded · waiting for a 12-second audio window; labels stay unknown until a voice segment is clear enough";
                        status = "Listening · offline English speech and acoustic speaker analysis · audio stays on this phone · input: " + inputRoute;
                    }
                } catch (Throwable speakerError) {
                    if (localExtractor != null) localExtractor.release();
                    if (localDiarizer != null) localDiarizer.release();
                    speakerStatus = "Acoustic diarization unavailable: " + speakerError.getClass().getSimpleName() + ": " + speakerError.getMessage();
                    status = "Captions continue; speaker analysis unavailable: " + speakerError.getMessage();
                }
            } catch (Throwable error) {
                if (localRecorder != null) {
                    try { localRecorder.stop(); } catch (Throwable ignored) { }
                    try { localRecorder.release(); } catch (Throwable ignored) { }
                }
                if (localGainControl != null) {
                    try { localGainControl.release(); } catch (Throwable ignored) { }
                }
                if (localNoiseSuppressor != null) {
                    try { localNoiseSuppressor.release(); } catch (Throwable ignored) { }
                }
                if (localRecognizer != null) {
                    try { localRecognizer.release(); } catch (Throwable ignored) { }
                }
                if (isCurrentSession(generation)) {
                    speakerStatus = "Speaker models could not start: " + error.getClass().getSimpleName() + ": " + error.getMessage();
                    status = "Speech could not start: " + error.getMessage();
                    releaseModels();
                }
            } finally {
                if (generation == sessionGeneration) starting = false;
            }
        }

        private void capture(long generation, AudioRecord captureRecorder, OnlineRecognizer captureRecognizer) {
            short[] pcm = new short[CHUNK];
            long utteranceStart = 0;
            String lastText = "";
            int routeRefreshChunks = 0;
            OnlineStream captureStream = null;
            try {
                captureStream = captureRecognizer.createStream("");
                while (running && isCurrentSession(generation)) {
                    if (routeRefreshChunks++ % 25 == 0) updateInputRoute(captureRecorder);
                    int count = captureRecorder.read(pcm, 0, pcm.length, AudioRecord.READ_BLOCKING);
                    if (count < 0) throw new IllegalStateException("Microphone read failed: " + count);
                    if (count == 0) continue;
                    if (!isCurrentSession(generation)) break;
                    float[] floats = new float[count];
                    double square = 0;
                    float gain = inputGain;
                    for (int i = 0; i < count; i++) {
                        floats[i] = Math.max(-1.0f, Math.min(1.0f, pcm[i] / 32768.0f * gain));
                        square += floats[i] * floats[i];
                    }
                    inputLevel = (float)Math.sqrt(square / count);
                    append(floats);
                    long total = sampleCount + count;
                    sampleCount = total;
                    captureStream.acceptWaveform(floats, RATE);
                    while (captureRecognizer.isReady(captureStream)) captureRecognizer.decode(captureStream);
                    boolean endpoint = captureRecognizer.isEndpoint(captureStream);
                    OnlineRecognizerResult result = captureRecognizer.getResult(captureStream);
                    String text = result.getText() == null ? "" : result.getText().trim();
                    if (!text.isEmpty() && (!text.equals(lastText) || endpoint)) {
                        long now = sessionStartMillis + total * 1000L / RATE;
                        if (utteranceStart == 0) utteranceStart = now;
                        Caption caption = new Caption(text, now, utteranceStart, endpoint);
                        synchronized (stateLock) {
                            latest = caption;
                            if (endpoint) {
                                recent.addLast(caption);
                                while (recent.size() > 32) recent.removeFirst();
                            }
                        }
                        applySpeaker(caption);
                        lastText = text;
                    }
                    if (endpoint) {
                        captureRecognizer.reset(captureStream);
                        lastText = "";
                        utteranceStart = 0;
                    }
                    if (diarizer != null && embeddingExtractor != null && total >= WINDOW && total - lastDiarizationSample >= STEP) {
                        lastDiarizationSample = total;
                        queueDiarization(total, generation);
                    }
                }
            } catch (Throwable error) {
                if (isCurrentSession(generation)) status = "Speech stopped: " + error.getMessage();
            } finally {
                if (captureStream != null) captureStream.release();
                if (generation == sessionGeneration) running = false;
            }
        }

        private void append(float[] values) {
            synchronized (ringLock) {
                long start = sampleCount;
                for (int i = 0; i < values.length; i++) ring[(int)((start + i) % ring.length)] = values[i];
            }
        }

        private float[] snapshot(int count, long end) {
            synchronized (ringLock) {
                if (end < count) return null;
                float[] copy = new float[count];
                long start = end - count;
                for (int i = 0; i < count; i++) copy[i] = ring[(int)((start + i) % ring.length)];
                return copy;
            }
        }

        private void queueDiarization(long endSample, long generation) {
            if (!isCurrentSession(generation)) return;
            if (diarizer == null || embeddingExtractor == null || !diarizationBusy.compareAndSet(false, true)) return;
            float[] audio = snapshot(WINDOW, endSample);
            if (audio == null) { diarizationBusy.set(false); return; }
            long startSample = endSample - audio.length;
            speakerStatus = "Analyzing a " + WINDOW_SECONDS + "-second audio window for speaker turns…";
            try {
                diarizationQueue.execute(() -> {
                    try { if (isCurrentSession(generation)) analyze(audio, startSample); }
                    finally { diarizationBusy.set(false); }
                });
            } catch (java.util.concurrent.RejectedExecutionException ignored) {
                diarizationBusy.set(false);
            }
        }

        private void analyze(float[] audio, long startSample) {
            try {
                OfflineSpeakerDiarizationSegment[] detected = diarizer.process(audio);
                if (detected == null || detected.length == 0) {
                    speakerStatus = "No voice turn detected in the latest window; speaker remains unknown";
                    return;
                }
                Map<Integer, List<WeightedVector>> local = new LinkedHashMap<>();
                for (OfflineSpeakerDiarizationSegment segment : detected) {
                    int from = Math.max(0, Math.min(audio.length, (int)(segment.getStart() * RATE)));
                    int to = Math.max(from, Math.min(audio.length, (int)(segment.getEnd() * RATE)));
                    if (to - from < MIN_EMBED) continue;
                    float[] vector = embed(audio, from, to);
                    if (vector == null) continue;
                    local.computeIfAbsent(segment.getSpeaker(), key -> new ArrayList<>())
                        .add(new WeightedVector(vector, to - from));
                }
                Map<Integer, float[]> averages = new LinkedHashMap<>();
                for (Map.Entry<Integer, List<WeightedVector>> item : local.entrySet())
                    averages.put(item.getKey(), average(item.getValue()));
                if (averages.isEmpty()) {
                    speakerStatus = "Voice detected, but no long or clear enough segment for a speaker label";
                    return;
                }
                Map<Integer, Integer> assigned = speakers.assign(averages);
                ArrayList<TimedSegment> updated = new ArrayList<>();
                for (OfflineSpeakerDiarizationSegment segment : detected) {
                    Integer id = assigned.get(segment.getSpeaker());
                    if (id == null) continue;
                    long start = sessionStartMillis + (startSample + (long)(segment.getStart() * RATE)) * 1000L / RATE;
                    long end = sessionStartMillis + (startSample + (long)(segment.getEnd() * RATE)) * 1000L / RATE;
                    updated.add(new TimedSegment(start, end, id, segment.getConfidence()));
                }
                synchronized (stateLock) {
                    long recentStart = sessionStartMillis + startSample * 1000L / RATE;
                    segments.removeIf(item -> item.end >= recentStart);
                    segments.addAll(updated);
                    segments.sort((a, b) -> Long.compare(a.start, b.start));
                    for (Caption caption : recent) applySpeakerLocked(caption);
                    if (latest != null) applySpeakerLocked(latest);
                    int speakerCount = speakers.size();
                    speakerStatus = "Acoustic diarization active · " + speakerCount +
                        (speakerCount == 1 ? " anonymous speaker" : " anonymous speakers") + " · provisional labels";
                }
            } catch (Throwable error) {
                speakerStatus = "Speaker analysis unavailable: " + error.getClass().getSimpleName() + ": " + error.getMessage();
                status = "Captions continue; speaker analysis unavailable: " + error.getMessage();
            }
        }

        private float[] embed(float[] audio, int from, int to) {
            OnlineStream input = embeddingExtractor.createStream();
            try {
                float[] slice = new float[to - from];
                System.arraycopy(audio, from, slice, 0, slice.length);
                input.acceptWaveform(slice, RATE);
                input.inputFinished();
                float[] vector = null;
                while (embeddingExtractor.isReady(input)) vector = embeddingExtractor.compute(input);
                return vector;
            } finally { input.release(); }
        }

        private static float[] average(List<WeightedVector> vectors) {
            if (vectors.isEmpty()) return new float[0];
            float[] result = new float[vectors.get(0).value.length];
            float total = 0;
            for (WeightedVector item : vectors) {
                if (item.value.length != result.length) continue;
                for (int i = 0; i < result.length; i++) result[i] += item.value[i] * item.weight;
                total += item.weight;
            }
            if (total > 0) for (int i = 0; i < result.length; i++) result[i] /= total;
            normalize(result);
            return result;
        }

        private void applySpeaker(Caption caption) {
            synchronized (stateLock) { applySpeakerLocked(caption); }
        }

        private void applySpeakerLocked(Caption caption) {
            TimedSegment best = null;
            // The endpoint time can include silence after a person stops speaking.
            // Match the diarized segment to the complete ASR utterance interval so
            // that a valid speaker turn is not lost just because ASR finalized late.
            long utteranceStart = caption.utteranceStart > 0 ? caption.utteranceStart : caption.at;
            long utteranceEnd = Math.max(utteranceStart, caption.at);
            long bestOverlap = -1;
            for (TimedSegment segment : segments) {
                long expandedStart = segment.start - SPEAKER_START_TOLERANCE_MS;
                long expandedEnd = segment.end + SPEAKER_END_TOLERANCE_MS;
                long overlap = Math.min(utteranceEnd, expandedEnd) - Math.max(utteranceStart, expandedStart);
                if (overlap >= 0 && (best == null || overlap > bestOverlap ||
                    (overlap == bestOverlap && segment.confidence > best.confidence))) {
                    best = segment;
                    bestOverlap = overlap;
                }
            }
            if (best != null) {
                caption.speaker = speakers.label(best.id);
                caption.speakerConfidence = best.confidence;
                caption.uncertain = best.confidence < CERTAIN_THRESHOLD;
            }
        }

        synchronized void stop() {
            sessionGeneration++;
            stopRequested = true;
            running = false;
            starting = false;
            AudioRecord current = recorder;
            if (current != null) {
                try { current.stop(); } catch (IllegalStateException ignored) { }
            }
            Thread capture = captureThread;
            Thread startup = startupThread;
            join(capture); join(startup);
            recorder = null;
            captureThread = null;
            startupThread = null;
            if (automaticGainControl != null) {
                try { automaticGainControl.release(); } catch (Throwable ignored) { }
                automaticGainControl = null;
            }
            if (noiseSuppressor != null) {
                try { noiseSuppressor.release(); } catch (Throwable ignored) { }
                noiseSuppressor = null;
            }
            if (current != null) current.release();
            ExecutorService queue = diarizationQueue;
            if (queue != null) {
                queue.shutdown();
                try {
                    while (!queue.awaitTermination(1, TimeUnit.SECONDS)) { }
                } catch (InterruptedException interrupted) { Thread.currentThread().interrupt(); }
                diarizationQueue = null;
            }
            diarizationBusy.set(false);
            releaseModels();
            synchronized (ringLock) { for (int i = 0; i < ring.length; i++) ring[i] = 0f; }
            synchronized (stateLock) { recent.clear(); segments.clear(); latest = null; }
            speakers.reset();
            inputLevel = 0f;
            AudioDeviceInfo preferred = preferredInputDeviceId < 0 ? null : findInputDevice(preferredInputDeviceId);
            inputRoute = preferred == null ? "Android default microphone" : describeInputDevice(preferred);
            speakerStatus = "Microphone off · speaker analysis stopped";
            status = "Microphone off · no audio or transcript saved";
        }

        private static void join(Thread thread) {
            if (thread == null || thread == Thread.currentThread()) return;
            try { thread.join(2000); } catch (InterruptedException interrupted) { Thread.currentThread().interrupt(); }
        }

        private void releaseModels() {
            if (embeddingExtractor != null) { try { embeddingExtractor.release(); } catch (Throwable ignored) { } embeddingExtractor = null; }
            if (diarizer != null) { try { diarizer.release(); } catch (Throwable ignored) { } diarizer = null; }
            if (recognizer != null) { try { recognizer.release(); } catch (Throwable ignored) { } recognizer = null; }
        }

        private void updateInputRoute(AudioRecord activeRecorder) {
            if (activeRecorder == null) return;
            try {
                AudioDeviceInfo device = activeRecorder.getRoutedDevice();
                if (device == null) {
                    inputRoute = "Android-selected microphone (route not reported)";
                    return;
                }
                inputRoute = describeInputDevice(device);
            } catch (Throwable ignored) {
                inputRoute = "Android-selected microphone (route not reported)";
            }
        }

        private AudioDeviceInfo findInputDevice(int id) {
            if (audioManager == null) return null;
            for (AudioDeviceInfo device : audioManager.getDevices(AudioManager.GET_DEVICES_INPUTS))
                if (device != null && device.isSource() && device.getId() == id) return device;
            return null;
        }

        private String describeInputDevice(AudioDeviceInfo device) {
            if (device == null) return "Android-selected microphone (route not reported)";
            String kind;
            switch (device.getType()) {
                case AudioDeviceInfo.TYPE_USB_DEVICE:
                case AudioDeviceInfo.TYPE_USB_HEADSET: kind = "USB mic"; break;
                case AudioDeviceInfo.TYPE_WIRED_HEADSET: kind = "wired headset mic"; break;
                case AudioDeviceInfo.TYPE_BLUETOOTH_SCO: kind = "Bluetooth mic"; break;
                case AudioDeviceInfo.TYPE_BUILTIN_MIC: kind = "phone mic"; break;
                default: kind = "Android mic"; break;
            }
            CharSequence product = device.getProductName();
            String name = product == null ? "" : product.toString().trim();
            return name.isEmpty() ? kind : kind + " · " + name;
        }

        String getStateJson() {
            try {
                JSONObject json = new JSONObject();
                Caption caption = latest;
                json.put("active", running || starting);
                json.put("starting", starting);
                json.put("running", running);
                json.put("status", status);
                json.put("text", caption == null ? "" : caption.text);
                json.put("speaker", caption == null ? "Speaker unknown" : caption.speaker);
                json.put("speakerUncertain", caption == null || caption.uncertain);
                json.put("speakerConfidence", caption == null ? 0 : caption.speakerConfidence);
                json.put("finalized", caption != null && caption.finalized);
                json.put("speakerStatus", speakerStatus);
                json.put("input", inputRoute);
                json.put("level", inputLevel);
                json.put("gain", inputGain);
                json.put("agc", automaticGainControl != null && automaticGainControl.getEnabled());
                json.put("noiseSuppression", noiseSuppressor != null && noiseSuppressor.getEnabled());
                JSONArray lines = new JSONArray();
                synchronized (stateLock) {
                    int skip = Math.max(0, recent.size() - 3);
                    int index = 0;
                    for (Caption line : recent) {
                        if (index++ < skip) continue;
                        JSONObject item = new JSONObject();
                        item.put("text", line.text);
                        item.put("speaker", line.speaker);
                        item.put("speakerUncertain", line.uncertain);
                        item.put("finalized", line.finalized);
                        lines.put(item);
                    }
                }
                json.put("lines", lines);
                return json.toString();
            } catch (Throwable error) { return "{\"active\":false,\"status\":\"Caption state unavailable\"}"; }
        }
    }

    private static final class Caption {
        final String text;
        final long at;
        final long utteranceStart;
        final boolean finalized;
        volatile String speaker = "Speaker unknown";
        volatile boolean uncertain = true;
        volatile float speakerConfidence;
        Caption(String text, long at, long utteranceStart, boolean finalized) {
            this.text = text; this.at = at; this.utteranceStart = utteranceStart; this.finalized = finalized;
        }
    }

    private static final class TimedSegment {
        final long start, end;
        final int id;
        final float confidence;
        TimedSegment(long start, long end, int id, float confidence) { this.start = start; this.end = end; this.id = id; this.confidence = confidence; }
    }

    private static final class WeightedVector {
        final float[] value;
        final float weight;
        WeightedVector(float[] value, float weight) { this.value = value; this.weight = weight; }
    }

    private static final class SpeakerBank {
        private static final class Entry { final int id; float[] vector; int count; Entry(int id, float[] vector) { this.id=id; this.vector=vector; count=1; } }
        private final ArrayList<Entry> entries = new ArrayList<>();
        private int nextId = 1;
        synchronized void reset() { entries.clear(); nextId = 1; }
        synchronized int size() { return entries.size(); }
        synchronized Map<Integer, Integer> assign(Map<Integer, float[]> vectors) {
            Map<Integer, Integer> result = new HashMap<>();
            List<Integer> used = new ArrayList<>();
            for (Map.Entry<Integer, float[]> vector : vectors.entrySet()) {
                Entry best = null; float score = -1;
                for (Entry entry : entries) if (!used.contains(entry.id)) {
                    float candidate = cosine(entry.vector, vector.getValue());
                    if (candidate > score) { score = candidate; best = entry; }
                }
                    if (best == null || score < Engine.MATCH_THRESHOLD) {
                    best = new Entry(nextId++, vector.getValue().clone()); entries.add(best);
                } else {
                    for (int i=0;i<best.vector.length;i++) best.vector[i] += (vector.getValue()[i]-best.vector[i])/(best.count+1f);
                    normalize(best.vector); best.count++;
                }
                used.add(best.id); result.put(vector.getKey(), best.id);
            }
            return result;
        }
        synchronized String label(int id) {
            StringBuilder value = new StringBuilder(); int n=id;
            while(n>0){n--; value.insert(0,(char)('A'+n%26)); n/=26;}
            return "Speaker " + value;
        }
        private static float cosine(float[] a,float[] b){if(a.length!=b.length)return -1;float d=0;for(int i=0;i<a.length;i++)d+=a[i]*b[i];return d;}
    }

    private static void normalize(float[] value) {
        double square=0; for(float v:value)square+=(double)v*v;
        float norm=(float)Math.sqrt(square); if(norm>0)for(int i=0;i<value.length;i++)value[i]/=norm;
    }
    private static float cosine(float[] a,float[] b) {
        if(a.length!=b.length)return -1;float dot=0;for(int i=0;i<a.length;i++)dot+=a[i]*b[i];return dot;
    }
}
