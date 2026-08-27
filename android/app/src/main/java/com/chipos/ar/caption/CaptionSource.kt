package com.chipos.ar.caption

import kotlinx.coroutines.flow.Flow

/**
 * Produces caption lines for Caption Lens.
 *
 * Implementations must:
 * - never start capturing audio until [start] is called after an explicit
 *   wearer action
 * - never send audio to a network
 * - expose [recordsAudio] honestly so the UI can show listening state
 */
interface CaptionSource {
    val id: String
    val displayName: String
    val recordsAudio: Boolean

    fun captions(): Flow<CaptionLine>

    suspend fun start()

    suspend fun stop()
}
