using NUnit.Framework;
using UnityEngine;
using ChipOSMarkII;

namespace ChipOSMarkII.Tests.EditMode
{
    public sealed class ChipOSAudioCaptionEditModeTests
    {
        [Test]
        public void AudioFrameDefaultValidationWorks()
        {
            var frame = new ChipOSAudioFrame
            {
                PcmData = new byte[320],
                SourceId = "UnitTestAudio"
            };

            frame.Sanitize();

            Assert.That(frame.SampleRateHz, Is.EqualTo(ChipOSAudioFrame.DefaultSampleRateHz));
            Assert.That(frame.Channels, Is.EqualTo(ChipOSAudioFrame.DefaultChannels));
            Assert.That(frame.BitsPerSample, Is.EqualTo(ChipOSAudioFrame.DefaultBitsPerSample));
            Assert.That(frame.IsValid, Is.True);
            Assert.That(frame.GetCompactSummary(), Is.Not.Empty);
        }

        [Test]
        public void SimulatedAudioSourceStartsStopsAndResets()
        {
            var source = new SimulatedAudioSource();

            Assert.That(source.State, Is.EqualTo(ChipOSAudioSourceState.Stopped));

            source.StartSource();
            Assert.That(source.State, Is.EqualTo(ChipOSAudioSourceState.Running));
            Assert.That(source.IsSourceReady, Is.True);

            source.StopSource();
            Assert.That(source.State, Is.EqualTo(ChipOSAudioSourceState.Stopped));

            source.ResetSource();
            Assert.That(source.Diagnostics.framesProduced, Is.EqualTo(0));
            Assert.That(source.Diagnostics.droppedFrames, Is.EqualTo(0));
        }

        [Test]
        public void SimulatedAudioSourceProducesValidFramesAndDiagnostics()
        {
            var source = new SimulatedAudioSource();
            ChipOSAudioFrame capturedFrame = null;
            source.AudioFrameProduced += frame => capturedFrame = frame;

            source.StartSource();
            source.Tick(0.3f);

            Assert.That(capturedFrame, Is.Not.Null);
            Assert.That(capturedFrame.IsValid, Is.True);
            Assert.That(capturedFrame.SampleRateHz, Is.EqualTo(16000));
            Assert.That(capturedFrame.Channels, Is.EqualTo(1));
            Assert.That(capturedFrame.BitsPerSample, Is.EqualTo(16));
            Assert.That(source.Diagnostics.framesProduced, Is.GreaterThan(0));
            Assert.That(source.Diagnostics.latestError, Is.Empty);
        }

        [Test]
        public void SimulatedAudioSourceDoesNotRequireMicrophonePermission()
        {
            var source = new SimulatedAudioSource();

            source.StartSource();
            source.Tick(0.3f);

            Assert.That(source.SourceName, Does.Contain("Simulated"));
            Assert.That(source.Diagnostics.latestError, Is.Empty);
            Assert.That(source.Diagnostics.GetDetailedSummary(), Does.Not.Contain("RECORD_AUDIO"));
        }

        [Test]
        public void CaptionServiceConsumesSpeechLikeFramesAndProducesMockCaptions()
        {
            var source = new SimulatedAudioSource();
            var bus = new ChipOSEventBus();
            var service = new ChipOSCaptionService();
            ChipOSCaptionEvent capturedCaption = null;
            service.CaptionProduced += caption => capturedCaption = caption;
            service.Bind(source, bus);

            source.StartSource();
            source.Tick(0.3f);

            Assert.That(service.Diagnostics.framesConsumed, Is.GreaterThan(0));
            Assert.That(service.Diagnostics.captionsProduced, Is.GreaterThan(0));
            Assert.That(capturedCaption, Is.Not.Null);
            Assert.That(capturedCaption.GetCompactSummary(), Is.Not.Empty);
        }

        [Test]
        public void CaptionServiceDoesNotSpamCaptionsForSilence()
        {
            var service = new ChipOSCaptionService();
            var silentFrame = new ChipOSAudioFrame(
                new byte[320],
                16000,
                1,
                16,
                1,
                0.1d,
                "SilentTest",
                0f,
                false);

            service.ConsumeFrame(silentFrame);

            Assert.That(service.Diagnostics.framesConsumed, Is.EqualTo(1));
            Assert.That(service.Diagnostics.captionsProduced, Is.EqualTo(0));
        }

