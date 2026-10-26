using System;

namespace IceResurfacer.Core
{
    /// <summary>Deterministic kinematic machine and material balance. No frame, audio, input, or Unity dependencies.</summary>
    public sealed class Simulation
    {
        public readonly SimulatorConfig Config;
        public readonly OperatorControls Controls = new OperatorControls();
        public readonly VehicleState Vehicle = new VehicleState();
        public readonly ConditionerState Conditioner = new ConditionerState();
        public readonly IceField Ice;
        public readonly bool Practice;
        public float Time { get; private set; }
        public float FloodWaterLiters { get; private set; }
        public float WashWaterLiters { get; private set; }
        public float SnowTankM3 { get; private set; }
        public float TankLift { get; private set; }
        public float HorizontalSnowM3 { get; private set; }
        public float VerticalSnowM3 { get; private set; }
        public float MachineLoad { get; private set; }
        public float EscapingSnowRate { get; private set; }
        public float WashPrimeSeconds { get; private set; }
        public float PumpDamage { get; private set; }
        public int Mistakes { get; private set; }
        public int TankDumps { get; private set; }
        public int SnowBreakerStrokes { get; private set; }
        public float DumpedSnowM3 { get; private set; }
        public float TotalSnowCollectedM3 { get; private set; }
        public float TotalFloodUsedLiters { get; private set; }
        public float TotalWashUsedLiters { get; private set; }
        public bool HasEnteredRink { get; private set; }
        public bool Finished { get; private set; }
        public string LastMessage { get; private set; } = "Drive through the gate. Lower with the blade out, then engage both augers.";
        public bool InServiceBay { get { return Vehicle.Z < -Config.RinkLength * .5f - 2.6f && Math.Abs(Vehicle.X) < Config.GateHalfWidth - Config.VehicleHalfWidth; } }
        public bool InDumpZone { get { return InServiceBay && Vehicle.Z <= -Config.RinkLength * .5f - Config.BayLength + 4.3f && Math.Abs(Vehicle.X) < 1.5f && Math.Cos(Vehicle.Heading) < -.85f; } }
        public bool EquipmentSafe { get { return Conditioner.Lift >= .97f && Controls.BladeDepthMm <= .001f && !Controls.HorizontalAuger && !Controls.VerticalAuger && !Controls.BoardBrush && !Controls.WashPump && Controls.WashValve <= .001f && Controls.FloodFlow <= .001f; } }
        public bool ProperExit { get { return HasEnteredRink && !exitUnsafe && InServiceBay && ConditionerFullyOutside() && EquipmentSafe; } }

        readonly float[] faultTimes = new float[16];
        readonly int[] footprint;
        int footprintCount;
        float remainder;
        bool exitUnsafe, dumping;
        float lastConditionerX, lastConditionerZ;
        const float FixedStep = .02f;

        public Simulation(SimulatorConfig config = null, bool practice = false, int seed = 546)
        {
            Config = config ?? new SimulatorConfig();
            Config.Validate();
            Practice = practice;
            Ice = new IceField(Config, seed);
            Vehicle.Z = -Config.RinkLength * .5f - 4f;
            Conditioner.Z = Vehicle.Z - Config.HitchBehindAxle - Config.DrawbarLength;
            lastConditionerX = Conditioner.X; lastConditionerZ = Conditioner.Z;
            FloodWaterLiters = Config.FloodCapacityLiters;
            WashWaterLiters = Config.WashCapacityLiters;
            footprint = new int[(int)Math.Ceiling((Config.ConditionerWidth + Config.ConditionerLength + .5f) / Config.CellSize) * (int)Math.Ceiling((Config.ConditionerWidth + Config.ConditionerLength + .5f) / Config.CellSize)];
            for (int i = 0; i < faultTimes.Length; i++) faultTimes[i] = -1000;
        }

