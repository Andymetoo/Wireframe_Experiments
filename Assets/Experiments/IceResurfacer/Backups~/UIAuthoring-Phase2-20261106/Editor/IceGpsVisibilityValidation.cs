using System;
using System.IO;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    // Native rendering/visibility checks run only in the disposable project copy.
    [InitializeOnLoad]
    public static class IceGpsVisibilityValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/",Out=Root+"Validation~/GpsVisibility/",Stage="Aurora.GpsVisibility.Validation";
        static int frames;
        static string SceneName=>SessionState.GetString(Stage+"Scene","IceResurfacer_AuthoredMachine");
        static IceGpsVisibilityValidation(){EditorApplication.update+=Tick;}
        static void Log(string message){Directory.CreateDirectory(Out);File.AppendAllText(Out+SceneName+"-unity-results.txt",message+"\n");Debug.Log(message);}
        static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Log("PASS "+message);}
        public static void Run()
        {
            try
            {
                if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))throw new InvalidOperationException("Use the isolated validation project.");
                string path=Root+"Scenes/IceResurfacer_AuthoredMachine.unity";var args=Environment.GetCommandLineArgs();
                for(int i=0;i<args.Length-1;i++)if(args[i]=="-iceScene")path=args[i+1];
                SessionState.SetString(Stage+"Scene",Path.GetFileNameWithoutExtension(path));Directory.CreateDirectory(Out);File.WriteAllText(Out+SceneName+"-unity-results.txt","");
                EditorSceneManager.OpenScene(path);SessionState.SetBool(Stage,true);frames=0;EditorApplication.EnterPlaymode();
            }
            catch(Exception e){Finish(e);}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Stage,false)||!EditorApplication.isPlaying||++frames<8)return;
            SessionState.SetBool(Stage,false);
            try{Validate(UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>());Finish(null);}
            catch(Exception e){Finish(e);}
        }
        static void Finish(Exception error)
        {SessionState.SetBool(Stage,false);if(error!=null){Log("FAIL "+error);Debug.LogException(error);}else Log("ALL NATIVE GPS VISIBILITY CHECKS PASSED");EditorApplication.Exit(error==null?0:1);}
        static LineRenderer Renderer(IceRouteGuide guide,string name)=>guide.transform.Find(name).GetComponent<LineRenderer>();
        static readonly string[] Names={"F5 advisory upcoming route","GPS contrasting border","GPS vehicle heading / rear axle","GPS heading border"};
        static void Shown(IceRouteGuide guide,bool shown,string reason)
        {guide.Refresh();Check(guide.GuidanceDisplayed==shown,reason+" shared visibility");foreach(string name in Names)Check(Renderer(guide,name).enabled==shown,reason+" / "+name);}
        static string State(IceResurfacerGame game)=>JsonUtility.ToJson(game.Sim.Vehicle)+JsonUtility.ToJson(game.Sim.Conditioner)+JsonUtility.ToJson(game.Sim.Controls)+JsonUtility.ToJson(game.Sim.GetReport())+JsonUtility.ToJson(game.Sim.Config);
        static void Validate(IceResurfacerGame game)
        {
            Check(game!=null&&game.Sim!=null,"scene initializes");game.enabled=false;game.NewSession(true);
            var guide=game.RouteGuide;guide.ConfigureValidationStorage(Out+"Recordings");guide.Refresh();guide.UseOriginalReference();
            if(guide.Visible)guide.Toggle();Shown(guide,false,"GPS off");
            Check(guide.StartRecording(),"recording fixture starts while GPS off");Shown(guide,false,"recording alone");guide.StopRecording("GPS visibility fixture");
            guide.Toggle();Shown(guide,true,"GPS on");
            var fit=guide.ActiveFit;double corner=0;
            foreach(var p in fit.samples)if(p.distance>30&&Math.Abs(p.curvature)>.05){corner=p.distance;break;}
            foreach(double distance in new[]{0d,50d,corner,fit.Length-1,fit.Length})
            {
                // Explicit pose/progress fixtures; Refresh itself must remain observational.
                var p=fit.At(distance);game.Sim.Vehicle.X=p.x+.4f;game.Sim.Vehicle.Z=p.z;game.Sim.Vehicle.Heading=p.heading+.17f;
                typeof(RouteProgress).GetProperty("Distance").SetValue(guide.Progress,distance);
                string state=State(game);double progress=guide.Progress.Distance;int leg=guide.Progress.Leg;
                Shown(guide,distance<fit.Length-.65,"distance "+distance.ToString("F2"));
                Check(state==State(game)&&guide.Progress.Distance==progress&&guide.Progress.Leg==leg,"render refresh preserves simulation and sequential navigation");
                Check(guide.LookEnd<=fit.Length&&guide.LookEnd-distance<=24.38401&&guide.DisplayPointCount<=70&&guide.MapPointCount<=62,"straight/corner/end preview bounds");
                if(!guide.GuidanceDisplayed)continue;
                var yellow=Renderer(guide,Names[2]);var origin=yellow.GetPosition(0);var end=yellow.GetPosition(2);var vehicle=game.Sim.Vehicle;
                Check(yellow.positionCount==3&&Vector2.Distance(new Vector2(origin.x,origin.z),new Vector2(vehicle.X,vehicle.Z))<.000001f,"yellow starts at live rear axle, including actual lateral offset");
                Check(Mathf.Abs((end-origin).magnitude-(game.Sim.Config.BodyFront+6))<.00001f&&Vector3.Dot((end-origin).normalized,new Vector3(Mathf.Sin(vehicle.Heading),0,Mathf.Cos(vehicle.Heading)))>.999999f,"yellow follows actual yaw and extends six metres beyond nose");
                Check(Vector3.Cross(yellow.GetPosition(1)-origin,end-origin).sqrMagnitude<.000001f,"yellow stays straight through GPS corners");
            }
            typeof(RouteProgress).GetProperty("Distance").SetValue(guide.Progress,50d);
            game.Sim.Vehicle.X=game.Sim.Vehicle.Z=game.Sim.Vehicle.Heading=game.Sim.Vehicle.Speed=0;
            game.Sim.Conditioner.X=0;game.Sim.Conditioner.Z=-(game.Sim.Config.HitchBehindAxle+game.Sim.Config.DrawbarLength);game.Sim.Conditioner.Heading=0;
            typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{0f});guide.Refresh();
            foreach(string view in new[]{"Forward","Controls","Rear","Chase"})
            {
                var yellow=Renderer(guide,Names[2]);var before=yellow.GetPosition(2);game.SelectCameraView(view);
                typeof(IceResurfacerGame).GetMethod("UpdateLook",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{true,1f});
                Shown(guide,true,"camera "+view);Check(yellow.GetPosition(2)==before,"camera changes never steer alignment geometry");
                Capture(game.DriverCamera,SceneName+"-"+view+".png");
            }
            game.SelectCameraView("Forward");game.TogglePause();Shown(guide,false,"paused");game.TogglePause();Shown(guide,true,"resumed");
            game.Sim.Vehicle.X+=.25f;
            typeof(IceRouteGuide).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(guide,null);
            Check(Mathf.Abs(Renderer(guide,Names[2]).GetPosition(0).x-game.Sim.Vehicle.X)<.000001f,"late presentation follows the latest driving pose without another route refresh");
            game.ToggleHelp();Shown(guide,false,"help open");game.ToggleHelp();
            guide.ToggleRecordingPanel();Shown(guide,false,"route panel open");guide.ToggleRecordingPanel();
            game.ToggleVideoMode();Shown(guide,false,"video / HUD hidden");game.ToggleVideoMode();Shown(guide,true,"HUD restored");
            Check(game.Sim.TryDismount(),"operator dismounts");Shown(guide,false,"on foot");Check(game.Sim.TryBoard(),"operator boards");Shown(guide,true,"seated again");
            guide.enabled=false;foreach(string name in Names)Check(!Renderer(guide,name).enabled,"component disabled hides "+name);guide.enabled=true;Shown(guide,true,"component re-enabled");
            typeof(IceRouteGuide).GetProperty("Available").SetValue(guide,false);Shown(guide,false,"guidance unavailable");typeof(IceRouteGuide).GetProperty("Available").SetValue(guide,true);Shown(guide,true,"guidance available");
            guide.Toggle();Shown(guide,false,"GPS hidden again");guide.Toggle();Shown(guide,true,"GPS shown again");
            using(var continuation=new RouteSilhouetteContinuation(game))
            {
                continuation.MeasureVisibility(game,1600,900);
                Check(continuation.LastSampledPoints>0&&continuation.LastSampledPoints<=145&&continuation.LastRayQueries<=145,"native silhouette point/raycast budget is bounded");
                int shapes=continuation.ShapeCount;var watch=System.Diagnostics.Stopwatch.StartNew();
                for(int i=0;i<30;i++){guide.Refresh();continuation.MeasureVisibility(game,1600,900);}watch.Stop();
                Check(continuation.ShapeCount==shapes,"longer route and heading add no visibility-query colliders");
                Log("PERFORMANCE native Refresh/visibility 30 iterations ms="+watch.Elapsed.TotalMilliseconds.ToString("F3")+"; samples="+continuation.LastSampledPoints+" raycasts="+continuation.LastRayQueries+" shapes="+shapes);
                game.TogglePause();continuation.MeasureVisibility(game,1600,900);Check(continuation.LastRayQueries==0&&continuation.LastVisibleSegments==0,"paused silhouette does no queries or drawing");game.TogglePause();
            }
            var shader=Renderer(guide,Names[2]).sharedMaterial.shader;
            Check(!ShaderUtil.ShaderHasError(shader)&&File.ReadAllText(Root+"Resources/TrainingRoute.shader").Contains("ZTest LEqual"),"both world lines use the existing depth-tested shader");
        }
        static void Capture(Camera camera,string name)
        {
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Out+name,image.EncodeToPNG());}
            finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
