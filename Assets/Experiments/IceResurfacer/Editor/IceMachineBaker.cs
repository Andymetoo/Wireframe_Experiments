using System;
using System.Collections.Generic;
using System.IO;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Bakes the one runtime builder's unbatched machine into persistent assets and an editable
    /// prefab. Explicit menu commands only; never touches the launch scene.
    /// </summary>
    public static class IceMachineBaker
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        public const string AuthoredScenePath=Root+"Scenes/IceResurfacer_AuthoredMachine.unity";
        public const string MachineFolder=Root+"Machine";
        public const string PrefabPath=MachineFolder+"/AuroraR54.prefab";
        public const string MeshAssetPath=MachineFolder+"/AuroraR54.Meshes.asset";
        public const string MaterialFolder=MachineFolder+"/Materials";
        public const string RootName="AURORA R-54 (authored)";

        [MenuItem("Experiments/Ice Resurfacer/Authored Machine/Create Experimental Scene")]
        public static void CreateExperimentalScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            OpenExperimentalScene();
        }
        public static Scene OpenExperimentalScene()
        {
            // A copy keeps the launch scene's calibration and materials; the original is never saved.
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(AuthoredScenePath)==null&&
               !AssetDatabase.CopyAsset(IceResurfacerEditor.LaunchScenePath,AuthoredScenePath))
                throw new InvalidOperationException("Could not copy the launch scene to "+AuthoredScenePath);
            return EditorSceneManager.OpenScene(AuthoredScenePath);
        }

        [MenuItem("Experiments/Ice Resurfacer/Authored Machine/Bake Machine Prefab")]
        public static void BakeMenu()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            try
            {
                var game=ExperimentalGame();
                if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null&&
                   !EditorUtility.DisplayDialog("Rebake Aurora prefab?","Rebaking regenerates "+PrefabPath+
                       " from the builder and DISCARDS every manual edit saved in it (moved controls, materials, replaced parts).",
                       "Rebake and discard edits","Cancel"))return;
                Selection.activeGameObject=Bake(game,true);
            }
            catch(Exception e) { EditorUtility.DisplayDialog("Aurora bake",e.Message,"OK"); }
        }

        static IceResurfacerGame ExperimentalGame()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=AuthoredScenePath)
                throw new InvalidOperationException("Open "+AuthoredScenePath+" first (Authored Machine > Create Experimental Scene). The launch scene is never baked into.");
            var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
            if(game==null||game.litTemplate==null)throw new InvalidOperationException("The experimental scene needs its IceResurfacerGame and Lit template.");
            return game;
        }

        /// <summary>Writes mesh, material and prefab assets, then places one instance under
        /// the experimental scene's game root and saves that scene.</summary>
        public static GameObject Bake(IceResurfacerGame game,bool overwrite)
        {
            if(game.gameObject.scene.path!=AuthoredScenePath)throw new InvalidOperationException("Bake only into "+AuthoredScenePath);
            if(!overwrite&&AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null)throw new InvalidOperationException(PrefabPath+" exists; refusing to overwrite authored edits.");
            var config=JsonUtility.FromJson<SimulatorConfig>(JsonUtility.ToJson(game.calibration));
            config.Validate();
            foreach(var old in game.GetComponentsInChildren<IceMachineRig>(true))UnityEngine.Object.DestroyImmediate(old.gameObject);

            // The working copy is never saved with the scene; only the prefab instance is.
            var temp=new GameObject(RootName);
            temp.hideFlags=HideFlags.DontSaveInEditor;
            IceWorld world=null;
            GameObject prefab;
            try
            {
                world=IceWorldBuilder.BuildMachinePreview(temp.transform,game.litTemplate,config);
                foreach(var atlas in temp.GetComponents<IceWorldTextAtlas>())UnityEngine.Object.DestroyImmediate(atlas);
                // Assembled edit-time pose: conditioner lowered on its hitch, cylinders on their pins.
                world.Conditioner.localPosition=new Vector3(0,.34f,-config.HitchBehindAxle-config.DrawbarLength);
                IcePhysicalControls.PoseCylinders(world);
                Regroup(world);
                Directory.CreateDirectory(MaterialFolder);
                PersistMaterials(temp);
                PersistMeshes(temp);
                temp.AddComponent<IceMachineRig>().RecordBake(world,config);
                temp.hideFlags=HideFlags.None;
                prefab=PrefabUtility.SaveAsPrefabAsset(temp,PrefabPath,out bool saved);
                if(!saved)throw new InvalidOperationException("Prefab save failed: "+PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
                // Builder-owned objects that were not persisted (arena palette, lettering) are discarded.
                if(world!=null)
                {
                    foreach(var mesh in world.Meshes)if(mesh!=null&&!EditorUtility.IsPersistent(mesh))UnityEngine.Object.DestroyImmediate(mesh);
                    foreach(var material in world.Materials)if(material!=null&&!EditorUtility.IsPersistent(material))UnityEngine.Object.DestroyImmediate(material);
                }
            }
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,game.gameObject.scene);
            instance.transform.SetParent(game.transform,false);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            Debug.Log("AURORA BAKE: "+PrefabPath+" with "+instance.GetComponentsInChildren<MeshRenderer>(true).Length+" selectable renderers.");
            return instance;
        }

        // Subassemblies are identity-transform groups: nothing moves, but loose parts become
        // individually selectable units. Names of parts are kept; audio and validation use them.
        static void Regroup(IceWorld w)
        {
            var machine=w.Machine;
            var conveyor=Child(machine,"Compact vertical snow conveyor / rear tank inlet");
            var guide=w.GuideRoller.parent;
            var wheels=Group("Wheels",machine);
            var hydraulics=Group("Hydraulic equipment",machine);
            var bodywork=Group("Chassis and bodywork",machine);
            foreach(var wheel in w.FrontWheels)wheel.SetParent(wheels,true);
            foreach(var wheel in w.RearWheels)wheel.SetParent(wheels,true);
            foreach(var group in new[]{w.TankCylinderBarrels,w.TankCylinderRods,w.ConditionerCylinderBarrels,w.ConditionerCylinderRods,w.TankCylinderBase,w.ConditionerCylinderBase})
                foreach(var part in group)if(part.parent==machine)part.SetParent(hydraulics,true);
            w.TankDischarge.SetParent(conveyor,true);
            var keep=new HashSet<Transform>{w.TankPivot,w.OperatorStation,w.AccessSteps,w.BrushArm,guide,conveyor,wheels,hydraulics,bodywork};
            var loose=new List<Transform>();
            foreach(Transform child in machine)if(!keep.Contains(child))loose.Add(child);
            foreach(var part in loose)
            {
                if(part.name=="Covered snow transfer throat"||part.name=="Short rear-wall delivery throat")part.SetParent(conveyor,true);
                else if(part.name=="Guide roller bracket")part.SetParent(guide,true);
                else part.SetParent(bodywork,true);
            }
        }
        static Transform Group(string name,Transform parent)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);return t;
        }
        static Transform Child(Transform parent,string name)
        {
            foreach(Transform child in parent)if(child.name==name)return child;
            throw new InvalidOperationException("Builder hierarchy changed: missing "+name);
        }

        static void PersistMaterials(GameObject root)
        {
            var saved=new Dictionary<Material,Material>();
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var text=renderer.GetComponent<TextMesh>();
                if(text!=null)
                {
                    // The depth-tested lettering material's atlas changes at runtime, so it is never
                    // an asset. Edit Mode uses the font's built-in material; Play swaps to the runtime one.
                    renderer.sharedMaterial=text.font.material;
                    continue;
                }
                var material=renderer.sharedMaterial;
                if(material==null||EditorUtility.IsPersistent(material))continue;
                if(!saved.TryGetValue(material,out var asset))
                {
                    string label=LabelOf(material);
                    // URP's first-import validation keeps _EMISSION only with an emissive GI flag (what
                    // its inspector sets); the template's EmissiveIsBlack would leave the fixture unlit.
                    if(material.IsKeywordEnabled("_EMISSION"))material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    string path=MaterialFolder+"/"+Safe(label)+".mat";
                    var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(existing!=null)
                    {
                        // Rebake keeps the asset (and its GUID) but restores the builder's values.
                        existing.CopyPropertiesFromMaterial(material);existing.shader=material.shader;
                        existing.shaderKeywords=material.shaderKeywords;existing.renderQueue=material.renderQueue;
                        existing.globalIlluminationFlags=material.globalIlluminationFlags;
                        EditorUtility.SetDirty(existing);asset=existing;
                    }
                    else { material.name=label;AssetDatabase.CreateAsset(material,path);asset=material; }
                    saved.Add(material,asset);
                }
                renderer.sharedMaterial=asset;
                // Matches the runtime batching rule for luminous fixtures.
                if(LabelOf(asset)=="Fixture luminous diffuser")renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            AssetDatabase.SaveAssets();
        }
        static string LabelOf(Material material) { return material.name.Replace("IceResurfacer / ",""); }

        static void PersistMeshes(GameObject root)
        {
            var meshes=new List<Mesh>();
            var unique=new HashSet<Mesh>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;
                if(mesh==null||EditorUtility.IsPersistent(mesh)||!unique.Add(mesh))continue;
                meshes.Add(mesh);
            }
            if(meshes.Count==0)throw new InvalidOperationException("No generated machine meshes found to persist.");
            // One container asset; rebake replaces it. Readable meshes are required by
            // RouteSilhouetteContinuation and the validation sweeps.
            if(AssetDatabase.LoadMainAssetAtPath(MeshAssetPath)!=null)AssetDatabase.DeleteAsset(MeshAssetPath);
            for(int i=0;i<meshes.Count;i++)
            {
                meshes[i].name=(i+1).ToString("000")+" "+meshes[i].name.Replace("IceResurfacer / ","");
                if(i==0)AssetDatabase.CreateAsset(meshes[i],MeshAssetPath);
                else AssetDatabase.AddObjectToAsset(meshes[i],MeshAssetPath);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MeshAssetPath);
        }
        static string Safe(string name)
        {
            foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'-');
            return name.Replace('/','-').Trim();
        }
    }
}
