using System;
using IceResurfacer.Core;

/// <summary>
/// Board contact while driving: glancing contact slides along the boards, square impacts stop, the
/// authored outline (chassis polygon, conditioner collider, guide roller) never passes the driving margin,
/// the guide roller (moved outboard to lead the conditioner side plates by 25 mm) is the part that rides the
/// boards in a straight run, and the original rectangle still behaves. Deterministic; the controls are the only input.
/// </summary>
internal static class ContactDrivingValidation
{
    // The authored Aurora's fitted envelope (Scenes/IceResurfacer_AuthoredMachine.unity calibration).
    static readonly float[] AuthoredOutline={-0.96f,-1.25f,-0.96f,-1.125f,-0.94f,-1.075f,-1.02f,-1.025f,-1.02f,-0.775f,-0.927f,-0.725f,-0.94f,-0.675f,-0.94f,-0.575f,-0.784f,-0.525f,-0.792f,-0.425f,-0.792f,-0.325f,-0.836f,-0.225f,-0.836f,-0.175f,-0.85f,-0.125f,-0.85f,0.125f,-0.836f,0.175f,-0.836f,0.225f,-0.813f,0.275f,-0.81f,0.425f,-0.75f,0.475f,-0.75f,0.525f,-0.745f,0.675f,-0.74f,0.825f,-0.748f,0.875f,-0.748f,0.975f,-0.732f,1.025f,-0.71f,1.075f,-0.712f,1.125f,-0.712f,1.175f,-0.738f,1.225f,-0.738f,1.325f,-0.796f,1.425f,-0.796f,1.625f,-0.836f,1.725f,-0.836f,1.775f,-0.85f,1.825f,-0.85f,2.125f,-0.836f,2.175f,-0.792f,2.275f,-0.792f,2.425f,-0.769f,2.475f,-0.769f,2.55f,0.769f,2.55f,0.769f,2.475f,0.792f,2.425f,0.792f,2.275f,0.836f,2.175f,0.85f,2.125f,0.85f,1.825f,0.836f,1.775f,0.836f,1.725f,0.796f,1.625f,0.796f,1.425f,0.738f,1.325f,0.738f,1.225f,0.712f,1.175f,0.712f,1.125f,0.71f,1.075f,0.731f,1.025f,0.744f,0.975f,0.744f,0.875f,0.74f,0.825f,0.75f,0.725f,0.751f,0.525f,0.785f,0.475f,0.81f,0.425f,0.813f,0.275f,0.836f,0.225f,0.836f,0.175f,0.85f,0.125f,0.884f,0.075f,0.888f,0.025f,0.888f,-0.175f,0.836f,-0.225f,0.833f,-0.375f,0.9f,-0.425f,0.903f,-0.475f,0.903f,-0.625f,0.9f,-0.675f,0.814f,-0.725f,1.02f,-0.775f,1.02f,-1.025f,0.153f,-1.075f,0.18f,-1.125f,0.18f,-1.25f};

    static SimulatorConfig Authored()
    {
        return new SimulatorConfig
        {
            DrawbarLength=.435f,TankHingeZ=2.411f,TankScaleX=1.0684f,TankScaleY=1.2685f,TankScaleZ=1.1841f,
            ConveyorDischargeX=-.021f,ConveyorDischargeHeight=1.6392096f,ConveyorDischargeZ=.023590684f,
            BrushPivotX=-.548f,BrushPivotZ=1.3f,BrushArmLength=.38849553f,BrushRadius=.192f,BrushRetractedDegrees=86.32715f,
            CollisionOutline=(float[])AuthoredOutline.Clone(),ConditionerCollisionHalfWidth=1.066f,ConditionerCollisionFront=.365f,ConditionerCollisionRear=.575f,
            GuideRollerX=-1.001f,GuideRollerZ=2.444f,GuideRollerRadius=.09f,
        };
    }

    public static void Run()
    {
        foreach(var (label,config) in new[]{("authored outline",Authored()),("original rectangle",new SimulatorConfig())})
        {
            config.Validate();
            foreach(float throttle in new[]{1f,.6f})
            foreach(float angle in new[]{0f,2f,5f,10f,15f})
            foreach(float steer in new[]{0f,-.3f,.3f})
            {
                var r=Straight(config,throttle,angle,angle==0?-.06f:steer,1);
                Check(r.deadStops==0&&r.speed>.5f*throttle*config.ForwardSpeed&&r.progress>.75f*throttle*config.ForwardSpeed*5,
                    label+": "+angle+" deg glance (throttle "+throttle+", steer "+steer+") keeps moving along the boards: "+r);
                Check(r.penetration<=0,label+": "+angle+" deg glance stays out of the boards: "+r);
            }
            foreach(float angle in new[]{25f,45f})
            {
                var r=Straight(config,1,angle,0,1);
                Check(r.minSpeed<.7f*config.ForwardSpeed&&r.penetration<=0,label+": "+angle+" deg impact is strongly resisted and contained: "+r);
            }
            var head=Straight(config,1,90,0,1);
            Check(head.speed==0&&head.penetration<=0&&Math.Abs(head.headingChange)<.0001f,label+": head-on impact stops square and contained: "+head);
            var lowered=Straight(config,.6f,10,-.3f,0);
            Check(lowered.deadStops==0&&lowered.penetration<=0,label+": lowered conditioner glance slides: "+lowered);
            Parallel(config,label);
            Corner(config,label);
            Recover(config,label);
            Console.WriteLine("  "+label+": glancing contacts slide, the roller rides straight runs, impacts stop, corner and recovery pass");
        }
        Escape();
    }

