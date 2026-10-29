using System;

namespace IceResurfacer.Core
{
    public struct IceSurfaceTotals
    {
        public int Covered, Cut, Wet, Overwater;
        public double Quality, WaterQuality;
    }

    public struct IceCell
    {
        public bool IsIce, Sensitive, Covered, Shavable;
        public float Scratch, SnowMm, CutDepthMm, WashLiters, FloodMm, Quality, OverwaterMm, LastWetTime;
        // Real residual groove depth in the ice. A shallow bite cannot erase a
        // deep skate gouge merely by changing its color or adding wash water.
        public float GougeDepthMm, InitialGougeDepthMm;
        // FloodMm is total retained water, including frozen ice. Only LiquidMm
        // can be recovered; phase changes conserve FloodMm.
        public float LiquidMm, FrozenFloodMm, FrozenOverwaterMm, LiquidUpdateTime;
        internal float LastProcessTime;
    }

    /// <summary>Compact spatial state. One cell is an area sample, never a Unity object.</summary>
    public sealed class IceField
    {
        public readonly int Columns, Rows;
        public readonly float CellSize, MinX, MinZ;
        public readonly IceCell[] Cells;
        public readonly int IceCellCount;
        public readonly int ShavableCellCount;
        public int Revision { get; private set; }
        public int DirtyCellCount { get; private set; }
        readonly SimulatorConfig config;
        readonly float[] freezeFactors = new float[4097];
        readonly float freezeTimeScale;
        readonly int[] dirtyCells;
        readonly bool[] dirtyFlags;
        readonly byte[] scoredFlags;
        readonly float[] scoredQuality,scoredWater;
        IceSurfaceTotals surfaceTotals;
        bool totalsValid=true;

        public IceField(SimulatorConfig config, int seed)
        {
            this.config = config;
            CellSize = config.CellSize;
            Columns = (int)Math.Ceiling(config.RinkWidth / CellSize);
            Rows = (int)Math.Ceiling(config.RinkLength / CellSize);
            MinX = -config.RinkWidth * .5f;
            MinZ = -config.RinkLength * .5f;
            Cells = new IceCell[Columns * Rows];
            dirtyCells=new int[Cells.Length];dirtyFlags=new bool[Cells.Length];
            scoredFlags=new byte[Cells.Length];scoredQuality=new float[Cells.Length];scoredWater=new float[Cells.Length];
            freezeTimeScale=256f/config.FreezeSeconds;
            for(int i=0;i<freezeFactors.Length;i++)
                freezeFactors[i]=(float)Math.Exp(-i/256f);
            uint state = unchecked((uint)seed) | 1u;
            for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Columns; col++)
            {
                float x = CellX(col), z = CellZ(row);
                if (!IsInside(x, z)) continue;
                int i = Index(col, row);
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                float random = (state & 65535) / 65535f;
                float crease = Math.Abs(z) - (config.RinkLength * .5f - 4.2f);
                bool sensitive = x * x + crease * crease < 3.4f * 3.4f;
                float traffic=WearNoise(x*.23f,z*.23f,seed);
                float deepPatch=Simulation.Clamp((traffic-.56f)/.30f,0,1);
                deepPatch=deepPatch*deepPatch;
                float ordinary=config.MinimumGougeDepthMm+(config.TypicalGougeDepthMm-config.MinimumGougeDepthMm)*(.35f+.65f*random);
                float gouge=ordinary+(config.DeepGougeDepthMm-config.TypicalGougeDepthMm)*deepPatch;
                if(sensitive)gouge=Math.Min(config.DeepGougeDepthMm,gouge+.018f);
                Cells[i] = new IceCell
                {
                    IsIce = true, Sensitive = sensitive, Shavable = IsInside(x,z,config.ShavingPerimeterExclusion),
                    GougeDepthMm=gouge,InitialGougeDepthMm=gouge,
                    Scratch=Simulation.Clamp(.12f+gouge/config.GougeRoughnessDepthMm,0,1),
                    SnowMm = .12f + random * .14f + (sensitive ? .17f : 0),
                    Quality = .12f + random * .09f, LastWetTime = -10000f, LastProcessTime = -10000f
                };
                IceCellCount++;
                if(Cells[i].Shavable)ShavableCellCount++;
                UpdateSurfaceTotals(i);
            }
        }

