using System;

namespace IceResurfacer.Core
{
    public enum WaterFillPort { None, Flood, Wash }
    public enum WaterHoseState { Stowed, Carried, Flood, Wash, Dropped }

    public sealed partial class Simulation
    {
        // Fictional bay calibration: an accelerated 12 L/s supply and an 11 m hose.
        // Water accounting uses the same liters as the resurfacing circuits.
        public const float ServiceMaximumFlowLitersPerSecond = 12f;
        public const float ServiceHoseLengthMeters = 11f;
        public const float ServiceReachMeters = 2f;
        public const float ServiceHolderReachMeters = 2.65f;
        // Separate rear-quarter filler necks, fixed to the water-tank structure,
        // never the lifting snow bin. Coordinates are shared with the scene.
        public const float FillPortLocalX = 1.02f;
        public const float FloodFillLocalZ = .05f;
        public const float WashFillLocalZ = -.55f;
        public const float FloodFillHeight = 1.22f;
        public const float WashFillHeight = 1.48f;
        public const float ServiceAnchorSide = ServiceLayout.SupplySide;
        public const float ServiceAnchorOutward = ServiceLayout.SupplyOutward;
        public float HoseLooseX { get; private set; }
        public float HoseLooseZ { get; private set; }
        public int HoseBreakaways { get; private set; }
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
        public bool ServiceDriveInhibited { get { return OnFoot || (GuidedTraining && HoseConnected); } }
        public string ServiceDriveInhibitReason
        {
            get
            {
                if (GuidedTraining && HoseConnected) return "Training assistance: water hose connected. Close the wall valve and disconnect before driving.";
                return OnFoot ? "Operator on foot: return to the left step or rear platform and press F to drive." : "";
            }
        }

        public bool TryDismount()
        {
            if (OnFoot) return true;
            if (GuidedTraining && (!InServiceBay || Math.Abs(Vehicle.Speed) > .035f))
                return ServiceReject("Training assistance: stop inside the service bay before dismounting. Free Simulation permits exits anywhere.");
            if (GuidedTraining && (!EquipmentSafe || TankRaised))
                return ServiceReject("Training assistance: stow equipment before dismounting. Free Simulation leaves your machinery settings unchanged.");
            OnFoot = true;
            Controls.Throttle = Controls.Steer = 0;
            LastMessage = Math.Abs(Vehicle.Speed) > .15f ? "Moving dismount: the unoccupied machine is coasting. Stay clear; F boards when near the step." :
                "On foot: mouse look, WASD walk, E interact, F board at the left step. Machinery remains as you left it.";
            if(Math.Abs(Vehicle.Speed)>.5f)Fault(19,LastMessage);
            return true;
        }

        public bool TryBoard(float distanceToStep = 0)
        {
            if (!OnFoot) return true;
            if (!Finite(distanceToStep) || distanceToStep > ServiceReachMeters)
                return ServiceReject("Return to the left step or rear operator platform and press F.");
            if (HoseState == WaterHoseState.Carried && GuidedTraining)
                return ServiceReject("Training assistance: return the hose nozzle to its wall holder before boarding.");
            if (HoseState == WaterHoseState.Carried)
            {
                // Boarding frees the operator's hands. The hose is left on the
                // floor, not deleted or silently returned to its remote holder.
                HoseState = WaterHoseState.Dropped;
            }
            OnFoot = false;
            LastMessage = HoseConnected ? (GuidedTraining ? ServiceDriveInhibitReason : "Hose still attached. Driving beyond its reach releases the breakaway coupling and spills water.") :
                HoseState == WaterHoseState.Dropped ? "Seated. Nozzle left on the floor; retrieve it with E when back on foot." : "Seated. Ready to drive.";
            return true;
        }

        public bool TakeHose(float distanceToHolder = 0)
        {
            if (!OnFoot || !HolderReach(distanceToHolder)) return ServiceReject("Walk to the hose holder and press E.");
            if (HoseState != WaterHoseState.Stowed) return ServiceReject("The hose is already in use; disconnect it at the machine inlet.");
            if (GuidedTraining && SupplyValve > .001f) return ServiceReject("Training assistance: close the wall supply valve before taking the nozzle.");
            HoseState = WaterHoseState.Carried;
            Layout.HoseHolder(out float x,out float z);
            HoseLooseX=x;HoseLooseZ=z;
            LastMessage = "Hose in hand. Walk to the blue FLOOD WATER or teal WASH WATER inlet and press E.";
            return true;
        }

        public bool ConnectHose(WaterFillPort port, float routeLength, float operatorDistance = 0)
        {
            if (!OnFoot || HoseState != WaterHoseState.Carried) return ServiceReject("Take the hose from its wall holder first.");
            if (port != WaterFillPort.Flood && port != WaterFillPort.Wash) return ServiceReject("This is not a water fill inlet. Use the labeled FLOOD WATER or WASH WATER coupling.");
            if (!WithinReach(operatorDistance)) return ServiceReject("Walk closer to the selected fill inlet.");
            if (!Finite(routeLength) || routeLength < 0)
                return ServiceReject("No clear hose route to this inlet. Park with the right-side fillers beside the wall reel.");
            if (routeLength > ServiceHoseLengthMeters)
                return ServiceReject("Inlet route is "+routeLength.ToString("0.0")+" m; the hose reaches 11 m. Park nearer the wall reel.");
            if (GuidedTraining && (!InServiceBay || !EquipmentSafe || Math.Abs(Vehicle.Speed) > .035f || TankRaised))
                return ServiceReject("Training assistance: connect to a stopped, stowed machine in the service bay.");
            if (GuidedTraining && SupplyValve > .001f) return ServiceReject("Training assistance: close the wall supply valve before coupling the hose.");
            HoseState = port == WaterFillPort.Flood ? WaterHoseState.Flood : WaterHoseState.Wash;
            LastMessage = "Hose coupled to " + (port == WaterFillPort.Flood ? "ice-making water" : "wash water") + ". Open the wall supply valve to fill.";
            return true;
        }

