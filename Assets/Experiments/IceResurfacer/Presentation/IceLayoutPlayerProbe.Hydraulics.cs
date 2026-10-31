#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Presentation
{
    // Runs only in the explicitly launched development-player validation fixture.
    public sealed partial class IceLayoutPlayerProbe
    {
        IEnumerator ValidateHydraulicDirections()
        {
            int firstPass=passes.Count,firstFailure=failures.Count;
            var previousMouse=Mouse.current;var previousKeyboard=Keyboard.current;
            var pointer=InputSystem.AddDevice<Mouse>();var keyboard=InputSystem.AddDevice<Keyboard>();
            var render=typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic);
            var update=typeof(IceResurfacerGame).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            void Render(){render.Invoke(game,new object[]{0f});}
            void Step(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++)game.Sim.Step(.02f);Render();}
            void Pointer(Vector2 p,bool down)
            {InputSystem.QueueStateEvent(pointer,new MouseState{position=p,buttons=(ushort)(down?1:0)});InputSystem.Update();game.PhysicalControls.UpdateInput(true,false);Render();}
            void Input(params Key[] keys)
            {InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();update.Invoke(game,null);}
            try
            {
                game.NewSession(false);game.Dashboard.Compact=false;
                var s=game.Sim;var c=s.Controls;var w=game.World;
                // A clear rink location permits full tank travel without fixture-wall collisions.
                // Only setup poses are seeded; every lift below uses the real input path and Step.
                s.Vehicle.X=s.Conditioner.X=0;s.Vehicle.Z=0;s.Conditioner.Z=-2;
                s.Vehicle.Heading=s.Conditioner.Heading=0;Render();
                foreach(bool conditioner in new[]{true,false})
                {
                    string key=conditioner?"Conditioner":"TankLift";
                    Transform lever=conditioner?w.ConditionerLever:w.DumpLever;
                    float duration=conditioner?s.Config.ConditionerLiftSeconds:s.Config.TankLiftSeconds;
                    float Position()=>conditioner?s.Conditioner.Lift:s.TankLift;
                    float Demand()=>conditioner?c.ConditionerHydraulic:c.TankHydraulic;
                    void Verify(bool raise,string source)
                    {
                        Check(raise?Demand()>.99f:Demand()<-.99f,key+" / "+source+" / demand "+(raise?"RAISE":"LOWER"));
                        Render();
                        float gripForward=Vector3.Dot(lever.TransformDirection(Vector3.up),w.Machine.forward);
                        Check(raise?gripForward>.3f:gripForward<-.3f,key+" / physical grip tilts "+(raise?"FORWARD":"BACK")+" for "+source);
                        Check(conditioner?c.TankHydraulic==0:c.ConditionerHydraulic==0,key+" / other hydraulic remains neutral");
                        if(conditioner&&Position()>.005f&&Position()<.995f)
                            Check(IceDashboard.ConditionerStatus(s)==(raise?"CONDITIONER RAISING":"CONDITIONER LOWERING"),key+" / actual status agrees with demand");
                    }
                    void Held(float seconds,bool raise,bool full,string source)
                    {
                        float before=Position();Step(seconds);
                        Check(full?Mathf.Abs(Position()-(raise?1:0))<.0001f:
                            raise?Position()>before+.001f:Position()<before-.001f,key+" / "+source+" moves actual mechanism "+(full?"to full ":"partly ")+(raise?"UP":"DOWN"));
                        Check(Mathf.Abs(w.TankPivot.localEulerAngles.x-s.TankLift*58)<.01f,key+" / tank geometry follows actual lift");
                        Check(Mathf.Abs(w.Conditioner.position.y-s.Conditioner.Lift*.34f)<.00001f,key+" / conditioner geometry follows actual lift");
                    }
                    void Released(string source)
                    {
                        float attained=Position();Step(.4f);
                        Check(Demand()==0&&Mathf.Abs(Position()-attained)<.00001f,key+" / "+source+" release stops and holds attained position");
                        Check(Quaternion.Angle(lever.localRotation,Quaternion.identity)<.01f,key+" / physical lever springs to center after "+source);
                        if(conditioner)
                            Check(IceDashboard.ConditionerStatus(s)==(attained<=.005f?"CONDITIONER DOWN":attained>=.995f?"CONDITIONER UP":"CONDITIONER PARTIALLY LOWERED"),key+" / neutral status reports actual position");
                    }
                    Vector2 knob=default,pull=default;
                    void PhysicalStart(bool raise)
                    {
                        game.DriverCamera.transform.LookAt(lever.TransformPoint(0,.225f,0),Vector3.up);
                        Vector2 Screen(Vector3 p)=>game.DriverCamera.WorldToScreenPoint(p);
                        knob=Screen(lever.TransformPoint(0,.225f,0));
                        pull=(Screen(lever.position-w.Machine.forward*.15f)-Screen(lever.position)).normalized;
                        Pointer(knob,false);Pointer(knob,true);
                        Check(Demand()==0,key+" / physical grip starts in neutral dead zone");
                        Pointer(knob+pull*(raise?-80:80),true);Verify(raise,"physical mouse drag");
                        Check(game.PhysicalControls.Hint.Contains("BACK toward seat to LOWER")&&game.PhysicalControls.Hint.Contains("FORWARD to RAISE"),key+" / physical tooltip directions");
                    }
                    void PhysicalStop(){Pointer(knob,false);Released("physical");}
                    Rect rect=IceDashboard.ControlRect(key);
                    Vector2 Forward()=>new Vector2(rect.center.x,rect.y+12);
                    Vector2 Back()=>new Vector2(rect.center.x,rect.yMax-12);
                    IEnumerator HudStart(bool raise)
                    {
                        yield return Send(EventType.MouseDown,rect.center);
                        Check(Demand()==0,key+" / HUD center is neutral");
                        yield return Send(EventType.MouseDrag,raise?Forward():Back());Verify(raise,"native HUD drag");
                    }
                    IEnumerator HudStop(bool raise)
                    {yield return Send(EventType.MouseUp,raise?Forward():Back());Released("HUD");}

                    PhysicalStart(true);Held(duration+.1f,true,true,"physical forward");PhysicalStop();
                    PhysicalStart(false);Held(duration*.35f,false,false,"physical back");PhysicalStop();
                    yield return HudStart(true);Held(duration*.15f,true,false,"HUD forward after physical");
                    yield return Capture("Hydraulic-"+key+"-forward-"+currentResolution+".png",key+" forward raises / upper HUD grip / actual partial lift");
                    yield return HudStop(true);
                    yield return HudStart(false);Held(duration*.15f,false,false,"HUD back");
                    yield return Capture("Hydraulic-"+key+"-back-"+currentResolution+".png",key+" back lowers / lower HUD grip / actual partial lift");
                    yield return HudStop(false);
                    PhysicalStart(true);Held(duration*.1f,true,false,"physical forward after HUD");PhysicalStop();
                    yield return HudStart(false);Held(duration+.1f,false,true,"HUD back");yield return HudStop(false);
                    PhysicalStart(true);Held(duration+.1f,true,true,"physical forward");PhysicalStop();
                    PhysicalStart(false);Held(duration+.1f,false,true,"physical back");PhysicalStop();
                    yield return HudStart(true);Held(duration+.1f,true,true,"HUD forward");
                    // Returning to center while still held also stops; a held drag can reverse.
                    yield return Send(EventType.MouseDrag,rect.center);Released("held HUD center");
                    yield return Send(EventType.MouseDrag,Back());Verify(false,"HUD reversal");Held(duration*.5f,false,false,"HUD reversal");
                    yield return HudStop(false);
                    foreach(bool raise in new[]{true,false})
                    {
                        Key keyCode=conditioner?Key.Digit1:Key.Digit7;
                        bool shift=conditioner?raise:!raise;
                        Input();Input(shift?new[]{keyCode,Key.RightShift}:new[]{keyCode});Verify(raise,"keyboard");
                        Held(duration*.1f,raise,false,"keyboard");Input();Render();Released("keyboard");
                    }
                    Check(!c.HorizontalAuger&&!c.VerticalAuger&&!c.WashPump&&!c.BoardBrush&&c.FloodFlow==0&&c.WashValve==0&&c.BladeDepthMm==0,key+" / unrelated machinery unchanged");
                }
            }
            finally
            {
                game.ReleaseControls();InputSystem.RemoveDevice(pointer);InputSystem.RemoveDevice(keyboard);
                if(previousMouse!=null&&previousMouse.added)previousMouse.MakeCurrent();
                if(previousKeyboard!=null&&previousKeyboard.added)previousKeyboard.MakeCurrent();
                game.NewSession(false);game.Dashboard.Compact=true;
                string directory=Path.Combine(outputDirectory,"HydraulicDirections");Directory.CreateDirectory(directory);
                var report=new List<string>(passes.GetRange(firstPass,passes.Count-firstPass));
                report.AddRange(failures.GetRange(firstFailure,failures.Count-firstFailure));
                report.Add(failures.Count==firstFailure?"PASS: all hydraulic direction checks ("+(passes.Count-firstPass)+").":"FAIL: hydraulic direction checks.");
                File.WriteAllLines(Path.Combine(directory,"Player-"+currentResolution+".txt"),report);
            }
        }
    }
}
#endif
