using System;

namespace IceResurfacer.Core
{
    public enum WaterFillPort { None, Flood, Wash }
    public enum WaterHoseState { Stowed, Carried, Flood, Wash }

    public sealed partial class Simulation
    {
        // Fictional bay calibration: an accelerated 12 L/s supply and an 11 m hose.
        // Water accounting uses the same liters as the resurfacing circuits.
        public const float ServiceMaximumFlowLitersPerSecond = 12f;
        public const float ServiceHoseLengthMeters = 11f;
        public const float ServiceReachMeters = 2f;
        public bool OnFoot { get; private set; }
        public WaterHoseState HoseState { get; private set; }
        public float SupplyValve { get; private set; }
        public float ServiceFlowLitersPerSecond { get; private set; }
        public float ServiceOverflowLitersPerSecond { get; private set; }
        public double TotalServiceSuppliedLiters { get; private set; }
        public double TotalServiceStoredLiters { get; private set; }
        public double TotalServiceOverflowLiters { get; private set; }
        public double TotalServiceDrainLiters { get; private set; }
        public bool HoseConnected { get { return HoseState == WaterHoseState.Flood || HoseState == WaterHoseState.Wash; } }
        public bool ServiceDriveInhibited { get { return HoseConnected || OnFoot; } }
        public string ServiceDriveInhibitReason
        {
            get
            {
                if (HoseConnected) return "Water hose connected: close the wall valve, disconnect at the inlet, and return the nozzle before driving.";
                return OnFoot ? "Operator on foot: return to the left boarding step and press F to drive." : "";
            }
        }

        public bool TryDismount()
        {
            if (OnFoot) return true;
            if (!InServiceBay || Math.Abs(Vehicle.Speed) > .035f)
                return ServiceReject("Stop fully inside the service bay before dismounting with F.");
            if (!EquipmentSafe || TankRaised)
                return ServiceReject("Stow the conditioner, retract the blade, stop augers/brush/pump, close both water valves and lower the snow tank before dismounting.");
            OnFoot = true;
            Controls.Throttle = Controls.Steer = 0;
            LastMessage = "Service mode: WASD walk, RMB look, E use hose/valve/inlet, F board at the left step.";
            return true;
        }

        public bool TryBoard(float distanceToStep = 0)
        {
            if (!OnFoot) return true;
            if (!Finite(distanceToStep) || distanceToStep > ServiceReachMeters)
                return ServiceReject("Return to the left boarding step and press F.");
            if (HoseState == WaterHoseState.Carried)
                return ServiceReject("Return the hose nozzle to its wall holder before boarding.");
            OnFoot = false;
            LastMessage = HoseConnected ? ServiceDriveInhibitReason : "Seated. Water hose clear; ready to drive.";
            return true;
        }

        public bool TakeHose(float distanceToHolder = 0)
        {
            if (!OnFoot || !InServiceBay || !WithinReach(distanceToHolder)) return ServiceReject("Walk to the hose holder in service mode.");
            if (HoseState != WaterHoseState.Stowed) return ServiceReject("The hose is already in use; disconnect it at the machine inlet.");
            if (SupplyValve > .001f) return ServiceReject("Close the wall supply valve before taking the nozzle.");
            HoseState = WaterHoseState.Carried;
            LastMessage = "Hose in hand. Walk to the blue ICE WATER or teal WASH WATER inlet and press E.";
            return true;
        }

        public bool ConnectHose(WaterFillPort port, float routeLength, float operatorDistance = 0)
        {
            if (!OnFoot || HoseState != WaterHoseState.Carried) return ServiceReject("Take the hose from its wall holder first.");
            if (port != WaterFillPort.Flood && port != WaterFillPort.Wash) return ServiceReject("This is not a water fill inlet. Use the labeled ICE WATER or WASH WATER coupling.");
            if (!WithinReach(operatorDistance)) return ServiceReject("Walk closer to the selected fill inlet.");
            if (!Finite(routeLength) || routeLength < 0 || routeLength > ServiceHoseLengthMeters)
                return ServiceReject("The 11 m hose cannot reach this inlet. Park nearer the water station.");
            if (!InServiceBay || !EquipmentSafe || Math.Abs(Vehicle.Speed) > .035f || TankRaised)
                return ServiceReject("Connect only to a stopped, safely stowed machine in the service bay.");
            if (SupplyValve > .001f) return ServiceReject("Close the wall supply valve before coupling the hose.");
            HoseState = port == WaterFillPort.Flood ? WaterHoseState.Flood : WaterHoseState.Wash;
            LastMessage = "Hose coupled to " + (port == WaterFillPort.Flood ? "ice-making water" : "wash water") + ". Open the wall supply valve to fill.";
            return true;
        }

