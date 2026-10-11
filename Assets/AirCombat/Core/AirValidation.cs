using System;
using System.Collections.Generic;
using System.Numerics;

namespace Meridian
{
    public static class AirValidation
    {
        public static string SortieSummary { get; private set; }
        public static void Run(Action<bool, string> check)
        {
            DynamicsValidation.Run(check);
            ValidateBallistics(check);
            ValidateMissions(check);
            ValidatePilots(check);
            ValidatePlayableSorties(check);
        }

        private static AircraftState Target(Vector3 position, int id = 1, int team = 1)
        {
            return new AircraftState { Id = id, Team = team, Role = AircraftRole.Fighter, Definition = AircraftDefinition.AxisFighter,
                Position = position, Rotation = Quaternion.Identity, Velocity = Vector3.UnitZ * 110f,
                Health = 100f, Alive = true, Ammo = 1000, Throttle = .8f };
        }

        private static void ValidateBallistics(Action<bool, string> check)
        {
            AircraftState target = Target(new Vector3(0f, 1000f, 500f));
            float fraction;
            check(Weapons.SweptAircraftHit(new Vector3(0f, 1000f, 400f), new Vector3(0f, 1000f, 600f), target.Position, target, out fraction), "swept projectile intersects fuselage between ticks");
            check(!Weapons.SweptAircraftHit(new Vector3(0f, 1010f, 400f), new Vector3(0f, 1010f, 600f), target.Position, target, out fraction), "projectile above aircraft misses");
            check(Weapons.SweptAircraftHit(new Vector3(5f, 1000f, 480f), new Vector3(5f, 1000f, 520f), target.Position, target, out fraction), "solid wing can be hit");
            check(!Weapons.SweptAircraftHit(new Vector3(5f, 1002f, 480f), new Vector3(5f, 1002f, 520f), target.Position, target, out fraction), "space above thin wing is not a sphere hit");
            check(Weapons.SweptAircraftHit(new Vector3(0f, 1000f, 500f), new Vector3(0f, 1000f, 500f), target.Position - Vector3.UnitX * 20f, target, out fraction), "moving aircraft crossing a projectile is swept");
            AircraftState shooter = Target(new Vector3(0f, 1000f, 0f), 0, 0);
            shooter.Role = AircraftRole.Player;
            target.Velocity = new Vector3(90f, 0f, 110f);
            Vector3 lead = Weapons.ComputeLead(shooter, target);
            check(lead.X > 55f && lead.X < 65f && lead.Y > target.Position.Y, "lead predicts lateral movement and compensates gravity");
            var weapons = new Weapons(123);
            var rounds = new List<Projectile>();
            for (int i = 0; i < 600; i++) weapons.Fire(shooter, true, 1f / 60f, rounds);
            check(weapons.PlayerRounds >= 133 && weapons.PlayerRounds <= 135, "rate of fire is bounded to 13.33 rounds/sec");
            check(shooter.Ammo == 1000 - weapons.PlayerRounds, "every fired round consumes ammunition");
            shooter.Ammo = 0;
            int before = rounds.Count;
            weapons.Fire(shooter, true, 2f, rounds);
            check(rounds.Count == before, "empty weapons cannot create projectiles");
            rounds.Clear();
            target.Health = 100f;
            var positions = new Dictionary<int, Vector3> { { 1, target.Position } };
            var events = new List<CombatEvent>();
            var targets = new List<AircraftState> { target };
            for (int i = 0; i < 8; i++)
                rounds.Add(new Projectile { Position = target.Position - Vector3.UnitZ * 10f, Velocity = Vector3.UnitZ * 760f, OwnerId = 0, Team = 0 });
            weapons.Step(1f / 30f, rounds, targets, positions, new TerrainField(1), events);
            check(!target.Alive && target.Health == 0f, "actual ballistic damage destroys aircraft");
            check(events.Count >= 8 && rounds.Count == 0, "hits consume rounds and produce geometric impact events");
            target = Target(new Vector3(0f, 1000f, 500f), 1, 0);
            rounds.Add(new Projectile { Position = target.Position - Vector3.UnitZ * 10f, Velocity = Vector3.UnitZ * 760f, OwnerId = 0, Team = 0 });
            targets[0] = target;
            weapons.Step(1f / 30f, rounds, targets, positions, new TerrainField(1), events);
            check(target.Health == 100f, "friendly courier is protected from friendly fire");
            weapons.Step(2f, rounds, targets, positions, new TerrainField(1), events);
            check(rounds.Count == 0, "expired projectiles are removed");
            check(FireAtCrossingTarget(true), "physical intercept with lead hits a crossing aircraft");
            check(!FireAtCrossingTarget(false), "aiming at current location misses a fast crossing aircraft");
        }

