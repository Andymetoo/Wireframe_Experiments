using System;
using IceResurfacer.Core;

internal static class ServiceValidation
{
    public static void Run()
    {
        var normal = new Simulation();
        Near(normal.FloodWaterLiters, 738, .001f, "normal flood start full");
        Near(normal.WashWaterLiters, 310, .001f, "normal wash start full");
        Check(!normal.ConfigureWaterForServiceTest(.4f, .4f), "scored sessions cannot use partial-water fixture");
        var s = new Simulation(practice:true);
        Check(s.ConfigureWaterForServiceTest(.5f, .25f), "practice partial fixture");
        s.Vehicle.Speed = .2f;
        Check(!s.TryDismount(), "moving dismount rejected");
        s.Vehicle.Speed = 0; s.Controls.HorizontalAuger = true;
        Check(!s.TryDismount(), "operating machinery dismount rejected");
        s.Controls.HorizontalAuger = false;
        Check(s.TryDismount(), "safe dismount");
        s.Controls.Throttle = 1; Tick(s, 2);
        Near(s.Vehicle.Speed, 0, .001f, "on-foot travel interlock");
        Check(s.TakeHose(), "take nozzle");
        Check(!s.ConnectHose(WaterFillPort.None, 5), "wrong inlet rejected");
        Check(!s.ConnectHose(WaterFillPort.Flood, 12), "hose length enforced");
        Check(!s.ConnectHose(WaterFillPort.Flood, 5, 3), "operator reach enforced");
        Check(!s.ConnectHose(WaterFillPort.Flood, float.NaN), "nonfinite hose length rejected");
        float flood = s.FloodWaterLiters, wash = s.WashWaterLiters;
        Check(s.SetSupplyValve(.5f), "wall supply half open");
        Tick(s, 2);
        Near(s.FloodWaterLiters, flood, .001f, "disconnected flood unchanged");
        Near(s.WashWaterLiters, wash, .001f, "disconnected wash unchanged");
        Near((float)s.TotalServiceDrainLiters, 12, .02f, "disconnected supply drains");
        Check(!s.ConnectHose(WaterFillPort.Flood, 5), "pressurized coupling prevented");
        Check(s.SetSupplyValve(0), "close wall valve");
        Check(s.ConnectHose(WaterFillPort.Flood, 5), "couple flood");
        Check(s.TryBoard(), "may board with connected hose for departure warning");
        Check(!s.TryFinish() && s.LastMessage.Contains("disconnect"), "finish blocked while hose remains connected with recovery guidance");
        s.Controls.Throttle = 1; Tick(s, 2);
        Near(s.Vehicle.Speed, 0, .001f, "connected hose prevents driving");
        Check(s.ServiceDriveInhibitReason.Contains("disconnect"), "interlock explains recovery");
        Check(s.TryDismount(), "return to service");
        Check(s.SetSupplyValve(.5f), "half flow"); Tick(s, 5);
        Near(s.FloodWaterLiters, flood + 30, .03f, "progressive calibrated flood fill");
        Near(s.WashWaterLiters, wash, .001f, "independent wash tank");
        Check(!s.DisconnectHose(), "pressurized disconnection prevented");
        Check(s.SetSupplyValve(1), "full flow"); Tick(s, 40);
        Near(s.FloodWaterLiters, 738, .001f, "flood capacity clamped");
        Check(s.ServiceOverflowLitersPerSecond > 11.9f, "full flood overflows");
        Check(s.SetSupplyValve(0) && s.DisconnectHose(), "flood disconnect recovery");
        Check(s.ConnectHose(WaterFillPort.Wash, 5), "connect wash after flood");
        Check(s.SetSupplyValve(1), "wash supply open"); Tick(s, 25);
        Near(s.WashWaterLiters, 310, .001f, "wash capacity clamped");
        Check(s.ServiceOverflowLitersPerSecond > 11.9f, "full wash overflows");
        double balance = s.TotalServiceStoredLiters + s.TotalServiceDrainLiters;
        Check(Math.Abs(s.TotalServiceSuppliedLiters - balance) < .08, "supplied liters = tank storage + drain liters");
        Near((float)s.TotalServiceStoredLiters, (738-flood)+(310-wash), .08f, "both actual tank gains accounted");
        Check(s.SetSupplyValve(0) && s.DisconnectHose() && s.ReturnHose() && s.TryBoard(), "full hose recovery cycle");
        Check(!s.ServiceDriveInhibited, "service inhibit clears");
        s.Controls.Throttle = 1; Tick(s, 1);
        Check(s.Vehicle.Speed > .1f, "drive after return and board");
        Console.WriteLine("PASS water service: separate capacities, hose lifecycle, safety, reach, partial flow, overflow and material balance");
    }
    static void Tick(Simulation sim, float seconds) { for(int i=0;i<(int)Math.Round(seconds/.02f);i++) sim.Step(.02f); }
    static void Check(bool success,string text) { if(!success) throw new Exception("water service: " + text); }
    static void Near(float a,float b,float tolerance,string text) { Check(Math.Abs(a-b)<=tolerance,text+" ("+a+" vs "+b+")"); }
}
