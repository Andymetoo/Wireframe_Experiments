using System;
using System.Diagnostics;
using IceResurfacer.Core;
using IceResurfacer.Editor;
static class GeometryComparison
{
    static void Main()
    {
        var s=new Simulation();int index=s.Ice.IndexAt(.225f,.225f);
        var cell=s.Ice.Cells[index];cell.SnowMm=30;s.Ice.Cells[index]=cell;
        SnowSurfaceGeometry.Build(s.Ice,index%s.Ice.Columns,index/s.Ice.Columns,.7f,out var p);
        Console.WriteLine($"isolated_mean_mm=30 peak_mm={p.Center.Y*1000:F4}");
        s=new Simulation();var apparatus=new OperatorMaterialValidation.Apparatus(s);s.Controls.BladeDepthMm=.35f;for(int i=0;i<800;i++)apparatus.Sample(0,-20+(i%400)*.1f);
        for(int i=0;i<1000;i++)SnowSurfaceGeometry.Build(s.Ice,index%s.Ice.Columns,index/s.Ice.Columns,.7f,out _);
        int count=0;var timer=Stopwatch.StartNew();long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int repeat=0;repeat<40;repeat++)for(int row=0;row<s.Ice.Rows;row++)for(int col=0;col<s.Ice.Columns;col++)
            if(SnowSurfaceGeometry.Build(s.Ice,col,row,.7f,out _))count++;
        bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;timer.Stop();
        Console.WriteLine($"scans=40 patches={count} milliseconds={timer.Elapsed.TotalMilliseconds:F3} allocated_bytes={bytes}");
        // Match eight real dirty chunks rather than repeatedly scanning an
        // entire rink, which the sparse renderer never does per frame.
        var selected=new System.Collections.Generic.HashSet<int>();
        int columns=(s.Ice.Columns+39)/40;
        for(int row=0;row<s.Ice.Rows&&selected.Count<8;row++)for(int col=0;col<s.Ice.Columns&&selected.Count<8;col++)
            if(s.Ice.Cells[s.Ice.Index(col,row)].SnowMm>SnowSurfaceGeometry.MeshThresholdMm)selected.Add(row/40*columns+col/40);
        var chunks=new int[selected.Count];selected.CopyTo(chunks);count=0;timer.Restart();bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int repeat=0;repeat<200;repeat++)foreach(int chunk in chunks)
        for(int row=chunk/columns*40;row<Math.Min(s.Ice.Rows,chunk/columns*40+40);row++)
        for(int col=chunk%columns*40;col<Math.Min(s.Ice.Columns,chunk%columns*40+40);col++)
        {
#if MOUNDS
            var current=s.Ice.Cells[s.Ice.Index(col,row)];
            if(!current.IsIce||current.SnowMm<=SnowSurfaceGeometry.MeshThresholdMm)continue;
#endif
            if(SnowSurfaceGeometry.Build(s.Ice,col,row,.7f,out _))count++;
        }
        bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;timer.Stop();
        Console.WriteLine($"eight_chunk_rebuilds=200 patches={count} total_ms={timer.Elapsed.TotalMilliseconds:F3} ms_per_eight_chunks={timer.Elapsed.TotalMilliseconds/200:F4} allocated_bytes={bytes}");
    }
}
