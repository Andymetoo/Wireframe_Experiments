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
        static readonly MethodInfo updateMachine=typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly object[] tickAnimation={.02f},initialAnimation={0f};
        static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException("Water service: "+message);results.Add("PASS "+message);}
        static void Input(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f)});
            InputSystem.Update();service.HandleInput(true,.02f);service.UpdateViewAndEffects(true,.02f);
        }
        static void Pulse(Key key){Input();Input(key);Input();}
        static void Tick(float seconds)
        {
            for(int i=0;i<(int)Math.Round(seconds/.02f);i++)
            {
                game.Sim.Step(.02f);updateMachine.Invoke(game,tickAnimation);service.UpdateViewAndEffects(true,.02f);
            }
        }
        static void SetMachinePose()
        {
            updateMachine.Invoke(game,initialAnimation);
        }
        static void Aim(Vector3 target)
        {
            Quaternion rotation=Quaternion.LookRotation(target-game.DriverCamera.transform.position);
            typeof(IceWaterService).GetField("footYaw",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,rotation.eulerAngles.y);
            typeof(IceWaterService).GetField("footPitch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,Mathf.DeltaAngle(0,rotation.eulerAngles.x));
            service.UpdateViewAndEffects(true,.02f);Input();
        }
        static Vector3 W(float side,float height,float outward)
        {game.Sim.Layout.ToWorld(side,outward,out float x,out float z);return new Vector3(x,height,z);}
        static void WalkTo(float side,float outward)
        {
            Vector3 target=W(side,0,outward);
            for(int i=0;i<500;i++)
            {
                Vector3 difference=target-service.FootPosition;difference.y=0;
                if(difference.magnitude<.075f){Input();return;}
                Aim(target+Vector3.up*1.68f);Input(Key.W);
            }
            throw new InvalidOperationException("Water service: WASD route blocked toward side="+side+", outward="+outward+"; foot="+service.FootPosition);
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
                game.NewSession(true,true);SetMachinePose();
                Check(service!=null,"interactive southeast service station initialized in launch scene");
                Check(game.Sim.FloodWaterLiters==738&&game.Sim.WashWaterLiters==310,"normal session starts full");
                Check(game.Sim.Layout.GateX>9&&game.Sim.Layout.GateZ< -25,"gate is in actual southeast rounded corner");
                Check(Vector3.Dot(service.HolderPosition-game.World.Machine.position,game.World.Machine.right)>3,"hose station is on parked machine's RIGHT");
                Check(service.FloodInlet.localPosition.x>=0&&Vector3.Dot(service.FloodInlet.position-game.World.Machine.position,game.World.Machine.right)>1,"rear-quarter fill necks remain on right fixed water-tank structure");
                Check(game.Sim.ConfigureWaterForServiceTest(.90f,.8f),"explicit practice partial-tank fixture");
                service.UpdateViewAndEffects(true,.21f);
                Capture("Service-bay.png",W(-4.5f,3.6f,10.5f),W(1,1.3f,5));
                Capture("Southeast-entrance.png",W(0,2.7f,5.7f),W(0,1.4f,-7));
                Capture("Southeast-service-room.png",W(-4.5f,3.6f,10.5f),W(1,1.3f,5));
                Pulse(Key.F);
                Check(game.Sim.OnFoot&&!game.DriverCamera.transform.IsChildOf(game.World.CameraMount),"actual F dismounts to grounded service camera");
                Check(Mathf.Abs(game.DriverCamera.transform.position.y-1.68f)<.001f,"on-foot viewpoint remains grounded");
                Input();Input();Quaternion beforeLook=game.DriverCamera.transform.rotation;
                InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(30,-12),position=new Vector2(Screen.width*.5f,Screen.height*.5f)});
                InputSystem.Update();service.HandleInput(true,.02f);
                Check(service.FootLookActive&&Quaternion.Angle(beforeLook,game.DriverCamera.transform.rotation)>1,"on-foot mouse look works without RMB");
                service.HandleInput(false,.02f);
                Check(!service.FootLookActive&&Cursor.lockState==CursorLockMode.None,"pause releases on-foot cursor capture");Input();
                // These are ordinary W presses with camera aimed at each waypoint,
                // following the aisle around the rear. No foot pose is teleported.
                WalkTo(-.8f,8.2f);WalkTo(3.5f,8.2f);WalkTo(4.4f,6.5f);
                Aim(service.HolderPosition);Pulse(Key.E);
                Check(game.Sim.HoseState==WaterHoseState.Carried,"actual E takes physical wall nozzle after walking around machine");
                Check(service.HoseVisibleLength>1&&service.HoseVisibleLength<=Simulation.ServiceHoseLengthMeters,"carried hose has bounded route around solid machine");
                WalkTo(3.55f,5.85f);Aim(service.FloodInlet.position);Pulse(Key.E);
                Check(game.Sim.HoseState==WaterHoseState.Flood,"right rear flood inlet couples independently");
                Check(Vector3.Distance(service.HoseNozzle.position,service.FloodInlet.position)<.001f,"visible nozzle terminates at connected fill neck");
                Capture("Service-hose-connected.png",W(4.6f,2.6f,8.5f),service.FloodInlet.position);
                WalkTo(4.35f,5.7f);Aim(service.SupplyPosition);Pulse(Key.E);
                Check(game.Sim.SupplyValve>.99f,"actual E opens wall supply");
                float before=game.Sim.FloodWaterLiters,washBefore=game.Sim.WashWaterLiters;Tick(3);
                Check(game.Sim.FloodWaterLiters>before+35&&game.Sim.WashWaterLiters==washBefore,"12 L/s fill affects only connected flood reservoir");
                Capture("Service-filling.png",W(4.6f,2.15f,7.8f),service.FloodInlet.position);
                Tick(5);
                Check(game.Sim.FloodWaterLiters==738&&game.Sim.ServiceOverflowLitersPerSecond>11.9f,"capacity is capped and excess water overflows");
                service.OverflowParticles.Simulate(.4f,true,false);
                var overflow=service.OverflowParticles;
                Check(overflow.emission.rateOverTime.constant>100&&overflow.main.maxParticles==160,"overflow uses fixed bounded 160-droplet pool");
                Check(overflow.GetComponent<ParticleSystemRenderer>().renderMode==ParticleSystemRenderMode.Mesh,"overflow retains rounded mesh droplets");
                Capture("Service-overflow.png",W(4.6f,2.15f,7.8f),service.FloodInlet.position-Vector3.up*.2f);
                var watch=new System.Diagnostics.Stopwatch();long allocated=GC.GetAllocatedBytesForCurrentThread();watch.Start();
                for(int i=0;i<240;i++)service.UpdateViewAndEffects(true,.02f);
                watch.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
                results.Add("Service presentation CPU sample, connected hose and overflow, 240 calls: "+(watch.Elapsed.TotalMilliseconds/240).ToString("F3")+" ms/call; "+allocated+" managed bytes. Excludes simulation, rendering/GPU and IMGUI; persistent floor meshes update at 5 Hz only when material state changes.");
                Aim(service.SupplyPosition);Pulse(Key.E);
                WalkTo(3.55f,5.85f);Aim(service.FloodInlet.position);Pulse(Key.E);
                Check(game.Sim.HoseState==WaterHoseState.Carried,"normal E disconnects flood hose");
                Aim(service.WashInlet.position);Pulse(Key.E);
                Check(game.Sim.HoseState==WaterHoseState.Wash,"same nozzle connects separate wash reservoir");
                WalkTo(4.35f,5.7f);Aim(service.SupplyPosition);Pulse(Key.E);Tick(7);
                Check(game.Sim.WashWaterLiters==310&&game.Sim.ServiceOverflowLitersPerSecond>11.9f,"wash reservoir fills and overflows independently");
                Aim(service.SupplyPosition);Pulse(Key.E);
                WalkTo(3.55f,6.25f);Aim(service.WashInlet.position);Pulse(Key.E);
                Check(game.Sim.HoseState==WaterHoseState.Carried,"wash nozzle disconnects normally");
                WalkTo(3.45f,6.55f);Aim(service.HolderPosition+Vector3.up*.48f);Input();
                Check(service.HolderHighlighted,"holder highlights with forgiving half-meter aim tolerance");
                Pulse(Key.E);
                Check(game.Sim.HoseState==WaterHoseState.Stowed,"E snaps nozzle home from practical two-meter approach");
                WalkTo(3.5f,8.2f);WalkTo(-.8f,8.2f);WalkTo(-.7f,6.3f);Pulse(Key.F);
                Check(!game.Sim.OnFoot&&game.DriverCamera.transform.IsChildOf(game.World.CameraMount),"walk back and F restores seated camera");
                Check(!service.FootLookActive&&Cursor.lockState==CursorLockMode.None,"boarding releases free-look cursor for physical cockpit controls");
                game.Sim.Controls.Throttle=1;Tick(1);
                Check(game.Sim.Vehicle.Speed>.1f&&!game.Sim.ServiceDriveInhibited,"machine drives after full service procedure");
                Check(Math.Abs(game.Sim.TotalServiceSuppliedLiters-game.Sim.TotalServiceStoredLiters-game.Sim.TotalServiceDrainLiters)<.03,"runtime supply is conserved into tank storage plus discharge");
                // New explicit test scenario: attach at the actual station, then
                // drive through the portal; only throttle moves the vehicle.
                game.NewSession(true,true);SetMachinePose();Pulse(Key.F);
                WalkTo(-.8f,8.2f);WalkTo(3.5f,8.2f);WalkTo(4.4f,6.5f);Aim(service.HolderPosition);Pulse(Key.E);
                WalkTo(3.55f,5.85f);Aim(service.FloodInlet.position);Pulse(Key.E);
                WalkTo(4.35f,5.7f);Aim(service.SupplyPosition);Pulse(Key.E);
                WalkTo(3.5f,8.2f);WalkTo(-.8f,8.2f);WalkTo(-.7f,6.3f);Pulse(Key.F);
                game.Sim.Controls.Throttle=1;Tick(11);
                Check(game.Sim.HoseBreakaways==1&&game.Sim.HoseState==WaterHoseState.Dropped,"actual driven departure releases hose when its routed reach is exceeded");
                Check(game.Sim.TotalServiceDrainLiters>0&&game.Sim.Vehicle.Speed>.1f&&!game.Sim.ServiceDriveInhibited,"breakaway spills and permits continued driving without stale locks");
                Capture("Service-hose-breakaway.png",W(3.5f,4.2f,3.8f),W(2,0,-2));
                game.Sim.Controls.HorizontalAuger=game.Sim.Controls.VerticalAuger=game.Sim.Controls.WashPump=true;
                game.Sim.Controls.BladeDepthMm=.25f;game.Sim.Controls.WashValve=.3f;game.Sim.Controls.FloodFlow=.2f;
                game.Sim.SetConditionerHydraulic(-1);game.Sim.SetTankHydraulic(1);Tick(3);
                game.Sim.SetConditionerHydraulic(0);game.Sim.SetTankHydraulic(0);
                float tankLift=game.Sim.TankLift,conditionerLift=game.Sim.Conditioner.Lift;
                Pulse(Key.F);
                Check(game.Sim.OnFoot&&game.Sim.Ice.IsInside(service.FootPosition.x,service.FootPosition.z),"actual F permits moving on-ice exit with raised tank and operating equipment");
                Check(game.Sim.TankLift==tankLift&&game.Sim.Conditioner.Lift==conditionerLift&&game.Sim.Controls.HorizontalAuger&&game.Sim.Controls.VerticalAuger&&game.Sim.Controls.WashPump&&game.Sim.Controls.FloodFlow==.2f&&game.Sim.Controls.WashValve==.3f,
                    "free dismount preserves attained hydraulics, conveyors, pump and both water valves");
                Capture("Free-simulation-on-foot.png",game.DriverCamera.transform.position,game.World.Machine.position+Vector3.up);
                results.Add("Service route uses keyboard F/E/W and ordinary walk collision; Aim supplies deterministic head orientation. No operator/vehicle pose writes during the service procedure or driven departure; session-start presentation poses are synchronized before testing.");
                File.WriteAllLines("Assets/Experiments/IceResurfacer/Validation~/Service-results.txt",results);
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();
                game.NewSession(true,true);game.enabled=enabled;
            }
        }
    }
}
