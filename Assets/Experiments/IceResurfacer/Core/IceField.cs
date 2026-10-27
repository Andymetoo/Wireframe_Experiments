using System;

namespace IceResurfacer.Core
{
    public struct IceCell
    {
        public bool IsIce, Sensitive, Covered;
        public float Scratch, SnowMm, CutDepthMm, WashLiters, FloodMm, Quality, OverwaterMm, LastWetTime;
        internal float LastProcessTime;
    }

    /// <summary>Compact spatial state. One cell is an area sample, never a Unity object.</summary>
    public sealed class IceField
    {
        public readonly int Columns, Rows;
        public readonly float CellSize, MinX, MinZ;
        public readonly IceCell[] Cells;
        public readonly int IceCellCount;
        readonly SimulatorConfig config;

        public IceField(SimulatorConfig config, int seed)
        {
            this.config = config;
            CellSize = config.CellSize;
            Columns = (int)Math.Ceiling(config.RinkWidth / CellSize);
            Rows = (int)Math.Ceiling(config.RinkLength / CellSize);
            MinX = -config.RinkWidth * .5f;
            MinZ = -config.RinkLength * .5f;
            Cells = new IceCell[Columns * Rows];
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
                    IsIce = true, Sensitive = sensitive, Scratch = .60f + random * .36f,
                    SnowMm = .12f + random * .14f + (sensitive ? .17f : 0),
                    Quality = .12f + random * .09f, LastWetTime = -10000f, LastProcessTime = -10000f
                };
                IceCellCount++;
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
            if (!cell.IsIce || cell.FloodMm <= 0) return 0;
            float age = Math.Max(0, time - cell.LastWetTime);
            return Math.Min(1f, cell.FloodMm / config.GoodFloodDepthMm) * (float)Math.Exp(-age / config.FreezeSeconds);
        }

        internal float LiquidDepth(IceCell cell, float time)
        {
            return cell.FloodMm * (float)Math.Exp(-Math.Max(0, time - cell.LastWetTime) / config.FreezeSeconds);
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
                    Cells[i]=cell;
                }
            }
            return total>0?volume:0;
        }
    }
}
