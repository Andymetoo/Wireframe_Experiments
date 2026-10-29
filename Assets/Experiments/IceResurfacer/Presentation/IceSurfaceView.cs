using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    // One mesh, one streaming state texture. No per-cell render objects or decals.
    public sealed class IceSurfaceView : System.IDisposable
    {
        public const int AgingCellsPerRefresh = 65536;
        public Texture2D StateTexture { get; private set; }
        public Texture2D MapTexture { get; private set; }
        public Texture2D ProcessTexture { get; private set; }
        readonly Color32[] pixels;
        readonly Color32[] mapPixels;
        readonly Color32[] processPixels;
        readonly Material material;
        readonly SimulatorConfig config;
        readonly int[] wetCells;
        readonly bool[] wetFlags;
        int wetCount,wetCursor;
        bool firstRefresh=true,forceRefresh;
        public int DebugMode { get; private set; }
        public int LastCellsUpdated { get; private set; }
        public int LastAgingCellsUpdated { get; private set; }
        public int ActiveWetCells { get { return wetCount; } }
        public double LastRefreshMilliseconds { get; private set; }
        public IceSurfaceView(IceField field, MeshRenderer renderer, Material template, SimulatorConfig calibration = null)
        {
            config=calibration??new SimulatorConfig();
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
            material.SetVector("_GridSize",new Vector4(field.Columns,field.Rows,1f/field.Columns,1f/field.Rows));
            // Sample the exact simulation rectangle, including a possible final
            // partial row/column. Authored mesh UVs are not a second width authority.
            material.SetVector("_GridWorld",new Vector4(field.MinX,field.MinZ,
                1f/(field.Columns*field.CellSize),1f/(field.Rows*field.CellSize)));
            renderer.sharedMaterial = material;
        }
        public void Refresh(Simulation sim)
        {
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            var field = sim.Ice;
            bool all=firstRefresh||forceRefresh;
            int count=all?pixels.Length:field.DirtyCellCount;
            LastCellsUpdated=count;LastAgingCellsUpdated=0;
            if(all)
            {
                // Explicit invalidation is exact, including removal of expired
                // active entries. Ordinary refreshes never scan the full grid.
                System.Array.Clear(wetFlags,0,wetFlags.Length);wetCount=wetCursor=0;
            }
            for (int n = 0; n < count; n++)
            {
                int i=all?n:field.DirtyCellIndex(n);
                var c = field.Cells[i];
                float wet = field.Wetness(i, sim.Time);
                pixels[i] = new Color(c.Scratch, Mathf.Clamp01(c.SnowMm / 1.5f), c.Quality, wet);
                // These channels describe actual cell history; no cosmetic trail or
                // coverage credit is generated outside the simulated footprints.
                processPixels[i]=new Color(Mathf.Clamp01(c.CutDepthMm/.08f),Mathf.Clamp01(c.WashLiters/(field.CellSize*field.CellSize*.15f)),Mathf.Clamp01(c.FloodMm/.3f),c.Shavable?1:0);
                if(wet>=.0001f&&!wetFlags[i]) { wetFlags[i]=true;wetCells[wetCount++]=i; }
                if (!c.IsIce) mapPixels[i] = new Color32(13, 24, 32, 0);
                else if(DebugMode==1)mapPixels[i]=!c.Shavable?new Color32(233,154,65,255):
                    c.Covered?new Color32(64,225,137,255):new Color32(65,112,130,255);
                else if(DebugMode==2)mapPixels[i]=new Color(Mathf.Clamp01(c.CutDepthMm/.05f),Mathf.Clamp01(c.WashLiters/(field.CellSize*field.CellSize*.1f)),Mathf.Clamp01(c.FloodMm/.3f),1);
                else mapPixels[i] = TreatmentColor(IceCoverage.Classify(c,config));
            }
            bool stateChanged=count>0;
            int agingCount=all?0:System.Math.Min(wetCount,AgingCellsPerRefresh);
            for(int n=0;n<agingCount&&wetCount>0;n++)
            {
                if(wetCursor>=wetCount)wetCursor=0;
                int i=wetCells[wetCursor];float wet=field.Wetness(i,sim.Time);
                // Wetness already clips the physical liquid result to [0,1].
                // Float-to-byte rounding avoids a double Math.Round per sample.
                byte alpha=(byte)(wet*255f+.5f);
                if(pixels[i].a!=alpha){pixels[i].a=alpha;stateChanged=true;}
                LastAgingCellsUpdated++;
                if(wet<.0001f)
                {
                    wetFlags[i]=false;wetCells[wetCursor]=wetCells[--wetCount];
                    // Visit the swapped entry next, rather than skipping it.
                }
                else wetCursor++;
            }
            // A final removal must upload its zero even when the active list is
            // now empty. Unchanged quantized ages need no texture upload.
            if(stateChanged) { StateTexture.SetPixels32(pixels); StateTexture.Apply(false,false); }
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
        public static Color32 TreatmentColor(IceTreatmentState state)
        {
            switch(state)
            {
                case IceTreatmentState.Uncut:return new Color32(110,126,137,255);
                case IceTreatmentState.CutButDry:return new Color32(183,135,209,255);
                case IceTreatmentState.WetButUncut:return new Color32(73,155,220,255);
                case IceTreatmentState.SnowCovered:return new Color32(229,234,230,255);
                case IceTreatmentState.Overwatered:return new Color32(233,154,65,255);
                case IceTreatmentState.Resurfaced:return new Color32(69,224,186,255);
                case IceTreatmentState.ShavingExcluded:return new Color32(80,105,112,255);
                default:return new Color32(13,24,32,0);
            }
        }
        // Useful after explicit fixture writes; normal simulation dirties cells.
        public void Invalidate() { forceRefresh=true; }
        public void Dispose()
        {
            Object.Destroy(StateTexture); Object.Destroy(MapTexture); Object.Destroy(ProcessTexture); Object.Destroy(material);
        }
    }
}
