# ChipOS Mark II Unity AR 0.1.21

- Release: [GitHub prerelease](https://github.com/walkermejames-commits/james-os-apk-repo/releases/tag/chipos-markii-unity-ar-0.1.21)
- Android package: `com.walkermejames.chiposmarkii`
- Version: `0.1.21`, version code `21`
- Architecture: ARM64
- Minimum Android API: 31; target API: 36
- Unity: 6000.6.5f1
- APK size: 225,338,124 bytes
- SHA-256: `A8B370AF33827C604482581579DEF4CB816DC97863D4127174249FC80AE7DFA3`
- Build result: 0 errors, 75 warnings; APK v2 signature verification passed

## Changes in this build

- Acoustic speaker analysis uses a 12-second window and matches detected segments against the full finalized caption interval.
- Added a software microphone gain control for quieter input routes.
- Kept the phone studio controls visible and exposed explicit unsupported-device status for spatial mapping and speech runtime.

## Emulator check

Installed and opened on the Samsung S26 API 36 x86_64 emulator. The startup dock was visible. Tapping camera mapping displayed the emulator limitation. Tapping live captions displayed that the bundled speech runtime is unavailable for x86_64. These checks exercised the visible UI and failure paths; they did not verify microphone capture or live captions.

The physical S26/Air 2 Ultra has not been verified with this build. Speech recognition accuracy, acoustic diarization, camera mapping, see-through rendering, room anchors and glasses input remain unknown. Do not treat the existence of these code paths or the emulator screen as evidence that those features work on the headset.
