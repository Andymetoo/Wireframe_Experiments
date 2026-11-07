using System;

namespace IceResurfacer.Core
{
    public sealed partial class IceField
    {
        // Thin working films remain where the towel laid them. Only excess
        // mobile liquid moves; frozen water and scoring history never teleport.
        // This is a bounded conservative height-field solver, not a fluid engine.
        const float SpreadStep = .10f;
        const int SpreadCellBudget = 32768;
        int[] spreadCells;
        bool[] spreadFlags;
        float[] spreadDelta;
        float[] spreadLiquid;
        uint[] spreadLiquidStamp;
        uint spreadStamp;
        int spreadCount,spreadCursor;
        float spreadClock, previousSpreadTime, previousSpreadFactor;
        public double RedistributedWaterLiters { get; private set; }
        public int ActiveStandingWaterCells { get { return spreadCount; } }
        public int LastSpreadCells { get; private set; }

        void TrackStandingWater(int index)
        {
            if(Cells[index].LiquidMm<=config.ExcessFloodDepthMm)return;
            if(spreadCells==null)
            {
                spreadCells=new int[Cells.Length];spreadFlags=new bool[Cells.Length];
                spreadDelta=new float[Cells.Length];
                spreadLiquid=new float[Cells.Length];spreadLiquidStamp=new uint[Cells.Length];
            }
            if(spreadFlags[index])return;
            spreadFlags[index]=true;spreadCells[spreadCount++]=index;
        }

        public void RedistributeWater(float time,float dt)
        {
            spreadClock+=dt;LastSpreadCells=0;
            if(spreadClock+0.000001f<SpreadStep)return;
            spreadClock-=SpreadStep;
            if(spreadCount==0)return;
            // The buffered flow pass reads the same source/neighbor several
            // times. Resolve its exact physical phase only once for this step.
            // Most active cells were resolved together on the preceding step;
            // reuse that identical exponential without quantizing elapsed time.
            if(++spreadStamp==0)
            {
                Array.Clear(spreadLiquidStamp,0,spreadLiquidStamp.Length);
                spreadStamp=1;
            }
            previousSpreadFactor=(float)Math.Exp(-Math.Max(0,time-previousSpreadTime)/config.FreezeSeconds);
            int count=Math.Min(spreadCount,SpreadCellBudget);
            // Delta buffering makes flow independent of traversal direction.
            // All four edges together can transfer at most 80% of source excess.
            int start=spreadCursor;
            for(int n=0;n<count;n++)
            {
                if(spreadCursor>=spreadCount)spreadCursor=0;
                int i=spreadCells[spreadCursor++];LastSpreadCells++;
                float liquid=SpreadLiquidDepth(i,time);
                float mobile=Math.Max(0,liquid-config.ExcessFloodDepthMm);
                if(mobile<=.0001f)continue;
                int col=i%Columns;
                if(col>0)Flow(i,i-1,liquid,mobile,time);
                if(col<Columns-1)Flow(i,i+1,liquid,mobile,time);
                if(i>=Columns)Flow(i,i-Columns,liquid,mobile,time);
                if(i<Cells.Length-Columns)Flow(i,i+Columns,liquid,mobile,time);
            }
            // The active list may grow while applying recipient deltas. Work on
            // the original slice plus its immediate neighbours, never the rink.
            int originalCount=spreadCount,applyCursor=start;
            for(int n=0;n<count;n++)
            {
                if(applyCursor>=originalCount)applyCursor=0;
                int i=spreadCells[applyCursor++];
                ApplySpread(i,time);
                int col=i%Columns;
                if(col>0)ApplySpread(i-1,time);
                if(col<Columns-1)ApplySpread(i+1,time);
                if(i>=Columns)ApplySpread(i-Columns,time);
                if(i<Cells.Length-Columns)ApplySpread(i+Columns,time);
            }
            // Retire the sampled stale entries only after delta application.
            // One swap per expired entry; persistent pooling keeps its location.
            int examined=0,index=Math.Min(start,spreadCount-1);
            while(examined<count&&spreadCount>0)
            {
                if(index>=spreadCount)index=0;
                int i=spreadCells[index];examined++;
                if(SpreadLiquidDepth(i,time)<=config.ExcessFloodDepthMm+.0001f)
                {spreadFlags[i]=false;spreadCells[index]=spreadCells[--spreadCount];}
                else index++;
            }
            spreadCursor=spreadCount>0?index%spreadCount:0;
            previousSpreadTime=time;
            CommitChanges();
        }

        float SpreadLiquidDepth(int i,float time)
        {
            if(spreadLiquidStamp[i]==spreadStamp)return spreadLiquid[i];
            ref readonly IceCell cell=ref Cells[i];
            float factor=cell.LiquidUpdateTime==time?1f:
                cell.LiquidUpdateTime==previousSpreadTime?previousSpreadFactor:
                (float)Math.Exp(-Math.Max(0,time-cell.LiquidUpdateTime)/config.FreezeSeconds);
            float liquid=cell.LiquidMm*factor;
            spreadLiquidStamp[i]=spreadStamp;spreadLiquid[i]=liquid;
            return liquid;
        }

        void Flow(int source,int destination,float liquid,float mobile,float time)
        {
            if(!Cells[destination].IsIce)return; // boards retain liquid
            float other=SpreadLiquidDepth(destination,time);
            float transfer=Math.Min(mobile*.20f,Math.Max(0,liquid-other)*.20f);
            if(transfer<=.000001f)return;
            spreadDelta[source]-=transfer;spreadDelta[destination]+=transfer;
            RedistributedWaterLiters+=(double)transfer*CellSize*CellSize;
        }

        void ApplySpread(int i,float time)
        {
            float delta=spreadDelta[i];if(delta==0)return;
            spreadDelta[i]=0;
            // Same phase accounting as ResolveLiquid, using the exact value
            // already read during buffered flow rather than another exponential.
            ref IceCell cell=ref Cells[i];
            float before=cell.LiquidMm,remaining=SpreadLiquidDepth(i,time);
            float fraction=before>.0000001f?Math.Max(0,1-remaining/before):0;
            cell.FrozenFloodMm+=before-remaining;
            cell.FrozenOverwaterMm+=(cell.OverwaterMm-cell.FrozenOverwaterMm)*fraction;
            cell.LiquidUpdateTime=time;
            cell.LiquidMm=Math.Max(0,remaining+delta);
            spreadLiquid[i]=cell.LiquidMm;
            cell.FloodMm=Math.Max(cell.FrozenFloodMm,cell.FloodMm+delta);
            float threshold=config.ExcessFloodDepthMm*(cell.Sensitive?.78f:1);
            // Redistributing liquid also redistributes its still-recoverable
            // defect. Frozen overwater remains a persistent surface defect.
            cell.OverwaterMm=cell.FrozenOverwaterMm+Math.Max(0,cell.LiquidMm-threshold);
            if(delta>0)cell.LastWetTime=time;
            UpdateQuality(ref cell);MarkCellChanged(i);
        }
    }
}
