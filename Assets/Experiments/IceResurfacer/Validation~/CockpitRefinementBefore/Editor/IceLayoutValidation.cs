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
            CheckPhysicalLayout(); CheckPhysicalControls(); CheckGeometryAuthoring(); CaptureExternal();
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
            Require(Mathf.Abs(LocalX(w.BodyTank)-game.Sim.Config.TankLateralOffset)<.00001f,"Tank body follows the authored centered shell offset");
            Require(w.TankPivot.localScale==Vector3.one&&w.BodyTank.localScale==Vector3.one,"Tank roof is authored lower without scaling moving assembly parents");
            Require(Mathf.Abs(w.TankInspection.localPosition.y-game.Sim.Config.TankGeometryY(1.05f))<.00001f,"Inspection opening follows the lowered real shell");
            Vector3 inlet=w.Machine.InverseTransformPoint(w.TankDischarge.position);
            Require(Vector3.Distance(inlet,new Vector3(game.Sim.Config.ConveyorOutletX,game.Sim.Config.ConveyorOutletY,game.Sim.Config.ConveyorOutletZ))<.00001f,"Actual compact rear-wall outlet matches snow transport coordinates");
            bool overheadPipe=false;
            foreach(var part in w.Machine.GetComponentsInChildren<Transform>())
                overheadPipe|=part.name=="Snow delivery chute"||part.name=="Snow delivery downspout";
            Require(!overheadPipe,"Oversized overhead conveyor assembly is absent");
            Require(RenderSettings.ambientLight==new Color(214f/255f,228f/255f,245f/255f),"Scene-specific ambient matches the requested Environment color-picker RGB");
            Require(LocalX(w.SnowBreakerPlunger)>LocalX(w.Seat),"Manual plunger is within reach to operator's right");
            Require(LocalX(w.Brush)<-.8f,"Board brush remains on the left while retracted");
            foreach(var t in w.Machine.GetComponentsInChildren<Transform>())
                Require(t.localScale.x>0&&t.localScale.y>0&&t.localScale.z>0,"Positive physical transform scale: "+t.name);
            float blade=w.CuttingBladeMesh.bounds.size.x*w.CuttingBlade.lossyScale.x;
            float towel=w.DistributionTowelMesh.bounds.size.x*w.DistributionTowel.lossyScale.x;
            float housing=w.ConditionerHousingMesh.bounds.size.x*w.ConditionerHousing.lossyScale.x;
            Require(Mathf.Abs(blade-game.Sim.Config.ConditionerWidth)<.001f,"Rendered blade matches simulated footprint");
            Require(Mathf.Abs(towel-game.Sim.Config.WaterDistributionWidth)<.001f,"Water towel matches simulated water distribution width");
            Require(housing>blade&&housing<game.Sim.Config.VehicleHalfWidth*2,"Housing fits the 84-inch vehicle envelope");
            float projection=Mathf.Tan(64*Mathf.Deg2Rad*.5f)/Mathf.Tan(game.DriverCamera.fieldOfView*Mathf.Deg2Rad*.5f)*towel/blade;
            Require(projection>1.10f&&projection<1.15f,"Real water spread plus lens projects wet half-width 10-15% wider per side");
            observations.Add("PASS physical LEFT seat/platform/steering/dashboard/pedals/access, seat-attached eye, centered tank/right-side plunger, unchanged left brush, positive scales.");
            observations.Add("Measured geometry: blade="+blade.ToString("F3")+"m; towel="+towel.ToString("F3")+"m; housing="+housing.ToString("F3")+"m; vehicle="+(game.Sim.Config.VehicleHalfWidth*2).ToString("F3")+"m.");
            observations.Add("61-degree seated lens and real water spread: apparent wet half-width ratio="+projection.ToString("F3")+" versus prior 64-degree lens / 1.956m water; scored coverage still requires actual blade contact.");
            observations.Add("Tank roof="+game.Sim.Config.TankRoofHeight.ToString("F3")+"m above hinge; conservative tapered interior="+game.Sim.Config.TankSnowPhysicalCapacityM3.ToString("F3")+"m3; full loose-equivalent stock="+game.Sim.Config.SnowCapacityM3.ToString("F2")+"m3; effective compaction="+(game.Sim.Config.SnowCapacityM3/game.Sim.Config.TankSnowPhysicalCapacityM3).ToString("F3")+":1. No material is added or discarded by visual compaction.");
        }
        static void CheckGeometryAuthoring()
        {
            var container=new GameObject("Validation only / unbatched authoring preview");
            container.transform.position=new Vector3(150,0,0);
            IceWorld preview=null;
            try
            {
                var config=JsonUtility.FromJson<Core.SimulatorConfig>(JsonUtility.ToJson(game.calibration));
                config.TankRoofHeight+=.025f;config.WheelTrack+=.04f;config.BrushPivotZ+=.03f;
                preview=IceWorldBuilder.BuildMachinePreview(container.transform,game.litTemplate,config);
                Require(preview.Machine.GetComponentsInChildren<MeshRenderer>().Length>100,"Authoring preview retains separately selectable actual machinery meshes");
                bool selectableFloor=false;
                foreach(Transform child in preview.TankPivot)
                    if(child.name=="Tapered steel tank floor / inside shell footprint")
                        selectableFloor=child.GetComponent<MeshFilter>()!=null&&child.GetComponent<MeshFilter>().sharedMesh!=null;
                Require(preview.BodyTank.GetComponentsInChildren<MeshRenderer>().Length==4&&selectableFloor,
                    "Preview preserves four independent wall/shoulder skins and the separate tapered steel floor");
                Require(Mathf.Abs(preview.TankInspection.localPosition.y-config.TankGeometryY(1.05f))<.00001f,"Authored tank roof parameter regenerates the same real inspection geometry");
                Require(Mathf.Abs(preview.FrontWheels[0].localPosition.x+config.WheelTrack*.5f)<.00001f,"Authoring wheel-track parameter moves actual steering pivot");
                Require(Mathf.Abs(preview.BrushArm.localPosition.z-config.BrushPivotZ)<.00001f,"Authoring brush parameter moves actual contact assembly");
                observations.Add("PASS shared-source unbatched machine preview: >100 selectable meshes, independent tank shell, parameter-driven roof/wheel/brush geometry. Runtime still batches materials; preview does not create an independent prefab implementation.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
                if(preview!=null)
                {
                    foreach(var mesh in preview.Meshes)UnityEngine.Object.DestroyImmediate(mesh);
                    foreach(var material in preview.Materials)UnityEngine.Object.DestroyImmediate(material);
                }
            }
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
                c.BladeDepthMm=0;c.WashValve=0;c.FloodFlow=0;c.ConditionerHydraulic=0;
                game.Sim.Vehicle.SteeringAngle=0;update.Invoke(game,new object[]{0f});
                Transform[] pivots={w.SteeringWheel,w.HorizontalAugerLever,w.VerticalAugerLever,w.BladeWheel,w.WashValve,w.FloodLever,w.ConditionerLever,w.AcceleratorPedal,w.BrakePedal};
                var rotations=new Quaternion[pivots.Length];
                for(int i=0;i<pivots.Length;i++)rotations[i]=pivots[i].localRotation;
                Quaternion cameraRotation=game.DriverCamera.transform.localRotation;
                Quaternion wheelRotation=w.FrontWheels[0].localRotation;
                c.Throttle=.6f;c.Brake=true;c.HorizontalAuger=true;c.VerticalAuger=true;
                c.BladeDepthMm=.3f;c.WashValve=.4f;c.FloodFlow=.6f;c.ConditionerHydraulic=.7f;
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
