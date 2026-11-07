using System;

namespace IceResurfacer.Core
{
    /// <summary>Sequential progress with a bounded local reacquisition window. Never writes simulation state.</summary>
    public sealed class RouteProgress
    {
        readonly RouteFit route;
        float lastX,lastZ;
        bool synchronized,explicitJoin,discontinuous;
        double detourTravel;
        int statusLeg=-1,statusOffset=-1;
        public double Distance {get;private set;}
        public int Leg {get;private set;}
        public float LateralError {get;private set;}
        public bool OffRoute {get;private set;}
        public string Status {get;private set;}="Join the start of the selected leg";
        public double TotalObservedTravel {get;private set;}
        public int LastSearchSamples {get;private set;}
        // Fifty feet measured ALONG the route, not a radius through adjacent passes.
        public const double ReacquireMeters=15.24;
        public const float ReacquireLateralMeters=.60f;
        public RouteProgress(RouteFit fit){route=fit;RejoinLeg(0);explicitJoin=false;Status="Join the nearby start of the route";}
        public void Synchronize(float x,float z){lastX=x;lastZ=z;synchronized=true;}
        public void RejoinLeg(int index)
        {
            Leg=Math.Max(0,Math.Min(route.legs.Count-1,index));Distance=route.legs[Leg].start;
            detourTravel=0;explicitJoin=true;discontinuous=false;synchronized=false;OffRoute=true;Status="Explicit join / leg "+(Leg+1);
            statusLeg=statusOffset=-1;
        }
        public void Observe(float x,float z,float heading,float speed)
        {
            bool firstObservation=!synchronized;
            if(firstObservation){Synchronize(x,z);}
            float step=TrainingRouteDefinition.Distance(x,z,lastX,lastZ);lastX=x;lastZ=z;
            LastSearchSamples=0;
            if(step>1||discontinuous){discontinuous=true;OffRoute=true;statusLeg=-1;Status="Position changed discontinuously; select a leg to rejoin";return;}
            TotalObservedTravel+=step;
            if(Math.Abs(speed)<.02f||step<.000001f&&!explicitJoin&&!firstObservation)return;
            int direction=speed<0?-1:1;
            var context=route.legs[Leg];
            // A driver can stop/reverse slightly before the mathematical cusp.
            // Accept only the adjacent context, near its shared endpoint and heading.
            if(direction!=context.direction&&Leg+1<route.legs.Count&&route.legs[Leg+1].direction==direction&&context.end-Distance<=1.25)
            {
                var endpoint=route.At(context.end);var next=route.samples[route.legs[Leg+1].first];
                if(TrainingRouteDefinition.Distance(x,z,endpoint.x,endpoint.z)<=1.5f&&Math.Abs(TrainingRouteDefinition.Angle(heading-next.heading))<1.15f)
                {Leg++;Distance=route.legs[Leg].start;context=route.legs[Leg];explicitJoin=false;detourTravel=Math.Max(step,.08f);statusLeg=-1;}
            }
            if(direction!=context.direction){OffRoute=true;statusLeg=-1;Status=context.direction<0?"Selected leg expects reverse travel":"Selected leg expects forward travel";return;}
            double horizon;
            if(explicitJoin)horizon=context.end;
            else
            {
                detourTravel+=step;
                horizon=Math.Min(Distance+Math.Min(ReacquireMeters,Math.Max(2,detourTravel*1.25+1)),context.end);
                // Ordinary turn transitions can enter the immediately adjacent leg.
                // No scan ever searches a later loop or skips a significant context.
                if(context.end-Distance<1.5&&Leg+1<route.legs.Count&&route.legs[Leg+1].direction==direction)
                    horizon=Math.Min(Distance+Math.Min(ReacquireMeters,Math.Max(2,detourTravel*1.25+1)),Math.Min(route.legs[Leg+1].end,context.end+3));
            }
            bool found=Project(x,z,heading,direction,context.start,horizon,.96f,out double bestDistance,out float lateral,!explicitJoin);
            bool acquired=false;
            // Prefer the current sequential projection at crossings. Only search
            // farther when it no longer describes the machine's actual track.
            // Pass markers can be crossed; a required reversal cannot be skipped.
            if(!explicitJoin&&(!found||lateral>.75f))
            {
                double joinEnd=Math.Min(Distance+ReacquireMeters,context.end);
                for(int next=Leg+1;joinEnd<Distance+ReacquireMeters&&next<route.legs.Count&&route.legs[next].direction==direction;next++)
                    joinEnd=Math.Min(Distance+ReacquireMeters,route.legs[next].end);
                if(Project(x,z,heading,direction,context.start,joinEnd,.65f,out double joinDistance,out float joinLateral,true)&&joinLateral<=ReacquireLateralMeters)
                {found=true;acquired=true;bestDistance=joinDistance;lateral=joinLateral;}
            }
            if(!found||lateral>2.25f)
            {OffRoute=true;statusLeg=-1;Status="Off route / meet the next 50 ft of track, or select a leg";return;}
            LateralError=lateral;
            // A close, direction-aligned crossing may recover up to fifty feet of
            // omitted progress. An explicit leg selection remains the distant join.
            double budget=explicitJoin?horizon-Distance:acquired?ReacquireMeters:Math.Max(.08,detourTravel*1.5+.08);
            Distance=Math.Max(Distance,Math.Min(bestDistance,Distance+budget));
            int sample=route.IndexAt(Distance);Leg=route.samples[sample].leg;
            OffRoute=LateralError>.75f;explicitJoin=false;detourTravel=0;
            int offset=OffRoute?(int)(LateralError*10):-1;
            if(statusLeg!=Leg||statusOffset!=offset)
            {Status=OffRoute?"Offset "+LateralError.ToString("F2")+" m / same sequential leg":"Following leg "+(Leg+1);statusLeg=Leg;statusOffset=offset;}
            // A reversal must be reached at its actual endpoint before the next
            // direction is accepted; passing a later reverse loop cannot select it.
            if(Leg+1<route.legs.Count&&route.legs[Leg].end-Distance<.25)
            {
                var end=route.At(route.legs[Leg].end);
                if(TrainingRouteDefinition.Distance(x,z,end.x,end.z)<.8f&&route.legs[Leg+1].direction!=direction)
                {Leg++;Distance=route.legs[Leg].start;explicitJoin=false;detourTravel=0;statusLeg=-1;Status="Direction change / leg "+(Leg+1);}
            }
        }
        bool Project(float x,float z,float heading,int direction,double contextStart,double horizon,float headingTolerance,out double distance,out float lateral,bool strictSegment=false)
        {
            int first=route.IndexAt(Math.Max(contextStart,Distance-1)),last=route.IndexAt(horizon);
            float best=float.PositiveInfinity;distance=Distance;lateral=float.PositiveInfinity;
            for(int i=first;i<=last&&i<route.samples.Count-1;i++)
            {
                LastSearchSamples++;var a=route.samples[i];var b=route.samples[i+1];if(a.direction!=direction)continue;
                float angle=TrainingRouteDefinition.Angle(heading-a.heading);if(Math.Abs(angle)>headingTolerance)continue;
                float dx=b.x-a.x,dz=b.z-a.z,len=dx*dx+dz*dz;
                float rawT=len<.000001f?0:((x-a.x)*dx+(z-a.z)*dz)/len;
                // Do not use a clamped endpoint to acquire a point just beyond
                // the fifty-foot window or round the end of the current leg.
                if(strictSegment&&(rawT<0||rawT>1))continue;
                float t=Simulation.Clamp(rawT,0,1);
                double s=a.distance+(b.distance-a.distance)*t;if(s<Distance-.5||s>horizon+.00001)continue;
                float ex=x-(a.x+dx*t),ez=z-(a.z+dz*t),error=ex*ex+ez*ez;
                float score=error+angle*angle*.5f+(float)Math.Max(0,s-Distance)*.0005f;
                if(score<best){best=score;distance=s;lateral=(float)Math.Sqrt(error);}
            }
            return !float.IsPositiveInfinity(best);
        }
        public int UpcomingTurn(double lookAhead=28)
        {
            int start=route.IndexAt(Distance+1),end=route.IndexAt(Math.Min(route.Length,Distance+lookAhead));
            for(int i=start;i<=end;i++)if(Math.Abs(route.samples[i].curvature)>.04f)return i;return -1;
        }
    }
}
