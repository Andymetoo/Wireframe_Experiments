using System;using System.Collections.Generic;using System.IO;using System.Reflection;
using IceResurfacer.Core;using IceResurfacer.Presentation;using UnityEditor;using UnityEngine;
using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;
namespace IceResurfacer.Editor
{
    public static class IceHUDVideoSpeedValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/Validation~/HUDVideoSpeed/";
        const string Gate="Aurora.OperatorSpeed.Persistence";
        static readonly List<string> results=new List<string>();
        static void Check(bool value,string text){if(!value)throw new Exception("HUD/video/speed: "+text);results.Add("PASS "+text);}
        internal static void Run(IceResurfacerGame game,Keyboard keyboard,Mouse mouse)
        {
            Directory.CreateDirectory(Root);results.Clear();bool enabled=game.enabled;game.enabled=false;
            bool had=PlayerPrefs.HasKey(IceOperatorPreferences.SpeedKey);float previous=PlayerPrefs.GetFloat(IceOperatorPreferences.SpeedKey,0);
            var update=typeof(IceResurfacerGame).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            void Input(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();update.Invoke(game,null);}
            try
            {
                game.ResetOperatorSpeed();game.NewSession(true);var g=game.RouteGuide;g.Refresh();if(g.Visible)g.Toggle();g.Toggle();
                float calibration=game.calibration.ForwardSpeed;
                var geometry=g.ActiveFit;double routeLength=geometry.Length;
                Check(Math.Abs(calibration-2.65f)<.000001f,"actual launch calibration is 2.65 m/s / "+game.OriginalForwardSpeedMph.ToString("F5")+" mph");
                foreach(float mph in new[]{3f,5.5f,8f})
                {game.SetOperatorSpeedMph(mph);Check(Math.Abs(game.Sim.MaximumForwardSpeedTarget-mph*.44704f)<.000001f&&game.calibration.ForwardSpeed==calibration,"runtime "+mph+" mph setting leaves serialized calibration unchanged");
                 Check(g.ActiveFit==geometry&&g.ActiveFit.Length==routeLength,"speed selection preserves active GPS geometry at "+mph+" mph");}
                foreach(float mph in new[]{3f,8f})
                {
                    var sim=new Simulation(practice:true);sim.Vehicle.X=sim.Conditioner.X=-5;sim.Vehicle.Z=-20;sim.Conditioner.Z=-22;sim.Vehicle.Heading=sim.Conditioner.Heading=0;
                    sim.SetMaximumForwardSpeed(mph*.44704f);sim.Controls.Throttle=1;
                    var recorder=new RouteRecorder();recorder.Start(sim,"Speed compatibility fixture","Deterministic speed fixture; NOT an operator route");
                    for(int i=0;i<250;i++){sim.Step(.02f);recorder.Observe(sim);}
                    recorder.Stop();recorder.Data.Validate();
                    Check(recorder.Data.distanceMeters>2&&recorder.Data.samples.Count>10&&recorder.Data.configuration.ForwardSpeed==calibration,"unchanged raw route format records actual driving at "+mph+" mph target");
                }
                game.SetOperatorSpeedMph(5.5f);game.NewSession(true);g.Refresh();Check(Math.Abs(game.SelectedForwardSpeedMph-5.5f)<.0001f,"PlayerPrefs maximum restores on a new session");
                Check(game.Sim.GetReport().SelectedMaxForwardSpeedMph==game.SelectedForwardSpeedMph,"run diagnostics record selected speed independently of scoring");
                game.ResetOperatorSpeed();Check(!PlayerPrefs.HasKey(IceOperatorPreferences.SpeedKey)&&game.Sim.MaximumForwardSpeedTarget==calibration,"Reset deletes only this operator preference and restores the exact original target");
                if(!g.Visible)g.Toggle();g.ToggleRecordingPanel();Check(g.PanelOpen,"route manager is open before video toggle");
                Input();Input(Key.F2);Input();int debug=game.SurfaceDebugMode;
                Input(Key.Numpad1);Input();Check(game.HudHidden&&!g.PanelOpen&&!game.HelpVisible&&!game.Paused,"actual Numpad 1 hides UI and closes blocking menus");
                Check(game.Surface.DebugMode==0&&game.SurfaceDebugMode==debug,"clean video suppresses ice diagnostic coloring while retaining the selected debug mode");
                Check(!game.Dashboard.PointerOverInterface(game,new Vector2(Screen.width*.5f,Screen.height*.5f)),"hidden UI does not intercept the centre mouse pointer");
                var line=g.transform.Find("F5 advisory upcoming route").GetComponent<LineRenderer>();g.Refresh();Check(g.Visible&&!line.enabled,"GPS selection/progress remain active while all route graphics are hidden");
                float time=game.Sim.Time;Input(Key.W);Check(game.RawThrottle==1&&game.Sim.Controls.Throttle==1,"normal driving inputs remain active in clean video");Input();
                Input(Key.Digit1);Check(game.HudHidden&&game.Sim.Controls.ConditionerHydraulic<0,"number-row machinery control is independent from Numpad 1");Input();
                Input(Key.Escape);Input();Check(game.Paused&&game.HudHidden,"explicit ESC remains recoverable during video mode");Input(Key.Escape);Input();
                Input(Key.Numpad1);Input();g.Refresh();Check(!game.HudHidden&&line.enabled&&game.Surface.DebugMode==debug,"Numpad 1 restores HUD, selected ice debug mode and previously enabled GPS");
                while(game.SurfaceDebugMode!=0){Input(Key.F2);Input();}
                Check(IceDashboard.SurveyMapRect.x<100,"survey defaults to upper-left coordinates");
                var s=game.Sim;s.Controls.BladeDepthMm=0;s.Conditioner.Lift=0;Check(IceDashboard.BladeEngagement(s)=="RETRACTED","schematic identifies retracted blade");
                s.Controls.BladeDepthMm=.1f;Check(IceDashboard.BladeEngagement(s)=="SHALLOW CUT"&&IceDashboard.BladeContactMm(s)>0,"schematic follows actual shallow engagement");
                s.Controls.BladeDepthMm=.8f;Check(IceDashboard.BladeEngagement(s)=="DEEP CUT","schematic follows deep effective bite");s.Conditioner.Lift=.6f;
                Check(IceDashboard.BladeContactMm(s)==0&&IceDashboard.BladeEngagement(s)=="OFF ICE","actual conditioner lift removes schematic contact despite a deep setting");
            }
            finally
            {
                if(game.HudHidden)game.ToggleVideoMode();if(game.RouteGuide.PanelOpen)game.RouteGuide.ToggleRecordingPanel();if(game.RouteGuide.Visible)game.RouteGuide.Toggle();
                if(had)IceOperatorPreferences.SaveMph(previous);else IceOperatorPreferences.ResetSpeed();game.NewSession(true);game.enabled=enabled;File.WriteAllLines(Root+"Unity-control-results.txt",results);
            }
        }
        public static void WritePersistence(){Begin("write");}
        public static void ReadPersistence(){Begin("read");}
        static void Begin(string action)
        {SessionState.SetString(Gate,action);UnityEditor.SceneManagement.EditorSceneManager.OpenScene(IceResurfacerEditor.ScenePath);Resume();EditorApplication.isPlaying=true;}
        [InitializeOnLoadMethod] static void Resume()
        {
            if(SessionState.GetString(Gate,"")=="")return;EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;
            if(EditorApplication.isPlaying)State(PlayModeStateChange.EnteredPlayMode);
        }
        static int frame;
        static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode){frame=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}}
        static void Tick()
        {
            if(++frame<30)return;EditorApplication.update-=Tick;
            try
            {
                var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();game.enabled=false;game.NewSession(true);Directory.CreateDirectory(Root);
                string action=SessionState.GetString(Gate,"");
                if(action=="write"){game.SetOperatorSpeedMph(5.5f);File.WriteAllText(Root+"Preference-write.txt","PASS selected 5.50 MPH in actual Play Mode; PlayerPrefs.Save called before leaving Play.");}
                else
                {Check(Math.Abs(game.SelectedForwardSpeedMph-5.5f)<.0001f&&game.calibration.ForwardSpeed==2.65f,"fresh Unity Play reloads 5.50 MPH from PlayerPrefs with scene calibration intact");game.ResetOperatorSpeed();File.WriteAllLines(Root+"Preference-fresh-play.txt",results);}
                SessionState.SetString(Gate,"");EditorApplication.Exit(0);
            }
            catch(Exception e){SessionState.SetString(Gate,"");Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
