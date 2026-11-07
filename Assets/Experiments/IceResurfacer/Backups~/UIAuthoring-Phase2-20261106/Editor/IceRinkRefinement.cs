using System;
using System.Collections.Generic;
using System.Linq;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// One-time, in-place refinement of the authored rink section (Arena/Sections/Rink boards and ice
    /// sheet.prefab): closed board/rail/glass solids, taller end and corner glass with a built
    /// transition, glass supports at the local glass height, and a solid post at each side of the
    /// service entrance. The four band meshes keep their asset objects (GUID and file ID); new parts
    /// are added beside them; nothing else in the arena is touched. Uses IceRinkBoards, the same
    /// definition the procedural builder uses. Backed up first with the recovery backup.
    /// </summary>
    public static class IceRinkRefinement
    {
        public const string SectionPath="Assets/Experiments/IceResurfacer/Arena/Sections/Rink boards and ice sheet.prefab";
        public const string MeshesPath="Assets/Experiments/IceResurfacer/Arena/Sections/Rink boards and ice sheet.Meshes.asset";
        const string MaterialFolder="Assets/Experiments/IceResurfacer/Arena/Materials/";
        static readonly string[] Bands={"White impact dasher","Yellow kick strip","Dark top rail","Continuous safety glass"};
        public const string EdgeName="Glass top edge channel", PostPrefix="Service gate board post / ";

        [MenuItem("Experiments/Ice Resurfacer/Authored Scene/Refine Rink Boards and Glass...")]
        static void Menu()
        {
            var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>(FindObjectsInactive.Include);
            if(game==null){EditorUtility.DisplayDialog("Refine rink","Open the authored scene first (its calibration defines the rink).","OK");return;}
            string plan=Describe(game.calibration,out bool applied);
            if(applied){EditorUtility.DisplayDialog("Refine rink","Already refined:\n\n"+plan,"OK");return;}
            if(!EditorUtility.DisplayDialog("Refine rink boards and glass",plan+"\n\nThe section prefab and its mesh asset are backed up to Backups~ first (Restore backup undoes it).","Refine","Cancel"))return;
            try{Debug.Log(Apply(game.calibration));}
            catch(Exception e){EditorUtility.DisplayDialog("Not refined",e.Message,"OK");}
        }

        public static string Describe(SimulatorConfig c,out bool applied)
        {
            var root=PrefabUtility.LoadPrefabContents(SectionPath);
            try
            {
                applied=root.transform.Find(EdgeName)!=null;
                return "Boards stay at "+IceRinkBoards.RailTop.ToString("0.00")+" m (48 in, inside the 40-48 in range).\n"+
                    "Glass: "+IceRinkBoards.EndGlassAboveBoards.ToString("0.00")+" m above the boards around both ends and every corner (top "+IceRinkBoards.EndGlassTop.ToString("0.00")+" m), "+
                    IceRinkBoards.SideGlassAboveBoards.ToString("0.00")+" m along the sides (top "+IceRinkBoards.SideGlassTop.ToString("0.00")+" m), one sloped panel between the two supports nearest each corner.\n"+
                    "Board, kickplate, rail and glass become closed solids ending in a board post at each side of the service entrance.\n"+
                    "Unchanged: ice footprint, corner radius, gate width, board-contact boundary, collision.";
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        /// <summary>Validates, backs up, then edits the section prefab and its mesh asset in place.</summary>
        public static string Apply(SimulatorConfig c)
        {
            c.Validate();
            var layout=new ServiceLayout(c);
            var root=PrefabUtility.LoadPrefabContents(SectionPath);
            var log=new List<string>();
            try
            {
                if(root.transform.Find(EdgeName)!=null)return "Rink already refined; nothing changed.";
                // The section must have been generated for this rink (same footprint): otherwise regenerate it instead.
                var section=root.GetComponent<IceArenaPart>();
                if(section!=null&&!string.IsNullOrEmpty(section.BakedConfigJson))
                {
                    var baked=JsonUtility.FromJson<SimulatorConfig>(section.BakedConfigJson);
                    if(baked.RinkWidth!=c.RinkWidth||baked.RinkLength!=c.RinkLength||baked.CornerRadius!=c.CornerRadius||baked.GateHalfWidth!=c.GateHalfWidth)
                        throw new InvalidOperationException("The rink section was generated for a different rink (width/length/corner/gate). Regenerate it with Advanced Recovery instead.");
                }
                var bands=new Dictionary<string,MeshFilter>();
                foreach(string name in Bands)
                {
                    var found=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name==name).ToList();
                    if(found.Count!=1)throw new InvalidOperationException("Expected exactly one '"+name+"' in the rink section, found "+found.Count+".");
                    var f0=found[0];
                    if(f0.sharedMesh==null||AssetDatabase.GetAssetPath(f0.sharedMesh)!=MeshesPath)throw new InvalidOperationException("'"+name+"' no longer uses its generated mesh; refine it by hand.");
                    if(f0.transform.parent!=root.transform||f0.transform.localPosition!=Vector3.zero||f0.transform.localRotation!=Quaternion.identity||f0.transform.localScale!=Vector3.one)
                        throw new InvalidOperationException("'"+name+"' was moved; the board line must stay on the simulated boards. Restore its generated pose first.");
                    bands[name]=f0;
                }
                var ice=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Continuous spatial ice sheet");
                if(ice==null)throw new InvalidOperationException("The ice sheet is missing from the rink section.");
                var supportMesh=root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f=>f.name=="Glass support")?.sharedMesh;

                string backup=IceAuthoredRecovery.BackUp("Before rink boards and glass refinement",new[]{SectionPath,MeshesPath});
                log.Add("Backup: "+backup);

                var boundary=IceRinkBoards.Boundary(c);
                var profile=new IceRinkBoards.GlassProfile(c,boundary,layout);
                float boards=IceRinkBoards.BoardClip(layout),glassClip=IceRinkBoards.GlassClip(layout);
                Replace(bands["White impact dasher"],IceRinkBoards.Band("dasher",c,layout,boundary,IceRinkBoards.KickTop,IceRinkBoards.BoardTop,IceRinkBoards.BoardThickness,boards,null,false),log);
                Replace(bands["Yellow kick strip"],IceRinkBoards.Band("kick",c,layout,boundary,IceRinkBoards.KickBottom,IceRinkBoards.KickTop,IceRinkBoards.KickThickness,boards,null,true,true),log);
                Replace(bands["Dark top rail"],IceRinkBoards.Band("rail",c,layout,boundary,IceRinkBoards.BoardTop,IceRinkBoards.RailTop,IceRinkBoards.RailThickness,boards,null,true,true),log);
                Replace(bands["Continuous safety glass"],IceRinkBoards.Band("glass",c,layout,boundary,IceRinkBoards.RailTop,0,IceRinkBoards.GlassThickness,glassClip,profile.Top),log);

                Material M(string name){var m=AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+name+".mat");if(m==null)throw new InvalidOperationException("Arena material missing: "+name);return m;}
                Material steel=M("Brushed stainless steel"),white=M("Porcelain enamel"),yellow=M("Dasher kickplate"),navy=M("Aurora midnight blue"),glass=M("Arena safety glass"),rubber=M("Tire and hydraulic rubber");
                int next=NextIndex();

                // Same order as the builder: bands, edge channel, supports, gate posts, ice sheet.
                var edge=MeshPart(EdgeName,root.transform,IceRinkBoards.Band("edge",c,layout,boundary,0,0,.024f,glassClip,p=>profile.Top(p)+.022f,true,true,-.006f,profile.Top),steel,ref next);
                edge.SetSiblingIndex(bands["Continuous safety glass"].transform.GetSiblingIndex()+1);
                log.Add("Added "+EdgeName);

                int adjusted=0;var kept=new List<string>();
                foreach(var support in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Glass support"))
                {
                    // Generated supports span 1.20-2.54 m (centre 1.87, half height 0.67); a support
                    // you resized is left alone.
                    if(Mathf.Abs(support.localPosition.y-1.87f)>.002f||Mathf.Abs(support.localScale.y-.67f)>.002f){kept.Add(support.name+" at "+support.localPosition);continue;}
                    Undo.RecordObject(support,"Glass support height");
                    float top=profile.Top(support.localPosition),bottom=IceRinkBoards.RailTop-.02f;
                    var p=support.localPosition;p.y=(bottom+top)*.5f;support.localPosition=p;
                    var s=support.localScale;s.y=(top-bottom)*.5f;support.localScale=s;
                    adjusted++;
                }
                log.Add("Glass supports raised to the local glass height: "+adjusted+(kept.Count>0?"; left as you edited them: "+string.Join(", ",kept):""));

                foreach(var post in IceRinkBoards.GatePosts(c,layout,profile,.75f-1.75f*.5f))
                {
                    var group=new GameObject(PostPrefix+post.Side).transform;group.SetParent(root.transform,false);
                    MeshPart("Gate post kick plate",group,IceRinkBoards.Prism("post kick",post.Plan,IceRinkBoards.KickBottom,IceRinkBoards.KickTop),yellow,ref next);
                    MeshPart("Gate post board",group,IceRinkBoards.Prism("post board",post.Plan,IceRinkBoards.KickTop,IceRinkBoards.BoardTop),white,ref next);
                    MeshPart("Gate post top rail",group,IceRinkBoards.Prism("post rail",post.Plan,IceRinkBoards.BoardTop,IceRinkBoards.RailTop),navy,ref next);
                    Primitive("Gate post rubber edge guard",group,PrimitiveType.Cylinder,(post.GuardBottom+post.GuardTop)*.5f,Quaternion.identity,
                        new Vector3(.045f,(post.GuardTop-post.GuardBottom).magnitude*.5f,.045f),rubber,supportMesh);
                    Primitive("Gate glass end stanchion",group,PrimitiveType.Cube,post.StanchionCentre+Vector3.up*(IceRinkBoards.RailTop+post.GlassTop+.022f)*.5f,Quaternion.Euler(0,-45,0),
                        new Vector3(.045f,post.GlassTop+.022f-IceRinkBoards.RailTop,.045f),steel,null);
                    MeshPart("Gate post glass return",group,IceRinkBoards.Panel("post glass",post.ReturnStart,post.ReturnEnd,IceRinkBoards.RailTop,post.GlassTop,IceRinkBoards.GlassThickness),glass,ref next);
                    MeshPart("Gate glass return top edge",group,IceRinkBoards.Panel("post edge",post.ReturnStart,post.ReturnEnd,post.GlassTop,post.GlassTop+.022f,.024f),steel,ref next);
                    group.SetSiblingIndex(ice.GetSiblingIndex());
                    log.Add("Added "+group.name);
                }
                PrefabUtility.SaveAsPrefabAsset(root,SectionPath);
                AssetDatabase.SaveAssets();
                log.Add("Saved "+SectionPath+" and "+MeshesPath+" (GUIDs unchanged).");
                return string.Join("\n",log);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        static void Replace(MeshFilter filter,Mesh generated,List<string> log)
        {
            var mesh=filter.sharedMesh;int before=mesh.vertexCount;
            IceRinkBoards.CopyInto(generated,mesh);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(mesh);
            log.Add(filter.name+": mesh '"+mesh.name+"' rebuilt in place ("+before+" -> "+mesh.vertexCount+" vertices)");
        }

        // New meshes are numbered after the section's existing sub-assets, like the bake does.
        static int NextIndex()
        {
            int max=0;
            foreach(var o in AssetDatabase.LoadAllAssetsAtPath(MeshesPath))
                if(o is Mesh m&&m.name.Length>4&&int.TryParse(m.name.Substring(0,3),out int n))max=Mathf.Max(max,n);
            return max+1;
        }

        static Transform MeshPart(string name,Transform parent,Mesh mesh,Material material,ref int index)
        {
            mesh.name=index.ToString("000")+" "+name;index++;
            AssetDatabase.AddObjectToAsset(mesh,MeshesPath);
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            if(material.name=="Arena safety glass")renderer.shadowCastingMode=ShadowCastingMode.Off;
            return go.transform;
        }

        static Transform Primitive(string name,Transform parent,PrimitiveType type,Vector3 position,Quaternion rotation,Vector3 scale,Material material,Mesh mesh)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            if(mesh!=null)go.GetComponent<MeshFilter>().sharedMesh=mesh;
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;go.transform.localScale=scale;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;
            return go.transform;
        }
    }
}
