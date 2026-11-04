using System;
using System.IO;
using System.Text;
using IceResurfacer.Core;

internal static class MinimapValidation
{
    static void Check(bool value,string message)
    { if(!value)throw new Exception("Minimap: "+message); }

    public static void Run()
    {
        var config=new SimulatorConfig();var field=new IceField(config,314);
        var cell=new IceCell {IsIce=true,Shavable=true,SnowMm=.1f,GougeDepthMm=.07f};
        Check(IceCoverage.Classify(cell,config)==IceTreatmentState.Uncut,"untreated patch must remain uncut");
        cell.CutDepthMm=IceCoverage.RequiredCutMm(config)+.000001f;field.UpdateQuality(ref cell);
        Check(IceCoverage.Classify(cell,config)==IceTreatmentState.CutButDry,"dry cut must not appear fully resurfaced");
        cell.CutDepthMm=IceCoverage.RequiredCutMm(config);cell.FloodMm=.1f;field.UpdateQuality(ref cell);
        Check(IceCoverage.Classify(cell,config)==IceTreatmentState.WetButUncut&&!cell.Covered,"strict cut boundary must remain incomplete despite wet shine");
        cell.CutDepthMm+=.000001f;cell.FloodMm=.08f;field.UpdateQuality(ref cell);
        Check(IceCoverage.Classify(cell,config)==IceTreatmentState.CutButDry&&!cell.Covered,"strict flood boundary must remain incomplete");
        cell.FloodMm=.12f;field.UpdateQuality(ref cell);
        Check(IceCoverage.Classify(cell,config)==IceTreatmentState.Resurfaced&&cell.Covered,"combined treatment uses unchanged coverage");
        Check(cell.GougeDepthMm>.06f,"coverage classification must not erase remaining physical grooves");
        cell.SnowMm=.25f;field.UpdateQuality(ref cell);
        Check(IceCoverage.Classify(cell,config)==IceTreatmentState.SnowCovered&&!cell.Covered,"loose snow at threshold blocks combined coverage");
        cell.SnowMm=.1f;cell.OverwaterMm=.051f;field.UpdateQuality(ref cell);
        Check(IceCoverage.Classify(cell,config)==IceTreatmentState.Overwatered&&cell.Covered,"excess water is separate from combined-coverage credit");
        cell.OverwaterMm=0;cell.Shavable=false;
        Check(IceCoverage.Classify(cell,config)==IceTreatmentState.ShavingExcluded,"inaccessible shaving rim needs an explicit explanation");

        var separated=new Simulation();Lane(separated,0);Lane(separated,2.05f);
        var overlapping=IceCalibrationValidation.BuildTwoPassFixture(out float alignedCenter,out _);
        int sliver=CountInteriorDeficits(separated,2.05f,out int wetUncut,out int dry,out int snowy);
        int noGap=CountInteriorDeficits(overlapping,alignedCenter,out int overlapWet,out int overlapDry,out int overlapSnow);
        Check(sliver>0&&wetUncut==sliver&&dry==0,"measured narrow seam must be wet but actually uncut");
        Check(noGap==0,"tire-aligned overlap must retain every existing combined-treatment cell");
        var report=new StringBuilder();
        report.AppendLine("AURORA / truthful minimap classification and sliver diagnosis");
        report.AppendLine("PASS strict cut/flood/snow boundaries; separate overwater; excluded rim; residual groove preservation.");
        report.AppendLine("Two actual forward working passes, 2.05m between centers: "+sliver+" incomplete cells in the inspected interior strip; all "+wetUncut+" are wet but uncut; "+snowy+" also exceed the loose-snow threshold; "+dry+" are dry. Deficits may overlap.");
        report.AppendLine("Tire-aligned fixture at "+alignedCenter.ToString("F4")+"m centers: "+noGap+" incomplete interior cells.");
        report.AppendLine("The water band is wider than the blade. A physically uncut seam can therefore look continuously wet in the world; wet sheen does not grant cutting credit.");
        report.AppendLine("A 5cm cell occupies about0.225px in the 1366 overview and0.316px at1920. Point-sampled categorical overview can omit or exaggerate a subpixel seam; hover magnification displays the actual cells without adding treatment.");
        report.AppendLine("Normal map colors now explain treatment state. Finish quality remains separately reported; F2 mask and cut/wash/flood views are preserved. No scoring threshold, state value or missing cell is changed by classification.");
        report.AppendLine("Fixtures set straight start poses for isolated geometry, then drive real simulation ticks; they are not the complete control-driven route.");
        string path=Path.Combine("Assets","Experiments","IceResurfacer","Validation~","Minimap-results.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,report.ToString());
        Console.WriteLine("  minimap seam: "+wetUncut+" truly wet/uncut cells versus "+noGap+" with tire-aligned overlap; original treatment thresholds retained");
    }

    static void Lane(Simulation s,float x)
    {
        s.Vehicle.X=x;s.Vehicle.Z=-16;s.Vehicle.Heading=s.Vehicle.Speed=s.Vehicle.SteeringAngle=0;
        s.Conditioner.X=x;s.Conditioner.Z=-16-s.Config.HitchBehindAxle-s.Config.DrawbarLength;s.Conditioner.Heading=0;s.Conditioner.Lift=0;
        var c=s.Controls;c.HydraulicHoldControls=false;c.ConditionerTarget=0;c.Brake=false;c.Throttle=.5f;c.Steer=0;
        c.BladeDepthMm=.35f;c.HorizontalAuger=c.VerticalAuger=true;c.WashValve=0;c.FloodFlow=.43f;
        for(int i=0;i<700;i++)s.Step(.02f);
        c.FloodFlow=c.Throttle=0;c.Brake=true;
    }
    static int CountInteriorDeficits(Simulation s,float second,out int wetUncut,out int dry,out int snowy)
    {
        wetUncut=dry=snowy=0;int count=0;var f=s.Ice;
        for(int row=0;row<f.Rows;row++)
        {
            float z=f.CellZ(row);if(z< -11||z> -7)continue;
            for(int col=0;col<f.Columns;col++)
            {
                float x=f.CellX(col);if(x<.3f||x>second-.3f)continue;
                var cell=f.Cells[f.Index(col,row)];if(cell.Covered)continue;
                count++;
                if(cell.CutDepthMm<=IceCoverage.RequiredCutMm(s.Config)&&cell.FloodMm>IceCoverage.RequiredFloodMm)wetUncut++;
                if(cell.FloodMm<=IceCoverage.RequiredFloodMm)dry++;
                if(cell.SnowMm>=IceCoverage.MaximumLooseSnowMm)snowy++;
            }
        }
        return count;
    }
}
