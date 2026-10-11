using System;
using System.Collections.Generic;
using System.Numerics;

namespace ColdSnap
{
    public enum MatchPhase { Faceoff, Playing, Goal, Final }

    public sealed class MatchStats
    {
        public readonly int[] Shots = new int[2];
        public readonly int[] Saves = new int[2];
        public readonly int[] Passes = new int[2];
        public readonly int[] Checks = new int[2];
        public readonly int[] Turnovers = new int[2];
        public readonly int[] Goals = new int[2];
        public readonly int[,] ShotTypes = new int[2, 4];
        public readonly int[] OneTimers = new int[2];
        public readonly int[] OneTimersBySkater = new int[6];
        public int Faceoffs;
        public int StuckResets;
        public int OutOfPlay;
    }

    /// <summary>Scene-independent, fixed-step hockey rules. All pilots submit the same bounded inputs.</summary>
    public sealed class HockeySession
    {
        public readonly int Seed;
        public readonly bool Practice;
        public readonly List<SkaterState> Skaters = new List<SkaterState>(8);
        public readonly SkaterState Player;
        public readonly PuckState Puck = new PuckState();
        public readonly MatchStats Stats = new MatchStats();
        public int BlueScore { get; private set; }
        public int OrangeScore { get; private set; }
        public float TimeRemaining { get; private set; } = 180f;
        public bool Overtime { get; private set; }
        public MatchPhase Phase { get; private set; }
        public float PhaseTime { get; private set; }
        public float Time { get; private set; }
        public string LastMessage { get; private set; }
        public float MessageTime { get; private set; }
        public string Result { get; private set; } = "Match in progress";
        public float PassCallTime { get; private set; }
        public int LastGoalTeam { get; private set; } = -1;
        public string LastFrozenDiagnostic { get; private set; } = "";
        private readonly HockeyAI[] pilots = new HockeyAI[8];
        private readonly HockeyInput[] inputs = new HockeyInput[8];
        private readonly bool[] wasOneTimerHeld = new bool[8];
        private readonly bool[] oneTimerArmed = new bool[8];
        private readonly bool[] oneTimerConsumed = new bool[8];
        private int lastControlTeam = -1;
        private int lastCollisionSerial;
        private int pendingShotTeam = -1;
        private float pendingShotTime;
        private float stuckTime;
        private Vector2 lastActivePuck;

        public HockeySession(int seed, bool practice = false)
        {
            Seed = seed;
            Practice = practice;
            Puck.ShotSequence = seed;
            for (int i = 0; i < 8; i++)
            {
                int team = i < 3 || i == 6 ? 0 : 1;
                var skater = new SkaterState { Id = i, Team = team, Goalie = i >= 6,
                    Role = i >= 6 ? "Goalkeeper" : i % 3 == 2 ? "Anchor" : i % 3 == 1 ? "Wing" : "Center" };
                Skaters.Add(skater);
                pilots[i] = new HockeyAI(unchecked(seed * 137 + i * 977), i);
            }
            Player = Skaters[0];
            BeginFaceoff();
            Message(practice ? "Open ice. Skate, pass and shoot; the clock is stopped." : "Blue attacks the orange end. Three minutes. Make your shift count.");
        }

        public void Step(HockeyInput input, float dt) { Advance(input, dt, false); }
        public void StepBots(float dt) { Advance(default(HockeyInput), dt, true); }

