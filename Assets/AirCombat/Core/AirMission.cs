using System;
using System.Collections.Generic;
using System.Numerics;

namespace Meridian
{
    public enum MissionKind { Duel, ConvoyCover, Interception, BomberEscort, GroundAttack, AceHunt, FleetBattle }
    public enum MissionOutcome { Flying, Victory, Defeat }

    /// <summary>One isolated sortie. The Unity shell owns input, presentation, and lifetime.</summary>
    public sealed partial class AirMission
    {
        public const float EscortDuration = 300f;
        public readonly int Seed;
        public readonly MissionKind Kind;
        public readonly TerrainField Terrain;
        public readonly List<AircraftState> Aircraft = new List<AircraftState>();
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        public readonly List<CombatEvent> Events = new List<CombatEvent>();
        public readonly List<AircraftState> Wingmen = new List<AircraftState>();
        public readonly List<AircraftState> Bombers = new List<AircraftState>();
        public readonly List<AircraftState> Raiders = new List<AircraftState>();
        public readonly List<GroundTarget> GroundTargets = new List<GroundTarget>();
        public readonly AircraftState Player;
        public readonly AircraftState Courier;
        private readonly Dictionary<int, PilotAI> pilots = new Dictionary<int, PilotAI>();
        private readonly Dictionary<int, Vector3> previousPositions = new Dictionary<int, Vector3>();
        private readonly HashSet<int> announcedLosses = new HashSet<int>();
        private readonly HashSet<int> announcedGroundLosses = new HashSet<int>();
        private readonly HashSet<int> arrivedBombers = new HashSet<int>();
        private readonly HashSet<int> escapedRaiders = new HashSet<int>();
        private readonly Dictionary<int, PilotOrders> orders = new Dictionary<int, PilotOrders>();
        private readonly Weapons weapons;
        private readonly GroundDefense groundDefense;
        private PilotAI botPilot;
        private PilotOrders botOrders;
        private readonly Vector3 courierDestination;
        public Vector3 RouteStart { get; private set; }
        public Vector3 RouteDestination { get; private set; }
        public Vector3 ObjectivePosition { get; private set; }
        public SquadronOrder CurrentOrder { get; private set; }
        public int SelectedTargetId { get; private set; } = -1;
        public int BombersArrived { get { return arrivedBombers.Count; } }
        public int EnemyEscaped { get { return escapedRaiders.Count; } }
        public int GroundDestroyed { get { return announcedGroundLosses.Count; } }
        public int RequiredBombers { get { return Kind == MissionKind.BomberEscort ? 2 : 0; } }
        public int GroundObjectivesDestroyed
        { get { int n = 0; foreach (GroundTarget target in GroundTargets) if (target.IsObjective && !target.Alive) n++; return n; } }
        public int RequiredGroundTargets
        { get { int n = 0; foreach (GroundTarget target in GroundTargets) if (target.IsObjective) n++; return n; } }
        public int WingmenAlive
        { get { int n = 0; foreach (AircraftState plane in Wingmen) if (plane.Alive) n++; return n; } }
        public int BombersAlive
        { get { int n = 0; foreach (AircraftState plane in Bombers) if (plane.Alive) n++; return n; } }
        public int CollisionLosses { get; private set; }
        public bool Tactical { get { return Kind >= MissionKind.Interception; } }
        public MissionOutcome Outcome { get; private set; }
        public float Time { get; private set; }
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public int Hits { get { return weapons.PlayerHits; } }
        public int PlayerAircraftHits { get { return weapons.PlayerAircraftHits; } }
        public int PlayerAircraftKills { get { return weapons.PlayerAircraftKills; } }
        public float PlayerAircraftDamage { get { return weapons.PlayerAircraftDamage; } }
        public float LastPlayerAircraftDamage { get { return weapons.LastPlayerAircraftDamage; } }
        public int LastPlayerAircraftTargetId { get { return weapons.LastPlayerAircraftTargetId; } }
        public int RoundsFired { get { return weapons.PlayerRounds; } }
        public int TotalRoundsFired { get { return weapons.TotalRounds; } }
        public int GroundRoundsFired
        { get { int rounds = 0; foreach (GroundTarget target in GroundTargets) rounds += target.ShotsFired; return rounds; } }
        public string Summary { get; private set; }
        public string LastMessage { get; private set; }
        public float MessageTime { get; private set; }
        public int EnemyRemaining
        {
            get { int count = 0; for (int i = 0; i < Aircraft.Count; i++) if (Aircraft[i].Team == 1 && Aircraft[i].Alive) count++; return count; }
        }
        public string Objective
        {
            get
            {
                switch (Kind)
                {
                    case MissionKind.Duel: return "Destroy the enemy fighter. Return alive.";
                    case MissionKind.ConvoyCover: return "Protect courier Lark for five minutes, or destroy both interceptors.";
                    case MissionKind.Interception: return "Stop both raid leaders before either reaches the defended boundary. Escort fighters are secondary.";
                    case MissionKind.BomberEscort: return "Deliver at least two of three bombers to friendly air cover. Stay close enough to protect them.";
                    case MissionKind.AceHunt: return "Defeat ace Voss and survive. His escorts are secondary targets; do not follow a low-energy climbing lure.";
                    case MissionKind.FleetBattle: return "Break the offensive: defeat " + RequiredFighterLosses + " enemy fighters and survive until every flight enters the sector. Blue flight supports you.";
                    default: return "Destroy all four convoy trucks. AAA and defending fighters are optional threats. Survive your strafing passes.";
                }
            }
        }
        public float EscortProgress { get { return Math.Min(1f, Time / EscortDuration); } }
        public float ObjectiveProgress
        {
            get
            {
                if (Kind == MissionKind.GroundAttack) return GroundObjectivesDestroyed / (float)Math.Max(1, RequiredGroundTargets);
                if (Kind == MissionKind.AceHunt) return AceAlive() ? 0f : 1f;
                if (Kind == MissionKind.FleetBattle) return Math.Min(1f, Kills / (float)Math.Max(1, RequiredFighterLosses))
                    * (EnemyFlightsEngaged / (float)TotalEnemyFlights);
                if (Kind == MissionKind.Interception)
                { int lost = 0; foreach (AircraftState plane in Raiders) if (!plane.Alive) lost++; return lost / (float)Math.Max(1, Raiders.Count); }
                if (Kind == MissionKind.BomberEscort)
                {
                    float progress = 0f;
                    foreach (AircraftState bomber in Bombers) progress += arrivedBombers.Contains(bomber.Id) ? 1f : bomber.Alive ? RouteFraction(bomber.Position) : 0f;
                    return Outcome == MissionOutcome.Victory ? 1f : Math.Min(1f, progress / Math.Max(1, Bombers.Count));
                }
                return Kind == MissionKind.ConvoyCover ? EscortProgress : EnemyRemaining == 0 ? 1f : 0f;
            }
        }
        public string ObjectiveStatus
        {
            get
            {
                if (Kind == MissionKind.BomberEscort) return BombersArrived + "/2 delivered | " + BombersAlive + "/3 bombers flying | route " + (int)(RouteFraction(BomberAnchor()) * 100f) + "%";
                if (Kind == MissionKind.GroundAttack) return GroundObjectivesDestroyed + "/" + RequiredGroundTargets + " trucks destroyed | " + (GroundTargets.Count - GroundDestroyed) + " ground contacts remain";
                if (Kind == MissionKind.Interception) return (int)(ObjectiveProgress * Raiders.Count) + "/" + Raiders.Count + " raiders stopped | " + EnemyEscaped + " escaped";
                if (Kind == MissionKind.AceHunt) return (AceAlive() ? "Ace Voss flying" : "Ace defeated") + " | " + EnemyRemaining + " enemy aircraft remain";
                if (Kind == MissionKind.FleetBattle) return Kills + "/" + RequiredFighterLosses + " victories | " + EnemyFlightsEngaged + "/" + TotalEnemyFlights + " flights engaged | " + EnemyRemaining + " airborne";
                return Kind == MissionKind.ConvoyCover ? "Lark " + (int)Math.Max(0, Courier.Health) + " | " + (int)Math.Max(0, EscortDuration - Time) + " seconds to cover" : EnemyRemaining + " hostile remaining";
            }
        }
        public string DebriefDetails
        { get { return ObjectiveStatus + "\nWingmen home: " + WingmenAlive + "/" + Wingmen.Count + " | Enemy aircraft lost: " + Kills + "\nPlayer rounds: " + RoundsFired + " | Confirmed impacts: " + Hits + " | Flak rounds: " + GroundRoundsFired; } }

