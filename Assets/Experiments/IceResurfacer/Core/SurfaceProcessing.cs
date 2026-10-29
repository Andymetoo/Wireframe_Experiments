using System;

namespace IceResurfacer.Core
{
    public sealed partial class Simulation
    {
        float[] footprintSide,footprintFore,footprintWaterWeight;
        float totalFloodWeight,iceFloodWeight;
        int allCutCells,iceCutCells;
        int reportRevision=-1,reportCovered,reportCut,reportWet,reportOverwater;
        double reportQuality,reportWaterQuality;
        public float CurrentRunoffLitersPerSecond { get; private set; }
        public double TotalOffIceWaterLiters { get; private set; }
        public double TotalRecoveredSurfaceLiters { get; private set; }
        public float SurfacePickupLitersPerSecond { get; private set; }
        public float BrushDeployment { get; private set; }
        public float BrushWorldX { get; private set; }
        public float BrushWorldZ { get; private set; }
        public bool BrushContact { get; private set; }
        public float BrushSweptSnowM3 { get; private set; }
        // Cumulative source/transfer meters. Loose pickup includes subsequent
        // recovery of deposited snow; it is not a second material source.
        public double TotalShavedIceM3 { get; private set; }
        public double TotalExpandedShavingsM3 { get; private set; }
        public double TotalLooseSnowPickupM3 { get; private set; }

        void FindFootprint()
        {
            if(footprintSide==null)
            {
                footprintSide=new float[footprint.Length];
                footprintFore=new float[footprint.Length];
                footprintWaterWeight=new float[footprint.Length];
            }
            footprintCount=allCutCells=iceCutCells=0;totalFloodWeight=iceFloodWeight=0;
            float halfWater=Config.WaterDistributionWidth*.5f;
            float halfWidth=Math.Max(Config.ConditionerWidth*.5f,halfWater+Config.WaterFilmSpreadMeters);
            float cellReach=Ice.CellSize*.707107f;
            float foreMin=Math.Min(-Config.ConditionerLength*.5f,Config.FloodContactFore-Config.FloodContactLength*.5f);
            float foreMax=Math.Max(Config.ConditionerLength*.5f,Config.FloodContactFore+Config.FloodContactLength*.5f);
            float radius=halfWidth+Math.Max(Math.Abs(foreMin),Math.Abs(foreMax))+cellReach;
            int x0=(int)Math.Floor((Conditioner.X-radius-Ice.MinX)/Ice.CellSize);
            int x1=(int)Math.Floor((Conditioner.X+radius-Ice.MinX)/Ice.CellSize);
            int z0=(int)Math.Floor((Conditioner.Z-radius-Ice.MinZ)/Ice.CellSize);
            int z1=(int)Math.Floor((Conditioner.Z+radius-Ice.MinZ)/Ice.CellSize);
            float sn=(float)Math.Sin(Conditioner.Heading),cs=(float)Math.Cos(Conditioner.Heading);
            for(int row=z0;row<=z1;row++)
            for(int col=x0;col<=x1;col++)
            {
                float dx=Ice.CellX(col)-Conditioner.X,dz=Ice.CellZ(row)-Conditioner.Z;
                float side=dx*cs-dz*sn,fore=dx*sn+dz*cs,absSide=Math.Abs(side);
                if(absSide>halfWidth+cellReach||fore<foreMin-cellReach||fore>foreMax+cellReach)continue;
                bool cutting=absSide<=Config.ConditionerWidth*.5f&&Math.Abs(fore)<=Config.ConditionerLength*.5f;
                if(cutting)allCutCells++;
                float weight=FloodCellWeight(side,fore,halfWater,sn,cs,cellReach);
                totalFloodWeight+=weight;
                if(col<0||col>=Ice.Columns||row<0||row>=Ice.Rows)continue;
                int index=Ice.Index(col,row);if(!Ice.Cells[index].IsIce)continue;
                if(cutting)iceCutCells++;
                footprint[footprintCount]=index;footprintSide[footprintCount]=side;
                footprintFore[footprintCount]=fore;footprintWaterWeight[footprintCount]=weight;
                iceFloodWeight+=weight;footprintCount++;
            }
            // Boards retain the cloth fringe; an off-ice source splits its
            // finite supply between actual ice cells and floor runoff.
            if(Ice.IsInside(Conditioner.X,Conditioner.Z)&&!Layout.IsPortal(Conditioner.X,Conditioner.Z))
            { totalFloodWeight=iceFloodWeight;allCutCells=iceCutCells; }
        }

