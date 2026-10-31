using System;using System.Collections.Generic;using System.IO;using System.Reflection;
using IceResurfacer.Core;using IceResurfacer.Presentation;using UnityEditor;using UnityEngine;
using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;
namespace IceResurfacer.Editor
{
    public static class IceRouteUXValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/Validation~/RouteUX/";
        static readonly List<string> results=new List<string>();
        static void Check(bool value,string text){if(!value)throw new Exception("Route UX: "+text);results.Add("PASS "+text);}
        static void Input(IceRouteGuide guide,Keyboard keyboard,Mouse mouse,params Key[] keys)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();typeof(IceRouteGuide).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(guide,null);}
        internal static void Run(IceResurfacerGame game,Keyboard keyboard,Mouse mouse)
        {
            results.Clear();Directory.CreateDirectory(Root);var g=game.RouteGuide;bool enabled=game.enabled;game.enabled=false;
            try
            {
                if(g.PanelOpen)g.ToggleRecordingPanel();if(g.Visible)g.Toggle();g.ConfigureValidationStorage(Path.GetFullPath(Root+"Library"));
                var paths=new List<string>();
                for(int take=0;take<2;take++)
                {
                    game.NewSession(true);g.Refresh();var s=game.Sim;Pose(s,-6+take,-18,0);g.RecordingName=take==0?"Practice west loop":"Practice east loop";
                    Input(g,keyboard,mouse);Input(g,keyboard,mouse,Key.Numpad9);Input(g,keyboard,mouse);
                    Check(g.Recorder.Recording,"Numpad 9 starts named take "+(take+1));
                    s.Controls.Throttle=.42f;for(int i=0;i<300;i++)Tick(s,g);
                    float turned=0,previous=s.Vehicle.Heading;s.Controls.Steer=.9f;
                    for(int i=0;i<2000&&turned<Math.PI-.04;i++){Tick(s,g);turned+=Math.Abs(TrainingRouteDefinition.Angle(s.Vehicle.Heading-previous));previous=s.Vehicle.Heading;}
                    s.Controls.Steer=0;for(int i=0;i<300;i++)Tick(s,g);
                    s.Controls.Throttle=0;s.Controls.Brake=true;for(int i=0;i<180;i++)Tick(s,g);
                    Input(g,keyboard,mouse,Key.NumLock,Key.Numpad9);Input(g,keyboard,mouse);
                    Check(!g.Recorder.Recording&&File.Exists(g.LastSavedPath),"Numpad 9 stops and saves a unique complete file with Num Lock state present");paths.Add(g.LastSavedPath);
                    var data=RouteRecordingStorage.Read(g.LastSavedPath);Check(data.complete&&data.distanceMeters>15&&data.samples.Count>50,"actual control-driven straight, rounded turn and return pass are recorded");
                }
                Check(paths[0]!=paths[1]&&File.Exists(paths[0])&&File.Exists(paths[1]),"both differently named recordings remain separately available");
                string first=File.ReadAllText(paths[0]),second=File.ReadAllText(paths[1]);
                Input(g,keyboard,mouse,Key.Numpad8);Input(g,keyboard,mouse);Check(g.PanelOpen&&g.Manager.SavedCount>=2,"Numpad 8 opens route manager with both saved recordings");
                Check(g.Manager.Select(paths[0])&&g.UseSavedRouteAsGps(paths[0]),"saved raw route becomes GPS-ready without an external editor: "+g.TrainingStatus);
                Check(g.ActiveRouteName=="Practice west loop"&&g.AutoFitPath.Length>0&&g.SelectedGuide!=null,"active name and separately saved automatic fit are exposed");
                Input(g,keyboard,mouse,Key.Numpad8);Input(g,keyboard,mouse);Input(g,keyboard,mouse,Key.Numpad7);Input(g,keyboard,mouse);
                Check(g.Visible&&g.LookEnd-g.Progress.Distance<=12.193&&g.DisplayPointCount>0,"Numpad 7 shows synchronized 40-foot world/map previews");
                var raw=RouteRecordingStorage.Read(paths[0]);int transitions=0,leg=0;
                foreach(var p in raw.samples)
                {game.Sim.Vehicle.X=p.vehicleX;game.Sim.Vehicle.Z=p.vehicleZ;game.Sim.Vehicle.Heading=p.vehicleHeadingRadians;game.Sim.Vehicle.Speed=p.speedMetersPerSecond;g.ObserveSimulationStep(game.Sim);g.Refresh();if(g.Progress.Leg>leg){transitions+=g.Progress.Leg-leg;leg=g.Progress.Leg;}if(g.ActiveFit.Length-g.Progress.Distance>1&&g.DisplayPointCount==0)throw new Exception("Guidance disappeared before route end");}
                Check(transitions>=2&&g.ActiveFit.Length-g.Progress.Distance<1,"recorded trajectory crosses multiple boundaries automatically without disappearing");
                var catalog=new RouteLibraryCatalog();catalog.Refresh(g.Library,g.GuideDirectory);var entry=catalog.entries.Find(e=>e.path==paths[0]);RouteLibraryCatalog.Rename(entry,"West loop renamed");
                Check(RouteLibraryCatalog.DisplayName(paths[0],"")=="West loop renamed"&&File.ReadAllText(paths[0])==first,"rename changes checksummed library metadata, retaining exact original route bytes");
                string copy=RouteLibraryCatalog.SaveCopy(entry,g.Library,g.GuideDirectory);catalog.Refresh(g.Library,g.GuideDirectory);var extra=catalog.entries.Find(e=>e.path==copy);RouteLibraryCatalog.DeleteConfirmed(extra,g.Library,g.GuideDirectory);
                Check(!File.Exists(copy)&&File.ReadAllText(paths[0])==first&&File.ReadAllText(paths[1])==second,"copy/delete operations on disposable test files retain both original recordings");
                string input=Root+"OperatorInputs";
                if(Directory.Exists(input))foreach(string path in Directory.GetFiles(input,"*.route.json"))
                {string bytes=File.ReadAllText(path);Check(g.UseSavedRouteAsGps(path),"actual operator recording automatically fits: "+Path.GetFileName(path)+" / "+g.TrainingStatus);Check(File.ReadAllText(path)==bytes,"actual operator input remains unchanged");}
                File.WriteAllLines(Root+"Saved-routes-manifest.txt",paths);
            }
            finally{if(g.PanelOpen)g.ToggleRecordingPanel();if(g.Visible)g.Toggle();g.UseOriginalReference();game.enabled=enabled;File.WriteAllLines(Root+"Unity-workflow-results.txt",results);}
        }
        static void Tick(Simulation s,IceRouteGuide g){s.Step(.02f);g.ObserveSimulationStep(s);}
        static void Pose(Simulation s,float x,float z,float h)
        {s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=h;s.Vehicle.Speed=0;s.Conditioner.X=x-Mathf.Sin(h)*1.1f;s.Conditioner.Z=z-Mathf.Cos(h)*1.1f;s.Conditioner.Heading=h;}
        public static void VerifyFreshPlay()
        {
            results.Clear();SessionState.SetBool("Aurora.RouteUXFresh",true);UnityEditor.SceneManagement.EditorSceneManager.OpenScene(IceResurfacerEditor.ScenePath);ResumeFresh();EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void ResumeFresh()
        {
            if(!SessionState.GetBool("Aurora.RouteUXFresh",false))return;
            EditorApplication.playModeStateChanged-=FreshState;EditorApplication.playModeStateChanged+=FreshState;
            if(EditorApplication.isPlaying)FreshState(PlayModeStateChange.EnteredPlayMode);
        }
        static int frames;
        static void FreshState(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode){frames=0;EditorApplication.update-=FreshTick;EditorApplication.update+=FreshTick;}}
        static void FreshTick()
        {
            if(++frames<30)return;EditorApplication.update-=FreshTick;
            Keyboard keyboard=null;Mouse mouse=null;
            try
            {
                var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();game.enabled=false;game.NewSession(true);var g=game.RouteGuide;
                var settings=UnityEngine.Object.Instantiate(InputSystem.settings);settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=settings;
                g.ConfigureValidationStorage(Path.GetFullPath(Root+"Library"));var paths=File.ReadAllLines(Root+"Saved-routes-manifest.txt");keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
                Input(g,keyboard,mouse,Key.Numpad8);Input(g,keyboard,mouse);Check(g.PanelOpen&&g.Manager.SavedCount>=2,"fresh Play instance opens saved routes with Numpad 8");
                Check(g.Manager.Select(paths[0])&&g.UseSavedRouteAsGps(paths[0]),"fresh Play loads and fits first raw recording without opening editor");
                Input(g,keyboard,mouse,Key.Numpad8);Input(g,keyboard,mouse);Input(g,keyboard,mouse,Key.Numpad7);Input(g,keyboard,mouse);
                Check(g.Visible&&g.ActiveRouteName=="West loop renamed"&&File.Exists(paths[1]),"renamed active route, enabled GPS and second recording survive Play restart");
                Check(g.LookEnd-g.Progress.Distance<=12.193,"fresh Play retains synchronized 40-foot preview");
                File.WriteAllLines(Root+"Fresh-play-results.txt",results);SessionState.SetBool("Aurora.RouteUXFresh",false);EditorApplication.Exit(0);
            }
            catch(Exception e){SessionState.SetBool("Aurora.RouteUXFresh",false);Debug.LogException(e);EditorApplication.Exit(1);}
            finally{if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);}
        }
    }
}
