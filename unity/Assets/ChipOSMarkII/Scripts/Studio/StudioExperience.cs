using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Android;

namespace ChipOSMarkII
{
    /// <summary>Local creative-studio dashboard. This is view-fixed UI, not room-mapped AR.</summary>
    public sealed class StudioExperience : MonoBehaviour
    {
        private const string Prefix = "ChipOS.Studio.";
        private static readonly string[] Areas =
        {
            "Writing desk", "Sewing & making", "Duck universe gallery", "Project shelves",
            "Emporium workbench", "Computer station", "Ideas board"
        };
        private static readonly string[] Descriptions =
        {
            "Novels, chapter progress, scene cards and a continue-writing shortcut. Details: Unknown until you add them.",
            "Patterns, garment panels, materials and next steps. Project details: Unknown until you add them.",
            "Canon notes and character references belong here. New creative prompts are suggestions, not canon.",
            "One card per active project. Project list and statuses: Unknown until supplied.",
            "Sourcing, repair, resale, house-clearance and launch-readiness tasks.",
            "Links to creative and technical work. Project links and status: Unknown until supplied.",
            "Capture an idea as text, then leave it unassigned or file it later. Voice commands are not enabled."
        };

        private RectTransform window;
        private RectTransform content;
        private RectTransform workspaceEditRoot;
        private Canvas studioCanvas;
        private GameObject overlayRoot;
        private Image fullScreenBackdrop;
        private Canvas[] legacyCanvases;
        private bool[] legacyCanvasEnabled;
        private Text title;
        private InputField noteField;
        private Text detailText;
        private Text capabilityText;
        private Button spatialButton;
        private Button captionPlacementButton;
        private Button displayModeButton;
        private Button microphoneInputButton;
        private Button microphoneGainButton;
        private Button[] quickActionButtons;
        private int focusedQuickActionIndex;
        private float nextControllerFocusChange;
        private Text glassesPointerReticle;
        private SpatialStudioController spatialController;
        private Text captionText;
        private Text microphoneStatus;
        private Text speakerAnalysisStatus;
        private Button microphoneButton;
        private bool microphoneRequested;
        private bool transparentHudRequested;
        private float nextSpeechPoll;
        private string lastCaptionSignature = string.Empty;
        private string latestCaptionText = "Live captions appear here after you start listening.";
        private int lastLayoutWidth;
        private int lastLayoutHeight;
        private int lastLayoutMode = -1;
        private readonly System.Collections.Generic.List<RaycastResult> uiRaycastResults = new System.Collections.Generic.List<RaycastResult>();
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaClass speechBridge;
        private AndroidJavaObject androidActivity;
#endif
        private bool visible;
        private bool areaBrowserOpen;
        private bool highContrast;
        private int textScale = 1;
        private int selectedArea = -1;
        private Color ink = new Color(.94f, .95f, .92f);
        private Color teal = new Color(.08f, .31f, .34f);
        private Color paper = new Color(.035f, .075f, .09f);

        public bool IsVisible => visible;

        private void Start()
        {
            transparentHudRequested = PlayerPrefs.GetInt("ChipOS.Studio.TransparentHud", 0) == 1;
            if (window == null) Build();
            if (window == null) return;
            SuppressLegacyCanvases();
            overlayRoot.SetActive(true);
            visible = true;
            window.gameObject.SetActive(true);
            ApplyResponsiveLayout();
            ShowQuickControls();
        }

        private void OnDestroy()
        {
            StopLiveCaptions();
            if (studioCanvas != null) Destroy(studioCanvas.gameObject);
#if UNITY_ANDROID && !UNITY_EDITOR
            androidActivity?.Dispose();
            speechBridge?.Dispose();
#endif
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) StopLiveCaptions();
        }

        private void Update()
        {
            var layoutMode = GetLayoutMode();
            if (Screen.width != lastLayoutWidth || Screen.height != lastLayoutHeight || layoutMode != lastLayoutMode)
                ApplyResponsiveLayout();
            if (visible) TryClickFocusedStudioControl();
            if (visible) RefreshSpatialStatus();
            if (visible && Time.unscaledTime >= nextSpeechPoll)
            {
                nextSpeechPoll = Time.unscaledTime + 0.4f;
                RefreshSpeechStatus();
            }
        }