        float FloodWeight(float side,float halfWater)
        {
            if(side<=halfWater)return (1-Config.WaterFilmFringeFraction)/Config.WaterDistributionWidth;
            if(Config.WaterFilmSpreadMeters<=0)return 0;
            float fringe=(side-halfWater)/Config.WaterFilmSpreadMeters;
            return Math.Max(0,1-fringe)*Config.WaterFilmFringeFraction/Config.WaterFilmSpreadMeters;
        }

        float FloodCellWeight(float side,float fore,float halfWater,float sn,float cs,float cellReach)
        {
            float length=Config.FloodContactLength*.5f,offset=fore-Config.FloodContactFore;
            if(Math.Abs(offset)>length+cellReach||Math.Abs(side)>halfWater+Config.WaterFilmSpreadMeters+cellReach)return 0;
            // Interior samples share a constant density. Only boundary/fringe
            // cells need area sampling, avoiding four tests across the whole pad.
            if(Math.Abs(side)<halfWater-cellReach&&Math.Abs(offset)<length-cellReach)
                return (1-Config.WaterFilmFringeFraction)/Config.WaterDistributionWidth;
            float quarter=Ice.CellSize*.25f,weight=0;
            for(int x=-1;x<=1;x+=2)
            for(int z=-1;z<=1;z+=2)
            {
                float sampleSide=side+quarter*(x*cs-z*sn);
                float sampleFore=offset+quarter*(x*sn+z*cs);
                if(Math.Abs(sampleFore)<=length)weight+=FloodWeight(Math.Abs(sampleSide),halfWater);
            }
            return weight*.25f;
        }

