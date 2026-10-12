using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace VectorCabinet.Editor
{
    /// <summary>Expansion builds deliberately leave the last released cabinet untouched.</summary>
    public static class SquadronBuild
    {
        [MenuItem("Vector Cabinet/Squadron/Validate All Games")]
        public static void Validate()
        {
            var report = new StringBuilder(); int checks = 0, failures = 0;
            Action<bool,string> check = (pass,name) => { checks++; if(!pass)failures++; report.AppendLine((pass?"PASS ":"FAIL ")+name.TrimEnd()); };
            Meridian.AirValidation.Run(check);
            report.AppendLine(Meridian.AirValidation.SortieSummary);
            ColdSnap.HockeyValidation.Run(check);
            report.AppendLine(ColdSnap.HockeyValidation.MatchSummary);
            for(int seed=0;seed<1000;seed++)
            {
                var errors=HollowHouse.ScenarioValidator.Validate(HollowHouse.ScenarioGenerator.Generate(seed));
                errors.AddRange(HollowHouse.SessionValidation.ValidateSeed(seed));
                check(errors.Count==0,"Haunted House complete seeded investigation "+seed+ (errors.Count>0?": "+string.Join("; ",errors):""));
            }
            report.AppendLine(checks+" checks; "+failures+" failures.");
            Directory.CreateDirectory("Documentation");
            File.WriteAllText("Documentation/squadron-unity-validation.txt",report.ToString());
            if(failures>0)throw new Exception("Squadron validation: "+failures+" failures");
            UnityEngine.Debug.Log("Squadron validation: "+checks+" checks, no failures.");
        }

        [MenuItem("Vector Cabinet/Squadron/Build Separate Windows Player")]
        public static void BuildWindows()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before building.");
            Directory.CreateDirectory("Builds/VectorCabinetSquadron");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=CabinetEditorTools.Scenes,
                locationPathName="Builds/VectorCabinetSquadron/VectorCabinet.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            string summary=report.summary.result+"\n"+report.summary.totalErrors+" errors\n"+report.summary.totalWarnings+" warnings\n"
                +report.summary.totalSize+" bytes\n"+string.Join("\n",CabinetEditorTools.Scenes)+"\n"+DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory("Documentation");
            File.WriteAllText("Documentation/squadron-build-result.txt",summary);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Squadron build failed: "+report.summary.result);
        }
        public static void ValidateAndBuild(){Validate();BuildWindows();}
    }
}
