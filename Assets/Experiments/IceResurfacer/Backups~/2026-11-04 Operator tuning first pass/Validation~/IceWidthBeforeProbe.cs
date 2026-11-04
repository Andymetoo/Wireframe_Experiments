using System;
using IceResurfacer.Core;
// The captured before copy includes only two added validation call sites;
// their methods did not yet exist. Stubs restore the unchanged old model.
namespace IceResurfacer.Core
{
    public sealed partial class SimulatorConfig
    { void ValidateIceCalibration(){} void ValidateFreedomCalibration(){} }
}
internal static class IceWidthBeforeProbe
{
    static void Place(Simulation s,float x)
    {s.Vehicle.X=x;s.Vehicle.Z=-12;s.Vehicle.Heading=s.Vehicle.Speed=0;s.Conditioner.X=x;s.Conditioner.Z=-13.1f;s.Conditioner.Heading=0;}
    static void Pass(Simulation s)
    {s.Controls.ConditionerTarget=0;s.Conditioner.Lift=0;s.Controls.BladeDepthMm=.35f;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;s.Controls.FloodFlow=.43f;s.Controls.Throttle=.5f;for(int i=0;i<600;i++)s.Step(.02f);}
    static int Main()
    {
        try
        {
            var s=new Simulation(new SimulatorConfig {ForwardSpeed=3f});Place(s,0);Pass(s);
            int row=s.Ice.IndexAt(0,-5)/s.Ice.Columns;float edge=0,min=0;int wet=0;
            for(int col=0;col<s.Ice.Columns;col++)if(s.Ice.Cells[s.Ice.Index(col,row)].FloodMm>.08f)
            {float x=s.Ice.CellX(col);edge=Math.Max(edge,x+.05f);min=Math.Min(min,x-.05f);wet++;}
            float center=edge+.835f;Place(s,center);Pass(s);int dry=0,unscored=0;
            for(int col=0;col<s.Ice.Columns;col++)
            {float x=s.Ice.CellX(col);if(x<-.5f||x>center+.5f)continue;var c=s.Ice.Cells[s.Ice.Index(col,row)];if(c.FloodMm<.08f)dry++;if(!c.Covered)unscored++;}
            Console.WriteLine("Before model: track1.370m+tire.300m => exterior.835m; strong wet width="+(edge-min).ToString("F3")+"m; aligned second center="+center.ToString("F3")+"m; dry/scored cell gaps="+dry+"/"+unscored+". No one-foot physical straight-pass gap reproduced; perceived gap requires operator camera/turn alignment playtesting.");
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