    struct Result
    {
        public float progress,speed,minSpeed,penetration,headingChange;public int deadStops;
        public override string ToString(){return "progress "+progress.ToString("F2")+" m, speed "+speed.ToString("F2")+" (min "+minSpeed.ToString("F2")+"), dead stops "+deadStops+", penetration "+(penetration*1000).ToString("F1")+" mm";}
    }

    static void Place(Simulation s,float x,float z,float heading,float speed,float lift)
    {
        s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=heading;s.Vehicle.Speed=speed;s.Vehicle.SteeringAngle=0;
        float behind=s.Config.HitchBehindAxle+s.Config.DrawbarLength;
        s.Conditioner.Heading=heading;s.Conditioner.X=x-(float)Math.Sin(heading)*behind;s.Conditioner.Z=z-(float)Math.Cos(heading)*behind;
        s.Controls.HydraulicHoldControls=false;s.Conditioner.Lift=lift;s.Controls.ConditionerTarget=lift;
    }

    /// <summary>Largest signed board distance of every collision sample and the roller rim (negative = inside).</summary>
    static float Clearance(Simulation s,float x,float z,float h)
    {
        var c=s.Config;float sn=(float)Math.Sin(h),cs=(float)Math.Cos(h),worst=float.MinValue;
        for(int i=0;i<s.CollisionSampleCount;i++){s.CollisionSample(i,out float lx,out float lz);worst=Math.Max(worst,s.Ice.BoardDistance(x+cs*lx+sn*lz,z-sn*lx+cs*lz,out _,out _));}
        float rx=x+cs*c.GuideRollerX+sn*c.GuideRollerZ,rz=z-sn*c.GuideRollerX+cs*c.GuideRollerZ;
        return Math.Max(worst,s.Ice.BoardDistance(rx,rz,out _,out _)+c.GuideRollerRadius);
    }

    static float PlaceX(Simulation s,float z,float heading,float gap)
    {
        float x=-s.Config.RinkWidth*.5f+2.5f;
        for(int i=0;i<80;i++){float d=Clearance(s,x,z,heading);x-=(-gap-d)*.9f;}
        return x;
    }

    static Result Straight(SimulatorConfig config,float throttle,float angleDeg,float steer,float lift)
    {
        var s=new Simulation(config,true);
        float heading=-angleDeg*(float)Math.PI/180f;
        float x=PlaceX(s,-6,heading,angleDeg==0?.025f:.40f);
        float back=angleDeg==0?0:Math.Min(4,.40f/(float)Math.Sin(angleDeg*Math.PI/180)+.5f);
        Place(s,x+(float)Math.Sin(-heading)*back,-6-(float)Math.Cos(heading)*back,heading,throttle*config.ForwardSpeed,lift);
        s.Controls.Throttle=throttle;s.Controls.Steer=steer;
        var r=new Result{minSpeed=float.MaxValue};float z0=s.Vehicle.Z,h0=float.NaN;bool touched=false;
        for(int i=0;i<250;i++)
        {
            float before=s.Vehicle.Speed;s.Step(.02f);
            // Past the 2 cm driving margin (a sample on the margin line reads 0).
            r.penetration=Math.Max(r.penetration,Clearance(s,s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading)+.02f-.0005f);
            if(s.BoardContact){if(!touched){touched=true;h0=s.Vehicle.Heading;}if(Math.Abs(before)>.2f&&s.Vehicle.Speed==0)r.deadStops++;}
            if(touched)r.minSpeed=Math.Min(r.minSpeed,Math.Abs(s.Vehicle.Speed));
        }
        r.progress=s.Vehicle.Z-z0;r.speed=s.Vehicle.Speed;r.headingChange=float.IsNaN(h0)?0:s.Vehicle.Heading-h0;
        if(r.minSpeed==float.MaxValue)r.minSpeed=r.speed;
        return r;
    }