        private void TryClickFocusedStudioControl()
        {
            if (studioCanvas == null || EventSystem.current == null) return;
            var pointerPressed = false;
            var controllerPressed = false;
            var hasHandPointer = false;
            var pointerScreenPosition = Vector2.zero;
            Button pointerTarget = null;

            foreach (var device in InputSystem.devices)
            {
                if (device is Unity.XR.XREAL.XREALHandTracking hand && hand.isTracked.ReadValue() > 0.5f)
                {
                    var ray = new Ray(hand.pointerPosition.ReadValue(), hand.pointerRotation.ReadValue() * Vector3.forward);
                    if (TryGetCanvasScreenPosition(ray, out pointerScreenPosition))
                    {
                        hasHandPointer = true;
                        pointerPressed |= hand.indexPressed.wasPressedThisFrame;
                    }
                }
                else if (device is Unity.XR.XREAL.XREALController controller)
                {
                    var axis = controller.Primary2DAxis.ReadValue();
                    if (Time.unscaledTime >= nextControllerFocusChange && Mathf.Abs(axis.y) >= 0.65f)
                    {
                        MoveQuickActionFocus(axis.y > 0f ? -1 : 1);
                        nextControllerFocusChange = Time.unscaledTime + 0.35f;
                    }
                    controllerPressed |= controller.TriggerButton.wasPressedThisFrame;
                }
            }

            if (hasHandPointer)
            {
                UpdateGlassesPointer(pointerScreenPosition);
                pointerTarget = FindButtonAt(pointerScreenPosition);
                if (pointerTarget != null) FocusQuickAction(pointerTarget);
            }
            else if (glassesPointerReticle != null)
            {
                glassesPointerReticle.gameObject.SetActive(false);
            }

            if (!pointerPressed && !controllerPressed) return;

            // A hand pinch activates the button under the visible hand pointer.
            if (pointerPressed)
            {
                if (pointerTarget != null) pointerTarget.onClick.Invoke();
                return;
            }

            // The XREAL controller has a trigger and touchpad but no UI pointer
            // pose in this SDK layout. Make it usable as a focus navigator.
            if (controllerPressed)
            {
                if (hasHandPointer && pointerTarget != null) pointerTarget.onClick.Invoke();
                else if (quickActionButtons != null && quickActionButtons.Length > 0)
                {
                    var focused = quickActionButtons[Mathf.Clamp(focusedQuickActionIndex, 0, quickActionButtons.Length - 1)];
                    if (focused != null && focused.isActiveAndEnabled && focused.IsInteractable()) focused.onClick.Invoke();
                    else
                    {
                        FocusQuickAction(spatialButton);
                        if (spatialButton != null && spatialButton.isActiveAndEnabled && spatialButton.IsInteractable()) spatialButton.onClick.Invoke();
                    }
                }
            }
        }

        private bool TryGetCanvasScreenPosition(Ray pointerRay, out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;
            if (studioCanvas == null || studioCanvas.worldCamera == null) return false;

            var camera = studioCanvas.worldCamera;
            var plane = new Plane(camera.transform.forward, camera.transform.position + camera.transform.forward * studioCanvas.planeDistance);
            if (!plane.Raycast(pointerRay, out var distance) || distance <= 0f) return false;
            screenPosition = RectTransformUtility.WorldToScreenPoint(camera, pointerRay.GetPoint(distance));
            return true;
        }

        private Button FindButtonAt(Vector2 screenPosition)
        {
            var raycaster = studioCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null) return null;

