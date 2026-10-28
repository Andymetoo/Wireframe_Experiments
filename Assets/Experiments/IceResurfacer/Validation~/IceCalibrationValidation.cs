using System;
using System.Diagnostics;
using IceResurfacer.Core;

internal static class IceCalibrationValidation
{
    static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    static void Near(double a,double b,double tolerance,string message){Assert(Math.Abs(a-b)<=tolerance,message+" ("+a+" vs "+b+")");}
    static void Tick(Simulation s,float seconds){for(int i=0;i<(int)Math.Round(seconds/.02f);i++)s.Step(.02f);}
    static void Place(Simulation s,float x,float z,float heading=0)
    {
        s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=heading;s.Vehicle.Speed=0;s.Vehicle.SteeringAngle=0;
        s.Conditioner.Heading=heading;s.Conditioner.X=x-(float)Math.Sin(heading)*(s.Config.HitchBehindAxle+s.Config.DrawbarLength);
        s.Conditioner.Z=z-(float)Math.Cos(heading)*(s.Config.HitchBehindAxle+s.Config.DrawbarLength);
        s.Controls.Brake=false;s.Controls.Throttle=s.Controls.Steer=0;
    }
    static void Operate(Simulation s)
    {
        s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;s.Controls.BladeDepthMm=.35f;
        s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;s.Controls.FloodFlow=.43f;s.Controls.Throttle=.5f;
    }
    public static void Run()
    {
        TwoPassOverlap();Perimeter();Recovery();FreshFloodIsolation();PortalPickupBudget();WaterKernelFootprint();FrozenWater();Brush();BrushFloorCache();ReverseConservation();Untouched();Performance();
        Console.WriteLine("  Ice calibration PASS: tire-aligned contiguous passes, physical fringe, shaving mask, real liquid pickup, frozen balance, brush contact");
    }

    public static Simulation BuildTwoPassFixture(out float secondCenter,out float firstWetEdge)
    {
        var s=new Simulation();Place(s,0,-12);Operate(s);Tick(s,12);
        Band(s,-5,.08f,false,out _,out firstWetEdge);
        float tireOuter=(s.Config.WheelTrack+s.Config.TireWidth)*.5f;
        secondCenter=firstWetEdge+tireOuter;
        Place(s,secondCenter,-12);Operate(s);Tick(s,12);s.Controls.Throttle=s.Controls.FloodFlow=0;
        return s;
    }

    static void Band(Simulation s,float z,float threshold,bool cut,out float min,out float max)
    {
        int row=s.Ice.IndexAt(0,z)/s.Ice.Columns;min=float.MaxValue;max=float.MinValue;
        for(int col=0;col<s.Ice.Columns;col++)
        {
            var cell=s.Ice.Cells[s.Ice.Index(col,row)];float value=cut?cell.CutDepthMm:cell.FloodMm;
            if(value<=threshold)continue;
            min=Math.Min(min,s.Ice.CellX(col)-s.Ice.CellSize*.5f);max=Math.Max(max,s.Ice.CellX(col)+s.Ice.CellSize*.5f);
        }
    }

    static void TwoPassOverlap()
    {
        var s=new Simulation();Place(s,0,-12);Operate(s);Tick(s,12);
        Band(s,-5,.08f,false,out float firstMin,out float firstEdge);
        Band(s,-5,.001f,false,out float filmMin,out float filmMax);
        Band(s,-5,.005f,true,out float cutMin,out float cutMax);
        float tireOuter=(s.Config.WheelTrack+s.Config.TireWidth)*.5f;
        float secondCenter=firstEdge+tireOuter;
        float theoreticalWetOverlap=s.Config.WaterDistributionWidth*.5f-tireOuter;
        float actualMeasuredOverlap=s.Config.WaterDistributionWidth-secondCenter;
        Near(tireOuter,.835f,.0001,"tire exterior geometry changed");
        Assert(theoreticalWetOverlap>.2f,"tire cue cannot create intended physical overlap");
        Assert(actualMeasuredOverlap>0,"measured strong-boundary alignment leaves no nominal towel overlap");
        Place(s,secondCenter,-12);Operate(s);Tick(s,12);
        int row=s.Ice.IndexAt(0,-5)/s.Ice.Columns,gap=0,unscored=0;
        for(int col=0;col<s.Ice.Columns;col++)
        {
            float x=s.Ice.CellX(col);if(x<-.5f||x>secondCenter+.5f)continue;
            var c=s.Ice.Cells[s.Ice.Index(col,row)];if(c.FloodMm<.08f)gap++;
            if(!c.Covered)unscored++;
        }
        Assert(gap==0,"outside-tire aligned pass left a dry cell strip");
        Assert(unscored==0,"contiguous tire-aligned center bands lack scored coverage");
        Near(secondCenter-tireOuter,firstEdge,.00001,"second tire not aligned to first wet/dry edge");
        Console.WriteLine("  overlap: tire exterior="+tireOuter.ToString("F3")+"m; blade edges="+cutMin.ToString("F3")+".."+cutMax.ToString("F3")+
            "m; strong wet band="+(firstEdge-firstMin).ToString("F3")+"m; all-liquid band="+(filmMax-filmMin).ToString("F3")+
            "m; second center="+secondCenter.ToString("F3")+"m; measured-boundary nominal towel overlap="+actualMeasuredOverlap.ToString("F3")+
            "m; ideal continuous towel-edge alignment overlap="+theoreticalWetOverlap.ToString("F3")+"m; dry/scored cell gaps="+gap+"/"+unscored+
            "; coverage="+(s.GetReport().Coverage*100).ToString("F2")+"%");
    }