        static float WearNoise(float x,float z,int seed)
        {
            int ix=(int)Math.Floor(x),iz=(int)Math.Floor(z);
            float fx=x-ix,fz=z-iz;fx=fx*fx*(3-2*fx);fz=fz*fz*(3-2*fz);
            float a=WearHash(ix,iz,seed),b=WearHash(ix+1,iz,seed);
            float c=WearHash(ix,iz+1,seed),d=WearHash(ix+1,iz+1,seed);
            return (a+(b-a)*fx)*(1-fz)+(c+(d-c)*fx)*fz;
        }
        static float WearHash(int x,int z,int seed)
        {
            uint value=unchecked((uint)(x*374761393+z*668265263+seed*144269));
            value=(value^(value>>13))*1274126177u;value^=value>>16;
            return (value&65535)/65535f;
        }

        public void ApplyBladeCut(ref IceCell cell,float biteMm,bool reverse)
        {
            if(biteMm<=0)return;
            cell.CutDepthMm+=biteMm;
            cell.GougeDepthMm=reverse?cell.GougeDepthMm+biteMm*.5f:Math.Max(0,cell.GougeDepthMm-biteMm);
            cell.Scratch=Simulation.Clamp(.06f+cell.GougeDepthMm/config.GougeRoughnessDepthMm,0,1);
        }

        public float GrooveRoughness(IceCell cell)
        { return Simulation.Clamp(.06f+cell.GougeDepthMm/config.GougeRoughnessDepthMm,0,1); }

        public int Index(int column, int row) { return row * Columns + column; }
        public float CellX(int column) { return MinX + (column + .5f) * CellSize; }
        public float CellZ(int row) { return MinZ + (row + .5f) * CellSize; }
        public bool IsInside(float x, float z, float margin = 0f)
        {
            float hx = config.RinkWidth * .5f - margin;
            float hz = config.RinkLength * .5f - margin;
            if (hx < 0 || hz < 0 || Math.Abs(x) > hx || Math.Abs(z) > hz) return false;
            float r = Math.Max(0, config.CornerRadius - margin);
            float dx = Math.Max(0, Math.Abs(x) - (hx - r));
            float dz = Math.Max(0, Math.Abs(z) - (hz - r));
            return dx * dx + dz * dz <= r * r + .000001f;
        }

        public float Wetness(int index, float time)
        {
            ref readonly IceCell cell = ref Cells[index];
            if (!cell.IsIce || cell.LiquidMm <= 0) return 0;
            float bucket = Math.Max(0,time-cell.LiquidUpdateTime)*freezeTimeScale;
            int low=(int)bucket;
            float factor=low>=freezeFactors.Length-1?0:
                freezeFactors[low]+(freezeFactors[low+1]-freezeFactors[low])*(bucket-low);
            // Wet fringe has a visible but proportional specular response. Its
            // weight follows actual liquid volume, including recovery.
            return Math.Min(1f,cell.LiquidMm*factor/config.GoodFloodDepthMm);
        }

        public float LiquidDepth(IceCell cell, float time)
        {
            return cell.LiquidMm * (float)Math.Exp(-Math.Max(0, time - cell.LiquidUpdateTime) / config.FreezeSeconds);
        }

        public float FrozenDepth(IceCell cell,float time) { return Math.Max(0,cell.FloodMm-LiquidDepth(cell,time)); }

        public void ResolveLiquid(ref IceCell cell,float time)
        {
            float before=cell.LiquidMm, remaining=LiquidDepth(cell,time);
            float fraction=before>.0000001f?Math.Max(0,1-remaining/before):0;
            cell.FrozenFloodMm+=before-remaining;
            cell.FrozenOverwaterMm+=(cell.OverwaterMm-cell.FrozenOverwaterMm)*fraction;
            cell.LiquidMm=remaining; cell.LiquidUpdateTime=time;
        }

