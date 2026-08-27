package com.chipos.ar.teach

data class TeachingCard(
    val title: String,
    val body: String,
    val sourceLine: String,
    val isMock: Boolean,
    val uncertainty: String,
)