    static void Perimeter()
    {
        var s=new Simulation();float x=-s.Config.RinkWidth*.5f+s.Config.VehicleHalfWidth+.065f;
        Place(s,x,-10);Operate(s);Tick(s,9);
        Band(s,-5,.01f,false,out float edge,out _);
        float gap=edge+s.Config.RinkWidth*.5f;
        Assert(gap<=.101f,"finite lateral film did not reach narrow board margin: "+gap+"m, vehicle="+s.Vehicle.X+","+s.Vehicle.Z);
        int excluded=0;
        for(int i=0;i<s.Ice.Cells.Length;i++)
        {
            var c=s.Ice.Cells[i];
            if(!c.IsIce)Near(c.FloodMm,0,0,"water crossed the boards");
            if(c.IsIce&&!c.Shavable)excluded++;
            if(c.Shavable)Assert(s.Ice.IsInside(s.Ice.CellX(i%s.Ice.Columns),s.Ice.CellZ(i/s.Ice.Columns),.1524f),"shaving mask includes unreachable outer cell");
        }
        Assert(excluded>0&&s.Ice.ShavableCellCount<s.Ice.IceCellCount,"six-inch scoring mask missing");
        var report=s.GetReport();Assert(report.WaterCoverage>0&&report.ShavingCoverage>0,"separate water/cut measures missing");
        Console.WriteLine("  perimeter: physical wet gap="+gap.ToString("F3")+"m; shavable="+s.Ice.ShavableCellCount+"/"+s.Ice.IceCellCount+
            " cells; excluded="+excluded+"; no non-ice cells wetted");
    }

    static double WaterMass(Simulation s)
    {
        double liters=0;foreach(var c in s.Ice.Cells)liters+=c.FloodMm*s.Ice.CellSize*s.Ice.CellSize;
        return liters;
    }
    static void Recovery()
    {
        var s=new Simulation(practice:true);Assert(s.ConfigureWaterForServiceTest(1,.5f),"partial wash fixture rejected");
        Place(s,0,0);s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;
        s.Controls.FloodFlow=1;s.Controls.WashValve=.2f;Tick(s,11);
        s.Controls.FloodFlow=0;s.Controls.WashValve=0;
        double before=WaterMass(s),waterTank=s.WashWaterLiters;float overBefore=0;
        foreach(var c in s.Ice.Cells)overBefore+=c.OverwaterMm;
        s.Controls.WashPump=true;s.Controls.Throttle=-.2f;Tick(s,5);
        double recovered=s.TotalRecoveredSurfaceLiters,after=WaterMass(s);
        float overAfter=0;foreach(var c in s.Ice.Cells)overAfter+=c.OverwaterMm;
        Assert(recovered>.3,"pickup did not collect existing standing water");
        Near(before-after,recovered,.003,"surface removal differs from metered recovery");
        Near(s.WashWaterLiters-waterTank,recovered,.003,"recovered water not returned to wash tank");
        Assert(recovered<=s.Config.WashPickupLitersPerSecond*5+.001,"pickup exceeded pump capacity");
        Assert(overAfter<overBefore,"actual liquid removal did not improve overwater penalty");
        float frozenBefore=0;foreach(var c in s.Ice.Cells)frozenBefore+=s.Ice.FrozenDepth(c,s.Time);
        Tick(s,3);float frozenAfter=0;foreach(var c in s.Ice.Cells)frozenAfter+=s.Ice.FrozenDepth(c,s.Time);
        Assert(frozenAfter>=frozenBefore-.001,"wash pickup removed frozen ice");
        Console.WriteLine("  pickup: "+recovered.ToString("F3")+"L recovered in 5s; ice loss and wash gain agree; overwater summed depth "+overBefore.ToString("F2")+" -> "+overAfter.ToString("F2")+"mm");
    }

