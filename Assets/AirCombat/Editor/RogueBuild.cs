using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace VectorCabinet.Editor
{
    /// <summary>Writes a separate release; previous cabinet executables are never replaced.</summary>
    [InitializeOnLoad]
    public static class RogueBuild
    {
        static double nextCheck;
        static RogueBuild() { EditorApplication.update += CheckCommand; }
        static void CheckCommand()
        {
            if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            const string command = "Library/AfterburnBuildCommand.txt";
            if (!File.Exists(command)) return;
            string requested = File.ReadAllText(command).Trim(); File.Delete(command);
            try
            {
                if (requested == "validate-build") ValidateAndBuild();
                else if (requested == "validate") Validate();
                else if (requested == "build") BuildWindows();
                else if (requested == "report") SaveBuildReport(BuildReport.GetLatestReport());
                else throw new ArgumentException("Unknown Afterburn command: " + requested);
                File.WriteAllText("Library/AfterburnBuildResult.txt", "OK " + requested + " " + DateTime.UtcNow.ToString("O"));
            }
            catch (Exception e)
            {
                File.WriteAllText("Library/AfterburnBuildResult.txt", e.ToString());
                UnityEngine.Debug.LogException(e);
            }
        }

        [MenuItem("Vector Cabinet/Afterburn/Validate All Games")]
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
                check(errors.Count==0,"Haunted House complete seeded investigation "+seed+(errors.Count>0?": "+string.Join("; ",errors):""));
            }
            report.AppendLine(checks+" checks; "+failures+" failures.");
            Directory.CreateDirectory("Documentation");
            File.WriteAllText("Documentation/roguelite-unity-validation.txt",report.ToString());
            if(failures>0)throw new Exception("Afterburn validation: "+failures+" failures");
            UnityEngine.Debug.Log("Afterburn validation: "+checks+" checks, no failures.");
        }

        [MenuItem("Vector Cabinet/Afterburn/Build Separate Windows Player")]
        public static void BuildWindows()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before building.");
            Directory.CreateDirectory("Builds/VectorCabinetRoguelite");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=CabinetEditorTools.Scenes,
                locationPathName="Builds/VectorCabinetRoguelite/VectorCabinet.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            SaveBuildReport(report);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Afterburn build failed: "+report.summary.result);
        }
        static void SaveBuildReport(BuildReport report)
        {
            if(report==null)throw new InvalidOperationException("No completed Unity build report available.");
            string summary=report.summary.result+"\n"+report.summary.totalErrors+" errors\n"+report.summary.totalWarnings+" warnings\n"
                +report.summary.totalSize+" bytes\n"+string.Join("\n",CabinetEditorTools.Scenes)+"\n"+DateTime.UtcNow.ToString("O");
            foreach(var step in report.steps)
                foreach(var message in step.messages)
                    if(message.type==UnityEngine.LogType.Warning||message.type==UnityEngine.LogType.Error)
                        summary+="\n"+message.type+": "+message.content;
            Directory.CreateDirectory("Documentation");
            File.WriteAllText("Documentation/roguelite-build-result.txt",summary);
        }
        public static void ValidateAndBuild(){Validate();BuildWindows();}
    }
}