        private static bool FireAtCrossingTarget(bool lead)
        {
            var shooter = Target(new Vector3(0f, 1100f, 0f), 0, 0);
            shooter.Role = AircraftRole.Player;
            var target = Target(new Vector3(0f, 1100f, 500f));
            target.Velocity = new Vector3(90f, 0f, 110f);
            Vector3 direction = Vector3.Normalize((lead ? Weapons.ComputeLead(shooter, target) : target.Position) - shooter.Position);
            var rounds = new List<Projectile> { new Projectile { Position = shooter.Position, Velocity = shooter.Velocity + direction * Weapons.MuzzleSpeed, OwnerId = 0, Team = 0 } };
            var planes = new List<AircraftState> { target };
            var previous = new Dictionary<int, Vector3>();
            var events = new List<CombatEvent>();
            var guns = new Weapons(1);
            var terrain = new TerrainField(1);
            for (int tick = 0; tick < 110 && rounds.Count > 0; tick++)
            {
                previous[1] = target.Position;
                target.Position += target.Velocity / 60f;
                guns.Step(1f / 60f, rounds, planes, previous, terrain, events);
            }
            return target.Health < 100f;
        }

        private static void ValidateMissions(Action<bool, string> check)
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var mission = new AirMission(seed, seed % 2 == 0 ? MissionKind.Duel : MissionKind.ConvoyCover);
                check(mission.Player.Alive && mission.EnemyRemaining == (mission.Kind == MissionKind.Duel ? 1 : 2), "valid initial mission " + seed);
                foreach (AircraftState plane in mission.Aircraft)
                    check(plane.Position.Y > mission.Terrain.Height(plane.Position.X, plane.Position.Z) + 200f, "spawn clears terrain " + seed + "/" + plane.Id);
            }
            var victory = new AirMission(1, MissionKind.Duel);
            victory.Aircraft[1].Alive = false;
            victory.Step(new FlightInput { Throttle = .8f }, 1f / 60f);
            check(victory.Outcome == MissionOutcome.Victory && victory.Score >= 1500, "last hostile destroyed completes sortie and scores");
            float finishedTime = victory.Time;
            victory.Step(new FlightInput { Fire = true, Throttle = .8f }, 1f);
            check(victory.Time == finishedTime && victory.RoundsFired == 0, "completed sorties freeze simulation");
            var defeat = new AirMission(2, MissionKind.ConvoyCover);
            defeat.Courier.Alive = false;
            defeat.Step(new FlightInput(), 1f / 60f);
            check(defeat.Outcome == MissionOutcome.Defeat, "courier loss fails escort");
            defeat = new AirMission(2, MissionKind.Duel);
            defeat.Player.Position = new Vector3(0f, -10f, 0f);
            defeat.Step(new FlightInput(), 1f / 60f);
            check(defeat.Outcome == MissionOutcome.Defeat, "player terrain crash fails sortie");
            var first = new AirMission(int.MinValue, MissionKind.ConvoyCover);
            var second = new AirMission(int.MinValue, MissionKind.ConvoyCover);
            for (int i = 0; i < 60 * 30; i++)
            {
                var input = new FlightInput { Throttle = .8f, Roll = (float)Math.Sin(i * .001f) * .08f, Fire = i % 3 == 0 };
                first.Step(input, 1f / 60f); second.Step(input, 1f / 60f);
            }
            for (int i = 0; i < first.Aircraft.Count; i++)
                check(first.Aircraft[i].Position == second.Aircraft[i].Position && first.Aircraft[i].Health == second.Aircraft[i].Health,
                    "fixed-seed simulation repeatability aircraft " + i);
            check(first.RoundsFired == second.RoundsFired && first.Outcome == second.Outcome, "fixed-seed combat repeatability");

            // A controlled firing fixture uses the real mission path, rounds, damage and outcome evaluation.
            var ballisticVictory = new AirMission(1944, MissionKind.Duel);
            AircraftState hostile = ballisticVictory.Aircraft[1];
            hostile.Position = ballisticVictory.Player.Position + ballisticVictory.Player.Forward * 180f;
            hostile.Rotation = ballisticVictory.Player.Rotation;
            hostile.Velocity = ballisticVictory.Player.Velocity;
            hostile.Health = Weapons.RoundDamage;
            hostile.Ammo = 0;
            for (int i = 0; i < 120 && ballisticVictory.Outcome == MissionOutcome.Flying; i++)
                ballisticVictory.Step(new FlightInput { Throttle = .7f, Fire = true }, 1f / 60f);
            check(ballisticVictory.Outcome == MissionOutcome.Victory && ballisticVictory.Hits > 0,
                "actual player fire can destroy the last enemy and produce victory");

