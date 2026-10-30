using System;
using IceResurfacer.Core;
using IceResurfacer.Editor;

internal static class RouteRecordingCoreValidation
{
    static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("  recorded route: "+text);}
    public static void Run()
    {
        var sim=new Simulation(practice:true);var recorder=new RouteRecorder();
        Check(recorder.Start(sim,"Core validation"),"start recording");
        var original=recorder.Data;
        Check(!recorder.Start(sim,"Do not reset")&&recorder.Data==original,"repeated start preserves current take");
        for(int i=0;i<1000;i++){sim.Step(.02f);recorder.Observe(sim);recorder.Observe(sim);}
        Check(recorder.Data.samples.Count==1&&Math.Abs(recorder.Data.durationSeconds-20)<.00001,"stationary periods and duplicate observer calls do not create points");
        Check(recorder.Stop()&&!recorder.Stop(),"stop is idempotent");recorder.Data.Validate();
        var projected=new ResurfacingRoute(recorder.Data);
        Check(projected.Points.Count==1,"raw recording projects into the existing ResurfacingRoute representation");
        recorder.Start(sim,"Session boundary");var next=new Simulation(practice:true);recorder.Observe(next);
        Check(!recorder.Recording&&recorder.Data.complete,"new simulation cannot silently concatenate a different session");
        var data=RecordedRouteFixture.Create();
        bool reverse=false,forward=false,turn=false,working=false;
        double maxGap=0;
        for(int i=0;i<data.samples.Count;i++)
        {
            var p=data.samples[i];reverse|=p.direction<0;forward|=p.direction>0;working|=p.conditionerLift<.05f&&p.horizontalAuger;
            if(i>0)
            {
                var before=data.samples[i-1];double angle=Math.Abs(Math.Atan2(Math.Sin(p.vehicleHeadingRadians-before.vehicleHeadingRadians),Math.Cos(p.vehicleHeadingRadians-before.vehicleHeadingRadians)));
                turn|=angle>.01;maxGap=Math.Max(maxGap,p.distanceMeters-before.distanceMeters);
            }
        }
        Check(reverse&&forward,"actual reverse departure and forward travel retained");
        Check(turn&&working,"rounded turns and actual conditioner/engagement states retained");
        Check(maxGap<.30,"20 cm / 2 degree policy retains turn precision; maximum sampled distance "+maxGap.ToString("F4")+" m");
        // Consecutive parallel passes remain in sequence even where their positions are close.
        bool nearby=false;
        for(int i=0;i<data.samples.Count&&!nearby;i+=5)
        for(int j=i+100;j<data.samples.Count;j+=5)
        {
            var a=data.samples[i];var b=data.samples[j];double dx=a.vehicleX-b.vehicleX,dz=a.vehicleZ-b.vehicleZ;
            double angle=Math.Abs(Math.Sin(a.vehicleHeadingRadians-b.vehicleHeadingRadians));
            if(dx*dx+dz*dz>.6*.6&&dx*dx+dz*dz<2.1*2.1&&b.distanceMeters-a.distanceMeters>30&&angle<.06){nearby=true;break;}
        }
        Check(nearby,"nearby parallel passes retain distinct later sequence positions");
        projected=new ResurfacingRoute(data);
        for(int i=0;i<data.samples.Count;i++)CheckPoint(projected,data,i);
        Check(data.provenance.Contains("NOT the operator"),"fixture is explicitly distinguished from real operator driving");
        Console.WriteLine("  deterministic fixture: "+data.samples.Count+" points / "+data.distanceMeters.ToString("F3")+" m / "+data.durationSeconds.ToString("F2")+" s; paired physics and every ice cell identical");
    }
    static void CheckPoint(ResurfacingRoute route,RecordedRouteData data,int i)
    {
        var p=data.samples[i];var v=route.Points[i];
        if(p.sequence!=i||v.X!=p.vehicleX||v.Z!=p.vehicleZ||v.Reverse!=(p.direction<0))throw new Exception("Projection reordered or altered raw route "+i);
    }
}