        public AirMission(int seed, MissionKind kind, AirEncounterOptions encounter = null)
        {
            Seed = seed; Kind = kind;
            EncounterPressure = encounter == null ? 0 : Math.Max(0, Math.Min(2, encounter.Pressure));
            Terrain = new TerrainField(seed);
            weapons = new Weapons(unchecked(seed ^ 0x48BA621D));
            groundDefense = new GroundDefense(unchecked(seed ^ 0x174b39));
            Random random = new Random(seed);
            float heading = ((float)random.NextDouble() - .5f) * 1.1f;
            Vector3 ahead = new Vector3((float)Math.Sin(heading), 0f, (float)Math.Cos(heading));
            Vector3 right = new Vector3(ahead.Z, 0f, -ahead.X);
            Player = Create(0, AircraftRole.Player, AircraftDefinition.AlliedFighter, new Vector3(0f, 1100f, 0f), heading, 116f, 0, "Kestrel");
            Player.Ammo = 1050;
            CurrentOrder = SquadronOrder.FormUp;
            if (Tactical)
            {
                courierDestination = Vector3.Zero;
                BuildSquadron(heading, ahead, right);
                if (kind == MissionKind.Interception) BuildInterception(heading, ahead, right, random);
                else if (kind == MissionKind.BomberEscort) BuildEscort(heading, ahead, right, random);
                else if (kind == MissionKind.GroundAttack) BuildGroundAttack(heading, ahead, right, random);
                else BuildExpandedEncounter(heading, ahead, right);
                ConfigureOrders();
                ConfigureEncounter(heading, ahead, right);
                SelectNextTarget();
                Summary = "Sortie in progress";
                return;
            }
            if (kind == MissionKind.ConvoyCover)
            {
                Courier = Create(1, AircraftRole.Courier, AircraftDefinition.Courier,
                    Player.Position + ahead * 850f + right * 180f - Vector3.UnitY * 210f, heading, 88f, 0, "Lark");
                Courier.Health = 180f;
                courierDestination = Courier.Position + ahead * 31000f;
                CreateEnemy(2, Player.Position + ahead * 2100f + right * 800f + Vector3.UnitY * 200f, heading + 2.9f, seed);
                CreateEnemy(3, Player.Position + ahead * 2550f - right * 1000f + Vector3.UnitY * 360f, heading - 2.6f, unchecked(seed + 73));
                Message("Lark carries the reconnaissance plates. Keep the interceptors off her tail.");
            }
            else
            {
                courierDestination = Vector3.Zero;
                CreateEnemy(1, Player.Position + ahead * 1250f + right * 300f + Vector3.UnitY * 70f, heading + 2.75f, seed);
                Message("One hostile ahead. Build energy, lead your shots, and stay above the hills.");
            }
            Summary = "Sortie in progress";
        }

