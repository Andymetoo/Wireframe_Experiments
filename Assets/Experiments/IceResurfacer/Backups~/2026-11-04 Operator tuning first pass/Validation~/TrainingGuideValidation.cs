using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using IceResurfacer.Core;

internal static class TrainingGuideValidation
{
    sealed class Envelope {public RecordedRouteData route {get;set;}}
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);Console.WriteLine("  training guide: "+message);}
    public static void Run()
    {
        RouteUXCoreValidation.Run();
        string source="Assets/Experiments/IceResurfacer/Validation~/RouteRecording/Deterministic-fixture.route.json";
        string bytes=File.ReadAllText(source);
        var raw=JsonSerializer.Deserialize<Envelope>(bytes,new JsonSerializerOptions{IncludeFields=true}).route;
        var d=TrainingRouteDefinition.FromRecording(raw,bytes,"core test source");
        var before=RouteFitter.Fit(d,raw);Check(before.curvatureErrors>0,"unconstrained reconstruction exposes real over-tight sections");
        var positions=new (float x,float z)[d.controls.Count];for(int i=0;i<positions.Length;i++)positions[i]=(d.controls[i].x,d.controls[i].z);
        TrainingRouteEditing.ConstrainTangents(d,80,true);var fit=RouteFitter.Fit(d,raw);
        float limit=d.configuration.Wheelbase/(float)Math.Tan(d.configuration.MaxSteeringRadians);
        Check(fit.Ready&&fit.minimumRadius>=limit,"constrained fixture passes turning radius and complete body/conditioner boundary checks");
        for(int i=0;i<positions.Length;i++)if(TrainingRouteDefinition.Distance(positions[i].x,positions[i].z,d.controls[i].x,d.controls[i].z)>.12001f)throw new Exception("Source corridor exceeded");
        Check(File.ReadAllText(source)==bytes&&d.rawRecordingJson==bytes,"fit preserves exact raw file and embedded source");
        Check(fit.maximumRawDeviation<.20f,"fixture stays within 20 cm of its recorded trajectory");
        var a=d.controls[20].Copy();var b=d.controls[21].Copy();int count=d.controls.Count;
        int split=TrainingRouteEditing.Split(d,20);float maxSplit=0;
        for(int i=0;i<=100;i++)
        {double t=i/100.0;var old=RouteFitter.Evaluate(a,b,t);var p=t<=.5?RouteFitter.Evaluate(d.controls[20],d.controls[21],t*2):RouteFitter.Evaluate(d.controls[21],d.controls[22],t*2-1);maxSplit=Math.Max(maxSplit,TrainingRouteDefinition.Distance(old.x,old.z,p.x,p.z));}
        Check(maxSplit<.00001f&&d.controls.Count==count+1,"inserting a control preserves the exact cubic to under 0.01 mm");
        Check(TrainingRouteEditing.Remove(d,split)&&!TrainingRouteEditing.Remove(d,0),"intermediate controls can be removed; endpoints are protected");
        d.controls[20]=a;d.controls[21]=b;d.controls[20].marker=RouteMarker.Pass;d.controls[20].label="Explicit test pass";
        var marked=RouteFitter.Fit(d,raw);bool labeled=false;foreach(var leg in marked.legs)labeled|=leg.label=="Explicit test pass";
        Check(labeled&&marked.Ready,"pass markers create an explicit sequential rejoin context");
        d.controls[20]=a;var save=d.controls[20].Copy();d.controls[20].x=50;
        Check(!RouteFitter.Fit(d,raw).Ready,"out-of-rink edits remain drafts and fail readiness");d.controls[20]=save;
        var tracker=new RouteProgress(fit);double previous=0;int maxSearch=0;
        foreach(var p in fit.samples)
        {
            tracker.Observe(p.x,p.z,p.heading,p.direction*.9f);
            if(tracker.Distance<previous||tracker.Distance>p.distance+.3)throw new Exception("Sequential progression skipped or reversed");previous=tracker.Distance;maxSearch=Math.Max(maxSearch,tracker.LastSearchSamples);
        }
        Check(fit.Length-tracker.Distance<.1,"full reverse entry, straightaways, rounded perimeter and inward loops complete in order");
        var overlap=LoopFixture();tracker=new RouteProgress(overlap);
        foreach(var p in overlap.samples){tracker.Observe(p.x,p.z,p.heading,.9f);if(tracker.Distance>p.distance+.02)throw new Exception("Skipped to a later coincident loop");}
        Check(overlap.Length-tracker.Distance<.01,"three exactly coincident loops cannot skip to a later pass");
        tracker.RejoinLeg(0);var start=overlap.samples[0];tracker.Observe(start.x,start.z,start.heading,.9f);previous=tracker.Distance;
        for(int i=0;i<300;i++)tracker.Observe(start.x,start.z,start.heading,0);
        Check(tracker.Distance==previous,"stationary periods do not advance route progress");
        var next=overlap.samples[1];tracker.Observe(next.x,next.z,next.heading,-.9f);
        Check(tracker.Distance==previous&&tracker.OffRoute,"wrong travel direction holds the current context");
        var recovered=overlap.samples[2];tracker.Observe(recovered.x,recovered.z,recovered.heading,.9f);
        Check(!tracker.OffRoute&&tracker.Status.StartsWith("Following",StringComparison.Ordinal),"normal travel clears a prior direction-warning status without manual restart");
        tracker.RejoinLeg(0);tracker.Observe(start.x,start.z,start.heading,.9f);tracker.Observe(next.x,next.z,next.heading+(float)Math.PI,.9f);
        Check(tracker.Distance<.01&&tracker.OffRoute,"opposite heading cannot acquire a nearby pass");
        tracker.RejoinLeg(0);for(int i=0;i<35;i++){var p=overlap.samples[i];tracker.Observe(p.x,p.z,p.heading,.9f);}
        previous=tracker.Distance;var at=overlap.samples[34];
        for(int i=1;i<=5;i++)tracker.Observe(at.x+i*.4f,at.z,at.heading,.9f);
        for(int i=4;i>=0;i--)tracker.Observe(at.x+i*.4f,at.z,at.heading,.9f);
        for(int i=35;i<=50;i++){var p=overlap.samples[i];tracker.Observe(p.x,p.z,p.heading,.9f);}
        Check(tracker.Distance>=previous&&Math.Abs(tracker.Distance-overlap.samples[50].distance)<.1,"driving deviation and return rejoins the same sequential context");
        previous=tracker.Distance;tracker.Observe(100,100,0,.9f);Check(tracker.Distance==previous&&tracker.OffRoute,"discontinuous position changes hold progress");
        tracker.RejoinLeg(7);var target=overlap.At(overlap.legs[7].start+2);tracker.Observe(target.x,target.z,target.heading,.9f);
        Check(tracker.Leg==7,"explicit leg selection allows deliberate distant rejoin without a restart");
        var legacy=new ResurfacingRoute(new SimulatorConfig());var reference=RouteFitter.Reference(legacy,new SimulatorConfig());
        Check(reference.samples[0].direction==-1,"original F5 reference starts with its existing reverse departure");
        for(int i=0;i<legacy.Points.Count;i++)if(legacy.Points[i].X!=reference.samples[i].x||legacy.Points[i].Z!=reference.samples[i].z)throw new Exception("Original reference changed");
        Check(true,"original generated F5 positions and sequence remain exact");
        var footprint=new RouteFootprints(fit);footprint.Compare();Check(footprint.MissedBladeCells==0,"future passes are excluded from live missed-area diagnostics");
        footprint.AdvanceExpected(fit.Length);footprint.Compare();Check(footprint.MissedBladeCells>0&&footprint.MissedWaterCells>0,"unprocessed expected blade and flood areas are reported separately");
        footprint.RebaseExpected(0);Check(footprint.MissedBladeCells==0&&footprint.MissedWaterCells==0,"explicit earlier rejoin resets expected prefix while preserving actual observations");footprint.AdvanceExpected(fit.Length);
        foreach(var p in fit.samples)footprint.Observe(p.conditionerX,p.conditionerZ,p.conditionerHeading,p.cutting,p.flooding,p.distance);
        footprint.Compare();Check(footprint.MissedBladeCells==0&&footprint.MissedWaterCells==0,"following modeled conditioner/contact widths covers the intended geometry exactly");
        for(int visit=0;visit<6;visit++)footprint.Observe(0,0,0,true,true,fit.Length+visit*4);
        footprint.Compare();Check(footprint.ExcessBladeCells>0&&footprint.ExcessWaterCells>0,"repeated revisits report excess overlap for both independent footprints");
        var widths=new RouteFootprints(new RouteFit{configuration=new SimulatorConfig()});widths.Observe(0,0,0,true,true,0);
        int Cell(float x,float z)=>(int)Math.Floor((z-widths.minZ)/RouteFootprints.Cell)*widths.columns+(int)Math.Floor((x-widths.minX)/RouteFootprints.Cell);
        Check(widths.actualBlade[Cell(.1f,.3f)]>0&&widths.actualWater[Cell(.1f,.3f)]==0&&
            widths.actualBlade[Cell(1.1f,-.5f)]==0&&widths.actualWater[Cell(1.1f,-.5f)]>0,
            "independent transverse samples verify blade width, wider flood fringe and separate rearward water outlet");
        var working=fit.samples.Find(p=>p.cutting);Check(TrainingRouteDefinition.Distance(working.x,working.z,working.conditionerX,working.conditionerZ)>1,
            "conditioner processing line is physically behind the rear-axle driving line");
        var sim=new Simulation(practice:true);var paired=new Simulation(practice:true);tracker=new RouteProgress(fit);var observed=new RouteFootprints(fit);
        foreach(var s in new[]{sim,paired})
        {s.Vehicle.X=-5;s.Vehicle.Z=-15;s.Vehicle.Heading=0;s.Conditioner.X=-5;s.Conditioner.Z=-16.1f;s.Conditioner.Heading=0;s.Conditioner.Lift=0;
            s.Controls.ConditionerTarget=0;s.Controls.BladeDepthMm=.35f;s.Controls.FloodFlow=.32f;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;}
        for(int i=0;i<900;i++)
        {
            foreach(var s in new[]{sim,paired}){s.Controls.Throttle=.35f;s.Controls.Steer=(float)Math.Sin(i*.02)*.3f;s.Step(.02f);}
            tracker.Observe(sim.Vehicle.X,sim.Vehicle.Z,sim.Vehicle.Heading,sim.Vehicle.Speed);observed.ObserveSimulation(sim);observed.AdvanceExpected(tracker.Distance);
            if(sim.Vehicle.X!=paired.Vehicle.X||sim.Vehicle.Z!=paired.Vehicle.Z||sim.Vehicle.Heading!=paired.Vehicle.Heading||sim.Conditioner.X!=paired.Conditioner.X||sim.Conditioner.Heading!=paired.Conditioner.Heading||sim.FloodWaterLiters!=paired.FloodWaterLiters||sim.TotalShavedIceM3!=paired.TotalShavedIceM3)throw new Exception("Guide affected simulation");
        }
        Check(MemoryMarshal.AsBytes(sim.Ice.Cells.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(paired.Ice.Cells.AsSpan()))&&
            JsonSerializer.Serialize(sim.GetReport(),new JsonSerializerOptions{IncludeFields=true})==JsonSerializer.Serialize(paired.GetReport(),new JsonSerializerOptions{IncludeFields=true}),
            "paired 900-step work passes retain exact vehicle/conditioner physics, water, every ice cell and scoring report");
        tracker=new RouteProgress(overlap);for(int i=0;i<100;i++){var p=overlap.samples[i];tracker.Observe(p.x,p.z,p.heading,.9f);}
        long allocation=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
        for(int i=0;i<10000;i++){var p=overlap.samples[99];tracker.Observe(p.x,p.z,p.heading,.9f);}watch.Stop();
        long used=GC.GetAllocatedBytesForCurrentThread()-allocation;
        Console.WriteLine("  fixture: "+fit.samples.Count+" samples / "+fit.Length.ToString("F3")+" m / min radius "+fit.minimumRadius.ToString("F4")+" m / raw deviation "+fit.maximumRawDeviation.ToString("F4")+" m; max local search "+maxSearch+" samples");
        Console.WriteLine("  stationary progress cost: "+watch.Elapsed.TotalMilliseconds.ToString("F3")+" ms / 10000 calls; allocation including Stopwatch="+used+" bytes");
    }
    static RouteFit LoopFixture()
    {
        var fit=new RouteFit{configuration=new SimulatorConfig(),name="Coincident-loop progress test",provenance="Synthetic test only"};
        const int samples=300;double distance=0;
        for(int i=0;i<=samples*3;i++)
        {
            double t=i*2*Math.PI/samples;var p=new GuideSample{x=(float)(6*Math.Cos(t)),z=(float)(6*Math.Sin(t)),heading=TrainingRouteDefinition.Angle((float)-t),direction=1,curvature=-1f/6,leg=Math.Min(11,i/75)};
            if(i>0){var b=fit.samples[i-1];distance+=TrainingRouteDefinition.Distance(p.x,p.z,b.x,b.z);}p.distance=distance;fit.samples.Add(p);
        }
        for(int i=0;i<12;i++)fit.legs.Add(new GuideLeg{first=i*75,last=(i+1)*75-1,start=fit.samples[i*75].distance,end=fit.samples[(i+1)*75].distance,direction=1,turn=true,label="Synthetic quarter "+i});
        return fit;
    }
}
