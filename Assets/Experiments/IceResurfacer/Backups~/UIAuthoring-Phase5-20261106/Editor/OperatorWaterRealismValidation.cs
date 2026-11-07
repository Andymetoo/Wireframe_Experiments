using System;
using IceResurfacer.Core;

namespace IceResurfacer.Editor
{
    // The same operating sequences can run against the before/after source copies.
    // No new-state property is required, so baseline failures are measurable too.
    public static class OperatorWaterRealismValidation
    {
        static void Tick(Simulation s,float seconds)
        { for(int i=0;i<(int)Math.Round(seconds/.02f);i++)s.Step(.02f); }
        static void Require(bool condition,bool enforce,string message)
        { if(enforce&&!condition)throw new InvalidOperationException(message); }
        static Simulation Ready(float washFraction=1)
        {
            var s=new Simulation(practice:true);
            if(!s.ConfigureWaterForServiceTest(1,washFraction))throw new InvalidOperationException("fixture tank setup");
            s.Vehicle.X=0;s.Vehicle.Z=-12;s.Vehicle.Heading=0;
            s.Conditioner.X=0;s.Conditioner.Z=-12-s.Config.HitchBehindAxle-s.Config.DrawbarLength;
            s.Conditioner.Heading=0;s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;
            return s;
        }
        static void WetLane(Simulation s,bool frozen=false)
        {
            for(int i=0;i<s.Ice.Cells.Length;i++)
            {
                var c=s.Ice.Cells[i];if(!c.IsIce)continue;
                float x=s.Ice.CellX(i%s.Ice.Columns),z=s.Ice.CellZ(i/s.Ice.Columns);
                if(Math.Abs(x)>1.5f||Math.Abs(z)>18)continue;
                s.Ice.AddWater(ref c,1.2f,frozen?-20000:0);s.Ice.Cells[i]=c;s.Ice.MarkCellChanged(i);
            }
            s.Ice.MarkChanged();
        }
        public static void Run(Action<string> log,bool enforce=true)
        {
            var s=Ready();double initial=OperatorMaterialValidation.WaterStock(s);
            s.Controls.WashValve=1;Tick(s,11);s.Controls.WashPump=true;
            double before=s.TotalWashRecirculatedLiters;float damage=s.PumpDamage;
            Tick(s,5);double unpowered=s.TotalWashRecirculatedLiters-before;
            Require(unpowered==0&&s.PumpDamage==damage&&s.Controls.WashPump,enforce,"unpowered pump returned water, wore impeller or changed switch");
            s.Controls.HorizontalAuger=true;before=s.TotalWashRecirculatedLiters;Tick(s,5);
            double powered=s.TotalWashRecirculatedLiters-before;
            Require(powered>4,enforce,"horizontal drive failed to restore wash return");
            s.Controls.HorizontalAuger=false;before=s.TotalWashRecirculatedLiters;Tick(s,2);
            double stopped=s.TotalWashRecirculatedLiters-before;
            Require(stopped==0,enforce,"stopping horizontal auger did not stop return");
            s.Controls.HorizontalAuger=true;
            typeof(Simulation).GetProperty("ConveyorJammed").SetValue(s,true);
            before=s.TotalWashRecirculatedLiters;damage=s.PumpDamage;Tick(s,2);
            double jammed=s.TotalWashRecirculatedLiters-before;
            Require(jammed==0&&s.PumpDamage==damage,enforce,"jammed horizontal drive ran pickup pump");
            double error=OperatorMaterialValidation.WaterStock(s)-initial;
            Require(Math.Abs(error)<.05,enforce,"drive-dependency water imbalance");
            log($"drive: H_off_return_5s_L={unpowered:F6} H_on_return_5s_L={powered:F6} stopped_return_2s_L={stopped:F6} jam_return_2s_L={jammed:F6} balance_L={error:F6}");

            var dry=Ready(0);dry.Controls.WashPump=true;Tick(dry,2);float offDamage=dry.PumpDamage;
            dry.Controls.HorizontalAuger=true;Tick(dry,2);float drivenDamage=dry.PumpDamage-offDamage;
            Require(offDamage==0&&drivenDamage>0,enforce,"dry wear must require actual impeller rotation");
            log($"dry: unpowered_damage={offDamage:F6} driven_damage_added={drivenDamage:F6}");

            var pickup=Ready(0);WetLane(pickup);initial=OperatorMaterialValidation.WaterStock(pickup);
            pickup.Controls.HorizontalAuger=pickup.Controls.VerticalAuger=true;pickup.Controls.Throttle=.3f;
            Tick(pickup,6);float tankBefore=pickup.WashWaterLiters;
            pickup.Controls.WashPump=true;Tick(pickup,10);
            double collected=pickup.TotalRecoveredSurfaceLiters;
            error=OperatorMaterialValidation.WaterStock(pickup)-initial;
            Require(collected>5&&pickup.PumpDamage==0,enforce,"moving wet chamber did not prime without wash supply");
            Require(pickup.TotalWashSuppliedLiters==0&&pickup.TotalFloodUsedLiters==0,enforce,"surface pickup invented supply");
            Require(Math.Abs(pickup.WashWaterLiters-tankBefore-collected)<.005&&Math.Abs(error)<.05,enforce,"surface pickup failed tank/sheet balance");
            Require(collected<=pickup.Config.WashPickupLitersPerSecond*10+.001,enforce,"surface pickup exceeded pump capacity");
            log($"surface_only: recovered_10s_L={collected:F6} tank_L={pickup.WashWaterLiters:F6} wash_supply_L={pickup.TotalWashSuppliedLiters:F6} pump_damage={pickup.PumpDamage:F6} balance_L={error:F6}");

            foreach(string mode in new[]{"stopped","blade_down","frozen","interrupted","raised","reverse"})
            {
                var rejected=Ready(0);WetLane(rejected,mode=="frozen");
                rejected.Controls.HorizontalAuger=true;
                rejected.Controls.Throttle=mode=="stopped"?0:mode=="reverse"?-.3f:.3f;
                rejected.Controls.BladeDepthMm=mode=="blade_down"?.35f:0;
                if(mode=="raised"){rejected.Conditioner.Lift=1;rejected.Controls.ConditionerTarget=1;}
                Tick(rejected,mode=="interrupted"?3:6);
                if(mode=="interrupted")
                {
                    rejected.Controls.Throttle=0;rejected.Controls.Brake=true;Tick(rejected,4);
                    rejected.Controls.Brake=false;rejected.Controls.Throttle=.3f;
                }
                rejected.Controls.WashPump=true;Tick(rejected,.5f);
                Require(rejected.TotalRecoveredSurfaceLiters==0&&rejected.PumpDamage>0,enforce,mode+" incorrectly primed surface pickup");
                log($"prime_rejected {mode}: recovered_L={rejected.TotalRecoveredSurfaceLiters:F6} pump_damage={rejected.PumpDamage:F6}");
            }
            var full=Ready(1);WetLane(full);initial=OperatorMaterialValidation.WaterStock(full);
            full.Controls.HorizontalAuger=true;full.Controls.Throttle=.3f;Tick(full,6);full.Controls.WashPump=true;Tick(full,2);
            error=OperatorMaterialValidation.WaterStock(full)-initial;
            Require(full.TotalRecoveredSurfaceLiters==0&&full.WashWaterLiters==full.Config.WashCapacityLiters&&Math.Abs(error)<.05,enforce,"full tank removed/discarded surface water");
            log($"full_tank: recovered_L={full.TotalRecoveredSurfaceLiters:F6} balance_L={error:F6}");
            log(enforce?"PASS water-drive and standing-water operating sequences":"BASELINE observations; assertions intentionally disabled");
        }
    }
}
