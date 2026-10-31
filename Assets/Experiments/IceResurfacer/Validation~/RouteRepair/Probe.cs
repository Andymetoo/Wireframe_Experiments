using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text.Json;using IceResurfacer.Core;
class Probe
{
 static readonly JsonSerializerOptions Options=new(){IncludeFields=true,WriteIndented=true};
 static void Main(string[] args)
 {
  var folder="Assets/Experiments/IceResurfacer/Validation~/RouteRepair/";
  string path=Directory.GetFiles("Assets/Experiments/IceResurfacer/RouteRecordings~","Andy_Try_1*.route.json")[0],json=File.ReadAllText(path);
  var raw=JsonSerializer.Deserialize<RawEnvelope>(json,Options).route;
  var d=TrainingRouteDefinition.FromRecording(raw,json,"diagnostic source");
  if(args.Length>1&&args[1]=="--guide")d=JsonSerializer.Deserialize<GuideEnvelope>(File.ReadAllText(args[2]),Options).guide;
  else if(args.Length>1&&args[1]=="--raw")RecordedTrackReconstruction.Restore(d,raw);
  else if(args.Length>1&&args[1]=="--repeat")
  {
   for(int pass=0;pass<4;pass++){
    TrainingRouteEditing.ConstrainTangents(d,80,true);var v=RouteFitter.Fit(d,raw);
    float drift=d.controls.Max(p=>{var r=raw.samples[p.sourceSequence];return TrainingRouteDefinition.Distance(p.x,p.z,r.vehicleX,r.vehicleZ);});
    Console.WriteLine("Legacy correction pass "+(pass+1)+": curvature="+v.curvatureErrors+", boundary="+v.boundaryErrors+", max source-anchor displacement="+drift+" m");
   }
  }
  else if(args.Length<2||args[1]!="--unconstrained")TrainingRouteEditing.ConstrainTangents(d,80,true);
  var fit=RouteFitter.Fit(d,raw);
  if(args.Length>1&&args[1]=="--raw")
  {
   var tracker=new RouteProgress(fit);double maxAhead=0;int stalledAt=-1;
   foreach(var p in raw.samples){tracker.Observe(p.vehicleX,p.vehicleZ,p.vehicleHeadingRadians,p.speedMetersPerSecond);maxAhead=Math.Max(maxAhead,tracker.Distance-p.distanceMeters);if(stalledAt<0&&p.distanceMeters-tracker.Distance>3)stalledAt=p.sequence;}
   Console.WriteLine("Raw observation replay: progress="+tracker.Distance+" / "+fit.Length+"; first gap>3m source="+stalledAt+"; max ahead="+maxAhead+"; contexts="+fit.legs.Count+"; warnings="+fit.recordedRadiusWarnings);
  }
  var config=TrainingRouteDefinition.CopyConfiguration(raw.configuration);config.CellSize=.5f;var workspace=new Simulation(config,true);
  var tank=typeof(Simulation).GetProperty("TankLift");
  object Corners(float x,float z,float h)
  {
   var bad=new List<object>();float sn=(float)Math.Sin(h),cs=(float)Math.Cos(h);
   for(int side=-1;side<=1;side+=2)for(int fore=-1;fore<=1;fore+=2){float px=x+cs*side*config.ConditionerWidth*.5f+sn*fore*config.ConditionerLength*.5f,pz=z-sn*side*config.ConditionerWidth*.5f+cs*fore*config.ConditionerLength*.5f;
    if(!workspace.Ice.IsInside(px,pz,.005f)&&!workspace.Layout.ContainsFloor(px,pz,.005f)){workspace.Layout.ToLocal(px,pz,out float localSide,out float outward);bad.Add(new{px,pz,localSide,outward,productionNavigable=workspace.Navigable(px,pz)});}}
   return bad;
  }
  var faults=new List<object>();int rawBodyFails=0,rawConditionerFails=0;float rawMinRadius=float.PositiveInfinity;var rawFails=new List<object>();
  for(int i=0;i<raw.samples.Count;i++){var p=raw.samples[i];tank.SetValue(workspace,p.tankLift);bool clear=workspace.ProbeVehiclePose(p.vehicleX,p.vehicleZ,p.vehicleHeadingRadians,out var contact);
   if(!clear){rawBodyFails++;rawFails.Add(new{i,p.distanceMeters,p.vehicleX,p.vehicleZ,contact});}
   if(((List<object>)Corners(p.conditionerX,p.conditionerZ,p.conditionerHeadingRadians)).Count>0)rawConditionerFails++;
   if(i>0){var prior=raw.samples[i-1];double ds=p.distanceMeters-prior.distanceMeters;float dh=Math.Abs(TrainingRouteDefinition.Angle(p.vehicleHeadingRadians-prior.vehicleHeadingRadians));if(ds>.001&&dh>.00001)rawMinRadius=Math.Min(rawMinRadius,(float)ds/dh);}
  }
  for(int i=0;i<fit.samples.Count;i++){
   var p=fit.samples[i];if(!p.boundaryError&&!p.curvatureError)continue;int lo=d.controls[p.control].sourceSequence,hi=d.controls[p.control+1].sourceSequence;int at=lo;float nearest=float.PositiveInfinity;
   for(int k=lo;k<=hi;k++){float delta=TrainingRouteDefinition.Distance(p.x,p.z,raw.samples[k].vehicleX,raw.samples[k].vehicleZ);if(delta<nearest){nearest=delta;at=k;}}
   var a=raw.samples[at];tank.SetValue(workspace,a.tankLift);bool rawClear=workspace.ProbeVehiclePose(a.vehicleX,a.vehicleZ,a.vehicleHeadingRadians,out var rawContact);
   tank.SetValue(workspace,p.tankLift);bool fittedClear=workspace.ProbeVehiclePose(p.x,p.z,p.heading,out var fittedContact);
   faults.Add(new{i,p.distance,control=p.control,lo,hi,source=at,rawDistance=a.distanceMeters,p.curvatureError,p.boundaryError,radius=1/Math.Max(.0000001f,Math.Abs(p.curvature)),nearest,rawClear,fittedClear,
    rawVehicle=new{a.vehicleX,a.vehicleZ,a.vehicleHeadingRadians,a.speedMetersPerSecond,a.direction,a.conditionerLift,a.tankLift},fitVehicle=new{p.x,p.z,p.heading},
    rawConditioner=new{a.conditionerX,a.conditionerZ,a.conditionerHeadingRadians},fitConditioner=new{p.conditionerX,p.conditionerZ,p.conditionerHeading},rawContact,fittedContact,
    rawBadCorners=Corners(a.conditionerX,a.conditionerZ,a.conditionerHeadingRadians),fitBadCorners=Corners(p.conditionerX,p.conditionerZ,p.conditionerHeading)});
  }
  var report=new{path,controls=d.controls.Count,samples=fit.samples.Count,fit.Length,fit.minimumRadius,fit.maximumRawDeviation,fit.curvatureErrors,fit.boundaryErrors,rawBodyFails,rawConditionerFails,rawMinRadius,rawFails,faults};
  File.WriteAllText(folder+(args.Length>0?args[0]:"Before.json"),JsonSerializer.Serialize(report,Options));
  if(args.Length>0&&args[0].StartsWith("Before")){File.WriteAllText(folder+args[0].Replace(".json","-definition.json"),JsonSerializer.Serialize(d,Options));File.WriteAllLines(folder+args[0].Replace(".json","-path.csv"),new[]{"distance,x,z,heading,conditionerX,conditionerZ,conditionerHeading,control,curvatureError,boundaryError"}.Concat(fit.samples.Select(p=>FormattableString.Invariant($"{p.distance},{p.x},{p.z},{p.heading},{p.conditionerX},{p.conditionerZ},{p.conditionerHeading},{p.control},{p.curvatureError},{p.boundaryError}"))));}
  Console.WriteLine(JsonSerializer.Serialize(new{controls=d.controls.Count,samples=fit.samples.Count,fit.Length,fit.minimumRadius,fit.maximumRawDeviation,fit.curvatureErrors,fit.boundaryErrors,rawBodyFails,rawConditionerFails,rawMinRadius},Options));
 }
 class RawEnvelope{public RecordedRouteData route;}
 class GuideEnvelope{public TrainingRouteDefinition guide;}
}
