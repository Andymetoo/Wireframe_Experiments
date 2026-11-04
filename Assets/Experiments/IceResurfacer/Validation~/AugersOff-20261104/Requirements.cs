using System;
using System.IO;
using System.Text.Json;
using IceResurfacer.Core;
using IceResurfacer.Editor;
static class Requirements
{
    static void Main(string[] args)
    {
        bool ledger=Array.IndexOf(args,"--ledger")>=0;
        if(!ledger){SnowCalibrationValidation.Run();OperatorSnowRealismValidation.Run(Console.WriteLine);}
        foreach(string file in new[]{"scene-config.json","procedural-config.json"})
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../../",file)));
            var json=new JsonSerializerOptions{IncludeFields=true};
            Func<SimulatorConfig> config=()=>
            {
                var basis=JsonSerializer.Deserialize<SimulatorConfig>(doc.RootElement.GetProperty("calibration").GetRawText(),json);
                var target=JsonSerializer.Deserialize<SimulatorConfig>(doc.RootElement.GetProperty("calibration").GetRawText(),json);
                if(doc.RootElement.TryGetProperty("tuning",out var t))JsonSerializer.Deserialize<OperatorTuningSettings>(t.GetRawText(),json).Apply(target,basis);
                return target;
            };
            Console.WriteLine("CURRENT SCENE "+file+" sha256="+doc.RootElement.GetProperty("scene_sha256").GetString());
            if(!ledger)OperatorSnowRealismValidation.Driven(Console.WriteLine,config(),file);
            OperatorMaterialValidation.FullSheet(config(),Console.WriteLine,true);
            OperatorMaterialValidation.FullSheet(config(),Console.WriteLine,false);
            if(!ledger)OperatorMaterialValidation.WashCut(config(),Console.WriteLine,1,true);
        }
        Console.WriteLine("ALL CURRENT-SCENE SNOW AND WATER REGRESSIONS PASSED");
    }
}