        public void Step(float dt)
        {
            if (Finished || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            // Fixed simulation cadence keeps render framerate out of the physics. Long pauses never teleport the machine.
            remainder += Math.Min(dt, .5f);
            while (remainder + .0000001f >= FixedStep)
            {
                Tick(FixedStep);
                remainder -= FixedStep;
            }
        }

        void Tick(float dt)
        {
            Time += dt;
            ClampControls();
            float oldLift = Conditioner.Lift;
            Conditioner.Lift = MoveTowards(Conditioner.Lift, Controls.ConditionerTarget, dt / Config.ConditionerLiftSeconds);
            if (Conditioner.Lift > oldLift && Controls.VerticalAuger)
                Fault(0, "Stop the vertical auger before raising the conditioner.");
            if (oldLift > .15f && Conditioner.Lift <= .15f && Controls.BladeDepthMm > .02f)
                Fault(1, "Lower with the blade retracted; adjust cutting depth after the conditioner is down.");
            UpdateVehicle(dt);
            UpdateConditioner(dt);
            CheckExit();
            FindFootprint();
            ProcessIce(dt);
            ConveySnow(dt);
            UpdateTank(dt);
            MachineLoad = Clamp(.13f + HorizontalSnowM3 / Config.ConditionerSnowCapacityM3 * .75f + VerticalSnowM3 / Config.VerticalSnowCapacityM3 * .55f +
                (Conditioner.Lift < .15f ? Controls.BladeDepthMm * Math.Abs(Vehicle.Speed) * .14f : 0) + (Controls.HorizontalAuger ? .08f : 0) + (Controls.VerticalAuger ? .09f : 0), 0, 1.8f);
            if (MachineLoad > 1.0f) Fault(2, "Conveyor overload: reduce speed and blade depth; clear both augers.");
        }

        void ClampControls()
        {
            Controls.Throttle = Clamp(Controls.Throttle, -1, 1); Controls.Steer = Clamp(Controls.Steer, -1, 1);
            Controls.ConditionerTarget = Clamp(Controls.ConditionerTarget, 0, 1); Controls.TankLiftTarget = Clamp(Controls.TankLiftTarget, 0, 1);
            Controls.BladeDepthMm = Clamp(Controls.BladeDepthMm, 0, Config.MaxBladeDepthMm);
            Controls.WashValve = Clamp(Controls.WashValve, 0, 1); Controls.FloodFlow = Clamp(Controls.FloodFlow, 0, 1);
        }

        void UpdateVehicle(float dt)
        {
            Vehicle.SteeringAngle = MoveTowards(Vehicle.SteeringAngle, Controls.Steer * Config.MaxSteeringRadians, Config.SteeringRate * dt);
            float command = Controls.Throttle;
            if (TankLift > .02f || Controls.TankLiftTarget > .02f)
            {
                if (Math.Abs(command) > .1f) Fault(3, "Lower the snow tank before moving.");
                command = 0;
            }
            float target = command * (command >= 0 ? Config.ForwardSpeed : Config.ReverseSpeed);
            float speed = Vehicle.Speed;
            float massFactor = 1f / (1f + SnowTankM3 * .09f);
            float rate;
            if (Controls.Brake || TankLift > .02f) { target = 0; rate = Config.ServiceBrakeDeceleration; }
            else if (Math.Abs(command) < .01f) { target = 0; rate = Config.HydrostaticDeceleration + Math.Abs(speed) * .12f; }
            else if (speed * command < -.005f) { target = 0; rate = Config.ServiceBrakeDeceleration; }
            else rate = Config.Acceleration * massFactor * Math.Max(.20f, 1f - MachineLoad * .35f);
            float nextSpeed = MoveTowards(speed, target, rate * dt);
            float travel = (speed + nextSpeed) * .5f * dt;
            float turn = travel * (float)Math.Tan(Vehicle.SteeringAngle) / Config.Wheelbase;
            float direction = Vehicle.Heading + turn * .5f;
            float x = Vehicle.X + (float)Math.Sin(direction) * travel, z = Vehicle.Z + (float)Math.Cos(direction) * travel;
            float heading = Wrap(Vehicle.Heading + turn);
            if (BodyFits(x, z, heading))
            {
                Vehicle.X = x; Vehicle.Z = z; Vehicle.Heading = heading; Vehicle.Speed = nextSpeed;
            }
            else
            {
                if (Math.Abs(speed) > .15f) Fault(4, "Board or service wall contact. Brake and steer clear.");
                Vehicle.Speed = 0;
            }
            if (Ice.IsInside(Vehicle.X, Vehicle.Z, 2f)) HasEnteredRink = true;
            if (Vehicle.Speed < -.12f && Conditioner.Lift < .8f) Fault(5, "Raise the conditioner before reversing; the lowered blade scores the surface.");
        }

        bool BodyFits(float x, float z, float heading)
        {
            float sn = (float)Math.Sin(heading), cs = (float)Math.Cos(heading);
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                float lx = (col - 1) * Config.VehicleHalfWidth;
                float lz = -Config.BodyRear + (Config.BodyFront + Config.BodyRear) * row * .5f;
                if (!Navigable(x + cs * lx + sn * lz, z - sn * lx + cs * lz)) return false;
            }
            return true;
        }

