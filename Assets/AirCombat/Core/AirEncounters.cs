using System;
using System.Collections.Generic;
using System.Numerics;

namespace Meridian
{
    public sealed partial class AirMission
    {
        sealed class IncomingFlight
        {
            public float Arrival;
            public int Count;
            public bool Launched;
            public bool Engaged;
            public Vector3 Direction;
            public readonly List<int> AircraftIds = new List<int>();
        }
        readonly List<IncomingFlight> incomingFlights = new List<IncomingFlight>();
        readonly Dictionary<int, float> formationRelease = new Dictionary<int, float>();
        int nextEncounterId = 30, plannedEnemies;
        public int EncounterPressure { get; private set; }
        public int EnemyFlightsLaunched { get; private set; }
        public int EnemyFlightsEngaged { get; private set; }
        public int TotalEnemyFlights { get { return 1 + incomingFlights.Count; } }
        public int PendingEnemyFlights { get { return TotalEnemyFlights - EnemyFlightsLaunched; } }
        public int AceId { get; private set; } = -1;
        public int RequiredFighterLosses { get { return Kind == MissionKind.FleetBattle ? plannedEnemies / 2 + 1 : 0; } }
        public string EncounterSummary
        {
            get
            {
                if (Kind == MissionKind.AceHunt)
                    return "Voss leads " + (1 + Math.Min(2, 1 + EncounterPressure)) + " fighters. Stock airframes; disciplined energy fighting and gunnery.";
                if (Kind == MissionKind.FleetBattle)
                    return "Break the offensive: defeat " + RequiredFighterLosses + "/" + plannedEnemies
                        + " fighters and face all " + TotalEnemyFlights + " enemy flights. Blue flight supports your squadron.";
                return EncounterPressure > 0 ? "A fresh enemy flight will approach during the mission. No inflated enemy hull or weapon damage."
                    : Kind == MissionKind.Interception ? "Crossing intercept: cut onto the raid's diagonal track before committing to a tail chase."
                    : "Standard opposition.";
            }
        }

        void BuildExpandedEncounter(float heading, Vector3 ahead, Vector3 right)
        {
            RouteStart = Player.Position + ahead * 3000f;
            RouteDestination = RouteStart + ahead * 6000f;
            ObjectivePosition = RouteStart;
            if (Kind == MissionKind.AceHunt)
            {
                Vector3 start = Player.Position + ahead * 3150f + right * 900f
                    + Vector3.UnitY * (350f + EncounterPressure * 150f);
                CreateEnemy(10, start, heading + 2.75f, Seed);
                AircraftState leader = Aircraft[Aircraft.Count - 1];
                leader.Callsign = "ACE / Voss"; AceId = leader.Id;
                pilots[leader.Id] = new PilotAI(unchecked(Seed * 397 + 1930), false, PilotSkill.Ace);
                int escorts = Math.Min(2, 1 + EncounterPressure);
                for (int i = 0; i < escorts; i++)
                {
                    Vector3 offset = leader.Right * (i == 0 ? -380f : 380f) - leader.Forward * (330f + i * 220f)
                        - Vector3.UnitY * (80f + i * 90f);
                    CreateEnemy(11 + i, start + offset, heading + 2.75f, unchecked(Seed + i * 113 + 17));
                    Aircraft[Aircraft.Count - 1].Callsign = "Voss escort " + (i + 1);
                }
                plannedEnemies = escorts + 1;
                Message("Ace Voss is above the formation. Do not follow a steep climbing lure. Keep energy, coordinate your wingmen and defeat the ace; escorts are secondary.");
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    AircraftState friendly = Create(3 + i, AircraftRole.Fighter, AircraftDefinition.AlliedFighter,
                        Player.Position + ahead * (650f - i * 350f) + right * (1400f + i * 420f) + Vector3.UnitY * (120f + i * 75f),
                        heading, 114f, 0, "Blue flight " + (i + 1));
                    pilots[friendly.Id] = new PilotAI(unchecked(Seed * 37 + friendly.Id * 571));
                }
                for (int i = 0; i < 3; i++)
                {
                    CreateEnemy(10 + i, Player.Position + ahead * (3350f + i * 340f)
                        + right * (i == 0 ? -850f : i == 1 ? -420f : 720f) + Vector3.UnitY * (200f + i * 90f),
                        heading + (float)Math.PI, unchecked(Seed + i * 73));
                    Aircraft[Aircraft.Count - 1].Callsign = "Falcon " + (i + 1);
                }
                AddIncomingFlight(75f, 2, right);
                if (EncounterPressure > 0) AddIncomingFlight(150f, 2, -right);
                plannedEnemies = 3 + 2 * incomingFlights.Count;
                Message("Blue flight supports your squadron. Break the offensive: defeat a majority of the enemy and survive until every flight enters the sector.");
            }
        }

