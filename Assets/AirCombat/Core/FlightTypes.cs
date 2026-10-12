using System;
using System.Numerics;

namespace Meridian
{
    public enum AircraftRole { Player, Fighter, Courier, Wingman, Bomber }

    /// <summary>SI units throughout. Shapes and performance are fictional period-inspired types.</summary>
    public sealed class AircraftDefinition
    {
        public string Name;
        public float Mass, WingArea, Wingspan, Length;
        public float MaxThrust, MaxPower, BaseDrag, InducedDrag;
        public float LiftAtZero, LiftSlope, StallAngle, StallSpeed;
        public float CruiseSpeed, MaxSpeed, MaxRollRate, MaxPitchRate, MaxYawRate;
        public int Ammunition;

        public static readonly AircraftDefinition AlliedFighter = new AircraftDefinition
        {
            Name = "Kestrel Mk.IX", Mass = 3100, WingArea = 22.5f, Wingspan = 11.2f, Length = 9.3f,
            MaxThrust = 17000, MaxPower = 1280000, BaseDrag = .027f, InducedDrag = .068f,
            LiftAtZero = .17f, LiftSlope = 4.8f, StallAngle = .29f, StallSpeed = 51,
            CruiseSpeed = 110, MaxSpeed = 192, MaxRollRate = 1.30f, MaxPitchRate = .48f,
            MaxYawRate = .25f, Ammunition = 900
        };

        public static readonly AircraftDefinition AxisFighter = new AircraftDefinition
        {
            Name = "Falke 109", Mass = 2950, WingArea = 17.5f, Wingspan = 9.9f, Length = 9.1f,
            MaxThrust = 17000, MaxPower = 1260000, BaseDrag = .027f, InducedDrag = .065f,
            LiftAtZero = .20f, LiftSlope = 5.0f, StallAngle = .28f, StallSpeed = 57,
            CruiseSpeed = 116, MaxSpeed = 200, MaxRollRate = 1.18f, MaxPitchRate = .43f,
            MaxYawRate = .23f, Ammunition = 700
        };

        public static readonly AircraftDefinition Courier = new AircraftDefinition
        {
            Name = "Heron C.47", Mass = 7500, WingArea = 91, Wingspan = 28, Length = 19.6f,
            MaxThrust = 28000, MaxPower = 1550000, BaseDrag = .033f, InducedDrag = .075f,
            LiftAtZero = .20f, LiftSlope = 4.8f, StallAngle = .30f, StallSpeed = 40,
            CruiseSpeed = 84, MaxSpeed = 143, MaxRollRate = .47f, MaxPitchRate = .21f,
            MaxYawRate = .13f, Ammunition = 0
        };

        public static readonly AircraftDefinition Bomber = new AircraftDefinition
        {
            Name = "Heron B.26", Mass = 8500, WingArea = 91, Wingspan = 28, Length = 19.6f,
            MaxThrust = 30000, MaxPower = 1850000, BaseDrag = .034f, InducedDrag = .075f,
            LiftAtZero = .20f, LiftSlope = 4.8f, StallAngle = .30f, StallSpeed = 43,
            CruiseSpeed = 92, MaxSpeed = 150, MaxRollRate = .43f, MaxPitchRate = .21f,
            MaxYawRate = .13f, Ammunition = 0
        };
    }

    public struct FlightInput
    {
        // Positive pitch raises the nose; positive roll banks right; positive yaw turns right.
        public float Pitch, Roll, Yaw, Throttle;
        public bool Fire;
    }

    public sealed class AircraftState
    {
        public int Id, Team;
        public string Callsign = "";
        public AircraftRole Role;
        public AircraftDefinition Definition = AircraftDefinition.AlliedFighter;
        public Vector3 Position, Velocity;
        public Quaternion Rotation = Quaternion.Identity;
        // Body-axis radians per second, using the quaternion's right-hand convention.
        public Vector3 AngularVelocity;
        public float Throttle = .7f, Health = 100;
        public int Ammo = 900;
        public float StallFactor, AngleOfAttack, LoadFactor, OverspeedTime;
        public float DistanceTravelled;
        public bool Alive = true;
        public string DestructionCause = "";
        public bool IsPlayer { get { return Role == AircraftRole.Player; } }
        public Vector3 Forward { get { return Vector3.Transform(Vector3.UnitZ, Rotation); } }
        public Vector3 Right { get { return Vector3.Transform(Vector3.UnitX, Rotation); } }
        public Vector3 Up { get { return Vector3.Transform(Vector3.UnitY, Rotation); } }
        public float Speed { get { return Velocity.Length(); } }
    }
}
