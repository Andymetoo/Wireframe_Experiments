#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IceResurfacer.Editor
{
    // Destructive authoring/input fixtures run only in the marked disposable project.
    [InitializeOnLoad]
    public static class IceMenuValidation
    {
        const string KeyName = "Aurora.Phase4.Acceptance";
        const string Procedural = "Assets/Experiments/IceResurfacer/Scenes/IceResurfacer.unity";
        static string Output => Path.GetFullPath("ValidationResults");
        static string Results => Path.Combine(Output, "Phase4-results.txt");
        static IceResurfacerGame game;
        static IceMenuScreens menus;
        static Mouse mouse, oldMouse;
        static Keyboard keyboard, oldKeyboard;
        static InputSettings previousSettings, temporarySettings;
        static InputSystemUIInputModule module;
        static int frames, step;
        static double started, next;
        static Simulation clickedSimulation;
        static IceMenuValidation()
        {
            if (SessionState.GetString(KeyName, "") == "") return;
            started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick;
        }
        public static void RunBatch()
        {
            try
            {
                IceMenuFactory.RequireIsolated(); Directory.CreateDirectory(Output);
                File.WriteAllText(Results, "Phase 4 isolated Unity tests. Injected input and camera rendering; native hardware and combined IMGUI visual inspection remain manual.\n");
                SessionState.SetString(KeyName + "Procedural", Hash(Procedural));
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                File.Copy("ValidationResults/Installed-Phase4.unity", IceMenuFactory.ScenePath, true);
                AssetDatabase.ImportAsset(IceMenuFactory.ScenePath, ImportAssetOptions.ForceUpdate);
                EditorSceneManager.OpenScene(IceMenuFactory.ScenePath); Locate(); Bindings(); Preview();
                File.Copy(IceMenuFactory.ScenePath, Path.Combine(Output, "Before-authoring.unity"), true);
                EditAndSave(); EditorSceneManager.OpenScene(IceMenuFactory.ScenePath); Locate(); CheckSaved();
                File.Copy(IceMenuFactory.ScenePath, Path.Combine(Output, "Authored-persistence.unity"), true);
                Begin("edited-play");
            }
            catch (Exception e) { Fail(e); }
        }
        public static void VerifyPersisted()
        {
            try
            {
                IceMenuFactory.RequireIsolated(); EditorSceneManager.OpenScene(IceMenuFactory.ScenePath); Locate(); CheckSaved();
                Check(PlayerPrefs.HasKey(IceOperatorPreferences.SpeedKey) && Mathf.Abs(IceOperatorPreferences.LoadMph(5) - 6.25f) < .001f,
                    "Forward-speed PlayerPrefs value survives a fresh Unity process (isolated company/product).");
                Begin("restart-play");
            }
            catch (Exception e) { Fail(e); }
        }
        public static void VerifyIntegrated()
        {
            try
            {
                IceMenuFactory.RequireIsolated(); Directory.CreateDirectory(Output);
                SessionState.SetString(KeyName + "Exact", Hash(IceMenuFactory.ScenePath));
                EditorSceneManager.OpenScene(IceMenuFactory.ScenePath); Locate(); Bindings(); Begin("exact-play");
            }
            catch (Exception e) { Fail(e); }
        }
        public static void RegressionPhase2()
        {
            IceMenuFactory.RequireIsolated(); File.WriteAllText("UIAuthoringPhase2ValidationProject.txt", "Phase 4 disposable regression only.");
            IceResurfacer.UI.Editor.IceMachineryConsoleValidation.VerifyIntegrated();
        }
        public static void RegressionPhase3()
        {
            IceMenuFactory.RequireIsolated(); File.WriteAllText("UIAuthoringPhase3ValidationProject.txt", "Phase 4 disposable regression only.");
            IceNavigationValidation.VerifyIntegrated();
        }
        public static void RegressionRoutes()
        {
            IceMenuFactory.RequireIsolated(); File.WriteAllText("UIAuthoringPhase3ValidationProject.txt", "Phase 4 disposable regression only.");
            IceNavigationValidation.CommandsBatch();
        }
        public static void RegressionInputs()
        {
            try
            {
                IceMenuFactory.RequireIsolated(); SessionState.SetString(KeyName + "Exact", Hash(IceMenuFactory.ScenePath));
                EditorSceneManager.OpenScene(IceMenuFactory.ScenePath); Begin("inputs-play");
            }
            catch (Exception e) { Fail(e); }
        }
        static void Locate() { game = Object.FindAnyObjectByType<IceResurfacerGame>(); menus = Object.FindAnyObjectByType<IceMenuScreens>(FindObjectsInactive.Include); }
        static IceMenuScreen[] Screens => new[] { menus.welcome, menus.pause, menus.help, menus.results };
        static IceMenuAction Action(IceMenuScreen screen, IceMenuActionKind kind) => menus.actions.Single(a => a.screen == screen && a.kind == kind);
        static IceMenuSetting Setting(IceMenuSettingKind kind) => menus.settings.Single(s => s.kind == kind);
        static IceResultWidget Widget(IceResultKind kind) => menus.resultWidgets.Single(w => w.kind == kind);
        static void Bindings()
        {
            Check(game != null && menus != null && menus.IsAvailable && Screens.Length == 4, "Four persistent editable screen roots have explicit scene bindings before Play.");
            Check(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length == 1 && Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include).Length == 1,
                "Uses exactly the existing Canvas and EventSystem.");
            Check(menus.actions.Length == 12 && menus.actions.All(a => a.owner == menus && a.screen != null && a.button != null), "All twelve menu actions bind explicitly, independent of hierarchy order.");
            Check(menus.settings.Length == 2 && menus.settings.All(s => s.owner == menus && s.screen == menus.pause && s.value != null && s.detail != null), "Both settings retain independently editable graphics and value/detail labels.");
            Check(menus.resultWidgets.Select(w => w.kind).Distinct().Count() == 17, "Seventeen explicit RunReport bindings retain every original metric and status.");
            Check(Screens.All(s => s.previewBadge.activeSelf && s.navigationOrder.Length > 0), "Saved sample-data badges and keyboard targets exist for all screens.");
            Check(PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(IceMenuFactory.ButtonPath)) == PrefabAssetType.Variant &&
                PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(IceMenuFactory.SettingPath)) == PrefabAssetType.Variant,
                "Menu button and settings are real variants of existing Phase 2/3 art.");
            Check(Action(menus.welcome, IceMenuActionKind.StartScored).GetComponentInChildren<TMP_Text>().text == "Start scored session", "Nested prefab captions preserve authored text overrides.");
        }
        static void Preview()
        {
            var groups = menus.canvas.GetComponentsInChildren<CanvasGroup>(true);
            var before = groups.Select(g => (g.alpha, g.interactable, g.blocksRaycasts)).ToArray();
            bool has = PlayerPrefs.HasKey(IceOperatorPreferences.SpeedKey); float pref = PlayerPrefs.GetFloat(IceOperatorPreferences.SpeedKey);
            foreach (var screen in Screens)
            {
                IceMenuPreview.Show(menus, screen.kind);
                Check(Screens.Count(s => s.visibility.alpha > .5f) == 1 && screen.visibility.alpha == 1 && !screen.visibility.interactable,
                    "Edit Mode preview selects only " + screen.kind + " without enabling runtime input.");
            }
            IceMenuPreview.Restore();
            Check(groups.Select((g, i) => g.alpha == before[i].alpha && g.interactable == before[i].interactable && g.blocksRaycasts == before[i].blocksRaycasts).All(b => b),
                "Restore returns every prior CanvasGroup value exactly.");
            IceMenuPreview.Show(menus, IceMenuScreenKind.Results);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Check(!IceMenuPreview.Active && groups.Select((g, i) => g.alpha == before[i].alpha).All(b => b), "Saving automatically restores temporary screen visibility.");
            Check(game.Sim == null && !game.Started && has == PlayerPrefs.HasKey(IceOperatorPreferences.SpeedKey) && pref == PlayerPrefs.GetFloat(IceOperatorPreferences.SpeedKey),
                "Preview neither constructs a simulation nor changes PlayerPrefs.");
        }
        static Image Background => menus.welcome.transform.Find("Full-screen background - replace artwork").GetComponent<Image>();
        static void EditAndSave()
        {
            Directory.CreateDirectory("Assets/Phase4ValidationArt");
            var texture = new Texture2D(4, 4); texture.SetPixels(Enumerable.Repeat(new Color(.16f, .24f, .29f), 16).ToArray()); texture.Apply();
            string path = "Assets/Phase4ValidationArt/Custom background.png"; File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport();
            var button = Action(menus.welcome, IceMenuActionKind.StartScored); var rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition += new Vector2(19, 7); rect.localRotation = Quaternion.Euler(0, 0, 1); rect.sizeDelta = new Vector2(342, 51);
            var text = button.GetComponentInChildren<TMP_Text>(); text.fontSize = 21; text.fontStyle = FontStyles.Bold;
            var image = button.GetComponent<Image>(); image.color = new Color(.59f, .71f, .65f); image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var style = image.GetComponent<IceUIStyle>(); style.applyColor = style.applySprite = style.applyButtonStates = false;
            Background.sprite = image.sprite; Background.GetComponent<IceUIStyle>().applySprite = false; Background.color = Color.white;
            Widget(IceResultKind.Score).value.rectTransform.sizeDelta = new Vector2(557, 49); Widget(IceResultKind.Score).value.fontSize = 32;
            foreach (var component in menus.GetComponentsInChildren<Component>(true)) if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); Check(EditorSceneManager.SaveScene(SceneManager.GetActiveScene()), "Deliberate button move/rotation/reskin, background sprite replacement and score resize saved.");
        }
        static void CheckSaved()
        {
            var button = Action(menus.welcome, IceMenuActionKind.StartScored); var rect = button.GetComponent<RectTransform>(); var text = button.GetComponentInChildren<TMP_Text>();
            Check(Vector2.Distance(rect.anchoredPosition, new Vector2(56, -285)) < .001f && rect.sizeDelta == new Vector2(342, 51) && Quaternion.Angle(rect.localRotation, Quaternion.Euler(0, 0, 1)) < .01f,
                "Startup button position, rotation and size survive reopening/Play.");
            Check(text.fontSize == 21 && text.fontStyle == FontStyles.Bold && button.GetComponent<Image>().sprite != null && AssetDatabase.GetAssetPath(button.GetComponent<Image>().sprite).Contains("Phase4ValidationArt"),
                "Local TMP styling and replacement button sprite survive reopening/Play.");
            Check(AssetDatabase.GetAssetPath(Background.sprite).Contains("Phase4ValidationArt") && !Background.GetComponent<IceUIStyle>().applySprite &&
                Widget(IceResultKind.Score).value.rectTransform.sizeDelta == new Vector2(557, 49) && Widget(IceResultKind.Score).value.fontSize == 32,
                "Full-screen background and independently resized score survive reopening/Play.");
        }
        static void Begin(string stage)
        {
            SessionState.SetString(KeyName, stage); frames = step = 0; next = 0; started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 480) throw new TimeoutException("Phase 4 acceptance timed out.");
                string stage = SessionState.GetString(KeyName, "");
                if (stage.EndsWith("-play"))
                {
                    if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 50 || EditorApplication.timeSinceStartup < next) return;
                    if (step == 0) { Locate(); PrepareInput(); if (!game.HasFocus) Invoke(game, "OnApplicationFocus", true); Refresh(); }
                    if (stage == "procedural-play") { ProceduralTest(); ExitPlay(stage); return; }
                    if (stage == "inputs-play")
                    {
                        game.NewSession(false); game.RouteGuide.ConfigureValidationStorage(Path.GetFullPath("Assets/Experiments/IceResurfacer/Validation~/Phase4InputRegression")); Refresh();
                        var surveyRect = (RectTransform)game.AuthoredSurvey.transform; var gpsRect = (RectTransform)game.AuthoredGps.transform;
                        Vector3 surveyPosition = surveyRect.position, gpsPosition = gpsRect.position;
                        // Move only the navigation presentation out of the fixture's cockpit
                        // sight lines. Its live registrations and pointer code stay enabled.
                        try
                        {
                            surveyRect.position += Vector3.right * Screen.width * 3; gpsRect.position += Vector3.right * Screen.width * 3; Canvas.ForceUpdateCanvases();
                            IceResurfacer.UI.Editor.IceUIInputValidation.Run(game.AuthoredHud);
                            IceResurfacer.UI.Editor.IceMachineryPhysicalValidation.Run(game.AuthoredConsole);
                        }
                        finally { surveyRect.position = surveyPosition; gpsRect.position = gpsPosition; Refresh(); }
                        Check(true, "Reused full Phase 1 HUD/cockpit and Phase 2 physical/keyboard parity suites completed with Phase 4 installed."); ExitPlay(stage); return;
                    }
                    if (stage == "restart-play") { CheckSaved(); Check(Mathf.Abs(game.SelectedForwardSpeedMph - 6.25f) < .001f, "Restart initializes live simulation from the persisted speed."); ExitPlay(stage); return; }
                    if (step == 0)
                    {
                        if (stage == "edited-play") CheckSaved();
                        Check(game.HasAuthoredMenus && menus.CurrentScreen == menus.welcome && !game.Started, "Startup shows welcome from existing game flags.");
                        Check(EventSystem.current.sendNavigationEvents && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(menus.welcome.transform), "Initial keyboard focus belongs to welcome.");
                        menus.MoveFocus(1); var selected = EventSystem.current.currentSelectedGameObject; menus.MoveFocus(-1);
                        Check(selected != EventSystem.current.currentSelectedGameObject, "Tab-order traversal supports forward and backward navigation.");
                        Click(Action(menus.welcome, IceMenuActionKind.StartScored)); clickedSimulation = game.Sim;
                        Check(game.Started && !game.Sim.Practice && !game.Sim.GuidedTraining && menus.CurrentScreen == null, "Real uGUI scored button starts existing scored free simulation.");
                        Action(menus.welcome, IceMenuActionKind.StartScored).button.onClick.Invoke();
                        Check(game.Sim == clickedSimulation && !EventSystem.current.sendNavigationEvents, "Duplicate callback cannot restart the session; gameplay restores previous navigation policy.");
                        ModalAndIntegration(); game.NewSession(false, false);
                    }
                    else if (step == 1)
                    {
                        Click(Action(menus.welcome, IceMenuActionKind.StartPractice));
                        Check(game.Sim.Practice && !game.Sim.GuidedTraining, "Practice starts unscored free simulation.");
                        game.NewSession(false, false);
                    }
                    else if (step == 2)
                    {
                        var action = Action(menus.welcome, IceMenuActionKind.StartTraining);
                        EventSystem.current.SetSelectedGameObject(action.gameObject); Keys(Key.Enter); module.Process(); Keys(); module.Process();
                        Check(game.Sim.Practice && game.Sim.GuidedTraining && game.Started, "Input System keyboard Submit starts existing unscored assisted training.");
                        game.Finish(); Check(!game.Sim.Finished, "Assisted training retains existing finish/equipment interlocks.");
                        game.NewSession(false); KeyPress(Key.Escape); Check(game.Paused && menus.CurrentScreen == menus.pause, "Escape opens pause without creating an independent state.");
                        TestSettings(); KeyPress(Key.F1); Check(game.Paused && game.HelpVisible && menus.CurrentScreen == menus.help, "F1 from Pause opens help while retaining Paused.");
                        TestHelp(); KeyPress(Key.F1); Check(game.Paused && !game.HelpVisible && menus.CurrentScreen == menus.pause, "Closing help returns to Pause without resuming.");
                    }
                    else if (step == 3)
                    {
                        Click(Action(menus.pause, IceMenuActionKind.Resume)); Check(!game.Paused && menus.CurrentScreen == null, "Resume returns to gameplay.");
                        KeyPress(Key.F1); KeyPress(Key.F1); Check(!game.Paused && menus.CurrentScreen == null, "Help from gameplay returns to running gameplay.");
                        game.TogglePause();
                    }
                    else if (step == 4)
                    {
                        Click(Action(menus.pause, IceMenuActionKind.NewSessionSheet)); Check(!game.Started && !game.Paused && menus.CurrentScreen == menus.welcome, "Pause New session returns to the original welcome workflow.");
                        game.NewSession(false); game.TogglePause();
                    }
                    else if (step == 5)
                    {
                        Click(Action(menus.pause, IceMenuActionKind.RefillPractice));
                        Check(game.Started && game.Sim.Practice && !game.Paused && !game.Sim.GuidedTraining, "Refill practice starts the existing unscored service session.");
                        Check(Mathf.Abs(game.SelectedForwardSpeedMph - 6.25f) < .001f, "Saved quarter-mph selection survives new sessions.");
                        game.NewSession(false); game.Sim.Step(2.4f); game.Finish(); Refresh(); ActualResults(false);
                        KeyPress(Key.F1); Check(menus.CurrentScreen == menus.help, "Results can open help."); KeyPress(Key.F1); Check(menus.CurrentScreen == menus.results, "Help closes back to the finished report.");
                        Measure(); Capture(stage == "edited-play" ? "edited" : "default");
                    }
                    else if (step == 6)
                    {
                        game.NewSession(false); game.Finish(); Refresh(); Click(Action(menus.results, IceMenuActionKind.StartPractice));
                        Check(game.Started && game.Sim.Practice && !game.Sim.Finished, "Results New practice starts a fresh unscored run.");
                        game.Finish(); Refresh(); ActualResults(true);
                    }
                    else if (step == 7)
                    {
                        Click(Action(menus.results, IceMenuActionKind.StartScored)); Check(game.Started && !game.Sim.Practice && !game.Sim.Finished, "Results New scored starts a fresh scored run.");
                        game.NewSession(false, false); var button = Action(menus.welcome, IceMenuActionKind.StartScored); Vector2 at = Center(button.GetComponent<RectTransform>());
                        Pointer(at); Pointer(at, true); game.ToggleHelp(); Refresh(); game.ToggleHelp(); Refresh(); Pointer(at);
                        Check(!game.Started && menus.CurrentScreen == menus.welcome, "Interrupted button press cannot fire after hide/reopen.");
                    }
                    else { ExitPlay(stage); return; }
                    step++; next = EditorApplication.timeSinceStartup + .12;
                }
                else if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if (stage == "edited-exit")
                    {
                        EditorSceneManager.OpenScene(IceMenuFactory.ScenePath); Locate(); CheckSaved(); Check(Screens.All(s => s.previewBadge.activeSelf), "Leaving Play restores saved sample badges.");
                        EditorSceneManager.OpenScene(Procedural); Begin("procedural-play");
                    }
                    else if (stage == "procedural-exit") { Check(Hash(Procedural) == SessionState.GetString(KeyName + "Procedural", ""), "Procedural scene remains byte-identical."); Finish(); }
                    else if (stage == "exact-exit") { Check(Hash(IceMenuFactory.ScenePath) == SessionState.GetString(KeyName + "Exact", ""), "Exact delivered scene retains every serialized byte after Play."); Finish(); }
                    else if (stage == "inputs-exit") { Check(Hash(IceMenuFactory.ScenePath) == SessionState.GetString(KeyName + "Exact", ""), "Full input regression retains exact delivered scene bytes."); Finish(); }
                    else if (stage == "restart-exit") Finish();
                }
            }
            catch (Exception e) { Fail(e); }
        }
        static void TestSettings()
        {
            var speed = Setting(IceMenuSettingKind.ForwardSpeed); var eye = Setting(IceMenuSettingKind.SeatedEye);
            Check(speed.slider.minValue == 3 && speed.slider.maxValue == 8 && !speed.slider.wholeNumbers && eye.slider.minValue == -.08f && eye.slider.maxValue == .12f,
                "Speed and eye ranges match the existing constraints.");
            speed.slider.value = 6.13f; Check(Mathf.Abs(game.SelectedForwardSpeedMph - 6.25f) < .001f && Mathf.Abs(speed.slider.value - 6.25f) < .001f, "Slider snaps 6.13 to the authoritative 6.25 mph quarter-step.");
            game.ToggleHelp(); speed.slider.value = 3.14f;
            Check(Mathf.Abs(speed.slider.value - 6.25f) < .001f && Mathf.Abs(game.SelectedForwardSpeedMph - 6.25f) < .001f,
                "A late slider callback after hiding Pause restores its display without changing the setting."); game.ToggleHelp();
            var anchor = game.World.OperatorControls.driverEyeAnchor; Vector3 position = anchor.position; Quaternion rotation = anchor.rotation;
            Vector3 before = (Vector3)Invoke(game, "HeadOffset", 0f); eye.slider.value = .12f; Vector3 after = (Vector3)Invoke(game, "HeadOffset", 0f);
            Check(game.UsesAuthoredDriverEye && before == after && anchor.position == position && anchor.rotation == rotation && game.operatorEyeHeight == .12f,
                "Seated fallback value changes without moving or double-offsetting the authored eye anchor.");
            var rect = speed.slider.GetComponent<RectTransform>(); Vector2 at = Center(rect); Pointer(at); Pointer(at, true); float held = game.SelectedForwardSpeedMph;
            Invoke(game, "OnApplicationFocus", false); Refresh(); Invoke(game, "OnApplicationFocus", true); Refresh();
            Pointer(at + Vector2.right * 120, true); Check(game.SelectedForwardSpeedMph == held, "A speed drag canceled by focus loss cannot continue while held after focus returns.");
            Pointer(at); speed.slider.value = 6.25f;
            Check(PlayerPrefs.GetFloat(IceOperatorPreferences.SpeedKey) == 6.25f, "Speed uses existing PlayerPrefs save path.");
        }
        static void ModalAndIntegration()
        {
            game.RouteGuide.ConfigureValidationStorage(Path.GetFullPath("Assets/Experiments/IceResurfacer/Validation~/Phase4Routes/" + DateTime.UtcNow.Ticks));
            Refresh(); Check(game.HasAuthoredHud && game.HasAuthoredConsole && game.HasAuthoredSurvey && game.HasAuthoredGps && game.HasAuthoredRoutes && game.AuthoredHud.Visible,
                "All five Phase 1-3 modules remain registered and live after starting a session.");
            game.AuthoredConsole.SetCompact(false); Refresh();
            var control = game.AuthoredConsole.controls.Single(c => c.kind == IceConsoleControlKind.Conditioner);
            var area = control.interactionArea; Vector2 hold = RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(new Vector3(area.rect.center.x, area.rect.yMax - 4)));
            Pointer(hold); Pointer(hold, true); game.AuthoredConsole.TickInput(); Check(control.IsHolding, "Existing hydraulic lever owns the live pointer before Pause.");
            game.TogglePause(); Refresh(); Check(!control.IsHolding && !game.AuthoredConsole.CanInteract && !game.AuthoredHud.Visible && !game.AuthoredSurvey.Visible && !game.AuthoredGps.Visible,
                "Pause releases held hydraulics and blocks all underlying authored driving interfaces.");
            Pointer(hold); var data = new PointerEventData(EventSystem.current) { position = hold }; var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Check(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(menus.pause.transform), "Actual modal background raycast belongs to Pause at an underlying machine control.");
            game.TogglePause(); Refresh();
            bool auger = game.Sim.Controls.VerticalAuger; var toggle = game.AuthoredConsole.controls.Single(c => c.kind == IceConsoleControlKind.VerticalAuger);
            Vector2 at = Center(toggle.interactionArea); Pointer(at); Pointer(at, true); Pointer(at); Check(game.Sim.Controls.VerticalAuger != auger, "Machinery button remains functional after modal close.");
            game.AuthoredConsole.SetCompact(true); game.RouteGuide.ToggleRecordingPanel(); Refresh();
            Check(game.AuthoredRoutes.Visible && !menus.ModalVisible, "Existing Route Manager retains modal priority."); game.RouteGuide.ToggleRecordingPanel(); Refresh();
            Check(game.AuthoredSurvey.Visible && game.AuthoredGps.Visible && menus.welcome.visibility.alpha == 0 && !menus.welcome.visibility.blocksRaycasts,
                "Closed screens leave no invisible full-screen blocker; map/GPS resume.");
        }
        static void TestHelp()
        {
            var scroll = menus.help.GetComponentInChildren<ScrollRect>(); foreach (var h in scroll.content.GetComponentsInChildren<IceUITextHeight>()) h.Refresh();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content); Canvas.ForceUpdateCanvases();
            Check(scroll.content.GetComponentsInChildren<IceUITextHeight>().Length == 6 && scroll.content.rect.height > scroll.viewport.rect.height,
                "Six authored help sections grow to content and use a scrollable viewport.");
            scroll.verticalNormalizedPosition = 1; Vector2 at = Center(scroll.viewport); Pointer(at);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at, scroll = new Vector2(0, -120) }); InputSystem.Update(); module.Process();
            Check(scroll.verticalNormalizedPosition < .999f, "Actual Input System wheel scrolls the help instructions."); scroll.verticalNormalizedPosition = 1;
        }
        static void ActualResults(bool practice)
        {
            RunReport report = game.Sim.GetReport(); Check(game.Sim.Finished && menus.CurrentScreen == menus.results && game.Report.Score == report.Score && game.Report.Practice == practice, "Results bind the actual finished simulation report.");
            Check(Widget(IceResultKind.Score).value.text == (practice ? Widget(IceResultKind.Score).practiceText : string.Format("{0:0.0} / 100", report.Score)), "Score/practice text uses actual RunReport without UI scoring.");
            Check(Widget(IceResultKind.Coverage).value.text == string.Format("{0:0.0}%", report.Coverage * 100) && Widget(IceResultKind.SelectedSpeed).value.text == string.Format("{0:F2} MPH", report.SelectedMaxForwardSpeedMph), "Coverage and selected speed use explicit report fields.");
            var readings = new[] {
                (IceResultKind.FinishQuality, report.FinishQuality), (IceResultKind.WaterDistribution, report.WaterDistribution),
                (IceResultKind.Missed, report.MissedFraction), (IceResultKind.ExcessWater, report.OverwaterFraction),
                (IceResultKind.Efficiency, report.OperationalEfficiency), (IceResultKind.ShavingCoverage, report.ShavingCoverage), (IceResultKind.WaterCoverage, report.WaterCoverage) };
            foreach (var reading in readings) Check(Widget(reading.Item1).value.text == string.Format("{0:0.0}%", reading.Item2 * 100), "Actual report metric: " + reading.Item1);
            Check(Widget(IceResultKind.Handling).value.text == string.Format("{0:0} dumps", report.TankDumps) && Widget(IceResultKind.Misuse).value.text == string.Format("{0:0} faults", report.Mistakes) &&
                Widget(IceResultKind.OriginalSpeed).value.text == string.Format("{0:F2} MPH", report.OriginalMaxForwardSpeedMph), "Handling, faults and original speed match the finished report.");
            if (!practice)
            {
                Check(Widget(IceResultKind.Coverage).points.text == string.Format("{0:0.0} / 40", report.CoveragePoints) &&
                    Widget(IceResultKind.FinishQuality).points.text == string.Format("{0:0.0} / 30", report.FinishPoints) &&
                    Widget(IceResultKind.WaterDistribution).points.text == string.Format("{0:0.0} / 10", report.WaterPoints) &&
                    Widget(IceResultKind.ExcessWater).points.text == string.Format("{0:0.0} / 10", report.OverwaterPoints) &&
                    Widget(IceResultKind.Efficiency).points.text == string.Format("{0:0.0} / 5", report.EfficiencyPoints) &&
                    Widget(IceResultKind.Handling).points.text == string.Format("{0:0.0} / 5", report.HandlingPoints) &&
                    Widget(IceResultKind.Misuse).points.text == string.Format("-{0:0.0}", report.MisusePenalty), "Every score and penalty column uses the authoritative report points.");
            }
            Check(practice ? report.Score == 0 && menus.resultWidgets.All(w => w.points == null || w.points.text == "") : !report.Complete && Widget(IceResultKind.Completion).value.text == Widget(IceResultKind.Completion).partialText,
                practice ? "Practice remains unscored and has no scoring point breakdown." : "Untreated scored run retains actual partial-sheet classification.");
            var sample = report; sample.Complete = true; Widget(IceResultKind.Heading).Refresh(in sample, false); Widget(IceResultKind.Completion).Refresh(in sample, false);
            Check(Widget(IceResultKind.Completion).value.text == Widget(IceResultKind.Completion).completeText, "Complete-report formatter supports authoritative Complete flag (representative formatter fixture only)."); menus.RefreshResults();
        }
        static void Measure()
        {
            int count = menus.ResultRefreshCount, objects = menus.GetComponentsInChildren<Transform>(true).Length;
            var watch = System.Diagnostics.Stopwatch.StartNew(); for (int i = 0; i < 1000; i++) menus.RefreshState(); watch.Stop();
            Check(menus.ResultRefreshCount == count && menus.GetComponentsInChildren<Transform>(true).Length == objects, "One thousand unchanged-result refreshes neither rebuild labels nor instantiate objects.");
            Pass("MEASUREMENT visible unchanged result state: " + (watch.Elapsed.TotalMilliseconds / 1000).ToString("F5") + " ms/call, excludes rendering and other UI.");
            long before = GC.GetAllocatedBytesForCurrentThread(); var calibration = new byte[4096]; long bytes = GC.GetAllocatedBytesForCurrentThread() - before; GC.KeepAlive(calibration);
            if (bytes < 4096) Pass("MEASUREMENT managed allocation counter UNAVAILABLE (4096-byte calibration reported " + bytes + "); full-frame Profiler remains manual.");
            else { before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) menus.RefreshState(); Pass("MEASUREMENT result-state bytes/call=" + (GC.GetAllocatedBytesForCurrentThread() - before) / 1000); }
        }
        static void Capture(string variant)
        {
            var canvas = menus.canvas; var camera = game.DriverCamera; var mode = canvas.renderMode; var priorCamera = canvas.worldCamera;
            float distance = canvas.planeDistance, aspect = camera.aspect; var target = camera.targetTexture; var active = RenderTexture.active;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .3f;
                foreach (var size in new[] { new Vector2Int(1366, 768), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) })
                foreach (var kind in new[] { IceMenuScreenKind.Welcome, IceMenuScreenKind.Pause, IceMenuScreenKind.Help, IceMenuScreenKind.Results })
                {
                    game.NewSession(false, kind == IceMenuScreenKind.Pause || kind == IceMenuScreenKind.Results);
                    if (kind == IceMenuScreenKind.Pause) game.TogglePause(); if (kind == IceMenuScreenKind.Help) game.ToggleHelp(); if (kind == IceMenuScreenKind.Results) game.Finish(); Refresh();
                    var rt = new RenderTexture(size.x, size.y, 24); var png = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = rt; camera.aspect = size.x / (float)size.y; Canvas.ForceUpdateCanvases();
                        foreach (var h in menus.help.GetComponentsInChildren<IceUITextHeight>()) h.Refresh(); Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                        png.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); png.Apply(); File.WriteAllBytes(Path.Combine(Output, "Phase4-" + variant + "-" + kind + "-" + size.x + "x" + size.y + ".png"), png.EncodeToPNG());
                        Check(menus.CurrentScreen != null && menus.CurrentScreen.kind == kind && Screens.Count(s => s.visibility.alpha == 1) == 1, "Rendered only " + kind + " at " + size);
                        foreach (var a in menus.actions.Where(a => a.screen.kind == kind))
                        {
                            var corners = new Vector3[4]; a.button.GetComponent<RectTransform>().GetWorldCorners(corners);
                            Check(corners.All(c => { Vector2 p = RectTransformUtility.WorldToScreenPoint(camera, c); return p.x >= 0 && p.y >= 0 && p.x <= size.x && p.y <= size.y; }), "Action within target viewport: " + a.name);
                        }
                    }
                    finally { camera.targetTexture = target; RenderTexture.active = active; Object.Destroy(png); Object.Destroy(rt); }
                }
            }
            finally { canvas.renderMode = mode; canvas.worldCamera = priorCamera; canvas.planeDistance = distance; camera.targetTexture = target; camera.aspect = aspect; RenderTexture.active = active; Refresh(); }
        }
        static void ProceduralTest()
        {
            Check(menus == null && !game.HasAuthoredMenus && !game.Started && game.Sim != null, "Procedural scene retains its legacy welcome and has no authored menu registration.");
            game.NewSession(true); KeyPress(Key.Escape); KeyPress(Key.F1); Check(game.Paused && game.HelpVisible, "Procedural legacy pause/help state paths remain functional.");
            KeyPress(Key.F1); KeyPress(Key.Escape); Keys(Key.Digit3); Invoke(game, "Update"); Keys(); Check(game.Sim.Controls.VerticalAuger, "Procedural keyboard machinery controls still work.");
            game.Finish(); Check(game.Sim.Finished && game.Report.Practice && game.Report.Score == 0, "Procedural practice finish retains legacy report behavior.");
        }
        static void Refresh()
        {
            menus?.RefreshState(); game.AuthoredHud?.RefreshNow(); game.AuthoredConsole?.RefreshNow(); game.AuthoredSurvey?.RefreshNow(); game.AuthoredGps?.RefreshNow(); game.AuthoredRoutes?.SyncVisibility(); Canvas.ForceUpdateCanvases();
        }
        static Vector2 Center(RectTransform r) => RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center));
        static void Click(IceMenuAction action) { Refresh(); Vector2 at = Center(action.button.GetComponent<RectTransform>()); Pointer(at); Pointer(at, true); Pointer(at); Refresh(); }
        static void Pointer(Vector2 at, bool down = false) { InputSystem.QueueStateEvent(mouse, new MouseState { position = at }.WithButton(MouseButton.Left, down)); InputSystem.Update(); module.Process(); }
        static void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        static void KeyPress(Key key) { Keys(key); Invoke(game, "Update"); Keys(); Invoke(game, "Update"); Refresh(); }
        static void PrepareInput()
        {
            previousSettings = InputSystem.settings; temporarySettings = Object.Instantiate(previousSettings); temporarySettings.hideFlags = HideFlags.HideAndDontSave;
            temporarySettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; temporarySettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; InputSystem.settings = temporarySettings;
            oldMouse = Mouse.current; oldKeyboard = Keyboard.current; mouse = InputSystem.AddDevice<Mouse>("Phase4 acceptance mouse"); mouse.MakeCurrent(); keyboard = InputSystem.AddDevice<Keyboard>("Phase4 acceptance keyboard"); keyboard.MakeCurrent();
            module = EventSystem.current?.GetComponent<InputSystemUIInputModule>();
        }
        static void RestoreInput()
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse); if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (oldMouse != null && oldMouse.added) oldMouse.MakeCurrent(); if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
            if (previousSettings != null) InputSystem.settings = previousSettings; if (temporarySettings != null) Object.Destroy(temporarySettings);
            mouse = oldMouse = null; keyboard = oldKeyboard = null; previousSettings = temporarySettings = null;
        }
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        static string Hash(string path) { using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
        static void ExitPlay(string stage) { RestoreInput(); SessionState.SetString(KeyName, stage.Replace("-play", "-exit")); EditorApplication.ExitPlaymode(); }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Pass(message); }
        static void Pass(string message) { Directory.CreateDirectory(Output); File.AppendAllText(Results, "PASS " + message + "\n"); }
        static void Finish() { Pass("Phase 4 automated Unity run complete; native input, complete guided run and full-frame Profiler remain separate manual acceptance."); SessionState.SetString(KeyName, ""); EditorApplication.update -= Tick; EditorApplication.Exit(0); }
        static void Fail(Exception e)
        {
            RestoreInput(); SessionState.SetString(KeyName, ""); EditorApplication.update -= Tick; Debug.LogException(e);
            if (File.Exists("UIAuthoringPhase4ValidationProject.txt")) { Directory.CreateDirectory(Output); File.AppendAllText(Results, "FAIL " + e + "\n"); }
            EditorApplication.Exit(1);
        }
    }
}
#endif
