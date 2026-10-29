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
            ValidateTankEnvelope(game,render);
            ValidateSideEscape(game,render);
            game.NewSession(true);
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
            float fullDepth=snowMesh.bounds.max.y;
            float windowClearance=game.World.TankInspection.position.y-
                game.World.TankSnow.TransformPoint(0,fullDepth,0).y;
            Check(snowRenderer.enabled&&Mathf.Abs(fullDepth-s.Config.TankSnowMaxDepth)<.001f&&
                Mathf.Abs(snowMesh.bounds.size.x-s.Config.TankSnowTopWidth)<.002f,
                "full tank surface fits the authored lowered shell and tapered upper width");
            Check(windowClearance>.03f&&windowClearance<.20f,
                "full tank snow sits directly beneath the clear inspection opening");
            observations.Add("Authored full snow depth="+fullDepth.ToString("F4")+"m; opening clearance="+windowClearance.ToString("F4")+
                "m; physical envelope="+s.Config.TankSnowPhysicalCapacityM3.ToString("F4")+"m3; retained loose-equivalent capacity="+
                s.Config.SnowCapacityM3.ToString("F2")+"m3; compaction="+(s.Config.SnowCapacityM3/s.Config.TankSnowPhysicalCapacityM3).ToString("F3")+".");
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
                Check(ps.main.maxParticles<=(ps.name.Contains("side escape")?720:360),"bounded particle pool: "+ps.name);
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
            var pitVertices=pitMesh.vertices;var pitTriangles=pitMesh.triangles;double pitVolume=0;
            bool finitePile=true;
            foreach(var vertex in pitVertices)finitePile&=!float.IsNaN(vertex.y)&&!float.IsInfinity(vertex.y);
            Check(finitePile,"pit pile profile remains finite including fractional-power edge vertices");
            for(int i=0;i<pitTriangles.Length;i+=3)
            {
                var a=pitVertices[pitTriangles[i]];var b=pitVertices[pitTriangles[i+1]];var d=pitVertices[pitTriangles[i+2]];
                float area=Vector3.Cross(b-a,d-a).y*.5f;
                pitVolume+=area*(a.y+b.y+d.y)/3;
            }
            Check(Math.Abs(pitVolume-s.PitSnowM3)<.0001,"rounded pit mesh integrates to actual discharged receiver volume");
            Check(pitMesh.bounds.max.y>.1f,"actual removed tank contents form a visible pit pile");
            bool upwardPile=true;foreach(var normal in pitMesh.normals)upwardPile&=normal.y>=-.00001f;
            Check(upwardPile,"dump pile normals face upward");
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
            observations.Add("Working simulation + presentation CPU sample (240 ticks): "+(watch.Elapsed.TotalMilliseconds/240).ToString("F3")+" ms/tick; managed allocation="+allocated+" bytes; 663 reusable tank/pit/trough vertices, sparse441-vertex deposit chunks, three360 and two720-particle snow pools. Excludes IMGUI/GPU.");
            IceCameraValidation.Aim(21,29,true);
            foreach(var ps in game.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Simulate(.35f,true,false);
                if(ps.name.Contains("conveyed"))Check(ps.particleCount>0,"working auger emits actual snow into the bin");
            }
            IceResurfacerEditor.Capture(game.DriverCamera,"Operator-auger-discharge.png",1920,1080);
            File.WriteAllLines(Output+"Operations-results.txt",observations);
        }

        static void PlaceSnowPass(Core.Simulation s,float x,float z,float blade,bool augers)
        {
            s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=0;s.Vehicle.Speed=s.Vehicle.SteeringAngle=0;
            s.Conditioner.X=x;s.Conditioner.Z=z-s.Config.HitchBehindAxle-s.Config.DrawbarLength;s.Conditioner.Heading=0;
            s.Conditioner.Lift=0;s.Controls.HydraulicHoldControls=false;s.Controls.ConditionerTarget=0;
            s.Controls.BladeDepthMm=blade;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=augers;
            s.Controls.Throttle=.38f;s.Controls.Steer=s.Controls.FloodFlow=s.Controls.WashValve=0;s.Controls.Brake=false;
        }
        static double SurfaceSnow(Core.Simulation s)
        {double value=0;foreach(var cell in s.Ice.Cells)value+=cell.SnowMm*s.Ice.CellSize*s.Ice.CellSize*.001;return value;}
        static void ValidateTankEnvelope(IceResurfacerGame game,Action<float> render)
        {
            game.NewSession(true);var s=game.Sim;var config=s.Config;int poses=0;
            foreach(float fill in new[]{.05f,.50f,.85f,1f})foreach(float lift in new[]{0f,.65f,1f})
            {
                // Explicit mesh-envelope apparatus, separate from the following
                // source-conserving transfer and dumping tests.
                typeof(Core.Simulation).GetProperty("SnowTankM3").SetValue(s,fill*config.SnowCapacityM3);
                typeof(Core.Simulation).GetProperty("TankLift").SetValue(s,lift);render(.02f);
                var mesh=game.World.TankSnow.GetComponent<MeshFilter>().sharedMesh;
                bool contained=true;
                foreach(var vertex in mesh.vertices)
                {
                    Vector3 p=game.World.TankPivot.InverseTransformPoint(game.World.TankSnow.TransformPoint(vertex));
                    float along=Mathf.Clamp01((p.z+2.03f)/2.05f);
                    // Physical shell side corners and closed door inner face,
                    // independent of the snow-profile width helpers under test.
                    float shoulder=config.TankGeometryY(Mathf.Lerp(.80f,.75f,along));
                    float roof=config.TankGeometryY(Mathf.Lerp(1.025f,.94f,along));
                    float halfWidth=Mathf.Lerp(Mathf.Lerp(.763f,.720f,along),Mathf.Lerp(.64f,.60f,along),
                        Mathf.InverseLerp(shoulder,roof,p.y));
                    contained&=!float.IsNaN(p.y)&&p.z>=-2.012f&&p.z<=-.0254f&&p.y>=.0849f&&p.y<roof&&
                        Mathf.Abs(p.x-config.TankLateralOffset)<halfWidth;
                }
                Check(contained,"actual tank snow vertices fit floor, front/rear door and tapered sidewalls at fill "+fill.ToString("F2")+" / lift "+lift.ToString("F2"));
                poses++;
            }
            observations.Add("Tank envelope apparatus: "+poses+" partial/full and lowered/raised poses; finite mesh uses authored length "+
                config.TankSnowInteriorLength.ToString("F3")+"m and centerZ "+config.TankSnowCenterZ.ToString("F3")+
                "m. Conserved3.54m3 loose stock maps to "+config.TankSnowPhysicalCapacityM3.ToString("F6")+"m3 physical envelope.");
        }
        static void ValidateSideEscape(IceResurfacerGame game,Action<float> render)
        {
            game.NewSession(true);var s=game.Sim;PlaceSnowPass(s,0,-20,.8f,false);
            observations.Add("Side-escape fixture explicitly sets an initial center-ice pose; subsequent motion, cutting, accumulation and recovery use simulation ticks. No tank or snow is injected.");
            bool earlyRetained=false;
            for(int i=0;i<2300&&s.Vehicle.Z<19;i++)
            {
                s.Step(.02f);
                if(s.HorizontalSnowM3>.01f&&s.HorizontalSnowM3<s.Config.ConditionerSnowCapacityM3*.8f&&s.SpilledSnowM3==0)earlyRetained=true;
            }
            render(.02f);game.Surface.Refresh(s);
            Check(earlyRetained,"stopped augers visibly retain initial finite trough stock before any snow escape");
            Check(s.ConditionerLeftEscapeRate>0&&s.ConditionerRightEscapeRate>0,"shaving with horizontal auger OFF has simultaneous left/right material escape");
            Check(s.SnowTankM3==0&&s.HorizontalSnowM3<=s.Config.ConditionerSnowCapacityM3,"off-auger fixture cannot hide escaped snow in the tank or exceed trough capacity");
            int sidePools=0;
            foreach(var ps in game.GetComponentsInChildren<ParticleSystem>())if(ps.name.Contains("side escape"))
            {
                ps.Simulate(.3f,true,false);sidePools++;
                Check(ps.particleCount>0&&ps.main.maxParticles<=720,"bounded source-driven side snow clumps: "+ps.name);
            }
            Check(sidePools==2,"exactly two pooled conditioner-side emitters exist");
            Check(game.Surface.DepositedSnowChunks>0&&game.Surface.DepositedSnowMaximumHeight>.002f,
                "actual escaped bulk material creates sparse three-dimensional snow-height meshes");
            float beforeHeight=game.Surface.DepositedSnowMaximumHeight;
            var trough=game.World.Conditioner.Find("Snow bunching in the finite conditioner trough");
            Check(trough!=null&&trough.GetComponent<MeshRenderer>().enabled,"finite accumulated trough snow is rendered behind the auger grille");
            double before=SurfaceSnow(s),escaped=s.SpilledSnowM3;float end=s.Vehicle.Z;
            var cameraObject=new GameObject("Snow side-escape evidence camera");var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.fieldOfView=55;camera.nearClipPlane=.04f;camera.farClipPlane=100;
            camera.aspect=1920f/1080f;
            camera.transform.position=game.World.Conditioner.TransformPoint(-3.7f,2.3f,-4.2f);
            camera.transform.LookAt(game.World.Conditioner.position+Vector3.up*.12f);
            IceResurfacerEditor.Capture(camera,"Snow-disabled-augers-both-sides.png",1920,1080);
            camera.orthographic=true;camera.orthographicSize=6;
            // Keep the same orthographic world bounds, but place the camera
            // below the 12.7 m opaque roof and its 10.4 m truss lower chords.
            camera.transform.position=new Vector3(0,8,end-6);camera.transform.rotation=Quaternion.Euler(90,0,0);
            IceResurfacerEditor.Capture(camera,"Snow-windrows-before-recovery.png",1920,1080);
            for(int pass=0;pass<2;pass++)for(int side=-1;side<=1;side+=2)
            {
                PlaceSnowPass(s,side*1.265f,-20,0,true);
                for(int i=0;i<2200&&s.Vehicle.Z<end;i++)s.Step(.02f);
            }
            s.Controls.BladeDepthMm=s.Controls.Throttle=0;s.Controls.Brake=true;
            for(int i=0;i<600;i++)s.Step(.02f);
            render(.02f);game.Surface.Refresh(s);
            double recovered=before-SurfaceSnow(s);
            Check(recovered>escaped*.8&&s.SnowTankM3>escaped,"enabled augers recover persistent side windrows with the blade retracted");
            Check(s.ConditionerLeftEscapeRate==0&&s.ConditionerRightEscapeRate==0,"side emission stops when actual escaping transfer stops");
            Check(game.Surface.DepositedSnowMaximumHeight<beforeHeight,
                "recovery reduces the same dirty chunk mesh heights as conserved surface snow");
            IceResurfacerEditor.Capture(camera,"Snow-windrows-after-recovery.png",1920,1080);
            ValidateRecoveryImages(camera,end);
            observations.Add("Live side fixture escaped "+escaped.ToString("F6")+" m3; subsequent surface pickup removed "+recovered.ToString("F6")+" m3 including incidental original loose snow. Detailed conservation and empty-revisit assertions are in the core snow probe.");
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
        static void ValidateRecoveryImages(Camera camera,float end)
        {
            var before=new Texture2D(2,2);var after=new Texture2D(2,2);
            try
            {
                before.LoadImage(File.ReadAllBytes(Output+"Snow-windrows-before-recovery.png"));
                after.LoadImage(File.ReadAllBytes(Output+"Snow-windrows-after-recovery.png"));
                var a=before.GetPixels32();var b=after.GetPixels32();
                int lowA=765,highA=0,lowB=765,highB=0;
                for(int i=0;i<a.Length;i+=16)
                {
                    int ca=a[i].r+a[i].g+a[i].b,cb=b[i].r+b[i].g+b[i].b;
                    lowA=Math.Min(lowA,ca);highA=Math.Max(highA,ca);
                    lowB=Math.Min(lowB,cb);highB=Math.Max(highB,cb);
                }
                Check(highA-lowA>90&&highB-lowB>90,
                    "both recovery images contain visible rink content instead of a uniform roof or clear color");
                double whiteReduction=0;int samples=0,changed=0;bool framed=true;
                // Sample the actual two deposited side bands, away from the
                // machine at the northern image edge. Pose changes cannot satisfy
                // this check by merely moving the vehicle silhouette.
                for(int side=-1;side<=1;side+=2)for(int z=0;z<=80;z++)for(int x=0;x<=8;x++)
                {
                    Vector3 p=camera.WorldToViewportPoint(new Vector3(side*1.265f+Mathf.Lerp(-.10f,.10f,x/8f),0,
                        Mathf.Lerp(end-10,end-2,z/80f)));
                    if(p.z<=0||p.x<0||p.x>=1||p.y<0||p.y>=1){framed=false;continue;}
                    int pixel=(int)(p.y*before.height)*before.width+(int)(p.x*before.width);
                    int delta=a[pixel].r+a[pixel].g+a[pixel].b-b[pixel].r-b[pixel].g-b[pixel].b;
                    whiteReduction+=delta/3.0;samples++;if(delta>6)changed++;
                }
                Check(framed&&samples>0,"all recovery comparison samples remain inside the fixed world view");
                Check(whiteReduction/samples>3&&changed>samples*.50,
                    "actual side-band pixels visibly lose deposited white snow after collection");
                observations.Add("Recovery image audit: identical orthographic world bounds, camera height 8 m below roof; "+
                    changed+" / "+samples+" side-band samples lose visible snow, mean RGB reduction "+
                    (whiteReduction/samples).ToString("F2")+" / 255. Material recovery remains independently measured.");
            }
            finally{UnityEngine.Object.Destroy(before);UnityEngine.Object.Destroy(after);}
        }
    }
}
