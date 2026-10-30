#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// Explicit development-player validation only. The normal launch scene never contains
    /// this component; the editor build tool attaches it to a disposable scene clone.
    /// </summary>
    public sealed class IceLayoutPlayerProbe : MonoBehaviour
    {
        readonly List<string> failures = new List<string>();
        readonly List<string> layoutIssues = new List<string>();
        readonly List<string> passes = new List<string>();
        readonly List<string> inputTrace = new List<string>();
        IceResurfacerGame game;
        string outputDirectory, currentResolution = "initialization";
        float startedAt;
        int sentEvents, consumedEvents;
        int requestedWidth = 1366, requestedHeight = 768;
        EventType expectedInput;
        IntPtr playerWindow;
        bool active, complete;
        InputSettings originalInputSettings,validationInputSettings;
        string ArtifactDirectory => Path.Combine(outputDirectory,"TrainingGuide","Player-"+currentResolution);

        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--aurora-validation-output") outputDirectory = Path.GetFullPath(args[i + 1]);
                if (args[i] == "--aurora-validation-width") requestedWidth = int.Parse(args[i + 1]);
                if (args[i] == "--aurora-validation-height") requestedHeight = int.Parse(args[i + 1]);
            }
            // Attaching the component without the explicit command does not run a test or write files.
            if (string.IsNullOrEmpty(outputDirectory)) { enabled = false; yield break; }
            var directory = new DirectoryInfo(outputDirectory);
            if (directory.Name != "Validation~" || directory.Parent == null || directory.Parent.Name != "IceResurfacer")
            { Debug.LogError("Aurora validation output must be the experiment's Validation~ directory."); Application.Quit(1); yield break; }
            Directory.CreateDirectory(outputDirectory);
            Application.runInBackground = true;
            // Native offscreen D3D presentation diagnostics can reopen the developer
            // console between capture yields. Keep that overlay out of this opt-in
            // HUD fixture; all managed errors still fail below, and the launcher
            // records native presentation diagnostics separately in the report.
            Debug.developerConsoleEnabled=false;
            // This deliberately non-focused player must accept its own virtual
            // keyboard events. Never change the user's asset or normal launch path.
            originalInputSettings=InputSystem.settings;validationInputSettings=UnityEngine.Object.Instantiate(originalInputSettings);
            validationInputSettings.hideFlags=HideFlags.HideAndDontSave;
            validationInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings=validationInputSettings;
            Application.logMessageReceived += OnLog;
            startedAt = Time.realtimeSinceStartup;
            active = true;
            yield return RunSafely(Run());
        }

        IEnumerator RunSafely(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0 && !complete)
            {
                object yielded = null;
                bool moved = false;
                Exception error = null;
                try { moved = stack.Peek().MoveNext(); if (moved) yielded = stack.Peek().Current; }
                catch (Exception e) { error = e; }
                if (error != null) { failures.Add(error.ToString()); Finish(); yield break; }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (yielded is IEnumerator nested) stack.Push(nested);
                else yield return yielded;
            }
        }

        IEnumerator Run()
        {
            while (game == null || game.Sim == null || game.Dashboard == null)
            { game = UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>(); yield return null; }
            game.enabled = false;
            game.NewSession(false);
            game.Dashboard.AuditLayout = true;
            // The launcher selects the client resolution before native startup.
            // One resolution per process avoids runtime swapchain changes.
            yield return new WaitForSecondsRealtime(2);
            playerWindow = FindOwnPlayerWindow();
            if (playerWindow == IntPtr.Zero) throw new InvalidOperationException("No process-owned Unity player window for native input validation.");
            yield return ValidateResolution(requestedWidth, requestedHeight);
            Finish();
        }

        IEnumerator ValidateResolution(int width, int height)
        {
            currentResolution = width + "x" + height;
            float until = Time.realtimeSinceStartup + 8;
            while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < until) yield return null;
            Check(Screen.width == width && Screen.height == height,
                "Requested client resolution (actual " + Screen.width + "x" + Screen.height + ")");
            // Give the native player window and its repaint loop time to settle.
            for (int i = 0; i < 8; i++) yield return null;
            game.NewSession(false);
            game.Dashboard.AuditLayout = true;
            yield return Capture("HUD-" + currentResolution + ".png", "normal HUD");
            Check(game.Dashboard.Compact,"Default HUD uses collapsed side console");
            Check(IceDashboard.ConditionerStatus(game.Sim)=="CONDITIONER UP","Persistent status reports actual raised conditioner");
            yield return Click("Console");
            Check(!game.Dashboard.Compact,"Right console expands through actual UI target");
            yield return Capture("HUD-"+currentResolution+"-console.png","expanded side console / transverse V C H D controls");
            game.Notice="Refill help remains available in message history after the compact notification fades.";
            yield return null;
            yield return Click("History");
            Check(game.Dashboard.HistoryVisible,"Message history opens without altering machine state");
            yield return Capture("HUD-"+currentResolution+"-messages.png","accessible notification history");
            yield return Click("History");
            Check(!game.Dashboard.HistoryVisible,"Message history closes through persistent control");
            game.Notice="";
            yield return Click("MachineLoad");
            Check(game.DiagnosticsVisible,"Machine Load meter opens its demand explanation");
            yield return Capture("HUD-"+currentResolution+"-diagnostics.png","load explanation and diagnostics");
            yield return Click("MachineLoad");
            Check(!game.DiagnosticsVisible,"Machine Load explanation closes without changing machinery");
            float surveyWater=game.Sim.FloodWaterLiters;
            yield return ClickAt(IceDashboard.SurveyMapRect.position+new Vector2(49,112));
            Check(game.Dashboard.InspectedSurveyCell>=0,"Native map pointer selects actual ice for magnified treatment inspection");
            Check(game.Sim.FloodWaterLiters==surveyWater,"Survey inspection does not apply water or alter machinery");
            yield return Capture("HUD-"+currentResolution+"-survey.png","magnified exact-cell survey and treatment legend");
            yield return ClickAt(new Vector2(530,130));

            var controls = game.Sim.Controls;
            yield return HoldHydraulic("Conditioner",false);
            Check(IceDashboard.ConditionerStatus(game.Sim)=="CONDITIONER PARTIALLY LOWERED","Neutral lever preserves actual partly lowered status");
            yield return Capture("HUD-"+currentResolution+"-conditioner-partial.png","actual held conditioner position");
            yield return Click("HorizontalAuger");
            Check(controls.HorizontalAuger, "Horizontal auger switch");
            yield return Click("VerticalAuger");
            Check(controls.VerticalAuger, "Vertical auger switch");
            yield return Click("BoardBrush");
            Check(controls.BoardBrush, "Board brush switch");

            float bladeBefore = controls.BladeDepthMm;
            yield return RotateDial("Blade");
            Check(Mathf.Abs(controls.BladeDepthMm-bladeBefore-.8f/14)<.01f, "Precise quarter-turn blade handwheel drag");
            controls.BladeDepthMm=0;
            yield return ValidateBladeTurns();
            float washBefore = controls.WashValve;
            yield return RotateWater("WashValve",.38f);
            Check(Mathf.Abs(controls.WashValve-.38f)<.035f, "Analog quarter-turn wash handle");
            yield return Click("WashPump");
            Check(controls.WashPump, "Pickup pump switch");
            yield return RotateWater("Flood",.43f);
            Check(Mathf.Abs(controls.FloodFlow - .43f) < .035f, "Independent analog quarter-turn flood handle");
            yield return HoldHydraulic("TankLift",true);

            // The manual actuator has its own real interlock. This UI fixture supplies a
            // lowered conditioner and running vertical auger; it never changes ice cells.
            game.Sim.Conditioner.Lift = 0;
            controls.ConditionerTarget = 0;
            controls.TankLiftTarget = 0;
            controls.VerticalAuger = true;
            Check(IceDashboard.ConditionerStatus(game.Sim)=="CONDITIONER DOWN","Actual lowered conditioner has explicit persistent DOWN status");
            yield return Capture("HUD-"+currentResolution+"-conditioner-down.png","conditioner down, blade set reported separately");
            int strokesBefore = game.Sim.SnowBreakerStrokes;
            yield return Click("SnowBreaker");
            Check(game.Sim.SnowBreakerStrokes == strokesBefore + 1, "Manual snow-breaker button and interlock");

            yield return Click("Guide");
            Check(game.HelpVisible, "Guide button opens");
            yield return Capture("HUD-" + currentResolution + "-guide.png", "operator guide");
            yield return Click("Guide");
            Check(!game.HelpVisible, "Guide button closes");
            yield return Click("Pause");
            Check(game.Paused, "Pause button");
            yield return Capture("HUD-" + currentResolution + "-pause.png", "pause panel");
            yield return Click("Pause");
            Check(!game.Paused, "Resume button");

            // Return to the default service-bay fixture for service/finish controls.
            game.NewSession(false);
            controls = game.Sim.Controls;
            controls.FloodFlow = .3f;
            game.Sim.Step(.2f);
            controls.FloodFlow = 0;
            game.Notice = "";
            float serviceHelpWater=game.Sim.FloodWaterLiters;
            yield return Click("Service");
            Check(game.Sim.FloodWaterLiters==serviceHelpWater && game.Notice.Contains("hose"),
                "Service help explains physical hose without silently topping up tanks");
            game.Notice = "";
            yield return Click("Finish");
            Check(game.Sim.Finished, "Free Simulation may finish a partial session without a progression guard");
            game.NewSession(false);
            yield return Click("NewSession");
            Check(!game.Started, "New-session button");
            yield return Capture("HUD-" + currentResolution + "-welcome.png", "welcome panel");
            yield return ClickAt(new Vector2(950, 480));
            Check(game.Started && game.Sim.Practice, "Unscored-practice launch button");
            game.NewSession(false, false);
            yield return ClickAt(new Vector2(510, 490));
            Check(game.Started && !game.Sim.Practice, "Scored-run launch button");

            // A legal partial run exercises the results UI; the separate deterministic
            // full-sheet validation covers score and coverage. Back on and drive out
            // with raised, dry equipment, then brake before calling the normal finish path.
            game.NewSession(false);
            controls = game.Sim.Controls;
            controls.Throttle = -.6f;
            for (int i = 0; i < 1000; i++) game.Sim.Step(.02f);
            controls.Throttle = .6f;
            for (int i = 0; i < 1400 && !game.Sim.InServiceBay; i++) game.Sim.Step(.02f);
            for (int i = 0; i < 180; i++) game.Sim.Step(.02f);
            controls.Throttle = 0; controls.Brake = true;
            for (int i = 0; i < 250; i++) game.Sim.Step(.02f);
            game.Finish();
            Check(game.Sim.Finished && game.Sim.ProperExit, "Legal partial run reaches results through normal finish path");
            yield return Capture("HUD-" + currentResolution + "-results.png", "results panel");
            // Cross the flush snow grate and contact the actual rear room wall.
            game.NewSession(false);
            game.Sim.Controls.Throttle=.08f;
            for(int i=0;i<3000&&!game.Sim.BoardContact;i++)game.Sim.Step(.02f);
            Check(game.Sim.LastMovementContact.Obstacle==MovementObstacle.ServiceWall,"F3 fixture crosses traversable grate and reaches actual rear wall");
            typeof(IceResurfacerGame).GetMethod("UpdateMachine",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(game,new object[]{0f});
            game.ToggleDiagnostics();
            game.SelectCameraView("Chase");game.TickInspectionCamera(2f);
            yield return Capture("HUD-"+currentResolution+"-pit-contact.png","F3 wall contact after crossing snow grate");
            game.Dashboard.Compact=true;
            yield return Capture("HUD-"+currentResolution+"-pit-overlay.png","compact F3 keeps the actual pit and footprint outlines visible");
            game.Dashboard.Compact=false;
            game.ToggleDiagnostics();
            game.NewSession(false);
            var beforeGuide=game.Sim.Vehicle;
            float guideX=beforeGuide.X,guideZ=beforeGuide.Z,guideWater=game.Sim.FloodWaterLiters;
            game.RouteGuide.Toggle();
            Check(game.RouteGuide.Visible&&game.Sim.Vehicle.X==guideX&&game.Sim.Vehicle.Z==guideZ&&game.Sim.FloodWaterLiters==guideWater,"Route guide toggle only changes drawing");
            yield return Capture("HUD-"+currentResolution+"-route.png","optional advisory route and next segment map");
            game.RouteGuide.Toggle();
            // Explicit on-ice camera fixture, separate from the control-driven full-run
            // harness. Only its initial pose is seeded; the shown cut uses real machinery.
            var surfaceSim=game.Sim;var surfaceControls=surfaceSim.Controls;
            surfaceSim.Vehicle.X=-5;surfaceSim.Vehicle.Z=-15;surfaceSim.Vehicle.Heading=0;
            surfaceSim.Conditioner.X=-5;surfaceSim.Conditioner.Z=-15-surfaceSim.Config.HitchBehindAxle-surfaceSim.Config.DrawbarLength;
            surfaceSim.Conditioner.Heading=0;surfaceSim.Conditioner.Lift=0;
            surfaceControls.ConditionerTarget=0;surfaceControls.Throttle=.42f;
            surfaceControls.BladeDepthMm=.35f;surfaceControls.HorizontalAuger=surfaceControls.VerticalAuger=true;
            surfaceControls.FloodFlow=.32f;
            for(int i=0;i<250;i++)surfaceSim.Step(.02f);
            game.Surface.Refresh(surfaceSim);
            typeof(IceResurfacerGame).GetProperty("Report").SetValue(game,surfaceSim.GetReport());
            typeof(IceResurfacerGame).GetMethod("UpdateMachine",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(game,new object[]{.02f});
            game.SelectCameraView("Forward");game.TickInspectionCamera(2f);
            yield return Capture("HUD-"+currentResolution+"-operator.png","on-ice seated operator and expanded controls / seeded camera fixture");
            game.Dashboard.Compact=true;
            yield return Capture("HUD-"+currentResolution+"-compact.png","compact operator view");
            game.Dashboard.Compact=false;
            yield return CaptureQuicklooks();
            game.StartServicePractice();
            game.Sim.TryDismount();
            typeof(IceWaterService).GetMethod("BeginWalk",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(game.WaterService,null);
            game.WaterService.HandleInput(true,0);
            game.WaterService.UpdateViewAndEffects(true,.25f);
            yield return Capture("HUD-"+currentResolution+"-service.png","service panel");
            yield return ValidateRouteRecorder();
            yield return ValidateTrainingGuide();
        }

        IEnumerator ValidateTrainingGuide()
        {
            game.NewSession(true);var guide=game.RouteGuide;guide.Refresh();guide.UseOriginalReference();
            Check(guide.SelectedGuide==null&&!guide.Visible,"Training fixture has not replaced or enabled the original F5 default");
            yield return Click("RouteRecorder");yield return ClickAt(new Vector2(1230,100));
            Check(guide.TrainingPanelOpen,"Native F6 Training Guides tab opens full-route authoring preview");
            yield return ClickAt(TrainingGuidePanel.ControlRect("Fixture").center);
            Check(guide.IsTestGuide&&guide.ActiveFit.Ready&&!guide.Visible,"Explicit fixture selection loads a labeled physically validated guide without enabling F5");
            yield return Capture("Training-"+currentResolution+"-overview.png","full-route preview reserved for training authoring interface");
            yield return ClickAt(new Vector2(690,757));
            Check(!guide.Footprints.ComparingFullPlan&&guide.Footprints.MissedBladeCells==0,"runtime missed-area view excludes all future unconfirmed passes");
            yield return ClickAt(new Vector2(175,757));
            string saved=TrainingGuideStorage.SaveCopy(guide.GuideDirectory,guide.SelectedGuide),bytes=File.ReadAllText(saved);
            yield return ClickAt(TrainingGuidePanel.ControlRect("Reference").center);
            Check(guide.SelectedGuide==null,"Native Original F5 Reference button restores the default source");
            yield return ClickAt(TrainingGuidePanel.ControlRect("Refresh").center);yield return ClickAt(new Vector2(1100,335));
            Check(guide.IsTestGuide,"Native guide-file list loads a persisted .guide.json without code changes");
            yield return ClickAt(TrainingGuidePanel.ControlRect("Toggle").center);
            Check(guide.Visible,"Native Show Guide / F5 button enables advisory guide");
            yield return ClickAt(RouteRecordingPanel.ControlRect("Close").center);
            var fit=guide.ActiveFit;int straight=-1,turn=-1;
            for(int i=0;i<fit.legs.Count;i++)
            {var leg=fit.legs[i];if(leg.direction>0&&leg.end-leg.start>12&&!leg.turn&&straight<0)straight=i;if(straight>=0&&i>straight&&leg.turn){turn=i;break;}}
            Check(straight>=0&&turn>=0,"fixture includes straight work passes followed by curved perimeter context");
            SetGuidePose(straight,Math.Max(fit.legs[straight].start,fit.legs[straight].end-12));
            game.SelectCameraView("Forward");game.TickInspectionCamera(2);
            Check(guide.DisplayPointCount>0&&guide.DisplayPointCount<=120&&guide.LookEnd-guide.Progress.Distance<=28.001,"live world preview stays bounded to current/upcoming 28 m");
            Check(guide.MapPointCount>0&&guide.MapPointCount<=120&&guide.MapPointAt(guide.MapPointCount-1).distance<=guide.LookEnd+.001,"Surface Survey uses limited cached route section rather than full overlapping pattern");
            yield return Capture("Training-"+currentResolution+"-seated.png","seated route line, upcoming turn card and readable Surface Survey");
            var previous=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>();
            var look=typeof(IceResurfacerGame).GetMethod("UpdateLook",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R));InputSystem.Update();look.Invoke(game,new object[]{true,.02f});
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();for(int i=0;i<20;i++)look.Invoke(game,new object[]{true,.02f});
                Check(game.Standing,"actual R input enters standing cockpit with guide enabled");
                yield return Capture("Training-"+currentResolution+"-standing.png","standing live route and map");
            }
            finally{InputSystem.RemoveDevice(keyboard);if(previous!=null&&previous.added)previous.MakeCurrent();}
            SetGuidePose(turn,fit.legs[turn].start+3);game.SelectCameraView("Chase");game.TickInspectionCamera(2);
            yield return Capture("Training-"+currentResolution+"-turn-chase.png","curved upcoming route is depth occluded by solid vehicle geometry");
            float x=game.Sim.Vehicle.X,z=game.Sim.Vehicle.Z,water=game.Sim.FloodWaterLiters;var throttle=game.Sim.Controls.Throttle;
            guide.RejoinLeg(Math.Min(turn+2,fit.legs.Count-1));guide.Refresh();guide.Toggle();guide.Toggle();
            Check(game.Sim.Vehicle.X==x&&game.Sim.Vehicle.Z==z&&game.Sim.FloodWaterLiters==water&&game.Sim.Controls.Throttle==throttle,"rejoin and repeated F5 toggles preserve free control, exact pose and machinery state");
            Check(File.ReadAllText(saved)==bytes,"runtime preview and guidance do not mutate saved guide data");
            ValidateGuideOcclusion();
            guide.Toggle();guide.UseOriginalReference();Check(guide.SelectedGuide==null&&!guide.Visible,"native validation restores original F5 source and leaves guide off");
            game.NewSession(true);
        }
        void SetGuidePose(int leg,double distance)
        {
            var guide=game.RouteGuide;var p=guide.ActiveFit.At(distance);var s=game.Sim;
            // Explicit camera/render fixture only; guidance itself never seeds a pose.
            s.Vehicle.X=p.x;s.Vehicle.Z=p.z;s.Vehicle.Heading=p.heading;s.Vehicle.Speed=p.direction*.9f;
            s.Conditioner.X=p.conditionerX;s.Conditioner.Z=p.conditionerZ;s.Conditioner.Heading=p.conditionerHeading;s.Conditioner.Lift=0;
            guide.RejoinLeg(leg);guide.ObserveSimulationStep(s);guide.Refresh();
            typeof(IceResurfacerGame).GetMethod("UpdateMachine",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(game,new object[]{0f});
            game.Surface.Invalidate();game.Surface.Refresh(s);
        }
        void ValidateGuideOcclusion()
        {
            var root=new GameObject("Validation-only training shader depth test");
            var cameraNode=new GameObject("Validation-only overhead depth camera");cameraNode.transform.SetParent(root.transform);
            var camera=cameraNode.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=2;
            camera.transform.position=new Vector3(1000,10,1000);camera.transform.rotation=Quaternion.Euler(90,0,0);
            camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.SetParent(root.transform);cube.layer=31;
            cube.transform.position=new Vector3(1000,1,1000);cube.transform.localScale=new Vector3(.8f,.5f,.8f);
            var opaque=new Material(Resources.Load<Material>("AuroraLit"));opaque.SetColor("_BaseColor",Color.red);opaque.SetColor("_EmissionColor",Color.red);opaque.EnableKeyword("_EMISSION");cube.GetComponent<Renderer>().sharedMaterial=opaque;
            var node=new GameObject("Actual training shader test line");node.transform.SetParent(root.transform);node.layer=31;
            var material=new Material(Resources.Load<Shader>("TrainingRoute"));material.SetColor("_BaseColor",Color.green);
            var line=node.AddComponent<LineRenderer>();line.sharedMaterial=material;line.startWidth=line.endWidth=.16f;line.positionCount=2;
            line.SetPosition(0,new Vector3(998.4f,0,1000));line.SetPosition(1,new Vector3(1001.6f,0,1000));
            var target=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;Texture2D image=null;
            try
            {
                target.Create();camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image=new Texture2D(128,128,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();
                Color hidden=image.GetPixel(64,64),visible=image.GetPixel(105,64);
                Check(hidden.r>hidden.g+.2f&&visible.g>visible.r+.2f,"native framebuffer: training line is visible on exposed ground and hidden behind an opaque solid");
                File.WriteAllBytes(Path.Combine(ArtifactDirectory,"Training-"+currentResolution+"-depth-occlusion.png"),image.EncodeToPNG());
            }
            finally
            {RenderTexture.active=old;camera.targetTexture=null;target.Release();Destroy(target);if(image!=null)Destroy(image);Destroy(root);Destroy(opaque);Destroy(material);}
        }

        IEnumerator ValidateRouteRecorder()
        {
            game.NewSession(true);var guide=game.RouteGuide;
            guide.ConfigureValidationStorage(Path.Combine(outputDirectory,"TrainingGuide","Player-Library-"+currentResolution));
            guide.RecordingName="Native UI validation — not an operator route";
            yield return Click("RouteRecorder");
            Check(guide.PanelOpen,"Persistent Route Recorder button opens the authoring panel");
            yield return Capture("Route-recorder-"+currentResolution+"-ready.png","route recorder accessible controls and persistence instructions");
            yield return ClickAt(RouteRecordingPanel.ControlRect("Start").center);
            Check(guide.Recorder.Recording,"Native Start New button starts a take");
            yield return Capture("Route-recorder-"+currentResolution+"-recording.png","active recording and first observed pose");
            var previous=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>();
            var update=typeof(IceResurfacerGame).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            int capture=Time.captureFramerate;Time.captureFramerate=60;yield return null;
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Digit2,Key.F));InputSystem.Update();update.Invoke(game,null);
                Check(game.Sim.Controls.Throttle==0&&!game.Sim.Controls.HorizontalAuger&&!game.Sim.OnFoot,"Typing/keys in the route panel do not drive, toggle machinery or dismount");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                yield return ClickAt(RouteRecordingPanel.ControlRect("Drive").center);
                Check(!guide.PanelOpen&&guide.Recorder.Recording,"Close and Drive retains the active recording");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S));InputSystem.Update();
                for(int i=0;i<180;i++)update.Invoke(game,null);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftCtrl));InputSystem.Update();
                for(int i=0;i<90;i++)update.Invoke(game,null);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                Check(guide.Recorder.Data.distanceMeters>1&&guide.Recorder.Data.samples.Count>10,"Normal S/Ctrl game input and fixed simulation observer capture a real driven reverse segment");
            }
            finally
            {Time.captureFramerate=capture;InputSystem.RemoveDevice(keyboard);if(previous!=null&&previous.added)previous.MakeCurrent();}
            yield return Click("RouteRecorder");yield return ClickAt(RouteRecordingPanel.ControlRect("Stop").center);
            Check(!guide.Recorder.Recording&&guide.Recorder.Data.complete,"Native Stop button finalizes raw observations");
            yield return ClickAt(RouteRecordingPanel.ControlRect("Save").center);
            Check(File.Exists(guide.LastSavedPath),"Native Save Named Copy persists JSON in the configured standalone library");
            yield return Capture("Route-recorder-"+currentResolution+"-driven-segment.png","actual input-driven segment saved from standalone UI");
            string fixture=Path.Combine(outputDirectory,"RouteRecording","Deterministic-fixture.route.json");
            var data=RouteRecordingStorage.Read(fixture);
            string copy=Path.Combine(guide.Library.DirectoryPath,"Deterministic-fixture.route.json");
            RouteRecordingStorage.Export(copy,data,File.Exists(copy));
            yield return ClickAt(RouteRecordingPanel.ControlRect("Refresh").center);
            yield return ClickAt(new Vector2(1110,461));
            Check(guide.Recorder.Data.provenance.Contains("NOT the operator")&&guide.Recorder.Data.samples.Count==data.samples.Count,"Native saved-route list loads the clearly labeled deterministic fixture");
            string bytes=File.ReadAllText(copy);
            yield return Capture("Route-recorder-"+currentResolution+"-full-preview.png","full raw fixture preview with start/end and chronological pass colors");
            Check(guide.RenderedRecordingSamples==data.samples.Count,"First loaded-route repaint renders every raw sample instead of the preceding take");
            yield return ClickAt(new Vector2(705,810));
            yield return Capture("Route-recorder-"+currentResolution+"-point-inspection.png","raw sequence slider and vehicle/conditioner pose inspection");
            Check(File.ReadAllText(copy)==bytes,"Preview and point inspection preserve the original raw file");
            yield return ClickAt(RouteRecordingPanel.ControlRect("Close").center);
            Check(!guide.PanelOpen,"Recorder closes through native UI after loading a saved route");
        }

        IEnumerator CaptureQuicklooks()
        {
            // This player belongs solely to the explicit validation process.
            // Virtual device events never send OS keyboard input to the user's Editor.
            var previous=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>();
            var look=typeof(IceResurfacerGame).GetMethod("UpdateLook",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            try
            {
                foreach(var key in new[]{Key.Q,Key.E,Key.C,Key.LeftShift})
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));InputSystem.Update();
                    Check(keyboard[key].isPressed&&Keyboard.current==keyboard,"Validation virtual keyboard receives "+key+" while player is non-focused");
                    for(int i=0;i<60;i++)look.Invoke(game,new object[]{true,.02f});
                    Check(game.HeldQuicklook!="","Held "+key+" enters temporary inspection");
                    Vector2 side=IceDashboard.CanvasToViewport(new Vector2(1400,650),Screen.width,Screen.height);
                    Check(!game.Dashboard.PointerOverInterface(game,new Vector2(side.x,Screen.height-side.y)),"Inspection "+key+" clears side-console pointer obstruction");
                    yield return Capture("HUD-"+currentResolution+"-look-"+key+".png","quicklook with persistent conditioner state and clear machinery view");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                    for(int i=0;i<30;i++)look.Invoke(game,new object[]{true,.02f});
                    Check(game.HeldQuicklook=="","Release "+key+" restores the operating view");
                }
            }
            finally {InputSystem.RemoveDevice(keyboard);if(previous!=null&&previous.added)previous.MakeCurrent();}
        }

        IEnumerator Click(string key) { yield return ClickAt(IceDashboard.ControlRect(key).center); }

        IEnumerator HoldHydraulic(string key,bool raise)
        {
            Rect rect=IceDashboard.ControlRect(key);
            Vector2 point=new Vector2(rect.center.x,raise?rect.y+12:rect.yMax-12);
            var s=game.Sim;var c=s.Controls;
            yield return Send(EventType.MouseDown,point);
            float demand=key=="Conditioner"?c.ConditionerHydraulic:c.TankHydraulic;
            Check(raise?demand>.99f:demand<-.99f,key+" held direction");
            for(int i=0;i<25;i++)s.Step(.02f);
            float attained=key=="Conditioner"?s.Conditioner.Lift:s.TankLift;
            yield return Send(EventType.MouseUp,point);
            demand=key=="Conditioner"?c.ConditionerHydraulic:c.TankHydraulic;
            Check(demand==0,key+" springs to neutral on native mouse release");
            for(int i=0;i<25;i++)s.Step(.02f);
            Check(Mathf.Abs(attained-(key=="Conditioner"?s.Conditioner.Lift:s.TankLift))<.0001f,key+" holds attained position after release");
        }
        IEnumerator RotateWater(string key,float fraction)
        {
            Rect rect=IceDashboard.ControlRect(key);float r=rect.width*.35f;
            Vector2 begin=rect.center+Vector2.left*r;
            float angle=(180-fraction*90)*Mathf.Deg2Rad;
            Vector2 end=rect.center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r;
            yield return Send(EventType.MouseDown,begin);
            yield return Send(EventType.MouseDrag,end);
            yield return Send(EventType.MouseUp,end);
        }

        IEnumerator ValidateBladeTurns()
        {
            Rect rect=IceDashboard.ControlRect("Blade");float radius=rect.width*.35f;
            yield return Send(EventType.MouseDown,rect.center+Vector2.right*radius);
            for(int quarter=1;quarter<=15;quarter++)
            {
                float angle=quarter*Mathf.PI*.5f;
                yield return Send(EventType.MouseDrag,rect.center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius);
                if(quarter==12)Check(game.Sim.Controls.BladeDepthMm>.67f&&game.Sim.Controls.BladeDepthMm<.70f,"three full turns retain remaining blade adjustment");
                if(quarter==14)Check(Mathf.Abs(game.Sim.Controls.BladeDepthMm-.8f)<.005f,"3.5 full turns reach deepest blade stop");
                if(quarter==15)Check(Mathf.Abs(game.Sim.Controls.BladeDepthMm-.8f)<.005f,"blade mechanical end stop rejects extra rotation");
            }
            Vector2 reversed=rect.center+Vector2.left*radius;
            yield return Send(EventType.MouseDrag,reversed);
            Check(Mathf.Abs(game.Sim.Controls.BladeDepthMm-(.8f-.8f/14))<.01f,"blade reverses continuously from end stop");
            yield return Send(EventType.MouseUp,reversed);
        }

        IEnumerator ClickAt(Vector2 canvasPoint)
        {
            yield return Send(EventType.MouseDown, canvasPoint);
            yield return Send(EventType.MouseUp, canvasPoint);
            yield return null;
        }

        IEnumerator DragLever(string key, float value)
        {
            Rect rect = IceDashboard.ControlRect(key);
            float y = rect.y + 9 + (1 - value) * (rect.height - 18);
            Vector2 begin = new Vector2(rect.x + 20, rect.yMax - 14);
            Vector2 end = new Vector2(rect.x + 20, y);
            yield return Send(EventType.MouseDown, begin);
            yield return Send(EventType.MouseDrag, end);
            yield return Send(EventType.MouseUp, end);
        }

        IEnumerator RotateDial(string key)
        {
            Rect rect = IceDashboard.ControlRect(key);
            yield return Send(EventType.MouseDown, rect.center + Vector2.right * rect.width * .35f);
            yield return Send(EventType.MouseDrag, rect.center + Vector2.up * rect.height * .35f);
            yield return Send(EventType.MouseUp, rect.center + Vector2.up * rect.height * .35f);
        }

        IEnumerator Scroll(string key, float amount)
        { yield return Send(EventType.ScrollWheel, IceDashboard.ControlRect(key).center, new Vector2(0, amount)); }

        IEnumerator Send(EventType type, Vector2 canvasPoint, Vector2 delta = default)
        {
            Vector2 clientPoint = IceDashboard.CanvasToViewport(canvasPoint, Screen.width, Screen.height);
            int target = ++sentEvents;
            expectedInput = type;
            PostMouse(type, clientPoint, delta);
            float deadline = Time.realtimeSinceStartup + 5;
            while (consumedEvents < target && !complete && Time.realtimeSinceStartup < deadline) yield return null;
            if (consumedEvents < target) throw new TimeoutException("Native player IMGUI did not consume " + type);
            // A clean normal repaint follows native window input before the next assertion.
            yield return null;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        delegate bool WindowCallback(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
        [DllImport("user32.dll", SetLastError = true)] static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
        [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; }
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
#endif

        static IntPtr FindOwnPlayerWindow()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            IntPtr found = IntPtr.Zero;
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                EnumWindows((window, parameter) =>
                {
                    GetWindowThreadProcessId(window, out uint owner);
                    if (owner != process.Id) return true;
                    var name = new StringBuilder(256); GetClassName(window, name, 256);
                    if (name.ToString() != "UnityWndClass") return true;
                    found = window; return false;
                }, IntPtr.Zero);
            }
            return found;
#else
            return IntPtr.Zero;
#endif
        }

        void PostMouse(EventType type, Vector2 point, Vector2 delta)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // Only this opt-in development player's window receives messages. This
            // does not move the OS pointer, activate a window or target another process.
            GetWindowThreadProcessId(playerWindow, out uint owner);
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
                if (owner != process.Id) throw new InvalidOperationException("Validation input window ownership changed.");
            int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y);
            uint message; int buttons = 0;
            if (type == EventType.MouseDown) { message = 0x0201; buttons = 1; }
            else if (type == EventType.MouseUp) message = 0x0202;
            else if (type == EventType.MouseDrag) { message = 0x0200; buttons = 1; }
            else if (type == EventType.ScrollWheel)
            {
                message = 0x020A; buttons = Mathf.RoundToInt(-delta.y * 40) << 16;
                var screenPoint = new NativePoint { X = x, Y = y };
                if (!ClientToScreen(playerWindow, ref screenPoint)) throw new InvalidOperationException("Cannot map validation wheel input.");
                x = screenPoint.X; y = screenPoint.Y;
            }
            else throw new ArgumentOutOfRangeException(nameof(type));
            IntPtr position = new IntPtr((x & 0xFFFF) | ((y & 0xFFFF) << 16));
            if (!PostMessage(playerWindow, message, new IntPtr(buttons), position))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Cannot post input to the validation player's own window.");
            inputTrace.Add("POST " + type + " client=" + point);
