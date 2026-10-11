using System;
using System.Numerics;

namespace Meridian
{
    /// <summary>
    /// A force-based point-mass airframe with damped body rates. Gravity, angle-of-attack lift,
    /// induced drag, propeller power and weathercock stability all act on retained world velocity.
    /// Automatic longitudinal trim assists keyboard flying; it does not level the wings or hold altitude.
    /// </summary>
    public static class FlightDynamics
    {
        public const float Gravity = 9.81f;
        public const float FixedStep = 1f / 60f;

        public static void Step(AircraftState state, FlightInput input, TerrainField terrain, float dt)
        { Step(state, input, dt, terrain); }

        public static void Step(AircraftState s, FlightInput input, float dt, TerrainField terrain)
        {
            if (!s.Alive || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            // Bound explicit integration even for callers using a variable frame time.
            int steps = Math.Max(1, (int)Math.Ceiling(dt / FixedStep));
            float h = dt / steps;
            for (int i = 0; i < steps && s.Alive; ++i) Integrate(s, input, h, terrain);
        }

        private static void Integrate(AircraftState s, FlightInput input, float dt, TerrainField terrain)
        {
            AircraftDefinition d = s.Definition;
            float speed = s.Velocity.Length();
            Vector3 forward = s.Forward, right = s.Right, up = s.Up;
            Vector3 air = Vector3.Transform(s.Velocity, Quaternion.Conjugate(s.Rotation));
            float alpha = (float)Math.Atan2(-air.Y, Math.Max(1, air.Z));
            float beta = (float)Math.Atan2(air.X, Math.Max(10, air.Z));
            float rho = 1.225f * (float)Math.Exp(-Math.Max(0, s.Position.Y) / 10000);
            float qS = .5f * rho * speed * speed * d.WingArea;
            float stall = Clamp((Math.Abs(alpha) - d.StallAngle) / .30f, 0, 1);
            float lowSpeed = Clamp((speed - 18) / (d.StallSpeed - 18), .04f, 1);
            float integrity = .40f + .60f * Clamp(s.Health / 100, 0, 1);

            // Longitudinal trim accounts for the gravitational load along the wing's normal.
            // A bank still demands elevator: neutral controls gradually lose height in a turn.
            float requiredCl = d.Mass * Gravity * Math.Max(.12f, up.Y) / Math.Max(qS * integrity, 1);
            float trimAlpha = Clamp((requiredCl - d.LiftAtZero) / d.LiftSlope, -.05f, .13f);
            float controlAuthority = lowSpeed * (1 - .66f * stall) * integrity;
            float compressibility = Clamp(1 - Math.Max(0, speed - d.MaxSpeed * .92f) / (d.MaxSpeed * .5f), .30f, 1);
            float pitch = Clamp(input.Pitch, -1, 1) * d.MaxPitchRate * controlAuthority * compressibility;
            float pitchStability = Clamp((alpha - trimAlpha) * 1.7f, -.43f, .43f);
            // If stalled, the aerodynamic nose-down moment remains when the pilot releases the stick.
            float targetX = -pitch + pitchStability;
            float targetY = Clamp(input.Yaw, -1, 1) * d.MaxYawRate * controlAuthority + Clamp(beta * 1.65f, -.6f, .6f);
            float targetZ = -Clamp(input.Roll, -1, 1) * d.MaxRollRate * controlAuthority * compressibility;
            Vector3 targetRates = new Vector3(targetX, targetY, targetZ);
            s.AngularVelocity += (targetRates - s.AngularVelocity) * (1 - (float)Math.Exp(-dt * 5));
            float rate = s.AngularVelocity.Length();
            if (rate > .00001f)
                s.Rotation = Quaternion.Normalize(s.Rotation * Quaternion.CreateFromAxisAngle(s.AngularVelocity / rate, rate * dt));

            forward = s.Forward; right = s.Right; up = s.Up;
            float cl = d.LiftAtZero + d.LiftSlope * alpha;
            cl = Clamp(cl, -1.35f, 1.58f) * (1 - .80f * stall);
            if (air.Z < 0) cl *= .1f;
            Vector3 liftDirection = Vector3.Cross(s.Velocity, right);
            if (liftDirection.LengthSquared() < .01f) liftDirection = up;
            else liftDirection = Vector3.Normalize(liftDirection);
            Vector3 vDirection = speed > .01f ? s.Velocity / speed : forward;
            float lift = qS * cl * integrity;
            float dragCoefficient = d.BaseDrag + d.InducedDrag * cl * cl + .85f * stall + .32f * beta * beta;
            float drag = qS * dragCoefficient;
            s.Throttle += (Clamp(input.Throttle, 0, 1) - s.Throttle) * Math.Min(1, dt * 1.6f);
            float thrust = Math.Min(d.MaxThrust, d.MaxPower / Math.Max(40, speed)) * s.Throttle * integrity;
            Vector3 force = liftDirection * lift - vDirection * drag + forward * thrust;
            // Sideslip produces a restoring sideforce; this is aerodynamic damping, never a velocity snap.
            force -= right * (qS * .35f * beta);
            Vector3 acceleration = force / d.Mass - Vector3.UnitY * Gravity;
            s.Velocity += acceleration * dt;
            Vector3 previous = s.Position;
            s.Position += s.Velocity * dt;
            s.DistanceTravelled += (s.Position - previous).Length();
            s.AngleOfAttack = alpha;
            s.StallFactor = Math.Max(stall, 1 - lowSpeed);
            s.LoadFactor = lift / (d.Mass * Gravity);

            // A short grace interval permits recovery from a steep dive; sustained overspeed damages structure.
            if (speed > d.MaxSpeed) s.OverspeedTime += dt;
            else s.OverspeedTime = Math.Max(0, s.OverspeedTime - dt * 2);
            if (s.OverspeedTime > 2)
                s.Health -= dt * (speed / d.MaxSpeed - 1) * 38;
            if (Math.Abs(s.LoadFactor) > 8.5f)
                s.Health -= dt * (Math.Abs(s.LoadFactor) - 8.5f) * 2.5f;

            if (!Finite(s.Position) || !Finite(s.Velocity) || !Finite(s.Rotation))
            {
                s.Position = previous; s.Velocity = Vector3.Zero;
                Destroy(s, "Airframe simulation exceeded its operating envelope");
            }
            else if (s.Position.Y <= (terrain == null ? 0 : terrain.Height(s.Position.X, s.Position.Z)) + 2)
                Destroy(s, "Terrain impact");
            else if (s.Health <= 0) Destroy(s, "Airframe lost");
        }

        public static bool Finite(Vector3 value)
        { return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z); }
        public static bool Finite(Quaternion value)
        { return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z) && IsFinite(value.W); }
        private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static float Clamp(float v, float lo, float hi) { return Math.Max(lo, Math.Min(hi, v)); }
        private static void Destroy(AircraftState s, string cause)
        { s.Alive = false; s.Health = 0; s.DestructionCause = cause; }
    }
}
