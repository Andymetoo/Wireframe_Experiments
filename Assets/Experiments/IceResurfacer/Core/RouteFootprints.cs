using System;

namespace IceResurfacer.Core
{
    /// <summary>20 cm diagnostic geometry, separate from authoritative 5 cm ice/scoring.</summary>
    public sealed class RouteFootprints
    {
        public const float Cell=.20f;
        public readonly int columns,rows;
        public readonly float minX,minZ;
        public readonly ushort[] plannedBlade,plannedWater,actualBlade,actualWater,expectedBlade,expectedWater;
        public readonly bool[] ice,shavable;
        readonly int[] bladeLeg,waterLeg,prefixBladeLeg,prefixWaterLeg;
        readonly double[] lastBladeVisit,lastWaterVisit;
        readonly SimulatorConfig config;
        readonly RouteFit fit;
        int expectedIndex;
        double actualDistance;
        float lastX,lastZ;
        bool started;
        public int MissedBladeCells {get;private set;}
        public int MissedWaterCells {get;private set;}
        public int ExcessBladeCells {get;private set;}
        public int ExcessWaterCells {get;private set;}
        public int PlannedBladeCells {get;private set;}
        public int PlannedWaterCells {get;private set;}
        public bool ComparingFullPlan {get;private set;}
        public RouteFootprints(RouteFit route)
        {
            fit=route;config=route.configuration;columns=(int)Math.Ceiling(config.RinkWidth/Cell);rows=(int)Math.Ceiling(config.RinkLength/Cell);
            minX=-config.RinkWidth*.5f;minZ=-config.RinkLength*.5f;int count=columns*rows;
            plannedBlade=new ushort[count];plannedWater=new ushort[count];actualBlade=new ushort[count];actualWater=new ushort[count];expectedBlade=new ushort[count];expectedWater=new ushort[count];
            ice=new bool[count];shavable=new bool[count];bladeLeg=new int[count];waterLeg=new int[count];prefixBladeLeg=new int[count];prefixWaterLeg=new int[count];
            lastBladeVisit=new double[count];lastWaterVisit=new double[count];
            var coarse=TrainingRouteDefinition.CopyConfiguration(config);coarse.CellSize=.5f;var rink=new IceField(coarse,546);
            for(int i=0;i<count;i++)
            {
                float x=X(i),z=Z(i);ice[i]=rink.IsInside(x,z);shavable[i]=rink.IsInside(x,z,config.ShavingPerimeterExclusion);
                bladeLeg[i]=waterLeg[i]=prefixBladeLeg[i]=prefixWaterLeg[i]=-1;lastBladeVisit[i]=lastWaterVisit[i]=-1000;
            }
            foreach(var p in route.samples)
            {
                if(p.cutting)Stamp(p.conditionerX,p.conditionerZ,p.conditionerHeading,config.ConditionerWidth,config.ConditionerLength,0,plannedBlade,bladeLeg,p.leg,null,0,true);
                if(p.flooding)Stamp(p.conditionerX,p.conditionerZ,p.conditionerHeading,config.WaterDistributionWidth+2*config.WaterFilmSpreadMeters,config.FloodContactLength,config.FloodContactFore,plannedWater,waterLeg,p.leg,null,0,false);
            }
            for(int i=0;i<count;i++){if(plannedBlade[i]>0)PlannedBladeCells++;if(plannedWater[i]>0)PlannedWaterCells++;}
        }
        public float X(int i) => minX+(i%columns+.5f)*Cell;
        public float Z(int i) => minZ+(i/columns+.5f)*Cell;
        public void Observe(float x,float z,float heading,bool blade,bool flood,double traveled)
        {
            if(blade)Stamp(x,z,heading,config.ConditionerWidth,config.ConditionerLength,0,actualBlade,null,0,lastBladeVisit,traveled,true);
            if(flood)Stamp(x,z,heading,config.WaterDistributionWidth+2*config.WaterFilmSpreadMeters,config.FloodContactLength,config.FloodContactFore,actualWater,null,0,lastWaterVisit,traveled,false);
        }
        public void ObserveSimulation(Simulation s)
        {
            if(started)actualDistance+=TrainingRouteDefinition.Distance(s.Vehicle.X,s.Vehicle.Z,lastX,lastZ);
            started=true;lastX=s.Vehicle.X;lastZ=s.Vehicle.Z;
            Observe(s.Conditioner.X,s.Conditioner.Z,s.Conditioner.Heading,s.Conditioner.Lift<.25f&&s.ActualBladeDepthMm>0&&Math.Abs(s.Vehicle.Speed)>.001f,
                s.Controls.FloodFlow>.001f&&s.FloodWaterLiters>0,actualDistance);
        }
        public void AddRaw(RecordedRouteData raw)
        {foreach(var p in raw.samples)Observe(p.conditionerX,p.conditionerZ,p.conditionerHeadingRadians,p.conditionerLift<.25f&&p.bladeDepthMm>0&&Math.Abs(p.speedMetersPerSecond)>.001f,p.floodValve>.001f,p.distanceMeters);}
        public void AdvanceExpected(double distance)
        {
            while(expectedIndex<fit.samples.Count&&fit.samples[expectedIndex].distance<=distance)
            {
                var p=fit.samples[expectedIndex++];
                if(p.cutting)Stamp(p.conditionerX,p.conditionerZ,p.conditionerHeading,config.ConditionerWidth,config.ConditionerLength,0,expectedBlade,prefixBladeLeg,p.leg,null,0,true);
                if(p.flooding)Stamp(p.conditionerX,p.conditionerZ,p.conditionerHeading,config.WaterDistributionWidth+2*config.WaterFilmSpreadMeters,config.FloodContactLength,config.FloodContactFore,expectedWater,prefixWaterLeg,p.leg,null,0,false);
            }
        }
        public void RebaseExpected(double distance)
        {
            Array.Clear(expectedBlade,0,expectedBlade.Length);Array.Clear(expectedWater,0,expectedWater.Length);
            for(int i=0;i<prefixBladeLeg.Length;i++)prefixBladeLeg[i]=prefixWaterLeg[i]=-1;
            expectedIndex=0;AdvanceExpected(distance);Compare();
        }
        public void Compare(bool fullPlan=false)
        {
            ComparingFullPlan=fullPlan;
            MissedBladeCells=MissedWaterCells=ExcessBladeCells=ExcessWaterCells=0;
            for(int i=0;i<ice.Length;i++)
            {
                ushort cut=fullPlan?plannedBlade[i]:expectedBlade[i],wet=fullPlan?plannedWater[i]:expectedWater[i];
                if(cut>0&&actualBlade[i]==0)MissedBladeCells++;if(wet>0&&actualWater[i]==0)MissedWaterCells++;
                if(actualBlade[i]>Math.Max(2,cut+1))ExcessBladeCells++;if(actualWater[i]>Math.Max(2,wet+1))ExcessWaterCells++;
            }
        }
        void Stamp(float x,float z,float heading,float width,float length,float fore,ushort[] visits,int[] legs,int leg,double[] last,double distance,bool blade)
        {
            float sn=(float)Math.Sin(heading),cs=(float)Math.Cos(heading);x+=sn*fore;z+=cs*fore;float radius=(width+length)*.5f+Cell;
            int x0=Math.Max(0,(int)((x-radius-minX)/Cell)),x1=Math.Min(columns-1,(int)((x+radius-minX)/Cell));
            int z0=Math.Max(0,(int)((z-radius-minZ)/Cell)),z1=Math.Min(rows-1,(int)((z+radius-minZ)/Cell));
            for(int row=z0;row<=z1;row++)for(int col=x0;col<=x1;col++)
            {
                int i=row*columns+col;if(blade?!shavable[i]:!ice[i])continue;float dx=X(i)-x,dz=Z(i)-z;
                if(Math.Abs(dx*cs-dz*sn)>width*.5f||Math.Abs(dx*sn+dz*cs)>length*.5f)continue;
                bool newVisit=legs!=null?legs[i]!=leg:distance-last[i]>2;
                if(newVisit&&visits[i]<ushort.MaxValue)visits[i]++;
                if(legs!=null)legs[i]=leg;else last[i]=distance;
            }
        }
    }
}
