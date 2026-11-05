using System;
using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace IceResurfacer.Presentation
{
    /// <summary>Sparse, continuous sloped patches over the conserved snow heightfield.</summary>
    public sealed class IceDepositedSnowView : IDisposable
    {
        const int CellsPerChunk=40,RebuildBudget=8;
        readonly Transform root;
        readonly Material snow;
        readonly Chunk[] chunks;
        readonly bool[] dirty,urgent;
        readonly int columns,rows;
        float roughness=-1;
        public int ActiveChunks {get;private set;}
        public float MaximumHeight {get;private set;}
        public int LastChunksRebuilt {get;private set;}
        public int LastVertices {get;private set;}
        public double VisibleVolumeM3 {get;private set;}

        sealed class Chunk
        {
            public readonly Mesh Mesh;
            public readonly MeshRenderer Renderer;
            public Vector3[] Vertices=new Vector3[384];
            public Vector3[] Normals=new Vector3[384];
            public int[] Indices=new int[384];
            public float Maximum;
            public double Volume;
            public int Count;
            public int IndexCount;
            readonly float[] corners=new float[(CellsPerChunk+1)*(CellsPerChunk+1)];
            public Chunk(Transform parent,Material material,int x,int z)
            {
                var node=new GameObject("Deposited snow / "+x+","+z);node.transform.SetParent(parent,false);
                Mesh=new Mesh{name=node.name};Mesh.MarkDynamic();
                node.AddComponent<MeshFilter>().sharedMesh=Mesh;
                Renderer=node.AddComponent<MeshRenderer>();Renderer.sharedMaterial=material;
                Renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            public void Begin()
            {
                Count=IndexCount=0;Maximum=0;Volume=0;
                for(int i=0;i<corners.Length;i++)corners[i]=float.NaN;
            }
            public float Corner(IceField field,int x,int z,int x0,int z0)
            {
                int index=(z-z0)*(CellsPerChunk+1)+x-x0;
                if(float.IsNaN(corners[index]))corners[index]=SnowSurfaceGeometry.CornerHeight(field,x,z);
                return corners[index];
            }
            static Vector3 Vector(SnowSurfacePoint p)=>new Vector3(p.X,p.Y,p.Z);
            public void Patch(IceField field,int x,int z,SnowSurfacePatch p)
            {
                if(Count+SnowSurfaceGeometry.VerticesPerPatch>Vertices.Length)
                {
                    int size=Mathf.Min(CellsPerChunk*CellsPerChunk*SnowSurfaceGeometry.VerticesPerPatch,Vertices.Length*2);
                    Array.Resize(ref Vertices,size);Array.Resize(ref Normals,size);
                }
                if(IndexCount+SnowSurfaceGeometry.IndicesPerPatch>Indices.Length)
                    Array.Resize(ref Indices,Mathf.Min(CellsPerChunk*CellsPerChunk*SnowSurfaceGeometry.IndicesPerPatch,Indices.Length*2));
                Vector3 a=Vector(SnowSurfaceGeometry.Normal(field,x,z)),b=Vector(SnowSurfaceGeometry.Normal(field,x,z+1));
                Vector3 c=Vector(SnowSurfaceGeometry.Normal(field,x+1,z+1)),d=Vector(SnowSurfaceGeometry.Normal(field,x+1,z));
                for(int i=0;i<SnowSurfaceGeometry.VerticesPerPatch;i++)
                {
                    var point=p.Point(i);Vertices[Count+i]=Vector(point)+Vector3.up*.0002f;
                    Vector3 normal=i%4==0?a:i%4==1?b:i%4==2?c:d;
                    float u=Mathf.InverseLerp(p.A.X,p.D.X,point.X),v=Mathf.InverseLerp(p.A.Z,p.B.Z,point.Z);
                    Normals[Count+i]=i<4?normal:Vector3.Lerp(Vector3.Lerp(a,d,u),Vector3.Lerp(b,c,u),v).normalized;
                    Maximum=Mathf.Max(Maximum,point.Y);
                }
                for(int i=0;i<SnowSurfaceGeometry.IndicesPerPatch;i++)Indices[IndexCount+i]=Count+SnowSurfaceGeometry.TriangleVertex(i);
                Count+=SnowSurfaceGeometry.VerticesPerPatch;IndexCount+=SnowSurfaceGeometry.IndicesPerPatch;
            }
        }
        public IceDepositedSnowView(IceField field,Transform parent)
        {
            root=new GameObject("Conserved loose snow / sparse mounded heightfield").transform;root.SetParent(parent,false);
            columns=Mathf.CeilToInt(field.Columns/(float)CellsPerChunk);rows=Mathf.CeilToInt(field.Rows/(float)CellsPerChunk);
            chunks=new Chunk[columns*rows];dirty=new bool[chunks.Length];urgent=new bool[chunks.Length];
            snow=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Deposited snow / opaque granular surface"};
            snow.SetColor("_BaseColor",new Color(.87f,.91f,.93f));snow.SetFloat("_Smoothness",.08f);
        }
        public void MarkSnowChanged(IceField field,int i)
        {
            int x=i%field.Columns,z=i/field.Columns;
            // Shared reconstruction consumes a two-cell halo. Removal has priority
            // so clean cells never wait behind the bounded additions queue.
            for(int cz=Mathf.Max(0,(z-2)/CellsPerChunk);cz<=Mathf.Min(rows-1,(z+2)/CellsPerChunk);cz++)
            for(int cx=Mathf.Max(0,(x-2)/CellsPerChunk);cx<=Mathf.Min(columns-1,(x+2)/CellsPerChunk);cx++)
            {
                int k=cz*columns+cx;dirty[k]=true;
                if(field.Cells[i].SnowMm<=SnowSurfaceGeometry.MeshThresholdMm)urgent[k]=true;
            }
        }
        public void Refresh(IceField field,bool all,float appearance=.7f)
        {
            appearance=Mathf.Clamp01(appearance);
            if(all||appearance!=roughness)for(int i=0;i<dirty.Length;i++)dirty[i]=true;
            roughness=appearance;LastChunksRebuilt=0;
            for(int i=0;i<chunks.Length;i++)if(dirty[i]&&urgent[i])Rebuild(field,i);
            int additions=0;
            for(int i=0;i<chunks.Length&&additions<RebuildBudget;i++)
                if(dirty[i]){Rebuild(field,i);additions++;}
            ActiveChunks=LastVertices=0;MaximumHeight=0;VisibleVolumeM3=0;
            foreach(var chunk in chunks)
            {
                if(chunk==null||!chunk.Renderer.enabled)continue;
                ActiveChunks++;LastVertices+=chunk.Count;MaximumHeight=Mathf.Max(MaximumHeight,chunk.Maximum);VisibleVolumeM3+=chunk.Volume;
            }
        }
        void Rebuild(IceField field,int index)
        {
            dirty[index]=urgent[index]=false;LastChunksRebuilt++;
            int x0=index%columns*CellsPerChunk,z0=index/columns*CellsPerChunk;
            int x1=Mathf.Min(field.Columns,x0+CellsPerChunk),z1=Mathf.Min(field.Rows,z0+CellsPerChunk);
            var chunk=chunks[index];
            if(chunk!=null)chunk.Begin();
            for(int z=z0;z<z1;z++)for(int x=x0;x<x1;x++)
            {
                var cell=field.Cells[field.Index(x,z)];
                if(!cell.IsIce||cell.SnowMm<=SnowSurfaceGeometry.MeshThresholdMm)continue;
                if(chunk==null){chunk=chunks[index]=new Chunk(root,snow,index%columns,index/columns);chunk.Begin();}
                if(!SnowSurfaceGeometry.Build(field,x,z,roughness,chunk.Corner(field,x,z,x0,z0),chunk.Corner(field,x,z+1,x0,z0),
                    chunk.Corner(field,x+1,z+1,x0,z0),chunk.Corner(field,x+1,z,x0,z0),out var p))continue;
                chunk.Patch(field,x,z,p);chunk.Volume+=p.Volume;
            }
            if(chunk==null)return;
            chunk.Renderer.enabled=chunk.Count>0;
            // Pickup can shrink a nonempty chunk. Remove its old triangle
            // references before uploading the smaller vertex/normal buffers.
            chunk.Mesh.Clear();
            if(chunk.Count==0)return;
            chunk.Mesh.SetVertices(chunk.Vertices,0,chunk.Count);
            chunk.Mesh.SetNormals(chunk.Normals,0,chunk.Count);
            chunk.Mesh.SetIndices(chunk.Indices,0,chunk.IndexCount,MeshTopology.Triangles,0,false);
            chunk.Mesh.RecalculateBounds();
        }
        public void Dispose()
        {
            root.gameObject.SetActive(false);
            foreach(var chunk in chunks)if(chunk!=null)DestroyOwned(chunk.Mesh);
            DestroyOwned(snow);DestroyOwned(root.gameObject);
        }
        static void DestroyOwned(UnityEngine.Object value)
        {
            if(Application.isPlaying)UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