            var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
            uiRaycastResults.Clear();
            raycaster.Raycast(pointer, uiRaycastResults);
            foreach (var hit in uiRaycastResults)
            {
                var button = hit.gameObject != null ? hit.gameObject.GetComponentInParent<Button>() : null;
                if (button != null && button.IsInteractable()) return button;
            }
            return null;
        }

        private void UpdateGlassesPointer(Vector2 screenPosition)
        {
            if (glassesPointerReticle == null || studioCanvas == null) return;
            var rootRect = overlayRoot != null ? overlayRoot.GetComponent<RectTransform>() : null;
            if (rootRect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rootRect, screenPosition, studioCanvas.worldCamera, out var localPosition))
            {
                glassesPointerReticle.gameObject.SetActive(false);
                return;
            }

            glassesPointerReticle.rectTransform.anchoredPosition = localPosition;
            glassesPointerReticle.gameObject.SetActive(true);
        }

        private void MoveQuickActionFocus(int direction)
        {
            if (quickActionButtons == null || quickActionButtons.Length == 0) return;
            for (var attempt = 0; attempt < quickActionButtons.Length; attempt++)
            {
                focusedQuickActionIndex = (focusedQuickActionIndex + direction + quickActionButtons.Length) % quickActionButtons.Length;
                var candidate = quickActionButtons[focusedQuickActionIndex];
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsInteractable()) continue;
                if (EventSystem.current != null) candidate.Select();
                return;
            }
        }

        private void FocusQuickAction(Button button)
        {
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable() || quickActionButtons == null) return;
            var index = Array.IndexOf(quickActionButtons, button);
            if (index < 0) return;
            focusedQuickActionIndex = index;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != button.gameObject)
                button.Select();
        }

        public void Toggle()
        {
            if (window == null) Build();
            if (visible) StopLiveCaptions();
            visible = !visible;
            overlayRoot.SetActive(visible);
            if (visible)
            {
                SuppressLegacyCanvases();
                ShowQuickControls();
            }
            else RestoreLegacyCanvases();
        }

        private void Build()
        {
            // Keep the working studio independent of ChipOSApp's hideable legacy HUD.
            // A restored HUD-hidden preference must never hide or disable studio controls.
            var canvasObject = new GameObject("ChipOS Studio Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            studioCanvas = canvasObject.GetComponent<Canvas>();
            // Camera-space UI is rendered into the XR eye buffers. ScreenSpaceOverlay
            // can disappear when the XREAL loader owns the display surface.
            studioCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            studioCanvas.worldCamera = Camera.main;
            studioCanvas.planeDistance = 1.2f;
            studioCanvas.overrideSorting = true;
            studioCanvas.sortingOrder = 500;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Screen.width >= Screen.height
                ? new Vector2(1920f, 1080f)
                : new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            var canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            var root = new GameObject("StudioOverlayRoot", typeof(RectTransform), typeof(Image));
            overlayRoot = root;
            root.transform.SetParent(studioCanvas.transform, false);
            var overlay = root.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero; overlay.offsetMax = Vector2.zero;
            fullScreenBackdrop = root.GetComponent<Image>();
            fullScreenBackdrop.color = new Color(0, 0, 0, 0);
            fullScreenBackdrop.raycastTarget = false;

            // Keep the always-on layer low in the view, with mapping, mic, mic-route,
            // display-mode controls, truthful status and the newest caption. The full
            // project workspace expands only after an explicit Areas tap.
            var dockObject = new GameObject("QuickActionDock", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            dockObject.transform.SetParent(root.transform, false);
            window = dockObject.GetComponent<RectTransform>();
            window.anchorMin = window.anchorMax = new Vector2(.5f, 0f);
            window.pivot = new Vector2(.5f, 0f);
            window.anchoredPosition = new Vector2(0, 28);
            window.sizeDelta = new Vector2(1180, 340);
            dockObject.GetComponent<Image>().color = new Color(.025f, .075f, .085f, .34f);
            dockObject.GetComponent<Image>().raycastTarget = true;
            var dockGroup = dockObject.GetComponent<CanvasGroup>();
            dockGroup.alpha = 1f; dockGroup.interactable = true; dockGroup.blocksRaycasts = true;

            var bar = MakeRect("StudioHeader", window, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -64), Vector2.zero);
            bar.GetComponent<Image>().color = new Color(.04f, .21f, .23f, .94f);
            var drag = bar.gameObject.AddComponent<StudioPanelDrag>(); drag.Target = window;
            title = MakeText("StudioTitle", bar, "CHIPOS  /  CREATIVE STUDIO", 21, Color.white, TextAnchor.MiddleLeft, new Vector2(-260, -8), new Vector2(-520, 46));
            displayModeButton = MakeButton(bar, "DisplayMode", "GLASSES HUD", new Vector2(-370, 0), ToggleDisplayMode);
            displayModeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 56);
            MakeButton(bar, "Areas", "Studio areas", new Vector2(-190, 0), ToggleAreaBrowser);

            var body = MakeRect("StudioBody", window, Vector2.zero, Vector2.one, new Vector2(18, 12), new Vector2(-18, -72));
            capabilityText = MakeText("StudioStatus", body, "Checking spatial runtime…", 14, ink, TextAnchor.MiddleLeft, new Vector2(8, -4), new Vector2(-630, 50));
            SetDockTextColumn(capabilityText, -4, 620, 50);
            capabilityText.horizontalOverflow = HorizontalWrapMode.Wrap;
            capabilityText.verticalOverflow = VerticalWrapMode.Overflow;
            spatialButton = MakeTopRightButton(body, "StartSpatial", "START CAMERA MAPPING", new Vector2(-8, -8), new Vector2(340, 72), ToggleSpatialMode);
            microphoneStatus = MakeText("MicrophoneStatus", body, "MICROPHONE OFF · Tap to start. Offline English speech and acoustic speaker analysis; Android noise reduction when available; audio is not saved or uploaded.", 15, ink, TextAnchor.MiddleLeft, new Vector2(8, -56), new Vector2(-630, 50));
            SetDockTextColumn(microphoneStatus, -56, 620, 50);
            microphoneStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            microphoneStatus.verticalOverflow = VerticalWrapMode.Overflow;
            microphoneButton = MakeTopRightButton(body, "LiveCaptions", "Start microphone + live captions", new Vector2(-8, -88), new Vector2(340, 72), ToggleLiveCaptions);
            microphoneInputButton = MakeTopRightButton(body, "MicrophoneInput", "MIC INPUT", new Vector2(-180, -168), new Vector2(168, 48), CycleMicrophoneInput);
            microphoneGainButton = MakeTopRightButton(body, "MicrophoneGain", "MIC GAIN 1×", new Vector2(-8, -168), new Vector2(168, 48), CycleMicrophoneGain);
            speakerAnalysisStatus = MakeText("SpeakerAnalysisStatus", body, "Speaker analysis is off until the microphone starts.", 16, ink, TextAnchor.MiddleLeft, new Vector2(8, -104), new Vector2(-630, 42));
            SetDockTextColumn(speakerAnalysisStatus, -104, 620, 42);
            speakerAnalysisStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            speakerAnalysisStatus.verticalOverflow = VerticalWrapMode.Overflow;
            captionText = MakeText("LiveCaption", body, "Captions appear here after you start the microphone.", 19, ink, TextAnchor.UpperLeft, new Vector2(8, -152), new Vector2(-630, 96));
            SetDockTextColumn(captionText, -152, 620, 72);
            captionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            captionText.verticalOverflow = VerticalWrapMode.Overflow;
            captionPlacementButton = MakeTopRightButton(body, "AnchorCaptions", "PLACE CAPTIONS IN ROOM", new Vector2(-8, -224), new Vector2(340, 64), PlaceCaptionPanel);
            quickActionButtons = new[] { spatialButton, microphoneButton, microphoneInputButton, microphoneGainButton, captionPlacementButton };
            FocusQuickAction(spatialButton);
            var inputHint = MakeText("GlassesInputHint", body,
                "Phone: tap · Glasses: point and pinch · Controller: touchpad to choose, trigger to activate",
                14, ink, TextAnchor.MiddleLeft, new Vector2(8, -292), new Vector2(-40, 22));
            SetDockTextColumn(inputHint, -292, 620, 22);
            var reticleObject = new GameObject("GlassesInputReticle", typeof(RectTransform), typeof(Text));
            reticleObject.transform.SetParent(root.transform, false);
            glassesPointerReticle = reticleObject.GetComponent<Text>();
            glassesPointerReticle.text = "●";
            glassesPointerReticle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            glassesPointerReticle.fontSize = 30;
            glassesPointerReticle.color = new Color(.35f, 1f, .72f, .95f);
            glassesPointerReticle.alignment = TextAnchor.MiddleCenter;
            glassesPointerReticle.raycastTarget = false;
            var reticleRect = glassesPointerReticle.rectTransform;
            reticleRect.anchorMin = reticleRect.anchorMax = new Vector2(.5f, .5f);
            reticleRect.pivot = new Vector2(.5f, .5f);
            reticleRect.sizeDelta = new Vector2(48, 48);
            reticleObject.SetActive(false);
            var areaRoot = MakeRect("AreaCards", body, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -270), new Vector2(0, -270));
            content = areaRoot;
            for (var i = 0; i < Areas.Length; i++)
            {
                var col = i % 2; var row = i / 2;
                var areaIndex = i;
                var card = MakeButton(areaRoot, "Area" + i, Areas[i], new Vector2(-520 + col * 450, -row * 68), () => OpenArea(areaIndex));
                card.GetComponent<RectTransform>().sizeDelta = new Vector2(430, 58);
                card.GetComponent<Image>().color = new Color(.10f, .22f, .24f, 1f);
                var cardLabel = card.GetComponentInChildren<Text>(); cardLabel.color = Color.white; cardLabel.fontSize = 21;
            }
            detailText = MakeText("StudioDetail", body, "", 19, ink, TextAnchor.UpperLeft, new Vector2(4, -280), new Vector2(-40, 96));
            detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailText.gameObject.SetActive(false);
            workspaceEditRoot = MakeRect("WorkspaceNoteTools", body, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 8), new Vector2(0, 180));
            var inputRect = MakeRect("LocalNoteInput", workspaceEditRoot, new Vector2(0, 0), new Vector2(1, 0), new Vector2(8, 64), new Vector2(-8, 164));
            var inputImage = inputRect.GetComponent<Image>(); inputImage.color = new Color(.10f, .14f, .16f, 1f);
            noteField = inputRect.gameObject.AddComponent<InputField>();
            var text = MakeText("NoteText", inputRect, "Add a local note or idea…", 17, ink, TextAnchor.UpperLeft, new Vector2(12, -4), new Vector2(-24, 92));
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            noteField.textComponent = text; noteField.lineType = InputField.LineType.MultiLineNewline;
            noteField.onValueChanged.AddListener(SaveNote);
            MakeBottomRightButton(workspaceEditRoot, "Save", "Save note", new Vector2(-8, 8), new Vector2(148, 54), () => SaveNote(noteField.text));
            MakeBottomRightButton(workspaceEditRoot, "Delete", "Clear note", new Vector2(-166, 8), new Vector2(148, 54), ClearNote);
            MakeBottomRightButton(workspaceEditRoot, "Suggestion", "Add suggestion", new Vector2(-324, 8), new Vector2(148, 54), AddSuggestion);
            MakeBottomRightButton(workspaceEditRoot, "AnchorSpatial", "Place area", new Vector2(-482, 8), new Vector2(148, 54), PlaceSelectedArea);
            content.gameObject.SetActive(false);
            detailText.gameObject.SetActive(false);
            workspaceEditRoot.gameObject.SetActive(false);
            spatialController = studioCanvas.GetComponent<SpatialStudioController>();
            if (spatialController == null) spatialController = canvasObject.AddComponent<SpatialStudioController>();
            spatialController.AreaSelected -= OpenAreaByTitle;
            spatialController.AreaSelected += OpenAreaByTitle;
            spatialController.SetLiveCaptionText(latestCaptionText);
            ApplyResponsiveLayout();
            visible = false;
            root.SetActive(false);
        }

        private void ApplyResponsiveLayout()
        {
            lastLayoutWidth = Screen.width;
            lastLayoutHeight = Screen.height;
            var landscape = Screen.width >= Screen.height;
            lastLayoutMode = GetLayoutMode();
            var scaler = studioCanvas != null ? studioCanvas.GetComponent<CanvasScaler>() : null;
            if (scaler != null)
            {
                scaler.referenceResolution = landscape
                    ? new Vector2(1920f, 1080f)
                    : new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 0f;
            }
            if (window != null)
            {
                var glassLayout = GetLayoutMode() == 1;
                if (glassLayout)
                {
                    studioCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    studioCanvas.worldCamera = Camera.main;
                    studioCanvas.planeDistance = 1.6f;
                    fullScreenBackdrop.color = new Color(0f, 0f, 0f, 0f);
                    window.anchorMin = window.anchorMax = new Vector2(.5f, 0f);
                    window.pivot = new Vector2(.5f, 0f);
                    window.anchoredPosition = new Vector2(0f, 32f);
                    window.sizeDelta = new Vector2(landscape ? 1080f : 960f,
                        areaBrowserOpen || selectedArea >= 0 ? 700f : 420f);
                    window.GetComponent<Image>().color = new Color(.025f, .075f, .085f, .48f);
                }
                else
                {
                    // A phone build is a normal app screen, not a transparent HUD over
                    // the launcher. Keep the controls in the centre and leave captions
                    // readable against a quiet, dark background.
                    studioCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    studioCanvas.worldCamera = null;
                    fullScreenBackdrop.color = new Color(.018f, .032f, .04f, .98f);
                    window.anchorMin = window.anchorMax = new Vector2(.5f, .5f);
                    window.pivot = new Vector2(.5f, .5f);
                    window.anchoredPosition = Vector2.zero;
                    window.sizeDelta = new Vector2(landscape ? 1420f : 1024f,
                        areaBrowserOpen || selectedArea >= 0 ? 760f : 480f);
                    window.GetComponent<Image>().color = new Color(.025f, .075f, .085f, .98f);
                }
                if (captionPlacementButton != null)
                    captionPlacementButton.gameObject.SetActive(!glassLayout ||
                        spatialController != null && spatialController.IsRoomAnchoringActive);
                var camera = Camera.main;
                if (camera != null && glassLayout && (transparentHudRequested || spatialController != null && spatialController.IsXrealRuntimeActive))
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Color.clear;
                }
                else if (camera != null && !glassLayout &&
                    (spatialController == null || !spatialController.IsRoomAnchoringActive))
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(.018f, .032f, .04f, 1f);
                }
            }
        }

        private int GetLayoutMode()
        {
            var spatialDisplay = spatialController != null &&
                (spatialController.IsXrealRuntimeActive ||
                    spatialController.IsPhoneArRuntimeActive && spatialController.IsRoomAnchoringActive);
            return spatialDisplay || transparentHudRequested ? 1 : 0;
        }

        private void ToggleDisplayMode()
        {
            if (spatialController != null && (spatialController.IsXrealRuntimeActive || spatialController.IsRoomAnchoringActive)) return;
            transparentHudRequested = !transparentHudRequested;
            PlayerPrefs.SetInt("ChipOS.Studio.TransparentHud", transparentHudRequested ? 1 : 0);
            PlayerPrefs.Save();
            ApplyResponsiveLayout();
            RefreshSpatialStatus();
        }

        private void HideStudio()
        {
            StopLiveCaptions();
            visible = false;
            if (overlayRoot != null) overlayRoot.SetActive(false);
            RestoreLegacyCanvases();
        }

        private void SuppressLegacyCanvases()
        {
            if (legacyCanvases == null)
            {
                legacyCanvases = FindObjectsOfType<Canvas>(true);
                legacyCanvasEnabled = new bool[legacyCanvases.Length];
                for (var i = 0; i < legacyCanvases.Length; i++)
                    legacyCanvasEnabled[i] = legacyCanvases[i] != null && legacyCanvases[i].enabled;
            }

            for (var i = 0; i < legacyCanvases.Length; i++)
            {
                var canvas = legacyCanvases[i];
                if (canvas != null && canvas != studioCanvas) canvas.enabled = false;
            }
        }

        private void RestoreLegacyCanvases()
        {
            if (legacyCanvases == null || legacyCanvasEnabled == null) return;
            for (var i = 0; i < legacyCanvases.Length; i++)
                if (legacyCanvases[i] != null) legacyCanvases[i].enabled = legacyCanvasEnabled[i];
        }

        private void ShowOverview()
        {
            selectedArea = -1;
            areaBrowserOpen = true;
            ApplyResponsiveLayout();
            title.text = "CHIPOS CREATIVE STUDIO";
            content.gameObject.SetActive(true);
            detailText.gameObject.SetActive(false);
            workspaceEditRoot.gameObject.SetActive(true);
            noteField.text = PlayerPrefs.GetString(Prefix + "unassigned", string.Empty);
        }

        private void ToggleAreaBrowser()
        {
            if (areaBrowserOpen && selectedArea < 0) ShowQuickControls();
            else ShowOverview();
            var button = window.Find("StudioHeader/Areas");
            var label = button != null ? button.GetComponentInChildren<Text>() : null;
            if (label != null) label.text = areaBrowserOpen ? "Close areas" : "Studio areas";
        }

        private void ShowQuickControls()
        {
            selectedArea = -1;
            areaBrowserOpen = false;
            title.text = "CHIPOS  /  CREATIVE STUDIO";
            ApplyResponsiveLayout();
            content.gameObject.SetActive(false);
            detailText.gameObject.SetActive(false);
            workspaceEditRoot.gameObject.SetActive(false);
            var button = window.Find("StudioHeader/Areas");
            var label = button != null ? button.GetComponentInChildren<Text>() : null;
            if (label != null) label.text = "Studio areas";
        }

        private void OpenArea(int index)
        {
            selectedArea = index;
            areaBrowserOpen = false;
            ApplyResponsiveLayout();
            content.gameObject.SetActive(false);
            detailText.gameObject.SetActive(true);
            workspaceEditRoot.gameObject.SetActive(true);
            title.text = Areas[index].ToUpperInvariant();
            detailText.text = Descriptions[index] + (index == 2 ? "\nSuggestions will be labeled separately from established canon." : "") + "\nSaved on this device only.";
            noteField.text = PlayerPrefs.GetString(Prefix + index, string.Empty);
            spatialController?.SetFocusedArea(Areas[index], noteField.text);
        }

        private void OpenAreaByTitle(string areaTitle)
        {
            var index = Array.IndexOf(Areas, areaTitle);
            if (index < 0) return;
            if (window == null) Build();
            if (window == null) return;
            if (!visible)
            {
                visible = true;
                overlayRoot.SetActive(true);
                SuppressLegacyCanvases();
            }
            OpenArea(index);
        }

        private void SaveNote(string value)
        {
            PlayerPrefs.SetString(Prefix + (selectedArea < 0 ? "unassigned" : selectedArea.ToString()), value ?? string.Empty);
            PlayerPrefs.Save();
            if (selectedArea >= 0) spatialController?.SetFocusedArea(Areas[selectedArea], value);
        }

        private void ClearNote()
        {
            var key = Prefix + (selectedArea < 0 ? "unassigned" : selectedArea.ToString());
            PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); noteField.text = string.Empty;
        }

        private void AddSuggestion()
        {
            if (selectedArea != 2) { detailText.text = "Open Duck universe gallery to add a clearly labeled suggestion."; return; }
            var existing = noteField.text;
            noteField.text = string.IsNullOrWhiteSpace(existing) ? "[Suggestion] " : existing + "\n[Suggestion] ";
            noteField.ActivateInputField();
        }

        private void ToggleSpatialMode()
        {
            if (spatialController == null) return;
            if (spatialController.IsRoomAnchoringActive || spatialController.IsRoomAnchoringStarting) spatialController.StopRoomAnchoring();
            else RequestCameraPermissionAndStartSpatialMapping();
            RefreshSpatialStatus();
        }

        private void RequestCameraPermissionAndStartSpatialMapping()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => spatialController?.StartRoomAnchoring();
                callbacks.PermissionDenied += _ => spatialController?.ReportPermissionStatus("Camera permission denied. Spatial mapping is off; phone studio controls remain available.");
                callbacks.PermissionDeniedAndDontAskAgain += _ => spatialController?.ReportPermissionStatus("Camera permission is blocked in Android settings. Spatial mapping is off; phone studio controls remain available.");
                spatialController.ReportPermissionStatus("Waiting for camera permission. Spatial mapping has not started and no camera frames are processed yet.");
                Permission.RequestUserPermission(Permission.Camera, callbacks);
                return;
            }
