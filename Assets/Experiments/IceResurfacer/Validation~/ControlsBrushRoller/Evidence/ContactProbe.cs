using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using IceResurfacer.Core;

/// <summary>
/// Driving probe for board contact, runnable against the old and the new Core. "visible" penetration is
/// always measured with the authored machine's fitted outline (chassis polygon, conditioner box, roller),
/// whatever geometry the simulation itself collides with.
/// </summary>
internal static class ContactProbe
{
    static readonly float[] Outline={-0.96f,-1.25f,-0.96f,-1.125f,-0.94f,-1.075f,-1.02f,-1.025f,-1.02f,-0.775f,-0.927f,-0.725f,-0.94f,-0.675f,-0.94f,-0.575f,-0.784f,-0.525f,-0.793f,-0.425f,-0.793f,-0.325f,-0.836f,-0.225f,-0.836f,-0.175f,-0.85f,-0.125f,-0.85f,0.125f,-0.836f,0.175f,-0.836f,0.225f,-0.813f,0.275f,-0.81f,0.425f,-0.75f,0.475f,-0.75f,0.525f,-0.745f,0.675f,-0.74f,0.825f,-0.748f,0.875f,-0.748f,0.975f,-0.732f,1.025f,-0.71f,1.075f,-0.712f,1.125f,-0.712f,1.175f,-0.738f,1.225f,-0.738f,1.325f,-0.796f,1.425f,-0.796f,1.625f,-0.836f,1.725f,-0.836f,1.775f,-0.85f,1.825f,-0.85f,2.125f,-0.836f,2.175f,-0.793f,2.275f,-0.793f,2.425f,-0.769f,2.475f,-0.769f,2.55f,0.769f,2.55f,0.769f,2.475f,0.793f,2.425f,0.793f,2.275f,0.836f,2.175f,0.85f,2.125f,0.85f,1.825f,0.836f,1.775f,0.836f,1.725f,0.796f,1.625f,0.796f,1.425f,0.738f,1.325f,0.738f,1.225f,0.712f,1.175f,0.712f,1.125f,0.71f,1.075f,0.731f,1.025f,0.744f,0.975f,0.744f,0.875f,0.74f,0.825f,0.75f,0.725f,0.751f,0.525f,0.785f,0.475f,0.81f,0.425f,0.813f,0.275f,0.836f,0.225f,0.836f,0.175f,0.85f,0.125f,0.885f,0.075f,0.888f,0.025f,0.888f,-0.175f,0.836f,-0.225f,0.833f,-0.375f,0.9f,-0.425f,0.903f,-0.475f,0.903f,-0.625f,0.9f,-0.675f,0.814f,-0.725f,1.02f,-0.775f,1.02f,-1.025f,0.154f,-1.075f,0.18f,-1.125f,0.18f,-1.25f};
    const float CondHalf=1.066f,CondFront=.36f,CondRear=.575f,RollerX=-1.001f,RollerZ=2.444f,RollerR=.09f;
    static readonly List<(float x,float z)> visible=new List<(float,float)>();

    static SimulatorConfig Authored(bool outline)
    {
        var c=new SimulatorConfig
        {
            DrawbarLength=.435f,TankHingeZ=2.411f,TankScaleX=1.0684f,TankScaleY=1.2685f,TankScaleZ=1.1841f,
            ConveyorDischargeX=-.021f,ConveyorDischargeHeight=1.6392096f,ConveyorDischargeZ=.023590684f,
            BrushPivotX=-.548f,BrushPivotZ=1.3f,BrushArmLength=.38849553f,BrushRadius=.192f,BrushRetractedDegrees=86.32715f,
        };
        if(outline)
        {
            Set(c,"CollisionOutline",Outline.Clone());Set(c,"ConditionerCollisionHalfWidth",CondHalf);
            Set(c,"ConditionerCollisionFront",CondFront);Set(c,"ConditionerCollisionRear",CondRear);
            c.GuideRollerX=RollerX;c.GuideRollerZ=RollerZ;c.GuideRollerRadius=RollerR;
        }
        return c;
    }
    static void Set(SimulatorConfig c,string name,object value)
    {
        var f=typeof(SimulatorConfig).GetField(name);
        if(f==null)throw new InvalidOperationException("This Core has no "+name);
        f.SetValue(c,value);
    }

