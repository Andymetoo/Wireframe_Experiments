using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Buffers.Binary;
using IceResurfacer.Core;

internal static class WaterPerformanceProbe
{
    static void Main()
    {
        Console.WriteLine("Core-only reproduction of long_session (no Unity/render/UI). " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        var s=new Simulation(practice:true);Pose(s);
        var c=s.Controls;c.Throttle=.42f;s.Conditioner.Lift=0;c.ConditionerTarget=0;c.BladeDepthMm=.35f;c.HorizontalAuger=c.VerticalAuger=true;c.FloodFlow=.32f;c.WashValve=.25f;
        long ticks=0,window=0,allocated=GC.GetAllocatedBytesForCurrentThread();int count=0,peak=0;double spreadWork=0;
        for(int frame=0;frame<30090;frame++)
        {
            if(s.Vehicle.Z>15)Pose(s);
            if(frame%180==0)c.Steer=frame%360==0?.25f:-.25f;
            long begin=Stopwatch.GetTimestamp();s.Step(.02f);
            if(frame%8==0){s.GetReport();s.Ice.ClearDirtyCells();}
            long elapsed=Stopwatch.GetTimestamp()-begin;
            if(frame>90){ticks+=elapsed;window+=elapsed;count++;spreadWork+=s.Ice.LastSpreadCells;peak=Math.Max(peak,s.Ice.ActiveStandingWaterCells);}
            if(frame>90&&frame%5000==0)
            {
                Console.WriteLine("window,"+frame+",mean_step_ms="+(window*1000.0/Stopwatch.Frequency/(frame==5000?4910:5000)).ToString("F6")+",active="+s.Ice.ActiveStandingWaterCells+",redistributed="+s.Ice.RedistributedWaterLiters.ToString("F9")+",hash="+Hash(s.Ice));window=0;
            }
        }
        long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
        double water=0;foreach(var cell in s.Ice.Cells)water+=cell.FloodMm*s.Ice.CellSize*s.Ice.CellSize;
        Console.WriteLine("summary,steps="+count+",mean_step_ms="+(ticks*1000.0/Stopwatch.Frequency/count).ToString("F6")+",peak_standing="+peak+",mean_sampled_cells="+(spreadWork/count).ToString("F3")+",retained_water="+water.ToString("F9")+",redistributed="+s.Ice.RedistributedWaterLiters.ToString("F9")+",diagnostic_allocated="+bytes+",hash="+Hash(s.Ice));
    }
    static void Pose(Simulation s)
    {s.Vehicle.X=-5;s.Vehicle.Z=-15;s.Vehicle.Heading=s.Vehicle.Speed=0;s.Conditioner.Heading=0;s.Conditioner.X=-5;s.Conditioner.Z=-15-s.Config.HitchBehindAxle-s.Config.DrawbarLength;}
    static string Hash(IceField ice)
    {
        using(var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            Span<byte> record=stackalloc byte[64];
            foreach(var cell in ice.Cells)
            {
                int n=0;
                Put(record,ref n,cell.Scratch);Put(record,ref n,cell.SnowMm);Put(record,ref n,cell.CutDepthMm);Put(record,ref n,cell.WashLiters);
                Put(record,ref n,cell.FloodMm);Put(record,ref n,cell.Quality);Put(record,ref n,cell.OverwaterMm);Put(record,ref n,cell.LastWetTime);
                Put(record,ref n,cell.GougeDepthMm);Put(record,ref n,cell.LiquidMm);Put(record,ref n,cell.FrozenFloodMm);Put(record,ref n,cell.FrozenOverwaterMm);Put(record,ref n,cell.LiquidUpdateTime);
                record[n++]=(byte)(cell.IsIce?1:0);record[n++]=(byte)(cell.Covered?1:0);hash.AppendData(record.Slice(0,n));
            }
            return Convert.ToHexString(hash.GetHashAndReset());
        }
    }
    static void Put(Span<byte> bytes,ref int offset,float value){BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(offset,4),BitConverter.SingleToInt32Bits(value));offset+=4;}
}
