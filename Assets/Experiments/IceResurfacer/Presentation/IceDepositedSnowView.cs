using System;
using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace IceResurfacer.Presentation
{
    /// <summary>Sparse, dirty 2 m surface chunks derived only from conserved ice snow.</summary>
    public sealed class IceDepositedSnowView : IDisposable
    {
        const int Segments=20;
        const float Size=2f;
        readonly Transform root;
        readonly Material snow;
        readonly Chunk[] chunks;
        readonly bool[] dirty;
        readonly int columns,rows;
        public int ActiveChunks {get;private set;}
        public float MaximumHeight {get;private set;}
        public int LastChunksRebuilt {get;private set;}

        sealed class Chunk
        {
            public readonly Mesh Mesh;
            public readonly MeshRenderer Renderer;
            public readonly Vector3[] Vertices=new Vector3[(Segments+1)*(Segments+1)];
            public readonly int[] Indices=new int[Segments*Segments*6];
            public float Maximum;
            public Chunk(Transform root,Material material,int x,int z)
            {
                var node=new GameObject("Deposited snow / "+x+","+z);node.transform.SetParent(root,false);
                Mesh=new Mesh{name=node.name};Mesh.MarkDynamic();
                node.AddComponent<MeshFilter>().sharedMesh=Mesh;
                Renderer=node.AddComponent<MeshRenderer>();Renderer.sharedMaterial=material;
                Renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        public IceDepositedSnowView(IceField field,Transform parent)
        {
            root=new GameObject("Conserved loose snow / sparse height meshes").transform;root.SetParent(parent,false);
            columns=Mathf.CeilToInt(field.Columns*field.CellSize/Size);rows=Mathf.CeilToInt(field.Rows*field.CellSize/Size);
            chunks=new Chunk[columns*rows];dirty=new bool[chunks.Length];
            snow=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Deposited snow / opaque granular surface"};
            snow.SetColor("_BaseColor",new Color(.87f,.91f,.93f));snow.SetFloat("_Smoothness",.12f);
        }
        public void MarkSnowChanged(IceField field,int i)
        {
            // Water, quality and process-history changes do not alter this mesh.
            // The surface view calls this only after an exact SnowMm change.
            float x=(i%field.Columns+.5f)*field.CellSize,z=(i/field.Columns+.5f)*field.CellSize;
            int left=Mathf.FloorToInt((x-.10f)/Size),right=Mathf.FloorToInt((x+.10f)/Size);
            int bottom=Mathf.FloorToInt((z-.10f)/Size),top=Mathf.FloorToInt((z+.10f)/Size);
            for(int cz=Mathf.Max(0,bottom);cz<=Mathf.Min(rows-1,top);cz++)
            for(int cx=Mathf.Max(0,left);cx<=Mathf.Min(columns-1,right);cx++)dirty[cz*columns+cx]=true;
        }
        public void Refresh(IceField field,bool all)
        {
            if(all)for(int i=0;i<dirty.Length;i++)dirty[i]=true;
            LastChunksRebuilt=0;
            ActiveChunks=0;MaximumHeight=0;
            for(int i=0;i<chunks.Length;i++)
            {
                if(dirty[i]){dirty[i]=false;Rebuild(field,i);LastChunksRebuilt++;}
                var chunk=chunks[i];if(chunk==null||!chunk.Renderer.enabled)continue;
                ActiveChunks++;MaximumHeight=Mathf.Max(MaximumHeight,chunk.Maximum);
            }
        }
        void Rebuild(IceField field,int index)
        {
            int cx=index%columns,cz=index/columns;
            float minX=field.MinX+cx*Size,minZ=field.MinZ+cz*Size;
            var chunk=chunks[index];
            if(chunk==null)
            {
                // Initial .12-.43 mm dust stays in the existing ice shader.
                // Allocate a chunk only when it contains an actual accumulation.
                bool deposit=false;
                int x0=Mathf.Max(0,Mathf.FloorToInt(cx*Size/field.CellSize)-1);
                int z0=Mathf.Max(0,Mathf.FloorToInt(cz*Size/field.CellSize)-1);
                int x1=Mathf.Min(field.Columns-1,Mathf.CeilToInt((cx+1)*Size/field.CellSize)+1);
                int z1=Mathf.Min(field.Rows-1,Mathf.CeilToInt((cz+1)*Size/field.CellSize)+1);
                for(int z=z0;z<=z1&&!deposit;z++)for(int x=x0;x<=x1;x++)
                    if(field.Cells[field.Index(x,z)].SnowMm>1f){deposit=true;break;}
                if(!deposit)return;
                chunk=chunks[index]=new Chunk(root,snow,cx,cz);
            }
            chunk.Maximum=0;
            for(int z=0;z<=Segments;z++)for(int x=0;x<=Segments;x++)
            {
                float px=minX+x*Size/Segments,pz=minZ+z*Size/Segments;
                float height=Sample(field,px,pz)*.001f;
                chunk.Maximum=Mathf.Max(chunk.Maximum,height);
                chunk.Vertices[z*(Segments+1)+x]=new Vector3(px,.0005f+height,pz);
            }
            int count=0;
            for(int z=0;z<Segments;z++)for(int x=0;x<Segments;x++)
            {
                int a=z*(Segments+1)+x,b=a+Segments+1;
                float maximum=Mathf.Max(Mathf.Max(chunk.Vertices[a].y,chunk.Vertices[a+1].y),Mathf.Max(chunk.Vertices[b].y,chunk.Vertices[b+1].y));
                if(maximum<.0015f)continue;
                chunk.Indices[count++]=a;chunk.Indices[count++]=b;chunk.Indices[count++]=a+1;
                chunk.Indices[count++]=a+1;chunk.Indices[count++]=b;chunk.Indices[count++]=b+1;
            }
            chunk.Renderer.enabled=count>0;
            if(count==0)return;
            chunk.Mesh.SetVertices(chunk.Vertices);chunk.Mesh.SetIndices(chunk.Indices,0,count,MeshTopology.Triangles,0,false);
            chunk.Mesh.RecalculateNormals();chunk.Mesh.RecalculateBounds();
        }
        static float Sample(IceField field,float x,float z)
        {
            // Four area samples around each 10 cm mesh vertex. The surface is
            // a low-pass reconstruction of actual bulk depth, with no height gain.
            float total=0;int count=0;
            for(int dz=-1;dz<=1;dz+=2)for(int dx=-1;dx<=1;dx+=2)
            {
                int i=field.IndexAt(x+dx*.025f,z+dz*.025f);
                if(i<0||!field.Cells[i].IsIce)continue;
                total+=field.Cells[i].SnowMm;count++;
            }
            return count>0?total/count:0;
        }
        public void Dispose()
        {
            // Destroy is deferred in play mode; hide the old session immediately,
            // including synchronous editor evidence renders before frame end.
            root.gameObject.SetActive(false);
            foreach(var chunk in chunks)if(chunk!=null)UnityEngine.Object.Destroy(chunk.Mesh);
            UnityEngine.Object.Destroy(snow);UnityEngine.Object.Destroy(root.gameObject);
        }
    }
}
