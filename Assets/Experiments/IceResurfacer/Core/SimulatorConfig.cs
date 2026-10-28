using System;

namespace IceResurfacer.Core
{
    /// <summary>SI units unless a field explicitly names another unit. Dynamics are calibration values, not manufacturer guarantees.</summary>
    [Serializable]
    public sealed partial class SimulatorConfig
    {
        public float RinkWidth = 26f;
        public float RinkLength = 60f;
        public float CornerRadius = 8.5f;
        public float CellSize = 0.1f;
        public float GateHalfWidth = 2.7f;
        public float BayLength = 14f;
        public float Wheelbase = 1.96f;
        public float VehicleHalfWidth = 1.065f;
        public float BodyFront = 2.5f;
        public float BodyRear = 0.45f;
        public float ConditionerWidth = 1.956f;
        public float WaterDistributionWidth = 2.10f;
        public float GuideRollerX = -1.015f;
        public float GuideRollerZ = 2.38f;
        public float GuideRollerRadius = .09f;
        public float ConditionerLength = 0.8f;
        public float HitchBehindAxle = 0.45f;
        public float DrawbarLength = 0.65f;
        public float MaxSteeringRadians = 0.412f;
        public float SteeringRate = 0.55f;
        public float ForwardSpeed = 2.65f;
        public float ReverseSpeed = 1.0f;
        public float Acceleration = 0.48f;
        public float HydrostaticDeceleration = 0.35f;
        public float ServiceBrakeDeceleration = 1.45f;
        public float ConditionerLiftSeconds = 2.1f;
        public float TankLiftSeconds = 5.5f;
        public float FloodCapacityLiters = 738f;
        public float WashCapacityLiters = 310f;
        public float SnowCapacityM3 = 3.54f;
        public float FloodMaxLitersPerSecond = 1.8f;
        public float WashMaxLitersPerSecond = 1.05f;
        public float WashRecoveryFraction = 0.82f;
        public float HorizontalCapacityM3PerSecond = 0.012f;
        public float VerticalCapacityM3PerSecond = 0.010f;
        public float ConditionerSnowCapacityM3 = 0.08f;
        public float VerticalSnowCapacityM3 = 0.055f;
        public float SnowBreakerStrokeM3 = 0.004f;
        public float SnowExpansionRatio = 3.1f;
        public float DumpM3PerSecond = 0.35f;
        public float MaxBladeDepthMm = .8f;
        // Nominal screw setting is not a fully engaged rectangular bite. The
        // floating shoes/rake admit this fraction as actual shaved ice depth.
        // Prototype calibration: expose both quantities, never scale the tank UI.
        public float BladeBiteFraction = .16f;
        public float BladeWheelTurns = 3.5f;
        public float ConveyorJamSeconds = 6f;
        public float RecommendedBladeDepthMm = 0.35f;
        public float CumulativeSafeCutMm = 1.2f;
        public float SensitiveCumulativeSafeCutMm = 0.55f;
        public float GoodFloodDepthMm = 0.30f;
        public float ExcessFloodDepthMm = 0.85f;
        public float FreezeSeconds = 85f;
        public float MinimumScoredCoverage = 0.95f;

