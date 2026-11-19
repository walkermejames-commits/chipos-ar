using System.Collections;
using System.Collections.Generic;
using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;
using Unity.XR.XREAL;

namespace ChipOSMarkII
{
    /// <summary>
    /// XREAL/AR Foundation spatial path. Room mapping starts only on an explicit user action.
    /// </summary>
    public sealed class SpatialStudioController : MonoBehaviour
    {
        private const string AnchorKeyPrefix = "ChipOS.Spatial.Anchor.";
        private const string CaptionAnchorTitle = "Live captions";
        private readonly List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
        private readonly Dictionary<string, ARAnchor> liveAnchors = new Dictionary<string, ARAnchor>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> anchorViews = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        private XROrigin origin;
        private ARSession session;
        private ARPlaneManager planeManager;
        private ARRaycastManager raycastManager;
        private ARAnchorManager anchorManager;
        private ARMeshManager meshManager;
        private ARCameraManager cameraManager;
        private AROcclusionManager occlusionManager;
        private Camera xrCamera;
        private Canvas studioCanvas;
        private XRHandSubsystem handSubsystem;
        private string focusedArea = string.Empty;
        private string focusedNote = string.Empty;
        private bool active;
        private bool trackingStartRequested;
        private bool previousPinch;
        private bool loadingSavedAnchors;
        private bool cameraPermissionRequestPending;
        private bool hasAttemptedRoomTracking;
        private bool meshOverlayMaterialAvailable;
        private float nextProviderStatusRefresh;
        private Coroutine trackingStartupTimeout;
        private Coroutine providerInitialization;
        private Coroutine presentationInitialization;
        private string liveCaptionText = "Microphone off · start live captions on the phone.";
        private string liveSpeakerStatus = "Speaker analysis is off.";

        private void Start()
        {
            xrCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            EnsureTrackedHeadCamera(xrCamera);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsAndroidEmulator())
            {
                SetStatus("EMULATOR 2D DISPLAY · This AVD cannot show the real room, XREAL see-through, camera mapping, or room anchors. Test those on the S26 with Air 2 Ultra; launch from ControlGlasses.");
                return;
            }
#endif
            TryStartXrealPresentation();
        }

        /// <summary>
        /// Creates the pose input used by both XREAL head tracking and the phone AR fallback.
        /// The XR display must follow the wearer's head even while room mapping is off.
        /// </summary>
        public static void EnsureTrackedHeadCamera(Camera camera)
        {
            if (camera == null) return;

            var driver = camera.GetComponent<TrackedPoseDriver>();
            if (driver == null) driver = camera.gameObject.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.ignoreTrackingState = true;

            if (driver.positionInput.action == null)
            {
                var position = new InputAction("Head Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
                position.AddBinding("<HandheldARInputDevice>/devicePosition");
                driver.positionInput = new InputActionProperty(position);
            }

            if (driver.rotationInput.action == null)
            {
                var rotation = new InputAction("Head Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
                rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");
                driver.rotationInput = new InputActionProperty(rotation);
            }
        }

        private void TryStartXrealPresentation()
        {
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null || manager.activeLoader != null) return;

            // Do not query XREALPlugin.GetDeviceCategory() before the loader starts:
            // the native plugin reports NotFindRuntime in that state, which used to
            // short-circuit startup before the loader could discover connected glasses.
            // XR Management probes the configured providers; the phone remains usable
            // if the XREAL runtime is absent.
            SetStatus("Checking the XREAL display runtime. Room mapping remains off until you start camera mapping.");
            presentationInitialization = StartCoroutine(InitializeXrealPresentation(manager));
        }

        private IEnumerator InitializeXrealPresentation(XRManagerSettings manager)
        {
            yield return manager.InitializeLoader();
            presentationInitialization = null;

            if (manager.activeLoader is XREALXRLoader)
            {
                try
                {
                    manager.StartSubsystems();
                    EnsureOpticalSeeThrough(manager.activeLoader);
                    if (trackingStartRequested)
                    {
                        StartTrackingWithActiveProvider();
                    }
                    else
                    {
                        SetStatus("XREAL see-through display and head tracking active · room mapping is off. Tap Start camera mapping to scan and place room anchors.");
                    }
                    yield break;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ChipOS XR] XREAL display startup failed: " + exception.GetType().Name);
                    try { manager.StopSubsystems(); } catch { }
                    manager.DeinitializeLoader();
                }
            }
            else if (manager.activeLoader != null)
            {
                // A phone ARCore loader is for the explicit mapping action only.
                manager.DeinitializeLoader();
            }

            if (trackingStartRequested)
                providerInitialization = StartCoroutine(InitializeProviderAndBeginTracking(manager));
            else
                RefreshIdleProviderStatus();
        }

        public event Action<string> AreaSelected;

