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

namespace IceResurfacer.UI.Editor
{
    /// <summary>Authoring and interaction checks restricted to the marked disposable Phase 2 project.</summary>
    [InitializeOnLoad]
    public static class IceMachineryConsoleValidation
    {
        const string Authored = "Assets/Experiments/IceResurfacer/Scenes/IceResurfacer_AuthoredMachine.unity";
        const string Procedural = "Assets/Experiments/IceResurfacer/Scenes/IceResurfacer.unity";
        const string SessionKey = "Aurora.Machinery.ValidationStage";
        const string Marker = "UIAuthoringPhase2ValidationProject.txt";
        static readonly Vector2 LeverPosition = new Vector2(-400, -68);
        static readonly Vector2 ValveSize = new Vector2(104, 88);
        static readonly Vector2 TogglePosition = new Vector2(-220, -428);
        static Mouse mouse, previousMouse;
        static Keyboard keyboard, previousKeyboard;
        static InputSettings inputSettings, temporarySettings;
        static InputSystemUIInputModule module;
        static IceMachineryConsole console;
        static IceResurfacerGame game;
        static int frames;
        static double started;
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string Output => Path.Combine(Root, "ValidationResults");
        static string Results => Path.Combine(Output, "Machinery-results.txt");

        static IceMachineryConsoleValidation()
        {
            if (SessionState.GetString(SessionKey, "") == "") return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        public static void RunBatch()
        {
            try
            {
                Guard(); Directory.CreateDirectory(Output);
                File.WriteAllText(Results, "Aurora Phase 2 isolated Unity authoring acceptance\nInput checks use injected Input System events and real runtime handlers; native desktop hardware and visible Editor inspection remain separate.\n");
                SessionState.SetString(SessionKey + ".proceduralHash", Hash(Procedural));
                IceResurfacer.Editor.IceMachineryConsoleFactory.BuildNewAssets();
                IceResurfacer.Editor.IceMachineryConsoleFactory.CompletePhase2AuthoringAssets();
                EditorSceneManager.OpenScene(Authored);
                console = IceResurfacer.Editor.IceMachineryConsoleFactory.InstallInScene(SceneManager.GetActiveScene());
                Require(console.ValidateBindings(out string error), "Persistent console bindings complete: " + (error ?? "all references valid"));
                Require(console.controls.Length == 18, "All 18 compact, machinery, status and confirmation controls are persistent objects.");
                Require(FindAll<EventSystem>().Length == 1, "Exactly one existing EventSystem remains in the scene.");
                Require(console.GetComponentsInChildren<LayoutGroup>(true).Length == 0, "Console contains no automatic layout groups that could undo manual positions.");
                Require(console.previewBadge.activeSelf, "Edit Mode identifies preview values without running the simulation.");
                Require(EditorSceneManager.SaveScene(SceneManager.GetActiveScene()), "Additive console installation saved in disposable project.");
                File.Copy(Authored, Path.Combine(Output, "InstalledBeforeAuthoringEdits.unity"), true);
                ApplyAuthoringChanges();
                Require(EditorSceneManager.SaveScene(SceneManager.GetActiveScene()), "Moved lever, resized valve, local TMP style and moved toggle saved as prefab overrides.");
                EditorSceneManager.OpenScene(Authored); CheckPersistence();
                BeginPlay("authored-play");
            }
            catch (Exception exception) { Fail(exception); }
        }

        public static void VerifyPersisted()
        {
            try { Guard(); EditorSceneManager.OpenScene(Authored); CheckPersistence(); Pass("Fresh Unity process retained every authoring edit."); EditorApplication.Exit(0); }
            catch (Exception exception) { Fail(exception); }
        }

        public static void VerifyIntegrated()
        {
            try
            {
                Guard();
                SessionState.SetString(SessionKey + ".integratedHash", Hash(Authored));
                EditorSceneManager.OpenScene(Authored);
                console = Find<IceMachineryConsole>();
                Require(console != null && console.ValidateBindings(out _), "Exact integrated scene has one correctly bound persistent machinery console.");
                Require(FindAll<EventSystem>().Length == 1 && FindAll<IceAuthoredHud>().Length == 1, "Exact integrated scene preserves one EventSystem and Phase 1 header.");
                BeginPlay("integrated-play");
            }
            catch (Exception exception) { Fail(exception); }
        }

        static void ApplyAuthoringChanges()
        {
            console = Find<IceMachineryConsole>();
            var lever = Control(IceConsoleControlKind.Conditioner).interactionArea;
            var valve = Control(IceConsoleControlKind.FloodValve).interactionArea;
            var toggle = (RectTransform)Control(IceConsoleControlKind.BoardBrush).transform;
            lever.anchoredPosition = LeverPosition;
            valve.sizeDelta = ValveSize;
            toggle.anchoredPosition = TogglePosition;
            var text = Control(IceConsoleControlKind.FloodValve).value;
            text.fontSize = 22; text.fontStyle = FontStyles.Bold; text.enableAutoSizing = false;
            var style = text.GetComponent<IceUIStyle>();
            if (style != null) { style.applyFontSize = false; PrefabUtility.RecordPrefabInstancePropertyModifications(style); }
            foreach (var modified in new Object[] { lever, valve, toggle, text }) PrefabUtility.RecordPrefabInstancePropertyModifications(modified);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static void CheckPersistence()
        {
            console = Find<IceMachineryConsole>();
            Canvas.ForceUpdateCanvases();
            Require(console != null && console.ValidateBindings(out _), "Reopened console has complete typed references.");
            Require(Vector2.Distance(Control(IceConsoleControlKind.Conditioner).interactionArea.anchoredPosition, LeverPosition) < .001f, "Moved hydraulic lever position persists.");
            Require(Vector2.Distance(Control(IceConsoleControlKind.FloodValve).interactionArea.sizeDelta, ValveSize) < .001f, "Resized valve interaction rectangle persists.");
            var resizedValve = Control(IceConsoleControlKind.FloodValve);
            Require(Vector3.Distance(resizedValve.movingGraphic.position, resizedValve.interactionArea.TransformPoint(resizedValve.interactionArea.rect.center)) < .001f,
                "Resized valve artwork rotor remains centered on its actual interaction rectangle.");
            var valueCorners = new Vector3[4]; resizedValve.value.rectTransform.GetWorldCorners(valueCorners);
            Require(valueCorners.Select(p => resizedValve.interactionArea.InverseTransformPoint(p).y).Max() < resizedValve.interactionArea.rect.yMin,
                "Resized valve readout remains below the resized face without overlap.");
            Require(Vector2.Distance(((RectTransform)Control(IceConsoleControlKind.BoardBrush).transform).anchoredPosition, TogglePosition) < .001f, "Independent maintained-toggle position persists.");
            var text = Control(IceConsoleControlKind.FloodValve).value;
            Require(Mathf.Abs(text.fontSize - 22) < .001f && text.fontStyle == FontStyles.Bold && !text.enableAutoSizing, "Local TMP font size and bold styling persist.");
            Require(!text.GetComponent<IceUIStyle>().applyFontSize, "Shared style preserves the explicit local font-size override.");
            if (!Application.isPlaying) Require(console.previewBadge.activeSelf && Control(IceConsoleControlKind.FloodValve).value.text == "45%", "Edit Mode retains representative design-only preview values and badge after Play.");
        }

        static void BeginPlay(string stage)
        {
            SessionState.SetString(SessionKey, stage); frames = 0; started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 240) throw new TimeoutException("Phase 2 Play acceptance exceeded 240 seconds.");
                string stage = SessionState.GetString(SessionKey, "");
                if (stage.EndsWith("-play"))
                {
                    if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 50) return;
                    PrepareInput();
                    if (stage == "authored-play") TestAuthored(); else if (stage == "integrated-play") TestIntegrated(); else TestProcedural();
                    RestoreInput(); SessionState.SetString(SessionKey, stage.Replace("-play", "-exit")); EditorApplication.ExitPlaymode();
                }
                else if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if (stage == "authored-exit")
                    {
                        EditorSceneManager.OpenScene(Authored); CheckPersistence();
                        Pass("Exit Play and scene reopen retain all four deliberate authoring changes.");
                        EditorSceneManager.OpenScene(Procedural);
                        Require(FindAll<IceMachineryConsole>().Length == 0 && FindAll<IceAuthoredHud>().Length == 0, "Procedural scene remains free of authored UI instances.");
                        BeginPlay("procedural-play");
                    }
                    else if (stage == "integrated-exit")
                    {
                        Require(Hash(Authored) == SessionState.GetString(SessionKey + ".integratedHash", ""), "Exact integrated scene passes Play smoke without changing any serialized bytes. SHA256=" + Hash(Authored));
                        SessionState.SetString(SessionKey, ""); EditorApplication.update -= Tick; EditorApplication.Exit(0);
                    }
                    else if (stage == "procedural-exit")
                    {
                        Require(Hash(Procedural) == SessionState.GetString(SessionKey + ".proceduralHash", ""), "Procedural scene bytes are unchanged.");
                        Pass("AUTOMATED PHASE 2 ACCEPTANCE COMPLETE. See separate physical input evidence and manual checklist for native hardware, visible Editor, full-frame performance and training workflows.");
                        SessionState.SetString(SessionKey, ""); EditorApplication.update -= Tick; EditorApplication.Exit(0);
                    }
                }
            }
            catch (Exception exception) { Fail(exception); }
        }

        static void TestAuthored()
        {
            console = Find<IceMachineryConsole>(); game = console.game;
            Require(game != null && game.AuthoredConsole == console && game.HasAuthoredConsole, "Play registers persistent console on the existing simulator.");
            if (!game.HasFocus && Application.isBatchMode)
            {
                Invoke(game, "OnApplicationFocus", true);
                Info("Hidden batch Editor did not own native window focus; injected acceptance invokes the game's focus-gained callback. Native focus acquisition remains a visible-Editor check.");
            }
            Reset();
            Require(console.IsAvailable && console.Visible && console.CanInteract, "Started driving enables authored console.");
            Require(!console.previewBadge.activeSelf, "Design preview badge is hidden in Play.");
            Require(console.controls.Where(c => c.kind == IceConsoleControlKind.Conditioner || c.kind == IceConsoleControlKind.TankLift || c.kind == IceConsoleControlKind.FloodValve || c.kind == IceConsoleControlKind.WashValve || c.kind == IceConsoleControlKind.Blade).All(c => c.selectable != null && c.selectable.targetGraphic != null), "Every custom lever, valve and blade wheel has an explicit selectable visual-state target.");
            CheckPersistence();
            int count = console.GetComponentsInChildren<Transform>(true).Length;
            console.SetCompact(true); console.RefreshNow();
            Require(console.compactGroup.alpha == 1 && console.expandedGroup.alpha == 0, "Compact mode has one visible, interactive group.");
            Click(Control(IceConsoleControlKind.OpenConsole));
            Require(!game.Dashboard.Compact && console.expandedGroup.alpha == 1, "Injected Open button expands existing hierarchy.");
            Click(Control(IceConsoleControlKind.CloseConsole));
            Require(game.Dashboard.Compact, "Injected Close button returns to compact mode.");
            KeyPress(Key.Tab);
            Require(!game.Dashboard.Compact, "Injected Tab uses existing keyboard path to expand the console.");
            Require(count == console.GetComponentsInChildren<Transform>(true).Length, "Mode changes preserve the authored object hierarchy.");
            TestHydraulics(); TestRotaries(); TestMaintained(); TestLifecycle(); TestActionsAndModal(); TestPhase1Regression();
            Reset();
            IceMachineryPhysicalValidation.Run(console);
            Reset();
            var regressionType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("IceResurfacer.Editor.IceOperatorIntegrationValidation")).First(t => t != null);
            var regressionResults = (System.Collections.Generic.List<string>)regressionType.GetMethod("Run").Invoke(null, new object[] { game });
            File.WriteAllLines(Path.Combine(Output, "OperatorIntegration-results.txt"), regressionResults);
            Pass("Existing operator integration regression completed: authored camera anchors, E/Q/C restore, cockpit picking at three lift heights, moved mounts, switches and horn release.");
            Reset(); CaptureVariants(); Measure(); CheckPersistence();
        }

        static void TestHydraulics()
        {
            foreach (var kind in new[] { IceConsoleControlKind.Conditioner, IceConsoleControlKind.TankLift })
            {
                var control = Control(kind);
                var sim = game.Sim;
                bool conditioner = kind == IceConsoleControlKind.Conditioner;
                float Demand() => conditioner ? sim.Controls.ConditionerHydraulic : sim.Controls.TankHydraulic;
                float Lift() => conditioner ? sim.Conditioner.Lift : sim.TankLift;
                control.selectable.interactable = false;
                Pointer(Point(control)); Pointer(Point(control, 0, -.32f), true);
                Require(Demand() == 0 && !control.IsHolding, kind + " explicitly disabled Selectable rejects hydraulic commands.");
                Pointer(Point(control)); control.selectable.interactable = true;
                Pointer(Point(control, 0, -.32f)); Pointer(Point(control, 0, -.32f), true);
                Require(Demand() == -1 && control.IsHolding, kind + " lower region commands -1 through actual uGUI input.");
                var selectionState = typeof(Selectable).GetProperty("currentSelectionState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control.selectable);
                Require(selectionState.ToString() == "Pressed", kind + " injected pointer hold enters the native uGUI pressed visual state.");
                if (conditioner) { float before = Lift(); sim.Step(.2f); Require(Lift() < before, "Conditioner lowering changes real lift while held."); }
                Pointer(Point(control, 0, .32f), true);
                Require(Demand() == 1, kind + " forward/top region commands +1 while dragging.");
                float prior = Lift(); sim.Step(.2f);
                Require(Lift() > prior || prior >= .999f, kind + " raising acts on authoritative hydraulics.");
                Pointer(Point(control, 0, .32f));
                float held = Lift(); sim.Step(.2f);
                Require(Demand() == 0 && !control.IsHolding && Mathf.Abs(Lift() - held) < .0001f, kind + " mouse release neutralizes demand and holds attained position.");
                Pointer(Point(control)); Pointer(Point(control), true);
                Require(Demand() == 0, kind + " authored central dead zone stays neutral.");
                Pointer(Point(control));
                Pointer(Point(control, 0, .32f), true); Pointer(Point(control, 1.5f, .32f), true);
                Require(Demand() == 0 && !control.IsHolding, kind + " pointer exit immediately releases momentary demand.");
                Pointer(Point(control, 1.5f, .32f));
            }
        }

        static void TestRotaries()
        {
            game.Sim.Controls.FloodFlow = .2f; game.Sim.Controls.WashValve = .4f;
            var flood = Control(IceConsoleControlKind.FloodValve);
            Rotate(flood, 0, 45);
            Require(Mathf.Abs(game.Sim.Controls.FloodFlow - .7f) < .01f && Mathf.Abs(game.Sim.Controls.WashValve - .4f) < .001f, "Counterclockwise half-quarter-turn opens flood by 50% independently of wash.");
            Rotate(flood, 45, 0);
            Require(Mathf.Abs(game.Sim.Controls.FloodFlow - .2f) < .01f, "Clockwise valve motion closes by the matching amount.");
            Rotate(Control(IceConsoleControlKind.WashValve), 0, 90);
            Require(game.Sim.Controls.WashValve == 1 && Mathf.Abs(game.Sim.Controls.FloodFlow - .2f) < .01f, "Wash valve is independent and clamps at fully open.");
            Rotate(flood, 0, -90);
            Require(game.Sim.Controls.FloodFlow == 0, "Flood valve clamps at fully closed.");
            Scroll(flood, 1);
            Require(Mathf.Abs(game.Sim.Controls.FloodFlow - .075f) < .001f, "Injected one-tick wheel scrolling opens flood by the legacy 7.5% notch (actual " + game.Sim.Controls.FloodFlow + ").");
            game.Sim.Controls.BladeDepthMm = 0;
            var blade = Control(IceConsoleControlKind.Blade);
            Rotate(blade, 90, 0);
            float expected = game.Sim.Config.MaxBladeDepthMm / (game.Sim.Config.BladeWheelTurns * 4);
            Require(Mathf.Abs(game.Sim.Controls.BladeDepthMm - expected) < .001f, "Clockwise blade quarter-turn respects actual configured 3.5-turn full travel.");
            float before = game.Sim.Controls.BladeDepthMm; Scroll(blade, 1);
            Require(Mathf.Abs(game.Sim.Controls.BladeDepthMm - before - game.Sim.Config.MaxBladeDepthMm * 22.5f / (game.Sim.Config.BladeWheelTurns * 360)) < .001f, "Injected positive scroll deepens the blade by its existing per-notch amount.");
            console.RefreshNow();
            Require(blade.value.text == game.Sim.Controls.BladeDepthMm.ToString("0.00") + " mm", "Blade millimeter readout reflects the authoritative setting.");
            Require(flood.value.fontSize == 22 && flood.value.fontStyle == FontStyles.Bold, "Live value updates preserve local TMP styling.");
        }

        static void TestMaintained()
        {
            foreach (var kind in new[] { IceConsoleControlKind.VerticalAuger, IceConsoleControlKind.HorizontalAuger, IceConsoleControlKind.BoardBrush, IceConsoleControlKind.WashPump })
            {
                var c = game.Sim.Controls;
                bool State() => kind == IceConsoleControlKind.VerticalAuger ? c.VerticalAuger : kind == IceConsoleControlKind.HorizontalAuger ? c.HorizontalAuger : kind == IceConsoleControlKind.BoardBrush ? c.BoardBrush : c.WashPump;
                bool before = State(); var control = Control(kind);
                Pointer(Point(control)); Pointer(Point(control), true);
                Require(State() == before, kind + " maintained button does not toggle on initial press.");
                Pointer(Point(control));
                Require(State() != before, kind + " release clicks exactly once and latches state.");
                console.RefreshNow(); Require(State() != before, kind + " visual refresh does not toggle the simulator.");
                Click(control); Require(State() == before, kind + " second click restores its maintained state.");
            }
            var brush = Control(IceConsoleControlKind.BoardBrush); bool brushBefore = game.Sim.Controls.BoardBrush;
            Pointer(Point(brush)); Pointer(Point(brush), true);
            game.ToggleVideoMode(); console.RefreshNow(); game.ToggleVideoMode(); console.RefreshNow();
            Pointer(Point(brush));
            Require(game.Sim.Controls.BoardBrush == brushBefore, "A pending maintained-button press canceled by hide/show cannot click on stale release.");
            Click(brush); Require(game.Sim.Controls.BoardBrush != brushBefore, "Fresh maintained click works after the canceled press.");
            Click(brush);
        }

        static void TestLifecycle()
        {
            var control = Control(IceConsoleControlKind.Conditioner);
            void StartHold() { console.SetCompact(false); console.RefreshNow(); Pointer(Point(control)); Pointer(Point(control, 0, -.32f), true); Require(game.Sim.Controls.ConditionerHydraulic == -1, "Lifecycle fixture acquired relocated hydraulic lever."); }
            void Released(string label) { console.TickInput(); Require(!control.IsHolding && game.Sim.Controls.ConditionerHydraulic == 0, label + " releases UI hydraulic demand."); Pointer(Point(control)); }
            StartHold(); game.TogglePause(); Released("Pause"); game.TogglePause();
            StartHold(); game.ToggleHelp(); Released("Help"); game.ToggleHelp();
            StartHold(); game.ToggleVideoMode(); Released("HUD hide"); game.ToggleVideoMode();
            StartHold(); console.SetCompact(true); Released("Compact mode");
            StartHold(); game.SelectCameraView("Rear"); Released("Camera mode change"); game.SelectCameraView("Forward");
            StartHold(); Invoke(console, "OnApplicationFocus", false); Released("Console focus-loss callback");
            StartHold(); Invoke(console, "OnApplicationPause", true); Released("Application-pause callback");
            StartHold(); control.enabled = false; Released("Control component disable"); control.enabled = true;
            StartHold(); console.enabled = false; Released("Console component disable"); console.enabled = true;
            StartHold(); Pointer(Point(control, 0, -.32f), false, false); Released("Polling a mouse release before UI module processing");
            StartHold(); var prior = game.Sim; game.NewSession(false); Released("Session reset"); Require(prior.Controls.ConditionerHydraulic == 0, "Reset also neutralizes the old simulation instance.");
            Reset();
            if (game.RouteGuide != null)
            {
                StartHold(); game.RouteGuide.ToggleRecordingPanel(); Released("Route recording panel");
                Require(!console.Visible, "Route panel suppresses console visibility."); game.RouteGuide.ToggleRecordingPanel();
            }
            Reset();
        }

        static void TestActionsAndModal()
        {
            int strokes = game.Sim.SnowBreakerStrokes;
            Click(Control(IceConsoleControlKind.SnowBreaker));
            Require(game.Sim.SnowBreakerStrokes == strokes + 1, "Injected breaker action calls the existing snow-breaker command once.");
            Click(Control(IceConsoleControlKind.Service));
            Require(game.Notice.Contains("wall hose"), "Service action preserves existing refill-help workflow.");
            var before = game.Sim;
            var floodControl = Control(IceConsoleControlKind.FloodValve);
            Pointer(Point(floodControl)); Pointer(Point(floodControl, .32f, 0), true);
            Pointer(Point(Control(IceConsoleControlKind.NewSession)), true); Pointer(Point(Control(IceConsoleControlKind.NewSession)));
            Require(!console.ConfirmationOpen && game.Sim == before, "Dragging from a valve onto New Session and releasing cannot click the destructive action.");
            Click(Control(IceConsoleControlKind.NewSession));
            Require(console.ConfirmationOpen && game.Sim == before, "New-session button opens explicit confirmation without discarding the run.");
            Require(console.confirmationGroup.blocksRaycasts && !console.expandedGroup.interactable, "Confirmation blocks underlying machinery interaction.");
            var hud = Find<IceAuthoredHud>(); var load = hud.widgets.Single(x => x.kind == IceHudWidgetKind.Load);
            bool diagnostics = game.DiagnosticsVisible;
            Pointer(Point((RectTransform)load.transform)); Pointer(Point((RectTransform)load.transform), true); Pointer(Point((RectTransform)load.transform));
            Require(game.DiagnosticsVisible == diagnostics, "Modal raycast shields the Phase 1 diagnostics button.");
            load.action.onClick.Invoke(); Require(game.DiagnosticsVisible == diagnostics, "Phase 1 binding guard also rejects diagnostics while console confirmation owns input.");
            Vector2 empty = ReferencePoint(600, 450);
            Require(game.Dashboard.PointerOverInterface(game, empty), "Modal shields empty-screen physical and legacy pointer paths.");
            Click(Control(IceConsoleControlKind.CancelAction));
            Require(!console.ConfirmationOpen && game.Sim == before && !game.Dashboard.PointerOverInterface(game, empty), "Cancel preserves session and removes the inactive fullscreen blocker.");
            Click(Control(IceConsoleControlKind.NewSession)); Click(Control(IceConsoleControlKind.ConfirmAction));
            Require(game.Sim != before && !game.Started && !console.Visible, "Confirmed new session uses existing startup flow and hides machinery.");
            Reset();
            Click(Control(IceConsoleControlKind.Finish));
            Require(game.Sim.Finished && !console.Visible, "Free Simulation Finish retains existing immediate finish/results semantics.");
            game.NewSession(false, true, IceResurfacer.Core.SimulationMode.GuidedTraining); console.RefreshNow(); console.SetCompact(false);
            Click(Control(IceConsoleControlKind.Finish));
            Require(!game.Sim.Finished && !string.IsNullOrEmpty(game.Notice), "Guided Training Finish preserves existing completion safeguards.");
            Reset();
        }

        static void TestPhase1Regression()
        {
            var hud = Find<IceAuthoredHud>(); hud.RefreshNow();
            Require(hud.IsAvailable && hud.Visible && hud.widgets.Length == 8, "Phase 1 header remains available with all eight widgets.");
            var flood = hud.widgets.Single(x => x.kind == IceHudWidgetKind.Flood);
            Require(Mathf.Abs(flood.fill.fillAmount - game.Sim.FloodWaterLiters / game.Sim.Config.FloodCapacityLiters) < .0001f, "Phase 1 water fill still uses actual capacity.");
            var load = hud.widgets.Single(x => x.kind == IceHudWidgetKind.Load); bool before = game.DiagnosticsVisible;
            Pointer(Point((RectTransform)load.transform)); Pointer(Point((RectTransform)load.transform), true); Pointer(Point((RectTransform)load.transform));
            Require(game.DiagnosticsVisible != before, "Phase 1 diagnostics still responds to injected mouse clicks outside confirmation.");
            game.ToggleDiagnostics();
            Require(game.Sim.TryDismount(), "Existing simulation dismount remains available."); console.RefreshNow(); hud.RefreshNow();
            Require(!console.Visible && hud.Visible && !hud.CanInteract, "On-foot state hides console while retaining the read-only Phase 1 header.");
            Require(game.Sim.TryBoard(), "Existing boarding remains available."); console.RefreshNow(); hud.RefreshNow();
            Require(console.Visible && hud.CanInteract, "Boarding restores both authored UI groups.");
        }

        static void CaptureVariants()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(IceResurfacer.Editor.IceMachineryConsoleFactory.ConsolePrefabPath).GetComponent<IceMachineryConsole>();
            IceConsoleControl Original(IceConsoleControlKind kind) => source.controls.Single(c => c.kind == kind);
            var lever = Control(IceConsoleControlKind.Conditioner).interactionArea;
            var valve = Control(IceConsoleControlKind.FloodValve).interactionArea;
            var toggle = (RectTransform)Control(IceConsoleControlKind.BoardBrush).transform;
            var text = Control(IceConsoleControlKind.FloodValve).value;
            try
            {
                lever.anchoredPosition = Original(IceConsoleControlKind.Conditioner).interactionArea.anchoredPosition;
                valve.sizeDelta = Original(IceConsoleControlKind.FloodValve).interactionArea.sizeDelta;
                toggle.anchoredPosition = ((RectTransform)Original(IceConsoleControlKind.BoardBrush).transform).anchoredPosition;
                text.fontSize = Original(IceConsoleControlKind.FloodValve).value.fontSize; text.fontStyle = FontStyles.Normal;
                CaptureModes("Default");
            }
            finally { lever.anchoredPosition = LeverPosition; valve.sizeDelta = ValveSize; toggle.anchoredPosition = TogglePosition; text.fontSize = 22; text.fontStyle = FontStyles.Bold; }
            CaptureModes("Edited");
        }

        static void CaptureModes(string variant)
        {
            var canvas = console.canvas; var camera = game.DriverCamera;
            var mode = canvas.renderMode; var previousCamera = canvas.worldCamera; float distance = canvas.planeDistance;
            var targetBefore = camera.targetTexture; var activeBefore = RenderTexture.active; float aspect = camera.aspect;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .5f;
                foreach (bool compact in new[] { true, false })
                foreach (var size in new[] { new Vector2Int(1366, 768), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) })
                {
                    console.SetCompact(compact); console.RefreshNow();
                    var target = new RenderTexture(size.x, size.y, 24); var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try
                    {
                        target.Create(); camera.targetTexture = target; camera.aspect = (float)size.x / size.y;
                        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                        string name = "Machinery-" + variant + "-" + (compact ? "Compact" : "Expanded") + "-" + size.x + "x" + size.y + ".png";
                        File.WriteAllBytes(Path.Combine(Output, name), image.EncodeToPNG());
                        Pass("Rendered " + name + "; temporary ScreenSpaceCamera capture of authored Canvas, legacy IMGUI excluded.");
                        foreach (var control in console.controls.Where(c => console.CanUse(c)))
                        {
                            var corners = new Vector3[4]; control.interactionArea.GetWorldCorners(corners);
                            Require(corners.Select(p => camera.WorldToScreenPoint(p)).All(p => p.x >= -1 && p.y >= -1 && p.x <= size.x + 1 && p.y <= size.y + 1),
                                variant + " " + (compact ? "compact" : "expanded") + " " + size.x + "x" + size.y + " keeps " + control.kind + " interaction rectangle inside capture.");
                        }
                    }
                    finally { camera.targetTexture = targetBefore; RenderTexture.active = activeBefore; Object.Destroy(image); Object.Destroy(target); }
                }
            }
            finally { canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.planeDistance = distance; camera.targetTexture = targetBefore; camera.aspect = aspect; RenderTexture.active = activeBefore; Canvas.ForceUpdateCanvases(); }
        }

        static void Measure()
        {
            long first = GC.GetAllocatedBytesForCurrentThread(); var probe = new byte[4096]; long calibration = GC.GetAllocatedBytesForCurrentThread() - first; GC.KeepAlive(probe);
            long before = GC.GetAllocatedBytesForCurrentThread(); var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++) console.RefreshNow(); watch.Stop();
            Info("Refresh microbenchmark " + (watch.Elapsed.TotalMilliseconds / 100).ToString("0.000") + " ms/call; " +
                (calibration >= 4096 ? ((GC.GetAllocatedBytesForCurrentThread() - before) / 100) + " managed bytes/call." : "allocation counter unavailable: retained 4096B calibration reported " + calibration + "B."));
            Info("Console contains " + console.GetComponentsInChildren<Graphic>(true).Length + " Graphics and " + console.GetComponentsInChildren<LayoutGroup>(true).Length + " layout groups. Full Canvas has " + console.canvas.GetComponentsInChildren<Graphic>(true).Length + " Graphics.");
            Info(UnityStats.drawCalls == 0 ? "Draw-call counter unavailable in hidden batchmode (zero despite rendered captures). Visible Profiler/Frame Debugger required." : "Editor drawCalls=" + UnityStats.drawCalls + ", setPassCalls=" + UnityStats.setPassCalls + "; includes scene/editor, not a UI-only baseline comparison.");
        }

        static void TestProcedural()
        {
            game = Find<IceResurfacerGame>(); Require(game != null && game.Sim != null, "Procedural launch still initializes existing simulation.");
            game.NewSession(false); Require(game.AuthoredConsole == null && game.AuthoredHud == null, "Procedural launch retains legacy UI ownership.");
            Require(game.Dashboard.PointerOverInterface(game, ReferencePoint(1078, 60)), "Procedural launch retains legacy header pointer region.");
            game.Dashboard.Compact = false;
            Require(game.Dashboard.PointerOverInterface(game, ReferencePoint(1320, 390)), "Procedural launch retains legacy expanded machinery pointer region.");
            var update = typeof(IceResurfacerGame).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
            Keys(Key.Digit3); update.Invoke(game, null); Require(game.Sim.Controls.VerticalAuger, "Procedural keyboard machinery command still functions."); Keys();
        }

        static void TestIntegrated()
        {
            console = Find<IceMachineryConsole>(); game = console.game;
            if (!game.HasFocus && Application.isBatchMode) Invoke(game, "OnApplicationFocus", true);
            Reset();
            Require(game.AuthoredConsole == console && console.IsAvailable && console.CanInteract && Find<IceAuthoredHud>().IsAvailable, "Exact integrated scene binds both authored modules in actual Play Mode.");
            console.SetCompact(true); Click(Control(IceConsoleControlKind.OpenConsole));
            Require(!game.Dashboard.Compact && console.expandedGroup.alpha == 1, "Exact integrated scene expands through injected native uGUI handlers.");
            var lever = Control(IceConsoleControlKind.Conditioner);
            Pointer(Point(lever)); Pointer(Point(lever, 0, -.32f), true);
            Require(game.Sim.Controls.ConditionerHydraulic == -1, "Exact integrated scene's default authored lever commands real hydraulics.");
            Pointer(Point(lever)); Require(game.Sim.Controls.ConditionerHydraulic == 0, "Exact integrated scene's hydraulic release is neutral.");
            game.Sim.Controls.FloodFlow = 0; Rotate(Control(IceConsoleControlKind.FloodValve), 0, 45);
            Require(Mathf.Abs(game.Sim.Controls.FloodFlow - .5f) < .01f, "Exact integrated scene's default valve controls the existing simulation.");
            Pass("Exact integrated scene smoke completed; no scene, prefab or authoring data was saved by this verification.");
        }

        static void Reset()
        {
            Keys(); game.NewSession(false); if (game.HudHidden) game.ToggleVideoMode(); if (game.DiagnosticsVisible) game.ToggleDiagnostics();
            Cursor.lockState = CursorLockMode.None; console.SetCompact(false); console.RefreshNow(); Find<IceAuthoredHud>().RefreshNow(); Canvas.ForceUpdateCanvases(); Pointer(ReferencePoint(600, 450));
        }
        static IceConsoleControl Control(IceConsoleControlKind kind) => console.controls.Single(c => c.kind == kind);
        static Vector2 Point(IceConsoleControl control, float x = 0, float y = 0) => Point(control.interactionArea, x, y);
        static Vector2 Point(RectTransform rect, float x = 0, float y = 0) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center + new Vector2(rect.rect.width * x, rect.rect.height * y)));
        static Vector2 ReferencePoint(float x, float y) { float s = Mathf.Min(Screen.width / 1600f, Screen.height / 900f); return new Vector2((Screen.width - 1600 * s) * .5f + x * s, Screen.height - (Screen.height - 900 * s) * .5f - y * s); }
        static void Pointer(Vector2 point, bool down = false, bool process = true)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, down)); InputSystem.Update(); if (process) module.Process();
        }
        static void Click(IceConsoleControl control) { console.RefreshNow(); Canvas.ForceUpdateCanvases(); Pointer(Point(control)); Pointer(Point(control), true); Pointer(Point(control)); console.RefreshNow(); }
        static void Rotate(IceConsoleControl control, float from, float to)
        {
            var rect = control.interactionArea;
            float radius = Mathf.Min(rect.rect.width, rect.rect.height) * .32f;
            Vector2 At(float degrees) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center +
                new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * radius));
            Pointer(At(from)); Pointer(At(from), true); Pointer(At(to), true); Pointer(At(to));
        }
        static void Scroll(IceConsoleControl control, float amount)
        {
            Pointer(Point(control)); InputSystem.QueueStateEvent(mouse, new MouseState { position = Point(control), scroll = new Vector2(0, amount) }); InputSystem.Update(); module.Process(); Pointer(Point(control));
        }
        static void Keys(params Key[] held) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(held)); InputSystem.Update(); }
        static void KeyPress(Key key) { Keys(key); Invoke(game, "Update"); Keys(); Invoke(game, "Update"); console.RefreshNow(); }
        static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        static T[] FindAll<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        static T Find<T>() where T : Component => FindAll<T>().SingleOrDefault();
        static string Hash(string path) { using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
        static void PrepareInput()
        {
            inputSettings = InputSystem.settings; temporarySettings = Object.Instantiate(inputSettings); temporarySettings.hideFlags = HideFlags.HideAndDontSave;
            temporarySettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            temporarySettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; InputSystem.settings = temporarySettings;
            previousMouse = Mouse.current; previousKeyboard = Keyboard.current;
            mouse = InputSystem.AddDevice<Mouse>("Aurora phase 2 acceptance mouse"); mouse.MakeCurrent();
            keyboard = InputSystem.AddDevice<Keyboard>("Aurora phase 2 acceptance keyboard"); keyboard.MakeCurrent();
            module = EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
        }
        static void RestoreInput()
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse); if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent(); if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            if (inputSettings != null) InputSystem.settings = inputSettings; if (temporarySettings != null) Object.Destroy(temporarySettings);
            mouse = previousMouse = null; keyboard = previousKeyboard = null; inputSettings = temporarySettings = null;
        }
        static void Guard() { if (!Root.Replace('\\', '/').Contains("/Validation/UIAuthoring-Phase2/") || !File.Exists(Path.Combine(Root, Marker))) throw new InvalidOperationException("This destructive authoring test requires the marked disposable Phase 2 validation project."); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Pass(message); }
        static void Pass(string message) { Directory.CreateDirectory(Output); File.AppendAllText(Results, "PASS " + message + "\n"); }
        static void Info(string message) { Directory.CreateDirectory(Output); File.AppendAllText(Results, "INFO " + message + "\n"); }
        static void Fail(Exception exception)
        {
            RestoreInput(); SessionState.SetString(SessionKey, ""); EditorApplication.update -= Tick; Debug.LogException(exception);
            if (File.Exists(Path.Combine(Root, Marker))) { Directory.CreateDirectory(Output); File.AppendAllText(Results, "FAIL " + exception + "\n"); }
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
