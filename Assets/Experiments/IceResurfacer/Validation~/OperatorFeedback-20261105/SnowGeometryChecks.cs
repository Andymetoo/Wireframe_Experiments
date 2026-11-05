using System;
using System.Diagnostics;
using IceResurfacer.Core;
static class SnowGeometryChecks
{
    static void Require(bool test,string message){if(!test)throw new Exception(message);}
    static double Integral(SnowSurfacePatch p)
    {
        double volume=0;
        for(int k=0;k<SnowSurfaceGeometry.IndicesPerPatch;k+=3)
        {
            var a=p.Point(SnowSurfaceGeometry.TriangleVertex(k));var b=p.Point(SnowSurfaceGeometry.TriangleVertex(k+1));var c=p.Point(SnowSurfaceGeometry.TriangleVertex(k+2));
            volume+=SnowSurfaceGeometry.Area(a,b,c)*(a.Y+b.Y+c.Y)/3;
        }
        return volume;
    }
    static IceField Field(Func<float,float,float> depth)
    {
        var f=new IceField(new SimulatorConfig(),546);
        for(int z=0;z<f.Rows;z++)for(int x=0;x<f.Columns;x++)
        {int i=f.Index(x,z);var c=f.Cells[i];c.SnowMm=c.IsIce?depth(f.CellX(x),f.CellZ(z))*1000:0;f.Cells[i]=c;}
        return f;
    }
    static void Main()
    {
        Plane();QuadraticMound();Conservation();SeamsAndCleanup();Performance();
        Console.WriteLine("PASS continuous snow reconstruction: flat/linear/quadratic field, exact volume, shared seams, clipped boards, cleanup, zero steady allocations");
    }
    static void Plane()
    {
        foreach(bool slope in new[]{false,true})
        {
            float Surface(float x,float z)=>1+(slope?x*.4f+z*.2f:0);
            var f=Field((x,z)=>Math.Max(0,Surface(x,z)));double before=0,after=0,maxVolume=0;
            for(int z=f.Rows/2-10;z<f.Rows/2+10;z++)for(int x=f.Columns/2-10;x<f.Columns/2+10;x++)
            {
                Require(SnowSurfaceGeometry.Build(f,x,z,.7f,out var p),"plane missing");BeforeSnowSurfaceGeometry.Build(f,x,z,.7f,out var old);
                for(int k=0;k<SnowSurfaceGeometry.VerticesPerPatch;k++)
                {var a=p.Point(k);var b=old.Point(k);after=Math.Max(after,Math.Abs(a.Y-Surface(a.X,a.Z)));before=Math.Max(before,Math.Abs(b.Y-Surface(b.X,b.Z)));}
                maxVolume=Math.Max(maxVolume,Math.Abs(Integral(p)-p.Volume));
            }
            Require(after<.000003,"reconstruction left cell ridges on a flat/linear bank");Require(before>.025,"before fixture did not reproduce cell caps");
            Console.WriteLine($"plane sloped={slope} before_max_cell_imprint_mm={before*1000:F6} after_mm={after*1000:F6} max_volume_error_m3={maxVolume:E4}");
        }
    }
    static void QuadraticMound()
    {
        var f=Field((x,z)=>Math.Max(0,1-(x*x+z*z)/2));double maxShift=0,maxVolume=0;int sloped=0;double represented=0;
        for(int z=f.Rows/2-20;z<f.Rows/2+20;z++)for(int x=f.Columns/2-20;x<f.Columns/2+20;x++)
        {
            if(!SnowSurfaceGeometry.Build(f,x,z,.7f,out var p))continue;
            if(Math.Abs(f.CellX(x))<.5&&Math.Abs(f.CellZ(z))<.5)
            {
                double bilinear=(p.A.Y+p.B.Y+p.C.Y+p.D.Y)*.25;
                maxShift=Math.Max(maxShift,Math.Abs(p.Center.Y-bilinear));
                if(Math.Abs(p.InnerA.Y-p.InnerC.Y)+Math.Abs(p.InnerB.Y-p.InnerD.Y)>.0001)sloped++;
            }
            maxVolume=Math.Max(maxVolume,Math.Abs(Integral(p)-p.Volume));represented+=Integral(p);
        }
        Console.WriteLine($"quadratic interior_sloped_patches={sloped} max_center_correction_mm={maxShift*1000:F6} max_volume_error_m3={maxVolume:E4} represented_m3={represented:F6}");
        Require(maxShift<.00001,"smooth mound still has individual raised cell caps");Require(sloped>300,"mound inner surfaces became flat columns");
    }
    static void Conservation()
    {
        var random=new Random(417);var f=Field((x,z)=>random.NextDouble()<.12?0:(float)random.NextDouble()*2);double maxError=0,maxRelative=0;int count=0;
        foreach(float rough in new[]{0f,.7f,1f})
        for(int z=0;z<f.Rows;z+=7)for(int x=0;x<f.Columns;x+=7)
        {
            if(!SnowSurfaceGeometry.Build(f,x,z,rough,out var p))continue;
            for(int k=0;k<SnowSurfaceGeometry.VerticesPerPatch;k++)
            {var a=p.Point(k);Require(a.Y>=-.000001&&!float.IsNaN(a.Y),"negative/nonfinite patch");Require(f.IsInside(a.X,a.Z,-.00002f),"facet outside boards");}
            double error=Math.Abs(Integral(p)-p.Volume);maxError=Math.Max(maxError,error);maxRelative=Math.Max(maxRelative,error/p.Volume);count++;
        }
        Require(maxRelative<.000002,"conservative reconstruction changed snow volume");
        Console.WriteLine($"random_board_patches={count} max_absolute_error_m3={maxError:E4} max_relative_error={maxRelative:E4}");
    }
    static void SeamsAndCleanup()
    {
        var f=Field((x,z)=>Math.Abs(x)<.3&&Math.Abs(z)<.3?.3f:0);int cx=f.Columns/2,cz=f.Rows/2;
        SnowSurfaceGeometry.Build(f,cx,cz,.7f,out var a);SnowSurfaceGeometry.Build(f,cx+1,cz,.7f,out var b);
        Require(a.D.Y==b.A.Y&&a.C.Y==b.B.Y,"shared heights disagree across cells");
        int i=f.Index(cx,cz);var c=f.Cells[i];c.SnowMm=0;f.Cells[i]=c;
        Require(!SnowSurfaceGeometry.Build(f,cx,cz,.7f,out _),"cleared cell owns triangles");SnowSurfaceGeometry.Build(f,cx+1,cz,.7f,out b);
        Require(b.A.Y==0&&b.B.Y==0,"neighbor surface spans cleared snow cell");
        Console.WriteLine("shared seam exact; cleared cell no triangles; adjacent shared edge returns to ice");
    }
    static void Performance()
    {
        var f=Field((x,z)=>Math.Abs(x)<1&&Math.Abs(z)<1?1-(x*x+z*z)*.2f:0);int cx=f.Columns/2,cz=f.Rows/2;
        for(int i=0;i<100;i++)SnowSurfaceGeometry.Build(f,cx,cz,.7f,out _);
        var timer=Stopwatch.StartNew();long bytes=GC.GetAllocatedBytesForCurrentThread();int n=0;
        for(int pass=0;pass<100;pass++)for(int z=cz-20;z<cz+20;z++)for(int x=cx-20;x<cx+20;x++)if(SnowSurfaceGeometry.Build(f,x,z,.7f,out _))n++;
        bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;timer.Stop();Require(bytes==0,"geometry allocations");
        Console.WriteLine($"performance patches={n} ms={timer.Elapsed.TotalMilliseconds:F3} steady_managed_bytes={bytes}");
    }
}
