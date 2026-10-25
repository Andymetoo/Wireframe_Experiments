using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TestAnxiety.Editor
{
    /// <summary>Explicit tools only: no automatic scene switching, play entry, build or settings mutation.</summary>
    public static class TestAnxietyEditorTools
    {
        public const string ScenePath = "Assets/Experiments/TestAnxiety/Scenes/TestAnxiety.unity";
        const string MaterialPath = "Assets/Experiments/TestAnxiety/Resources/TestAnxietyLit.mat";
        const string DataPath = "Assets/Experiments/TestAnxiety/Resources/TestAnxietyExam.json";

        [MenuItem("Test Anxiety/Open Prototype Scene")]
        public static void OpenScene()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) { RecreateScene(); return; }
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Test Anxiety/Recreate Prototype Scene")]
        public static void RecreateScene()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            TextAsset data = AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (data == null || material == null) throw new InvalidOperationException("Import the TestAnxiety Resources assets before recreating the scene.");
            // Validate before replacing the explicitly requested prototype scene.
            new ExamSession(JsonUtility.FromJson<ExamCatalog>(data.text));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("TEST ANXIETY / procedural classroom");
            var game = root.AddComponent<TestAnxietyGame>();
            game.litTemplate = material;
            game.examData = data;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = root;
            Debug.Log("TEST ANXIETY scene saved. Press Play to assemble the furnished classroom and begin. No build or graphics settings were changed.");
        }

        [MenuItem("Test Anxiety/Validate Catalog and Progression")]
        public static void ValidateCatalog()
        {
            TextAsset data = AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            if (data == null) throw new InvalidOperationException("Missing " + DataPath);
            ExamCatalog catalog = JsonUtility.FromJson<ExamCatalog>(data.text);
            int sections = 0;
            for (int choice = 0; choice < 4; choice++)
            {
                var session = new ExamSession(catalog);
                sections = session.SectionCount;
                bool hasDesignation = false;
                for (int section = 0; section < sections; section++)
                {
                    if (session.NeedsArchiveRecord)
                    {
                        Require(session.MarkArchiveRead(), "Read required record");
                        Require(session.CollectArchiveRecord(), "Collect required record");
                    }
                    Require(session.OpenBook(), "Open booklet");
                    for (int q = 0; q < session.CurrentSection.Questions.Count; q++)
                    {
                        ExamQuestion question = session.CurrentSection.Questions[q];
                        bool designation = question.Id == "release-choice";
                        hasDesignation |= designation;
                        session.MarkQuestionViewed(q);
                        Require(session.SetAnswer(q, designation ? choice : question.CorrectOption), "Record answer");
                    }
                    session.CloseBook();
                    session.ObserveBehind();
                    Require(session.OpenBook(), "Reopen booklet");
                    for (int q = 0; q < session.CurrentSection.Questions.Count; q++) session.MarkQuestionViewed(q);
                    session.CloseBook();
                    Require(session.BeginSubmission(), "Submit section " + section + ": " + session.SubmissionBlockReason);
                    // Bounded simulation: malformed state fails validation rather than hanging the Editor.
                    for (int tick = 0; tick < 73 && session.NeedsQuietInterval; tick++)
                        session.AdvanceQuietInterval(.25f, true);
                    Require(session.CanFinishScan, "Complete required observation interval");
                    Require(session.FinishScan(), "Finish scan");
                    Require(session.RetrieveResults(), "Retrieve paper");
                    Require(session.AdvanceAfterReview(), "Accept report");
                }
                Require(session.Stage == ExamStage.AwaitingExit, "Final exit permission");
                Require(session.CompleteAtExit(), "Complete prototype");
                if (hasDesignation) Require(session.FinalReleaseChoice == choice, "Preserve submitted designation");
            }
            Debug.Log("TEST ANXIETY: catalog and full examination progression passed (" + sections + " sections, four response paths). This check does not exercise scene visuals, collisions, input, or audio.");
        }

        static void Require(bool condition, string step)
        { if (!condition) throw new InvalidOperationException("TEST ANXIETY validation failed: " + step); }
    }
}
