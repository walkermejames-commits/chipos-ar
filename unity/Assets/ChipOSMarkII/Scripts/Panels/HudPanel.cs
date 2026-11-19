using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ChipOSMarkII
{
    /// <summary>
    /// Base class for runtime-generated HUD panels with safe layout transitions.
    /// </summary>
    public abstract class HudPanel : MonoBehaviour, IChipHudModule
    {
        [System.Serializable]
        protected sealed class TextSlot
        {
            private Component richTextComponent;
            private PropertyInfo richTextProperty;
            public Text legacyText;

            public bool IsValid => richTextComponent != null || legacyText != null;

            public void BindRichText(Component component, PropertyInfo textProperty)
            {
                richTextComponent = component;
                richTextProperty = textProperty;
            }

            public void SetText(string value)
            {
                OptionalTextRenderer.SetText(richTextComponent, richTextProperty, value);

                if (legacyText != null)
                {
                    legacyText.text = value;
                }
            }
        }

        [SerializeField] private HudPanelId panelType = HudPanelId.Custom;
        [SerializeField] private string panelId = string.Empty;
        [SerializeField] private Vector2 defaultAnchoredPosition = Vector2.zero;
        [SerializeField] private Vector2 defaultSize = new Vector2(420f, 180f);
        [SerializeField] private Vector3 defaultEulerAngles = Vector3.zero;
        [SerializeField] private bool startVisible = true;
        [SerializeField] private Color backgroundColor = new Color(0.05f, 0.08f, 0.12f, 0.88f);
        [SerializeField] private Color accentColor = new Color(0.35f, 0.86f, 1f, 1f);

        private ChipOSApp app;
        private RectTransform runtimeRoot;
        private RectTransform contentRoot;
        private CanvasGroup canvasGroup;
        private Image backgroundImage;
        private Coroutine transitionRoutine;
        private Vector2 capturedDefaultPosition;
        private Vector2 capturedDefaultSize;
        private Vector3 capturedDefaultEulerAngles;
        private bool defaultsCaptured;
        private bool contentBuilt;
        private bool panelInitialized;
        private float currentAlpha = 1f;
        private bool eventBusSubscribed;

        public HudPanelId PanelType => panelType;
        public string PanelId => ResolvePanelId();
        public string ModuleId => PanelId;
        public bool IsRuntimeVisible => currentAlpha > 0.05f;
        public bool HasRuntimeView => runtimeRoot != null;
        public Vector2 DefaultAnchoredPosition => defaultsCaptured ? capturedDefaultPosition : defaultAnchoredPosition;
        public Vector2 DefaultSize => defaultsCaptured ? capturedDefaultSize : defaultSize;
        public Vector3 DefaultEulerAngles => defaultsCaptured ? capturedDefaultEulerAngles : defaultEulerAngles;
        public float DefaultAlpha => startVisible ? 1f : 0f;
        public Vector2 CurrentAnchoredPosition => runtimeRoot != null ? runtimeRoot.anchoredPosition : DefaultAnchoredPosition;
        public Vector2 CurrentSize => runtimeRoot != null ? runtimeRoot.sizeDelta : DefaultSize;
        protected ChipOSApp App => app;
        protected RectTransform ContentRoot => contentRoot;
        protected Color AccentColor => accentColor;
        protected Color BackgroundColor => backgroundColor;

        private void OnEnable()
        {
            if (runtimeRoot != null)
            {
                runtimeRoot.gameObject.SetActive(true);
            }

            var owner = FindFirstObjectByType<ChipOSApp>();
            if (owner != null)
            {
                owner.RegisterPanel(this);
            }
        }

        private void OnDisable()
        {
            OnPanelDisabled();
            UnsubscribeFromEventBus();

            if (app != null)
            {
                app.UnregisterPanel(this);
            }

            if (runtimeRoot != null)
            {
                runtimeRoot.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            OnPanelDestroyed();
            UnsubscribeFromEventBus();

            if (app != null)
            {
                app.UnregisterPanel(this);
            }

            if (runtimeRoot != null && runtimeRoot.gameObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(runtimeRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(runtimeRoot.gameObject);
                }
            }
        }

        public void Initialize(ChipOSApp owner, Transform canvasParent)
        {
            app = owner;
            EnsureRuntimeHierarchy(canvasParent);
            CaptureDefaultsIfNeeded();
            ApplyCanvasState(startVisible ? 1f : 0f);
            ValidateReferences();
            SubscribeToEventBus();

            if (!panelInitialized)
            {
                OnPanelInitialized();
                panelInitialized = true;
            }
        }

        public void InitializeModule(ChipOSApp owner, Transform canvasParent)
        {
            Initialize(owner, canvasParent);
        }

        /// <summary>
        /// Fades the panel in using its current layout transform.
        /// </summary>
        public virtual void Show()
        {
            FadeTo(1f, 0.18f);
        }

        public virtual void ShowPanel()
        {
            Show();
        }

        public void ShowModule()
        {
            Show();
        }

        /// <summary>
        /// Fades the panel out using its current layout transform.
        /// </summary>
        public virtual void Hide()
        {
            FadeTo(0f, 0.18f);
        }

        public virtual void HidePanel()
        {
            Hide();
        }

        public void HideModule()
        {
            Hide();
        }

        /// <summary>
        /// Changes only the panel alpha, keeping the current position, rotation, and scale.
        /// </summary>
        public virtual void FadeTo(float alpha, float duration)
        {
            if (runtimeRoot == null)
            {
                return;
            }

            TransitionTo(runtimeRoot.anchoredPosition, runtimeRoot.sizeDelta, alpha, runtimeRoot.localScale, runtimeRoot.localEulerAngles, duration);
        }

        /// <summary>
        /// Sets the anchored position immediately.
        /// </summary>
        public virtual void SetPosition(Vector2 anchoredPosition)
        {
            if (runtimeRoot == null)
            {
                return;
            }

            runtimeRoot.anchoredPosition = anchoredPosition;
        }

        public virtual void SetPanelPosition(Vector3 position)
        {
            SetPosition(new Vector2(position.x, position.y));
        }

        /// <summary>
        /// Resets the panel to its captured default layout.
        /// </summary>
        public virtual void ResetPanel()
        {
            ResetPanel(0f);
        }

        /// <summary>
        /// Resets the panel to its captured default layout over time.
        /// </summary>
        public virtual void ResetPanel(float duration)
        {
            if (runtimeRoot == null)
            {
                return;
            }

            TransitionTo(
                capturedDefaultPosition,
                capturedDefaultSize,
                startVisible ? 1f : 0f,
                Vector3.one,
                capturedDefaultEulerAngles,
                duration);
        }

        /// <summary>
        /// Validates required runtime references without treating optional fallbacks as errors.
        /// </summary>
        public virtual bool ValidateReferences()
        {
            var valid = runtimeRoot != null && contentRoot != null && canvasGroup != null && backgroundImage != null;
            if (!valid)
            {
                SafeLog("Runtime UI references are incomplete.", LogType.Warning);
            }

            return valid;
        }

        public virtual bool ValidatePanel()
        {
            return ValidateReferences();
        }

        public virtual void Refresh(ChipState state)
        {
        }

        public virtual void HandleStateChanged(ChipState state)
        {
            Refresh(state);
        }

        public virtual void HandleChipEvent(ChipEvent chipEvent)
        {
            if (App != null)
            {
                Refresh(App.State);
            }
        }

        public virtual void UpdateDisplayedContent()
        {
            if (App != null)
            {
                Refresh(App.State);
            }
        }

        public virtual string GetDiagnosticsSummary()
        {
            return $"{PanelId} | Visible: {IsRuntimeVisible} | Ready: {HasRuntimeView}";
        }

        /// <summary>
        /// Stops any active tween so resets and layout changes do not fight each other.
        /// </summary>
        public void StopTransition()
        {
            if (transitionRoutine == null)
            {
                return;
            }

            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        /// <summary>
        /// Moves the panel to a target state, including position, size, scale, and alpha.
        /// </summary>
        public void TransitionTo(Vector2 anchoredPosition, Vector2 sizeDelta, float alpha, Vector3 scale, float duration)
        {
            var rotation = runtimeRoot != null ? runtimeRoot.localEulerAngles : defaultEulerAngles;
            TransitionTo(anchoredPosition, sizeDelta, alpha, scale, rotation, duration);
        }

        /// <summary>
        /// Moves the panel to a target state, including position, rotation, size, scale, and alpha.
        /// </summary>
        public void TransitionTo(Vector2 anchoredPosition, Vector2 sizeDelta, float alpha, Vector3 scale, Vector3 eulerAngles, float duration)
        {
            if (runtimeRoot == null)
            {
                return;
            }

            runtimeRoot.gameObject.SetActive(true);
            StopTransition();

            if (duration <= 0f)
            {
                ApplyLayoutInstant(anchoredPosition, sizeDelta, alpha, scale, eulerAngles);
                return;
            }

            transitionRoutine = StartCoroutine(AnimateLayout(anchoredPosition, sizeDelta, alpha, scale, eulerAngles, duration));
        }

        protected abstract void BuildContent(RectTransform contentRoot);

        protected virtual void OnPanelInitialized()
        {
        }

        protected virtual void OnPanelDisabled()
        {
        }

        protected virtual void OnPanelDestroyed()
        {
        }

        protected virtual void OnChipEventReceived(ChipEvent chipEvent)
        {
        }

        protected TextSlot CreateLabel(
            string objectName,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            int fontSize,
            TextAnchor alignment,
            FontStyle fallbackStyle,
            Color color)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);

            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;

            var slot = new TextSlot();

            if (OptionalTextRenderer.TryCreatePreferredText(labelObject, fontSize, color, alignment, out var richText, out var richTextProperty))
            {
                slot.BindRichText(richText, richTextProperty);
                return slot;
            }

            var text = labelObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fallbackStyle;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            slot.legacyText = text;
            return slot;
        }

        protected RectTransform CreateBlock(string objectName, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var block = new GameObject(objectName, typeof(RectTransform));
            block.transform.SetParent(parent, false);

            var rect = block.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
            return rect;
        }

        protected void SafeLog(string message, LogType logType = LogType.Log)
        {
            var formatted = $"[{PanelId}] {message}";

            switch (logType)
            {
                case LogType.Warning:
                    Debug.LogWarning(formatted, this);
                    break;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    Debug.LogError(formatted, this);
                    break;
                default:
                    Debug.Log(formatted, this);
                    break;
            }
        }

        protected void SetBackgroundTint(Color color)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }
        }

        private IEnumerator AnimateLayout(Vector2 targetPosition, Vector2 targetSize, float targetAlpha, Vector3 targetScale, Vector3 targetEulerAngles, float duration)
        {
            var startPosition = runtimeRoot.anchoredPosition;
            var startSize = runtimeRoot.sizeDelta;
            var startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            var startScale = runtimeRoot.localScale;
            var startRotation = runtimeRoot.localRotation;
            var targetRotation = Quaternion.Euler(targetEulerAngles);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = Mathf.SmoothStep(0f, 1f, t);

                runtimeRoot.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, eased);
                runtimeRoot.sizeDelta = Vector2.Lerp(startSize, targetSize, eased);
                runtimeRoot.localScale = Vector3.Lerp(startScale, targetScale, eased);
                runtimeRoot.localRotation = Quaternion.Slerp(startRotation, targetRotation, eased);
                ApplyCanvasState(Mathf.Lerp(startAlpha, targetAlpha, eased));

                yield return null;
            }

            ApplyLayoutInstant(targetPosition, targetSize, targetAlpha, targetScale, targetEulerAngles);
            transitionRoutine = null;
        }

        private void ApplyLayoutInstant(Vector2 anchoredPosition, Vector2 sizeDelta, float alpha, Vector3 scale, Vector3 eulerAngles)
        {
            runtimeRoot.anchoredPosition = anchoredPosition;
            runtimeRoot.sizeDelta = sizeDelta;
            runtimeRoot.localScale = scale;
            runtimeRoot.localEulerAngles = eulerAngles;
            ApplyCanvasState(alpha);
        }

        private void ApplyCanvasState(float alpha)
        {
            if (canvasGroup == null)
            {
                if (runtimeRoot != null)
                {
                    runtimeRoot.gameObject.SetActive(alpha > 0.05f);
                    currentAlpha = Mathf.Clamp01(alpha);
                }

                return;
            }

            canvasGroup.alpha = Mathf.Clamp01(alpha);
            canvasGroup.interactable = canvasGroup.alpha > 0.95f;
            canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.05f;
            currentAlpha = canvasGroup.alpha;
        }

        private void EnsureRuntimeHierarchy(Transform canvasParent)
        {
            if (canvasParent == null)
            {
                SafeLog("No canvas parent was provided.", LogType.Warning);
                return;
            }

            if (runtimeRoot == null)
            {
                var rootObject = new GameObject($"{PanelId}_View", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                rootObject.transform.SetParent(canvasParent, false);
                rootObject.transform.SetAsLastSibling();

                runtimeRoot = rootObject.GetComponent<RectTransform>();
                canvasGroup = rootObject.GetComponent<CanvasGroup>();
                backgroundImage = rootObject.GetComponent<Image>();
            }
            else if (runtimeRoot.parent != canvasParent)
            {
                runtimeRoot.SetParent(canvasParent, false);
            }

            if (runtimeRoot == null || canvasGroup == null || backgroundImage == null)
            {
                return;
            }

            runtimeRoot.gameObject.SetActive(true);
            runtimeRoot.anchorMin = new Vector2(0.5f, 0.5f);
            runtimeRoot.anchorMax = new Vector2(0.5f, 0.5f);
            runtimeRoot.pivot = new Vector2(0.5f, 0.5f);
            runtimeRoot.sizeDelta = defaultSize;
            runtimeRoot.anchoredPosition = defaultAnchoredPosition;
            runtimeRoot.localScale = Vector3.one;
            runtimeRoot.localEulerAngles = defaultEulerAngles;

            backgroundImage.color = backgroundColor;
            backgroundImage.raycastTarget = false;

            if (contentRoot == null)
            {
                var contentObject = new GameObject("Content", typeof(RectTransform));
                contentObject.transform.SetParent(runtimeRoot, false);
                contentRoot = contentObject.GetComponent<RectTransform>();
                contentRoot.anchorMin = Vector2.zero;
                contentRoot.anchorMax = Vector2.one;
                contentRoot.offsetMin = new Vector2(16f, 14f);
                contentRoot.offsetMax = new Vector2(-16f, -14f);
                contentRoot.localScale = Vector3.one;
            }

            if (!contentBuilt)
            {
                BuildContent(contentRoot);
                contentBuilt = true;
            }
        }

        private void SubscribeToEventBus()
        {
            if (eventBusSubscribed || App == null || App.EventBus == null)
            {
                return;
            }

            App.EventBus.Subscribe(HandlePublishedEvent);
            eventBusSubscribed = true;
        }

        private void UnsubscribeFromEventBus()
        {
            if (!eventBusSubscribed || App == null || App.EventBus == null)
            {
                eventBusSubscribed = false;
                return;
            }

            App.EventBus.Unsubscribe(HandlePublishedEvent);
            eventBusSubscribed = false;
        }

        private void HandlePublishedEvent(ChipEvent chipEvent)
        {
            HandleChipEvent(chipEvent);
            OnChipEventReceived(chipEvent);
        }

        private void CaptureDefaultsIfNeeded()
        {
            if (defaultsCaptured || runtimeRoot == null)
            {
                return;
            }

            capturedDefaultPosition = runtimeRoot.anchoredPosition;
            capturedDefaultSize = runtimeRoot.sizeDelta;
            capturedDefaultEulerAngles = runtimeRoot.localEulerAngles;
            defaultsCaptured = true;
        }

        private string ResolvePanelId()
        {
            switch (panelType)
            {
                case HudPanelId.Chip:
                    return "Chip";
                case HudPanelId.Task:
                    return "Task";
                case HudPanelId.Environment:
                    return "Environment";
                case HudPanelId.Debug:
                    return "Debug";
                case HudPanelId.Caption:
                    return "Caption";
                case HudPanelId.Custom:
                default:
                    return ResolveCustomPanelId();
            }
        }

        private string ResolveCustomPanelId()
        {
            var rawId = string.IsNullOrWhiteSpace(panelId) ? gameObject.name : panelId;
            return NormalizeCustomPanelId(rawId);
        }

        private static string NormalizeCustomPanelId(string rawId)
        {
            if (string.IsNullOrWhiteSpace(rawId))
            {
                return "Custom";
            }

            if (rawId.Equals("ChipPanel"))
            {
                return "Chip";
            }

            if (rawId.Equals("TaskPanel"))
            {
                return "Task";
            }

            if (rawId.Equals("EnvironmentPanel"))
            {
                return "Environment";
            }

            if (rawId.Equals("DebugPanel"))
            {
                return "Debug";
            }

            if (rawId.Equals("CaptionPanel"))
            {
                return "Caption";
            }

            if (rawId.Equals("AlertPanel"))
            {
                return "Alert";
            }

            if (rawId.Equals("StatusStrip"))
            {
                return "StatusStrip";
            }

            return rawId.Trim();
        }
    }
}
