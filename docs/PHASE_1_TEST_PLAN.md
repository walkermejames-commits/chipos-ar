# Phase 1 test plan — Caption Lens on glasses

This is the first glasses-on check for James. It only tests the native Android Caption Lens scaffold: a fixed low panel, sample captions, listening controls, and presentation knobs. It does not test real speech recognition, 6DoF, cameras, or ControlGlasses 3D launch.

ChipOS AR is not a medical, navigation, or balance product. Walk only in a familiar, clear indoor space. Sit for the first pass if that is more comfortable.

## You need

- XREAL Air 2 Ultra
- The USB-C cable that came with the glasses
- Android phone with USB-C video output (DisplayPort Alt Mode). Official spatial AR later wants a Samsung S24 or newer flagship; Phase 1 will still try any DP-capable phone
- The ChipOS AR debug APK installed (`com.chipos.ar.debug`)
- A well-lit room where you can sit or stand still
- This page, or a notebook, to record what you see

You do **not** need ControlGlasses, Unity, or a microphone for this test.

## Install (once)

On the Windows PC, with the phone on USB debugging:

```bat
cd chipos-ar\android
gradlew.bat installDebug
```

Or press Run in Android Studio.

Then unplug the PC cable. Plug the glasses into the phone. Wear the glasses. Unlock the phone and open **ChipOS AR — Caption Lens**.

## The test (about five minutes)

Do these in order. Write **pass / fail / notes** for each line.

### 1. Image in the glasses

- [ ] I see the phone app in the glasses (dark screen, not a blank or PC desktop).
- [ ] I can read the title **ChipOS AR — Caption Lens** without tilting my head into an awkward angle.
- [ ] The middle of my view is mostly empty. The caption card sits low, not over faces or the floor immediately in front of me.

If the glasses stay black: the phone may lack USB-C DisplayPort, or the cable is charge-only. Stop here and note the phone model.

### 2. Listening light (always visible)

- [ ] At the top left I see a status chip. Before I tap anything it says **NOT LISTENING** and that the microphone is off.
- [ ] I tap **Start Listening**. The chip turns green and says **LISTENING — SAMPLE TEXT**. It still says the microphone is off.
- [ ] Sample sentences change every few seconds. This is fake text. I am not speaking yet.
- [ ] I tap **Stop Listening**. The chip changes to **STOPPED**. Sample text stops changing. The last line can stay on screen.

If the status chip is ever missing, that is a fail. Listening state must stay visible.

### 3. Readability

- [ ] **Text +** makes captions larger. **Text −** makes them smaller. Default (L) is readable for me, or I write which size is better.
- [ ] **Contrast** cycles white-on-black, yellow-on-black, and black-on-yellow. I write which one I would keep.
- [ ] **Panel position** moves the card: bottom centre, bottom left, bottom right, lower centre. I write which one stays out of my walking view.

### 4. Privacy check

- [ ] I never granted a microphone permission dialog (Phase 1 must not ask).
- [ ] The UI never says it is recording.
- [ ] I am willing to say, out loud: “This build is sample captions only.”

### 5. Hardware notes for Codex / Grok

Copy these values as shown on the status chip and in the phone’s Settings:

| Field | Value |
| --- | --- |
| Phone model | |
| Android version | |
| One UI / other skin | |
| `Displays: N` before glasses connected | |
| `Displays: N` with glasses connected | |
| Did the glasses replace the phone screen, or sit as a second screen? | |
| Did sample captions stay readable while I turned my head? | |
| Anything overlapping the centre of view? | |
| Comfort after two minutes | |

Head turning does **not** pin the panel in world space in Phase 1. The card is part of the 2D phone image. If it moves with the virtual screen, that is expected.

## Pass / fail

**Pass** if all of 1, 2, 3, and 4 are true enough to keep using the panel, and section 5 is filled in.

**Fail** if you cannot see the app in the glasses, cannot find Start/Stop, cannot read the sample line, or the listening light disappears.

Paste the table plus any fail notes back to Grok and Codex. Do not move on to real speech-to-text until this pass is done.

## Explicitly out of scope

- Real speech recognition
- Speaker names other than the sample “Speaker 1”
- 6DoF world-locked captions
- Hand tracking
- Glasses cameras
- Cloud audio
- Outdoor mobility tests