        public void AddWater(ref IceCell cell,float depthMm,float time)
        {
            if(depthMm<=0)return;
            ResolveLiquid(ref cell,time);
            cell.LiquidMm+=depthMm;cell.FloodMm+=depthMm;cell.LastWetTime=time;
            float threshold=config.ExcessFloodDepthMm*(cell.Sensitive?.78f:1);
            // Frozen defects persist. New liquid excess is recoverable only
            // while that part of the film is still liquid.
            cell.OverwaterMm=Math.Max(cell.OverwaterMm,
                cell.FrozenOverwaterMm+Math.Max(0,cell.LiquidMm-threshold));
        }

        public float RemoveLiquid(ref IceCell cell,float requestedMm,float time)
        {
            ResolveLiquid(ref cell,time);
            float amount=Math.Min(Math.Max(0,requestedMm),Math.Max(0,cell.LiquidMm-config.WashPickupResidualMm));
            cell.LiquidMm-=amount;cell.FloodMm=Math.Max(cell.FrozenFloodMm,cell.FloodMm-amount);
            cell.OverwaterMm=Math.Max(cell.FrozenOverwaterMm,cell.OverwaterMm-amount);
            return amount;
        }

        public void UpdateQuality(ref IceCell cell)
        {
            float area=CellSize*CellSize;
            float film=Simulation.Clamp(cell.FloodMm/config.GoodFloodDepthMm,0,1);
            float overcut=Math.Max(0,cell.CutDepthMm-
                (cell.Sensitive?config.SensitiveCumulativeSafeCutMm:config.CumulativeSafeCutMm));
            cell.Quality=Simulation.Clamp((1-cell.Scratch)*.58f+film*.32f+
                Simulation.Clamp(cell.WashLiters/(area*.1f),0,1)*.10f-cell.SnowMm*.45f-cell.OverwaterMm*.35f-overcut*.5f,0,1);
            cell.Covered=cell.CutDepthMm>.08f*config.BladeBiteFraction&&cell.FloodMm>.08f&&cell.SnowMm<.25f;
        }

        // Explicit fixture/raw-array invalidation preserves the original public
        // contract. Production mutations register each changed cell and commit
        // through CommitChanges, so normal reports never rescan the entire grid.
        public void MarkChanged() { Revision++;totalsValid=false; }
        internal void CommitChanges() { Revision++; }
        public void MarkCellChanged(int index)
        {
            if(totalsValid)UpdateSurfaceTotals(index);
            if(dirtyFlags[index])return;
            dirtyFlags[index]=true;dirtyCells[DirtyCellCount++]=index;
        }
        public int DirtyCellIndex(int n) { return dirtyCells[n]; }
        public void ClearDirtyCells()
        {
            for(int i=0;i<DirtyCellCount;i++)dirtyFlags[dirtyCells[i]]=false;
            DirtyCellCount=0;
        }

        public IceSurfaceTotals GetSurfaceTotals()
        {
            if(!totalsValid)
            {
                surfaceTotals=default;
                Array.Clear(scoredFlags,0,scoredFlags.Length);
                Array.Clear(scoredQuality,0,scoredQuality.Length);
                Array.Clear(scoredWater,0,scoredWater.Length);
                for(int i=0;i<Cells.Length;i++)UpdateSurfaceTotals(i);
                totalsValid=true;
            }
            return surfaceTotals;
        }

        void UpdateSurfaceTotals(int index)
        {
            ref readonly IceCell cell=ref Cells[index];
            byte next=0;float quality=0,water=0;
            if(cell.IsIce)
            {
                if(cell.Shavable)
                {
                    if(cell.Covered)next|=1;
                    if(cell.CutDepthMm>.08f*config.BladeBiteFraction&&cell.SnowMm<.25f)next|=2;
                }
                if(cell.FloodMm>.08f)next|=4;
                if(cell.OverwaterMm>.05f)next|=8;
                quality=cell.Quality;water=Simulation.Clamp(cell.FloodMm/config.GoodFloodDepthMm,0,1);
            }
            int old=scoredFlags[index];
            surfaceTotals.Covered+=(next&1)-(old&1);
            surfaceTotals.Cut+=((next>>1)&1)-((old>>1)&1);
            surfaceTotals.Wet+=((next>>2)&1)-((old>>2)&1);
            surfaceTotals.Overwater+=((next>>3)&1)-((old>>3)&1);
            surfaceTotals.Quality+=(double)quality-scoredQuality[index];
            surfaceTotals.WaterQuality+=(double)water-scoredWater[index];
            scoredFlags[index]=next;scoredQuality[index]=quality;scoredWater[index]=water;
        }

