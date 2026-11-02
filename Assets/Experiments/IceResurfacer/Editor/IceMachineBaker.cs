using System;
using System.Collections.Generic;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Generates the machine from the one runtime builder (unbatched) as persistent assets and
    /// an editable prefab. Generation only ever writes to new locations; it never overwrites
    /// the authored machine. Replacing it is IceAuthoredRecovery's job (backup + typed confirm).
    /// No menu here runs automatically; see IceAuthoredRecovery for the commands.
    /// </summary>
    public static class IceMachineBaker
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        public const string AuthoredScenePath=Root+"Scenes/IceResurfacer_AuthoredMachine.unity";
        public const string MachineFolder=Root+"Machine";
        public const string PrefabName="AuroraR54.prefab", MeshAssetName="AuroraR54.Meshes.asset";
        public const string PrefabPath=MachineFolder+"/"+PrefabName;
        public const string MeshAssetPath=MachineFolder+"/"+MeshAssetName;
        public const string MaterialFolder=MachineFolder+"/Materials";
        public const string RootName="AURORA R-54 (authored)";

        /// <summary>First-time setup only: copies the launch scene when the authored scene does
        /// not exist yet. An existing authored scene is opened, never replaced.</summary>
        public static Scene OpenOrCreateAuthoredScene()
        {
            // A copy keeps the launch scene's calibration and materials; the original is never saved.
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(AuthoredScenePath)==null&&
               !AssetDatabase.CopyAsset(IceResurfacerEditor.LaunchScenePath,AuthoredScenePath))
                throw new InvalidOperationException("Could not copy the launch scene to "+AuthoredScenePath);
            return EditorSceneManager.OpenScene(AuthoredScenePath);
        }

        public static IceResurfacerGame AuthoredGame()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=AuthoredScenePath)
                throw new InvalidOperationException("Open "+AuthoredScenePath+" first (Experiments > Ice Resurfacer > Open Authored Scene). The launch scene is never changed.");
            return SceneGame(scene);
        }
        /// <summary>The game in the active scene (authored or launch); read only.</summary>
        public static IceResurfacerGame SceneGame(Scene scene)
        {
            foreach(var root in scene.GetRootGameObjects())
            {
                var game=root.GetComponentInChildren<IceResurfacerGame>(true);
                if(game!=null)
                {
                    if(game.litTemplate==null)throw new InvalidOperationException("The scene's IceResurfacerGame needs its Lit template.");
                    return game;
                }
            }
            throw new InvalidOperationException("Open the authored (or launch) Ice Resurfacer scene first; it supplies the calibration.");
        }

        /// <summary>First-time setup: generates the machine into Machine/ and places one instance in
        /// the authored scene. Refuses when an authored machine already exists.</summary>
        public static GameObject SetUpFirstMachine(IceResurfacerGame game)
        {
            if(game.gameObject.scene.path!=AuthoredScenePath)throw new InvalidOperationException("Set up only in "+AuthoredScenePath);
            if(AssetDatabase.LoadMainAssetAtPath(PrefabPath)!=null||AssetDatabase.LoadMainAssetAtPath(MeshAssetPath)!=null)
                throw new InvalidOperationException(PrefabPath+" already exists. Generation never overwrites the authored machine; use Advanced Recovery.");
            if(game.GetComponentInChildren<IceMachineRig>(true)!=null)throw new InvalidOperationException("The authored scene already contains a machine.");
            var prefab=GenerateAssets(game,MachineFolder);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,game.gameObject.scene);
            instance.transform.SetParent(game.transform,false);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            Debug.Log("AURORA MACHINE: "+PrefabPath+" with "+instance.GetComponentsInChildren<MeshRenderer>(true).Length+" selectable renderers.");
            return instance;
        }

        /// <summary>Writes a complete machine (prefab, meshes, materials) into folder, which must not
        /// already hold one. Touches no scene and no other asset.</summary>
        public static GameObject GenerateAssets(IceResurfacerGame game,string folder)
        {
            if(AssetDatabase.LoadMainAssetAtPath(folder+"/"+PrefabName)!=null)throw new InvalidOperationException(folder+"/"+PrefabName+" exists; generation never overwrites.");
            var config=JsonUtility.FromJson<SimulatorConfig>(JsonUtility.ToJson(game.calibration));
            config.Validate();
            IceAssetPersistence.CreateFolder(folder);
            return IceAssetPersistence.InPreviewScene(RootName,temp =>
            {
                IceWorld world=null;
                try
                {
                    world=IceWorldBuilder.BuildMachinePreview(temp,game.litTemplate,config);
                    foreach(var atlas in temp.GetComponents<IceWorldTextAtlas>())UnityEngine.Object.DestroyImmediate(atlas);
                    // Assembled edit-time pose: conditioner lowered on its hitch, cylinders on their pins.
                    world.Conditioner.localPosition=new Vector3(0,.34f,-config.HitchBehindAxle-config.DrawbarLength);
                    IcePhysicalControls.PoseCylinders(world);
                    Regroup(world);
                    IceAssetPersistence.PersistMaterials(temp,folder+"/Materials");
                    IceAssetPersistence.PersistMeshes(temp,folder+"/"+MeshAssetName);
                    temp.gameObject.AddComponent<IceMachineRig>().RecordBake(world,config);
                    var prefab=PrefabUtility.SaveAsPrefabAsset(temp.gameObject,folder+"/"+PrefabName,out bool saved);
                    if(!saved)throw new InvalidOperationException("Prefab save failed: "+folder+"/"+PrefabName);
                    return prefab;
                }
                finally
                {
                    // Builder-owned objects that were not persisted (arena palette, lettering) are discarded.
                    if(world!=null)
                    {
                        foreach(var mesh in world.Meshes)if(mesh!=null&&!EditorUtility.IsPersistent(mesh))UnityEngine.Object.DestroyImmediate(mesh);
                        foreach(var material in world.Materials)if(material!=null&&!EditorUtility.IsPersistent(material))UnityEngine.Object.DestroyImmediate(material);
                    }
                }
            });
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
    }
}
