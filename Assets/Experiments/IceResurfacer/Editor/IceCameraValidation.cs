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
                foreach(var key in new[]{Key.Q,Key.E,Key.C,Key.X})
                {
                    Step();Step(false,default,true,key);for(int i=0;i<20;i++)Step();
                    var preset=Head.localRotation;for(int i=0;i<25;i++)Step(false,new Vector2(50,50));Same(preset,key+" preset persists after release");
                    Check(Mathf.Abs(Mathf.DeltaAngle(0,Head.localEulerAngles.y))<=170.01f,key+" respects head yaw limit");
                    passes.Add(key+" calibrated pose: local eyes="+Head.localPosition+" angles="+Head.localEulerAngles);
                    IceResurfacerEditor.Capture(game.DriverCamera,"Camera-preset-"+key+".png",1920,1080);
                }
                Aim(-100,45,false);Step(true);Step(true);Step(true,new Vector2(10000,-10000));
                Check(Mathf.Abs(Mathf.DeltaAngle(0,Head.localEulerAngles.y)-170)<.001f&&Mathf.Abs(Mathf.DeltaAngle(0,Head.localEulerAngles.x)-80)<.001f,"positive yaw and downward pitch limits preserved");
                Step(true,new Vector2(-20000,20000));
                Check(Mathf.Abs(Mathf.DeltaAngle(0,Head.localEulerAngles.y)+170)<.001f&&Mathf.Abs(Mathf.DeltaAngle(0,Head.localEulerAngles.x)+55)<.001f,"negative yaw and upward pitch limits preserved");
                Sightlines();
                game.NewSession(false);Same(Quaternion.Euler(5,0,0),"new session resets head forward");
                Check(Head.localPosition==Vector3.zero,"new session resets seated height");
                File.WriteAllLines("Assets/Experiments/IceResurfacer/Validation~/Camera-results.txt",passes);
            }
            finally { Step();game.enabled=enabled; }
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
                    Vector3 opening=game.World.TankPivot.TransformPoint(-.49f+x*.10f,1.06f,-1.94f+z*.12f);
                    if(Physics.Raycast(Head.position,(opening-Head.position).normalized,out hit,6))
                    {firstHit=hit.transform.name;if(hit.transform==game.World.TankSnow)visibleSnow++;}
                }
                Check(visibleSnow>=2,"standing opening exposes actual snow over multiple rays ("+visibleSnow+"/25; last hit "+firstHit+")");
                Aim(-170,72,false);Physics.SyncTransforms();
                Vector3 edge=game.World.Conditioner.TransformPoint(-game.Sim.Config.WaterDistributionWidth*.5f-.007f,.075f,-.1f);
                Check(Physics.Raycast(Head.position,(edge-Head.position).normalized,out hit,4)&&hit.transform.IsChildOf(game.World.Conditioner),"seated shoulder view sees outer conditioner plate without solid obstruction");
                IceResurfacerEditor.Capture(game.DriverCamera,"Operator-board-side-edge.png",1920,1080);
                Step();Step(false,default,true,Key.E);for(int i=0;i<30;i++)Step();
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
            }
            finally { foreach(var collider in colliders)UnityEngine.Object.DestroyImmediate(collider); }
        }
    }
}