#endif
            spatialController.StartRoomAnchoring();
        }

        private void PlaceSelectedArea()
        {
            if (selectedArea < 0)
            {
                detailText.text = "Open a studio area first, then place its card on a detected surface.";
                return;
            }
            if (spatialController == null || !spatialController.IsRoomAnchoringActive)
            {
                detailText.text = "Start AR first. Camera and tracking sensors will be used locally for placement.";
                return;
            }
            spatialController.PlaceArea(Areas[selectedArea], noteField.text);
            RefreshSpatialStatus();
        }

        private void PlaceCaptionPanel()
        {
            if (spatialController == null || !spatialController.IsRoomAnchoringActive)
            {
                detailText.text = "Start AR, scan a surface, then place the live caption panel. View-fixed captions remain available on the phone.";
                return;
            }
            spatialController.PlaceCaptionPanel();
            RefreshSpatialStatus();
        }

        private void RefreshSpatialStatus()
        {
            if (capabilityText != null && spatialController != null)
            {
                capabilityText.text = transparentHudRequested && !spatialController.IsXrealRuntimeActive && !spatialController.IsRoomAnchoringActive
                    ? "2D GLASSES LAYOUT ONLY · XREAL see-through and tracking are not active, so this remains a phone display. Launch ChipOS from ControlGlasses with Air 2 Ultra connected for real see-through and room mapping. " + spatialController.Status
                    : spatialController.Status;
            }
            if (spatialButton != null && spatialController != null)
            {
                var label = spatialButton.GetComponentInChildren<Text>();
                if (label != null) label.text = spatialController.IsRoomAnchoringActive
                    ? "STOP CAMERA MAPPING"
                    : spatialController.IsRoomAnchoringStarting ? "CANCEL MAPPING START" : "START CAMERA MAPPING";
            }
            if (displayModeButton != null && spatialController != null)
            {
                var label = displayModeButton.GetComponentInChildren<Text>();
                if (label != null) label.text = spatialController.IsXrealRuntimeActive
                    ? "SEE-THROUGH ACTIVE"
                    : spatialController.IsRoomAnchoringActive ? "AR VIEW ACTIVE"
                    : transparentHudRequested ? "PHONE LAYOUT" : "GLASSES LAYOUT";
                displayModeButton.interactable = !spatialController.IsXrealRuntimeActive && !spatialController.IsRoomAnchoringActive;
            }
        }

        private void ToggleLiveCaptions()
        {
            if (microphoneRequested) StopLiveCaptions();
            else RequestMicrophoneAndStart();
        }

        private void CycleMicrophoneInput()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (speechBridge == null) speechBridge = new AndroidJavaClass("com.chipos.speech.ChipOSSpeechBridge");
                if (androidActivity == null)
                    androidActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
                var route = speechBridge.CallStatic<string>("cycleInputDevice", androidActivity);
                SetSpeechStatus(string.IsNullOrWhiteSpace(route) ? "Android could not change the microphone input." : route);
                RefreshSpeechStatus();
            }
            catch (Exception error)
            {
                SetSpeechStatus("Microphone input selection unavailable: " + error.GetType().Name + ".");
            }
