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
    /// <summary>Actual Input System mouse gestures against the rendered mechanical parts.</summary>
    internal static class IcePhysicalValidation
    {
        static readonly List<string> results=new List<string>();
        static IceResurfacerGame game;
        static Mouse mouse;
        static Action<float> render;
        static void Check(bool valid,string message)
        { if(!valid)throw new InvalidOperationException("Physical controls: "+message);results.Add("PASS "+message); }
        static Vector2 Screen(Vector3 position) { return game.DriverCamera.WorldToScreenPoint(position); }
        static void Pointer(Vector2 position,bool down)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState { position=position,buttons=(ushort)(down?1:0) });
            InputSystem.Update();game.PhysicalControls.UpdateInput(true,false);
        }
        static void Aim(Transform t,Vector3 point)
        { game.DriverCamera.transform.LookAt(t.TransformPoint(point),Vector3.up); }
        static void Hit(Transform t,Vector3 point,string name)
        {
            Aim(t,point);
            string actual=game.PhysicalControls.GetHitControl(Screen(t.TransformPoint(point)));
            Check(actual==name,"screen ray selects "+name+" (actual: "+actual+")");
        }
        // The visible grip as authored (in the handle's frame): the generated layout's (-0.085, stem height, 0),
        // or wherever an authored valve puts it (a lengthened stem raises it).
        static Vector3 GripLocal(bool wash)
        {
            var grips=game.World.ControlGrips??IcePhysicalControls.MeasureGrips(game.World,game.Sim.Config);
            return grips[wash?6:5].Center;
        }
        /// <summary>Where the measured grip is drawn at a given setting (the handle turns -90 deg per unit flow).</summary>
        static Vector3 ValvePoint(Transform handle,float value)
        {
            var grip=GripLocal(handle==game.World.WashValve);
            return handle.parent.TransformPoint(handle.localPosition+Quaternion.Euler(0,-90*value,0)*Vector3.Scale(handle.localScale,grip));
        }
        static void Valve(Transform handle,bool wash,float goal)
        {
            float start=wash?game.Sim.Controls.WashValve:game.Sim.Controls.FloodFlow;
            Aim(handle,Vector3.up*GripLocal(wash).y);Pointer(Screen(ValvePoint(handle,start)),false);
            Pointer(Screen(ValvePoint(handle,start)),true);
            // A hand takes the grip round in 3 deg frames, the handle redrawn between them (the game reads one
            // pointer position a frame). Seen edge-on at full lift, a single jump of up to 90 deg is ambiguous.
            int frames=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(goal-start)*30));
            for(int i=1;i<=frames;i++){Pointer(Screen(ValvePoint(handle,Mathf.Lerp(start,goal,i/(float)frames))),true);render(0);}
            Pointer(Screen(ValvePoint(handle,goal)),false);
            float actual=wash?game.Sim.Controls.WashValve:game.Sim.Controls.FloodFlow;
            Check(Mathf.Abs(actual-goal)<.002f,(wash?"wash":"flood")+" handle arc gives "+goal.ToString("P0")+" actual flow");
            Check(Quaternion.Angle(handle.localRotation,Quaternion.Euler(0,-90*goal,0))<.2f,"physical handle angle matches authoritative flow");
        }
        public static void Run(IceResurfacerGame owner,Keyboard keys,Mouse pointer)
        {
            game=owner;mouse=pointer;results.Clear();bool wasEnabled=game.enabled;game.enabled=false;
            render=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),game,typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic));
            try
            {
                game.NewSession(true);InputSystem.QueueStateEvent(keys,new KeyboardState());InputSystem.Update();render(0);
                var w=game.World;var s=game.Sim;var c=s.Controls;
                Check(w.VerticalAugerLever.localPosition.x<w.ConditionerLever.localPosition.x&&
                    w.ConditionerLever.localPosition.x<w.HorizontalAugerLever.localPosition.x&&
                    w.HorizontalAugerLever.localPosition.x<w.DumpLever.localPosition.x&&
                    Mathf.Abs(w.VerticalAugerLever.localPosition.z-w.DumpLever.localPosition.z)<.00001f,
                    "actual mounted assemblies follow operator-left to right VERTICAL / CONDITIONER / HORIZONTAL / DUMP");
                // Grips stay apart through both quarter turns (the rig's own crowding threshold).
                float closest=float.PositiveInfinity;
                Vector3 Grip(Transform h,int degrees)=>h.parent.TransformPoint(h.localPosition+h.localRotation*Quaternion.Euler(0,-degrees,0)*(GripLocal(h==w.WashValve)+Vector3.left*.005f));
                for(int a=0;a<=90;a+=5)for(int b=0;b<=90;b+=5)closest=Mathf.Min(closest,Vector3.Distance(Grip(w.FloodLever,a),Grip(w.WashValve,b)));
                Check(Mathf.Abs(w.FloodValveMount.localPosition.z-w.WashValveMount.localPosition.z)<.00001f&&
                    w.WashValveMount.localPosition.x>w.FloodValveMount.localPosition.x&&closest>=IceMachineRigEditor.GripSpacingWarning,
                    "water handles occupy adjacent transverse bearings with clearance through quarter turns (closest grips "+(closest*1000).ToString("F0")+" mm)");
                Hit(w.ConditionerLever,new Vector3(0,.225f,0),"Conditioner hydraulic lever");
                Hit(w.HorizontalAugerLever,new Vector3(0,.225f,0),"Horizontal auger lever");
                Hit(w.VerticalAugerLever,new Vector3(0,.225f,0),"Vertical auger lever");
                Hit(w.DumpLever,new Vector3(0,.225f,0),"Snow tank hydraulic lever");
                Hit(w.WashValve,GripLocal(true)+Vector3.left*.005f,"Wash water valve");
                Hit(w.FloodLever,GripLocal(false)+Vector3.left*.005f,"Flood water valve");
                Hit(w.BladeWheel,new Vector3(.11f,.045f,0),"Blade handwheel");
                Hit(w.SnowBreakerPlunger,new Vector3(0,.22f,0),"Snow-breaker plunger");

                foreach(var lever in new[]{w.HorizontalAugerLever,w.VerticalAugerLever})
                {
                    bool otherBefore=lever==w.HorizontalAugerLever?c.VerticalAuger:c.HorizontalAuger;
                    Aim(lever,Vector3.up*.225f);Vector2 p=Screen(lever.TransformPoint(0,.225f,0));
                    Pointer(p,false);Pointer(p,true);Pointer(p,false);render(0);
                    Check(lever==w.HorizontalAugerLever?c.HorizontalAuger:c.VerticalAuger,"auger remains latched after mouse release");
                    Check(Quaternion.Angle(lever.localRotation,Quaternion.Euler(24,0,0))<.01f,"auger RUN detent is physically distinct from neutral");
                    Check((lever==w.HorizontalAugerLever?c.VerticalAuger:c.HorizontalAuger)==otherBefore&&c.ConditionerHydraulic==0&&c.TankHydraulic==0,"relocated auger pick does not actuate another hydraulic system");
                }
                c.HorizontalAuger=false;render(0);
                Check(c.VerticalAuger,"vertical auger remains independent when horizontal is stopped");
                c.VerticalAuger=false;render(0);

                Aim(w.ConditionerLever,Vector3.up*.225f);Vector2 knob=Screen(w.ConditionerLever.TransformPoint(0,.225f,0));
                Vector2 pull=(Screen(w.ConditionerLever.position-w.Machine.forward*.15f)-Screen(w.ConditionerLever.position)).normalized;
                Pointer(knob,false);Pointer(knob,true);Pointer(knob+pull*80,true);
                Check(c.ConditionerHydraulic<-.9f,"pulling the physical lever back toward the seat requests conditioner lowering");
                s.Step(.30f);render(0);float attained=s.Conditioner.Lift;
                Check(attained<1&&attained>0,"conditioner lowering takes time");
                Pointer(knob+pull*80,false);s.Step(.5f);render(0);
                Check(c.ConditionerHydraulic==0&&Mathf.Abs(s.Conditioner.Lift-attained)<.00001f,"released spring control returns neutral and holds attained height");
                Check(Quaternion.Angle(w.ConditionerLever.localRotation,Quaternion.identity)<.01f,"rendered hydraulic lever springs to neutral");
                Check(c.TankHydraulic==0&&!c.HorizontalAuger&&!c.VerticalAuger,"relocated conditioner gesture leaves tank and augers untouched");
                Aim(w.DumpLever,Vector3.up*.225f);knob=Screen(w.DumpLever.TransformPoint(0,.225f,0));
                pull=(Screen(w.DumpLever.position-w.Machine.forward*.15f)-Screen(w.DumpLever.position)).normalized;
                Pointer(knob,false);Pointer(knob,true);Pointer(knob-pull*80,true);render(0);
                Check(c.TankHydraulic>.9f&&c.ConditionerHydraulic==0&&!c.HorizontalAuger&&!c.VerticalAuger,"rightmost dump lever raises only the snow tank while held");
                s.Step(.2f);render(0);float tank=s.TankLift;Pointer(knob-pull*80,false);s.Step(.3f);render(0);
                Check(c.TankHydraulic==0&&Mathf.Abs(s.TankLift-tank)<.000001f&&Quaternion.Angle(w.DumpLever.localRotation,Quaternion.identity)<.01f,"dump lever springs to neutral and holds attained tank angle on release");
                Check(w.ConditionerCylinderRods[0].localScale.y>0&&w.TankCylinderRods[0].localScale.y>0,"both hydraulic mechanisms have extended physical piston rods");
                game.SelectCameraView("Controls");
                for(int i=0;i<80;i++)typeof(IceResurfacerGame).GetMethod("UpdateLook",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{true,.02f});
                IceResurfacerEditor.Capture(game.DriverCamera,"Cockpit-hydraulic-controls.png",1920,1080);

                c.FloodFlow=c.WashValve=0;render(0);
                Aim(w.OperatorStation,new Vector3(.65f,1.53f,.04f));
                IceResurfacerEditor.Capture(game.DriverCamera,"Water-valves-closed.png",1920,1080);
                Valve(w.FloodLever,false,.25f);Valve(w.WashValve,true,.65f);
                Check(Mathf.Abs(c.FloodFlow-.25f)<.002f,"wash handle does not change flood setting");
                Aim(w.OperatorStation,new Vector3(.65f,1.53f,.04f));
                IceResurfacerEditor.Capture(game.DriverCamera,"Water-valves-partial.png",1920,1080);
                Valve(w.FloodLever,false,1);Valve(w.WashValve,true,1);
                Aim(w.OperatorStation,new Vector3(.65f,1.53f,.04f));
                IceResurfacerEditor.Capture(game.DriverCamera,"Water-valves-open.png",1920,1080);
                Valve(w.FloodLever,false,0);Valve(w.WashValve,true,0);
                Check(IcePhysicalControls.ValveFlowFromAngle(-30)==0&&IcePhysicalControls.ValveFlowFromAngle(120)==1,"valve travel stops at closed and open limits");

                // Turn by the spinner knob where it is drawn (generated: 0.11 m out, 0.045 m up), 3.75 turns in 30 deg steps.
                var grips=w.ControlGrips??IcePhysicalControls.MeasureGrips(w,s.Config);
                Vector3 spinner=grips[7].Knob,wheelCentre=w.BladeWheel.position,axis=w.BladeWheel.parent.rotation*Vector3.up;
                Aim(w.BladeWheel,spinner);c.BladeDepthMm=0;render(0);
                Vector3 arm=w.BladeWheel.TransformPoint(spinner)-wheelCentre;
                Vector2 start=Screen(wheelCentre+arm);
                Pointer(start,false);Pointer(start,true);
                for(int i=1;i<=45;i++)
                {
                    Pointer(Screen(wheelCentre+Quaternion.AngleAxis(30*i,axis)*arm),true);render(0);
                }
                Pointer(start,false);render(0);
                Check(Mathf.Abs(c.BladeDepthMm-s.Config.MaxBladeDepthMm)<.0001f,"blade wheel reaches and respects the 3.5-turn mechanical end stop");
                Check(Quaternion.Angle(w.BladeWheel.localRotation,Quaternion.Euler(0,s.Config.BladeWheelTurns*360,0))<.01f,"blade handwheel and screw setting share one state");
                results.Add("Screenshots are runtime camera renders of real geometry. Subjective lever feel, audio balance and actual operator reach still require human calibration.");
                File.WriteAllLines("Assets/Experiments/IceResurfacer/Validation~/Physical-controls-results.txt",results);
            }
            finally
            {
                File.WriteAllLines("Assets/Experiments/IceResurfacer/Validation~/Physical-controls-results.txt",results);
                Pointer(new Vector2(10,10),false);game.PhysicalControls.Release();game.NewSession(true);game.enabled=wasEnabled;
            }
        }
    }
}
