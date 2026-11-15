# ChipOS AR

## Current Android build: ChipOS Studio 0.3.2

Native teal-and-brass creative studio with seven project areas, private editable project cards, offline English subtitles and acoustic speaker diarization. **Glasses remote** keeps touch controls on the phone and shows captions in a separate external presentation display when Android supports it. The remote can connect/disconnect, start/stop speech, size and position captions, hide/show them, and run a labeled display check with the microphone off. If the glasses mirror instead of extending the display, the remote explains the limitation. Open **Captions** for the complete caption settings, or **Float** for a movable overlay over other phone apps.

Microphone processing stays on the device. The floating overlay does not save audio or transcripts. In-app transcript saving remains an explicit action. Project information is editable and removable. This experimental APK uses the existing Android test signing certificate.

This APK provides view-fixed external captions, not room-anchored AR. It does not claim a room scan, LiDAR support or validated S26/XREAL tracking. See `docs/RELEASE_0.3.2.md` and `docs/SOURCE_UNIFICATION.md` for verification, source roles and device limitations. The older Unity keyboard prototype displayed mock captions; do not use it to test the Studio or live speech. Older setup instructions below describe earlier releases.

The older **ChipOS Mark II Unity first build** is an unconnected keyboard demo with simulated text like “Caption frame received.” It is not the Studio, speech recognition, or AR. Do not use that APK for glasses testing.

An accessibility-first augmented-reality environment for **XREAL Air 2 Ultra** glasses.

## First prototype

The first usable release is **Caption Lens**: live captions visible in the glasses, clear listening controls, adjustable presentation, and local-first transcript handling.

## Product direction

ChipOS AR will grow in careful stages:

1. Live captions and optional speaker labels.
2. An on-demand AI teaching overlay for objects, documents, and practical tasks.
3. A sound-awareness compass that indicates detected sounds and, only where sensors allow, an estimated direction.
4. Experimental spatial-awareness research using the Air 2 Ultra's 6DoF tracking and any validated companion/external depth hardware.

It is not a medical device, navigation system, or replacement for ordinary mobility and safety judgement.

## Principles

- Local processing and explicit consent by default.
- The wearer can always see when microphones or recording are active.
- Every feature reports uncertainty instead of guessing.
- Information stays readable and out of the centre of vision.
- Features downgrade safely when hardware capability, tracking, light, or confidence is inadequate.

## Team

- James — founder, product owner, and real-world tester.
- Grok 1 — XR/Android platform engineering.
- Grok 2 — accessibility, captions, and AI interaction design.
- Grok 3 — spatial sound, sensing, calibration, and safety engineering.
- Codex — systems architecture, code review, documentation, and delivery discipline.

## Planned structure

```text
android/       Android/XREAL app
docs/          product, hardware, privacy, and test notes
prototypes/    isolated experiments and simulators
```

## Safety and privacy

No audio, transcripts, room maps, images, or cloud processing are enabled without a clear user choice. Speaker labels are provisional unless a user has explicitly named them. Spatial and sound cues are assistive only.

## Setup — Windows PC and Android phone

This is the first buildable Android project. It is a native Kotlin/Compose app. When the Air 2 Ultra is plugged into the phone over USB-C DisplayPort, the phone UI is expected to appear as a **2D image in the glasses**. That is the closest verified native baseline.

Official **6DoF / hand-tracking / mesh AR** is a different path: Unity + XREAL SDK 3.1.0, launched from ControlGlasses or MyGlasses. See `docs/HARDWARE_CAPABILITIES.md`. Do not confuse this APK with that SDK app.

### On the Windows PC

