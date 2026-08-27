# Hardware capabilities — XREAL Air 2 Ultra

Last verified against public XREAL documentation on **2026-08-27**.

This file records what we could confirm from official sources, what still has to be tested on James’s glasses and phone, which functions need a companion computer, and where external sensors may be needed later. It does not invent hardware that is not in those sources.

ChipOS AR is not a medical device, navigation system, or balance aid.

## Official sources used

- [XREAL SDK Overview](https://docs.xreal.com/) (docs version 3.1.0)
- [Getting Started with XREAL SDK](https://docs.xreal.com/Getting%20Started%20with%20XREAL%20SDK)
- [XREAL Glasses](https://docs.xreal.com/XREALDevices/XREAL%20Glasses)
- [XREAL SDK 3.0.0 release notes](https://docs.xreal.com/Release%20Note/XREAL%20SDK%203.0.0)
- [XREAL developer site / Air 2 Ultra product page](https://developer.xreal.com/)
- [XREAL SDK download page](https://developer.xreal.com/download/) (Unity SDK 3.1.0 listed, published 2025-11-25)

## Confirmed facts

### Device

| Item | Official statement |
| --- | --- |
| Product | XREAL Air 2 Ultra |
| Role | Lightweight spatial-computing glasses for developers; USB-C tethered, not a standalone computer |
| Display | Sony Micro OLED, 1920×1080 per eye, 52° FOV, up to 120 Hz in 2D and 90 Hz in 3D |
| Brightness | Up to 500 nits in 2D; up to 250 nits in 3D (developer site) |
| Weight | ~80 g (SDK glasses table) / 83 g (developer site) |
| Connection | USB-C DisplayPort Alt Mode |
| RGB camera | **No** on Air 2 Ultra. Official table: RGB Camera = No. Product page: “Video recording and photography are not supported.” |
| Tracking cameras | **Yes** — dual grayscale / “3D environment sensors”. These are the 6DoF SLAM cameras |
| IMU | Yes |
| Audio | Dual stereo speakers and **2 microphones** |
| Tracking class | `XREAL_DEVICE_CATEGORY_REALITY` — glasses with 6DoF (Air 2 Ultra). Contrast: Air 2 / One series are `XREAL_DEVICE_CATEGORY_VISION` (3DoF) |
| Spatial features (via XREAL SDK) | 6DoF tracking, plane detection, image tracking, hand tracking, depth mesh, spatial anchors |
| Electrochromic dimming | 3 levels, hardware button |
| RGB photography / first-person RGB | Not supported on this model. XREAL Eye is a detachable RGB accessory for the One series, not listed as part of Air 2 Ultra |

### Official development route

The current official AR path is **Unity + XREAL SDK 3.1.0**, not a native Android spatial SDK.

- Unity LTS: 2021.3.x, 2022.3.x, or 6000.0.x, with Android Build Support.
- XREAL XR Plugin (`com.xreal.xr`), XR Plugin Management, XR Interaction Toolkit, optional AR Foundation / XR Hands.
- Graphics: OpenGL ES 3. Android API: SDK docs ask for Android 12 (API 31) or later installed; player minimum is listed as Android 10.0 or higher.
- Architecture: ARM64 / IL2CPP. ARMv7 is not supported from NRSDK 2.2.0 onward.
- Stereo rendering, 6DoF, hand tracking, and environment features are provided by the Unity plugin, not by stock Jetpack XR on this headset.
- **Android XR / Jetpack XR / XREAL Aura** is a different, later platform. It is not the documented route for Air 2 Ultra.

### Companion computer is required

Air 2 Ultra does not run apps by itself.

| Companion | What official docs say |
| --- | --- |
| Supported Android phone | Required for XREAL SDK apps. Getting Started (3.1.0): “Currently, only the Samsung S24, and subsequent flagship series are supported.” SDK 3.0.0 notes: they only test Samsung flagships; 3.0.0 is compatible with Samsung S24. Developer site still lists Spatial Computing on Samsung S22/S23 Snapdragon as well — treat S24+ as the currently guaranteed set. |
| Beam Pro | Alternative to a phone. Launch 3D apps with **MyGlasses**. |
| ControlGlasses | Required on a phone (not Beam Pro) to perform OTA updates and **launch 3D applications**. Nebula is no longer maintained. |
| Screen mirroring | Any USB-C video-output device (Windows, Mac, Android, iPhone, Steam Deck, consoles, etc.) can use the glasses as a 2D display. That is general use, not spatial AR. |

Launch flow for an official 3D/AR APK:

1. Build on PC.
2. `adb` install onto the phone or Beam Pro.
3. Disconnect the PC, connect the glasses to the phone / Beam Pro.
4. Open **ControlGlasses** (phone) or **MyGlasses** (Beam Pro) and start the app.
5. Grant overlay permission (“Allow to display over other apps”) if prompted.

### Permissions the SDK documents

XREAL Settings can add:

- **CAMERA** — access the camera (on Air 2 Ultra this means the grayscale tracking cameras through the SDK, not an RGB webcam).
- **AUDIO** — access audio.
- **VIBRATION** — vibration.

Overlay / display-over-other-apps is called out at first launch.

Do **not** upgrade development phones to **Android 16**. Official docs: compatibility issues are under investigation.

### Microphones and cameras — what is actually on the glasses

Confirmed on-device:

- Two built-in microphones.
- Two grayscale environment / SLAM cameras.
- IMU.
- Dual speakers.

Not on Air 2 Ultra:

- RGB camera.
- Standalone compute, battery for independent apps, or cellular.

Whether those two microphones appear to Android as a USB headset, and whether an ordinary Android app can open the grayscale cameras as a Camera2/UVC device, is **not** confirmed in the official SDK getting-started pages. Unofficial work exists for descrambling the UVC grayscale feed on a host computer; that is research, not the supported app path.

## Phone-required functions

These do not run on the glasses alone:

| Function | Where it actually runs |
| --- | --- |
| App process, UI, Gradle APK | Companion Android phone or Beam Pro |
| Official 6DoF / hand tracking / mesh / anchors | XREAL SDK inside the Unity APK on the companion |
| Launching 3D AR apps | ControlGlasses (phone) or MyGlasses (Beam Pro) |
| Firmware / OTA for glasses when using a phone | ControlGlasses |
| Caption Lens Phase 1 | Native Android app on the phone; glasses used as a USB-C 2D display |
| Future on-device speech-to-text | Companion CPU/NPU, after we verify a local engine |
| Future teaching overlay | Companion, on demand |
| USB debugging / first install | Windows (or Mac) PC + `adb` |

A Windows PC is the development machine. It is not required at glasses-on test time except to install the APK.

## Unknowns to test on James’s hardware

Record the phone model, Android version, and One UI / firmware versions in the Phase 1 test notes.

1. **Phone model.** Is it a Samsung S24 or later flagship? If not, official 3D AR is unverified; 2D USB-C mirroring may still work if the phone has DisplayPort Alt Mode.
2. **USB-C DisplayPort.** Does plugging the Air 2 Ultra into the phone show the phone UI in the glasses without extra apps?
3. **Display count.** Caption Lens shows `Displays: N`. Does N stay 1 (mirrored primary) or become 2 (separate presentation display) when the glasses connect?
4. **ControlGlasses.** Can the Phase 1 APK be listed and opened from ControlGlasses? Unknown — this is a native Compose app, not a Unity XREAL SDK build. The `com.nreal.supportDevices` manifest hint is copied from the official FAQ; we have not verified it on hardware.
5. **Glasses microphones vs phone microphone.** When the glasses are connected, which input does Android pick? Phase 1 does not open any microphone.
6. **Grayscale cameras from a normal Android app.** Official access is through XREAL SDK. Direct Camera2/UVC access is unverified and must not be assumed.
7. **3D SBS vs 2D.** ControlGlasses switching the glasses to 3D split-screen is documented for SDK apps. A 2D Compose UI in 3D mode would look wrong (half per eye). Phase 1 should be tested in ordinary 2D display mode first.
8. **Overlay permission.** Needed for SDK 3D apps. May or may not be needed for this 2D baseline.
9. **Android 15/16.** Do not move the test phone to Android 16 until XREAL says it is compatible.
10. **Comfort / FOV.** Is the default bottom caption card low enough, or does it sit in the walking line of sight?

## External-sensor research (later)

Spatial-awareness work in the product brief is research. Air 2 Ultra gives 6DoF plus a depth mesh **through the Unity SDK**, not as a public native depth API we have verified.

Possible later add-ons, none of which are in Phase 1:

- XREAL SDK depth mesh and plane detection (Unity path).
- A calibrated external depth or radar module if on-glasses mesh is too coarse or unavailable to native code.
- Phone microphone array for sound direction if glasses mics cannot yield a trustworthy azimuth.
- XREAL Beam Pro cameras are for spatial photo/video on the Beam, not a substitute for Air 2 Ultra RGB (the glasses have none).

Do not describe any of that as navigation, medical, or balance equipment.

## What Caption Lens Phase 1 actually uses

| Resource | Phase 1 |
| --- | --- |
| Companion phone | Yes — the app runs here |
| Glasses as USB-C 2D display | Expected; James must confirm |
| XREAL Unity SDK 6DoF | No |
| Glasses grayscale cameras | No |
| Any microphone | No |
| Cloud audio | No |
| ControlGlasses | Not required for the first 2D test |
| Sample captions | Yes |

The Android project is modular so speech-to-text, diarization, teaching overlays, sound mapping, and spatial research can plug in later without rewriting the caption panel.
