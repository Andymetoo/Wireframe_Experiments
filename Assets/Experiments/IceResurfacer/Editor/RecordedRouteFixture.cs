using System;
using IceResurfacer.Core;

namespace IceResurfacer.Editor
{
    /// <summary>Deterministic test driver of the existing generated reference. Never an operator-authored route.</summary>
    public static class RecordedRouteFixture
    {
        public static RecordedRouteData Create()
        {
            var sim=new Simulation(practice:true);var reference=new Simulation(practice:true);
            var route=new ResurfacingRoute(sim.Config);var recorder=new RouteRecorder();
            recorder.Start(sim,"DETERMINISTIC VALIDATION FIXTURE","Deterministic generated-reference test driver; NOT the operator's actual route");
            recorder.Data.id="00000000000000000000000000000054";recorder.Data.createdUtc="2026-10-29T00:00:00.0000000+00:00";
            // Ten stationary seconds must advance time without a trail of duplicate points.
            for(int i=0;i<500;i++)Tick(sim,reference,recorder);
            if(recorder.Data.samples.Count!=1)throw new Exception("Stationary fixture generated redundant observations.");
            int waypoint=1,end=Math.Min(route.WorkStart+1000,route.WorkEnd-1);
            for(int tick=0;waypoint<end;tick++)
            {
                if(tick>45000)throw new Exception("Recorded-route fixture failed to complete the reference segment.");
                while(waypoint<end&&Distance(route.Points[waypoint],sim)<1.25)waypoint++;
                var p=route.Points[waypoint];float distance=Distance(p,sim);
                float desired=(float)Math.Atan2(p.X-sim.Vehicle.X,p.Z-sim.Vehicle.Z);
                float steer=(float)Math.Atan2(2*sim.Config.Wheelbase*Math.Sin(desired-sim.Vehicle.Heading),Math.Max(.1,distance));
                sim.Controls.Steer=Simulation.Clamp(steer/sim.Config.MaxSteeringRadians,-1,1);
                sim.Controls.Throttle=p.Reverse?-.65f:.34f;sim.Controls.ConditionerTarget=p.Working?0:1;
                sim.Controls.HorizontalAuger=sim.Controls.VerticalAuger=p.Working;
                sim.Controls.FloodFlow=p.Working?.18f:0;sim.Controls.BladeDepthMm=p.Working?.35f:0;
                CopyControls(sim,reference);Tick(sim,reference,recorder);
            }
            sim.Controls.Throttle=0;sim.Controls.Brake=true;CopyControls(sim,reference);
            for(int i=0;i<650;i++)Tick(sim,reference,recorder);
            recorder.Stop("Deterministic validation segment complete");recorder.Data.Validate();
            // Every actual ice cell must match the run without a recorder.
            for(int i=0;i<sim.Ice.Cells.Length;i++)if(!sim.Ice.Cells[i].Equals(reference.Ice.Cells[i]))throw new Exception("Recording changed ice cell "+i);
            return recorder.Data;
        }
        static float Distance(RoutePoint p,Simulation sim)
        {float dx=p.X-sim.Vehicle.X,dz=p.Z-sim.Vehicle.Z;return(float)Math.Sqrt(dx*dx+dz*dz);}
        static void CopyControls(Simulation a,Simulation b)
        {
            var x=a.Controls;var y=b.Controls;
            y.Throttle=x.Throttle;y.Steer=x.Steer;y.Brake=x.Brake;y.ConditionerTarget=x.ConditionerTarget;
            y.HorizontalAuger=x.HorizontalAuger;y.VerticalAuger=x.VerticalAuger;y.FloodFlow=x.FloodFlow;y.BladeDepthMm=x.BladeDepthMm;
        }
        static void Tick(Simulation sim,Simulation reference,RouteRecorder recorder)
        {
            sim.Step(.02f);reference.Step(.02f);recorder.Observe(sim);
            if(sim.Vehicle.X!=reference.Vehicle.X||sim.Vehicle.Z!=reference.Vehicle.Z||sim.Vehicle.Heading!=reference.Vehicle.Heading||
                sim.Vehicle.Speed!=reference.Vehicle.Speed||sim.Vehicle.SteeringAngle!=reference.Vehicle.SteeringAngle||
                sim.Conditioner.X!=reference.Conditioner.X||sim.Conditioner.Z!=reference.Conditioner.Z||sim.Conditioner.Heading!=reference.Conditioner.Heading||
                sim.Conditioner.Lift!=reference.Conditioner.Lift||sim.TotalFloodUsedLiters!=reference.TotalFloodUsedLiters||
                sim.TotalExpandedShavingsM3!=reference.TotalExpandedShavingsM3||sim.SnowTankM3!=reference.SnowTankM3||sim.Time!=reference.Time)
                throw new Exception("Route observer changed the paired vehicle / conditioner / material simulation.");
        }
    }
}
