# ChipOS AR

An accessibility-first augmented-reality environment for **XREAL Air 2 Ultra** glasses.

## First prototype

The first usable release is **Caption Lens**: live captions visible in the glasses, clear listening controls, adjustable presentation, and local-first transcript handling.

## Product direction

ChipOS AR will grow in careful stages:

1. Live captions and optional speaker labels.
2. An on-demand AI teaching overlay for objects, documents, and practical tasks.
3. A sound-awareness compass that indicates detected sounds and, only where sensors allow, an estimated direction.
4. Experimental spatial-awareness research using the Air 2 Ultra's 6DoF tracking and any validated companion/external depth hardware.

It is not a medical device, navigation system, or replacement for ordinary mobility and safety judgement.

## Principles

- Local processing and explicit consent by default.
- The wearer can always see when microphones or recording are active.
- Every feature reports uncertainty instead of guessing.
- Information stays readable and out of the centre of vision.
- Features downgrade safely when hardware capability, tracking, light, or confidence is inadequate.

## Team

- James — founder, product owner, and real-world tester.
- Grok 1 — XR/Android platform engineering.
- Grok 2 — accessibility, captions, and AI interaction design.
- Grok 3 — spatial sound, sensing, calibration, and safety engineering.
- Codex — systems architecture, code review, documentation, and delivery discipline.

## Planned structure

```text
android/       Android/XREAL app
docs/          product, hardware, privacy, and test notes
prototypes/    isolated experiments and simulators
```

## Safety and privacy

No audio, transcripts, room maps, images, or cloud processing are enabled without a clear user choice. Speaker labels are provisional unless a user has explicitly named them. Spatial and sound cues are assistive only.
