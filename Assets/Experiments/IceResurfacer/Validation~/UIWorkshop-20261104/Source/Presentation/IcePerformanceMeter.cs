using System;
using Unity.Profiling;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>Bounded, quarter-second readout. No scene queries or per-frame strings.</summary>
    public sealed class IcePerformanceMeter : IDisposable
    {
        ProfilerRecorder gc, draws;
        readonly FrameTiming[] timing=new FrameTiming[1];
        long workStart;
        double frameMean, workMean;
        float refresh;
        public string Summary { get; private set; } = "Measuring frames...";
        public string Details { get; private set; } = "";
        public IcePerformanceMeter()
        {
            gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1);
        }
        public void BeginFrame(float dt)
        {
            workStart=System.Diagnostics.Stopwatch.GetTimestamp();
            // Scene construction and resuming after a debugger pause are not
            // representative frames; wait for normal cadence before seeding EMA.
            if(dt<=0||dt>.25f)return;
            double blend=1-Math.Exp(-dt*3);
            frameMean=frameMean==0?dt:frameMean+(dt-frameMean)*blend;
            refresh-=dt;
            if(refresh>0)return;
            refresh=.25f;
            Summary=(1/Math.Max(.0001,frameMean)).ToString("0")+" FPS  /  "+(frameMean*1000).ToString("0.0")+" ms   F4";
            uint count=FrameTimingManager.GetLatestTimings(1,timing);
            string gpu=count>0&&timing[0].gpuFrameTime>0?timing[0].gpuFrameTime.ToString("0.00")+" ms":"unavailable";
            Details="CPU simulation + view: "+workMean.ToString("0.00")+" ms  |  GPU: "+gpu+
                "\nGC last frame: "+(gc.Valid?gc.LastValue.ToString():"unavailable")+" B  |  Draw calls: "+(draws.Valid?draws.LastValue.ToString():"unavailable");
            FrameTimingManager.CaptureFrameTimings();
        }
        public void EndWork()
        {
            double value=(System.Diagnostics.Stopwatch.GetTimestamp()-workStart)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            workMean=workMean==0?value:workMean+(value-workMean)*.06;
        }
        public void Dispose(){gc.Dispose();draws.Dispose();}
    }
}