        public bool Navigable(float x, float z)
        {
            return Ice.IsInside(x, z, .02f) || (Math.Abs(x) <= Config.GateHalfWidth - .02f && z >= -Config.RinkLength * .5f - Config.BayLength + .05f && z <= -Config.RinkLength * .5f + .1f);
        }

        void UpdateConditioner(float dt)
        {
            lastConditionerX = Conditioner.X; lastConditionerZ = Conditioner.Z;
            float sn = (float)Math.Sin(Vehicle.Heading), cs = (float)Math.Cos(Vehicle.Heading);
            float hitchX = Vehicle.X - sn * Config.HitchBehindAxle, hitchZ = Vehicle.Z - cs * Config.HitchBehindAxle;
            float desired = (float)Math.Atan2(hitchX - Conditioner.X, hitchZ - Conditioner.Z);
            // A distance-constrained drawbar naturally tracks the inside of a turn.
            float delta = Wrap(desired - Conditioner.Heading);
            if (Math.Abs(Vehicle.Speed) > .001f) Conditioner.Heading = Wrap(Conditioner.Heading + Clamp(delta, -1.7f * dt, 1.7f * dt));
            // Avoid a trailer flipping through the machine if backed into a tight turn.
            Conditioner.Heading = Wrap(Vehicle.Heading + Clamp(Wrap(Conditioner.Heading - Vehicle.Heading), -.9f, .9f));
            Conditioner.X = hitchX - (float)Math.Sin(Conditioner.Heading) * Config.DrawbarLength;
            Conditioner.Z = hitchZ - (float)Math.Cos(Conditioner.Heading) * Config.DrawbarLength;
        }

        void CheckExit()
        {
            if (!HasEnteredRink) return;
            float halfZ = Config.RinkLength * .5f;
            if (Conditioner.Z > -halfZ + Config.ConditionerWidth && Vehicle.Z > -halfZ + Config.ConditionerWidth) exitUnsafe = false;
            float envelope = Math.Abs((float)Math.Sin(Conditioner.Heading)) * Config.ConditionerWidth * .5f + Math.Abs((float)Math.Cos(Conditioner.Heading)) * Config.ConditionerLength * .5f;
            if (Conditioner.Z - envelope < -halfZ && Conditioner.Z + envelope > -halfZ - .2f && !EquipmentSafe)
            {
                exitUnsafe = true;
                Fault(6, "Threshold: retract blade, stop augers and water, stow brush, and fully lift conditioner before leaving.");
            }
        }

        bool ConditionerFullyOutside()
        {
            float envelope = Math.Abs((float)Math.Sin(Conditioner.Heading)) * Config.ConditionerWidth * .5f + Math.Abs((float)Math.Cos(Conditioner.Heading)) * Config.ConditionerLength * .5f;
            return Conditioner.Z + envelope < -Config.RinkLength * .5f - .1f;
        }

