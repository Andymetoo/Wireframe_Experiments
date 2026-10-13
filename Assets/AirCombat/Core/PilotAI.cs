using System;
using System.Collections.Generic;
using System.Numerics;

namespace Meridian
{
    /// <summary>A delayed tactical pilot that commands the same aerodynamic model as the player.</summary>
    public sealed partial class PilotAI
    {
        private readonly Random random;
        private readonly float aggression;
        private float thinkTimer, maneuverTimer, elapsed, weave, desiredThrottle = .83f, pursuitTime;
        private Vector3 aimPoint, escapeDirection;
        private bool fireIntent;
        private int targetId = -1;
        public string State { get; private set; }
        public int ShotsRequested { get; private set; }
        public float MaximumBank { get; private set; }
        public PilotSkill Skill { get; private set; }

        public PilotAI(int seed, bool courier = false, PilotSkill skill = PilotSkill.Regular)
        {
            Skill = skill;
            random = new Random(seed);
            aggression = .75f + (float)random.NextDouble() * .25f;
            weave = (float)random.NextDouble() * 6.28f;
            pursuitTime = -(float)random.NextDouble() * 14f;
            State = courier ? "Outbound" : "Intercept";
        }

        public FlightInput Update(AircraftState self, IList<AircraftState> aircraft, TerrainField terrain,
            Vector3 courierDestination, float dt)
        {
            elapsed += dt; thinkTimer -= dt; maneuverTimer -= dt;
            pursuitTime += dt;
            if (thinkTimer <= 0f)
            {
                thinkTimer += .18f + (float)random.NextDouble() * .09f;
                Think(self, aircraft, terrain, courierDestination);
            }
            FlightInput result = FlyToward(self, aimPoint, desiredThrottle);
            result.Fire = fireIntent && self.Ammo > 0;
            if (result.Fire) ShotsRequested++;
            return result;
        }

