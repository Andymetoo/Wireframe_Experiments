using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using IceResurfacer.Core;
using IceResurfacer.Editor;

static class Probe
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static SimulatorConfig Config(bool authored)
    {
        string scene=Path.Combine(AppContext.BaseDirectory,"scene-config.json");
        using var doc=JsonDocument.Parse(File.ReadAllText(scene));
        var c=JsonSerializer.Deserialize<SimulatorConfig>(doc.RootElement.GetProperty("calibration").GetRawText(),Json);
        if(authored)
        {
            var t=JsonSerializer.Deserialize<OperatorTuningSettings>(doc.RootElement.GetProperty("tuning").GetRawText(),Json);
            t.Apply(c,c);
        }
        else c=new SimulatorConfig();
        return c;
    }
    static void Main(string[] args)
    {
        foreach(bool authored in new[]{true,false})
        foreach(float depth in new[]{.15f,.35f,.8f})
        foreach(int mode in new[]{0,1,2,3})Measure(authored,depth,mode);
        foreach(bool authored in new[]{true,false})OperatorMaterialValidation.OrdinaryStreak(Config(authored),Console.WriteLine,args.Length>0);
        BareIce();
        OperatorSnowRealismValidation.Driven(Console.WriteLine,Config(true),"authored");
        Console.WriteLine("PASS onset/source/ordinary streak probes");
    }
    static void BareIce()
    {
        var s=new Simulation(Config(true));
        for(int i=0;i<s.Ice.Cells.Length;i++){var cell=s.Ice.Cells[i];cell.SnowMm=0;s.Ice.Cells[i]=cell;}
        var a=new OperatorMaterialValidation.Apparatus(s);s.Controls.BladeDepthMm=.35f;float first=-1;
        for(int n=0;n<500;n++)
        {
            a.Sample(0,-25+n*.1f);
            if(first<0&&s.SpilledSnowM3>=.0005f)first=(n+1)*.1f;
        }
        OperatorMaterialValidation.Near(OperatorMaterialValidation.SnowStock(s)-s.TotalExpandedShavingsM3,0,.00001,"bare-ice conservation");
        Console.WriteLine($"bare_ice initial_loose_L=0 substantial_m={first:F2} new_bulk_L={s.TotalExpandedShavingsM3*1000:F6} retained_L={s.HorizontalSnowM3*1000:F6} escaped_transfer_L={s.SpilledSnowM3*1000:F6}");
    }
    static void Measure(bool authored,float depth,int mode)
    {
        var s=new Simulation(Config(authored));var a=new OperatorMaterialValidation.Apparatus(s);
        double initial=OperatorMaterialValidation.SnowStock(s);float first=-1,substantial=-1;int steps=0;
        s.Controls.BladeDepthMm=depth;s.Controls.HorizontalAuger=(mode&1)!=0;s.Controls.VerticalAuger=(mode&2)!=0;
        // Fresh, separated prescribed lanes: no distance timer and no reused spill source.
        // First lane duplicates the Astra apparatus, including its rounded start.
        for(int lane=0;lane<4;lane++)for(int n=0;n<500;n++)
        {
            a.Sample(-11.4f+lane*1.9f,-25+n*.1f);steps++;
            if(first<0&&s.EscapingSnowRate>0)first=steps*.1f;
            if(substantial<0&&s.SpilledSnowM3>=.0005f&&s.EscapingSnowRate>=.00025f)substantial=steps*.1f;
        }
        double balance=OperatorMaterialValidation.SnowStock(s)-initial-s.TotalExpandedShavingsM3;
        OperatorMaterialValidation.Near(balance,0,.00005,"snow conservation");
        Console.WriteLine($"scene={(authored?"authored":"procedural")} setting={depth:F2} actual_mm={s.ActualBladeDepthMm:F3} H={s.Controls.HorizontalAuger} V={s.Controls.VerticalAuger} first_m={first:F2} substantial_m={substantial:F2} initial_L={initial*1000:F4} shaved_solid_L={s.TotalShavedIceM3*1000:F4} new_bulk_L={s.TotalExpandedShavingsM3*1000:F4} trough_L={s.HorizontalSnowM3*1000:F4} throat_L={s.VerticalSnowM3*1000:F4} tank_L={s.SnowTankM3*1000:F4} surface_L={OperatorMaterialValidation.SurfaceSnow(s)*1000:F4} spilled_transfer_L={s.SpilledSnowM3*1000:F4} residual_L={balance*1000:F6}");
    }
}
