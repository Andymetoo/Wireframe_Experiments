using System;
using System.IO;
using System.Text;
using IceResurfacer.Core;

internal static class SurfaceConsistencyValidation
{
    static void Check(bool value,string message){if(!value)throw new Exception("Surface consistency: "+message);}
    static void Tick(Simulation s,float seconds){for(int i=0;i<(int)Math.Round(seconds/.02f);i++)s.Step(.02f);}
    static void Place(Simulation s,float x,float z)
    {
        s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=s.Vehicle.Speed=s.Vehicle.SteeringAngle=0;
        s.Conditioner.X=x;s.Conditioner.Z=z-s.Config.HitchBehindAxle-s.Config.DrawbarLength;s.Conditioner.Heading=0;s.Conditioner.Lift=0;
        s.Controls.ConditionerTarget=0;s.Controls.HydraulicHoldControls=false;s.Controls.Brake=true;
    }
    public static void Run()
    {
        var report=new StringBuilder("AURORA / surface consistency regression\n");
        var cfg=new SimulatorConfig();var initial=new IceField(cfg,314);
        int span=IceOverview.CellsPerPixel(initial),blocks=0,snowBlocks=0,strictSnow=0;
        foreach(var cell in initial.Cells)if(cell.IsIce&&IceCoverage.Classify(cell,cfg)==IceTreatmentState.SnowCovered)strictSnow++;
        for(int row=0;row<initial.Rows;row+=span)for(int col=0;col<initial.Columns;col+=span)
        {
            var sample=IceOverview.Summarize(initial,cfg,col,row,span);if(sample.IceCells==0)continue;
            blocks++;if(sample.SnowDepthMm>.55f)snowBlocks++;
        }
        Check(strictSnow>10000&&snowBlocks==0,"initial thin dust is conserved but cannot turn the overview into white static");
        report.AppendLine("Initial strict snow cells="+strictSnow+"; heavy-snow overview blocks="+snowBlocks+"/"+blocks+". Initial .12-.26mm dust plus .17mm sensitive-zone increment is retained.");
        int center=initial.IndexAt(0,0),left=center%initial.Columns/span*span,bottom=center/initial.Columns/span*span;
        for(int row=bottom;row<bottom+span;row++)for(int col=left;col<left+span;col++)
        {
            int i=initial.Index(col,row);var c=initial.Cells[i];c.FloodMm=c.LiquidMm=.3f;c.SnowMm=0;
            c.CutDepthMm=col==left?0:.05f;initial.UpdateQuality(ref c);initial.Cells[i]=c;
        }
        var seam=IceOverview.Summarize(initial,cfg,left,bottom,span);
        Check(Math.Abs(seam.WetUncut-.25f)<.00001f&&Math.Abs(seam.WetCut-.75f)<.00001f&&seam.WaterDeficitFraction==0,
            "one 5cm uncut seam retains its exact quarter-area blue contribution without inventing a dry gap");
        Check(initial.Cells[initial.Index(left,bottom)].Covered==false,"water-only seam never receives cutting credit");
        initial.DepositSnow(.05f,.05f,.002f,.15f);
        var drift=IceOverview.Summarize(initial,cfg,left,bottom,span);
        Check(drift.SnowDepthMm>2,"a concentrated true deposit remains conspicuous after area aggregation");
        report.AppendLine("PASS one-cell 5cm wet/uncut seam=25% of its20cm block; watercoverage100%, cutting75%; actual2L snow deposit remains visible.");

        double previousArea=0,previousMass=0;
        foreach(int seconds in new[]{5,15,30})
        {
            var s=new Simulation(practice:true);Check(s.ConfigureWaterForServiceTest(1,.5f),"pickup reservoir has space at fixture start");
            Place(s,0,0);s.Controls.FloodFlow=1;Tick(s,seconds);
            Measure(s,out double mass,out double liquid,out double frozen,out double area,out float peak);
            double released=s.TotalFloodUsedLiters;
            Check(Math.Abs(mass-released)<.035,"stationary released water equals all retained liquid+frozen at "+seconds+"s");
            Check(Math.Abs(liquid+frozen-mass)<.00001,"phase change conserves mass");
            Check(area>previousArea&&mass>previousMass,"longer stationary flood expands the physical patch");
            Check(s.Ice.RedistributedWaterLiters>0&&s.GetReport().OverwaterFraction>0,"finite redistribution and existing penalty recognize pooling");
            Check(s.Ice.LastSpreadCells<=32768,"redistribution respects bounded work budget");
            previousArea=area;previousMass=mass;
            report.AppendLine(seconds+"s: released="+released.ToString("F4")+"L, retained="+mass.ToString("F4")+"L, liquid="+liquid.ToString("F4")+"L, frozen="+frozen.ToString("F4")+"L, >.08mm area="+area.ToString("F4")+"m2, peak="+peak.ToString("F3")+"mm, gross lateral transfer="+s.Ice.RedistributedWaterLiters.ToString("F3")+"L.");
            if(seconds==30)
            {
                s.Controls.FloodFlow=0;
                s.Controls.WashValve=.2f;Tick(s,11);s.Controls.WashValve=0;s.Controls.WashPump=true;
                s.Controls.Brake=false;s.Controls.Throttle=-.25f;Tick(s,8);
                Check(s.TotalRecoveredSurfaceLiters>.1,"primed pickup recovers pooled still-liquid water");
                report.AppendLine("30s pool recovery through actual squeegee="+s.TotalRecoveredSurfaceLiters.ToString("F4")+"L.");
            }
        }
        var edge=new IceField(cfg,314);edge.DepositWater(12.85f,0,20,0,.2f);
        for(int i=1;i<=300;i++)edge.RedistributeWater(i*.1f,.1f);
        double edgeMass=0;foreach(var c in edge.Cells){edgeMass+=c.FloodMm*edge.CellSize*edge.CellSize;Check(c.IsIce||c.FloodMm==0,"boards retain liquid inside actual ice cells");}
        Check(Math.Abs(edgeMass-20)<.01,"board-adjacent spread conserves all20L");
        var phase=new IceField(cfg,314);int sampleIndex=phase.IndexAt(0,0);var wet=phase.Cells[sampleIndex];phase.AddWater(ref wet,.3f,0);phase.Cells[sampleIndex]=wet;
        float last=2;
        foreach(int age in new[]{0,300,600,1200,1800})
        {
            float liquid=phase.LiquidDepth(wet,age),optical=(float)Math.Pow(phase.Wetness(sampleIndex,age),.70);
            Check(optical<last,"actual-liquid optical response progressively settles");last=optical;
            report.AppendLine("Age "+age+"s: liquid="+liquid.ToString("F5")+"mm frozen="+phase.FrozenDepth(wet,age).ToString("F5")+"mm opticalwet="+optical.ToString("F4")+".");
        }
        Check(last<.06f,"mostly frozen film has substantially reduced wet sheen after30min");
        report.AppendLine("PASS finite spreading, board retention, pickup, aggregated map and physical phase progression. No scoring thresholds weakened.");
        File.WriteAllText("Assets/Experiments/IceResurfacer/Validation~/Surface-consistency-results.txt",report.ToString());
        Console.WriteLine(report);
    }
    internal static void Measure(Simulation s,out double mass,out double liquid,out double frozen,out double area,out float peak)
    {
        mass=liquid=frozen=area=0;peak=0;double cellArea=s.Ice.CellSize*s.Ice.CellSize;
        foreach(var c in s.Ice.Cells)
        {
            if(!c.IsIce)continue;float l=s.Ice.LiquidDepth(c,s.Time);
            mass+=c.FloodMm*cellArea;liquid+=l*cellArea;frozen+=(c.FloodMm-l)*cellArea;
            if(c.FloodMm>.08f)area+=cellArea;peak=Math.Max(peak,l);
        }
    }
}
