using System;

namespace IceResurfacer.Presentation
{
    // Presentation sampling only. Does not observe progress or write simulation state.
    public static class GpsGuidanceGeometry
    {
        public const float LookAheadMeters=24.384f,HiddenNearMeters=.55f,HeadingBeyondFrontMeters=6f;
        public const int PointCapacity=120,MaximumSilhouetteIntervals=144;

        public readonly struct Window
        {
            public readonly double Start,End;
            public readonly int WorldPoints,MapPoints;
            public Window(double distance,double routeLength)
            {
                End=Math.Min(routeLength,distance+LookAheadMeters);
                Start=Math.Min(End,distance+HiddenNearMeters);
                WorldPoints=End-Start<.1?0:Math.Min(PointCapacity,(int)Math.Ceiling((End-Start)/.35)+1);
                MapPoints=Math.Min(PointCapacity,Math.Max(2,(int)Math.Ceiling((End-distance)/.4)+1));
            }
        }
        public static void HeadingEnd(float x,float z,float headingRadians,float bodyFront,out float endX,out float endZ)
        {
            float length=bodyFront+HeadingBeyondFrontMeters;
            endX=x+(float)Math.Sin(headingRadians)*length;
            endZ=z+(float)Math.Cos(headingRadians)*length;
        }
        public static int SilhouetteIntervals(double length)
        {
            // Keep the old near-route density where it fits. The longer window
            // cannot double the expensive visible-mesh physics queries.
            return Math.Min(MaximumSilhouetteIntervals,Math.Max(2,(int)Math.Ceiling(length/.09)));
        }
        public static double SilhouetteOffset(int index,int intervals,double length)
        {
            if(length<=0)return 0;
            double near=Math.Min(length,8);
            int nearIntervals=Math.Min(intervals,(int)Math.Ceiling(near/.09));
            if(nearIntervals==intervals)return length*index/intervals;
            // Preserve dense silhouette edges near the cockpit. Spend the rest
            // of the fixed budget on the more distant continuation.
            return index<=nearIntervals?near*index/nearIntervals:
                near+(length-near)*(index-nearIntervals)/(intervals-nearIntervals);
        }
    }
}