        private void BuildSquadron(float heading, Vector3 ahead, Vector3 right)
        {
            Player.Ammo = 1500;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                AircraftState plane = Create(i + 1, AircraftRole.Wingman, AircraftDefinition.AlliedFighter,
                    Player.Position - ahead * (180f + i * 100f) + right * (side * 170f) + Vector3.UnitY * (i == 0 ? 35f : -30f),
                    heading, 114f, 0, i == 0 ? "Rook" : "Finch");
                plane.Ammo = 1100; Wingmen.Add(plane);
                pilots[plane.Id] = new PilotAI(unchecked(Seed * 71 + plane.Id * 419));
            }
        }

        private void BuildInterception(float heading, Vector3 ahead, Vector3 right, Random random)
        {
            // A crossing intercept gives time to enter the raiders' stern quarter. The previous
            // head-on merge demanded an immediate 180-degree reversal before a long tail chase.
            float routeHeading = heading + .55f;
            Vector3 route = new Vector3((float)Math.Sin(routeHeading), 0f, (float)Math.Cos(routeHeading));
            Vector3 routeRight = new Vector3(route.Z, 0f, -route.X);
            RouteStart = Player.Position + ahead * 1800f - right * 1400f - Vector3.UnitY * 150f;
            RouteDestination = RouteStart + route * 35000f;
            ObjectivePosition = RouteDestination;
            for (int i = 0; i < 2; i++)
            {
                CreateEnemy(10 + i, RouteStart + routeRight * ((i == 0 ? -1f : 1f) * 310f) + route * i * 230f,
                    routeHeading, unchecked(Seed + i * 53));
                AircraftState raid = Aircraft[Aircraft.Count - 1];
                raid.Callsign = "Raid " + (i + 1); Raiders.Add(raid);
            }
            int escortCount = 1 + random.Next(2);
            for (int i = 0; i < escortCount; i++)
                CreateEnemy(12 + i, RouteStart + route * 1300f + routeRight * ((i == 0 ? -1f : 1f) * 1500f) + Vector3.UnitY * 380f,
                    routeHeading, unchecked(Seed + i * 127 + 23));
            Message("Raid crossing ahead from the left. Cut onto its track, then attack from behind. Send your wingmen after a raid leader; escorts are distractions.");
        }

