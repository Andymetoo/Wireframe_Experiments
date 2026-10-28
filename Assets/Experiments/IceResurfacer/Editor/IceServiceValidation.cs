using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Editor
{
    internal static class IceServiceValidation
    {
        static IceResurfacerGame game;
        static IceWaterService service;
        static Keyboard keyboard;
        static Mouse mouse;
        static readonly List<string> results=new List<string>();
        static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException("Water service: "+message);results.Add("PASS "+message);}
        static void Input(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f)});
            InputSystem.Update();service.HandleInput(true,.02f);service.UpdateViewAndEffects(true,.02f);
        }
        static void Pulse(Key key){Input();Input(key);Input();}
        static void Walk(int ticks,params Key[] keys){for(int i=0;i<ticks;i++)Input(keys);Input();}
        static void Tick(float seconds)
        {for(int i=0;i<(int)Math.Round(seconds/.02f);i++){game.Sim.Step(.02f);service.UpdateViewAndEffects(true,.02f);}}
        static void Aim(Vector3 target)
        {
            Quaternion rotation=Quaternion.LookRotation(target-game.DriverCamera.transform.position);
            typeof(IceWaterService).GetField("footYaw",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,rotation.eulerAngles.y);
            typeof(IceWaterService).GetField("footPitch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,Mathf.DeltaAngle(0,rotation.eulerAngles.x));
            service.UpdateViewAndEffects(true,.02f);Input();
        }
        static void Capture(string file,Vector3 position,Vector3 target)
        {
            var go=new GameObject("Water service evidence camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.fieldOfView=58;camera.nearClipPlane=.04f;camera.farClipPlane=150;
            camera.transform.position=position;camera.transform.LookAt(target);
            IceResurfacerEditor.Capture(camera,file,1920,1080);UnityEngine.Object.Destroy(go);
        }
        public static void Run(IceResurfacerGame target,Keyboard keys,Mouse pointer)
        {
            game=target;keyboard=keys;mouse=pointer;service=game.WaterService;results.Clear();
            bool enabled=game.enabled;game.enabled=false;
            try
            {
                game.NewSession(true,true);
                Check(service!=null,"interactive service station initialized in launch scene");
                Check(game.Sim.FloodWaterLiters==738&&game.Sim.WashWaterLiters==310,"normal session starts full");
                Check(game.Sim.ConfigureWaterForServiceTest(.90f,.8f),"explicit practice partial-tank fixture");
                service.UpdateViewAndEffects(true,.21f);
                Capture("Service-bay.png",new Vector3(-3.7f,3.0f,-39),new Vector3(1.0f,1.0f,-34.6f));
                Pulse(Key.F);
                Check(game.Sim.OnFoot&&!game.DriverCamera.transform.IsChildOf(game.World.CameraMount),"F safely dismounts to grounded service camera");
                Check(Mathf.Abs(game.DriverCamera.transform.position.y-1.68f)<.001f,"on-foot viewpoint remains at grounded standing height");
                // Walk round the rear of the vehicle using the actual keyboard path.
                // Initial foot yaw is +90 degrees: D walks south, W east, A north.
                Walk(70,Key.D);Walk(148,Key.W);Walk(28,Key.A);
                Check(service.FootPosition.x>3.5f&&service.FootPosition.z< -35.0f,"WASD reaches wall station around solid machine footprint");
                Aim(new Vector3(4.04f,1.15f,-35.98f));Pulse(Key.E);
                Check(game.Sim.HoseState==WaterHoseState.Carried,"E takes physical wall nozzle");
                Check(service.HoseVisibleLength>1&&service.HoseVisibleLength<=Simulation.ServiceHoseLengthMeters,"carried hose has bounded physical route");
                // Aim north to walk toward the right-side pair of machine inlets.
                Aim(service.FootPosition+new Vector3(0,1.68f,4));Walk(58,Key.W);Walk(30,Key.A);
                Aim(service.FloodInlet.position);Pulse(Key.E);
                Check(game.Sim.HoseState==WaterHoseState.Flood,"physical flood inlet couples independently");
                Check(Vector3.Distance(service.HoseNozzle.position,service.FloodInlet.position)<.001f,"visible nozzle ends at connected inlet");
                Capture("Service-hose-connected.png",new Vector3(4.05f,2.7f,-37.8f),new Vector3(1.3f,1.0f,-33.2f));
                // Walk back to the wall valve; the connected hose remains attached.
                Aim(service.FootPosition+new Vector3(0,1.68f,-4));Walk(27,Key.W);Walk(28,Key.A);
                Aim(new Vector3(4.02f,1.25f,-34.78f));Pulse(Key.E);
                Check(game.Sim.SupplyValve>.99f,"E opens real wall supply valve");
                float before=game.Sim.FloodWaterLiters,washBefore=game.Sim.WashWaterLiters;Tick(3);
                Check(game.Sim.FloodWaterLiters>before+35&&game.Sim.WashWaterLiters==washBefore,"connected flood tank fills at measured 12 L/s; wash remains unchanged");
                Capture("Service-filling.png",new Vector3(3.9f,1.9f,-35.5f),new Vector3(1.2f,1.05f,-32.9f));
                Tick(5);
                Check(game.Sim.FloodWaterLiters==738&&game.Sim.ServiceOverflowLitersPerSecond>11.9f,"full tank stores no excess; real overflow continues");
                foreach(var ps in service.GetComponentsInChildren<ParticleSystem>())ps.Simulate(.4f,true,false);
                var overflow=service.OverflowParticles;
                Check(overflow!=null&&overflow.emission.rateOverTime.constant>100&&overflow.main.maxParticles==160,"overflow emits a bounded visible stream");
                var droplets=overflow.GetComponent<ParticleSystemRenderer>();
                Check(droplets.renderMode==ParticleSystemRenderMode.Mesh&&droplets.mesh!=null,"overflow uses rounded mesh droplets instead of opaque billboard quads");
                Capture("Service-overflow.png",new Vector3(3.9f,2.1f,-35.8f),new Vector3(1.7f,.65f,-33.2f));
                var watch=new System.Diagnostics.Stopwatch();long allocated=GC.GetAllocatedBytesForCurrentThread();watch.Start();
                for(int i=0;i<240;i++)service.UpdateViewAndEffects(true,.02f);
                watch.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
                results.Add("Service presentation CPU sample (240 calls, connected hose plus live overflow): "+(watch.Elapsed.TotalMilliseconds/240).ToString("F3")+" ms/call; managed allocation="+allocated+" bytes. Includes periodic tank-label refresh, excludes simulation, IMGUI and GPU. Fixed 160-droplet pool and reusable six-node hose route.");
                Aim(new Vector3(4.02f,1.25f,-34.78f));Pulse(Key.E);
                Check(game.Sim.SupplyValve==0,"wall valve stops supply");
                Aim(service.FootPosition+new Vector3(0,1.68f,4));Walk(27,Key.W);Walk(28,Key.A);
                Aim(service.FloodInlet.position);Pulse(Key.E);
                Check(game.Sim.HoseState==WaterHoseState.Carried,"E releases the depressurized flood coupling");
                Aim(service.WashInlet.position);Pulse(Key.E);
                Check(game.Sim.HoseState==WaterHoseState.Wash,"same nozzle connects independently to wash inlet");
                Aim(service.FootPosition+new Vector3(0,1.68f,-4));Walk(27,Key.W);Walk(28,Key.A);
                Aim(new Vector3(4.02f,1.25f,-34.78f));Pulse(Key.E);Tick(7);
                Check(game.Sim.WashWaterLiters==310&&game.Sim.ServiceOverflowLitersPerSecond>11.9f,"wash tank fills and overflows independently");
                Pulse(Key.E);
                Aim(service.FootPosition+new Vector3(0,1.68f,4));Walk(27,Key.W);Walk(28,Key.A);
                Aim(service.WashInlet.position);Pulse(Key.E);
                Aim(service.FootPosition+new Vector3(0,1.68f,-4));Walk(58,Key.W);Walk(28,Key.A);
                Aim(new Vector3(4.04f,1.15f,-35.98f));Pulse(Key.E);
                Check(game.Sim.HoseState==WaterHoseState.Stowed,"nozzle returns to wall holder through normal interaction");
                Aim(service.FootPosition+new Vector3(0,1.68f,-4));Walk(28,Key.W);
                Aim(service.FootPosition+new Vector3(-4,1.68f,0));Walk(145,Key.W);
                Aim(service.FootPosition+new Vector3(0,1.68f,4));Walk(70,Key.W);Pulse(Key.F);
                Check(!game.Sim.OnFoot&&game.DriverCamera.transform.IsChildOf(game.World.CameraMount),"walk back to left step and F restore seated camera");
                game.Sim.Controls.Throttle=1;Tick(1);
                Check(game.Sim.Vehicle.Speed>.1f&&!game.Sim.ServiceDriveInhibited,"machine resumes forward travel after both service cycles");
                Check(game.Sim.TotalServiceOverflowLiters>0,"overflow liters recorded independently of tank storage");
                Check(Math.Abs(game.Sim.TotalServiceSuppliedLiters-game.Sim.TotalServiceStoredLiters-game.Sim.TotalServiceDrainLiters)<.02,"runtime supply equals stored volume plus drain flow within float tolerance");
                results.Add("Runtime captures are rendered scene evidence. Mechanical feel and walking comfort still require operator playtesting.");
                File.WriteAllLines("Assets/Experiments/IceResurfacer/Validation~/Service-results.txt",results);
            }
            finally{InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();game.NewSession(true,true);game.enabled=enabled;}
        }
    }
}
