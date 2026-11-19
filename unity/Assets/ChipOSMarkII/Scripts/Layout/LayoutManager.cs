using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Registers HUD panels, applies named layouts, and persists the last stable layout.
    /// </summary>
    public sealed partial class LayoutManager : MonoBehaviour
    {
        [Serializable]
        private struct PanelLayout
        {
            public string panelId;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public float alpha;
            public Vector3 scale;
            public Vector3 eulerAngles;

            public PanelLayout(string panelId, Vector2 anchoredPosition, Vector2 sizeDelta, float alpha, Vector3 scale, Vector3 eulerAngles)
            {
                this.panelId = panelId;
                this.anchoredPosition = anchoredPosition;
                this.sizeDelta = sizeDelta;
                this.alpha = alpha;
                this.scale = scale;
                this.eulerAngles = eulerAngles;
            }
        }

        private const string LayoutPrefKey = "ChipOSMarkII.LayoutMode";
        private const string LastStableLayoutPrefKey = "ChipOSMarkII.LastStableLayoutMode";

        [SerializeField] private float transitionDuration = 0.24f;
        [SerializeField] private float minimumPanelSpacing = 28f;
        [SerializeField] private bool verboseLogging = true;
        [SerializeField] private bool runSelfTestOnPlay = true;
        [SerializeField] private bool preserveLastStableLayout = true;
        [SerializeField] [TextArea(2, 4)] private string inspectorStatus = "LayoutManager awaiting bind.";

        private readonly Dictionary<string, HudPanel> registeredPanels = new Dictionary<string, HudPanel>(StringComparer.Ordinal);
        private readonly Dictionary<LayoutMode, Dictionary<string, PanelLayout>> layoutProfiles = new Dictionary<LayoutMode, Dictionary<string, PanelLayout>>();
        private readonly Dictionary<LayoutMode, string> layoutDescriptions = new Dictionary<LayoutMode, string>();

        private ChipOSApp app;

        public LayoutMode CurrentMode { get; private set; } = LayoutMode.Default;
        public LayoutMode LastStableMode { get; private set; } = LayoutMode.Default;
        public bool IsPanicMode => CurrentMode == LayoutMode.Panic;
        public string InspectorStatus => inspectorStatus;
        public bool SelfTestOnPlay => runSelfTestOnPlay;
        public int RegisteredPanelCount => registeredPanels.Count;

        /// <summary>
        /// Returns the short human-readable description for a named layout profile.
        /// </summary>
        public string GetLayoutDescription(LayoutMode mode)
        {
            EnsureProfiles();
            return layoutDescriptions.TryGetValue(mode, out var description) && !string.IsNullOrWhiteSpace(description)
                ? description
                : "Prototype-safe modular HUD layout.";
        }

        /// <summary>
        /// Reads the layout that would be used on startup without applying it yet.
        /// </summary>
        public LayoutMode LoadStartupLayoutPreview()
        {
            EnsureProfiles();
            return LoadSavedLayout();
        }

        /// <summary>
        /// Binds the layout manager to the app controller.
        /// </summary>
        public void Bind(ChipOSApp owner)
        {
            app = owner;
            EnsureProfiles();
            LastStableMode = LoadLastStableLayout();
            UpdateInspectorStatus("Bound");
        }

        /// <summary>
        /// Registers a panel once so it can receive layout updates.
        /// </summary>
        public void RegisterPanel(HudPanel panel)
        {
            if (panel == null)
            {
                DebugLog("Ignored null panel registration.");
                return;
            }

            var panelId = panel.PanelId;
            if (string.IsNullOrWhiteSpace(panelId))
            {
                app?.Warn($"Skipped registering panel on '{panel.gameObject.name}' because it had no stable id.");
                return;
            }

            RemoveNullRegistrations();

            if (registeredPanels.TryGetValue(panelId, out var existing))
            {
                if (ReferenceEquals(existing, panel))
                {
                    return;
                }

                app?.Warn($"Duplicate layout registration for panel '{panelId}'. Keeping the first instance.");
                return;
            }

            registeredPanels.Add(panelId, panel);
            DebugLog($"Registered panel '{panelId}'.");

            if (layoutProfiles.Count > 0)
            {
                var targets = ResolveTargetLayouts(CurrentMode);
                if (targets.TryGetValue(panelId, out var layout))
                {
                    ApplyLayoutToPanel(panel, layout, true);
                }
            }

            UpdateInspectorStatus($"Registered {registeredPanels.Count} panel(s)");
        }

        /// <summary>
        /// Safely unregisters a panel so stale layout entries do not accumulate.
        /// </summary>
        public void UnregisterPanel(HudPanel panel)
        {
            if (panel == null || registeredPanels.Count == 0)
            {
                return;
            }

            var staleKeys = new List<string>();
            foreach (var entry in registeredPanels)
            {
                if (entry.Value == null || ReferenceEquals(entry.Value, panel))
                {
                    staleKeys.Add(entry.Key);
                }
            }

            for (var i = 0; i < staleKeys.Count; i++)
            {
                registeredPanels.Remove(staleKeys[i]);
            }

            if (staleKeys.Count > 0)
            {
                UpdateInspectorStatus($"Unregistered {staleKeys.Count} panel(s)");
            }
        }

        /// <summary>
        /// Applies the saved layout, falling back to Default when persistence is invalid.
        /// </summary>
        public void ApplyStartupLayout()
        {
            var startupMode = LoadSavedLayout();
            ApplyLayoutInternal(startupMode, true, false);

            if (runSelfTestOnPlay)
            {
                RunSelfTest();
            }
        }

        /// <summary>
        /// Applies a named layout to all registered panels.
        /// </summary>
        public void ApplyLayout(LayoutMode mode, bool immediate)
        {
            ApplyLayoutInternal(mode, immediate, true);
        }

        /// <summary>
        /// Restores the last non-panic layout.
        /// </summary>
        public void RestoreLastStableLayout(bool immediate)
        {
            ApplyLayoutInternal(LastStableMode, immediate, true);
        }

        /// <summary>
        /// Clears layout persistence so the next boot starts from Default.
        /// </summary>
        public void ResetSavedLayout()
        {
            PlayerPrefs.DeleteKey(LayoutPrefKey);
            PlayerPrefs.DeleteKey(LastStableLayoutPrefKey);
            PlayerPrefs.Save();
            LastStableMode = LayoutMode.Default;
            UpdateInspectorStatus("Saved layout cleared");
            DebugLog("Cleared saved layout preferences.");
        }

        private void ApplyLayoutInternal(LayoutMode mode, bool immediate, bool updatePreferences)
        {
            EnsureProfiles();
            RemoveNullRegistrations();

            var resolvedMode = ResolveLayoutMode(mode);
            CurrentMode = resolvedMode;

            if (resolvedMode != LayoutMode.Panic)
            {
                LastStableMode = resolvedMode;
            }

            var resolvedLayouts = ResolveTargetLayouts(resolvedMode);
            foreach (var entry in registeredPanels)
            {
                if (!resolvedLayouts.TryGetValue(entry.Key, out var layout))
                {
                    continue;
                }

                ApplyLayoutToPanel(entry.Value, layout, immediate);
            }

            if (updatePreferences)
            {
                SaveLayoutState(resolvedMode);
            }

            UpdateInspectorStatus($"Current: {CurrentMode} | Panels: {registeredPanels.Count}");
            DebugLog($"Applied layout '{resolvedMode}'{(immediate ? " instantly" : " with smoothing")}.");
        }

        private void ApplyLayoutToPanel(HudPanel panel, PanelLayout layout, bool immediate)
        {
            if (panel == null)
            {
                DebugLog($"Skipped applying layout for '{layout.panelId}' because the panel reference is null.");
                return;
            }

            if (!panel.HasRuntimeView)
            {
                DebugLog($"Skipped panel '{panel.PanelId}' because its runtime view is not ready yet.");
                return;
            }

            panel.StopTransition();
            panel.TransitionTo(
                layout.anchoredPosition,
                layout.sizeDelta,
                layout.alpha,
                layout.scale,
                layout.eulerAngles,
                immediate ? 0f : transitionDuration);
            }

        private Dictionary<string, PanelLayout> ResolveTargetLayouts(LayoutMode mode)
        {
            var targets = new Dictionary<string, PanelLayout>(StringComparer.Ordinal);
            layoutProfiles.TryGetValue(mode, out var profile);

            foreach (var entry in registeredPanels)
            {
                var panel = entry.Value;
                if (panel == null)
                {
                    continue;
                }

                if (profile != null && profile.TryGetValue(entry.Key, out var configuredLayout))
                {
                    targets[entry.Key] = ClampLayoutToBounds(configuredLayout);
                    continue;
                }

                DebugLog($"Layout '{mode}' has no explicit profile for panel '{entry.Key}'. Using panel defaults.");
                targets[entry.Key] = ClampLayoutToBounds(BuildFallbackLayout(panel));
            }

            ResolveOverlaps(targets);
            return targets;
        }

        private PanelLayout BuildFallbackLayout(HudPanel panel)
        {
            return new PanelLayout(
                panel.PanelId,
                panel.DefaultAnchoredPosition,
                panel.DefaultSize,
                panel.DefaultAlpha,
                Vector3.one,
                panel.DefaultEulerAngles);
        }

        private PanelLayout ClampLayoutToBounds(PanelLayout layout)
        {
            var safeSize = app != null ? app.GetHudReferenceSize() : new Vector2(1920f, 1080f);
            var halfWidth = Mathf.Max(0f, (safeSize.x * 0.5f) - (layout.sizeDelta.x * 0.5f));
            var halfHeight = Mathf.Max(0f, (safeSize.y * 0.5f) - (layout.sizeDelta.y * 0.5f));
            layout.anchoredPosition = new Vector2(
                Mathf.Clamp(layout.anchoredPosition.x, -halfWidth, halfWidth),
                Mathf.Clamp(layout.anchoredPosition.y, -halfHeight, halfHeight));
            return layout;
        }

        private void ResolveOverlaps(Dictionary<string, PanelLayout> targets)
        {
            if (targets == null || targets.Count < 2)
            {
                return;
            }

            var keys = new List<string>(targets.Keys);
            for (var pass = 0; pass < 6; pass++)
            {
                var movedAny = false;
                for (var i = 0; i < keys.Count; i++)
                {
                    if (!targets.TryGetValue(keys[i], out var primary) || primary.alpha <= 0.05f)
                    {
                        continue;
                    }

                    for (var j = i + 1; j < keys.Count; j++)
                    {
                        if (!targets.TryGetValue(keys[j], out var secondary) || secondary.alpha <= 0.05f)
                        {
                            continue;
                        }

                        if (!TryResolveOverlap(primary, ref secondary))
                        {
                            continue;
                        }

                        targets[keys[j]] = ClampLayoutToBounds(secondary);
                        movedAny = true;
                    }
                }

                if (!movedAny)
                {
                    return;
                }
            }
        }

        private bool TryResolveOverlap(PanelLayout anchorLayout, ref PanelLayout movingLayout)
        {
            var anchorRect = ToRect(anchorLayout);
            var movingRect = ToRect(movingLayout);

            var overlapX = Mathf.Min(anchorRect.xMax, movingRect.xMax) - Mathf.Max(anchorRect.xMin, movingRect.xMin);
            var overlapY = Mathf.Min(anchorRect.yMax, movingRect.yMax) - Mathf.Max(anchorRect.yMin, movingRect.yMin);

            if (overlapX <= 0f || overlapY <= 0f)
            {
                return false;
            }

            if (overlapY <= overlapX)
            {
                var direction = movingLayout.anchoredPosition.y >= anchorLayout.anchoredPosition.y ? 1f : -1f;
                movingLayout.anchoredPosition += new Vector2(0f, direction * (overlapY + minimumPanelSpacing));
                return true;
            }

            var horizontalDirection = movingLayout.anchoredPosition.x >= anchorLayout.anchoredPosition.x ? 1f : -1f;
            movingLayout.anchoredPosition += new Vector2(horizontalDirection * (overlapX + minimumPanelSpacing), 0f);
            return true;
        }

        private static Rect ToRect(PanelLayout layout)
        {
            var size = layout.sizeDelta;
            var position = layout.anchoredPosition;
            return new Rect(
                position.x - (size.x * 0.5f),
                position.y - (size.y * 0.5f),
                size.x,
                size.y);
        }

        private void EnsureProfiles()
        {
            if (layoutProfiles.Count > 0)
            {
                return;
            }

            layoutDescriptions.Clear();
            layoutDescriptions[LayoutMode.Default] = "Balanced cockpit spread with readable assistant, tasks, alerts, and diagnostics.";
            layoutDescriptions[LayoutMode.Desk] = "Workspace-first layout with broader panels and calmer side channels.";
            layoutDescriptions[LayoutMode.Walking] = "Motion-friendly layout that keeps the center lane clean and the shell lightweight.";
            layoutDescriptions[LayoutMode.Focus] = "Primary task focus with secondary modules dimmed or hidden.";
            layoutDescriptions[LayoutMode.Minimal] = "Low-noise shell with only lightweight status surfaces left visible.";
            layoutDescriptions[LayoutMode.Panic] = "High-contrast recovery surface with non-essential modules hidden.";

            layoutProfiles.Add(LayoutMode.Default, Profile(
                new PanelLayout("StatusStrip", new Vector2(0f, 490f), new Vector2(1240f, 70f), 1f, Vector3.one, Vector3.zero),
                new PanelLayout("Chip", new Vector2(-470f, 245f), new Vector2(520f, 200f), 1f, Vector3.one, Vector3.zero),
                new PanelLayout("Task", new Vector2(-470f, -45f), new Vector2(460f, 250f), 1f, Vector3.one, new Vector3(0f, 0f, -1.5f)),
                new PanelLayout("Alert", new Vector2(470f, 60f), new Vector2(470f, 220f), 0.96f, Vector3.one, Vector3.zero),
                new PanelLayout("Environment", new Vector2(500f, -220f), new Vector2(350f, 150f), 0.78f, new Vector3(0.96f, 0.96f, 1f), Vector3.zero),
                new PanelLayout("Debug", new Vector2(500f, 280f), new Vector2(430f, 220f), 0.92f, Vector3.one, Vector3.zero)));

            layoutProfiles.Add(LayoutMode.Desk, Profile(
                new PanelLayout("StatusStrip", new Vector2(0f, 490f), new Vector2(1320f, 70f), 1f, Vector3.one, Vector3.zero),
                new PanelLayout("Chip", new Vector2(-180f, 250f), new Vector2(620f, 210f), 1f, Vector3.one, Vector3.zero),
                new PanelLayout("Task", new Vector2(-500f, -130f), new Vector2(470f, 260f), 1f, Vector3.one, new Vector3(0f, 0f, -1f)),
                new PanelLayout("Alert", new Vector2(450f, 245f), new Vector2(410f, 180f), 0.9f, new Vector3(0.98f, 0.98f, 1f), Vector3.zero),
                new PanelLayout("Environment", new Vector2(470f, -85f), new Vector2(390f, 180f), 0.88f, Vector3.one, new Vector3(0f, 0f, 1.2f)),
                new PanelLayout("Debug", new Vector2(460f, -280f), new Vector2(420f, 210f), 0.92f, Vector3.one, Vector3.zero)));

            layoutProfiles.Add(LayoutMode.Walking, Profile(
                new PanelLayout("StatusStrip", new Vector2(0f, 480f), new Vector2(1260f, 68f), 0.96f, Vector3.one, Vector3.zero),
                new PanelLayout("Chip", new Vector2(0f, 255f), new Vector2(590f, 185f), 1f, Vector3.one, new Vector3(0f, 0f, -2f)),
                new PanelLayout("Task", new Vector2(505f, 25f), new Vector2(360f, 225f), 0.88f, new Vector3(0.96f, 0.96f, 1f), new Vector3(0f, 0f, 3f)),
                new PanelLayout("Alert", new Vector2(0f, -205f), new Vector2(520f, 150f), 0.9f, new Vector3(0.96f, 0.96f, 1f), Vector3.zero),
                new PanelLayout("Environment", new Vector2(-500f, 35f), new Vector2(350f, 160f), 0.82f, new Vector3(0.94f, 0.94f, 1f), new Vector3(0f, 0f, -3f)),
                new PanelLayout("Debug", new Vector2(-565f, -275f), new Vector2(320f, 160f), 0.72f, new Vector3(0.96f, 0.96f, 1f), Vector3.zero)));

            layoutProfiles.Add(LayoutMode.Focus, Profile(
                new PanelLayout("StatusStrip", new Vector2(0f, 490f), new Vector2(1180f, 68f), 0.96f, Vector3.one, Vector3.zero),
                new PanelLayout("Chip", new Vector2(0f, 215f), new Vector2(660f, 210f), 1f, Vector3.one, Vector3.zero),
                new PanelLayout("Task", new Vector2(0f, -105f), new Vector2(560f, 270f), 1f, Vector3.one, Vector3.zero),
                new PanelLayout("Alert", new Vector2(0f, -345f), new Vector2(540f, 135f), 0.82f, new Vector3(0.94f, 0.94f, 1f), Vector3.zero),
                new PanelLayout("Environment", new Vector2(-650f, 255f), new Vector2(300f, 140f), 0f, new Vector3(0.9f, 0.9f, 1f), Vector3.zero),
                new PanelLayout("Debug", new Vector2(640f, 255f), new Vector2(320f, 150f), 0.15f, new Vector3(0.94f, 0.94f, 1f), Vector3.zero)));

            layoutProfiles.Add(LayoutMode.Minimal, Profile(
                new PanelLayout("StatusStrip", new Vector2(0f, 494f), new Vector2(1120f, 62f), 0.96f, Vector3.one, Vector3.zero),
                new PanelLayout("Chip", new Vector2(-530f, 302f), new Vector2(410f, 145f), 1f, Vector3.one, Vector3.zero),
                new PanelLayout("Task", new Vector2(555f, -315f), new Vector2(330f, 190f), 0f, new Vector3(0.94f, 0.94f, 1f), Vector3.zero),
                new PanelLayout("Alert", new Vector2(500f, 302f), new Vector2(360f, 130f), 0.72f, new Vector3(0.94f, 0.94f, 1f), Vector3.zero),
                new PanelLayout("Environment", new Vector2(560f, 105f), new Vector2(300f, 150f), 0f, new Vector3(0.92f, 0.92f, 1f), Vector3.zero),
                new PanelLayout("Debug", new Vector2(560f, -110f), new Vector2(330f, 150f), 0f, new Vector3(0.94f, 0.94f, 1f), Vector3.zero)));

            layoutProfiles.Add(LayoutMode.Panic, Profile(
                new PanelLayout("StatusStrip", new Vector2(0f, 470f), new Vector2(1180f, 76f), 1f, Vector3.one, Vector3.zero),
                new PanelLayout("Chip", new Vector2(0f, 250f), new Vector2(520f, 140f), 0f, new Vector3(0.94f, 0.94f, 1f), Vector3.zero),
                new PanelLayout("Task", new Vector2(0f, -290f), new Vector2(380f, 180f), 0f, new Vector3(0.9f, 0.9f, 1f), Vector3.zero),
                new PanelLayout("Alert", new Vector2(0f, 40f), new Vector2(760f, 280f), 1f, new Vector3(1.04f, 1.04f, 1f), Vector3.zero),
                new PanelLayout("Environment", new Vector2(0f, -120f), new Vector2(340f, 160f), 0f, new Vector3(0.9f, 0.9f, 1f), Vector3.zero),
                new PanelLayout("Debug", new Vector2(0f, -360f), new Vector2(380f, 150f), 0f, new Vector3(0.92f, 0.92f, 1f), Vector3.zero)));
        }

        private static Dictionary<string, PanelLayout> Profile(params PanelLayout[] layouts)
        {
            var profile = new Dictionary<string, PanelLayout>(StringComparer.Ordinal);

            for (var i = 0; i < layouts.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(layouts[i].panelId))
                {
                    profile[layouts[i].panelId] = layouts[i];
                }
            }

            return profile;
        }

        private LayoutMode LoadSavedLayout()
        {
            if (TryLoadLayoutPref(LayoutPrefKey, out var savedMode))
            {
                if (savedMode == LayoutMode.Panic)
                {
                    DebugLog("Ignored saved Panic layout on startup. Falling back to the last stable layout.");
                    return LoadLastStableLayout();
                }

                DebugLog($"Loaded saved layout '{savedMode}'.");
                return savedMode;
            }

            if (TryLoadLayoutPref(LastStableLayoutPrefKey, out var fallbackMode))
            {
                DebugLog($"Loaded fallback stable layout '{fallbackMode}'.");
                return fallbackMode;
            }

            DebugLog("No valid saved layout was found. Falling back to Default.");
            return LayoutMode.Default;
        }

        private LayoutMode LoadLastStableLayout()
        {
            if (!TryLoadLayoutPref(LastStableLayoutPrefKey, out var savedMode) || savedMode == LayoutMode.Panic)
            {
                return LayoutMode.Default;
            }

            return savedMode;
        }

        private bool TryLoadLayoutPref(string key, out LayoutMode mode)
        {
            mode = LayoutMode.Default;

            if (string.IsNullOrWhiteSpace(key) || !PlayerPrefs.HasKey(key))
            {
                return false;
            }

            var savedValue = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(savedValue))
            {
                DebugLog($"Ignored empty PlayerPrefs value for '{key}'.");
                return false;
            }

            if (!Enum.TryParse(savedValue, true, out LayoutMode parsedMode))
            {
                DebugLog($"Ignored invalid PlayerPrefs value '{savedValue}' for '{key}'.");
                return false;
            }

            if (!layoutProfiles.ContainsKey(parsedMode))
            {
                DebugLog($"Ignored unknown layout '{parsedMode}' from '{key}'.");
                return false;
            }

            mode = parsedMode;
            return true;
        }

        private LayoutMode ResolveLayoutMode(LayoutMode mode)
        {
            if (layoutProfiles.ContainsKey(mode))
            {
                return mode;
            }

            app?.Warn($"Requested unknown layout '{mode}'. Falling back to Default.");
            return LayoutMode.Default;
        }

        private void SaveLayoutState(LayoutMode mode)
        {
            if (mode == LayoutMode.Panic && preserveLastStableLayout)
            {
                DebugLog("Skipped saving Panic layout so the last stable layout remains recoverable.");
                return;
            }

            try
            {
                PlayerPrefs.SetString(LayoutPrefKey, mode.ToString());
                PlayerPrefs.SetString(LastStableLayoutPrefKey, LastStableMode.ToString());
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LayoutManager] Failed to persist layout state: {ex.Message}", this);
                return;
            }

            DebugLog($"Saved layout '{mode}' with last stable '{LastStableMode}'.");
        }

        private void RemoveNullRegistrations()
        {
            if (registeredPanels.Count == 0)
            {
                return;
            }

            var removedAny = false;
            var staleKeys = new List<string>();

            foreach (var entry in registeredPanels)
            {
                if (entry.Value == null)
                {
                    staleKeys.Add(entry.Key);
                }
            }

            for (var i = 0; i < staleKeys.Count; i++)
            {
                removedAny = registeredPanels.Remove(staleKeys[i]) || removedAny;
            }

            if (removedAny)
            {
                DebugLog($"Removed {staleKeys.Count} stale panel registration(s).");
            }
        }

        private void UpdateInspectorStatus(string status)
        {
            inspectorStatus = $"{status}\nCurrent Layout: {CurrentMode}\nLast Stable: {LastStableMode}\nRegistered Panels: {registeredPanels.Count}";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DebugLog(string message)
        {
            if (!verboseLogging || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Debug.Log($"[LayoutManager] {message}", this);
        }
    }
}
