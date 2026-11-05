using System;
using System.Diagnostics;
using IceResurfacer.Core;
using IceResurfacer.Editor;
static class Checks
{
    static void Require(bool value,string message)=>OperatorMaterialValidation.Require(value,message);
    static double Integral(SnowSurfacePatch p)
    {
        double value=0;
        for(int i=0;i<SnowSurfaceGeometry.IndicesPerPatch;i+=3)
        {
            var a=p.Point(SnowSurfaceGeometry.TriangleVertex(i));var b=p.Point(SnowSurfaceGeometry.TriangleVertex(i+1));var c=p.Point(SnowSurfaceGeometry.TriangleVertex(i+2));
            value+=SnowSurfaceGeometry.Area(a,b,c)*(a.Y+b.Y+c.Y)/3;
        }
        return value;
    }
    static void Main()
    {
        Shape();Spout();OperatorSnowRealismValidation.Run(Console.WriteLine);
        Console.WriteLine("ALL MOUND / SPOUT / MATERIAL CHECKS PASSED");
    }
    static void Shape()
    {
        var field=new IceField(new SimulatorConfig(),546);int index=field.IndexAt(.225f,.225f);
        var cell=field.Cells[index];cell.SnowMm=30;field.Cells[index]=cell;
        foreach(float rough in new[]{0f,.7f,1f})
        {
            Require(SnowSurfaceGeometry.Build(field,index%field.Columns,index/field.Columns,rough,out var patch),"mound missing");
            double topArea=SnowSurfaceGeometry.Area(patch.InnerA,patch.InnerB,patch.InnerD)+SnowSurfaceGeometry.Area(patch.InnerD,patch.InnerB,patch.InnerC);
            Require(topArea>=field.CellSize*field.CellSize*.57,"broad mound collapsed into a tip");
            Require(patch.Center.Y<=.030f*1.30f,"isolated bank still has a needle peak");
            OperatorMaterialValidation.Near(Integral(patch),patch.Volume,1e-10,"cap integral differs from actual snow");
            Console.WriteLine($"isolated roughness={rough} mean_mm=30 peak_mm={patch.Center.Y*1000:F4} cap_area_fraction={topArea/(field.CellSize*field.CellSize):F4} integral_error_m3={Integral(patch)-patch.Volume:E4}");
        }
        int previous=0;float previousPeak=0;
        foreach(float volume in new[]{.0001f,.001f,.005f})
        {
            var f=new IceField(new SimulatorConfig(),546);
            for(int i=0;i<f.Cells.Length;i++){var c=f.Cells[i];c.SnowMm=0;f.Cells[i]=c;}
            f.DepositSnow(.225f,.225f,volume,.22f);int count=0;float peak=0,min=100,max=-100;double integrated=0;
            for(int row=0;row<f.Rows;row++)for(int col=0;col<f.Columns;col++)
            {
                if(!SnowSurfaceGeometry.Build(f,col,row,.7f,out var p))continue;
                count++;peak=Math.Max(peak,p.Center.Y);min=Math.Min(min,p.A.X);max=Math.Max(max,p.D.X);integrated+=Integral(p);
            }
            Require(count>=previous&&peak>previousPeak,"more snow did not make a wider/thicker mound");
            Require(integrated<=volume+1e-8,"reconstruction invented visible volume");
            Console.WriteLine($"quantity_L={volume*1000:F3} patches={count} width_m={max-min:F4} peak_mm={peak*1000:F4} represented_L={integrated*1000:F6}");previous=count;previousPeak=peak;
        }
    }
    static void Spout()
    {
        var sample=new SnowSpoutPresentation();
        foreach(float setting in new[]{0f,1f,3f})Require(sample.Sample(0,.02f,setting)==0,"no-flow spout");
        int low=0,normal=0,high=0;
        foreach(float rate in new[]{.00005f,.0007355f,.010f})
        {
            sample.Reset();int count=0;
            for(int i=0;i<100;i++)count+=sample.Sample(rate*.02,.02f);
            if(rate<.0001f)low=count;else if(rate<.001f)normal=count;else high=count;
            Console.WriteLine($"spout delivered_Lps={rate*1000:F4} samples_2s={count}");
        }
        Require(low<normal&&normal<high,"spout does not scale with actual throughput");
        Require(sample.Sample(0,.02f)==0&&sample.Sample(.1,0)==0&&sample.Sample(0,.02f)==0,"stopped/paused delivery produced an old jet");
        Require(sample.Sample(1,.02f)==SnowSpoutPresentation.MaximumParticlesPerFrame&&sample.Sample(0,.02f)==0,"sample pool cap/backlog");
        foreach(int mode in new[]{0,1,2,3})
        {
            var s=new Simulation();var a=new OperatorMaterialValidation.Apparatus(s);s.Controls.BladeDepthMm=.35f;
            s.Controls.HorizontalAuger=(mode&1)!=0;s.Controls.VerticalAuger=(mode&2)!=0;double old=0;int emitted=0;sample.Reset();
            for(int i=0;i<200;i++)
            {
                a.Sample(0,-10+i*.1f);double current=s.TotalSnowCollectedM3;emitted+=sample.Sample(current-old,.1f);old=current;
            }
            Require(mode==3?emitted>0:emitted==0,"auger state invented bin delivery");
            Console.WriteLine($"transport_mode={mode} actual_tank_L={old*1000:F6} spout_samples={emitted}");
        }
        foreach(string block in new[]{"empty","jammed","full","raised","vertical-off"})
        {
            var s=new Simulation();s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;
            void Set(string property,object value)=>typeof(Simulation).GetProperty(property).SetValue(s,value);
            if(block!="empty")Set("VerticalSnowM3",.01f);
            if(block=="jammed")Set("ConveyorJammed",true);
            if(block=="full")Set("SnowTankM3",s.Config.SnowCapacityM3);
            if(block=="raised")Set("TankLift",.5f);
            if(block=="vertical-off")s.Controls.VerticalAuger=false;
            var convey=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),s,typeof(Simulation).GetMethod("ConveySnow",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic));
            convey(.02f);sample.Reset();int count=sample.Sample(s.TotalSnowCollectedM3,.02f);
            Require(count==0&&s.TotalSnowCollectedM3==0,"blocked transport produced a bin spout");
            Console.WriteLine($"spout_block={block} delivered_L=0 particles=0");
        }
        var timer=Stopwatch.StartNew();long bytes=GC.GetAllocatedBytesForCurrentThread();int total=0;
        for(int i=0;i<100000;i++)total+=sample.Sample(.00001,.02f);
        bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;timer.Stop();Require(bytes==0,"spout sampler allocates");
        Console.WriteLine($"spout_perf frames=100000 samples={total} ms={timer.Elapsed.TotalMilliseconds:F4} managed_bytes={bytes}");
    }
}
