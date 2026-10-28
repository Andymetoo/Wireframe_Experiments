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
            observations.Add("Working simulation + presentation CPU sample (240 ticks): "+(watch.Elapsed.TotalMilliseconds/240).ToString("F3")+" ms/tick; managed allocation="+allocated+" bytes; 442 dynamic snow vertices, four 360-particle snow pools. Excludes IMGUI/GPU.");
            IceCameraValidation.Aim(21,29,true);
            foreach(var ps in game.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Simulate(.35f,true,false);
                if(ps.name.Contains("conveyed"))Check(ps.particleCount>0,"working auger emits actual snow into the bin");
            }
            IceResurfacerEditor.Capture(game.DriverCamera,"Operator-auger-discharge.png",1920,1080);
            File.WriteAllLines(Output+"Operations-results.txt",observations);
        }
    }
}