    static void Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        BuildVisible();
        bool newCore=typeof(SimulatorConfig).GetField("CollisionOutline")!=null;
        if(args.Length>0&&args[0]=="--bench"){Bench(newCore);return;}
        var configs=new List<(string,SimulatorConfig)>{("authored scene calibration as saved",Authored(false))};
        if(newCore)configs.Add(("authored + fitted outline",Authored(true)));
        configs.Add(("launch scene defaults",new SimulatorConfig()));
        foreach(var (label,config) in configs)
        {
            Console.WriteLine("=== "+label+(newCore?" (new contact response)":" (original contact response)"));
            Console.WriteLine("  scenario                                         progress  speed in->out (min) deadStops contacts first   normal  roller  visible-pen sim-pen  heading");
            Parallel(config,1f);Parallel(config,.6f);
            foreach(float throttle in new[]{1f,.6f})
            foreach(float angle in new[]{2f,5f,10f,15f,25f,45f,90f})
            foreach(float steer in new[]{0f,-.3f,-1f,.3f})
            {
                if(angle>=45&&steer!=0)continue;
                Straight(config,throttle,angle,steer,1f);
            }
            Straight(config,1,5,0,0f);Straight(config,.6f,10,-.3f,0f);
            Corner(config,1f,0f);Corner(config,1f,-.15f);Corner(config,.6f,-.15f);Corner(config,.6f,-1f);
            Recover(config,2.65f);Recover(config,1f);
            GlanceThenReverse(config);
            Gate(config);
        }
    }

    static void Bench(bool newCore)
    {
        var configs=new List<(string,SimulatorConfig)>{("launch defaults",new SimulatorConfig()),("authored as saved",Authored(false))};
        if(newCore)configs.Add(("authored + outline",Authored(true)));
        foreach(var (label,config) in configs)
        foreach(bool contact in new[]{false,true})
        {
            double best=double.MaxValue;
            for(int run=0;run<5;run++)
            {
                var s=new Simulation(config,true);
                float x=contact?PlaceX(s,-20,0,.025f):0;
                Place(s,x,-20,0,1.6f,1);s.Controls.Throttle=.6f;s.Controls.Steer=contact?-.06f:0;
                for(int i=0;i<50;i++)s.Step(.02f);
                var watch=System.Diagnostics.Stopwatch.StartNew();
                for(int i=0;i<1000;i++)s.Step(.02f);
                watch.Stop();best=Math.Min(best,watch.Elapsed.TotalMilliseconds/1000);
            }
            Console.WriteLine("  BENCH | "+label.PadRight(22)+" | "+(contact?"sliding along boards":"free driving       ")+" | "+best.ToString("F4")+" ms per 20 ms step (best of 5 x 1000)");
        }
    }

    // ---- Scenarios --------------------------------------------------------------------------------

    static void Place(Simulation s,float x,float z,float heading,float speed,float lift)
    {
        s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=heading;s.Vehicle.Speed=speed;s.Vehicle.SteeringAngle=0;
        float behind=s.Config.HitchBehindAxle+s.Config.DrawbarLength;
        s.Conditioner.Heading=heading;s.Conditioner.X=x-(float)Math.Sin(heading)*behind;s.Conditioner.Z=z-(float)Math.Cos(heading)*behind;
        s.Controls.HydraulicHoldControls=false;s.Conditioner.Lift=lift;s.Controls.ConditionerTarget=lift;
    }

    /// <summary>West-board x that puts the simulated outermost point at the given gap (heading h).</summary>
    static float PlaceX(Simulation s,float z,float heading,float gap)
    {
        float x=-s.Config.RinkWidth*.5f+2.5f;
        for(int i=0;i<80;i++){float d=SimClearance(s,x,z,heading);x-=(-gap-d)*.9f;}
        return x;
    }

    static void Parallel(SimulatorConfig config,float throttle)
    {
        var s=new Simulation(config,true);
        float x=PlaceX(s,-6,0,.025f);
        Place(s,x,-6,0,throttle*config.ForwardSpeed,1);
        s.Controls.Throttle=throttle;s.Controls.Steer=-.06f;
        Report(s,"parallel light contact thr "+throttle.ToString("F1")+" steer -0.06",6f,0,1);
    }

    static void Straight(SimulatorConfig config,float throttle,float angleDeg,float steer,float lift)
    {
        var s=new Simulation(config,true);
        float heading=-angleDeg*(float)Math.PI/180f;
        float x=PlaceX(s,-6,heading,.40f);
        float back=Math.Min(4,.40f/(float)Math.Sin(angleDeg*Math.PI/180)+.5f);
        Place(s,x+(float)Math.Sin(-heading)*back,-6-(float)Math.Cos(heading)*back,heading,throttle*config.ForwardSpeed,lift);
        s.Controls.Throttle=throttle;s.Controls.Steer=steer;
        Report(s,"straight thr "+throttle.ToString("F1")+" "+angleDeg.ToString("F0").PadLeft(2)+" deg steer "+steer.ToString("+0.0;-0.0;0")+" lift "+lift.ToString("F0"),5f,0,1);
    }

    static void Corner(SimulatorConfig config,float throttle,float steer)
    {
        var s=new Simulation(config,true);
        float x=PlaceX(s,10,0,.03f);
        Place(s,x,10,0,throttle*config.ForwardSpeed,1);
        s.Controls.Throttle=throttle;s.Controls.Steer=steer;
        Report(s,"NW corner thr "+throttle.ToString("F1")+" steer "+steer.ToString("+0.00;-0.00;0"),9f,float.NaN,float.NaN);
    }

    static void Recover(SimulatorConfig config,float speed)
    {
        var s=new Simulation(config,true);
        float x=-config.RinkWidth*.5f+4;
        Place(s,x,0,-(float)Math.PI*.5f,speed,1);s.Controls.Throttle=speed/config.ForwardSpeed;
        float worst=0;bool stopped=false;float stopTime=-1;
        for(int i=0;i<200;i++){s.Step(.02f);worst=Math.Max(worst,VisiblePenetration(s));if(!stopped&&s.BoardContact&&s.Vehicle.Speed==0){stopped=true;stopTime=s.Time;}}
        float hx=s.Vehicle.X,hz=s.Vehicle.Z,hh=s.Vehicle.Heading;
        s.Controls.Throttle=-1;for(int i=0;i<100;i++){s.Step(.02f);worst=Math.Max(worst,VisiblePenetration(s));}
        Console.WriteLine("  head-on 90 deg at "+speed.ToString("F2")+" m/s: stopped="+stopped+(stopped?" at "+stopTime.ToString("F2")+"s":"")+
            " heading change "+((s.Vehicle.Heading-hh)*180/Math.PI).ToString("F2")+" deg | reverse 2 s: "+Dist(hx,hz,s.Vehicle.X,s.Vehicle.Z).ToString("F2")+
            " m, speed "+s.Vehicle.Speed.ToString("F2")+", contact "+s.BoardContact+" | visible pen "+(worst*1000).ToString("F0")+" mm");
    }

    static void GlanceThenReverse(SimulatorConfig config)
    {
        var s=new Simulation(config,true);
        float heading=-12*(float)Math.PI/180f;
        float x=PlaceX(s,-6,heading,.25f);
        Place(s,x+.6f,-8,heading,1.6f,1);s.Controls.Throttle=.6f;s.Controls.Steer=-.3f;
        float worst=0;for(int i=0;i<150;i++){s.Step(.02f);worst=Math.Max(worst,VisiblePenetration(s));}
        float cx=s.Vehicle.X,cz=s.Vehicle.Z;bool contact=s.BoardContact||s.GuideRollerContact;
        s.Controls.Throttle=-1;s.Controls.Steer=.5f;for(int i=0;i<150;i++){s.Step(.02f);worst=Math.Max(worst,VisiblePenetration(s));}
        s.Controls.Steer=-.5f;for(int i=0;i<50;i++){s.Step(.02f);worst=Math.Max(worst,VisiblePenetration(s));}
        Console.WriteLine("  glance 12 deg then reverse away (steer right, then left): contact before reverse "+contact+" | moved "+Dist(cx,cz,s.Vehicle.X,s.Vehicle.Z).ToString("F2")+
            " m, off boards "+(-SimClearance(s,s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading)).ToString("F2")+" m, contact now "+s.BoardContact+" | visible pen "+(worst*1000).ToString("F0")+" mm");
    }

    static void Gate(SimulatorConfig config)
    {
        // Reverse out of the service bay through the gate onto the ice, then drive back in.
        var s=new Simulation(config,true);
        s.Layout.ParkingPose(out float px,out float pz,out float ph);
        Place(s,px,pz,ph,0,1);
        float worst=0;int contacts=0;
        s.Controls.Throttle=-1;for(int i=0;i<700;i++){s.Step(.02f);worst=Math.Max(worst,VisiblePenetration(s));if(s.BoardContact)contacts++;}
        s.Layout.ToLocal(s.Vehicle.X,s.Vehicle.Z,out _,out float outward);
        bool onIce=s.Ice.IsInside(s.Vehicle.X,s.Vehicle.Z);
        s.Controls.Throttle=.5f;for(int i=0;i<900;i++){s.Step(.02f);worst=Math.Max(worst,VisiblePenetration(s));if(s.BoardContact)contacts++;}
        s.Layout.ToLocal(s.Vehicle.X,s.Vehicle.Z,out _,out float back);
        Console.WriteLine("  service gate: reversed to outward "+outward.ToString("F2")+" (on ice "+onIce+"), returned to outward "+back.ToString("F2")+" | contact ticks "+contacts+" | visible pen "+(worst*1000).ToString("F0")+" mm");
        // Glancing the gate jamb: angled reverse exit that brushes the post.
        var j=new Simulation(config,true);
        j.Layout.ToWorld(1.1f,4.5f,out float jx,out float jz);
        Place(j,jx,jz,j.Layout.OutwardHeading+.10f,0,1);
        float jw=0;int jc=0;j.Controls.Throttle=-.6f;j.Controls.Steer=.25f;
        for(int i=0;i<500;i++){j.Step(.02f);jw=Math.Max(jw,VisiblePenetration(j));if(j.BoardContact)jc++;}
        j.Layout.ToLocal(j.Vehicle.X,j.Vehicle.Z,out _,out float jo);
        Console.WriteLine("  gate jamb glance (reverse, off-centre): reached outward "+jo.ToString("F2")+" | contact ticks "+jc+" last "+j.LastMovementContact.Reason+" | visible pen "+(jw*1000).ToString("F0")+" mm");
    }

    static void Report(Simulation s,string label,float seconds,float tx,float tz)
    {
        float startX=s.Vehicle.X,startZ=s.Vehicle.Z,speed0=s.Vehicle.Speed;
        int contacts=0,deadStops=0;string first="none";float minSpeed=float.MaxValue,maxNormal=0,worstVisible=0,worstSim=0;
        float lastHeading=s.Vehicle.Heading,maxYawJump=0;
        int steps=(int)(seconds/.02f);
        for(int i=0;i<steps;i++)
        {
            float before=s.Vehicle.Speed;
            s.Step(.02f);
            worstVisible=Math.Max(worstVisible,VisiblePenetration(s));
            worstSim=Math.Max(worstSim,Math.Max(0,SimClearance(s,s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading)+.0f));
            float yaw=Math.Abs(Wrap(s.Vehicle.Heading-lastHeading));maxYawJump=Math.Max(maxYawJump,yaw);lastHeading=s.Vehicle.Heading;
            if(s.BoardContact)
            {
                contacts++;
                if(first=="none")first=s.LastMovementContact.GuideRoller?"ROLLER":ConditionerContact(s)?"CONDTNR":"BODY";
                maxNormal=Math.Max(maxNormal,s.ContactNormalSpeed);
                if(Math.Abs(before)>.2f&&s.Vehicle.Speed==0)deadStops++;
            }
            if(first!="none")minSpeed=Math.Min(minSpeed,Math.Abs(s.Vehicle.Speed));
        }
        float progress=float.IsNaN(tx)?Dist(startX,startZ,s.Vehicle.X,s.Vehicle.Z):(s.Vehicle.X-startX)*tx+(s.Vehicle.Z-startZ)*tz;
        Console.WriteLine("  "+label.PadRight(47)+" "+progress.ToString("F2").PadLeft(6)+" m  "+speed0.ToString("F2")+"->"+s.Vehicle.Speed.ToString("F2")+
            " ("+(minSpeed==float.MaxValue?" -  ":minSpeed.ToString("F2"))+")  "+deadStops.ToString().PadLeft(3)+"  "+contacts.ToString().PadLeft(6)+"  "+first.PadRight(7)+
            " "+maxNormal.ToString("F2")+"  "+s.GuideRollerTravel.ToString("F2").PadLeft(5)+"  "+(worstVisible*1000).ToString("F0").PadLeft(5)+" mm "+(worstSim*1000).ToString("F0").PadLeft(4)+" mm  "+
            (s.Vehicle.Heading*180/Math.PI).ToString("F1").PadLeft(6)+" | max yaw step "+(maxYawJump*180/Math.PI).ToString("F2")+" deg");
    }

    // ---- Geometry ----------------------------------------------------------------------------------

    static void BuildVisible()
    {
        void Loop(float[] o){for(int i=0;i<o.Length;i+=2){int j=(i+2)%o.Length;float ax=o[i],az=o[i+1],bx=o[j],bz=o[j+1];int n=Math.Max(1,(int)Math.Ceiling(Math.Sqrt((bx-ax)*(bx-ax)+(bz-az)*(bz-az))/.05));for(int k=0;k<n;k++)visible.Add((ax+(bx-ax)*k/n,az+(bz-az)*k/n));}}
        Loop(Outline);
        float c=-(.45f+.435f);Loop(new[]{-CondHalf,c-CondRear,-CondHalf,c+CondFront,CondHalf,c+CondFront,CondHalf,c-CondRear});
        for(int i=0;i<16;i++){float a=i*(float)Math.PI/8;visible.Add((RollerX+(float)Math.Cos(a)*RollerR,RollerZ+(float)Math.Sin(a)*RollerR));}
    }

    /// <summary>How far the authored machine's visible plan outline is past the boards or service walls (m).</summary>
    static float VisiblePenetration(Simulation s)
    {
        float sn=(float)Math.Sin(s.Vehicle.Heading),cs=(float)Math.Cos(s.Vehicle.Heading),worst=0;
        foreach(var (lx,lz) in visible)
        {
            float px=s.Vehicle.X+cs*lx+sn*lz,pz=s.Vehicle.Z-sn*lx+cs*lz;
            if(s.Ice.IsInside(px,pz)||s.Layout.ContainsFloor(px,pz))continue;
            float d=s.Layout.IsPortal(px,pz)||!s.Ice.IsInside(s.Vehicle.X,s.Vehicle.Z)?.001f:s.Ice.BoardDistance(px,pz,out _,out _);
            worst=Math.Max(worst,Math.Max(d,.001f));
        }
        return worst;
    }

    /// <summary>Largest signed board distance of what the simulation itself collides with (negative = inside).</summary>
    static float SimClearance(Simulation s,float x,float z,float h)
    {
        var c=s.Config;float sn=(float)Math.Sin(h),cs=(float)Math.Cos(h),worst=float.MinValue;
        var count=typeof(Simulation).GetProperty("CollisionSampleCount");
        if(count!=null)
        {
            var sample=typeof(Simulation).GetMethod("CollisionSample");int n=(int)count.GetValue(s);var argsArray=new object[3];
            for(int i=0;i<n;i++){argsArray[0]=i;sample.Invoke(s,argsArray);float lx=(float)argsArray[1],lz=(float)argsArray[2];worst=Math.Max(worst,s.Ice.BoardDistance(x+cs*lx+sn*lz,z-sn*lx+cs*lz,out _,out _));}
        }
        else
            for(int row=0;row<3;row++)for(int col=0;col<3;col++)
            {float lx=(col-1)*c.VehicleHalfWidth,lz=-c.BodyRear+(c.BodyFront+c.BodyRear)*row*.5f;worst=Math.Max(worst,s.Ice.BoardDistance(x+cs*lx+sn*lz,z-sn*lx+cs*lz,out _,out _));}
        float rx=x+cs*c.GuideRollerX+sn*c.GuideRollerZ,rz=z-sn*c.GuideRollerX+cs*c.GuideRollerZ;
        return Math.Max(worst,s.Ice.BoardDistance(rx,rz,out _,out _)+c.GuideRollerRadius);
    }

    static bool ConditionerContact(Simulation s)
    {
        var m=s.LastMovementContact;float dx=m.PointX-m.VehicleX,dz=m.PointZ-m.VehicleZ,sn=(float)Math.Sin(m.Heading),cs=(float)Math.Cos(m.Heading);
        return sn*dx+cs*dz< -(s.Config.HitchBehindAxle+s.Config.DrawbarLength)+.45f;
    }
    static float Dist(float ax,float az,float bx,float bz){return (float)Math.Sqrt((ax-bx)*(ax-bx)+(az-bz)*(az-bz));}
    static float Wrap(float a){while(a>Math.PI)a-=(float)(2*Math.PI);while(a< -Math.PI)a+=(float)(2*Math.PI);return a;}
}