        void FindFootprint()
        {
            footprintCount = 0;
            float radius = (Config.ConditionerWidth + Config.ConditionerLength) * .5f;
            int x0 = Math.Max(0, (int)Math.Floor((Conditioner.X - radius - Ice.MinX) / Ice.CellSize));
            int x1 = Math.Min(Ice.Columns - 1, (int)Math.Floor((Conditioner.X + radius - Ice.MinX) / Ice.CellSize));
            int z0 = Math.Max(0, (int)Math.Floor((Conditioner.Z - radius - Ice.MinZ) / Ice.CellSize));
            int z1 = Math.Min(Ice.Rows - 1, (int)Math.Floor((Conditioner.Z + radius - Ice.MinZ) / Ice.CellSize));
            float sn = (float)Math.Sin(Conditioner.Heading), cs = (float)Math.Cos(Conditioner.Heading);
            for (int row = z0; row <= z1; row++)
            for (int col = x0; col <= x1; col++)
            {
                float dx = Ice.CellX(col) - Conditioner.X, dz = Ice.CellZ(row) - Conditioner.Z;
                float side = dx * cs - dz * sn, fore = dx * sn + dz * cs;
                int i = Ice.Index(col, row);
                if (Ice.Cells[i].IsIce && Math.Abs(side) <= Config.ConditionerWidth * .5f && Math.Abs(fore) <= Config.ConditionerLength * .5f)
                    footprint[footprintCount++] = i;
            }
        }