        void ProcessIce(float dt)
        {
            float pressure=Clamp(1-Conditioner.Lift/.25f,0,1);
            float dx=Conditioner.X-lastConditionerX,dz=Conditioner.Z-lastConditionerZ;
            float distance=(float)Math.Sqrt(dx*dx+dz*dz);
            float advance=Math.Min(1,distance/Config.ConditionerLength),area=Ice.CellSize*Ice.CellSize;
            float flood=Math.Min(FloodWaterLiters,Controls.FloodFlow*Config.FloodMaxLitersPerSecond*dt);
            float wash=Math.Min(WashWaterLiters,Controls.WashValve*Config.WashMaxLitersPerSecond*dt);
            FloodWaterLiters-=flood;TotalFloodUsedLiters+=flood;WashWaterLiters-=wash;
            float availableLiquid=0;
            if(Controls.WashPump&&pressure>.8f)
                for(int n=0;n<footprintCount;n++)
                    if(InPickup(n))
                        availableLiquid+=Math.Max(0,Ice.LiquidDepth(Ice.Cells[footprint[n]],Time)-Config.WashPickupResidualMm)*area;
            if(wash>.00001f&&pressure>.8f&&footprintCount>0)
                WashPrimeSeconds=Math.Min(30,WashPrimeSeconds+dt);
            else if(!(Controls.WashPump&&availableLiquid>.0001f))
                WashPrimeSeconds=Math.Max(0,WashPrimeSeconds-dt*2);
            bool pumpReady=WashPrimeSeconds>=10&&pressure>.8f&&footprintCount>0;
            if(Controls.WashPump&&!pumpReady)
            {
                PumpDamage=Math.Min(1,PumpDamage+dt*.007f);
                Fault(7,"Wash pickup pump is dry. Prime with the wash valve for 10 seconds before starting it.");
            }
            float pumpCapacity=Controls.WashPump&&pumpReady?
                Config.WashPickupLitersPerSecond*dt*(1-PumpDamage*.75f):0;
            float recovery=Math.Min(pumpCapacity,wash*Config.WashRecoveryFraction);
            recovery=Math.Min(recovery,Math.Max(0,Config.WashCapacityLiters-WashWaterLiters));
            WashWaterLiters+=recovery;TotalWashUsedLiters+=wash-recovery;
            // Fresh wash return and old surface water share one finite pump and
            // one tank. A full wash reservoir leaves the excess on the sheet.
            float pickup=Math.Min(availableLiquid,Math.Min(Math.Max(0,pumpCapacity-recovery)*Config.WashRecoveryFraction,
                Math.Max(0,Config.WashCapacityLiters-WashWaterLiters)));
            float actuallyPickedUp=0;SurfacePickupLitersPerSecond=0;
            if(flood>0&&Math.Abs(Vehicle.Speed)<.12f)Fault(8,"Flood water is pooling while stopped. Close the flow lever.");
            if(Controls.WashValve>.01f&&WashWaterLiters<=.001f)Fault(9,"Wash-water tank empty: shut the valve and refill.");
            if(Controls.FloodFlow>.01f&&FloodWaterLiters<=.001f)Fault(10,"Ice-making water tank empty: refill when convenient.");
            float floodOnIce=totalFloodWeight>0?flood*iceFloodWeight/totalFloodWeight:0;
            float washOnIce=allCutCells>0?(wash-recovery)*iceCutCells/allCutCells:0;
            float runoff=Math.Max(0,flood-floodOnIce+wash-recovery-washOnIce);
            CurrentRunoffLitersPerSecond=runoff/dt;TotalOffIceWaterLiters+=runoff;
            float washDepth=iceCutCells>0?washOnIce/(iceCutCells*area):0;
            float washPerCell=allCutCells>0?wash/allCutCells:0;
            float blade=ActualBladeDepthMm*pressure*advance,newSnow=0;bool anyChanged=false;
            double shaved=0,loosePickup=0;
            for(int n=0;n<footprintCount;n++)
            {
                int index=footprint[n];var cell=Ice.Cells[index];
                bool cutting=Math.Abs(footprintSide[n])<=Config.ConditionerWidth*.5f&&Math.Abs(footprintFore[n])<=Config.ConditionerLength*.5f;
                float cellPickup=0;
                if(pickup>0&&InPickup(n))
                {
                    float recoverable=Math.Max(0,Ice.LiquidDepth(cell,Time)-Config.WashPickupResidualMm)*area;
                    cellPickup=Ice.RemoveLiquid(ref cell,pickup*recoverable/availableLiquid/area,Time)*area;
                    actuallyPickedUp+=cellPickup;
                }
                float waterDepth=(totalFloodWeight>0?flood*footprintWaterWeight[n]/totalFloodWeight/area:0)+(cutting?washDepth:0);
                float snowRemoved=cutting?Math.Min(cell.SnowMm,pressure*advance*Config.LooseSnowPickupDepthMm):0;
                float bite=cutting?blade:0;
                bool washContact=cutting&&washPerCell>0&&pressure>.7f;
                if(waterDepth<=0&&bite<=0&&snowRemoved<=0&&cellPickup<=0&&!washContact)continue;
                Ice.ApplyBladeCut(ref cell,bite,Vehicle.Speed<-.02f);
                cell.SnowMm-=snowRemoved;newSnow+=(snowRemoved+bite*Config.SnowExpansionRatio)*area*.001f;
                shaved+=(double)bite*area*.001;loosePickup+=(double)snowRemoved*area*.001;
                if(washContact)
                { cell.WashLiters+=washPerCell;cell.Scratch=Math.Max(Ice.GrooveRoughness(cell),cell.Scratch-washPerCell/area*.025f); }
                Ice.AddWater(ref cell,waterDepth,Time);
                Ice.UpdateQuality(ref cell);cell.LastProcessTime=Time;Ice.Cells[index]=cell;
                Ice.MarkCellChanged(index);anyChanged=true;
            }
            WashWaterLiters=Math.Min(Config.WashCapacityLiters,WashWaterLiters+actuallyPickedUp);
            TotalRecoveredSurfaceLiters+=actuallyPickedUp;SurfacePickupLitersPerSecond=actuallyPickedUp/dt;
            // Pickup above uses the availability sampled at the start of this
            // tick. Portal runoff may land on those same cells: deposit it only
            // after recovery, so it cannot inflate the fixed pickup budget.
            if(runoff>0)
            {
                float sn=(float)Math.Sin(Conditioner.Heading),cs=(float)Math.Cos(Conditioner.Heading);
                float floodSpill=Math.Max(0,flood-floodOnIce),washSpill=Math.Max(0,wash-recovery-washOnIce);
                DepositRunoff(Conditioner.X+sn*Config.FloodContactFore,Conditioner.Z+cs*Config.FloodContactFore,floodSpill);
                DepositRunoff(Conditioner.X+sn*Config.WashPickupContactFore,Conditioner.Z+cs*Config.WashPickupContactFore,washSpill);
            }
            TotalShavedIceM3+=shaved;TotalExpandedShavingsM3+=shaved*Config.SnowExpansionRatio;TotalLooseSnowPickupM3+=loosePickup;
            HorizontalSnowM3+=newSnow;UpdateBoardBrush(dt,distance);
            if(anyChanged)Ice.CommitChanges();
        }

