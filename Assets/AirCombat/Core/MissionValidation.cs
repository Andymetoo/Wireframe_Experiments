using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Meridian
{
    /// <summary>Objective fixtures are explicit; full sortie diagnostics never move or heal combatants.</summary>
    public static class MissionValidation
    {
        public static string Summary { get; private set; } = "Tactical sorties not run";

        public static void Run(Action<bool, string> check)
        {
            ValidateGeneration(check);
            ValidateGroundWeapons(check);
            ValidateObjectives(check);
            ValidateDeterminism(check);
            ValidateSorties(check);
            ValidateExpertInterceptions(check);
        }

        private static void ValidateGeneration(Action<bool, string> check)
        {
            foreach (MissionKind kind in TacticalKinds())
                for (int seed = 0; seed < 24; seed++)
                {
                    var mission = new AirMission(seed, kind);
                    var ids = new HashSet<int>(); bool valid = mission.Wingmen.Count == 2 && mission.WingmenAlive == 2;
                    foreach (AircraftState plane in mission.Aircraft)
                        valid &= ids.Add(plane.Id) && plane.Alive && plane.Position.Y > mission.Terrain.Height(plane.Position.X, plane.Position.Z) + 350f;
                    foreach (GroundTarget target in mission.GroundTargets)
                        valid &= ids.Add(target.Id) && target.Alive && target.Position.Y >= mission.Terrain.Height(target.Position.X, target.Position.Z);
                    check(valid, kind + " seeded force generation, unique IDs and terrain clearance " + seed);
                    if (kind == MissionKind.Interception) check(mission.Raiders.Count == 2 && mission.EnemyRemaining >= 3, "interception separates raid objectives from escorts " + seed);
                    if (kind == MissionKind.BomberEscort) check(mission.Bombers.Count == 3 && mission.RequiredBombers == 2, "bomber escort has real formation and survival margin " + seed);
                    if (kind == MissionKind.GroundAttack) check(mission.RequiredGroundTargets == 4 && mission.GroundTargets.Count == 6, "ground convoy separates logistics from optional flak " + seed);
                }
            var orders = new AirMission(1944, MissionKind.BomberEscort);
            orders.IssueOrder(SquadronOrder.CoverMe);
            check(orders.CurrentOrder == SquadronOrder.CoverMe && orders.LastMessage.Contains("tail"), "order changes mission state with acknowledgment");
            int selected = orders.SelectNextTarget();
            bool hostile = false; foreach (AircraftState plane in orders.Aircraft) if (plane.Id == selected && plane.Team == 1) hostile = true;
            check(hostile, "target selection chooses an in-range hostile rather than a friendly asset");
            var restart = new AirMission(1944, MissionKind.BomberEscort);
            check(restart.CurrentOrder == SquadronOrder.FormUp && restart.Time == 0 && restart.CollisionLosses == 0, "new sortie resets command, time and diagnostic state");
            restart.StepBots(float.NaN); restart.StepBots(-1f); restart.Step(new FlightInput(), float.PositiveInfinity);
            check(restart.Time == 0f && restart.CurrentOrder == SquadronOrder.FormUp, "invalid simulation deltas do not advance time or activate bot commands");
        }

        private static void ValidateGroundWeapons(Action<bool, string> check)
        {
            var terrain = new TerrainField(1944);
            var truck = new GroundTarget { Id = 100, Position = new Vector3(0, terrain.Height(0, 0) + 2f, 0), Health = 39f };
            truck.PreviousPosition = truck.Position;
            float fraction;
            check(Weapons.SweptGroundHit(truck.Position - Vector3.UnitZ * 40f, truck.Position + Vector3.UnitZ * 40f, truck, out fraction), "swept projectile hits truck between simulation ticks");
            check(!Weapons.SweptGroundHit(truck.Position + Vector3.UnitY * 5f - Vector3.UnitZ * 40f, truck.Position + Vector3.UnitY * 5f + Vector3.UnitZ * 40f, truck, out fraction), "round above truck misses, without an inflated target sphere");
            var rounds = new List<Projectile>(); var planes = new List<AircraftState>();
            var previous = new Dictionary<int, Vector3>(); var events = new List<CombatEvent>();
            var targets = new List<GroundTarget> { truck }; var guns = new Weapons(1944);
            for (int i = 0; i < 3; i++) rounds.Add(new Projectile { Position = truck.Position - Vector3.UnitZ * 9f,
                Velocity = Vector3.UnitZ * 760f, Team = 0, OwnerId = 0 });
            guns.Step(1f / 30f, rounds, planes, previous, terrain, events, targets);
            check(!truck.Alive && truck.Health == 0 && guns.PlayerHits == 3 && rounds.Count == 0, "three real rounds disable a convoy truck and are consumed");
            truck.Alive = true; truck.Health = 39f;
            rounds.Add(new Projectile { Position = truck.Position - Vector3.UnitZ * 9f, Velocity = Vector3.UnitZ * 760f, Team = 0, OwnerId = 1 });
            guns.Step(1f / 30f, rounds, planes, previous, terrain, events, targets);
            check(truck.Health == 26f && guns.PlayerHits == 3, "wingman physical hit does not inflate the player's hit count");
            rounds.Add(new Projectile { Position = truck.Position - Vector3.UnitZ * 9f, Velocity = Vector3.UnitZ * 760f, Team = 1, OwnerId = 101 });
            guns.Step(1f / 30f, rounds, planes, previous, terrain, events, targets);
            check(truck.Health == 26f, "AAA cannot damage its own convoy");

            var mission = new AirMission(81, MissionKind.GroundAttack);
            GroundTarget aaa = mission.GroundTargets[4];
            var defense = new GroundDefense(81); rounds.Clear();
            var targetPlane = new AircraftState { Id = 0, Team = 0, Position = aaa.Position + new Vector3(0, 300, 120),
                Velocity = Vector3.UnitX * 110f, Role = AircraftRole.Player };
            planes.Add(targetPlane);
            int ammo = aaa.Ammunition;
            for (int i = 0; i < 300; i++) defense.Step(1f / 60f, mission.GroundTargets, planes, rounds);
            check(aaa.ShotsFired > 0 && aaa.Ammunition == ammo - aaa.ShotsFired && aaa.ShotsFired <= 11, "AAA finite bursts consume ammunition at a bounded rate");
            check(rounds.Count > 0 && rounds[0].Velocity.Length() > 500f && rounds[0].OwnerId >= 100, "AAA emits visible physical projectiles instead of automatic damage");
            aaa.Alive = false; int shots = aaa.ShotsFired;
            for (int i = 0; i < 300; i++) defense.Step(1f / 60f, mission.GroundTargets, planes, rounds);
            check(aaa.ShotsFired == shots, "destroyed flak stops firing");

            var wing = mission.Wingmen[0];
            rounds.Clear(); previous.Clear(); previous[wing.Id] = wing.Position;
            for (int i = 0; i < 8; i++) rounds.Add(new Projectile { Position = wing.Position - wing.Forward * 10f,
                Velocity = wing.Forward * 760f, Team = 1, OwnerId = 10 });
            guns.Step(1f / 30f, rounds, mission.Aircraft, previous, mission.Terrain, events, mission.GroundTargets);
            check(!wing.Alive && wing.Health == 0, "friendly wingmen are mortal under the same projectile damage rules");
        }

        private static void ValidateObjectives(Action<bool, string> check)
        {
            var escort = new AirMission(101, MissionKind.BomberEscort);
            foreach (AircraftState plane in escort.Aircraft) if (plane.Team == 1) plane.Alive = false;
            escort.Step(new FlightInput { Throttle = .7f }, 1f / 60f);
            check(escort.Outcome == MissionOutcome.Flying && escort.BombersArrived == 0, "destroying every fighter does not substitute for bomber arrival");
            for (int i = 0; i < 2; i++) escort.Bombers[i].Position = escort.RouteDestination + Vector3.Normalize(escort.RouteDestination - escort.RouteStart) * (100f + i * 300f);
            escort.Step(new FlightInput { Throttle = .7f }, 1f / 60f);
            check(escort.Outcome == MissionOutcome.Victory && escort.BombersArrived == 2, "two distinct bomber route crossings complete escort");
            float frozen = escort.Time; escort.StepBots(1f);
            check(escort.Time == frozen, "completed tactical sortie stops simulation");
            escort = new AirMission(102, MissionKind.BomberEscort);
            escort.Bombers[0].Alive = escort.Bombers[1].Alive = false;
            foreach (AircraftState plane in escort.Aircraft) if (plane.Team == 1) plane.Alive = false;
            escort.Step(new FlightInput(), 1f / 60f);
            check(escort.Outcome == MissionOutcome.Defeat, "bomber loss threshold takes precedence over eliminating enemies");

            var interception = new AirMission(103, MissionKind.Interception);
            foreach (AircraftState raider in interception.Raiders) raider.Alive = false;
            interception.Step(new FlightInput { Throttle = .7f }, 1f / 60f);
            check(interception.Outcome == MissionOutcome.Victory && interception.EnemyRemaining > 0, "interception completes when raid leaders are stopped despite surviving escorts");
            interception = new AirMission(104, MissionKind.Interception);
            interception.Raiders[0].Position = interception.RouteDestination + Vector3.Normalize(interception.RouteDestination - interception.RouteStart) * 100f;
            interception.Step(new FlightInput { Throttle = .7f }, 1f / 60f);
            check(interception.Outcome == MissionOutcome.Defeat && interception.EnemyEscaped == 1, "a raid crossing the defended boundary causes genuine objective failure");

            var ground = new AirMission(105, MissionKind.GroundAttack);
            foreach (AircraftState plane in ground.Aircraft) if (plane.Team == 1) plane.Alive = false;
            ground.Step(new FlightInput { Throttle = .7f }, 1f / 60f);
            check(ground.Outcome == MissionOutcome.Flying, "ground attack cannot be won by fighter kills alone");
            foreach (GroundTarget target in ground.GroundTargets) if (target.IsObjective) target.Alive = false;
            ground.Step(new FlightInput { Throttle = .7f }, 1f / 60f);
            check(ground.Outcome == MissionOutcome.Victory && ground.GroundDestroyed == 4 && ground.GroundTargets[4].Alive, "logistics destruction wins while optional flak can remain");
            var ballisticGround = new AirMission(109, MissionKind.GroundAttack);
            // A controlled collision fixture tests the entire projectile->damage->objective path.
            // These rounds are explicitly placed; the full sorties below use aircraft guns only.
            foreach (GroundTarget target in ballisticGround.GroundTargets)
                if (target.IsObjective)
                    for (int round = 0; round < 3; round++)
                        ballisticGround.Projectiles.Add(new Projectile { Position = target.Position + Vector3.UnitY * 8f,
                            Velocity = -Vector3.UnitY * 760f, Team = 0, OwnerId = 0 });
            ballisticGround.Step(new FlightInput { Throttle = .7f }, 1f / 30f);
            check(ballisticGround.Outcome == MissionOutcome.Victory && ballisticGround.Hits == 12 && ballisticGround.GroundObjectivesDestroyed == 4,
                "real projectile impacts cause damage, four objective losses and ground mission victory");
            ground = new AirMission(106, MissionKind.GroundAttack);
            foreach (GroundTarget target in ground.GroundTargets) if (target.IsObjective) target.Alive = false;
            ground.Player.Alive = false;
            ground.Step(new FlightInput(), 1f / 60f);
            check(ground.Outcome == MissionOutcome.Defeat, "player death on objective completion tick remains defeat");
            var collision = new AirMission(107, MissionKind.BomberEscort);
            collision.Wingmen[0].Position = collision.Player.Position;
            collision.Wingmen[0].Velocity = collision.Player.Velocity;
            collision.Step(new FlightInput { Throttle = .7f }, 1f / 60f);
            check(!collision.Player.Alive && !collision.Wingmen[0].Alive && collision.CollisionLosses == 2, "real fuselage collision destroys both aircraft without teleport correction");
        }

        private static void ValidateDeterminism(Action<bool, string> check)
        {
            foreach (MissionKind kind in TacticalKinds())
            {
                var a = new AirMission(int.MinValue, kind); var b = new AirMission(int.MinValue, kind);
                for (int i = 0; i < 60 * 25; i++) { a.StepBots(1f / 60f); b.StepBots(1f / 60f); }
                bool same = a.Outcome == b.Outcome && a.TotalRoundsFired == b.TotalRoundsFired && a.CurrentOrder == b.CurrentOrder;
                for (int i = 0; i < a.Aircraft.Count; i++) same &= a.Aircraft[i].Position == b.Aircraft[i].Position && a.Aircraft[i].Health == b.Aircraft[i].Health;
                for (int i = 0; i < a.GroundTargets.Count; i++) same &= a.GroundTargets[i].Position == b.GroundTargets[i].Position && a.GroundTargets[i].Health == b.GroundTargets[i].Health;
                check(same, kind + " seed reproduces complete aircraft, command and ground state");
            }
        }

        private static void ValidateSorties(Action<bool, string> check)
        {
            var report = new StringBuilder("TACTICAL SORTIES (unmodified real physics / weapons / pilots)\n");
            // Keep the full six-seed interception audit in the regression population; a geometry
            // change should not require one historical seed to remain the sole proof of bot success.
            int[] seeds = { 1944, 81, 31337, 0, 17, 88 };
            foreach (MissionKind kind in TacticalKinds())
            {
                int earnedWins = 0;
                foreach (int seed in seeds)
                {
                    var mission = new AirMission(seed, kind); bool finite = true, bounded = true; int slowTicks = 0;
                    for (int tick = 0; tick < 60 * 605 && mission.Outcome == MissionOutcome.Flying; tick++)
                    {
                        mission.StepBots(1f / 60f);
                        if (tick % 60 != 0) continue;
                        foreach (AircraftState plane in mission.Aircraft)
                        {
                            finite &= FlightDynamics.Finite(plane.Position) && FlightDynamics.Finite(plane.Velocity) && FlightDynamics.Finite(plane.Rotation);
                            bounded &= plane.Ammo >= 0 && plane.Health >= 0f;
                            if (plane.Alive && plane.Speed < plane.Definition.StallSpeed * .9f) slowTicks++;
                        }
                        bounded &= mission.Projectiles.Count < 350;
                        foreach (GroundTarget target in mission.GroundTargets) bounded &= target.Health >= 0f && target.Ammunition >= 0;
                    }
                    check(finite && mission.Outcome != MissionOutcome.Flying, kind + " full bot sortie reaches finite terminal outcome " + seed);
                    check(slowTicks < 180, kind + " no sustained squadron-wide stall loop " + seed);
                    check(mission.TotalRoundsFired > 0, kind + " pilots engage using actual ammunition " + seed);
                    check(bounded, kind + " ammunition, health and projectile lifetime stay bounded " + seed);
                    if (mission.Outcome == MissionOutcome.Victory) earnedWins++;
                    report.Append(kind).Append(" seed=").Append(seed).Append(" ").Append(mission.Outcome)
                        .Append(" t=").Append(mission.Time.ToString("F1")).Append(" rounds=").Append(mission.TotalRoundsFired)
                        .Append(" player hits=").Append(mission.Hits).Append(" wingmen=").Append(mission.WingmenAlive)
                        .Append(" flak rounds=").Append(mission.GroundRoundsFired).Append(" collisions=").Append(mission.CollisionLosses)
                        .Append(" | ").Append(mission.ObjectiveStatus).Append('\n');
                    foreach (AircraftState plane in mission.Aircraft)
                        if (!plane.Alive) report.Append("  Lost ").Append(plane.Callsign).Append(": ").Append(plane.DestructionCause).Append('\n');
                }
                check(earnedWins > 0, kind + " can be completed by live pilots through actual movement, gunnery and objective progress");
            }
            Summary = report.ToString();
        }

        private static MissionKind[] TacticalKinds()
        { return new[] { MissionKind.Interception, MissionKind.BomberEscort, MissionKind.GroundAttack }; }

        private static void ValidateExpertInterceptions(Action<bool, string> check)
        {
            var report = new StringBuilder("CONTROLS-ONLY TACTICAL INTERCEPTIONS (player and wingmen split raid targets)\n");
            int wins = 0;
            foreach (int seed in new[] { 1944, 81, 31337 })
            {
                var mission = new AirMission(seed, MissionKind.Interception);
                var pilot = new TacticalValidationPilot(seed);
                for (int tick = 0; tick < 60 * 481 && mission.Outcome == MissionOutcome.Flying; tick++)
                    mission.Step(pilot.Update(mission), 1f / 60f);
                if (mission.Outcome == MissionOutcome.Victory) wins++;
                check(mission.Outcome != MissionOutcome.Flying && mission.Hits > 0, "controls-only interception pilot lands physical rounds and reaches an outcome " + seed);
                report.Append("Interception seed=").Append(seed).Append(' ').Append(mission.Outcome)
                    .Append(" t=").Append(mission.Time.ToString("F1")).Append(" hits=").Append(mission.Hits)
                    .Append(" rounds=").Append(mission.RoundsFired).Append(" wingmen=").Append(mission.WingmenAlive)
                    .Append(" | ").Append(mission.ObjectiveStatus).Append('\n');
            }
            check(wins > 0, "at least one complete interception is earned through player controls, squadron orders and physical gunnery");
            Summary += report.ToString();
        }
    }
}
