using System;
using System.Diagnostics;
using System.Reflection;
using IceResurfacer.Core;

static class BrushChecks
{
    static readonly MethodInfo update=typeof(Simulation).GetMethod("UpdateBoardBrush",BindingFlags.Instance|BindingFlags.NonPublic);
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Near(float a,float b,float tolerance,string message){Check(Math.Abs(a-b)<=tolerance,message+": "+a+" versus "+b);}
    static void TickBrush(Simulation s,float dt=.02f,float distance=0){update.Invoke(s,new object[]{dt,distance});}
    static SimulatorConfig Config()=>new SimulatorConfig{BrushPivotX=-.548f,BrushPivotZ=1.3f,BrushArmLength=.38849553f,BrushRadius=.192f,BrushRetractedDegrees=86.32715f,BrushComplianceTravel=.12f,GuideRollerX=-.827f,GuideRollerZ=2.444f};
    static Simulation Fixture(float compression)
    {
        var s=new Simulation(Config());s.Vehicle.X=0;s.Vehicle.Z=0;s.Vehicle.Heading=0;s.Controls.BoardBrush=true;
        for(int i=0;i<50;i++)TickBrush(s);
        BoardBrushArticulation.Contact(s.Config,1,out float lx,out _);
        s.Vehicle.X=-s.Config.RinkWidth*.5f+s.Config.BrushRadius-compression-lx;
        TickBrush(s);return s;
    }
    static void Run(string name,Action action){action();passed++;Console.WriteLine("PASS "+name);}
    static void Main()
    {
        Run("saved endpoints derive current mechanism and reject unequal arm lengths",()=>{
            var c=Config();
            foreach(float deployment in new[]{0f,.25f,.5f,.75f,1f}){
                BoardBrushArticulation.Contact(c,deployment,out float x,out float z);
                Check(BoardBrushArticulation.Endpoint(c.BrushPivotX,c.BrushPivotZ,c.BrushArmLength,x,z,out float angle,out _),"valid endpoint rejected");
                Near(angle,BoardBrushArticulation.ArmDegrees(c,deployment),.0001f,"endpoint angle");
            }
            Check(!BoardBrushArticulation.Endpoint(c.BrushPivotX,c.BrushPivotZ,c.BrushArmLength,-1.2f,1.3f,out _,out _),"impossible arm length accepted");
        });
        Run("insufficient reach does not claim board contact",()=>{var s=Fixture(-.08f);Check(!s.BrushContact,"contact with 80 mm gap");Near(s.BrushCompression,0,.00001f,"false compression");});
        Run("light compression remains in contact without vibration",()=>{
            var s=Fixture(.025f);float min=1,max=0;
            for(int i=0;i<500;i++){TickBrush(s);min=Math.Min(min,s.BrushCompression);max=Math.Max(max,s.BrushCompression);Check(s.BrushContact&&!s.BrushTravelLimited,"contact lost");}
            Near(s.BrushCompression,.025f,.00001f,"light compression");Near(max-min,0,.00001f,"stationary vibration");
            Near(s.Ice.BoardDistance(s.BrushWorldX,s.BrushWorldZ,out _,out _),-s.Config.BrushRadius,.00001f,"disc/board mismatch");
        });
        Run("full compression bounded and genuine overtravel reported",()=>{
            var s=Fixture(.12f);Near(s.BrushCompression,.12f,.00001f,"full compression");Check(!s.BrushTravelLimited,"limit reported prematurely");
            s.Vehicle.X-=.05f;TickBrush(s);Check(s.BrushTravelLimited,"overtravel hidden");Near(s.BrushCompression,.12f,.00001f,"travel exceeded");
        });
        Run("release returns smoothly without snapping or overshoot",()=>{
            var s=Fixture(.08f);s.Vehicle.X+=.4f;float before=s.BrushCompression;TickBrush(s);
            Check(s.BrushCompression>before*.9f&&s.BrushCompression<before,"release snapped or stalled");
            float first=s.BrushCompression,last=first;
            for(int i=0;i<150;i++){TickBrush(s);Check(s.BrushCompression<=last+.000001f,"release oscillates");last=s.BrushCompression;}
            Check(last<.00001f&&!s.BrushContact,"spring failed to settle");
            Console.WriteLine("  release first step "+before.ToString("F6")+" -> "+first.ToString("F6")+" m; settled to "+last.ToString("G6")+" m");
        });
        Run("curved boards at 2.65 m/s keep disc and board aligned",()=>{
            var s=Fixture(.04f);float radius=s.Config.CornerRadius-s.Config.BrushRadius+.04f;
            float centerX=-s.Config.RinkWidth*.5f+s.Config.CornerRadius,centerZ=s.Config.RinkLength*.5f-s.Config.CornerRadius;
            float maxPenetration=0,maxStep=0;float prevX=0,prevZ=0;
            for(int i=0;i<200;i++){
                float a=i*.02f*2.65f/radius,heading=a;
                s.Vehicle.Heading=heading;
                BoardBrushArticulation.Contact(s.Config,1,out float lx,out float lz);
                float sn=(float)Math.Sin(heading),cs=(float)Math.Cos(heading);
                float restX=centerX-radius*(float)Math.Cos(a),restZ=centerZ+radius*(float)Math.Sin(a);
                s.Vehicle.X=restX-lx*cs-lz*sn;s.Vehicle.Z=restZ+lx*sn-lz*cs;
                TickBrush(s);
                float penetration=s.Ice.BoardDistance(s.BrushWorldX,s.BrushWorldZ,out _,out _)+s.Config.BrushRadius;
                maxPenetration=Math.Max(maxPenetration,penetration);
                if(i>0)maxStep=Math.Max(maxStep,(float)Math.Sqrt(Math.Pow(s.BrushWorldX-prevX,2)+Math.Pow(s.BrushWorldZ-prevZ,2)));
                prevX=s.BrushWorldX;prevZ=s.BrushWorldZ;
                Check(s.BrushContact&&!s.BrushTravelLimited,"corner contact lost");
            }
            Check(maxPenetration<.00002f&&maxStep<.06f,"corner discontinuity");Console.WriteLine("  max corner penetration "+maxPenetration.ToString("F8")+" m; head step "+maxStep.ToString("F5")+" m");
        });
        Run("snow clearing uses the compressed disc and conserves material",()=>{
            var s=Fixture(.06f);float[] before=new float[s.Ice.Cells.Length];double totalBefore=0;
            for(int i=0;i<before.Length;i++){before[i]=s.Ice.Cells[i].SnowMm;totalBefore+=before[i];}
            TickBrush(s,.02f,.05f);int changed=0;double totalAfter=0;
            for(int i=0;i<before.Length;i++){
                float after=s.Ice.Cells[i].SnowMm;totalAfter+=after;
                if(after<before[i]-.000001f){int row=i/s.Ice.Columns,col=i%s.Ice.Columns;
                    float dx=s.Ice.CellX(col)-s.BrushWorldX,dz=s.Ice.CellZ(row)-s.BrushWorldZ;
                    Check(dx*dx+dz*dz<=s.Config.BrushRadius*s.Config.BrushRadius+.000001f,"swept outside visible disc");changed++;}
            }
            Check(changed>0&&s.BrushSweptSnowM3>0,"no sweep");Check(Math.Abs(totalBefore-totalAfter)<.002,"brush created or destroyed material");
            Console.WriteLine("  swept "+changed+" cells; volume error "+(Math.Abs(totalBefore-totalAfter)*s.Ice.CellSize*s.Ice.CellSize*.001).ToString("G6")+" m3");
        });
        Run("fixed-step spring deterministic across render cadence",()=>{
            var a=Fixture(.08f);var b=Fixture(.08f);a.Vehicle.X+=.4f;b.Vehicle.X+=.4f;
            for(int i=0;i<100;i++)a.Step(.02f);for(int i=0;i<50;i++)b.Step(.04f);
            Near(a.BrushWorldX,b.BrushWorldX,0,"cadence changed brush X");Near(a.BrushCompression,b.BrushCompression,0,"cadence changed compression");
        });
        Run("procedural geometry retains board clearance",()=>{
            var s=new Simulation();s.Vehicle.X=-s.Config.RinkWidth*.5f+s.Config.VehicleHalfWidth+.065f;s.Vehicle.Z=0;s.Vehicle.Heading=0;s.Controls.BoardBrush=true;
            for(int i=0;i<100;i++)TickBrush(s);
            Check(!s.BrushTravelLimited,"procedural travel too short");Near(s.Ice.BoardDistance(s.BrushWorldX,s.BrushWorldZ,out _,out _),-s.Config.BrushRadius,.00001f,"procedural kickplate penetration");
        });
        Run("normal driving preserves contact and actual sweep at operating speeds",()=>{
            foreach(float throttle in new[]{.6f,1f}){
                var s=new Simulation(Config());s.Vehicle.X=-s.Config.RinkWidth*.5f+s.Config.VehicleHalfWidth+.025f;s.Vehicle.Z=-10;s.Vehicle.Heading=0;
                s.Conditioner.X=s.Vehicle.X;s.Conditioner.Z=s.Vehicle.Z-s.Config.HitchBehindAxle-s.Config.DrawbarLength;
                s.Controls.BoardBrush=true;s.Controls.Throttle=throttle;int contacts=0;
                for(int i=0;i<500;i++){s.Step(.02f);if(i>50&&s.BrushContact)contacts++;Check(!s.BrushTravelLimited,"driving exceeded spring travel");}
                Check(contacts>400&&s.BrushSweptSnowM3>0,"operating contact or sweep missing");
                Console.WriteLine("  throttle "+throttle+", final speed "+s.Vehicle.Speed.ToString("F3")+" m/s; "+contacts+" contacting steps");
            }
        });
        Run("spring update has no managed allocation",()=>{
            var s=Fixture(.025f);var tick=(Action<float,float>)update.CreateDelegate(typeof(Action<float,float>),s);
            for(int i=0;i<1000;i++)tick(.02f,0);
            var watch=new Stopwatch();long before=GC.GetAllocatedBytesForCurrentThread();watch.Start();
            for(int i=0;i<100000;i++)tick(.02f,0);
            watch.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"brush update allocates: "+bytes);Console.WriteLine("  100000 brush-only steps: "+(watch.Elapsed.TotalMilliseconds/100000).ToString("F6")+" ms/step, "+bytes+" managed bytes; no renderer/GPU claim");
        });
        Console.WriteLine("Brush focused deterministic checks: "+passed+" passed. Unity visual/Undo acceptance is separate.");
    }
}
