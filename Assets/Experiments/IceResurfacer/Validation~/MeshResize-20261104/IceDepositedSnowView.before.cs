using System;
using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace IceResurfacer.Presentation
{
    /// <summary>Sparse chunks; four flat facets per occupied simulation cell.</summary>
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
            public Chunk(Transform parent,Material material,int x,int z)
            {
                var node=new GameObject("Deposited snow / "+x+","+z);node.transform.SetParent(parent,false);
                Mesh=new Mesh{name=node.name};Mesh.MarkDynamic();
                node.AddComponent<MeshFilter>().sharedMesh=Mesh;
                Renderer=node.AddComponent<MeshRenderer>();Renderer.sharedMaterial=material;
                Renderer.shadowCastingMode=ShadowCastingMode.Off;
                for(int i=0;i<Indices.Length;i++)Indices[i]=i;
            }
            public void Triangle(SnowSurfacePoint a,SnowSurfacePoint b,SnowSurfacePoint c)
            {
                if(Count+3>Vertices.Length)
                {
                    int previous=Vertices.Length,size=Mathf.Min(CellsPerChunk*CellsPerChunk*12,previous*2);
                    Array.Resize(ref Vertices,size);Array.Resize(ref Normals,size);Array.Resize(ref Indices,size);
                    for(int i=previous;i<size;i++)Indices[i]=i;
                }
                var va=new Vector3(a.X,a.Y+.0002f,a.Z);var vb=new Vector3(b.X,b.Y+.0002f,b.Z);var vc=new Vector3(c.X,c.Y+.0002f,c.Z);
                Vector3 normal=Vector3.Cross(vb-va,vc-va).normalized;
                Vertices[Count]=va;Vertices[Count+1]=vb;Vertices[Count+2]=vc;
                Normals[Count]=Normals[Count+1]=Normals[Count+2]=normal;
                Maximum=Mathf.Max(Maximum,Mathf.Max(a.Y,Mathf.Max(b.Y,c.Y)));Count+=3;
            }
        }
        public IceDepositedSnowView(IceField field,Transform parent)
        {
            root=new GameObject("Conserved loose snow / sparse faceted meshes").transform;root.SetParent(parent,false);
            columns=Mathf.CeilToInt(field.Columns/(float)CellsPerChunk);rows=Mathf.CeilToInt(field.Rows/(float)CellsPerChunk);
            chunks=new Chunk[columns*rows];dirty=new bool[chunks.Length];urgent=new bool[chunks.Length];
            snow=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Deposited snow / opaque granular surface"};
            snow.SetColor("_BaseColor",new Color(.87f,.91f,.93f));snow.SetFloat("_Smoothness",.08f);
        }
        public void MarkSnowChanged(IceField field,int i)
        {
            int x=i%field.Columns,z=i/field.Columns;
            // Corner depths consume immediate neighbours. Removal has priority
            // so clean cells never wait behind the bounded additions queue.
            for(int cz=Mathf.Max(0,(z-1)/CellsPerChunk);cz<=Mathf.Min(rows-1,(z+1)/CellsPerChunk);cz++)
            for(int cx=Mathf.Max(0,(x-1)/CellsPerChunk);cx<=Mathf.Min(columns-1,(x+1)/CellsPerChunk);cx++)
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
            if(chunk!=null){chunk.Count=0;chunk.Maximum=0;chunk.Volume=0;}
            for(int z=z0;z<z1;z++)for(int x=x0;x<x1;x++)
            {
                if(!SnowSurfaceGeometry.Build(field,x,z,roughness,out var p))continue;
                if(chunk==null)chunk=chunks[index]=new Chunk(root,snow,index%columns,index/columns);
                chunk.Triangle(p.A,p.B,p.Center);chunk.Triangle(p.B,p.C,p.Center);
                chunk.Triangle(p.C,p.D,p.Center);chunk.Triangle(p.D,p.A,p.Center);chunk.Volume+=p.Volume;
            }
            if(chunk==null)return;
            chunk.Renderer.enabled=chunk.Count>0;
            if(chunk.Count==0){chunk.Mesh.Clear();return;}
            chunk.Mesh.SetVertices(chunk.Vertices,0,chunk.Count);
            chunk.Mesh.SetNormals(chunk.Normals,0,chunk.Count);
            chunk.Mesh.SetIndices(chunk.Indices,0,chunk.Count,MeshTopology.Triangles,0,false);
            chunk.Mesh.RecalculateBounds();
        }
        public void Dispose()
        {
            root.gameObject.SetActive(false);
            foreach(var chunk in chunks)if(chunk!=null)UnityEngine.Object.Destroy(chunk.Mesh);
            UnityEngine.Object.Destroy(snow);UnityEngine.Object.Destroy(root.gameObject);
        }
    }
}
