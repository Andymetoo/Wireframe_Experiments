using System;

namespace IceResurfacer.Core
{
    public sealed partial class Simulation
    {
        public bool SnowContact { get; private set; }
        public float SnowContactDepthMeters { get; private set; }
        public float LoadSnow { get; private set; }
        bool deepSnowFeeding;

        // Snow is a yielding obstacle, not a rigid board. Contact with the real
        // stock limits travel, while the lowered intake meters the same stock
        // into the existing conveyors. Nothing is deleted by the tyres/body.
        float UpdateSnowContact(float command,float dt)
        {
            float sn=(float)Math.Sin(Vehicle.Heading),cs=(float)Math.Cos(Vehicle.Heading);
            float maximum=0;
            float behind=Config.HitchBehindAxle+Config.DrawbarLength;
            for(int row=0;row<=6;row++)
            {
                float fore=-behind+(Config.BodyFront+behind)*row/6f;
                for(int col=0;col<=8;col++)
                {
                    float side=Config.VehicleHalfWidth*(col/4f-1);
                    maximum=Math.Max(maximum,Ice.SnowDepthMeters(Vehicle.X+side*cs+fore*sn,Vehicle.Z-side*sn+fore*cs));
                }
            }
            SnowContactDepthMeters=maximum;
            float excess=Math.Max(0,maximum-Config.LooseSnowIntakeHeightMm*.001f);
            SnowContact=excess>.001f;
            LoadSnow=Clamp(excess*1.3f,0,.9f);
            float limit=Math.Max(MaximumForwardSpeedTarget,Config.ReverseSpeed);
            if(!SnowContact&&!deepSnowFeeding)return limit;
            // A metre-deep bank reduces ordinary travel to roughly 0.1 m/s.
            // The reaction is applied as deceleration in UpdateVehicle, so
            // impact does not teleport the chassis or abruptly reset velocity.
            limit=Math.Min(limit,.08f+limit/(1+excess*excess*180));
            if(command<=0||Conditioner.Lift>.001f)return limit;
            float depth=0;
            for(int col=0;col<16;col++)
            {
                float side=Config.ConditionerWidth*((col+.5f)/16f-.5f);
                float columnDepth=0;
                for(int row=0;row<5;row++)
                {
                    float fore=-behind+Config.ConditionerLength*(row/4f-.5f);
                    columnDepth=Math.Max(columnDepth,Ice.SnowDepthMeters(Vehicle.X+side*cs+fore*sn,Vehicle.Z-side*sn+fore*cs));
                }
                depth+=Math.Max(0,columnDepth-(deepSnowFeeding?0:Config.LooseSnowIntakeHeightMm*.001f));
            }
            depth/=16;
            if(depth<=.001f)return limit;
            // A deep bank must pass through the finite intake as the machine
            // creeps into it. A stopped/full/jammed conveyor leaves the pile;
            // reverse remains available to back out of that contact.
            float intake=Controls.HorizontalAuger&&!ConveyorJammed?
                Math.Min(Config.HorizontalCapacityM3PerSecond,Math.Max(0,Config.VerticalSnowCapacityM3-VerticalSnowM3)/dt):0;
            float plow=Math.Max(.006f,intake/(Config.ConditionerWidth*depth));
            return Math.Min(limit,plow);
        }
    }
}
