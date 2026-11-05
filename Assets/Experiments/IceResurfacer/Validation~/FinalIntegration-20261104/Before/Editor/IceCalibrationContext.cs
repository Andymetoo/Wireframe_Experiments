using System;
using System.Collections.Generic;
using System.IO;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Which scene calibration an Inspector measures an authored part against. In a scene that is the
    /// IceResurfacerGame above it. In Prefab Mode (no game above the prefab root) it is the calibration
    /// of the scene that uses the prefab: the loaded scene when it is open, otherwise the saved authored
    /// scene file, read without opening it. Only when no scene uses the prefab does it fall back to the
    /// configuration captured at bake time, and then it says so: those differences may be false positives.
    /// </summary>
    public static class IceCalibrationContext
    {
        public enum Source { AuthoredScene, ProceduralScene, OtherScene, AuthoredSceneForPrefab, SavedAuthoredSceneForPrefab, PrefabFallback }

        public sealed class Context
        {
            public SimulatorConfig Config;
            public Source Source;
            public string Label;
            /// <summary>The live game whose calibration an explicit Apply/Transfer may write; null when read-only.</summary>
            public IceResurfacerGame Game;
            public bool Writable { get { return Game!=null&&(Source==Source.AuthoredScene||Source==Source.ProceduralScene||Source==Source.OtherScene); } }
            public bool Trustworthy { get { return Source!=Source.PrefabFallback; } }
            public MessageType Message { get { return Source==Source.PrefabFallback?MessageType.Warning:MessageType.Info; } }
        }

        static string cachedPath; static DateTime cachedTime; static string cachedPrefab; static string cachedJson;

        public static Context Resolve(Component part,string bakedConfigJson=null)
        {
            var game=part!=null?part.GetComponentInParent<IceResurfacerGame>(true):null;
            if(game!=null)
            {
                string path=game.gameObject.scene.path;
                var source=path==IceMachineBaker.AuthoredScenePath?Source.AuthoredScene:path==IceResurfacerEditor.LaunchScenePath?Source.ProceduralScene:Source.OtherScene;
                string name=string.IsNullOrEmpty(path)?game.gameObject.scene.name:Path.GetFileNameWithoutExtension(path);
                return new Context{Config=game.calibration,Source=source,Game=game,
                    Label=(source==Source.AuthoredScene?"Authored scene calibration":source==Source.ProceduralScene?"Original procedural scene calibration":"Scene calibration")+" ("+name+")"};
            }
            string prefab=PrefabPath(part);
            if(!string.IsNullOrEmpty(prefab))
            {
                // A loaded scene that holds this prefab: its live calibration, including unsaved edits.
                for(int i=0;i<SceneManager.sceneCount;i++)
                {
                    var scene=SceneManager.GetSceneAt(i);
                    if(!scene.isLoaded)continue;
                    foreach(var root in scene.GetRootGameObjects())
                    foreach(var candidate in root.GetComponentsInChildren<IceResurfacerGame>(true))
                        if(HoldsPrefab(candidate,prefab))
                            return new Context{Config=candidate.calibration,Source=Source.AuthoredSceneForPrefab,
                                Label="Authored scene calibration ("+scene.name+", open; Prefab Mode is read-only)"};
                }
                // The saved authored scene file.
                var saved=SavedSceneCalibration(IceMachineBaker.AuthoredScenePath,prefab);
                if(saved!=null)
                    return new Context{Config=saved,Source=Source.SavedAuthoredSceneForPrefab,
                        Label="Authored scene calibration ("+Path.GetFileNameWithoutExtension(IceMachineBaker.AuthoredScenePath)+", saved file; Prefab Mode is read-only)"};
            }
            var fallback=string.IsNullOrEmpty(bakedConfigJson)?new SimulatorConfig():JsonUtility.FromJson<SimulatorConfig>(bakedConfigJson);
            return new Context{Config=fallback,Source=Source.PrefabFallback,
                Label="Prefab-only fallback: configuration captured when the machine was baked. No scene using this prefab was found, so differences listed here may be false positives."};
        }

        /// <summary>The prefab asset a part belongs to: the open Prefab Mode stage, or a prefab asset itself.</summary>
        static string PrefabPath(Component part)
        {
            if(part==null)return null;
            var stage=PrefabStageUtility.GetPrefabStage(part.gameObject);
            if(stage!=null)return stage.assetPath;
            if(PrefabUtility.IsPartOfPrefabAsset(part))return AssetDatabase.GetAssetPath(part.gameObject);
            return null;
        }

        static bool HoldsPrefab(IceResurfacerGame game,string prefab)
        {
            foreach(Transform child in game.GetComponentsInChildren<Transform>(true))
                if(PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject)==prefab)return true;
            return false;
        }

        /// <summary>Calibration of the saved scene at path when it holds the prefab; cached by file time.</summary>
        static SimulatorConfig SavedSceneCalibration(string path,string prefab)
        {
            if(!File.Exists(path))return null;
            var time=File.GetLastWriteTimeUtc(path);
            if(cachedPath!=path||cachedTime!=time||cachedPrefab!=prefab)
            {
                cachedPath=path;cachedTime=time;cachedPrefab=prefab;cachedJson=null;
                Scene preview=default;
                try
                {
                    preview=EditorSceneManager.OpenPreviewScene(path);
                    foreach(var root in preview.GetRootGameObjects())
                    foreach(var game in root.GetComponentsInChildren<IceResurfacerGame>(true))
                        if(cachedJson==null&&HoldsPrefab(game,prefab))cachedJson=JsonUtility.ToJson(game.calibration);
                }
                catch(Exception e){Debug.LogWarning("Could not read the calibration of "+path+": "+e.Message);}
                finally{if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);}
            }
            return cachedJson==null?null:JsonUtility.FromJson<SimulatorConfig>(cachedJson);
        }

        public static void DrawLabel(Context context)
        {
            EditorGUILayout.HelpBox("Calibration source: "+context.Label,context.Message);
        }
    }
}
