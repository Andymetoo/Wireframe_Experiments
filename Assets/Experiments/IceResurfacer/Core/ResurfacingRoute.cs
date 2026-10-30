using System;
using System.Collections.Generic;

namespace IceResurfacer.Core
{
    public readonly struct RoutePoint
    {
        public readonly float X,Z;
        public readonly bool Reverse,Working;
        public RoutePoint(double x,double z,bool reverse,bool working){X=(float)x;Z=(float)z;Reverse=reverse;Working=working;}
    }

    /// <summary>Advisory rear-axle reference path. No access to controls, cells or scores.</summary>
    public sealed class ResurfacingRoute
    {
        public readonly List<RoutePoint> Points=new List<RoutePoint>(12000);
        public readonly float PassSpacing,MinimumRadius;
        public int WorkStart {get;private set;}
        public int WorkEnd {get;private set;}
        public float Length {get;private set;}
        double x,z,heading;
        bool working;
        // Recorded observations project into the same advisory path representation.
        // This copies positions in sequence; the raw samples remain untouched.
        public ResurfacingRoute(RecordedRouteData recorded)
        {
            recorded.Validate();PassSpacing=0;MinimumRadius=0;WorkStart=0;WorkEnd=recorded.samples.Count;
            foreach(var p in recorded.samples)
                Points.Add(new RoutePoint(p.vehicleX,p.vehicleZ,p.direction<0,p.conditionerLift<.05f&&p.bladeDepthMm>0));
            Length=(float)recorded.distanceMeters;
        }
        public ResurfacingRoute(SimulatorConfig c)
        {
            var layout=new ServiceLayout(c);
            PassSpacing=Math.Min(c.ConditionerWidth,c.WaterDistributionWidth)-.356f;
            PassSpacing=Math.Max(1.1f,Math.Min(1.6f,PassSpacing));
            MinimumRadius=Math.Max(4.6f,c.Wheelbase/(float)Math.Tan(c.MaxSteeringRadians)+.1f);
            layout.ParkingPose(out float px,out float pz,out float ph);x=px;z=pz;heading=ph;
            Add(false);Line(-layout.ParkOutward-14.3);
            Turn(Math.PI*.75,Math.Max(5,MinimumRadius));Line(x+4.5);Turn(Math.PI/2,6.92);
            WorkStart=Points.Count;working=true;
            float halfWidth=c.RinkWidth*.5f,halfLength=c.RinkLength*.5f;
            int rings=Math.Max(1,(int)Math.Floor((halfWidth-MinimumRadius-1.53f)/PassSpacing)+1);
            for(int ring=0;ring<rings;ring++)
            {
                double inset=1.53+ring*PassSpacing;
                if(ring>0)Shift(PassSpacing);
                Box(halfWidth-inset,halfLength-inset,Math.Max(MinimumRadius,c.CornerRadius-inset));
                if(ring==0)Line(8.52);
            }
            int lanes=Math.Max(1,(int)Math.Floor((c.RinkWidth-11.4f)/PassSpacing));
            for(int lane=0;lane<lanes;lane++)
            {
                double left=-halfWidth+2.05+lane*PassSpacing;
                Shift(left-x);Box(MinimumRadius,halfLength-9.4,MinimumRadius);
            }
            WorkEnd=Points.Count;working=false;
            Turn(-Math.PI,MinimumRadius);Line(4);Turn(-Math.PI/2,Math.Max(5,MinimumRadius));
            double targetSum=layout.GateX+layout.GateZ+layout.PitSide*Math.Sqrt(2);
            double r=Math.Max(5,MinimumRadius),turnSum=r*(Math.Sin(Math.PI/4)-(1-Math.Cos(Math.PI/4)));
            Line(targetSum-x-z-turnSum);Turn(Math.PI/4,r);
            layout.ToLocal((float)x,(float)z,out _,out float outward);Line(7.9-outward);
        }
        void Add(bool reverse)
        {
            if(Points.Count>=20000)throw new InvalidOperationException("Reference route exceeds its bounded point budget.");
            if(Points.Count>0){var p=Points[Points.Count-1];Length+=(float)Math.Sqrt((x-p.X)*(x-p.X)+(z-p.Z)*(z-p.Z));}
            Points.Add(new RoutePoint(x,z,reverse,working));
        }
        void Line(double length)
        {
            int n=Math.Max(1,(int)Math.Ceiling(Math.Abs(length)/.4));
            for(int i=0;i<n;i++){x+=Math.Sin(heading)*length/n;z+=Math.Cos(heading)*length/n;Add(length<0);}
        }
        void Turn(double angle,double radius)
        {
            if(Math.Abs(angle)<.000001)return;
            int n=Math.Max(1,(int)Math.Ceiling(Math.Abs(angle*radius)/.4));double da=angle/n,sign=Math.Sign(angle);
            for(int i=0;i<n;i++)
            {x+=radius/sign*(Math.Cos(heading)-Math.Cos(heading+da));z+=radius/sign*(Math.Sin(heading+da)-Math.Sin(heading));heading+=da;Add(false);}
        }
        void Shift(double dx)
        {
            double radius=Math.Max(5,MinimumRadius),a=Math.Acos(1-Math.Min(Math.Abs(dx)/(2*radius),1.99));
            Turn(Math.Sign(dx)*a,radius);Turn(-Math.Sign(dx)*a,radius);
        }
        void Box(double halfWidth,double halfLength,double radius)
        {
            Line(halfLength-radius-z);Turn(Math.PI/2,radius);Line(2*(halfWidth-radius));Turn(Math.PI/2,radius);
            Line(2*(halfLength-radius));Turn(Math.PI/2,radius);Line(2*(halfWidth-radius));Turn(Math.PI/2,radius);Line(-12-z);
        }
    }
}
