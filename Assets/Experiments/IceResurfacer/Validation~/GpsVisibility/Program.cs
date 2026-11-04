using System;
using System.Diagnostics;
using IceResurfacer.Core;
using IceResurfacer.Presentation;

static class Program
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static void Near(double a,double b,double tolerance,string message){Check(Math.Abs(a-b)<=tolerance,message);}
    static void Main()
    {
        Near(GpsGuidanceGeometry.LookAheadMeters,12.192*2,.000002,"lookahead doubles the historical distance");
        var c=new SimulatorConfig();var route=new ResurfacingRoute(c);var fit=RouteFitter.Reference(route,c);
        double corner=-1,straight=-1;
        foreach(var p in fit.samples)
        {
            if(p.distance>40&&p.distance<fit.Length-30)
            {if(Math.Abs(p.curvature)>.05&&corner<0)corner=p.distance;if(Math.Abs(p.curvature)<.0001&&straight<0)straight=p.distance;}
        }
        Check(corner>0&&straight>0,"reference supplies actual straight and corner test contexts");
        foreach(double distance in new[]{0,straight,corner,fit.Length-1,fit.Length-.6,fit.Length})
        {
            var w=new GpsGuidanceGeometry.Window(distance,fit.Length);
            Near(w.End,Math.Min(fit.Length,distance+24.384),.000002,"window ends at lookahead or route end at "+distance.ToString("F2")+" m");
            Check(w.Start<=w.End&&w.End<=fit.Length&&w.WorldPoints<=70&&w.MapPoints<=62,"world/map geometry stays bounded");
            for(int i=0;i<w.WorldPoints;i++)
            {
                var p=fit.At(w.Start+(w.End-w.Start)*i/(w.WorldPoints-1));
                CheckFinite(p.x);CheckFinite(p.z);
            }
            if(w.WorldPoints>1)Near(fit.At(w.End).distance,w.End,.000001,"final preview sample stays on sequential route");
        }
        Check(new GpsGuidanceGeometry.Window(fit.Length,fit.Length).WorldPoints==0,"route completion hides world guidance without wrapping");
        foreach(float heading in new[]{0f,(float)Math.PI/2,(float)Math.PI,-(float)Math.PI/2,.73f,12.3f})
        {
            GpsGuidanceGeometry.HeadingEnd(2,-3,heading,c.BodyFront,out float x,out float z);
            float dx=x-2,dz=z+3;
            Near(Math.Sqrt(dx*dx+dz*dz),c.BodyFront+6,.000002,"heading length extends six metres beyond nose");
            Near(dx*Math.Cos(heading)-dz*Math.Sin(heading),0,.000002,"heading remains collinear with live vehicle yaw");
        }
        GpsGuidanceGeometry.HeadingEnd(2,-3,(float)Math.PI/2,c.BodyFront,out float eastX,out float eastZ);
        Near(eastX,10.5,.000002,"offset east-facing vehicle heads east independently of desired path");Near(eastZ,-3,.000002,"rear-axle origin remains at actual vehicle Z");
        int oldQueries=(int)Math.Ceiling((12.192-.35)/.09)+1;
        int newQueries=GpsGuidanceGeometry.SilhouetteIntervals(24.384-.35)+1;
        Check(newQueries==145&&newQueries<oldQueries*1.1,"maximum silhouette samples/raycasts grow less than 10% ("+oldQueries+" -> "+newQueries+")");
        Check(GpsGuidanceGeometry.SilhouetteIntervals(100000)==144,"silhouette budget stays fixed even for oversized input");
        double priorOffset=0;int intervals=newQueries-1;
        for(int i=0;i<=intervals;i++)
        {
            double offset=GpsGuidanceGeometry.SilhouetteOffset(i,intervals,24.384-.35);
            if(offset<priorOffset||offset<0||offset>24.384-.35+.000001)throw new Exception("nonsequential silhouette sampling");
            if(offset<=8&&offset-priorOffset>.090001)throw new Exception("near cockpit silhouette became coarse");
            priorOffset=offset;
        }
        Near(priorOffset,24.384-.35,.000001,"adaptive silhouette reaches the full window while retaining near-cockpit density");
        // Exercise production sampling against the actual fitted route, keeping
        // sequential progress and all source samples read-only.
        var progress=new RouteProgress(fit);double before=progress.Distance;int leg=progress.Leg,samples=fit.samples.Count;
        double checksum=0;var watch=new Stopwatch();long allocated=GC.GetAllocatedBytesForCurrentThread();watch.Start();
        const int frames=10000;
        for(int frame=0;frame<frames;frame++)
        {
            double distance=(fit.Length-25)*frame/frames;var w=new GpsGuidanceGeometry.Window(distance,fit.Length);
            for(int i=0;i<w.WorldPoints;i++)checksum+=fit.At(w.Start+(w.End-w.Start)*i/(w.WorldPoints-1)).x;
            for(int i=0;i<w.MapPoints;i++)checksum+=fit.At(distance+(w.End-distance)*i/(w.MapPoints-1)).z;
        }
        watch.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
        Check(progress.Distance==before&&progress.Leg==leg&&fit.samples.Count==samples,"preview leaves route/progress untouched");
        Check(!double.IsNaN(checksum),"sampling benchmark produces finite geometry");
        Check(bytes==0,"preview sampling allocates no managed memory per frame");
        Console.WriteLine("PREVIEW benchmark: "+frames+" frames, "+watch.Elapsed.TotalMilliseconds.ToString("F2")+" ms total, "+(watch.Elapsed.TotalMilliseconds/frames).ToString("F4")+" ms/frame; managed allocations="+bytes+" bytes");
        Console.WriteLine("ALL GPS GEOMETRY CHECKS PASSED; native renderer/camera/visibility checks use IceGpsVisibilityValidation.Run.");
    }
    static void CheckFinite(float value){if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("nonfinite preview point");}
}
