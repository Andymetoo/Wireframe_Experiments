// Disposable validation-only replacement for the probe already attached to the
// packaged Mono layout player. Never imported by the production Unity project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    public sealed class IceLayoutPlayerProbe : MonoBehaviour
    {
        IceResurfacerGame game;
        string output, current,suite="After";
        bool active, finished, sliderLogged,showUI=true;
        readonly List<string> issues = new List<string>();
        readonly List<string> errors = new List<string>();
        readonly List<string> facts = new List<string>();
        float began;

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i=0; i+1<args.Length; i++)
            {
                if (args[i]=="--aurora-validation-output") output=Path.GetFullPath(args[i+1]);
                if (args[i]=="--aurora-validation-suite")suite=args[i+1];
            }
            if (string.IsNullOrEmpty(output)) { enabled=false; yield break; }
            if (!output.Replace('\\','/').Contains("/Validation~/OperatorFeedback-20261105"))
                throw new InvalidOperationException("UI probe requires its dedicated validation folder.");
            output=Path.Combine(output,suite,"Player-"+Screen.width+"x"+Screen.height);
            Directory.CreateDirectory(output);
            Application.runInBackground=true;
            Application.logMessageReceived+=Log;
            Debug.developerConsoleEnabled=false;
            began=Time.realtimeSinceStartup;
            active=true;
            while(game==null||game.Sim==null||game.Dashboard==null||game.RouteGuide==null)
            {game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();yield return null;}
            game.enabled=false;
            game.RouteGuide.ConfigureValidationStorage(Path.Combine(output,"Routes"));
            game.RouteGuide.enabled=false;
            game.Dashboard.AuditLayout=true;
            yield return new WaitForSecondsRealtime(2);
            if(suite.StartsWith("Before",StringComparison.Ordinal))
            {yield return SnowFixtures();Complete();yield break;}
            game.NewSession(false,false);
            yield return Capture("01-welcome");
            game.NewSession(false);
            yield return Capture("02-hud");
            game.Dashboard.Compact=false;
            yield return Capture("03-console");
            game.TogglePause();
            yield return Capture("04-pause");
            game.TogglePause();
            game.ToggleHelp();
            yield return Capture("05-help");
            game.ToggleHelp();
            game.ToggleDiagnostics();
            yield return Capture("06-diagnostics");
            game.ToggleDiagnostics();
            game.RouteGuide.ToggleRecordingPanel();
            yield return Capture("07-routes");
            game.RouteGuide.ShowRawDetails();
            yield return Capture("08-route-recording");
            game.RouteGuide.ShowTrainingPanel();
            yield return Capture("09-training-guide");
            game.RouteGuide.ToggleRecordingPanel();
            game.Finish();
            yield return Capture("10-results");
            yield return GpsFixture();
            yield return SnowFixtures();
            MeshResizeChecks();
            Complete();
        }
        static readonly System.Reflection.BindingFlags Private=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        void SyncWorld(){typeof(IceResurfacerGame).GetMethod("UpdateMachine",Private).Invoke(game,new object[]{0f});}
        void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);facts.Add("PASS "+message);}
        void ClearSnow()
        {
            var field=game.Sim.Ice;
            for(int i=0;i<field.Cells.Length;i++){var cell=field.Cells[i];cell.SnowMm=0;field.Cells[i]=cell;field.MarkCellChanged(i);}
            field.MarkChanged();
        }
        void RefreshSnow()
        {
            game.Surface.Invalidate();game.Surface.Refresh(game.Sim);
            for(int i=0;i<64;i++)game.Surface.Refresh(game.Sim);
        }
        static double FieldStock(IceField field)
        {double stock=0;foreach(var cell in field.Cells)if(cell.IsIce)stock+=cell.SnowMm*.001*field.CellSize*field.CellSize;return stock;}
        static string FieldFingerprint(IceField field)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {foreach(var cell in field.Cells)writer.Write(cell.SnowMm);writer.Flush();using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","");}
        }
        void MeshFacts(string fixture)
        {
            facts.Add(fixture+" field_m3="+FieldStock(game.Sim.Ice).ToString("R")+" visible_m3="+game.Surface.DepositedSnowVisibleVolumeM3.ToString("R")+
                " maximum_m="+game.Surface.DepositedSnowMaximumHeight+" vertices="+game.Surface.DepositedSnowVertices+" field_sha256="+FieldFingerprint(game.Sim.Ice));
        }
        IEnumerator SnowFixtures()
        {
            showUI=false;
            var camera=game.DriverCamera;var parent=camera.transform.parent;
            var localPosition=camera.transform.localPosition;var localRotation=camera.transform.localRotation;float fov=camera.fieldOfView;
            var rendererList=new List<Renderer>(game.World.Machine.GetComponentsInChildren<Renderer>());
            rendererList.AddRange(game.World.Conditioner.GetComponentsInChildren<Renderer>());
            var machineRenderers=rendererList.ToArray();var enabled=new bool[machineRenderers.Length];
            for(int i=0;i<machineRenderers.Length;i++){enabled[i]=machineRenderers[i].enabled;machineRenderers[i].enabled=false;}
            game.NewSession(false);game.RouteGuide.Refresh();if(game.RouteGuide.Visible)game.RouteGuide.Toggle();
            camera.transform.SetParent(null,true);camera.fieldOfView=48;
            var s=game.Sim;ClearSnow();
            s.Vehicle.X=0;s.Vehicle.Z=-10;s.Vehicle.Heading=0;s.Vehicle.Speed=0;
            s.Conditioner.X=0;s.Conditioner.Z=-10-s.Config.HitchBehindAxle-s.Config.DrawbarLength;s.Conditioner.Heading=0;
            s.Controls.Throttle=0;s.Controls.Brake=true;
            typeof(Simulation).GetProperty("SnowTankM3").SetValue(s,s.Config.SnowCapacityM3,null);
            float seeded=s.SnowTankM3;s.SetTankHydraulic(1);
            for(int i=0;i<2000;i++)s.Step(.02f);
            s.SetTankHydraulic(0);SyncWorld();RefreshSnow();
            Require(s.DumpSnowOnIceM3>seeded*.95f,"Real full-bin hydraulic dump delivered >95% of seeded stock onto ice");
            Require(Math.Abs(s.SnowTankM3+FieldStock(s.Ice)-seeded)<.0001,"Full-bin dump snow mass balances within 0.1 L");
            facts.Add("Dump seed_m3="+seeded+" retained_m3="+s.SnowTankM3+" deposited_m3="+s.DumpSnowOnIceM3+" lift="+s.TankLift+" landing="+s.DumpLandingX+","+s.DumpLandingZ);
            MeshFacts("Actual full-bin dump");
            for(int i=0;i<machineRenderers.Length;i++)machineRenderers[i].enabled=false;
            var target=new Vector3(s.DumpLandingX,4.5f,s.DumpLandingZ);
            camera.fieldOfView=55;camera.transform.position=new Vector3(target.x+12,8,target.z-15);camera.transform.LookAt(target);
            yield return Capture("20-full-bin-dump-wide");
            camera.fieldOfView=48;
            camera.transform.position=new Vector3(target.x+5,2.6f,target.z-6);camera.transform.LookAt(new Vector3(target.x,.65f,target.z));
            yield return Capture("21-full-bin-dump-ground");
            ClearSnow();s.Ice.DepositSnow(0,0,1.5f,1.25f);RefreshSnow();MeshFacts("Fixed 1.5 m3 radius1.25 deposition");
            Require(Math.Abs(FieldStock(s.Ice)-1.5)<.00001,"Fixed deposited mound holds 1.5 m3 exactly within 0.01 L");
            camera.transform.position=new Vector3(2.8f,1.4f,-2.8f);camera.transform.LookAt(new Vector3(0,.4f,0));
            yield return Capture("22-identical-stock-mound");
            camera.transform.position=new Vector3(0,3.8f,-.6f);camera.transform.LookAt(new Vector3(0,.2f,0));
            yield return Capture("23-identical-stock-mound-top");
            for(int i=0;i<machineRenderers.Length;i++)if(machineRenderers[i]!=null)machineRenderers[i].enabled=enabled[i];
            camera.transform.SetParent(parent,false);camera.transform.localPosition=localPosition;camera.transform.localRotation=localRotation;camera.fieldOfView=fov;
            showUI=true;
        }
        IEnumerator GpsFixture()
        {
            game.NewSession(false);game.RouteGuide.enabled=true;game.RouteGuide.PrepareTrainingPreview();
            var fit=game.RouteGuide.ActiveFit;Require(fit!=null,"GPS reference route available");
            GuideSample chosen=default;int selected=-1;
            for(int i=0;i<fit.samples.Count;i++)if(fit.samples[i].cutting&&fit.samples[i].direction>0&&fit.samples[i].distance>45){chosen=fit.samples[i];selected=i;break;}
            Require(selected>=0,"GPS fixture uses a forward working route sample");
            var s=game.Sim;s.Vehicle.X=chosen.x;s.Vehicle.Z=chosen.z;s.Vehicle.Heading=chosen.heading;s.Vehicle.Speed=0;
            s.Conditioner.X=chosen.x-Mathf.Sin(chosen.heading)*(s.Config.HitchBehindAxle+s.Config.DrawbarLength);
            s.Conditioner.Z=chosen.z-Mathf.Cos(chosen.heading)*(s.Config.HitchBehindAxle+s.Config.DrawbarLength);s.Conditioner.Heading=chosen.heading;
            SyncWorld();game.SelectCameraView("Forward");game.TickInspectionCamera(2);
            if(!game.RouteGuide.Visible)game.RouteGuide.Toggle();game.RouteGuide.RejoinLeg(chosen.leg);
            game.RouteGuide.Progress.Observe(chosen.x,chosen.z,chosen.heading,.1f);game.RouteGuide.Refresh();
            game.Dashboard.Compact=true;yield return Capture("11-driver-gps-alignment");
            var property=typeof(IceRouteGuide).GetProperty("MachineAlignment");Require(property!=null,"GPS machine alignment component exists");
            var alignment=property.GetValue(game.RouteGuide,null);var type=alignment.GetType();
            int segments=(int)type.GetProperty("LastVisibleSegments").GetValue(alignment,null),rays=(int)type.GetProperty("LastRayQueries").GetValue(alignment,null);
            Require(segments>0&&rays<=65,"Forward driver yellow alignment visible: segments="+segments+", ray queries="+rays+" <=65");
            game.RouteGuide.Toggle();game.RouteGuide.enabled=false;
        }
        void MeshResizeChecks()
        {
            var field=new IceField(new SimulatorConfig(),546);
            for(int i=0;i<field.Cells.Length;i++){var cell=field.Cells[i];cell.SnowMm=0;field.Cells[i]=cell;}
            var host=new GameObject("Validation-only snow resize");host.transform.position=new Vector3(200,0,200);
            var view=new IceDepositedSnowView(field,host.transform);
            int x0=field.Columns/2/40*40+4,z0=field.Rows/2/40*40+4;
            try
            {
                foreach(int side in new[]{10,6,12,4,2,0,1,10,2,0})
                {
                    for(int z=0;z<12;z++)for(int x=0;x<12;x++)
                    {int i=field.Index(x0+x,z0+z);var cell=field.Cells[i];cell.SnowMm=x<side&&z<side?30:0;field.Cells[i]=cell;field.MarkCellChanged(i);view.MarkSnowChanged(field,i);}
                    field.MarkChanged();for(int i=0;i<64;i++)view.Refresh(field,false);
                    int count=0;
                    foreach(var filter in host.GetComponentsInChildren<MeshFilter>())
                    {
                        var mesh=filter.sharedMesh;count+=mesh.vertexCount;
                        Require(mesh.normals.Length==mesh.vertexCount,"Mesh normals match vertices at side="+side);
                        foreach(var normal in mesh.normals)if(float.IsNaN(normal.x)||float.IsNaN(normal.y)||float.IsNaN(normal.z))throw new Exception("Nonfinite snow normal");
                        foreach(int index in mesh.triangles)if(index<0||index>=mesh.vertexCount)throw new Exception("Stale snow mesh index");
                    }
                    Require(count==side*side*SnowSurfaceGeometry.VerticesPerPatch,"Native mesh shrink/grow/empty occupancy side="+side);
                    Require(Math.Abs(view.VisibleVolumeM3-FieldStock(field))<1e-8,"Native mesh volume matches stock at side="+side);
                }
                int bx=field.Columns/2/40*40,bz=field.Rows/2/40*40;
                for(int z=bz-3;z<=bz+3;z++)for(int x=bx-3;x<=bx+3;x++)
                {int i=field.Index(x,z);var cell=field.Cells[i];cell.SnowMm=80+(x-bx)*8;field.Cells[i]=cell;field.MarkCellChanged(i);view.MarkSnowChanged(field,i);}
                field.MarkChanged();for(int i=0;i<64;i++)view.Refresh(field,false);
                int changed=field.Index(bx+1,bz+1);var mutated=field.Cells[changed];mutated.SnowMm+=55;field.Cells[changed]=mutated;
                field.MarkCellChanged(changed);field.MarkChanged();view.MarkSnowChanged(field,changed);
                for(int i=0;i<64;i++)view.Refresh(field,false);
                var shared=new Dictionary<Vector2,Tuple<Vector3,Vector3,string>>();int seams=0;
                foreach(var filter in host.GetComponentsInChildren<MeshFilter>())
                {
                    var vertices=filter.sharedMesh.vertices;var normals=filter.sharedMesh.normals;
                    for(int n=0;n<vertices.Length;n+=SnowSurfaceGeometry.VerticesPerPatch)
                    {
                        var first=vertices[n];int x=Mathf.RoundToInt((first.x-field.MinX)/field.CellSize),z=Mathf.RoundToInt((first.z-field.MinZ)/field.CellSize);
                        if(!SnowSurfaceGeometry.Build(field,x,z,.7f,out var patch))throw new Exception("Cached mesh owns a non-existent fresh patch");
                        for(int j=0;j<SnowSurfaceGeometry.VerticesPerPatch;j++)
                        {
                            var p=patch.Point(j);if(Vector3.Distance(vertices[n+j],new Vector3(p.X,p.Y+.0002f,p.Z))>.00002f)throw new Exception("Cached neighbor differs from fresh geometry after two-cell-halo edit");
                            if(j>=4)continue;var key=new Vector2(vertices[n+j].x,vertices[n+j].z);
                            if(shared.TryGetValue(key,out var other)&&other.Item3!=filter.name)
                            {if(Vector3.Distance(vertices[n+j],other.Item1)>.00002f||Vector3.Distance(normals[n+j],other.Item2)>.00002f)throw new Exception("Chunk boundary seam mismatch");seams++;}
                            else shared[key]=Tuple.Create(vertices[n+j],normals[n+j],filter.name);
                        }
                    }
                }
                Require(seams>0,"Shared chunk-boundary positions/normals agree after two-cell-halo mutation; seam pairs="+seams);
                Require(true,"All cached uploaded patches equal fresh uncached geometry after halo mutation");
            }
            finally{view.Dispose();UnityEngine.Object.Destroy(host);}
        }
        IEnumerator Capture(string name)
        {
            current=name;
            Debug.developerConsoleVisible=false;
            yield return null;
            Debug.developerConsoleVisible=false;
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            if(texture==null)throw new InvalidOperationException("No Unity framebuffer for "+name);
            File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            if(showUI)foreach(var issue in game.Dashboard.LayoutIssues)issues.Add(name+": "+issue);
            if(game.RouteGuide.PanelOpen)
                foreach(var issue in game.RouteGuide.Manager.LayoutIssues)issues.Add(name+": "+issue);
        }
        void OnGUI()
        {
            if(!active||finished||game==null||game.Dashboard==null||game.enabled)return;
            Debug.developerConsoleVisible=false;
            if(!showUI)return;
            try { game.Dashboard.Draw(game,Screen.width,Screen.height); SliderMetrics(); }
            catch(Exception e){errors.Add(current+": "+e);Complete();}
        }
        void SliderMetrics()
        {
            if(!game.Paused||sliderLogged)return;
            var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
            var slider=(GUIStyle)typeof(IceUITheme).GetField("slider",flags).GetValue(null);
            var thumb=(GUIStyle)typeof(IceUITheme).GetField("sliderThumb",flags).GetValue(null);
            if(slider==null||thumb==null)return;
            sliderLogged=true;
            var lines=new List<string>();
            void Inspect(string name,GUIStyle style)
            {
                lines.Add(name+" fixedHeight="+style.fixedHeight+" fixedWidth="+style.fixedWidth+" alignment="+style.alignment);
                lines.Add("  padding="+style.padding+" margin="+style.margin+" overflow="+style.overflow+" border="+style.border);
            }
            Inspect("Inherited slider",GUI.skin.horizontalSlider);Inspect("Inherited thumb",GUI.skin.horizontalSliderThumb);
            Inspect("Theme slider",slider);Inspect("Theme thumb",thumb);
            var control=IceDashboard.ControlRect("SpeedSlider");
            var inner=thumb.margin.Remove(slider.padding.Remove(control));
            lines.Add("Last drawn speed-slider control="+control+" native thumb y="+inner.y+" height="+thumb.fixedHeight);
            lines.Add("Track style overflow extents="+slider.overflow.Add(control));
            File.WriteAllLines(Path.Combine(output,"slider-metrics.txt"),lines);
        }
        void Update()
        {
            if(active&&!finished)Debug.developerConsoleVisible=false;
            if(active&&!finished&&Time.realtimeSinceStartup-began>120)
            {errors.Add("Timed out before all UI captures completed.");Complete();}
        }
        void Log(string message,string stack,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)
                errors.Add(type+": "+message+"\n"+stack);
        }
        void Complete()
        {
            if(finished)return;finished=true;
            Application.logMessageReceived-=Log;
            var report=new List<string>{
                "Actual Unity "+Application.unityVersion+" Mono player framebuffer / "+Screen.width+"x"+Screen.height,
                suite+" runtime source rebuilt against the existing player's own Managed assemblies; see native-source-manifest-"+suite+".json.",
                "Validation-only configuration serialization shim preserves the old packaged scene schema; production source is unchanged.",
                "The packaged scene/assets are from an earlier validation build. Snow fixtures use the named source snapshot and isolated stock; detached viewpoints hide machine geometry for unobstructed comparison.",
                "No full Editor import, current scene fidelity, new interactions, or focused-window latency verified by this probe.",
                "Measured layout issues: "+issues.Count, "Runtime errors: "+errors.Count
            };
            report.AddRange(issues);report.AddRange(errors);report.AddRange(facts);
            File.WriteAllLines(Path.Combine(output,"UI-results.txt"),report);
            Application.Quit(errors.Count==0&&issues.Count==0?0:1);
        }
    }
}