        public bool IsRoomAnchoringActive => active;
        public bool IsRoomAnchoringStarting => trackingStartRequested && !active;
        public bool HasCaptionPanelAnchor => liveAnchors.ContainsKey(CaptionAnchorTitle);
        public bool IsXrealRuntimeActive => XRGeneralSettings.Instance != null &&
            XRGeneralSettings.Instance.Manager != null && XRGeneralSettings.Instance.Manager.activeLoader is XREALXRLoader;
        public bool IsPhoneArRuntimeActive => XRGeneralSettings.Instance != null &&
            XRGeneralSettings.Instance.Manager != null && XRGeneralSettings.Instance.Manager.activeLoader != null && !IsXrealRuntimeActive;
        public string Status { get; private set; } = "SPATIAL MAPPING OFF · Camera and room tracking are off.";

        public void ReportPermissionStatus(string status)
        {
            SetStatus(status);
        }

        public void SetFocusedArea(string title, string note)
        {
            focusedArea = title ?? string.Empty;
            focusedNote = note ?? string.Empty;
            RefreshStatus();
        }

        public void StartRoomAnchoring()
        {
            if (active || trackingStartRequested) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsAndroidEmulator())
            {
                hasAttemptedRoomTracking = true;
                SetStatus("SPATIAL MAPPING UNAVAILABLE ON THIS EMULATOR · test the S26 with Air 2 Ultra connected and launch ChipOS from ControlGlasses.");
                return;
            }
#endif
            hasAttemptedRoomTracking = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                if (cameraPermissionRequestPending) return;
                cameraPermissionRequestPending = true;
                SetStatus("Android camera permission is needed for room tracking. On Air 2 Ultra this is the two grayscale mapping cameras, not RGB video.");
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ =>
                {
                    cameraPermissionRequestPending = false;
                    StartRoomAnchoring();
                };
                callbacks.PermissionDenied += _ =>
                {
                    cameraPermissionRequestPending = false;
                    SetStatus("Camera permission denied. Room mapping is off; enable Camera for ChipOS in Android settings to scan the room.");
                };
                callbacks.PermissionDeniedAndDontAskAgain += _ =>
                {
                    cameraPermissionRequestPending = false;
                    SetStatus("Camera permission is blocked. Enable Camera for ChipOS in Android settings before starting a room scan.");
                };
                Permission.RequestUserPermission(Permission.Camera, callbacks);
                return;
            }