        private void Think(AircraftState self, IList<AircraftState> aircraft, TerrainField terrain, Vector3 destination)
        {
            fireIntent = false;
            float speed = self.Velocity.Length();
            float agl = self.Position.Y - terrain.Height(self.Position.X, self.Position.Z);
            float terrainAhead = terrain.Height(self.Position.X + self.Velocity.X * 4f, self.Position.Z + self.Velocity.Z * 4f);
            if (agl < 220f || self.Position.Y < terrainAhead + 240f)
            {
                State = "Terrain escape";
                aimPoint = self.Position + Horizontal(self.Forward) * 1200f;
                aimPoint.Y = Math.Max(self.Position.Y + 500f, terrainAhead + 650f);
                desiredThrottle = 1f; return;
            }
            if (speed < self.Definition.StallSpeed * 1.25f)
            {
                State = "Recover airspeed";
                aimPoint = self.Position + Horizontal(self.Forward) * 1300f - Vector3.UnitY * Math.Min(250f, agl - 250f);
                desiredThrottle = 1f; return;
            }
            if (self.Role == AircraftRole.Courier)
            {
                State = "Outbound";
                aimPoint = destination;
                bool underAttack = false;
                for (int i = 0; i < aircraft.Count; i++)
                    if (aircraft[i].Alive && aircraft[i].Team != self.Team
                        && Vector3.DistanceSquared(aircraft[i].Position, self.Position) < 1000f * 1000f) underAttack = true;
                if (underAttack)
                {
                    Vector3 route = Horizontal(destination - self.Position);
                    Vector3 lateral = new Vector3(route.Z, 0f, -route.X);
                    aimPoint = self.Position + route * 2400f + lateral * ((float)Math.Sin(elapsed * .14f + weave) * 1000f);
                    State = "Evasive weave";
                }
                aimPoint.Y = Math.Max(820f, terrainAhead + 500f);
                desiredThrottle = .76f;
                return;
            }

            AircraftState target = SelectTarget(self, aircraft);
            if (target == null)
            {
                State = "Patrol";
                aimPoint = new Vector3((float)Math.Sin(elapsed * .012f) * 2200f, 1150f, (float)Math.Cos(elapsed * .012f) * 2200f);
                desiredThrottle = .8f; return;
            }
            Vector3 offset = target.Position - self.Position;
            float distance = offset.Length();
            Vector3 direction = offset / Math.Max(distance, 1f);
            float noseDot = Vector3.Dot(self.Forward, direction);
            float targetNoseDot = Vector3.Dot(target.Forward, -direction);
            bool threatened = targetNoseDot > .94f && noseDot < .4f && distance < 650f;

            if (maneuverTimer > 0f && (State == "Extend" || State == "Break"))
            {
                aimPoint = self.Position + escapeDirection * 1700f;
                aimPoint.Y = Math.Max(650f, aimPoint.Y);
                desiredThrottle = 1f;
                return;
            }
            if ((distance < 140f && noseDot > .35f) || (threatened && random.NextDouble() < .32f)
                || (pursuitTime > 20f + aggression * 9f && distance < 3000f && noseDot < .4f))
            {
                bool defensive = threatened;
                State = defensive ? "Break" : "Extend";
                float side = random.Next(2) == 0 ? -1f : 1f;
                escapeDirection = Vector3.Normalize(Horizontal(self.Forward) + Horizontal(self.Right) * side * (defensive ? 1.7f : .5f)
                    + Vector3.UnitY * (speed > 105f ? .22f : -.08f));
                maneuverTimer = defensive ? 5.5f : 8f;
                pursuitTime = 0f;
                aimPoint = self.Position + escapeDirection * 1700f;
                desiredThrottle = 1f; return;
            }

            State = distance > 1100f ? "Intercept" : "Attack";
            Vector3 lead = Weapons.ComputeLead(self, target);
            float lag = distance > 850f ? Math.Min(3f, distance / 350f) : 0f;
            aimPoint = distance > 850f ? target.Position + target.Velocity * lag : lead;
            // A poor-energy aircraft avoids pulling straight up after a climbing opponent.
            float climbLimit = speed > 120f ? .36f : .15f;
            aimPoint.Y = Math.Max(self.Position.Y - distance * .35f,
                Math.Min(aimPoint.Y, self.Position.Y + distance * climbLimit));
            aimPoint.Y = Math.Max(560f, aimPoint.Y);
            float wobble = (float)Math.Sin(elapsed * .8f + weave);
            aimPoint += self.Right * (wobble * (distance < 700f ? 1.5f : 8f));
            float desiredSpeed = Math.Max(self.Definition.StallSpeed * 1.55f,
                Math.Min(166f, target.Velocity.Length() + (distance - 220f) * .032f));
            // A hard turning fight calls for corner speed, not a full-power high-radius chase.
            if (noseDot < .45f && distance < 2300f)
                desiredSpeed = Math.Min(desiredSpeed, 108f + distance * .008f);
            float density = 1.225f * (float)Math.Exp(-Math.Max(0f, self.Position.Y) / 10000f);
            float targetQS = .5f * density * desiredSpeed * desiredSpeed * self.Definition.WingArea;
            float targetCL = self.Definition.Mass * 9.81f / Math.Max(targetQS, 1f);
            float trimThrottle = targetQS * (self.Definition.BaseDrag + self.Definition.InducedDrag * targetCL * targetCL)
                * desiredSpeed / self.Definition.MaxPower;
            desiredThrottle = Clamp(trimThrottle + (desiredSpeed - speed) * .026f, .03f, 1f);
            if (self.Health < 40f) desiredThrottle = 1f;
            Vector3 gunAim = Vector3.Normalize(lead - self.Position);
            float aimDot = Vector3.Dot(self.Forward, gunAim);
            // Finite burst windows and small aim errors let opponents miss and overshoot.
            bool burst = (elapsed + weave) % 2.5f < 1.5f * aggression;
            fireIntent = distance < 1000f && distance > 45f && aimDot > .9982f && burst;
        }

