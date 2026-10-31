using System.Collections.Generic;
using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IceResurfacer.Presentation
{
    /// <summary>Exact visible-mesh ray queries in an isolated physics scene; never a world x-ray pass.</summary>
    public sealed class RouteSilhouetteContinuation:System.IDisposable
    {
        sealed class Shape{public Transform source,copy;public bool moving;}
        readonly List<Shape> shapes=new List<Shape>();
        readonly HashSet<int> machine=new HashSet<int>();
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        readonly Scene scene;
        readonly PhysicsScene physics;
        public int LastVisibleSegments {get;private set;}
        public int ShapeCount=>shapes.Count;
        public RouteSilhouetteContinuation(IceResurfacerGame game)
        {
            scene=SceneManager.CreateScene("Aurora GPS visibility queries "+game.GetInstanceID(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));physics=scene.GetPhysicsScene();
            foreach(var renderer in game.GetComponentsInChildren<MeshRenderer>())
            {
                var filter=renderer.GetComponent<MeshFilter>();if(!renderer.enabled||filter==null||filter.sharedMesh==null)continue;
                bool opaque=false;foreach(var material in renderer.sharedMaterials)if(material!=null&&material.renderQueue<2500)opaque=true;
                if(!opaque)continue;
                var mesh=QueryMesh(filter.sharedMesh,renderer.transform.lossyScale);if(mesh==null)continue;
                var node=new GameObject("GPS query / "+renderer.name){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(node,scene);
                var collider=node.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
                bool onMachine=renderer.transform.IsChildOf(game.World.Machine)||renderer.transform.IsChildOf(game.World.Conditioner);
                if(onMachine)machine.Add(collider.GetInstanceID());
                shapes.Add(new Shape{source=renderer.transform,copy=node.transform,moving=onMachine});Copy(shapes[shapes.Count-1]);
            }
            Physics.SyncTransforms();
        }
        Mesh QueryMesh(Mesh original,Vector3 scale)
        {
            // Some visible particles/labels contain collapsed triangles or zero-size
            // meshes. They have no opaque silhouette and must not reach PhysX cooking.
            if(!original.isReadable||Vector3.Scale(original.bounds.size,scale).sqrMagnitude<.000225f)return null;
            var vertices=original.vertices;var indices=original.triangles;if(vertices.Length<3||indices.Length<3)return null;
            var valid=new List<int>(indices.Length);
            for(int i=0;i+2<indices.Length;i+=3)
            {
                var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                if(Vector3.Cross(Vector3.Scale(b-a,scale),Vector3.Scale(c-a,scale)).sqrMagnitude<1e-12f)continue;
                valid.Add(indices[i]);valid.Add(indices[i+1]);valid.Add(indices[i+2]);
            }
            if(valid.Count==0)return null;if(valid.Count==indices.Length)return original;
            var clean=new Mesh{name="GPS-only cleaned query mesh",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};clean.vertices=vertices;clean.SetTriangles(valid,0);clean.RecalculateBounds();ownedMeshes.Add(clean);return clean;
        }
        static void Copy(Shape shape)
        {shape.copy.SetPositionAndRotation(shape.source.position,shape.source.rotation);shape.copy.localScale=shape.source.lossyScale;shape.copy.gameObject.SetActive(shape.source.gameObject.activeInHierarchy);}
        public bool MachineOccludes(Camera camera,Vector3 target)
        {
            Vector3 delta=target-camera.transform.position;float length=delta.magnitude;
            return length>.01f&&physics.Raycast(camera.transform.position,delta/length,out var hit,length-.015f)&&machine.Contains(hit.collider.GetInstanceID());
        }
        public void Draw(IceResurfacerGame game,float width,float height)
        {
            LastVisibleSegments=0;var guide=game.RouteGuide;
            if(guide==null||!guide.Visible||game.HudHidden||guide.ActiveFit==null||!game.Started||game.Paused||game.HelpVisible||guide.PanelOpen||game.Sim.Finished||game.Sim.OnFoot)return;
            foreach(var shape in shapes)if(shape.moving&&shape.source!=null)Copy(shape);
            Physics.SyncTransforms();var camera=game.DriverCamera;var fit=guide.ActiveFit;
            float scale=Mathf.Min(width/1600,height/900);Vector2 offset=new Vector2((width-1600*scale)*.5f,(height-900*scale)*.5f);
            double start=guide.Progress.Distance+.35,end=guide.LookEnd;
            bool previous=false;Vector2 last=default;
            int count=Mathf.Max(2,Mathf.CeilToInt((float)(end-start)/.09f));
            for(int i=0;i<=count;i++)
            {
                var sample=fit.At(start+(end-start)*i/count);var point=new Vector3(sample.x,.048f,sample.z);var screen=camera.WorldToViewportPoint(point);
                bool visible=screen.z>.1f&&screen.x>0&&screen.x<1&&screen.y>0&&screen.y<1&&MachineOccludes(camera,point);
                Vector2 at=(new Vector2(screen.x*width,(1-screen.y)*height)-offset)/scale;
                if(visible&&previous&&Vector2.Distance(last,at)<35)
                {Stroke(last,at,4.8f,new Color(.06f,0,.055f,.88f));var color=guide.GpsColor;color.a*=.78f;Stroke(last,at,2.4f,color);LastVisibleSegments++;}
                last=at;previous=visible;
            }
        }
        static void Stroke(Vector2 a,Vector2 b,float width,Color color)
        {
            var matrix=GUI.matrix;var old=GUI.color;GUI.color=color;
            GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
            GUI.DrawTexture(new Rect(0,-width*.5f,Vector2.Distance(a,b),width),Texture2D.whiteTexture);GUI.color=old;GUI.matrix=matrix;
        }
        public void Dispose(){if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);foreach(var mesh in ownedMeshes)Object.Destroy(mesh);}
    }
}