#else
            SetSpeechStatus("Microphone input selection requires the Android app.");
#endif
        }

        private void CycleMicrophoneGain()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (speechBridge == null) speechBridge = new AndroidJavaClass("com.chipos.speech.ChipOSSpeechBridge");
                if (androidActivity == null)
                    androidActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
                var result = speechBridge.CallStatic<string>("cycleInputGain", androidActivity);
                SetSpeechStatus(string.IsNullOrWhiteSpace(result) ? "Microphone gain did not change." : result);
                RefreshSpeechStatus();
            }
            catch (Exception error)
            {
                SetSpeechStatus("Microphone gain control unavailable: " + error.GetType().Name + ".");
            }
#else
            SetSpeechStatus("Microphone gain control requires the Android app.");
#endif
        }

        private void RequestMicrophoneAndStart()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (speechBridge == null) speechBridge = new AndroidJavaClass("com.chipos.speech.ChipOSSpeechBridge");
                if (androidActivity == null)
                    androidActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
                var availability = speechBridge.CallStatic<string>("getAvailabilityStatus", androidActivity);
                if (string.IsNullOrWhiteSpace(availability) || !availability.StartsWith("Offline speech is available", StringComparison.Ordinal))
                {
                    SetSpeechStatus(string.IsNullOrWhiteSpace(availability) ? "Offline speech is unavailable on this device." : availability);
                    if (speakerAnalysisStatus != null) speakerAnalysisStatus.text = "SPEAKER ANALYSIS · Unavailable on this device; no microphone was opened.";
                    return;
                }
            }
            catch (Exception error)
            {
                SetSpeechStatus("Offline speech availability could not be checked: " + error.GetType().Name + ". The microphone is off.");
                return;
            }
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => StartLiveCaptions();
                callbacks.PermissionDenied += _ => SetSpeechStatus("Microphone permission denied. Captions have not started.");
                callbacks.PermissionDeniedAndDontAskAgain += _ => SetSpeechStatus("Microphone permission is blocked in Android settings. Captions have not started.");
                SetSpeechStatus("Waiting for microphone permission. No audio is captured yet.");
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
                return;
            }
            StartLiveCaptions();
