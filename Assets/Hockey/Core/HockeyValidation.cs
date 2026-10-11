using System;
using System.Numerics;
using System.Text;

namespace ColdSnap
{
    public static class HockeyValidation
    {
        public static string MatchSummary { get; private set; }
        public static void Run(Action<bool, string> check)
        {
            PhysicsValidation.Run(check);
            ValidateMatchFlow(check);
            ValidateCooperation(check);
            ValidateLoosePuckRecovery(check);
            ValidateReproducibility(check);
            ValidateMatches(check);
        }

        private static void ValidateLoosePuckRecovery(Action<bool, string> check)
        {
            Vector2[] loose = { new Vector2(13.82f, 0f), new Vector2(-13.82f, 0f), new Vector2(11.8f, 27.4f),
                new Vector2(-.388381f, -27.22284f), new Vector2(.388381f, 27.22284f), new Vector2(0f, 28.2f) };
            for (int test = 0; test < loose.Length; test++)
            {
                var match = new HockeySession(1988 + test, true);
                for (int i = 0; i < 140; i++) match.StepBots(1f / 60f);
                for (int i = 0; i < 3; i++)
                {
                    match.Skaters[i].Position = new Vector2(-6f + i * 6f, 0f);
                    match.Skaters[i].Velocity = Vector2.Zero;
                    match.Skaters[i].StickCooldown = 0f;
                }
                if (test >= 3)
                {
                    float side = Math.Sign(loose[test].Y);
                    match.Skaters[0].Position = new Vector2(-2.26f, side * 26.5f);
                    match.Skaters[1].Position = new Vector2(2.26f, side * 26.5f);
                }
                if (test == 5)
                {
                    match.Skaters[0].Position = new Vector2(-2.8f, 24f);
                    match.Skaters[1].Position = new Vector2(0f, 0f);
                }
                match.Puck.Position = loose[test]; match.Puck.Velocity = Vector2.Zero;
                match.Puck.ControllerId = -1; match.Puck.NoControlTime = 0f;
                match.Puck.LastTouchId = -1; match.Puck.LastTouchTeam = -1;
                bool recovered = false;
                for (int frame = 0; frame < 600; frame++)
                {
                    match.StepBots(1f / 60f);
                    if (match.Puck.LastTouchTeam == 0 && Vector2.Distance(match.Puck.Position, loose[test]) > 1f)
                    { recovered = true; break; }
                }
                check(recovered, "stationary puck " + test + ": AI physically recovers side-board/corner/behind-net puck in ten seconds");
                check(match.Stats.StuckResets == 0, "stationary puck " + test + ": recovery needs no whistle or position reset");
            }
        }

        private static void ValidateCooperation(Action<bool, string> check)
        {
            var match = new HockeySession(321);
            for (int i = 0; i < 140; i++) match.Step(default(HockeyInput), 1f / 60f);
            for (int i = 0; i < 6; i++)
            {
                match.Skaters[i].Position = new Vector2(10f, 12f + i);
                match.Skaters[i].Velocity = Vector2.Zero;
                match.Skaters[i].StickCooldown = 0f;
            }
            match.Player.Position = new Vector2(6f, 0f);
            match.Skaters[1].Position = new Vector2(0f, -5f);
            match.Skaters[1].Facing = 0f;
            match.Puck.Position = new Vector2(0f, -3.9f);
            match.Puck.Velocity = Vector2.Zero;
            match.Puck.NoControlTime = 0f;
            PuckPhysics.Guide(match.Puck, match.Skaters, 1f / 120f);
            match.Step(new HockeyInput { Call = true }, 1f / 60f);
            for (int i = 0; i < 50; i++) match.Step(default(HockeyInput), 1f / 60f);
            check(match.Stats.Passes[0] > 0, "teammate responds to human pass call through a clear physical lane");
            check(match.Puck.LastTouchId == 1 || match.Puck.Position.X > 1f, "called pass travels toward human support rather than teleporting possession");
        }

