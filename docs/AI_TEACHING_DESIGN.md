# AI teaching design

“Teach me this” is an on-demand help card, not a continuous tutor and not a see-through AI that watches the room.

ChipOS AR is not a medical, navigation, or balance product. Teaching copy must not pretend otherwise.

## What the wearer gets

One short card, shown only after:

- the **Teach me this** button, or
- the voice command **“teach me this”**

The card sits just above the caption panel, still in the lower part of the view. It must not cover the centre of vision.

**Hide help** (button or voice) dismisses it. It does not linger after Stop Listening unless the wearer left it open.

## What the card contains

1. Title: **Teach me this**
2. Two to four plain sentences
3. The caption line it used
4. An uncertainty line that says this is a guess and whether anything was uploaded

No extra panels, no stacked tips, no auto-play.

## Phase 2 engine

There is no bundled on-device LLM yet, and no live cloud model.

`MockTeachingEngine` reads the last caption and returns a canned brief explanation (travel board, hard-to-read print, a mentioned time, or a generic “repeat the concrete nouns” fallback).

If **Cloud AI** is opted in, the card still stays local and states: *Cloud AI is switched on, but no remote model is connected yet, so this card is still a local mock. Nothing was uploaded.*

That keeps the opt-in honest without quietly sending speech.

## Later live model (not this build)

When a real engine is wired, the same card shape stays. Rules:

- Default remains local-first.
- Cloud stays opt-in and visible.
- Do not send audio. Send only the short caption text the wearer already saw, and only after opt-in.
- Refuse unsafe instructions. Say when the model cannot see the object.
- Air 2 Ultra has **no RGB camera**, so the engine must not claim to “look at” the scene unless a verified camera path exists.
- Keep one next step, not a lecture.

## Voice commands that belong to teaching

| Phrase | Action |
| --- | --- |
| teach me this | Open the card for the current caption |
| hide help | Dismiss the card |

Related caption commands live in the same parser (`VoiceCommandParser`): start captions, stop captions, make it bigger, move subtitles down.

Commands are matched as whole utterances so ordinary conversation is less likely to fire them. In Test mode, **Test voice** runs each command in turn without speaking.
