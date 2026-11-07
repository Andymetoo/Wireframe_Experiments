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
    public static class IceNavigationValidation
    {
        const string SessionKey = "Aurora.Phase3.Acceptance";
        const string Procedural = "Assets/Experiments/IceResurfacer/Scenes/IceResurfacer.unity";
        static string Output => Path.GetFullPath("ValidationResults");
        static string Results => Path.Combine(Output, "Phase3-results.txt");
        static Mouse mouse, oldMouse;
        static Keyboard keyboard, oldKeyboard;
        static InputSettings settings, temporary;
        static InputSystemUIInputModule module;
        static IceResurfacerGame game;
        static IceSurfaceSurvey survey;
        static IceGpsStatus gps;
        static IceRouteManager manager;
        static int frames;
        static double started;
        static int commandStep;
        static float nextCommand;
        static string commandRaw, commandGuide, commandCopy, originalRawHash;
        static IceNavigationValidation()
        {
            if (SessionState.GetString(SessionKey, "") == "") return;
            started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick;
        }
        public static void RunBatch()
        {
            try
            {
                IceNavigationFactory.RequireIsolated(); Directory.CreateDirectory(Output);
                File.WriteAllText(Results, "Phase 3 isolated Unity acceptance. Injected input and camera rendering; native hardware/combined IMGUI remain manual.\n");
                SessionState.SetString(SessionKey + "ProceduralHash", Hash(Procedural));
                EditorSceneManager.OpenScene(IceNavigationFactory.ScenePath); Locate(); CheckBindings();
                File.Copy(IceNavigationFactory.ScenePath, Path.Combine(Output, "Before-authoring.unity"), true);
                EditAndSave(); File.Copy(IceNavigationFactory.ScenePath, Path.Combine(Output, "Authored-persistence.unity"), true);
                EditorSceneManager.OpenScene(IceNavigationFactory.ScenePath); Locate(); CheckSaved(); Begin("authored-play");
            }
            catch (Exception e) { Fail(e); }
        }
        public static void VerifyPersisted()
        {
            try { IceNavigationFactory.RequireIsolated(); EditorSceneManager.OpenScene(IceNavigationFactory.ScenePath); Locate(); CheckSaved(); Pass("Fresh Editor process retained all Phase 3 authoring changes."); EditorApplication.Exit(0); }
            catch (Exception e) { Fail(e); }
        }
        public static void VerifyIntegrated()
        {
            try
            {
                IceNavigationFactory.RequireIsolated(); Directory.CreateDirectory(Output);
                SessionState.SetString(SessionKey + "ExactHash", Hash(IceNavigationFactory.ScenePath));
                EditorSceneManager.OpenScene(IceNavigationFactory.ScenePath); Locate(); CheckBindings(); Begin("exact-play");
            }
            catch (Exception e) { Fail(e); }
        }
        public static void CommandsBatch()
        {
            try { IceNavigationFactory.RequireIsolated(); EditorSceneManager.OpenScene(IceNavigationFactory.ScenePath); Begin("commands-play"); }
            catch (Exception e) { Fail(e); }
        }
        static void Locate()
        {
            game = Object.FindFirstObjectByType<IceResurfacerGame>(); survey = Object.FindFirstObjectByType<IceSurfaceSurvey>();
            gps = Object.FindFirstObjectByType<IceGpsStatus>(); manager = Object.FindFirstObjectByType<IceRouteManager>();
        }
        static void CheckBindings()
        {
            Check(survey != null && gps != null && manager != null, "Three persistent Phase 3 modules exist in Edit Mode.");
            Check(survey.game == game && gps.game == game && manager.game == game, "Explicit module-to-game references are serialized.");
            Check(Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include).Length == 1 && Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length == 1,
                "Exactly one existing Canvas and EventSystem; no competing input architecture.");
            Check(survey.previewBadge.activeSelf && gps.previewBadge.activeSelf && manager.previewBadge.activeSelf, "Representative previews and badges are saved without running simulation.");
            Check(manager.previewRows.GetComponentsInChildren<IceRouteRow>().Length == 3 && manager.previewRows.GetComponentsInChildren<IceRouteRow>().All(r => r.previewOnly), "Three clearly identified harmless sample route rows.");
            Check(manager.actions.All(a => a != null && a.owner == manager && a.button != null), "All route actions explicitly bind the saved manager.");
            Check(manager.actions.Select(a => a.kind).Distinct().Count() == Enum.GetValues(typeof(IceRouteActionKind)).Length, "Every inventoried route action has an authored Button.");
            Check(survey.map.texture != null && manager.rowPrefab != null, "Preview map and reusable route-row prefab assigned.");
        }
        static void EditAndSave()
        {
            var map = survey.map.rectTransform; map.anchoredPosition = new Vector2(24, -15); map.sizeDelta = new Vector2(74, -45);
            var gpsRect = (RectTransform)gps.transform; gpsRect.anchoredPosition = new Vector2(460, -210);
            var window = (RectTransform)manager.transform; window.anchoredPosition = new Vector2(12, 8); window.sizeDelta = new Vector2(1260, 770);
            survey.coverage.fontSize = 27; survey.coverage.fontStyle = FontStyles.Bold; survey.coverage.GetComponent<IceUIStyle>().applyFontSize = false;
            var bg = survey.body.GetComponentInChildren<Image>(); bg.GetComponent<IceUIStyle>().applyColor = false; bg.color = new Color(.74f, .78f, .71f);
            foreach (var o in new Object[] { map, gpsRect, window, survey.coverage, survey.coverage.GetComponent<IceUIStyle>(), bg, bg.GetComponent<IceUIStyle>() }) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); Check(EditorSceneManager.SaveScene(SceneManager.GetActiveScene()), "Moved/resized map, GPS and manager plus local background/TMP changes saved.");
        }
        static void CheckSaved()
        {
            Check(Vector2.Distance(survey.map.rectTransform.anchoredPosition, new Vector2(24, -15)) < .001f && Vector2.Distance(survey.map.rectTransform.sizeDelta, new Vector2(74, -45)) < .001f, "Independently moved/resized map survives scene reopening.");
            Check(Vector2.Distance(((RectTransform)gps.transform).anchoredPosition, new Vector2(460, -210)) < .001f, "Authored GPS location survives reopening.");
            Check(Vector2.Distance(((RectTransform)manager.transform).sizeDelta, new Vector2(1260, 770)) < .001f, "Authored Route Manager width/height survives reopening.");
            Check(survey.coverage.fontSize == 27 && survey.coverage.fontStyle == FontStyles.Bold && !survey.coverage.GetComponent<IceUIStyle>().applyFontSize, "Local TMP appearance survives reopening.");
        }
        static void Begin(string stage)
        {
            SessionState.SetString(SessionKey, stage); frames = 0; started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 360) throw new TimeoutException("Phase 3 acceptance timed out.");
                string stage = SessionState.GetString(SessionKey, "");
                if (stage == "commands-play") { TickCommands(); return; }
                if (stage.EndsWith("-play"))
                {
                    if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 50) return;
                    PrepareInput(); Locate();
                    if (stage == "procedural-play") TestProcedural();
                    else { if (!game.HasFocus) Invoke(game, "OnApplicationFocus", true); game.NewSession(false); game.RouteGuide.ConfigureValidationStorage(Path.GetFullPath("Assets/Experiments/IceResurfacer/Validation~/UIAuthoringPhase3Routes"));
                        if (stage == "exact-play") TestExact(); else TestAuthored(); }
                    RestoreInput(); SessionState.SetString(SessionKey, stage.Replace("-play", "-exit")); EditorApplication.ExitPlaymode();
                }
                else if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if (stage == "authored-exit")
                    {
                        EditorSceneManager.OpenScene(IceNavigationFactory.ScenePath); Locate(); CheckSaved(); Check(survey.previewBadge.activeSelf && manager.previewRows.activeSelf, "Edit Mode preview restored after Play without copying live readings.");
                        EditorSceneManager.OpenScene(Procedural); Begin("procedural-play");
                    }
                    else if (stage == "procedural-exit")
                    { Check(Hash(Procedural) == SessionState.GetString(SessionKey + "ProceduralHash", ""), "Procedural scene bytes unchanged."); Finish(); }
                    else if (stage == "exact-exit")
                    { Check(Hash(IceNavigationFactory.ScenePath) == SessionState.GetString(SessionKey + "ExactHash", ""), "Exact delivered scene retained every serialized byte through Play."); Finish(); }
                    else if (stage == "commands-exit") Finish();
                }
            }
            catch (Exception e) { Fail(e); }
        }
        static void TickCommands()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 50 || Time.unscaledTime < nextCommand) return;
            if (commandStep == 0)
            {
                PrepareInput(); Locate(); if (!game.HasFocus) Invoke(game, "OnApplicationFocus", true); game.NewSession(false);
                string directory = Path.GetFullPath("Assets/Experiments/IceResurfacer/Validation~/UIPhase3Commands/" + DateTime.UtcNow.Ticks);
                game.RouteGuide.ConfigureValidationStorage(directory); Directory.CreateDirectory(game.RouteGuide.GuideDirectory);
                string fixture = "Assets/Experiments/IceResurfacer/Validation~/UIPhase3Fixtures/UnchangedGuideCopy.guide.json";
                string incompatible = Path.Combine(game.RouteGuide.GuideDirectory, "AUTOMATED-unchanged-guide.guide.json"); File.Copy(fixture, incompatible); string beforeGuide = Hash(incompatible);
                game.RouteGuide.ToggleRecordingPanel(); manager.SyncVisibility(); manager.RefreshLibrary(); manager.SelectPath(incompatible); manager.Activate(IceRouteActionKind.UseGps);
                Check(game.RouteGuide.ActiveSourcePath != incompatible && game.RouteGuide.TrainingStatus.Contains("GuideRollerX") && Hash(incompatible) == beforeGuide,
                    "Existing guide with different GuideRollerX remains rejected and byte-identical; no automatic revalidation.");
                game.RouteGuide.ToggleRecordingPanel(); game.Sim.Vehicle.X = 0; game.Sim.Vehicle.Z = -10; game.Sim.Vehicle.Heading = 0;
                for (int i = 0; i < 5; i++) game.Sim.Step(.02f);
                game.RouteGuide.RecordingName = "AUTOMATED current-calibration straight fixture"; Check(game.RouteGuide.StartRecording(), "Disposable current-geometry fixture starts through existing recorder.");
                game.Sim.Controls.Throttle = .4f;
                for (int i = 0; i < 250; i++) { game.Sim.Step(.02f); game.RouteGuide.ObserveSimulationStep(game.Sim); }
                game.Sim.Controls.Throttle = 0; game.RouteGuide.StopRecording(); game.RouteGuide.SaveRecording(); var raw = game.RouteGuide.Recorder.Data;
                var definition = TrainingGuideStorage.Create(raw);
                commandGuide = TrainingGuideStorage.SaveCopy(game.RouteGuide.GuideDirectory, definition);
                commandRaw = Path.Combine(directory, "AUTOMATED-command.route.json"); File.WriteAllText(commandRaw, RouteRecordingStorage.Encode(raw)); originalRawHash = Hash(commandRaw);
                var partial = RouteRecordingStorage.Decode(RouteRecordingStorage.Encode(raw)); partial.complete = false; game.RouteGuide.Library.SaveDraft(partial);
                File.WriteAllText(Path.Combine(directory, "AUTOMATED-invalid.route.json"), "invalid");
                game.RouteGuide.ToggleRecordingPanel(); manager.SyncVisibility(); manager.RefreshLibrary(); manager.SelectPath(commandGuide); manager.RefreshNow(); manager.Activate(IceRouteActionKind.Refresh);
                manager.Activate(IceRouteActionKind.UseGps);
                Check(game.RouteGuide.ActiveSourcePath == commandGuide && game.RouteGuide.SelectedGuide != null, "Authored Use as GPS activates separate current-geometry test guide with strict compatibility: " + game.RouteGuide.TrainingStatus);
                game.RouteGuide.PrepareTrainingPreview(); gps.RefreshNow();
                bool requested = false; var previous = IceRouteGuide.EditRouteRequested;
                try { IceRouteGuide.EditRouteRequested = p => requested = p == commandGuide; manager.Activate(IceRouteActionKind.Edit); }
                finally { IceRouteGuide.EditRouteRequested = previous; }
                Check(requested, "Edit route invokes existing Editor callback for the explicit selected guide.");
                manager.SelectPath(commandRaw); manager.Activate(IceRouteActionKind.Rename); manager.renameInput.text = "AUTOMATED renamed library alias";
                manager.Activate(IceRouteActionKind.Confirm);
                Check(Hash(commandRaw) == originalRawHash && manager.SelectedPath == commandRaw && RouteLibraryCatalog.DisplayName(commandRaw, "") == "AUTOMATED renamed library alias", "Rename preserves recorded bytes and stable selection; only library alias changes.");
            }
            else if (commandStep == 1)
            {
                manager.SelectPath(commandRaw); int before = game.RouteGuide.Library.ListFiles().Length; manager.Activate(IceRouteActionKind.SaveCopy); commandCopy = manager.SelectedPath;
                manager.Activate(IceRouteActionKind.SaveCopy);
                Check(commandCopy != commandRaw && File.Exists(commandCopy) && game.RouteGuide.Library.ListFiles().Length == before + 1 && Hash(commandRaw) == originalRawHash, "Save new copy creates exactly one separate file and leaves selected source unchanged.");
            }
            else if (commandStep == 2)
            {
                manager.SelectPath(commandCopy); manager.Activate(IceRouteActionKind.Delete); Check(File.Exists(commandCopy), "Delete requires explicit per-file confirmation.");
                manager.Activate(IceRouteActionKind.Confirm); Check(!File.Exists(commandCopy) && File.Exists(commandRaw) && File.Exists(commandGuide), "Confirmed deletion removes only the captured disposable copy.");
                manager.Activate(IceRouteActionKind.Refresh);
                string draft = game.RouteGuide.Library.ListFiles().Single(p => p.Contains("Drafts")); manager.SelectPath(draft);
                Check(!manager.CanActivate(IceRouteActionKind.UseGps) && manager.CanActivate(IceRouteActionKind.PreviewLoad), "Incomplete recovery remains inspectable but cannot activate as GPS.");
                manager.SelectPath(Path.Combine(game.RouteGuide.Library.DirectoryPath, "AUTOMATED-invalid.route.json"));
                Check(!manager.CanActivate(IceRouteActionKind.UseGps) && !manager.CanActivate(IceRouteActionKind.SaveCopy), "Malformed saved file displays an error and cannot activate or save a copy.");
            }
            else if (commandStep == 3)
            {
                manager.SelectPath(commandRaw); Refresh(); TestWheel(manager.listContent.GetComponentInParent<ScrollRect>(), commandRaw); var button = Action(IceRouteActionKind.SaveCopy).button;
                var rect = button.GetComponent<RectTransform>(); var at = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)); int before = game.RouteGuide.Library.ListFiles().Length;
                Pointer(at); Pointer(at, true); game.RouteGuide.ToggleRecordingPanel(); manager.SyncVisibility(); game.RouteGuide.ToggleRecordingPanel(); manager.SyncVisibility(); Pointer(at);
                Check(game.RouteGuide.Library.ListFiles().Length == before, "A canceled save press cannot fire after manager hide/reopen.");
                game.RouteGuide.ToggleRecordingPanel(); manager.SyncVisibility(); Refresh(); TestPhysicalOwnership();
            }
            else
            {
                RestoreInput(); SessionState.SetString(SessionKey, "commands-exit"); EditorApplication.ExitPlaymode(); return;
            }
            commandStep++; nextCommand = Time.unscaledTime + .55f;
        }
        static void TestPhysicalOwnership()
        {
            var gpsRect = (RectTransform)gps.transform; Vector3 gpsPosition = gpsRect.position; gpsRect.position += Vector3.right * Screen.width * 2; Canvas.ForceUpdateCanvases();
            var camera = game.DriverCamera.transform; game.SelectCameraView("Controls"); game.TickControlsAnchor(1);
            var grip = IcePhysicalControls.GripPositions(game.World, game.Sim.Config)[1]; camera.LookAt(grip, Vector3.up);
            var at = (Vector2)game.DriverCamera.WorldToScreenPoint(grip);
            Check(game.PhysicalControls.GetHitControl(at) == "Conditioner hydraulic lever", "Physical cockpit lever remains pickable with Phase 3 installed.");
            var root = (RectTransform)survey.transform; Vector3 previous = root.position;
            var current = RectTransformUtility.WorldToScreenPoint(null, survey.map.rectTransform.TransformPoint(survey.map.rectTransform.rect.center));
            var delta = at - current; root.position += new Vector3(delta.x, delta.y); Canvas.ForceUpdateCanvases();
            Check(game.Dashboard.PointerOverInterface(game, at), "Relocated survey blocks cockpit picking at its actual authored location.");
            root.position = previous; Canvas.ForceUpdateCanvases();
            Check(!game.Dashboard.PointerOverInterface(game, at), "Moving survey away restores cockpit pointer access without an obsolete survey rectangle.");
            game.ReleaseControls(); Pointer(at); InputSystem.QueueStateEvent(mouse, new MouseState { position = at }.WithButton(MouseButton.Right, true)); InputSystem.Update();
            Invoke(game, "UpdateLook", true, .02f); Refresh(); Check(!survey.CanInteract, "RMB free look releases survey interaction ownership.");
            Invoke(game, "ReleaseLook"); Pointer(at); Refresh();
            gpsRect.position = gpsPosition; Canvas.ForceUpdateCanvases();
        }
        static void TestExact()
        {
            Refresh(); Check(game.HasAuthoredHud && game.HasAuthoredConsole && game.HasAuthoredSurvey && game.HasAuthoredGps && game.HasAuthoredRoutes, "Exact delivered scene registers all five authored modules in real Play.");
            TestMap(); TestGps(); Capture("Delivered");
        }
        static void TestAuthored()
        {
            Refresh(); CheckSaved();
            Check(game.HasAuthoredHud && game.HasAuthoredConsole && game.HasAuthoredSurvey && game.HasAuthoredGps && game.HasAuthoredRoutes, "All five modules coexist and register in Play.");
            Check(!survey.previewBadge.activeSelf && !gps.previewBadge.activeSelf && !manager.previewRows.activeSelf, "Play removes preview-only values and rows.");
            Check(survey.map.texture == game.Surface.OverviewTexture, "Survey RawImage binds the authoritative live overview texture.");
            TestMap(); TestGps(); TestRoutes(); TestCoexistence(); Capture("Edited"); Measure();
        }
        static void Refresh()
        {
            survey?.RefreshNow(); gps?.RefreshNow(); survey?.SyncVisibility(); gps?.SyncVisibility(); manager?.SyncVisibility(); manager?.RefreshNow(); game.AuthoredHud?.RefreshNow(); game.AuthoredConsole?.RefreshNow(); Canvas.ForceUpdateCanvases();
        }
        static void TestMap()
        {
            Refresh(); var map = survey.map; var field = game.Sim.Ice;
            foreach (var p in new[] { new Vector2(.025f, .025f), new Vector2(-5.025f, 10.025f), new Vector2(5.025f, -10.025f) })
            {
                var point = RectTransformUtility.WorldToScreenPoint(null, map.rectTransform.TransformPoint(map.WorldToLocal(p.x, p.y)));
                Check(map.TryCell(point, null, out int cell) && cell == field.IndexAt(p.x, p.y), "Moved/resized map projects correct physical cell at " + p);
                Pointer(point); Pointer(point, true); Pointer(point);
                Check(survey.InspectedCell == cell && survey.patchDetail.text.Contains("cell " + cell), "Actual UI pointer click pins the corresponding live patch detail."); survey.ClosePatch();
            }
            var originalSize = map.rectTransform.sizeDelta; map.rectTransform.sizeDelta = new Vector2(260, 0); Canvas.ForceUpdateCanvases();
            var letterbox = RectTransformUtility.WorldToScreenPoint(null, map.rectTransform.TransformPoint(new Vector2(map.rectTransform.rect.xMin + 1, map.rectTransform.rect.center.y)));
            Check(!map.TryWorld(letterbox, null, out _), "Aspect-preserved map excludes letterbox bars from inspection."); map.rectTransform.sizeDelta = originalSize; Canvas.ForceUpdateCanvases();
            Click(survey.collapseButton); Check(survey.Collapsed && survey.body.alpha == 0 && !survey.body.blocksRaycasts, "Collapse hides map graphics and their actual hit regions.");
            Click(survey.collapseButton); Check(!survey.Collapsed && survey.body.alpha == 1, "Expand restores the authored body.");
            var texture = game.Surface.OverviewTexture; int uploads = game.Surface.LastTextureUploads; survey.RefreshNow();
            Check(ReferenceEquals(texture, survey.map.texture) && game.Surface.LastTextureUploads == uploads, "UI refresh never uploads or reconstructs the ice textures.");
            for (int i = 0; i < 3; i++)
            { KeyPress(Key.F2); survey.RefreshNow(); Check(survey.map.texture == (game.Surface.DebugMode == 0 ? game.Surface.OverviewTexture : game.Surface.MapTexture), "Existing F2 mode " + game.Surface.DebugMode + " uses its authoritative texture."); }
            // Surface changes use the unchanged simulation, then its existing refresh path.
            game.Sim.Vehicle.X = 0; game.Sim.Vehicle.Z = 0; game.Sim.Vehicle.Heading = 0; game.Sim.Conditioner.Lift = 0; game.Sim.Controls.ConditionerTarget = 0;
            game.Sim.Controls.HorizontalAuger = game.Sim.Controls.VerticalAuger = true; game.Sim.Controls.FloodFlow = .3f; game.Sim.Controls.Throttle = .4f;
            for (int i = 0; i < 40; i++) game.Sim.Step(.02f);
            game.Surface.Refresh(game.Sim); survey.RefreshNow();
            Check(game.Surface.LastCellsUpdated > 0 && survey.map.texture == game.Surface.OverviewTexture, "Live simulation changes refresh the same overview texture."); game.ReleaseControls();
        }
        static void TestGps()
        {
            if (!game.RouteGuide.Visible) game.RouteGuide.Toggle(); game.RouteGuide.PrepareTrainingPreview(); game.RouteGuide.Refresh(); gps.RefreshNow();
            Check(gps.routeName.text == game.RouteGuide.ActiveRouteName && gps.leg.text.Contains("LEG"), "GPS labels bind live selected route and leg.");
            Check(IceRouteGuide.LookAheadMeters == 24.384f && game.RouteGuide.DisplayPointCount >= 2, "Existing 80 ft world guidance remains active.");
            Click(gps.toggleButton); Check(!game.RouteGuide.Visible && gps.leg.text == "GPS HIDDEN", "Authored show/hide action updates existing GPS visibility.");
            KeyPress(Key.Numpad7); Check(game.RouteGuide.Visible, "Numpad 7 still toggles world GPS.");
            KeyPress(Key.F5); Check(!game.RouteGuide.Visible, "F5 still toggles world GPS."); KeyPress(Key.F5);
            var fixture = Resources.Load<TextAsset>("TrainingTestGuide"); var definition = TrainingGuideStorage.Decode(fixture.text);
            definition.configuration.RinkWidth += 1;
            Check(!game.RouteGuide.SelectTrainingGuide(definition), "Strict incompatible-guide rejection is unchanged.");
        }
        static void TestRoutes()
        {
            var guide = game.RouteGuide; guide.RecordingName = "AUTOMATED Phase3 disposable route";
            if (!guide.PanelOpen) guide.ToggleRecordingPanel(); manager.SyncVisibility(); manager.RefreshNow();
            Click(Action(IceRouteActionKind.Record).button); Check(guide.Recorder.Recording && !guide.PanelOpen, "Authored Record starts recording and closes manager to drive.");
            game.Sim.Controls.Throttle = .4f;
            for (int i = 0; i < 140; i++) { game.Sim.Step(.02f); guide.ObserveSimulationStep(game.Sim); }
            game.Sim.Controls.Throttle = 0; guide.ToggleRecordingPanel(); manager.SyncVisibility();
            Click(Action(IceRouteActionKind.StopSave).button); Check(!guide.Recorder.Recording && File.Exists(guide.LastSavedPath), "Authored Stop & save publishes a complete new route in disposable storage.");
            int before = Directory.GetFiles(guide.Library.DirectoryPath, "*.route.json").Length;
            manager.Activate(IceRouteActionKind.StopSave); manager.Activate(IceRouteActionKind.SaveCopy);
            Check(Directory.GetFiles(guide.Library.DirectoryPath, "*.route.json").Length == before, "Repeated same-frame/repeated save callbacks cannot publish duplicate files.");
            string path = guide.LastSavedPath;
            // Meaningful list stress; identical payloads intentionally have independent stable file paths.
            byte[] data = File.ReadAllBytes(path);
            for (int i = 0; i < 150; i++) File.WriteAllBytes(Path.Combine(guide.Library.DirectoryPath, "AUTOMATED-list-" + i.ToString("D3") + ".route.json"), data);
            File.WriteAllText(Path.Combine(guide.Library.DirectoryPath, "AUTOMATED-invalid.route.json"), "invalid test file");
            manager.RefreshLibrary(); manager.SelectPath(path); manager.RefreshNow(); Canvas.ForceUpdateCanvases();
            Check(manager.CatalogCount >= 152 && manager.RuntimeRowCount >= 152, "Scrollable list binds 150+ disposable saved files and invalid-file status.");
            int pool = manager.RuntimeRowCount; for (int i = 0; i < 20; i++) manager.RefreshNow();
            Check(manager.RuntimeRowCount == pool, "Ordinary refresh never recreates route rows.");
            var selected = manager.GetComponentsInChildren<IceRouteRow>().Single(r => r.BoundPath == path);
            Check(selected.title.text.Contains("AUTOMATED") && selected.selectedIndicator.activeSelf && !selected.previewOnly, "Live row explicitly binds the correct path/name/selected state.");
            var scroll = manager.listContent.GetComponentInParent<ScrollRect>(); scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
            Check(scroll.content.rect.height > scroll.viewport.rect.height && manager.SelectedPath == path, "List scrolls with authored layout spacing without changing file selection.");
            TestWheel(scroll, path);
            var old = manager.SelectedPath; manager.RefreshLibrary(); Check(manager.SelectedPath == old, "File refresh/reordering preserves explicit selected path.");
            Click(Action(IceRouteActionKind.PreviewLoad).button, false); Check(guide.Recorder.Data != null && guide.LastSavedPath == path, "Authored load invokes existing raw-recording workflow.");
            Check(manager.vehicleSampleMarker != null && manager.conditionerSampleMarker != null, "Raw preview annotations are saved editable UI objects.");
            manager.sampleSlider.value = manager.sampleSlider.maxValue; manager.RefreshNow();
            Check(manager.vehicleSampleMarker.gameObject.activeSelf && manager.sampleAnnotation.text == "#" + guide.Recorder.Data.samples[guide.Recorder.Data.samples.Count - 1].sequence,
                "Raw sample scrubber drives the matching vehicle/conditioner markers and sample-number annotation.");
            manager.recordingName.ActivateInputField(); EventSystem.current.SetSelectedGameObject(manager.recordingName.gameObject); manager.recordingName.ForceLabelUpdate();
            manager.recordingName.text = "Keyboard focus route";
            Check(manager.TextEditing && guide.RecordingName == "Keyboard focus route", "TMP editing owns keyboard focus and binds the next recording name.");
            KeyPress(Key.Tab); Check(manager.Visible, "Typing focus does not route Tab into the machinery console shortcut.");
            IceNavigationUI.ClearFocus(manager.transform);
            manager.Activate(IceRouteActionKind.GuidesTab); Check(guide.TrainingPanelOpen, "Authored Guides tab preserves existing panel state.");
            manager.Activate(IceRouteActionKind.RawTab); Check(guide.AdvancedPanel && !guide.TrainingPanelOpen, "Authored Raw/recovery tab preserves existing state.");
            manager.Activate(IceRouteActionKind.LibraryTab); Check(!guide.AdvancedPanel, "Authored Library tab restores ordinary route manager.");
            manager.RefreshNow(); guide.ToggleRecordingPanel(); manager.SyncVisibility();
            Check(!manager.Visible && !manager.TextEditing && Cursor.lockState == CursorLockMode.None, "Closing manager clears keyboard ownership and releases cursor capture.");
        }
        static void TestCoexistence()
        {
            Refresh(); Check(game.AuthoredHud.widgets.Length == 8 && game.AuthoredHud.Visible, "Phase 1 eight-widget HUD still binds and shows live values.");
            game.AuthoredConsole.SetCompact(false); game.AuthoredConsole.RefreshNow();
            var control = game.AuthoredConsole.controls.Single(c => c.kind == IceConsoleControlKind.VerticalAuger);
            bool before = game.Sim.Controls.VerticalAuger;
            var rect = control.interactionArea; var at = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            Pointer(at); Pointer(at, true); Pointer(at); Check(game.Sim.Controls.VerticalAuger != before, "Phase 2 machinery command still works through real uGUI.");
            game.AuthoredConsole.SetCompact(true); game.TogglePause(); Refresh(); Check(!survey.Visible && !gps.Visible, "Pause hides Phase 3 driving widgets.");
            game.TogglePause(); game.ToggleHelp(); Refresh(); Check(!survey.Visible && !gps.Visible, "Help hides Phase 3 driving widgets.");
            game.ToggleHelp(); game.ToggleVideoMode(); Refresh(); Check(!survey.Visible && !gps.Visible && !manager.Visible, "Numpad 1 hide-all policy includes all three modules.");
            game.ToggleVideoMode(); Refresh(); Check(survey.Visible && gps.Visible, "Returning from hide-all restores authored widgets.");
        }
        static void TestProcedural()
        {
            game.NewSession(false); Check(game.Sim != null && !game.HasAuthoredSurvey && !game.HasAuthoredGps && !game.HasAuthoredRoutes && !game.HasAuthoredHud && !game.HasAuthoredConsole, "Procedural launch remains free of authored UI and initializes existing simulation.");
            Keys(Key.Digit3); Invoke(game, "Update"); Check(game.Sim.Controls.VerticalAuger, "Procedural keyboard machinery path remains functional."); Keys();
        }
        static void Capture(string variant)
        {
            if (game.RouteGuide.PanelOpen) game.RouteGuide.ToggleRecordingPanel(); Refresh();
            var canvas = survey.canvas; var camera = game.DriverCamera;
            var mode = canvas.renderMode; var priorCamera = canvas.worldCamera; float distance = canvas.planeDistance, aspect = camera.aspect;
            var priorTarget = camera.targetTexture; var priorActive = RenderTexture.active;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .3f;
                foreach (var size in new[] { new Vector2Int(1366, 768), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) })
                foreach (bool routes in new[] { false, true })
                {
                    if (routes != game.RouteGuide.PanelOpen) game.RouteGuide.ToggleRecordingPanel(); Refresh(); if (routes) { manager.RefreshNow(); Canvas.ForceUpdateCanvases(); }
                    var rt = new RenderTexture(size.x, size.y, 24); var png = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = rt; camera.aspect = size.x / (float)size.y; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                        png.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); png.Apply();
                        File.WriteAllBytes(Path.Combine(Output, "Phase3-" + variant + "-" + (routes ? "Routes" : "SurveyGPS") + "-" + size.x + "x" + size.y + ".png"), png.EncodeToPNG());
                        Check(routes ? manager.visibility.alpha == 1 : survey.visibility.alpha == 1 && gps.visibility.alpha == 1, "Rendered " + (routes ? "Route Manager" : "Survey/GPS") + " at " + size);
                    }
                    finally { camera.targetTexture = priorTarget; RenderTexture.active = priorActive; Object.Destroy(png); Object.Destroy(rt); }
                }
            }
            finally { canvas.renderMode = mode; canvas.worldCamera = priorCamera; canvas.planeDistance = distance; camera.targetTexture = priorTarget; camera.aspect = aspect; RenderTexture.active = priorActive; if (game.RouteGuide.PanelOpen) game.RouteGuide.ToggleRecordingPanel(); Refresh(); }
        }
        static void Measure()
        {
            long calibrationBefore = GC.GetAllocatedBytesForCurrentThread(); var calibration = new byte[4096]; long calibrationBytes = GC.GetAllocatedBytesForCurrentThread() - calibrationBefore; GC.KeepAlive(calibration);
            Refresh(); var watch = System.Diagnostics.Stopwatch.StartNew(); for (int i = 0; i < 200; i++) { survey.RefreshNow(); gps.RefreshNow(); } watch.Stop();
            Pass("MEASUREMENT survey+GPS binding mean: " + (watch.Elapsed.TotalMilliseconds / 200).ToString("F4") + " ms / refresh (200 calls; excludes rendering).");
            if (!game.RouteGuide.PanelOpen) game.RouteGuide.ToggleRecordingPanel(); manager.SyncVisibility(); manager.RefreshNow();
            watch.Restart(); for (int i = 0; i < 200; i++) manager.RefreshNow(); watch.Stop();
            Pass("MEASUREMENT manager binding with " + manager.CatalogCount + " files: " + (watch.Elapsed.TotalMilliseconds / 200).ToString("F4") + " ms / refresh (200 calls; excludes rendering and file refresh).");
            Pass("MEASUREMENT graphics: survey " + survey.GetComponentsInChildren<Graphic>(true).Length + ", GPS " + gps.GetComponentsInChildren<Graphic>(true).Length + ", manager incl pooled rows " + manager.GetComponentsInChildren<Graphic>(true).Length + ". Full-frame allocations/draw/Canvas comparison remains manual.");
            if (calibrationBytes < 4096) Pass("MEASUREMENT allocation counter UNAVAILABLE: retained 4096-byte calibration reported " + calibrationBytes + " bytes; no zero-allocation claim.");
            else
            {
                long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 200; i++) manager.RefreshNow();
                Pass("MEASUREMENT manager binding allocation " + ((GC.GetAllocatedBytesForCurrentThread() - before) / 200) + " bytes/refresh; string formatting at scheduled refresh, not full-frame allocation.");
            }
            game.RouteGuide.ToggleRecordingPanel(); manager.SyncVisibility();
        }
        static IceRouteAction Action(IceRouteActionKind kind) => manager.actions.Single(a => a.kind == kind);
        static void TestWheel(ScrollRect scroll, string selected)
        {
            scroll.verticalNormalizedPosition = 1; Canvas.ForceUpdateCanvases();
            Vector2 scrollAt = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.TransformPoint(scroll.viewport.rect.center)); Pointer(scrollAt);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = scrollAt, scroll = new Vector2(0, -120) }); InputSystem.Update(); module.Process();
            Check(scroll.verticalNormalizedPosition < .999f && manager.SelectedPath == selected, "Real Input System wheel events scroll the file viewport without changing route identity.");
        }
        static void Pointer(Vector2 at, bool down = false)
        { InputSystem.QueueStateEvent(mouse, new MouseState { position = at }.WithButton(MouseButton.Left, down)); InputSystem.Update(); module.Process(); }
        static void Click(Button button, bool requireVisible = true)
        {
            if (requireVisible) { Refresh(); Canvas.ForceUpdateCanvases(); }
            var rect = button.GetComponent<RectTransform>(); var at = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            if (!requireVisible) { button.GetComponent<IceUIClickGuard>()?.OnPointerDown(new PointerEventData(EventSystem.current)); button.onClick.Invoke(); return; }
            Pointer(at); Pointer(at, true); Pointer(at);
        }
        static void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        static void KeyPress(Key key) { Keys(key); Invoke(game, "Update"); Invoke(game.RouteGuide, "Update"); Keys(); Invoke(game, "Update"); Invoke(game.RouteGuide, "Update"); Refresh(); }
        static void PrepareInput()
        {
            settings = InputSystem.settings; temporary = Object.Instantiate(settings); temporary.hideFlags = HideFlags.HideAndDontSave;
            temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; temporary.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; InputSystem.settings = temporary;
            oldMouse = Mouse.current; oldKeyboard = Keyboard.current; mouse = InputSystem.AddDevice<Mouse>("Phase3 acceptance mouse"); mouse.MakeCurrent(); keyboard = InputSystem.AddDevice<Keyboard>("Phase3 acceptance keyboard"); keyboard.MakeCurrent();
            module = EventSystem.current?.GetComponent<InputSystemUIInputModule>();
        }
        static void RestoreInput()
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse); if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (oldMouse != null && oldMouse.added) oldMouse.MakeCurrent(); if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
            if (settings != null) InputSystem.settings = settings; if (temporary != null) Object.Destroy(temporary);
            mouse = oldMouse = null; keyboard = oldKeyboard = null; temporary = settings = null;
        }
        static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        static string Hash(string path) { using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Pass(message); }
        static void Pass(string message) { Directory.CreateDirectory(Output); File.AppendAllText(Results, "PASS " + message + "\n"); }
        static void Finish() { Pass("Phase 3 automated run complete; manual native-input, visual and full-frame profiling checks remain separate."); SessionState.SetString(SessionKey, ""); EditorApplication.update -= Tick; EditorApplication.Exit(0); }
        static void Fail(Exception e)
        {
            RestoreInput(); SessionState.SetString(SessionKey, ""); EditorApplication.update -= Tick; Debug.LogException(e);
            if (File.Exists("UIAuthoringPhase3ValidationProject.txt")) { Directory.CreateDirectory(Output); File.AppendAllText(Results, "FAIL " + e + "\n"); }
            EditorApplication.Exit(1);
        }
    }
}
#endif
