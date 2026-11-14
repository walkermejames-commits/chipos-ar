# ChipOS Studio 0.3.1 — glasses remote

This update addresses a serious usability problem in the earlier prototype. The archived ChipOS Mark II Unity demo was keyboard driven, showed simulated strings such as “Caption frame received,” and did not do live speech recognition. It was mistakenly presented alongside the requested glasses software. It is neither the Studio nor an AR/speech build; its download is now visibly marked as an old mock demo in the APK index.

## What changed

The Studio APK now has a **Glasses remote** page that separates phone controls from the text shown in an external presentation display. Connect and disconnect the display, explicitly start or stop speech, change caption size and position, hide/show captions, or show a labeled display-test pattern without starting the microphone. When no separate Android display is available, it explains that the phone may be mirroring and disables the connect control.

The output is an optical, black-background subtitle window. It follows the display surface; it does not persist to a wall or object. There is no camera access, scene understanding, depth mesh or room scan in this build. It is not tracked 6DoF AR. External-display and optical visibility behavior must still be checked with the actual S26 and Air 2 Ultra.

## Verification

- `assembleDebug`, ARM64 `assembleRelease`, Android test APK and `lintDebug` pass.
- Four Android API 35 emulator instrumentation tests pass: offline English transcription and two-speaker diarization; create/save/edit/delete local project; foreground caption overlay survives Home and stops from its button; and an independent virtual display shows live text, supports sizing and hiding, displays a clearly labeled test pattern, and returns to “Microphone off” on stop.
- The real-display-window test uses an Android virtual presentation display. It verifies app-side output and control behavior, not glasses, optical visibility, Samsung DeX, microphone routing or room tracking.
- APK is ARM64, signature verifies and matches ChipOS 0.2.4 / 0.3.0 test certificate (`49dd995914a6582eaa33c76fa0c128bd801d43d564a205ba45295d1f9b3a44f2`); 16 KB ZIP alignment passes. It uses an Android debug/test signing key, not a production signing key.
- SHA-256 is supplied in `SHA256SUMS.txt`.

## Install and try

Install the `chipos-studio-0.3.1-arm64.apk` update. Open **Glasses remote** on the phone. Plug in the glasses and enable an extended/separate display in Samsung DeX or Android display settings if available. The remote must say **Separate display detected** before **Connect glasses display** can work. Tap **Test display** to check visibility without turning on the mic. Tap **Start live captions** and grant microphone access only when ready; use **Stop microphone** when finished. If Android presents only a mirrored phone screen, a separate phone remote cannot be achieved through that display route; this page reports the limitation. Test with the glasses safely seated and a companion available.

The independent Unity/XREAL 6DoF build remains a separate, unfinished deliverable. Unity Android build modules are now installed. The editor batch check exited before project compilation because this PC has no active Unity entitlement (`Access token is unavailable`; zero entitlements). The project pins Unity 6000.4.8f1 while this PC has 6000.6.5f1. Activate an eligible Unity license in Unity Hub, then resolve the project/editor version before attempting that build. A successful Unity APK build is not yet verified.
