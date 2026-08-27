# Decisions log

| Date | Decision | Reason | Owner |
| --- | --- | --- | --- |
| 2026-08-27 | Target XREAL Air 2 Ultra first | James owns this model; it supports 6DoF tracking | James |
| 2026-08-27 | Caption Lens is the MVP | It is independently useful and testable before advanced sensing | Team |
| 2026-08-27 | Local-first privacy model | Conversations and room maps are sensitive | Team |
| 2026-08-27 | Phase 1 Caption Lens is a native Android 2D app on the companion phone | Official spatial AR is Unity + XREAL SDK 3.1.0; there is no official native spatial SDK for Air 2 Ultra. Native Compose is the smallest buildable baseline and can be shown over USB-C DisplayPort mirroring | Grok (XR/Android) |
| 2026-08-27 | Phase 2 live captions use Android on-device SpeechRecognizer; cloud speech is opt-in; default mode is scripted test | Best realistic local-first path on the verified phone+glasses setup without bundling a model. Default SpeechRecognizer may upload audio, so it is not the default | Grok (accessibility / AI interaction) |
| 2026-08-27 | Live speaker labels are pause-based and always marked uncertain; names only if the wearer types them | No face recognition or stranger identification | Grok (accessibility / AI interaction) |
| 2026-08-27 | Teach me this is a single local mock card until a real model is wired | No caption text or audio is uploaded by default | Grok (accessibility / AI interaction) |