        private static void ValidateReproducibility(Action<bool, string> check)
        {
            var first = new HockeySession(815);
            var second = new HockeySession(815);
            for (int i = 0; i < 3600; i++) { first.StepBots(1f / 60f); second.StepBots(1f / 60f); }
            check(first.Puck.Position == second.Puck.Position && first.Puck.Velocity == second.Puck.Velocity,
                "identical seeds and bot inputs reproduce puck trajectory exactly");
            check(first.BlueScore == second.BlueScore && first.OrangeScore == second.OrangeScore && first.Stats.Shots[0] == second.Stats.Shots[0],
                "identical seeded matches reproduce scores and shot decisions");
            bool equal = true;
            for (int i = 0; i < 8; i++) equal &= first.Skaters[i].Position == second.Skaters[i].Position;
            check(equal, "all eight pilots remain deterministic across a repeated minute");
        }

        private static void ValidateMatchFlow(Action<bool, string> check)
        {
            var match = new HockeySession(1234);
            check(match.Skaters.Count == 8 && match.Player.Id == 0, "three skaters and goalie per team; stable human identity");
            check(match.Phase == MatchPhase.Faceoff, "match starts with readable faceoff countdown");
            float clock = match.TimeRemaining;
            match.Step(default(HockeyInput), 1f / 60f);
            check(match.TimeRemaining == clock && match.Puck.Position == Vector2.Zero, "faceoff freezes clock and physical play");
            for (int i = 0; i < 150; i++) match.Step(default(HockeyInput), 1f / 60f);
            check(match.Phase == MatchPhase.Playing && match.TimeRemaining < clock, "countdown starts live play and regulation clock");
            float before = match.TimeRemaining;
            match.Step(new HockeyInput { Call = true }, .01f);
            check(match.PassCallTime > 2f, "human can call for teammate pass");
            check(match.TimeRemaining < before, "live action advances match clock");

            var practice = new HockeySession(44, true);
            for (int i = 0; i < 600; i++) practice.Step(default(HockeyInput), 1f / 60f);
            check(practice.TimeRemaining == 180f && practice.Phase != MatchPhase.Final, "practice clock does not expire");
            check(practice.Skaters[3].Speed < .1f, "practice opposition skaters wait while goalkeeper remains active");

            var physicalGoal = new HockeySession(3);
            for (int i = 0; i < 140; i++) physicalGoal.Step(default(HockeyInput), 1f / 60f);
            for (int i = 0; i < 8; i++) physicalGoal.Skaters[i].Position = new Vector2(i % 2 == 0 ? -10f : 10f, -15f + i * 2f);
            physicalGoal.Puck.Position = new Vector2(0f, 24f); physicalGoal.Puck.Velocity = new Vector2(0f, 30f);
            physicalGoal.Puck.ControllerId = -1; physicalGoal.Puck.NoControlTime = 1f;
            for (int i = 0; i < 8; i++) physicalGoal.Step(default(HockeyInput), 1f / 60f);
            check(physicalGoal.BlueScore == 1 && physicalGoal.OrangeScore == 0 && physicalGoal.Phase == MatchPhase.Goal, "actual moving puck crossing goal line scores exactly once");
            clock = physicalGoal.TimeRemaining;
            for (int i = 0; i < 290; i++) physicalGoal.Step(default(HockeyInput), 1f / 60f);
            check(physicalGoal.Stats.Faceoffs == 2 && physicalGoal.Phase == MatchPhase.Playing, "goal celebration transitions through center faceoff back to play");
            check(physicalGoal.TimeRemaining > clock - .5f, "goal and faceoff pauses preserve regulation time");
        }

