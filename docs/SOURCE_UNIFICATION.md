# ChipOS source status

## Native Android fallback

`android/` builds ChipOS Studio 0.3.2. It has seven editable creative areas, offline English transcription, acoustic speaker diarization, and phone or external-display captions. Its glasses display is view-fixed; this APK does not provide room mapping or spatial anchors.

## Unity/XREAL project

`unity/` contains the source project for ChipOS Mark II Unity AR 0.1.21. The Android build uses an explicit microphone start, the offline Sherpa bridge and speaker-analysis models, a phone studio fallback, and XREAL/AR Foundation spatial controls. Code paths for mapping and anchor placement exist; this does not establish that those features work on the target hardware.

The source project was built with Unity 6000.6.5f1 as package `com.walkermejames.chiposmarkii`, version `0.1.21`, version code 21, minimum API 31 and target API 36. The ARM64 APK is 225,338,124 bytes, built with 0 errors and 75 warnings. APK v2 signature verification passed. SHA-256: `A8B370AF33827C604482581579DEF4CB816DC97863D4127174249FC80AE7DFA3`.

## Verification and limitations

- The APK was installed on the Samsung S26 API 36 x86_64 emulator. The startup dock appeared. Tapping camera mapping reported that spatial tracking is unavailable on the emulator. Tapping captions reported that the bundled speech runtime is unavailable for x86_64. The app remained open.
- This confirms UI visibility and those failure messages only. Emulator testing did not verify microphone capture, speech accuracy, diarization, XREAL see-through, mapping cameras, room-anchor placement or Air 2 Ultra input.
- The APK is ARM64-only. Live speech and spatial behavior remain unknown until tested on the physical Samsung S26 with Air 2 Ultra.
- The Unity EditMode test invocation did not produce a results file; no test-suite pass count is claimed for 0.1.21.
- The APK and full build notes are in the [0.1.21 GitHub release](https://github.com/walkermejames-commits/james-os-apk-repo/releases/tag/chipos-markii-unity-ar-0.1.21) and [`docs/UNITY_RELEASE_0.1.21.md`](UNITY_RELEASE_0.1.21.md).

## Build prerequisites

Use Unity 6000.6.5f1 with the Android build module. The Unity project requires XREAL Unity SDK 3.1.0 under `unity/Packages/com.xreal.xr`. That vendor package is not included in Git because the local copy has no redistribution license; obtain it from XREAL and place it at that path before opening the project. Generated builds, editor caches, logs, and the archived legacy models are excluded from Git. The speech bridge source and its large model/runtime files are tracked with Git LFS.

## Privacy and personal information

Microphone capture starts only after the user chooses the live-caption action and grants Android permission. Speech processing is local; captions and speaker labels are not saved by the Unity bridge. Project notes remain on-device. Unknown personal details remain unknown, and creative suggestions remain distinct from established canon.
