using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HollowHouse.Editor
{
    /// <summary>Explicit editor/build entry points and a project-local automation mailbox.</summary>
    [InitializeOnLoad]
    public static class HouseEditorTools
    {
        const string ScenePath = "Assets/HauntedHouse/Scenes/HauntedHouse.unity";
        static double nextCheck;
        static HouseEditorTools() { EditorApplication.update += CheckCommand; }

        static void CheckCommand()
        {
            if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            if(EditorApplication.isPlaying && SessionState.GetBool("HouseSmokeRequested",false))
            {
                SessionState.SetBool("HouseSmokeRequested",false);
                var gameView=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
                gameView.Focus();
                RuntimeSmoke.RunInEditor(SessionState.GetBool("HouseSmokeMapOnly",false));
            }
            const string path = "Library/HouseCommand.txt";
            if (!File.Exists(path)) return;
            string command = File.ReadAllText(path).Trim();
            File.Delete(path);
            try
            {
                if (command == "validate") Validate();
                else if (command == "build") BuildWindows();
                else if (command == "build-dev") BuildDevelopment();
                else if (command == "open") OpenScene();
                else if (command == "play") { OpenScene(); EditorApplication.isPlaying = true; }
                else if (command == "play-smoke" || command == "play-map")
                { OpenScene(); SessionState.SetBool("HouseSmokeMapOnly",command=="play-map"); SessionState.SetBool("HouseSmokeRequested",true); EditorApplication.isPlaying = true; }
                else if (command == "stop") EditorApplication.isPlaying = false;
                else throw new ArgumentException("Unknown House command: "+command);
                File.WriteAllText("Library/HouseCommandResult.txt", "OK " + command + " " + DateTime.UtcNow.ToString("O"));
            }
            catch(Exception e) { File.WriteAllText("Library/HouseCommandResult.txt", e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Haunted House/Open Game Scene")]
        public static void OpenScene()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Haunted House/Validate 1000 Cases")]
        public static void Validate()
        {
            int errors=0;
            for(int seed=0;seed<1000;seed++)
            {
                var failures=ScenarioValidator.Validate(ScenarioGenerator.Generate(seed));
                failures.AddRange(SessionValidation.ValidateSeed(seed));
                foreach(var failure in failures) {errors++; Debug.LogError("Case "+seed+": "+failure);}
            }
            if(errors!=0) throw new Exception("Scenario validation failed: "+errors+" errors.");
            Debug.Log("Haunted House: 1000 deterministic cases passed structural, evidence and full simulation validation.");
        }

        [MenuItem("Haunted House/Build Windows Player")]
        public static void BuildWindows()
        { BuildPlayer(false); }

        [MenuItem("Haunted House/Build Development Player")]
        public static void BuildDevelopment()
        { BuildPlayer(true); }

        static void BuildPlayer(bool development)
        {
            VectorCabinet.Editor.CabinetEditorTools.Build(development);
        }

        public static void ValidateAndBuild() { Validate(); BuildWindows(); }
    }
}
