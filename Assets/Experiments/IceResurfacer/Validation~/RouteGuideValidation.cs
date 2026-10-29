using System;
using IceResurfacer.Core;

internal static class RouteGuideValidation
{
    static double integratedFloodInput;
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        integratedFloodInput=0;
        var s=new Simulation();var c=s.Config;float initialX=s.Vehicle.X,initialZ=s.Vehicle.Z;
        var route=new ResurfacingRoute(c);
        Assert(s.Vehicle.X==initialX&&s.Vehicle.Z==initialZ&&s.TotalFloodUsedLiters==0,"Constructing advisory geometry mutated the simulation");
        Assert(route.PassSpacing<c.ConditionerWidth&&route.PassSpacing<c.WaterDistributionWidth,"Route overlap must respect narrower physical blade");
        Assert(route.MinimumRadius>=c.Wheelbase/Math.Tan(c.MaxSteeringRadians),"Reference turn radius smaller than chassis capability");
        Assert(route.Points.Count<20000&&route.WorkStart>1&&route.WorkEnd<route.Points.Count,"Route needs entry, bounded work and return");
        for(int i=1;i<route.Points.Count;i++)
        {
            var p=route.Points[i];var before=route.Points[i-1];
            double d=Math.Sqrt((p.X-before.X)*(p.X-before.X)+(p.Z-before.Z)*(p.Z-before.Z));
            Assert(!double.IsNaN(d)&&d<=.401,"Route must have finite continuous segments at point "+i);
        }
        double initialSnow=SnowStock(s),beforeSpill=0;
        float[] snowBefore=null,snowAfter=null;bool spillStarted=false,spillEnded=false,pooled=false;
        double spillVolume=0;int witnessCount=0;float poolTime=0;double poolLiquid=0;
        int waypoint=1,stopped=0;
        for(int tick=0;tick<150000;tick++)
        {
            while(waypoint<route.Points.Count-1&&Distance(route.Points[waypoint],s)<1.25)waypoint++;
            var p=route.Points[waypoint];float distance=Distance(p,s);
            if(waypoint==route.Points.Count-1&&distance<.15)break;
            float desired=(float)Math.Atan2(p.X-s.Vehicle.X,p.Z-s.Vehicle.Z);
            float steer=(float)Math.Atan2(2*c.Wheelbase*Math.Sin(desired-s.Vehicle.Heading),Math.Max(.1,distance));
            s.Controls.Steer=Simulation.Clamp(steer/c.MaxSteeringRadians,-1,1);
            s.Controls.Throttle=p.Reverse?-.65f:.34f;
            s.Controls.ConditionerTarget=p.Working?0:1;
            if(p.Working)
            {
                s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;
                s.Controls.BladeDepthMm=s.Conditioner.Lift<.1f?.3f:0;
                s.Controls.FloodFlow=s.Conditioner.Lift<.1f?.23f:0;s.Controls.BoardBrush=true;
            }
            else
            {
                s.Controls.BladeDepthMm=0;s.Controls.FloodFlow=0;s.Controls.BoardBrush=false;
                bool snow=s.HorizontalSnowM3+s.VerticalSnowM3>.00001f;
                s.Controls.HorizontalAuger=s.Controls.VerticalAuger=snow;
                if(snow)s.Controls.ConditionerTarget=0;
            }
            // This is a validation driver following the advisory geometry, not
            // an in-game autopilot. These mistakes use only real equipment controls.
            bool spillPass=p.Working&&waypoint>=route.WorkStart+15&&waypoint<route.WorkStart+95;
            if(spillPass)
            {
                if(!spillStarted)
                {
                    snowBefore=SnowSamples(s);beforeSpill=s.SpilledSnowM3;spillStarted=true;
                    Console.WriteLine("  deliberate deep-cut / conveyors OFF begins at waypoint "+waypoint+", world "+s.Vehicle.X.ToString("F2")+","+s.Vehicle.Z.ToString("F2")+", time="+s.Time.ToString("F1"));
                }
                s.Controls.BladeDepthMm=s.Conditioner.Lift<.1f?c.MaxBladeDepthMm:0;
                s.Controls.HorizontalAuger=s.Controls.VerticalAuger=false;
            }
            else if(spillStarted&&!spillEnded)
            {
                snowAfter=SnowSamples(s);spillVolume=s.SpilledSnowM3-beforeSpill;
                for(int cell=0;cell<snowAfter.Length;cell++)if(snowAfter[cell]>snowBefore[cell]+.25f)witnessCount++;
                spillEnded=true;
                Console.WriteLine("  conveyors ON after "+spillVolume.ToString("F6")+" m3 escaped; "+witnessCount+" actual cells gained >0.25mm loose snow; trough="+s.HorizontalSnowM3.ToString("F6")+" m3");
            }
            if(spillEnded&&!pooled&&p.Working&&waypoint>=route.WorkStart+150)
            {
                s.Controls.Throttle=0;s.Controls.Brake=true;s.Controls.FloodFlow=0;s.Controls.BladeDepthMm=0;
                Tick(s,2);
                double used=s.TotalFloodUsedLiters;WaterStock(s,out double liquidBefore,out double frozenBefore);
                s.Controls.FloodFlow=1;Tick(s,5);s.Controls.FloodFlow=0;
                WaterStock(s,out double liquidAfter,out double frozenAfter);
                double released=s.TotalFloodUsedLiters-used,retained=(liquidAfter+frozenAfter)-(liquidBefore+frozenBefore);
                Assert(Math.Abs(released-9)<.015&&Math.Abs(released-retained)<.025,"Stationary fullrun flood must remain finite and accounted on ice");
                float maxLiquid=0;for(int cell=0;cell<s.Ice.Cells.Length;cell++)if(s.Ice.Cells[cell].IsIce)maxLiquid=Math.Max(maxLiquid,s.Ice.LiquidDepth(s.Ice.Cells[cell],s.Time));
                Assert(maxLiquid>c.ExcessFloodDepthMm,"Five-second stationary flood must create actual excess water");
                Console.WriteLine("  stationary flood5s: released="+released.ToString("F4")+"L, retained liquid+frozen delta="+retained.ToString("F4")+"L; maximum liquid="+maxLiquid.ToString("F3")+"mm");
                pooled=true;poolTime=s.Time;poolLiquid=liquidAfter;s.Controls.Brake=false;
                continue;
            }
            MeasuredStep(s);
            stopped=tick>100&&Math.Abs(s.Vehicle.Speed)<.001?stopped+1:0;
            Assert(stopped<150,"Reference route blocked "+waypoint+" / "+route.Points.Count+" at "+s.Vehicle.X+","+s.Vehicle.Z+" "+s.LastMovementContact.Reason);
            Assert(tick<149999,"Reference route timeout");
        }
        s.Controls.Throttle=0;s.Controls.Brake=true;Tick(s,2);s.Controls.HorizontalAuger=s.Controls.VerticalAuger=false;s.Controls.ConditionerTarget=1;Tick(s,3);
        Assert(s.HasEnteredRink&&s.InServiceBay,"Reference route must enter and return through actual gate");
        s.Controls.TankLiftTarget=1;Tick(s,16);
        for(int i=0;i<9&&s.SnowTankM3>.000001f;i++)
        {s.SetTankHydraulic(-1);Tick(s,.36f);s.SetTankHydraulic(1);Tick(s,.36f);s.SetTankHydraulic(0);Tick(s,.5f);}
        s.Controls.HydraulicHoldControls=false;s.Controls.TankLiftTarget=0;Tick(s,6);
        Assert(s.TryFinish()&&s.ProperExit,"Reference route allows unloading and normal results");
        var report=s.GetReport();
        Assert(spillStarted&&spillEnded&&spillVolume>.005&&witnessCount>20&&pooled,"Full run must actually exercise overflow and stationary pooling");
        double deposited=0,remaining=0;int recoveredCells=0;
        for(int i=0;i<snowAfter.Length;i++)
        {
            if(snowAfter[i]<=snowBefore[i]+.25f)continue;
            deposited+=(snowAfter[i]-snowBefore[i])*s.Ice.CellSize*s.Ice.CellSize*.001;
            remaining+=Math.Max(0,s.Ice.Cells[i].SnowMm-snowBefore[i])*s.Ice.CellSize*s.Ice.CellSize*.001;
            if(s.Ice.Cells[i].SnowMm<snowAfter[i]-.25f)recoveredCells++;
        }
        double recovered=deposited-remaining,snowError=SnowStock(s)-initialSnow-s.TotalExpandedShavingsM3;
        Assert(recovered>deposited*.10&&recoveredCells>20,"Later overlapping passes must reduce the same escaped ground cells");
        Assert(s.TotalLooseSnowPickupM3>recovered&&s.TotalSnowCollectedM3>recovered&&s.PitSnowM3>recovered,"Recovered ground snow follows conveyors to tank and manual unloading");
        Assert(Math.Abs(snowError)<.0002,"Whole-run snow must conserve initial stock plus expanded shavings without double collection");
        WaterStock(s,out double endLiquid,out double endFrozen);double floor=0;foreach(var ground in s.Environment.Cells)floor+=ground.WaterLiters;
        double waterError=endLiquid+endFrozen+floor+s.Environment.DrainedWaterLiters-integratedFloodInput-s.TotalWashUsedLiters+s.TotalRecoveredSurfaceLiters;
        Assert(endFrozen>100&&s.Time-poolTime>300,"Full run must progress actual liquid-to-frozen state over minutes");
        Console.WriteLine("  water audit detail: field="+(endLiquid+endFrozen).ToString("F6")+"L, floor="+floor.ToString("F6")+"L, drain="+s.Environment.DrainedWaterLiters.ToString("F6")+"L, integrated actual discharge="+integratedFloodInput.ToString("F6")+"L, float released counter="+s.TotalFloodUsedLiters.ToString("F6")+"L, tank delta="+(s.Config.FloodCapacityLiters-s.FloodWaterLiters).ToString("F6")+"L, error="+waterError.ToString("F6")+"L");
        Assert(Math.Abs(waterError)<.025,"Whole-run water phase, floor and drain stock must conserve released liters");
        Console.WriteLine("  same-cell windrow witnesses: deposited excess="+deposited.ToString("F6")+"m3, remaining excess="+remaining.ToString("F6")+"m3, net recovered="+recovered.ToString("F6")+"m3 across "+recoveredCells+" cells; total loose pickup="+s.TotalLooseSnowPickupM3.ToString("F6")+"m3; physical snow balance error="+snowError.ToString("F8")+"m3");
        Console.WriteLine("  final water: liquid="+endLiquid.ToString("F4")+"L, frozen="+endFrozen.ToString("F4")+"L, floor="+floor.ToString("F4")+"L, drain="+s.Environment.DrainedWaterLiters.ToString("F4")+"L, balance error="+waterError.ToString("F6")+"L; "+(s.Time-poolTime).ToString("F1")+"s since stationary flood (whole-sheet liquid then "+poolLiquid.ToString("F3")+"L)");
        Console.WriteLine("  No pose writes, refills, material resets or scoring changes. Runtime guide remains read-only; this test controller drives the generated path. Guide visibility and manual departures are separate native UI tests.");
        Console.WriteLine("  generated advisory route: "+route.Points.Count+" points / "+route.Length.ToString("F1")+" m; overlap spacing="+route.PassSpacing.ToString("F3")+"m radius="+route.MinimumRadius.ToString("F3")+"m; coverage="+report.Coverage.ToString("P2")+" water="+report.WaterCoverage.ToString("P2")+" score="+report.Score.ToString("F1")+" flood="+s.TotalFloodUsedLiters.ToString("F2")+"L pit="+s.PitSnowM3.ToString("F4")+"m3 elapsed="+s.Time.ToString("F1")+"s misuse="+s.Mistakes);
        Assert(report.Coverage>=.95f,"Deliberate mistakes must be recovered while completing at least95% of reachable ice; route never grants credit by itself");
    }
    static float[] SnowSamples(Simulation s)
    {var data=new float[s.Ice.Cells.Length];for(int i=0;i<data.Length;i++)data[i]=s.Ice.Cells[i].SnowMm;return data;}
    static double SnowStock(Simulation s)
    {
        double value=s.SnowTankM3+s.HorizontalSnowM3+s.VerticalSnowM3+s.PitSnowM3;
        foreach(var cell in s.Ice.Cells)if(cell.IsIce)value+=cell.SnowMm*s.Ice.CellSize*s.Ice.CellSize*.001;
        foreach(var cell in s.Environment.Cells)value+=cell.SnowM3;return value;
    }
    static void WaterStock(Simulation s,out double liquid,out double frozen)
    {
        liquid=frozen=0;foreach(var cell in s.Ice.Cells)if(cell.IsIce)
        {liquid+=s.Ice.LiquidDepth(cell,s.Time)*s.Ice.CellSize*s.Ice.CellSize;frozen+=s.Ice.FrozenDepth(cell,s.Time)*s.Ice.CellSize*s.Ice.CellSize;}
    }
    static float Distance(RoutePoint p,Simulation s){float x=p.X-s.Vehicle.X,z=p.Z-s.Vehicle.Z;return(float)Math.Sqrt(x*x+z*z);}
    static void MeasuredStep(Simulation s)
    {
        // Integrate exactly the finite float flow the core consumes, in double.
        // Long-run float tank/cumulative-counter roundoff is reported separately.
        integratedFloodInput+=Math.Min(s.FloodWaterLiters,s.Controls.FloodFlow*s.Config.FloodMaxLitersPerSecond*.02f);
        s.Step(.02f);
    }
    static void Tick(Simulation s,float seconds){for(int i=0;i<(int)Math.Round(seconds/.02);i++)MeasuredStep(s);}
}