        private void BuildEscort(float heading, Vector3 ahead, Vector3 right, Random random)
        {
            RouteStart = Player.Position + ahead * 1050f - Vector3.UnitY * 120f;
            RouteDestination = RouteStart + ahead * (18500f + random.Next(1600));
            ObjectivePosition = RouteDestination;
            for (int i = 0; i < 3; i++)
            {
                Vector3 offset = i == 0 ? Vector3.Zero : right * (i == 1 ? -230f : 230f) - ahead * (i == 1 ? 210f : 340f);
                AircraftState bomber = Create(20 + i, AircraftRole.Bomber, AircraftDefinition.Bomber,
                    RouteStart + offset, heading, 92f, 0, "Heron " + (i + 1));
                bomber.Health = 195f; bomber.Ammo = 0; Bombers.Add(bomber);
                pilots[bomber.Id] = new PilotAI(unchecked(Seed + 101 + i * 67), true);
            }
            for (int i = 0; i < 3; i++)
                CreateEnemy(10 + i, RouteStart + ahead * (3400f + i * 1350f)
                    + right * ((i % 2 == 0 ? -1f : 1f) * (1400f + random.Next(500))) + Vector3.UnitY * (250f + i * 140f),
                    heading + (float)Math.PI + (i - 1) * .16f, unchecked(Seed + i * 73));
            Message("Three Herons outbound. At least two must reach air cover. Kill counts cannot replace the bombers.");
        }

        private void BuildGroundAttack(float heading, Vector3 ahead, Vector3 right, Random random)
        {
            Vector3 centre = ahead * (3600f + random.Next(600)) + right * (random.Next(800) - 400);
            centre.Y = Terrain.Height(centre.X, centre.Z);
            ObjectivePosition = centre; RouteStart = centre; RouteDestination = centre + ahead * 2000f;
            for (int i = 0; i < 6; i++)
            {
                bool aaa = i >= 4;
                Vector3 position = aaa ? centre + right * (i == 4 ? -115f : 115f) + ahead * (i == 4 ? 80f : 280f)
                    : centre + ahead * (i * 82f) + right * ((i & 1) * 13f);
                var target = new GroundTarget { Id = 100 + i, Name = aaa ? "AAA " + (i - 3) : "Truck " + (i + 1),
                    Kind = aaa ? GroundTargetKind.AntiAircraft : GroundTargetKind.Truck, IsObjective = !aaa,
                    Extents = aaa ? new Vector3(3.2f, 1.8f, 3.2f) : new Vector3(3f, 2f, 6f),
                    Health = aaa ? 65f : 39f, MaxHealth = aaa ? 65f : 39f,
                    Heading = heading, Velocity = aaa ? Vector3.Zero : ahead * 2.4f };
                position.Y = Terrain.Height(position.X, position.Z) + target.Extents.Y;
                target.Position = target.PreviousPosition = position;
                target.FireDelay = i * .37f; GroundTargets.Add(target);
            }
            for (int i = 0; i < 2; i++)
                CreateEnemy(10 + i, centre + ahead * (2600f + i * 900f) + right * (i == 0 ? 1100f : -1600f)
                    + Vector3.UnitY * (1200f + i * 230f), heading + (float)Math.PI, unchecked(Seed + i * 73));
            Message("Four supply trucks on the valley road. Strafe, extend, and climb; the flak guns are optional but dangerous.");
        }