        private AircraftState SelectTarget(AircraftState self, IList<AircraftState> aircraft)
        {
            AircraftState best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < aircraft.Count; i++)
            {
                AircraftState other = aircraft[i];
                if (!other.Alive || other.Team == self.Team) continue;
                float score = Vector3.Distance(self.Position, other.Position);
                if (other.Role == AircraftRole.Courier) score *= .72f;
                if (other.Id == targetId) score *= .78f;
                // A close fighter pointing at us takes priority over the objective.
                Vector3 toSelf = self.Position - other.Position;
                if (other.IsPlayer && toSelf.LengthSquared() < 700f * 700f
                    && Vector3.Dot(other.Forward, Vector3.Normalize(toSelf)) > .85f) score *= .35f;
                if (score < bestScore) { bestScore = score; best = other; }
            }
            targetId = best == null ? -1 : best.Id;
            return best;
        }

        private FlightInput FlyToward(AircraftState self, Vector3 point, float throttle)
        {
            Vector3 d = point - self.Position;
            float horizontalDistance = (float)Math.Sqrt(d.X * d.X + d.Z * d.Z);
            float heading = (float)Math.Atan2(self.Forward.X, self.Forward.Z);
            float targetHeading = (float)Math.Atan2(d.X, d.Z);
            float headingError = Wrap(targetHeading - heading);
            float bank = (float)Math.Atan2(-self.Right.Y, self.Up.Y);
            float bankLimit = self.Role == AircraftRole.Courier || self.Role == AircraftRole.Bomber ? .48f : 1.22f;
            if (State == "Formation" || State == "Rejoin") bankLimit = .82f;
            if (State == "Strafe") bankLimit = .65f;
            if (State == "Terrain escape" || State == "Recover airspeed") bankLimit = .24f;
            float desiredBank = Clamp(headingError * 1.45f, -bankLimit, bankLimit);
            float desiredPitch = (float)Math.Atan2(d.Y, Math.Max(horizontalDistance, 1f));
            desiredPitch = Clamp(desiredPitch, State == "Strafe" ? -.40f : -.30f, State == "Terrain escape" || State == "Pull up" ? .45f : .34f);
            float pitch = (float)Math.Asin(Clamp(self.Forward.Y, -1f, 1f));
            float pitchError = desiredPitch - pitch;
            float roll = Clamp(Wrap(desiredBank - bank) * 1.7f + self.AngularVelocity.Z * .50f, -1f, 1f);
            float cosBank = Math.Max(.35f, (float)Math.Cos(bank));
            float turnCompensation = 9.81f * Math.Abs((float)Math.Tan(bank) * (float)Math.Sin(bank))
                / Math.Max(self.Speed * self.Definition.MaxPitchRate, 10f);
            float rho = 1.225f * (float)Math.Exp(-Math.Max(0f, self.Position.Y) / 10000f);
            float qS = .5f * rho * self.Speed * self.Speed * self.Definition.WingArea;
            float clOneG = self.Definition.Mass * 9.81f / Math.Max(qS, 1f);
            float extraAlpha = clOneG * (1f / cosBank - cosBank) / self.Definition.LiftSlope;
            float trimCompensation = extraAlpha * 1.7f / self.Definition.MaxPitchRate;
            float pull = Clamp(pitchError * 4.2f / cosBank + self.AngularVelocity.X * .45f + turnCompensation + trimCompensation, -1f, 1f);
            MaximumBank = Math.Max(MaximumBank, Math.Abs(bank));
            return new FlightInput { Pitch = pull, Roll = roll, Yaw = Math.Abs(bank) < .35f ? Clamp(headingError * .5f, -.20f, .20f) : 0f, Throttle = throttle };
        }

        private static Vector3 Horizontal(Vector3 value)
        {
            value.Y = 0f;
            return value.LengthSquared() > .0001f ? Vector3.Normalize(value) : Vector3.UnitZ;
        }
        private static float Wrap(float angle)
        { while (angle > Math.PI) angle -= (float)Math.PI * 2f; while (angle < -Math.PI) angle += (float)Math.PI * 2f; return angle; }
        private static float Clamp(float x, float min, float max) { return Math.Max(min, Math.Min(max, x)); }
    }
}
