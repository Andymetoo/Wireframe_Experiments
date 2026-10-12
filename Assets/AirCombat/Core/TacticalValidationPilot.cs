using System;
using System.Numerics;

namespace Meridian
{
    /// <summary>Controls-only expert diagnostic pilot. It issues legal wing orders and bounded flight inputs.
    /// This is a regression driver, not a runtime assist: it never writes position, damage, velocity or ammunition.</summary>
    public sealed class TacticalValidationPilot
    {
        private readonly PilotAI safety;
        private readonly PilotOrders safetyOrders = new PilotOrders();
        private Vector3 previousTargetVelocity;
        private float pitchIntegral;
        private bool recovering, gaveOrders;
        private int targetId = -1, orderedId = -1;

        public TacticalValidationPilot(int seed) { safety = new PilotAI(unchecked(seed + 947)); }

        public FlightInput Update(AirMission mission)
        {
            AircraftState self = mission.Player, target = null;
            if (mission.Kind == MissionKind.Interception)
            {
                foreach (AircraftState raid in mission.Raiders)
                    if (raid.Alive && (target == null || raid.Id == targetId)) target = raid;
                AircraftState wingTarget = null;
                foreach (AircraftState raid in mission.Raiders) if (raid.Alive && raid != target) { wingTarget = raid; break; }
                if (wingTarget == null) wingTarget = target;
                if (wingTarget != null && (!gaveOrders || orderedId != wingTarget.Id))
                { mission.IssueOrder(SquadronOrder.Engage, wingTarget.Id); gaveOrders = true; orderedId = wingTarget.Id; }
            }
            else
            {
                float nearest = float.MaxValue;
                foreach (AircraftState plane in mission.Aircraft)
                {
                    if (!plane.Alive || plane.Team == self.Team) continue;
                    float distance = Vector3.DistanceSquared(plane.Position, self.Position);
                    if (distance < nearest) { nearest = distance; target = plane; }
                }
                if (!gaveOrders) { mission.IssueOrder(SquadronOrder.Engage); gaveOrders = true; }
            }
            float ground = mission.Terrain.Height(self.Position.X + self.Velocity.X * 4f, self.Position.Z + self.Velocity.Z * 4f);
            float clearance = self.Position.Y - ground;
            if (clearance + Math.Min(0f, self.Velocity.Y) * 5f < 390f) recovering = true;
            if (recovering && clearance > 650f && self.Velocity.Y > 0f) recovering = false;
            if (recovering)
            {
                float bank = (float)Math.Atan2(-self.Right.Y, self.Up.Y);
                float pitchAngle = (float)Math.Asin(Clamp(self.Forward.Y));
                return new FlightInput { Throttle = 1f, Roll = Clamp(-bank * 1.7f + self.AngularVelocity.Z * .4f),
                    Pitch = Clamp((.3f - pitchAngle) * 3f * (float)Math.Cos(bank) + self.AngularVelocity.X * .4f) };
            }
            if (target == null || self.Speed < 68f)
            {
                safetyOrders.ObjectiveAnchor = mission.RouteStart;
                return safety.Update(self, mission.Aircraft, mission.Terrain, safetyOrders, 1f / 60f);
            }
            if (target.Id != targetId) { targetId = target.Id; previousTargetVelocity = target.Velocity; pitchIntegral = 0; }
            float squaredDistance = Vector3.DistanceSquared(target.Position, self.Position);
            float flightTime = (float)Math.Sqrt(squaredDistance) / Weapons.MuzzleSpeed;
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
                Throttle = self.Speed < 112f || squaredDistance > 750f * 750f && local.Z > .6f ? 1f : self.Speed > 147f ? .2f : .7f,
                Fire = local.Z > .9995f && squaredDistance < 800f * 800f };
        }

        private static float Clamp(float x) { return Math.Max(-1f, Math.Min(1f, x)); }
    }
}
