using System;
using IceResurfacer.Core;

internal static class ServiceCollisionValidation
{
    public static void Run(bool baseline=false)
    {
        Console.WriteLine(baseline?"Historical service collision baseline":"Service collision and room validation");
        foreach(float angle in new[]{0f,-15f,15f,-30f,30f,90f})
        {
            var s=new Simulation(practice:true);
            s.Layout.ToWorld(angle==90?5:0,angle==90?11.5f:6.3f,out s.Vehicle.X,out s.Vehicle.Z);
            s.Vehicle.Heading=s.Layout.OutwardHeading+angle*(float)Math.PI/180;
            s.Controls.Throttle=.08f;
            for(int i=0;i<3000&&!s.BoardContact;i++)s.Step(.02f);
            s.Layout.ToLocal(s.Vehicle.X,s.Vehicle.Z,out float side,out float outward);
            float gap=PitGap(s);
            float nx,nz;
            if(baseline)s.Layout.ContactNormal(s.Vehicle.X,s.Vehicle.Z,out nx,out nz);
            else{nx=s.LastMovementContact.NormalX;nz=s.LastMovementContact.NormalZ;}
            Console.WriteLine("approach="+angle+"deg, center side/out="+side.ToString("F4")+"/"+outward.ToString("F4")+
                ", sampled body-to-visible-pit gap="+gap.ToString("F4")+"m, contact="+s.BoardContact+
                ", speed="+s.Vehicle.Speed.ToString("F4")+", contact normal="+nx.ToString("F3")+"/"+nz.ToString("F3"));
            Check(s.BoardContact,"slow approach reaches a real boundary");
            if(!baseline)
            {
                Check(gap>=-.002f&&gap<.025f,"pit stop matches rendered outline within a tick and1cm numerical clearance");
                Check(s.LastMovementContact.Obstacle==MovementObstacle.PitRim,"diagnostic identifies pit rim");
                Check(!s.ProbeVehiclePose(s.LastMovementContact.VehicleX,s.LastMovementContact.VehicleZ,s.LastMovementContact.Heading,out _),"rejected pose is actually obstructed");
                float ns=ServiceLayout.Axis*(nx+nz),no=ServiceLayout.Axis*(nx-nz);
                Check(angle==90?ns>.99f:no<-.99f,"normal points away from contacted pit face");
                float heading=s.Vehicle.Heading,px=s.Vehicle.X,pz=s.Vehicle.Z;s.Controls.Throttle=-.12f;
                for(int i=0;i<100;i++)s.Step(.02f);
                Check(!s.BoardContact&&Math.Abs(s.Vehicle.Heading-heading)<.0001f&&Distance(px,pz,s.Vehicle.X,s.Vehicle.Z)>.08f,"ordinary reverse clears contact without auto alignment or reset");
            }
        }
        if(!baseline){GlancingPit();WaterFloor();TankContacts();}
    }
    static void GlancingPit()
    {
        var s=new Simulation(practice:true);
        s.Layout.ToWorld(2.5f,9.1f,out s.Vehicle.X,out s.Vehicle.Z);
        s.Vehicle.Heading=s.Layout.OutwardHeading+75f*(float)Math.PI/180;
        s.Vehicle.Heading=(float)Math.Atan2(Math.Sin(s.Vehicle.Heading),Math.Cos(s.Vehicle.Heading));
        float heading=s.Vehicle.Heading;
        Check(s.ProbeVehiclePose(s.Vehicle.X,s.Vehicle.Z,heading,out _),"glancing pit approach begins clear");
        s.Controls.Throttle=.08f;bool touched=false;
        for(int i=0;i<250;i++){s.Step(.02f);touched|=s.LastMovementContact.Obstacle==MovementObstacle.PitRim;}
        Check(touched&&Math.Abs(s.Vehicle.Heading-heading)<.00001f,"glancing service contact cannot auto-align or rotate machine");
        Check(s.ProbeVehiclePose(s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading,out _),"glancing contact cannot penetrate pit");
        Console.WriteLine("75 degree glancing contact: physical slide, zero automatic heading change");
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
        for(int i=0;i<3000&&!pit.BoardContact;i++)pit.Step(.02f);
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
