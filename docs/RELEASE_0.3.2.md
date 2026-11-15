# ChipOS Studio 0.3.2 — consolidated Android build

This ARM64 test build packages the native Studio, its editable project areas, offline caption engine, acoustic speaker labels and Android glasses display controls in one installable app. It replaces the archived Unity keyboard demo for Studio and speech use.

The build is based on the 0.3.1 feature set. It updates the app version and builds with Android API 36 support. It does not add tracked spatial AR. The Unity/XREAL tracking project still has simulated captions and remains separate. See [the source audit](SOURCE_UNIFICATION.md).

## Verification on this PC

- `assembleDebug`, ARM64 `assembleRelease` and `lintDebug` completed successfully.
- The release APK is signed with the existing Android debug/test certificate (SHA-256: `49dd995914a6582eaa33c76fa0c128bd801d43d564a205ba45295d1f9b3a44f2`) so it can update the experimental 0.3.x Studio APK. It is not a production or Play Store signing key.
- The ARM64 APK is 138,124,087 bytes, passes APK signature verification and 16 KB zip alignment, and has SHA-256 `8a359d2a61901bcdab7a2c8e555d4ae44d7f68e4fb9ea9084ded38116c44023e`.
- Instrumentation tests were not run for this build: `adb devices -l` reported no online phone or emulator after restart. The existing release notes document previous emulator test coverage.
- The app could not be checked on the Samsung S26 or XREAL Air 2 Ultra during this build.

## What the device should support

The app opens to the creative studio and offers **Captions**, **Float**, and **Glasses remote**. Speech uses bundled offline models and microphone access begins only after the user starts listening. The glasses remote can show text on an Android external presentation display when one is available. If Android mirrors the phone instead, its status explains that limitation.

Glasses output is view-fixed, not room-anchored AR. No LiDAR, camera scan, spatial anchors, hand tracking or depth mesh are included in this APK. The microphone, display route, optical visibility and installation must be checked on the actual phone and glasses.