1. Install [Android Studio](https://developer.android.com/studio) (stable).
2. In the first-run wizard, install:
   - Android SDK 35
   - Android SDK Build-Tools
   - Android SDK Platform-Tools (`adb`)
   - The bundled **JDK 17**
3. Clone this repository. In Git Bash or PowerShell:

   ```bat
   git clone https://github.com/walkermejames-commits/chipos-ar.git
   cd chipos-ar
   ```

4. Open the **`android/`** folder in Android Studio (**File → Open → chipos-ar/android**). Let Gradle sync. If `local.properties` is missing, Android Studio will write `sdk.dir` for you. A Windows example is in `android/local.properties.example`.
5. Connect the Android phone with a data USB-C cable. On the phone: **Settings → About phone → tap Build number seven times**, then enable **USB debugging**. Accept the RSA prompt.
6. Build and install the debug APK:

   ```bat
   cd android
   gradlew.bat assembleDebug
   gradlew.bat installDebug
   ```

   Or click **Run** in Android Studio. The debug package id is `com.chipos.ar.debug`. The APK lands at `android\app\build\outputs\apk\debug\app-debug.apk`.

Command-line build needs JDK 17 on `PATH` or `JAVA_HOME` (Android Studio’s JBR is fine; typical path `C:\Program Files\Android\Android Studio\jbr`).

### On the Android phone

1. Unlock the phone and confirm **ChipOS AR — Caption Lens** is installed.
2. Unplug the PC.
3. Plug the XREAL Air 2 Ultra into the phone with the glasses’ USB-C cable (must support DisplayPort, not charge-only).
4. Wear the glasses. Unlock the phone if the image is dark.
5. Open the app if it is not already in front.

You do **not** need ControlGlasses for this first 2D test. You **will** need ControlGlasses (phone) or MyGlasses (Beam Pro) later for official 3D XREAL SDK apps.

Do not upgrade the test phone to **Android 16**; XREAL currently warns that compatibility is under investigation.

### What you should see

- Dark stage, centre of the view kept clear.
- Title **ChipOS AR — Caption Lens** on a low caption card.
- A listening chip that is always visible: **NOT LISTENING**, **LISTENING — SAMPLE TEXT**, or **STOPPED**.
- **Start Listening** / **Stop Listening**, text size, contrast, and panel position.
- Sample sentences only. The microphone stays off. No cloud audio.

Then follow `docs/PHASE_1_TEST_PLAN.md`.

## Phase 2 Caption Lens

The current APK (`0.2.2-caption-lens`) starts on the bundled **offline live** caption engine, with the microphone off until the user taps **Start live listening** and grants permission. During live capture, recent captions span the bottom of the display, speaker labels use stable per-session colors, the main controls collapse to a stop button, and mic boost can be adjusted under **More**. Demo modes remain available from **Choose engine**:

- **Demo — sample captions**: original Phase 1 source, microphone off.
- **Demo — several speakers**: scripted Speaker 1–3 conversation, microphone off.
- **Live — on-device speech**: Android `createOnDeviceSpeechRecognizer`, with audio kept on-device. If the phone lacks that engine, Start Listening refuses; it never falls back to cloud speech. Speaker labels remain unknown in this mode.
- **Live — offline ASR + acoustic speakers**: bundled sherpa-onnx streaming English captions plus rolling acoustic diarization. Anonymous speaker labels are session-only and may arrive late or be uncertain. This experimental mode currently targets arm64-v8a. See `docs/DIARIZATION_ENGINE.md` for model details, footprint, and limitations.
- Optional local transcript session, off by default: start/stop, show session, delete session, delete saved file. No cloud sync.
- **Teach me this**: one local mock card with uncertainty wording.
- Voice-command parser plus **Test voice** for start/stop captions, make it bigger, move subtitles down, teach me this, hide help.

Follow `docs/PHASE_2_TEST_PLAN.md` after the panel is readable in the glasses.

## Docs

Existing product writing is unchanged:

- `docs/PRODUCT_BRIEF.md`
- `docs/DECISIONS.md`

Hardware and tests:

- `docs/HARDWARE_CAPABILITIES.md`
- `docs/PHASE_1_TEST_PLAN.md`
- `docs/PHASE_2_TEST_PLAN.md`
- `docs/CAPTION_PRIVACY.md`
- `docs/AI_TEACHING_DESIGN.md`

## Privacy in this build

- No `INTERNET` permission.
- No hidden recording. Listening state is on screen at all times.
- Default mode does not open the microphone.
- `RECORD_AUDIO` is requested only for live modes, and only when you tap Start Listening.
- Transcripts are off until **Start session**. Files stay in app-private storage.
- There is no cloud speech path and no silent fallback.
- Teach me this is a local mock.
- No face recognition. No stranger identification.
