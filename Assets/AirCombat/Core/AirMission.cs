using System;
using System.Collections.Generic;
using System.Numerics;

namespace Meridian
{
    public enum MissionKind { Duel, ConvoyCover }
    public enum MissionOutcome { Flying, Victory, Defeat }

    /// <summary>One isolated sortie. The Unity shell owns input, presentation, and lifetime.</summary>
    public sealed class AirMission
    {
        public const float EscortDuration = 300f;
        public readonly int Seed;
        public readonly MissionKind Kind;
        public readonly TerrainField Terrain;
        public readonly List<AircraftState> Aircraft = new List<AircraftState>();
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        public readonly List<CombatEvent> Events = new List<CombatEvent>();
        public readonly AircraftState Player;
        public readonly AircraftState Courier;
        private readonly Dictionary<int, PilotAI> pilots = new Dictionary<int, PilotAI>();
        private readonly Dictionary<int, Vector3> previousPositions = new Dictionary<int, Vector3>();
        private readonly HashSet<int> announcedLosses = new HashSet<int>();
        private readonly Weapons weapons;
        private readonly Vector3 courierDestination;
        public MissionOutcome Outcome { get; private set; }
        public float Time { get; private set; }
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public int Hits { get { return weapons.PlayerHits; } }
        public int RoundsFired { get { return weapons.PlayerRounds; } }
        public int TotalRoundsFired { get { return weapons.TotalRounds; } }
        public string Summary { get; private set; }
        public string LastMessage { get; private set; }
        public float MessageTime { get; private set; }
        public int EnemyRemaining
        {
            get { int count = 0; for (int i = 0; i < Aircraft.Count; i++) if (Aircraft[i].Team == 1 && Aircraft[i].Alive) count++; return count; }
        }
        public string Objective
        {
            get { return Kind == MissionKind.Duel ? "Destroy the enemy fighter. Return alive."
                : "Protect courier Lark for five minutes, or destroy both interceptors."; }
        }
        public float EscortProgress { get { return Math.Min(1f, Time / EscortDuration); } }

        public AirMission(int seed, MissionKind kind)
        {
            Seed = seed; Kind = kind;
            Terrain = new TerrainField(seed);
            weapons = new Weapons(unchecked(seed ^ 0x48BA621D));
            Random random = new Random(seed);
            float heading = ((float)random.NextDouble() - .5f) * 1.1f;
            Vector3 ahead = new Vector3((float)Math.Sin(heading), 0f, (float)Math.Cos(heading));
            Vector3 right = new Vector3(ahead.Z, 0f, -ahead.X);
            Player = Create(0, AircraftRole.Player, AircraftDefinition.AlliedFighter, new Vector3(0f, 1100f, 0f), heading, 116f, 0, "Kestrel");
            Player.Ammo = 1050;
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
        { PilotAI pilot; return pilots.TryGetValue(id, out pilot) ? pilot.State : "Player"; }

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
            MessageTime = Math.Max(0f, MessageTime - dt);
            for (int i = Events.Count - 1; i >= 0; i--)
            { Events[i].Age += dt; if (Events[i].Age >= Events[i].Lifetime) Events.RemoveAt(i); }
            for (int i = 0; i < Aircraft.Count; i++) previousPositions[Aircraft[i].Id] = Aircraft[i].Position;
            for (int i = 0; i < Aircraft.Count; i++)
            {
                AircraftState state = Aircraft[i];
                if (!state.Alive) continue;
                FlightInput input = state.IsPlayer ? playerInput : pilots[state.Id].Update(state, Aircraft, Terrain, courierDestination, dt);
                FlightDynamics.Step(state, input, Terrain, dt);
                weapons.Fire(state, input.Fire, dt, Projectiles);
            }
            weapons.Step(dt, Projectiles, Aircraft, previousPositions, Terrain, Events);
            for (int i = 0; i < Aircraft.Count; i++)
            {
                AircraftState state = Aircraft[i];
                if (state.Alive || !announcedLosses.Add(state.Id)) continue;
                Events.Add(new CombatEvent(state.Position, CombatEventKind.Destroyed));
                if (state.Team == 1)
                { Kills++; Score += 1000; Message("Enemy aircraft destroyed. " + EnemyRemaining + " hostile remaining."); }
            }
            if (!Player.Alive) Finish(MissionOutcome.Defeat, "Kestrel lost. Your sortie has ended.");
            else if (Courier != null && !Courier.Alive) Finish(MissionOutcome.Defeat, "Lark was shot down. The reconnaissance plates are lost.");
            else if (EnemyRemaining == 0) Finish(MissionOutcome.Victory, Kind == MissionKind.Duel
                ? "Airspace clear. You won the duel." : "Both interceptors destroyed. Lark is clear to continue.");
            else if (Kind == MissionKind.ConvoyCover && Time >= EscortDuration)
                Finish(MissionOutcome.Victory, "Lark reached friendly air cover. The reconnaissance plates are safe.");
            else if (Time >= 720f) Finish(MissionOutcome.Defeat, "Fuel reserve exhausted. The patrol was recalled before the objective was secured.");
            else if (Player.Ammo == 0 && Kind == MissionKind.Duel && Time > 30f)
                MessageOnce("Guns empty. Use the pause menu to restart or return to the collection.");
        }

        private void Finish(MissionOutcome outcome, string summary)
        {
            Outcome = outcome; Summary = summary;
            if (outcome == MissionOutcome.Victory)
                Score += 500 + (int)Math.Max(0f, Player.Health * 4f) + (Courier != null && Courier.Alive ? 500 : 0);
            Score += Hits * 15;
            Message(summary);
        }
        private void Message(string text) { LastMessage = text; MessageTime = 7f; }
        private void MessageOnce(string text) { if (LastMessage != text) Message(text); }
    }
}