        private void ConfigureOrders()
        {
            for (int i = 0; i < Aircraft.Count; i++)
            {
                AircraftState plane = Aircraft[i];
                if (plane.IsPlayer) continue;
                var command = new PilotOrders { Order = CurrentOrder, ObjectiveAnchor = ObjectivePosition,
                    Destination = RouteDestination, ProtectedAssets = plane.Team == 0 ? Bombers : null, GroundTargets = GroundTargets,
                    CanStrafe = Kind == MissionKind.GroundAttack && plane.Team == 0, PreferBombers = Kind == MissionKind.BomberEscort && plane.Team == 1 };
                if (plane.Role == AircraftRole.Wingman)
                {
                    command.Leader = Player;
                    command.FormationOffset = new Vector3(plane.Id == 1 ? -170f : 170f, plane.Id == 1 ? 35f : -30f, plane.Id == 1 ? -180f : -280f);
                }
                if (plane.Role == AircraftRole.Bomber || Raiders.Contains(plane))
                {
                    command.FollowRoute = true;
                    Vector3 routeDirection = Vector3.Normalize(RouteDestination - RouteStart);
                    Vector3 offset = plane.Position - RouteStart;
                    // Aim beyond the completion plane: a point on the gate invites an orbit before crossing it.
                    command.Destination += routeDirection * 3000f + offset - routeDirection * Vector3.Dot(offset, routeDirection);
                    if (plane.Role == AircraftRole.Bomber && plane != Bombers[0])
                    {
                        command.Leader = Bombers[0];
                        command.FormationOffset = Vector3.Transform(plane.Position - Bombers[0].Position, Quaternion.Conjugate(Bombers[0].Rotation));
                    }
                }
                orders[plane.Id] = command;
            }
        }

