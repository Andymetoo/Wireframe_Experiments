using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Shared by the machine and arena bakers: turns builder-owned meshes and materials into
    /// assets. Never overwrites an existing asset; destructive replacement lives only in
    /// IceAuthoredRecovery behind a verified backup and typed confirmation.
    /// </summary>
    public static class IceAssetPersistence
    {
        /// <summary>Builds generated geometry in an isolated preview scene, so the open scene is
        /// neither modified nor dirtied, and always discards it afterwards.</summary>
        public static T InPreviewScene<T>(string name,Func<Transform,T> build)
        {
            var preview=EditorSceneManager.NewPreviewScene();
            var active=SceneManager.GetActiveScene();
            bool wasDirty=active.isDirty;
            try
            {
                var root=new GameObject(name);
                SceneManager.MoveGameObjectToScene(root,preview);
                return build(root.transform);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                // Builder helpers create each object in the active scene before parenting it into
                // the preview scene; nothing remains there, so a clean scene stays clean.
                if(!wasDirty&&active.isDirty&&active.IsValid())ClearDirtiness(active);
            }
        }
        static void ClearDirtiness(Scene scene)
        {
            var method=typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            if(method!=null)method.Invoke(null,new object[]{scene});
        }

        /// <summary>Saves every non-asset material under root into folder (reusing an asset that
        /// already exists there or in reuseFolder, by name, without changing it).</summary>
        public static void PersistMaterials(Transform root,string folder,string reuseFolder=null)
        {
            CreateFolder(folder);
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
                if(material==null)continue;
                if(!EditorUtility.IsPersistent(material))
                {
                    if(!saved.TryGetValue(material,out var asset))
                    {
                        string label=LabelOf(material);
                        asset=Existing(reuseFolder,label)??Existing(folder,label);
                        if(asset==null)
                        {
                            // URP's first-import validation keeps _EMISSION only with an emissive GI flag (what
                            // its inspector sets); the template's EmissiveIsBlack would leave fixtures unlit.
                            if(material.IsKeywordEnabled("_EMISSION"))material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
                            // Its "preserve specular" default would switch transparent glass to premultiplied
                            // alpha on import; the builder's glass is plain SrcAlpha/OneMinusSrcAlpha.
                            if(material.HasProperty("_Surface")&&material.GetFloat("_Surface")==1&&material.HasProperty("_BlendModePreserveSpecular"))
                                material.SetFloat("_BlendModePreserveSpecular",0);
                            material.name=label;
                            AssetDatabase.CreateAsset(material,folder+"/"+Safe(label)+".mat");
                            asset=material;
                        }
                        saved.Add(material,asset);
                    }
                    renderer.sharedMaterial=asset;
                    material=asset;
                }
                // Matches the runtime batching rule for luminous fixtures and glass.
                string name=LabelOf(material);
                if(name=="Fixture luminous diffuser"||name=="Arena safety glass")renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            AssetDatabase.SaveAssets();
        }
        static Material Existing(string folder,string label)
        {
            return folder==null?null:AssetDatabase.LoadAssetAtPath<Material>(folder+"/"+Safe(label)+".mat");
        }
        public static string LabelOf(Material material) { return material.name.Replace("IceResurfacer / ",""); }

        /// <summary>Saves every non-asset mesh under root as sub-assets of one new container.
        /// Returns how many were saved (none: no container is written).</summary>
        public static int PersistMeshes(Transform root,string path)
        {
            if(AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new InvalidOperationException(path+" already exists; generation never overwrites assets.");
            var meshes=new List<Mesh>();
            var unique=new HashSet<Mesh>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;
                if(mesh==null||EditorUtility.IsPersistent(mesh)||!unique.Add(mesh))continue;
                meshes.Add(mesh);
            }
            if(meshes.Count==0)return 0;
            // Readable meshes are required by RouteSilhouetteContinuation, drift checks and validation.
            for(int i=0;i<meshes.Count;i++)
            {
                meshes[i].name=(i+1).ToString("000")+" "+meshes[i].name.Replace("IceResurfacer / ","");
                if(i==0)AssetDatabase.CreateAsset(meshes[i],path);
                else AssetDatabase.AddObjectToAsset(meshes[i],path);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            return meshes.Count;
        }

        public static string Safe(string name)
        {
            foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'-');
            return name.Replace('/','-').Trim();
        }

        /// <summary>A new, unused folder such as Recovery/Machine 2026-11-01 153012.</summary>
        public static string UniqueFolder(string parent,string label)
        {
            string stamp=DateTime.Now.ToString("yyyy-MM-dd HHmmss");
            string path=parent+"/"+label+" "+stamp;
            for(int i=2;AssetDatabase.IsValidFolder(path)||Directory.Exists(path);i++)path=parent+"/"+label+" "+stamp+" "+i;
            return path;
        }
        public static void CreateFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            // AssetDatabase.CreateFolder silently renames when the directory already exists on disk.
            if(Directory.Exists(path)) { AssetDatabase.ImportAsset(path);if(AssetDatabase.IsValidFolder(path))return; }
            string parent=Path.GetDirectoryName(path).Replace('\\','/');
            CreateFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
    }
}