#endif
            BeginRoomAnchoring();
        }

        private void BeginRoomAnchoring()
        {
            trackingStartRequested = true;
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null)
            {
                AbortRoomTracking("Unity XR management is unavailable. The phone studio remains available.");
                return;
            }

            if (manager.activeLoader == null)
            {
                if (presentationInitialization != null)
                {
                    SetStatus("Connecting the XREAL display first; camera mapping will start as soon as the spatial provider is ready.");
                    return;
                }
                SetStatus("Starting the room-tracking provider on request. The phone controls stay available.");
                if (providerInitialization != null) StopCoroutine(providerInitialization);
                providerInitialization = StartCoroutine(InitializeProviderAndBeginTracking(manager));
                return;
            }

            StartTrackingWithActiveProvider();
        }

        private IEnumerator InitializeProviderAndBeginTracking(XRManagerSettings manager)
        {
            yield return manager.InitializeLoader();
            providerInitialization = null;
            if (!trackingStartRequested) yield break;
            if (manager.activeLoader == null)
            {
                AbortRoomTracking("No compatible spatial provider could start. Use the phone controls; connect the Air 2 Ultra and open ChipOS from ControlGlasses for XREAL tracking.");
                yield break;
            }

            try
            {
                manager.StartSubsystems();
            }
            catch (Exception exception)
            {
                AbortRoomTracking($"Spatial provider could not start ({exception.GetType().Name}). The phone studio remains available.");
                yield break;
            }

            StartTrackingWithActiveProvider();
        }

        private void StartTrackingWithActiveProvider()
        {
            if (!EnsureSpatialRig()) return;
            if (session == null)
            {
                AbortRoomTracking("No AR session is available. The view-fixed studio remains usable.");
                return;
            }
            var loader = XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null
                ? XRGeneralSettings.Instance.Manager.activeLoader : null;
            var isXreal = loader is XREALXRLoader;
            var cameraBackground = xrCamera.GetComponent<ARCameraBackground>();
            if (cameraBackground != null) cameraBackground.enabled = !isXreal;
            if (isXreal) EnsureOpticalSeeThrough(loader);
            if (!session.enabled) session.enabled = true;
            if (studioCanvas != null)
            {
                studioCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                studioCanvas.worldCamera = xrCamera;
                studioCanvas.planeDistance = 1.6f;
            }
            if (trackingStartupTimeout != null) StopCoroutine(trackingStartupTimeout);
            trackingStartupTimeout = StartCoroutine(ConfirmTrackingStarted());
            StartCoroutine(PrepareRoomTracking());
        }

        public void StopRoomAnchoring()
        {
            active = false;
            trackingStartRequested = false;
            if (providerInitialization != null)
            {
                StopCoroutine(providerInitialization);
                providerInitialization = null;
            }
            if (trackingStartupTimeout != null)
            {
                StopCoroutine(trackingStartupTimeout);
                trackingStartupTimeout = null;
            }
            if (planeManager != null) planeManager.enabled = false;
            if (meshManager != null) meshManager.enabled = false;
            if (session != null) session.enabled = false;
            if (cameraManager != null) cameraManager.enabled = false;
            if (occlusionManager != null) occlusionManager.enabled = false;
            SetMeshRenderersVisible(false);
            var cameraBackground = xrCamera != null ? xrCamera.GetComponent<ARCameraBackground>() : null;
            if (cameraBackground != null) cameraBackground.enabled = false;
            foreach (var item in anchorViews.Values)
                if (item != null) item.SetActive(false);
            if (studioCanvas != null && xrCamera != null)
            {
                studioCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                studioCanvas.worldCamera = xrCamera;
                studioCanvas.planeDistance = 1.6f;
            }
            hasAttemptedRoomTracking = false;
            nextProviderStatusRefresh = 0f;
            SetStatus("Spatial mapping paused. The phone/glasses controls remain available; room cards are hidden.");
        }

        public void PlaceArea(string areaTitle, string note)
        {
            focusedArea = areaTitle ?? string.Empty;
            focusedNote = note ?? string.Empty;
            if (!active || raycastManager == null || xrCamera == null)
            {
                SetStatus("Start AR and open a studio area before placing its card.");
                return;
            }
            if (liveAnchors.ContainsKey(focusedArea))
            {
                if (anchorViews.TryGetValue(focusedArea, out var view) && view != null) view.SetActive(true);
                SetStatus($"{focusedArea} already has an anchor in this session. Its saved position is restored.");
                return;
            }

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (!raycastManager.Raycast(center, raycastHits, TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint))
            {
                SetStatus("No surface is tracked at the view centre yet. Slowly look across a well-lit, textured table or wall, then place again.");
                return;
            }
            var hit = raycastHits[0];
            var surfaceNormal = hit.pose.up.sqrMagnitude > 0.5f ? hit.pose.up : Vector3.up;
            var facing = Vector3.ProjectOnPlane(xrCamera.transform.forward, surfaceNormal).normalized;
            if (facing.sqrMagnitude < 0.01f) facing = Vector3.ProjectOnPlane(xrCamera.transform.up, surfaceNormal).normalized;
            if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
            var panelPose = new Pose(hit.pose.position + surfaceNormal * 0.04f, Quaternion.LookRotation(facing, surfaceNormal));
            CreateAnchorAtPose(panelPose);
        }

        public void SetLiveCaptionText(string text)
        {
            liveCaptionText = string.IsNullOrWhiteSpace(text) ? "Speaker unknown\nWaiting for speech." : text.Trim();
            RefreshCaptionPanelText();
        }

        public void SetSpeakerAnalysisStatus(string status)
        {
            liveSpeakerStatus = string.IsNullOrWhiteSpace(status) ? "Speaker status unavailable." : status.Trim();
            RefreshCaptionPanelText();
        }

        private void RefreshCaptionPanelText()
        {
            if (!anchorViews.TryGetValue(CaptionAnchorTitle, out var view) || view == null) return;
            var label = view.GetComponentInChildren<Text>();
            if (label != null) label.text = "LIVE CAPTIONS\n\n" + liveCaptionText + "\n\n" + liveSpeakerStatus;
        }

        public void PlaceCaptionPanel()
        {
            if (!active || raycastManager == null || xrCamera == null)
            {
                SetStatus("Start AR first. The phone caption panel remains available without spatial tracking.");
                return;
            }
            if (liveAnchors.ContainsKey(CaptionAnchorTitle))
            {
                if (anchorViews.TryGetValue(CaptionAnchorTitle, out var existing) && existing != null) existing.SetActive(true);
                SetStatus("The live caption panel is already attached to its room anchor.");
                return;
            }
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (!raycastManager.Raycast(center, raycastHits, TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint))
            {
                SetStatus("No surface is tracked at the view centre yet. Slowly scan a well-lit, textured surface, then place captions again.");
                return;
            }
            var hit = raycastHits[0];
            var normal = hit.pose.up.sqrMagnitude > 0.5f ? hit.pose.up : Vector3.up;
            var facing = Vector3.ProjectOnPlane(xrCamera.transform.forward, normal).normalized;
            if (facing.sqrMagnitude < 0.01f) facing = Vector3.ProjectOnPlane(xrCamera.transform.up, normal).normalized;
            if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
            focusedArea = CaptionAnchorTitle;
            focusedNote = liveCaptionText;
            CreateAnchorAtPose(new Pose(hit.pose.position + normal * 0.04f, Quaternion.LookRotation(facing, normal)));
        }

        private IEnumerator PrepareRoomTracking()
        {
            var loader = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager.activeLoader : null;
            if (loader == null)
            {
                AbortRoomTracking("No compatible XR provider is active. Install the XREAL app through ControlGlasses and connect the Ultra, or use the view-fixed mode.");
                yield break;
            }

            // XREAL's loader is started by ControlGlasses and does not use ARCore's
            // Android runtime install flow. Only the phone fallback performs that check.
            var isXreal = loader is XREALXRLoader;
            // XREAL plane/mesh tracking uses the glasses' tracking cameras inside
            // the provider. ARCameraManager starts the separate RGB camera stream,
            // which is neither needed for mapping nor used as optical passthrough.
            // Keep RGB capture off on XREAL; the phone ARCore fallback needs it.
            if (cameraManager != null) cameraManager.enabled = !isXreal;
            if (occlusionManager != null) occlusionManager.enabled = !isXreal;
            var cameraBackground = xrCamera != null ? xrCamera.GetComponent<ARCameraBackground>() : null;
            if (cameraBackground != null) cameraBackground.enabled = !isXreal;
            if (isXreal) EnsureOpticalSeeThrough(loader);
            if (!isXreal)
            {
                yield return ARSession.CheckAvailability();
                if (!trackingStartRequested) yield break;
                if (ARSession.state == ARSessionState.Unsupported)
                {
                    AbortRoomTracking("This device/provider does not support AR tracking. The view-fixed studio remains available.");
                    yield break;
                }
                if (ARSession.state == ARSessionState.NeedsInstall)
                {
                    SetStatus("Installing the AR runtime. Follow any Android system prompt.");
                    yield return ARSession.Install();
                    if (!trackingStartRequested) yield break;
                }
                if (ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.NeedsInstall)
                {
                    AbortRoomTracking("AR runtime is unavailable or setup was declined. No room map was created.");
                    yield break;
                }
            }

            try
            {
                if (planeManager != null)
                {
                    planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
                    planeManager.enabled = true;
                }
                if (meshManager != null) meshManager.enabled = true;
                SetMeshRenderersVisible(true);
                if (occlusionManager != null) occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
                handSubsystem = XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRHandSubsystem>();
            }
            catch (Exception exception)
            {
                AbortRoomTracking($"Spatial mapping could not start ({exception.GetType().Name}). The phone studio remains available.");
                yield break;
            }

            active = true;
            SetStatus("AR tracking is starting. Scan the work surface slowly; place the selected area at the view centre. Room data stays on this device/provider.");
            if (!loadingSavedAnchors) StartCoroutine(RestoreSavedAnchors());
        }

        private IEnumerator ConfirmTrackingStarted()
        {
            const float timeoutSeconds = 20f;
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (trackingStartRequested && Time.realtimeSinceStartup < deadline)
            {
                if (active && ARSession.state == ARSessionState.SessionTracking)
                {
                    trackingStartRequested = false;
                    var loader = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager.activeLoader : null;
                    var background = xrCamera != null ? xrCamera.GetComponent<ARCameraBackground>() : null;
                    if (loader is XREALXRLoader)
                    {
                        if (xrCamera != null)
                        {
                            xrCamera.clearFlags = CameraClearFlags.SolidColor;
                            xrCamera.backgroundColor = Color.clear;
                        }
                        if (background != null) background.enabled = false;
                    }
                    else if (background != null) background.enabled = true;
                    trackingStartupTimeout = null;
                    RefreshStatus();
                    yield break;
                }
                if (ARSession.state == ARSessionState.Unsupported)
                {
                    trackingStartupTimeout = null;
                    AbortRoomTracking("Spatial mapping unavailable: this XR provider reports that AR tracking is unsupported. The phone studio remains available.");
                    yield break;
                }
                yield return null;
            }

            trackingStartupTimeout = null;
            if (!trackingStartRequested || (active && ARSession.state == ARSessionState.SessionTracking)) yield break;
            var state = ARSession.state.ToString();
            AbortRoomTracking($"Spatial mapping unavailable: camera and room tracking did not become active within {timeoutSeconds:0} seconds (session: {state}). The phone studio remains available.");
        }

        private void AbortRoomTracking(string message)
        {
            active = false;
            trackingStartRequested = false;
            if (planeManager != null) planeManager.enabled = false;
            if (meshManager != null) meshManager.enabled = false;
            if (session != null) session.enabled = false;
            if (cameraManager != null) cameraManager.enabled = false;
            if (occlusionManager != null) occlusionManager.enabled = false;
            SetMeshRenderersVisible(false);
            var cameraBackground = xrCamera != null ? xrCamera.GetComponent<ARCameraBackground>() : null;
            if (cameraBackground != null) cameraBackground.enabled = false;
            if (studioCanvas != null && xrCamera != null)
            {
                studioCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                studioCanvas.worldCamera = xrCamera;
                studioCanvas.planeDistance = 1.6f;
            }
            SetStatus(message);
        }

        private bool EnsureSpatialRig()
        {
            if (origin != null) return true;
            xrCamera = Camera.main;
            if (xrCamera == null) xrCamera = FindFirstObjectByType<Camera>();
            if (xrCamera == null)
            {
                SetStatus("XR camera is missing. The view-fixed studio remains available.");
                return false;
            }

            studioCanvas = GetComponent<Canvas>();
            if (studioCanvas == null) studioCanvas = FindFirstObjectByType<Canvas>();

            var root = new GameObject("ChipOS XREAL Spatial Origin");
            root.SetActive(false);
            origin = root.AddComponent<XROrigin>();
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 0f;
            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(root.transform, false);
            origin.CameraFloorOffsetObject = cameraOffset;
            origin.Camera = xrCamera;
            xrCamera.transform.SetParent(cameraOffset.transform, false);
            xrCamera.transform.localPosition = Vector3.zero;
            xrCamera.transform.localRotation = Quaternion.identity;
            EnsureTrackedHeadCamera(xrCamera);
            xrCamera.stereoTargetEye = StereoTargetEyeMask.Both;
            cameraManager = xrCamera.gameObject.GetComponent<ARCameraManager>();
            if (cameraManager == null) cameraManager = xrCamera.gameObject.AddComponent<ARCameraManager>();
            var cameraBackground = xrCamera.gameObject.GetComponent<ARCameraBackground>();
            if (cameraBackground == null) cameraBackground = xrCamera.gameObject.AddComponent<ARCameraBackground>();
            cameraBackground.enabled = false;
            occlusionManager = xrCamera.gameObject.GetComponent<AROcclusionManager>();
            if (occlusionManager == null) occlusionManager = xrCamera.gameObject.AddComponent<AROcclusionManager>();
            // AddComponent enables AR Foundation managers immediately. Keep the
            // separate RGB feed closed until the phone ARCore fallback needs it.
            cameraManager.enabled = false;
            occlusionManager.enabled = false;

            var sessionObject = new GameObject("ChipOS AR Session");
            sessionObject.SetActive(false);
            session = sessionObject.AddComponent<ARSession>();
            var featureManagers = new GameObject("AR Feature Managers");
            featureManagers.transform.SetParent(root.transform, false);
            planeManager = featureManagers.AddComponent<ARPlaneManager>();
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            planeManager.planePrefab = CreateSpatialPlanePrefab();
            raycastManager = featureManagers.AddComponent<ARRaycastManager>();
            anchorManager = featureManagers.AddComponent<ARAnchorManager>();
            meshManager = featureManagers.AddComponent<ARMeshManager>();
            meshManager.density = 0.35f;
            meshManager.normals = true;
            meshManager.meshPrefab = CreateSpatialMeshPrefab();
            root.SetActive(true);
            sessionObject.SetActive(true);
            planeManager.enabled = false;
            meshManager.enabled = false;
            return true;
        }

        private MeshFilter CreateSpatialMeshPrefab()
        {
            var prefab = new GameObject("ChipOS Room Mesh Overlay");
            var filter = prefab.AddComponent<MeshFilter>();
            var renderer = prefab.AddComponent<MeshRenderer>();
            renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader != null)
            {
                meshOverlayMaterialAvailable = true;
                var material = new Material(shader) { name = "ChipOS translucent room mesh" };
                var tint = new Color(.12f, .78f, .92f, .16f);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
                if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                renderer.sharedMaterial = material;
            }
            else
            {
                meshOverlayMaterialAvailable = false;
                renderer.enabled = false;
            }
            return filter;
        }

        private GameObject CreateSpatialPlanePrefab()
        {
            var plane = new GameObject("ChipOS Detected Plane", typeof(MeshFilter), typeof(MeshRenderer), typeof(ARPlane), typeof(ARPlaneMeshVisualizer));
            var renderer = plane.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader != null)
            {
                var material = new Material(shader) { name = "ChipOS detected plane" };
                var tint = new Color(.98f, .66f, .24f, .22f);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
                if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                renderer.sharedMaterial = material;
            }
            return plane;
        }

        private void SetMeshRenderersVisible(bool visible)
        {
            if (meshManager == null) return;
            foreach (var mesh in meshManager.meshes)
            {
                if (mesh == null) continue;
                var renderer = mesh.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = visible;
            }
        }

        private async void CreateAnchorAtPose(Pose pose)
        {
            var anchorTitle = focusedArea;
            var anchorNote = focusedNote;
            try
            {
                SetStatus($"Placing {anchorTitle} on the detected surface…");
                var result = await anchorManager.TryAddAnchorAsync(pose);
                if (!result.status.IsSuccess() || result.value == null)
                {
                    SetStatus("The spatial provider could not create an anchor at that surface. Try scanning another spot.");
                    return;
                }

                var anchor = result.value;
                liveAnchors[anchorTitle] = anchor;
                CreateAnchorView(anchor, anchorTitle, anchorNote);
                SetStatus(BuildAnchorStatus(anchor, pose, anchorTitle == CaptionAnchorTitle
                    ? "Live caption panel placed and attached to this room anchor."
                    : "Studio area placed and attached to this room anchor."));

                if (SupportsPersistentAnchors())
                {
                    var save = await anchorManager.TrySaveAnchorAsync(anchor);
                    if (save.status.IsSuccess())
                    {
                        PlayerPrefs.SetString(AnchorKeyPrefix + anchorTitle, save.value.ToString());
                        PlayerPrefs.Save();
                        SetStatus(BuildAnchorStatus(anchor, pose, anchorTitle == CaptionAnchorTitle
                            ? "Live caption panel anchored and saved for future sessions on this device."
                            : "Studio area anchored and saved for future sessions on this device."));
                    }
                    else
                    {
                        SetStatus(BuildAnchorStatus(anchor, pose, anchorTitle == CaptionAnchorTitle
                            ? "Live caption panel anchored for this session; persistent save is unavailable."
                            : "Studio area anchored for this session; persistent save is unavailable."));
                    }
                }
                else
                {
                    SetStatus(BuildAnchorStatus(anchor, pose, anchorTitle == CaptionAnchorTitle
                        ? "Live caption panel anchored for this session. This provider does not support persistent anchors."
                        : "Studio area anchored for this session. This provider does not support persistent anchors."));
                }
            }
            catch (Exception exception)
            {
                SetStatus($"Anchor placement failed: {exception.GetType().Name}. The view-fixed card is still available.");
            }
        }

        private IEnumerator RestoreSavedAnchors()
        {
            loadingSavedAnchors = true;
            yield return null;
            LoadSavedAnchors();
        }

        private async void LoadSavedAnchors()
        {
            try
            {
                if (!SupportsPersistentAnchors())
                {
                    loadingSavedAnchors = false;
                    return;
                }
                // Persistent allocation remains valid while anchor loads await over frames.
                var result = await anchorManager.TryGetSavedAnchorIdsAsync(Allocator.Persistent);
                if (!result.status.IsSuccess()) { loadingSavedAnchors = false; return; }
                using (var ids = result.value)
                {
                    foreach (var id in ids)
                    {
                var area = FindAreaForSavedGuid(id);
                        if (string.IsNullOrEmpty(area) || liveAnchors.ContainsKey(area)) continue;
                        var loaded = await anchorManager.TryLoadAnchorAsync(id);
                        if (!loaded.status.IsSuccess() || loaded.value == null) continue;
                        liveAnchors[area] = loaded.value;
                        var note = area == CaptionAnchorTitle
                            ? "Microphone off · start live captions on the phone."
                            : PlayerPrefs.GetString("ChipOS.Studio." + AreaIndex(area), string.Empty);
                        CreateAnchorView(loaded.value, area, note);
                    }
                }
                loadingSavedAnchors = false;
                SetStatus($"Restored {liveAnchors.Count} saved studio anchor(s). Verify tracking before relying on their positions.");
            }
            catch (Exception exception)
            {
                loadingSavedAnchors = false;
                SetStatus($"Could not restore saved anchors ({exception.GetType().Name}). Place a new one after scanning the room.");
            }
        }

        private static string FindAreaForSavedGuid(UnityEngine.XR.ARSubsystems.SerializableGuid guid)
        {
            var idText = guid.ToString();
            var knownAreas = new[] { "Writing desk", "Sewing & making", "Duck universe gallery", "Project shelves", "Emporium workbench", "Computer station", "Ideas board", CaptionAnchorTitle };
            foreach (var area in knownAreas)
                if (string.Equals(PlayerPrefs.GetString(AnchorKeyPrefix + area, string.Empty), idText, StringComparison.OrdinalIgnoreCase)) return area;
            return string.Empty;
        }

        private bool SupportsPersistentAnchors()
        {
            if (anchorManager == null || anchorManager.subsystem == null) return false;
            // XREAL SDK 3.1 implements save/load APIs but its descriptor omits the generic
            // capability flags; provider-specific handling is needed for the Ultra path.
            var loader = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager.activeLoader : null;
            // AR Foundation 6.6 no longer exposes the provider descriptor from a running
            // subsystem. The XREAL 3.1 loader is the only path this prototype persists.
            return loader is XREALXRLoader;
        }

        private static int AreaIndex(string area)
        {
            switch (area)
            {
                case "Writing desk": return 0;
                case "Sewing & making": return 1;
                case "Duck universe gallery": return 2;
                case "Project shelves": return 3;
                case "Emporium workbench": return 4;
                case "Computer station": return 5;
                default: return 6;
            }
        }

        private void CreateAnchorView(ARAnchor anchor, string title, string note)
        {
            if (anchor == null) return;
            if (anchorViews.TryGetValue(title, out var existing) && existing != null) Destroy(existing);
            var canvasObject = new GameObject("Studio Anchor — " + title, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            canvasObject.transform.SetParent(anchor.transform, false);
            if (title != CaptionAnchorTitle)
            {
                var hitTarget = canvasObject.AddComponent<BoxCollider>();
                // Collider lives under a canvas scaled to 0.001 world units per UI unit.
                hitTarget.size = new Vector3(760f, 340f, 40f);
                hitTarget.center = Vector3.zero;
                var marker = canvasObject.AddComponent<SpatialAreaMarker>();
                marker.AreaTitle = title;
                marker.Selected = selected => AreaSelected?.Invoke(selected);
            }
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.GetComponent<Image>().color = new Color(.025f, .13f, .14f, .90f);
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(760, 340);
            rect.localScale = Vector3.one * .001f;
            rect.localPosition = Vector3.up * .12f;
            rect.localRotation = Quaternion.identity;
            var labelObject = new GameObject("Area label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(canvasObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(26, 20); labelRect.offsetMax = new Vector2(-26, -20);
            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 32; label.fontStyle = FontStyle.Bold; label.color = new Color(.96f, .9f, .77f);
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.text = title == CaptionAnchorTitle
                ? "LIVE CAPTIONS\n\n" + liveCaptionText + "\n\n" + liveSpeakerStatus
                : string.IsNullOrWhiteSpace(note) ? title + "\n\nNo local note yet." : title + "\n\n" + note;
            anchorViews[title] = canvasObject;
        }

        private string BuildAnchorStatus(ARAnchor anchor, Pose pose, string message)
        {
            var provider = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager.activeLoader : null;
            if (provider is XREALXRLoader)
            {
                var quality = anchorManager.GetAnchorQuality(anchor.trackableId, pose);
                var tracking = XREALPlugin.GetTrackingType();
                return $"{message} XREAL {tracking}; anchor map quality {quality}. RGB camera capture stays off. Keep the room well lit and move slowly while the map stabilizes.";
            }
            var depth = occlusionManager != null && occlusionManager.currentEnvironmentDepthMode != EnvironmentDepthMode.Disabled
                ? "ARCore environment depth active (phone camera/depth estimation; no LiDAR)."
                : "ARCore plane tracking active; environment depth is unsupported on this device.";
            return $"{message} {depth}";
        }

        private void Update()
        {
            var activeLoader = XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null
                ? XRGeneralSettings.Instance.Manager.activeLoader : null;
            if (activeLoader is XREALXRLoader) EnsureOpticalSeeThrough(activeLoader);
            if (!active)
            {
                if (!trackingStartRequested && !hasAttemptedRoomTracking && Time.unscaledTime >= nextProviderStatusRefresh)
                {
                    nextProviderStatusRefresh = Time.unscaledTime + 1f;
                    RefreshIdleProviderStatus();
                }
                return;
            }
            if (handSubsystem == null && XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager.activeLoader != null)
                handSubsystem = XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRHandSubsystem>();
            CheckPinchPlacement();
            if (ARSession.state == ARSessionState.SessionTracking && Time.frameCount % 90 == 0)
                RefreshStatus();
        }

        private void CheckPinchPlacement()
        {
            var pinched = false;
            if (handSubsystem != null && handSubsystem.running)
            {
                pinched = TrySelectPinch(handSubsystem.leftHand) || TrySelectPinch(handSubsystem.rightHand);
            }

            // XREAL's controller click and Android touch commonly arrive through Unity's
            // pointer input. Cast through the view centre so gaze + click/tap can activate a card.
            var xrealTriggerPressed = false;
            foreach (var device in InputSystem.devices)
            {
                if (device is XREALController controller && controller.TriggerButton.wasPressedThisFrame)
                {
                    xrealTriggerPressed = true;
                    break;
                }
            }
            var pointerPressed = xrealTriggerPressed || Input.GetMouseButtonDown(0) || Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began;
            if (pointerPressed && xrCamera != null && Physics.Raycast(xrCamera.ViewportPointToRay(new Vector3(.5f, .5f)), out var hit, 8f))
            {
                var marker = hit.collider.GetComponent<SpatialAreaMarker>();
                marker?.Activate();
            }
            previousPinch = pinched;
        }

        private bool TrySelectPinch(XRHand hand)
        {
            if (!hand.isTracked) return false;
            var thumb = hand.GetJoint(XRHandJointID.ThumbTip);
            var index = hand.GetJoint(XRHandJointID.IndexTip);
            if (!thumb.TryGetPose(out var thumbPose) || !index.TryGetPose(out var indexPose)) return false;
            var pinchPoint = (thumbPose.position + indexPose.position) * .5f;
            var isPinching = Vector3.Distance(thumbPose.position, indexPose.position) < 0.025f;
            if (!isPinching || previousPinch) return isPinching;

            foreach (var view in anchorViews.Values)
            {
                if (view == null || !view.activeInHierarchy) continue;
                var local = view.transform.InverseTransformPoint(pinchPoint);
                if (Mathf.Abs(local.x) > 380f || Mathf.Abs(local.y) > 170f || Mathf.Abs(local.z) > 60f) continue;
                view.GetComponent<SpatialAreaMarker>()?.Activate();
                return true;
            }
            return true;
        }

        private void RefreshStatus()
        {
            if (!active) return;
            var sessionState = ARSession.state;
            var planes = planeManager != null ? planeManager.trackables.count : 0;
            var meshCount = meshManager != null && meshManager.enabled ? meshManager.meshes.Count : 0;
            var provider = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager.activeLoader : null;
            var providerName = provider is XREALXRLoader ? "XREAL Ultra 6DoF" : provider != null ? "ARCore phone AR" : "No provider";
            var depthStatus = meshCount > 0
                ? meshOverlayMaterialAvailable ? $"room mesh overlay visible · {meshCount} regions" : $"{meshCount} mesh regions found, but their display shader is unavailable"
                :
                occlusionManager != null && occlusionManager.currentEnvironmentDepthMode != EnvironmentDepthMode.Disabled ? "phone environment depth active · room mesh pending" : "room mesh pending";
            var handStatus = handSubsystem != null && handSubsystem.running ? "hand pinch enabled" : "hand tracking unavailable";
            Status = $"{providerName} · {sessionState} · {planes} planes · {depthStatus} · {handStatus}. ChipOS does not record or upload video or room maps.";
        }

        private void RefreshIdleProviderStatus()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsAndroidEmulator())
            {
                SetStatus("EMULATOR 2D DISPLAY · No XREAL or ARCore room tracking runtime. See-through, camera mapping, and room anchors require the S26 with Air 2 Ultra launched from ControlGlasses.");
                return;
            }
#endif
            var loader = XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null
                ? XRGeneralSettings.Instance.Manager.activeLoader : null;
            if (loader is XREALXRLoader)
            {
                EnsureOpticalSeeThrough(loader);
                SetStatus("SEE-THROUGH DISPLAY READY · physical room stays visible; camera mapping is off until you tap Start camera mapping. Use phone touch, hand pinch, or controller touchpad + trigger.");
            }
            else if (loader != null)
            {
                SetStatus($"PHONE AR MODE · {loader.name} is active. Camera mapping starts only when you tap Start camera mapping. For Air 2 Ultra 6DoF, launch from ControlGlasses.");
            }
            else
            {
                SetStatus("2D PHONE DISPLAY · no XREAL see-through runtime is active. Phone AR mapping may be available; for Air 2 Ultra tracking and see-through, connect the glasses and launch ChipOS from ControlGlasses. Phone controls remain available.");
            }
        }

        private void EnsureOpticalSeeThrough(XRLoader loader)
        {
            if (!(loader is XREALXRLoader)) return;
            if (xrCamera == null) xrCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (xrCamera == null) return;
            xrCamera.clearFlags = CameraClearFlags.SolidColor;
            xrCamera.backgroundColor = Color.clear;
            var cameraBackground = xrCamera.GetComponent<ARCameraBackground>();
            if (cameraBackground != null) cameraBackground.enabled = false;
        }

        private static bool IsAndroidEmulator()
        {
            var model = SystemInfo.deviceModel ?? string.Empty;
            return model.IndexOf("sdk_gphone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   model.IndexOf("emulator", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SetStatus(string message) => Status = message;
    }

    /// <summary>Selectable world-space area card. Its selection opens the matching focused studio view.</summary>
    public sealed class SpatialAreaMarker : MonoBehaviour
    {
        public string AreaTitle { get; set; }
        public Action<string> Selected { get; set; }

        public void Activate()
        {
            if (!string.IsNullOrWhiteSpace(AreaTitle)) Selected?.Invoke(AreaTitle);
        }
    }
}
