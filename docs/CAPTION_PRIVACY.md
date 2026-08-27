# Caption privacy

ChipOS AR captions are conversations. This file is the rule sheet for audio, transcripts, and speaker labels.

It is not a medical, legal, or surveillance product.

## Defaults

| Setting | Default |
| --- | --- |
| Mode | **Demo — sample captions** (Phase 1 path, microphone off) |
| Microphone | Off until **Start Listening** in **Live — on-device speech** |
| Transcript session | Off |
| Cloud speech | **Not implemented. No fallback.** |
| Face recognition | Not present |
| Naming strangers | Forbidden. Labels stay Speaker 1, Speaker 2 until the wearer types a name |

Listening state is always on screen.

## What this app is allowed to do

- Open the phone microphone only after **Start Listening** (or the “start captions” voice command) in **Live — on-device speech**.
- Use Android’s on-device `SpeechRecognizer.createOnDeviceSpeechRecognizer` when the OS reports it available.
- If that engine is missing, **refuse to start live captions**, keep the microphone off, and show **Engine: UNAVAILABLE**. It must not start the cloud recognizer.
- Hold a local transcript session in memory after **Start session**.
- Write `files/transcripts/saved-session.txt` only when **Stop session** runs with lines present.
- Delete the current session and delete the saved file when asked.
- Let the wearer type a name for Speaker N.

## What this app must not do

- Start listening on launch.
- Request the microphone for demo modes.
- Silently fall back to Google/cloud speech.
- Upload audio, transcripts, or images.
- Guess a person’s name, use a face, or match a voiceprint to an identity database.
- Hide the listening indicator.

There is no `INTERNET` permission.

## Speaker labels

Until the wearer names a slot, the only allowed labels are **Speaker 1**, **Speaker 2**, and so on.

Live diarization is pause-based turn taking. It is a guess. Live mode must show **Speaker uncertain**. Demo speakers are scripted roles, not identified people.

## Transcripts

- Disabled by default.
- **Start session** / **Stop session** control capture.
- **Show session** lists the current in-memory lines.
- **Stop session** writes the local file if the session is not empty.
- **Delete session** clears memory.
- **Delete saved transcript** removes the file.

## Glasses microphones

Air 2 Ultra has two built-in microphones. Whether Android uses those or the phone mic is still unknown (`docs/HARDWARE_CAPABILITIES.md`). This build does not claim glasses-mic capture.
