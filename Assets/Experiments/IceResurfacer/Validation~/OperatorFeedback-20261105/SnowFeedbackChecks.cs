using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Diagnostics;
using IceResurfacer.Core;
using IceResurfacer.Editor;

static class SnowFeedbackChecks
{
    static bool baseline;
    static void Main(string[] args)
    {
        baseline=Array.IndexOf(args,"--baseline")>=0;
        foreach(string file in new[]{"scene-config.json","procedural-config.json"})
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,file)));
            var json=new JsonSerializerOptions{IncludeFields=true};
            Func<SimulatorConfig> config=()=>
            {
                var basis=JsonSerializer.Deserialize<SimulatorConfig>(doc.RootElement.GetProperty("calibration").GetRawText(),json);
                var target=JsonSerializer.Deserialize<SimulatorConfig>(doc.RootElement.GetProperty("calibration").GetRawText(),json);
                if(doc.RootElement.TryGetProperty("tuning",out var t))JsonSerializer.Deserialize<OperatorTuningSettings>(t.GetRawText(),json).Apply(target,basis);
                return target;
            };
            Console.WriteLine("CURRENT SCENE "+file+" sha256="+doc.RootElement.GetProperty("scene_sha256").GetString());
            DumpReturn(config(),file);
            if(!baseline)DumpReturn(config(),file+" full-bin",3.54f);
            if(!baseline)foreach(string state in new[]{"running","stopped","vertical-off","full","jammed","raised"})ResistanceAndStates(config(),state);
        }
        if(!baseline)ProfileAndCollision();
        Console.WriteLine("SNOW FEEDBACK CHECKS "+(baseline?"BASELINE MEASURED":"PASSED"));
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Set(Simulation s,string name,object value)=>typeof(Simulation).GetProperty(name).GetSetMethod(true).Invoke(s,new[]{value});
    static void Pose(Simulation s,float x,float z)
    {
        s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=0;s.Vehicle.Speed=0;s.Vehicle.SteeringAngle=0;
        s.Conditioner.X=x;s.Conditioner.Z=z-s.Config.HitchBehindAxle-s.Config.DrawbarLength;s.Conditioner.Heading=0;
    }
    static void Run(Simulation s,float seconds){for(int i=0;i<(int)(seconds/.02f+.5f);i++)s.Step(.02f);}
    static void Balance(Simulation s,double initial)
    {
        double residual=OperatorMaterialValidation.SnowStock(s)-initial-s.TotalExpandedShavingsM3;
        Require(Math.Abs(residual)<.00007,"volume imbalance "+residual);
    }
    static void DumpReturn(SimulatorConfig config,string label,float initialTank=1)
    {
        var s=new Simulation(config);Pose(s,0,-12);
        // Declared initial loaded-tank fixture. Every subsequent transfer and
        // position change uses the ordinary controls and fixed-step simulation.
        Set(s,"SnowTankM3",initialTank);double initial=OperatorMaterialValidation.SnowStock(s);
        s.Controls.Throttle=.4f;Run(s,8);s.Controls.Throttle=0;s.Controls.Brake=true;Run(s,3);
        s.Controls.TankLiftTarget=1;Run(s,20);
        float peak=0;foreach(var cell in s.Ice.Cells)peak=Math.Max(peak,cell.SnowMm);
        float dumped=s.DumpSnowOnIceM3;float landing=s.DumpLandingZ;
        s.Controls.TankLiftTarget=0;Run(s,7);Require(!s.TankRaised,"tank did not stow");
        s.Controls.Brake=false;s.Controls.Throttle=-.5f;Run(s,12);
        s.Controls.Throttle=0;s.Controls.Brake=true;Run(s,3);
        s.Controls.ConditionerTarget=0;Run(s,3);
        s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;
        s.Controls.Brake=false;s.Controls.Throttle=.4f;
        double picked=s.TotalLooseSnowPickupM3;float collected=s.TotalSnowCollectedM3;
        float minSpeed=10,maxLoad=0;int ticks=0;
        while(ticks++<24000&&s.Conditioner.Z<landing+3)
        {
            s.Step(.02f);
            if(Math.Abs(s.Conditioner.Z-landing)<1)minSpeed=Math.Min(minSpeed,s.Vehicle.Speed);
            maxLoad=Math.Max(maxLoad,s.MachineLoad);
        }
        s.Controls.Throttle=0;s.Controls.Brake=true;Run(s,10);
        Balance(s,initial);
        double returned=s.TotalSnowCollectedM3-collected;
        Console.WriteLine($"dump_return scene={label} dumped_L={dumped*1000:F3} peak_mm={peak:F2} recovery_seconds={ticks*.02:F2} recovered_loose_L={(s.TotalLooseSnowPickupM3-picked)*1000:F3} accepted_tank_L={returned*1000:F3} min_pile_speed_mps={minSpeed:F5} max_load={maxLoad:F3} conditioner_z={s.Conditioner.Z:F3} landing_z={landing:F3} spill_L={s.SpilledSnowM3*1000:F4}");
        if(!baseline)
        {
            Require(dumped>initialTank*.97f,"dump fixture did not reach ice");Require(peak<1100,"dump remained a tall fixed-footprint column");
            Require(returned>initialTank*.45,"driven return failed to recover meaningful tank stock");
            Require(minSpeed<.15,"deep pile did not strongly oppose travel");
            Require(ticks<24000,"deep intake failed to make forward progress");
        }
    }
    static void ResistanceAndStates(SimulatorConfig config,string state)
    {
        var s=new Simulation(config);Pose(s,0,-1.1f);
        // A 400 L known loose bank under the front of the lowered conditioner.
        s.Ice.DepositSnow(0,-2,.4f,.7f);
        s.Conditioner.Lift=state=="raised"?1:0;s.Controls.ConditionerTarget=s.Conditioner.Lift;
        s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;
        if(state=="stopped")s.Controls.HorizontalAuger=s.Controls.VerticalAuger=false;
        if(state=="vertical-off")s.Controls.VerticalAuger=false;
        if(state=="full")Set(s,"SnowTankM3",config.SnowCapacityM3);
        if(state=="jammed")Set(s,"ConveyorJammed",true);
        double initial=OperatorMaterialValidation.SnowStock(s),surface=OperatorMaterialValidation.SurfaceSnow(s);
        float collected=s.TotalSnowCollectedM3;
        s.Controls.Throttle=.4f;Run(s,12);Balance(s,initial);
        float received=s.TotalSnowCollectedM3-collected;
        double remaining=OperatorMaterialValidation.SurfaceSnow(s);
        Console.WriteLine($"deep_state {state} accepted_L={received*1000:F3} remaining_surface_L={remaining*1000:F3} speed_mps={s.Vehicle.Speed:F6} x={s.Vehicle.X:F2} z={s.Vehicle.Z:F2} spill_L={s.SpilledSnowM3*1000:F3} h_L={s.HorizontalSnowM3*1000:F3} v_L={s.VerticalSnowM3*1000:F3}");
        if(state=="running")Require(received>.04,"powered deep pickup failed");
        else Require(received<.00001,"unavailable transport delivered to tank: "+state);
        if(state=="raised")Require(Math.Abs(surface-remaining)<1e-6,"raised conditioner picked up snow");
        // All states still oppose the pile, even though only powered transport
        // can deliver stock to the tank. Raised machine can slowly push across.
        if(state!="raised")Require(s.Vehicle.Speed<.15,"unavailable intake drove through deep bank: "+state);
    }
    static void ProfileAndCollision()
    {
        var s=new Simulation();
        var pour=(Func<float,float,float,float,float,float>)Delegate.CreateDelegate(typeof(Func<float,float,float,float,float,float>),s.Ice,"DepositSnowPile");
        pour(0,0,.007f,0,.6f);
        double initial=OperatorMaterialValidation.SnowStock(s);
        var watch=Stopwatch.StartNew();long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<500;i++)pour(0,0,.007f,0,.6f);
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;watch.Stop();
        double added=OperatorMaterialValidation.SnowStock(s)-initial;
        Require(Math.Abs(added-3.5)<.00002,"repeated pile pour lost volume");
        Require(allocated==0,"steady pile deposition allocated");
        Console.WriteLine($"pile_cpu 500_ticks_ms={watch.Elapsed.TotalMilliseconds:F3} mean_tick_ms={watch.Elapsed.TotalMilliseconds/500:F4} steady_allocated_bytes={allocated} deposited_m3={added:F8}");
        Pose(s,0,-4);s.Vehicle.Speed=s.Config.ForwardSpeed;s.Controls.Throttle=1;
        // Conditioner is raised: only real body/pile contact may slow this
        // deliberately fast approach. A long caller frame still uses 20 ms ticks.
        double stock=OperatorMaterialValidation.SnowStock(s);float start=s.Vehicle.Z;
        s.Step(.5f);
        float moved=s.Vehicle.Z-start;
        Console.WriteLine($"raised_contact_approach first_halfsecond_m={moved:F6} speed_mps={s.Vehicle.Speed:F6}");
        Require(moved<s.Config.ForwardSpeed*.5f&&s.Vehicle.Speed<s.Config.ForwardSpeed*.8f,"raised chassis ignored snow contact in a long frame");
        Run(s,3);
        Require(s.Vehicle.Speed<.2,"raised chassis failed to yield to deep snow");
        float loadedSpeed=s.Vehicle.Speed;
        s.Controls.Throttle=0;Run(s,5);
        Require(Math.Abs(s.Vehicle.Speed)<.001f,"zero throttle failed to stop in snow");
        Require(Math.Abs(OperatorMaterialValidation.SnowStock(s)-stock)<1e-6,"contact created/deleted stock");
        Console.WriteLine($"raised_contact first_halfsecond_m={moved:F6} speed_in_pile_mps={loadedSpeed:F6} stopped_speed_mps={s.Vehicle.Speed:F6} pickup_L={s.TotalLooseSnowPickupM3*1000:F9}");
    }
}
