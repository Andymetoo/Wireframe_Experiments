using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;

namespace IceResurfacer.Editor
{
    // Explicit batch-only inspection; does not run on import or modify the open project.
    internal static class IceLayoutValidation
    {
        const string Output="Assets/Experiments/IceResurfacer/Validation~/";
        static readonly List<string> observations=new List<string>();
        static IceResurfacerGame game;
        public static void Begin(IceResurfacerGame target)
        {
            game=target; observations.Clear();
            CheckPhysicalLayout(); CheckPhysicalControls(); CaptureExternal();
            File.WriteAllLines(Output+"Layout-results.txt",observations);
        }
        static void Require(bool condition,string message)
        { if(!condition)throw new InvalidOperationException("Layout validation: "+message); }
        static float LocalX(Transform target) { return game.World.Machine.InverseTransformPoint(target.position).x; }
        static void CheckPhysicalLayout()
        {
            var w=game.World;
            foreach(var t in new[]{w.OperatorStation,w.Seat,w.SteeringWheel,w.SteeringColumn,w.Dashboard,w.Pedals,w.AccessSteps,w.AcceleratorPedal,w.BrakePedal})
                Require(t!=null&&LocalX(t)<-.25f,"Physical assembly belongs on vehicle LEFT: "+(t==null?"missing reference":t.name));
            Require(w.CameraMount.IsChildOf(w.Seat),"Camera is attached to the physical seat");
            Require(LocalX(w.CameraMount)<-.5f&&game.World.Machine.InverseTransformPoint(w.CameraMount.position).y>1.65f,"Realistic left seated eye position");
            Require(LocalX(w.BodyTank)>0,"Tank body is physically offset toward the operator's right");
            Require(LocalX(w.SnowBreakerPlunger)>LocalX(w.Seat),"Manual plunger is within reach to operator's right");
            Require(LocalX(w.Brush)<-1,"Board brush remains on the left");
            foreach(var t in w.Machine.GetComponentsInChildren<Transform>())
                Require(t.localScale.x>0&&t.localScale.y>0&&t.localScale.z>0,"Positive physical transform scale: "+t.name);
            float blade=w.CuttingBladeMesh.bounds.size.x*w.CuttingBlade.lossyScale.x;
            float towel=w.DistributionTowelMesh.bounds.size.x*w.DistributionTowel.lossyScale.x;
            float housing=w.ConditionerHousingMesh.bounds.size.x*w.ConditionerHousing.lossyScale.x;
            Require(Mathf.Abs(blade-game.Sim.Config.ConditionerWidth)<.001f,"Rendered blade matches simulated footprint");
            Require(Mathf.Abs(towel-blade)<.001f,"Water towel covers the same physical working width");
            Require(housing>blade&&housing<game.Sim.Config.VehicleHalfWidth*2,"Housing fits the 84-inch vehicle envelope");
            float projection=Mathf.Tan(76*Mathf.Deg2Rad*.5f)/Mathf.Tan(game.DriverCamera.fieldOfView*Mathf.Deg2Rad*.5f);
            Require(projection>1.24f&&projection<1.26f,"Authentic working width projects approximately 25% wider");
            observations.Add("PASS physical LEFT seat/platform/steering/dashboard/pedals/access, seat-attached eye, right-side body/plunger, unchanged left brush, positive scales.");
            observations.Add("Measured geometry: blade="+blade.ToString("F3")+"m; towel="+towel.ToString("F3")+"m; housing="+housing.ToString("F3")+"m; vehicle="+(game.Sim.Config.VehicleHalfWidth*2).ToString("F3")+"m.");
            observations.Add("64-degree seated lens: apparent world-width ratio="+projection.ToString("F3")+" versus prior 76-degree lens; no widened texture or scoring footprint.");
        }
        static void CaptureExternal()
        {
            game.DriverCamera.transform.localPosition=new Vector3(.52f,.03f,0);
            game.DriverCamera.transform.localRotation=Quaternion.Euler(55,155,0);
            IceResurfacerEditor.Capture(game.DriverCamera,"Operator-right-down.png",1920,1080);
            game.DriverCamera.transform.localPosition=Vector3.zero;
            game.DriverCamera.transform.localRotation=Quaternion.Euler(5,0,0);
            IceResurfacerEditor.Capture(game.DriverCamera,"Operator-left-seat-forward.png",1920,1080);
            var obj=new GameObject("Isolated validation / physical operator inspection");
            var camera=obj.AddComponent<Camera>(); camera.enabled=false;
            camera.fieldOfView=50; camera.nearClipPlane=.05f; camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.08f,.12f,.16f);
            camera.transform.position=game.World.Machine.TransformPoint(-4.4f,3.4f,-4.8f);
            camera.transform.LookAt(game.World.Machine.TransformPoint(-.08f,1.1f,.55f));
            IceResurfacerEditor.Capture(camera,"Operator-external-left-rear.png",1920,1080);
            camera.transform.position=game.World.Machine.TransformPoint(0,7.6f,.7f);
            camera.transform.rotation=game.World.Machine.rotation*Quaternion.Euler(90,0,0);
            IceResurfacerEditor.Capture(camera,"Operator-external-top.png",1920,1080);
            UnityEngine.Object.Destroy(obj);
        }
        static void CheckPhysicalControls()
        {
            var w=game.World;var c=game.Sim.Controls;
            var fields=c.GetType().GetFields();var original=new object[fields.Length];
            for(int i=0;i<fields.Length;i++)original[i]=fields[i].GetValue(c);
            float oldSteering=game.Sim.Vehicle.SteeringAngle;
            var update=typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                c.Throttle=0;c.Brake=false;c.HorizontalAuger=false;c.VerticalAuger=false;
                c.BladeDepthMm=0;c.WashValve=0;c.FloodFlow=0;c.ConditionerTarget=0;
                game.Sim.Vehicle.SteeringAngle=0;update.Invoke(game,new object[]{0f});
                Transform[] pivots={w.SteeringWheel,w.HorizontalAugerLever,w.VerticalAugerLever,w.BladeWheel,w.WashValve,w.FloodLever,w.ConditionerLever,w.AcceleratorPedal,w.BrakePedal};
                var rotations=new Quaternion[pivots.Length];
                for(int i=0;i<pivots.Length;i++)rotations[i]=pivots[i].localRotation;
                Quaternion cameraRotation=game.DriverCamera.transform.localRotation;
                Quaternion wheelRotation=w.FrontWheels[0].localRotation;
                c.Throttle=.6f;c.Brake=true;c.HorizontalAuger=true;c.VerticalAuger=true;
                c.BladeDepthMm=.3f;c.WashValve=.4f;c.FloodFlow=.6f;c.ConditionerTarget=.35f;
                game.Sim.Vehicle.SteeringAngle=.25f;update.Invoke(game,new object[]{0f});
                for(int i=0;i<pivots.Length;i++)
                {
                    Require(Quaternion.Angle(rotations[i],pivots[i].localRotation)>1,"Working physical control animation: "+pivots[i].name);
                    Require(pivots[i].GetComponentsInChildren<MeshRenderer>().Length>0,"Animated geometry survived batching: "+pivots[i].name);
                }
                Require(Quaternion.Angle(cameraRotation,game.DriverCamera.transform.localRotation)<.001f,"Steering leaves head orientation unchanged");
                Quaternion steeringDelta=w.FrontWheels[0].localRotation*Quaternion.Inverse(wheelRotation);
                Require(Quaternion.Angle(steeringDelta,Quaternion.Euler(0,.25f*Mathf.Rad2Deg,0))<.01f,"Positive steering remains a world-right turn independently of wheel roll");
                observations.Add("PASS relocated physical steering/auger levers/blade/wash/flood/lift/pedal animation pivots and rendered children; world steering sign and independent head orientation retained.");
            }
            finally
            {
                for(int i=0;i<fields.Length;i++)fields[i].SetValue(c,original[i]);
                game.Sim.Vehicle.SteeringAngle=oldSteering;update.Invoke(game,new object[]{0f});
            }
        }
        public static bool Tick() { return true; }
    }
}
