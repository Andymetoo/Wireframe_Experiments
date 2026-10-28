using System;
using IceResurfacer.Core;

internal static class ServiceFreedomValidation
{
    public static void Run()
    {
        var free=new Simulation(practice:true);
        free.Vehicle.X=free.Vehicle.Z=0;free.Vehicle.Heading=0;
        free.Conditioner.X=0;free.Conditioner.Z=-1.1f;
        free.Controls.ConditionerTarget=0;free.Controls.BladeDepthMm=.25f;
        free.Controls.HorizontalAuger=free.Controls.VerticalAuger=free.Controls.WashPump=true;
        free.Controls.FloodFlow=.2f;free.Controls.WashValve=.3f;free.Controls.TankLiftTarget=.5f;
        Tick(free,3);free.Vehicle.Speed=.7f;
        float tank=free.TankLift,lift=free.Conditioner.Lift;
        Check(free.TryDismount(),"Free Simulation allows moving, on-ice dismount with raised tank and active machinery");
        Check(free.TankLift==tank&&free.Conditioner.Lift==lift&&free.Controls.BladeDepthMm==.25f&&
            free.Controls.HorizontalAuger&&free.Controls.VerticalAuger&&free.Controls.WashPump&&free.Controls.FloodFlow==.2f&&free.Controls.WashValve==.3f,
            "dismount preserves every machinery command and actual hydraulic position");
        free.Step(.02f);Check(free.Vehicle.Speed>.65f,"moving exit releases pedal into hydrostatic coasting rather than instant stop");
        Check(free.TryBoard(),"unusual machinery configuration remains boardable");

        var s=new Simulation(practice:true);
        Check(s.ConfigureWaterForServiceTest(.5f,.5f),"start-full service fixture explicitly reduces practice tanks");
        Check(s.TryDismount()&&s.TakeHose(2.5f),"wall holder reach accepts practical 2.5 m approach");
        Check(s.ConnectHose(WaterFillPort.Flood,4.6f)&&s.SetSupplyValve(.5f),"flood coupling and half supply open");
        float before=s.FloodWaterLiters,other=s.WashWaterLiters;Tick(s,2);
        Near(s.FloodWaterLiters-before,12,.02f,"actual gradual fill at half of 12 L/s");
        Near(s.WashWaterLiters,other,.001f,"independent wash reservoir unchanged");
        Check(s.DisconnectHose(),"free pressurized uncoupling remains possible");
        Tick(s,1);Check(s.TotalServiceDrainLiters>5.9,"pressurized disconnected hose spills instead of vanishing water");
        Check(s.ReturnHose(2.5f)&&s.HoseState==WaterHoseState.Stowed,"forgiving return does not trap a pressurized nozzle in carried state");
        Check(s.SupplyValve==.5f,"return does not silently close operator's wall valve");
        Check(s.SetSupplyValve(0)&&s.TakeHose()&&s.TryBoard(),"boarding with nozzle in Free Simulation is recoverable");
        Check(s.HoseState==WaterHoseState.Dropped,"boarding leaves the nozzle on the floor instead of teleporting it to holder");
        Check(s.TryDismount()&&s.PickUpDroppedHose()&&s.ReturnHose(),"loose nozzle can be recovered and returned without reset");

        var pull=new Simulation(practice:true);
        Check(pull.ConfigureWaterForServiceTest(.9f,.5f)&&pull.TryDismount()&&pull.TakeHose()&&pull.ConnectHose(WaterFillPort.Wash,4.6f)&&pull.SetSupplyValve(1)&&pull.TryBoard(),"prepare connected-hose departure fixture");
        Check(!pull.ServiceDriveInhibited,"attached hose does not inhibit travel in Free Simulation");
        pull.Controls.Throttle=1;Tick(pull,1);
        Check(pull.Vehicle.Speed>.1f,"vehicle actually moves with hose attached");
        Tick(pull,11);
        Check(pull.HoseBreakaways==1&&pull.HoseState==WaterHoseState.Dropped,"physical reach releases breakaway coupling once during driven departure");
        Check(pull.TotalServiceDrainLiters>0&&pull.SupplyValve==1,"wall supply continues spilling after breakaway until operator closes it");
        Check(pull.EffectiveDriveDemand>.9f&&!pull.ServiceDriveInhibited,"broken coupling never leaves stale drive inhibition");
        Near((float)(pull.TotalServiceSuppliedLiters-pull.TotalServiceStoredLiters-pull.TotalServiceDrainLiters),0,.08f,"supply conserved across fill, overflow and loose-hose flow");

        var training=new Simulation(practice:true,mode:SimulationMode.GuidedTraining);
        training.Controls.HorizontalAuger=true;
        Check(!training.TryDismount()&&training.LastMessage.Contains("Training assistance"),"training restrictions are explicit and do not alter Free Simulation");
        training.Controls.HorizontalAuger=false;
        Check(training.TryDismount()&&training.TakeHose()&&training.ConnectHose(WaterFillPort.Wash,4.6f)&&training.TryBoard(),"training service fixture");
        Check(training.ServiceDriveInhibited&&training.ServiceDriveInhibitReason.Contains("Training assistance"),"guided hose departure assistance is labeled");
        Console.WriteLine("PASS service freedom: moving unsafe exits, persistent machinery, free hose departure, physical breakaway, forgiving return, dropped-nozzle recovery, volume balance, and explicit training-only gates");
    }
    static void Tick(Simulation s,float seconds){for(int i=0;i<(int)Math.Round(seconds/.02f);i++)s.Step(.02f);}
    static void Check(bool value,string text){if(!value)throw new Exception("Service freedom: "+text);}
    static void Near(float a,float b,float tolerance,string text){Check(Math.Abs(a-b)<=tolerance,text+" ("+a+" vs "+b+")");}
}