        public bool DisconnectHose(float operatorDistance = 0)
        {
            if (!OnFoot || !HoseConnected || !WithinReach(operatorDistance)) return ServiceReject("Walk to the connected machine inlet to uncouple the hose.");
            if (GuidedTraining && SupplyValve > .001f) return ServiceReject("Training assistance: close the wall supply valve before disconnecting the pressurized hose.");
            HoseState = WaterHoseState.Carried;
            LastMessage = SupplyValve > .001f ? "Pressurized hose uncoupled: water is spilling from the nozzle. Close the wall supply to stop it." : "Hose disconnected. Connect the other tank or return the nozzle to its wall holder.";
            return true;
        }

        public bool ReturnHose(float distanceToHolder = 0)
        {
            if (!OnFoot || HoseState != WaterHoseState.Carried || !HolderReach(distanceToHolder)) return ServiceReject("Carry the disconnected nozzle near the highlighted wall holder and press E.");
            if (GuidedTraining && SupplyValve > .001f) return ServiceReject("Training assistance: close the wall supply valve before stowing the hose.");
            HoseState = WaterHoseState.Stowed;
            LastMessage = SupplyValve > .001f ? "Nozzle secured, but water is still running to the floor. Close the wall supply valve." : "Nozzle secured. Return to the left boarding step and press F.";
            return true;
        }

        public bool PickUpDroppedHose(float distanceToNozzle = 0)
        {
            if (!OnFoot || HoseState != WaterHoseState.Dropped || !WithinReach(distanceToNozzle))
                return ServiceReject("Walk to the loose nozzle on the floor and press E.");
            HoseState = WaterHoseState.Carried;
            LastMessage = SupplyValve > .001f ? "Nozzle recovered. Water is flowing; close the wall supply or connect a tank." : "Nozzle recovered. Connect a tank or return it to the highlighted holder.";
            return true;
        }

        public void SetCarriedHosePosition(float x, float z)
        {
            if (HoseState == WaterHoseState.Carried && Finite(x) && Finite(z)) { HoseLooseX = x; HoseLooseZ = z; }
        }

        public bool CheckHoseReach(float routedLength)
        {
            if (!HoseConnected || GuidedTraining || float.IsNaN(routedLength) || routedLength <= ServiceHoseLengthMeters) return false;
            Layout.SupplyAnchor(out float ax, out float az);
            VehiclePoint(FillPortLocalX + .14f, HoseState == WaterHoseState.Wash ? WashFillLocalZ : FloodFillLocalZ, out float x, out float z);
            float dx=x-ax,dz=z-az,d=(float)Math.Sqrt(dx*dx+dz*dz),scale=Math.Min(1,(ServiceHoseLengthMeters-1.4f)/Math.Max(.001f,d));
            HoseLooseX=ax+dx*scale;HoseLooseZ=az+dz*scale;
            HoseState=WaterHoseState.Dropped;HoseBreakaways++;
            LastMessage="Hose pulled taut: breakaway coupling released. The machine can move; close the wall supply and retrieve the loose nozzle with E.";
            Fault(18,LastMessage);
            return true;
        }

        void VehiclePoint(float localX,float localZ,out float x,out float z)
        {
            float sn=(float)Math.Sin(Vehicle.Heading),cs=(float)Math.Cos(Vehicle.Heading);
            x=Vehicle.X+localX*cs+localZ*sn;z=Vehicle.Z-localX*sn+localZ*cs;
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
            Environment.TickWater(dt,Ice,Layout);
            ServiceFlowLitersPerSecond = ServiceOverflowLitersPerSecond = 0;
            if (HoseConnected && !GuidedTraining)
            {
                Layout.SupplyAnchor(out float ax, out float az);
                VehiclePoint(FillPortLocalX + .14f, HoseState == WaterHoseState.Wash ? WashFillLocalZ : FloodFillLocalZ, out float x, out float z);
                float dx=x-ax,dz=z-az;
                CheckHoseReach((float)Math.Sqrt(dx*dx+dz*dz)+.9f);
            }
            if (SupplyValve <= .001f) return;
            float supplied = ServiceMaximumFlowLitersPerSecond * SupplyValve * dt;
            TotalServiceSuppliedLiters += supplied;
            ServiceFlowLitersPerSecond = supplied / dt;
            if (!HoseConnected)
            {
                TotalServiceDrainLiters += supplied;
                float spillX=HoseLooseX,spillZ=HoseLooseZ;
                if(HoseState==WaterHoseState.Stowed) Layout.HoseHolder(out spillX,out spillZ);
                DepositRunoff(spillX,spillZ,supplied);
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
            if(overflow>0)
            {
                VehiclePoint(FillPortLocalX+.11f,(flood?FloodFillLocalZ:WashFillLocalZ)+.13f,out float sx,out float sz);
                DepositRunoff(sx,sz,overflow);
            }
        }

        bool ServiceReject(string message) { LastMessage = message; return false; }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static bool WithinReach(float distance) { return Finite(distance) && distance >= 0 && distance <= ServiceReachMeters; }
        static bool HolderReach(float distance) { return Finite(distance) && distance >= 0 && distance <= ServiceHolderReachMeters; }
    }
}
