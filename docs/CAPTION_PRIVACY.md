# Caption privacy

ChipOS AR captions are conversations. This file is the rule sheet for audio, transcripts, and speaker labels.

It is not a medical, legal, or surveillance product.

## Defaults

| Setting | Default |
| --- | --- |
| Default mode | **Live — offline ASR + acoustic speakers**, with the microphone off until **Start live listening** is tapped and permission is granted. Demo modes remain available in the engine picker. |
| Microphone | Off until **Start live listening** in a live engine |
| Transcript session | Off |
| Cloud speech | **Not implemented. No fallback.** |
| Face recognition | Not present |
| Naming strangers | Forbidden. Labels stay Speaker 1, Speaker 2 until the wearer types a name |

Listening state is always on screen.

## What this app is allowed to do

- Open the phone microphone only after **Start live listening** (or the “start captions” voice command) in a live engine.
- In the default **Live — offline ASR + acoustic speakers** mode, process audio locally with bundled sherpa-onnx models. Audio is kept in a bounded in-memory rolling buffer and is not uploaded.
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

The bundled Sherpa engine uses acoustic segmentation and speaker embeddings to cluster anonymous speakers. Labels are provisional, may arrive after the words, and may change or merge. Other engines may use uncertain turn-taking labels. Demo speakers are scripted roles, not identified people.

## Floating captions and studio

The Float action explains the overlay before requesting display-over-other-apps and microphone access. A microphone foreground service keeps listening after leaving the app only when the wearer explicitly starts this mode. Its visible overlay and persistent notification provide Stop controls. It does not restart automatically and does not save audio or transcripts. In-app listening stops when the activity leaves the foreground. Audio buffers are cleared when the speech source stops.

Studio project cards are stored privately on this device and can be edited or deleted. No cloud sync, camera capture, room scanning or public project view is implemented. Avoid displaying private notes when mirroring the screen to others.

## Transcripts

- Disabled by default.
- **Start session** / **Stop session** control capture.
- **Show session** lists the current in-memory lines.
- **Stop session** writes the local file if the session is not empty.
- **Delete session** clears memory.
- **Delete saved transcript** removes the file.

## Glasses microphones

Air 2 Ultra has two built-in microphones. Whether Android uses those or the phone mic is still unknown (`docs/HARDWARE_CAPABILITIES.md`). This build does not claim glasses-mic capture.
