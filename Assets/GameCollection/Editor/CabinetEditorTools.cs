using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VectorCabinet.Editor
{
    [InitializeOnLoad]
    public static class CabinetEditorTools
    {
        public static readonly string[] Scenes={"Assets/GameCollection/Scenes/Cabinet.unity","Assets/HauntedHouse/Scenes/HauntedHouse.unity","Assets/AirCombat/Scenes/Meridian1944.unity"};
        static double nextCheck;
        static CabinetEditorTools()
        {
            EditorApplication.update+=CheckCommand;
            CompilationPipeline.assemblyCompilationFinished+=(path,messages)=>
            {
                Directory.CreateDirectory("Logs/AirCombat");
                foreach(var message in messages)
                    if(message.type==CompilerMessageType.Error)
                        File.AppendAllText("Logs/AirCombat/compile-errors.txt",message.file+":"+message.line+" "+message.message+"\n");
            };
        }
        static void CheckCommand()
        {
            if(EditorApplication.timeSinceStartup<nextCheck||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            nextCheck=EditorApplication.timeSinceStartup+.5;
            const string path="Library/CabinetCommand.txt";
            if(!File.Exists(path))return;
            string command=File.ReadAllText(path).Trim();File.Delete(path);
            try
            {
                if(command=="open")OpenCabinet();
                else if(command=="play"||command=="play-smoke")
                {
                    OpenCabinet();SessionState.SetBool("VectorCabinet.Smoke",command=="play-smoke");EditorApplication.isPlaying=true;
                    EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
                }
                else if(command=="stop")EditorApplication.isPlaying=false;
                else if(command=="validate")Validate();
                else if(command=="build")Build(false);
                else if(command=="build-dev")Build(true);
                else throw new ArgumentException("Unknown Cabinet command: "+command);
                File.WriteAllText("Library/CabinetCommandResult.txt","OK "+command+" "+DateTime.UtcNow.ToString("O"));
            }
            catch(Exception e){File.WriteAllText("Library/CabinetCommandResult.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Vector Cabinet/Open Launcher")]
        public static void OpenCabinet()
        {
            if(EditorApplication.isPlaying)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(Scenes[0]);
        }
        [MenuItem("Vector Cabinet/Validate Both Games")]
        public static void Validate()
        {
            int checks=0,failures=0;var report=new StringBuilder();
            Meridian.AirValidation.Run((pass,name)=>{checks++;if(!pass)failures++;report.AppendLine((pass?"PASS ":"FAIL ")+name);});
            for(int seed=0;seed<1000;seed++)
            {
                var errors=HollowHouse.ScenarioValidator.Validate(HollowHouse.ScenarioGenerator.Generate(seed));
                errors.AddRange(HollowHouse.SessionValidation.ValidateSeed(seed));checks++;
                foreach(var error in errors){failures++;report.AppendLine("FAIL Haunted case "+seed+": "+error);}
            }
            report.AppendLine(checks+" checks; "+failures+" failures (including 1000 Haunted House generation and complete resolution sessions).");
            Directory.CreateDirectory("Documentation");File.WriteAllText("Documentation/cabinet-unity-validation.txt",report.ToString());
            if(failures>0)throw new Exception("Cabinet validation failed: "+failures);
            Debug.Log("Vector Cabinet validation passed: "+checks+" checks.");
        }
        [MenuItem("Vector Cabinet/Build Windows Player")]
        public static void BuildRelease(){Build(false);}
        [MenuItem("Vector Cabinet/Build Development Player")]
        public static void BuildDevelopment(){Build(true);}
        public static void Build(bool development)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before building.");
            Directory.CreateDirectory("Builds/VectorCabinet");
            PlayerSettings.productName="The Vector Cabinet";PlayerSettings.companyName="Impossible Structures";
            PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            EditorBuildSettings.scenes=Array.ConvertAll(Scenes,s=>new EditorBuildSettingsScene(s,true));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=Scenes,locationPathName="Builds/VectorCabinet/VectorCabinet.exe",target=BuildTarget.StandaloneWindows64,
                options=development?BuildOptions.Development:BuildOptions.None});
            string summary=report.summary.result+"\n"+report.summary.totalErrors+" errors\n"+report.summary.totalWarnings+" warnings\n"+report.summary.totalSize+" bytes\n"+string.Join("\n",Scenes)+"\n"+DateTime.UtcNow.ToString("O");
            File.WriteAllText("Builds/cabinet-build-result.txt",summary);
            Directory.CreateDirectory("Documentation");File.WriteAllText("Documentation/cabinet-build-result.txt",summary);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Cabinet build failed: "+report.summary.result);
        }
    }
}