        public void Validate()
        {
            ValidateIceCalibration();
            ValidateFreedomCalibration();
            float[] values = { RinkWidth, RinkLength, CornerRadius, CellSize, GateHalfWidth, BayLength,
                Wheelbase, VehicleHalfWidth, BodyFront, BodyRear, ConditionerWidth, WaterDistributionWidth,
                GuideRollerX, GuideRollerZ, GuideRollerRadius, BladeBiteFraction, BladeWheelTurns, ConveyorJamSeconds, ConditionerLength,
                HitchBehindAxle, DrawbarLength, MaxSteeringRadians, SteeringRate, ForwardSpeed, ReverseSpeed,
                Acceleration, HydrostaticDeceleration, ServiceBrakeDeceleration, ConditionerLiftSeconds,
                TankLiftSeconds, FloodCapacityLiters, WashCapacityLiters, SnowCapacityM3, FloodMaxLitersPerSecond,
                WashMaxLitersPerSecond, WashRecoveryFraction, HorizontalCapacityM3PerSecond,
                VerticalCapacityM3PerSecond, ConditionerSnowCapacityM3, VerticalSnowCapacityM3, SnowBreakerStrokeM3,
                SnowExpansionRatio, DumpM3PerSecond, MaxBladeDepthMm, RecommendedBladeDepthMm,
                CumulativeSafeCutMm, SensitiveCumulativeSafeCutMm, GoodFloodDepthMm, ExcessFloodDepthMm,
                FreezeSeconds, MinimumScoredCoverage };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentException("Ice resurfacer configuration must contain finite numbers.");
            if (RinkWidth < 10 || RinkWidth > 120 || RinkLength < 20 || RinkLength > 120 || CellSize < 0.08f || CellSize > 0.5f ||
                CornerRadius < 0 || CornerRadius > Math.Min(RinkWidth, RinkLength) * .5f ||
                Wheelbase <= 0 || DrawbarLength <= 0 || ConditionerWidth <= 0 || WaterDistributionWidth <= 0 ||
                WaterDistributionWidth > VehicleHalfWidth*2 || ConditionerLength <= 0 || GuideRollerRadius <= 0 ||
                BladeBiteFraction <= 0 || BladeBiteFraction > 1 || BladeWheelTurns < 3 || BladeWheelTurns > 4 || ConveyorJamSeconds <= 0 ||
                FloodCapacityLiters <= 0 || WashCapacityLiters <= 0 || SnowCapacityM3 <= 0 ||
                ConditionerLiftSeconds <= 0 || TankLiftSeconds <= 0 || FreezeSeconds <= 0 ||
                GateHalfWidth <= VehicleHalfWidth || BayLength < 12f || BayLength <= BodyFront + BodyRear ||
                MaxSteeringRadians <= 0 || MaxSteeringRadians >= 1.2f || SteeringRate <= 0 ||
                ForwardSpeed <= 0 || ReverseSpeed <= 0 || Acceleration <= 0 || HydrostaticDeceleration <= 0 ||
                ServiceBrakeDeceleration <= 0 || MaxBladeDepthMm <= 0 || RecommendedBladeDepthMm <= 0 ||
                ConditionerSnowCapacityM3 <= 0 || VerticalSnowCapacityM3 <= 0 || SnowBreakerStrokeM3 <= 0 ||
                VehicleHalfWidth <= 0 || BodyFront <= 0 || BodyRear <= 0 || HitchBehindAxle < 0 ||
                FloodMaxLitersPerSecond <= 0 || WashMaxLitersPerSecond <= 0 || WashRecoveryFraction < 0 || WashRecoveryFraction > 1 ||
                HorizontalCapacityM3PerSecond <= 0 || VerticalCapacityM3PerSecond <= 0 || SnowExpansionRatio < 1 || DumpM3PerSecond <= 0 ||
                RecommendedBladeDepthMm < 0 || RecommendedBladeDepthMm > MaxBladeDepthMm ||
                CumulativeSafeCutMm <= 0 || SensitiveCumulativeSafeCutMm <= 0 ||
                GoodFloodDepthMm <= 0 || ExcessFloodDepthMm < GoodFloodDepthMm ||
                MinimumScoredCoverage <= 0 || MinimumScoredCoverage > 1)
                throw new ArgumentException("Ice resurfacer configuration has invalid geometry, dynamics, or capacity.");
        }
    }

    public sealed class OperatorControls
    {
        public float Throttle, Steer;
        public bool Brake;
        // Live controls use spring-centred hydraulic spool commands. Explicit
        // targets remain available for deterministic fixtures and calibration.
        public bool HydraulicHoldControls;
        public float ConditionerHydraulic, TankHydraulic;
        public float ConditionerTarget = 1f;
        public bool HorizontalAuger, VerticalAuger, BoardBrush;
        public float BladeDepthMm;
        public float WashValve;
        public bool WashPump;
        public float FloodFlow;
        public float TankLiftTarget;
    }

    public sealed class VehicleState
    {
        // Rear axle position; heading zero is +Z, positive heading turns toward +X.
        public float X, Z, Heading, Speed, SteeringAngle;
    }

    public sealed class ConditionerState
    {
        public float X, Z, Heading;
        public float Lift = 1f;
    }

    public struct RunReport
    {
        public float Coverage, FinishQuality, OverwaterFraction, MissedFraction, Score, ElapsedSeconds;
        public float ShavingCoverage, WaterCoverage, WaterDistribution, OperationalEfficiency;
        public float CoveragePoints, FinishPoints, WaterPoints, OverwaterPoints, EfficiencyPoints, HandlingPoints, MisusePenalty;
        public int Mistakes, TankDumps;
        public bool ProperExit, Complete, Practice;
    }
}
