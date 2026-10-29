using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Editor
{
    // Explicit isolated-project capture entry. Never runs in the user's Editor.
    [InitializeOnLoad]
    public static class IceCockpitRefinementValidation
    {
        const string BeforeKey="IceResurfacer.CockpitBefore";
        const string Root="Assets/Experiments/IceResurfacer/Validation~/";
        static int frames;
        static readonly List<string> results=new List<string>();
        static void Check(bool valid,string message)
        { if(!valid)throw new InvalidOperationException("Cockpit refinement: "+message);results.Add("PASS "+message); }
        static IceCockpitRefinementValidation()
        {
            if(Application.isBatchMode&&SessionState.GetBool(BeforeKey,false))EditorApplication.update+=BeforeTick;
        }
        public static void CaptureBefore()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))
                throw new InvalidOperationException("Use the isolated validation project for baseline captures.");
            SessionState.SetBool(BeforeKey,true);
            EditorSceneManager.OpenScene(IceResurfacerEditor.ScenePath);
            EditorApplication.update-=BeforeTick;EditorApplication.update+=BeforeTick;
            EditorApplication.EnterPlaymode();
        }
        static void BeforeTick()
        {
            if(!EditorApplication.isPlaying)return;
            var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
            if(game==null||game.Sim==null||++frames<30)return;
            try { CaptureViews(game,"CockpitRefinementBefore");SessionState.SetBool(BeforeKey,false);EditorApplication.Exit(0); }
            catch(Exception e) { Debug.LogException(e);SessionState.SetBool(BeforeKey,false);EditorApplication.Exit(1); }
            EditorApplication.update-=BeforeTick;
        }
        internal static void CaptureViews(IceResurfacerGame game,string folder)
        {
            Directory.CreateDirectory(Root+folder);
            bool enabled=game.enabled;game.enabled=false;
            var camera=game.DriverCamera;var head=camera.transform;
            var render=typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic);
            var look=typeof(IceResurfacerGame).GetMethod("UpdateLook",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                game.NewSession(true);
                game.Sim.Vehicle.X=game.Sim.Vehicle.Z=game.Sim.Vehicle.Heading=0;
                game.Sim.Conditioner.X=0;game.Sim.Conditioner.Z=-1.14f;game.Sim.Conditioner.Heading=0;
                game.Sim.Step(.02f);
                render.Invoke(game,new object[]{0f});
                head.localPosition=new Vector3(0,game.operatorEyeHeight,0);
                head.localRotation=Quaternion.Euler(55,15,0);
                IceResurfacerEditor.Capture(camera,folder+"/Steering-seated.png",1920,1080);
                head.localPosition=new Vector3(0,game.operatorEyeHeight,-.18f);
                head.localRotation=Quaternion.Euler(42,75,0);
                IceResurfacerEditor.Capture(camera,folder+"/Controls-seated-fixed.png",1920,1080);
                game.SelectCameraView("Controls");
                for(int i=0;i<100;i++)look.Invoke(game,new object[]{true,.02f});
                IceResurfacerEditor.Capture(camera,folder+"/Controls-E.png",1920,1080);
                game.SelectCameraView("Rear");
                for(int i=0;i<100;i++)look.Invoke(game,new object[]{true,.02f});
                IceResurfacerEditor.Capture(camera,folder+"/Rear-shoulder.png",1920,1080);
                var external=new GameObject("Explicit brush comparison camera");
                try
                {
                    var side=external.AddComponent<Camera>();side.enabled=false;side.fieldOfView=48;
                    side.transform.position=game.World.Machine.TransformPoint(-4.5f,1.20f,1.05f);
                    side.transform.LookAt(game.World.Machine.TransformPoint(-.40f,.56f,.98f));
                    IceResurfacerEditor.Capture(side,folder+"/Brush-side.png",1920,1080);
                    game.Sim.Controls.BoardBrush=true;
                    for(int i=0;i<40;i++)game.Sim.Step(.02f);
                    render.Invoke(game,new object[]{0f});
                    IceResurfacerEditor.Capture(side,folder+"/Brush-deployed.png",1920,1080);
                }
                finally { UnityEngine.Object.DestroyImmediate(external); }
            }
            finally {game.NewSession(true);game.enabled=enabled;}
        }

        internal static void Run(IceResurfacerGame game,Keyboard keys,Mouse mouse)
        {
            results.Clear();bool enabled=game.enabled;game.enabled=false;
            var render=typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic);
            var look=typeof(IceResurfacerGame).GetMethod("UpdateLook",BindingFlags.Instance|BindingFlags.NonPublic);
            var update=typeof(IceResurfacerGame).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            var colliders=new List<MeshCollider>();var probeObject=new GameObject("Cockpit clearance probe");
            var probe=probeObject.AddComponent<SphereCollider>();probe.radius=.022f;
            void Render(float dt=0) {render.Invoke(game,new object[]{dt});Physics.SyncTransforms();}
            void Pointer(Vector2 position,bool down=false,bool allowed=true,bool hud=false)
            {
                InputSystem.QueueStateEvent(mouse,new MouseState{position=position,buttons=(ushort)(down?1:0)});
                InputSystem.Update();game.PhysicalControls.UpdateInput(allowed,hud);
            }
            Vector2 Screen(Vector3 point) {return game.DriverCamera.WorldToScreenPoint(point);}
            try
            {
                InputSystem.QueueStateEvent(keys,new KeyboardState());InputSystem.Update();
                CaptureViews(game,"CockpitRefinementAfter");
                game.NewSession(true);var s=game.Sim;var w=game.World;var c=s.Controls;var config=s.Config;
                s.Vehicle.X=s.Vehicle.Z=s.Vehicle.Heading=0;
                s.Conditioner.X=0;s.Conditioner.Z=-1.14f;s.Conditioner.Heading=0;
                s.Step(.02f);Render();
                Check(Mathf.Abs(w.BrushArm.localPosition.z-1.16f)<.00001f,"complete brush mount moves 0.46 m forward from the handoff geometry");
                for(int phase=0;phase<2;phase++)
                for(int step=0;step<41;step++)
                {
                    c.BoardBrush=phase==0;s.Vehicle.Heading=step%3==0?.64f:step%3==1?-1.67f:0;
                    s.Step(.02f);Render();
                    float angle=(1-s.BrushDeployment)*config.BrushRetractedDegrees*Mathf.Deg2Rad;
                    Vector3 expected=w.Machine.TransformPoint(config.BrushPivotX-config.BrushArmLength*Mathf.Cos(angle),0,
                        config.BrushPivotZ-config.BrushArmLength*Mathf.Sin(angle));
                    Check(new Vector2(w.Brush.position.x-s.BrushWorldX,w.Brush.position.z-s.BrushWorldZ).magnitude<.00001f&&
                        new Vector2(expected.x-s.BrushWorldX,expected.z-s.BrushWorldZ).magnitude<.0001f,
                        "brush footprint / pivot / visible disc agree through "+(phase==0?"deployment ":"retraction ")+step);
                }
                s.Vehicle.Heading=0;Render();
                Vector3 hub=w.OperatorStation.InverseTransformPoint(w.SteeringWheel.position);
                Check(hub.z>.50f&&Mathf.Abs(hub.y-config.SteeringWheelHeight)<.00001f&&
                    Vector3.Angle(w.Machine.up,w.SteeringWheel.up)>60,"steering hub is forward of the seat and wheel plane is substantially upright");
                Check(w.SteeringWheel.IsChildOf(w.SteeringColumn),"wheel and connected upper/lower column share a coherent assembly");
                var foot=Array.Find(w.SteeringColumn.GetComponentsInChildren<Transform>(),t=>t.name=="Steering column foot");
                var floor=Array.Find(w.OperatorStation.GetComponentsInChildren<Transform>(),t=>t.name=="Left operator floor");
                Vector3 footOnDeck=w.OperatorStation.InverseTransformPoint(foot.position);
                Vector3 floorOnDeck=w.OperatorStation.InverseTransformPoint(floor.position);
                Check(Mathf.Abs(footOnDeck.y-foot.lossyScale.y-floorOnDeck.y-floor.lossyScale.y*.5f)<.005f&&
                    Mathf.Abs(footOnDeck.z-floorOnDeck.z)<floor.lossyScale.z*.5f&&
                    Mathf.Abs(footOnDeck.x-floorOnDeck.x)<floor.lossyScale.x*.5f,
                    "steering column foot seats on the actual operator platform without a floating mount");
                var support=Array.Find(w.SteeringColumn.GetComponentsInChildren<Transform>(),t=>t.name=="Steering column support");
                var accelerator=Array.Find(w.AcceleratorPedal.GetComponentsInChildren<Transform>(),t=>t.name=="Accelerator pedal");
                var brake=Array.Find(w.BrakePedal.GetComponentsInChildren<Transform>(),t=>t.name=="Brake pedal");
                var supportBox=support.gameObject.AddComponent<BoxCollider>();
                var acceleratorBox=accelerator.gameObject.AddComponent<BoxCollider>();
                var brakeBox=brake.gameObject.AddComponent<BoxCollider>();
                try
                {
                    foreach(bool pressed in new[]{false,true})
                    {
                        c.Throttle=pressed?1:0;c.Brake=pressed;Render();
                        foreach(var pedal in new[]{acceleratorBox,brakeBox})
                            Check(!Physics.ComputePenetration(pedal,pedal.transform.position,pedal.transform.rotation,
                                supportBox,support.position,support.rotation,out var direction,out float depth)||depth<.001f,
                                "column support clears "+pedal.name+" with pedals "+(pressed?"fully pressed":"released"));
                    }
                }
                finally
                {
                    c.Throttle=0;c.Brake=false;Render();
                    UnityEngine.Object.DestroyImmediate(supportBox);UnityEngine.Object.DestroyImmediate(acceleratorBox);UnityEngine.Object.DestroyImmediate(brakeBox);
                }
                foreach(var filter in w.Machine.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=filter.GetComponent<Renderer>();
                    if(filter.sharedMesh==null||renderer==null||!renderer.enabled||filter.transform.IsChildOf(w.SteeringWheel))continue;
                    var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;colliders.Add(collider);
                }
                Physics.SyncTransforms();
                foreach(float fraction in new[]{-1f,0f,1f})
                {
                    s.Vehicle.SteeringAngle=config.MaxSteeringRadians*fraction;Render();
                    Check(Quaternion.Angle(w.SteeringWheel.localRotation,Quaternion.Euler(-config.SteeringWheelTilt,0,0)*
                        Quaternion.Euler(0,s.Vehicle.SteeringAngle*Mathf.Rad2Deg*5,0))<.06f,
                        "upright steering wheel follows actual vehicle steering at "+fraction);
                    for(int i=0;i<48;i++)
                    {
                        float a=i*Mathf.PI/24;Vector3 rim=w.SteeringWheel.TransformPoint(Mathf.Cos(a)*.19f,0,Mathf.Sin(a)*.19f);
                        foreach(var collider in colliders)
                            if(Physics.ComputePenetration(probe,rim,Quaternion.identity,collider,collider.transform.position,collider.transform.rotation,out var direction,out float depth)&&depth>.001f)
                                throw new InvalidOperationException("Steering rim intersects "+collider.name+" by "+depth+" m at "+w.OperatorStation.InverseTransformPoint(rim));
                    }
                }
                results.Add("PASS full steering rim clears actual seat, control bank, dashboard and machine meshes at left / center / right steering");
                s.Vehicle.SteeringAngle=0;Render();
                Vector3 blade=w.OperatorStation.InverseTransformPoint(w.BladeWheel.position);
                Check(blade.x>w.OperatorStation.InverseTransformPoint(w.FloodValveMount.position).x&&
                    blade.x<w.OperatorStation.InverseTransformPoint(w.WashValveMount.position).x&&blade.z<-.17f,
                    "complete blade screw is between and behind the water bearings, closer to the operator");
                Check(config.WaterValveStemHeight>.20f&&Mathf.Abs(w.FloodValveMount.position.y-w.WashValveMount.position.y)<.001f,
                    "independent adjacent water handles have tall quarter-turn stems");
                Vector3 breaker=w.OperatorStation.InverseTransformPoint(w.SnowBreakerPlunger.position);
                Check(breaker.x<1.09f&&breaker.x>w.OperatorStation.InverseTransformPoint(w.DumpLever.position).x,
                    "snow breaker and fixed guide are beside the rightmost hydraulic lever");
                game.SelectCameraView("Controls");for(int i=0;i<90;i++)look.Invoke(game,new object[]{true,.02f});
                var controls=new[]{w.ConditionerLever,w.HorizontalAugerLever,w.VerticalAugerLever,w.DumpLever,w.FloodLever,w.WashValve,w.BladeWheel,w.SnowBreakerPlunger};
                var points=new[]{new Vector3(0,.225f,0),new Vector3(0,.225f,0),new Vector3(0,.225f,0),new Vector3(0,.225f,0),
                    new Vector3(-.09f,config.WaterValveStemHeight,0),new Vector3(-.09f,config.WaterValveStemHeight,0),new Vector3(.11f,.045f,0),new Vector3(0,.22f,0)};
                string[] names={"Conditioner hydraulic lever","Horizontal auger lever","Vertical auger lever","Snow tank hydraulic lever","Flood water valve","Wash water valve","Blade handwheel","Snow-breaker plunger"};
                var block=new MaterialPropertyBlock();int color=Shader.PropertyToID("_BaseColor");
                for(int i=0;i<controls.Length;i++)
                {
                    Vector3 target=controls[i].TransformPoint(points[i]),viewport=game.DriverCamera.WorldToViewportPoint(target);
                    Check(viewport.z>0&&viewport.x>.02f&&viewport.x<.98f&&viewport.y>.02f&&viewport.y<.98f,"seated E camera frames "+names[i]);
                    Pointer(Screen(target));Check(game.PhysicalControls.HighlightedControl==names[i],"existing picker hovers individual "+names[i]);
                    int tinted=0;
                    for(int j=0;j<controls.Length;j++)foreach(var renderer in controls[j].GetComponentsInChildren<Renderer>())
                    {
                        if(!renderer.enabled)continue;renderer.GetPropertyBlock(block);
                        if(block.HasColor(color)){tinted++;Check(i==j,"hover tint stays on the selected moving assembly");}
                    }
                    Check(tinted>0,"URP property block visibly tints "+names[i]);
                    if(i==0||i==4)IceResurfacerEditor.Capture(game.DriverCamera,"CockpitRefinementAfter/Hover-"+(i==0?"hydraulic":"water")+".png",1920,1080);
                    Check(Physics.Raycast(game.DriverCamera.transform.position,(target-game.DriverCamera.transform.position).normalized,out var hit,2)&&hit.transform.IsChildOf(controls[i]),
                        "actual mesh/collider ray reaches relocated "+names[i]+" without obstruction");
                }
                Vector3 breakerRest=w.SnowBreakerPlunger.localPosition;
                int strokes=s.SnowBreakerStrokes;
                Vector2 breakerGrip=Screen(w.SnowBreakerPlunger.TransformPoint(0,.22f,0));
                Pointer(breakerGrip);Pointer(breakerGrip,true);Pointer(breakerGrip);
                Check(s.SnowBreakerStrokes==strokes+1,"physical breaker click invokes exactly one simulation stroke");
                Render(.25f);
                Check(Mathf.Abs(w.SnowBreakerPlunger.localPosition.y-breakerRest.y+.18f)<.00001f,
                    "relocated breaker rod and T handle complete the 18 cm downstroke");
                Check(game.PhysicalControls.GetHitControl(Screen(w.SnowBreakerPlunger.TransformPoint(0,.22f,0)))==names[7],
                    "breaker pick target follows the animated downstroke");
                Render(.36f);
                Check(Vector3.Distance(w.SnowBreakerPlunger.localPosition,breakerRest)<.00001f,
                    "relocated breaker returns exactly to its fixed guide after the stroke");
                Vector2 knob=Screen(w.ConditionerLever.TransformPoint(0,.225f,0));
                Pointer(knob);Pointer(knob,true);Pointer(new Vector2(2,2),true);
                Check(game.PhysicalControls.IsDragging&&game.PhysicalControls.HighlightedControl==names[0],"dragging retains feedback outside the grip");
                Pointer(new Vector2(2,2));
                Check(!game.PhysicalControls.IsDragging&&game.PhysicalControls.HighlightedControl==""&&c.ConditionerHydraulic==0,"release outside control clears tint and spring demand");
                Pointer(knob);Pointer(knob,false,true,true);Check(game.PhysicalControls.HighlightedControl=="","HUD occlusion clears hover");
                Pointer(knob);game.TogglePause();Check(game.PhysicalControls.HighlightedControl=="","pause clears highlight");game.TogglePause();
                Pointer(knob);game.ToggleHelp();Check(game.PhysicalControls.HighlightedControl=="","help clears highlight");game.ToggleHelp();
                Pointer(knob);game.SelectCameraView("Chase");Check(game.PhysicalControls.HighlightedControl=="","external camera transition clears highlight");
                game.SelectCameraView("Controls");for(int i=0;i<90;i++)look.Invoke(game,new object[]{true,.02f});
                knob=Screen(w.ConditionerLever.TransformPoint(0,.225f,0));
                Pointer(knob);Pointer(knob,false,false);Check(game.PhysicalControls.HighlightedControl=="","disabled / manual-look / on-foot input clears highlight");
                Pointer(knob);
                typeof(IceResurfacerGame).GetMethod("OnApplicationFocus",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{false});
                Check(game.PhysicalControls.HighlightedControl=="","actual focus-loss callback clears highlight");
                typeof(IceResurfacerGame).GetMethod("OnApplicationFocus",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{true});
                Pointer(knob);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=knob,buttons=2});InputSystem.Update();update.Invoke(game,null);
                Check(game.Looking&&game.PhysicalControls.HighlightedControl=="","actual RMB input clears physical-control feedback");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=knob});InputSystem.Update();update.Invoke(game,null);
                Pointer(knob);InputSystem.QueueStateEvent(keys,new KeyboardState(Key.LeftShift));InputSystem.Update();update.Invoke(game,null);
                Check(game.PhysicalControls.HighlightedControl=="","right-shoulder camera input clears highlight");
                InputSystem.QueueStateEvent(keys,new KeyboardState());InputSystem.Update();
                game.SelectCameraView("Controls");for(int i=0;i<90;i++)look.Invoke(game,new object[]{true,.02f});
                knob=Screen(w.ConditionerLever.TransformPoint(0,.225f,0));Pointer(knob);
                InputSystem.QueueStateEvent(keys,new KeyboardState(Key.F));InputSystem.Update();update.Invoke(game,null);
                Check(s.OnFoot&&game.PhysicalControls.HighlightedControl=="","actual dismount input clears physical-control feedback");
                InputSystem.QueueStateEvent(keys,new KeyboardState());InputSystem.Update();
                game.PhysicalControls.Release();
                foreach(var control in controls)foreach(var renderer in control.GetComponentsInChildren<Renderer>())
                {renderer.GetPropertyBlock(block);Check(!block.HasColor(color),"release restores original control material property blocks");}
            }
            catch(Exception e) {results.Add("FAIL "+e.Message);throw;}
            finally
            {
                File.WriteAllLines(Root+"Cockpit-refinement-results.txt",results);
                foreach(var collider in colliders)UnityEngine.Object.DestroyImmediate(collider);
                UnityEngine.Object.DestroyImmediate(probeObject);game.PhysicalControls.Release();game.NewSession(true);game.enabled=enabled;
            }
        }
    }
}
