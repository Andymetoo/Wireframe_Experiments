using System;
using System.Collections.Generic;
using System.Numerics;

namespace Meridian
{
    /// <summary>Small physical collision fixtures for the aircraft-only cockpit hit-confirmation contract.</summary>
    public static class CombatFeedbackValidation
    {
        public static void Run(Action<bool, string> check)
        {
            var guns = new Weapons(1944); var terrain = new TerrainField(81);
            var rounds = new List<Projectile>(); var events = new List<CombatEvent>();
            var target = new AircraftState { Id = 3, Team = 1, Role = AircraftRole.Fighter,
                Position = new Vector3(0, 1700, 300), Health = 25f };
            var aircraft = new List<AircraftState> { target };
            var previous = new Dictionary<int, Vector3> { { target.Id, target.Position } };
            guns.Step(1f / 60f, rounds, aircraft, previous, terrain, events);
            check(guns.PlayerAircraftHits == 0 && guns.PlayerAircraftKills == 0 && guns.LastPlayerAircraftTargetId == -1,
                "Aircraft feedback: looking toward an aircraft with no impact never reports a hit");

            rounds.Add(Round(target.Position + Vector3.UnitY * 12f, 0, 13));
            guns.Step(1f / 60f, rounds, aircraft, previous, terrain, events);
            check(guns.PlayerAircraftHits == 0 && target.Health == 25,
                "Aircraft feedback: a projectile crossing above the target is a miss");
            rounds.Clear(); rounds.Add(Round(target.Position, 0, 13));
            guns.Step(1f / 60f, rounds, aircraft, previous, terrain, events);
            check(guns.PlayerAircraftHits == 1 && guns.PlayerAircraftKills == 0 && guns.PlayerAircraftDamage == 13
                && guns.LastPlayerAircraftDamage == 13 && guns.LastPlayerAircraftTargetId == 3 && target.Health == 12,
                "Aircraft feedback: actual player projectile damage increments aircraft-only hit and damage counters");
            check(events.Count == 1 && events[0].AircraftId == 3 && events[0].OwnerId == 0
                && events[0].AircraftDamage == 13 && !events[0].DestroyedAircraft,
                "Aircraft feedback: impact metadata identifies damaged aircraft, shooter and exact hull removed");
            rounds.Add(Round(target.Position, 0, 30));
            guns.Step(1f / 60f, rounds, aircraft, previous, terrain, events);
            check(!target.Alive && guns.PlayerAircraftHits == 2 && guns.PlayerAircraftKills == 1
                && guns.PlayerAircraftDamage == 25 && guns.LastPlayerAircraftDamage == 12,
                "Aircraft feedback: lethal impact confirms one destruction and reports remaining hull rather than overkill damage");
            check(events[events.Count - 1].DestroyedAircraft, "Aircraft feedback: killing impact is explicitly distinguishable from a normal hit");
            rounds.Add(Round(target.Position, 0, 30));
            guns.Step(1f / 60f, rounds, aircraft, previous, terrain, events);
            check(guns.PlayerAircraftHits == 2 && guns.PlayerAircraftKills == 1,
                "Aircraft feedback: later rounds crossing an already destroyed aircraft cannot duplicate the kill");

            target.Alive = true; target.Health = 100; rounds.Clear(); events.Clear();
            rounds.Add(Round(target.Position, 1, 13));
            guns.Step(1f / 60f, rounds, aircraft, previous, terrain, events);
            check(target.Health == 87 && guns.PlayerAircraftHits == 2 && events[0].OwnerId == 1,
                "Aircraft feedback: wingman hits remain physical but never appear as the player's hit confirmation");
            rounds.Add(Round(target.Position, 0, 0));
            guns.Step(1f / 60f, rounds, aircraft, previous, terrain, events);
            check(target.Health == 87 && guns.PlayerAircraftHits == 2,
                "Aircraft feedback: a zero-damage contact does not trigger confirmation");

            target.DamageTakenMultiplier = .72f;
            rounds.Add(Round(target.Position, 0, 13f * 1.6f));
            guns.Step(1f / 60f, rounds, aircraft, previous, terrain, events);
            check(guns.PlayerAircraftHits == 3 && Math.Abs(guns.LastPlayerAircraftDamage - 13f * 1.6f * .72f) < .001f,
                "Aircraft feedback: boosted ammunition and armor report the damage actually applied by combat");

            target.Team = 0; rounds.Add(Round(target.Position, 0, 13));
            guns.Step(1f / 60f, rounds, aircraft, previous, terrain, events);
            check(guns.PlayerAircraftHits == 3, "Aircraft feedback: rounds intersecting a friendly cannot produce hostile-hit feedback");

            rounds.Clear(); events.Clear();
            var truck = new GroundTarget { Id = 100, Team = 1, Position = new Vector3(40, 1700, 300),
                PreviousPosition = new Vector3(40, 1700, 300), Extents = new Vector3(3, 2, 6), Health = 39, MaxHealth = 39 };
            rounds.Add(Round(truck.Position, 0, 13));
            guns.Step(1f / 60f, rounds, aircraft, previous, terrain, events, new List<GroundTarget> { truck });
            check(truck.Health == 26 && guns.PlayerAircraftHits == 3 && events.Count == 1 && events[0].AircraftId == -1,
                "Aircraft feedback: real ground-objective hits retain their existing feedback without triggering the aircraft-only indicator");
        }

        static Projectile Round(Vector3 center, int owner, float damage)
        {
            return new Projectile { Position = center - Vector3.UnitZ * 5f,
                Velocity = Vector3.UnitZ * 760f, OwnerId = owner, Team = 0, Damage = damage };
        }
    }
}