    /// <summary>
    /// Straight along the boards, steered lightly in or steering neutral on the board face: the guide roller is
    /// the only part on the boards, bears nearly all the way and turns with the run, and the conditioner side
    /// plates (when a collider) stay off them by most of the roller's lead.
    /// </summary>
    static void Parallel(SimulatorConfig config,string label)
    {
        foreach(var (steer,throttle,gap) in new[]{(-.06f,1f,.025f),(-.04f,.6f,.025f),(0f,1f,.0205f)})
        {
            var s=new Simulation(config,true);
            float x=PlaceX(s,-6,0,gap);Place(s,x,-6,0,throttle*config.ForwardSpeed,1);
            s.Controls.Throttle=throttle;s.Controls.Steer=steer;
            int touched=0,bearing=0;bool other=false;float conditioner=float.MaxValue,z0=s.Vehicle.Z,rolled0=s.GuideRollerTravel;
            for(int i=0;i<250;i++)
            {
                s.Step(.02f);
                other|=s.BoardContact&&!s.LastMovementContact.GuideRoller;
                if(touched>0||s.BoardContact||s.GuideRollerContact){touched++;if(s.GuideRollerContact)bearing++;}
                var plates=s.ConditionerCollisionPolygon();
                if(plates.Length>0)conditioner=Math.Min(conditioner,-PolygonClearance(s,plates));
            }
            float progress=s.Vehicle.Z-z0,rolled=s.GuideRollerTravel-rolled0;
            string run=label+": parallel run (steer "+steer+", throttle "+throttle+"): roller bearing "+bearing+"/"+touched+" steps, rolled "+rolled.ToString("F2")+" of "+progress.ToString("F2")+
                " m, conditioner "+(conditioner==float.MaxValue?"not a collider":(conditioner*1000).ToString("F0")+" mm off the boards");
            Check(!other&&touched>0&&bearing>=.9f*touched&&rolled>=.95f*progress,run+" - the roller alone rides the boards");
            Check(conditioner>.04f,run+" - the conditioner stays clear");
        }
    }

    /// <summary>Largest signed board distance along a machine-space polygon's edges (negative = inside).</summary>
    static float PolygonClearance(Simulation s,float[] polygon)
    {
        float sn=(float)Math.Sin(s.Vehicle.Heading),cs=(float)Math.Cos(s.Vehicle.Heading),worst=float.MinValue;
        for(int i=0;i<polygon.Length;i+=2)
        {
            int j=(i+2)%polygon.Length;
            for(int k=0;k<10;k++)
            {
                float lx=polygon[i]+(polygon[j]-polygon[i])*k/10f,lz=polygon[i+1]+(polygon[j+1]-polygon[i+1])*k/10f;
                worst=Math.Max(worst,s.Ice.BoardDistance(s.Vehicle.X+cs*lx+sn*lz,s.Vehicle.Z-sn*lx+cs*lz,out _,out _));
            }
        }
        return worst;
    }

    static void Corner(SimulatorConfig config,string label)
    {
        var s=new Simulation(config,true);
        float x=PlaceX(s,10,0,.03f);Place(s,x,10,0,config.ForwardSpeed,1);
        s.Controls.Throttle=1;s.Controls.Steer=-.15f;int deadStops=0;float worst=float.MinValue;
        for(int i=0;i<450;i++)
        {
            float before=s.Vehicle.Speed;s.Step(.02f);worst=Math.Max(worst,Clearance(s,s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading));
            if(s.BoardContact&&Math.Abs(before)>.2f&&s.Vehicle.Speed==0)deadStops++;
        }
        Check(deadStops==0&&s.Vehicle.Heading>1.2f&&s.Vehicle.Speed>1.5f&&worst<=-.0195f,label+": the rounded corner carries the machine round without a dead stop ("+(s.Vehicle.Heading*180/Math.PI).ToString("F1")+" deg, "+s.Vehicle.Speed.ToString("F2")+" m/s)");
    }

    static void Recover(SimulatorConfig config,string label)
    {
        var s=new Simulation(config,true);
        Place(s,-config.RinkWidth*.5f+4,0,-(float)Math.PI*.5f,config.ForwardSpeed,1);s.Controls.Throttle=1;
        for(int i=0;i<150;i++)s.Step(.02f);
        Check(s.BoardContact&&s.Vehicle.Speed==0&&s.DriveInhibitReason.Contains("Reverse"),label+": a head-on stop explains reverse-clear recovery");
        float x=s.Vehicle.X;s.Controls.Throttle=-1;for(int i=0;i<100;i++)s.Step(.02f);
        Check(s.Vehicle.X-x>.5f&&!s.BoardContact,label+": reversing away recovers ("+(s.Vehicle.X-x).ToString("F2")+" m)");
    }

    /// <summary>A pose placed inside the driving margin can always move out, never deeper.</summary>
    static void Escape()
    {
        var s=new Simulation(Authored(),true);
        Place(s,0,27.45f,0,0,1);
        float start=Clearance(s,s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading);
        s.Controls.Throttle=1;for(int i=0;i<100;i++)s.Step(.02f);
        Check(Clearance(s,s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading)<=start+.00001f&&s.BoardContact,"a pose inside the margin cannot be driven deeper");
        s.Controls.Throttle=-1;for(int i=0;i<50;i++)s.Step(.02f);
        Check(s.Vehicle.Speed< -.1f&&Clearance(s,s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading)<start,"a pose inside the margin reverses out of it");
    }

    static void Check(bool valid,string message){ if(!valid)throw new Exception(message); }
}
