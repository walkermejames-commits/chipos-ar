package com.chipos.ar.xreal

/**
 * How this APK relates to official XREAL development.
 *
 * Official spatial AR for Air 2 Ultra is Unity + XREAL SDK 3.1.0, launched
 * from ControlGlasses (phone) or MyGlasses (Beam Pro). There is no official
 * native Android spatial SDK for these glasses as of 2026-08-27.
 *
 * Phase 1 is the closest verified native baseline: a regular Android activity
 * shown on the companion phone. When the glasses are plugged in over USB-C
 * DisplayPort, that activity is expected to appear as a 2D image in the
 * glasses. That path is "screen mirroring / general use", not 6DoF AR.
 *
 * See docs/HARDWARE_CAPABILITIES.md.
 */
object XrealBaseline {
    const val OFFICIAL_SDK = "XREAL SDK 3.1.0 for Unity"
    const val PHASE_1_PATH = "Native Android 2D activity on companion phone"
    const val DEVICE_CATEGORY_REALITY = "XREAL_DEVICE_CATEGORY_REALITY"
}
