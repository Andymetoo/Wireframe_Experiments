using System;
using System.Collections.Generic;
using System.Reflection;

namespace IceResurfacer.Core
{
    [Flags] public enum RouteMarker { None=0,Turn=1,Pass=2,Stop=4,Entry=8,Exit=16 }
    [Serializable]
    public sealed class RouteControlPoint
    {
        public float x,z,headingRadians,incoming,outgoing;
        public int direction=1,sourceSequence;
        public bool cutting,flooding;
        // Optional derived-guide field. Raw recording schema is unchanged.
        public bool recordedTrack;
        public float nominalSpeed=.9f,tankLift;
        public RouteMarker marker;
        public string label="";
        public RouteControlPoint Copy() => (RouteControlPoint)MemberwiseClone();
    }
    [Serializable]
    public sealed class TrainingRouteDefinition
    {
        public int version=1;
        public string id,name,createdUtc,sourceRecordingId,sourceSha256,rawRecordingJson;
        public SimulatorConfig configuration;
        public List<RouteControlPoint> controls=new List<RouteControlPoint>();
        public void Validate()
        {
            if((version!=1&&version!=2)||!Guid.TryParseExact(id,"N",out _)||string.IsNullOrWhiteSpace(name)||name.Length>100||
                string.IsNullOrEmpty(sourceRecordingId)||string.IsNullOrEmpty(sourceSha256)||string.IsNullOrEmpty(rawRecordingJson)||configuration==null)
                throw new FormatException("Invalid training-guide identity or raw-source reference.");
            configuration.Validate();
            if(controls==null||controls.Count<2||controls.Count>5000)throw new FormatException("A guide needs 2–5000 ordered control points.");
            foreach(var p in controls)
            {
                if(version==1&&p!=null&&p.recordedTrack)throw new FormatException("Recorded-track spans require derived guide version 2; raw recordings remain version 1.");
                if(p==null||!RecordedRouteData.Finite(p.x)||!RecordedRouteData.Finite(p.z)||Math.Abs(p.x)>10000||Math.Abs(p.z)>10000||
                    !RecordedRouteData.Finite(p.headingRadians)||Math.Abs(p.headingRadians)>1000||!RecordedRouteData.Finite(p.incoming)||!RecordedRouteData.Finite(p.outgoing)||
                    p.incoming<.01f||p.outgoing<.01f||p.incoming>50||p.outgoing>50||Math.Abs(p.direction)!=1||p.sourceSequence<0||
                    !RecordedRouteData.Finite(p.nominalSpeed)||p.nominalSpeed<.05f||p.nominalSpeed>5||!RecordedRouteData.Finite(p.tankLift)||p.tankLift<0||p.tankLift>1)
                    throw new FormatException("Invalid control point. Tangents, poses and direction must be finite and bounded.");
            }
        }
        public static SimulatorConfig CopyConfiguration(SimulatorConfig source)
        {
            var copy=new SimulatorConfig();foreach(var f in typeof(SimulatorConfig).GetFields(BindingFlags.Instance|BindingFlags.Public))f.SetValue(copy,f.GetValue(source));return copy;
        }
        public static TrainingRouteDefinition FromRecording(RecordedRouteData raw,string rawJson,string sha,float spacing=2.5f)
        {
            raw.Validate();
            var d=new TrainingRouteDefinition{id=Guid.NewGuid().ToString("N"),name=raw.name+" / fitted guide",createdUtc=DateTimeOffset.UtcNow.ToString("O"),
                sourceRecordingId=raw.id,sourceSha256=sha,rawRecordingJson=rawJson,configuration=CopyConfiguration(raw.configuration)};
            int previousDirection=1;
            for(int i=0;i<raw.samples.Count;i++)if(raw.samples[i].direction!=0){previousDirection=raw.samples[i].direction;break;}
            double lastDistance=-100;float lastHeading=raw.samples[0].vehicleHeadingRadians;
            for(int i=0;i<raw.samples.Count;i++)
            {
                var p=raw.samples[i];int direction=p.direction==0?previousDirection:p.direction;
                if(p.direction==0)
                    for(int j=i+1;j<raw.samples.Count;j++)if(raw.samples[j].direction!=0){direction=raw.samples[j].direction;break;}
                bool reversal=direction!=previousDirection;
                bool equipment=d.controls.Count>0&&(d.controls[d.controls.Count-1].cutting!=(p.conditionerLift<.25f&&p.bladeDepthMm>0)||d.controls[d.controls.Count-1].flooding!=(p.floodValve>.001f));
                if(i==0||i==raw.samples.Count-1||reversal||equipment||p.distanceMeters-lastDistance>=spacing||Math.Abs(Angle(p.vehicleHeadingRadians-lastHeading))>.44f)
                {
                    if(d.controls.Count>0&&Distance(p.vehicleX,p.vehicleZ,d.controls[d.controls.Count-1].x,d.controls[d.controls.Count-1].z)<.35f)
                    {
                        var prior=d.controls[d.controls.Count-1];
                        prior.cutting=p.conditionerLift<.25f&&p.bladeDepthMm>0;prior.flooding=p.floodValve>.001f;
                        if(reversal||i==raw.samples.Count-1)
                        {prior.x=p.vehicleX;prior.z=p.vehicleZ;prior.headingRadians=p.vehicleHeadingRadians;prior.sourceSequence=i;prior.direction=direction;}
                        if(reversal)prior.marker|=RouteMarker.Stop;previousDirection=direction;continue;
                    }
                    var point=new RouteControlPoint{x=p.vehicleX,z=p.vehicleZ,headingRadians=p.vehicleHeadingRadians,sourceSequence=i,direction=direction,
                        cutting=p.conditionerLift<.25f&&p.bladeDepthMm>0,flooding=p.floodValve>.001f,tankLift=p.tankLift,
                        nominalSpeed=Math.Max(.15f,Math.Min(1.2f,Math.Abs(p.speedMetersPerSecond))),marker=reversal?RouteMarker.Stop:RouteMarker.None};
                    d.controls.Add(point);lastDistance=p.distanceMeters;lastHeading=p.vehicleHeadingRadians;
                }
                previousDirection=direction;
            }
            if(d.controls.Count<2)throw new InvalidOperationException("Recording contains too little travel to fit a guide.");
            d.controls[0].marker|=RouteMarker.Entry;d.controls[d.controls.Count-1].marker|=RouteMarker.Exit;
            ResetTangents(d);return d;
        }
        public static void ResetTangents(TrainingRouteDefinition d)
        {
            for(int i=0;i<d.controls.Count-1;i++)
            {
                var a=d.controls[i];var b=d.controls[i+1];float chord=Distance(a.x,a.z,b.x,b.z);
                double quarter=Angle(b.headingRadians-a.headingRadians)*.25;
                float handle=(float)(chord/(3*Math.Pow(Math.Cos(quarter),2)));
                a.outgoing=Math.Max(.02f,Math.Min(chord*.52f,handle));b.incoming=a.outgoing;
            }
            d.controls[0].incoming=d.controls[0].outgoing;d.controls[d.controls.Count-1].outgoing=d.controls[d.controls.Count-1].incoming;
        }
        public static float Distance(float x,float z,float bx,float bz) {double dx=x-bx,dz=z-bz;return(float)Math.Sqrt(dx*dx+dz*dz);}
        public static float Angle(float value) {while(value>Math.PI)value-=(float)(2*Math.PI);while(value< -Math.PI)value+=(float)(2*Math.PI);return value;}
    }
    public struct GuideSample
    {
        public float x,z,heading,curvature,safeSpeed,conditionerX,conditionerZ,conditionerHeading;
        public double distance;
        public int direction,control,leg;
        public bool cutting,flooding;
        public float tankLift;
        public bool curvatureError,boundaryError;
        public bool recordedTrack,recordedRadiusWarning;
        public int sourceSequence;
        public float sourceFraction,conditionerLift;
        public double sourceDistance;
        public MovementContact boundaryContact;
        public bool conditionerBoundary;
    }
    public struct GuideLeg
    {
        public int first,last,direction;
        public bool turn;
        public string label;
        public double start,end;
    }
    public sealed class RouteFit
    {
        public readonly List<GuideSample> samples=new List<GuideSample>();
        public readonly List<GuideLeg> legs=new List<GuideLeg>();
        public readonly List<string> issues=new List<string>();
        public SimulatorConfig configuration;
        public string name,provenance;
        public int curvatureErrors,boundaryErrors;
        public int recordedRadiusWarnings,recordedSamples,sourceErrors;
        public bool validationPerformed;
        public readonly List<RouteProblemRegion> problems=new List<RouteProblemRegion>();
        public float minimumRadius=float.PositiveInfinity,maximumRawDeviation;
        public bool Ready => curvatureErrors==0&&boundaryErrors==0&&sourceErrors==0&&samples.Count>1;
        public bool IdealSplineCertified=>Ready&&validationPerformed&&recordedSamples==0;
        public double Length => samples.Count==0?0:samples[samples.Count-1].distance;
        public GuideSample At(double distance)
        {
            if(samples.Count==0)return default;
            int i=IndexAt(distance);if(i>=samples.Count-1)return samples[samples.Count-1];
            var a=samples[i];var b=samples[i+1];float t=(float)Math.Max(0,Math.Min(1,(distance-a.distance)/Math.Max(.000001,b.distance-a.distance)));
            var p=a;p.x+=(b.x-a.x)*t;p.z+=(b.z-a.z)*t;p.heading+=TrainingRouteDefinition.Angle(b.heading-a.heading)*t;
            p.conditionerX+=(b.conditionerX-a.conditionerX)*t;p.conditionerZ+=(b.conditionerZ-a.conditionerZ)*t;
            p.conditionerHeading+=TrainingRouteDefinition.Angle(b.conditionerHeading-a.conditionerHeading)*t;p.distance=distance;return p;
        }
        public int IndexAt(double distance)
        {
            int lo=0,hi=samples.Count-1;while(lo<hi){int mid=(lo+hi+1)/2;if(samples[mid].distance<=distance)lo=mid;else hi=mid-1;}return lo;
        }
        public bool Compatible(SimulatorConfig c)
        {
            string[] names={"RinkWidth","RinkLength","CornerRadius","GateHalfWidth","BayLength","Wheelbase","MaxSteeringRadians","SteeringRate","VehicleHalfWidth","BodyFront","BodyRear",
                "HitchBehindAxle","DrawbarLength","ConditionerYawLimitRadians","ConditionerWidth","ConditionerLength","WaterDistributionWidth","WaterFilmSpreadMeters","FloodContactFore","FloodContactLength"};
            foreach(string name in names){var f=typeof(SimulatorConfig).GetField(name);if(Math.Abs((float)f.GetValue(c)-(float)f.GetValue(configuration))>.0001f)return false;}return true;
        }
    }
    public static class RouteFitter
    {
        public static RouteFit Fit(TrainingRouteDefinition d,RecordedRouteData raw)
        {
            d.Validate();raw.Validate();if(raw.id!=d.sourceRecordingId)throw new FormatException("Guide and raw-source IDs differ.");
            int previousSequence=-1;
            foreach(var control in d.controls)
            {if(control.sourceSequence<previousSequence||control.sourceSequence>=raw.samples.Count)throw new FormatException("Control source anchors must retain raw sequence order.");previousSequence=control.sourceSequence;}
            var fit=new RouteFit{configuration=d.configuration,name=d.name,provenance=raw.provenance};
            double distance=0;
            for(int segment=0;segment<d.controls.Count-1;segment++)
            {
                var a=d.controls[segment];var b=d.controls[segment+1];float chord=TrainingRouteDefinition.Distance(a.x,a.z,b.x,b.z);
                if(a.recordedTrack)
                {
                    RecordedTrackReconstruction.Append(fit,raw,a,b,segment,ref distance);
                    continue;
                }
                int divisions=Math.Max(4,(int)Math.Ceiling((chord+a.outgoing+b.incoming)*2/.10));
                if(divisions>20000||chord<.03f){fit.issues.Add("Degenerate or excessively long segment "+segment);fit.curvatureErrors++;continue;}
                for(int step=segment==0?0:1;step<=divisions;step++)
                {
                    var p=Evaluate(a,b,step/(double)divisions);p.control=segment;
                    if(fit.samples.Count>0){var before=fit.samples[fit.samples.Count-1];distance+=TrainingRouteDefinition.Distance(p.x,p.z,before.x,before.z);}
                    p.distance=distance;fit.samples.Add(p);
                    if(fit.samples.Count>100000)throw new FormatException("Fitted guide exceeds 100,000 samples.");
                }
            }
            if(fit.samples.Count<2)throw new FormatException("No usable curve segments.");
            Complete(fit,raw.samples[0].conditionerHeadingRadians-raw.samples[0].vehicleHeadingRadians);
            ApplyMarkers(fit,d);
            // Compare each fitted sample only to its corresponding raw sequence interval.
            for(int sample=0;sample<fit.samples.Count;sample++)
            {
                var p=fit.samples[sample];
                if(p.recordedTrack)continue;
                int lo=d.controls[p.control].sourceSequence,hi=d.controls[p.control+1].sourceSequence;
                lo=Math.Max(0,Math.Min(raw.samples.Count-1,lo));hi=Math.Max(lo,Math.Min(raw.samples.Count-1,hi));float nearest=float.PositiveInfinity;
                RecordedTrackReconstruction.Match(raw,p.x,p.z,lo,hi,out int sequence,out float fraction,out nearest);
                p.sourceSequence=sequence;p.sourceFraction=fraction;p.sourceDistance=RecordedTrackReconstruction.Interpolate(raw,sequence,fraction).distanceMeters;fit.samples[sample]=p;
                fit.maximumRawDeviation=Math.Max(fit.maximumRawDeviation,nearest);
            }
            RouteFitDiagnostics.Build(fit,raw);
            return fit;
        }
        static void ApplyMarkers(RouteFit fit,TrainingRouteDefinition d)
        {
            var starts=new SortedSet<int>();var labels=new Dictionary<int,string>();
            foreach(var leg in fit.legs)starts.Add(leg.first);
            for(int control=1;control<d.controls.Count-1;control++)if(d.controls[control].marker!=RouteMarker.None)
            {
                int at=0;while(at<fit.samples.Count-1&&fit.samples[at].control<control)at++;
                starts.Add(at);labels[at]=string.IsNullOrWhiteSpace(d.controls[control].label)?d.controls[control].marker.ToString():d.controls[control].label;
            }
            var indices=new List<int>(starts);fit.legs.Clear();
            for(int k=0;k<indices.Count;k++)
            {
                int first=indices[k],last=k+1<indices.Count?indices[k+1]-1:fit.samples.Count-1;
                bool turn=false;for(int i=first;i<=last;i++)if(Math.Abs(fit.samples[i].curvature)>.035f)turn=true;
                AddLeg(fit,first,last,turn);var leg=fit.legs[k];leg.start=fit.samples[first].distance;leg.end=k+1<indices.Count?fit.samples[indices[k+1]].distance:fit.Length;
                if(labels.TryGetValue(first,out string label))leg.label=label;fit.legs[k]=leg;
                for(int i=first;i<=last;i++){var p=fit.samples[i];p.leg=k;fit.samples[i]=p;}
            }
        }
        public static GuideSample Evaluate(RouteControlPoint a,RouteControlPoint b,double t)
        {
            double sign=a.direction;
            double ax=a.x+Math.Sin(a.headingRadians)*a.outgoing*sign,az=a.z+Math.Cos(a.headingRadians)*a.outgoing*sign;
            double bx=b.x-Math.Sin(b.headingRadians)*b.incoming*sign,bz=b.z-Math.Cos(b.headingRadians)*b.incoming*sign;
            double u=1-t;
            double x=u*u*u*a.x+3*u*u*t*ax+3*u*t*t*bx+t*t*t*b.x,z=u*u*u*a.z+3*u*u*t*az+3*u*t*t*bz+t*t*t*b.z;
            double dx=3*u*u*(ax-a.x)+6*u*t*(bx-ax)+3*t*t*(b.x-bx),dz=3*u*u*(az-a.z)+6*u*t*(bz-az)+3*t*t*(b.z-bz);
            double ddx=6*u*(bx-2*ax+a.x)+6*t*(b.x-2*bx+ax),ddz=6*u*(bz-2*az+a.z)+6*t*(b.z-2*bz+az);
            double norm=Math.Sqrt(dx*dx+dz*dz);
            return new GuideSample{x=(float)x,z=(float)z,heading=TrainingRouteDefinition.Angle((float)Math.Atan2(dx,dz)+(a.direction<0?(float)Math.PI:0)),
                curvature=norm<.00001?100:(float)((dz*ddx-dx*ddz)/(norm*norm*norm)),direction=a.direction,cutting=a.cutting,flooding=a.flooding,tankLift=a.tankLift,safeSpeed=a.nominalSpeed};
        }
        public static RouteFit Reference(ResurfacingRoute route,SimulatorConfig config)
        {
            var fit=new RouteFit{configuration=config,name="Generated reference",provenance="Synthetic generated reference; not an operator recording"};double distance=0;
            for(int i=0;i<route.Points.Count;i++)
            {
                var p=route.Points[i];var next=route.Points[Math.Min(i+1,route.Points.Count-1)];var before=route.Points[Math.Max(0,i-1)];
                int direction=(i<route.Points.Count-1?next.Reverse:p.Reverse)?-1:1;
                bool sameBefore=i>0&&(p.Reverse?-1:1)==direction;
                float dx=next.X-(sameBefore?before.X:p.X),dz=next.Z-(sameBefore?before.Z:p.Z);
                if(i==route.Points.Count-1){dx=p.X-before.X;dz=p.Z-before.Z;}
                if(i>0)distance+=TrainingRouteDefinition.Distance(p.X,p.Z,before.X,before.Z);
                fit.samples.Add(new GuideSample{x=p.X,z=p.Z,distance=distance,heading=TrainingRouteDefinition.Angle((float)Math.Atan2(dx,dz)+(direction<0?(float)Math.PI:0)),
                    direction=direction,cutting=p.Working,flooding=p.Working,safeSpeed=.9f});
            }
            for(int i=1;i<fit.samples.Count-1;i++)
            {var p=fit.samples[i];p.curvature=TrainingRouteDefinition.Angle(fit.samples[i+1].heading-fit.samples[i-1].heading)/(float)Math.Max(.01,fit.samples[i+1].distance-fit.samples[i-1].distance);fit.samples[i]=p;}
            Complete(fit,0,false);return fit;
        }
        static void Complete(RouteFit fit,float initialRelative,bool check=true)
        {
            fit.validationPerformed=check;
            var c=fit.configuration;var coarse=TrainingRouteDefinition.CopyConfiguration(c);coarse.CellSize=.5f;
            var workspace=new Simulation(coarse,true);var first=fit.samples[0];
            workspace.Conditioner.Heading=TrainingRouteDefinition.Angle(first.heading+initialRelative);
            workspace.Conditioner.X=first.x-(float)Math.Sin(first.heading)*c.HitchBehindAxle-(float)Math.Sin(workspace.Conditioner.Heading)*c.DrawbarLength;
            workspace.Conditioner.Z=first.z-(float)Math.Cos(first.heading)*c.HitchBehindAxle-(float)Math.Cos(workspace.Conditioner.Heading)*c.DrawbarLength;
            float limit=(float)Math.Tan(c.MaxSteeringRadians)/c.Wheelbase;int previousLegStart=0;bool turn=false;
            for(int i=0;i<fit.samples.Count;i++)
            {
                var p=fit.samples[i];var prior=i==0?p:fit.samples[i-1];float ds=(float)(p.distance-prior.distance);
                p.safeSpeed=Math.Min(p.safeSpeed,p.direction<0?c.ReverseSpeed:c.ForwardSpeed);
                int ticks=Math.Max(1,(int)Math.Ceiling(ds/(Math.Max(.15f,p.safeSpeed)*.02f)));
                for(int tick=1;!p.recordedTrack&&tick<=ticks;tick++)
                {
                    float t=tick/(float)ticks;
                    workspace.ProjectGuideConditioner(prior.x+(p.x-prior.x)*t,prior.z+(p.z-prior.z)*t,prior.heading+TrainingRouteDefinition.Angle(p.heading-prior.heading)*t,
                        p.direction*Math.Max(.15f,p.safeSpeed),ds/Math.Max(.15f,p.safeSpeed)/ticks,p.tankLift);
                }
                if(p.recordedTrack){workspace.SetGuideObservation(p);fit.recordedSamples++;}
                else {p.conditionerX=workspace.Conditioner.X;p.conditionerZ=workspace.Conditioner.Z;p.conditionerHeading=workspace.Conditioner.Heading;}
                float absolute=Math.Abs(p.curvature);if(absolute>.0001f)fit.minimumRadius=Math.Min(fit.minimumRadius,1/absolute);
                p.curvatureError=!p.recordedTrack&&absolute>limit*1.001f;
                p.recordedRadiusWarning=p.recordedTrack&&absolute>limit*1.001f;
                if(p.recordedRadiusWarning)fit.recordedRadiusWarnings++;
                if(check)
                {
                    bool body=workspace.ProbeVehiclePose(p.x,p.z,p.heading,out var contact);
                    bool conditioner=ConditionerClear(workspace,p,out var conditionerContact);
                    p.boundaryError=!body||!conditioner;p.conditionerBoundary=body&&!conditioner;p.boundaryContact=body?conditionerContact:contact;
                }
                if(check&&p.curvatureError)fit.curvatureErrors++;if(p.boundaryError)fit.boundaryErrors++;
                if(i>0&&ds>.0001f)
                {
                    float steering=(float)Math.Atan(p.curvature*c.Wheelbase*p.direction),before=(float)Math.Atan(prior.curvature*c.Wheelbase*prior.direction);
                    float slope=Math.Abs(steering-before)/ds;if(slope>.001f)p.safeSpeed=Math.Min(p.safeSpeed,c.SteeringRate/slope*.85f);
                }
                bool nextTurn=turn?absolute>.022f:absolute>.035f;
                bool contextChanged=i>0&&(p.direction!=prior.direction||p.cutting!=prior.cutting||p.flooding!=prior.flooding);
                bool turnChanged=nextTurn!=turn&&p.distance-fit.samples[previousLegStart].distance>1.5;
                if(contextChanged||turnChanged)
                {AddLeg(fit,previousLegStart,i-1,turn);previousLegStart=i;turn=nextTurn;}
                p.leg=fit.legs.Count;fit.samples[i]=p;
            }
            AddLeg(fit,previousLegStart,fit.samples.Count-1,turn);
            // Adjacent legs share continuous route-distance boundaries.
            for(int i=0;i<fit.legs.Count;i++)
            {var leg=fit.legs[i];leg.start=fit.samples[leg.first].distance;leg.end=i+1<fit.legs.Count?fit.samples[fit.legs[i+1].first].distance:fit.Length;fit.legs[i]=leg;}
            if(fit.curvatureErrors>0)fit.issues.Add(fit.curvatureErrors+" samples exceed minimum rear-axle radius "+(1/limit).ToString("F3")+" m.");
            if(fit.boundaryErrors>0)fit.issues.Add(fit.boundaryErrors+" samples intersect body/conditioner boundaries. Repair red sections before using this guide.");
            if(fit.recordedSamples>0)fit.issues.Add("Recorded-track reconstruction preserves observed vehicle heading and actual conditioner poses. Clearance validated; not certified as an ideal smooth spline.");
            if(fit.recordedRadiusWarnings>0)fit.issues.Add(fit.recordedRadiusWarnings+" observed heading changes exceed the ideal bicycle-radius constraint; inspect recorded maneuvers. This does not certify a derived spline.");
        }
        static void AddLeg(RouteFit fit,int first,int last,bool turn)
        {if(last<first)return;var p=fit.samples[first];fit.legs.Add(new GuideLeg{first=first,last=last,direction=p.direction,turn=turn,label=(p.direction<0?"Reverse / ":"")+(turn?"turn":p.cutting?"processing pass":"travel")});}
        internal static bool ConditionerClear(Simulation workspace,GuideSample p,out MovementContact contact)
        {
            contact=default;
            float sn=(float)Math.Sin(p.conditionerHeading),cs=(float)Math.Cos(p.conditionerHeading);var c=workspace.Config;
            for(int side=-1;side<=1;side+=2)for(int fore=-1;fore<=1;fore+=2)
            {
                float x=p.conditionerX+cs*side*c.ConditionerWidth*.5f+sn*fore*c.ConditionerLength*.5f;
                float z=p.conditionerZ-sn*side*c.ConditionerWidth*.5f+cs*fore*c.ConditionerLength*.5f;
                if(!workspace.Ice.IsInside(x,z,.005f)&&!workspace.Layout.ContainsFloor(x,z,.005f))
                {
                    workspace.Layout.ToLocal(x,z,out float localSide,out float outward);
                    contact=new MovementContact{Obstacle=outward>workspace.Layout.NeckStart&&Math.Abs(localSide)<workspace.Layout.RoomHalfWidth+3?MovementObstacle.ServiceWall:MovementObstacle.RinkBoards,
                        PointX=x,PointZ=z,VehicleX=p.x,VehicleZ=p.z,Heading=p.heading};return false;
                }
            }
            return true;
        }
    }
    public sealed partial class Simulation
    {
        // Only the private workspace created by RouteFitter calls this method.
        // It reuses the production conditioner authority without changing live physics.
        internal void ProjectGuideConditioner(float x,float z,float heading,float speed,float dt,float lift)
        {Vehicle.X=x;Vehicle.Z=z;Vehicle.Heading=heading;Vehicle.Speed=speed;TankLift=lift;UpdateConditioner(dt);}
        internal void SetGuideObservation(GuideSample p)
        {Vehicle.X=p.x;Vehicle.Z=p.z;Vehicle.Heading=p.heading;TankLift=p.tankLift;Conditioner.X=p.conditionerX;Conditioner.Z=p.conditionerZ;Conditioner.Heading=p.conditionerHeading;Conditioner.Lift=p.conditionerLift;}
    }
}