#else
            throw new PlatformNotSupportedException("Native window input probe requires the explicit Windows development player.");
#endif
        }

        IEnumerator Capture(string name, string context)
        {
            // Suppress only the development-build overlay; errors remain in the report.
            Debug.developerConsoleVisible = false;
            yield return null;
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            if (texture == null) throw new InvalidOperationException("No rendered framebuffer for " + name);
            Directory.CreateDirectory(ArtifactDirectory);File.WriteAllBytes(Path.Combine(ArtifactDirectory, name), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            foreach (string issue in game.Dashboard.LayoutIssues)
            {
                string entry = currentResolution + " / " + context + ": " + issue;
                if (!layoutIssues.Contains(entry)) layoutIssues.Add(entry);
            }
            File.WriteAllText(Path.Combine(ArtifactDirectory, "HUD-layout-issues-" + currentResolution + ".txt"),
                layoutIssues.Count == 0 ? "PASS: no measured text clipping in captured HUD states.\n" : string.Join("\n", layoutIssues) + "\n");
        }

        void OnGUI()
        {
            if (!active || complete || game == null || game.Dashboard == null) return;
            try
            {
                EventType nativeType = Event.current.type;
                if ((Event.current.isMouse || nativeType == EventType.ScrollWheel) && inputTrace.Count < 160)
                    inputTrace.Add("IMGUI " + nativeType + " position=" + Event.current.mousePosition);
                game.Dashboard.Draw(game, Screen.width, Screen.height);
                if (sentEvents > consumedEvents && nativeType == expectedInput) consumedEvents = sentEvents;
            }
            catch (Exception e) { failures.Add(e.ToString()); Finish(); }
        }

        void Update()
        {
            // The explicit HUD probe disables Game.Update, so sample its real
            // player frame cadence here instead of freezing the startup sample.
            if(active&&!complete&&game!=null&&game.Performance!=null)
            {game.Performance.BeginFrame(Time.unscaledDeltaTime);game.Performance.EndWork();}
            if (active && !complete && Time.realtimeSinceStartup - startedAt > 120)
            { failures.Add("Development-player probe exceeded its 120-second timeout."); Finish(); }
        }

        void Check(bool passed, string message)
        {
            string entry = currentResolution + " / " + message;
            if (passed) passes.Add("PASS " + entry);
            else failures.Add("FAIL " + entry);
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (!complete && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                failures.Add("Runtime " + type + ": " + message + "\n" + stack);
        }

        void Finish()
        {
            if (complete) return;
            complete = true;
            if(originalInputSettings!=null)InputSystem.settings=originalInputSettings;
            if(validationInputSettings!=null){UnityEngine.Object.Destroy(validationInputSettings);validationInputSettings=null;}
            Application.logMessageReceived -= OnLog;
            if (layoutIssues.Count > 0) failures.Add("Measured HUD clipping: " + layoutIssues.Count + " entries; see HUD-layout-issues-" + currentResolution + ".txt.");
            var report = new StringBuilder();
            report.AppendLine("Aurora native development-player HUD validation / Unity " + Application.unityVersion);
            report.AppendLine("Actual player framebuffer captures include the world and live IMGUI instruments.");
            report.AppendLine("The opt-in probe disables the Development Console overlay; native presentation diagnostics remain in the player log and are counted by the launcher.");
            report.AppendLine("Native mouse messages target only this player's own window; hydraulic checks advance fixed simulation ticks while held and after release.");
            report.AppendLine("Handwheel and valve dragging are tested. Native wheel messages use the OS pointer, so optional scroll shortcuts are not automated here.");
            report.AppendLine("The results capture follows an actual raised, dry entry and safe return through the normal finish path.");
            foreach (string pass in passes) report.AppendLine(pass);
            foreach (string failure in failures) report.AppendLine(failure);
            if (failures.Count > 0) foreach (string input in inputTrace) report.AppendLine(input);
            report.AppendLine(failures.Count == 0 ? "PASS: all player HUD checks." : "FAIL: " + failures.Count + " check(s).");
            File.WriteAllText(Path.Combine(outputDirectory, "HUD-player-results-" + currentResolution + ".txt"), report.ToString());
            Debug.Log(report.ToString());
            Application.Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
#endif