        bool InPickup(int n)
        {
            return Math.Abs(footprintSide[n])<=Config.ConditionerWidth*.5f&&
                Math.Abs(footprintFore[n]-Config.WashPickupContactFore)<=Config.WashPickupContactLength*.5f;
        }

        void UpdateBoardBrush(float dt,float distance)
        {
            BrushDeployment=MoveTowards(BrushDeployment,Controls.BoardBrush?1:0,dt*1.6f);
            float angle=(1-BrushDeployment)*Config.BrushRetractedDegrees*(float)Math.PI/180;
            float localX=Config.BrushPivotX-Config.BrushArmLength*(float)Math.Cos(angle);
            float localZ=Config.BrushPivotZ-Config.BrushArmLength*(float)Math.Sin(angle);
            float sn=(float)Math.Sin(Vehicle.Heading),cs=(float)Math.Cos(Vehicle.Heading);
            BrushWorldX=Vehicle.X+localX*cs+localZ*sn;BrushWorldZ=Vehicle.Z-localX*sn+localZ*cs;
            float boardDistance=Ice.BoardDistance(BrushWorldX,BrushWorldZ,out float nx,out float nz);
            BrushContact=BrushDeployment>.5f&&Ice.IsInside(Vehicle.X,Vehicle.Z)&&boardDistance>=-Config.BrushRadius-.025f;
            if(BrushContact&&boardDistance>-Config.BrushRadius)
            {
                float compression=boardDistance+Config.BrushRadius;
                BrushWorldX+=nx*compression;BrushWorldZ+=nz*compression;
            }
            if(!Controls.BoardBrush||BrushDeployment<.5f||distance<=0)return;
            BrushEdge(distance);
        }

