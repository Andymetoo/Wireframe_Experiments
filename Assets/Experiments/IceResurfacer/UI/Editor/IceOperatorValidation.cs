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
    [InitializeOnLoad]
    public static class IceOperatorValidation
    {
        const string SessionKey = "Aurora.Phase5.Acceptance";
        const string Procedural = "Assets/Experiments/IceResurfacer/Scenes/IceResurfacer.unity";
        static string Output => Path.GetFullPath("ValidationResults");
        static string Results => Path.Combine(Output, "Phase5-results.txt");
        static IceResurfacerGame game;
        static IceOperatorOverlay ui;
        static Mouse mouse, oldMouse;
        static Keyboard keyboard, oldKeyboard;
        static InputSettings originalSettings, inputSettings;
        static InputSystemUIInputModule module;
        static int step, frames;
        static double started, next;
        static string variant;
        static bool serviceCaptured, sawFlood, sawWash, sawOverflow;
        static IceOperatorValidation()
        {
            if (SessionState.GetString(SessionKey, "") == "") return;
            started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick;
        }
        public static void RunBatch()
        {
            try
            {
                IceOperatorFactory.RequireIsolated(); Directory.CreateDirectory(Output);
                File.WriteAllText(Results, "Phase 5 isolated real Unity acceptance. Input is injected; native hardware/cursor and combined IMGUI remain separate manual checks.\n");
                SessionState.SetString(SessionKey + "Procedural", Hash(Procedural));
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                File.Copy("ValidationResults/Installed-Phase5.unity", IceOperatorFactory.ScenePath, true); AssetDatabase.ImportAsset(IceOperatorFactory.ScenePath, ImportAssetOptions.ForceUpdate);
                EditorSceneManager.OpenScene(IceOperatorFactory.ScenePath); Locate(); Bindings(); Preview(); EditAndSave();
                EditorSceneManager.OpenScene(IceOperatorFactory.ScenePath); Locate(); CheckSaved();
                File.Copy(IceOperatorFactory.ScenePath, Path.Combine(Output, "Authored-persistence.unity"), true); Begin("edited-play");
            }
            catch (Exception e) { Fail(e); }
        }
        public static void VerifyPersisted()
        {
            try { IceOperatorFactory.RequireIsolated(); EditorSceneManager.OpenScene(IceOperatorFactory.ScenePath); Locate(); CheckSaved(); Pass("Fresh Editor process preserves moved service UI, resized warning, custom font and replacement background."); EditorApplication.Exit(0); }
            catch (Exception e) { Fail(e); }
        }
        public static void VerifyIntegrated()
        {
            try
            {
                IceOperatorFactory.RequireIsolated(); SessionState.SetString(SessionKey + "Exact", Hash(IceOperatorFactory.ScenePath));
                EditorSceneManager.OpenScene(IceOperatorFactory.ScenePath); Locate(); Bindings(); Begin("exact-play");
            }
            catch (Exception e) { Fail(e); }
        }
        static void Phase4Marker() { IceOperatorFactory.RequireIsolated(); File.WriteAllText("UIAuthoringPhase4ValidationProject.txt", "Phase 5 disposable regression only."); }
        public static void RegressionMenus() { Phase4Marker(); IceMenuValidation.VerifyIntegrated(); }
        public static void RegressionInputs() { Phase4Marker(); IceMenuValidation.RegressionInputs(); }
        public static void RegressionRoutes() { Phase4Marker(); IceMenuValidation.RegressionRoutes(); }
        public static void RegressionNavigation() { Phase4Marker(); IceMenuValidation.RegressionPhase3(); }
        public static void FinalizeNewAssets()
        {
            try { IceOperatorFactory.FinalizeNewDefaults(); EditorApplication.Exit(0); }
            catch (Exception e) { Fail(e); }
        }
        public static void CaptureProjection()
        {
            try
            {
                IceOperatorFactory.RequireIsolated(); SessionState.SetString(SessionKey + "Exact", Hash(IceOperatorFactory.ScenePath));
                EditorSceneManager.OpenScene(IceOperatorFactory.ScenePath); Begin("projection-play");
            }
            catch (Exception e) { Fail(e); }
        }
        static void Locate() { game = Object.FindAnyObjectByType<IceResurfacerGame>(); ui = Object.FindAnyObjectByType<IceOperatorOverlay>(FindObjectsInactive.Include); }
        static IceServiceReadout Field(IceServiceField kind) => ui.serviceFields.Single(f => f.kind == kind);
        static IceOperatorAction Action(IceOperatorActionKind kind) => ui.actions.Single(a => a.kind == kind);
        static void Bindings()
        {
            Check(game != null && ui != null && ui.IsAvailable && ui.game == game, "Persistent Phase 5 root and explicit scene bindings exist before Play.");
            Check(ui.serviceFields.Select(f => f.kind).Distinct().Count() == 7 && ui.serviceFields.All(f => f.value != null), "Seven independently editable service fields retain explicit data kinds.");
            Check(ui.actions.Length == 6 && ui.actions.All(a => a.owner == ui && a.button != null), "Six existing status/history commands use persistent bound buttons.");
            Check(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length == 1 && Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include).Length == 1, "One existing Canvas and EventSystem; no added input architecture.");
            Check(PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(IceOperatorFactory.NoticePath)) == PrefabAssetType.Variant && ui.historyRowPrefab != null, "Notification variants and bounded history use editable prefab sources.");
            Check(ui.previewBadge.activeSelf && ui.historyPreviewRows.GetComponentsInChildren<IceMessageHistoryRow>().Length == 3, "Saved preview values and three clearly identified history samples need no simulation.");
            Check(ui.gpsProjection != null && ui.gpsProjection.owner == ui && !ui.gpsProjection.raycastTarget, "Projected GPS is a saved editable non-raycasting graphic with explicit owner.");
            Check(new[] { ui.routine, ui.critical, ui.driveWarning, ui.hint }.All(m => !m.GetComponent<Image>().raycastTarget) && !ui.performanceGroup.GetComponent<Image>().raycastTarget,
                "Informational notice/hint/warning/F4 art cannot steal physical-control clicks.");
        }
        static void Preview()
        {
            var groups = ui.canvas.GetComponentsInChildren<CanvasGroup>(true); var before = groups.Select(g => (g.alpha, g.interactable, g.blocksRaycasts)).ToArray();
            bool has = PlayerPrefs.HasKey(IceOperatorPreferences.SpeedKey); float pref = PlayerPrefs.GetFloat(IceOperatorPreferences.SpeedKey);
            foreach (IceOperatorPreviewKind kind in Enum.GetValues(typeof(IceOperatorPreviewKind))) IceOperatorPreview.Show(ui, kind);
            Check(ui.critical.visibility.alpha == 1 && ui.serviceGroup.alpha == 0 && game.Sim == null, "Warnings preview is presentation only, with no simulator.");
            var menus = Object.FindAnyObjectByType<IceMenuScreens>(); IceMenuPreview.Show(menus, IceMenuScreenKind.Help);
            Check(!IceOperatorPreview.Active && IceMenuPreview.Active, "Menu preview safely restores the operator preview before taking ownership.");
            IceOperatorPreview.Show(ui, IceOperatorPreviewKind.WaterService);
            Check(!IceMenuPreview.Active && ui.serviceGroup.alpha == 1, "Operator preview safely restores the session preview before taking ownership.");
            IceOperatorPreview.Restore();
            Check(groups.Select((g, i) => g.alpha == before[i].alpha && g.interactable == before[i].interactable && g.blocksRaycasts == before[i].blocksRaycasts).All(b => b), "Cross-phase preview Restore returns every prior group/input value exactly.");
            IceOperatorPreview.Show(ui, IceOperatorPreviewKind.History); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Check(!IceOperatorPreview.Active && has == PlayerPrefs.HasKey(IceOperatorPreferences.SpeedKey) && pref == PlayerPrefs.GetFloat(IceOperatorPreferences.SpeedKey), "Save restores transient preview; PlayerPrefs remain unchanged.");
        }
        static void EditAndSave()
        {
            Directory.CreateDirectory("Assets/Phase5ValidationArt");
            var texture = new Texture2D(4, 4); texture.SetPixels(Enumerable.Repeat(new Color(.82f, .74f, .59f), 16).ToArray()); texture.Apply();
            const string art = "Assets/Phase5ValidationArt/Notification background.png"; File.WriteAllBytes(art, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(art); var importer = (TextureImporter)AssetImporter.GetAtPath(art); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport();
            var text = ui.critical.content; const string font = "Assets/Phase5ValidationArt/Local message font.asset";
            if (!File.Exists(font)) Check(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(text.font), font), "Custom local TMP font asset fixture copied.");
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(font); text.fontSize = 18; text.fontStyle = FontStyles.Bold; text.GetComponent<IceUIStyle>().applyFont = false;
            ((RectTransform)ui.serviceGroup.transform).anchoredPosition = new Vector2(170, -380); Field(IceServiceField.Prompt).value.rectTransform.anchoredPosition = new Vector2(32, -80);
            var warning = ui.critical.GetComponent<RectTransform>(); warning.sizeDelta = new Vector2(920, 70); warning.anchoredPosition = new Vector2(300, -680);
            var image = ui.critical.GetComponent<Image>(); image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art); image.color = Color.white;
            var style = image.GetComponent<IceUIStyle>(); style.applySprite = style.applyColor = false;
            foreach (var component in ui.GetComponentsInChildren<Component>(true)) if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); Check(EditorSceneManager.SaveScene(SceneManager.GetActiveScene()), "Moved service prompt/panel, resized warning, changed font asset and replacement notification sprite saved.");
        }
        static void CheckSaved()
        {
            Check(((RectTransform)ui.serviceGroup.transform).anchoredPosition == new Vector2(170, -380) && Field(IceServiceField.Prompt).value.rectTransform.anchoredPosition == new Vector2(32, -80), "Moved service panel and independent prompt survive save/reopen/Play.");
            Check(ui.critical.GetComponent<RectTransform>().sizeDelta == new Vector2(920, 70) && ui.critical.content.fontSize == 18 && ui.critical.content.fontStyle == FontStyles.Bold &&
                AssetDatabase.GetAssetPath(ui.critical.content.font).Contains("Phase5ValidationArt") && !ui.critical.content.GetComponent<IceUIStyle>().applyFont, "Resized warning and local TMP font asset survive without theme reset.");
            Check(AssetDatabase.GetAssetPath(ui.critical.GetComponent<Image>().sprite).Contains("Phase5ValidationArt") && !ui.critical.GetComponent<IceUIStyle>().applySprite, "Replacement warning background survives reopening and Play.");
        }
        static void Begin(string stage)
        {
            SessionState.SetString(SessionKey, stage); step = frames = 0; next = 0; started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 600) throw new TimeoutException("Phase 5 acceptance timed out.");
                string stage = SessionState.GetString(SessionKey, "");
                if (stage.EndsWith("-play"))
                {
                    if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 50 || EditorApplication.timeSinceStartup < next) return;
                    if (step == 0) { Locate(); PrepareInput(); Invoke(game, "OnApplicationFocus", true); variant = stage == "edited-play" ? "edited" : "default"; }
                    if (stage == "procedural-play") { ProceduralTest(); ExitPlay(stage); return; }
                    if (stage == "projection-play") { game.NewSession(true); TestProjection(); Capture("ProjectedGPS"); ExitPlay(stage); return; }
                    if (step == 0)
                    {
                        if (stage == "edited-play") CheckSaved(); game.NewSession(true);
                        game.RouteGuide.ConfigureValidationStorage(Path.GetFullPath("Assets/Experiments/IceResurfacer/Validation~/Phase5Routes/" + DateTime.UtcNow.Ticks));
                        Refresh(); Check(game.HasAuthoredHud && game.HasAuthoredConsole && game.HasAuthoredSurvey && game.HasAuthoredGps && game.HasAuthoredRoutes && game.HasAuthoredMenus && game.HasAuthoredOperatorUI, "All seven authored presenters register together in real Play.");
                        TestMessages(); PrepareRoute(); TestProjection();
                    }
                    else if (step == 1)
                    {
                        serviceCaptured = sawFlood = sawWash = sawOverflow = false;
                        Directory.CreateDirectory("Assets/Experiments/IceResurfacer/Validation~");
                        ServiceSuite.GetMethod("Run", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { game, keyboard, mouse, (Action<string>)ObserveService, false });
                        Check(sawFlood && sawWash && sawOverflow && serviceCaptured, "Real walked F/E service sequence supplied both ports, overflow and matching three-resolution UI captures.");
                    }
                    else if (step == 2) TestServicePointer();
                    else if (step == 3)
                    {
                        game.NewSession(true); Refresh(); Click(Action(IceOperatorActionKind.History));
                        Check(game.Dashboard.HistoryVisible && ui.historyGroup.alpha == 1 && !game.Paused, "Actual Messages button opens existing nonmodal history without pausing.");
                    }
                    else if (step == 4)
                    {
                        bool before = game.Dashboard.MessagesVisible; Click(Action(IceOperatorActionKind.RoutineMessages));
                        Check(game.Dashboard.MessagesVisible != before, "Actual routine-message toggle changes the single existing message policy.");
                    }
                    else if (step == 5)
                    {
                        Click(Action(IceOperatorActionKind.CloseHistory)); Check(!game.Dashboard.HistoryVisible, "History Close uses existing dashboard state.");
                        var rect = Action(IceOperatorActionKind.History).GetComponent<RectTransform>(); Vector2 at = Center(rect);
                        Pointer(at); Pointer(at, true); game.ToggleHelp(); Refresh(); game.ToggleHelp(); Refresh(); Pointer(at);
                        Check(!game.Dashboard.HistoryVisible, "Canceled bottom-strip press cannot fire after help hide/reopen.");
                    }
                    else if (step == 6)
                    {
                        Click(Action(IceOperatorActionKind.Routes)); Refresh(); Check(game.RouteGuide.PanelOpen && !ui.Visible && !ui.statusGroup.blocksRaycasts, "Actual Routes action yields all Phase 5 input to existing manager modal.");
                        game.RouteGuide.ToggleRecordingPanel(); Refresh();
                    }
                    else if (step == 7)
                    {
                        Click(Action(IceOperatorActionKind.Pause)); Refresh(); Check(game.Paused && !ui.Visible && game.AuthoredMenus.CurrentScreen == game.AuthoredMenus.pause, "Actual Pause action uses Phase 4 modal and releases new UI.");
                    }
                    else if (step == 8)
                    {
                        game.TogglePause(); Refresh(); Click(Action(IceOperatorActionKind.Help)); Refresh(); Check(game.HelpVisible && !ui.Visible, "Actual Help action uses existing F1/help state."); game.ToggleHelp(); Refresh();
                        game.ToggleVideoMode(); Refresh(); Check(!ui.Visible && !ui.serviceGroup.blocksRaycasts && !ui.statusGroup.blocksRaycasts && ui.performanceGroup.alpha == 0, "Numpad 1 policy hides all new player-facing groups and pointer regions."); game.ToggleVideoMode(); Refresh();
                        CaptureCombinations(); Measure();
                    }
                    else { ExitPlay(stage); return; }
                    step++; next = EditorApplication.timeSinceStartup + .15;
                }
                else if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Check(!Resources.FindObjectsOfTypeAll<MeshCollider>().Any(c => c != null && c.gameObject.name.StartsWith("GPS query / ", StringComparison.Ordinal)), "Leaving Play retains no transient GPS query colliders.");
                    if (stage == "edited-exit")
                    {
                        EditorSceneManager.OpenScene(IceOperatorFactory.ScenePath); Locate(); CheckSaved(); Check(ui.previewBadge.activeSelf && ui.historyPreviewRows.activeSelf, "Exiting Play restores saved sample objects without live data writes.");
                        EditorSceneManager.OpenScene(Procedural); Begin("procedural-play");
                    }
                    else if (stage == "procedural-exit") { Check(Hash(Procedural) == SessionState.GetString(SessionKey + "Procedural", ""), "Procedural scene bytes unchanged."); Finish(); }
                    else if (stage == "exact-exit") { Check(Hash(IceOperatorFactory.ScenePath) == SessionState.GetString(SessionKey + "Exact", ""), "Exact delivered scene retains all serialized bytes after Phase 5 Play tests."); Finish(); }
                    else if (stage == "projection-exit")
                    {
                        Check(Hash(IceOperatorFactory.ScenePath) == SessionState.GetString(SessionKey + "Exact", ""), "Dedicated GPS projection render retains exact delivered scene bytes.");
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                        File.Copy("ValidationResults/Authored-persistence.unity", IceOperatorFactory.ScenePath, true); AssetDatabase.ImportAsset(IceOperatorFactory.ScenePath, ImportAssetOptions.ForceUpdate);
                        EditorSceneManager.OpenScene(IceOperatorFactory.ScenePath); Locate(); CheckSaved();
                        Pass("Final fresh-process reopen retains all deliberate Phase 5 authoring changes."); Finish();
                    }
                }
            }
            catch (Exception e) { Fail(e); }
        }
        static void TestMessages()
        {
            if (!game.Dashboard.MessagesVisible) game.Dashboard.ToggleRoutineMessages();
            game.Notice = "AUTOMATED routine notice"; Refresh(); int before = game.Dashboard.MessageHistory.Count;
            for (int i = 0; i < 20; i++) Refresh();
            Check(game.Dashboard.MessageHistory.Count == before && ui.routine.visibility.alpha > 0, "Routine notice appears once; repeated observers do not duplicate history.");
            if (!game.Dashboard.HistoryVisible) game.Dashboard.ToggleHistory();
            for (int i = 0; i < 40; i++) { game.Notice = "AUTOMATED ordered notice " + i; Refresh(); }
            Check(game.Dashboard.MessageHistory.Count == 32 && game.Dashboard.MessageHistory[31].EndsWith("39") && ui.RuntimeRowCount == 32, "History preserves newest-first display, original order and exact 32-entry limit with bounded prefab pool.");
            int count = ui.GetComponentsInChildren<Transform>(true).Length; for (int i = 0; i < 100; i++) ui.RefreshNow();
            Check(ui.RuntimeRowCount == 32 && ui.GetComponentsInChildren<Transform>(true).Length == count, "Repeated history refresh neither instantiates duplicate rows nor rebuilds object hierarchy.");
            Set(game.Dashboard, "noticeAt", Time.unscaledTime - 7.5f); ui.SyncVisibility(); Check(ui.routine.visibility.alpha > .1f && ui.routine.visibility.alpha < .9f, "Routine fade retains original last-second presentation.");
            Set(game.Dashboard, "noticeAt", Time.unscaledTime - 8.1f); ui.SyncVisibility(); Check(ui.routine.visibility.alpha == 0 && !ui.routine.visibility.blocksRaycasts, "Eight-second expiry removes the notice and its pointer region.");
            game.NewSession(true); TriggerPumpWarning(); if (game.Dashboard.MessagesVisible) game.Dashboard.ToggleRoutineMessages(); Refresh();
            Check(game.Sim.PumpDamage > .05f && game.Sim.Mistakes > 0 && ui.critical.visibility.alpha == 1 && ui.critical.content.text.Contains("WASH PUMP WEAR") && ui.routine.visibility.alpha == 0,
                "Actual dry-pump fault drives persistent critical warning even with routine messages disabled.");
            if (variant == "edited") CheckSaved();
        }
        static void TriggerPumpWarning()
        {
            game.Sim.Controls.HorizontalAuger = true; game.Sim.Controls.WashPump = true; game.Sim.Controls.WashValve = 0;
            for (int i = 0; i < 12000 && game.Sim.PumpDamage <= .051f; i++) game.Sim.Step(.02f);
            game.Notice = game.Sim.LastMessage; Refresh();
        }
        static void TestProjection()
        {
            var guide = game.RouteGuide; if (!guide.Visible) guide.Toggle(); guide.PrepareTrainingPreview(); guide.Refresh();
            var camera = game.DriverCamera.transform; Vector3 position = camera.localPosition; Quaternion rotation = camera.localRotation;
            camera.LookAt(game.World.Machine.TransformPoint(new Vector3(0, .05f, .7f))); Refresh();
            int projected = ui.gpsProjection.SegmentCount;
            guide.Continuation.MeasureVisibility(game, Screen.width, Screen.height); guide.MachineAlignment.Measure(game, guide.Continuation, Screen.width, Screen.height);
            Check(projected > 0 && projected == guide.Continuation.LastVisibleSegments + guide.MachineAlignment.LastVisibleSegments,
                "Authored GPS graphic receives exactly the existing occluded route/alignment segments with a nonzero real silhouette fixture.");
            Check(guide.Continuation.LastSampledPoints <= GpsGuidanceGeometry.MaximumSilhouetteIntervals + 1 && guide.MachineAlignment.LastRayQueries <= GpsMachineAlignment.Intervals + 1,
                "Projected GPS retains the original bounded visibility-query budgets.");
            int dirties = 0; UnityEngine.Events.UnityAction dirty = () => dirties++;
            ui.gpsProjection.RegisterDirtyVerticesCallback(dirty);
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew(); for (int i = 0; i < 100; i++) ui.gpsProjection.ProjectNow(Screen.width, Screen.height); watch.Stop();
                Check(dirties == 0, "One hundred unchanged-camera GPS projections do not dirty the Canvas mesh.");
                Pass("MEASUREMENT authored GPS projection: " + (watch.Elapsed.TotalMilliseconds / 100).ToString("F5") + " ms/call, existing queries plus cached segment sink.");
                watch.Restart(); for (int i = 0; i < 100; i++) { guide.Continuation.MeasureVisibility(game, Screen.width, Screen.height); guide.MachineAlignment.Measure(game, guide.Continuation, Screen.width, Screen.height); } watch.Stop();
                Pass("MEASUREMENT legacy GPS visibility/alignment query reference: " + (watch.Elapsed.TotalMilliseconds / 100).ToString("F5") + " ms/call; excludes drawing in both measurements.");
            }
            finally { ui.gpsProjection.UnregisterDirtyVerticesCallback(dirty); }
            if (SessionState.GetString(SessionKey, "") != "projection-play") { camera.localPosition = position; camera.localRotation = rotation; Refresh(); }
        }
        static void PrepareRoute()
        {
            game.NewSession(true); game.RouteGuide.RecordingName = "AUTOMATED Phase5 disposable recording";
            Check(game.RouteGuide.StartRecording(), "Existing recorder starts a disposable integration fixture.");
            game.Sim.Controls.Throttle = -.4f; for (int i = 0; i < 140; i++) { game.Sim.Step(.02f); game.RouteGuide.ObserveSimulationStep(game.Sim); }
            game.Sim.Controls.Throttle = 0; game.RouteGuide.StopRecording(); game.RouteGuide.SaveRecording(); Check(File.Exists(game.RouteGuide.LastSavedPath), "Existing recording save writes only redirected validation storage."); game.NewSession(true);
        }
        static void ObserveService(string label)
        {
            bool enabled = game.enabled; game.enabled = true;
            try
            {
                Refresh();
                if (!game.Sim.OnFoot) return;
                Check(ui.ServiceVisible && ui.serviceGroup.alpha == 1 && Field(IceServiceField.Prompt).value.text == game.WaterService.Prompt && Field(IceServiceField.Notice).value.text == game.Notice,
                    "Real service state/target binds saved UI: " + label);
                if (game.Sim.HoseState == WaterHoseState.Flood) { sawFlood = true; Check(Field(IceServiceField.Hose).value.text == "FLOOD WATER CONNECTED", "Flood connection readout follows authoritative hose state."); }
                if (game.Sim.HoseState == WaterHoseState.Wash) { sawWash = true; Check(Field(IceServiceField.Hose).value.text == "WASH WATER CONNECTED", "Wash connection readout follows authoritative hose state."); }
                if (game.Sim.ServiceOverflowLitersPerSecond > .01f)
                {
                    sawOverflow = true; Check(Field(IceServiceField.Overflow).value.text.Contains("OVERFLOW") && Field(IceServiceField.Flow).value.text == game.Sim.ServiceFlowLitersPerSecond.ToString("0.0") + " L/s", "Actual finite-tank overflow and filling flow update independently editable fields.");
                    if (!serviceCaptured) { Capture("OnFootOverflow"); serviceCaptured = true; }
                }
                if (variant == "edited") CheckSaved();
            }
            finally { game.enabled = enabled; }
        }
        static void TestServicePointer()
        {
            game.NewSession(true); ServiceCall("SetMachinePose"); ServiceCall("Pulse", Key.F); ServiceCall("WalkToStation"); ServiceCall("Aim", game.WaterService.HolderPosition); Refresh();
            var panel = (RectTransform)ui.serviceGroup.transform; Vector3 saved = panel.position;
            Vector2 middle = new Vector2(Screen.width * .5f, Screen.height * .5f); Vector2 current = Center(panel); panel.position += new Vector3(middle.x - current.x, middle.y - current.y); Canvas.ForceUpdateCanvases();
            Pointer(middle); ServicePointer(middle, true);
            Check(game.Sim.HoseState == WaterHoseState.Stowed && game.Dashboard.PointerOverInterface(game, middle), "Relocated service panel over the actual aimed holder blocks click acquisition at its authored rectangle.");
            ServicePointer(middle); panel.position = saved + Vector3.right * Screen.width * 3; Canvas.ForceUpdateCanvases();
            ServicePointer(middle, true); Check(game.Sim.HoseState == WaterHoseState.Carried, "Moving service panel away restores actual click-to-take without stale legacy service rectangle."); ServicePointer(middle);
            Vector2 old = IceDashboard.CanvasToViewport(new Vector2(600, 820), Screen.width, Screen.height); old.y = Screen.height - old.y;
            Check(!game.Dashboard.PointerOverInterface(game, old), "Old y>700 service hit band no longer captures unrelated space.");
            ServiceCall("Pulse", Key.E); Check(game.Sim.HoseState == WaterHoseState.Stowed, "Keyboard E returns hose after the UI click test.");
            panel.position = saved; game.TogglePause(); Refresh(); Check(!ui.ServiceVisible && !ui.serviceGroup.blocksRaycasts, "Pause hides on-foot UI and releases its pointer region."); game.TogglePause(); Refresh();
            game.ToggleVideoMode(); Refresh(); ServiceCall("Pulse", Key.E); Check(game.Sim.HoseState == WaterHoseState.Carried && !ui.Visible, "E service interaction still works with video-mode UI hidden.");
            game.ToggleVideoMode(); Refresh(); game.NewSession(true);
        }
        static void ServicePointer(Vector2 at, bool down = false)
        { InputSystem.QueueStateEvent(mouse, new MouseState { position = at }.WithButton(MouseButton.Left, down)); InputSystem.Update(); game.WaterService.HandleInput(true, .02f); module.Process(); Refresh(); }
        // The existing world-validation assembly deliberately has no dependency on
        // the UI Editor assembly. Reflect its test-only runner without adding a cycle.
        static Type ServiceSuite => Type.GetType("IceResurfacer.Editor.IceServiceValidation, IceResurfacer.Editor", true);
        static object ServiceCall(string name, params object[] args) => ServiceSuite.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        static void CaptureCombinations()
        {
            game.NewSession(true); game.AuthoredConsole.SetCompact(true); Refresh(); Capture("DrivingCompact");
            if (!game.RouteGuide.Visible) game.RouteGuide.Toggle(); game.RouteGuide.PrepareTrainingPreview(); game.RouteGuide.Refresh(); Refresh(); Capture("SurveyGPS");
            game.AuthoredConsole.SetCompact(false); TriggerPumpWarning(); Refresh(); Capture("ExpandedWarning");
            if (!game.Dashboard.HistoryVisible) game.Dashboard.ToggleHistory(); Refresh(); Capture("History"); game.Dashboard.ToggleHistory();
            game.RouteGuide.ToggleRecordingPanel(); game.AuthoredRoutes.RefreshLibrary(); game.AuthoredRoutes.RefreshNow(); Refresh(); Capture("Routes"); game.RouteGuide.ToggleRecordingPanel();
            game.TogglePause(); Refresh(); Capture("Pause"); game.TogglePause(); game.Finish(); Refresh(); Capture("Results");
        }
        static void Capture(string state)
        {
            var canvas = ui.canvas; var camera = game.DriverCamera; var mode = canvas.renderMode; var priorCamera = canvas.worldCamera;
            float distance = canvas.planeDistance, aspect = camera.aspect; var target = camera.targetTexture; var active = RenderTexture.active;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .3f;
                foreach (var size in new[] { new Vector2Int(1366, 768), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) })
                {
                    var rt = new RenderTexture(size.x, size.y, 24); var png = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = rt; camera.aspect = size.x / (float)size.y; Refresh(); Canvas.ForceUpdateCanvases();
                        // The accelerated fixture captures several states inside one
                        // Editor update. Snap only transient ColorTint interpolation
                        // to its steady state; authored sprites/colors remain intact.
                        foreach (var selectable in canvas.GetComponentsInChildren<Selectable>(true))
                            if (selectable.targetGraphic != null && selectable.transition == Selectable.Transition.ColorTint)
                                selectable.targetGraphic.CrossFadeColor((selectable.IsInteractable() ? selectable.colors.normalColor : selectable.colors.disabledColor) * selectable.colors.colorMultiplier, 0, true, true);
                        camera.Render(); RenderTexture.active = rt;
                        png.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); png.Apply(); File.WriteAllBytes(Path.Combine(Output, "Phase5-" + variant + "-" + state + "-" + size.x + "x" + size.y + ".png"), png.EncodeToPNG());
                        Check(true, "Rendered integrated " + state + " at " + size + " / " + variant);
                    }
                    finally { camera.targetTexture = target; RenderTexture.active = active; Object.DestroyImmediate(png); rt.Release(); Object.DestroyImmediate(rt); }
                }
            }
            finally { canvas.renderMode = mode; canvas.worldCamera = priorCamera; canvas.planeDistance = distance; camera.targetTexture = target; camera.aspect = aspect; RenderTexture.active = active; Refresh(); }
        }
        static void Measure()
        {
            game.NewSession(true); Refresh();
            game.TogglePause(); Refresh();
            foreach (var presenter in new MonoBehaviour[] { game.AuthoredHud, game.AuthoredConsole, game.AuthoredSurvey, game.AuthoredGps })
            {
                Set(presenter, "nextRefresh", Time.unscaledTime - 1); Invoke(presenter, "LateUpdate");
                Check((float)presenter.GetType().GetField("nextRefresh", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter) < Time.unscaledTime,
                    "Hidden " + presenter.GetType().Name + " synchronizes visibility/releases input without scheduling text refresh.");
            }
            game.TogglePause(); Refresh();
            foreach (bool hidden in new[] { false, true }) foreach (bool compact in new[] { true, false })
            {
                if (game.HudHidden != hidden) game.ToggleVideoMode(); game.AuthoredConsole.SetCompact(compact); Refresh();
                int objects = ui.GetComponentsInChildren<Transform>(true).Length, refreshes = ui.ContentRefreshCount;
                var watch = System.Diagnostics.Stopwatch.StartNew(); for (int i = 0; i < 500; i++) ui.SyncVisibility(); watch.Stop();
                Check(objects == ui.GetComponentsInChildren<Transform>(true).Length && refreshes == ui.ContentRefreshCount, "Visibility polling does not instantiate objects or refresh inactive text.");
                Pass("MEASUREMENT Phase5 visibility " + (hidden ? "hidden" : "visible") + "/" + (compact ? "compact" : "expanded") + ": " + (watch.Elapsed.TotalMilliseconds / 500).ToString("F5") + " ms/call; excludes simulation/rendering.");
            }
            if (game.HudHidden) game.ToggleVideoMode();
            long start = GC.GetAllocatedBytesForCurrentThread(); var calibration = new byte[4096]; long allocated = GC.GetAllocatedBytesForCurrentThread() - start; GC.KeepAlive(calibration);
            Pass(allocated < 4096 ? "MEASUREMENT allocation counter UNAVAILABLE: retained 4096-byte calibration reported " + allocated + "; no zero-allocation claim." : "MEASUREMENT allocation counter calibrated; whole-frame allocation still requires Profiler.");
        }
        static void ProceduralTest()
        {
            Check(ui == null && !game.HasAuthoredOperatorUI && !game.HasAuthoredMenus && game.Sim != null, "Procedural scene has no authored UI dependency and retains legacy presenter path.");
            game.NewSession(true); game.Notice = "AUTOMATED procedural notice"; game.Dashboard.ObserveMessages(game); Check(game.Dashboard.MessageHistory.Count == 1, "Procedural legacy messages use the same authoritative history.");
            Keys(Key.F); Invoke(game, "Update"); Keys(); Check(game.Sim.OnFoot, "Procedural F dismount/service remains functional.");
            game.TogglePause(); game.ToggleHelp(); Check(game.Paused && game.HelpVisible, "Procedural pause/help state remains functional."); game.ToggleHelp(); game.TogglePause();
        }
        static void Refresh()
        { ui?.RefreshNow(); game.AuthoredHud?.RefreshNow(); game.AuthoredConsole?.RefreshNow(); game.AuthoredSurvey?.RefreshNow(); game.AuthoredSurvey?.SyncVisibility(); game.AuthoredGps?.RefreshNow(); game.AuthoredGps?.SyncVisibility(); game.AuthoredRoutes?.SyncVisibility(); game.AuthoredMenus?.RefreshState(); ui?.gpsProjection?.ProjectNow(game.DriverCamera.pixelWidth, game.DriverCamera.pixelHeight); Canvas.ForceUpdateCanvases(); }
        static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        static void Click(IceOperatorAction action) { Refresh(); Vector2 at = Center(action.GetComponent<RectTransform>()); Pointer(at); Pointer(at, true); Pointer(at); Refresh(); }
        static void Pointer(Vector2 at, bool down = false) { InputSystem.QueueStateEvent(mouse, new MouseState { position = at }.WithButton(MouseButton.Left, down)); InputSystem.Update(); module.Process(); }
        static void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        static void PrepareInput()
        {
            originalSettings = InputSystem.settings; inputSettings = Object.Instantiate(originalSettings); inputSettings.hideFlags = HideFlags.HideAndDontSave;
            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; InputSystem.settings = inputSettings;
            oldMouse = Mouse.current; oldKeyboard = Keyboard.current; mouse = InputSystem.AddDevice<Mouse>("Phase5 acceptance mouse"); keyboard = InputSystem.AddDevice<Keyboard>("Phase5 acceptance keyboard"); mouse.MakeCurrent(); keyboard.MakeCurrent();
            module = EventSystem.current?.GetComponent<InputSystemUIInputModule>();
        }
        static void RestoreInput()
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse); if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (oldMouse != null && oldMouse.added) oldMouse.MakeCurrent(); if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
            if (originalSettings != null) InputSystem.settings = originalSettings; if (inputSettings != null) Object.DestroyImmediate(inputSettings);
            mouse = oldMouse = null; keyboard = oldKeyboard = null; originalSettings = inputSettings = null;
        }
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static string Hash(string path) { using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
        static void ExitPlay(string stage) { RestoreInput(); SessionState.SetString(SessionKey, stage.Replace("-play", "-exit")); EditorApplication.ExitPlaymode(); }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Pass(message); }
        static void Pass(string message) { Directory.CreateDirectory(Output); File.AppendAllText(Results, "PASS " + message + "\n"); }
        static void Finish() { Pass("Phase 5 Unity acceptance complete; native cursor, full-frame profiling and manual visual sign-off remain separate."); SessionState.SetString(SessionKey, ""); EditorApplication.update -= Tick; EditorApplication.Exit(0); }
        static void Fail(Exception e)
        {
            RestoreInput(); SessionState.SetString(SessionKey, ""); EditorApplication.update -= Tick; Debug.LogException(e);
            if (File.Exists("UIAuthoringPhase5ValidationProject.txt")) { Directory.CreateDirectory(Output); File.AppendAllText(Results, "FAIL " + e + "\n"); }
            EditorApplication.Exit(1);
        }
    }
}
#endif
