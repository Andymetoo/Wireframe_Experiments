using System;

namespace IceResurfacer.Core
{
    /// <summary>Bounded interpolation of immutable observations, never a vehicle controller.</summary>
    public static class RecordedTrackReconstruction
    {
        public static void Restore(TrainingRouteDefinition definition,RecordedRouteData raw)
        {
            raw.Validate();
            if(definition.sourceRecordingId!=raw.id)throw new FormatException("Guide source does not match this recording.");
            definition.version=2;
            for(int i=0;i<definition.controls.Count;i++)
            {
                var c=definition.controls[i];
                if(c.sourceSequence<0||c.sourceSequence>=raw.samples.Count)throw new FormatException("Invalid source anchor.");
                var p=raw.samples[c.sourceSequence];c.x=p.vehicleX;c.z=p.vehicleZ;c.headingRadians=p.vehicleHeadingRadians;
                c.direction=Direction(raw,c.sourceSequence);c.recordedTrack=i<definition.controls.Count-1;
                c.tankLift=p.tankLift;c.cutting=p.conditionerLift<.25f&&p.bladeDepthMm>0;c.flooding=p.floodValve>.001f;
            }
            TrainingRouteDefinition.ResetTangents(definition);
        }
        public static void DetachPoint(TrainingRouteDefinition definition,int index)
        {
            if(index<0||index>=definition.controls.Count)throw new ArgumentOutOfRangeException(nameof(index));
            if(index>0)definition.controls[index-1].recordedTrack=false;
            definition.controls[index].recordedTrack=false;
        }
        public static int Direction(RecordedRouteData raw,int sequence)
        {
            if(raw.samples[sequence].direction!=0)return raw.samples[sequence].direction;
            for(int i=sequence+1;i<raw.samples.Count;i++)if(raw.samples[i].direction!=0)return raw.samples[i].direction;
            for(int i=sequence-1;i>=0;i--)if(raw.samples[i].direction!=0)return raw.samples[i].direction;
            return 1;
        }
        internal static void Append(RouteFit fit,RecordedRouteData raw,RouteControlPoint a,RouteControlPoint b,int control,ref double distance)
        {
            var ra=raw.samples[a.sourceSequence];var rb=raw.samples[b.sourceSequence];
            if(!Anchored(a,ra)||!Anchored(b,rb))throw new FormatException("Edited recorded-track anchor: convert the adjacent sections to Bezier, or restore the recorded track.");
            double start=distance;bool first=fit.samples.Count==0;
            for(int index=a.sourceSequence;index<b.sourceSequence;index++)
            {
                var p=raw.samples[index];var next=raw.samples[index+1];
                double ds=next.distanceMeters-p.distanceMeters;
                float chord=TrainingRouteDefinition.Distance(p.vehicleX,p.vehicleZ,next.vehicleX,next.vehicleZ);
                int divisions=Math.Max(1,Math.Max((int)Math.Ceiling(chord/.025f),(int)Math.Ceiling(Math.Abs(TrainingRouteDefinition.Angle(next.vehicleHeadingRadians-p.vehicleHeadingRadians))/.004f)));
                if(divisions>2000||chord>Math.Max(1,raw.distanceThresholdMeters*4))
                {fit.sourceErrors++;fit.issues.Add("Discontinuous raw trajectory at sequence "+index+"; no clearance certification.");if(divisions>2000)divisions=2000;}
                for(int step=first?0:1;step<=divisions;step++)
                {
                    float t=step/(float)divisions;var r=Interpolate(raw,index,t);
                    var guide=new GuideSample{x=r.vehicleX,z=r.vehicleZ,heading=r.vehicleHeadingRadians,recordedTrack=true,control=control,
                        direction=Direction(raw,step==divisions?index+1:index),sourceSequence=index,sourceFraction=t,sourceDistance=r.distanceMeters,
                        conditionerX=r.conditionerX,conditionerZ=r.conditionerZ,conditionerHeading=r.conditionerHeadingRadians,conditionerLift=r.conditionerLift,
                        cutting=r.conditionerLift<.25f&&r.bladeDepthMm>0,flooding=r.floodValve>.001f,tankLift=r.tankLift,safeSpeed=Math.Max(.15f,Math.Abs(r.speedMetersPerSecond)),
                        curvature=ds>.0001?(float)(TrainingRouteDefinition.Angle(next.vehicleHeadingRadians-p.vehicleHeadingRadians)/ds):0,
                        distance=start+r.distanceMeters-ra.distanceMeters};
                    fit.samples.Add(guide);first=false;
                    if(fit.samples.Count>100000)throw new FormatException("Guide exceeds 100,000 samples.");
                }
            }
            distance=start+rb.distanceMeters-ra.distanceMeters;
        }
        static bool Anchored(RouteControlPoint c,RecordedRouteSample p)=>TrainingRouteDefinition.Distance(c.x,c.z,p.vehicleX,p.vehicleZ)<.0001f&&Math.Abs(TrainingRouteDefinition.Angle(c.headingRadians-p.vehicleHeadingRadians))<.0001f;
        public static RecordedRouteSample Interpolate(RecordedRouteData raw,int sequence,float t)
        {
            var a=raw.samples[Math.Max(0,Math.Min(raw.samples.Count-1,sequence))];var b=raw.samples[Math.Min(raw.samples.Count-1,sequence+1)];
            t=Math.Max(0,Math.Min(1,t));var r=t<1?a:b;
            r.vehicleX=a.vehicleX+(b.vehicleX-a.vehicleX)*t;r.vehicleZ=a.vehicleZ+(b.vehicleZ-a.vehicleZ)*t;
            r.vehicleHeadingRadians=TrainingRouteDefinition.Angle(a.vehicleHeadingRadians+TrainingRouteDefinition.Angle(b.vehicleHeadingRadians-a.vehicleHeadingRadians)*t);
            r.conditionerX=a.conditionerX+(b.conditionerX-a.conditionerX)*t;r.conditionerZ=a.conditionerZ+(b.conditionerZ-a.conditionerZ)*t;
            r.conditionerHeadingRadians=TrainingRouteDefinition.Angle(a.conditionerHeadingRadians+TrainingRouteDefinition.Angle(b.conditionerHeadingRadians-a.conditionerHeadingRadians)*t);
            r.conditionerLift=a.conditionerLift+(b.conditionerLift-a.conditionerLift)*t;r.tankLift=a.tankLift+(b.tankLift-a.tankLift)*t;
            r.speedMetersPerSecond=a.speedMetersPerSecond+(b.speedMetersPerSecond-a.speedMetersPerSecond)*t;
            r.distanceMeters=a.distanceMeters+(b.distanceMeters-a.distanceMeters)*t;r.seconds=a.seconds+(b.seconds-a.seconds)*t;return r;
        }
        public static void Match(RecordedRouteData raw,float x,float z,int lo,int hi,out int sequence,out float fraction,out float distance)
        {
            sequence=lo;fraction=0;distance=float.PositiveInfinity;
            for(int i=lo;i<Math.Max(lo+1,hi);i++)
            {
                var a=raw.samples[i];var b=raw.samples[Math.Min(i+1,raw.samples.Count-1)];
                float dx=b.vehicleX-a.vehicleX,dz=b.vehicleZ-a.vehicleZ,n=dx*dx+dz*dz;
                float t=n<.0000001f?0:Simulation.Clamp(((x-a.vehicleX)*dx+(z-a.vehicleZ)*dz)/n,0,1);
                float delta=TrainingRouteDefinition.Distance(x,z,a.vehicleX+dx*t,a.vehicleZ+dz*t);
                if(delta<distance){distance=delta;sequence=i;fraction=t;}
            }
        }
    }
}
