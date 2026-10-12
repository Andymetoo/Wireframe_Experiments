using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Meridian
{
    /// <summary>Bounded tactical fixtures. Pilots must move through FlightDynamics; no aircraft are
    /// repositioned during formation, recovery or avoidance trials.</summary>
    public static class TacticalValidation
    {
        public static string Summary { get; private set; } = "Tactical handling not run";
        const float Tick = 1f / 60f;

        public static void Run(Action<bool, string> check)
        {
            var report = new StringBuilder("TACTICAL HANDLING (real aerodynamic integration)\n");
            ValidateControlBoundary(check);
            ValidateAwareness(check);
            ValidateOrdersAndGunnery(check);
            ValidateFormation(check, report, false, false);
            ValidateFormation(check, report, true, false);
            ValidateFormation(check, report, false, true);
            ValidateRecovery(check, report);
            ValidateTraffic(check, report);
            Summary = report.ToString();
        }

        static AircraftState Plane(int id, int team, Vector3 position, float speed = 112f)
        {
            return new AircraftState { Id = id, Team = team, Callsign = "Fixture " + id,
                Role = team == 0 ? AircraftRole.Wingman : AircraftRole.Fighter,
                Definition = AircraftDefinition.AlliedFighter, Position = position,
                Velocity = Vector3.UnitZ * speed, Rotation = Quaternion.Identity, Ammo = 900, Health = 100 };
        }

        static void ValidateControlBoundary(Action<bool, string> check)
        {
            var self = Plane(1, 0, new Vector3(0, 1400, 0));
            var target = Plane(2, 1, new Vector3(800, 1700, 1200));
            var planes = new List<AircraftState> { self, target };
            var terrain = new TerrainField(1944); var pilot = new PilotAI(91);
            Vector3 position = self.Position, velocity = self.Velocity, rates = self.AngularVelocity;
            Quaternion rotation = self.Rotation;
            bool bounded = true;
            var orders = new PilotOrders();
            for (int i = 0; i < 120; i++) bounded &= Bounded(pilot.Update(self, planes, terrain, orders, Tick));
            check(self.Position == position && self.Velocity == velocity && self.Rotation == rotation &&
                self.AngularVelocity == rates && self.Health == 100 && self.Ammo == 900,
                "tactical decisions never mutate aircraft position, velocity, attitude, health or ammunition");
            check(bounded, "tactical pilots issue finite bounded player-equivalent flight controls");
            for (int i = 0; i < 60 * 8; i++)
                FlightDynamics.Step(self, pilot.Update(self, planes, terrain, orders, Tick), Tick, terrain);
            check(self.Alive && self.Position.X > position.X + 40 && Math.Abs(self.Right.Y) > .12f,
                "offset interception banks and changes trajectory through aerodynamic flight");
        }

        static void ValidateAwareness(Action<bool, string> check)
        {
            var terrain = new TerrainField(71);
            var self = Plane(1, 0, new Vector3(0, 1500, 0));
            var enemy = Plane(2, 1, new Vector3(0, 1500, 8000));
            var planes = new List<AircraftState> { self, enemy };
            var pilot = new PilotAI(10); var orders = new PilotOrders { DesignatedTargetId = enemy.Id };
            pilot.Update(self, planes, terrain, orders, Tick);
            check(pilot.TargetId == -1, "designating an aircraft outside awareness range does not reveal it");
            enemy.Position = new Vector3(0, 1500, -2400);
            pilot = new PilotAI(10); pilot.Update(self, planes, terrain, orders, Tick);
            check(pilot.TargetId == -1, "distant aircraft behind the pilot remain outside the visual scan");
            enemy.Position = new Vector3(300, 1500, 2100);
            pilot = new PilotAI(10); pilot.Update(self, planes, terrain, orders, Tick);
            check(pilot.TargetId == enemy.Id, "a visible forward contact inside scan range can be designated");
            enemy.Alive = false;
            for (int i = 0; i < 40; i++) pilot.Update(self, planes, terrain, orders, Tick);
            check(pilot.TargetId == -1, "destroyed contacts are dropped despite previous target commitment");
            enemy.Alive = true; enemy.Team = 0;
            pilot = new PilotAI(10); pilot.Update(self, planes, terrain, orders, Tick);
            check(pilot.TargetId == -1, "friendly aircraft never become tactical gun targets");
        }

        static void ValidateOrdersAndGunnery(Action<bool, string> check)
        {
            var terrain = new TerrainField(1944);
            var self = Plane(1, 0, new Vector3(0, 1600, 0));
            var leader = Plane(0, 0, new Vector3(-170, 1565, 180)); leader.Role = AircraftRole.Player;
            var enemy = Plane(2, 1, new Vector3(0, 1600, 650));
            var planes = new List<AircraftState> { self, leader, enemy };
            var orders = new PilotOrders { Leader = leader, Order = SquadronOrder.Engage,
                FormationOffset = new Vector3(170, 35, -180), DesignatedTargetId = enemy.Id };
            var pilot = new PilotAI(12);
            bool fired = false;
            for (int i = 0; i < 180; i++) fired |= pilot.Update(self, planes, terrain, orders, Tick).Fire;
            check(pilot.TargetId == enemy.Id && fired, "engage order yields finite firing windows on a visible aligned target");
            orders.Order = SquadronOrder.Regroup; orders.Revision++;
            bool regroupFire = false;
            for (int i = 0; i < 60; i++) regroupFire |= pilot.Update(self, planes, terrain, orders, Tick).Fire;
            check(!regroupFire && pilot.TargetId == -1 && pilot.State == "Formation",
                "regroup revokes offensive firing and restores the assigned formation slot");

            orders.Order = SquadronOrder.Engage; orders.Revision++;
            leader.Position = new Vector3(0, 1600, 260);
            pilot = new PilotAI(12); bool screenedFire = false;
            for (int i = 0; i < 180; i++) screenedFire |= pilot.Update(self, planes, terrain, orders, Tick).Fire;
            check(!screenedFire, "a friendly aircraft crossing the gun line suppresses AI firing");

            leader.Position = new Vector3(0, 1600, 400);
            enemy.Position = new Vector3(0, 1600, 80);
            var distraction = Plane(3, 1, new Vector3(900, 1600, 300));
            distraction.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Math.PI);
            planes.Add(distraction); orders.Order = SquadronOrder.CoverMe; orders.DesignatedTargetId = -1; orders.Revision++;
            pilot = new PilotAI(12); pilot.Update(self, planes, terrain, orders, Tick);
            check(pilot.TargetId == enemy.Id, "cover order prioritizes an enemy aiming at the flight leader");

            self.Health = 20; orders.Order = SquadronOrder.Engage; orders.Revision++;
            pilot = new PilotAI(12);
            FlightInput damaged = pilot.Update(self, planes, terrain, orders, Tick);
            check(pilot.State == "Withdraw" && !damaged.Fire, "seriously damaged pilots withdraw instead of obeying suicidal pursuit");
        }

        static void ValidateFormation(Action<bool, string> check, StringBuilder report, bool displaced, bool turning)
        {
            var terrain = new TerrainField(1944);
            var leader = Plane(0, 0, new Vector3(0, 1500, 0)); leader.Role = AircraftRole.Player;
            Vector3 offset = new Vector3(-170, 35, -180);
            var follower = Plane(1, 0, leader.Position + offset + (displaced ? new Vector3(-1400, 110, -1800) : Vector3.Zero));
            var planes = new List<AircraftState> { leader, follower };
            var leadPilot = new PilotAI(19); var wingPilot = new PilotAI(71);
            var route = new PilotOrders { FollowRoute = true, Destination = new Vector3(0, 1500, 22000) };
            var wingOrder = new PilotOrders { Leader = leader, FormationOffset = offset, Order = SquadronOrder.FormUp };
            float initialError = Vector3.Distance(follower.Position, leader.Position + offset), maxError = 0, minSeparation = float.MaxValue;
            bool finite = true, bounded = true;
            int seconds = displaced ? 105 : turning ? 80 : 55;
            for (int tick = 0; tick < seconds * 60 && leader.Alive && follower.Alive; tick++)
            {
                if (turning && tick == 20 * 60) { route.Destination = new Vector3(12000, 1650, 22000); route.Revision++; }
                FlightInput a = leadPilot.Update(leader, planes, terrain, route, Tick);
                FlightInput b = wingPilot.Update(follower, planes, terrain, wingOrder, Tick);
                Vector3 before = follower.Position; Quaternion rotation = follower.Rotation;
                FlightDynamics.Step(leader, a, Tick, terrain); FlightDynamics.Step(follower, b, Tick, terrain);
                finite &= FlightDynamics.Finite(follower.Position) && FlightDynamics.Finite(follower.Velocity) && FlightDynamics.Finite(follower.Rotation);
                bounded &= Vector3.Distance(before, follower.Position) < 4.1f && Math.Abs(Quaternion.Dot(rotation, follower.Rotation)) > .999f;
                minSeparation = Math.Min(minSeparation, Vector3.Distance(leader.Position, follower.Position));
                if (tick > 20 * 60) maxError = Math.Max(maxError, wingPilot.FormationError);
            }
            string label = displaced ? "separated wingman rejoin" : turning ? "turning leader formation" : "straight leader formation";
            check(leader.Alive && follower.Alive && finite && bounded, label + " uses finite continuous flight without correction teleports");
            check(minSeparation > 60, label + " maintains safe physical spacing");
            if (displaced) check(wingPilot.FormationError < 700 && wingPilot.FormationError < initialError * .4f,
                "a displaced wingman closes a two-kilometre formation gap within 105 seconds");
            else check(wingPilot.FormationError < 500 && maxError < (turning ? 950 : 650), label + " retains a useful bounded slot after leader motion");
            report.Append(label).Append(" initial=").Append(initialError.ToString("F0")).Append(" final=")
                .Append(wingPilot.FormationError.ToString("F0")).Append(" peak=").Append(maxError.ToString("F0"))
                .Append(" closest=").Append(minSeparation.ToString("F0")).Append(" m\n");
        }

        static void ValidateRecovery(Action<bool, string> check, StringBuilder report)
        {
            var terrain = new TerrainField(1944);
            var self = Plane(1, 0, new Vector3(0, 1800, 0), 56);
            var planes = new List<AircraftState> { self }; var pilot = new PilotAI(83);
            var orders = new PilotOrders { FollowRoute = true, Destination = new Vector3(0, 1800, 9000) };
            FlightInput initial = pilot.Update(self, planes, terrain, orders, Tick);
            check(pilot.State == "Recover airspeed" && initial.Throttle > .95f, "slow pilots prioritize energy recovery over the assigned climb");
            for (int i = 0; i < 15 * 60 && self.Alive; i++)
                FlightDynamics.Step(self, pilot.Update(self, planes, terrain, orders, Tick), Tick, terrain);
            check(self.Alive && self.Speed > self.Definition.StallSpeed * 1.4f && self.StallFactor < .2f,
                "low-energy recovery regains maneuvering speed using aerodynamic acceleration");
            report.Append("speed recovery final=").Append(self.Speed.ToString("F1")).Append(" m/s\n");

            self = Plane(1, 0, new Vector3(-2600, 205, -2800));
            self.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, .10f); self.Velocity = self.Forward * 112;
            planes = new List<AircraftState> { self }; pilot = new PilotAI(83);
            float startAgl = self.Position.Y - terrain.Height(self.Position.X, self.Position.Z), minAgl = startAgl;
            initial = pilot.Update(self, planes, terrain, orders, Tick);
            check(pilot.State == "Terrain escape" && initial.Pitch > .2f, "descending low aircraft override mission orders with an early pull-up");
            // This departure crosses the hill beyond the airfield after seven seconds. Include
            // the complete crossing instead of measuring the climb's AGL at the ridge itself.
            for (int i = 0; i < 15 * 60 && self.Alive; i++)
            {
                FlightDynamics.Step(self, pilot.Update(self, planes, terrain, orders, Tick), Tick, terrain);
                minAgl = Math.Min(minAgl, self.Position.Y - terrain.Height(self.Position.X, self.Position.Z));
            }
            float finalAgl = self.Position.Y - terrain.Height(self.Position.X, self.Position.Z);
            check(self.Alive && minAgl > 60 && finalAgl > startAgl + 100,
                "terrain escape actually clears the ground through elevator and lift");
            report.Append("terrain recovery minimum=").Append(minAgl.ToString("F0")).Append(" final=").Append(finalAgl.ToString("F0")).Append(" m AGL\n");
        }

        static bool Bounded(FlightInput input)
        {
            return !float.IsNaN(input.Pitch) && !float.IsNaN(input.Roll) && !float.IsNaN(input.Yaw) && !float.IsNaN(input.Throttle)
                && Math.Abs(input.Pitch) <= 1 && Math.Abs(input.Roll) <= 1 && Math.Abs(input.Yaw) <= 1 && input.Throttle >= 0 && input.Throttle <= 1;
        }

        static void ValidateTraffic(Action<bool, string> check, StringBuilder report)
        {
            var terrain = new TerrainField(1944);
            var first = Plane(1, 0, new Vector3(0, 1500, -450));
            var second = Plane(2, 0, new Vector3(0, 1500, 450));
            second.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Math.PI);
            second.Velocity = second.Forward * 112;
            var planes = new List<AircraftState> { first, second };
            var firstPilot = new PilotAI(18); var secondPilot = new PilotAI(21);
            var firstRoute = new PilotOrders { FollowRoute = true, Destination = new Vector3(0, 1500, 9000) };
            var secondRoute = new PilotOrders { FollowRoute = true, Destination = new Vector3(0, 1500, -9000) };
            float closest = float.MaxValue; bool avoided = false;
            for (int i = 0; i < 12 * 60; i++)
            {
                FlightInput a = firstPilot.Update(first, planes, terrain, firstRoute, Tick);
                FlightInput b = secondPilot.Update(second, planes, terrain, secondRoute, Tick);
                avoided |= firstPilot.State == "Avoid traffic" || secondPilot.State == "Avoid traffic";
                FlightDynamics.Step(first, a, Tick, terrain); FlightDynamics.Step(second, b, Tick, terrain);
                closest = Math.Min(closest, Vector3.Distance(first.Position, second.Position));
            }
            check(avoided, "converging friendly traffic triggers predictive avoidance before the crossing");
            check(first.Alive && second.Alive && closest > 20, "head-on friendly encounter clears both fuselages using normal bank and elevator");
            report.Append("head-on avoidance closest=").Append(closest.ToString("F1")).Append(" m\n");
        }
    }
}