#else
            SetSpeechStatus("Live microphone captions require the Android app. Editor audio remains simulated.");
#endif
        }

        private void StartLiveCaptions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (speechBridge == null) speechBridge = new AndroidJavaClass("com.chipos.speech.ChipOSSpeechBridge");
                if (androidActivity == null)
                    androidActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
                microphoneRequested = speechBridge.CallStatic<bool>("start", androidActivity);
                SetSpeechStatus(microphoneRequested
                    ? "Starting bundled offline speech and speaker analysis. Audio stays on this phone and is not saved."
                    : "Offline speech could not start. Check microphone permission and model availability.");
            }
            catch (Exception error)
            {
                microphoneRequested = false;
                SetSpeechStatus("Offline speech unavailable: " + error.GetType().Name + ". The microphone is off.");
            }
#endif
        }

        private void StopLiveCaptions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { speechBridge?.CallStatic("stop"); }
            catch (Exception error) { Debug.LogWarning("[ChipOS Speech] Stop failed: " + error.GetType().Name); }
#endif
            microphoneRequested = false;
            if (microphoneButton != null)
            {
                var label = microphoneButton.GetComponentInChildren<Text>();
                if (label != null) label.text = "Start microphone + live captions";
            }
            if (microphoneStatus != null) microphoneStatus.text = "MICROPHONE OFF · Audio is not being captured.";
        }

        private void RefreshSpeechStatus()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (speechBridge != null) try
            {
                var state = JsonUtility.FromJson<SpeechBridgeState>(speechBridge.CallStatic<string>("getStateJson"));
                microphoneRequested = state != null && state.active;
                if (microphoneGainButton != null && state != null)
                {
                    var gainLabel = microphoneGainButton.GetComponentInChildren<Text>();
                    if (gainLabel != null) gainLabel.text = "MIC GAIN " + state.gain.ToString("0.#") + "×";
                }
                var label = microphoneButton != null ? microphoneButton.GetComponentInChildren<Text>() : null;
                if (label != null) label.text = state != null && state.starting
                    ? "LOADING OFFLINE SPEECH…"
                    : microphoneRequested ? "STOP CAPTIONS & MICROPHONE" : "Start microphone + live captions";
                if (microphoneStatus != null && state != null)
                {
                    var effectiveLevel = state.level;
                    microphoneStatus.text = state.running
                            ? "MICROPHONE ON · Offline, audio not saved · Input: " + state.input + " · level " + effectiveLevel.ToString("0.000") + " · gain " + state.gain.ToString("0.#") + "×" +
                            (state.noiseSuppression ? " · noise reduction on" : "") +
                            (effectiveLevel < 0.004f ? " · Quiet input; move closer, check the mic, or raise gain." :
                                effectiveLevel > 0.22f ? " · Very loud input; lower gain or move the mic farther away." : "")
                        : state.starting
                            ? "STARTING · Loading offline speech models. Microphone remains off until loading finishes."
                            : "MICROPHONE OFF · " + state.status + (string.IsNullOrWhiteSpace(state.input) ? "" : " · Selected input: " + state.input);
                }
                if (speakerAnalysisStatus != null && state != null)
                    speakerAnalysisStatus.text = "SPEAKER ANALYSIS · " + (string.IsNullOrWhiteSpace(state.speakerStatus)
                        ? "Status unavailable; speaker remains unknown."
                        : state.speakerStatus);
                if (state != null)
                {
                    var lines = new StringBuilder();
                    if (state.lines != null)
                    {
                        foreach (var line in state.lines)
                        {
                            if (line == null || string.IsNullOrWhiteSpace(line.text)) continue;
                            if (lines.Length > 0) lines.AppendLine();
                            var lineSpeaker = string.IsNullOrWhiteSpace(line.speaker) ? "Speaker unknown" : line.speaker;
                            lines.Append(lineSpeaker).Append(line.speakerUncertain ? " ?" : "").AppendLine(":").Append(line.text);
                        }
                    }
                    var currentLineIncluded = false;
                    if (!string.IsNullOrWhiteSpace(state.text) && state.lines != null)
                    {
                        foreach (var line in state.lines)
                            if (line != null && string.Equals(line.text, state.text, StringComparison.Ordinal))
                            {
                                currentLineIncluded = true;
                                break;
                            }
                    }
                    if (!string.IsNullOrWhiteSpace(state.text) && !currentLineIncluded)
                    {
                        if (lines.Length > 0) lines.AppendLine();
                        var speaker = string.IsNullOrWhiteSpace(state.speaker) ? "Speaker unknown" : state.speaker;
                        lines.Append(speaker).Append(state.speakerUncertain ? " ?" : "").AppendLine(":").Append(state.text);
                        if (!state.finalized) lines.Append(" …");
                    }
                    if (lines.Length > 0)
                    {
                        var signature = lines.ToString();
                        if (signature != lastCaptionSignature)
                        {
                            lastCaptionSignature = signature;
                            latestCaptionText = signature;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SetSpeechStatus("Caption status unavailable: " + error.GetType().Name + ".");
            }
#endif
            if (spatialController != null)
            {
                spatialController.SetLiveCaptionText(latestCaptionText);
                if (speakerAnalysisStatus != null)
                    spatialController.SetSpeakerAnalysisStatus(speakerAnalysisStatus.text);
                if (spatialController.HasCaptionPanelAnchor)
                    captionText.text = "Live caption panel is attached to its room anchor.";
                else captionText.text = latestCaptionText;
            }
        }

        private void SetSpeechStatus(string message)
        {
            if (microphoneStatus != null) microphoneStatus.text = message;
        }

        [Serializable]
        private sealed class SpeechBridgeState
        {
            public bool active;
            public bool starting;
            public bool running;
            public string status;
            public string text;
            public string speaker;
            public bool speakerUncertain;
            public string speakerStatus;
            public string input;
            public float level;
            public float gain = 1f;
            public bool noiseSuppression;
            public bool finalized;
            public CaptionLineState[] lines;
        }

        [Serializable]
        private sealed class CaptionLineState
        {
            public string text;
            public string speaker;
            public bool speakerUncertain;
            public bool finalized;
        }

        private void AdjustText(int step) { textScale = Mathf.Clamp(textScale + step, 0, 3); foreach (var t in window.GetComponentsInChildren<Text>(true)) t.fontSize = Mathf.Clamp(t.fontSize + step * 2, 12, 32); }
        private void ToggleContrast() { highContrast = !highContrast; paper = highContrast ? Color.black : new Color(.035f,.075f,.09f); ink = highContrast ? Color.white : new Color(.94f,.95f,.92f); window.GetComponent<Image>().color = paper; foreach (var t in window.GetComponentsInChildren<Text>(true)) if (t.name != "StudioTitle" && !t.name.EndsWith("Label", StringComparison.Ordinal)) t.color = ink; foreach (var button in window.GetComponentsInChildren<Button>(true)) if (button.name.StartsWith("Area", StringComparison.Ordinal)) button.GetComponent<Image>().color = highContrast ? new Color(.08f,.08f,.08f) : new Color(.10f,.22f,.24f,1f); var inputImage = noteField != null ? noteField.GetComponent<Image>() : null; if (inputImage != null) inputImage.color = highContrast ? new Color(.08f,.08f,.08f) : new Color(.10f,.14f,.16f,1f); }
        private void Resize(float delta) { window.sizeDelta = new Vector2(Mathf.Clamp(window.sizeDelta.x + delta, 1000, 1700), Mathf.Clamp(window.sizeDelta.y + delta * .6f, 680, 960)); }

        private static RectTransform MakeRect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.color = new Color(1,1,1,0); image.raycastTarget = false;
            var r = go.GetComponent<RectTransform>(); r.anchorMin=min; r.anchorMax=max; r.offsetMin=offsetMin; r.offsetMax=offsetMax; r.localScale=Vector3.one; return r;
        }
        private Text MakeText(string name, Transform parent, string value, int size, Color color, TextAnchor alignment, Vector2 position, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var r=go.GetComponent<RectTransform>(); r.anchorMin=new Vector2(0,1); r.anchorMax=new Vector2(1,1); r.pivot=new Vector2(.5f,1); r.anchoredPosition=position; r.sizeDelta=sizeDelta;
            var t=go.GetComponent<Text>(); t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize=size; t.color=color; t.alignment=alignment; t.text=value; t.raycastTarget=false; return t;
        }

        private static void SetDockTextColumn(Text text, float topOffset, float width, float height)
        {
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(10, topOffset);
            rect.sizeDelta = new Vector2(width, height);
        }
        private Button MakeButton(Transform parent, string name, string label, Vector2 position, Action action)
        {
            var go=new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=new Vector2(1,.5f); r.pivot=new Vector2(1,.5f); r.anchoredPosition=position; r.sizeDelta=new Vector2(Mathf.Clamp(30 + label.Length * 9, 60, 170),56);
            go.GetComponent<Image>().color=teal; var b=go.GetComponent<Button>(); b.onClick.AddListener(()=>action());
            var txt=MakeText(name+"Label",go.transform,label,17,Color.white,TextAnchor.MiddleCenter,new Vector2(0,0),Vector2.zero); txt.rectTransform.anchorMin=Vector2.zero; txt.rectTransform.anchorMax=Vector2.one; txt.rectTransform.offsetMin=Vector2.zero; txt.rectTransform.offsetMax=Vector2.zero; txt.raycastTarget=false;
            return b;
        }

        private Button MakeTopRightButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Action action)
        {
            var button = MakeButton(parent, name, label, position, action);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return button;
        }

        private Button MakeBottomRightButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Action action)
        {
            var button = MakeButton(parent, name, label, position, action);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return button;
        }
    }

    public sealed class StudioPanelDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform Target { get; set; }
        public void OnBeginDrag(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData) { if (Target != null) Target.anchoredPosition += eventData.delta; }
    }
}
