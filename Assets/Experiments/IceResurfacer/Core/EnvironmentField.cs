using System;

namespace IceResurfacer.Core
{
    public struct GroundCell { public float SnowM3, WaterLiters; }
    /// <summary>Persistent material on the non-ice floor. Fixed storage avoids an object for every spill.</summary>
    public sealed class EnvironmentField
    {
        public readonly float CellSize=1f, MinX, MinZ;
        public readonly int Columns,Rows;
        public readonly GroundCell[] Cells;
        public int Revision { get; private set; }
        public EnvironmentField(SimulatorConfig config,ServiceLayout layout)
        {
            MinX=-config.RinkWidth*.5f-4; MinZ=-config.RinkLength*.5f-config.BayLength-8;
            Columns=(int)Math.Ceiling(config.RinkWidth+config.BayLength+16);
            Rows=(int)Math.Ceiling(config.RinkLength+config.BayLength+16);
            Cells=new GroundCell[Columns*Rows];
        }
        public float CellX(int column){return MinX+(column+.5f)*CellSize;}
        public float CellZ(int row){return MinZ+(row+.5f)*CellSize;}
        public void Deposit(float x,float z,float snowM3,float waterLiters)
        {
            // Machine and spray remain within these generous authored-world bounds.
            int col=Math.Max(0,Math.Min(Columns-1,(int)Math.Floor((x-MinX)/CellSize)));
            int row=Math.Max(0,Math.Min(Rows-1,(int)Math.Floor((z-MinZ)/CellSize)));
            int index=row*Columns+col;
            var cell=Cells[index];cell.SnowM3+=Math.Max(0,snowM3);cell.WaterLiters+=Math.Max(0,waterLiters);Cells[index]=cell;Revision++;
        }
    }

    public sealed partial class Simulation
    {
        public void DepositRunoff(float x,float z,float liters)
        {
            if(liters<=0)return;
            float stored=Ice.IsInside(x,z)?Ice.DepositWater(x,z,liters,Time,.25f):0;
            if(liters>stored)Environment.Deposit(x,z,0,liters-stored);
        }
    }
}
