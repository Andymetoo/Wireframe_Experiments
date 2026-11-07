using System;
using System.Collections.Generic;
using System.IO;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Generates the arena from the one runtime builder (plus IceWaterService's fixed station)
    /// as persistent section prefabs nested in one arena prefab. Each section owns its prefab
    /// and mesh container, so one section can be regenerated without touching the others.
    /// Generation only writes to new locations; it never overwrites authored assets.
    /// </summary>
    public static class IceArenaBaker
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        public const string ArenaFolder=Root+"Arena";
        public const string PrefabName="NorthlineArena.prefab";
        public const string PrefabPath=ArenaFolder+"/"+PrefabName;
        public const string MaterialFolder=ArenaFolder+"/Materials";
        public const string RootName="NORTHLINE ICE CENTRE / enclosed arena";
        public const string StationSection="Southeast service wall / right of nose-in parked machine";

        /// <summary>Sections in hierarchy order; names are the builder's own group names.</summary>
        public static readonly string[] Sections =
        {
            "Building shell", "Rink boards and ice sheet", "Tiered spectator seating", "Steel trusses and arena lighting",
            "Southeast diagonal portal / backstage maintenance room", "Parked goals", "Banners and signage",
            "Centre suspended scoreboard", StationSection
        };
        public static string SectionPrefabPath(string folder,string section) { return folder+"/Sections/"+IceAssetPersistence.Safe(section)+".prefab"; }
        public static string SectionMeshPath(string folder,string section) { return folder+"/Sections/"+IceAssetPersistence.Safe(section)+".Meshes.asset"; }

        /// <summary>First-time setup: generates the arena into Arena/ and places one instance in the
        /// authored scene (after a verified scene backup). Refuses when an authored arena exists.</summary>
        public static GameObject SetUpFirstArena(IceResurfacerGame game)
        {
            var scene=game.gameObject.scene;
            if(scene.path!=IceMachineBaker.AuthoredScenePath)throw new InvalidOperationException("Set up only in "+IceMachineBaker.AuthoredScenePath+"; the launch scene stays procedural.");
            if(AssetDatabase.IsValidFolder(ArenaFolder))throw new InvalidOperationException(ArenaFolder+" already exists. Generation never overwrites the authored arena; use Advanced Recovery.");
            if(game.GetComponentInChildren<IceArenaRig>(true)!=null)throw new InvalidOperationException("The authored scene already contains an arena.");
            if(scene.isDirty)throw new InvalidOperationException("Save the authored scene first, so the backup matches what is on disk.");
            string backup=IceAuthoredRecovery.BackUp("Authored scene before arena setup",new[]{IceMachineBaker.AuthoredScenePath});
            var prefab=Generate(game,ArenaFolder,null);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            instance.transform.SetParent(game.transform,false);
            // Edit Mode shows the lighting Play applies (IceResurfacerGame.Awake sets and restores
            // these at runtime); they are this scene's own settings, not project settings.
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(214f/255f,228f/255f,245f/255f); RenderSettings.ambientIntensity=1;
            RenderSettings.fog=true; RenderSettings.fogColor=new Color(.10f,.15f,.20f); RenderSettings.fogDensity=.0035f;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("NORTHLINE ARENA: "+PrefabPath+" with "+instance.GetComponentsInChildren<Renderer>(true).Length+" selectable renderers and "+
                instance.GetComponentsInChildren<Light>(true).Length+" lights. Scene backup: "+backup);
            return instance;
        }

        /// <summary>Writes a complete arena (section prefabs, meshes, materials, root prefab) into
        /// folder, reusing materials from reuseMaterials by name without changing them.</summary>
        public static GameObject Generate(IceResurfacerGame game,string folder,string reuseMaterials)
        {
            if(AssetDatabase.LoadMainAssetAtPath(folder+"/"+PrefabName)!=null)throw new InvalidOperationException(folder+"/"+PrefabName+" exists; generation never overwrites.");
            return Build(game,(arena,config) =>
            {
                IceAssetPersistence.PersistMaterials(arena,folder+"/Materials",reuseMaterials);
                IceAssetPersistence.CreateFolder(folder+"/Sections");
                var root=new GameObject(RootName);
                root.transform.SetParent(arena.parent,false);
                foreach(string section in Sections)
                {
                    var prefab=SaveSection(Child(arena,section),folder,section);
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.scene);
                    instance.transform.SetParent(root.transform,false);
                }
                root.AddComponent<IceArenaRig>().RecordBake(config);
                var saved=PrefabUtility.SaveAsPrefabAsset(root,folder+"/"+PrefabName,out bool ok);
                if(!ok)throw new InvalidOperationException("Arena prefab save failed: "+folder+"/"+PrefabName);
                return saved;
            });
        }

        /// <summary>Writes one freshly generated section into folder (its prefab and meshes). Its
        /// materials resolve to reuseMaterials by name; missing ones are created in folder/Materials.</summary>
        public static GameObject GenerateSection(IceResurfacerGame game,string section,string folder,string reuseMaterials)
        {
            if(Array.IndexOf(Sections,section)<0)throw new ArgumentException("Unknown arena section: "+section);
            return Build(game,(arena,config) =>
            {
                var part=Child(arena,section);
                IceAssetPersistence.PersistMaterials(part,folder+"/Materials",reuseMaterials);
                IceAssetPersistence.CreateFolder(folder+"/Sections");
                return SaveSection(part,folder,section);
            });
        }

        static GameObject Build(IceResurfacerGame game,Func<Transform,SimulatorConfig,GameObject> persist)
        {
            var config=JsonUtility.FromJson<SimulatorConfig>(JsonUtility.ToJson(game.calibration));
            config.Validate();
            return IceAssetPersistence.InPreviewScene("Arena generation",temp =>
            {
                IceWorld world=null;GameObject host=null;var stationMaterials=new List<Material>();
                try
                {
                    world=IceWorldBuilder.BuildArenaPreview(temp,game.litTemplate,config);
                    foreach(var atlas in temp.GetComponents<IceWorldTextAtlas>())UnityEngine.Object.DestroyImmediate(atlas);
                    var arena=Child(temp,RootName);
                    // The fixed water station comes from IceWaterService's own build code.
                    host=new GameObject("Water station generation"){hideFlags=HideFlags.HideAndDontSave};
                    var station=host.AddComponent<IceWaterService>().BuildFixedStation(game,arena,stationMaterials);
                    // Parity with the runtime station, which was never batched.
                    station.gameObject.AddComponent<IceKeepUnbatched>();
                    Tag(arena,world);
                    var names=new List<string>();foreach(Transform child in arena)names.Add(child.name);
                    if(string.Join("|",names)!=string.Join("|",Sections))
                        throw new InvalidOperationException("Builder arena sections changed: "+string.Join(", ",names));
                    string json=JsonUtility.ToJson(config);
                    foreach(var part in arena.GetComponentsInChildren<IceArenaPart>(true))part.RecordBake(json);
                    return persist(arena,config);
                }
                finally
                {
                    if(host!=null)UnityEngine.Object.DestroyImmediate(host);
                    foreach(var material in stationMaterials)if(material!=null&&!EditorUtility.IsPersistent(material))UnityEngine.Object.DestroyImmediate(material);
                    if(world!=null)
                    {
                        foreach(var mesh in world.Meshes)if(mesh!=null&&!EditorUtility.IsPersistent(mesh))UnityEngine.Object.DestroyImmediate(mesh);
                        foreach(var material in world.Materials)if(material!=null&&!EditorUtility.IsPersistent(material))UnityEngine.Object.DestroyImmediate(material);
                    }
                }
            });
        }

        static GameObject SaveSection(Transform section,string folder,string name)
        {
            string path=SectionPrefabPath(folder,name);
            if(AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new InvalidOperationException(path+" exists; generation never overwrites.");
            IceAssetPersistence.PersistMeshes(section,SectionMeshPath(folder,name));
            var prefab=PrefabUtility.SaveAsPrefabAsset(section.gameObject,path,out bool ok);
            if(!ok)throw new InvalidOperationException("Section prefab save failed: "+path);
            return prefab;
        }

        // Roles by the builder's own part names; counts are exact so a builder change is caught.
        static void Tag(Transform arena,IceWorld world)
        {
            var config=new Dictionary<string,(IceArenaRole role,int count)>
            {
                {"Continuous spatial ice sheet",(IceArenaRole.IceSheet,1)},
                {"White impact dasher",(IceArenaRole.RinkBoardBand,1)},{"Yellow kick strip",(IceArenaRole.RinkBoardBand,1)},
                {"Dark top rail",(IceArenaRole.RinkBoardBand,1)},{"Continuous safety glass",(IceArenaRole.RinkBoardBand,1)},
                {"Southeast diagonal portal / backstage maintenance room",(IceArenaRole.ServiceBay,1)},
                {"Machine room concrete apron",(IceArenaRole.ServiceFloor,1)},
                {"Machine room side wall",(IceArenaRole.ServiceSideWall,2)},{"Machine room rear wall",(IceArenaRole.ServiceRearWall,1)},
                {"Portal front return wall",(IceArenaRole.PortalReturnWall,2)},
                {"Gate opened along portal jamb",(IceArenaRole.GateJamb,2)},{"Portal upright",(IceArenaRole.GateJamb,2)},
                {"Dark slots under traversable dump grate",(IceArenaRole.SnowPit,1)},
                {"Accumulating dumped snow / physical pit",(IceArenaRole.PitSnow,1)},
                {"Arena pool light",(IceArenaRole.ArenaLight,8)},
                {StationSection,(IceArenaRole.WaterStation,1)},
                {"Wall supply valve 90 degree pivot",(IceArenaRole.SupplyValveHandle,1)},
                {"Forgiving nozzle holder highlight",(IceArenaRole.NozzleHolderHighlight,1)},
            };
            var found=new Dictionary<string,int>();
            foreach(var t in arena.GetComponentsInChildren<Transform>(true))
            {
                if(!config.TryGetValue(t.name,out var entry))continue;
                t.gameObject.AddComponent<IceArenaPart>().role=entry.role;
                found.TryGetValue(t.name,out int n);found[t.name]=n+1;
            }
            foreach(var entry in config)
                if(!found.TryGetValue(entry.Key,out int n)||n!=entry.Value.count)
                    throw new InvalidOperationException("Builder arena changed: expected "+entry.Value.count+" '"+entry.Key+"', found "+n);
            int levels=0;
            foreach(var text in arena.GetComponentsInChildren<TextMesh>(true))
                if(text.text.StartsWith("FLOOD WATER",StringComparison.Ordinal)){text.gameObject.AddComponent<IceArenaPart>().role=IceArenaRole.StationLevels;levels++;}
            if(levels!=1)throw new InvalidOperationException("Builder station changed: expected one tank level repeater, found "+levels);
            if(world.IceMesh.transform.name!="Continuous spatial ice sheet"||world.PitSnow.name!="Accumulating dumped snow / physical pit")
                throw new InvalidOperationException("Builder arena bindings changed.");
            foreach(Transform section in arena)
            {
                var part=section.GetComponent<IceArenaPart>()??section.gameObject.AddComponent<IceArenaPart>();
                part.sectionRoot=true;
            }
        }

        static Transform Child(Transform parent,string name)
        {
            foreach(Transform child in parent)if(child.name==name)return child;
            throw new InvalidOperationException("Builder arena changed: missing "+name);
        }

        /// <summary>The live section prefab files (prefab, optional meshes).</summary>
        public static List<string> SectionFiles(string folder,string section)
        {
            var files=new List<string>{SectionPrefabPath(folder,section)};
            if(File.Exists(SectionMeshPath(folder,section)))files.Add(SectionMeshPath(folder,section));
            return files;
        }
    }
}
