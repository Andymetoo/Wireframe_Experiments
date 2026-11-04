using System;
using System.Reflection;
using IceResurfacer.Core;

internal static class SnowCalibrationValidation
{
    // An apparatus sweep isolates the blade and conveyors. It intentionally
    // prescribes straight conditioner poses across the whole rounded sheet;
    // it is not a drivable route, edge-clearance test or scored playthrough.
    sealed class Apparatus
    {
        public readonly Simulation Sim;
        readonly Action footprint,load;
        readonly Action<float> process,convey;
        readonly FieldInfo lastX,lastZ;
        public double GrossSnowInput,LoadTotal;public int Samples;public float PeakLoad;
        public Apparatus(Simulation sim)
        {
            Sim=sim;
            footprint=(Action)Delegate.CreateDelegate(typeof(Action),sim,typeof(Simulation).GetMethod("FindFootprint",BindingFlags.Instance|BindingFlags.NonPublic));
            process=Bind("ProcessIce");convey=Bind("ConveySnow");
            load=(Action)Delegate.CreateDelegate(typeof(Action),sim,typeof(Simulation).GetMethod("UpdateMachineLoad",BindingFlags.Instance|BindingFlags.NonPublic));
            lastX=typeof(Simulation).GetField("lastConditionerX",BindingFlags.Instance|BindingFlags.NonPublic);
            lastZ=typeof(Simulation).GetField("lastConditionerZ",BindingFlags.Instance|BindingFlags.NonPublic);
        }
        Action<float> Bind(string name) { return (Action<float>)Delegate.CreateDelegate(typeof(Action<float>),Sim,typeof(Simulation).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)); }
        public void Sample(float x,float z,float advance,float dt=.1f)
        {
            Sim.Conditioner.X=x;Sim.Conditioner.Z=z;Sim.Conditioner.Heading=0;Sim.Conditioner.Lift=0;
            Sim.Vehicle.X=x;Sim.Vehicle.Z=z+Sim.Config.HitchBehindAxle+Sim.Config.DrawbarLength;Sim.Vehicle.Heading=0;Sim.Vehicle.Speed=advance/dt;
            lastX.SetValue(Sim,x);lastZ.SetValue(Sim,z-advance);
            footprint();float before=Sim.HorizontalSnowM3;process(dt);GrossSnowInput+=Sim.HorizontalSnowM3-before;convey(dt);
            load();LoadTotal+=Sim.MachineLoad;PeakLoad=Math.Max(PeakLoad,Sim.MachineLoad);Samples++;
        }
        public void Drain()
        { for(int i=0;i<120;i++)convey(.1f); }
    }
    struct Totals
    {
        public double Snow,Cut,Gouge,Quality,Overcut;public int Cells;
    }
    static Totals Scan(Simulation s)
    {
        var result=new Totals();double area=s.Ice.CellSize*s.Ice.CellSize;
        foreach(var c in s.Ice.Cells)if(c.IsIce)
        {
            result.Cells++;result.Snow+=c.SnowMm*area*.001;result.Cut+=c.CutDepthMm*area*.001;
            result.Gouge+=c.GougeDepthMm;result.Quality+=c.Quality;
            result.Overcut+=Math.Max(0,c.CutDepthMm-(c.Sensitive?s.Config.SensitiveCumulativeSafeCutMm:s.Config.CumulativeSafeCutMm));
        }
        return result;
    }
    static double Ground(Simulation s) { double value=0;foreach(var c in s.Environment.Cells)value+=c.SnowM3;return value; }
    public static void MeasureFullSheet()
    {
        Console.WriteLine("Full-sheet apparatus calibration: true 5 cm cells, 1 m/s straight sweeps, 1.95 m lane pitch, 0.10 m advance; no flood/wash. Prescribed poses include perimeter cells; NOT a driven route.");
        Console.WriteLine("Each second pass starts with empty apparatus reservoirs and the exact first-pass surface. This isolates repeated cutting after servicing; remaining field deposits persist. Gross loose pickup may include recollected spills. Estimated load uses the production formula, not measured engine power.");
        Console.WriteLine("setting_mm,effective_bite_mm,augers,pass,shaved_ice_m3,gross_loose_pickup_m3,expanded_shavings_m3,horizontal_m3,vertical_m3,tank_m3,tank_percent,escaped_transfer_m3,surface_snow_m3,ground_snow_m3,mean_estimated_load,peak_estimated_load,mean_residual_gouge_mm,mean_quality,mean_overcut_actual_mm,balance_error_m3");
        foreach(float depth in new[]{.15f,.35f,.8f})
        foreach(string augers in new[]{"both_on","both_off","horizontal_on_vertical_off"})
        {
            Simulation prior=null;
            for(int pass=1;pass<=2;pass++)
            {
                var s=new Simulation();
                if(prior!=null){Array.Copy(prior.Ice.Cells,s.Ice.Cells,s.Ice.Cells.Length);s.Ice.MarkChanged();}
                s.Controls.BladeDepthMm=depth;s.Controls.HorizontalAuger=augers!="both_off";s.Controls.VerticalAuger=augers=="both_on";
                var before=Scan(s);var fixture=new Apparatus(s);
                for(float x=-12.675f;x<13;x+=1.95f)
                for(int step=0;step<=610;step++)fixture.Sample(x,-30.5f+step*.1f,.1f);
                fixture.Drain();var after=Scan(s);
                double shaved=after.Cut-before.Cut,expanded=shaved*s.Config.SnowExpansionRatio;
                double ground=Ground(s),stock=after.Snow+s.HorizontalSnowM3+s.VerticalSnowM3+s.SnowTankM3+ground+s.PitSnowM3;
                double error=stock-before.Snow-expanded;
                if(Math.Abs(error)>.001)throw new Exception("Snow stock balance failed: "+error);
                Near(s.TotalShavedIceM3,shaved,.00002,"source meter differs from independent cut-depth scan");
                Near(s.TotalExpandedShavingsM3,expanded,.00006,"expansion meter differs from independent source scan");
                Console.WriteLine(string.Join(",",depth.ToString("F2"),s.ActualBladeDepthMm.ToString("F3"),augers,pass,
                    shaved.ToString("F6"),(fixture.GrossSnowInput-expanded).ToString("F6"),expanded.ToString("F6"),
                    s.HorizontalSnowM3.ToString("F6"),s.VerticalSnowM3.ToString("F6"),s.SnowTankM3.ToString("F6"),(s.SnowTankM3/s.Config.SnowCapacityM3*100).ToString("F2"),
                    s.SpilledSnowM3.ToString("F6"),after.Snow.ToString("F6"),ground.ToString("F6"),(fixture.LoadTotal/fixture.Samples).ToString("F4"),fixture.PeakLoad.ToString("F4"),
                    (after.Gouge/after.Cells).ToString("F6"),(after.Quality/after.Cells).ToString("F4"),(after.Overcut/after.Cells).ToString("F4"),error.ToString("F8")));
                prior=s;
            }
        }
    }

    static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    static void Near(double a,double b,double tolerance,string message){Assert(Math.Abs(a-b)<=tolerance,message+" ("+a+" vs "+b+")");}
    static double Stock(Simulation s){return Scan(s).Snow+Ground(s)+s.HorizontalSnowM3+s.VerticalSnowM3+s.SnowTankM3+s.PitSnowM3;}
    static void Line(Apparatus a,float x,float from,float to)
    { for(int step=0;step<=(int)Math.Round((to-from)/.1f);step++)a.Sample(x,from+step*.1f,.1f); }
    public static void Run()
    {
        BladeRange();SideEscapeRecovery();FailureStates();BoardRetention();SnowKernel();
        Console.WriteLine("  Snow calibration PASS: normal/deep blade mapping, finite two-sided overflow, recovered stock, distinct failures, board retention and source accounting.");
    }
    static void BladeRange()
    {
        var config=new SimulatorConfig();Near(config.BladeWheelTurns,3.5,0,"handwheel range changed");
        Near(config.EffectiveBladeDepth(.15f),.024,.000001,"light cut changed");Near(config.EffectiveBladeDepth(.35f),.056,.000001,"normal cut changed");
        Near(config.EffectiveBladeDepth(.8f),.60,.000001,"maximum actual bite not calibrated");
        Near(config.SnowExpansionRatio,3.1,.000001,"snow expansion changed");Near(config.SnowCapacityM3,3.54,.000001,"tank gauge capacity changed");
        float prior=0;for(int i=0;i<=800;i++){float bite=config.EffectiveBladeDepth(i*.001f);Assert(bite>=prior,"blade response not monotonic");prior=bite;}
        var s=new Simulation(config);var apparatus=new Apparatus(s);s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;
        s.Controls.BladeDepthMm=.35f;apparatus.Sample(0,0,.1f);Near(s.MachineLoad,.349,.00001,"normal load/drive response changed");
        s.Controls.BladeDepthMm=.8f;apparatus.Sample(0,.1f,.1f);Assert(s.MachineLoad>1.4,"deep actual bite lacks mechanical load");
        var c=s.Ice.Cells[s.Ice.IndexAt(2,0)];c.SnowMm=c.GougeDepthMm=c.Scratch=0;c.FloodMm=.3f;c.CutDepthMm=.6f;s.Ice.UpdateQuality(ref c);float once=c.Quality;
        c.CutDepthMm=1.2f;s.Ice.UpdateQuality(ref c);Assert(c.Quality<once-.15,"repeated deep removal lacks uniformity penalty");
        Console.WriteLine("  blade calibration: .15/.35/.80 settings -> .024/.056/.600 mm actual; normal 1 m/s load=.349; maximum load="+s.MachineLoad.ToString("F4")+"; 3.5 turns and 3.54 m3 capacity preserved.");
    }
    static void SideEscapeRecovery()
    {
        var s=new Simulation();var a=new Apparatus(s);s.Controls.BladeDepthMm=.8f;
        double initial=Stock(s);Line(a,0,-15,-5);
        Assert(s.HorizontalSnowM3>0&&s.HorizontalSnowM3<s.Config.ConditionerSnowCapacityM3,"trough did not retain the first finite accumulation");
        Near(s.SpilledSnowM3,0,0,"cosmetic snow appeared before the trough filled");
        Line(a,0,-4.9f,15);
        Assert(s.ConditionerLeftEscapeRate>0&&s.ConditionerRightEscapeRate>0,"both physical side exits are not shedding");
        Near(s.ConditionerLeftEscapeRate,s.ConditionerRightEscapeRate,.0000001,"symmetric source split differs");
        Near(s.HorizontalSnowM3,s.Config.ConditionerSnowCapacityM3,.0000001,"conditioner capacity exceeded");
        Near(s.SnowTankM3,0,0,"stopped conveyors collected snow into the tank");
        double before=Scan(s).Snow,escaped=s.SpilledSnowM3,left=0,right=0;
        for(int i=0;i<s.Ice.Cells.Length;i++)
        {
            float x=s.Ice.CellX(i%s.Ice.Columns),z=s.Ice.CellZ(i/s.Ice.Columns);
            if(z<-5||z>15||Math.Abs(x)<1.02f||Math.Abs(x)>1.5f)continue;
            double volume=s.Ice.Cells[i].SnowMm*s.Ice.CellSize*s.Ice.CellSize*.001;
            if(x<0)left+=volume;else right+=volume;
        }
        Assert(left>escaped*.30&&right>escaped*.30,"side windrows lack persistent material");
        Near(Stock(s),initial+s.TotalExpandedShavingsM3,.00002,"side escape lost source material");
        s.Controls.BladeDepthMm=0;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;
        // Real repeated pickups cross both old windrows; no snow-cell or tank writes.
        for(int pass=0;pass<8;pass++){Line(a,-1.265f,-15,15);Line(a,1.265f,-15,15);}a.Drain();
        double recovered=before-Scan(s).Snow;
        Assert(recovered>escaped*.85&&s.SnowTankM3>escaped,"engaged conveyors did not recover side deposits");
        Near(Stock(s),initial+s.TotalExpandedShavingsM3,.00003,"spill recollection counted material twice");
        double tank=s.SnowTankM3;Line(a,-1.265f,-15,15);Line(a,1.265f,-15,15);a.Drain();
        Near(s.SnowTankM3,tank,.00001,"revisiting an empty windrow collects the same snow twice");
        Console.WriteLine("  side escape/recovery: escaped="+escaped.ToString("F6")+" m3; left/right persistent bands="+left.ToString("F6")+"/"+right.ToString("F6")+" m3; actual recovered surface="+recovered.ToString("F6")+" m3; mass residual="+(Stock(s)-initial-s.TotalExpandedShavingsM3).ToString("F8")+" m3.");
    }
    static void FailureStates()
    {
        var s=new Simulation(new SimulatorConfig{ConditionerSnowCapacityM3=.005f,VerticalSnowCapacityM3=.003f});var a=new Apparatus(s);
        s.Controls.BladeDepthMm=.8f;s.Controls.HorizontalAuger=true;Line(a,0,-5,5);
        Assert(s.VerticalSnowM3>.00299f&&s.SnowTankM3==0,"stopped vertical did not retain the throat stock");
        Assert((s.SnowCondition&SnowTransportCondition.VerticalStopped)!=0&&(s.SnowCondition&SnowTransportCondition.Congested)!=0,"vertical stop/backpressure state missing");
        Assert(s.ConditionerLeftEscapeRate>0&&s.VerticalSpillRate==0&&!s.ConveyorJammed,"stopped vertical emitted at the upper chute or pretended to be a jam");
        s=new Simulation(new SimulatorConfig{SnowCapacityM3=.006f,ConditionerSnowCapacityM3=.003f,VerticalSnowCapacityM3=.002f});a=new Apparatus(s);
        s.Controls.BladeDepthMm=.8f;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;bool upper=false;
        for(int i=0;i<250;i++){a.Sample(0,-10+i*.1f,.1f);upper|=s.VerticalSpillRate>0;}
        Assert(upper&&s.ConveyorJammed&&(s.SnowCondition&SnowTransportCondition.TankFull)!=0,"full bin did not reject upper-chute snow then jam");
        Assert(s.ConditionerLeftEscapeRate>0&&s.VerticalSpillRate==0,"latched jam did not shift incoming snow to side escapes");
        s=new Simulation();a=new Apparatus(s);s.Controls.BladeDepthMm=.8f;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;
        typeof(Simulation).GetProperty("TankLift").SetValue(s,.45f); // declared pose fixture, no material injected
        Line(a,0,-8,8);
        Assert(s.VerticalSpillRate>0&&s.SnowTankM3==0&&!s.ConveyorJammed&&s.ConditionerLeftEscapeRate==0,"raised open chute was treated as a full/blocked bin");
        Assert((s.SnowCondition&SnowTransportCondition.RaisedBin)!=0,"raised-bin status absent");
        Near(s.UpperSnowOriginX-s.Vehicle.X,s.Config.ConveyorOutletX,.000001,"upper snow does not originate at the fixed downspout X");
        Near(s.UpperSnowOriginZ-s.Vehicle.Z,s.Config.ConveyorOutletZ,.000001,"upper snow does not originate at the fixed downspout Z");
        Near(s.UpperSnowOriginY,s.Config.ConveyorOutletY,.000001,"upper snow origin height differs from the authored downspout");
        Console.WriteLine("  failure states: horizontal OFF retains trough then both sides; vertical OFF fills throat then both sides; full tank rejects upper chute then jams; jam stops upper transport; raised bin misses upper chute without a jam.");
    }
    static void BoardRetention()
    {
        var s=new Simulation(new SimulatorConfig{ConditionerSnowCapacityM3=.001f});var a=new Apparatus(s);s.Controls.BladeDepthMm=.8f;
        double initial=Stock(s);Line(a,-11.90f,-5,5);
        Assert(s.ConditionerLeftEscapeRate>0,"near-board side source missing");
        Assert(s.Ice.IsInside(s.ConditionerLeftLandingX,s.ConditionerLeftLandingZ,.16f),"snow landing crossed a solid board");
        Near(Ground(s),0,0,"solid board leaked snow outside the rink");Near(Stock(s),initial+s.TotalExpandedShavingsM3,.00002,"board-constrained escape lost volume");
    }
    static void SnowKernel()
    {
        var s=new Simulation();int i=s.Ice.IndexAt(.225f,.225f);var before=s.Ice.Cells[i];
        s.Ice.DepositSnow(.025f,.025f,.001f,.21f);var after=s.Ice.Cells[i];
        Near(after.Quality,before.Quality,0,"zero-weight snow kernel modified an untouched cell");Near(after.SnowMm,before.SnowMm,0,"snow escaped its kernel");
        for(int n=0;n<s.Ice.DirtyCellCount;n++)Assert(s.Ice.DirtyCellIndex(n)!=i,"zero-weight snow kernel dirtied an untouched renderer cell");
    }
}
