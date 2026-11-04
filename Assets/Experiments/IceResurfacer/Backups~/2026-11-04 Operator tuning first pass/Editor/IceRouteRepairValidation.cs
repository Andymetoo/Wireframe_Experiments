using System;using System.Collections.Generic;using System.IO;using System.Reflection;using System.Diagnostics;
using IceResurfacer.Core;using IceResurfacer.Presentation;using UnityEditor;using UnityEngine;using UnityEngine.InputSystem;
using Debug=UnityEngine.Debug;
namespace IceResurfacer.Editor
{
    public static class IceRouteRepairValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/Validation~/RouteRepair/";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static readonly List<string> results=new List<string>();
        static void Check(bool value,string label){if(!value)throw new InvalidOperationException("Route repair: "+label);results.Add("PASS "+label);File.WriteAllLines(Root+"Progress.txt",results);}
        static void Isolated(){if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))throw new InvalidOperationException("Use the isolated validation project.");Directory.CreateDirectory(Root);}
        public static void RunGuiBatch()
        {
            UnityEngine.Object host=null,container=null;TrainingRouteEditorWindow window=null;
            try
            {
                Isolated();results.Clear();window=ScriptableObject.CreateInstance<TrainingRouteEditorWindow>();window.position=new Rect(0,0,1400,1000);
                var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.HostView",true);host=ScriptableObject.CreateInstance(type);
                var containerType=typeof(EditorWindow).Assembly.GetType("UnityEditor.ContainerWindow",true);container=ScriptableObject.CreateInstance(containerType);
                containerType.GetProperty("rootView",Flags).SetValue(container,host);
                type.GetProperty("position",Flags).SetValue(host,new Rect(0,0,1400,1000));
                type.GetProperty("actualView",Flags).SetValue(host,window);
                string source=Root+"Inputs/Andy_Try_1.route.json",bytes=File.ReadAllText(source);var raw=RouteRecordingStorage.Decode(bytes);window.Create(raw);
                void Draw(){window.SendEvent(new Event{type=EventType.Layout});window.SendEvent(new Event{type=EventType.Repaint});}
                // A non-displayed auxiliary window has no native focus owner.
                // Dispatch input inside its real OnGUI context, bypassing only
                // native AuxWindowManager focus; production handlers are identical.
                void Send(Event value){window.QueueValidationEvent(value);window.SendEvent(new Event{type=EventType.Repaint});}
                Draw();
                if(window.GuiDrawCount==0)
                {
                    var methods=new List<string>();foreach(var target in new[]{type,type.BaseType,typeof(GUIUtility),containerType})foreach(var m in target.GetMethods(Flags|BindingFlags.Static))methods.Add(target.Name+" "+m);
                    File.WriteAllLines(Root+"GUI-host-methods.txt",methods);
                }
                Check(window.GuiDrawCount>0,"real EditorWindow OnGUI/Layout/Repaint runs in a hidden HostView without opening a desktop window");
                Check(window.VisibleMarkerCount<window.Document.controls.Count*.8f,"overview thins the 398 control markers without covering the route");
                var plotField=typeof(TrainingRouteEditorWindow).GetField("plot",Flags);Rect plot=(Rect)plotField.GetValue(window);var cursor=plot.center+new Vector2(43,-19);
                var world=window.Viewport.FromGui(plot,cursor);Send(new Event{type=EventType.ScrollWheel,mousePosition=cursor,delta=new Vector2(0,-6)});Draw();
                results.Add("INFO wheel zoom="+window.Viewport.Zoom+"; anchor error="+Vector2.Distance(world,window.Viewport.FromGui(plot,cursor))+"; plot="+plot);
                Check(window.Viewport.Zoom>2&&Vector2.Distance(world,window.Viewport.FromGui(plot,cursor))<.00001f,"actual mouse wheel zooms around the cursor with matching world coordinates");
                var panPoint=window.Viewport.ToGui(plot,world.x,world.y);
                Send(new Event{type=EventType.MouseDown,button=2,mousePosition=plot.center});
                Send(new Event{type=EventType.MouseDrag,button=2,mousePosition=plot.center+new Vector2(30,-20),delta=new Vector2(30,-20)});
                Send(new Event{type=EventType.MouseUp,button=2,mousePosition=plot.center+new Vector2(30,-20)});Draw();
                Check(Vector2.Distance(window.Viewport.ToGui(plot,world.x,world.y)-panPoint,new Vector2(30,-20))<.001f,"actual middle-button dragging pans every preview layer by the requested delta");
                var selection=typeof(TrainingRouteEditorWindow).GetField("selected",Flags);
                foreach(int index in new[]{-1,int.MaxValue,398,397,0}){selection.SetValue(window,index);Draw();Check(window.Selected>=0&&window.Selected<window.Document.controls.Count,"DrawControls clamps stale selection "+index+" before indexing");}
                window.MovePoint(30,window.Document.controls[30].x+.01f,window.Document.controls[30].z);window.InsertPoint();Draw();window.RemovePoint();Draw();window.UndoEdit();Draw();window.UndoEdit(true);Draw();
                Check(window.GuiDrawCount>12,"insertion, deletion, undo and redo repaint without selection exceptions");
                window.RestoreRecordedTrack();selection.SetValue(window,30);window.ZoomSelected();Draw();plot=(Rect)plotField.GetValue(window);
                var control=window.Document.controls[30];var start=new Vector2(control.x,control.z);var node=window.Viewport.ToGui(plot,start.x,start.y);
                Send(new Event{type=EventType.MouseDown,button=0,mousePosition=node});Send(new Event{type=EventType.MouseUp,button=0,mousePosition=node});Draw();
                Check(control.recordedTrack,"selecting a node without dragging never converts or distorts the recorded track");
                Send(new Event{type=EventType.MouseDown,button=0,mousePosition=node});Send(new Event{type=EventType.MouseDrag,button=0,mousePosition=node+new Vector2(4,3),delta=new Vector2(4,3)});Send(new Event{type=EventType.MouseUp,button=0,mousePosition=node+new Vector2(4,3)});Draw();
                Check(!control.recordedTrack&&Vector2.Distance(new Vector2(control.x,control.z),start)>.001f,"zoomed node dragging changes only adjacent derived sections using the shared viewport transform");
                window.UndoEdit();Draw();control=window.Document.controls[30];node=window.Viewport.ToGui(plot,control.x,control.z);start=new Vector2(control.x,control.z);
                Send(new Event{type=EventType.MouseDown,button=0,mousePosition=node});Send(new Event{type=EventType.MouseDrag,button=0,mousePosition=plot.position-new Vector2(10,10),delta=new Vector2(-500,-500)});Send(new Event{type=EventType.MouseUp,button=0,mousePosition=plot.position-new Vector2(10,10)});Draw();
                Check(new Vector2(control.x,control.z)==start&&control.recordedTrack,"dragging outside the active viewport does not edit the route");
                int direction=control.direction;var handle=window.Viewport.ToGui(plot,control.x+Mathf.Sin(control.headingRadians)*control.outgoing*direction,control.z+Mathf.Cos(control.headingRadians)*control.outgoing*direction);float previous=control.outgoing;
                Send(new Event{type=EventType.MouseDown,button=0,mousePosition=handle});Send(new Event{type=EventType.MouseDrag,button=0,mousePosition=handle+new Vector2(3,2),delta=new Vector2(3,2)});Send(new Event{type=EventType.MouseUp,button=0,mousePosition=handle+new Vector2(3,2)});Draw();
                Check(!control.recordedTrack&&Math.Abs(control.outgoing-previous)>.00001f,"Bezier handle hitboxes and fine dragging remain accurate while zoomed");window.UndoEdit();Draw();
                window.Create(RouteRecordingStorage.Read(Root+"Inputs/Short.route.json"));selection.SetValue(window,397);Draw();Check(window.Selected==window.Document.controls.Count-1,"loading a shorter route cannot retain an out-of-range selection from the 398-point route");
                var controls=window.Document.controls;window.Document.controls=new List<RouteControlPoint>();selection.SetValue(window,397);Draw();Check(window.Selected==-1,"empty control collection displays an empty state without indexing");
                window.Document.controls=controls;window.RestoreRecordedTrack();Draw();
                window.LoadGuide(Root+"Inputs/Operator-saved-draft.guide.json");window.FitWholeRoute();Draw();window.NavigateProblem(-1);Draw();
                Check(window.SelectedProblem==1,"Previous Error starts at the final actual invalid region");
                window.FitWholeRoute();Draw();plot=(Rect)plotField.GetValue(window);var failure=window.Fit.problems[0].fitted;var errorPoint=window.Viewport.ToGui(plot,failure.x,failure.z);
                Send(new Event{type=EventType.MouseDown,button=0,mousePosition=errorPoint});Send(new Event{type=EventType.MouseUp,button=0,mousePosition=errorPoint});Draw();
                Check(window.SelectedProblem==0&&window.Selected==61&&window.Viewport.Zoom>1,"clicking a curvature error selects its responsible section, diagnostics and useful zoom");
                window.RestoreRecordedTrack();Draw();window.FitWholeRoute();Draw();Check(window.Viewport.Zoom==1,"Fit Whole Route reliably restores the overview");
                Check(File.ReadAllText(source)==bytes,"GUI regression leaves the original raw file unchanged");
                File.WriteAllLines(Root+"Editor-GUI-results.txt",results);EditorApplication.Exit(0);
            }
            catch(Exception e){results.Add("FAIL "+e);File.WriteAllLines(Root+"Editor-GUI-results.txt",results);Debug.LogException(e);EditorApplication.Exit(1);}
            finally{if(container!=null)UnityEngine.Object.DestroyImmediate(container);else if(host!=null)UnityEngine.Object.DestroyImmediate(host);if(window!=null)UnityEngine.Object.DestroyImmediate(window);}
        }
        const string Gate="Aurora.RouteRepair.Play";
        public static void VerifyPersisted()
        {
            try
            {
                Isolated();results.Clear();var d=TrainingGuideStorage.Read(Root+"Inputs/Corrected_Andy_Try_1.guide.json");var raw=TrainingGuideStorage.Raw(d);var fit=RouteFitter.Fit(d,raw);
                Check(!EditorApplication.isPlaying&&d.version==2&&raw.name=="Andy_Try_1","a fresh Unity process reloads the actual corrected version-2 guide after Play ended");
                Check(d.rawRecordingJson==File.ReadAllText(Root+"Inputs/Andy_Try_1.route.json"),"fresh-process corrected guide retains exact original raw source");
                Check(fit.Ready&&fit.boundaryErrors==0&&fit.curvatureErrors==0&&Math.Abs(fit.Length-raw.distanceMeters)<.000001,"fresh-process reconstruction preserves the complete validated 1003.234 m route");
                File.WriteAllLines(Root+"Fresh-guide-persistence.txt",results);EditorApplication.Exit(0);
            }
            catch(Exception e){results.Add("FAIL "+e);File.WriteAllLines(Root+"Fresh-guide-persistence.txt",results);Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void RunBatch(){Isolated();SessionState.SetBool(Gate,true);UnityEditor.SceneManagement.EditorSceneManager.OpenScene(IceResurfacerEditor.ScenePath);Resume();EditorApplication.isPlaying=true;}
        [InitializeOnLoadMethod] static void Resume(){if(!SessionState.GetBool(Gate,false))return;EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;if(EditorApplication.isPlaying)State(PlayModeStateChange.EnteredPlayMode);}
        static int frames;
        static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode){frames=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}}
        static void Tick()
        {
            if(++frames<30)return;EditorApplication.update-=Tick;
            try{Run(UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>());SessionState.SetBool(Gate,false);EditorApplication.Exit(0);}
            catch(Exception e){SessionState.SetBool(Gate,false);results.Add("FAIL "+e);File.WriteAllLines(Root+"Unity-real-route-results.txt",results);Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Run(IceResurfacerGame game)
        {
            results.Clear();game.enabled=false;game.NewSession(true);var window=ScriptableObject.CreateInstance<TrainingRouteEditorWindow>();
            try
            {
                string path=Root+"Inputs/Andy_Try_1.route.json",bytes=File.ReadAllText(path);var raw=RouteRecordingStorage.Decode(bytes);
                window.LoadGuide(Root+"Inputs/Operator-saved-draft.guide.json");
                Check(window.Fit.samples.Count==33701&&window.Fit.curvatureErrors==16&&window.Fit.boundaryErrors==63,"actual saved draft reproduces all reported 16 curvature and 63 boundary errors");
                Check(window.Fit.problems.Count==2,"79 failing samples are grouped into two distinct problem regions");
                window.FitWholeRoute();File.WriteAllBytes(Root+"Before-overview.png",window.PreviewPng());
                window.NavigateProblem(1);Check(window.SelectedProblem==0&&window.Selected==61,"Next Error selects the first actual curvature region and its neighboring control");
                File.WriteAllBytes(Root+"Before-curvature-zoom.png",window.PreviewPng());window.NavigateProblem(1);File.WriteAllBytes(Root+"Before-boundary-zoom.png",window.PreviewPng());
                foreach(var issue in window.Fit.problems)Check(issue.rawVehicleClear&&issue.rawConditionerClear,"raw observation passes production clearance at problem region "+issue.start.ToString("F3")+" m");
                window.RestoreRecordedTrack();Check(window.Document.version==2,"recorded reconstruction uses explicit derived-guide version 2; raw schema stays version 1");
                window.UndoEdit();Check(window.Document.version==1&&window.Fit.curvatureErrors==16&&window.Fit.boundaryErrors==63,"undo restores the original legacy guide and exact diagnostic counts");
                window.UndoEdit(true);var fit=window.Fit;
                Check(fit.Ready&&fit.boundaryErrors==0&&fit.curvatureErrors==0&&fit.recordedRadiusWarnings==0,"restored actual recording passes unchanged collision and radius checks");
                Check(!fit.IdealSplineCertified&&fit.recordedSamples==fit.samples.Count,"recorded-track guidance is clearly distinguished from ideal smooth-spline certification");
                float conditionerDeviation=0,vehicleDeviation=0;
                foreach(var p in fit.samples)
                {
                    var a=raw.samples[p.sourceSequence];var b=raw.samples[Math.Min(raw.samples.Count-1,p.sourceSequence+1)];float t=p.sourceFraction;
                    vehicleDeviation=Mathf.Max(vehicleDeviation,Vector2.Distance(new Vector2(p.x,p.z),new Vector2(a.vehicleX+(b.vehicleX-a.vehicleX)*t,a.vehicleZ+(b.vehicleZ-a.vehicleZ)*t)));
                    conditionerDeviation=Mathf.Max(conditionerDeviation,Vector2.Distance(new Vector2(p.conditionerX,p.conditionerZ),new Vector2(a.conditionerX+(b.conditionerX-a.conditionerX)*t,a.conditionerZ+(b.conditionerZ-a.conditionerZ)*t)));
                }
                Check(vehicleDeviation<.000001f&&conditionerDeviation<.000001f,"all 46,616 guide vehicle and conditioner positions agree with their source intervals within 0.001 mm");
                Check(Math.Abs(fit.Length-raw.distanceMeters)<.000001&&fit.maximumRawDeviation==0,"full 1003.234 m source distance and zero raw-polyline displacement are preserved");
                File.WriteAllBytes(Root+"After-south-board-zoom.png",window.PreviewPng());window.FitWholeRoute();File.WriteAllBytes(Root+"After-overview.png",window.PreviewPng());
                window.MovePoint(397,window.Document.controls[397].x,window.Document.controls[397].z);window.UndoEdit();typeof(TrainingRouteEditorWindow).GetField("selected",Flags).SetValue(window,397);window.ZoomSelected();
                File.WriteAllBytes(Root+"After-service-entrance-zoom.png",window.PreviewPng());
                window.Viewport.ZoomTo(game.Sim.Layout.GateX,game.Sim.Layout.GateZ,7);window.Rebuild();File.WriteAllBytes(Root+"After-service-gate-and-room.png",window.PreviewPng());
                window.Viewport.ShowGuidePath=window.Viewport.ShowRawPath=false;window.Rebuild();File.WriteAllBytes(Root+"After-actual-conditioner-path.png",window.PreviewPng());window.Viewport.ShowGuidePath=window.Viewport.ShowRawPath=true;
                Rect rect=new Rect(0,0,900,760);var cursor=new Vector2(347,419);var before=window.Viewport.FromGui(rect,cursor);
                window.Viewport.ZoomAt(rect,cursor,-3);var after=window.Viewport.FromGui(rect,cursor);Check(Vector2.Distance(before,after)<.00001f,"cursor-centered zoom preserves the world point under the pointer");
                var pixel=window.Viewport.ToGui(rect,before.x,before.y);window.Viewport.Pan(rect,new Vector2(38,-24));var moved=window.Viewport.ToGui(rect,before.x,before.y);
                Check(Vector2.Distance(moved-pixel,new Vector2(38,-24))<.0001f,"pan moves every layer by the same exact screen-space delta");
                for(int i=0;i<100;i++){float x=-15+i*.3f,z=-30+i*.6f;var gui=window.Viewport.ToGui(rect,x,z);Check(Vector2.Distance(window.Viewport.FromGui(rect,gui),new Vector2(x,z))<.00001f,"viewport roundtrip "+i);}
                window.Rebuild();File.WriteAllBytes(Root+"After-zoom-and-pan.png",window.PreviewPng());window.FitWholeRoute();
                window.Document.name="Andy_Try_1 / recorded track (repaired)";
                string saved=window.SaveCopy(Root+"Library");var loaded=TrainingGuideStorage.Read(saved);Check(TrainingGuideStorage.Raw(loaded).id==raw.id&&loaded.rawRecordingJson==bytes,"corrected guide persists separately with the unchanged original recording embedded");
                game.RouteGuide.ConfigureValidationStorage(Root+"RuntimeLibrary");Directory.CreateDirectory(game.RouteGuide.GuideDirectory);string runtime=Path.Combine(game.RouteGuide.GuideDirectory,Path.GetFileName(saved));File.Copy(saved,runtime,true);
                Check(game.RouteGuide.UseSavedRouteAsGps(runtime),"Route Manager GPS path accepts the separately saved corrected guide");if(!game.RouteGuide.Visible)game.RouteGuide.Toggle();
                var progress=new RouteProgress(fit);double maxAhead=0;int maxSearch=0;var clock=Stopwatch.StartNew();
                foreach(var p in raw.samples){progress.Observe(p.vehicleX,p.vehicleZ,p.vehicleHeadingRadians,p.speedMetersPerSecond);maxAhead=Math.Max(maxAhead,progress.Distance-p.distanceMeters);maxSearch=Math.Max(maxSearch,progress.LastSearchSamples);if(progress.Distance>p.distanceMeters+.25)throw new Exception("Skipped ahead to a later pass at source "+p.sequence);}
                clock.Stop();Check(Math.Abs(progress.Distance-fit.Length)<.000001,"all raw observations traverse the full sequential GPS including closely overlapping loops and entrance/exit");
                Check(game.RouteGuide.LookEnd-game.RouteGuide.Progress.Distance<=12.192001,"live preview remains bounded to 40 feet");
                var unsafeGuide=TrainingGuideStorage.Read(saved);unsafeGuide.controls[30].x=100;
                Check(!game.RouteGuide.SelectTrainingGuide(unsafeGuide),"unsafe edited recorded-track anchor cannot bypass physical validation");
                var invalidRaw=RouteRecordingStorage.Decode(bytes);invalidRaw.id=Guid.NewGuid().ToString("N");invalidRaw.name="SYNTHETIC INVALID TRANSLATION";invalidRaw.provenance="Deterministic negative fixture; NOT an operator recording.";
                for(int i=0;i<invalidRaw.samples.Count;i++){var p=invalidRaw.samples[i];p.vehicleX+=40;p.conditionerX+=40;invalidRaw.samples[i]=p;}
                invalidRaw.Validate();var prior=game.RouteGuide.SelectedGuide;
                Check(!game.RouteGuide.SelectTrainingGuide(TrainingGuideStorage.Create(invalidRaw))&&game.RouteGuide.SelectedGuide==prior,"synthetic raw trajectory outside the physical floor remains blocked; raw fallback cannot certify unsafe geometry");
                Check(File.ReadAllText(path)==bytes,"all tests leave Andy_Try_1 byte-for-byte unchanged");
                results.Add("Traversal: "+fit.Length.ToString("F9")+" m; "+fit.samples.Count+" guide samples; "+fit.legs.Count+" contexts; maximum source-distance lead="+maxAhead.ToString("F6")+" m; max local search="+maxSearch+"; replay="+clock.Elapsed.TotalMilliseconds.ToString("F3")+" ms.");
                File.WriteAllText(Root+"Corrected-guide-path.txt",Path.GetFullPath(saved));File.WriteAllLines(Root+"Unity-real-route-results.txt",results);
            }
            finally{if(game.RouteGuide.Visible)game.RouteGuide.Toggle();game.RouteGuide.UseOriginalReference();UnityEngine.Object.DestroyImmediate(window);}
        }
    }
}