        void ProcessIce(float dt)
        {
            float pressure = Clamp(1f - Conditioner.Lift / .25f, 0, 1);
            float distance = (float)Math.Sqrt((Conditioner.X - lastConditionerX) * (Conditioner.X - lastConditionerX) + (Conditioner.Z - lastConditionerZ) * (Conditioner.Z - lastConditionerZ));
            float advance = Math.Min(1f, distance / Config.ConditionerLength);
            float flood = Math.Min(FloodWaterLiters, Controls.FloodFlow * Config.FloodMaxLitersPerSecond * dt);
            float wash = Math.Min(WashWaterLiters, Controls.WashValve * Config.WashMaxLitersPerSecond * dt);
            FloodWaterLiters -= flood; TotalFloodUsedLiters += flood;
            WashWaterLiters -= wash;
            if (wash > .00001f && pressure > .8f && footprintCount > 0) WashPrimeSeconds = Math.Min(30, WashPrimeSeconds + dt);
            else if (Controls.WashValve <= .001f) WashPrimeSeconds = Math.Max(0, WashPrimeSeconds - dt * 2f);
            bool pumpReady = WashPrimeSeconds >= 10f && pressure > .8f && footprintCount > 0;
            if (Controls.WashPump && !pumpReady)
            {
                PumpDamage = Math.Min(1, PumpDamage + dt * .007f);
                Fault(7, "Wash pickup pump is dry. Prime with the wash valve for 10 seconds before starting it.");
            }
            float recovery = Controls.WashPump && pumpReady ? wash * Config.WashRecoveryFraction * (1f - PumpDamage * .75f) : 0;
            WashWaterLiters = Math.Min(Config.WashCapacityLiters, WashWaterLiters + recovery);
            TotalWashUsedLiters += wash - recovery;
            if (flood > 0 && Math.Abs(Vehicle.Speed) < .12f) Fault(8, "Flood water is pooling while stopped. Close the flow lever.");
            if (Controls.WashValve > .01f && WashWaterLiters <= .001f) Fault(9, "Wash-water tank empty: shut the valve and service in the bay.");
            if (Controls.FloodFlow > .01f && FloodWaterLiters <= .001f) Fault(10, "Ice-making water tank empty: service in the bay.");
            if (footprintCount == 0) return;
            float area = Ice.CellSize * Ice.CellSize;
            float floodDepth = (flood + wash - recovery) / (footprintCount * area);
            float washPerCell = wash / footprintCount;
            float blade = Controls.BladeDepthMm * pressure * advance;
            float newSnow = 0;
            for (int n = 0; n < footprintCount; n++)
            {
                int index = footprint[n]; var cell = Ice.Cells[index];
                float snowRemoved = Math.Min(cell.SnowMm, pressure * advance * .6f);
                if (Vehicle.Speed < -.02f)
                {
                    cell.Scratch = Math.Min(1, cell.Scratch + blade * 2.5f);
                    cell.Quality = Math.Max(0, cell.Quality - blade);
                }
                else if (blade > 0)
                {
                    cell.CutDepthMm += blade;
                    cell.Scratch = Math.Max(0, cell.Scratch - blade * 2.3f);
                }
                cell.SnowMm -= snowRemoved;
                newSnow += (snowRemoved + blade * Config.SnowExpansionRatio) * area * .001f;
                if (washPerCell > 0 && pressure > .7f)
                {
                    cell.WashLiters += washPerCell;
                    cell.Scratch = Math.Max(0, cell.Scratch - washPerCell / area * .025f);
                }
                if (floodDepth > 0)
                {
                    cell.FloodMm = Ice.LiquidDepth(cell, Time) + floodDepth;
                    cell.LastWetTime = Time;
                    float excessLimit = Config.ExcessFloodDepthMm * (cell.Sensitive ? .78f : 1f);
                    cell.OverwaterMm = Math.Max(cell.OverwaterMm, Math.Max(0, cell.FloodMm - excessLimit));
                }
                float film = Clamp(cell.FloodMm / Config.GoodFloodDepthMm, 0, 1);
                float clean = 1f - cell.Scratch;
                float overcut = Math.Max(0, cell.CutDepthMm - (cell.Sensitive ? Config.SensitiveCumulativeSafeCutMm : Config.CumulativeSafeCutMm));
                float finish = Clamp(clean * .58f + film * .32f + Clamp(cell.WashLiters / (area * .1f), 0, 1) * .10f - cell.SnowMm * .45f - cell.OverwaterMm * .35f - overcut * .5f, 0, 1);
                cell.Quality = finish;
                if (cell.CutDepthMm > .08f && cell.FloodMm > .08f && cell.SnowMm < .25f) cell.Covered = true;
                cell.LastProcessTime = Time;
                Ice.Cells[index] = cell;
            }
            HorizontalSnowM3 += newSnow;
            if (Controls.BoardBrush && pressure > .8f && advance > 0) BrushEdge(distance);
        }

        void BrushEdge(float distance)
        {
            // The brush extends to the operator's LEFT and sweeps only actual perimeter contact.
            float sn = (float)Math.Sin(Vehicle.Heading), cs = (float)Math.Cos(Vehicle.Heading);
            float bx = Vehicle.X - cs * (Config.VehicleHalfWidth + .28f), bz = Vehicle.Z + sn * (Config.VehicleHalfWidth + .28f);
            if (Ice.IsInside(bx, bz, .60f)) return;
            if (bx < Ice.MinX - .4f || bx > -Ice.MinX + .4f || bz < Ice.MinZ - .4f || bz > -Ice.MinZ + .4f) return;
            int cx = Math.Max(0, Math.Min(Ice.Columns - 1, (int)Math.Floor((bx - Ice.MinX) / Ice.CellSize)));
            int cz = Math.Max(0, Math.Min(Ice.Rows - 1, (int)Math.Floor((bz - Ice.MinZ) / Ice.CellSize)));
            for (int z = Math.Max(0, cz - 2); z <= Math.Min(Ice.Rows - 1, cz + 2); z++)
            for (int x = Math.Max(0, cx - 2); x <= Math.Min(Ice.Columns - 1, cx + 2); x++)
            {
                int i = Ice.Index(x, z); var cell = Ice.Cells[i];
                if (!cell.IsIce || Ice.IsInside(Ice.CellX(x), Ice.CellZ(z), .65f)) continue;
                float amount = Math.Min(cell.SnowMm, distance * .6f);
                cell.SnowMm -= amount;
                cell.Quality = Math.Min(1, cell.Quality + amount * .1f);
                HorizontalSnowM3 += amount * Ice.CellSize * Ice.CellSize * .001f;
                Ice.Cells[i] = cell;
            }
        }