        private void Advance(HockeyInput playerInput, float dt, bool autoPlayer)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f || Phase == MatchPhase.Final) return;
            dt = Math.Min(dt, .25f);
            while (dt > .000001f)
            {
                float step = Math.Min(dt, 1f / 120f);
                Tick(playerInput, step, autoPlayer);
                // One button press is one action even when a caller submits a long step.
                playerInput.Shoot = false; playerInput.Pass = false; playerInput.Check = false; playerInput.Call = false;
                dt -= step;
                if (Phase == MatchPhase.Final) break;
            }
        }

        private void Tick(HockeyInput playerInput, float dt, bool autoPlayer)
        {
            Time += dt;
            MessageTime = Math.Max(0f, MessageTime - dt);
            PassCallTime = Math.Max(0f, PassCallTime - dt);
            pendingShotTime = Math.Max(0f, pendingShotTime - dt);
            if (pendingShotTime <= 0f) pendingShotTeam = -1;
            if (Phase != MatchPhase.Playing)
            {
                PhaseTime -= dt;
                if (PhaseTime <= 0f)
                {
                    if (Phase == MatchPhase.Goal)
                    {
                        if (Overtime) Finish(); else BeginFaceoff();
                    }
                    else
                    {
                        Phase = MatchPhase.Playing;
                        Message("Puck down. Play!");
                    }
                }
                return;
            }
            if (playerInput.Call) { PassCallTime = 2.5f; Message("Stick raised: calling for the puck."); }
            if (!Practice)
            {
                TimeRemaining = Math.Max(0f, TimeRemaining - dt);
                if (TimeRemaining <= 0f)
                {
                    if (!Overtime && BlueScore == OrangeScore)
                    {
                        Overtime = true; TimeRemaining = 60f; BeginFaceoff(); Message("Sudden death. Next goal wins; one extra minute.");
                    }
                    else Finish();
                    return;
                }
            }

            for (int i = 0; i < Skaters.Count; i++)
            {
                SkaterState skater = Skaters[i];
                if (skater == Player && !autoPlayer) inputs[i] = playerInput;
                else if (Practice && skater.Team == 1 && !skater.Goalie)
                    inputs[i] = new HockeyInput { Brake = true, Aim = skater.Forward };
                else inputs[i] = pilots[i].Think(this, skater, dt);
                Skating.Step(skater, inputs[i], dt);
            }
            RinkPhysics.ResolvePlayers(Skaters);

            for (int i = 0; i < Skaters.Count; i++)
            {
                SkaterState skater = Skaters[i];
                if (skater.Goalie) continue;
                HockeyInput input = inputs[i];
                bool releasedAfterOneTimer = !input.OneTimerHeld && wasOneTimerHeld[i] && oneTimerConsumed[i];
                if (!input.OneTimerHeld)
                { oneTimerArmed[i] = false; oneTimerConsumed[i] = false; }
                else if (!wasOneTimerHeld[i])
                {
                    // Readying the stick while already controlling a puck remains a normal charged shot.
                    oneTimerArmed[i] = Puck.ControllerId != skater.Id && !PuckPhysics.CanStrike(skater, Puck);
                    oneTimerConsumed[i] = false;
                }
                wasOneTimerHeld[i] = input.OneTimerHeld;
                if (releasedAfterOneTimer) input.Shoot = false;
                Vector2 relativeArrival = Puck.Velocity - skater.Velocity;
                Vector2 towardSkater = skater.Position - Puck.Position;
                bool arrives = relativeArrival.LengthSquared() > 1.25f * 1.25f && Vector2.Dot(relativeArrival, towardSkater) > .1f;
                bool oneTimer = input.OneTimerHeld && oneTimerArmed[i] && !oneTimerConsumed[i]
                    && Puck.ControllerId != skater.Id && arrives && PuckPhysics.CanStrike(skater, Puck);
                if (oneTimer)
                {
                    input.Shoot = true; input.Pass = false; input.ShotKind = HockeyShotKind.OneTimer;
                    input.ShotPower = Math.Max(.65f, input.ShotPower);
                }
                if (input.Check) TryCheck(skater, input.Aim);
                if (input.Pass || input.Shoot)
                {
                    input.Aim = input.Aim.LengthSquared() > .01f ? Vector2.Normalize(input.Aim) : skater.Forward;
                    input.ShotPower = Math.Max(.1f, Math.Min(1f, input.ShotPower));
                    if (PuckPhysics.Strike(skater, Puck, input))
                    {
                        if (input.Pass) { Stats.Passes[skater.Team]++; if (skater == Player) Message("Pass away. Find the return lane."); }
                        else
                        {
                            Stats.Shots[skater.Team]++; pendingShotTeam = skater.Team; pendingShotTime = 2.5f;
                            Stats.ShotTypes[skater.Team, (int)input.ShotKind]++;
                            if (oneTimer)
                            {
                                Stats.OneTimers[skater.Team]++;
                                Stats.OneTimersBySkater[skater.Id]++;
                                oneTimerConsumed[i] = true; oneTimerArmed[i] = false;
                            }
                            if (skater == Player) Message(oneTimer ? "One-timer!" : input.ShotKind + " shot away.");
                        }
                    }
                }
            }
            int controller = PuckPhysics.Guide(Puck, Skaters, dt);
            if (controller >= 0)
            {
                int team = Skaters[controller].Team;
                if (lastControlTeam >= 0 && lastControlTeam != team) Stats.Turnovers[team]++;
                lastControlTeam = team;
                pendingShotTeam = -1;
            }
            float puckSpeed = Puck.Velocity.Length();
            int goal = PuckPhysics.Step(Puck, Skaters, dt);
            if (Puck.CollisionSerial != lastCollisionSerial)
            {
                lastCollisionSerial = Puck.CollisionSerial;
                if (Puck.LastCollisionId >= 0 && Puck.LastCollisionId < Skaters.Count)
                {
                    SkaterState hit = Skaters[Puck.LastCollisionId];
                    if (hit.Goalie && puckSpeed > 6f && pendingShotTeam >= 0 && hit.Team != pendingShotTeam)
                    {
                        Stats.Saves[hit.Team]++;
                        pendingShotTeam = -1;
                        Message(hit.Team == 0 ? "Blue keeper turns it away. Clear the rebound!" : "Saved. The rebound is live!");
                    }
                }
            }
            if (goal >= 0) { AwardGoal(goal); return; }
            if (Puck.OutOfPlay)
            { Stats.OutOfPlay++; BeginFaceoff(); Message("Puck over the glass. Center faceoff."); return; }
            DiagnosePuck(dt);
        }

        private void TryCheck(SkaterState checker, Vector2 aim)
        {
            if (checker.CheckCooldown > 0f || checker.Stun > 0f) return;
            checker.CheckCooldown = .85f;
            aim = aim.LengthSquared() > .01f ? Vector2.Normalize(aim) : checker.Forward;
            for (int i = 0; i < Skaters.Count; i++)
            {
                SkaterState other = Skaters[i];
                if (other.Team == checker.Team || other.Goalie) continue;
                Vector2 offset = other.Position - checker.Position;
                float d = offset.Length();
                if (d > 1.75f || (d > .1f && Vector2.Dot(offset / d, aim) < .4f)) continue;
                Vector2 direction = d > .1f ? offset / d : aim;
                float strength = 2.1f + Math.Min(2.2f, checker.Velocity.Length() * .3f);
                other.Velocity += direction * strength;
                other.Stun = .48f;
                checker.Velocity *= .72f;
                Stats.Checks[checker.Team]++;
                if (Puck.Height <= .55f && (Puck.ControllerId == other.Id || Vector2.Distance(Puck.Position, other.Position) < 1.8f))
                {
                    Puck.Velocity += direction * 5.2f;
                    Puck.ControllerId = -1; Puck.NoControlTime = .35f;
                }
                if (checker == Player) Message("Clean body check. Chase the loose puck.");
                break;
            }
            if (Puck.Height <= .55f && Vector2.Distance(Puck.Position, checker.Position) < 2f && Puck.ControllerId != checker.Id)
            {
                Puck.Velocity += aim * 3.5f;
                Puck.ControllerId = -1; Puck.NoControlTime = .2f;
            }
        }

        private void AwardGoal(int team)
        {
            Stats.Goals[team]++;
            if (team == 0) BlueScore++; else OrangeScore++;
            LastGoalTeam = team;
            Phase = MatchPhase.Goal; PhaseTime = 2.5f;
            Puck.ControllerId = -1;
            Message(team == 0 ? "BLUE GOAL!" : "ORANGE GOAL!");
        }

        private void BeginFaceoff()
        {
            Phase = MatchPhase.Faceoff; PhaseTime = 2.2f;
            Stats.Faceoffs++;
            Puck.Position = Vector2.Zero; Puck.Velocity = Vector2.Zero;
            Puck.Height = .05f; Puck.VerticalVelocity = 0f; Puck.OutOfPlay = false;
            Puck.LastStrikeWasPass = false; Puck.LastPasserId = -1; Puck.LastPassTeam = -1; Puck.PassAge = 0f;
            Puck.ControllerId = -1; Puck.NoControlTime = 0f; Puck.ControlTime = 0f;
            Puck.LastTouchId = -1; Puck.LastTouchTeam = -1;
            lastControlTeam = -1; stuckTime = 0f; lastActivePuck = Vector2.Zero;
            pendingShotTeam = -1; pendingShotTime = 0f;
            for (int i = 0; i < Skaters.Count; i++)
            {
                SkaterState skater = Skaters[i];
                float direction = skater.Team == 0 ? 1f : -1f;
                int slot = skater.Id % 3;
                skater.Position = skater.Goalie ? new Vector2(0f, -direction * 23.7f)
                    : slot == 0 ? new Vector2((Stats.Faceoffs % 2 == 0 ? .25f : -.25f) * direction, -direction * 1.9f)
                    : new Vector2(slot == 1 ? -6.5f : 6.5f, -direction * (slot == 1 ? 5f : 8f));
                if (Practice && skater.Team == 1 && !skater.Goalie) skater.Position = new Vector2(11.7f, 8f + slot * 4f);
                skater.Velocity = Vector2.Zero;
                skater.Facing = skater.Team == 0 ? 0f : (float)Math.PI;
                skater.Cooldown = 0f; skater.StickCooldown = 0f; skater.CheckCooldown = 0f; skater.Stun = 0f;
                skater.GoalieHandHeight = 1.05f; skater.GoalieHandReach = .45f;
                skater.Intent = skater.Goalie ? "Track angle" : "Faceoff";
                pilots[i].Reset();
                wasOneTimerHeld[i] = false; oneTimerArmed[i] = false; oneTimerConsumed[i] = false;
            }
        }

        private void DiagnosePuck(float dt)
        {
            // A dead puck is openly whistled and reset, never silently assigned or moved during play.
            if (Vector2.DistanceSquared(lastActivePuck, Puck.Position) > .45f * .45f || Math.Abs(Puck.VerticalVelocity) > .2f)
            { lastActivePuck = Puck.Position; stuckTime = 0f; }
            else stuckTime += dt;
            if (stuckTime > 18f && Puck.Velocity.LengthSquared() < .16f)
            {
                LastFrozenDiagnostic = "time=" + Time.ToString("F2") + ", puck=" + Puck.Position + ", velocity=" + Puck.Velocity + ", controller=" + Puck.ControllerId;
                for (int i = 0; i < 6; i++) LastFrozenDiagnostic += "; skater " + i + " pos=" + Skaters[i].Position + ", facing=" + Skaters[i].Facing.ToString("F2") + ", " + Skaters[i].Intent;
                Stats.StuckResets++; BeginFaceoff(); Message("Puck stranded. Center faceoff.");
            }
        }

        private void Finish()
        {
            Phase = MatchPhase.Final; PhaseTime = 0f;
            Result = BlueScore > OrangeScore ? "BLUE WINS" : OrangeScore > BlueScore ? "ORANGE WINS" : "HONORS EVEN";
            Message(Result + " — " + BlueScore + " : " + OrangeScore);
        }

        private void Message(string message) { LastMessage = message; MessageTime = 3.5f; }
    }
}
