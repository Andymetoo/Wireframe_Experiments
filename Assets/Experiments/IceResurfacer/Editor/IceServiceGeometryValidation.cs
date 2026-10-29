using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEngine;

namespace IceResurfacer.Editor
{
    internal static class IceServiceGeometryValidation
    {
        static readonly MethodInfo updateMachine=typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly object[] animation={.02f};
        static readonly List<string> results=new List<string>();
        static IceResurfacerGame game;
        static void Check(bool passed,string message)
        {if(!passed)throw new InvalidOperationException("Service geometry: "+message);results.Add("PASS "+message);}
        static void Tick()
        {game.Sim.Step(.02f);updateMachine.Invoke(game,animation);game.WaterService.UpdateViewAndEffects(true,.02f);}
        static Vector3 W(float side,float y,float outward)
        {game.Sim.Layout.ToWorld(side,outward,out float x,out float z);return new Vector3(x,y,z);}
        static void Capture(string name,Vector3 position,Vector3 target)
        {
            var node=new GameObject("Service geometry evidence");var camera=node.AddComponent<Camera>();camera.enabled=false;
            camera.fieldOfView=58;camera.nearClipPlane=.04f;camera.farClipPlane=150;
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            IceResurfacerEditor.Capture(camera,name,1920,1080);UnityEngine.Object.Destroy(node);
        }
        public static void Run(IceResurfacerGame target)
        {
            game=target;results.Clear();bool diagnostics=game.DiagnosticsVisible;
            try
            {
                foreach(float angle in new[]{0f,-15f,15f,-30f,30f,90f})
                {
                    game.NewSession(true,true);var s=game.Sim;
                    s.Layout.ToWorld(angle==90?5:0,angle==90?11.5f:6.3f,out s.Vehicle.X,out s.Vehicle.Z);
                    s.Vehicle.Heading=s.Layout.OutwardHeading+angle*Mathf.Deg2Rad;
                    s.Controls.Throttle=.08f;
                    for(int i=0;i<3000&&!s.BoardContact;i++)Tick();
                    Check(s.LastMovementContact.Obstacle==MovementObstacle.PitRim,"slow "+angle+" degree approach reports actual pit rim");
                    Check(!s.ProbeVehiclePose(s.LastMovementContact.VehicleX,s.LastMovementContact.VehicleZ,s.LastMovementContact.Heading,out _),"overlay rejected pose remains obstructed at "+angle+" degrees");
                    if(!game.DiagnosticsVisible)game.ToggleDiagnostics();
                    var overlay=game.GetComponentInChildren<IceCollisionOverlay>();
                    Check(overlay!=null,"fixed collision overlay initialized");overlay.Refresh();
                    int overlayLines=0;
                    foreach(var line in overlay.GetComponentsInChildren<LineRenderer>())if(line.name.StartsWith("F3 ",StringComparison.Ordinal))overlayLines++;
                    Check(overlayLines==6,"F3 uses six reused line renderers beside the existing hose renderers");
                    if(angle==0||angle==30||angle==90)
                        Capture("Pit-contact-"+angle+"deg.png",W(-4.6f,4.0f,6.6f),W(0,.55f,10.8f));
                    float heading=s.Vehicle.Heading,x=s.Vehicle.X,z=s.Vehicle.Z;
                    s.Controls.Throttle=-.12f;for(int i=0;i<100;i++)Tick();
                    Check(!s.BoardContact&&Mathf.Abs(s.Vehicle.Heading-heading)<.0001f&&Vector2.Distance(new Vector2(x,z),new Vector2(s.Vehicle.X,s.Vehicle.Z))>.08f,"ordinary reverse clears pit with unchanged heading at "+angle+" degrees");
                }
                game.NewSession(true,true);var raised=game.Sim;
                raised.Vehicle.X=10.1f;raised.Vehicle.Z=0;raised.Vehicle.Heading=Mathf.PI*.5f;
                raised.SetTankHydraulic(1);for(int i=0;i<350;i++)Tick();
                Check(raised.TankLift>.03f&&raised.TankLift<.99f&&raised.LastMovementContact.Obstacle==MovementObstacle.TankGlass,"raising near actual glass stops the moving shell at contact");
                Check(raised.LastMovementContact.PointY<2.55f&&raised.LastMovementContact.NormalX<-.99f,"contact is below glass top and normal points onto ice");
                Check(Quaternion.Angle(game.World.TankPivot.localRotation,Quaternion.Euler(raised.TankLift*58,0,0))<.01f,"rendered tank shares core 58 degree lift geometry");
                Check(Vector3.Distance(game.World.TankPivot.localPosition,new Vector3(0,.78f,2.46f))<.001f&&Vector3.Distance(game.World.TankDoor.localPosition,new Vector3(.20f,1.02f,.002f))<.001f,"core and rendered moving contact pivots agree");
                game.GetComponentInChildren<IceCollisionOverlay>().Refresh();
                Capture("Raised-tank-glass-contact.png",new Vector3(7,3.1f,-4.5f),new Vector3(11.5f,1.5f,0));
                float held=raised.TankLift;raised.SetTankHydraulic(0);raised.Controls.Throttle=-.2f;
                for(int i=0;i<180;i++)Tick();
                Check(raised.Vehicle.Speed<-.1f&&Mathf.Abs(raised.TankLift-held)<.0001f,"actual Unity presentation reverses clear with tank held at attained lift");

                game.NewSession(true,true);var floor=game.Sim;
                if(game.DiagnosticsVisible)game.ToggleDiagnostics();
                game.GetComponentInChildren<IceCollisionOverlay>().Refresh();
                floor.Layout.ToWorld(-3.5f,6.3f,out float sourceX,out float sourceZ);
                floor.Environment.Deposit(sourceX,sourceZ,0,20);
                game.WaterService.UpdateViewAndEffects(true,.21f);
                MeshFilter floorMesh=null;
                foreach(var filter in game.WaterService.GetComponentsInChildren<MeshFilter>())if(filter.name=="Spatial floor runoff / conserved liters")floorMesh=filter;
                Check(floorMesh!=null,"persistent floor-water mesh exists");
                var wetMaterial=floorMesh.GetComponent<MeshRenderer>().sharedMaterial;
                Check(wetMaterial.HasProperty("_Room")&&Mathf.Abs(wetMaterial.GetVector("_Room").x-floor.Layout.RoomHalfWidth)<.001f,"wet-floor clipping footprint is a serialized material property available to SRP batching");
                bool visibleSource=false;
                foreach(var vertex in floorMesh.sharedMesh.vertices)if(vertex.y>0&&Vector2.Distance(new Vector2(vertex.x,vertex.z),new Vector2(sourceX,sourceZ))<1.1f)visibleSource=true;
                Check(visibleSource,"actual nonzero water produces vertices above floor at source");
                Capture("Service-floor-source.png",W(-1.8f,2.8f,4.1f),W(-3.6f,0,5.8f));
                for(int i=0;i<4000;i++)Tick();
                double stored=0;int wet=0;
                foreach(var cell in floor.Environment.Cells)if(cell.WaterLiters>.000001f){stored+=cell.WaterLiters;wet++;}
                Check(wet>1&&floor.Environment.DrainedWaterLiters>0,"localized source gradually spreads to drain in runtime");
                Check(Math.Abs(20-stored-floor.Environment.DrainedWaterLiters)<.002,"runtime floor liters plus physical drain conserve input");
                Capture("Service-floor-drained.png",W(-1.8f,2.8f,4.1f),W(-3.6f,0,5.8f));
                var sourceImage=new Texture2D(2,2);var drainedImage=new Texture2D(2,2);
                try
                {
                    sourceImage.LoadImage(File.ReadAllBytes("Assets/Experiments/IceResurfacer/Validation~/Service-floor-source.png"));
                    drainedImage.LoadImage(File.ReadAllBytes("Assets/Experiments/IceResurfacer/Validation~/Service-floor-drained.png"));
                    var sourcePixels=sourceImage.GetPixels32();var drainedPixels=drainedImage.GetPixels32();int changed=0;
                    for(int i=0;i<sourcePixels.Length;i++)if(Math.Abs(sourcePixels[i].r-drainedPixels[i].r)+Math.Abs(sourcePixels[i].g-drainedPixels[i].g)+Math.Abs(sourcePixels[i].b-drainedPixels[i].b)>6)changed++;
                    Check(changed>200,"finite puddle is visibly different after draining at identical camera state ("+changed+" pixels)");
                }
                finally{UnityEngine.Object.Destroy(sourceImage);UnityEngine.Object.Destroy(drainedImage);}
                results.Add("20 L explicit floor source after 80 simulated seconds: "+stored.ToString("F4")+" L retained, "+floor.Environment.DrainedWaterLiters.ToString("F4")+" L drained; "+wet+" nonzero cells.");
                results.Add("Fixture pose writes occur only at new-session setup. Approaches and recovery use normal throttle, and tank lifting uses its held hydraulic control.");
            }
            catch(Exception e){results.Add("FAIL "+e.Message);throw;}
            finally
            {File.WriteAllLines("Assets/Experiments/IceResurfacer/Validation~/Service-geometry-results.txt",results);game.NewSession(true,true);if(game.DiagnosticsVisible!=diagnostics)game.ToggleDiagnostics();}
        }
    }
}
