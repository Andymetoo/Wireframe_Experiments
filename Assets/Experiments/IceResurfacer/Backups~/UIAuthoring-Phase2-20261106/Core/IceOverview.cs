using System;

namespace IceResurfacer.Core
{
    /// <summary>Area summary for an operator overview, separate from exact-cell scoring.</summary>
    public struct IceOverviewSample
    {
        public float IceFraction, WetCut, WetUncut, DryCut, Unprocessed, Excluded;
        public float SnowDepthMm, ExcessFraction, CutDeficitFraction, WaterDeficitFraction;
        public int IceCells;
    }

    public static class IceOverview
    {
        // A 20 cm overview pixel represents sixteen 5 cm physical cells. It is
        // independent of the screen size; resizing cannot choose random samples.
        public static int CellsPerPixel(IceField field)
        {return Math.Max(1,(int)Math.Round(.20f/field.CellSize));}

        public static IceOverviewSample Summarize(IceField field,SimulatorConfig config,int left,int bottom,int span)
        {
            IceOverviewSample result=default;int total=0;
            for(int row=bottom;row<Math.Min(field.Rows,bottom+span);row++)
            for(int col=left;col<Math.Min(field.Columns,left+span);col++)
            {
                total++;var cell=field.Cells[field.Index(col,row)];if(!cell.IsIce)continue;
                result.IceCells++;
                bool cut=cell.CutDepthMm>IceCoverage.RequiredCutMm(config);
                bool wet=cell.FloodMm>IceCoverage.RequiredFloodMm;
                if(wet) {if(cut)result.WetCut++;else result.WetUncut++;}
                else if(cut)result.DryCut++;
                else if(!cell.Shavable)result.Excluded++;
                else result.Unprocessed++;
                if(cell.Shavable&&!cut)result.CutDeficitFraction++;
                if(!wet)result.WaterDeficitFraction++;
                result.SnowDepthMm+=cell.SnowMm;
                if(cell.OverwaterMm>IceCoverage.ExcessWaterMm)result.ExcessFraction++;
            }
            if(result.IceCells==0)return result;
            float inverse=1f/result.IceCells;
            result.IceFraction=(float)result.IceCells/Math.Max(1,total);
            result.WetCut*=inverse;result.WetUncut*=inverse;result.DryCut*=inverse;
            result.Unprocessed*=inverse;result.Excluded*=inverse;
            result.SnowDepthMm*=inverse;result.ExcessFraction*=inverse;
            result.CutDeficitFraction*=inverse;result.WaterDeficitFraction*=inverse;
            return result;
        }
    }
}
