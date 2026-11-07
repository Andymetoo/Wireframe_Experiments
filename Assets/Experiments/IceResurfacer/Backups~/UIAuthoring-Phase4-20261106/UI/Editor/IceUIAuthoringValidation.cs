using System;
using System.IO;
using System.Linq;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IceResurfacer.UI.Editor
{
    /// <summary>Destructive authoring acceptance runs only in an explicitly marked disposable project.</summary>
    [InitializeOnLoad]
    public static class IceUIAuthoringValidation
    {
        const string Authored = "Assets/Experiments/IceResurfacer/Scenes/IceResurfacer_AuthoredMachine.unity";
        const string Procedural = "Assets/Experiments/IceResurfacer/Scenes/IceResurfacer.unity";
        const string Key = "Aurora.UIAuthoring.ValidationStage";
        static readonly Vector2 TestPosition = new Vector2(80, -680);
        static readonly Vector2 TestSize = new Vector2(280, 120);
        static readonly Color TestColor = new Color(.17f, .23f, .31f, 1);
        static int frames;
        static double started;
        static InputSettings originalInput, validationInput;
        static Mouse validationMouse, previousMouse;
        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string Results => Path.Combine(ProjectRoot, "ValidationResults", "UIAuthoring-results.txt");

        static IceUIAuthoringValidation()
        {
            if (SessionState.GetString(Key, "") != "")
            {
                started = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            }
        }

        public static void RunBatch()
        {
            try
            {
                RequireIsolated();
                Directory.CreateDirectory(Path.GetDirectoryName(Results));
                File.WriteAllText(Results, "Aurora Phase 1 isolated Unity authoring acceptance\n" +
                    "Checks below use Unity APIs and programmatic input actions. Native mouse, visual review, and full frame profiling require separate evidence.\n");
                SessionState.SetString(Key + ".proceduralHash", Hash(Procedural));
                EditorSceneManager.OpenScene(Authored);
                var hud = Find<IceAuthoredHud>();
                Require(hud != null && hud.game != null && hud.canvas != null && hud.raycaster != null,
                    "Authored scene has persistent Canvas, HUD, simulator and raycaster references.");
                Require(hud.widgets != null && hud.widgets.Length == 8, "Eight modular indicators are serialized.");
                foreach (var widget in hud.widgets)
                    Require(widget != null && widget.ValidateBinding(out _), "Serialized widget binding is complete.");
                var scaler = hud.canvas.GetComponent<CanvasScaler>();
                Require(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                    scaler.referenceResolution == new Vector2(1600, 900), "Canvas uses the 1600 x 900 authoring reference.");
                var flood = Widget(hud, IceHudWidgetKind.Flood);
                var rect = (RectTransform)flood.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = TestPosition;
                rect.sizeDelta = TestSize;
                var background = flood.GetComponent<Image>();
                Require(background != null, "Flood widget exposes an editable background Image.");
                foreach (var style in flood.GetComponentsInChildren<IceUIStyle>(true))
                    if (style.image == background)
                    {
                        style.applyColor = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(style);
                    }
                background.color = TestColor;
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(background);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Require(EditorSceneManager.SaveScene(SceneManager.GetActiveScene()), "Moved, resized and reskinned flood widget saved.");
                EditorSceneManager.OpenScene(Authored);
                CheckPersistence();
                BeginPlay("authored-play");
            }
            catch (Exception error) { Fail(error); }
        }

        public static void VerifyPersisted()
        {
            try
            {
                RequireIsolated();
                EditorSceneManager.OpenScene(Authored);
                CheckPersistence();
                Pass("Fresh Unity process reopened the modified authored scene successfully.");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Fail(error); }
        }

        static void BeginPlay(string stage)
        {
            SessionState.SetString(Key, stage);
            frames = 0;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 180) throw new TimeoutException("Play Mode acceptance exceeded 180 seconds.");
                var stage = SessionState.GetString(Key, "");
                if (stage.EndsWith("-play"))
                {
                    if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 50) return;
                    PrepareInput();
                    if (stage == "authored-play") TestAuthoredPlay();
                    else TestProceduralPlay();
                    RestoreInput();
                    SessionState.SetString(Key, stage.Replace("-play", "-exit"));
                    EditorApplication.ExitPlaymode();
                }
                else if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if (stage == "authored-exit")
                    {
                        EditorSceneManager.OpenScene(Authored);
                        CheckPersistence();
                        Pass("Edit Mode reopen after Play retained the authored transform and local background override.");
                        EditorSceneManager.OpenScene(Procedural);
                        Require(Find<IceAuthoredHud>() == null, "Procedural launch scene has no authored HUD instance.");
                        BeginPlay("procedural-play");
                    }
                    else if (stage == "procedural-exit")
                    {
                        Require(Hash(Procedural) == SessionState.GetString(Key + ".proceduralHash", ""), "Procedural scene serialized bytes were unchanged by acceptance.");
                        Pass("AUTOMATED AUTHORING ACCEPTANCE COMPLETE. Native hardware input/cursor lock, GameView overlay review, and full frame/draw-call profiling remain separate checks.");
                        SessionState.SetString(Key, "");
                        EditorApplication.update -= Tick;
                        EditorApplication.Exit(0);
                    }
                }
            }
            catch (Exception error) { Fail(error); }
        }

        static void TestAuthoredPlay()
        {
            var hud = Find<IceAuthoredHud>();
            var game = hud.game;
            Require(game != null && game.Sim != null && game.AuthoredHud == hud, "Play binds the persistent HUD to the authoritative live simulator.");
            game.NewSession(false);
            Cursor.lockState = CursorLockMode.None;
            hud.RefreshNow();
            Canvas.ForceUpdateCanvases();
            Require(hud.Visible && hud.CanInteract && hud.visibility.alpha == 1, "Started driving state displays and enables the authored HUD.");
            Require(hud.previewBadge == null || !hud.previewBadge.activeInHierarchy, "Design preview badge is hidden during Play.");
            CheckPersistence();
            var flood = Widget(hud, IceHudWidgetKind.Flood);
            Require(flood.value.text == game.Sim.FloodWaterLiters.ToString("0") + " L", "Flood numeric value equals live simulation water.");
            Require(Mathf.Abs(flood.fill.fillAmount - game.Sim.FloodWaterLiters / game.Sim.Config.FloodCapacityLiters) < .0001f,
                "Flood fill uses actual simulation tank capacity.");
            float before = game.Sim.FloodWaterLiters;
            game.Sim.Controls.FloodFlow = .8f;
            game.Sim.Step(.1f);
            hud.RefreshNow();
            Require(game.Sim.FloodWaterLiters < before && Mathf.Abs(flood.fill.fillAmount - game.Sim.FloodWaterLiters / game.Sim.Config.FloodCapacityLiters) < .0001f,
                "Changing water through the simulation updates the meter without replacing authored layout.");
            game.Sim.Controls.FloodFlow = 0;
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)flood.transform;
            Vector2 moved = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            Require(hud.ContainsPointer(moved), "uGUI raycasting follows the relocated flood panel.");
            Require(game.Dashboard.PointerOverInterface(game, moved), "Machinery pointer guard uses the relocated uGUI panel.");
            Vector2 oldFlood = ReferencePoint(1078, 60);
            Require(!hud.ContainsPointer(oldFlood), "Original flood location has no migrated uGUI target after the move.");
            Require(!game.Dashboard.PointerOverInterface(game, oldFlood), "Legacy header rectangle no longer blocks the original empty flood location.");
            var load = Widget(hud, IceHudWidgetKind.Load);
            bool diagnosticBefore = game.DiagnosticsVisible;
            load.action.onClick.Invoke();
            Require(game.DiagnosticsVisible != diagnosticBefore, "Programmatic Button.onClick invokes existing diagnostics command.");
            load.action.onClick.Invoke();
            TestInjectedPointer(hud, load, moved);
            IceUIInputValidation.Run(hud);
            game.ToggleVideoMode(); hud.RefreshNow();
            Require(!hud.Visible && hud.visibility.alpha == 0 && !hud.ContainsPointer(moved), "Video/HUD hide command removes drawing and pointer blocking.");
            game.ToggleVideoMode();
            game.TogglePause(); hud.RefreshNow();
            Require(!hud.Visible && !hud.visibility.blocksRaycasts, "Pause hides migrated HUD raycasts.");
            game.TogglePause(); game.ToggleHelp(); hud.RefreshNow();
            Require(!hud.Visible && !hud.visibility.blocksRaycasts, "Help hides migrated HUD raycasts.");
            game.ToggleHelp(); hud.RefreshNow();
            Require(hud.Visible, "Returning to driving restores the authored HUD.");
            Require(game.Sim.TryDismount(), "Simulation dismount command enters on-foot state.");
            hud.RefreshNow();
            Require(hud.Visible && !hud.CanInteract && !hud.visibility.blocksRaycasts && !hud.ContainsPointer(moved),
                "Simulation on-foot state keeps the header visible and read-only without blocking service look.");
            diagnosticBefore = game.DiagnosticsVisible;
            load.action.onClick.Invoke();
            Require(game.DiagnosticsVisible == diagnosticBefore, "On-foot header ignores diagnostics button commands.");
            Require(game.Sim.TryBoard(), "Simulation boarding command returns to seated state.");
            hud.RefreshNow();
            Require(hud.CanInteract, "Seated state restores header interaction.");
            long calibrationBefore = GC.GetAllocatedBytesForCurrentThread();
            var allocationProbe = new byte[4096];
            long calibrationBytes = GC.GetAllocatedBytesForCurrentThread() - calibrationBefore;
            GC.KeepAlive(allocationProbe);
            bool allocationCounterAvailable = calibrationBytes >= 4096;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++) hud.RefreshNow();
            watch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Pass("Refresh microbenchmark: " + (watch.Elapsed.TotalMilliseconds / 100).ToString("0.000") + " ms/call. " +
                (allocationCounterAvailable ? (allocated / 100) + " managed bytes/call." : "Managed allocation count unavailable: retained 4096-byte calibration array reported " + calibrationBytes + " bytes.") +
                " Includes data formatting; excludes full frame rendering. Screen=" + Screen.width + "x" + Screen.height + ".");
            Pass("Authored Canvas inventory: " + hud.canvas.GetComponentsInChildren<Graphic>(true).Length + " graphics; " +
                hud.canvas.GetComponentsInChildren<LayoutGroup>(true).Length + " automatic layout groups. Full frame baseline comparison is not part of this measurement.");
            CaptureVariants(hud);
        }

        static void TestInjectedPointer(IceAuthoredHud hud, IceHudWidget load, Vector2 moved)
        {
            var module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            Require(module != null && module.isActiveAndEnabled, "Input System UI module is active.");
            var loadRect = (RectTransform)load.transform;
            Vector2 click = RectTransformUtility.WorldToScreenPoint(null, loadRect.TransformPoint(loadRect.rect.center));
            bool before = hud.game.DiagnosticsVisible;
            Pointer(module, click, false);
            Pointer(module, click, true);
            Pointer(module, click, false);
            Require(hud.game.DiagnosticsVisible != before, "Injected Input System mouse press/release routes through uGUI to diagnostics.");
            hud.game.ToggleDiagnostics();
            Pointer(module, moved, false);
            Pointer(module, moved, true);
            Require(hud.ContainsPointer(moved), "Injected UI press establishes pointer ownership.");
            hud.game.PhysicalControls.UpdateInput(true, hud.game.Dashboard.PointerOverInterface(hud.game, moved));
            Require(!hud.game.PhysicalControls.IsDragging, "Injected UI press cannot start a physical machinery drag.");
            Vector2 empty = ReferencePoint(750, 600);
            Pointer(module, empty, true);
            Require(hud.ContainsPointer(empty), "UI press retains pointer ownership while dragged outside the panel.");
            Pointer(module, empty, false);
            Require(!hud.ContainsPointer(empty), "Releasing outside UI clears pointer ownership.");
        }

        static void Pointer(InputSystemUIInputModule module, Vector2 position, bool pressed)
        {
            var state = new MouseState { position = position };
            state = state.WithButton(MouseButton.Left, pressed);
            InputSystem.QueueStateEvent(validationMouse, state);
            InputSystem.Update();
            module.Process();
        }

        static void CaptureVariants(IceAuthoredHud hud)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Experiments/IceResurfacer/UI/Prefabs/Aurora UI.prefab");
            var prefabHud = source.GetComponentInChildren<IceAuthoredHud>(true);
            var prefabFlood = Widget(prefabHud, IceHudWidgetKind.Flood);
            var flood = Widget(hud, IceHudWidgetKind.Flood);
            var rect = (RectTransform)flood.transform;
            var original = (RectTransform)prefabFlood.transform;
            var background = flood.GetComponent<Image>();
            try
            {
                rect.anchorMin = original.anchorMin; rect.anchorMax = original.anchorMax;
                rect.pivot = original.pivot; rect.anchoredPosition = original.anchoredPosition; rect.sizeDelta = original.sizeDelta;
                background.color = prefabFlood.GetComponent<Image>().color;
                CaptureResolutions(hud, "Default");
            }
            finally
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = TestPosition; rect.sizeDelta = TestSize;
                background.color = TestColor;
                Canvas.ForceUpdateCanvases();
            }
            CaptureResolutions(hud, "Moved");
            CheckPersistence();
        }

        static void CaptureResolutions(IceAuthoredHud hud, string variant)
        {
            var camera = hud.game.DriverCamera;
            var oldMode = hud.canvas.renderMode;
            var oldCamera = hud.canvas.worldCamera;
            float oldDistance = hud.canvas.planeDistance;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            float oldAspect = camera.aspect;
            try
            {
                // Overlay UI cannot be included by Camera.Render. This temporary camera mode
                // captures the same authored hierarchy/scaler for explicit visual inspection.
                hud.canvas.renderMode = RenderMode.ScreenSpaceCamera;
                hud.canvas.worldCamera = camera;
                hud.canvas.planeDistance = .5f;
                foreach (var size in new[] { new Vector2Int(1366, 768), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) })
                {
                    var target = new RenderTexture(size.x, size.y, 24);
                    var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try
                    {
                        target.Create();
                        camera.targetTexture = target;
                        camera.aspect = (float)size.x / size.y;
                        Canvas.ForceUpdateCanvases();
                        camera.Render();
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                        image.Apply();
                        string path = Path.Combine(ProjectRoot, "ValidationResults", "AuthoredHUD-" + variant + "-camera-" + size.x + "x" + size.y + ".png");
                        File.WriteAllBytes(path, image.EncodeToPNG());
                        Pass("Rendered authored canvas camera capture: " + Path.GetFileName(path) + ". ScreenSpaceCamera test capture; legacy IMGUI is excluded.");
                        if (UnityStats.drawCalls == 0 && UnityStats.setPassCalls == 0)
                            File.AppendAllText(Results, "NOT VERIFIED Draw-call statistics: Editor counters returned zero despite a rendered capture in hidden batchmode. Use visible Editor Profiler/Frame Debugger.\n");
                        else
                            Pass("Editor render counters observed after capture: drawCalls=" + UnityStats.drawCalls +
                                ", setPassCalls=" + UnityStats.setPassCalls + ". Includes scene/editor work; this is not a UI-only regression comparison.");
                    }
                    finally
                    {
                        camera.targetTexture = oldTarget;
                        RenderTexture.active = oldActive;
                        UnityEngine.Object.Destroy(image);
                        UnityEngine.Object.Destroy(target);
                    }
                }
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.aspect = oldAspect;
                RenderTexture.active = oldActive;
                hud.canvas.renderMode = oldMode;
                hud.canvas.worldCamera = oldCamera;
                hud.canvas.planeDistance = oldDistance;
                Canvas.ForceUpdateCanvases();
            }
        }

        static void TestProceduralPlay()
        {
            var game = Find<IceResurfacerGame>();
            Require(game != null && game.Sim != null, "Procedural launch scene still initializes its simulation.");
            game.NewSession(false);
            Require(game.AuthoredHud == null, "Procedural scene retains legacy HUD ownership.");
            Require(game.Dashboard.PointerOverInterface(game, ReferencePoint(1078, 60)), "Procedural scene retains its legacy header pointer region.");
        }

        static void CheckPersistence()
        {
            var hud = Find<IceAuthoredHud>();
            var flood = Widget(hud, IceHudWidgetKind.Flood);
            var rect = (RectTransform)flood.transform;
            Require(Vector2.Distance(rect.anchoredPosition, TestPosition) < .001f && Vector2.Distance(rect.sizeDelta, TestSize) < .001f &&
                rect.anchorMin == new Vector2(0, 1) && rect.anchorMax == new Vector2(0, 1) && rect.pivot == new Vector2(0, 1),
                "Flood position, dimensions, anchors and pivot match saved authoring changes.");
            var background = flood.GetComponent<Image>();
            Require(background != null && Vector4.Distance(background.color, TestColor) < .001f, "Local flood background color remains authored.");
            Require(flood.GetComponentsInChildren<IceUIStyle>(true).All(style => style.image != background || !style.applyColor),
                "Background shared-color channel remains opted out for the local override.");
            if (!Application.isPlaying)
            {
                Require(flood.value.text == "738 L", "Reopened Edit Mode restores the design-only flood preview value.");
                Require(hud.previewBadge != null && hud.previewBadge.activeSelf, "Reopened Edit Mode identifies values as design previews.");
            }
        }

        static void PrepareInput()
        {
            originalInput = InputSystem.settings;
            validationInput = UnityEngine.Object.Instantiate(originalInput);
            validationInput.hideFlags = HideFlags.HideAndDontSave;
            validationInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            validationInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = validationInput;
            previousMouse = Mouse.current;
            validationMouse = InputSystem.AddDevice<Mouse>("Aurora UI authoring acceptance mouse");
            validationMouse.MakeCurrent();
        }
        static void RestoreInput()
        {
            if (validationMouse != null && validationMouse.added) InputSystem.RemoveDevice(validationMouse);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            validationMouse = previousMouse = null;
            if (originalInput != null) InputSystem.settings = originalInput;
            if (validationInput != null) UnityEngine.Object.Destroy(validationInput);
            originalInput = validationInput = null;
        }
        static Vector2 ReferencePoint(float x, float topY)
        {
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            return new Vector2((Screen.width - 1600 * scale) * .5f + x * scale,
                Screen.height - ((Screen.height - 900 * scale) * .5f + topY * scale));
        }
        static IceHudWidget Widget(IceAuthoredHud hud, IceHudWidgetKind kind) => hud.widgets.Single(widget => widget != null && widget.kind == kind);
        static T Find<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).SingleOrDefault();
        static string Hash(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        static void RequireIsolated()
        {
            string project = ProjectRoot.Replace('\\', '/');
            if (!project.Contains("/Validation/UIAuthoring-Phase1/") || !File.Exists(Path.Combine(ProjectRoot, "UIAuthoringValidationProject.txt")))
                throw new InvalidOperationException("Run this authoring test only in the marked Validation/UIAuthoring-Phase1 disposable project.");
        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Pass(message);
        }
        static void Pass(string message)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Results));
            File.AppendAllText(Results, "PASS " + message + "\n");
        }
        static void Fail(Exception error)
        {
            RestoreInput();
            SessionState.SetString(Key, "");
            EditorApplication.update -= Tick;
            Debug.LogException(error);
            if (File.Exists(Path.Combine(ProjectRoot, "UIAuthoringValidationProject.txt")))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Results));
                File.AppendAllText(Results, "FAIL " + error + "\n");
            }
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
