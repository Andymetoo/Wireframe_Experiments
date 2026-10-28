using System;

namespace IceResurfacer.Core
{
    /// <summary>Deterministic kinematic machine and material balance. No frame, audio, input, or Unity dependencies.</summary>
    public sealed partial class Simulation
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
        public bool TankRaised { get { return TankLift > .001f; } }
        public float HorizontalSnowM3 { get; private set; }
        public float VerticalSnowM3 { get; private set; }
        public float MachineLoad { get; private set; }
        public float EscapingSnowRate { get; private set; }
        public float DischargeSnowRate { get; private set; }
        public float VerticalSpillRate { get; private set; }
        public float DumpSnowRate { get; private set; }
        public float ResidualSnowM3 { get; private set; }
        public int TankShakeoutCount { get; private set; }
        public bool BoardContact { get; private set; }
        public float EffectiveDriveDemand { get; private set; }
        public string DriveInhibitReason
        {
            get
            {
                if (Finished) return "Run finished. Start a new session to drive.";
                if (ServiceDriveInhibited) return ServiceDriveInhibitReason;
                if (TankRaised) return "Snow tank raised. Hold the dump lever LOWER until fully stowed.";
                if (Controls.Brake) return "Service brake applied. Release CTRL to drive.";
                if (BoardContact && Math.Abs(Vehicle.Speed) < .05f && Math.Abs(Controls.Throttle) > .01f)
                    return "Board or wall contact. Reverse clear, then steer away from the wall.";
                if (Vehicle.Speed * Controls.Throttle < -.005f) return "Braking through zero before changing direction. Keep the requested pedal held.";
                return "";
            }
        }
        public float SpilledSnowM3 { get; private set; }
        public float OffIceSnowM3 { get; private set; }
        public float ConveyorCongestion { get; private set; }
        public bool ConveyorJammed { get; private set; }
        public bool GuideRollerContact { get; private set; }
        public float GuideRollerTravel { get; private set; }
        public float ActualBladeDepthMm { get { return Controls.BladeDepthMm*Config.BladeBiteFraction; } }
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
        float remainder, jamTime;
        bool exitUnsafe, dumping;
        float shakePeak, shakeLow;
        bool shakeArmed;
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
            float extent=Math.Max(Config.ConditionerWidth,Config.WaterDistributionWidth)+Config.ConditionerLength+.5f;
            footprint = new int[(int)Math.Ceiling(extent / Config.CellSize) * (int)Math.Ceiling(extent / Config.CellSize)];
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
            if (Controls.HydraulicHoldControls)
                Controls.ConditionerTarget = Clamp(Conditioner.Lift + Controls.ConditionerHydraulic * dt / Config.ConditionerLiftSeconds, 0, 1);
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
            TickWaterService(dt);
            float excessBite=Math.Max(0,Controls.BladeDepthMm/Config.RecommendedBladeDepthMm-1);
            MachineLoad = Clamp(.13f + HorizontalSnowM3 / Config.ConditionerSnowCapacityM3 * .75f + VerticalSnowM3 / Config.VerticalSnowCapacityM3 * .55f +
                (Conditioner.Lift < .15f ? Math.Abs(Vehicle.Speed)*(Controls.BladeDepthMm*.14f+excessBite*excessBite*.22f) : 0) + (Controls.HorizontalAuger ? .08f : 0) + (Controls.VerticalAuger ? .09f : 0)+ConveyorCongestion*.35f+(ConveyorJammed?.5f:0), 0, 1.8f);
            if (MachineLoad > 1.0f) Fault(2, "Conveyor overload: retract blade and stop. Switch both augers OFF before inspecting a jam.");
        }

        void ClampControls()
        {
            Controls.Throttle = Clamp(Controls.Throttle, -1, 1); Controls.Steer = Clamp(Controls.Steer, -1, 1);
            Controls.ConditionerTarget = Clamp(Controls.ConditionerTarget, 0, 1); Controls.TankLiftTarget = Clamp(Controls.TankLiftTarget, 0, 1);
            Controls.ConditionerHydraulic = Clamp(Controls.ConditionerHydraulic, -1, 1);
            Controls.TankHydraulic = Clamp(Controls.TankHydraulic, -1, 1);
            Controls.BladeDepthMm = Clamp(Controls.BladeDepthMm, 0, Config.MaxBladeDepthMm);
            Controls.WashValve = Clamp(Controls.WashValve, 0, 1); Controls.FloodFlow = Clamp(Controls.FloodFlow, 0, 1);
            if (TankRaised && (Controls.HorizontalAuger || Controls.VerticalAuger))
            {
                Controls.HorizontalAuger = Controls.VerticalAuger = false;
                LastMessage = "Augers cannot engage while the snow tank is raised. Lower the dump tank first.";
            }
        }

        public void SetConditionerHydraulic(float command)
        {
            Controls.HydraulicHoldControls = true;
            Controls.ConditionerHydraulic = Clamp(command, -1, 1);
            Controls.ConditionerTarget = Conditioner.Lift;
        }

        public void SetTankHydraulic(float command)
        {
            Controls.HydraulicHoldControls = true;
            Controls.TankHydraulic = Clamp(command, -1, 1);
            Controls.TankLiftTarget = TankLift;
        }

        void UpdateVehicle(float dt)
        {
            Vehicle.SteeringAngle = MoveTowards(Vehicle.SteeringAngle, Controls.Steer * Config.MaxSteeringRadians, Config.SteeringRate * dt);
            float command = Controls.Throttle;
            // A refused hydraulic request is not a raised tank. The old target
            // check latched travel off even when the dump interlock kept it down.
            if (TankRaised || ServiceDriveInhibited)
            {
                if (Math.Abs(command) > .1f) LastMessage = DriveInhibitReason;
                command = 0;
            }
            EffectiveDriveDemand = command;
            float target = command * (command >= 0 ? Config.ForwardSpeed : Config.ReverseSpeed);
            float speed = Vehicle.Speed;
            float massFactor = 1f / (1f + SnowTankM3 * .09f);
            float rate;
            if (Controls.Brake || TankRaised || ServiceDriveInhibited) { target = 0; rate = Config.ServiceBrakeDeceleration; }
            else if (Math.Abs(command) < .01f) { target = 0; rate = Config.HydrostaticDeceleration + Math.Abs(speed) * .12f; }
            else if (speed * command < -.005f) { target = 0; rate = Config.ServiceBrakeDeceleration; }
            else rate = Config.Acceleration * massFactor * Math.Max(.20f, 1f - MachineLoad * .35f);
            float nextSpeed = MoveTowards(speed, target, rate * dt);
            float travel = (speed + nextSpeed) * .5f * dt;
            float turn = travel * (float)Math.Tan(Vehicle.SteeringAngle) / Config.Wheelbase;
            float direction = Vehicle.Heading + turn * .5f;
            float x = Vehicle.X + (float)Math.Sin(direction) * travel, z = Vehicle.Z + (float)Math.Cos(direction) * travel;
            float heading = Wrap(Vehicle.Heading + turn);
            GuideRollerContact=false;
            BoardContact=false;
            if (BodyFits(x, z, heading))
            {
                Vehicle.X = x; Vehicle.Z = z; Vehicle.Heading = heading; Vehicle.Speed = nextSpeed;
            }
            else
            {
                BoardContact=true;
                if (Math.Abs(speed) > .15f) Fault(4, "Board or service wall contact. Brake and steer clear.");
                // Project a shallow contact onto the local board tangent. A small
                // alignment correction follows the roller instead of repeatedly
                // steering the nose through the wall. Every accepted pose is tested.
                ContactNormal(x,z,heading,out float nx,out float nz);
                float dx=x-Vehicle.X,dz=z-Vehicle.Z;
                float normal=dx*nx+dz*nz;
                float incidence=Math.Abs(normal)/Math.Max(.000001f,Math.Abs(travel));
                bool sliding=false;
                if(incidence<.45f&&Math.Abs(travel)>.000001f)
                {
                    float sx=dx-normal*nx,sz=dz-normal*nz;
                    float tangent=(float)Math.Atan2(sx*Math.Sign(travel),sz*Math.Sign(travel));
                    float aligned=Wrap(Vehicle.Heading+Clamp(Wrap(tangent-Vehicle.Heading),-.018f,.018f));
                    float ax=Vehicle.X+sx*.96f,az=Vehicle.Z+sz*.96f;
                    if(BodyFits(ax,az,aligned))
                    {
                        Vehicle.X=ax;Vehicle.Z=az;Vehicle.Heading=aligned;
                        Vehicle.Speed=MoveTowards(nextSpeed*(float)Math.Sqrt(Math.Max(0,1-incidence*incidence)),0,.12f*dt);
                        float sn=(float)Math.Sin(aligned),cs=(float)Math.Cos(aligned);
                        float rx=ax+cs*Config.GuideRollerX+sn*Config.GuideRollerZ;
                        float rz=az-sn*Config.GuideRollerX+cs*Config.GuideRollerZ;
                        GuideRollerContact=Ice.BoardDistance(rx,rz,out _,out _)>-Config.GuideRollerRadius-.10f;
                        sliding=true;
                    }
                }
                if(!sliding)Vehicle.Speed=0;
            }
            float rs=(float)Math.Sin(Vehicle.Heading),rc=(float)Math.Cos(Vehicle.Heading);
            float rollerX=Vehicle.X+rc*Config.GuideRollerX+rs*Config.GuideRollerZ;
            float rollerZ=Vehicle.Z-rs*Config.GuideRollerX+rc*Config.GuideRollerZ;
            GuideRollerContact=Ice.BoardDistance(rollerX,rollerZ,out _,out _)>-Config.GuideRollerRadius-.025f;
            if(GuideRollerContact)GuideRollerTravel+=Vehicle.Speed*dt;
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
            float rx=x+cs*Config.GuideRollerX+sn*Config.GuideRollerZ;
            float rz=z-sn*Config.GuideRollerX+cs*Config.GuideRollerZ;
            for(int i=0;i<8;i++)
            {
                float a=i*(float)Math.PI*.25f;
                if(!Navigable(rx+(float)Math.Cos(a)*Config.GuideRollerRadius,rz+(float)Math.Sin(a)*Config.GuideRollerRadius))return false;
            }
            Ice.BoardDistance(rx,rz,out float nx,out float nz);
            if(!Navigable(rx-nx*Config.GuideRollerRadius,rz-nz*Config.GuideRollerRadius))return false;
            return true;
        }

        void ContactNormal(float x,float z,float heading,out float nx,out float nz)
        {
            nx=0;nz=0;float worst=float.MinValue;
            float sn=(float)Math.Sin(heading),cs=(float)Math.Cos(heading);
            for(int i=0;i<10;i++)
            {
                float lx=i==9?Config.GuideRollerX:(i%3-1)*Config.VehicleHalfWidth;
                float lz=i==9?Config.GuideRollerZ:-Config.BodyRear+(Config.BodyFront+Config.BodyRear)*(i/3)*.5f;
                float px=x+cs*lx+sn*lz,pz=z-sn*lx+cs*lz;
                float d=Ice.BoardDistance(px,pz,out float tx,out float tz)+(i==9?Config.GuideRollerRadius:0);
                // The service-bay opening is a union, not a solid south board.
                if(Math.Abs(px)<Config.GateHalfWidth-.05f&&pz<-Config.RinkLength*.5f+.1f)continue;
                if(d>worst){worst=d;nx=tx;nz=tz;}
            }
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
            float width=Math.Max(Config.ConditionerWidth,Config.WaterDistributionWidth);
            float radius = (width + Config.ConditionerLength) * .5f;
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
                if (Ice.Cells[i].IsIce && Math.Abs(side) <= width * .5f && Math.Abs(fore) <= Config.ConditionerLength * .5f)
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
            float sn=(float)Math.Sin(Conditioner.Heading),cs=(float)Math.Cos(Conditioner.Heading);
            int cutCount=0,waterCount=0;
            for(int n=0;n<footprintCount;n++)
            {
                int i=footprint[n];
                float side=Math.Abs((Ice.CellX(i%Ice.Columns)-Conditioner.X)*cs-(Ice.CellZ(i/Ice.Columns)-Conditioner.Z)*sn);
                if(side<=Config.ConditionerWidth*.5f)cutCount++;
                if(side<=Config.WaterDistributionWidth*.5f)waterCount++;
            }
            float floodDepth = waterCount>0?flood/(waterCount*area):0;
            float washDepth = cutCount>0?(wash-recovery)/(cutCount*area):0;
            float washPerCell = cutCount>0?wash/cutCount:0;
            float blade = ActualBladeDepthMm * pressure * advance;
            float newSnow = 0;
            for (int n = 0; n < footprintCount; n++)
            {
                int index = footprint[n]; var cell = Ice.Cells[index];
                float side=Math.Abs((Ice.CellX(index%Ice.Columns)-Conditioner.X)*cs-(Ice.CellZ(index/Ice.Columns)-Conditioner.Z)*sn);
                bool cutting=side<=Config.ConditionerWidth*.5f;
                float waterDepth=(side<=Config.WaterDistributionWidth*.5f?floodDepth:0)+(cutting?washDepth:0);
                float snowRemoved = cutting?Math.Min(cell.SnowMm, pressure * advance * .6f):0;
                float bite=cutting?blade:0;
                if (cutting&&Vehicle.Speed < -.02f)
                {
                    cell.Scratch = Math.Min(1, cell.Scratch + blade * 2.5f);
                    cell.Quality = Math.Max(0, cell.Quality - blade);
                }
                else if (bite > 0)
                {
                    cell.CutDepthMm += bite;
                    cell.Scratch = Math.Max(0, cell.Scratch - bite / Config.BladeBiteFraction * 2.3f);
                }
                cell.SnowMm -= snowRemoved;
                newSnow += (snowRemoved + bite * Config.SnowExpansionRatio) * area * .001f;
                if (cutting&&washPerCell > 0 && pressure > .7f)
                {
                    cell.WashLiters += washPerCell;
                    cell.Scratch = Math.Max(0, cell.Scratch - washPerCell / area * .025f);
                }
                if (waterDepth > 0)
                {
                    cell.FloodMm = Ice.LiquidDepth(cell, Time) + waterDepth;
                    cell.LastWetTime = Time;
                    float excessLimit = Config.ExcessFloodDepthMm * (cell.Sensitive ? .78f : 1f);
                    cell.OverwaterMm = Math.Max(cell.OverwaterMm, Math.Max(0, cell.FloodMm - excessLimit));
                }
                float film = Clamp(cell.FloodMm / Config.GoodFloodDepthMm, 0, 1);
                float clean = 1f - cell.Scratch;
                float overcut = Math.Max(0, cell.CutDepthMm/Config.BladeBiteFraction - (cell.Sensitive ? Config.SensitiveCumulativeSafeCutMm : Config.CumulativeSafeCutMm));
                float finish = Clamp(clean * .58f + film * .32f + Clamp(cell.WashLiters / (area * .1f), 0, 1) * .10f - cell.SnowMm * .45f - cell.OverwaterMm * .35f - overcut * .5f, 0, 1);
                cell.Quality = finish;
                if (cell.CutDepthMm > .08f*Config.BladeBiteFraction && cell.FloodMm > .08f && cell.SnowMm < .25f) cell.Covered = true;
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
            EscapingSnowRate = 0;DischargeSnowRate=0;VerticalSpillRate=0;
            float crowd=Clamp((SnowTankM3/Config.SnowCapacityM3-.85f)/.15f,0,1);
            ConveyorCongestion=Math.Max(crowd,jamTime/Config.ConveyorJamSeconds);
            if (Controls.HorizontalAuger&&!ConveyorJammed)
            {
                float transported = Math.Min(HorizontalSnowM3, Math.Min(Config.HorizontalCapacityM3PerSecond * dt, Math.Max(0, Config.VerticalSnowCapacityM3 - VerticalSnowM3)));
                HorizontalSnowM3 -= transported; VerticalSnowM3 += transported;
            }
            float rejected=0;
            if (Controls.VerticalAuger && !TankRaised&&!ConveyorJammed)
            {
                float delivered=Math.Min(VerticalSnowM3,Config.VerticalCapacityM3PerSecond*dt*(1-crowd*.6f));
                float transported = Math.Min(delivered,Math.Max(0, Config.SnowCapacityM3 - SnowTankM3));
                VerticalSnowM3 -= delivered; SnowTankM3 += transported; TotalSnowCollectedM3 += transported;
                DischargeSnowRate=transported/dt;
                rejected=delivered-transported;
                if(rejected>0)
                {
                    VerticalSpillRate=rejected/dt;
                    SpillAtDischarge(rejected);
                }
            }
            jamTime=rejected>0?Math.Min(Config.ConveyorJamSeconds,jamTime+dt):Math.Max(0,jamTime-dt*.15f);
            if(jamTime>=Config.ConveyorJamSeconds-.0001f)ConveyorJammed=true;
            float spill = Math.Max(0, HorizontalSnowM3 - Config.ConditionerSnowCapacityM3) + Math.Max(0, VerticalSnowM3 - Config.VerticalSnowCapacityM3);
            HorizontalSnowM3 = Math.Min(HorizontalSnowM3, Config.ConditionerSnowCapacityM3);
            VerticalSnowM3 = Math.Min(VerticalSnowM3, Config.VerticalSnowCapacityM3);
            if (spill > 0)
            {
                SpilledSnowM3+=spill;
                float deposited=Ice.DepositSnow(Conditioner.X,Conditioner.Z,spill,.45f);
                OffIceSnowM3+=spill-deposited;
            }
            EscapingSnowRate=(spill+rejected)/dt;
            if(EscapingSnowRate>0)Fault(11,"Snow is spilling onto the ice. Retract blade, stop and empty the tank.");
            if(ConveyorJammed)Fault(14,"AUGER JAM: stop, retract blade and switch BOTH augers OFF. Empty tank, then use J to reset after inspection.");
            if (SnowTankM3 >= Config.SnowCapacityM3 * .98f) Fault(12, "Snow tank full. Stop cutting and return to the dump bay.");
        }

        void SpillAtDischarge(float volume)
        {
            float sn=(float)Math.Sin(Vehicle.Heading),cs=(float)Math.Cos(Vehicle.Heading);
            SpilledSnowM3+=volume;
            for(int side=-1;side<=1;side+=2)
            {
                float x=Vehicle.X+cs*side*1.1f+sn*.25f,z=Vehicle.Z-sn*side*1.1f+cs*.25f;
                float deposited=Ice.DepositSnow(x,z,volume*.5f,.4f);
                OffIceSnowM3+=volume*.5f-deposited;
            }
        }

        public bool TryClearConveyorJam()
        {
            if(Controls.HorizontalAuger||Controls.VerticalAuger||Math.Abs(Vehicle.Speed)>.035f||Controls.BladeDepthMm>.001f||TankRaised||SnowTankM3>=Config.SnowCapacityM3*.98f)
            { LastMessage="Jam inspection/reset requires a stop, blade out, BOTH augers OFF, tank empty enough and fully lowered.";return false; }
            ConveyorJammed=false;jamTime=0;ConveyorCongestion=0;
            LastMessage="Stopped conveyor inspection/reset complete. Start augers only after the obstruction is cleared.";return true;
        }

        void UpdateTank(float dt)
        {
            DumpSnowRate=0;
            bool safe = InDumpZone && EquipmentSafe && Math.Abs(Vehicle.Speed) < .035f && !ServiceDriveInhibited;
            bool mayRaise = safe && Math.Abs(Controls.Throttle) < .01f;
            float target = Controls.HydraulicHoldControls
                ? Clamp(TankLift + Controls.TankHydraulic * dt / Config.TankLiftSeconds, 0, 1)
                : Controls.TankLiftTarget;
            if (target > TankLift && !mayRaise)
            {
                LastMessage = "Dump raise refused: face the far-bay pit, release W/S, stop, stow equipment and clear the service hose. Hold LOWER to stow a raised tank.";
                target = TankLift;
            }
            // A rejected request must never remain a latent movement interlock.
            Controls.TankLiftTarget = target;
            float previousLift = TankLift;
            TankLift = MoveTowards(TankLift, target, dt / Config.TankLiftSeconds);
            if (dumping && TankLift > .72f)
            {
                if (TankLift < previousLift)
                {
                    shakeLow = Math.Min(shakeLow, TankLift);
                    if (shakePeak - shakeLow >= .035f) shakeArmed = true;
                }
                if (shakeArmed && TankLift - shakeLow >= .025f && TankLift > previousLift && safe)
                {
                    ResidualSnowM3 *= .28f;
                    if (ResidualSnowM3 < .0001f) ResidualSnowM3 = 0;
                    TankShakeoutCount++;
                    shakeArmed = false; shakePeak = TankLift; shakeLow = TankLift;
                }
                if (!shakeArmed && TankLift > shakePeak) shakePeak = shakeLow = TankLift;
            }
            if (TankLift > .8f && safe && SnowTankM3 > 0)
            {
                if (!dumping)
                {
                    TankDumps++; dumping = true;
                    // Calibration assumption: 2.5% initially adheres to the bin.
                    // Small real lowering/raising movements release 72% of each
                    // remaining layer; all released snow still uses the outlet.
                    ResidualSnowM3 = SnowTankM3 * .025f;
                    shakePeak = shakeLow = TankLift; shakeArmed = false;
                }
                float amount = Math.Min(Math.Max(0, SnowTankM3 - ResidualSnowM3), Config.DumpM3PerSecond * dt);
                SnowTankM3 -= amount; DumpedSnowM3 += amount;
                DumpSnowRate=amount/dt;
                LastMessage = amount > 0 ? "Snow falling into the pit. Hold position; lower the tank before travelling."
                    : "Residual snow in bin. Briefly lower a few degrees, then raise to dislodge it. Repeat as needed.";
            }
            if (TankLift < .1f) { dumping = false; shakeArmed = false; }
        }

        /// <summary>
        /// Manual assistance at the existing horizontal-to-vertical conveyor throat.
        /// Conserves snow and respects free capacity; it does not empty or enlarge a tank.
        /// Stroke volume is a gameplay calibration rather than a manufacturer rating.
        /// </summary>
        public bool StrokeSnowBreaker()
        {
            if (Finished || ConveyorJammed || TankRaised || Conditioner.Lift > .15f || !Controls.VerticalAuger)
            {
                LastMessage = ConveyorJammed?"Jam: stop BOTH augers before inspection/reset. Do not use the running snow breaker to clear a physical jam.":"Snow breaker: lower the conditioner and tank, and run the vertical auger first.";
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
            if (!InServiceBay || !EquipmentSafe || Math.Abs(Vehicle.Speed) > .035f || TankRaised)
            { LastMessage = "Service requires a stop in the bay with all equipment stowed and tank lowered."; return false; }
            FloodWaterLiters = Config.FloodCapacityLiters; WashWaterLiters = Config.WashCapacityLiters; PumpDamage = 0; WashPrimeSeconds = 0;
            LastMessage = "Water tanks refilled; wash pickup inspected. Ready for another pass.";
            return true;
        }

        public bool TryFinish()
        {
            if (ServiceDriveInhibited || SupplyValve > .001f)
            { LastMessage = ServiceDriveInhibited ? ServiceDriveInhibitReason : "Close the wall water supply and return the hose before finishing."; return false; }
            if (!HasEnteredRink) { LastMessage = "Enter the rink and resurface before completing the run."; return false; }
            if (!ProperExit || Math.Abs(Vehicle.Speed) > .035f || TankRaised)
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