        void ConveySnow(float dt)
        {
            EscapingSnowRate = 0;
            if (Controls.HorizontalAuger)
            {
                float transported = Math.Min(HorizontalSnowM3, Math.Min(Config.HorizontalCapacityM3PerSecond * dt, Math.Max(0, Config.VerticalSnowCapacityM3 - VerticalSnowM3)));
                HorizontalSnowM3 -= transported; VerticalSnowM3 += transported;
            }
            if (Controls.VerticalAuger && TankLift < .02f)
            {
                float transported = Math.Min(VerticalSnowM3, Math.Min(Config.VerticalCapacityM3PerSecond * dt, Math.Max(0, Config.SnowCapacityM3 - SnowTankM3)));
                VerticalSnowM3 -= transported; SnowTankM3 += transported; TotalSnowCollectedM3 += transported;
            }
            float spill = Math.Max(0, HorizontalSnowM3 - Config.ConditionerSnowCapacityM3) + Math.Max(0, VerticalSnowM3 - Config.VerticalSnowCapacityM3);
            HorizontalSnowM3 = Math.Min(HorizontalSnowM3, Config.ConditionerSnowCapacityM3);
            VerticalSnowM3 = Math.Min(VerticalSnowM3, Config.VerticalSnowCapacityM3);
            if (spill > 0)
            {
                EscapingSnowRate = spill / dt;
                Fault(11, "Snow is escaping the conditioner. Clear the conveyors or empty the tank.");
                if (footprintCount > 0)
                {
                    float mm = spill * 1000f / (footprintCount * Ice.CellSize * Ice.CellSize);
                    for (int n = 0; n < footprintCount; n++)
                    {
                        int i = footprint[n]; var cell = Ice.Cells[i]; cell.SnowMm += mm; cell.Quality = Math.Max(0, cell.Quality - mm * .5f); Ice.Cells[i] = cell;
                    }
                }
            }
            if (SnowTankM3 >= Config.SnowCapacityM3 * .98f) Fault(12, "Snow tank full. Stop cutting and return to the dump bay.");
        }

        void UpdateTank(float dt)
        {
            bool safe = InDumpZone && EquipmentSafe && Math.Abs(Vehicle.Speed) < .035f;
            float target = Controls.TankLiftTarget;
            if (target > TankLift && !safe)
            {
                Fault(13, "Face the back-wall snow pit and stop in the far bay. Stow conditioner and stop conveyors/water before dumping.");
                target = TankLift;
            }
            TankLift = MoveTowards(TankLift, target, dt / Config.TankLiftSeconds);
            if (TankLift > .8f && safe && SnowTankM3 > 0)
            {
                if (!dumping) { TankDumps++; dumping = true; }
                float amount = Math.Min(SnowTankM3, Config.DumpM3PerSecond * dt);
                SnowTankM3 -= amount; DumpedSnowM3 += amount;
                LastMessage = "Snow tank unloading. Wait until empty, then lower the tank before moving.";
            }
            if (TankLift < .1f) dumping = false;
        }

