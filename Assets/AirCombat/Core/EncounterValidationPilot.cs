using System;
using System.Numerics;

namespace Meridian
{
    /// <summary>Opt-in regression driver for larger fights. Keeps a selected target instead of
    /// chasing a different nearest contact every frame. Only writes legal orders and bounded controls;
    /// never edits aircraft state, projectiles, ammunition, objectives, equipment or damage.</summary>
    public sealed class EncounterValidationPilot
    {
        readonly PilotAI safety;
        readonly PilotOrders safetyOrders = new PilotOrders();
        AircraftState target;
        Vector3 previousVelocity;
        float pitchIntegral;
        bool recovering;
        int orderedId = -1;
        public int TargetId { get { return target == null ? -1 : target.Id; } }
        public int TargetChanges { get; private set; }

        public EncounterValidationPilot(int seed) { safety = new PilotAI(unchecked(seed + 947)); }

        public FlightInput Update(AirMission mission)
        {
            AircraftState self = mission.Player;
            AircraftState selected = SelectTarget(mission);
            if (selected != target)
            {
                target = selected; pitchIntegral = 0;
                if (target != null) { previousVelocity = target.Velocity; TargetChanges++; }
            }
            if (target != null && target.Id != orderedId)
            {
                // Split a raid rather than spending the whole flight on the same fleeing leader.
                AircraftState wingTarget = target;
                if (mission.Kind == MissionKind.Interception)
                    foreach (var raid in mission.Raiders) if (raid.Alive && raid.Id != target.Id) { wingTarget = raid; break; }
                mission.IssueOrder(SquadronOrder.Engage, wingTarget.Id); orderedId = target.Id;
            }
            float ground = mission.Terrain.Height(self.Position.X + self.Velocity.X * 4, self.Position.Z + self.Velocity.Z * 4);
            float clearance = self.Position.Y - ground;
            if (clearance + Math.Min(0f, self.Velocity.Y) * 5f < 390f) recovering = true;
            if (recovering && clearance > 650 && self.Velocity.Y > 0) recovering = false;
            if (recovering)
            {
                float bank = (float)Math.Atan2(-self.Right.Y, self.Up.Y);
                float pitchAngle = (float)Math.Asin(Clamp(self.Forward.Y));
                return new FlightInput { Throttle = 1, Roll = Clamp(-bank * 1.7f + self.AngularVelocity.Z * .4f),
                    Pitch = Clamp((.3f - pitchAngle) * 3f * (float)Math.Cos(bank) + self.AngularVelocity.X * .4f) };
            }
            if (target == null || self.Speed < 68)
            {
                safetyOrders.ObjectiveAnchor = mission.ObjectivePosition;
                safetyOrders.DesignatedTargetId = target == null ? -1 : target.Id;
                return safety.Update(self, mission.Aircraft, mission.Terrain, safetyOrders, 1f / 60f);
            }
            float distanceSquared = Vector3.DistanceSquared(target.Position, self.Position);
            float flightTime = (float)Math.Sqrt(distanceSquared) / (Weapons.MuzzleSpeed * Math.Max(.25f, self.MuzzleSpeedMultiplier));
            Vector3 acceleration = (target.Velocity - previousVelocity) * 60;
            previousVelocity = target.Velocity;
            if (acceleration.LengthSquared() > 60 * 60) acceleration = Vector3.Zero;
            Vector3 offset = Weapons.ComputeLead(self, target) + acceleration * (.5f * flightTime * flightTime) - self.Position;
            if (offset.LengthSquared() < 1) return new FlightInput { Throttle = .8f };
            Vector3 local = Vector3.Transform(Vector3.Normalize(offset), Quaternion.Conjugate(self.Rotation));
            float bankError = (float)Math.Atan2(local.X, local.Y);
            float pitchError = (float)Math.Atan2(local.Y, local.Z);
            float roll = Clamp(bankError * .85f + self.AngularVelocity.Z * .4f);
            float pitch = Clamp(pitchError * 2.7f + self.AngularVelocity.X * .5f);
            if (local.Z < 0) pitch = .85f;
            if (local.Z > .94f)
            {
                float currentBank = (float)Math.Atan2(-self.Right.Y, self.Up.Y);
                float targetBank = Vector3.Dot(self.Forward, target.Forward) > .5f ? (float)Math.Atan2(-target.Right.Y, target.Up.Y) : 0;
                roll = Clamp((targetBank - currentBank) * 1.3f + self.AngularVelocity.Z * .4f);
                pitchIntegral = Clamp(pitchIntegral + local.Y / 60f * 4);
                pitch = Clamp(local.Y * 11 + self.AngularVelocity.X * .3f + pitchIntegral);
            }
            else pitchIntegral = 0;
            return new FlightInput { Roll = roll, Pitch = pitch, Yaw = Clamp(local.X * 1.5f),
                Throttle = self.Speed < 112 || distanceSquared > 750 * 750 && local.Z > .6f ? 1 : self.Speed > 147 ? .2f : .7f,
                Fire = local.Z > .9995f && distanceSquared < 800 * 800 };
        }

        AircraftState SelectTarget(AirMission mission)
        {
            if (mission.Kind == MissionKind.AceHunt)
                foreach (var aircraft in mission.Aircraft) if (aircraft.Id == mission.AceId && aircraft.Alive) return aircraft;
            if (target != null && target.Alive && target.Team != mission.Player.Team
                && Vector3.DistanceSquared(target.Position, mission.Player.Position) < 10000f * 10000f) return target;
            if (mission.Kind == MissionKind.Interception)
                foreach (var raid in mission.Raiders) if (raid.Alive) return raid;
            AircraftState nearest = null; float best = float.MaxValue;
            foreach (var aircraft in mission.Aircraft)
            {
                if (!aircraft.Alive || aircraft.Team == mission.Player.Team) continue;
                float distance = Vector3.DistanceSquared(aircraft.Position, mission.Player.Position);
                if (distance < best) { best = distance; nearest = aircraft; }
            }
            return nearest;
        }

        static float Clamp(float value) { return Math.Max(-1f, Math.Min(1f, value)); }
    }
}
