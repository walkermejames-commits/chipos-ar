# ChipOS Studio 0.3.0 — experimental Android build

Teal-and-brass native studio with seven interactive project areas, editable local project cards, status, next action, notes and explicit Confirmed / Suggestion / Unknown provenance. No personal biography or story canon is invented.

Bundled offline English speech recognition and acoustic diarization provide provisional anonymous speaker labels. Captions offers detailed controls. Float starts a draggable caption overlay over other Android apps, with text sizing and Stop controls. Microphone access and display-over-other-apps access require explicit user action; a persistent notification indicates background listening. The APK has no INTERNET permission.

## Install and use

1. Download the ARM64 APK on the phone and open it. Allow installation from that browser/file manager if Android requests it.
2. Open ChipOS Studio. Use Captions and Start live listening; allow microphone access.
3. For captions over other apps, choose Float, read the explanation, and allow display over other apps in Android Settings. Return and choose Float again to enable it. Allow microphone/notification permissions as requested.
4. Drag the caption header to move it; use A−/A+ to resize text and Stop to end listening. Speaker labels can take about 12 seconds and remain provisional.
5. Connect compatible XREAL display hardware for mirroring. Verify phone/OS compatibility and microphone routing on the actual device.

## Verification

- Debug and ARM64 release builds pass; lint has no errors (existing warnings remain).
- Android API 35 x86_64 emulator: 3 instrumentation tests pass. The bundled models transcribe the supplied English audio and separate its two voices; the floating microphone overlay survives Home and stops from its button; a project is created through the UI and storage reload/update/delete are checked.
- Gradle's connected-test transport failed; the same installed test suite passed through direct Android instrumentation.
- APK signature verifies and matches the previous 0.2.4 signing certificate. Uses an Android debug/test certificate, not a production distribution key.
- ARM64-only native libraries; 16 KB ZIP alignment check passes. This is not a substitute for native-library/device validation on a 16 KB page-size phone.

## Remaining work

S26 and Air 2 Ultra hardware have not been tested. This is a native 2D studio and view-fixed speech overlay, not the separate Unity 6DoF spatial build. Unity Android support installation and editor license activation remain required before that build can be compiled and checked. No LiDAR, depth sensing, hand tracking or measured room map is claimed by this APK. Noise, overlapping voices, distant microphones and speaker changes need device testing. Studio cards currently organize projects; specialist pattern editors and external project integrations remain future work.

Next practical step: test the APK on the S26 with two speakers, verify microphone routing and floating-overlay comfort in the glasses, then finish Unity setup and validate anchors using the actual XREAL tracking runtime.
