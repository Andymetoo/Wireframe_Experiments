using System;
using System.Collections.Generic;
using System.Reflection;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Editor
{
    /// <summary>Real Transform, Camera and Input System validation; requires a licensed Unity Play session.</summary>
    public static class IceOperatorIntegrationValidation
    {
        [MenuItem("Experiments/Ice Resurfacer/Validation/Operator anchors and cockpit controls (Play Mode)")]
        public static void RunCurrent()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play Mode in the authored or procedural scene first.");
            var game=UnityEngine.Object.FindFirstObjectByType<IceResurfacerGame>();
            if(game==null)throw new InvalidOperationException("No active IceResurfacerGame.");
            foreach(string result in Run(game))Debug.Log(result);
        }

        public static List<string> Run(IceResurfacerGame game)
        {
            var results=new List<string>();
            void Check(bool value,string label)
            {if(!value)throw new InvalidOperationException("Operator integration: "+label);results.Add("PASS "+label);}
            var station=game.World.OperatorControls;var camera=game.DriverCamera.transform;
            Check(station!=null&&station.driverEyeAnchor!=null&&station.controlsViewAnchor!=null,"persistent driver and controls anchor binding");
            Check(camera.parent==station.driverEyeAnchor,"seated camera follows the authored driver anchor");
            var oldMouse=Mouse.current;var oldKeys=Keyboard.current;
            var mouse=InputSystem.AddDevice<Mouse>();var keys=InputSystem.AddDevice<Keyboard>();
            mouse.MakeCurrent();keys.MakeCurrent();
            bool enabled=game.enabled;game.enabled=false;
            var c=game.Sim.Controls;bool brush=c.BoardBrush,pump=c.WashPump;
            float lift=game.Sim.Conditioner.Lift;
            var savedPosition=camera.localPosition;var savedRotation=camera.localRotation;
            var render=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),game,typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.NonPublic|BindingFlags.Instance));
            var look=(Action<bool,float>)Delegate.CreateDelegate(typeof(Action<bool,float>),game,typeof(IceResurfacerGame).GetMethod("UpdateLook",BindingFlags.NonPublic|BindingFlags.Instance));
            void Keys(params Key[] held){InputSystem.QueueStateEvent(keys,new KeyboardState(held));InputSystem.Update();}
            void Pointer(Vector2 position,bool down)
            {InputSystem.QueueStateEvent(mouse,new MouseState{position=position,buttons=(ushort)(down?1:0)});InputSystem.Update();game.PhysicalControls.UpdateInput(true,false);}
            Vector2 Point(int index)=>game.DriverCamera.WorldToScreenPoint(IcePhysicalControls.GripPositions(game.World,game.Sim.Config)[index]);
            try
            {
                Keys();game.SelectCameraView("Forward");look(true,1);
                var beforePosition=camera.localPosition;var beforeRotation=camera.localRotation;
                Keys(Key.E);look(true,.05f);
                Check(Vector3.Distance(camera.position,station.controlsViewAnchor.position)>.001f,"E transition is smooth rather than an immediate jump");
                look(true,station.controlsEnterSeconds+1);
                Check(Vector3.Distance(camera.position,station.controlsViewAnchor.position)<.00001f&&Quaternion.Angle(camera.rotation,station.controlsViewAnchor.rotation)<.001f,"E settles at the exact authored position and rotation");
                Keys(Key.E,Key.Q);look(true,.1f);Keys(Key.C,Key.E,Key.Q);look(true,.1f);
                Keys(Key.E,Key.Q);look(true,1);Keys(Key.Q);look(true,1);Keys();look(true,station.controlsReturnSeconds+1);
                Check(Vector3.Distance(camera.localPosition,beforePosition)<.00001f&&Quaternion.Angle(camera.localRotation,beforeRotation)<.001f,"overlapping E/Q/C holds restore the original seated pose exactly");
                look(true,.02f);
                Check(Vector3.Distance(camera.localPosition,beforePosition)<.00001f&&Quaternion.Angle(camera.localRotation,beforeRotation)<.001f,"restored pose remains stable on the following frame");

                game.SelectCameraView("Controls");game.TickControlsAnchor(1);
                foreach(float height in new[]{0f,.5f,1f})
                {
                    game.Sim.Conditioner.Lift=height;render(0);
                    var targets=IcePhysicalControls.GripPositions(game.World,game.Sim.Config);
                    foreach(int index in new[]{1,2,3,4,5,6,7,8})
                    {
                        var v=game.DriverCamera.WorldToViewportPoint(targets[index]);
                        Check(v.z>0&&v.x>0&&v.x<1&&v.y>0&&v.y<1,"E frames control "+index+" at conditioner lift "+height);
                        string picked=game.PhysicalControls.GetHitControl(Point(index));
                        string[] names={"","Conditioner hydraulic lever","Horizontal auger lever","Vertical auger lever","Snow tank hydraulic lever","Flood water valve","Wash water valve","Blade handwheel","Snow-breaker plunger"};
                        Check(picked==names[index],"E picks "+names[index]+" at conditioner lift "+height);
                    }
                }

                // A normal seated look down/left reaches the authored left-hand panel.
                game.SelectCameraView("Forward");look(true,1);
                foreach(int index in new[]{9,10,11})
                {
                    var part=station.Control(index);Check(part!=null,"persistent physical control "+index);
                    var renderers=part.GetComponentsInChildren<Renderer>();
                    Check(Array.Exists(renderers,r=>r.enabled),"control "+index+" retains live renderers after batching");
                    var targets=IcePhysicalControls.GripPositions(game.World,game.Sim.Config);
                    camera.LookAt(targets[index],Vector3.up);
                    var point=Point(index);Pointer(point,false);Pointer(point,true);
                    Check(game.PhysicalControls.HighlightedControl!="","control "+index+" provides active highlight");
                    if(index==9)Check(c.BoardBrush!=brush,"brush switch writes the existing simulation state");
                    if(index==11)Check(c.WashPump!=pump,"pickup switch writes the existing pump state");
                    if(index==10)Check(game.HornPressed,"horn sounds only during the mouse hold");
                    Pointer(point,false);Check(!game.HornPressed,"mouse release clears momentary horn");
                    render(0);
                    if(index!=10)
                    {
                        var on=part.localRotation;
                        if(index==9)c.BoardBrush=!c.BoardBrush;else c.WashPump=!c.WashPump;
                        render(0);Check(Quaternion.Angle(on,part.localRotation)>1,"keyboard/HUD state changes animate switch "+index);
                    }
                    // Geometry-based picking follows an authored mount moved and non-uniformly scaled.
                    var mount=part.parent;var position=mount.localPosition;var scale=mount.localScale;
                    try
                    {
                        mount.localPosition+=new Vector3(.025f,.02f,-.015f);mount.localScale=Vector3.Scale(scale,new Vector3(1.2f,.9f,1.1f));
                        camera.LookAt(IcePhysicalControls.GripPositions(game.World,game.Sim.Config)[index],Vector3.up);
                        Check(game.PhysicalControls.GetHitControl(Point(index))==new[]{"Board brush switch","Horn button","Wash pickup pump switch"}[index-9],"moved/scaled control "+index+" remains pickable");
                    }
                    finally{mount.localPosition=position;mount.localScale=scale;}
                }
                game.SetHorn(true);
                var audio=game.GetComponent<IceMachineAudio>();
                var horn=(AudioSource)typeof(IceMachineAudio).GetField("horn",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(audio);
                game.ReleaseControls();
                Check(!game.HornPressed&&!horn.isPlaying&&horn.volume==0,"pause/focus/dismount release stops the horn without requiring another frame");
                return results;
            }
            finally
            {
                game.ReleaseControls();c.BoardBrush=brush;c.WashPump=pump;game.Sim.Conditioner.Lift=lift;render(0);
                Keys();look(true,1);game.SelectCameraView("Forward");camera.localPosition=savedPosition;camera.localRotation=savedRotation;
                InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keys);oldMouse?.MakeCurrent();oldKeys?.MakeCurrent();game.enabled=enabled;
            }
        }
    }
}
