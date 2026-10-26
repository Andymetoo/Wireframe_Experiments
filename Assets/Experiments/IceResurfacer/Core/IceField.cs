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
    }
}
