using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Meridian
{
    /// <summary>Encounter state fixtures are explicitly separate from actual controls-only sorties.</summary>
    public static class EncounterValidation
    {
        const float Tick = 1f / 60f;
        public static string Summary { get; private set; } = "Expanded encounters not validated.";

        public static void Run(Action<bool, string> check)
        {
            ValidateGeneration(check);
            ValidateWaves(check);
            ValidateAce(check);
            ValidateProgression(check);
            var report = new StringBuilder("EXPANDED ENCOUNTERS / ACTUAL CONTROLS-ONLY SORTIES\n");
            int aceWins = 0, fleetWins = 0;
            foreach (MissionKind kind in new[] { MissionKind.AceHunt, MissionKind.FleetBattle })
                foreach (int seed in new[] { 1944, 17, 88 })
                {
                    var mission = new AirMission(seed, kind);
                    var expert = new TacticalValidationPilot(seed);
                    float contact = -1f, opportunity = -1f, shot = -1f;
                    int peakPlanes = 0, peakRounds = 0;
                    bool finite = true;
                    for (int tick = 0; tick < 601 * 60 && mission.Outcome == MissionOutcome.Flying; tick++)
                    {
                        if (kind == MissionKind.AceHunt) mission.StepBots(Tick);
                        else mission.Step(expert.Update(mission), Tick);
                        peakPlanes = Math.Max(peakPlanes, mission.Aircraft.Count);
                        peakRounds = Math.Max(peakRounds, mission.Projectiles.Count);
                        if (mission.RoundsFired > 0 && shot < 0) shot = mission.Time;
                        foreach (AircraftState plane in mission.Aircraft)
                        {
                            finite &= FlightDynamics.Finite(plane.Position) && FlightDynamics.Finite(plane.Velocity)
                                && FlightDynamics.Finite(plane.Rotation) && plane.Ammo >= 0;
                            if (!plane.Alive || plane.Team == 0) continue;
                            float distance = Vector3.Distance(plane.Position, mission.Player.Position);
                            if (contact < 0 && distance < 2500f) contact = mission.Time;
                            if (opportunity < 0 && distance < 900f && Vector3.Dot(mission.Player.Forward,
                                Vector3.Normalize(Weapons.ComputeLead(mission.Player, plane) - mission.Player.Position)) > .9982f) opportunity = mission.Time;
                        }
                    }
                    if (mission.Outcome == MissionOutcome.Victory)
                    { if (kind == MissionKind.AceHunt) aceWins++; else fleetWins++; }
                    check(finite && peakPlanes <= 12 && peakRounds < 400,
                        kind + " " + seed + " actual battle preserves finite flight and bounded aircraft/projectiles");
                    check(mission.Outcome != MissionOutcome.Flying, kind + " " + seed + " reaches an authoritative terminal outcome");
                    check(mission.TotalRoundsFired > 20 && contact >= 0 && opportunity >= 0,
                        kind + " " + seed + " produces actual contact, maneuvering and firing opportunities");
                    report.Append(kind).Append(" seed=").Append(seed).Append(" result=").Append(mission.Outcome)
                        .Append(" contact=").Append(contact.ToString("F1")).Append("s firing-window=").Append(opportunity.ToString("F1"))
                        .Append("s first-shot=").Append(shot.ToString("F1")).Append("s duration=").Append(mission.Time.ToString("F1"))
                        .Append("s kills=").Append(mission.Kills).Append(" player-hits=").Append(mission.Hits)
                        .Append(" wingmen=").Append(mission.WingmenAlive).Append(" collisions=").Append(mission.CollisionLosses)
                        .Append(" peak-aircraft=").Append(peakPlanes).Append(" peak-rounds=").Append(peakRounds)
                        .Append(" cause=").Append(mission.Summary).AppendLine();
                }
            check(aceWins > 0, "Ace encounter is won through actual stock-aircraft combat without outcome overrides");
            check(fleetWins > 0, "Larger air battle is won through actual stock-aircraft combat without outcome overrides");
            Summary = report.ToString();
        }

        static void ValidateGeneration(Action<bool, string> check)
        {
            foreach (MissionKind kind in new[] { MissionKind.AceHunt, MissionKind.FleetBattle })
                for (int pressure = 0; pressure <= 2; pressure++)
                {
                    bool valid = true, stock = true, repeated = true;
                    for (int seed = 0; seed < 24; seed++)
                    {
                        var mission = new AirMission(seed, kind, new AirEncounterOptions { Pressure = pressure });
                        var other = new AirMission(seed, kind, new AirEncounterOptions { Pressure = pressure });
                        var ids = new HashSet<int>();
                        valid &= mission.WingmenAlive == 2 && mission.Outcome == MissionOutcome.Flying;
                        for (int i = 0; i < mission.Aircraft.Count; i++)
                        {
                            AircraftState plane = mission.Aircraft[i];
                            valid &= ids.Add(plane.Id) && plane.Position.Y > mission.Terrain.Height(plane.Position.X, plane.Position.Z) + 300f;
                            repeated &= plane.Position == other.Aircraft[i].Position && plane.Rotation == other.Aircraft[i].Rotation;
                            if (plane.Team == 1) stock &= plane.Health == 100f && plane.DamageMultiplier == 1f
                                && plane.Definition.MaxPower == AircraftDefinition.AxisFighter.MaxPower;
                        }
                        valid &= kind != MissionKind.AceHunt || mission.AceId >= 0 && mission.GetPilot(mission.AceId).Skill == PilotSkill.Ace;
                    }
                    check(valid && repeated, kind + " pressure " + pressure + " has 24 reproducible, separated and terrain-safe forces");
                    check(stock, kind + " pressure " + pressure + " uses stock enemy hull, power and projectile damage");
                }
        }

        static void ValidateWaves(Action<bool, string> check)
        {
            foreach (int pressure in new[] { 0, 1 })
            {
                var mission = new AirMission(88, MissionKind.FleetBattle, new AirEncounterOptions { Pressure = pressure });
                foreach (AircraftState enemy in mission.Aircraft) if (enemy.Team == 1) enemy.Alive = false;
                mission.Step(new FlightInput { Throttle = .78f }, Tick);
                check(mission.Outcome == MissionOutcome.Flying && mission.PendingEnemyFlights > 0,
                    "Battle fixture pressure " + pressure + ": destroying opening flight cannot finish before scheduled threats");
                // Time is advanced explicitly for this objective fixture, not for gameplay evidence.
                typeof(AirMission).GetProperty("Time").SetValue(mission, 76f);
                int initialPlanes = mission.Aircraft.Count;
                mission.Step(new FlightInput { Throttle = .78f }, Tick);
                bool distant = true;
                for (int i = initialPlanes; i < mission.Aircraft.Count; i++)
                    foreach (AircraftState friendly in mission.Aircraft)
                        if (friendly.Alive && friendly.Team == 0)
                            distant &= Vector3.Distance(mission.Aircraft[i].Position, friendly.Position) > 6500f;
                check(distant && mission.EnemyFlightsLaunched == 2 && mission.EnemyFlightsEngaged == 1
                    && mission.Outcome == MissionOutcome.Flying,
                    "Battle fixture pressure " + pressure + ": distant spawning neither ambushes friendlies nor counts as entering combat");
                foreach (AircraftState enemy in mission.Aircraft) if (enemy.Team == 1) enemy.Alive = false;
                mission.Step(new FlightInput { Throttle = .78f }, Tick);
                if (pressure == 0)
                    check(mission.Outcome == MissionOutcome.Victory, "Battle fixture: defeated incoming flight counts as faced after genuine objective accounting");
                else
                {
                    check(mission.Outcome == MissionOutcome.Flying, "Final battle fixture: the third flight prevents an early two-wave victory");
                    typeof(AirMission).GetProperty("Time").SetValue(mission, 151f);
                    mission.Step(new FlightInput { Throttle = .78f }, Tick);
                    check(mission.Outcome == MissionOutcome.Flying && mission.EnemyFlightsEngaged == 2,
                        "Final battle fixture: kill quota alone cannot win while third flight is still outside the sector");
                    foreach (AircraftState enemy in mission.Aircraft)
                        if (enemy.Team == 1 && enemy.Alive) enemy.Position = mission.ObjectivePosition + Vector3.UnitX * 4100f + Vector3.UnitY * 400f;
                    mission.Step(new FlightInput { Throttle = .78f }, Tick);
                    check(mission.Outcome == MissionOutcome.Victory && mission.EnemyFlightsEngaged == 3,
                        "Final battle fixture: quota and actual final-flight sector entry complete the offensive objective");
                }
                string summary = mission.Summary; float time = mission.Time; int score = mission.Score;
                mission.StepBots(.25f); mission.Step(new FlightInput { Fire = true, Throttle = 1 }, .25f);
                check(mission.Time == time && mission.Score == score && mission.Summary == summary,
                    "Battle finalization remains immutable throughout subsequent presentation calls");
            }
            foreach (MissionKind kind in new[] { MissionKind.AceHunt, MissionKind.FleetBattle })
            {
                var loss = new AirMission(1944, kind); loss.Player.Alive = false; loss.Player.Health = 0;
                loss.Step(new FlightInput(), Tick);
                check(loss.Outcome == MissionOutcome.Defeat, kind + " player loss remains an authoritative mission failure");
            }
        }

        static void ValidateAce(Action<bool, string> check)
        {
            var self = new AircraftState { Id = 10, Role = AircraftRole.Fighter, Team = 1,
                Definition = AircraftDefinition.AxisFighter, Position = new Vector3(0, 1800, 0), Velocity = Vector3.UnitZ * 90f };
            var target = new AircraftState { Id = 0, Team = 0, Position = self.Position + new Vector3(0, 400, 1300), Velocity = Vector3.UnitZ * 160f };
            var planes = new List<AircraftState> { self, target };
            var ace = new PilotAI(81, false, PilotSkill.Ace);
            var orders = new PilotOrders { ObjectiveAnchor = self.Position };
            Vector3 position = self.Position, velocity = self.Velocity; Quaternion rotation = self.Rotation;
            FlightInput input = ace.Update(self, planes, new TerrainField(81), orders, Tick);
            check(ace.State == "Extend", "Ace recognizes a losing climb and extends to regain energy instead of following the lure");
            check(self.Position == position && self.Velocity == velocity && self.Rotation == rotation && self.Health == 100 && self.Ammo == 900,
                "Ace decisions do not write transforms, momentum, damage or ammunition");
            check(Math.Abs(input.Pitch) <= 1 && Math.Abs(input.Roll) <= 1 && Math.Abs(input.Yaw) <= 1 && input.Throttle >= 0 && input.Throttle <= 1,
                "Ace decisions command the same bounded flight input as every other pilot");
            FlightDynamics.Step(self, input, new TerrainField(81), Tick);
            check(Vector3.Distance(self.Position, position) < 3f && self.Rotation != rotation && self.Health == 100,
                "Ace movement is produced by ordinary finite aerodynamic integration");
        }

        static void ValidateProgression(Action<bool, string> check)
        {
            var run = new RogueRun(42); bool escalating = true, stock = true, concise = true;
            for (int stage = 0; stage < RogueRun.SortieCount; stage++)
            {
                if (stage == 0) escalating &= run.Routes[0].Kind == MissionKind.Duel;
                if (stage == 1) escalating &= run.Routes[1].Kind == MissionKind.Interception && run.Routes[2].Kind == MissionKind.AceHunt;
                if (stage == 2) escalating &= run.Routes[1].Kind == MissionKind.FleetBattle && run.Routes[1].EncounterPressure == 0;
                if (stage == 3) escalating &= run.Routes[1].Kind == MissionKind.FleetBattle && run.Routes[1].EncounterPressure == 1 && run.Routes[2].EncounterPressure == 2;
                foreach (RogueRoute route in run.Routes)
                {
                    concise &= route.Description.Length <= 170;
                    var encounter = new AirMission(route.Seed, route.Kind, new AirEncounterOptions { Pressure = route.EncounterPressure });
                    foreach (AircraftState plane in encounter.Aircraft) if (plane.Team == 1) stock &= plane.Health == 100 && plane.DamageMultiplier == 1;
                }
                RogueValidation.CompleteFixture(run.LaunchRoute(0)); run.FinishSortie();
                if (stage < RogueRun.SortieCount - 1) run.ChooseReward(0);
            }
            check(escalating && run.Phase == RoguePhase.Victory, "Four-sortie decks escalate from patrol to reinforced formations and a defined final offensive");
            check(stock, "Roguelite tactical escalation never inflates enemy hull or gun damage");
            check(concise, "Route descriptions state the tactical problem in at most 170 characters for readable cards");
        }
    }
}
