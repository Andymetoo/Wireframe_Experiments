using System;

namespace IceResurfacer.Core
{
    [Flags]
    public enum SnowTransportCondition
    {
        None=0,HorizontalStopped=1,VerticalStopped=2,Congested=4,TankFull=8,Jammed=16,RaisedBin=32
    }
    /// <summary>Deterministic kinematic machine and material balance. No frame, audio, input, or Unity dependencies.</summary>
    public sealed partial class Simulation
    {
        public readonly SimulatorConfig Config;
        public readonly OperatorControls Controls = new OperatorControls();
        public readonly VehicleState Vehicle = new VehicleState();
        public readonly ConditionerState Conditioner = new ConditionerState();
        public readonly IceField Ice;
        public readonly ServiceLayout Layout;
        public readonly EnvironmentField Environment;
        public readonly bool Practice;
        float forwardSpeedOverride;
        public float MaximumForwardSpeedTarget=>forwardSpeedOverride>0?forwardSpeedOverride:Config.ForwardSpeed;
        /// <summary>Runtime operator target only; calibration and actual velocity remain untouched.</summary>
        public void SetMaximumForwardSpeed(float metersPerSecond)
        {
            if(float.IsNaN(metersPerSecond)||float.IsInfinity(metersPerSecond)||metersPerSecond<=0)throw new ArgumentOutOfRangeException(nameof(metersPerSecond));
            forwardSpeedOverride=metersPerSecond;
        }
        public readonly SimulationMode Mode;
        public bool GuidedTraining { get { return Mode == SimulationMode.GuidedTraining; } }
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
        public float ConditionerLeftEscapeRate { get; private set; }
        public float ConditionerRightEscapeRate { get; private set; }
        public float ConditionerSnowFill { get { return Clamp(HorizontalSnowM3/Config.ConditionerSnowCapacityM3,0,1); } }
        public SnowTransportCondition SnowCondition { get; private set; }
        public float ConditionerLeftExitX { get; private set; }
        public float ConditionerLeftExitZ { get; private set; }
        public float ConditionerRightExitX { get; private set; }
        public float ConditionerRightExitZ { get; private set; }
        public float ConditionerLeftLandingX { get; private set; }
        public float ConditionerLeftLandingZ { get; private set; }
        public float ConditionerRightLandingX { get; private set; }
        public float ConditionerRightLandingZ { get; private set; }
        public float UpperSnowOriginX { get; private set; }
        public float UpperSnowOriginY { get; private set; }
        public float UpperSnowOriginZ { get; private set; }
        public float UpperSnowVelocityX { get; private set; }
        public float UpperSnowVelocityY { get; private set; }
        public float UpperSnowVelocityZ { get; private set; }
        public float DumpSnowRate { get; private set; }
        public float ResidualSnowM3 { get; private set; }
        public int TankShakeoutCount { get; private set; }
        public bool BoardContact { get; private set; }
        public float EffectiveDriveDemand { get; private set; }
        public float TractionSlip { get; private set; }
        public float LateralVelocity { get; private set; }
        public float TractionGrip { get; private set; } = 1f;
        public float RaisedTankTravelSeconds { get; private set; }
        public float LoadCutting { get; private set; }
        public float LoadConveyors { get; private set; }
        public float LoadCongestion { get; private set; }
        public string DriveInhibitReason
        {
            get
            {
                if (Finished) return "Run finished. Start a new session to drive.";
                if (ServiceDriveInhibited) return ServiceDriveInhibitReason;
                if (GuidedTraining && TankRaised) return "Training assistance: snow tank raised. Hold the dump lever LOWER until fully stowed.";
                if (Controls.Brake) return "Service brake applied. Release CTRL to drive.";
                if (BoardContact && Math.Abs(Vehicle.Speed) < .05f && Math.Abs(Controls.Throttle) > .01f)
                    return LastMovementContact.Reason+" contact. Reverse clear, then steer away from the obstacle.";
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
        public float ActualBladeDepthMm { get { return Config.EffectiveBladeDepth(Controls.BladeDepthMm); } }
        public float WashPrimeSeconds { get; private set; }
        public float SurfacePickupPrimeSeconds { get; private set; }
        // The switch engages the clutch; the horizontal conveyor supplies its drive.
        public bool WashPumpRunning => Controls.WashPump && Controls.HorizontalAuger && !ConveyorJammed;
        public float PumpDamage { get; private set; }
        public int Mistakes { get; private set; }
        public int TankDumps { get; private set; }
        public int SnowBreakerStrokes { get; private set; }
        public float DumpedSnowM3 { get; private set; }
        public float PitSnowM3 { get; private set; }
        public float DumpSnowOnIceM3 { get; private set; }
        public float DumpOriginX { get; private set; }
        public float DumpOriginY { get; private set; }
        public float DumpOriginZ { get; private set; }
        public float DumpVelocityX { get; private set; }
        public float DumpVelocityY { get; private set; }
        public float DumpVelocityZ { get; private set; }
        public float DumpLandingX { get; private set; }
        public float DumpLandingZ { get; private set; }
        public float TotalSnowCollectedM3 { get; private set; }
        public float TotalFloodUsedLiters { get; private set; }
        public float TotalWashUsedLiters { get; private set; }
        public bool HasEnteredRink { get; private set; }
        public bool Finished { get; private set; }
        public string LastMessage { get; private set; } = "Drive through the gate. Lower with the blade out, then engage both augers.";
        public bool InServiceBay { get { return Layout.InsideRoom(Vehicle.X,Vehicle.Z); } }
        public bool InDumpZone { get { ComputeDumpTrajectory(out float x,out float y,out float z,out float vx,out float vy,out float vz); float t=FallTime(y,vy);return Layout.ContainsPit(x+vx*t,z+vz*t,.35f); } }
        public bool EquipmentSafe { get { return Conditioner.Lift >= .97f && Controls.BladeDepthMm <= .001f && !Controls.HorizontalAuger && !Controls.VerticalAuger && !Controls.BoardBrush && !Controls.WashPump && Controls.WashValve <= .001f && Controls.FloodFlow <= .001f; } }
        public bool ProperExit { get { return HasEnteredRink && !exitUnsafe && InServiceBay && ConditionerFullyOutside() && EquipmentSafe; } }

        readonly float[] faultTimes = new float[20];
        readonly int[] footprint;
        int footprintCount;
        float remainder, jamTime;
        bool exitUnsafe, dumping;
        float shakePeak, shakeLow;
        bool shakeArmed;
        float lastConditionerX, lastConditionerZ;
        const float FixedStep = .02f;

        public Simulation(SimulatorConfig config = null, bool practice = false, int seed = 546, SimulationMode mode = SimulationMode.FreeSimulation)
        {
            Config = config ?? new SimulatorConfig();
            Config.Validate();
            Practice = practice;
            Mode = mode;
            Layout = new ServiceLayout(Config);
            Environment = new EnvironmentField(Config,Layout);
            Ice = new IceField(Config, seed);
            Layout.ParkingPose(out Vehicle.X,out Vehicle.Z,out Vehicle.Heading);
            Conditioner.Heading=Vehicle.Heading;
            Conditioner.X = Vehicle.X - (float)Math.Sin(Vehicle.Heading)*(Config.HitchBehindAxle+Config.DrawbarLength);
            Conditioner.Z = Vehicle.Z - (float)Math.Cos(Vehicle.Heading)*(Config.HitchBehindAxle+Config.DrawbarLength);
            lastConditionerX = Conditioner.X; lastConditionerZ = Conditioner.Z;
            FloodWaterLiters = Config.FloodCapacityLiters;
            WashWaterLiters = Config.WashCapacityLiters;
            float extent=Math.Max(Config.ConditionerWidth,Config.WaterDistributionWidth)+2*Config.WaterFilmSpreadMeters+Config.ConditionerLength+.5f;
            footprint = new int[(int)Math.Ceiling(extent / Config.CellSize) * (int)Math.Ceiling(extent / Config.CellSize)];
            for (int i = 0; i < faultTimes.Length; i++) faultTimes[i] = -1000;
            UpdateBoardBrush(0,0);
            UpdateSnowExitPositions();
            LastMessage=GuidedTraining?"Guided Training assistance is active. Enter through the southeast gate with equipment raised.":"Free Simulation: machinery remains under your control. Enter through the southeast gate when ready.";
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
            UpdateConditioner();
            CheckExit();
            FindFootprint();
            ProcessIce(dt);
            ConveySnow(dt);
            UpdateTank(dt);
            TickWaterService(dt);
            UpdateMachineLoad();
            if (MachineLoad > 1.0f) Fault(2, "High machine load: reduce cutting depth or ground speed; inspect the conveyor state.");
        }

        void UpdateMachineLoad()
        {
            float excessBite=Math.Max(0,Controls.BladeDepthMm/Config.RecommendedBladeDepthMm-1);
            float deeperBite=Math.Max(0,ActualBladeDepthMm-Controls.BladeDepthMm*Config.BladeBiteFraction);
            LoadCutting = Conditioner.Lift < .15f ? Math.Abs(Vehicle.Speed)*(Controls.BladeDepthMm*.14f+excessBite*excessBite*.22f+deeperBite*1.5f) : 0;
            LoadConveyors = HorizontalSnowM3/Config.ConditionerSnowCapacityM3*.75f+VerticalSnowM3/Config.VerticalSnowCapacityM3*.55f+(Controls.HorizontalAuger?.08f:0)+(Controls.VerticalAuger?.09f:0);
            LoadCongestion = ConveyorCongestion*.35f+(ConveyorJammed?.5f:0);
            MachineLoad = Clamp(.13f+LoadCutting+LoadConveyors+LoadCongestion,0,1.8f);
        }

        void ClampControls()
        {
            Controls.Throttle = Clamp(Controls.Throttle, -1, 1); Controls.Steer = Clamp(Controls.Steer, -1, 1);
            Controls.ConditionerTarget = Clamp(Controls.ConditionerTarget, 0, 1); Controls.TankLiftTarget = Clamp(Controls.TankLiftTarget, 0, 1);
            Controls.ConditionerHydraulic = Clamp(Controls.ConditionerHydraulic, -1, 1);
            Controls.TankHydraulic = Clamp(Controls.TankHydraulic, -1, 1);
            Controls.BladeDepthMm = Clamp(Controls.BladeDepthMm, 0, Config.MaxBladeDepthMm);
            Controls.WashValve = Clamp(Controls.WashValve, 0, 1); Controls.FloodFlow = Clamp(Controls.FloodFlow, 0, 1);
            if (GuidedTraining && TankRaised && (Controls.HorizontalAuger || Controls.VerticalAuger))
            {
                Controls.HorizontalAuger = Controls.VerticalAuger = false;
                LastMessage = "Training assistance: augers are held OFF while the snow tank is raised. Lower the dump tank first.";
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
            // Only instructional assistance may inhibit travel for raised tanks
            // or attached hoses. Free Simulation preserves the hazardous state.
            bool trainingBrake=GuidedTraining&&(TankRaised||ServiceDriveInhibited);
            if (trainingBrake || ServiceDriveInhibited)
            {
                if (Math.Abs(command) > .1f) LastMessage = DriveInhibitReason;
                command = 0;
            }
            EffectiveDriveDemand = command;
            float target = command * (command >= 0 ? MaximumForwardSpeedTarget : Config.ReverseSpeed);
            float speed = Vehicle.Speed;
            float massFactor = 1f / (1f + SnowTankM3 * .09f);
            float rate;
            if (Controls.Brake || trainingBrake) { target = 0; rate = Config.ServiceBrakeDeceleration; }
            else if (Math.Abs(command) < .01f) { target = 0; rate = Config.HydrostaticDeceleration + Math.Abs(speed) * .12f; }
            else if (speed * command < -.005f) { target = 0; rate = Config.ServiceBrakeDeceleration; }
            else if (forwardSpeedOverride>0&&command>0&&speed>MaximumForwardSpeedTarget) { rate=Config.HydrostaticDeceleration+Math.Abs(speed)*.12f; }
            else rate = Config.Acceleration * massFactor * Math.Max(.20f, 1f - MachineLoad * .35f);
            float lateralDemand=speed*speed*Math.Abs((float)Math.Tan(Vehicle.SteeringAngle))/Config.Wheelbase;
            bool onIce=Ice.IsInside(Vehicle.X,Vehicle.Z);
            float availableGrip=Config.IceLateralGrip*(1-TankLift*Config.RaisedTankGripPenalty);
            float accelerationSlip=onIce&&Math.Abs(command)>.85f&&Math.Abs(target-speed)>.5f?.10f:0;
            float wantedSlip=onIce?Clamp((lateralDemand-availableGrip)/availableGrip*.6f+accelerationSlip,0,.55f):0;
            TractionSlip=MoveTowards(TractionSlip,wantedSlip,dt*(wantedSlip>TractionSlip?1.1f:Config.TireSlipRecovery));
            TractionGrip=1-TractionSlip*.30f;
            float nextSpeed = MoveTowards(speed, target, rate * TractionGrip * dt);
            float travel = (speed + nextSpeed) * .5f * dt;
            float turn = travel * (float)Math.Tan(Vehicle.SteeringAngle) / Config.Wheelbase*(1-TractionSlip*.22f);
            float direction = Vehicle.Heading + turn * .5f;
            float desiredLateral=-Math.Sign(Vehicle.SteeringAngle)*Config.MaximumLateralSlip*TractionSlip*Math.Sign(speed);
            LateralVelocity=MoveTowards(LateralVelocity,desiredLateral,dt*.35f);
            float lateralTravel=LateralVelocity*dt;
            float x = Vehicle.X + (float)Math.Sin(direction) * travel+(float)Math.Cos(direction)*lateralTravel;
            float z = Vehicle.Z + (float)Math.Cos(direction) * travel-(float)Math.Sin(direction)*lateralTravel;
            float heading = Wrap(Vehicle.Heading + turn);
            sustainedContact=BoardContact||GuideRollerContact;
            GuideRollerContact=false;
            BoardContact=false;
            float startSin=(float)Math.Sin(Vehicle.Heading),startCos=(float)Math.Cos(Vehicle.Heading);
            float startRollerX=Vehicle.X+startCos*Config.GuideRollerX+startSin*Config.GuideRollerZ;
            float startRollerZ=Vehicle.Z-startSin*Config.GuideRollerX+startCos*Config.GuideRollerZ;
            if (ProbeVehiclePose(x, z, heading,out MovementContact rejected))
            {
                Vehicle.X = x; Vehicle.Z = z; Vehicle.Heading = heading; Vehicle.Speed = nextSpeed;
            }
            else
            {
                var hit=DeepestContact(x,z,heading,rejected);
                RecordContact(hit,(x-Vehicle.X)/dt,(z-Vehicle.Z)/dt,turn/dt);
                LastMovementContact=hit;LastMovementContactTime=Time;
                BoardContact=true;
                // Glancing contact slides, a square impact stops (see ResolveContactStep). Rolling on
                // the guide roller or a light scrape is not a mistake; a real bump, and any hard hit, is.
                float allowed=ResolveContactStep(dt,x,z,heading,out float fx,out float fz,out float fh,out float correction,out bool rollerOnly);
                // A pose already inside the driving margin (placed there, or the envelope changed) may always
                // move out of it: any step that reduces the deepest penetration is taken, never one deeper.
                if(allowed<.05f&&DeepestPenetration(Vehicle.X,Vehicle.Z,Vehicle.Heading,out var inside)&&!inside.Tank&&
                   DeepestPenetration(x,z,heading,out var after)&&!after.Tank&&after.Depth<inside.Depth-.00001f)
                { fx=x;fz=z;fh=heading;allowed=1;correction=0; }
                LastContactAllowedFraction=allowed;
                if (Math.Abs(speed) > .15f && ContactNormalSpeed > (rollerOnly ? .6f : SlidingImpactSpeed)) Fault(4, "Board or service wall contact. Brake and steer clear.");
                Vehicle.X=fx;Vehicle.Z=fz;Vehicle.Heading=fh;
                float resolved=allowed<.05f?0:nextSpeed*allowed;
                // Friction on the board face, from the normal reaction the tyres can sustain.
                float reaction=Math.Min(correction/(dt*dt),Config.IceLateralGrip);
                Vehicle.Speed=MoveTowards(resolved,0,(rollerOnly?Config.GuideRollerFriction:Config.BodyContactFriction)*reaction*dt);
                // The boards also absorb sideways tyre slip toward them.
                float sn=(float)Math.Sin(fh),cs=(float)Math.Cos(fh);
                if(Vehicle.Speed==0||LateralVelocity*(cs*hit.NormalX-sn*hit.NormalZ)<0)LateralVelocity=0;
            }
            float rs=(float)Math.Sin(Vehicle.Heading),rc=(float)Math.Cos(Vehicle.Heading);
            float rollerX=Vehicle.X+rc*Config.GuideRollerX+rs*Config.GuideRollerZ;
            float rollerZ=Vehicle.Z-rs*Config.GuideRollerX+rc*Config.GuideRollerZ;
            float rollerBoard=Ice.BoardDistance(rollerX,rollerZ,out float boardX,out float boardZ);
            GuideRollerContact=!Layout.IsPortal(rollerX,rollerZ)&&rollerBoard>-Config.GuideRollerRadius-.025f&&Ice.IsInside(Vehicle.X,Vehicle.Z);
            if(GuideRollerContact)
            {
                // The roller turns with its centre's travel along the board face, forward positive.
                float tx=-boardZ,tz=boardX;
                if(tx*rs+tz*rc<0){tx=-tx;tz=-tz;}
                GuideRollerTravel+=(rollerX-startRollerX)*tx+(rollerZ-startRollerZ)*tz;
            }
            if (Ice.IsInside(Vehicle.X, Vehicle.Z, 2f)) HasEnteredRink = true;
            if (Vehicle.Speed < -.12f && Conditioner.Lift < .8f) Fault(5, "Raise the conditioner before reversing; the lowered blade scores the surface.");
            if(TankRaised&&Math.Abs(Vehicle.Speed)>.15f)
            {
                RaisedTankTravelSeconds+=dt;
                Fault(16,"Raised tank: higher centre of mass reduces cornering grip. Lower it when safe; travel remains under your control.");
            }
        }

        public bool Navigable(float x, float z)
        {
            return Ice.IsInside(x, z, .02f) || Layout.ContainsVehicleFloor(x,z);
        }

        void UpdateConditioner()
        {
            lastConditionerX = Conditioner.X; lastConditionerZ = Conditioner.Z;
            float sn = (float)Math.Sin(Vehicle.Heading), cs = (float)Math.Cos(Vehicle.Heading);
            // The lifting links hold the conditioner square to the chassis at
            // every lift height, including while reversing or standing still.
            float behindAxle = Config.HitchBehindAxle + Config.DrawbarLength;
            Conditioner.Heading = Vehicle.Heading;
            Conditioner.X = Vehicle.X - sn * behindAxle;
            Conditioner.Z = Vehicle.Z - cs * behindAxle;
        }

        void CheckExit()
        {
            if (!HasEnteredRink) return;
            if(Ice.IsInside(Conditioner.X,Conditioner.Z,Config.ConditionerWidth)&&Ice.IsInside(Vehicle.X,Vehicle.Z,Config.ConditionerWidth))exitUnsafe=false;
            Layout.ToLocal(Conditioner.X,Conditioner.Z,out float side,out float outward);
            float relative=Conditioner.Heading-Layout.OutwardHeading;
            float envelope=Math.Abs((float)Math.Sin(relative))*Config.ConditionerWidth*.5f+Math.Abs((float)Math.Cos(relative))*Config.ConditionerLength*.5f;
            if(Math.Abs(side)<Config.GateHalfWidth&&outward+envelope>-.3f&&outward-envelope<.3f&&!EquipmentSafe)
            {
                exitUnsafe = true;
                Fault(6, "Equipment crossing the threshold while working: blade wear, snow loss and runoff. Raise/stow for a clean exit.");
            }
        }

        bool ConditionerFullyOutside()
        {
            Layout.ToLocal(Conditioner.X,Conditioner.Z,out _,out float outward);
            float relative=Conditioner.Heading-Layout.OutwardHeading;
            float envelope=Math.Abs((float)Math.Sin(relative))*Config.ConditionerWidth*.5f+Math.Abs((float)Math.Cos(relative))*Config.ConditionerLength*.5f;
            return outward-envelope>.25f;
        }

        void ConveySnow(float dt)
        {
            EscapingSnowRate = 0;DischargeSnowRate=0;VerticalSpillRate=0;
            ConditionerLeftEscapeRate=ConditionerRightEscapeRate=0;
            UpdateSnowExitPositions();
            float crowd=TankLift<.08f?Clamp((SnowTankM3/Config.SnowCapacityM3-.85f)/.15f,0,1):0;
            ConveyorCongestion=Math.Max(crowd,jamTime/Config.ConveyorJamSeconds);
            if (Controls.HorizontalAuger&&!ConveyorJammed)
            {
                float transported = Math.Min(HorizontalSnowM3, Math.Min(Config.HorizontalCapacityM3PerSecond * dt, Math.Max(0, Config.VerticalSnowCapacityM3 - VerticalSnowM3)));
                HorizontalSnowM3 -= transported; VerticalSnowM3 += transported;
            }
            float rejected=0;
            if (Controls.VerticalAuger && !ConveyorJammed)
            {
                float delivered=Math.Min(VerticalSnowM3,Config.VerticalCapacityM3PerSecond*dt*(1-crowd*.6f));
                // The elevated bin inlet no longer meets the fixed conveyor chute.
                // Keep the motor running; existing snow misses the bin physically.
                float transported = TankLift<.08f?Math.Min(delivered,Math.Max(0, Config.SnowCapacityM3 - SnowTankM3)):0;
                VerticalSnowM3 -= delivered; SnowTankM3 += transported; TotalSnowCollectedM3 += transported;
                DischargeSnowRate=transported/dt;
                rejected=delivered-transported;
                if(rejected>0)
                {
                    VerticalSpillRate=rejected/dt;
                    SpillAtDischarge(rejected);
                }
            }
            // A missed elevated bin is an open chute, not a mechanical blockage.
            // Only material rejected by the connected/full bin builds backpressure.
            jamTime=rejected>0&&TankLift<.08f?Math.Min(Config.ConveyorJamSeconds,jamTime+dt):Math.Max(0,jamTime-dt*.15f);
            if(jamTime>=Config.ConveyorJamSeconds-.0001f)ConveyorJammed=true;
            float spill = Math.Max(0, HorizontalSnowM3 - Config.ConditionerSnowCapacityM3);
            float verticalExcess=Math.Max(0, VerticalSnowM3-Config.VerticalSnowCapacityM3);
            HorizontalSnowM3 = Math.Min(HorizontalSnowM3, Config.ConditionerSnowCapacityM3);
            VerticalSnowM3 = Math.Min(VerticalSnowM3, Config.VerticalSnowCapacityM3);
            if (spill > 0)
            {
                SpilledSnowM3+=spill;
                ConditionerLeftEscapeRate=ConditionerRightEscapeRate=spill*.5f/dt;
                DepositEscapedSnow(ConditionerLeftLandingX,ConditionerLeftLandingZ,spill*.5f,.16f);
                DepositEscapedSnow(ConditionerRightLandingX,ConditionerRightLandingZ,spill*.5f,.16f);
            }
            if(verticalExcess>0){SpillAtDischarge(verticalExcess);VerticalSpillRate+=verticalExcess/dt;}
            EscapingSnowRate=(spill+rejected+verticalExcess)/dt;
            bool blockedThroat=VerticalSnowM3>=Config.VerticalSnowCapacityM3*.95f&&HorizontalSnowM3>0;
            ConveyorCongestion=Math.Max(Math.Max(crowd,jamTime/Config.ConveyorJamSeconds),blockedThroat?1:0);
            SnowCondition=SnowTransportCondition.None;
            if(!Controls.HorizontalAuger)SnowCondition|=SnowTransportCondition.HorizontalStopped;
            if(!Controls.VerticalAuger)SnowCondition|=SnowTransportCondition.VerticalStopped;
            if(ConveyorCongestion>.01f)SnowCondition|=SnowTransportCondition.Congested;
            if(SnowTankM3>=Config.SnowCapacityM3*.98f)SnowCondition|=SnowTransportCondition.TankFull;
            if(ConveyorJammed)SnowCondition|=SnowTransportCondition.Jammed;
            if(TankLift>=.08f)SnowCondition|=SnowTransportCondition.RaisedBin;
            if(EscapingSnowRate>0)Fault(11,TankLift>=.08f&&rejected>0?"Raised bin misses the upper conveyor outlet: snow is falling beside the machine.":
                !Controls.HorizontalAuger?"Horizontal auger OFF: full conditioner trough is shedding snow from both sides.":
                !Controls.VerticalAuger?"Vertical auger OFF: the transfer throat is full; snow is backing up to the conditioner side exits.":
                ConveyorJammed?"Jammed conveyors: incoming snow is spilling from the conditioner side exits.":
                "Snow transport is congested. Reduce incoming snow or empty the tank.");
            if(ConveyorJammed)Fault(14,"AUGER JAM: stop, retract blade and switch BOTH augers OFF. Empty tank, then use J to reset after inspection.");
            if (SnowTankM3 >= Config.SnowCapacityM3 * .98f) Fault(12, "Snow tank full. Stop cutting and return to the dump bay.");
        }

        void UpdateSnowExitPositions()
        {
            float sn=(float)Math.Sin(Conditioner.Heading),cs=(float)Math.Cos(Conditioner.Heading);
            float side=Math.Max(Config.ConditionerWidth,Config.WaterDistributionWidth)*.5f+.015f,fore=-.20f;
            ConditionerLeftExitX=Conditioner.X-cs*side+sn*fore;ConditionerLeftExitZ=Conditioner.Z+sn*side+cs*fore;
            ConditionerRightExitX=Conditioner.X+cs*side+sn*fore;ConditionerRightExitZ=Conditioner.Z-sn*side+cs*fore;
            float lx=ConditionerLeftExitX-cs*.20f,lz=ConditionerLeftExitZ+sn*.20f;
            float rx=ConditionerRightExitX+cs*.20f,rz=ConditionerRightExitZ-sn*.20f;
            ConstrainSnowAtBoards(ref lx,ref lz);ConstrainSnowAtBoards(ref rx,ref rz);
            ConditionerLeftLandingX=lx;ConditionerLeftLandingZ=lz;ConditionerRightLandingX=rx;ConditionerRightLandingZ=rz;
        }

        void ConstrainSnowAtBoards(ref float x,ref float z)
        {
            if(!Ice.IsInside(Conditioner.X,Conditioner.Z)||Layout.IsPortal(x,z))return;
            float distance=Ice.BoardDistance(x,z,out float nx,out float nz);
            if(distance>-.17f){x+=nx*(distance+.17f);z+=nz*(distance+.17f);}
        }

        void DepositEscapedSnow(float x,float z,float volume,float radius)
        {
            float deposited=Ice.DepositSnow(x,z,volume,radius);
            OffIceSnowM3+=volume-deposited;
            if(volume>deposited)Environment.Deposit(x,z,volume-deposited,0);
        }

        void SpillAtDischarge(float volume)
        {
            float sn=(float)Math.Sin(Vehicle.Heading),cs=(float)Math.Cos(Vehicle.Heading);
            SpilledSnowM3+=volume;
            // Compact fixed rear-tank inlet, shared with authored TankDischarge.
            // Escaped material deflects off the body
            // toward its nearest side; this is a bounded transfer trajectory,
            // not a particle collision solver or a second material source.
            UpperSnowOriginX=Vehicle.X+cs*Config.ConveyorOutletX+sn*Config.ConveyorOutletZ;UpperSnowOriginY=Config.ConveyorOutletY;
            UpperSnowOriginZ=Vehicle.Z-sn*Config.ConveyorOutletX+cs*Config.ConveyorOutletZ;UpperSnowVelocityY=-.25f;
            float x=Vehicle.X+cs*(Config.VehicleHalfWidth+.18f)+sn*Config.ConveyorOutletZ;
            float z=Vehicle.Z-sn*(Config.VehicleHalfWidth+.18f)+cs*Config.ConveyorOutletZ;
            if(Ice.IsInside(Vehicle.X,Vehicle.Z)&&!Layout.IsPortal(x,z))
            {
                float distance=Ice.BoardDistance(x,z,out float nx,out float nz);
                if(distance>-.26f){x+=nx*(distance+.26f);z+=nz*(distance+.26f);}
            }
            float flight=FallTime(UpperSnowOriginY,UpperSnowVelocityY);
            UpperSnowVelocityX=(x-UpperSnowOriginX)/flight;UpperSnowVelocityZ=(z-UpperSnowOriginZ)/flight;
            DepositEscapedSnow(x,z,volume,.25f);
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
            bool mayRaise = !GuidedTraining || (safe && Math.Abs(Controls.Throttle) < .01f);
            float target = Controls.HydraulicHoldControls
                ? Clamp(TankLift + Controls.TankHydraulic * dt / Config.TankLiftSeconds, 0, 1)
                : Controls.TankLiftTarget;
            if (target > TankLift && !mayRaise)
            {
                LastMessage = "Training assistance: raise over the pit while stopped, controls neutral, equipment stowed and hose clear. Free Simulation removes this assistance.";
                target = TankLift;
            }
            // A rejected request must never remain a latent movement interlock.
            Controls.TankLiftTarget = target;
            float previousLift = TankLift;
            float nextLift=MoveTowards(TankLift,target,dt/Config.TankLiftSeconds);
            if(!ProbeTankPose(Vehicle.X,Vehicle.Z,Vehicle.Heading,nextLift,out MovementContact tankContact))
            {
                LastMovementContact=tankContact;LastMovementContactTime=Time;
                LastMessage=tankContact.Reason+". Reverse clear before continuing the lift.";
                Controls.TankLiftTarget=TankLift;
            }
            else TankLift=nextLift;
            if (dumping && TankLift > .72f)
            {
                if (TankLift < previousLift)
                {
                    shakeLow = Math.Min(shakeLow, TankLift);
                    if (shakePeak - shakeLow >= .035f) shakeArmed = true;
                }
                if (shakeArmed && TankLift - shakeLow >= .025f && TankLift > previousLift && (!GuidedTraining||safe))
                {
                    ResidualSnowM3 *= .28f;
                    if (ResidualSnowM3 < .0001f) ResidualSnowM3 = 0;
                    TankShakeoutCount++;
                    shakeArmed = false; shakePeak = TankLift; shakeLow = TankLift;
                }
                if (!shakeArmed && TankLift > shakePeak) shakePeak = shakeLow = TankLift;
            }
            ComputeDumpTrajectory(out float ox,out float oy,out float oz,out float vx,out float vy,out float vz);
            DumpOriginX=ox;DumpOriginY=oy;DumpOriginZ=oz;DumpVelocityX=vx;DumpVelocityY=vy;DumpVelocityZ=vz;
            float fall=FallTime(oy,vy);DumpLandingX=ox+vx*fall;DumpLandingZ=oz+vz*fall;
            if (TankLift > .8f && (!GuidedTraining||safe) && SnowTankM3 > 0)
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
                if(amount>0)DepositDump(amount);
                LastMessage = amount > 0 ? (Layout.ContainsPit(DumpLandingX,DumpLandingZ)?"Snow falling into the pit under the actual outlet.":"Snow dumping onto the ground under the outlet. It remains in the environment.")
                    : "Residual snow in bin. Briefly lower a few degrees, then raise to dislodge it. Repeat as needed.";
            }
            if (TankLift < .1f) { dumping = false; shakeArmed = false; }
        }

        void ComputeDumpTrajectory(out float x,out float y,out float z,out float vx,out float vy,out float vz)
        {
            // Same hinge/outlet coordinates as the authored tank, with gravity
            // and vehicle momentum. No attraction or snapping toward the pit.
            float angle=TankLift*58f*(float)Math.PI/180f;
            Config.TankPointToMachine(Config.TankLateralOffset,.16f,.12f,angle,out float lateral,out y,out float localZ);
            float sn=(float)Math.Sin(Vehicle.Heading),cs=(float)Math.Cos(Vehicle.Heading);
            x=Vehicle.X+cs*lateral+sn*localZ;z=Vehicle.Z-sn*lateral+cs*localZ;
            float forward=Vehicle.Speed+Config.DumpExitSpeed;
            vx=sn*forward+cs*LateralVelocity;vz=cs*forward-sn*LateralVelocity;vy=-.18f;
        }
        static float FallTime(float height,float verticalSpeed)
        {return (verticalSpeed+(float)Math.Sqrt(verticalSpeed*verticalSpeed+2*9.81f*Math.Max(.01f,height)))/9.81f;}
        void DepositDump(float amount)
        {
            float sn=(float)Math.Sin(Vehicle.Heading),cs=(float)Math.Cos(Vehicle.Heading);
            for(int sample=0;sample<9;sample++)
            {
                float lateral=(sample-4)*.15f;
                float x=DumpLandingX+cs*lateral,z=DumpLandingZ-sn*lateral,volume=amount/9f;
                if(Layout.ContainsPit(x,z)){PitSnowM3+=volume;continue;}
                float onIce=Ice.IsInside(x,z)?Ice.DepositSnow(x,z,volume,.22f):0;
                DumpSnowOnIceM3+=onIce;
                float ground=volume-onIce;
                OffIceSnowM3+=ground;
                if(ground>0)Environment.Deposit(x,z,ground,0);
            }
            if(!Layout.ContainsPit(DumpLandingX,DumpLandingZ))Fault(17,"Snow deposited outside the pit. The material remains on the ice or floor and may be recollected.");
        }

        /// <summary>
        /// Manual assistance at the existing horizontal-to-vertical conveyor throat.
        /// Conserves snow and respects free capacity; it does not empty or enlarge a tank.
        /// Stroke volume is a gameplay calibration rather than a manufacturer rating.
        /// </summary>
        public bool StrokeSnowBreaker()
        {
            if (Finished || ConveyorJammed || (GuidedTraining&&(TankRaised || Conditioner.Lift > .15f || !Controls.VerticalAuger)))
            {
                LastMessage = Finished?"Session finished. Start a new session to operate machinery.":ConveyorJammed?"Jam: stop BOTH augers before inspection/reset. Do not use the running snow breaker to clear a physical jam.":"Training assistance: lower the conditioner and tank, and run the vertical auger before the snow breaker.";
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
            FloodWaterLiters = Config.FloodCapacityLiters; WashWaterLiters = Config.WashCapacityLiters; PumpDamage = 0; WashPrimeSeconds = SurfacePickupPrimeSeconds = 0;
            LastMessage = "Water tanks refilled; wash pickup inspected. Ready for another pass.";
            return true;
        }

        public bool TryFinish()
        {
            if(!GuidedTraining)
            {
                Finished=true;
                LastMessage=Practice?"Free Simulation session complete; review actual surface conditions.":"Scored Free Simulation complete; condition and consequences determine the result.";
                return true;
            }
            if (ServiceDriveInhibited || SupplyValve > .001f)
            { LastMessage = "Training assistance: close the wall supply, clear the hose and return to the controls before finishing."; return false; }
            if (!HasEnteredRink) { LastMessage = "Training assistance: enter the rink and resurface before completing the run."; return false; }
            if (!ProperExit || Math.Abs(Vehicle.Speed) > .035f || TankRaised)
            { LastMessage = "Training assistance: finishing requires a proper raised exit, stopped in the bay, equipment off and tank lowered."; return false; }
            if (SnowTankM3 > .005f || HorizontalSnowM3 > .001f || VerticalSnowM3 > .001f)
            { LastMessage = "Training assistance: clear conveyor snow and empty the tank before finishing."; return false; }
            Finished = true;
            LastMessage = Practice ? "Practice complete. Review the surface map; no score recorded." : "Run complete. Surface coverage and finish are measured from the actual ice.";
            return true;
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
