using System;
using IceResurfacer.Core;
using IceResurfacer.Editor;

static class Measure
{
    static void Main(string[] args)
    {
        bool tuned=Array.IndexOf(args,"--tuned")>=0;
        Func<SimulatorConfig> config=()=>new SimulatorConfig{FloodMaxLitersPerSecond=tuned?.72f:1.8f,
            WashRecoveryFraction=tuned?.644f:.82f,LooseSnowPickupDepthMm=tuned?50:6};
        var c=config();
        if(args.Length>1&&args[0]=="--scene")
        {
            var json=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(args[1]));
            var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
            Func<SimulatorConfig> authored=()=>System.Text.Json.JsonSerializer.Deserialize<SimulatorConfig>(json.RootElement.GetProperty("calibration").GetRawText(),options);
            var settings=System.Text.Json.JsonSerializer.Deserialize<OperatorTuningSettings>(json.RootElement.GetProperty("tuning").GetRawText(),options);
            var basis=authored();basis.Validate();var target=authored();settings.Apply(target,basis);target.Validate();
            SettingsTests(basis,settings,authored);
            OperatorMaterialValidation.WashTreatmentResponse(()=>{var result=authored();settings.Apply(result,basis);return result;},Console.WriteLine);
            if(Array.IndexOf(args,"--quick")>=0)return;
            Console.WriteLine("ACTUAL AUTHORED CALIBRATION");
            OperatorMaterialValidation.WashCut(authored(),Console.WriteLine,1,true);
            OperatorMaterialValidation.WashCut(target,Console.WriteLine,1,true);
            OperatorMaterialValidation.WashCut(target,Console.WriteLine,.5f,true);
            OperatorMaterialValidation.OrdinaryStreak(authored(),Console.WriteLine,false);
            OperatorMaterialValidation.OrdinaryStreak(target,Console.WriteLine,true);
            OperatorMaterialValidation.FullSheet(authored(),Console.WriteLine,true);
            OperatorMaterialValidation.FullSheet(target,Console.WriteLine,true);
            OperatorMaterialValidation.FullSheet(target,Console.WriteLine,false);
            var expanded=authored();settings.ShavingsExpansionMultiplier=2.5f;settings.Apply(expanded,basis);
            OperatorMaterialValidation.FullSheet(expanded,Console.WriteLine,true);
            OperatorMaterialValidation.DrivenCut(new SimulatorConfig(),Console.WriteLine,"procedural");
            OperatorMaterialValidation.DrivenCut(authored(),Console.WriteLine,"authored_before");
            OperatorMaterialValidation.DrivenCut(target,Console.WriteLine,"authored_tuned");
            Console.WriteLine("ALL SCENE MATERIAL TESTS PASSED");return;
        }
        if(args.Length>0&&args[0]=="--calibrate")
        {
            foreach(float r in new[]{.5f,.6f,.7f,.8f,.82f,.85f,.88f,.9f})
            {var test=config();test.FloodMaxLitersPerSecond=.72f;test.WashRecoveryFraction=r;OperatorMaterialValidation.WashCut(test,Console.WriteLine,1,true);}
            return;
        }
        if(args.Length==2&&args[0]=="--recovery")
        {c.WashRecoveryFraction=float.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture);OperatorMaterialValidation.WashCut(c,Console.WriteLine);return;}
        Console.WriteLine("mode="+(tuned?"tuned":"baseline")+" flood_full_Lps="+c.FloodMaxLitersPerSecond+
            " flood_duration_s="+c.FloodCapacityLiters/c.FloodMaxLitersPerSecond);
        OperatorMaterialValidation.WashCut(config(),Console.WriteLine);
        if(Array.IndexOf(args,"--wash-only")>=0)return;
        OperatorMaterialValidation.WashCut(config(),Console.WriteLine,.5f);
        OperatorMaterialValidation.WashCut(config(),Console.WriteLine,1,true);
        OperatorMaterialValidation.WashCut(config(),Console.WriteLine,1,false,false);
        OperatorMaterialValidation.OrdinaryStreak(config(),Console.WriteLine,tuned);
        OperatorMaterialValidation.FullSheet(config(),Console.WriteLine,true);
        OperatorMaterialValidation.FullSheet(config(),Console.WriteLine,false);
    }
    static void SettingsTests(SimulatorConfig basis,OperatorTuningSettings settings,Func<SimulatorConfig> copy)
    {
        foreach(var field in typeof(OperatorTuningSettings).GetFields())
        foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1,100})
        {
            var bad=OperatorTuningSettings.From(basis);field.SetValue(bad,value);bool rejected=false;
            try{bad.Validate();}catch(ArgumentException){rejected=true;}
            OperatorMaterialValidation.Require(rejected,"invalid tuning accepted: "+field.Name);
        }
        var c=copy();settings.Apply(c,basis);float flow=c.FloodMaxLitersPerSecond;
        settings.Apply(c,basis);OperatorMaterialValidation.Near(c.FloodMaxLitersPerSecond,flow,0,"compounding tuning");
        OperatorMaterialValidation.Require(new RouteFit{configuration=basis}.Compatible(c),"material tuning changed strict GPS geometry compatibility");
        OperatorMaterialValidation.Require(c.FloodCapacityLiters==basis.FloodCapacityLiters&&c.WashCapacityLiters==basis.WashCapacityLiters&&c.SnowCapacityM3==basis.SnowCapacityM3,"capacity changed");
        OperatorTuningSettings.Restore(c,basis);
        foreach(var field in typeof(SimulatorConfig).GetFields())
        {
            if(field.FieldType==typeof(float))OperatorMaterialValidation.Near((float)field.GetValue(c),(float)field.GetValue(basis),0,"restore "+field.Name);
        }
        var mixed=new Simulation(copy());var apparatus=new OperatorMaterialValidation.Apparatus(mixed);
        double original=OperatorMaterialValidation.SnowStock(mixed);
        mixed.Controls.HorizontalAuger=mixed.Controls.VerticalAuger=true;mixed.Controls.BladeDepthMm=.35f;
        apparatus.Line(0,-5,5);apparatus.Drain();float before=mixed.SnowTankM3;
        var changed=OperatorTuningSettings.From(basis);changed.ShavingsExpansionMultiplier=2.5f;changed.Apply(mixed.Config,basis);
        OperatorMaterialValidation.Near(mixed.SnowTankM3,before,0,"expansion rescaled existing stock");
        apparatus.Line(3,-5,5);apparatus.Drain();
        OperatorMaterialValidation.Near(OperatorMaterialValidation.SnowStock(mixed),original+mixed.TotalExpandedShavingsM3,.00001,"mixed expansion conservation");
        Console.WriteLine("PASS tuning bounds/nonfinite rejection, noncompounding application, fixed tank capacities, exact material-setting restore, unchanged strict GPS compatibility and live mixed-expansion conservation");
    }
}
