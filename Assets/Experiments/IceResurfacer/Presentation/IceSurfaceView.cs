using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    // One mesh, one streaming state texture. No per-cell render objects or decals.
    public sealed class IceSurfaceView : System.IDisposable
    {
        public Texture2D StateTexture { get; private set; }
        public Texture2D MapTexture { get; private set; }
        public Texture2D ProcessTexture { get; private set; }
        readonly Color32[] pixels;
        readonly Color32[] mapPixels;
        readonly Color32[] processPixels;
        readonly Material material;
        readonly int[] wetCells;
        readonly bool[] wetFlags;
        int wetCount;
        bool firstRefresh=true,forceRefresh;
        public int DebugMode { get; private set; }
        public int LastCellsUpdated { get; private set; }
        public int ActiveWetCells { get { return wetCount; } }
        public double LastRefreshMilliseconds { get; private set; }
        public IceSurfaceView(IceField field, MeshRenderer renderer, Material template)
        {
            StateTexture = new Texture2D(field.Columns, field.Rows, TextureFormat.RGBA32, false, true)
                { name = "Ice / scratch, snow, finish, wet", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            MapTexture = new Texture2D(field.Columns, field.Rows, TextureFormat.RGBA32, false)
                { name = "Live surface survey", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            pixels = new Color32[field.Columns * field.Rows];
            mapPixels = new Color32[pixels.Length];
            processPixels = new Color32[pixels.Length];
            wetCells=new int[pixels.Length];wetFlags=new bool[pixels.Length];
            ProcessTexture = new Texture2D(field.Columns,field.Rows,TextureFormat.RGBA32,false,true)
                { name="Ice / actual cut, wash, flood",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear };
            material = new Material(template) { name = "Northline / spatial ice" };
            material.SetTexture("_StateTex", StateTexture);
            material.SetTexture("_ProcessTex",ProcessTexture);
            material.SetVector("_GridSize",new Vector4(field.Columns,field.Rows,0,0));
            renderer.sharedMaterial = material;
        }
        public void Refresh(Simulation sim)
        {
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            var field = sim.Ice;
            bool all=firstRefresh||forceRefresh;
            int count=all?pixels.Length:field.DirtyCellCount;
            LastCellsUpdated=count;
            for (int n = 0; n < count; n++)
            {
                int i=all?n:field.DirtyCellIndex(n);
                var c = field.Cells[i];
                float wet = field.Wetness(i, sim.Time);
                pixels[i] = new Color(c.Scratch, Mathf.Clamp01(c.SnowMm / 1.5f), c.Quality, wet);
                // These channels describe actual cell history; no cosmetic trail or
                // coverage credit is generated outside the simulated footprints.
                processPixels[i]=new Color(Mathf.Clamp01(c.CutDepthMm/.08f),Mathf.Clamp01(c.WashLiters/(field.CellSize*field.CellSize*.15f)),Mathf.Clamp01(c.FloodMm/.3f),c.Shavable?1:0);
                if(c.LiquidMm>0&&!wetFlags[i]) { wetFlags[i]=true;wetCells[wetCount++]=i; }
                if (!c.IsIce) mapPixels[i] = new Color32(13, 24, 32, 0);
                else if(DebugMode==1)mapPixels[i]=!c.Shavable?new Color32(233,154,65,255):
                    c.Covered?new Color32(64,225,137,255):new Color32(65,112,130,255);
                else if(DebugMode==2)mapPixels[i]=new Color(Mathf.Clamp01(c.CutDepthMm/.05f),Mathf.Clamp01(c.WashLiters/(field.CellSize*field.CellSize*.1f)),Mathf.Clamp01(c.FloodMm/.3f),1);
                else if (c.OverwaterMm > .15f) mapPixels[i] = new Color32(233, 154, 65, 255);
                else if (c.Covered) mapPixels[i] = Color.Lerp(new Color(.3f, .51f, .58f), new Color(.27f, .88f, .73f), c.Quality);
                else mapPixels[i] = Color.Lerp(new Color(.29f, .37f, .43f), new Color(.63f, .68f, .7f), c.Scratch);
            }
            for(int n=wetCount-1;n>=0;n--)
            {
                int i=wetCells[n];float wet=field.Wetness(i,sim.Time);
                pixels[i].a=(byte)Mathf.Clamp(Mathf.RoundToInt(wet*255),0,255);
                if(wet<.0001f)
                { wetFlags[i]=false;wetCells[n]=wetCells[--wetCount]; }
            }
            if(count>0||wetCount>0) { StateTexture.SetPixels32(pixels); StateTexture.Apply(false,false); }
            if(count>0)
            {
                ProcessTexture.SetPixels32(processPixels);ProcessTexture.Apply(false,false);
                MapTexture.SetPixels32(mapPixels);MapTexture.Apply(false,false);
            }
            field.ClearDirtyCells();firstRefresh=forceRefresh=false;
            LastRefreshMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        public void SetDebugMode(int mode)
        {
            mode=Mathf.Clamp(mode,0,2);if(DebugMode==mode)return;
            DebugMode=mode;material.SetFloat("_DebugMode",mode);forceRefresh=true;
        }
        // Useful after explicit fixture writes; normal simulation dirties cells.
        public void Invalidate() { forceRefresh=true; }
        public void Dispose()
        {
            Object.Destroy(StateTexture); Object.Destroy(MapTexture); Object.Destroy(ProcessTexture); Object.Destroy(material);
        }
    }
}
