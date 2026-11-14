# Offline live speaker diarization

Caption Lens has two live engines. **Live — on-device speech** uses Android's local speech recognizer and does not claim to identify speakers. **Live — offline ASR + acoustic speakers** uses sherpa-onnx 1.13.8 and a single `AudioRecord` stream for both English streaming captions and acoustic diarization. No audio is uploaded; the app still has no `INTERNET` permission.

The sherpa mode bundles its runtime and models in the APK, so it can start without a model download or network access. It is arm64-v8a only. The current model assets total about 73 MB before APK compression: the ASR encoder, decoder, joiner and tokens; pyannote segmentation; and the English 3D-Speaker ERes2Net embedding model.

Speaker analysis runs on a rolling 12-second waveform every 4 seconds. That means the first acoustic labels cannot appear until the first window has accumulated, and labels may revise recent captions after analysis. Segment embeddings are matched with cosine similarity against a session-only in-memory speaker bank (initial threshold 0.68). The bank is erased when listening stops. Labels stay anonymous (`Speaker A`, `Speaker B`, …); names are disabled for this mode because user-entered names in the existing app are stored across sessions. The numeric speaker IDs and their UI colors are stable for the current session only; the acoustic identity is not saved between sessions.

The live glasses view places recent captions in a full-width bottom subtitle band and hides the larger settings dock while the microphone is active. Text cannot be anchored next to an actual person because this prototype does not track speaker positions. Audio capture uses Android's system-selected microphone route, attempts platform automatic gain control when available, and applies a user-adjustable digital gain from 1.0× to 3.0×. Higher gain can also amplify room noise; the status banner reports the routed input device.

This is an experimental prototype, not a verified theatre-ready diarizer. Short lines, music, reverberation, distance, overlapping dialogue, and similar voices can cause missed or merged speakers. The app should show an unknown speaker when it cannot form a usable embedding. No real-device latency, CPU, thermal, or accuracy measurements have been made yet. Test with a borrowed arm64 phone before relying on labels in a performance.

## Bundled components

- sherpa-onnx Android runtime 1.13.8 (Apache-2.0); release AAR SHA-256: `633c24321e06b1fe79feafa03ea16cbc0f8a286641e2da3559bac91bdb13bd96`.
- Streaming Zipformer English 20M model (Apache-2.0), official sherpa-onnx `asr-models` release.
- Pyannote segmentation 3.0 INT8, with its upstream MIT license included at `android/app/src/main/assets/models/diarization/SEGMENTATION-LICENSE`.
- English 3D-Speaker ERes2Net VoxCeleb embedding model. The downloaded file SHA-256 is `c59158379255ad66e161679cca6af8d52d51e389e3224ab7d7a7baae295c2db5`. The sherpa-linked model repository does not include a model card/license statement; confirm the upstream model terms before redistributing this APK publicly.

The source/runtime reference is pinned to the official [sherpa-onnx v1.13.8 release](https://github.com/k2-fsa/sherpa-onnx/releases/tag/v1.13.8). The ASR and diarization model files come from sherpa-onnx's official model releases and linked Hugging Face model repository.