        public bool DisconnectHose(float operatorDistance = 0)
        {
            if (!OnFoot || !HoseConnected || !WithinReach(operatorDistance)) return ServiceReject("Walk to the connected machine inlet to uncouple the hose.");
            if (SupplyValve > .001f) return ServiceReject("Close the wall supply valve before disconnecting the pressurized hose.");
            HoseState = WaterHoseState.Carried;
            LastMessage = "Hose disconnected. Connect the other tank or return the nozzle to its wall holder.";
            return true;
        }

        public bool ReturnHose(float distanceToHolder = 0)
        {
            if (!OnFoot || HoseState != WaterHoseState.Carried || !WithinReach(distanceToHolder)) return ServiceReject("Carry the disconnected nozzle to its wall holder.");
            if (SupplyValve > .001f) return ServiceReject("Close the wall supply valve before stowing the hose.");
            HoseState = WaterHoseState.Stowed;
            LastMessage = "Nozzle secured. Return to the left boarding step and press F.";
            return true;
        }

        public bool SetSupplyValve(float opening, float distanceToValve = 0)
        {
            if (!OnFoot || !WithinReach(distanceToValve)) return ServiceReject("Walk to the wall supply valve to operate it.");
            SupplyValve = Clamp(opening, 0, 1);
            LastMessage = SupplyValve <= .001f ? "Wall supply closed. The hose can be safely disconnected." :
                HoseConnected ? "Water flowing. Watch the tank level; close the wall valve when full." : "Supply open with no tank connected: water discharges to the bay drain.";
            return true;
        }

        // Explicit practice fixture. This does not affect ordinary start-full sessions.
        public bool ConfigureWaterForServiceTest(float floodFraction, float washFraction)
        {
            if (!Practice || !InServiceBay || !EquipmentSafe || Math.Abs(Vehicle.Speed) > .035f || HoseConnected || SupplyValve > .001f)
                return ServiceReject("Partial-tank testing requires a stopped practice machine in the bay, equipment stowed and hose disconnected.");
            FloodWaterLiters = Config.FloodCapacityLiters * Clamp(floodFraction, 0, 1);
            WashWaterLiters = Config.WashCapacityLiters * Clamp(washFraction, 0, 1);
            LastMessage = "Practice fixture: tanks set partially full for water-service testing.";
            return true;
        }

        void TickWaterService(float dt)
        {
            ServiceFlowLitersPerSecond = ServiceOverflowLitersPerSecond = 0;
            if (SupplyValve <= .001f) return;
            float supplied = ServiceMaximumFlowLitersPerSecond * SupplyValve * dt;
            TotalServiceSuppliedLiters += supplied;
            ServiceFlowLitersPerSecond = supplied / dt;
            if (!HoseConnected)
            {
                TotalServiceDrainLiters += supplied;
                return;
            }
            bool flood = HoseState == WaterHoseState.Flood;
            float before = flood ? FloodWaterLiters : WashWaterLiters;
            float capacity = flood ? Config.FloodCapacityLiters : Config.WashCapacityLiters;
            float after = Math.Min(capacity, before + supplied);
            float stored = Math.Max(0, after - before);
            float overflow = Math.Max(0, supplied - stored);
            if (flood) FloodWaterLiters = after; else WashWaterLiters = after;
            TotalServiceStoredLiters += stored;
            TotalServiceOverflowLiters += overflow;
            TotalServiceDrainLiters += overflow;
            ServiceOverflowLitersPerSecond = overflow / dt;
        }

        bool ServiceReject(string message) { LastMessage = message; return false; }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static bool WithinReach(float distance) { return Finite(distance) && distance >= 0 && distance <= ServiceReachMeters; }
    }
}
