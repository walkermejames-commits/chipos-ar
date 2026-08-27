package com.chipos.ar.modules

/**
 * Extension seams for later ChipOS AR work. None of these are implemented
 * in Phase 1. Caption Lens talks only to [com.chipos.ar.caption.CaptionSource].
 */

/** Live speech-to-text. Must stay on-device unless the wearer opts into a named cloud engine. */
interface SpeechToTextModule

/** Speaker labels stay "Speaker 1", "Speaker 2" until the wearer names them. */
interface SpeakerDiarizationModule

/** On-demand teaching overlay. Must state uncertainty. Not wired. */
interface TeachingOverlayModule

/** Detected-sound cues. Direction only when a sensor path is verified. */
interface SoundMappingModule

/**
 * Spatial-awareness research using Air 2 Ultra 6DoF tracking and any later
 * validated external depth hardware. Assistive cues only. Not a navigation
 * or balance product.
 */
interface SpatialAwarenessModule
