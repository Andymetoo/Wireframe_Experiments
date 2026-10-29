using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IceResurfacer.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Editor
{
    // Explicit batch suite using the real Input System and runtime camera method.
    internal static class IceCameraValidation
    {
        static IceResurfacerGame game;
        static Keyboard keyboard;
        static Mouse mouse;
        static MethodInfo look;
        static readonly List<string> passes=new List<string>();
        static Transform Head { get { return game.DriverCamera.transform; } }
        static void Check(bool value,string message)
        { if(!value)throw new InvalidOperationException("Camera: "+message);passes.Add("PASS "+message); }
        static void Same(Quaternion q,string message)
        { Check(Vector3.Distance(q*Vector3.forward,Head.localRotation*Vector3.forward)<.000001f,message); }
        static void Step(bool held=false,Vector2 delta=default,bool running=true,params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            InputSystem.QueueStateEvent(mouse,new MouseState{buttons=(ushort)(held?2:0),delta=delta,position=new Vector2(400,300)});
            InputSystem.Update();look.Invoke(game,new object[]{running,.02f});
        }
        internal static void Aim(float yaw,float pitch,bool raised)
        {
            Step(false,default,true,raised?new[]{Key.Space}:new Key[0]);
            float y=Mathf.DeltaAngle(0,Head.localEulerAngles.y),p=Mathf.DeltaAngle(0,Head.localEulerAngles.x);
            Step(true);Step(true);
            Step(true,new Vector2((yaw-y)/.11f,(p-pitch)/.11f),true,raised?new[]{Key.Space}:new Key[0]);
            for(int i=0;i<70;i++)Step(false,default,true,raised?new[]{Key.Space}:new Key[0]);
        }
        public static void Run(IceResurfacerGame target,Keyboard keys,Mouse pointer)
        {
            game=target;keyboard=keys;mouse=pointer;passes.Clear();
            look=typeof(IceResurfacerGame).GetMethod("UpdateLook",BindingFlags.Instance|BindingFlags.NonPublic);
            var update=typeof(IceResurfacerGame).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            bool enabled=game.enabled;game.enabled=false;
            try
            {
                // Initial physical-layout captures intentionally set transforms. Use
                // the designated recenter input to restore the runtime head state.
                Step(false,default,true,Key.X);for(int i=0;i<15;i++)Step();
                Quaternion forward=Head.localRotation;
                Step(true,new Vector2(10000,-10000));Step(true,new Vector2(10000,-10000));
                Same(forward,"cursor lock acquisition deltas are discarded");
                Step(true,new Vector2(-1400,-400));Quaternion rear=Head.localRotation;
                Check(Mathf.DeltaAngle(0,Head.localEulerAngles.y)<-140,"RMB can look backward within existing limits");
                Step(false,new Vector2(8000,8000));
                Check(!game.Looking&&Cursor.lockState==CursorLockMode.None&&Cursor.visible,"release restores unlocked visible instrument cursor");
                for(int i=0;i<120;i++)Step(false,new Vector2(700,-600));
                Same(rear,"released rear view stays exact for 2.4 seconds despite cursor movement");
                Step(true,new Vector2(-9000,9000));Step(true,new Vector2(-9000,9000));
                Same(rear,"reacquiring RMB introduces no sudden head rotation");
                Step(true,new Vector2(10,0));
                Check(Vector3.Distance(rear*Vector3.forward,Head.localRotation*Vector3.forward)>.01f,"new relative mouse motion resumes from saved pose");
                Aim(40,70,false);Quaternion down=Head.localRotation;
                for(int i=0;i<70;i++)Step(false,new Vector2(500,100),true,Key.Space);
                Same(down,"raising viewpoint preserves downward head orientation");
                Check(Head.localPosition.y>.47f&&Head.localPosition.z>.095f,"Space raises physical eyes 0.48m and leans 0.10m forward");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space,Key.W,Key.D));InputSystem.Update();
                update.Invoke(game,null);
                Check(game.Sim.Controls.Throttle==1&&game.Sim.Controls.Steer==1&&!game.Sim.Controls.Brake,"raised stance retains WASD without applying service brake");
                Quaternion local=Head.localRotation,world=Head.rotation;
                Quaternion machine=game.World.Machine.rotation;
                game.World.Machine.rotation=Quaternion.Euler(0,37,0)*machine;
                Same(local,"turning vehicle preserves local head orientation");
                Check(Vector3.Distance(Head.forward,Quaternion.Euler(0,37,0)*(world*Vector3.forward))<.00001f,"vehicle turn naturally carries world viewpoint");
                game.World.Machine.rotation=machine;
                for(int i=0;i<100;i++)Step();
                Same(down,"returning to seated height preserves head orientation");
                Check(Mathf.Abs(Head.localPosition.y-game.operatorEyeHeight)<.001f&&Head.localPosition.z<.001f,"Space release smoothly returns to adjustable seated eyes");
                Step(false,default,true,Key.R);for(int i=0;i<40;i++)Step();
                Check(game.Standing&&Head.localPosition.y>.48f,"R toggles persistent raised stance after release");Same(down,"R standing preserves head orientation");
                Step(false,default,true,Key.R);for(int i=0;i<40;i++)Step();Check(!game.Standing,"second R sits down");
                game.TogglePause();Step(false,default,false);Same(down,"pause does not recenter");game.TogglePause();
                game.ToggleHelp();Step(false,default,false);Same(down,"guide does not recenter");game.ToggleHelp();
                typeof(IceResurfacerGame).GetMethod("OnApplicationFocus",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{false});
                Step(false,default,false);Same(down,"focus loss does not recenter");
                Aim(-155,60,true);Quaternion start=Head.localRotation;
                Step(false,default,true,Key.X,Key.Space);
                Check(Quaternion.Angle(Head.localRotation,Quaternion.Euler(5,0,0))>90&&Quaternion.Angle(start,Head.localRotation)>0,"X begins a smooth recenter without teleporting");
                for(int i=0;i<4;i++)Step(false,default,true,Key.Space);
                Check(Quaternion.Angle(Head.localRotation,Quaternion.Euler(5,0,0))>20,"recenter remains animated at 0.10 seconds");
                for(int i=0;i<5;i++)Step(false,default,true,Key.Space);
                Same(Quaternion.Euler(5,0,0),"recenter reaches forward after 0.20 seconds");
                Check(Head.localPosition.y>.47f,"recenter preserves raised stance");
                float blade=game.Sim.Controls.BladeDepthMm;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Period));InputSystem.Update();update.Invoke(game,null);
                Check(game.Sim.Controls.BladeDepthMm>blade,"period deepens blade without a camera-key conflict");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift,Key.W));InputSystem.Update();update.Invoke(game,null);
                Check(game.Sim.Controls.Throttle==1&&game.HeldQuicklook=="Rear"&&!game.ChaseActive,"Left Shift rear glance works with full W demand and is not precision throttle");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightShift,Key.W,Key.Digit1));InputSystem.Update();update.Invoke(game,null);
                Check(Mathf.Abs(game.Sim.Controls.Throttle-.42f)<.00001f&&game.Sim.Controls.ConditionerHydraulic==1&&game.HeldQuicklook=="","Right Shift retains precision and machinery modifier without rotating the camera");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();update.Invoke(game,null);
                Quicklooks();
                Aim(-100,45,false);Step(true);Step(true);Step(true,new Vector2(10000,-10000));
                Check(Mathf.Abs(Mathf.DeltaAngle(0,Head.localEulerAngles.y)-170)<.001f&&Mathf.Abs(Mathf.DeltaAngle(0,Head.localEulerAngles.x)-80)<.001f,"positive yaw and downward pitch limits preserved");
                Step(true,new Vector2(-20000,20000));
                Check(Mathf.Abs(Mathf.DeltaAngle(0,Head.localEulerAngles.y)+170)<.001f&&Mathf.Abs(Mathf.DeltaAngle(0,Head.localEulerAngles.x)+55)<.001f,"negative yaw and upward pitch limits preserved");
                Sightlines();
                WheelArticulation();
                game.NewSession(false);Same(Quaternion.Euler(5,0,0),"new session resets head forward");
                Check(Head.localPosition==Vector3.zero,"new session resets seated height");
                File.WriteAllLines("Assets/Experiments/IceResurfacer/Validation~/Camera-results.txt",passes);
            }
            finally { File.WriteAllLines("Assets/Experiments/IceResurfacer/Validation~/Camera-results.txt",passes);Step();game.enabled=enabled; }
        }
        static void Hold(Key key,int frames=70)
        { for(int i=0;i<frames;i++)Step(false,default,true,key); }
        static void Quicklooks()
        {
            foreach(var key in new[]{Key.Q,Key.E,Key.C,Key.LeftShift})
            {
                Aim(-137,42,false);Quaternion before=Head.localRotation;Vector3 position=Head.localPosition;
                Hold(key);
                Check(game.HeldQuicklook==(key==Key.Q?"Conditioner":key==Key.E?"Controls":key==Key.C?"Chase":"Rear"),key+" remains active while held");
                if(key==Key.C)
                {
                    Check(game.ChaseActive&&Head.parent!=game.World.CameraMount,"C is an external camera rather than a head rotation");
                    Vector3 local=game.World.Machine.InverseTransformPoint(Head.position);
                    Check(local.z<-3&&local.y>2.5f,"C frames the whole machine from above and behind ("+local+")");
                }
                else Check(Head.parent==game.World.CameraMount,"operator quicklook stays attached to physical seated eyes");
                if(key==Key.LeftShift)Check(!game.ChaseActive&&game.HeldQuicklook=="Rear"&&Mathf.Abs(Mathf.DeltaAngle(0,Head.localEulerAngles.y)+160)<.01f,"Left Shift is a rearward shoulder glance from the operator seat");
                if(key==Key.Q)Check(Mathf.Abs(Mathf.DeltaAngle(0,Head.localEulerAngles.y)+95)<.01f&&Head.localPosition.x<-.25f,"Q looks down at nine o'clock with a small left lean");
                passes.Add(key+" held pose: machine eyes="+game.World.Machine.InverseTransformPoint(Head.position)+" local angles="+Head.localEulerAngles);
                IceResurfacerEditor.Capture(game.DriverCamera,"Camera-preset-"+key+".png",1920,1080);
                Step();Check(Quaternion.Angle(before,Head.localRotation)>.5f,key+" release animates instead of snapping");
                for(int i=0;i<30;i++)Step();Same(before,key+" release restores the exact custom prior orientation");
                Check(Vector3.Distance(position,Head.localPosition)<.000001f,key+" release restores the exact prior eye position");
            }
            Aim(24,-16,false);Quaternion original=Head.localRotation;Vector3 originalPosition=Head.localPosition;
            Hold(Key.Q);for(int i=0;i<20;i++)Step(false,default,true,Key.Q,Key.E);
            Check(game.HeldQuicklook=="Controls","E has deterministic priority over held Q");
            for(int i=0;i<35;i++)Step(false,default,true,Key.Q,Key.E,Key.C);
            Check(game.ChaseActive,"C has deterministic priority over held Q and E");
            for(int i=0;i<25;i++)Step(false,default,true,Key.Q,Key.E);
            Check(game.HeldQuicklook=="Controls"&&!game.ChaseActive,"releasing C returns to the still-held E inspection");
            Hold(Key.Q,25);Check(game.HeldQuicklook=="Conditioner","releasing E returns to the still-held Q inspection");
            for(int i=0;i<25;i++)Step();Same(original,"overlapping holds retain the original normal view snapshot");
            Check(Vector3.Distance(originalPosition,Head.localPosition)<.000001f,"overlapping holds restore the original eye position");
            Hold(Key.C);game.RestoreSeatedCameraBeforeDismount();Same(original,"dismount preparation restores the actual normal seated orientation");
            Check(!game.ChaseActive&&Head.parent==game.World.CameraMount,"dismount preparation removes the external camera parent");
            Step();Hold(Key.Q);Step(false,default,true,Key.Q,Key.X);Hold(Key.Q,25);
            Same(Quaternion.Euler(5,0,0),"X cancels a held inspection and recenters until all inspection keys are released");
            Step();IceResurfacerEditor.Capture(game.DriverCamera,"Camera-preset-X.png",1920,1080);
            foreach(Vector2 orientation in new[]{new Vector2(0,5),new Vector2(160,12),new Vector2(-30,74)})
            foreach(Key key in new[]{Key.Q,Key.E,Key.C,Key.LeftShift})
            {
                Aim(orientation.x,orientation.y,false);Quaternion prior=Head.localRotation;Vector3 eyes=Head.localPosition;
                Hold(key,30);for(int i=0;i<15;i++)Step();
                Same(prior,key+" restores prior forward/rear/down orientation "+orientation);
                Check(Vector3.Distance(eyes,Head.localPosition)<.000001f,key+" restores prior forward/rear/down eyes "+orientation);
            }
            Aim(-120,30,false);Hold(Key.Q);Step(true,default,true,Key.Q);Step(true,default,true,Key.Q);Step(true);
            Quaternion returning=Head.localRotation;Step(true,new Vector2(10,0));
            Check(Quaternion.Angle(returning,Head.localRotation)<2,"RMB interrupts a return from its rendered pose without a stale-angle jump");Step();
            Aim(18,22,false);Quaternion stanceView=Head.localRotation;
            Hold(Key.E);Step(false,default,true,Key.E,Key.R);Hold(Key.E);
            for(int i=0;i<70;i++)Step();Same(stanceView,"R during E preserves the original head orientation on release");
            Check(game.Standing&&Head.localPosition.y>.47f,"R during E retains the requested standing height after inspection returns");
            Step(false,default,true,Key.R);for(int i=0;i<70;i++)Step();
            for(int i=0;i<70;i++)Step(false,default,true,Key.Space);
            for(int i=0;i<70;i++)Step(false,default,true,Key.Q,Key.Space);
            Hold(Key.Q);for(int i=0;i<70;i++)Step();Same(stanceView,"Space during Q preserves the original head orientation on release");
            Check(!game.Standing&&Mathf.Abs(Head.localPosition.y-game.operatorEyeHeight)<.001f,"releasing Space during Q returns to seated height without retaining a stale raised offset");
            ChaseWalls();
        }
        static void ChaseWalls()
        {
            var machine=game.World.Machine;Vector3 priorPosition=machine.position;Quaternion priorRotation=machine.rotation;
            try
            {
                // Reverse/turn paths intentionally exercise the side boards, end
                // boards and room back wall without modifying any vehicle physics.
                foreach(Vector3 pose in new[]{new Vector3(-11.5f,0,0),new Vector3(0,0,27),priorPosition})
                {
                    machine.position=pose;Hold(Key.C,75);
                    var camera=new IceChaseCamera(game);Check(camera.Clear(Head.position),"chase camera remains within real floor/wall envelope at "+pose);
                    Vector3 previous=Head.position;
                    bool clear=true,smooth=true;
                    for(int i=0;i<40;i++)
                    {
                        machine.rotation=Quaternion.Euler(0,i*.6f,0)*priorRotation;
                        Step(false,default,true,Key.C);
                        clear&=camera.Clear(Head.position);smooth&=Vector3.Distance(previous,Head.position)<1.2f;previous=Head.position;
                    }
                    Check(clear,"tight-turn chase remains within wall clearance through 40 consecutive frames at "+pose);
                    Check(smooth,"chase smooths tight-turn displacement through 40 consecutive frames at "+pose);
                    for(int i=0;i<15;i++)Step();machine.rotation=priorRotation;
                }
                machine.SetPositionAndRotation(new Vector3(-11.8f,0,0),Quaternion.identity);Aim(-137,42,false);Hold(Key.Q,30);
                machine.position=new Vector3(-11.9f,0,0);for(int i=0;i<20;i++)Step();
                Check(game.Sim.Ice.IsInside(Head.position.x,Head.position.z,.079f),"restored shoulder eyes stay inside the boards when the vehicle moves during inspection");
            }
            finally {machine.SetPositionAndRotation(priorPosition,priorRotation);for(int i=0;i<20;i++)Step();}
        }
        static void Sightlines()
        {
            Aim(0,5,false);IceResurfacerEditor.Capture(game.DriverCamera,"Operator-seated.png",1920,1080);
            // Aim through the near part of the opening; a shallow center ray would
            // strike the far wall before reaching the nearly empty snow surface.
            Aim(21,29,true);IceResurfacerEditor.Capture(game.DriverCamera,"Operator-standing-window.png",1920,1080);
            var colliders=new List<MeshCollider>();
            try
            {
                foreach(var root in new[]{game.World.Machine,game.World.Conditioner})
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var filter=renderer.GetComponent<MeshFilter>();
                    if(!renderer.enabled||filter==null||filter.sharedMesh==null)continue;
                    var collider=renderer.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;colliders.Add(collider);
                }
                Physics.SyncTransforms();
                int visibleSnow=0;RaycastHit hit=default;string firstHit="none";
                for(int x=0;x<5;x++)for(int z=0;z<5;z++)
                {
                    Vector3 opening=game.World.TankPivot.TransformPoint(game.Sim.Config.TankGeometryX(-.34f+x*.09f),game.Sim.Config.TankGeometryY(1.06f),-1.94f+z*.12f);
                    if(Physics.Raycast(Head.position,(opening-Head.position).normalized,out hit,6))
                    {firstHit=hit.transform.name;if(hit.transform==game.World.TankSnow)visibleSnow++;}
                }
                Check(visibleSnow>=2,"standing opening exposes actual snow over multiple rays ("+visibleSnow+"/25; last hit "+firstHit+")");
                Hold(Key.Q);Physics.SyncTransforms();
                Vector3 edge=game.World.Conditioner.TransformPoint(-game.Sim.Config.WaterDistributionWidth*.5f-.007f,.075f,-.1f);
                Check(Physics.Raycast(Head.position,(edge-Head.position).normalized,out hit,4)&&hit.transform.IsChildOf(game.World.Conditioner),"seated shoulder view sees outer conditioner plate without solid obstruction");
                Vector3 edgeScreen=game.DriverCamera.WorldToViewportPoint(edge),brushScreen=game.DriverCamera.WorldToViewportPoint(game.World.Brush.position);
                passes.Add("Q frame: actual outer conditioner="+edgeScreen+"; board brush="+brushScreen+" (viewport coordinates).");
                Check(edgeScreen.z>0&&edgeScreen.x>.02f&&edgeScreen.x<.98f&&edgeScreen.y>.02f&&edgeScreen.y<.98f,"Q includes the actual outer conditioner edge in the frame");
                Check(brushScreen.z>0&&brushScreen.x>.02f&&brushScreen.x<.98f&&brushScreen.y>.02f&&brushScreen.y<.98f,"Q includes the actual board brush in the same frame");
                IceResurfacerEditor.Capture(game.DriverCamera,"Operator-board-side-edge.png",1920,1080);
                Hold(Key.E);
                Physics.SyncTransforms();
                Vector3 hub=game.World.BladeWheel.TransformPoint(0,.034f,0);
                Check(Physics.Raycast(Head.position,(hub-Head.position).normalized,out hit,3)&&hit.transform.IsChildOf(game.World.BladeWheel),"E preset exposes actual blade hub without seat or manifold obstruction");
                int visibleRim=0;
                for(int i=0;i<8;i++)
                {
                    float angle=i*Mathf.PI/4;
                    Vector3 rim=game.World.BladeWheel.TransformPoint(Mathf.Cos(angle)*.13f,.012f,Mathf.Sin(angle)*.13f);
                    if(Physics.Raycast(Head.position,(rim-Head.position).normalized,out hit,3)&&hit.transform.IsChildOf(game.World.BladeWheel))visibleRim++;
                }
                Check(visibleRim>=6,"E preset exposes blade rim for mouse operation ("+visibleRim+"/8 rays)");
                float last=-1;
                foreach(var lever in new[]{game.World.VerticalAugerLever,game.World.ConditionerLever,game.World.HorizontalAugerLever,game.World.DumpLever})
                {
                    Vector3 knob=lever.TransformPoint(0,.225f,0),screen=game.DriverCamera.WorldToViewportPoint(knob);
                    Check(screen.z>0&&screen.x>last&&screen.x<.98f&&screen.y>.02f&&screen.y<.98f,"physical lever order from the seated E view: "+lever.name);last=screen.x;
                    Check(Physics.Raycast(Head.position,(knob-Head.position).normalized,out hit,3)&&hit.transform.IsChildOf(lever),"E has an unobstructed ray to "+lever.name);
                }
            }
            finally { foreach(var collider in colliders)UnityEngine.Object.DestroyImmediate(collider); }
        }
        static void WheelArticulation()
        {
            for(int i=0;i<25;i++)Step();Aim(0,5,false);
            var w=game.World;var s=game.Sim;
            passes.Add("Wheel sightline camera: machine-local eyes="+w.Machine.InverseTransformPoint(Head.position).ToString("F3")+", head angles="+Head.localEulerAngles.ToString("F3")+", vertical FOV="+game.DriverCamera.fieldOfView.ToString("F1"));
            var render=typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic);
            float oldSteering=s.Vehicle.SteeringAngle,oldSpeed=s.Vehicle.Speed;
            var colliders=new List<MeshCollider>();
            var probeObject=new GameObject("Validation only / tire clearance probe");
            var probe=probeObject.AddComponent<SphereCollider>();probe.radius=.003f;
            try
            {
                foreach(var renderer in w.Machine.GetComponentsInChildren<MeshRenderer>())
                {
                    var filter=renderer.GetComponent<MeshFilter>();if(!renderer.enabled||filter==null||filter.sharedMesh==null)continue;
                    var collider=renderer.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;colliders.Add(collider);
                }
                bool allVisible=true;
                foreach(int steer in new[]{-1,0,1})
                {
                    s.Vehicle.SteeringAngle=steer*s.Config.MaxSteeringRadians;s.Vehicle.Speed=0;render.Invoke(game,new object[]{0f});Physics.SyncTransforms();
                    for(int i=0;i<2;i++)
                    {
                        var wheel=w.FrontWheels[i];Vector3 expected=Quaternion.Euler(0,s.Vehicle.SteeringAngle*Mathf.Rad2Deg,0)*Vector3.right;
                        Check(Vector3.Angle(w.Machine.InverseTransformDirection(wheel.right),expected)<.01f,"front wheel "+i+" steering matches authoritative angle at "+steer);
                        Check(Mathf.Abs(wheel.localPosition.x-(i==0?-1:1)*s.Config.WheelTrack*.5f)<.00001f&&Mathf.Abs(wheel.localPosition.z-s.Config.Wheelbase)<.00001f,"front wheel "+i+" preserves physical track and wheelbase");
                        for(int sample=0;sample<24;sample++)for(int side=-1;side<=1;side+=2)
                        {
                            float a=sample*Mathf.PI/12;
                            Vector3 tire=wheel.TransformPoint(side*s.Config.TireWidth*.45f,Mathf.Cos(a)*.39f,Mathf.Sin(a)*.39f);
                            foreach(var collider in colliders)
                            {
                                if(collider.transform.IsChildOf(wheel))continue;
                                if(Physics.ComputePenetration(probe,tire,Quaternion.identity,collider,collider.transform.position,collider.transform.rotation,out Vector3 direction,out float depth)&&depth>.0031f)
                                    throw new InvalidOperationException("Camera: tire swept surface intersects "+collider.name+" at steering "+steer+", wheel "+i+", depth "+depth);
                            }
                        }
                        passes.Add("PASS actual front tire swept surface clears machine meshes at steering "+steer+", wheel "+i);
                    }
                    int visible=0;var left=w.FrontWheels[0];
                    for(int i=0;i<12;i++)
                    {
                        float a=i*Mathf.PI/6;Vector3 point=left.TransformPoint(-s.Config.TireWidth*.51f,Mathf.Cos(a)*.35f,Mathf.Sin(a)*.35f);
                        Vector3 screen=game.DriverCamera.WorldToViewportPoint(point);
                        bool framed=screen.z>0&&screen.x>0&&screen.x<1&&screen.y>0&&screen.y<1;
                        bool struck=Physics.Raycast(Head.position,(point-Head.position).normalized,out RaycastHit hit,5);
                        if(framed&&struck&&hit.transform.IsChildOf(left))visible++;
                        passes.Add("Tire sightline steering="+steer+", ray="+i+", machine-local target="+w.Machine.InverseTransformPoint(point).ToString("F3")+", viewport="+screen.ToString("F3")+", first hit="+(struck?hit.transform.name:"none")+", own tire="+(struck&&hit.transform.IsChildOf(left)));
                    }
                    passes.Add("Outer-face silhouette probes at steering "+steer+": "+visible+"/12. These targets lie outside the carcass; rays may miss it and hit fittings beyond the wheel.");
                    // Sample genuine upper tread across its width. The seated eye
                    // lies inboard of the narrow tire's outer face; aiming outside
                    // that face is not a test of visible rubber. The old 5m rays
                    // could miss it and report a headlamp behind the intended target.
                    int crownVisible=0;var obstructions=new Dictionary<string,int>();
                    Quaternion steeringFrame=w.Machine.rotation*Quaternion.Euler(0,s.Vehicle.SteeringAngle*Mathf.Rad2Deg,0);
                    for(int across=-1;across<=1;across++)for(int arc=-6;arc<=6;arc++)
                    {
                        float a=arc*Mathf.PI/12;
                        Vector3 point=left.position+steeringFrame*new Vector3(across*s.Config.TireWidth*.35f,Mathf.Cos(a)*.375f,Mathf.Sin(a)*.375f);
                        Vector3 screen=game.DriverCamera.WorldToViewportPoint(point),direction=point-Head.position;
                        bool framed=screen.z>0&&screen.x>0&&screen.x<1&&screen.y>0&&screen.y<1;
                        bool struck=Physics.Raycast(Head.position,direction.normalized,out RaycastHit hit,direction.magnitude+.005f);
                        if(framed&&struck&&hit.transform.IsChildOf(left))crownVisible++;
                        else
                        {
                            string reason=!framed?"outside frame":struck?hit.transform.name:"no tire hit before target";
                            obstructions.TryGetValue(reason,out int count);obstructions[reason]=count+1;
                            if(framed&&struck&&count==0)passes.Add("First crown obstruction steering="+steer+": "+reason+", hit="+w.Machine.InverseTransformPoint(hit.point).ToString("F3")+", target="+w.Machine.InverseTransformPoint(point).ToString("F3"));
                        }
                    }
                    IceResurfacerEditor.Capture(game.DriverCamera,"Operator-steering-"+(steer<0?"left":steer>0?"right":"center")+".png",1920,1080);
                    // The same seated eye can naturally glance down to inspect
                    // tread articulation. Keep both this view and the default
                    // forward capture; never change seat position or wheel angles.
                    Aim(-4,21,false);
                    IceResurfacerEditor.Capture(game.DriverCamera,"Operator-steering-"+(steer<0?"left":steer>0?"right":"center")+"-inspection.png",1920,1080);
                    Aim(0,5,false);
                    allVisible&=crownVisible>=6;
                    passes.Add((crownVisible>=6?"PASS ":"FAIL ")+"seated front-left upper tread has unoccluded visible surface at steering "+steer+" ("+crownVisible+"/39 actual-tire rays)");
                    foreach(var obstruction in obstructions)passes.Add("Crown nonvisible steering="+steer+": "+obstruction.Key+" = "+obstruction.Value);
                    if(steer==0)
                    {
                        Bounds body=MeshEnvelope(w.Machine),conditioner=MeshEnvelope(w.Conditioner);
                        float envelopeRear=Mathf.Min(body.min.z,-s.Config.HitchBehindAxle-s.Config.DrawbarLength+conditioner.min.z);
                        passes.Add("Measured mesh envelope with centered wheels: body="+body.size.ToString("F3")+"m, body forward="+body.max.z.ToString("F3")+"m; conditioner="+conditioner.size.ToString("F3")+"m; aligned overall length="+(body.max.z-envelopeRear).ToString("F3")+"m. Includes physical fittings; not a manufacturer-envelope claim.");
                    }
                }
                Check(allVisible,"all three seated steering poses expose at least six real upper-tread rays; see per-pose obstruction and viewport diagnostics above");
                var before=w.FrontWheels[0].localRotation;var rear=w.RearWheels[0].localRotation;
                s.Vehicle.Speed=.7f;render.Invoke(game,new object[]{.2f});
                Check(Quaternion.Angle(before,w.FrontWheels[0].localRotation)>15&&Quaternion.Angle(rear,w.RearWheels[0].localRotation)>15,"front and rear tires roll under actual vehicle motion");
            }
            finally {s.Vehicle.SteeringAngle=oldSteering;s.Vehicle.Speed=oldSpeed;render.Invoke(game,new object[]{0f});foreach(var collider in colliders)UnityEngine.Object.DestroyImmediate(collider);UnityEngine.Object.DestroyImmediate(probeObject);}
        }
        static Bounds MeshEnvelope(Transform root)
        {
            Bounds result=default;bool started=false;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                if(filter.sharedMesh==null||renderer==null||!renderer.enabled)continue;
                foreach(Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point=root.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if(!started){result=new Bounds(point,Vector3.zero);started=true;}else result.Encapsulate(point);
                }
            }
            return result;
        }
    }
}