    static void FrozenWater()
    {
        var config=new SimulatorConfig {FreezeSeconds=2};var s=new Simulation(config);
        int index=s.Ice.IndexAt(0,0);var cell=s.Ice.Cells[index];
        s.Ice.AddWater(ref cell,2,0);float total=cell.FloodMm;
        float removed=s.Ice.RemoveLiquid(ref cell,10,24);
        Near(cell.FloodMm,total,0,"already-frozen film was erased");
        Near(removed,0,0,"frozen film entered wash tank");
        Near(cell.FrozenFloodMm,2,.00002,"freeze did not conserve retained water");
        Assert(cell.OverwaterMm>1&&cell.FrozenOverwaterMm>1,"frozen overwater defect silently disappeared");
    }

    static double TotalWater(Simulation s)
    {
        double sum=s.FloodWaterLiters+s.WashWaterLiters+WaterMass(s);
        foreach(var cell in s.Environment.Cells)sum+=cell.WaterLiters;
        return sum;
    }
    static void PortalPickupBudget()
    {
        var s=new Simulation(new SimulatorConfig{WashCapacityLiters=10});
        Place(s,0,-12);s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;s.Controls.WashValve=.2f;Tick(s,30);
        s.Controls.WashValve=0;
        s.Layout.ToWorld(0,-1.6f,out float x,out float z);Place(s,x,z,s.Layout.InwardHeading);
        // Explicit near-capacity fixture: enough liquid to operate the pickup,
        // with the towel partly over the portal but its outlet still on ice.
        typeof(Simulation).GetProperty("WashWaterLiters").SetValue(s,s.Config.WashCapacityLiters-.0001f);
        for(int i=0;i<s.Ice.Cells.Length;i++)
        {
            var c=s.Ice.Cells[i];if(!c.IsIce)continue;
            float dx=s.Ice.CellX(i%s.Ice.Columns)-s.Conditioner.X,dz=s.Ice.CellZ(i/s.Ice.Columns)-s.Conditioner.Z;
            if(dx*dx+dz*dz>3.24f)continue;
            s.Ice.AddWater(ref c,.036f,s.Time);s.Ice.Cells[i]=c;s.Ice.MarkCellChanged(i);
        }
        s.Ice.MarkChanged();s.Controls.FloodFlow=1;s.Controls.WashPump=true;
        float beforeTank=s.WashWaterLiters,room=s.Config.WashCapacityLiters-beforeTank;
        double beforeRecovery=s.TotalRecoveredSurfaceLiters,total=TotalWater(s);
        s.Step(.02f);double recovered=s.TotalRecoveredSurfaceLiters-beforeRecovery;
        Assert(s.CurrentRunoffLitersPerSecond>0,"portal fixture did not exercise mixed ice/floor runoff");
        Assert(recovered>0&&recovered<=room+.0000002,"portal runoff inflated the fixed tank-capacity pickup budget");
        Assert(recovered<=s.Config.WashPickupLitersPerSecond*.02f*s.Config.WashRecoveryFraction+.0000002,"portal runoff exceeded pump capacity");
        Near(s.WashWaterLiters-beforeTank,recovered,.0000015,"near-full wash tank discarded recovered surface water");
        Near(TotalWater(s),total,.0001,"mixed portal source/pickup lost water across tanks, ice and floor");
        Console.WriteLine("  portal pickup: "+recovered.ToString("F7")+"L within "+room.ToString("F7")+"L tank room; runoff remains conserved");
    }
    static void WaterKernelFootprint()
    {
        var s=new Simulation();var before=(IceCell[])s.Ice.Cells.Clone();s.Ice.DepositWater(0,0,.1f,s.Time,.25f);
        for(int i=0;i<s.Ice.Cells.Length;i++)
        {
            float x=s.Ice.CellX(i%s.Ice.Columns),z=s.Ice.CellZ(i/s.Ice.Columns);
            if(x*x+z*z<.25f*.25f)continue;
            Near(s.Ice.Cells[i].Quality,before[i].Quality,0,"zero-weight spill kernel changed untouched ice quality");
            Near(s.Ice.Cells[i].FloodMm,0,0,"spill water escaped its physical kernel");
        }
    }
    static void BrushFloorCache()
    {
        // An isolated contact fixture exercises the same brush-to-floor branch
        // as a doorway edge without making navigation part of this unit check.
        var s=new Simulation();Place(s,14,0);s.Controls.BoardBrush=true;
        var before=s.GetReport();int revision=s.Ice.Revision;
        var brush=typeof(Simulation).GetMethod("UpdateBoardBrush",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        brush.Invoke(s,new object[]{.5f,.1f});
        double ground=0;foreach(var cell in s.Environment.Cells)ground+=cell.SnowM3;
        Assert(ground>0&&s.BrushSweptSnowM3>0,"brush cache fixture did not sweep ice snow onto floor");
        Assert(s.Ice.Revision>revision,"brush removal onto floor failed to invalidate surface totals");
        Assert(s.GetReport().FinishQuality!=before.FinishQuality,"cached report hid real brush changes to the ice");
    }

    static void FreshFloodIsolation()
    {
        var s=new Simulation();Place(s,-5,-12);s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;
        s.Controls.WashValve=.2f;Tick(s,30);s.Controls.WashValve=0;
        Place(s,0,-12);s.Controls.FloodFlow=.43f;s.Controls.WashPump=true;s.Controls.Throttle=.5f;Tick(s,8);
        Assert(s.WashPrimeSeconds>=10,"fresh flood test lost pump prime before checking contact geometry");
        Near(s.TotalRecoveredSurfaceLiters,0,.0001,"pickup recaptured fresh water behind its own squeegee on a forward pass");
        Assert(s.TotalFloodUsedLiters>5,"fresh flood isolation did not exercise water delivery");
    }

    static void Brush()
    {
        var s=new Simulation();Place(s,-s.Config.RinkWidth*.5f+s.Config.VehicleHalfWidth+.065f,-8);
        s.Controls.BoardBrush=true;s.Controls.Throttle=.35f;Tick(s,5);
        Assert(s.BrushDeployment>.99f&&s.BrushContact,"left brush failed to deploy into actual board contact");
        float board=s.Ice.BoardDistance(s.BrushWorldX,s.BrushWorldZ,out _,out _);
        Assert(board<=-s.Config.BrushRadius+.002f,"brush disc penetrates kickplate");
        Assert(s.BrushSweptSnowM3>0,"contact brush swept no spatial snow");
        float before=s.BrushSweptSnowM3;s.Controls.BoardBrush=false;Tick(s,2);
        Assert(s.BrushDeployment<.001f&&!s.BrushContact,"brush did not retract");
        Near(s.BrushSweptSnowM3,before,0,"retracted brush still swept snow");
    }

    static void Performance()
    {
        var s=new Simulation();Place(s,0,-10);Operate(s);Tick(s,2);
        var watch=Stopwatch.StartNew();long allocated=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<5000;i++)s.Step(.02f);
        long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;watch.Stop();
        Console.WriteLine("  core 5000 active ticks: "+(watch.Elapsed.TotalMilliseconds/5000).ToString("F4")+"ms/tick, "+bytes+" managed bytes (CPU only; no Unity/render/UI)");
        Assert(bytes<1024,"new per-tick allocations in ice core");
    }
    static void ReverseConservation()
    {
        var s=new Simulation();Place(s,0,-5);Operate(s);s.Controls.FloodFlow=0;s.Controls.Throttle=-.3f;
        double before=0;foreach(var c in s.Ice.Cells)before+=c.SnowMm;
        Tick(s,3);double after=0,cut=0;foreach(var c in s.Ice.Cells){after+=c.SnowMm;cut+=c.CutDepthMm;}
        double generated=(before-after+cut*s.Config.SnowExpansionRatio)*s.Ice.CellSize*s.Ice.CellSize*.001;
        Near(generated,s.HorizontalSnowM3+s.VerticalSnowM3+s.SnowTankM3,.00001,"reverse cutting created unaccounted snow");
    }
    static void Untouched()
    {
        var s=new Simulation();Place(s,0,-5);var before=(IceCell[])s.Ice.Cells.Clone();s.Controls.Throttle=.3f;Tick(s,3);
        for(int i=0;i<before.Length;i++)Near(s.Ice.Cells[i].Quality,before[i].Quality,0,"raised inactive machinery altered untouched ice quality");
    }
}
