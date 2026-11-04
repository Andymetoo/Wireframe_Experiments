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
        public Texture2D OverviewTexture { get; private set; }
        public Texture2D DepthTexture { get; private set; }
        public Texture2D ProcessTexture { get; private set; }
        readonly Color32[] pixels;
        readonly Color32[] mapPixels;
        readonly Color32[] processPixels;
        readonly Color32[] overviewPixels,depthPixels;
        readonly bool[] overviewDirty;
        readonly int[] overviewDirtyIndices;
        readonly byte[] overviewStates;
        readonly byte[] dirtyWetStates;
        readonly float[] snowDepths;
        int overviewDirtyCount;
        readonly int overviewSpan,overviewColumns,overviewRows;
        readonly IceDepositedSnowView depositedSnow;
        readonly Material material;
        readonly SimulatorConfig config;
        readonly int[] wetCells;
        readonly bool[] wetFlags;
        int wetCount,wetCursor;
        bool firstRefresh=true,forceRefresh;
        public int DebugMode { get; private set; }
        public int LastCellsUpdated { get; private set; }
        public int LastAgingCellsUpdated { get; private set; }
        public int LastAgingCellsReused { get; private set; }
        public int LastOverviewPixelsUpdated { get; private set; }
        public int LastTextureUploads { get; private set; }
        public int LastSnowChunksRebuilt { get { return depositedSnow.LastChunksRebuilt; } }
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
            depthPixels=new Color32[pixels.Length];
            overviewSpan=IceOverview.CellsPerPixel(field);
            overviewColumns=(field.Columns+overviewSpan-1)/overviewSpan;
            overviewRows=(field.Rows+overviewSpan-1)/overviewSpan;
            overviewPixels=new Color32[overviewColumns*overviewRows];overviewDirty=new bool[overviewPixels.Length];
            overviewDirtyIndices=new int[overviewPixels.Length];overviewStates=new byte[pixels.Length];
            snowDepths=new float[pixels.Length];dirtyWetStates=new byte[pixels.Length];
            OverviewTexture=new Texture2D(overviewColumns,overviewRows,TextureFormat.RGBA32,false)
                {name="Area weighted surface overview / 20 cm",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            DepthTexture=new Texture2D(field.Columns,field.Rows,TextureFormat.RGBA32,false,true)
                {name="Actual standing liquid depth / frozen film",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            wetCells=new int[pixels.Length];wetFlags=new bool[pixels.Length];
            ProcessTexture = new Texture2D(field.Columns,field.Rows,TextureFormat.RGBA32,false,true)
                { name="Ice / actual cut, wash, flood",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear };
            material = new Material(template) { name = "Northline / spatial ice" };
            material.SetTexture("_StateTex", StateTexture);
            material.SetTexture("_ProcessTex",ProcessTexture);
            material.SetTexture("_DepthTex",DepthTexture);
            material.SetVector("_GridSize",new Vector4(field.Columns,field.Rows,1f/field.Columns,1f/field.Rows));
            // Sample the exact simulation rectangle, including a possible final
            // partial row/column. Authored mesh UVs are not a second width authority.
            material.SetVector("_GridWorld",new Vector4(field.MinX,field.MinZ,
                1f/(field.Columns*field.CellSize),1f/(field.Rows*field.CellSize)));
            renderer.sharedMaterial = material;
            depositedSnow=new IceDepositedSnowView(field,renderer.transform.parent);
        }
        public void Refresh(Simulation sim)
        {
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            var field = sim.Ice;
            bool all=firstRefresh||forceRefresh;
            int count=all?pixels.Length:field.DirtyCellCount;
            LastCellsUpdated=count;LastAgingCellsUpdated=LastAgingCellsReused=LastOverviewPixelsUpdated=LastTextureUploads=0;
            bool stateChanged=all,depthChanged=all,processChanged=all,mapChanged=all,overviewChanged=all;
            float inverseGoodDepth=1f/config.GoodFloodDepthMm,area=field.CellSize*field.CellSize;
            float requiredCut=IceCoverage.RequiredCutMm(config);
            if(all)
            {
                // Explicit invalidation is exact, including removal of expired
                // active entries. Ordinary refreshes never scan the full grid.
                System.Array.Clear(wetFlags,0,wetFlags.Length);wetCount=wetCursor=0;
            }
            for (int n = 0; n < count; n++)
            {
                int i=all?n:field.DirtyCellIndex(n);
                ref readonly var c = ref field.Cells[i];
                float liquid=field.LiquidDepthFast(i,sim.Time),wet=Mathf.Min(1,liquid*inverseGoodDepth);
                var state=new Color32(Quantize(c.Scratch),Quantize(c.SnowMm/1.5f),Quantize(c.Quality),Quantize(wet));
                if(Different(pixels[i],state)){pixels[i]=state;stateChanged=true;}
                var depth=DepthPixel(liquid,c.FloodMm);
                if(Different(depthPixels[i],depth)){depthPixels[i]=depth;depthChanged=true;}
                bool snowChanged=snowDepths[i]!=c.SnowMm;
                if(snowChanged){snowDepths[i]=c.SnowMm;depositedSnow.MarkSnowChanged(field,i);}
                // Only these exact classifications and snow depth contribute to
                // the overview. Sub-threshold liquid redistribution cannot alter
                // it, so do not resummarize sixteen cells for every water delta.
                byte overviewState=(byte)((c.IsIce?1:0)|(c.CutDepthMm>requiredCut?2:0)|
                    (c.FloodMm>IceCoverage.RequiredFloodMm?4:0)|(c.Shavable?8:0)|
                    (c.OverwaterMm>IceCoverage.ExcessWaterMm?16:0));
                if(all||snowChanged||overviewStates[i]!=overviewState)
                {
                    overviewStates[i]=overviewState;
                    int oi=(i/field.Columns/overviewSpan)*overviewColumns+(i%field.Columns/overviewSpan);
                    if(!overviewDirty[oi]){overviewDirty[oi]=true;overviewDirtyIndices[overviewDirtyCount++]=oi;}
                }
                // These channels describe actual cell history; no cosmetic trail or
                // coverage credit is generated outside the simulated footprints.
                var process=new Color32(Quantize(c.CutDepthMm/.08f),Quantize(c.WashLiters/(area*.15f)),Quantize(c.FloodMm/.3f),c.Shavable?(byte)255:(byte)0);
                if(Different(processPixels[i],process)){processPixels[i]=process;processChanged=true;}
                if(wet>=.0001f&&!wetFlags[i]) { wetFlags[i]=true;wetCells[wetCount++]=i; }
                dirtyWetStates[i]=wet>=.0001f?(byte)1:(byte)2;
                Color32 map;
                if (!c.IsIce) map = new Color32(13, 24, 32, 0);
                else if(DebugMode==1)map=!c.Shavable?new Color32(233,154,65,255):
                    c.Covered?new Color32(64,225,137,255):new Color32(65,112,130,255);
                else if(DebugMode==2)map=new Color32(Quantize(c.CutDepthMm/.05f),Quantize(c.WashLiters/(area*.1f)),Quantize(c.FloodMm/.3f),255);
                else map = TreatmentColor(IceCoverage.Classify(c,config));
                if(Different(mapPixels[i],map)){mapPixels[i]=map;mapChanged=true;}
            }
            int agingCount=all?0:System.Math.Min(wetCount,AgingCellsPerRefresh);
            for(int n=0;n<agingCount&&wetCount>0;n++)
            {
                if(wetCursor>=wetCount)wetCursor=0;
                int i=wetCells[wetCursor];
                // Dirty cells already have exact current phase/depth bytes. Their
                // marker also retains the unquantized expiry decision, so reuse
                // cannot leave a cleared cell alive merely because its alpha is 0.
                if(dirtyWetStates[i]!=0)
                {
                    LastAgingCellsUpdated++;LastAgingCellsReused++;
                    if(dirtyWetStates[i]==2){wetFlags[i]=false;wetCells[wetCursor]=wetCells[--wetCount];}
                    else wetCursor++;
                    continue;
                }
                float liquid=field.LiquidDepthFast(i,sim.Time),wet=Mathf.Min(1,liquid*inverseGoodDepth);
                // Wetness already clips the physical liquid result to [0,1].
                // Float-to-byte rounding avoids a double Math.Round per sample.
                byte alpha=(byte)(wet*255f+.5f);
                if(pixels[i].a!=alpha){pixels[i].a=alpha;stateChanged=true;}
                var depth=DepthPixel(liquid,field.Cells[i].FloodMm);
                if(depthPixels[i].r!=depth.r||depthPixels[i].g!=depth.g){depthPixels[i]=depth;depthChanged=true;}
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
            if(stateChanged) { StateTexture.SetPixels32(pixels); StateTexture.Apply(false,false);LastTextureUploads++; }
            if(depthChanged){DepthTexture.SetPixels32(depthPixels);DepthTexture.Apply(false,false);LastTextureUploads++;}
            if(processChanged){ProcessTexture.SetPixels32(processPixels);ProcessTexture.Apply(false,false);LastTextureUploads++;}
            if(mapChanged){MapTexture.SetPixels32(mapPixels);MapTexture.Apply(false,false);LastTextureUploads++;}
            for(int n=0;n<overviewDirtyCount;n++)
            {
                int i=overviewDirtyIndices[n];overviewDirty[i]=false;
                var summary=IceOverview.Summarize(field,config,i%overviewColumns*overviewSpan,i/overviewColumns*overviewSpan,overviewSpan);
                var color=OverviewColor(summary);LastOverviewPixelsUpdated++;
                if(Different(overviewPixels[i],color)){overviewPixels[i]=color;overviewChanged=true;}
            }
            overviewDirtyCount=0;
            if(overviewChanged){OverviewTexture.SetPixels32(overviewPixels);OverviewTexture.Apply(false,false);LastTextureUploads++;}
            depositedSnow.Refresh(field,all);
            // Clear only this refresh's dirty markers; wet aging never scans or
            // clears the full grid during ordinary operation.
            for(int n=0;n<count;n++)dirtyWetStates[all?n:field.DirtyCellIndex(n)]=0;
            field.ClearDirtyCells();firstRefresh=forceRefresh=false;
            LastRefreshMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        static byte Quantize(float value)
        {return value<=0?(byte)0:value>=1?(byte)255:(byte)(value*255f+.5f);}
        static bool Different(Color32 a,Color32 b)
        {return a.r!=b.r||a.g!=b.g||a.b!=b.b||a.a!=b.a;}
        static Color32 DepthPixel(float liquid,float total)
        {return new Color32(Quantize(Mathf.Sqrt(Mathf.Clamp01(liquid/16f))),Quantize((total-liquid)/.3f),0,255);}
        public static Color32 OverviewColor(IceOverviewSample s)
        {
            if(s.IceCells==0)return new Color32(13,24,32,0);
            Color color=(Color)TreatmentColor(IceTreatmentState.Uncut)*s.Unprocessed+
                (Color)TreatmentColor(IceTreatmentState.CutButDry)*s.DryCut+
                (Color)TreatmentColor(IceTreatmentState.WetButUncut)*s.WetUncut+
                (Color)TreatmentColor(IceTreatmentState.Resurfaced)*s.WetCut+
                (Color)TreatmentColor(IceTreatmentState.ShavingExcluded)*s.Excluded;
            // Initial loose dust .12-.43 mm is real but not a heavy snowdrift.
            // Area-mean depth exposes concentrated deposits without random white
            // pixels or artificial crease circles. Exact thresholds stay in F2.
            float snow=Mathf.Clamp01((s.SnowDepthMm-.55f)/1.45f);
            color=Color.Lerp(color,TreatmentColor(IceTreatmentState.SnowCovered),snow);
            color=Color.Lerp(color,TreatmentColor(IceTreatmentState.Overwatered),s.ExcessFraction*.82f);
            color.a=s.IceFraction;return color;
        }
        public int DepositedSnowChunks {get{return depositedSnow.ActiveChunks;}}
        public float DepositedSnowMaximumHeight {get{return depositedSnow.MaximumHeight;}}
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
            depositedSnow.Dispose();
            Object.Destroy(StateTexture); Object.Destroy(MapTexture); Object.Destroy(OverviewTexture);
            Object.Destroy(DepthTexture);Object.Destroy(ProcessTexture); Object.Destroy(material);
        }
    }
}
