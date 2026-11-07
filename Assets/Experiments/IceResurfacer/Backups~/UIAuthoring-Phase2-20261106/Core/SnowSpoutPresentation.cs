using System;

namespace IceResurfacer.Core
{
    /// <summary>Cosmetic sampling of an already completed tank transfer. Never moves snow.</summary>
    public sealed class SnowSpoutPresentation
    {
        public const int MaximumParticlesPerFrame=64;
        public const float SamplesPerM3=180000;
        float remainder;
        public float DeliveryRate {get;private set;}
        public void Reset(){remainder=0;DeliveryRate=0;}
        public int Sample(double deliveredM3,float dt,float visibility=1)
        {
            DeliveryRate=0;
            if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt)||visibility<=0)
            {remainder=0;return 0;}
            if(deliveredM3<=0||double.IsNaN(deliveredM3)||double.IsInfinity(deliveredM3))return 0;
            visibility=Simulation.Clamp(visibility,0,3);
            DeliveryRate=(float)(deliveredM3/dt);
            double requested=deliveredM3*SamplesPerM3*visibility+remainder;
            double whole=Math.Floor(requested);remainder=(float)(requested-whole);
            // Drop excess cosmetic samples; never accumulate a delayed fake jet.
            return (int)Math.Min(whole,MaximumParticlesPerFrame);
        }
        public static SnowSurfacePoint Velocity(SnowSurfacePoint outlet,SnowSurfacePoint opening)
        {
            // Cross beneath the forward half of the existing opening, clear of
            // its central guard. The fixed inlet and authored opening set aim.
            return new SnowSurfacePoint((opening.X-outlet.X)/.30f,1.05f,(opening.Z+.16f-outlet.Z)/.30f);
        }
    }
}
