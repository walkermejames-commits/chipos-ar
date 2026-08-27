# Phase 2 test plan — Caption Lens

Do Phase 1 first if you have not already seen the panel in the glasses (`docs/PHASE_1_TEST_PLAN.md`).

Sit for the first pass. This is not a mobility test.

## You need

- XREAL Air 2 Ultra on USB-C DisplayPort
- Companion Android phone with the **0.2.0** debug APK (`com.chipos.ar.debug`)
- A quiet indoor room
- Optional: a second person talking, only for the live-mic pass

## A. Offline demo (no talking, no microphone)

Default mode is **Demo — sample captions** (the Phase 1 sample path).

1. Open the app. Confirm **NOT LISTENING**, **Mic: off**, and no microphone permission dialog.
2. Tap **Start Listening**. Chip: **LISTENING — DEMO (MIC OFF)**. Sample lines change. Mic stays off.
3. Tap **Choose engine**. Confirm three engines: sample demo, several speakers, and live on-device (Ready or **NOT YET AVAILABLE**). There must be no Google/cloud option. Select **Demo — several speakers**. Start Listening again. Read **Speaker 1 / 2 / 3**. Labels must not be personal names.
4. Tap **Teach me this**. One help card appears above the captions and says it is a local mock.
5. Tap **Test voice** until **Hide help**. The card goes away.
6. **More → Start session**, **Show session**. Confirm lines appear in the session list. **Stop session**, then **Delete session** and **Delete saved transcript**.
7. You must never have been asked for the microphone.

**Pass** if demo captions work in the glasses, teach/hide works, session delete works, and the mic stays off.

## B. Live on-device speech (talking)

Only if the banner says the on-device engine is ready. If it says **UNAVAILABLE**, skip this section. Do **not** look for a cloud-speech mode — there isn’t one.

1. **Choose engine → Live — on-device speech**. If the row says **NOT YET AVAILABLE**, tap Start Listening anyway: you must **not** get a microphone prompt, and the banner must say the engine is unavailable. Then go back to sample demo.
2. If it says **Available**, tap **Start Listening**. The microphone permission must appear **now**, not earlier.
3. Allow it. Chip: **LISTENING — ON-DEVICE**, **Mic: on**.
4. Speak a short sentence. A caption should appear (partials allowed).
5. Speakers stay **Speaker 1 / 2**. The **Speaker uncertain** note must be visible. The app must not invent a name.
6. Tap **Stop Listening**. Mic off.

If permission is denied, live mode must not listen. Demo modes must still work.

## C. Glasses-on check

Wear the glasses on default demo mode.

- Can you read **ChipOS AR — Caption Lens** low in view?
- Tap **Teach me this**. Does the card stay out of the centre?

Write phone model, Android version, and whether on-device speech was available.

## Out of scope

- Cloud speech
- Face recognition
- Sound Compass
- Spatial Awareness Assist
- 6DoF world-locked captions
