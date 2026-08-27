package com.chipos.ar.teach

import com.chipos.ar.modules.TeachingOverlayModule

interface TeachingEngine : TeachingOverlayModule {
    fun explain(captionText: String, cloudOptIn: Boolean): TeachingCard
}
