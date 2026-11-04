using System;
using System.Reflection;
using IceResurfacer.Core;

namespace IceResurfacer.Editor
{
    // Production processing at prescribed apparatus poses, not a driven route.
    // Shared by the standalone conservation runner and isolated Unity checks.
    public static class OperatorMaterialValidation
    {
        public sealed class Apparatus
        {
            public readonly Simulation Sim;
            readonly Action footprint;
            readonly Action<float> process, convey;
            readonly FieldInfo lastX, lastZ;
            readonly PropertyInfo time;
            public Apparatus(Simulation sim)
            {
                Sim=sim;
                footprint=(Action)Delegate.CreateDelegate(typeof(Action),sim,Method("FindFootprint"));
                process=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),sim,Method("ProcessIce"));
                convey=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),sim,Method("ConveySnow"));
                lastX=typeof(Simulation).GetField("lastConditionerX",BindingFlags.Instance|BindingFlags.NonPublic);
                lastZ=typeof(Simulation).GetField("lastConditionerZ",BindingFlags.Instance|BindingFlags.NonPublic);
                time=typeof(Simulation).GetProperty("Time");
            }
            static MethodInfo Method(string name)=>typeof(Simulation).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic);
            public void Sample(float x,float z,float advance=.1f,float dt=.1f)
            {
                Sim.Conditioner.X=x;Sim.Conditioner.Z=z;Sim.Conditioner.Heading=0;Sim.Conditioner.Lift=0;
                Sim.Vehicle.X=x;Sim.Vehicle.Z=z+Sim.Config.HitchBehindAxle+Sim.Config.DrawbarLength;
                Sim.Vehicle.Heading=0;Sim.Vehicle.Speed=advance/dt;
                lastX.SetValue(Sim,x);lastZ.SetValue(Sim,z-advance);
                time.SetValue(Sim,Sim.Time+dt);
                footprint();process(dt);convey(dt);
            }
            public void Line(float x,float from,float to)
            { for(int i=0;i<=(int)Math.Round((to-from)/.1f);i++)Sample(x,from+i*.1f); }
            public void Drain(){for(int i=0;i<120;i++)convey(.1f);}
        }
        public static double SurfaceSnow(Simulation s)
        { double v=0,area=s.Ice.CellSize*s.Ice.CellSize*.001;foreach(var c in s.Ice.Cells)if(c.IsIce)v+=c.SnowMm*area;return v; }
        public static double SnowStock(Simulation s)
        {
            double v=SurfaceSnow(s)+s.SnowTankM3+s.HorizontalSnowM3+s.VerticalSnowM3+s.PitSnowM3;
            foreach(var c in s.Environment.Cells)v+=c.SnowM3;return v;
        }
        public static double WaterStock(Simulation s)
        {
            double v=s.FloodWaterLiters+s.WashWaterLiters+s.Environment.DrainedWaterLiters;
            foreach(var c in s.Ice.Cells)v+=c.FloodMm*s.Ice.CellSize*s.Ice.CellSize;
            foreach(var c in s.Environment.Cells)v+=c.WaterLiters;return v;
        }
        public static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static void Near(double a,double b,double tolerance,string message)
        {Require(Math.Abs(a-b)<=tolerance,message+": "+a+" vs "+b);}

        public static float WashCut(SimulatorConfig config,Action<string> log,float valve=1,bool flood=false,bool pump=true)
        {
            var s=new Simulation(config);var a=new Apparatus(s);
            double water=WaterStock(s);float min=s.WashWaterLiters;
            var returnMeter=typeof(Simulation).GetProperty("TotalWashRecirculatedLiters");
            Func<double> returns=()=>returnMeter!=null?(double)returnMeter.GetValue(s)+s.TotalRecoveredSurfaceLiters:0;
            s.Controls.WashValve=valve;s.Controls.FloodFlow=flood?1:0;
            for(int i=0;i<6000;i++)
            {
                // Prime for the first ten seconds before enabling the pickup pump.
                s.Controls.WashPump=pump&&i>=100;
                int lane=i/500;float x=-10+lane%11*1.9f,z=-25+(i%500)*.1f;
                double oldReturns=returns();
                a.Sample(x,z);min=Math.Min(min,s.WashWaterLiters);
                Require(returns()-oldReturns<=config.WashPickupLitersPerSecond*.1+.000001,"pickup exceeded finite pump capacity");
                Require(s.WashWaterLiters>=0&&s.WashWaterLiters<=config.WashCapacityLiters,"wash tank bounds");
            }
            double loss=water-WaterStock(s);
            // Existing float tank arithmetic accumulates <0.25 L error over
            // 6,000 transactions; compare all stock, including frozen film.
            Near(loss,0,.25,"wash/flood conservation (L)");
            Require(s.PumpDamage==0,"correct priming damaged pump");
            double treatment=0,scratch=0;foreach(var c in s.Ice.Cells){treatment+=c.WashLiters;scratch+=c.Scratch;}
            log("wash600 valve="+valve+" flood="+flood+" pump="+pump+" recovery="+config.WashRecoveryFraction.ToString("F6")+
                " remaining_L="+s.WashWaterLiters.ToString("F4")+" net_loss_L="+(310-s.WashWaterLiters).ToString("F4")+
                " surface_return_L="+s.TotalRecoveredSurfaceLiters.ToString("F4")+" treatment_L="+treatment.ToString("F4")+
                " scratch_sum="+scratch.ToString("F4")+" balance_error_L="+loss.ToString("F6"));
            return s.WashWaterLiters;
        }
        public static void FullSheet(SimulatorConfig config,Action<string> log,bool augers)
        {
            var s=new Simulation(config);var a=new Apparatus(s);double snow=SnowStock(s),water=WaterStock(s);
            s.Controls.HorizontalAuger=s.Controls.VerticalAuger=augers;s.Controls.BladeDepthMm=.35f;
            s.Controls.FloodFlow=1;
            for(float x=-12.675f;x<13;x+=1.95f)a.Line(x,-30.5f,30.5f);
            a.Drain();double snowError=SnowStock(s)-snow-s.TotalExpandedShavingsM3;
            double waterError=WaterStock(s)-water;
            Near(snowError,0,.00015,"full sheet snow conservation m3");Near(waterError,0,.25,"full sheet water conservation L");
            log("full_sheet augers="+augers+" seconds="+s.Time.ToString("F1")+" flood_L="+s.TotalFloodUsedLiters.ToString("F3")+
                " shaved_solid_m3="+s.TotalShavedIceM3.ToString("F6")+" expanded_m3="+s.TotalExpandedShavingsM3.ToString("F6")+
                " tank_m3="+s.SnowTankM3.ToString("F6")+" tank_pct="+(100*s.SnowTankM3/config.SnowCapacityM3).ToString("F3")+
                " overflow_transfer_m3="+s.SpilledSnowM3.ToString("F6")+" surface_m3="+SurfaceSnow(s).ToString("F6")+
                " snow_error_m3="+snowError.ToString("F8")+" water_error_L="+waterError.ToString("F6"));
        }
        public static void OrdinaryStreak(SimulatorConfig config,Action<string> log,bool expectClear)
        {
            var s=new Simulation(config);var a=new Apparatus(s);
            // Finite material source: 10 L loose snow laid as an ordinary narrow windrow.
            float deposited=0;for(int i=0;i<100;i++)deposited+=s.Ice.DepositSnow(0,-5+i*.1f,.0001f,.12f);
            double snow=SnowStock(s);
            s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;s.Controls.BladeDepthMm=.35f;
            a.Line(0,-6,6);a.Drain();
            double residual=0;float max=0;int white=0;
            for(int i=0;i<s.Ice.Cells.Length;i++)
            {
                float x=s.Ice.CellX(i%s.Ice.Columns),z=s.Ice.CellZ(i/s.Ice.Columns);
                if(Math.Abs(x)>.20f||z< -5||z>4.9f)continue;
                float depth=s.Ice.Cells[i].SnowMm;residual+=depth*s.Ice.CellSize*s.Ice.CellSize*.001;
                max=Math.Max(max,depth);if(depth>.36f)white++;
            }
            Near(SnowStock(s)-snow-s.TotalExpandedShavingsM3,0,.00002,"recollection mass balance");
            if(expectClear)Near(residual,0,.0000001,"ordinary streak not fully recollected");
            log("ordinary_streak source_m3="+deposited.ToString("F6")+" pickup_mm="+config.LooseSnowPickupDepthMm+
                " remaining_m3="+residual.ToString("F8")+" max_mm="+max.ToString("F6")+" white_cells="+white+
                " tank_m3="+s.SnowTankM3.ToString("F6"));
        }
        public static void DrivenCut(SimulatorConfig config,Action<string> log,string label)
        {
            var s=new Simulation(config);var route=new ResurfacingRoute(config);int waypoint=1,stalled=0;
            double stock=SnowStock(s),water=WaterStock(s);
            Func<RoutePoint,float> distance=p=>{float x=p.X-s.Vehicle.X,z=p.Z-s.Vehicle.Z;return (float)Math.Sqrt(x*x+z*z);};
            for(int tick=0;tick<150000;tick++)
            {
                while(waypoint<route.Points.Count-1&&distance(route.Points[waypoint])<1.25f)waypoint++;
                var p=route.Points[waypoint];float d=distance(p);
                if(waypoint==route.Points.Count-1&&d<.2f)break;
                float desired=(float)Math.Atan2(p.X-s.Vehicle.X,p.Z-s.Vehicle.Z);
                float steer=(float)Math.Atan2(2*config.Wheelbase*Math.Sin(desired-s.Vehicle.Heading),Math.Max(.1f,d));
                s.Controls.Steer=Simulation.Clamp(steer/config.MaxSteeringRadians,-1,1);s.Controls.Throttle=p.Reverse?-.34f:.34f;
                bool working=waypoint>route.WorkStart&&waypoint<route.WorkEnd;s.Controls.ConditionerTarget=working?0:1;
                bool contact=working&&s.Conditioner.Lift<.1f;
                s.Controls.HorizontalAuger=s.Controls.VerticalAuger=working||s.HorizontalSnowM3+s.VerticalSnowM3>.00001f;
                s.Controls.BladeDepthMm=contact?.3f:0;s.Controls.FloodFlow=contact?.6f:0;
                s.Controls.WashValve=contact?1:0;s.Controls.WashPump=contact&&s.WashPrimeSeconds>=10;s.Controls.BoardBrush=contact;
                s.Step(.02f);stalled=tick>100&&Math.Abs(s.Vehicle.Speed)<.001f?stalled+1:0;
                Require(stalled<200&&tick!=149999,label+" generated-reference drive stalled/timeout at "+waypoint+"/"+route.Points.Count+" "+s.Vehicle.X+","+s.Vehicle.Z);
            }
            double snowError=SnowStock(s)-stock-s.TotalExpandedShavingsM3,waterError=WaterStock(s)-water;
            Near(snowError,0,.0002,label+" driven snow conservation");Near(waterError,0,1.5,label+" driven water conservation");
            var report=s.GetReport();log("DRIVEN "+label+" generated reference seconds="+s.Time.ToString("F2")+" coverage="+report.Coverage.ToString("F6")+
                " flood_remaining_L="+s.FloodWaterLiters.ToString("F4")+" wash_remaining_L="+s.WashWaterLiters.ToString("F4")+" tank_m3="+s.SnowTankM3.ToString("F6")+
                " snow_error_m3="+snowError.ToString("F8")+" water_error_L="+waterError.ToString("F6"));
            Require(waypoint>=route.WorkEnd&&s.InServiceBay,label+" representative full cut failed to return");
        }
        public static void WashTreatmentResponse(Func<SimulatorConfig> config,Action<string> log)
        {
            double[] scratch=new double[2],quality=new double[2],treatment=new double[2];
            for(int test=0;test<2;test++)
            {
                var s=new Simulation(config());var a=new Apparatus(s);s.Controls.WashValve=test==0?1:.5f;
                a.Line(0,-20,20);
                foreach(var cell in s.Ice.Cells)if(cell.IsIce){scratch[test]+=cell.Scratch;quality[test]+=cell.Quality;treatment[test]+=cell.WashLiters;}
            }
            Require(treatment[0]>treatment[1]*1.99,"lower wash valve lost treatment-flow response");
            Require(scratch[0]<scratch[1]&&quality[0]>quality[1],"lower wash flow lost ice-quality response");
            log("PASS half wash flow halves treatment and leaves more scratches/lower quality: scratch_sum="+scratch[0].ToString("F4")+" / "+scratch[1].ToString("F4")+" quality_sum="+quality[0].ToString("F4")+" / "+quality[1].ToString("F4"));
        }
    }
}
