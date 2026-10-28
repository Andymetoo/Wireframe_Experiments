using System;
using System.Collections.Generic;
using IceResurfacer.Core;

internal static class FreedomValidation
{
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Near(double a,double b,double epsilon,string message){Assert(Math.Abs(a-b)<=epsilon,message+" ("+a+" vs "+b+")");}
    static void Tick(Simulation s,float seconds){for(int i=0;i<(int)Math.Round(seconds/.02f);i++)s.Step(.02f);}
    static void Place(Simulation s,float x,float z,float heading=0)
    {
        s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=heading;s.Vehicle.Speed=0;s.Vehicle.SteeringAngle=0;
        s.Conditioner.Heading=heading;s.Conditioner.X=x-(float)Math.Sin(heading)*1.1f;s.Conditioner.Z=z-(float)Math.Cos(heading)*1.1f;
    }
    public static void Run()
    {
        Freedom();DumpAccounting();Traction();SpatialLayout();UnconventionalSession();
        Console.WriteLine("  Free Simulation, training isolation, moving dismount, arbitrary dumps, material balance, traction and southeast geometry PASS");
    }
    static void Freedom()
    {
        var s=new Simulation();Place(s,0,0);
        s.Controls.ConditionerTarget=0;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=s.Controls.BoardBrush=true;
        s.Controls.WashValve=.3f;s.Controls.FloodFlow=.25f;s.Controls.BladeDepthMm=.4f;
        s.Controls.TankLiftTarget=.7f;s.Controls.Throttle=.5f;Tick(s,4);
        Assert(s.TankLift>.6f&&s.Vehicle.Speed>.5f&&s.Controls.HorizontalAuger&&s.Controls.VerticalAuger,"free tank/auger/travel restriction remains");
        float moving=s.Vehicle.Speed,lift=s.TankLift,conditioner=s.Conditioner.Lift;
        Assert(s.TryDismount(),"moving raised-tank on-ice dismount rejected");
        Assert(s.Controls.HorizontalAuger&&s.Controls.VerticalAuger&&s.Controls.FloodFlow>.2f&&s.Controls.BladeDepthMm>.3f,"dismount silently corrected machinery");
        s.Step(.02f);Assert(s.Vehicle.Speed>moving*.9f,"moving dismount instant-stopped vehicle");
        Near(s.Conditioner.Lift,conditioner,.001,"dismount retracted conditioner");Near(s.TankLift,lift,.01,"dismount lowered tank");
        Tick(s,5);Assert(s.TryBoard(1),"unusual state cannot board nearby");
        s.Controls.Throttle=-.5f;Tick(s,2);Assert(s.Vehicle.Speed<-.1f,"reboarding left drive stuck");
        Assert(s.TryFinish(),"free results gated by tutorial finish rules");
        var training=new Simulation(mode:SimulationMode.GuidedTraining);Place(training,0,0);
        training.Controls.TankLiftTarget=1;Tick(training,.2f);
        Near(training.TankLift,0,0,"training assistance missing");Assert(training.LastMessage.Contains("Training assistance"),"training reason unlabeled");
        training.Controls.Throttle=.5f;Tick(training,2);Assert(training.Vehicle.Speed>.2f,"rejected training target latched drive");
        Assert(!training.TryDismount()&&training.LastMessage.Contains("Training assistance"),"training dismount assistance isolation");
        var freeAfter=new Simulation();Place(freeAfter,0,0);Assert(freeAfter.TryDismount(),"training leaked to next free simulation");
        var chute=new Simulation();Place(chute,0,-15);chute.Conditioner.Lift=0;chute.Controls.ConditionerTarget=0;
        chute.Controls.HorizontalAuger=chute.Controls.VerticalAuger=true;chute.Controls.BladeDepthMm=.5f;
        chute.Controls.TankLiftTarget=.4f;chute.Controls.Throttle=.3f;Tick(chute,20);
        Assert(chute.SpilledSnowM3>.003f&&!chute.ConveyorJammed,"open raised-bin chute incorrectly became a blockage");
    }
    static Simulation Collect()
    {
        var s=new Simulation();Place(s,0,-15);s.Controls.ConditionerTarget=0;s.Conditioner.Lift=0;
        s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;s.Controls.BladeDepthMm=.8f;s.Controls.Throttle=.5f;Tick(s,12);
        s.Controls.Throttle=0;s.Controls.Brake=true;s.Controls.BladeDepthMm=0;Tick(s,2);
        s.Controls.HorizontalAuger=s.Controls.VerticalAuger=false;s.Controls.ConditionerTarget=1;Tick(s,3);
        Assert(s.SnowTankM3>.01f,"snow collection fixture failed");return s;
    }
    static double IceSnow(Simulation s){double v=0;foreach(var cell in s.Ice.Cells)if(cell.IsIce)v+=cell.SnowMm*s.Ice.CellSize*s.Ice.CellSize*.001;return v;}
    static double GroundSnow(Simulation s){double v=0;foreach(var cell in s.Environment.Cells)v+=cell.SnowM3;return v;}
    static void Shake(Simulation s)
    {
        for(int i=0;i<9&&s.SnowTankM3>.000001f;i++)
        {s.SetTankHydraulic(-1);Tick(s,.36f);s.SetTankHydraulic(1);Tick(s,.36f);s.SetTankHydraulic(0);Tick(s,.5f);}
    }
    static void DumpAccounting()
    {
        foreach(int location in new[]{0,1,2})
        {
            var s=Collect();
            if(location==0)Place(s,0,0,.38f);
            if(location==1){s.Layout.ToWorld(s.Layout.PitSide,8.15f,out float x,out float z);Place(s,x,z,s.Layout.OutwardHeading+.17f);}
            if(location==2){s.Layout.ToWorld(2,7.9f,out float x,out float z);Place(s,x,z,s.Layout.InwardHeading+.8f);}
            double ice=IceSnow(s),ground=GroundSnow(s),stock=s.SnowTankM3;
            float px=s.Vehicle.X,pz=s.Vehicle.Z,heading=s.Vehicle.Heading;
            s.Controls.TankLiftTarget=1;Tick(s,10);Shake(s);
            Near(s.Vehicle.X,px,0,"dump snapped x");Near(s.Vehicle.Z,pz,0,"dump snapped z");Near(s.Vehicle.Heading,heading,0,"dump snapped heading");
            Assert(s.SnowTankM3<.0001f&&s.TankShakeoutCount>0,"arbitrary dump residual not recoverable");
            double transferred=IceSnow(s)-ice+GroundSnow(s)-ground+s.PitSnowM3;
            Near(transferred+s.SnowTankM3,stock,.00004,"spatial dump snow not conserved");
            if(location==0)Assert(s.DumpSnowOnIceM3>.005f&&s.PitSnowM3==0,"ice dump magically entered pit");
            if(location==1)Assert(s.PitSnowM3>.005f,"angled/off-centre actual outlet failed pit");
            if(location==2)Assert(GroundSnow(s)-ground>.005f,"floor dump missing spatial deposit");
            Console.WriteLine("  dump site="+location+" heading="+heading.ToString("F2")+" discharged="+s.DumpedSnowM3.ToString("F5")+"m3 pit="+s.PitSnowM3.ToString("F5")+" ice="+s.DumpSnowOnIceM3.ToString("F5")+" ground="+(GroundSnow(s)-ground).ToString("F5"));
        }
    }
    static void Traction()
    {
        var normal=new Simulation();Place(normal,0,-10);normal.Controls.Throttle=.42f;normal.Controls.Steer=1;Tick(normal,9);
        Assert(normal.TractionSlip<.01f&&Math.Abs(normal.LateralVelocity)<.005f,"normal resurfacing unstable");
        var aggressive=new Simulation();Place(aggressive,-4,-12);aggressive.Controls.Throttle=1;Tick(aggressive,7);
        aggressive.Controls.Steer=1;float peak=0;for(int i=0;i<450;i++){aggressive.Step(.02f);peak=Math.Max(peak,aggressive.TractionSlip);}
        Assert(peak>.03f&&Math.Abs(aggressive.LateralVelocity)<.23f,"aggressive corner lacks restrained slip");
        aggressive.Controls.Throttle=0;aggressive.Controls.Steer=0;Tick(aggressive,9);Assert(aggressive.TractionSlip<.01f,"grip failed recovery");
        var brake=new Simulation();Place(brake,0,-10);brake.Vehicle.Speed=2;float z=brake.Vehicle.Z;Tick(brake,6);float coast=brake.Vehicle.Z-z;
        Place(brake,0,-10);brake.Vehicle.Speed=2;brake.Controls.Brake=true;Tick(brake,6);float stop=brake.Vehicle.Z+10;
        Assert(coast>stop*1.5f&&stop>.8f,"hydrostatic/service brake calibration invalid");
        brake.Controls.Brake=false;brake.Controls.Throttle=-1;Tick(brake,2);Assert(brake.Vehicle.Speed<-.2f,"reverse recovery failed");
        Console.WriteLine("  traction peak="+peak.ToString("P1")+" maximum lateral drift="+aggressive.Config.MaximumLateralSlip.ToString("F2")+"m/s; 2m/s coast="+coast.ToString("F2")+"m brake="+stop.ToString("F2")+"m");
    }
    static void SpatialLayout()
    {
        var s=new Simulation();Assert(s.Vehicle.X>13&&s.Vehicle.Z<-27&&s.InServiceBay,"southeast start not in room");
        Assert(!s.Navigable(0,-31),"central south invisible gate remains");
        s.Layout.ToWorld(0,0,out float gx,out float gz);Assert(s.Navigable(gx,gz),"southeast portal blocked");
        Near(s.Vehicle.Heading,s.Layout.OutwardHeading,.0001,"default machine must face the pit");
        s.Controls.Throttle=-.65f;Tick(s,18);Assert(s.HasEnteredRink,"cannot back from nose-in parking onto ice");
        s.Controls.Throttle=.4f;
        for(int i=0;i<1400&&!s.InServiceBay;i++)s.Step(.02f);
        Assert(s.InServiceBay,"cannot drive forward from rink into service room");
        Console.WriteLine("  diagonal gate="+gx.ToString("F3")+","+gz.ToString("F3")+"; reverse departure and forward return under drive controls PASS");
    }
    static void UnconventionalSession()
    {
        // Starts at the normal saved-scene pose; every subsequent vehicle pose
        // is integrated. Deliberately runs equipment while leaving backstage.
        var s=new Simulation();s.Controls.HorizontalAuger=s.Controls.VerticalAuger=s.Controls.BoardBrush=true;
        s.Controls.ConditionerTarget=0;s.Controls.BladeDepthMm=.3f;s.Controls.FloodFlow=.35f;s.Controls.WashValve=.3f;
        s.Controls.TankLiftTarget=.35f;s.Controls.Throttle=-.35f;Tick(s,3);
        Assert(s.TryDismount(),"unconventional moving dismount failed");Tick(s,5);
        Assert(s.Controls.HorizontalAuger&&s.Controls.FloodFlow>.3f&&s.TankLift>.3f,"unconventional machinery state corrected");
        Assert(s.TryBoard(1),"unconventional boarding failed");s.Controls.TankLiftTarget=0;s.Controls.Throttle=-.65f;Tick(s,25);
        Assert(s.HasEnteredRink&&s.SnowTankM3>.001f,"unconventional actual entry/cutting failed");
        s.Controls.Throttle=0;s.Controls.Brake=true;Tick(s,2);s.Controls.TankLiftTarget=1;Tick(s,10);Shake(s);
        Assert(s.DumpSnowOnIceM3>0&&s.PitSnowM3==0,"unconventional ice dump not spatial");
        double floorWater=0;foreach(var cell in s.Environment.Cells)floorWater+=cell.WaterLiters;
        Assert(floorWater>0,"backstage water never created runoff");
        Assert(s.TryFinish(),"unconventional scored Free Simulation rejected completion");
        var report=s.GetReport();Assert(report.Mistakes>0&&report.Coverage<.1f&&report.Score>=0,"unconventional consequences/score missing");
        Console.WriteLine("  unconventional driven session: coverage="+report.Coverage.ToString("P2")+", score="+report.Score.ToString("F1")+", misuse="+report.Mistakes+", floor water="+floorWater.ToString("F2")+"L, dumped ontoice="+s.DumpSnowOnIceM3.ToString("F5")+"m3; completed without reset");
    }

