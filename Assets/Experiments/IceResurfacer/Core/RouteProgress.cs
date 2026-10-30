using System;

namespace IceResurfacer.Core
{
    /// <summary>Sequential, travel-budgeted progress. Has no controls or writable simulation reference.</summary>
    public sealed class RouteProgress
    {
        readonly RouteFit route;
        float lastX,lastZ;
        bool synchronized,explicitJoin;
        double detourTravel;
        int statusLeg=-1,statusOffset=-1;
        public double Distance {get;private set;}
        public int Leg {get;private set;}
        public float LateralError {get;private set;}
        public bool OffRoute {get;private set;}
        public string Status {get;private set;}="Join the start of the selected leg";
        public double TotalObservedTravel {get;private set;}
        public int LastSearchSamples {get;private set;}
        public RouteProgress(RouteFit fit){route=fit;RejoinLeg(0);}
        public void Synchronize(float x,float z){lastX=x;lastZ=z;synchronized=true;}
        public void RejoinLeg(int index)
        {
            Leg=Math.Max(0,Math.Min(route.legs.Count-1,index));Distance=route.legs[Leg].start;
            detourTravel=0;explicitJoin=true;synchronized=false;OffRoute=true;Status="Explicit join / leg "+(Leg+1);
            statusLeg=statusOffset=-1;
        }
        public void Observe(float x,float z,float heading,float speed)
        {
            if(!synchronized){Synchronize(x,z);}
            float step=TrainingRouteDefinition.Distance(x,z,lastX,lastZ);lastX=x;lastZ=z;
            if(step>1){OffRoute=true;statusLeg=-1;Status="Position changed discontinuously; select a leg to rejoin";return;}
            TotalObservedTravel+=step;
            if(Math.Abs(speed)<.02f||step<.000001f&&!explicitJoin)return;
            int direction=speed<0?-1:1;
            var context=route.legs[Leg];
            if(direction!=context.direction){OffRoute=true;statusLeg=-1;Status=context.direction<0?"Selected leg expects reverse travel":"Selected leg expects forward travel";return;}
            double horizon;
            if(explicitJoin)horizon=context.end;
            else
            {
                detourTravel+=step;
                horizon=Math.Min(Distance+Math.Max(2,detourTravel*1.25+1),context.end);
                // Ordinary turn transitions can enter the immediately adjacent leg.
                // No scan ever searches a later loop or skips a significant context.
                if(context.end-Distance<1.5&&Leg+1<route.legs.Count&&route.legs[Leg+1].direction==direction)
                    horizon=Math.Min(Distance+Math.Max(2,detourTravel*1.25+1),Math.Min(route.legs[Leg+1].end,context.end+3));
            }
            int first=route.IndexAt(Math.Max(context.start,Distance-1)),last=route.IndexAt(horizon);
            float best=float.PositiveInfinity;double bestDistance=Distance;LastSearchSamples=0;
            for(int i=first;i<=last&&i<route.samples.Count-1;i++)
            {
                LastSearchSamples++;var a=route.samples[i];var b=route.samples[i+1];if(a.direction!=direction)continue;
                float angle=TrainingRouteDefinition.Angle(heading-a.heading);if(Math.Abs(angle)>.96f)continue;
                float dx=b.x-a.x,dz=b.z-a.z,len=dx*dx+dz*dz;
                float t=len<.000001f?0:Simulation.Clamp(((x-a.x)*dx+(z-a.z)*dz)/len,0,1);
                double s=a.distance+(b.distance-a.distance)*t;if(s<Distance-.5||s>horizon+.001)continue;
                float ex=x-(a.x+dx*t),ez=z-(a.z+dz*t),error=ex*ex+ez*ez;
                float score=error+angle*angle*.5f+(float)Math.Max(0,s-Distance)*.0005f;
                if(score<best){best=score;bestDistance=s;LateralError=(float)Math.Sqrt(error);}
            }
            if(float.IsPositiveInfinity(best)||LateralError>2.25f)
            {OffRoute=true;statusLeg=-1;Status="Off route / keep current leg, or explicitly rejoin another";return;}
            // Only a deliberate leg selection can acquire a distant part of that leg.
            // Normal progress is constrained by distance physically traveled while away.
            double budget=explicitJoin?horizon-Distance:Math.Max(.08,detourTravel*1.5+.08);
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
                {Leg++;Distance=route.legs[Leg].start;explicitJoin=true;Status="Direction change / leg "+(Leg+1);}
            }
        }
        public int UpcomingTurn(double lookAhead=28)
        {
            int start=route.IndexAt(Distance+1),end=route.IndexAt(Math.Min(route.Length,Distance+lookAhead));
            for(int i=start;i<=end;i++)if(Math.Abs(route.samples[i].curvature)>.04f)return i;return -1;
        }
    }
}
