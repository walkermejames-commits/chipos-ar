plugins { id("com.android.library") }

android {
    namespace = "com.chipos.speech"
    compileSdk = 36
    defaultConfig { minSdk = 31 }
}

dependencies {
    // Reuse the exact sherpa-onnx runtime shipped by the native Studio app.
    compileOnly(files("../../../android/app/libs/sherpa-onnx-1.13.8.aar"))
    // sherpa-onnx's Java API uses Kotlin runtime classes. Unity imports this
    // AAR directly and does not resolve its Maven runtime dependencies, so
    // stage kotlin-stdlib beside the bridge AAR as a Unity Android plugin.
    implementation("org.jetbrains.kotlin:kotlin-stdlib:2.0.21")
}

tasks.register<Copy>("stageUnityRuntimeDependencies") {
    from(configurations.named("releaseRuntimeClasspath")) {
        include("kotlin-stdlib-*.jar")
    }
    into(rootProject.projectDir.resolve("../Assets/Plugins/Android"))
}

tasks.register<Copy>("stageUnityBridgeAar") {
    dependsOn("bundleReleaseAar")
    from(layout.buildDirectory.file("outputs/aar/speechbridge-release.aar"))
    into(rootProject.projectDir.resolve("../Assets/Plugins/Android"))
    rename { "chipos-speechbridge-release.aar" }
}
