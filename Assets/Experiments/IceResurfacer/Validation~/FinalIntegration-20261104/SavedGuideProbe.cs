using System;
using System.IO;
using System.Text.Json;
using IceResurfacer.Core;
internal static class SavedGuideProbe
{
    sealed class Envelope { public TrainingRouteDefinition guide; }
    sealed class RawEnvelope { public RecordedRouteData route; }
    static void Main()
    {
        var options=new JsonSerializerOptions{IncludeFields=true};int ready=0,files=0;
        foreach(string path in Directory.GetFiles("Assets/Experiments/IceResurfacer/RouteRecordings~/Guides","*.guide.json"))
        {
            byte[] original=File.ReadAllBytes(path);var d=JsonSerializer.Deserialize<Envelope>(original,options).guide;
            var raw=JsonSerializer.Deserialize<RawEnvelope>(d.rawRecordingJson,options).route;
            var prior=RouteFitter.Fit(d,raw);d.configuration=ContactDrivingValidation.Authored();d.configuration.GuideRollerX=-1.001f;
            var before=RouteFitter.Fit(d,raw);d.configuration=ContactDrivingValidation.Authored();
            var compact=RouteFitter.Fit(d,raw);files++;if(compact.Ready)ready++;
            Console.WriteLine(Path.GetFileName(path)+" / "+d.name+": saved-calibration ready="+prior.Ready+", boundary="+prior.boundaryErrors+", curvature="+prior.curvatureErrors+"; pre-integration outline ready="+before.Ready+", boundary="+before.boundaryErrors+"; compact ready="+compact.Ready+", boundary="+compact.boundaryErrors+", curvature="+compact.curvatureErrors+", source="+compact.sourceErrors+", samples="+compact.samples.Count+", length="+compact.Length.ToString("F3")+" m");
            if(before.Ready&&!compact.Ready||compact.boundaryErrors>before.boundaryErrors)throw new Exception("Compact guide correction invalidated a previously valid guide: "+d.name);
            if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original)).Equals(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))))throw new Exception("Guide source changed during read-only check.");
        }
        Console.WriteLine("PASS read-only revalidation: "+ready+"/"+files+" saved guides ready with compact calibration; all guide and embedded raw-source data preserved.");
    }
}