        void ConfigureEncounter(float heading, Vector3 ahead, Vector3 right)
        {
            EnemyFlightsLaunched = 1;
            EnemyFlightsEngaged = 1;
            if (Kind == MissionKind.AceHunt)
            {
                AircraftState leader = null;
                foreach (AircraftState plane in Aircraft) if (plane.Id == AceId) leader = plane;
                foreach (AircraftState plane in Aircraft)
                    if (plane.Team == 1 && plane.Id != AceId) FormEnemyFlight(plane, leader, 18f);
            }
            if (Kind == MissionKind.FleetBattle)
            {
                // Distinct initial assignments prevent an entire flight from singling out the human.
                foreach (AircraftState plane in Aircraft)
                {
                    if (plane.IsPlayer) continue;
                    orders[plane.Id].CombatRadius = 5600f;
                    if (plane.Team == 1) orders[plane.Id].DesignatedTargetId = plane.Id % 5;
                    else if (plane.Role != AircraftRole.Wingman) orders[plane.Id].DesignatedTargetId = plane.Id == 3 ? 11 : 12;
                }
            }
            if (EncounterPressure > 0 && (Kind == MissionKind.BomberEscort || Kind == MissionKind.GroundAttack))
                AddIncomingFlight(75f, EncounterPressure, right);
        }

        void AddIncomingFlight(float arrival, int count, Vector3 direction)
        {
            incomingFlights.Add(new IncomingFlight { Arrival = arrival, Count = count, Direction = direction });
        }

        void FormEnemyFlight(AircraftState plane, AircraftState leader, float release)
        {
            if (leader == null) return;
            PilotOrders command = orders[plane.Id];
            command.Leader = leader; command.Order = SquadronOrder.FormUp;
            command.FormationOffset = Vector3.Transform(plane.Position - leader.Position, Quaternion.Conjugate(leader.Rotation));
            formationRelease[plane.Id] = release;
        }

        void AdvanceEncounter()
        {
            foreach (IncomingFlight flight in incomingFlights)
            {
                if (flight.Launched || Time < flight.Arrival) continue;
                flight.Launched = true; EnemyFlightsLaunched++;
                Vector3 anchor = Kind == MissionKind.BomberEscort ? BomberAnchor()
                    : Kind == MissionKind.FleetBattle ? ObjectivePosition : Player.Position;
                Vector3 direction = flight.Direction;
                Vector3 start = anchor + direction * 8200f;
                // Reinforcements are born outside every friendly visual/combat bubble, never on a tail.
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    bool clear = true;
                    foreach (AircraftState friendly in Aircraft)
                        if (friendly.Alive && friendly.Team == 0 && Vector3.DistanceSquared(start, friendly.Position) < 7200f * 7200f)
                        { clear = false; break; }
                    if (clear) break;
                    start += direction * 2500f;
                }
                start.Y = Math.Max(anchor.Y + 450f, Terrain.Height(start.X, start.Z) + 900f);
                Vector3 toward = anchor - start;
                float heading = (float)Math.Atan2(toward.X, toward.Z);
                Vector3 right = new Vector3((float)Math.Cos(heading), 0f, -(float)Math.Sin(heading));
                AircraftState leader = null;
                for (int i = 0; i < flight.Count; i++)
                {
                    int id = nextEncounterId++;
                    flight.AircraftIds.Add(id);
                    CreateEnemy(id, start + right * (i * 460f) + direction * (i * 240f) - Vector3.UnitY * (i * 90f),
                        heading, unchecked(Seed + id * 211));
                    AircraftState plane = Aircraft[Aircraft.Count - 1];
                    plane.Callsign = (EnemyFlightsLaunched == 2 ? "Raven " : "Kite ") + (i + 1);
                    orders[id] = new PilotOrders { Order = SquadronOrder.Engage, ObjectiveAnchor = anchor,
                        CombatRadius = Kind == MissionKind.FleetBattle ? 5600f : 0f,
                        PreferBombers = Kind == MissionKind.BomberEscort, DesignatedTargetId = Kind == MissionKind.FleetBattle ? id % 5 : -1 };
                    if (leader == null) leader = plane; else FormEnemyFlight(plane, leader, Time + 22f);
                }
                Message("Enemy flight " + EnemyFlightsLaunched + "/" + TotalEnemyFlights + " reported on the flank. " + flight.Count + " fresh fighters approaching.");
            }
            foreach (IncomingFlight flight in incomingFlights)
            {
                if (!flight.Launched || flight.Engaged) continue;
                bool allLost = true, entered = false;
                foreach (AircraftState plane in Aircraft)
                {
                    if (!flight.AircraftIds.Contains(plane.Id) || !plane.Alive) continue;
                    allLost = false;
                    if (Vector3.DistanceSquared(plane.Position, ObjectivePosition) <= 5000f * 5000f) entered = true;
                }
                if (allLost || entered) { flight.Engaged = true; EnemyFlightsEngaged++; }
            }
            if (formationRelease.Count == 0) return;
            foreach (AircraftState plane in Aircraft)
            {
                float release;
                if (!plane.Alive || !formationRelease.TryGetValue(plane.Id, out release)) continue;
                PilotOrders command = orders[plane.Id];
                if (command.Order != SquadronOrder.FormUp) continue;
                bool engage = Time >= release || command.Leader == null || !command.Leader.Alive;
                foreach (AircraftState friendly in Aircraft)
                    if (friendly.Alive && friendly.Team != plane.Team && Vector3.DistanceSquared(plane.Position, friendly.Position) < 2800f * 2800f) engage = true;
                if (engage)
                {
                    command.Order = SquadronOrder.Engage; command.DesignatedTargetId = plane.Id % (Kind == MissionKind.FleetBattle ? 5 : 3); command.Revision++;
                    if (command.Leader != null && !command.Leader.Alive) command.Leader = null;
                }
            }
        }

        bool AceAlive()
        {
            foreach (AircraftState plane in Aircraft) if (plane.Id == AceId) return plane.Alive;
            return false;
        }
    }
}
