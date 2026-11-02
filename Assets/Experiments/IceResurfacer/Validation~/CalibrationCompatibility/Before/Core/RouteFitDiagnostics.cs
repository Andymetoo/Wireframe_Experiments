using System;

namespace IceResurfacer.Core
{
    [Flags] public enum RouteProblemKind { None=0,Curvature=1,VehicleBoundary=2,ConditionerBoundary=4,RecordedManeuver=8 }
    public sealed class RouteProblemRegion
    {
        public int first,last,controlFirst,controlLast,sourceFirst,sourceLast,worst;
        public double start,end;
        public RouteProblemKind kind;
        public float minimumRadius=float.PositiveInfinity,maximumDeviation;
        public bool rawVehicleClear,rawConditionerClear;
        public RecordedRouteSample raw;
        public GuideSample fitted;
        public MovementContact rawContact;
        public string reason,suggestion;
        public bool Blocking=>kind!=RouteProblemKind.RecordedManeuver;
    }
    public static class RouteFitDiagnostics
    {
        public static void Build(RouteFit fit,RecordedRouteData raw)
        {
            fit.problems.Clear();RouteProblemRegion region=null;
            var c=TrainingRouteDefinition.CopyConfiguration(fit.configuration);c.CellSize=.5f;var workspace=new Simulation(c,true);
            for(int i=0;i<fit.samples.Count;i++)
            {
                var p=fit.samples[i];var kind=(p.curvatureError?RouteProblemKind.Curvature:0)|(p.boundaryError?(p.conditionerBoundary?RouteProblemKind.ConditionerBoundary:RouteProblemKind.VehicleBoundary):0)|(p.recordedRadiusWarning?RouteProblemKind.RecordedManeuver:0);
                if(kind==RouteProblemKind.None)continue;
                if(region==null||region.kind!=kind||p.distance-region.end>.35)
                {
                    region=new RouteProblemRegion{first=i,last=i,worst=i,controlFirst=p.control,controlLast=p.control,sourceFirst=p.sourceSequence,sourceLast=p.sourceSequence,
                        start=p.distance,end=p.distance,kind=kind};fit.problems.Add(region);
                }
                region.last=i;region.end=p.distance;region.controlLast=p.control;region.sourceLast=p.sourceSequence;
                float radius=Math.Abs(p.curvature)<.00001f?float.PositiveInfinity:1/Math.Abs(p.curvature);
                var observation=RecordedTrackReconstruction.Interpolate(raw,p.sourceSequence,p.sourceFraction);
                float deviation=TrainingRouteDefinition.Distance(p.x,p.z,observation.vehicleX,observation.vehicleZ);
                bool worse=i==region.first||(p.boundaryError?deviation>region.maximumDeviation:radius<region.minimumRadius);
                region.minimumRadius=Math.Min(region.minimumRadius,radius);region.maximumDeviation=Math.Max(region.maximumDeviation,deviation);
                if(!worse)continue;
                region.worst=i;region.fitted=p;region.raw=observation;
                var rawPose=new GuideSample{x=observation.vehicleX,z=observation.vehicleZ,heading=observation.vehicleHeadingRadians,tankLift=observation.tankLift,
                    conditionerX=observation.conditionerX,conditionerZ=observation.conditionerZ,conditionerHeading=observation.conditionerHeadingRadians,conditionerLift=observation.conditionerLift};
                workspace.SetGuideObservation(rawPose);region.rawVehicleClear=workspace.ProbeVehiclePose(rawPose.x,rawPose.z,rawPose.heading,out var hit);region.rawContact=hit;
                region.rawConditionerClear=RouteFitter.ConditionerClear(workspace,rawPose,out _);
                region.reason=p.boundaryError?(p.conditionerBoundary?"Conditioner envelope / ":p.boundaryContact.GuideRoller?"Guide roller / ":"Vehicle body envelope / ")+p.boundaryContact.Reason:
                    p.recordedRadiusWarning?"Observed maneuver exceeds the ideal bicycle radius; smooth-spline certification withheld.":"Derived Bezier curvature exceeds the unchanged steering limit.";
                region.suggestion=region.rawVehicleClear&&region.rawConditionerClear?"Restore the recorded track; this observation has production vehicle clearance. Inspect neighboring edited curves together.":"Raw clearance also fails here. Inspect the exact contact point; do not certify this section automatically.";
            }
        }
    }
}
