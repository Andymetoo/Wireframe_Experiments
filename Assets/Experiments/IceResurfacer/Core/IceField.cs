using System;

namespace IceResurfacer.Core
{
    public struct IceCell
    {
        public bool IsIce, Sensitive, Covered, Shavable;
        public float Scratch, SnowMm, CutDepthMm, WashLiters, FloodMm, Quality, OverwaterMm, LastWetTime;
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
                Cells[i] = new IceCell
                {
                    IsIce = true, Sensitive = sensitive, Shavable = IsInside(x,z,config.ShavingPerimeterExclusion), Scratch = .60f + random * .36f,
                    SnowMm = .12f + random * .14f + (sensitive ? .17f : 0),
                    Quality = .12f + random * .09f, LastWetTime = -10000f, LastProcessTime = -10000f
                };
                IceCellCount++;
                if(Cells[i].Shavable)ShavableCellCount++;
            }
        }

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
            var cell = Cells[index];
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
            float overcut=Math.Max(0,cell.CutDepthMm/config.BladeBiteFraction-
                (cell.Sensitive?config.SensitiveCumulativeSafeCutMm:config.CumulativeSafeCutMm));
            cell.Quality=Simulation.Clamp((1-cell.Scratch)*.58f+film*.32f+
                Simulation.Clamp(cell.WashLiters/(area*.1f),0,1)*.10f-cell.SnowMm*.45f-cell.OverwaterMm*.35f-overcut*.5f,0,1);
            cell.Covered=cell.CutDepthMm>.08f*config.BladeBiteFraction&&cell.FloodMm>.08f&&cell.SnowMm<.25f;
        }

        public void MarkChanged() { Revision++; }
        public void MarkCellChanged(int index)
        {
            if(dirtyFlags[index])return;
            dirtyFlags[index]=true;dirtyCells[DirtyCellCount++]=index;
        }
        public int DirtyCellIndex(int n) { return dirtyCells[n]; }
        public void ClearDirtyCells()
        {
            for(int i=0;i<DirtyCellCount;i++)dirtyFlags[dirtyCells[i]]=false;
            DirtyCellCount=0;
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
            if(total>0)MarkChanged();
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
                if(pass==0)total+=weight;
                else if(total>0)
                {
                    var cell=Cells[i]; float mm=volume*1000*weight/(total*CellSize*CellSize);
                    cell.SnowMm+=mm; cell.Quality=Math.Max(0,cell.Quality-mm*.5f);
                    cell.Covered=cell.Covered&&cell.SnowMm<.25f;
                    Cells[i]=cell;MarkCellChanged(i);
                }
            }
            if(total>0)MarkChanged();
            return total>0?volume:0;
        }
    }
}
