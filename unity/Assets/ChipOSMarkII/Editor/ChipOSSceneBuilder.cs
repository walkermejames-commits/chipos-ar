using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChipOSMarkII.Editor
{
    public static class ChipOSSceneBuilder
    {
        public const string MainScenePath = "Assets/ChipOSMarkII/Scenes/ChipOSMarkII_Main.unity";
        public const string CanvasObjectName = "ChipOSCanvas";
        public const string RootObjectName = "ChipOSRoot";

        [MenuItem("Tools/ChipOS Mark II/Build Main Scene", false, 100)]
        public static void BuildMainSceneMenu()
        {
            BuildMainScene();
        }

        [MenuItem("Tools/ChipOS Mark II/Rebuild Main Scene And Run Health Check", false, 101)]
        public static void RebuildMainSceneAndRunHealthCheckMenu()
        {
            BuildMainScene();
            ChipOSProjectHealthCheck.RunProjectHealthCheck();
        }

        public static Scene BuildMainScene()
        {
            EnsureFolders();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("Scene rebuild cancelled because modified scenes were not saved.");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera();
            var canvas = CreateCanvas();
            CreateSafeAreaRoot(canvas);
            CreateEventSystem();
            CreateChipRoot(canvas);

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ChipOSSceneBuilder] Scene rebuilt at {MainScenePath}", canvas);
            return scene;
        }

        public static string GetRepoRootPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/ChipOSMarkII");
            EnsureFolder("Assets/ChipOSMarkII/Editor");
            EnsureFolder("Assets/ChipOSMarkII/Scenes");
            EnsureFolder("Assets/ChipOSMarkII/Scripts");
            EnsureFolder("Assets/ChipOSMarkII/Scripts/Core");
            EnsureFolder("Assets/ChipOSMarkII/Scripts/Panels");
            EnsureFolder("Assets/ChipOSMarkII/Scripts/Input");
            EnsureFolder("Assets/ChipOSMarkII/Scripts/Debug");
            EnsureFolder("Assets/ChipOSMarkII/Scripts/Layout");
            EnsureFolder("Assets/ChipOSMarkII/Scripts/Device");
            EnsureFolder("Assets/ChipOSMarkII/Tests");
            EnsureFolder("Assets/ChipOSMarkII/Tests/EditMode");
            EnsureFolder("Assets/ChipOSMarkII/Prefabs");
            EnsureFolder("Assets/ChipOSMarkII/Materials");
            EnsureFolder("Assets/ChipOSMarkII/UI");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var name = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent) && !string.IsNullOrWhiteSpace(name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void CreateMainCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            SpatialStudioController.EnsureTrackedHeadCamera(camera);
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject(
                CanvasObjectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            scaler.defaultSpriteDPI = 96f;
            scaler.fallbackScreenDPI = 96f;
            scaler.dynamicPixelsPerUnit = 1f;

            var group = canvasObject.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            var rect = canvasObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            EditorUtility.SetDirty(rect);

            return canvas;
        }

        private static void CreateSafeAreaRoot(Canvas canvas)
        {
            var safeAreaObject = new GameObject("SafeAreaRoot", typeof(RectTransform));
            safeAreaObject.transform.SetParent(canvas.transform, false);
            safeAreaObject.transform.SetAsFirstSibling();

            var rect = safeAreaObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void CreateEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.transform.position = Vector3.zero;
        }

        private static void CreateChipRoot(Canvas canvas)
        {
            var chipRootObject = new GameObject(RootObjectName);
            var app = chipRootObject.AddComponent<ChipOSApp>();
            chipRootObject.AddComponent<ChipOSRuntime>();
            chipRootObject.AddComponent<LayoutManager>();
            chipRootObject.AddComponent<InputRouter>();

            var serializedApp = new SerializedObject(app);
            serializedApp.FindProperty("sceneHudCanvas").objectReferenceValue = canvas;
            serializedApp.ApplyModifiedPropertiesWithoutUndo();

            CreatePanel<ChipPanel>(canvas.transform, "ChipPanel", HudPanelId.Chip, "Chip");
            CreatePanel<TaskPanel>(canvas.transform, "TaskPanel", HudPanelId.Task, "Task");
            CreatePanel<CaptionPanel>(canvas.transform, "CaptionPanel", HudPanelId.Caption, "Caption");
            CreatePanel<AlertPanel>(canvas.transform, "AlertPanel", HudPanelId.Custom, "Alert");
            CreatePanel<StatusStrip>(canvas.transform, "StatusStrip", HudPanelId.Custom, "StatusStrip");
            CreatePanel<DebugPanel>(canvas.transform, "DebugPanel", HudPanelId.Debug, "Debug");
        }

        private static void CreatePanel<TPanel>(Transform parent, string objectName, HudPanelId panelType, string panelId) where TPanel : HudPanel
        {
            var panelObject = new GameObject(objectName);
            panelObject.transform.SetParent(parent, false);
            panelObject.transform.localScale = Vector3.one;

            var panel = panelObject.AddComponent<TPanel>();
            var serializedPanel = new SerializedObject(panel);
            serializedPanel.FindProperty("panelType").enumValueIndex = (int)panelType;
            serializedPanel.FindProperty("panelId").stringValue = panelId;
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