        private static void ValidateMatches(Action<bool, string> check)
        {
            var summary = new StringBuilder("Seeded full bot matches (all actions use player physics):\n");
            int goals = 0, shots = 0, saves = 0, passes = 0, turnovers = 0;
            bool blueWin = false, orangeWin = false, draw = false, overtime = false;
            int[] seeds = { 1988, 1944, 7, 93, 71319, 2026, 83, 501, 111 };
            for (int s = 0; s < seeds.Length; s++)
            {
                var match = new HockeySession(seeds[s]);
                bool finite = true, containment = true, control = true, roleSeparation = false;
                float maxPlayerSpeed = 0f;
                for (int frame = 0; frame < 21000 && match.Phase != MatchPhase.Final; frame++)
                {
                    match.StepBots(1f / 60f);
                    finite &= HockeyMath.Finite(match.Puck.Position) && HockeyMath.Finite(match.Puck.Velocity);
                    containment &= RinkPhysics.Inside(match.Puck.Position, .1f);
                    control &= match.Puck.ControllerId >= -1 && match.Puck.ControllerId < 6;
                    for (int i = 0; i < 8; i++)
                    {
                        finite &= HockeyMath.Finite(match.Skaters[i].Position) && HockeyMath.Finite(match.Skaters[i].Velocity);
                        maxPlayerSpeed = Math.Max(maxPlayerSpeed, match.Skaters[i].Speed);
                    }
                    if (match.Phase == MatchPhase.Playing && Vector2.Distance(match.Skaters[0].Position, match.Skaters[1].Position) > 5f
                        && Vector2.Distance(match.Skaters[1].Position, match.Skaters[2].Position) > 5f) roleSeparation = true;
                }
                int totalShots = match.Stats.Shots[0] + match.Stats.Shots[1];
                int totalPasses = match.Stats.Passes[0] + match.Stats.Passes[1];
                int totalSaves = match.Stats.Saves[0] + match.Stats.Saves[1];
                int totalTurnovers = match.Stats.Turnovers[0] + match.Stats.Turnovers[1];
                check(finite, "seed " + seeds[s] + ": finite state throughout full game");
                check(containment, "seed " + seeds[s] + ": puck remains in rounded rink");
                check(control, "seed " + seeds[s] + ": control never belongs to goalie or invalid id");
                check(match.Phase == MatchPhase.Final, "seed " + seeds[s] + ": full match reaches final score");
                check(totalShots > 3, "seed " + seeds[s] + ": AI creates repeated shot attempts");
                check(totalPasses > 1, "seed " + seeds[s] + ": AI passes between support players");
                check(totalTurnovers > 1, "seed " + seeds[s] + ": contested possession changes teams");
                check(roleSeparation, "seed " + seeds[s] + ": support roles spread across useful lanes");
                check(match.Stats.StuckResets == 0, "seed " + seeds[s] + ": no stuck-puck watchdog interventions; " + match.LastFrozenDiagnostic);
                check(maxPlayerSpeed <= 18.01f, "seed " + seeds[s] + ": all pilots respect bounded skating velocity");
                float finalClock = match.TimeRemaining;
                Vector2 finalPuck = match.Puck.Position;
                match.StepBots(.25f);
                check(finalClock == match.TimeRemaining && finalPuck == match.Puck.Position, "seed " + seeds[s] + ": final score freezes gameplay until restart");
                blueWin |= match.BlueScore > match.OrangeScore;
                orangeWin |= match.OrangeScore > match.BlueScore;
                draw |= match.BlueScore == match.OrangeScore;
                overtime |= match.Overtime;
                goals += match.BlueScore + match.OrangeScore; shots += totalShots; passes += totalPasses; saves += totalSaves; turnovers += totalTurnovers;
                summary.Append("seed ").Append(seeds[s]).Append(": ").Append(match.BlueScore).Append('-').Append(match.OrangeScore)
                    .Append(", shots ").Append(totalShots).Append(", saves ").Append(totalSaves).Append(", passes ").Append(totalPasses)
                    .Append(", takeaways ").Append(totalTurnovers).Append(", checks ").Append(match.Stats.Checks[0] + match.Stats.Checks[1])
                    .Append(", frozen-puck resets ").Append(match.Stats.StuckResets).Append(", elapsed ").Append(match.Time.ToString("F1")).Append("s\n");
            }
            check(goals >= 8, "bot series produces earned goals through physical shooting (" + goals + " goals from " + shots + " attempts)");
            check(saves >= 8, "bot series includes physical goalkeeper saves and rebounds (" + saves + " saves)");
            check(shots > goals * 2, "shooting has misses/saves rather than automatic goals");
            check(blueWin && orangeWin && draw && overtime, "full bot series exercises blue win, orange win, overtime and draw outcomes");
            summary.Append("TOTAL goals ").Append(goals).Append(", shots ").Append(shots).Append(", saves ").Append(saves).Append(", passes ").Append(passes).Append(", takeaways ").Append(turnovers);
            MatchSummary = summary.ToString();
        }
    }
}
