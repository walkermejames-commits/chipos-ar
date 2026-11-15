# ChipOS source audit and unification

## What is in this APK

The canonical, installable app source is `android/`, a native Kotlin and Jetpack Compose project. It brings the creative studio and speech workflow into one Android application:

- Seven editable, private project areas with explicit confirmed, suggested and unknown labels.
- On-device Sherpa-ONNX English transcription and acoustic speaker diarization.
- A full-screen caption view, optional floating caption service, and a phone-controlled external presentation display.
- Local transcript handling with explicit save controls; no network permission or camera access.

The Android external presentation display is view-fixed. It does not provide room anchors, spatial mapping, LiDAR access, hand tracking or a room scan.

## What remains separate

The local Unity Mark II project at `C:\Users\imedt\ChipOSMarkII-Build` contains XREAL SDK/AR Foundation experiments for spatial tracking, planes and anchors. It is not part of the Git source tree or the Studio APK. Its caption/audio presentation is simulated, so that project cannot be used to validate speech recognition or diarization.

The old Unity keyboard demo is archived separately and should not be distributed as the Studio. Its placeholder “Caption frame received” is not live speech.

## Integration boundary

The working user-facing release is the native Studio because it contains the verified speech and useful project controls. Spatial tracking remains a separate research target. A unified tracked AR build still requires integrating the native offline speech pipeline into an XREAL SDK app and then testing that app on the S26 with Air 2 Ultra. No LiDAR capability is claimed for the phone or glasses.

## Privacy boundary

Speech capture begins only after an explicit user action and microphone permission. Audio processing is local. Transcript persistence is user-initiated. The Unity prototype's simulated captions must not be described as audio capture or speech recognition.
