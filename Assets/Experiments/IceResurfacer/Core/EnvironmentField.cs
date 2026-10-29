using System;

namespace IceResurfacer.Core
{
    public struct GroundCell { public float SnowM3, WaterLiters, WaterX, WaterZ; }
    /// <summary>Persistent material on the non-ice floor. Fixed storage avoids an object for every spill.</summary>
    public sealed class EnvironmentField
    {
        public readonly float CellSize=1f, MinX, MinZ;
        public readonly int Columns,Rows;
        public readonly GroundCell[] Cells;
        public int Revision { get; private set; }
        public double DrainedWaterLiters { get; private set; }
        public double DepositedWaterLiters { get; private set; }
        readonly float[] waterDelta,waterMomentX,waterMomentZ;
        readonly bool[] floorCell;
        bool floorReady;
        float waterClock;
        public EnvironmentField(SimulatorConfig config,ServiceLayout layout)
        {
            MinX=-config.RinkWidth*.5f-4; MinZ=-config.RinkLength*.5f-config.BayLength-8;
            Columns=(int)Math.Ceiling(config.RinkWidth+config.BayLength+16);
            Rows=(int)Math.Ceiling(config.RinkLength+config.BayLength+16);
            Cells=new GroundCell[Columns*Rows];
            waterDelta=new float[Cells.Length];waterMomentX=new float[Cells.Length];waterMomentZ=new float[Cells.Length];floorCell=new bool[Cells.Length];
        }
        public float CellX(int column){return MinX+(column+.5f)*CellSize;}
        public float CellZ(int row){return MinZ+(row+.5f)*CellSize;}
        public void Deposit(float x,float z,float snowM3,float waterLiters)
        {
            // Machine and spray remain within these generous authored-world bounds.
            int col=Math.Max(0,Math.Min(Columns-1,(int)Math.Floor((x-MinX)/CellSize)));
            int row=Math.Max(0,Math.Min(Rows-1,(int)Math.Floor((z-MinZ)/CellSize)));
            int index=row*Columns+col;
            var cell=Cells[index];cell.SnowM3+=Math.Max(0,snowM3);
            if(waterLiters>0)
            {
                float total=cell.WaterLiters+waterLiters;
                cell.WaterX=(cell.WaterX*cell.WaterLiters+x*waterLiters)/total;
                cell.WaterZ=(cell.WaterZ*cell.WaterLiters+z*waterLiters)/total;
                cell.WaterLiters=total;
                DepositedWaterLiters+=waterLiters;
            }
            Cells[index]=cell;Revision++;
        }
        public void TickWater(float dt,IceField ice,ServiceLayout layout)
        {
            if(DepositedWaterLiters<=DrainedWaterLiters+.000001)return;
            waterClock+=dt;if(waterClock<.2f)return;float step=Math.Min(waterClock,.25f);waterClock=0;
            if(!floorReady)
            {
                for(int i=0;i<Cells.Length;i++)
                {float x=CellX(i%Columns),z=CellZ(i/Columns);floorCell[i]=layout.ContainsFloor(x,z,.025f)&&!layout.ContainsPit(x,z)&&!ice.IsInside(x,z);}
                floorReady=true;
            }
            layout.FloorDrain(out float drainX,out float drainZ);
            Array.Clear(waterDelta,0,waterDelta.Length);Array.Clear(waterMomentX,0,waterMomentX.Length);Array.Clear(waterMomentZ,0,waterMomentZ.Length);
            bool changed=false;
            for(int i=0;i<Cells.Length;i++)
            {
                var cell=Cells[i];if(cell.WaterLiters<=.02f||!layout.ContainsFloor(cell.WaterX,cell.WaterZ)||layout.ContainsPit(cell.WaterX,cell.WaterZ)||ice.IsInside(cell.WaterX,cell.WaterZ))continue;
                int col=i%Columns,row=i/Columns;
                float sourceHead=cell.WaterLiters+.6f*Distance(cell.WaterX,cell.WaterZ,drainX,drainZ);
                for(int n=0;n<4;n++)
                {
                    int nc=col+(n==0?-1:n==1?1:0),nr=row+(n==2?-1:n==3?1:0);
                    if(nc<0||nc>=Columns||nr<0||nr>=Rows)continue;
                    int next=nr*Columns+nc;if(!floorCell[next])continue;
                    float cx=CellX(nc),cz=CellZ(nr),head=Cells[next].WaterLiters+.6f*Distance(cx,cz,drainX,drainZ);
                    float flow=Math.Min((cell.WaterLiters-.02f)*.24f,Math.Max(0,sourceHead-head-.04f)*.35f*step);
                    if(flow<.000001f)continue;
                    float px=Math.Max(cx-.48f,Math.Min(cx+.48f,cell.WaterX)),pz=Math.Max(cz-.48f,Math.Min(cz+.48f,cell.WaterZ));
                    if(!layout.ContainsFloor(px,pz,.025f)||layout.ContainsPit(px,pz)||ice.IsInside(px,pz)){px=cx;pz=cz;}
                    bool clear=true;
                    for(int sample=1;sample<4;sample++)
                    {
                        float t=sample*.25f,tx=cell.WaterX+(px-cell.WaterX)*t,tz=cell.WaterZ+(pz-cell.WaterZ)*t;
                        if(!layout.ContainsFloor(tx,tz,.025f)||layout.ContainsPit(tx,tz)||ice.IsInside(tx,tz)){clear=false;break;}
                    }
                    if(!clear)continue;
                    waterDelta[i]-=flow;waterMomentX[i]-=flow*cell.WaterX;waterMomentZ[i]-=flow*cell.WaterZ;
                    waterDelta[next]+=flow;waterMomentX[next]+=flow*px;waterMomentZ[next]+=flow*pz;changed=true;
                }
            }
            if(changed)for(int i=0;i<Cells.Length;i++)
            {
                if(waterDelta[i]==0&&waterMomentX[i]==0&&waterMomentZ[i]==0)continue;
                var cell=Cells[i];float total=Math.Max(0,cell.WaterLiters+waterDelta[i]);
                if(total>.000001f){cell.WaterX=(cell.WaterX*cell.WaterLiters+waterMomentX[i])/total;cell.WaterZ=(cell.WaterZ*cell.WaterLiters+waterMomentZ[i])/total;}
                cell.WaterLiters=total;Cells[i]=cell;
            }
            int dc=Math.Max(0,Math.Min(Columns-1,(int)((drainX-MinX)/CellSize))),dr=Math.Max(0,Math.Min(Rows-1,(int)((drainZ-MinZ)/CellSize)));
            int drainIndex=dr*Columns+dc;var drain=Cells[drainIndex];float removed=Math.Min(drain.WaterLiters,1.4f*step);
            if(removed>0){drain.WaterLiters-=removed;Cells[drainIndex]=drain;DrainedWaterLiters+=removed;changed=true;}
            if(changed)Revision++;
        }
        static float Distance(float x,float z,float tx,float tz){float dx=x-tx,dz=z-tz;return(float)Math.Sqrt(dx*dx+dz*dz);}
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