        /// <summary>
        /// Manual assistance at the existing horizontal-to-vertical conveyor throat.
        /// Conserves snow and respects free capacity; it does not empty or enlarge a tank.
        /// Stroke volume is a gameplay calibration rather than a manufacturer rating.
        /// </summary>
        public bool StrokeSnowBreaker()
        {
            if (Finished || TankLift > .02f || Conditioner.Lift > .15f || !Controls.VerticalAuger)
            {
                LastMessage = "Snow breaker: lower the conditioner and tank, and run the vertical auger first.";
                return false;
            }
            float moved = Math.Min(HorizontalSnowM3, Math.Min(Config.SnowBreakerStrokeM3,
                Math.Max(0, Config.VerticalSnowCapacityM3 - VerticalSnowM3)));
            HorizontalSnowM3 -= moved;
            VerticalSnowM3 += moved;
            SnowBreakerStrokes++;
            LastMessage = moved > 0 ? "Manual snow-breaker stroke assisted the conveyor throat." : "Manual snow-breaker stroke completed; no loose snow moved.";
            return true;
        }

        public bool Refill()
        {
            if (!InServiceBay || !EquipmentSafe || Math.Abs(Vehicle.Speed) > .035f || TankLift > .02f)
            { LastMessage = "Service requires a stop in the bay with all equipment stowed and tank lowered."; return false; }
            FloodWaterLiters = Config.FloodCapacityLiters; WashWaterLiters = Config.WashCapacityLiters; PumpDamage = 0; WashPrimeSeconds = 0;
            LastMessage = "Water tanks refilled; wash pickup inspected. Ready for another pass.";
            return true;
        }

        public bool TryFinish()
        {
            if (!HasEnteredRink) { LastMessage = "Enter the rink and resurface before completing the run."; return false; }
            if (!ProperExit || Math.Abs(Vehicle.Speed) > .035f || TankLift > .02f)
            { LastMessage = "Finish requires a proper raised exit, stopped in the bay, all equipment off, and tank lowered."; return false; }
            if (SnowTankM3 > .005f || HorizontalSnowM3 > .001f || VerticalSnowM3 > .001f)
            { LastMessage = "Clear remaining conveyor snow on the rink, then empty the snow tank in the bay before finishing."; return false; }
            Finished = true;
            LastMessage = Practice ? "Practice complete. Review the surface map; no score recorded." : "Run complete. Surface coverage and finish are measured from the actual ice.";
            return true;
        }

        public RunReport GetReport()
        {
            int covered = 0, overwater = 0; double quality = 0;
            for (int i = 0; i < Ice.Cells.Length; i++)
            {
                var cell = Ice.Cells[i]; if (!cell.IsIce) continue;
                if (cell.Covered) covered++; if (cell.OverwaterMm > .05f) overwater++;
                quality += cell.Quality;
            }
            float coverage = (float)covered / Ice.IceCellCount, q = (float)(quality / Ice.IceCellCount), over = (float)overwater / Ice.IceCellCount;
            float tank = TotalSnowCollectedM3 < .005f || (DumpedSnowM3 >= TotalSnowCollectedM3 * .98f && SnowTankM3 < .005f) ? 1 : 0;
            return new RunReport
            {
                Coverage = coverage, MissedFraction = 1 - coverage, FinishQuality = q, OverwaterFraction = over,
                Score = Practice ? 0 : Clamp(coverage * 45 + q * 35 + (1 - over) * 10 + tank * 5 + (ProperExit ? 5 : 0) - Mistakes * 1.2f, 0, 100),
                Mistakes = Mistakes, TankDumps = TankDumps, ProperExit = ProperExit,
                Complete = Finished && coverage >= Config.MinimumScoredCoverage, Practice = Practice, ElapsedSeconds = Time
            };
        }

        void Fault(int id, string message)
        {
            LastMessage = message;
            if (Time - faultTimes[id] < 12f) return;
            Mistakes++; faultTimes[id] = Time;
        }

        public static float Clamp(float value, float min, float max) { return float.IsNaN(value) ? min : Math.Max(min, Math.Min(max, value)); }
        static float MoveTowards(float value, float target, float delta) { return value < target ? Math.Min(target, value + delta) : Math.Max(target, value - delta); }
        static float Wrap(float angle) { while (angle > Math.PI) angle -= (float)(2 * Math.PI); while (angle < -Math.PI) angle += (float)(2 * Math.PI); return angle; }
    }
}
