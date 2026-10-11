using System;
using System.Numerics;
namespace Meridian
{

// A test pilot may only issue the same bounded stick, throttle and trigger commands as a person.
public sealed class ValidationPilot
{
    private readonly PilotAI safety;
    private Vector3 previousTargetVelocity;
    private float pitchIntegral;
    private bool recovering;
    public ValidationPilot(int seed) { safety = new PilotAI(seed); }
    public FlightInput Update(AirMission mission)
    {
        AircraftState self = mission.Player, target = null;
        float nearest = float.MaxValue;
        foreach (AircraftState plane in mission.Aircraft)
        {
            if (plane.Team == self.Team || !plane.Alive) continue;
            float dist = Vector3.DistanceSquared(self.Position, plane.Position);
            if (dist < nearest) { nearest = dist; target = plane; }
        }
        float ground = mission.Terrain.Height(self.Position.X + self.Velocity.X * 4f, self.Position.Z + self.Velocity.Z * 4f);
        float clearance = self.Position.Y - ground;
        if (mission.Kind == MissionKind.ConvoyCover && clearance + Math.Min(0f, self.Velocity.Y) * 5f < 430f) recovering = true;
        if (recovering && clearance > 650f && self.Velocity.Y > 0f) recovering = false;
        if (recovering)
        {
            float bank = (float)Math.Atan2(-self.Right.Y, self.Up.Y);
            float pitchAngle = (float)Math.Asin(Math.Max(-1f, Math.Min(1f, self.Forward.Y)));
            return new FlightInput { Throttle = 1f, Roll = Clamp(-bank * 1.7f + self.AngularVelocity.Z * .4f),
                Pitch = Clamp((.3f - pitchAngle) * 3f * (float)Math.Cos(bank) + self.AngularVelocity.X * .4f) };
        }
        if (target == null || self.Speed < 68f || (mission.Kind == MissionKind.Duel && self.Position.Y < mission.Terrain.Height(self.Position.X, self.Position.Z) + 360f))
            return safety.Update(self, mission.Aircraft, mission.Terrain, Vector3.Zero, 1f / 60f);
        float flightTime = (float)Math.Sqrt(nearest) / Weapons.MuzzleSpeed;
        Vector3 acceleration = (target.Velocity - previousTargetVelocity) * 60f;
        previousTargetVelocity = target.Velocity;
        if (acceleration.LengthSquared() > 60f * 60f) acceleration = Vector3.Zero;
        Vector3 direction = Vector3.Normalize(Weapons.ComputeLead(self, target) + acceleration * (.5f * flightTime * flightTime) - self.Position);
        Vector3 local = Vector3.Transform(direction, Quaternion.Conjugate(self.Rotation));
        float bankError = (float)Math.Atan2(local.X, local.Y);
        float pitchError = (float)Math.Atan2(local.Y, local.Z);
        float roll = Clamp(bankError * .85f + self.AngularVelocity.Z * .4f);
        float pitch = Clamp(pitchError * 2.7f + self.AngularVelocity.X * .5f);
        if (local.Z < 0f) pitch = .85f;
        if (local.Z > .94f)
        {
            float currentBank = (float)Math.Atan2(-self.Right.Y, self.Up.Y);
            float targetBank = Vector3.Dot(self.Forward, target.Forward) > .5f ? (float)Math.Atan2(-target.Right.Y, target.Up.Y) : 0f;
            roll = Clamp((targetBank - currentBank) * 1.3f + self.AngularVelocity.Z * .4f);
            pitchIntegral = Clamp(pitchIntegral + local.Y / 60f * 4f);
            pitch = Clamp(local.Y * 11f + self.AngularVelocity.X * .3f + pitchIntegral);
        }
        else pitchIntegral = 0f;
        return new FlightInput { Roll = roll, Pitch = pitch, Yaw = Clamp(local.X * 1.5f),
            Throttle = self.Speed < 112f || (mission.Kind == MissionKind.ConvoyCover && nearest > 750f * 750f && local.Z > .6f) ? 1f : self.Speed > 137f ? .2f : .6f,
            Fire = local.Z > .9995f && nearest < 800f * 800f };
    }
    private static float Clamp(float x) { return Math.Max(-1f, Math.Min(1f, x)); }
}

}
