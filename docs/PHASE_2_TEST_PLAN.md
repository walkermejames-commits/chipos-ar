# Phase 2 test plan — useful Caption Lens

Do Phase 1 first if you have not already seen the panel in the glasses (`docs/PHASE_1_TEST_PLAN.md`). Phase 2 adds live speech, speakers, transcripts, Teach me this, and voice commands.

Sit for the first pass. This is not a mobility test.

## You need

- XREAL Air 2 Ultra on USB-C DisplayPort
- Companion Android phone with the **0.2.0** debug APK (`com.chipos.ar.debug`)
- A quiet indoor room
- Optional: a second person talking, only for the live-mic pass

## A. Simulation (no talking, no microphone)

Default mode is **Test — several speakers**.

1. Open the app. Confirm **NOT LISTENING** and **Mic: off**.
2. Tap **Start Listening**. Chip: **LISTENING — TEST MODE**. Still **Mic: off**.
3. Read three different labels: **Speaker 1**, **Speaker 2**, **Speaker 3** on a short platform/train script. The note must say these are test labels, not real identities.
4. Tap **Teach me this**. A single help card appears above the captions. It must say it is a local mock.
5. Say nothing. Tap **Test voice** until it runs **Hide help**. The card goes away.
6. Tap **Test voice** through **Make it bigger** and **Move subtitles down**. Text grows; the panel moves lower.
7. Turn **Save transcript** on (under **More**). Wait for a few lines. Turn it off. Tap **Delete session**, then **Delete saved transcript**.
8. Confirm you were never asked for the microphone.

**Pass** if you can follow a three-speaker script in the glasses, teach/hide works, and the mic stays off.

## B. Live on-device speech (talking)

Only if the banner says **On-device speech: available**.

1. Tap **Mode** until **Live — on-device speech**.
2. Tap **Start Listening**. Allow microphone if asked.
3. Chip must read **LISTENING — ON-DEVICE** and **Mic: on**.
4. Speak a short sentence. A caption should appear with low delay (partials allowed).
5. If Speakers are on, labels stay **Speaker 1** / **Speaker 2** and the orange/amber note **Speaker guess uncertain** is visible. The app must not invent a name.
6. Optionally type a name with **Name this speaker**. That is the only way a personal name appears.
7. Tap **Stop Listening**. Mic off. Chip **STOPPED**.

If on-device speech is **not available**, do not use Live on-device. Stay in Test mode, or read section C.

## C. Cloud speech (optional, opt-in only)

Skip unless you intend to send audio to the system cloud recognizer.

1. **More → Cloud speech: ON**.
2. Mode **Live — cloud speech**.
3. Start Listening. Chip must be **orange** and say **LISTENING — CLOUD SPEECH** and that audio may leave the phone.
4. Stop Listening. Turn **Cloud speech** back off.

## D. Glasses-on check (the one that matters)

Wear the glasses with Test mode running.

- Can you read Speaker 1 / 2 / 3 without the card sitting in the middle of your view?
- After **Teach me this**, is the help card still low, or does it block faces and the floor ahead?
- Use **Move subtitles down** / **Test voice** if it is too high.

Write phone model, Android version, whether on-device speech was available, and whether glasses mics or the phone mic seemed to be in use.

## Out of scope

- Face recognition
- True voice biometric diarization
- A live teaching model
- 6DoF world-locked captions
- Outdoor navigation