        private AircraftState Create(int id, AircraftRole role, AircraftDefinition definition, Vector3 position,
            float heading, float speed, int team, string callsign)
        {
            var state = new AircraftState { Id = id, Role = role, Definition = definition, Position = position,
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, heading), Team = team, Callsign = callsign,
                Health = 100f, Ammo = 1800, Throttle = .82f, Alive = true };
            state.Velocity = state.Forward * speed;
            Aircraft.Add(state);
            if (role == AircraftRole.Courier) pilots[id] = new PilotAI(unchecked(Seed + 101), true);
            return state;
        }

        private void CreateEnemy(int id, Vector3 position, float heading, int seed)
        {
            Create(id, AircraftRole.Fighter, AircraftDefinition.AxisFighter, position, heading, 113f, 1, "Falke " + id);
            pilots[id] = new PilotAI(unchecked(seed * 397 + id * 193));
        }

        public string GetPilotState(int id)
        { PilotAI pilot; return pilots.TryGetValue(id, out pilot) ? pilot.State : id == 0 && botPilot != null ? botPilot.State : "Player"; }

        public PilotAI GetPilot(int id)
        { PilotAI pilot; return pilots.TryGetValue(id, out pilot) ? pilot : id == 0 ? botPilot : null; }

        public void IssueOrder(SquadronOrder order, int targetId = -1)
        {
            if (!Tactical || Outcome != MissionOutcome.Flying || !Enum.IsDefined(typeof(SquadronOrder), order)) return;
            CurrentOrder = order;
            if (IsHostileTarget(targetId)) SelectedTargetId = targetId;
            for (int i = 0; i < Wingmen.Count; i++)
            {
                PilotOrders command = orders[Wingmen[i].Id];
                command.Order = order; command.DesignatedTargetId = order == SquadronOrder.Engage ? SelectedTargetId : -1;
                command.Revision++;
            }
            string acknowledgment = order == SquadronOrder.FormUp ? "Forming on your wing."
                : order == SquadronOrder.Engage ? "Engaging your designated target."
                : order == SquadronOrder.CoverMe ? "Watching your tail." : "Breaking off. Rejoining your flight.";
            Message(WingmenAlive > 0 ? "Flight: " + acknowledgment : "No wingmen remain to acknowledge.");
        }

        public int SelectNextTarget()
        {
            // Stable ordering: aircraft first, then convoy. Acquisition is bounded to 10 km.
            int bestAfter = int.MaxValue, first = int.MaxValue;
            for (int i = 0; i < Aircraft.Count; i++)
            {
                AircraftState plane = Aircraft[i];
                if (!plane.Alive || plane.Team == Player.Team || Vector3.DistanceSquared(plane.Position, Player.Position) > 10000f * 10000f) continue;
                first = Math.Min(first, plane.Id);
                if (plane.Id > SelectedTargetId) bestAfter = Math.Min(bestAfter, plane.Id);
            }
            for (int i = 0; i < GroundTargets.Count; i++)
            {
                GroundTarget target = GroundTargets[i];
                if (!target.Alive || target.Team == Player.Team || Vector3.DistanceSquared(target.Position, Player.Position) > 10000f * 10000f) continue;
                first = Math.Min(first, target.Id);
                if (target.Id > SelectedTargetId) bestAfter = Math.Min(bestAfter, target.Id);
            }
            SelectedTargetId = bestAfter != int.MaxValue ? bestAfter : first != int.MaxValue ? first : -1;
            // Merely inspecting contacts does not silently change an existing attack command.
            return SelectedTargetId;
        }

        private bool IsHostileTarget(int id)
        {
            foreach (AircraftState plane in Aircraft) if (plane.Id == id && plane.Alive && plane.Team != Player.Team) return true;
            foreach (GroundTarget target in GroundTargets) if (target.Id == id && target.Alive && target.Team != Player.Team) return true;
            return false;
        }

        public void StepBots(float dt)
        {
            if (Outcome != MissionOutcome.Flying || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            if (botPilot == null)
            {
                botPilot = new PilotAI(unchecked(Seed * 19 + 613));
                botOrders = new PilotOrders { Order = SquadronOrder.Engage, ObjectiveAnchor = ObjectivePosition,
                    ProtectedAssets = Bombers, GroundTargets = GroundTargets, CanStrafe = Kind == MissionKind.GroundAttack,
                    CombatRadius = Kind == MissionKind.FleetBattle ? 5600f : 0f };
                if (Tactical) IssueOrder(SquadronOrder.Engage, BotObjectiveTarget());
            }
            float remaining = Math.Min(dt, .25f);
            while (remaining > .000001f && Outcome == MissionOutcome.Flying)
            {
                float tick = Math.Min(remaining, 1f / 60f);
                botOrders.ObjectiveAnchor = TacticalAnchor();
                int objectiveTarget = BotObjectiveTarget();
                if (botOrders.DesignatedTargetId != objectiveTarget)
                {
                    botOrders.DesignatedTargetId = objectiveTarget; botOrders.Revision++;
                    if (Tactical && objectiveTarget >= 0) IssueOrder(SquadronOrder.Engage, objectiveTarget);
                }
                FlightInput input = Tactical ? botPilot.Update(Player, Aircraft, Terrain, botOrders, tick)
                    : botPilot.Update(Player, Aircraft, Terrain, courierDestination, tick);
                Tick(input, tick); remaining -= tick;
            }
        }

        private int BotObjectiveTarget()
        {
            if (Kind == MissionKind.Interception)
            { foreach (AircraftState raider in Raiders) if (raider.Alive) return raider.Id; }
            if (Kind == MissionKind.GroundAttack)
            { foreach (GroundTarget target in GroundTargets) if (target.Alive && target.IsObjective) return target.Id; }
            if (Kind == MissionKind.AceHunt && AceAlive()) return AceId;
            return -1;
        }

        public void Step(FlightInput playerInput, float dt)
        {
            if (Outcome != MissionOutcome.Flying || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            // A lost rendering frame must not become one destructive physics step.
            float remaining = Math.Min(dt, .25f);
            while (remaining > .000001f)
            {
                float tick = Math.Min(remaining, 1f / 60f);
                Tick(playerInput, tick);
                remaining -= tick;
                if (Outcome != MissionOutcome.Flying) break;
            }
        }

        private void Tick(FlightInput playerInput, float dt)
        {
            Time += dt;
            AdvanceEncounter();
            MessageTime = Math.Max(0f, MessageTime - dt);
            for (int i = Events.Count - 1; i >= 0; i--)
            { Events[i].Age += dt; if (Events[i].Age >= Events[i].Lifetime) Events.RemoveAt(i); }
            for (int i = 0; i < Aircraft.Count; i++) previousPositions[Aircraft[i].Id] = Aircraft[i].Position;
            if (Tactical)
            {
                Vector3 anchor = TacticalAnchor();
                foreach (PilotOrders command in orders.Values) command.ObjectiveAnchor = anchor;
                foreach (GroundTarget target in GroundTargets) target.Step(dt, Terrain);
                groundDefense.Step(dt, GroundTargets, Aircraft, Projectiles);
            }
            for (int i = 0; i < Aircraft.Count; i++)
            {
                AircraftState state = Aircraft[i];
                if (!state.Alive) continue;
                FlightInput input = state.IsPlayer ? playerInput : Tactical
                    ? pilots[state.Id].Update(state, Aircraft, Terrain, orders[state.Id], dt)
                    : pilots[state.Id].Update(state, Aircraft, Terrain, courierDestination, dt);
                FlightDynamics.Step(state, input, Terrain, dt);
                weapons.Fire(state, input.Fire, dt, Projectiles);
            }
            weapons.Step(dt, Projectiles, Aircraft, previousPositions, Terrain, Events, GroundTargets);
            if (Tactical) CheckAircraftCollisions();
            for (int i = 0; i < Aircraft.Count; i++)
            {
                AircraftState state = Aircraft[i];
                if (state.Alive || !announcedLosses.Add(state.Id)) continue;
                Events.Add(new CombatEvent(state.Position, CombatEventKind.Destroyed));
                if (state.Team == 1)
                { Kills++; Score += 1000; Message("Enemy aircraft destroyed. " + EnemyRemaining + " hostile remaining."); }
                else if (state.Role == AircraftRole.Wingman) Message(state.Callsign + " is down. The flight has lost a pilot.");
                else if (state.Role == AircraftRole.Bomber) Message(state.Callsign + " lost. " + BombersAlive + " bombers remain.");
            }
            for (int i = 0; i < GroundTargets.Count; i++)
            {
                GroundTarget target = GroundTargets[i];
                if (target.Alive || !announcedGroundLosses.Add(target.Id)) continue;
                Score += target.IsObjective ? 750 : 350;
                Events.Add(new CombatEvent(target.Position, CombatEventKind.Destroyed));
                Message(target.Name + " destroyed. " + GroundObjectivesDestroyed + "/" + RequiredGroundTargets + " supply trucks disabled.");
            }
            if (!Player.Alive) Finish(MissionOutcome.Defeat, "Kestrel lost. Your sortie has ended.");
            else if (Tactical) EvaluateTacticalOutcome();
            else if (Courier != null && !Courier.Alive) Finish(MissionOutcome.Defeat, "Lark was shot down. The reconnaissance plates are lost.");
            else if (EnemyRemaining == 0) Finish(MissionOutcome.Victory, Kind == MissionKind.Duel
                ? "Airspace clear. You won the duel." : "Both interceptors destroyed. Lark is clear to continue.");
            else if (Kind == MissionKind.ConvoyCover && Time >= EscortDuration)
                Finish(MissionOutcome.Victory, "Lark reached friendly air cover. The reconnaissance plates are safe.");
            else if (Time >= 720f) Finish(MissionOutcome.Defeat, "Fuel reserve exhausted. The patrol was recalled before the objective was secured.");
            else if (Player.Ammo == 0 && Kind == MissionKind.Duel && Time > 30f)
                MessageOnce("Guns empty. Use the pause menu to restart or return to the collection.");
        }

        private Vector3 BomberAnchor()
        {
            Vector3 centre = Vector3.Zero; int alive = 0;
            foreach (AircraftState bomber in Bombers) if (bomber.Alive) { centre += bomber.Position; alive++; }
            return alive > 0 ? centre / alive : RouteDestination;
        }

        private Vector3 TacticalAnchor()
        {
            if (Kind == MissionKind.BomberEscort) return BomberAnchor();
            if (Kind == MissionKind.Interception)
            {
                // The briefing supplies a route and cruise estimate, not live omniscient enemy positions.
                Vector3 route = RouteDestination - RouteStart;
                float fraction = Math.Min(1f, Time * AircraftDefinition.AxisFighter.CruiseSpeed / Math.Max(1f, route.Length()));
                return RouteStart + route * fraction;
            }
            return ObjectivePosition;
        }

        private float RouteFraction(Vector3 position)
        {
            Vector3 route = RouteDestination - RouteStart; route.Y = 0f;
            Vector3 travel = position - RouteStart; travel.Y = 0f;
            return Math.Max(0f, Math.Min(1f, Vector3.Dot(travel, route) / Math.Max(1f, route.LengthSquared())));
        }

        private bool CrossedDestination(Vector3 position)
        {
            Vector3 direction = RouteDestination - RouteStart; direction.Y = 0f;
            return Vector3.Dot(position - RouteDestination, direction) >= 0f;
        }

        private void EvaluateTacticalOutcome()
        {
            if (Kind == MissionKind.Interception)
            {
                int remaining = 0;
                foreach (AircraftState raider in Raiders)
                {
                    if (!raider.Alive) continue;
                    remaining++;
                    if (CrossedDestination(raider.Position)) escapedRaiders.Add(raider.Id);
                }
                if (EnemyEscaped > 0) Finish(MissionOutcome.Defeat, "A raid leader crossed the defended boundary. The strike got through.");
                else if (remaining == 0) Finish(MissionOutcome.Victory, "Both raid leaders stopped. The defended sector is safe.");
            }
            else if (Kind == MissionKind.BomberEscort)
            {
                int possible = arrivedBombers.Count;
                foreach (AircraftState bomber in Bombers)
                {
                    if (!bomber.Alive || arrivedBombers.Contains(bomber.Id)) continue;
                    possible++;
                    if (CrossedDestination(bomber.Position))
                    {
                        arrivedBombers.Add(bomber.Id);
                        Message(bomber.Callsign + " reached friendly air cover. " + BombersArrived + "/2 delivered.");
                    }
                }
                if (possible < RequiredBombers) Finish(MissionOutcome.Defeat, "Too many bombers lost. Fewer than two can reach the destination.");
                else if (BombersArrived >= RequiredBombers) Finish(MissionOutcome.Victory, "Two Herons delivered to friendly air cover. The bomber formation completed its mission.");
            }
            else if (Kind == MissionKind.AceHunt)
            {
                if (!AceAlive()) Finish(MissionOutcome.Victory, "Ace Voss defeated. The enemy fighter leader will not return to this sector.");
            }
            else if (Kind == MissionKind.FleetBattle)
            {
                if (EnemyFlightsEngaged == TotalEnemyFlights && Kills >= RequiredFighterLosses)
                    Finish(MissionOutcome.Victory, "The enemy offensive is broken. Your two flights have won control of the sector.");
            }
            else if (GroundObjectivesDestroyed >= RequiredGroundTargets)
                Finish(MissionOutcome.Victory, "Supply convoy destroyed. The ground offensive has been delayed.");
            if (Outcome == MissionOutcome.Flying && Time >= (Kind == MissionKind.GroundAttack || Kind == MissionKind.FleetBattle ? 600f : 480f))
                Finish(MissionOutcome.Defeat, "Sortie fuel limit reached before the mission objective was secured.");
            if (Outcome == MissionOutcome.Flying && Player.Ammo == 0 && Time > 30f)
                MessageOnce("Kestrel guns empty. Surviving wingmen can continue; use Engage to direct the flight.");
        }

        private void CheckAircraftCollisions()
        {
            for (int a = 0; a < Aircraft.Count; a++)
            {
                AircraftState first = Aircraft[a];
                if (!first.Alive) continue;
                for (int b = a + 1; b < Aircraft.Count; b++)
                {
                    AircraftState second = Aircraft[b];
                    if (!second.Alive) continue;
                    // Conservative fuselage cores, swept through the frame. Close wingtip passes remain possible.
                    float radius = (first.Role == AircraftRole.Bomber ? 4f : 2.2f) + (second.Role == AircraftRole.Bomber ? 4f : 2.2f);
                    Vector3 start = previousPositions[first.Id] - previousPositions[second.Id];
                    Vector3 delta = first.Position - second.Position - start;
                    float t = Math.Max(0f, Math.Min(1f, -Vector3.Dot(start, delta) / Math.Max(.000001f, delta.LengthSquared())));
                    if ((start + delta * t).LengthSquared() >= radius * radius) continue;
                    first.Alive = second.Alive = false; first.Health = second.Health = 0f;
                    first.DestructionCause = second.DestructionCause = "Aircraft collision";
                    CollisionLosses += 2; break;
                }
            }
        }

        private void Finish(MissionOutcome outcome, string summary)
        {
            Outcome = outcome; Summary = summary;
            if (outcome == MissionOutcome.Victory)
                Score += 500 + (int)Math.Max(0f, Player.Health * 4f) + (Courier != null && Courier.Alive ? 500 : 0)
                    + WingmenAlive * 250 + BombersArrived * 500;
            Score += Hits * 15;
            Message(summary);
        }
        private void Message(string text) { LastMessage = text; MessageTime = 7f; }
        private void MessageOnce(string text) { if (LastMessage != text) Message(text); }
    }
}
