using System;using System.Reflection;using IceResurfacer.Core;
internal static class OperatorSpeedValidation
{
    const float Mph=.44704f;
    static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("  operator speed: "+text);}
    static Simulation New(float mph)
    {
        var s=new Simulation(practice:true);s.Vehicle.X=-5;s.Vehicle.Z=-24;s.Vehicle.Heading=s.Vehicle.Speed=0;s.Conditioner.X=-5;s.Conditioner.Z=-25.1f;s.Conditioner.Heading=0;s.Controls.Throttle=1;s.SetMaximumForwardSpeed(mph*Mph);return s;
    }
    static void Ticks(Simulation s,int n){for(int i=0;i<n;i++)s.Step(.02f);}
    public static void Run()
    {
        foreach(float mph in new[]{3f,2.65f/Mph,8f})
        {
            var s=New(mph);float baseline=s.Config.ForwardSpeed;Ticks(s,600);
            Check(Math.Abs(s.Vehicle.Speed-mph*Mph)<.005f,"steady full-pedal "+mph.ToString("F4")+" mph = "+s.Vehicle.Speed.ToString("F6")+" m/s");
            Check(s.Config.ForwardSpeed==baseline&&s.Config.ReverseSpeed==1,"runtime target preserves original forward/reverse calibration");
            Check(Math.Abs(s.GetReport().SelectedMaxForwardSpeedMph-mph)<.001f,"run report records selected maximum for comparison");
        }
        var moving=New(8);Ticks(moving,600);float before=moving.Vehicle.Speed;moving.SetMaximumForwardSpeed(3*Mph);
        Check(moving.Vehicle.Speed==before,"lowering a moving limit never assigns vehicle velocity");moving.Step(.02f);
        Check(moving.Vehicle.Speed<before&&before-moving.Vehicle.Speed<.05f,"overspeed decelerates smoothly through the existing hydrostatic/traction response");Ticks(moving,249);
        Check(Math.Abs(moving.Vehicle.Speed-3*Mph)<.01f,"lowered limit converges to 3 mph");before=moving.Vehicle.Speed;moving.SetMaximumForwardSpeed(8*Mph);
        Check(moving.Vehicle.Speed==before,"raising the limit preserves instantaneous velocity");Ticks(moving,100);
        Check(moving.Vehicle.Speed>before+.4f&&moving.Vehicle.Speed<8*Mph,"normal acceleration approaches a raised limit without an override");
        var unloaded=New(8);var loaded=New(8);typeof(Simulation).GetProperty("SnowTankM3").SetValue(loaded,loaded.Config.SnowCapacityM3*.9f);Ticks(unloaded,150);Ticks(loaded,150);
        Check(loaded.Vehicle.Speed<unloaded.Vehicle.Speed,"existing snow-tank mass still reduces acceleration");
        before=loaded.Vehicle.Speed;loaded.Controls.Brake=true;Ticks(loaded,100);Check(loaded.Vehicle.Speed<.01f&&before>0,"service braking retains authority over the selected target");
        var coast=New(8);Ticks(coast,250);before=coast.Vehicle.Speed;coast.Controls.Throttle=0;Ticks(coast,50);Check(coast.Vehicle.Speed<before-.25f,"hydrostatic pedal-release braking remains active");
        var snow=New(8);snow.Conditioner.Lift=0;snow.Controls.ConditionerTarget=0;snow.Controls.BladeDepthMm=.8f;snow.Controls.HorizontalAuger=snow.Controls.VerticalAuger=true;
        for(int i=0;i<snow.Ice.Cells.Length;i++){var cell=snow.Ice.Cells[i];if(cell.IsIce)cell.SnowMm=6;snow.Ice.Cells[i]=cell;}
        var light=New(8);Ticks(snow,150);Ticks(light,150);Check(snow.MachineLoad>light.MachineLoad&&snow.Vehicle.Speed<light.Vehicle.Speed,"deep-snow cutting and original machinery load still affect achieved acceleration");
        foreach(float mph in new[]{3f,8f})
        {var reverse=New(mph);reverse.Vehicle.Z=0;reverse.Conditioner.Z=-1.1f;reverse.Controls.Throttle=-1;Ticks(reverse,400);Check(Math.Abs(reverse.Vehicle.Speed+reverse.Config.ReverseSpeed)<.002f,"reverse speed remains unchanged at forward limit "+mph);}
        var baselineRun=New(2.65f/Mph);var exact=New(2.65f/Mph);exact.SetMaximumForwardSpeed(exact.Config.ForwardSpeed);
        for(int i=0;i<500;i++){baselineRun.Controls.Steer=exact.Controls.Steer=(float)Math.Sin(i*.02)*.2f;baselineRun.Step(.02f);exact.Step(.02f);}
        Check(baselineRun.Vehicle.X==exact.Vehicle.X&&baselineRun.Vehicle.Z==exact.Vehicle.Z&&baselineRun.Vehicle.Heading==exact.Vehicle.Heading,"selecting the original value preserves steering and motion exactly");
    }
}
