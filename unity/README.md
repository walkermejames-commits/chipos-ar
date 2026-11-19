# ChipOS Mark II Unity AR

This folder is the source project for the experimental Unity/XREAL APK. The latest published APK is 0.1.21; verification limits are documented in [`../docs/UNITY_RELEASE_0.1.21.md`](../docs/UNITY_RELEASE_0.1.21.md).

## Open the project

1. Install Unity 6000.6.5f1 with Android Build Support.
2. Obtain XREAL Unity SDK 3.1.0 through XREAL's official developer distribution and extract the package to `Packages/com.xreal.xr`. This vendor SDK package and its `nractivitylife_6-release.aar` compatibility library are intentionally excluded from this repository because the local copy has no redistribution license.
3. Open this `unity/` folder in Unity Hub and allow Unity Package Manager to resolve the remaining packages.
4. Build Android ARM64 using **Tools → ChipOS Mark II → Build Android ARM64**.

The generated APKs, Unity `Library`, editor logs, Gradle caches and legacy model archive are excluded from Git. `AndroidSpeechPlugin/` contains the bridge source; the Unity project also imports the packaged Android AARs and local model assets tracked through Git LFS.
