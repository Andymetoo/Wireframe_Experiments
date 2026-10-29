using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using IceResurfacer.Presentation;
using UnityEngine;

namespace IceResurfacer.Editor
{
    internal static class IceOperationValidation
    {
        const string Output="Assets/Experiments/IceResurfacer/Validation~/";
        static readonly List<string> observations=new List<string>();
        static void Check(bool value,string message)
        { if(!value)throw new InvalidOperationException("Operations: "+message);observations.Add("PASS "+message); }
        public static void Run(IceResurfacerGame game)
        {
            observations.Clear();game.enabled=false;
            var update=typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic);
            var render=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),game,update);
            ValidateSideEscape(game,render);
            game.NewSession(true);
            var s=game.Sim;var c=s.Controls;
            c.HydraulicHoldControls=false; // legacy deterministic route fixture; live controls use hold commands.
            // Explicit rendering stress fixture: seed only the initial tank stock.
            // All subsequent flow, spill, jam and dump use the real simulation.
            // This is NOT evidence that normal play fills the tank this quickly.
            typeof(Core.Simulation).GetProperty("SnowTankM3").SetValue(s,s.Config.SnowCapacityM3*.99999f);
            observations.Add("Rendering fixture begins with 99.999% tank stock; default 3.54m3 capacity, geometry and transfer rates. Core conservation/recovery tests use no tank writes.");
            s.Vehicle.X=0;s.Vehicle.Z=-15;s.Conditioner.X=0;s.Conditioner.Z=-16.1f;
            s.Conditioner.Lift=0;c.ConditionerTarget=0;c.HorizontalAuger=c.VerticalAuger=true;
            c.BladeDepthMm=.8f;c.Throttle=.42f;
            bool spilling=false;
            for(int i=0;i<600;i++)
            { s.Step(.02f);if(s.VerticalSpillRate>0){spilling=true;break;} }
            for(int i=0;i<50;i++)s.Step(.02f);
            render(.02f);game.Surface.Refresh(s);
            Check(spilling&&s.SnowTankM3<=s.Config.SnowCapacityM3,"full tank ejects actual rejected vertical throughput");
            var snowRenderer=game.World.TankSnow.GetComponent<MeshRenderer>();
            var snowMesh=game.World.TankSnow.GetComponent<MeshFilter>().sharedMesh;
            Check(snowRenderer.enabled&&snowMesh.bounds.max.y>.88f,"full tank surface reaches inspection opening");
            IceCameraValidation.Aim(21,29,true);
            foreach(var ps in game.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Simulate(.5f,true,false);
                if(ps.name.Contains("Congested"))Check(ps.particleCount>0,"overflow emits visible pooled particles from rejected source snow");
            }
            IceResurfacerEditor.Capture(game.DriverCamera,"Operator-full-tank-window.png",1920,1080);
            var cameraObject=new GameObject("Explicit operation render inspection");var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.fieldOfView=53;camera.nearClipPlane=.05f;camera.farClipPlane=150;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.13f,.18f);
            camera.transform.position=game.World.Machine.TransformPoint(-3.8f,3.5f,-3.5f);
            camera.transform.LookAt(game.World.Machine.TransformPoint(0,1.1f,.55f));
            IceResurfacerEditor.Capture(camera,"Snow-overflow.png",1920,1080);
            for(int i=0;i<400;i++)s.Step(.02f);
            render(.02f);Check(s.ConveyorJammed,"continued full-tank operation latches a mechanical jam");
            var auger=game.World.VerticalAuger.localRotation;render(.2f);
            Check(Quaternion.Angle(auger,game.World.VerticalAuger.localRotation)<.01f,"jammed auger flights stop rotating");
            c.BladeDepthMm=0;c.Throttle=0;c.Brake=true;c.HorizontalAuger=c.VerticalAuger=false;c.ConditionerTarget=1;
            for(int i=0;i<250;i++)s.Step(.02f);
            render(.02f);
            foreach(var ps in game.GetComponentsInChildren<ParticleSystem>())
            {
                if(ps.name.Contains("conveyed")||ps.name.Contains("Congested"))
                    Check(ps.emission.rateOverTime.constant==0,"stopped augers cease emission: "+ps.name);
                Check(ps.main.maxParticles<=360,"bounded particle pool: "+ps.name);
            }
            s.Layout.ToWorld(s.Layout.PitSide,s.Layout.PitOutward-3.85f,out s.Vehicle.X,out s.Vehicle.Z);s.Vehicle.Heading=s.Layout.OutwardHeading;s.Vehicle.Speed=0;
            s.Conditioner.Heading=s.Vehicle.Heading;s.Conditioner.X=s.Vehicle.X-Mathf.Sin(s.Vehicle.Heading)*1.1f;s.Conditioner.Z=s.Vehicle.Z-Mathf.Cos(s.Vehicle.Heading)*1.1f;
            c.TankLiftTarget=1;
            float full=s.SnowTankM3;
            for(int i=0;i<1000&&s.DumpedSnowM3<full*.35f;i++)s.Step(.02f);
            render(.02f);float partial=s.SnowTankM3;
            foreach(var ps in game.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Simulate(.6f,true,false);
                if(ps.name.Contains("unloading"))Check(ps.particleCount>0,"dump emits visible pooled particles while tank volume decreases");
            }
            Check(partial>0&&partial<full&&s.DumpSnowRate>0,"tilted tank visibly empties progressively with a live discharge rate");
            Check(game.World.TankDoor.localEulerAngles.x>180,"forward dump door opens independently of shell");
            var pitMesh=game.World.PitSnow.GetComponent<MeshFilter>().sharedMesh;
            Check(pitMesh.bounds.max.y>.1f,"actual removed tank contents form a visible pit pile");
            camera.transform.position=game.World.Machine.TransformPoint(3.8f,3.2f,3.5f);camera.transform.LookAt(game.World.Machine.TransformPoint(0,1.1f,2.8f));
            IceResurfacerEditor.Capture(camera,"Snow-dumping.png",1920,1080);
            for(int i=0;i<700;i++)s.Step(.02f);
            for(int cycle=0;cycle<6;cycle++)
            {
                s.SetTankHydraulic(-1);for(int i=0;i<18;i++)s.Step(.02f);
                s.SetTankHydraulic(1);for(int i=0;i<18;i++)s.Step(.02f);
                s.SetTankHydraulic(0);for(int i=0;i<25;i++)s.Step(.02f);
            }
            render(.02f);Check(s.SnowTankM3==0&&!snowRenderer.enabled,"empty tank hides internal snow surface");
            s.SetTankHydraulic(-1);for(int i=0;i<300;i++)s.Step(.02f);s.SetTankHydraulic(0);render(.02f);
            Check(s.TankLift==0,"dump tank returns fully to lowered position");
            IceResurfacerEditor.Capture(camera,"Snow-dumped-pit.png",1920,1080);
            UnityEngine.Object.Destroy(cameraObject);
            s.TryClearConveyorJam();s.Vehicle.X=0;s.Vehicle.Z=-15;s.Vehicle.Heading=0;s.Conditioner.X=0;s.Conditioner.Z=-16.1f;s.Conditioner.Heading=0;
            s.Conditioner.Lift=0;c.ConditionerTarget=0;c.HorizontalAuger=c.VerticalAuger=true;c.Throttle=.42f;c.Brake=false;c.BladeDepthMm=.35f;
            for(int i=0;i<50;i++){s.Step(.02f);render(.02f);}
            // Whole CPU sample includes the live cut/snow mesh and two streaming
            // texture uploads every 8 ticks. It excludes IMGUI, GPU and display.
            var watch=new Stopwatch();long allocated=GC.GetAllocatedBytesForCurrentThread();watch.Start();
            for(int i=0;i<240;i++){s.Step(.02f);render(.02f);if(i%8==0)game.Surface.Refresh(s);}
            watch.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            observations.Add("Working simulation + presentation CPU sample (240 ticks): "+(watch.Elapsed.TotalMilliseconds/240).ToString("F3")+" ms/tick; managed allocation="+allocated+" bytes; 663 reusable snow vertices, five 360-particle snow pools. Excludes IMGUI/GPU.");
            IceCameraValidation.Aim(21,29,true);
            foreach(var ps in game.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Simulate(.35f,true,false);
                if(ps.name.Contains("conveyed"))Check(ps.particleCount>0,"working auger emits actual snow into the bin");
            }
            IceResurfacerEditor.Capture(game.DriverCamera,"Operator-auger-discharge.png",1920,1080);
            File.WriteAllLines(Output+"Operations-results.txt",observations);
        }

        static void PlaceSnowPass(Core.Simulation s,float x,float z,float blade,bool augers)
        {
            s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=0;s.Vehicle.Speed=s.Vehicle.SteeringAngle=0;
            s.Conditioner.X=x;s.Conditioner.Z=z-s.Config.HitchBehindAxle-s.Config.DrawbarLength;s.Conditioner.Heading=0;
            s.Conditioner.Lift=0;s.Controls.HydraulicHoldControls=false;s.Controls.ConditionerTarget=0;
            s.Controls.BladeDepthMm=blade;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=augers;
            s.Controls.Throttle=.38f;s.Controls.Steer=s.Controls.FloodFlow=s.Controls.WashValve=0;s.Controls.Brake=false;
        }
        static double SurfaceSnow(Core.Simulation s)
        {double value=0;foreach(var cell in s.Ice.Cells)value+=cell.SnowMm*s.Ice.CellSize*s.Ice.CellSize*.001;return value;}
        static void ValidateSideEscape(IceResurfacerGame game,Action<float> render)
        {
            game.NewSession(true);var s=game.Sim;PlaceSnowPass(s,0,-20,.8f,false);
            observations.Add("Side-escape fixture explicitly sets an initial center-ice pose; subsequent motion, cutting, accumulation and recovery use simulation ticks. No tank or snow is injected.");
            bool earlyRetained=false;
            for(int i=0;i<2300&&s.Vehicle.Z<19;i++)
            {
                s.Step(.02f);
                if(s.HorizontalSnowM3>.01f&&s.HorizontalSnowM3<s.Config.ConditionerSnowCapacityM3*.8f&&s.SpilledSnowM3==0)earlyRetained=true;
            }
            render(.02f);game.Surface.Refresh(s);
            Check(earlyRetained,"stopped augers visibly retain initial finite trough stock before any snow escape");
            Check(s.ConditionerLeftEscapeRate>0&&s.ConditionerRightEscapeRate>0,"shaving with horizontal auger OFF has simultaneous left/right material escape");
            Check(s.SnowTankM3==0&&s.HorizontalSnowM3<=s.Config.ConditionerSnowCapacityM3,"off-auger fixture cannot hide escaped snow in the tank or exceed trough capacity");
            int sidePools=0;
            foreach(var ps in game.GetComponentsInChildren<ParticleSystem>())if(ps.name.Contains("side escape"))
            {
                ps.Simulate(.3f,true,false);sidePools++;
                Check(ps.particleCount>0&&ps.main.maxParticles<=360,"bounded source-driven side snow particles: "+ps.name);
            }
            Check(sidePools==2,"exactly two pooled conditioner-side emitters exist");
            var trough=game.World.Conditioner.Find("Snow bunching in the finite conditioner trough");
            Check(trough!=null&&trough.GetComponent<MeshRenderer>().enabled,"finite accumulated trough snow is rendered behind the auger grille");
            double before=SurfaceSnow(s),escaped=s.SpilledSnowM3;float end=s.Vehicle.Z;
            var cameraObject=new GameObject("Snow side-escape evidence camera");var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.fieldOfView=55;camera.nearClipPlane=.04f;camera.farClipPlane=100;
            camera.transform.position=game.World.Conditioner.TransformPoint(-3.7f,2.3f,-4.2f);
            camera.transform.LookAt(game.World.Conditioner.position+Vector3.up*.12f);
            IceResurfacerEditor.Capture(camera,"Snow-disabled-augers-both-sides.png",1920,1080);
            camera.orthographic=true;camera.orthographicSize=6;
            camera.transform.position=new Vector3(0,22,end-6);camera.transform.rotation=Quaternion.Euler(90,0,0);
            IceResurfacerEditor.Capture(camera,"Snow-windrows-before-recovery.png",1920,1080);
            for(int pass=0;pass<2;pass++)for(int side=-1;side<=1;side+=2)
            {
                PlaceSnowPass(s,side*1.265f,-20,0,true);
                for(int i=0;i<2200&&s.Vehicle.Z<end;i++)s.Step(.02f);
            }
            s.Controls.BladeDepthMm=s.Controls.Throttle=0;s.Controls.Brake=true;
            for(int i=0;i<600;i++)s.Step(.02f);
            render(.02f);game.Surface.Refresh(s);
            double recovered=before-SurfaceSnow(s);
            Check(recovered>escaped*.8&&s.SnowTankM3>escaped,"enabled augers recover persistent side windrows with the blade retracted");
            Check(s.ConditionerLeftEscapeRate==0&&s.ConditionerRightEscapeRate==0,"side emission stops when actual escaping transfer stops");
            IceResurfacerEditor.Capture(camera,"Snow-windrows-after-recovery.png",1920,1080);
            observations.Add("Live side fixture escaped "+escaped.ToString("F6")+" m3; subsequent surface pickup removed "+recovered.ToString("F6")+" m3 including incidental original loose snow. Detailed conservation and empty-revisit assertions are in the core snow probe.");
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
