using System;
using System.IO;
using System.Text.Json;
using IceResurfacer.Core;
using IceResurfacer.Editor;
static class Water
{
    static void Main()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"scene-config.json")));
        var options=new JsonSerializerOptions{IncludeFields=true};
        var config=JsonSerializer.Deserialize<SimulatorConfig>(json.RootElement.GetProperty("calibration").GetRawText(),options);
        var tuning=JsonSerializer.Deserialize<OperatorTuningSettings>(json.RootElement.GetProperty("tuning").GetRawText(),options);
        tuning.Apply(config,config);config.Validate();
        OperatorMaterialValidation.Near(config.FloodMaxLitersPerSecond,.72,.000001,"preserved authored flood flow");
        OperatorMaterialValidation.Near(config.WashRecoveryFraction,.644,.000001,"preserved wash efficiency");
        OperatorMaterialValidation.WashCut(config,Console.WriteLine,1,true);
        Console.WriteLine("PASS current authored ten-minute wash fixture; quantities were not retuned");
    }
}
