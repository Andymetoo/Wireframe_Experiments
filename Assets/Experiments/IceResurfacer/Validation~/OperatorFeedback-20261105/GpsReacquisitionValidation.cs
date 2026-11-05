using System;
using IceResurfacer.Core;

internal static class GpsReacquisitionValidation
{
    static void Check(bool pass,string message)
    {if(!pass)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static RouteFit Straight(int direction=1,bool markers=false)
    {
        var fit=new RouteFit{configuration=new SimulatorConfig()};
        for(int i=0;i<=400;i++)fit.samples.Add(new GuideSample{x=0,z=i*.1f*direction,distance=i*.1,heading=0,direction=direction,leg=markers?Math.Min(3,i/100):0});
        for(int leg=0;leg<(markers?4:1);leg++)fit.legs.Add(new GuideLeg{first=markers?leg*100:0,last=markers?(leg+1)*100-1:399,start=markers?leg*10:0,end=markers?(leg+1)*10:40,direction=direction});
        return fit;
    }
    static void Main()
    {
        TrainingGuideValidation.Run();
        var fit=Straight();var progress=new RouteProgress(fit);
        // A continuous approach crosses the route well ahead of expected progress.
        progress.Synchronize(1,10);
        progress.Observe(.8f,10,0,.9f);Check(progress.Distance==0,"parallel lane outside the pickup tolerance does not acquire");
        progress.Observe(.5f,10,0,.9f);Check(Math.Abs(progress.Distance-10)<.001&&!progress.OffRoute,"crossing the route ten metres ahead acquires without returning to its start");
        progress.Observe(.3f,10.1f,0,.9f);Check(progress.Distance>10.09,"normal tracking continues after acquisition");

        progress=new RouteProgress(fit);progress.Synchronize(.8f,15.24f);progress.Observe(.4f,15.24f,0,.9f);
        Check(Math.Abs(progress.Distance-15.24)<.0001,"fifty feet along the route is inside the pickup window");
        progress=new RouteProgress(fit);progress.Synchronize(.8f,15.26f);progress.Observe(.4f,15.26f,0,.9f);
        Check(progress.Distance==0&&progress.OffRoute,"a crossing beyond fifty feet cannot acquire via a clamped endpoint");
        progress=new RouteProgress(fit);progress.Synchronize(3,17);
        for(int lap=0;lap<10;lap++)
        {
            for(int step=1;step<=5;step++)progress.Observe(3+step*.1f,17,0,.9f);
            for(int step=4;step>=0;step--)progress.Observe(3+step*.1f,17,0,.9f);
        }
        for(int step=29;step>=0;step--)progress.Observe(step*.1f,17,0,.9f);
        Check(progress.Distance==0&&progress.OffRoute,"long off-route detour cannot walk the cursor through an artificial fifty-foot endpoint");

        progress=new RouteProgress(fit);progress.Synchronize(0,0);progress.Observe(0,10,0,.9f);
        progress.Observe(0,10.1f,0,.9f);Check(progress.Distance==0&&progress.OffRoute,"teleport and subsequent motion cannot silently reacquire");
        progress.RejoinLeg(0);progress.Observe(0,30,0,.9f);Check(Math.Abs(progress.Distance-30)<.001,"explicit leg selection still permits a deliberate distant join");

        progress=new RouteProgress(Straight(markers:true));progress.Synchronize(.8f,12);progress.Observe(.4f,12,0,.9f);
        Check(progress.Leg==1&&Math.Abs(progress.Distance-12)<.001,"nearby pickup crosses a same-direction pass marker");
        var reversing=Straight(markers:true);var first=reversing.legs[0];first.direction=-1;reversing.legs[0]=first;
        for(int i=0;i<100;i++){var p=reversing.samples[i];p.direction=-1;reversing.samples[i]=p;}
        progress=new RouteProgress(reversing);progress.Synchronize(.8f,12);progress.Observe(.4f,12,0,.9f);
        Check(progress.Distance==0,"nearby forward segment cannot bypass the current reverse leg");

        progress=new RouteProgress(Straight(-1));progress.Synchronize(.8f,-10);progress.Observe(.4f,-10,0,-.9f);
        Check(Math.Abs(progress.Distance-10)<.001,"correct reverse travel can acquire its nearby reverse segment");
        progress=new RouteProgress(fit);progress.Synchronize(.8f,10);progress.Observe(.4f,10,0,-.9f);
        Check(progress.Distance==0,"wrong travel direction cannot pick up a forward route");
        progress=new RouteProgress(fit);progress.Synchronize(.8f,10);progress.Observe(.4f,10,(float)Math.PI,.9f);
        Check(progress.Distance==0,"opposite heading cannot pick up a route");
        progress=new RouteProgress(fit);progress.Synchronize(.8f,10);progress.Observe(.4f,10,(float)Math.PI/2,.9f);
        Check(progress.Distance==0,"crossing at a right angle waits until the machine turns along the route");
        progress=new RouteProgress(fit);progress.Synchronize(.8f,10);progress.Observe(.4f,10,0,0);
        Check(progress.Distance==0,"stationary machine does not acquire a passing route");

        // A later loop occupies precisely the same ground, beyond the distance window.
        var loops=Straight();for(int i=0;i<=400;i++){var p=loops.samples[i];p.distance+=80;p.leg=1;loops.samples.Add(p);}
        loops.legs.Add(new GuideLeg{first=401,last=800,start=80,end=120,direction=1});
        progress=new RouteProgress(loops);progress.Synchronize(.8f,10);progress.Observe(.4f,10,0,.9f);
        Check(Math.Abs(progress.Distance-10)<.001&&progress.Leg==0,"coincident later loop is rejected in favour of nearby sequential track");
        Console.WriteLine("ALL GPS REACQUISITION CHECKS PASSED");
    }
}
