using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>Read-only measurement and screenshots of the authored rink (isolated project only).</summary>
    public static class IceRinkSurvey
    {
        public const string Out="Assets/Experiments/IceResurfacer/Validation~/RinkAndFillPorts/";

        public static void Survey()
        {
            int code=0;var sb=new StringBuilder();
            try
            {
                if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))throw new InvalidOperationException("isolated only");
                Directory.CreateDirectory(Out);
                EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
                string prefix=Arg("-rinkPrefix","Before");
                sb.Append(Measure());
                File.WriteAllText(Out+"rink-"+prefix+".txt",sb.ToString());
                Shots(prefix);
            }
            catch(Exception e){Debug.LogException(e);File.AppendAllText(Out+"rink-survey-error.txt",e+"\n");code=1;}
            EditorApplication.Exit(code);
        }

        public static void ApplyRink()
        {
            int code=0;
            try
            {
                if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))throw new InvalidOperationException("isolated only");
                Directory.CreateDirectory(Out);
                EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
                var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>(FindObjectsInactive.Include);
                File.WriteAllText(Out+"rink-apply.txt",IceRinkRefinement.Apply(game.calibration)+"\n");
                EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
                File.WriteAllText(Out+"rink-After.txt",Measure());
                Shots("After");
            }
            catch(Exception e){Debug.LogException(e);File.AppendAllText(Out+"rink-apply.txt","FAIL "+e+"\n");code=1;}
            EditorApplication.Exit(code);
        }

        public static string Measure()
        {
            var sb=new StringBuilder();
            var rig=UnityEngine.Object.FindAnyObjectByType<IceArenaRig>(FindObjectsInactive.Include);
            var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>(FindObjectsInactive.Include);
            var layout=new ServiceLayout(game.calibration);
            sb.AppendLine("arena instance overrides:");
            foreach(var m in PrefabUtility.GetPropertyModifications(rig.gameObject)??new PropertyModification[0])
                sb.AppendLine("  "+(m.target!=null?m.target.name:"(null)")+"."+m.propertyPath+" = "+m.value);
            var rink=rig.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Rink boards and ice sheet");
            sb.AppendLine("rink section children: "+rink.childCount+"; prefab source "+AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(rink.gameObject)));
            var groups=new Dictionary<string,int>();
            foreach(Transform c in rink){groups.TryGetValue(c.name,out int n);groups[c.name]=n+1;}
            foreach(var g in groups)sb.AppendLine("  "+g.Value+" x "+g.Key);
            foreach(var f in rink.GetComponentsInChildren<MeshFilter>(true))
            {
                if(f.name=="Glass support")continue;
                var mesh=f.sharedMesh;if(mesh==null){sb.AppendLine("  "+f.name+": no mesh");continue;}
                var v=mesh.vertices.Select(p=>f.transform.TransformPoint(p)-rig.transform.parent.position).ToArray();
                float y0=v.Min(p=>p.y),y1=v.Max(p=>p.y);
                var part=f.GetComponent<IceArenaPart>();
                sb.AppendLine("BAND "+f.name+" | role "+(part!=null?part.role.ToString():"-")+" | y "+y0.ToString("F3")+".."+y1.ToString("F3")+" | verts "+mesh.vertexCount+" tris "+mesh.triangles.Length/3+
                    " | mesh '"+mesh.name+"' in "+Path.GetFileName(AssetDatabase.GetAssetPath(mesh))+" | material "+f.GetComponent<Renderer>().sharedMaterial.name+
                    " | normals "+(mesh.normals.Length==mesh.vertexCount)+" | localScale "+f.transform.localScale+" localPos "+f.transform.localPosition);
                // Thickness: spread of signed board distance.
                float din=v.Min(p=>Dist(p,game.calibration)),dout=v.Max(p=>Dist(p,game.calibration));
                sb.AppendLine("     board distance "+din.ToString("F3")+".."+dout.ToString("F3")+" (thickness "+(dout-din).ToString("F3")+")");
                // Gate ends: vertices nearest the opening on each side.
                foreach(int s in new[]{-1,1})
                {
                    float best=9;Vector3 bestP=default;
                    foreach(var p in v){layout.ToLocal(p.x,p.z,out float side,out float outward);if(Mathf.Sign(side)==s&&outward>-2&&outward<2&&Mathf.Abs(side)<best){best=Mathf.Abs(side);bestP=p;}}
                    layout.ToLocal(bestP.x,bestP.z,out float bs,out float bo);
                    sb.AppendLine("     gate end "+(s<0?"southwest":"northeast")+": closest |side| "+best.ToString("F3")+" (gate half width "+layout.GateHalfWidth+"), outward "+bo.ToString("F3"));
                }
            }
            var supports=rink.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Glass support").ToList();
            if(supports.Count>0)
            {
                var b=supports.Select(t=>t.GetComponent<Renderer>().bounds).ToList();
                sb.AppendLine("GLASS SUPPORTS "+supports.Count+": y "+b.Min(x=>x.min.y-rig.transform.parent.position.y).ToString("F3")+".."+b.Max(x=>x.max.y-rig.transform.parent.position.y).ToString("F3")+", scale "+supports[0].localScale);
            }
            // Gate jambs, gate glass, uprights.
            foreach(var t in rig.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Gate opened")||t.name=="Open gate glass"||t.name=="Portal upright"||t.name=="Portal protection bollard"||t.name=="Portal overhead lintel"))
            {
                var r=t.GetComponent<Renderer>();if(r==null)continue;var bb=r.bounds;
                Vector3 c=bb.center-rig.transform.parent.position;layout.ToLocal(c.x,c.z,out float side,out float outward);
                sb.AppendLine("GATE PART "+t.name+" | side "+side.ToString("F3")+" outward "+outward.ToString("F3")+" | y "+(bb.min.y-rig.transform.parent.position.y).ToString("F3")+".."+(bb.max.y-rig.transform.parent.position.y).ToString("F3")+" | scale "+t.localScale);
            }
            sb.AppendLine("== drift");
            foreach(var f in IceArenaDrift.Evaluate(rig,game.calibration))sb.AppendLine((f.Drifted?"  DRIFT ":"  ok    ")+f.Label+": "+f.Detail);
            return sb.ToString();
        }

        static float Dist(Vector3 p,SimulatorConfig c)
        {
            float qx=Mathf.Abs(p.x)-(c.RinkWidth*.5f-c.CornerRadius),qz=Mathf.Abs(p.z)-(c.RinkLength*.5f-c.CornerRadius);
            float ox=Mathf.Max(0,qx),oz=Mathf.Max(0,qz);
            return Mathf.Sqrt(ox*ox+oz*oz)+Mathf.Min(Mathf.Max(qx,qz),0)-c.CornerRadius;
        }

        public static void Shots(string prefix)
        {
            var rig=UnityEngine.Object.FindAnyObjectByType<IceArenaRig>(FindObjectsInactive.Include);
            var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>(FindObjectsInactive.Include);
            Vector3 o=rig.transform.parent.position;
            var layout=new ServiceLayout(game.calibration);
            Vector3 Bay(float side,float y,float outward){layout.ToWorld(side,outward,out float x,out float z);return o+new Vector3(x,y,z);}
            var views=new List<(string,Vector3,Vector3,float)>
            {
                ("Rink-Full",o+new Vector3(-6,3.2f,-6),o+new Vector3(4,1.4f,22),62),
                ("Rink-EndGlass-North",o+new Vector3(0,1.8f,14),o+new Vector3(0,2.0f,30),55),
                ("Rink-EndGlass-South",o+new Vector3(-3,1.8f,-14),o+new Vector3(-3,2.0f,-30),55),
                ("Rink-Side-To-End-Transition",o+new Vector3(-6,1.9f,8),o+new Vector3(-13,2.0f,24),58),
                ("Rink-Corner-Northwest",o+new Vector3(0,1.7f,15),o+new Vector3(-10.5f,1.8f,27.5f),55),
                ("Gate-From-Ice",Bay(0,1.7f,-6.5f),Bay(0,1.2f,0),60),
                ("Gate-End-Northeast",Bay(1.2f,1.1f,-2.4f),Bay(2.75f,.8f,-.3f),45),
                ("Gate-End-Southwest",Bay(-1.2f,1.1f,-2.4f),Bay(-2.75f,.8f,-.3f),45),
                ("Gate-End-Northeast-FromBay",Bay(1.3f,1.0f,2.2f),Bay(2.75f,.8f,-.3f),45),
                ("Gate-End-Southwest-FromBay",Bay(-1.3f,1.0f,2.2f),Bay(-2.75f,.8f,-.3f),45),
                // Orthographic elevations (negative value = half height in metres).
                ("Elevation-West-Side-To-Corner",o+new Vector3(4,2.0f,20),o+new Vector3(-13,2.0f,20),-2.6f),
                ("Elevation-North-End",o+new Vector3(0,2.0f,16),o+new Vector3(0,2.0f,30),-2.6f),
            };
            var node=new GameObject("Rink survey camera"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var cam=node.AddComponent<Camera>();cam.nearClipPlane=.03f;cam.farClipPlane=200;
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.09f,.13f,.18f);
                Directory.CreateDirectory(Out+"Shots");
                foreach(var v in views)
                {
                    cam.orthographic=v.Item4<0;if(v.Item4<0)cam.orthographicSize=-v.Item4;else cam.fieldOfView=v.Item4;
                    node.transform.position=v.Item2;node.transform.LookAt(v.Item3);
                    for(int i=0;i<2;i++)IceResurfacerEditor.Capture(cam,"RinkAndFillPorts/Shots/warm.png",320,180);
                    IceResurfacerEditor.Capture(cam,"RinkAndFillPorts/Shots/"+prefix+"-"+v.Item1+".png",1600,900);
                }
                File.Delete(Out+"Shots/warm.png");
            }
            finally{UnityEngine.Object.DestroyImmediate(node);}
        }

        static string Arg(string name,string fallback)
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]==name)return args[i+1];
            return fallback;
        }
    }
}
