using System;
using System.IO;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace IceResurfacer.Editor
{
    public static class IcePerformanceBuild
    {
        public static void Build()
        {
            try
            {
                if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))throw new InvalidOperationException("Use isolated validation project.");
                EditorSceneManager.OpenScene(IceResurfacerEditor.ScenePath);
                new GameObject("Explicit benchmark only").AddComponent<IcePerformanceProbe>();
                string scene="Assets/Experiments/IceResurfacer/Scenes/PerformancePlayer.unity";
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),scene);
                PlayerSettings.companyName="Aurora Prototype Validation";PlayerSettings.productName="Aurora Performance Validation";
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
                PlayerSettings.enableFrameTimingStats=true;PlayerSettings.runInBackground=true;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
                string output=Path.GetFullPath("../PerformanceBuild/AuroraPerformance.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                EditorApplication.Exit(result.summary.result==BuildResult.Succeeded?0:1);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