        void BrushEdge(float distance)
        {
            int cx=(int)Math.Floor((BrushWorldX-Ice.MinX)/Ice.CellSize),cz=(int)Math.Floor((BrushWorldZ-Ice.MinZ)/Ice.CellSize);
            int reach=(int)Math.Ceiling(Config.BrushRadius/Ice.CellSize);
            float moved=0;
            for(int row=Math.Max(0,cz-reach);row<=Math.Min(Ice.Rows-1,cz+reach);row++)
            for(int col=Math.Max(0,cx-reach);col<=Math.Min(Ice.Columns-1,cx+reach);col++)
            {
                int i=Ice.Index(col,row);var cell=Ice.Cells[i];if(!cell.IsIce)continue;
                float dx=Ice.CellX(col)-BrushWorldX,dz=Ice.CellZ(row)-BrushWorldZ;
                if(dx*dx+dz*dz>Config.BrushRadius*Config.BrushRadius)continue;
                float amount=Math.Min(cell.SnowMm,distance*.6f);if(amount<=0)continue;
                cell.SnowMm-=amount;Ice.UpdateQuality(ref cell);Ice.Cells[i]=cell;Ice.MarkCellChanged(i);
                moved+=amount*Ice.CellSize*Ice.CellSize*.001f;
            }
            if(moved<=0)return;
            Ice.CommitChanges();
            // Sweeping redistributes snow toward the conditioner; it does not
            // award cutting coverage or teleport it directly into the tank.
            float tx=Vehicle.X-(float)Math.Cos(Vehicle.Heading)*.72f;
            float tz=Vehicle.Z+(float)Math.Sin(Vehicle.Heading)*.72f;
            float placed=Ice.DepositSnow(tx,tz,moved,.20f);
            if(placed<moved) { Environment.Deposit(tx,tz,moved-placed,0);OffIceSnowM3+=moved-placed; }
            BrushSweptSnowM3+=moved;
        }

        public RunReport GetReport()
        {
            // Surface totals are unchanged while walking, viewing controls or
            // sitting idle. Driver/time/misuse values below stay live.
            if(reportRevision!=Ice.Revision)
            {
                var totals=Ice.GetSurfaceTotals();
                reportCovered=totals.Covered;reportCut=totals.Cut;
                reportWet=totals.Wet;reportOverwater=totals.Overwater;
                reportQuality=totals.Quality;reportWaterQuality=totals.WaterQuality;
                reportRevision=Ice.Revision;
            }
            float coverage=(float)reportCovered/Math.Max(1,Ice.ShavableCellCount);
            float q=(float)(reportQuality/Ice.IceCellCount),over=(float)reportOverwater/Ice.IceCellCount;
            double groundSnow=0;foreach(var ground in Environment.Cells)groundSnow+=ground.SnowM3;
            // Conveyed throughput can count the same snow twice after a spill
            // and recollection. Judge the current material state instead.
            float tank=TotalSnowCollectedM3<.005f||
                (PitSnowM3>.0001f&&SnowTankM3<.005f&&HorizontalSnowM3<.001f&&VerticalSnowM3<.001f&&groundSnow<.001f)?1:0;
            float water=(float)(reportWaterQuality/Ice.IceCellCount);
            float usefulLiters=(float)(reportWaterQuality*Ice.CellSize*Ice.CellSize*Config.GoodFloodDepthMm);
            float efficiency=TotalFloodUsedLiters>.1f?Clamp(usefulLiters/TotalFloodUsedLiters,0,1):0;
            float coveragePoints=coverage*40,finishPoints=q*30,waterPoints=water*10;
            float overwaterPoints=(1-over)*10,efficiencyPoints=efficiency*5;
            float handlingPoints=(tank+(ProperExit?1:0))*2.5f,misuse=Math.Min(30,Mistakes*1.2f);
            return new RunReport
            {
                Coverage=coverage,ShavingCoverage=(float)reportCut/Math.Max(1,Ice.ShavableCellCount),
                WaterCoverage=(float)reportWet/Ice.IceCellCount,WaterDistribution=water,
                MissedFraction=1-coverage,FinishQuality=q,OverwaterFraction=over,OperationalEfficiency=efficiency,
                CoveragePoints=coveragePoints,FinishPoints=finishPoints,WaterPoints=waterPoints,
                OverwaterPoints=overwaterPoints,EfficiencyPoints=efficiencyPoints,HandlingPoints=handlingPoints,MisusePenalty=misuse,
                Score=Practice?0:Clamp(coveragePoints+finishPoints+waterPoints+overwaterPoints+efficiencyPoints+handlingPoints-misuse,0,100),
                Mistakes=Mistakes,TankDumps=TankDumps,ProperExit=ProperExit,
                Complete=Finished&&coverage>=Config.MinimumScoredCoverage,Practice=Practice,ElapsedSeconds=Time
            };
        }
    }
}
