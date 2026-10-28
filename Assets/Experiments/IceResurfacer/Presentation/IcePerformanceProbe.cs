#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using IceResurfacer.Core;
using Unity.Profiling;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    // Explicit development-player benchmark. It is never attached to the launch scene.
    public sealed class IcePerformanceProbe : MonoBehaviour
    {
        readonly string[] scenarios={"driving_full_hud","resurfacing_full_hud","resurfacing_compact","snow_overflow","camera_changes","service_walking","hose_filling","hose_overflow","dump_shakeout","long_session","on_foot_movement","late_wet_sheet"};
        readonly List<double> frameMs=new List<double>(1500), cpuMs=new List<double>(1500), gpuMs=new List<double>(1500);
        readonly StringBuilder report=new StringBuilder();
        readonly FrameTiming[] timings=new FrameTiming[1];
        IceResurfacerGame game;
        Action<float> render;
        Action<Vector3> moveFoot;
        Action applyFoot;
        ProfilerRecorder gc, draws;
        string output;
        int scenario=-1, frame, errors, gcStart, enduranceFrames=1500;
        long allocated, maxAllocated, simulationAllocated;
        double lastFrame, cpuTotal, memoryStart, surfaceRefreshTotal;
        Vector3 bayStart; float bayHeading;
        bool active, finishing, lateWetSheet;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)
            {
                if(args[i]=="--aurora-performance-output")output=args[i+1];
                if(args[i]=="--aurora-endurance-frames")enduranceFrames=Mathf.Clamp(int.Parse(args[i+1]),1500,100000);
            }
            if(string.IsNullOrEmpty(output)){enabled=false;return;}
            Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=-1;
            Debug.developerConsoleEnabled=false;
            game=FindAnyObjectByType<IceResurfacerGame>();game.enabled=false;
            render=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),game,typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic));
            moveFoot=(Action<Vector3>)Delegate.CreateDelegate(typeof(Action<Vector3>),game.WaterService,typeof(IceWaterService).GetMethod("MoveFoot",BindingFlags.Instance|BindingFlags.NonPublic));
            applyFoot=(Action)Delegate.CreateDelegate(typeof(Action),game.WaterService,typeof(IceWaterService).GetMethod("ApplyFootCamera",BindingFlags.Instance|BindingFlags.NonPublic));
            gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1);
            Application.logMessageReceived+=Log;
            Directory.CreateDirectory(output);
            report.AppendLine("Aurora explicit rendered-workload benchmark / "+DateTime.UtcNow.ToString("u"));
            report.AppendLine("Unity "+Application.unityVersion+" / "+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType+" / "+Screen.width+"x"+Screen.height);
            report.AppendLine("OS "+SystemInfo.operatingSystem+" / CPU "+SystemInfo.processorType+" / development Mono player, VSync off, hidden offscreen window.");
            report.AppendLine("One deterministic 20 ms simulation tick per rendered frame, live machine/ice/hose effects and IMGUI. Fixed workload throughput, not focused-window input latency or an Editor FPS claim. 90 warmup + 300 measured frames each; long session "+enduranceFrames+" measured frames. Setup seeds poses/tanks solely to profile reproducible loads.");
            frameMs.Capacity=cpuMs.Capacity=gpuMs.Capacity=enduranceFrames+1;
            report.AppendLine("GPU timing is reported only when the platform returns a positive FrameTiming sample. CPU_work includes simulation, machine effects, service and periodic survey/report, excludes IMGUI/render. Frame includes whole player loop. GC uses whole-frame recorder and separately measured work allocation.");
            report.AppendLine("service_walking is the matched baseline camera-sweep fixture; on_foot_movement additionally executes the real collision-aware walking and camera routines, added for final validation.");
            report.AppendLine("late_wet_sheet is an explicitly seeded benchmark, NOT a control-driven completed run: every ice cell receives 0.30 mm real film in 16 spatial stripes aged 0..15 minutes through AddWater/ResolveLiquid. Driving, machinery, HUD, rendering and active wet fading then execute normally after warmup.");
            report.AppendLine("Appended columns: active_wet_cells is the end-of-scenario active wet list count; surface_refresh_ms_per_frame is measured Surface.Refresh CPU time divided by measured workload frames, including zero-cost frames between refreshes. Seed/setup and warmup costs are excluded.");
            report.AppendLine("scenario,frames,mean_frame_ms,p95_frame_ms,fps,cpu_work_ms,gpu_ms,gc_bytes_per_frame,max_gc_bytes,work_bytes_per_frame,gc_collections,managed_growth_bytes,draw_calls,active_wet_cells,surface_refresh_ms_per_frame");
            active=true;Next();lastFrame=Time.realtimeSinceStartupAsDouble;
        }
        void Next()
        {
            scenario++; if(scenario>=scenarios.Length){Finish();return;}
            lateWetSheet=scenarios[scenario]=="late_wet_sheet";
            game.NewSession(true);game.enabled=false;
            var s=game.Sim;var c=s.Controls;
            bayStart=new Vector3(s.Vehicle.X,0,s.Vehicle.Z);bayHeading=s.Vehicle.Heading;
            c.HydraulicHoldControls=false;
            if(scenario<5||scenario==9||lateWetSheet)
            {
                Pose(s,-5,-15,0);c.Throttle=.42f;
                if(scenario!=0){s.Conditioner.Lift=0;c.ConditionerTarget=0;c.BladeDepthMm=.35f;c.HorizontalAuger=c.VerticalAuger=true;c.FloodFlow=.32f;c.WashValve=.25f;}
                if(scenario==3){typeof(Simulation).GetProperty("SnowTankM3").SetValue(s,s.Config.SnowCapacityM3*.99999f);c.BladeDepthMm=.8f;}
            }
            if(scenario>=5&&scenario<=7||scenario==10)
            {
                s.ConfigureWaterForServiceTest(scenario==7?1:.35f,.3f);s.TryDismount();
                typeof(IceWaterService).GetMethod("BeginWalk",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(game.WaterService,null);
                if(scenario>5&&scenario<8){s.TakeHose();s.ConnectHose(WaterFillPort.Flood,6);s.SetSupplyValve(1);}
            }
            if(scenario==8)
            {
                s.Layout.ToWorld(s.Layout.PitSide,s.Layout.PitOutward-3.85f,out float pitX,out float pitZ);
                Pose(s,pitX,pitZ,s.Layout.OutwardHeading);
                typeof(Simulation).GetProperty("SnowTankM3").SetValue(s,1.8f);c.TankLiftTarget=1;
            }
            if(lateWetSheet)SeedLateWetSheet(s);
            game.Dashboard.Compact=scenario==2;
            render(0);game.Surface.Invalidate();game.Surface.Refresh(s);
            frame=0;frameMs.Clear();cpuMs.Clear();gpuMs.Clear();allocated=maxAllocated=simulationAllocated=0;cpuTotal=surfaceRefreshTotal=0;
            memoryStart=GC.GetTotalMemory(false);gcStart=GC.CollectionCount(0);
        }
        static void SeedLateWetSheet(Simulation s)
        {
            const int stripeCount=16;
            const float oldestAge=15*60;
            // Explicit history fixture. Phase changes use the production water
            // API; no clamped wetness or shader-only whole-sheet override.
            typeof(Simulation).GetProperty("Time").SetValue(s,oldestAge);
            for(int i=0;i<s.Ice.Cells.Length;i++)
            {
                var cell=s.Ice.Cells[i];if(!cell.IsIce)continue;
                int row=i/s.Ice.Columns;
                int stripe=Math.Min(stripeCount-1,row*stripeCount/s.Ice.Rows);
                float age=oldestAge*stripe/(stripeCount-1);
                s.Ice.AddWater(ref cell,.30f,s.Time-age);
                s.Ice.ResolveLiquid(ref cell,s.Time);
                s.Ice.UpdateQuality(ref cell);s.Ice.Cells[i]=cell;s.Ice.MarkCellChanged(i);
            }
            s.Ice.MarkChanged();
        }
        static void Pose(Simulation s,float x,float z,float heading)
        {
            s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=heading;s.Vehicle.Speed=0;
            s.Conditioner.Heading=heading;s.Conditioner.X=x-Mathf.Sin(heading)*(s.Config.HitchBehindAxle+s.Config.DrawbarLength);s.Conditioner.Z=z-Mathf.Cos(heading)*(s.Config.HitchBehindAxle+s.Config.DrawbarLength);
        }
        void Update()
        {
            if(!active||finishing)return;
            double now=Time.realtimeSinceStartupAsDouble, elapsed=(now-lastFrame)*1000;lastFrame=now;
            if(frame>90)
            {
                frameMs.Add(elapsed);long bytes=gc.Valid?gc.LastValue:0;allocated+=bytes;maxAllocated=Math.Max(maxAllocated,bytes);
                uint n=FrameTimingManager.GetLatestTimings(1,timings);
                if(n>0&&timings[0].gpuFrameTime>0)gpuMs.Add(timings[0].gpuFrameTime);
            }
            FrameTimingManager.CaptureFrameTimings();
            var s=game.Sim;var c=s.Controls;
            if((scenario<5||scenario==9||lateWetSheet)&&s.Vehicle.Z>15)Pose(s,-5,-15,0);
            if(scenario==4)game.DriverCamera.transform.localRotation=Quaternion.Euler(25+15*Mathf.Sin(frame*.02f),120*Mathf.Sin(frame*.015f),0);
            if(scenario==5)game.DriverCamera.transform.rotation=Quaternion.Euler(12,frame*.45f,0);
            if(scenario==9&&frame%180==0)c.Steer=frame%360==0?.25f:-.25f;
            if(scenario==8&&frame>380)c.TankLiftTarget=frame%80<25?.85f:1;
            long before=GC.GetAllocatedBytesForCurrentThread();long started=System.Diagnostics.Stopwatch.GetTimestamp();
            if(scenario==10)
            {
                int leg=frame/75%4;
                Vector3 walk=leg==0?game.World.Machine.right:leg==1?game.World.Machine.forward:leg==2?-game.World.Machine.right:-game.World.Machine.forward;
                moveFoot(walk*.036f);applyFoot();
            }
            s.Step(.02f);render(.02f);game.WaterService.UpdateViewAndEffects(true,.02f);
            if(frame%8==0)
            {
                game.Surface.Refresh(s);
                if(frame>90)surfaceRefreshTotal+=game.Surface.LastRefreshMilliseconds;
                typeof(IceResurfacerGame).GetProperty("Report").SetValue(game,s.GetReport());
            }
            double work=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            if(frame>90){cpuMs.Add(work);cpuTotal+=work;simulationAllocated+=GC.GetAllocatedBytesForCurrentThread()-before;}
            frame++;
            if(scenario==9&&enduranceFrames>1500&&frame>90&&frame%5000==0)
            {
                double sumWindow=0;int count=Math.Min(1000,frameMs.Count);
                for(int i=frameMs.Count-count;i<frameMs.Count;i++)sumWindow+=frameMs[i];
                report.AppendLine("endurance_window,"+frame+",mean_last_1000_ms="+(sumWindow/count).ToString("F3")+",managed_bytes="+GC.GetTotalMemory(false)+",gc0="+GC.CollectionCount(0));
            }
            if(frame>=90+(scenario==9?enduranceFrames:300))
            {
                frameMs.Sort();double sum=0;foreach(double value in frameMs)sum+=value;
                double gpu=0;foreach(double value in gpuMs)gpu+=value;
                int n=frameMs.Count;double mean=sum/Math.Max(1,n);
                report.AppendLine(scenarios[scenario]+","+n+","+mean.ToString("F3")+","+frameMs[Math.Min(n-1,(int)(n*.95))].ToString("F3")+","+(1000/mean).ToString("F1")+","+(cpuTotal/Math.Max(1,cpuMs.Count)).ToString("F3")+","+(gpuMs.Count>0?(gpu/gpuMs.Count).ToString("F3"):"unavailable")+","+(allocated/Math.Max(1,n))+","+maxAllocated+","+(simulationAllocated/Math.Max(1,cpuMs.Count))+","+(GC.CollectionCount(0)-gcStart)+","+(GC.GetTotalMemory(false)-memoryStart)+","+(draws.Valid?draws.LastValue:-1)+","+game.Surface.ActiveWetCells+","+(surfaceRefreshTotal/Math.Max(1,cpuMs.Count)).ToString("F3"));
                Next();
            }
        }
        void OnGUI(){if(active&&!finishing)game.Dashboard.Draw(game);}
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;report.AppendLine("ERROR "+message);}}
        void Finish()
        {
            if(finishing)return;finishing=true;active=false;
            report.AppendLine("Managed runtime errors: "+errors);report.AppendLine("Long-session growth is a live-heap delta, not a leak diagnosis; inspect GC collections and final repeat before inferring leaks.");
            File.WriteAllText(Path.Combine(output,"Performance-"+Screen.width+"x"+Screen.height+".csv"),report.ToString());
            gc.Dispose();draws.Dispose();Application.logMessageReceived-=Log;Application.Quit(errors==0?0:1);
        }
    }
}
#endif
