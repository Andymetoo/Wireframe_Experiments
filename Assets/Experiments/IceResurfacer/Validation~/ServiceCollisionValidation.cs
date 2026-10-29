using System;
using IceResurfacer.Core;

internal static class ServiceCollisionValidation
{
    public static void Run(bool baseline=false)
    {
        Console.WriteLine("Traversable snow grate, bounded conditioner and physical contact validation");
        TraverseGrate();ContactAudio();ConditionerCompliance();WaterFloor();TankContacts();
    }
    static void Place(Simulation s,float side,float outward,float heading)
    {
        s.Layout.ToWorld(side,outward,out s.Vehicle.X,out s.Vehicle.Z);s.Vehicle.Heading=heading;
        s.Conditioner.Heading=heading;s.Conditioner.X=s.Vehicle.X-(float)Math.Sin(heading)*1.1f;s.Conditioner.Z=s.Vehicle.Z-(float)Math.Cos(heading)*1.1f;
    }
    static void TraverseGrate()
    {
        foreach(float angle in new[]{0f,-15f,15f,-30f,30f,90f})
        {
            var s=new Simulation(practice:true);
            Place(s,angle==90?5:(float)Math.Sin(angle*Math.PI/180)*3,angle==90?11.5f:6.3f,s.Layout.OutwardHeading+angle*(float)Math.PI/180);
            s.Controls.Throttle=.16f;bool crossed=false;
            for(int i=0;i<3000;i++)
            {
                s.Step(.02f);s.Layout.ToLocal(s.Vehicle.X,s.Vehicle.Z,out float side,out float outward);
                crossed|=s.Layout.ContainsPit(s.Vehicle.X,s.Vehicle.Z)||s.Layout.ContainsPit(
                    s.Vehicle.X+(float)Math.Sin(s.Vehicle.Heading)*s.Config.BodyFront,
                    s.Vehicle.Z+(float)Math.Cos(s.Vehicle.Heading)*s.Config.BodyFront);
                Check(s.LastMovementContact.Obstacle!=MovementObstacle.PitRim,"grate cannot reject a vehicle pose");
                if((angle==90&&side<-2.6f)||(angle!=90&&outward>10.85f))break;
                Check(!s.BoardContact,"approach and crossing remain clear until the actual room wall");
            }
            Check(crossed&&s.Vehicle.Speed>.1f,"machine drives on/through unloading grate at "+angle+" degrees");
            Check(s.ProbeVehiclePose(s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading,out _),"accepted crossing pose remains physically clear");
            Console.WriteLine("grate approach="+angle+"deg crossed with speed="+s.Vehicle.Speed.ToString("F3")+"m/s; no contact event="+(s.ContactEventSequence==0));
        }
        var wall=new Simulation(practice:true);wall.Controls.Throttle=.2f;
        for(int i=0;i<3000&&!wall.BoardContact;i++)wall.Step(.02f);
        Check(wall.BoardContact&&wall.LastMovementContact.Obstacle==MovementObstacle.ServiceWall,"only actual end wall stops a straight drive beyond grate");
        float heading=wall.Vehicle.Heading,x=wall.Vehicle.X,z=wall.Vehicle.Z;wall.Controls.Throttle=-.2f;
        for(int i=0;i<150;i++)wall.Step(.02f);
        Check(!wall.BoardContact&&Math.Abs(wall.Vehicle.Heading-heading)<.0001f&&Distance(x,z,wall.Vehicle.X,wall.Vehicle.Z)>.2f,"wall recovery is ordinary reverse without automatic alignment");
        wall.Layout.ToWorld(0,wall.Layout.PitOutward,out float px,out float pz);wall.Environment.Deposit(px,pz,0,12);
        wall.Environment.TickWater(.21f,wall.Ice,wall.Layout);
        Check(Math.Abs(wall.Environment.GrateDrainedWaterLiters-12)<.0001,"water over traversable grate enters accounted receiver drain");
    }
    static Simulation Hit(float heading,float speed,bool wall=false)
    {
        var s=new Simulation(practice:true);
        if(wall)Place(s,0,9f,s.Layout.OutwardHeading);
        else{s.Vehicle.X=heading==0?0:-11.6f;s.Vehicle.Z=heading==0?26:-10;s.Vehicle.Heading=heading;}
        s.Vehicle.Speed=speed;s.Controls.Throttle=speed/s.Config.ForwardSpeed;
        for(int i=0;i<2500&&s.ContactEventSequence==0;i++)s.Step(.02f);
        Check(s.ContactEventSequence>0,"physical approach emits a finite contact event");return s;
    }
    static void ContactAudio()
    {
        var slow=Hit(0,.3f);var hard=Hit(0,2f);var glance=Hit(-.08f,2f);var wall=Hit(0,.6f,true);
        Check(hard.AudibleContactNormalSpeed>slow.AudibleContactNormalSpeed*3,"direct high speed impact exceeds slow contact");
        Check(glance.AudibleContactNormalSpeed<hard.AudibleContactNormalSpeed*.45f,"fast glance severity follows normal velocity rather than total speed");
        Check(hard.AudibleContactSeverity>slow.AudibleContactSeverity,"impact volume calibration distinguishes direct speeds");
        Check(wall.AudibleContact.Obstacle==MovementObstacle.ServiceWall,"service wall contact is a physical audio source");
        float hardNormal=hard.AudibleContactNormalSpeed;int first=hard.ContactEventSequence;for(int i=0;i<500;i++)hard.Step(.02f);
        Check(hard.ContactEventSequence-first<=26,"holding against wall is bounded by contact cooldown and velocity floor");
        var brake=new Simulation(practice:true);brake.Controls.Throttle=1;brake.Controls.Brake=true;
        for(int i=0;i<200;i++)brake.Step(.02f);
        Check(brake.ContactEventSequence==0,"braking/nonphysical refusal emits no collision sound");
        Console.WriteLine("contact normal m/s slow/direct/glancing/wall="+slow.AudibleContactNormalSpeed.ToString("F3")+"/"+hardNormal.ToString("F3")+"/"+glance.AudibleContactNormalSpeed.ToString("F3")+"/"+wall.AudibleContactNormalSpeed.ToString("F3")+"; events over held10s="+(hard.ContactEventSequence-first));
    }
    static void ConditionerCompliance()
    {
        var rigid=new Simulation(new SimulatorConfig{ConditionerYawLimitRadians=0},true);var compliant=new Simulation(practice:true);
        rigid.Vehicle.X=compliant.Vehicle.X=0;rigid.Vehicle.Z=compliant.Vehicle.Z=-12;rigid.Vehicle.Heading=compliant.Vehicle.Heading=0;
        rigid.Conditioner.X=compliant.Conditioner.X=0;rigid.Conditioner.Z=compliant.Conditioner.Z=-13.1f;rigid.Conditioner.Heading=compliant.Conditioner.Heading=0;
        rigid.Controls.Throttle=compliant.Controls.Throttle=.4f;rigid.Controls.Steer=compliant.Controls.Steer=1;float peak=0;
        for(int i=0;i<450;i++)
        {
            rigid.Step(.02f);compliant.Step(.02f);
            float yaw=(float)Math.Atan2(Math.Sin(compliant.Conditioner.Heading-compliant.Vehicle.Heading),Math.Cos(compliant.Conditioner.Heading-compliant.Vehicle.Heading));peak=Math.Max(peak,Math.Abs(yaw));
            Check(Math.Abs(yaw)<=compliant.Config.ConditionerYawLimitRadians+.00001f,"conditioner remains within physical compliance");
            Check(Math.Abs(rigid.Vehicle.X-compliant.Vehicle.X)<.00001f&&Math.Abs(rigid.Vehicle.Z-compliant.Vehicle.Z)<.00001f&&Math.Abs(rigid.Vehicle.Heading-compliant.Vehicle.Heading)<.00001f,"conditioner correction does not change chassis turn");
        }
        Check(peak>.02f,"curved pass exercises limited compliance");
        Console.WriteLine("conditioner maximum yaw="+(peak*180/Math.PI).ToString("F3")+"deg, chassis path unchanged; kinematic full-lock rear axle radius="+(compliant.Config.Wheelbase/Math.Tan(compliant.Config.MaxSteeringRadians)).ToString("F4")+"m");
    }
    static void WaterFloor()
    {
        var s=new Simulation(practice:true);s.Layout.ToWorld(-3.5f,6.3f,out float x,out float z);
        s.Environment.Deposit(x,z,0,20);
        double initial=FloorWater(s);
        for(int i=0;i<400;i++)s.Environment.TickWater(.2f,s.Ice,s.Layout);
        double floor=FloorWater(s),drain=s.Environment.DrainedWaterLiters;int wet=0;
        foreach(var cell in s.Environment.Cells)if(cell.WaterLiters>.00001f)
        {
            wet++;Check(s.Layout.ContainsFloor(cell.WaterX,cell.WaterZ)&&!s.Layout.ContainsPit(cell.WaterX,cell.WaterZ)&&!s.Ice.IsInside(cell.WaterX,cell.WaterZ),"floor flow remains clipped to actual service floor");
        }
        Check(wet>1&&drain>0,"real source water spreads gradually and reaches drain");
        Check(Math.Abs(initial-floor-drain)<.002,"floor water plus cumulative drain conserves deposited liters");
        Console.WriteLine("floor water: input="+initial.ToString("F4")+"L, remaining="+floor.ToString("F4")+"L, drained="+drain.ToString("F4")+"L, wet cells="+wet);
    }
    static double FloorWater(Simulation s){double total=0;foreach(var cell in s.Environment.Cells)total+=cell.WaterLiters;return total;}
    static void TankContacts()
    {
        var s=new Simulation(practice:true);
        s.Vehicle.X=10.1f;s.Vehicle.Z=0;s.Vehicle.Heading=(float)Math.PI*.5f;
        Check(s.ProbeVehiclePose(s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading,out _),"stowed machine clears east boards at fixture");
        Check(!s.ProbeTankPose(s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading,1,out MovementContact hit)&&hit.Obstacle==MovementObstacle.TankGlass&&hit.PointY<2.55f,"actual opening door intersects glass at height, rather than rejecting overhead space");
        s.SetTankHydraulic(1);for(int i=0;i<350;i++)s.Step(.02f);
        Check(s.TankLift>.03f&&s.TankLift<.99f&&s.LastMovementContact.Obstacle==MovementObstacle.TankGlass,"lift holds at actual tank/door contact");
        float stopped=s.TankLift;Check(s.ProbeTankPose(s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading,stopped,out _),"attained tank pose remains outside glass");
        s.SetTankHydraulic(0);s.Controls.Throttle=-.2f;for(int i=0;i<300;i++)s.Step(.02f);
        Check(s.Vehicle.Speed<-.1f&&s.TankLift==stopped,"reverse moves away without forced tank lowering or lock");
        s.Controls.Throttle=0;s.SetTankHydraulic(1);for(int i=0;i<350;i++)s.Step(.02f);
        Check(s.TankLift>.99f,"same hydraulic command completes after moving clear");
        var pit=new Simulation(practice:true);pit.Controls.Throttle=.08f;
        for(int i=0;i<3000;i++){pit.Step(.02f);pit.Layout.ToLocal(pit.Vehicle.X,pit.Vehicle.Z,out _,out float outPos);if(outPos>=8.15f)break;}
        pit.Controls.Brake=true;for(int i=0;i<100;i++)pit.Step(.02f);
        pit.Controls.Throttle=0;pit.SetTankHydraulic(1);for(int i=0;i<350;i++)pit.Step(.02f);
        Check(pit.TankLift>.99f&&pit.InDumpZone,"raised tank remains free to overhang physical pit");
        Console.WriteLine("tank contacts: lift stopped="+stopped.ToString("F4")+", actual surface contact height="+hit.PointY.ToString("F4")+"m; reverse/continued lift/pit overhang passed");
    }
    static float Distance(float x,float z,float tx,float tz){float dx=x-tx,dz=z-tz;return(float)Math.Sqrt(dx*dx+dz*dz);}
    static float PitGap(Simulation s)
    {
        float best=float.MaxValue,sn=(float)Math.Sin(s.Vehicle.Heading),cs=(float)Math.Cos(s.Vehicle.Heading);
        for(int row=0;row<3;row++)for(int col=0;col<3;col++)
        {
            float lx=(col-1)*s.Config.VehicleHalfWidth,lz=-s.Config.BodyRear+(s.Config.BodyFront+s.Config.BodyRear)*row*.5f;
            s.Layout.ToLocal(s.Vehicle.X+cs*lx+sn*lz,s.Vehicle.Z-sn*lx+cs*lz,out float side,out float outward);
            float dx=Math.Max(0,Math.Abs(side-s.Layout.PitSide)-s.Layout.PitHalfWidth);
            float dz=Math.Max(0,Math.Abs(outward-s.Layout.PitOutward)-s.Layout.PitHalfLength);
            best=Math.Min(best,(float)Math.Sqrt(dx*dx+dz*dz));
        }
        return best;
    }
    static void Check(bool value,string message){if(!value)throw new Exception("Service collision: "+message);}
}
