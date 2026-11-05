using System;
using System.Diagnostics;
using System.Reflection;
using IceResurfacer.Core;

namespace IceResurfacer.Editor
{
    /// <summary>Production material/geometry checks shared with the standalone runner.</summary>
    public static class OperatorSnowRealismValidation
    {
        static void Require(bool ok,string message)=>OperatorMaterialValidation.Require(ok,message);
        static void Near(double a,double b,double tolerance,string message)=>OperatorMaterialValidation.Near(a,b,tolerance,message);
        static double Stock(Simulation s)=>OperatorMaterialValidation.SnowStock(s);
        static void Balance(Simulation s,double initial)=>Near(Stock(s)-initial-s.TotalExpandedShavingsM3,0,.00005,"snow balance m3");
        static Simulation Make(float pickup=50)=>new Simulation(new SimulatorConfig{LooseSnowPickupDepthMm=pickup});
        static double Band(Simulation s,float x,float width,float from,float to)
        {
            double total=0;
            for(int i=0;i<s.Ice.Cells.Length;i++)
            {
                if(Math.Abs(s.Ice.CellX(i%s.Ice.Columns)-x)>width||s.Ice.CellZ(i/s.Ice.Columns)<from||s.Ice.CellZ(i/s.Ice.Columns)>to)continue;
                total+=s.Ice.Cells[i].SnowMm*s.Ice.CellSize*s.Ice.CellSize*.001;
            }
            return total;
        }
        public static void Run(Action<string> log)
        {
            Settings(log);RestartAndRecollect(log);DeepPasses(log);BoardAndGeometry(log);Driven(log);Performance(log);LongCut(log);
            log("PASS augers-off restart, genuine one-pass recollection, deep repeated pickup, board-clipped volume-normalized facets, driven onset and allocation/performance checks");
        }
        static void Settings(Action<string> log)
        {
            var authored=new SimulatorConfig();var settings=OperatorTuningSettings.From(authored);
            var target=new SimulatorConfig();settings.FloodFlowMultiplier=.4f;settings.WashRecoveryFraction=.644f;settings.LooseSnowPickupDepthMm=50;
            settings.Apply(target,authored);settings.Apply(target,authored);
            Near(target.ConditionerUsableSnowCapacityM3,.0136,.00000001,"default usable retention");
            Near(target.FloodMaxLitersPerSecond,.72,.000001,"existing flood tuning compounded");
            Near(authored.ConditionerSnowCapacityM3,.08,.00000001,"authored setting mutated");
            Require(new RouteFit{configuration=authored}.Compatible(target),"snow tuning invalidated GPS geometry");
            OperatorTuningSettings.Restore(target,authored);
            foreach(var field in typeof(SimulatorConfig).GetFields())
                if(field.FieldType==typeof(float))Near((float)field.GetValue(target),(float)field.GetValue(authored),0,"tuning restore "+field.Name);
            foreach(var field in typeof(OperatorTuningSettings).GetFields())
            foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1,float.MaxValue})
            {
                var invalid=OperatorTuningSettings.From(authored);field.SetValue(invalid,value);bool rejected=false;
                try{invalid.Validate();}catch(ArgumentException){rejected=true;}
                Require(rejected,"invalid physical tuning accepted: "+field.Name);
            }
            log("PASS finite tuning bounds, noncompounding application, exact restore, saved calibration and strict GPS compatibility");
        }
        static void RestartAndRecollect(Action<string> log)
        {
            foreach(float pickup in new[]{6f,50f})
            {
                var s=Make(pickup);var a=new OperatorMaterialValidation.Apparatus(s);double initial=Stock(s);
                s.Controls.BladeDepthMm=.35f;a.Line(0,-24,24);
                double escaped=s.SpilledSnowM3,before=OperatorMaterialValidation.SurfaceSnow(s);
                Require(escaped>.018,"ordinary continued cut failed to create substantial escaped stock");
                float retained=s.HorizontalSnowM3;
                s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;a.Drain();
                Near(s.HorizontalSnowM3+s.VerticalSnowM3,0,1e-8,"restarted augers did not clear reservoirs");
                Near(s.SnowTankM3,retained,.000001,"cleared retention did not enter tank");
                // Blade stays engaged. Two aligned lanes, one over each actual side exit.
                a.Line(-1.265f,-25,25);a.Line(1.265f,-25,25);a.Drain();
                double left=Band(s,-1.265f,.35f,-24,24),right=Band(s,1.265f,.35f,-24,24);
                Near(left+right,0,1e-10,"ordinary escaped deposits remained after one aligned pass");
                Require(s.SnowTankM3>retained+escaped*.9,"picked-up material failed to reach tank");Balance(s,initial);
                log($"restart_recollect pickup_mm={pickup} escaped_transfer_L={escaped*1000:F6} retained_to_tank_L={retained*1000:F6} surface_recovered_L={(before-OperatorMaterialValidation.SurfaceSnow(s))*1000:F6} band_remaining_L={(left+right)*1000:F9} tank_L={s.SnowTankM3*1000:F6} residual_L={(Stock(s)-initial-s.TotalExpandedShavingsM3)*1000:F6}");
                OperatorMaterialValidation.OrdinaryStreak(new SimulatorConfig{LooseSnowPickupDepthMm=pickup},log,true);
            }
        }
        static void DeepPasses(Action<string> log)
        {
            foreach(float pickup in new[]{6f,50f})
            {
                var s=Make(pickup);var a=new OperatorMaterialValidation.Apparatus(s);
                double injected=0;
                // Declared finite fixture source: a 120 mm deep, 15 cm wide bank.
                for(int i=0;i<s.Ice.Cells.Length;i++)
                {
                    float x=s.Ice.CellX(i%s.Ice.Columns),z=s.Ice.CellZ(i/s.Ice.Columns);
                    if(Math.Abs(x)>.075f||z< -1||z>1)continue;
                    var c=s.Ice.Cells[i];c.SnowMm+=120;s.Ice.Cells[i]=c;s.Ice.MarkCellChanged(i);
                    injected+=120*s.Ice.CellSize*s.Ice.CellSize*.001;
                }
                s.Ice.MarkChanged();double initial=Stock(s);int passes=0;
                s.Controls.BladeDepthMm=.35f;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;
                double remaining=Band(s,0,.075f,-1,1),first=0;
                while(remaining>1e-10&&passes<30)
                {
                    a.Line(0,-2,2);a.Drain();passes++;remaining=Band(s,0,.075f,-1,1);if(passes==1)first=remaining;
                    Balance(s,initial);
                }
                Require(first>0&&remaining==0&&passes>1,"deep bank did not require finite repeated collection");
                log($"deep_bank pickup_mm={pickup} fixture_L={injected*1000:F6} after_first_L={first*1000:F6} passes={passes} remaining_L={remaining*1000:F9} tank_L={s.SnowTankM3*1000:F6} spill_transfer_L={s.SpilledSnowM3*1000:F6}");
            }
        }
        static double Integral(SnowSurfacePatch p)
        {
            return (SnowSurfaceGeometry.Area(p.A,p.B,p.Center)*(p.A.Y+p.B.Y+p.Center.Y)+
                SnowSurfaceGeometry.Area(p.B,p.C,p.Center)*(p.B.Y+p.C.Y+p.Center.Y)+
                SnowSurfaceGeometry.Area(p.C,p.D,p.Center)*(p.C.Y+p.D.Y+p.Center.Y)+
                SnowSurfaceGeometry.Area(p.D,p.A,p.Center)*(p.D.Y+p.A.Y+p.Center.Y))/3;
        }
        static void BoardAndGeometry(Action<string> log)
        {
            foreach(bool corner in new[]{false,true})
            {
                var s=Make();var a=new OperatorMaterialValidation.Apparatus(s);double initial=Stock(s);
                s.Controls.BladeDepthMm=.8f;
                a.Line(corner?-10:-11.9f,corner?-27:-5,corner?-23:5);
                Require(s.SpilledSnowM3>0,"board fixture did not spill");
                double ground=0;foreach(var c in s.Environment.Cells)ground+=c.SnowM3;
                Near(ground,0,1e-9,"solid boards leaked snow to environment");Balance(s,initial);
                double represented=0,maximumError=0;int patches=0;
                foreach(float roughness in new[]{0f,.7f,1f})
                {
                    represented=0;patches=0;
                    for(int row=0;row<s.Ice.Rows;row++)for(int col=0;col<s.Ice.Columns;col++)
                    {
                        if(!SnowSurfaceGeometry.Build(s.Ice,col,row,roughness,out var p))continue;
                        Require(p.Center.Y>=0,"negative snow crest");
                        foreach(var point in new[]{p.A,p.B,p.C,p.D,p.Center})
                            Require(s.Ice.IsInside(point.X,point.Z,-.00002f),"snow facet crossed rounded boards");
                        double error=Math.Abs(Integral(p)-p.Volume);maximumError=Math.Max(maximumError,error);
                        Near(Integral(p),p.Volume,1e-10,"facet volume differs from cell snow");represented+=Integral(p);patches++;
                    }
                }
                // A cleared cell owns no triangles, even beside retained banks.
                int index=s.Ice.IndexAt(corner?-10:-11.9f,corner?-25:0);var cell=s.Ice.Cells[index];cell.SnowMm=0;s.Ice.Cells[index]=cell;
                Require(!SnowSurfaceGeometry.Build(s.Ice,index%s.Ice.Columns,index/s.Ice.Columns,1,out _),"cleared cell retained facets");
                log($"board corner={corner} spill_L={s.SpilledSnowM3*1000:F6} ground_L={ground*1000:F9} patches={patches} represented_L={represented*1000:F6} max_integral_error_m3={maximumError:E3} cleared_cell_has_triangles=False");
            }
        }
        public static void Driven(Action<string> log,SimulatorConfig config=null,string label="procedural")
        {
            var s=new Simulation(config??new SimulatorConfig());
            s.Vehicle.X=0;s.Vehicle.Z=-22;s.Vehicle.Heading=0;s.Conditioner.X=0;s.Conditioner.Z=s.Vehicle.Z-s.Config.HitchBehindAxle-s.Config.DrawbarLength;s.Conditioner.Heading=0;s.Conditioner.Lift=0;
            s.Controls.ConditionerTarget=0;s.Controls.BladeDepthMm=.35f;s.Controls.Throttle=.4f;
            double initial=Stock(s);float distance=0,first=-1;
            for(int i=0;i<2200;i++)
            {
                float x=s.Vehicle.X,z=s.Vehicle.Z;s.Step(.02f);
                float dx=s.Vehicle.X-x,dz=s.Vehicle.Z-z;distance+=(float)Math.Sqrt(dx*dx+dz*dz);
                if(s.SpilledSnowM3>=.0005f&&first<0)first=distance;
            }
            Require(first>=13.716f&&first<=27.432f,"driven onset outside 15–30 yards");Balance(s,initial);
            log($"driven scene={label} substantial_distance_m={first:F4} yards={first/.9144f:F4} traveled_m={distance:F4} spilled_transfer_L={s.SpilledSnowM3*1000:F6} retained_L={s.HorizontalSnowM3*1000:F6} residual_L={(Stock(s)-initial-s.TotalExpandedShavingsM3)*1000:F6}");
        }
        static void Performance(Action<string> log)
        {
            var s=Make();var a=new OperatorMaterialValidation.Apparatus(s);s.Controls.BladeDepthMm=.35f;
            for(int i=0;i<800;i++)a.Sample(0,-20+(i%400)*.1f);
            int patches=0;var timer=Stopwatch.StartNew();long bytes=GC.GetAllocatedBytesForCurrentThread();
            for(int pass=0;pass<40;pass++)for(int row=0;row<s.Ice.Rows;row++)for(int col=0;col<s.Ice.Columns;col++)
                if(SnowSurfaceGeometry.Build(s.Ice,col,row,.7f,out _))patches++;
            long allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;timer.Stop();
            Require(allocated==0,"snow facet math allocates during rebuild");
            log($"geometry_perf scans=40 patches={patches} milliseconds={timer.Elapsed.TotalMilliseconds:F3} allocated_bytes={allocated}");
            // Bind hot-path methods once. Reflection fixture setters are excluded
            // from allocation results; production transport itself is measured.
            var convey=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),s,typeof(Simulation).GetMethod("ConveySnow",BindingFlags.Instance|BindingFlags.NonPublic));
            s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;convey(.02f);
            timer.Restart();bytes=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<90000;i++)convey(.02f);
            allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;timer.Stop();
            Require(allocated==0,"steady conveyor update allocates");
            log($"transport_perf simulated_seconds=1800 steps=90000 milliseconds={timer.Elapsed.TotalMilliseconds:F3} allocated_bytes={allocated}");
        }
        static void LongCut(Action<string> log)
        {
            var s=Make();s.Controls.ConditionerTarget=0;s.Conditioner.Lift=0;s.Controls.Throttle=.4f;
            Action<int> pose=lane=>
            {
                s.Vehicle.X=-8+lane%9*2;s.Vehicle.Z=-20;s.Vehicle.Heading=0;s.Vehicle.Speed=1;s.Vehicle.SteeringAngle=0;
                s.Conditioner.X=s.Vehicle.X;s.Conditioner.Z=s.Vehicle.Z-1.1f;s.Conditioner.Heading=0;
            };
            pose(0);s.Controls.BladeDepthMm=.35f;
            for(int i=0;i<1000;i++)s.Step(.02f);
            double initial=Stock(s)-s.TotalExpandedShavingsM3;var timer=Stopwatch.StartNew();long bytes=GC.GetAllocatedBytesForCurrentThread();
            // Repeated prescribed lane resets keep this CPU soak on the sheet.
            // This is not a vehicle route or a rendered-frame benchmark.
            for(int i=0;i<90000;i++)
            {
                if(i%1000==0)pose(i/1000);
                s.Controls.HorizontalAuger=s.Controls.VerticalAuger=i%6000>=3000;
                s.Controls.BladeDepthMm=i<75000?.35f:.8f;s.Step(.02f);
            }
            long allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;timer.Stop();
            Require(allocated==0,"long active cut accumulated managed allocations");Balance(s,initial);
            double ground=0;foreach(var c in s.Environment.Cells)ground+=c.SnowM3;
            log($"active_soak seconds=1800 steps=90000 ms={timer.Elapsed.TotalMilliseconds:F3} allocated_bytes={allocated} initial_loose_L={initial*1000:F6} new_bulk_L={s.TotalExpandedShavingsM3*1000:F6} shaved_solid_L={s.TotalShavedIceM3*1000:F6} trough_L={s.HorizontalSnowM3*1000:F6} throat_L={s.VerticalSnowM3*1000:F6} tank_L={s.SnowTankM3*1000:F6} surface_L={OperatorMaterialValidation.SurfaceSnow(s)*1000:F6} ground_L={ground*1000:F6} pit_L={s.PitSnowM3*1000:F6} spill_transfer_L={s.SpilledSnowM3*1000:F6} residual_L={(Stock(s)-initial-s.TotalExpandedShavingsM3)*1000:F6}");
        }
    }
}