            var escort = new AirMission(88, MissionKind.ConvoyCover);
            foreach (AircraftState plane in escort.Aircraft) if (plane.Team == 1) plane.Ammo = 0;
            for (int i = 0; i < 18100 && escort.Outcome == MissionOutcome.Flying; i++)
                escort.Step(new FlightInput { Throttle = .6f }, 1f / 60f);
            check(escort.Outcome == MissionOutcome.Victory && escort.Time >= AirMission.EscortDuration && escort.Courier.Alive,
                "surviving five minutes completes courier objective without requiring kills");
            check(escort.EnemyRemaining == 2, "escort completion does not depend on opponents crashing");
        }

        private static void ValidatePilots(Action<bool, string> check)
        {
            int[] seeds = { 0, 1, 17, 1944, int.MinValue, int.MaxValue };
            for (int s = 0; s < seeds.Length; s++)
            {
                var duel = new AirMission(seeds[s], MissionKind.Duel);
                AircraftState enemy = duel.Aircraft[1];
                Quaternion rotation = enemy.Rotation;
                float maxBank = 0f, maxRate = 0f;
                for (int i = 0; i < 60 * 240 && duel.Outcome == MissionOutcome.Flying; i++)
                {
                    Quaternion before = enemy.Rotation;
                    duel.Step(new FlightInput { Throttle = .62f }, 1f / 60f);
                    maxBank = Math.Max(maxBank, Math.Abs((float)Math.Atan2(-enemy.Right.Y, enemy.Up.Y)));
                    float dot = Math.Min(1f, Math.Abs(Quaternion.Dot(before, enemy.Rotation)));
                    maxRate = Math.Max(maxRate, (float)Math.Acos(dot) * 2f * 60f);
                }
                string suffix = " seed " + seeds[s];
                check(enemy.Alive && enemy.Position.Y > duel.Terrain.Height(enemy.Position.X, enemy.Position.Z), "pilot avoids terrain" + suffix);
                check(maxBank > .5f && enemy.Rotation != rotation, "pilot banks and turns to pursue" + suffix);
                check(maxRate < 2.3f, "pilot obeys bounded physical attitude rates" + suffix);
                check(duel.TotalRoundsFired > 15, "pilot obtains firing solutions and fires" + suffix);
                check(duel.Player.Health < 100f, "pilot ballistic fire damages a predictable target" + suffix);
                check(duel.Outcome == MissionOutcome.Defeat, "ignoring pursuing enemy can fail the sortie" + suffix);
                check(duel.Projectiles.Count < 100 && duel.Events.Count < 100, "transient combat objects remain bounded" + suffix);
            }

            for (int s = 0; s < 8; s++)
            {
                var terrain = new TerrainField(s);
                var self = Target(new Vector3(100f, terrain.Height(100f, 100f) + 150f, 100f));
                self.Velocity = new Vector3(0f, -12f, 110f);
                var pilot = new PilotAI(s);
                var planes = new List<AircraftState> { self };
                bool recovering = false;
                for (int i = 0; i < 60 * 20 && self.Alive; i++)
                {
                    FlightInput input = pilot.Update(self, planes, terrain, Vector3.Zero, 1f / 60f);
                    recovering |= pilot.State == "Terrain escape";
                    FlightDynamics.Step(self, input, terrain, 1f / 60f);
                }
                check(recovering && self.Alive && self.Position.Y > terrain.Height(self.Position.X, self.Position.Z) + 200f,
                    "pilot recovers from a descending low-altitude situation " + s);
            }
        }

        private static void ValidatePlayableSorties(Action<bool, string> check)
        {
            int[] seeds = { 1944, 0, 1, 17, 88, 31337 };
            float minTime = float.MaxValue, maxTime = 0f;
            int totalRounds = 0, wins = 0, escortWins = 0;
            foreach (int seed in seeds)
            {
                var mission = new AirMission(seed, MissionKind.Duel);
                var pilot = new ValidationPilot(seed);
                for (int tick = 0; tick < 60 * 300 && mission.Outcome == MissionOutcome.Flying; tick++)
                    mission.Step(pilot.Update(mission), 1f / 60f);
                check(mission.Outcome == MissionOutcome.Victory && mission.Hits >= 8,
                    "bounded-control test pilot wins through ballistic fire, seed " + seed);
                check(mission.Player.Alive && mission.RoundsFired < 300, "test pilot conserves ammunition and survives, seed " + seed);
                if (mission.Outcome == MissionOutcome.Victory) wins++;
                minTime = Math.Min(minTime, mission.Time); maxTime = Math.Max(maxTime, mission.Time);
                totalRounds += mission.RoundsFired;
            }
            foreach (int seed in new[] { 1944, 1, 88 })
            {
                var mission = new AirMission(seed, MissionKind.ConvoyCover);
                var pilot = new ValidationPilot(seed);
                for (int tick = 0; tick < 60 * 301 && mission.Outcome == MissionOutcome.Flying; tick++)
                    mission.Step(pilot.Update(mission), 1f / 60f);
                check(mission.Outcome == MissionOutcome.Victory && mission.Player.Alive && mission.Courier.Alive,
                    "flight-controller pilot can complete armed escort scenario, seed " + seed);
                check(mission.TotalRoundsFired > 20 && mission.Courier.Health < 180f,
                    "escort victory survives an active enemy attack, seed " + seed);
                if (mission.Outcome == MissionOutcome.Victory) escortWins++;
            }
            SortieSummary = "Complete dogfights: " + wins + "/6 controller-driven ballistic victories; " + minTime.ToString("F1")
                + "-" + maxTime.ToString("F1") + " seconds; " + totalRounds + " rounds total. Armed escort victories: " + escortWins
                + "/3. No state, damage or transform overrides in these nine sorties.";
        }
    }
}