        [Test]
        public void CaptionEventSummaryIsNotEmpty()
        {
            var captionEvent = new ChipOSCaptionEvent(
                "Caption frame received",
                0.82f,
                1d,
                1.25d,
                "UnitTestAudio",
                true,
                false,
                12);

            Assert.That(captionEvent.GetCompactSummary(), Is.Not.Empty);
            Assert.That(captionEvent.GetCompactSummary(), Does.Contain("Caption frame received"));
        }

        [Test]
        public void ManualMockCaptionUpdatesDiagnostics()
        {
            var service = new ChipOSCaptionService();
            ChipOSCaptionEvent capturedCaption = null;
            service.CaptionProduced += caption => capturedCaption = caption;

            var manualCaption = service.EmitManualMockCaption("Manual mock caption emitted");

            Assert.That(manualCaption, Is.Not.Null);
            Assert.That(capturedCaption, Is.SameAs(manualCaption));
            Assert.That(service.Diagnostics.captionsProduced, Is.EqualTo(1));
            Assert.That(service.Diagnostics.lastCaption, Is.EqualTo("Manual mock caption emitted"));
            Assert.That(service.Diagnostics.lastConfidence, Is.EqualTo(1f));
        }

        [Test]
        public void RuntimeRegistersAudioAndCaptionServices()
        {
            var root = new GameObject("ChipOSAudioRuntimeTest");

            try
            {
                var app = root.AddComponent<ChipOSApp>();
                var runtime = root.AddComponent<ChipOSRuntime>();
                var layoutManager = root.AddComponent<LayoutManager>();
                var inputRouter = root.AddComponent<InputRouter>();

                layoutManager.Bind(app);
                inputRouter.Bind(app);
                runtime.Bind(app, layoutManager, inputRouter);

                Assert.That(runtime.Services.Get<IChipOSAudioSource>(), Is.Not.Null);
                Assert.That(runtime.Services.Get<SimulatedAudioSource>(), Is.Not.Null);
                Assert.That(runtime.Services.Get<ChipOSCaptionService>(), Is.Not.Null);
                Assert.That(runtime.AudioSource, Is.Not.Null);
                Assert.That(runtime.CaptionService, Is.Not.Null);
                Assert.That(runtime.AudioSource.SourceName, Does.Contain("Simulated"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DebugPanelReadsAudioAndCaptionDiagnosticsSafely()
        {
            var root = new GameObject("ChipOSDebugPanelAudioTest");
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var safeAreaRoot = new GameObject("SafeAreaRoot", typeof(RectTransform));
            var panelObject = new GameObject("DebugPanel", typeof(DebugPanel));

            try
            {
                var app = root.AddComponent<ChipOSApp>();
                var runtime = root.AddComponent<ChipOSRuntime>();
                var layoutManager = root.AddComponent<LayoutManager>();
                var inputRouter = root.AddComponent<InputRouter>();
                safeAreaRoot.transform.SetParent(canvasObject.transform, false);

                layoutManager.Bind(app);
                inputRouter.Bind(app);
                runtime.Bind(app, layoutManager, inputRouter);

                var debugPanel = panelObject.GetComponent<DebugPanel>();
                debugPanel.InitializeModule(app, safeAreaRoot.transform);
                debugPanel.HandleStateChanged(app.State);

                Assert.That(debugPanel.ValidatePanel(), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
                Object.DestroyImmediate(safeAreaRoot);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CaptionPanelCanExistWithoutCaptionService()
        {
            var root = new GameObject("ChipOSCaptionPanelNoServiceTest");
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var safeAreaRoot = new GameObject("SafeAreaRoot", typeof(RectTransform));
            var panelObject = new GameObject("CaptionPanel", typeof(CaptionPanel));

            try
            {
                var app = root.AddComponent<ChipOSApp>();
                safeAreaRoot.transform.SetParent(canvasObject.transform, false);

                var captionPanel = panelObject.GetComponent<CaptionPanel>();
                captionPanel.InitializeModule(app, safeAreaRoot.transform);
                captionPanel.HandleStateChanged(app.State);

                Assert.That(captionPanel.ValidatePanel(), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
                Object.DestroyImmediate(safeAreaRoot);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(root);
            }
        }
    }
}