    readonly struct Point
    {
        public readonly float X,Z;public Point(double x,double z){X=(float)x;Z=(float)z;}
    }
    sealed class Route
    {
        public readonly List<Point> Points=new List<Point>();public double X,Z,Heading;
        public Route(Simulation s){X=s.Vehicle.X;Z=s.Vehicle.Z;Heading=s.Vehicle.Heading;Points.Add(new Point(X,Z));}
        public void Line(double length){int n=(int)Math.Ceiling(Math.Abs(length)/.12);for(int i=0;i<n;i++){X+=Math.Sin(Heading)*length/n;Z+=Math.Cos(Heading)*length/n;Points.Add(new Point(X,Z));}}
        public void Turn(double angle,double radius)
        {int n=(int)Math.Ceiling(Math.Abs(angle*radius)/.12);double da=angle/n,sign=Math.Sign(angle);for(int i=0;i<n;i++){X+=radius/sign*(Math.Cos(Heading)-Math.Cos(Heading+da));Z+=radius/sign*(Math.Sin(Heading+da)-Math.Sin(Heading));Heading+=da;Points.Add(new Point(X,Z));}}
        public void Shift(double dx){double a=Math.Acos(1-Math.Abs(dx)/10);Turn(Math.Sign(dx)*a,5);Turn(-Math.Sign(dx)*a,5);}
        public void Box(double halfWidth,double halfLength,double radius)
        {Line(halfLength-radius-Z);Turn(Math.PI/2,radius);Line(2*(halfWidth-radius));Turn(Math.PI/2,radius);Line(2*(halfLength-radius));Turn(Math.PI/2,radius);Line(2*(halfWidth-radius));Turn(Math.PI/2,radius);Line(-12-Z);}
    }
    static float Distance(Point p,VehicleState v){float x=p.X-v.X,z=p.Z-v.Z;return(float)Math.Sqrt(x*x+z*z);}
    public static void FullRun()
    {
        var s=new Simulation();s.Controls.Throttle=-.65f;
        // Back out from the actual nose-in start, then turn within the rink.
        // This prefix uses the drive controls, never a pose correction.
        for(int i=0;i<2500;i++)
        {
            s.Layout.ToLocal(s.Vehicle.X,s.Vehicle.Z,out _,out float exitOutward);
            if(exitOutward<=-14)break;
            s.Step(.02f);
            Assert(i<2499,"nose-in reverse departure blocked");
        }
        s.Controls.Throttle=0;s.Controls.Brake=true;Tick(s,2);s.Controls.Brake=false;
        Assert(s.HasEnteredRink,"nose-in full run failed reverse entry");
        var route=new Route(s);
        route.Turn(Math.PI*.75,5);route.Line(route.X+4.5);route.Turn(Math.PI/2,6.92);
        int workStart=route.Points.Count;
        for(int ring=0;ring<5;ring++)
        {
            double inset=1.53+ring*1.6;if(ring>0)route.Shift(1.6);
            route.Box(13-inset,30-inset,Math.Max(4.6,8.5-inset));
            if(ring==0)
            {
                route.Line(8.52);
                for(int i=workStart+25;i<route.Points.Count;i++)
                {var p=route.Points[i];double x=p.X,z=p.Z;if(Math.Abs(x)>11.3)x+=Math.Sign(x)*.22*Math.Max(0,Math.Min(1,(21.5-Math.Abs(z))/3));if(Math.Abs(z)>28.3)z+=Math.Sign(z)*.22*Math.Max(0,Math.Min(1,(4.5-Math.Abs(x))/2));route.Points[i]=new Point(x,z);}
            }
        }
        for(int lane=0;lane<9;lane++){double left=-10.95+lane*1.625;route.Shift(left-route.X);route.Box(4.6,20.6,4.6);}
        int workEnd=route.Points.Count;
        // A feasible connector clears the east boards before the southeast
        // approach. All arcs respect the machine's roughly 4.6 m turn radius.
        route.Turn(-Math.PI,4.6);route.Line(4);route.Turn(-Math.PI/2,5);
        double targetSum=s.Layout.GateX+s.Layout.GateZ+s.Layout.PitSide*Math.Sqrt(2);
        double turnSum=5*(Math.Sin(Math.PI/4)-(1-Math.Cos(Math.PI/4)));
        route.Line(targetSum-route.X-route.Z-turnSum);route.Turn(Math.PI/4,5);
        s.Layout.ToLocal((float)route.X,(float)route.Z,out _,out float outward);
        route.Line(7.9-outward);
        int waypoint=1,stopped=0;
        for(int tick=0;tick<135000;tick++)
        {
            while(waypoint<route.Points.Count-1&&Distance(route.Points[waypoint],s.Vehicle)<1.25f)waypoint++;
            var target=route.Points[waypoint];float distance=Distance(target,s.Vehicle);
            if(waypoint==route.Points.Count-1&&distance<.15f)break;
            float desired=(float)Math.Atan2(target.X-s.Vehicle.X,target.Z-s.Vehicle.Z);
            float steer=(float)Math.Atan2(2*s.Config.Wheelbase*Math.Sin(desired-s.Vehicle.Heading),Math.Max(.1f,distance));
            s.Controls.Steer=Simulation.Clamp(steer/s.Config.MaxSteeringRadians,-1,1);s.Controls.Throttle=.34f;
            bool working=waypoint>workStart&&waypoint<workEnd;s.Controls.ConditionerTarget=working?0:1;
            if(working){s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;s.Controls.BladeDepthMm=s.Conditioner.Lift<.1f?.3f:0;s.Controls.FloodFlow=s.Conditioner.Lift<.1f?.23f:0;s.Controls.BoardBrush=true;}
            else{s.Controls.BladeDepthMm=0;s.Controls.FloodFlow=0;s.Controls.BoardBrush=false;bool snow=s.HorizontalSnowM3+s.VerticalSnowM3>.00001f;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=snow;if(snow)s.Controls.ConditionerTarget=0;}
            s.Step(.02f);stopped=tick>100&&Math.Abs(s.Vehicle.Speed)<.001f?stopped+1:0;
            Assert(stopped<100,"route blocked waypoint="+waypoint+"/"+route.Points.Count+" pose="+s.Vehicle.X.ToString("F2")+","+s.Vehicle.Z.ToString("F2")+" heading="+s.Vehicle.Heading.ToString("F2"));
            if(tick==134999)throw new Exception("full route timeout waypoint="+waypoint);
        }
        s.Controls.Throttle=0;s.Controls.Brake=true;Tick(s,2);s.Controls.HorizontalAuger=s.Controls.VerticalAuger=false;s.Controls.ConditionerTarget=1;Tick(s,3);
        Assert(s.InServiceBay,"full run failed return service room: "+s.Vehicle.X+","+s.Vehicle.Z);
        s.Controls.TankLiftTarget=1;Tick(s,16);Shake(s);s.Controls.HydraulicHoldControls=false;s.Controls.TankLiftTarget=0;Tick(s,6);
        Assert(s.TryFinish(),"free scored run cannot finish");var report=s.GetReport();
        int allCovered=0;foreach(var cell in s.Ice.Cells)if(cell.IsIce&&cell.Covered)allCovered++;
        Console.WriteLine("  southeast full run: coverage="+report.Coverage.ToString("P2")+", wholeice combinedcoverage="+((float)allCovered/s.Ice.IceCellCount).ToString("P2")+", watercoverage="+report.WaterCoverage.ToString("P2")+", finish="+report.FinishQuality.ToString("P1")+", score="+report.Score.ToString("F1")+", water="+s.TotalFloodUsedLiters.ToString("F1")+"L, pit="+s.PitSnowM3.ToString("F3")+"m3, time="+s.Time.ToString("F0")+"s, mistakes="+s.Mistakes+", properexit="+report.ProperExit+", return="+s.Vehicle.X.ToString("F2")+","+s.Vehicle.Z.ToString("F2"));
        Assert(s.ProperExit&&s.PitSnowM3>=s.TotalSnowCollectedM3*.98f,"route failed actual clean exit or pit disposal");
        Assert(report.Coverage>=.95f,"southeast route below95% baseline");
    }
}
