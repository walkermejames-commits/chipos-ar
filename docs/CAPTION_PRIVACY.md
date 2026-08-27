# Caption privacy

ChipOS AR captions are conversations. This file is the rule sheet for audio, transcripts, speaker labels, and cloud features.

It is not a medical, legal, or surveillance product.

## Defaults

| Setting | Default |
| --- | --- |
| Microphone | Off until **Start Listening** in a live mode |
| Test / scripted captions | No microphone, no recording |
| Transcript saving | Off |
| Cloud speech | Off |
| Cloud AI | Off |
| Face recognition | Not present |
| Naming strangers | Forbidden. Labels stay Speaker 1, Speaker 2 until the wearer types a name |

Listening state is always on screen. If the green/orange chip is missing, that is a bug.

## What this app is allowed to do

- Open the phone microphone only after an explicit Start Listening tap (or the “start captions” voice command) in **Live — on-device speech** or **Live — cloud speech**.
- Prefer Android’s on-device `SpeechRecognizer` (`createOnDeviceSpeechRecognizer`) so audio stays on the phone when the OS supports it.
- Keep an in-memory session of caption lines while the app is running.
- Write a transcript file under the app’s private storage **only** if **Save transcript** is on.
- Delete the current session and delete the saved file when the wearer asks.
- Let the wearer type a name for Speaker N. That name is stored on the phone.

## What this app must not do

- Start listening on launch.
- Record in the background after Stop Listening.
- Upload audio or transcripts because a feature is “easier” that way.
- Turn on cloud speech or cloud AI without a visible opt-in control.
- Guess a person’s legal name, use a face, or match a voiceprint to an identity database.
- Hide the listening indicator.

There is no `INTERNET` permission in the Phase 2 APK. Cloud speech, if opted in, uses the system recognizer process (often Google). The orange **LISTENING — CLOUD SPEECH** chip must stay visible for that mode.

## Speaker labels

Until the wearer names a slot, the only allowed labels are **Speaker 1**, **Speaker 2**, and so on.

Live diarization in this build is pause-based turn taking. It is a guess. The UI must show **Speaker guess uncertain** in live modes. Test-mode speakers are scripted roles, not identified people.

## Transcripts

- Off: nothing is written to disk.
- On: lines are written to `files/transcripts/saved-session.txt` on this phone.
- **Delete session** clears the on-screen session.
- **Delete saved transcript** removes the file.

Uninstalling the app removes app-private files.

## Cloud opt-in wording

Use this meaning, not weaker language:

- **Cloud speech: on** — audio may leave this phone through the system speech service.
- **Cloud AI: on** — the wearer is willing to send caption text to a remote model later. Phase 2 still does **not** send it; Teach me this stays a local mock and says so.

## Glasses microphones

Air 2 Ultra has two built-in microphones. Whether Android uses those mics or the phone mic when the glasses are plugged in is still an unknown (see `docs/HARDWARE_CAPABILITIES.md`). Phase 2 does not claim glasses-mic capture.