        // A service spill joins the same liquid/frozen balance. The kernel is
        // clipped to the boards and never writes into a non-ice cell.
        public float DepositWater(float x,float z,float liters,float time,float radius=.25f)
        {
            if(liters<=0||!IsInside(x,z))return 0;
            int center=IndexAt(x,z);if(center<0||!Cells[center].IsIce)return 0;
            int cx=center%Columns,cz=center/Columns,reach=(int)Math.Ceiling(radius/CellSize);
            float total=0;
            for(int pass=0;pass<2;pass++)
            for(int row=Math.Max(0,cz-reach);row<=Math.Min(Rows-1,cz+reach);row++)
            for(int col=Math.Max(0,cx-reach);col<=Math.Min(Columns-1,cx+reach);col++)
            {
                int i=Index(col,row);if(!Cells[i].IsIce)continue;
                float dx=CellX(col)-x,dz=CellZ(row)-z;
                float weight=Math.Max(0,1-(dx*dx+dz*dz)/(radius*radius));
                if(weight<=0)continue;
                if(pass==0)total+=weight;
                else if(total>0)
                {
                    var cell=Cells[i];AddWater(ref cell,liters*weight/(total*CellSize*CellSize),time);
                    UpdateQuality(ref cell);Cells[i]=cell;MarkCellChanged(i);
                }
            }
            if(total>0)CommitChanges();
            return total>0?liters:0;
        }

        public int IndexAt(float x, float z)
        {
            int col = (int)Math.Floor((x - MinX) / CellSize), row = (int)Math.Floor((z - MinZ) / CellSize);
            if (col < 0 || col >= Columns || row < 0 || row >= Rows) return -1;
            return Index(col, row);
        }

        // Signed distance to the rounded boards; negative is inside. The normal
        // points back onto the ice and is stable across straights and corners.
        public float BoardDistance(float x,float z,out float nx,out float nz)
        {
            float qx=Math.Abs(x)-(config.RinkWidth*.5f-config.CornerRadius);
            float qz=Math.Abs(z)-(config.RinkLength*.5f-config.CornerRadius);
            float ox=Math.Max(0,qx),oz=Math.Max(0,qz);
            float length=(float)Math.Sqrt(ox*ox+oz*oz);
            if(length>.00001f) { nx=-Math.Sign(x)*ox/length; nz=-Math.Sign(z)*oz/length; }
            else { nx=qx>qz?-Math.Sign(x):0; nz=qx>qz?0:-Math.Sign(z); }
            return length+Math.Min(Math.Max(qx,qz),0)-config.CornerRadius;
        }

        // Deposits only existing snow into nearby ice cells, including rounded
        // boundaries. Two passes normalize the kernel without temporary arrays.
        public float DepositSnow(float x,float z,float volume,float radius=.3f)
        {
            int cx=(int)Math.Floor((x-MinX)/CellSize),cz=(int)Math.Floor((z-MinZ)/CellSize);
            int reach=(int)Math.Ceiling(radius/CellSize);
            float total=0;
            for(int pass=0;pass<2;pass++)
            for(int row=Math.Max(0,cz-reach);row<=Math.Min(Rows-1,cz+reach);row++)
            for(int col=Math.Max(0,cx-reach);col<=Math.Min(Columns-1,cx+reach);col++)
            {
                int i=Index(col,row); if(!Cells[i].IsIce)continue;
                float dx=CellX(col)-x,dz=CellZ(row)-z;
                float weight=Math.Max(0,1-(dx*dx+dz*dz)/(radius*radius));
                if(weight<=0)continue;
                if(pass==0)total+=weight;
                else if(total>0)
                {
                    var cell=Cells[i]; float mm=volume*1000*weight/(total*CellSize*CellSize);
                    cell.SnowMm+=mm; cell.Quality=Math.Max(0,cell.Quality-mm*.5f);
                    cell.Covered=cell.Covered&&cell.SnowMm<.25f;
                    Cells[i]=cell;MarkCellChanged(i);
                }
            }
            if(total>0)CommitChanges();
            return total>0?volume:0;
        }
    }
}
