# ChipOS Mark II

ChipOS Mark II is a desktop-first Unity HUD shell. This layer adds a stronger local operating core: central state, a lightweight event bus, richer layout profiles, panic recovery, task cycling, modular HUD surfaces, and a simulated device seam without introducing XR or online dependencies yet.

## Open The Scene

Open [ChipOSMarkII_Main.unity](/E:/chip%20ios%20mark2/Assets/ChipOSMarkII/Scenes/ChipOSMarkII_Main.unity).

If the scene is stale or missing objects, run `Tools > ChipOS Mark II > Build Main Scene`.

## Controls

- `H`: Toggle HUD fade
- `R`: Reset layout and clear saved layout state
- `P`: Enter panic layout
- `O`: Restore the last stable layout
- `T`: Cycle local tasks
- `F1`: Default layout
- `F2`: Desk layout
- `F3`: Walking layout
- `F4`: Focus layout
- `F5`: Minimal layout
- `1`: Device context `Desk`
- `2`: Device context `Walking`
- `3`: Device context `Focus`
- `4`: Device context `Unknown`
- `B`: Battery `-10%`
- `N`: Battery `+10%`
- `C`: Toggle charging
- `Y`: Cycle tracking state
- `U`: Toggle comfort warning
- `Arrow Keys`: Simulate head pose
- `Backspace`: Reset simulated device

## Architecture

- `ChipState` is the serializable runtime snapshot for app mode, layout, panic state, alert level, active task, input source, system message, and timestamps.
- `ChipEventBus` publishes lightweight internal events so panels react to state changes without direct panel-to-panel wiring.
- `ChipOSApp` owns boot flow, module registration, state mutation, event publication, HUD visibility, and panic recovery.
- `LayoutManager` owns named layout profiles, descriptions, transition targets, persistence, and self-test coverage.
- `InputRouter` and `KeyboardChipInputProvider` keep keyboard input behind a reusable command layer that future XREAL, gaze, voice, or touch providers can reuse.
- `ChipOSRuntime` owns service registration, typed runtime events, notifications, persistence, and the device adapter seam.
- `IChipOSDeviceService` and `SimulatedChipOSDeviceService` provide editor-safe device status, tracking, context, battery, comfort, and pose simulation for future XR integration.
- `HudPanel` is the shared module base with safe runtime UI generation, fades, movement, rotation, scale transitions, and Unity UI text fallback.

## Modules

- `ChipPanel`
- `TaskPanel`
- `AlertPanel`
- `StatusStrip`
- `DebugPanel`
- `EnvironmentPanel` as an optional future-facing module

## Scene Hierarchy

- `Main Camera`
- `ChipOSCanvas`
- `EventSystem`
- `ChipOSRoot`

`ChipOSCanvas` children:

- `SafeAreaRoot`
- `ChipPanel`
- `TaskPanel`
- `AlertPanel`
- `StatusStrip`
- `DebugPanel`

`ChipOSRoot` components:

- `ChipOSApp`
- `ChipOSRuntime`
- `LayoutManager`
- `InputRouter`

## Layout System

- Layouts are keyed by `LayoutMode` and restored from `PlayerPrefs` on startup.
- Each profile now defines panel visibility, position, scale, opacity, and a short description.
- `Panic` hides non-essential modules and preserves the previous non-panic layout for `Restore`.
- Duplicate panel registrations are ignored safely and stale/null registrations are pruned automatically.

## Boot And Diagnostics

- Play Mode now starts in `Boot` mode with short boot messages before switching to a ready state.
- `DebugPanel` shows mode, layout, HUD visibility, panic state, alert level, current task, last input, last event, scene name, Unity version, FPS, and simulated device state.
- `AlertPanel` becomes the calm recovery surface in panic mode.
- `StatusStrip` provides a lightweight always-on state readout.
- `LayoutManager.RunSelfTest()` checks registration health, layout configuration, and text backend readiness.

## Current Limitations

- No NRSDK/XREAL integration yet
- No OpenXR integration yet
- No real hardware device SDK yet; the project uses simulation only
- Keyboard-only input and device simulation in this layer
- Prototype-safe 2D layouts only
- No backend assistant services yet

## Next Steps

- Validate this shell in the Unity editor with the manual checklist.
- Add a real XR adapter behind `IChipOSDeviceService` on a separate branch.
- Introduce device-aware anchoring and presentation once the local shell is stable.
