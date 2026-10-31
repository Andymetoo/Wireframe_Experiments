using System;using System.IO;using System.Text.Json;using IceResurfacer.Core;
internal static class RouteUXCoreValidation
{
    sealed class Envelope{public RecordedRouteData route{get;set;}}
    public static void Run()
    {
        var fit=RouteFitter.Reference(new ResurfacingRoute(new SimulatorConfig()),new SimulatorConfig());var tracker=new RouteProgress(fit);
        foreach(var p in fit.samples)tracker.Observe(p.x,p.z,p.heading,p.direction*.9f);
        if(fit.Length-tracker.Distance>.15||tracker.Leg!=fit.legs.Count-1)throw new Exception("Original reference stuck at a leg/direction boundary");
        Console.WriteLine("  route UX: original reference automatically traverses all "+fit.legs.Count+" legs; first boundary="+fit.legs[0].end.ToString("F3")+" m");
        string folder="Assets/Experiments/IceResurfacer/Validation~/RouteUX/OperatorInputs";
        if(!Directory.Exists(folder))return;
        foreach(string path in Directory.GetFiles(folder,"*.route.json"))
        {
            byte[] original=File.ReadAllBytes(path);string json=File.ReadAllText(path);var raw=JsonSerializer.Deserialize<Envelope>(json,new JsonSerializerOptions{IncludeFields=true}).route;
            var d=TrainingRouteDefinition.FromRecording(raw,json,"read-only core verification");TrainingRouteEditing.ConstrainTangents(d,80,true);fit=RouteFitter.Fit(d,raw);
            if(!fit.Ready)throw new Exception("Actual operator take cannot be automatically fitted: "+Path.GetFileName(path));
            tracker=new RouteProgress(fit);int boundaries=0,previous=0;
            foreach(var p in raw.samples)
            {
                tracker.Observe(p.vehicleX,p.vehicleZ,p.vehicleHeadingRadians,p.speedMetersPerSecond);
                if(tracker.Leg>previous){boundaries+=tracker.Leg-previous;previous=tracker.Leg;}
            }
            if(fit.Length-tracker.Distance>1.5||boundaries<2)throw new Exception("Actual driven trajectory did not progress automatically: "+Path.GetFileName(path)+" "+tracker.Distance+" / "+fit.Length);
            if(!original.AsSpan().SequenceEqual(File.ReadAllBytes(path)))throw new Exception("Operator file changed");
            Console.WriteLine("  route UX: actual operator take "+raw.distanceMeters.ToString("F1")+" m: auto fit ready; "+boundaries+" boundaries crossed from original recorded poses; raw bytes unchanged");
        }
    }
}
