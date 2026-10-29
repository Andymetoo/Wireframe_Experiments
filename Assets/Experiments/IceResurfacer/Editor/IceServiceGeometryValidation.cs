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
            game.GetComponentInChildren<IceCollisionOverlay>()?.Refresh();
            var node=new GameObject("Service geometry evidence");var camera=node.AddComponent<Camera>();camera.enabled=false;
            camera.fieldOfView=58;camera.nearClipPlane=.04f;camera.farClipPlane=150;
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            IceResurfacerEditor.Capture(camera,name,1920,1080);UnityEngine.Object.Destroy(node);
        }
        static void ContactCaptures()
        {
            int voices=0;
            foreach(var source in game.GetComponentsInChildren<AudioSource>())
                if(source.name.StartsWith("Physical contact voice ",StringComparison.Ordinal))
                {voices++;Check(source.spatialBlend==1&&source.dopplerLevel==0,"impact voice is spatial at actual contact without artificial Doppler");}
            Check(voices==3,"contact audio uses three bounded reused sources");
            foreach(string scenario in new[]{"slow-direct","hard-direct","fast-glance","service-wall"})
            {
                game.NewSession(true,true);var s=game.Sim;
                float speed=scenario=="slow-direct"?.3f:scenario=="service-wall"?.6f:2;
                if(scenario=="service-wall"){s.Layout.ToWorld(0,9,out s.Vehicle.X,out s.Vehicle.Z);s.Vehicle.Heading=s.Layout.OutwardHeading;}
                else{s.Vehicle.X=scenario=="fast-glance"?-11.6f:0;s.Vehicle.Z=scenario=="fast-glance"?-10:26;s.Vehicle.Heading=scenario=="fast-glance"?-.08f:0;}
                s.Vehicle.Speed=speed;s.Controls.Throttle=speed/s.Config.ForwardSpeed;
                for(int i=0;i<2500&&s.ContactEventSequence==0;i++)Tick();
                Check(s.ContactEventSequence>0,"physical contact severity event: "+scenario);
                results.Add(scenario+": normal "+s.AudibleContactNormalSpeed.ToString("F3")+" m/s, severity "+s.AudibleContactSeverity.ToString("F3")+", roller="+s.AudibleContact.GuideRoller+", obstacle="+s.AudibleContact.Obstacle);
                var hit=s.AudibleContact;Vector3 point=new Vector3(hit.PointX,.4f,hit.PointZ);
                Vector3 inward=new Vector3(hit.NormalX,0,hit.NormalZ);
                Capture("Contact-"+scenario+".png",point+inward*5+Vector3.up*3-new Vector3(inward.z,0,-inward.x)*2,point);
            }
        }
        static void ConditionerCaptures()
        {
            game.NewSession(true,true);var s=game.Sim;s.Vehicle.X=0;s.Vehicle.Z=-12;s.Vehicle.Heading=0;
            s.Conditioner.X=0;s.Conditioner.Z=-13.1f;s.Conditioner.Heading=0;
            s.Controls.Throttle=.4f;s.Controls.ConditionerTarget=0;
            for(int i=0;i<250;i++)Tick();
            Vector3 center=new Vector3(s.Vehicle.X,0,s.Vehicle.Z);
            Capture("Conditioner-straight.png",center+new Vector3(.01f,7,-.5f),center+new Vector3(0,0,.5f));
            s.Controls.Steer=1;for(int i=0;i<250;i++)Tick();
            float yaw=Mathf.DeltaAngle(s.Vehicle.Heading*Mathf.Rad2Deg,s.Conditioner.Heading*Mathf.Rad2Deg);
            Check(Mathf.Abs(yaw)<=s.Config.ConditionerYawLimitRadians*Mathf.Rad2Deg+.001f,"rendered curved conditioner is limited to close-mounted yaw compliance");
            Check(Quaternion.Angle(game.World.Conditioner.rotation,Quaternion.Euler(0,s.Conditioner.Heading*Mathf.Rad2Deg,0))<.01f,"rendered conditioner uses processing heading");
            center=new Vector3(s.Vehicle.X,0,s.Vehicle.Z);
            Capture("Conditioner-curved.png",center+new Vector3(.01f,7,-.5f),center+new Vector3(0,0,.5f));
            results.Add("Curved conditioner relative yaw: "+yaw.ToString("F3")+" degrees; lift "+s.Conditioner.Lift.ToString("F3")+".");
        }
        public static void Run(IceResurfacerGame target)
        {
            game=target;results.Clear();bool diagnostics=game.DiagnosticsVisible;
            try
            {
                foreach(float angle in new[]{0f,-15f,15f,-30f,30f,90f})
                {
                    game.NewSession(true,true);var s=game.Sim;
                    s.Layout.ToWorld(angle==90?5:(float)Math.Sin(angle*Math.PI/180)*3,angle==90?11.5f:6.3f,out s.Vehicle.X,out s.Vehicle.Z);
                    s.Vehicle.Heading=s.Layout.OutwardHeading+angle*Mathf.Deg2Rad;s.Controls.Throttle=.16f;
                    bool crossed=false,clear=true;
                    for(int i=0;i<3000;i++)
                    {
                        Tick();s.Layout.ToLocal(s.Vehicle.X,s.Vehicle.Z,out float side,out float outward);
                        crossed|=s.Layout.ContainsPit(s.Vehicle.X+Mathf.Sin(s.Vehicle.Heading)*s.Config.BodyFront,s.Vehicle.Z+Mathf.Cos(s.Vehicle.Heading)*s.Config.BodyFront);
                        clear&=!s.BoardContact;
                        if((angle==90&&side<-2.6f)||(angle!=90&&outward>10.85f))break;
                    }
                    Check(clear&&crossed&&s.Vehicle.Speed>.1f&&s.ContactEventSequence==0,"moving across actual grate creates no collision event at "+angle+" degrees");
                    if(!game.DiagnosticsVisible)game.ToggleDiagnostics();
                    var overlay=game.GetComponentInChildren<IceCollisionOverlay>();Check(overlay!=null,"fixed collision overlay initialized");overlay.Refresh();
                    int overlayLines=0;foreach(var line in overlay.GetComponentsInChildren<LineRenderer>())if(line.name.StartsWith("F3 ",StringComparison.Ordinal))overlayLines++;
                    Check(overlayLines==6,"diagnostics retain bounded reused line renderers");
                    if(angle==0||angle==30||angle==90)Capture("Grate-traversal-"+angle+"deg.png",W(-4.6f,4.0f,6.6f),W(0,.55f,10.8f));
                }
                ContactCaptures();
                ConditionerCaptures();
                game.NewSession(true,true);var raised=game.Sim;
                raised.Vehicle.X=10.1f;raised.Vehicle.Z=0;raised.Vehicle.Heading=Mathf.PI*.5f;
                raised.SetTankHydraulic(1);for(int i=0;i<350;i++)Tick();
                Check(raised.TankLift>.03f&&raised.TankLift<.99f&&raised.LastMovementContact.Obstacle==MovementObstacle.TankGlass,"raising near actual glass stops the moving shell at contact");
                Check(raised.LastMovementContact.PointY<2.55f&&raised.LastMovementContact.NormalX<-.99f,"contact is below glass top and normal points onto ice");
                Check(Quaternion.Angle(game.World.TankPivot.localRotation,Quaternion.Euler(raised.TankLift*58,0,0))<.01f,"rendered tank shares core 58 degree lift geometry");
                Check(Vector3.Distance(game.World.TankPivot.localPosition,new Vector3(0,.78f,2.46f))<.001f&&Vector3.Distance(game.World.TankDoor.localPosition,new Vector3(raised.Config.TankLateralOffset,raised.Config.TankGeometryY(1.02f),.002f))<.001f,"core and rendered moving contact pivots agree");
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
                floor.Environment.Deposit(sourceX,sourceZ,.35f,0);
                game.WaterService.UpdateViewAndEffects(true,.21f);
                MeshFilter snowFloor=null;
                foreach(var filter in game.WaterService.GetComponentsInChildren<MeshFilter>())
                    if(filter.name=="Spatial dumped floor snow / conserved volume")snowFloor=filter;
                Check(snowFloor!=null,"continuous floor snow surface exists");
                var snowMesh=snowFloor.sharedMesh;var vertices=snowMesh.vertices;var triangles=snowMesh.triangles;
                double visibleVolume=0;float highest=0,minimumNormal=1;
                foreach(var normal in snowMesh.normals)minimumNormal=Mathf.Min(minimumNormal,normal.y);
                foreach(var vertex in vertices)highest=Mathf.Max(highest,vertex.y);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                    float signedArea=Vector3.Cross(b-a,c-a).y*.5f;
                    if(signedArea<-.000001f)throw new InvalidOperationException("Floor snow triangle faces downward");
                    visibleVolume+=signedArea*(Mathf.Max(0,a.y-.0005f)+Mathf.Max(0,b.y-.0005f)+Mathf.Max(0,c.y-.0005f))/3;
                }
                Check(highest>.03f&&minimumNormal>=-.00001f,"floor pile has visible height and outward/upward normals");
                Check(Math.Abs(visibleVolume-.35)<.001,"continuous floor snow mesh integrates to the conserved deposited0.35m3");
                Capture("Service-floor-snow.png",W(-1.8f,2.8f,4.1f),W(-3.6f,0,5.8f));
                results.Add("Explicit floor-snow apparatus deposit0.350m3: rendered triangle integral="+visibleVolume.ToString("F6")+
                    "m3; maximumheight="+highest.ToString("F4")+"m; minimum upwardnormal="+minimumNormal.ToString("F4")+". Runtime dumping is validated separately.");
                results.Add("Fixture pose writes occur only at new-session setup. Approaches and recovery use normal throttle, and tank lifting uses its held hydraulic control.");
            }
            catch(Exception e){results.Add("FAIL "+e.Message);throw;}
            finally
            {File.WriteAllLines("Assets/Experiments/IceResurfacer/Validation~/Service-geometry-results.txt",results);game.NewSession(true,true);if(game.DiagnosticsVisible!=diagnostics)game.ToggleDiagnostics();}
        }
    }
}
