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
            ShotGestureValidation.Run(check);
            ValidateMatchFlow(check);
            ValidateDrawClock(check);
            ValidateCooperation(check);
            ValidateAirborneGoals(check);
            ValidateRoofRestWhistle(check);
            ValidateOneTimers(check);
            ValidateLoosePuckRecovery(check);
            ValidateReproducibility(check);
            ValidateMatches(check);
        }

        private static void ValidateRoofRestWhistle(Action<bool, string> check)
        {
            var roof = ContactFixture(940);
            roof.Puck.Position = new Vector2(0f, RinkPhysics.GoalLine + RinkPhysics.GoalDepth * .5f);
            roof.Puck.Velocity = Vector2.Zero;
            roof.Puck.Height = RinkPhysics.GoalHeight + RinkPhysics.PuckHalfHeight;
            roof.Puck.VerticalVelocity = 0f;
            bool restedOnRoof = false, resetToIce = false;
            for (int i = 0; i < 1260; i++)
            {
                roof.Step(default(HockeyInput), 1f / 60f);
                if (i == 120) restedOnRoof = roof.Puck.Height >= RinkPhysics.GoalHeight
                    && roof.Puck.Velocity.LengthSquared() < .01f && Math.Abs(roof.Puck.VerticalVelocity) < .2f;
                if (roof.Stats.StuckResets > 0)
                {
                    resetToIce = roof.Phase == MatchPhase.Faceoff && roof.Puck.Height == .05f
                        && roof.Puck.VerticalVelocity == 0f && roof.Puck.Position == Vector2.Zero;
                    break;
                }
            }
            check(restedOnRoof, "unreachable roof puck physically settles above the crossbar");
            check(roof.Stats.StuckResets == 1 && resetToIce,
                "stationary puck on goal roof is whistled within twenty-one seconds and reset to ice faceoff");
        }

        private static void ValidateDrawClock(Action<bool, string> check)
        {
            // Isolate clock rules by preventing all stick contacts, rather than relying on a lucky bot draw.
            var tied = new HockeySession(987);
            for (int frame = 0; frame < 24000 && tied.Phase != MatchPhase.Final; frame++)
            {
                for (int i = 0; i < tied.Skaters.Count; i++) tied.Skaters[i].Stun = 1f;
                tied.Step(default(HockeyInput), 1f / 60f);
            }
            check(tied.Overtime, "tied regulation clock always starts sudden-death overtime");
            check(tied.Phase == MatchPhase.Final && tied.BlueScore == 0 && tied.OrangeScore == 0 && tied.TimeRemaining == 0f,
                "scoreless overtime clock expires into a final draw without requiring a goal");
        }

        private static HockeySession ContactFixture(int seed)
        {
            var match = new HockeySession(seed, true);
            for (int i = 0; i < 140; i++) match.Step(default(HockeyInput), 1f / 60f);
            for (int i = 0; i < 8; i++)
            {
                match.Skaters[i].Position = new Vector2(-10f, -20f + i * 2f);
                match.Skaters[i].Velocity = Vector2.Zero;
                match.Skaters[i].Facing = 0f;
                match.Skaters[i].StickCooldown = 0f;
            }
            match.Puck.ControllerId = -1; match.Puck.NoControlTime = 0f;
            match.Puck.Height = .05f; match.Puck.VerticalVelocity = 0f;
            return match;
        }

        private static void ValidateAirborneGoals(Action<bool, string> check)
        {
            var high = ContactFixture(930);
            high.Puck.Position = new Vector2(.8f, 24f); high.Puck.Velocity = new Vector2(0f, 30f);
            high.Puck.Height = 1.85f; high.Puck.NoControlTime = 1f;
            for (int i = 0; i < 8; i++) high.Step(default(HockeyInput), 1f / 60f);
            check(high.BlueScore == 1 && high.Phase == MatchPhase.Goal, "legal airborne shot under raised crossbar scores through actual goal plane");
            for (int i = 0; i < 151; i++) high.Step(default(HockeyInput), 1f / 60f);
            check(high.Phase == MatchPhase.Faceoff && high.Puck.Height == .05f && high.Puck.VerticalVelocity == 0f,
                "faceoff clears airborne height and vertical momentum after goal");

            var over = ContactFixture(931);
            over.Puck.Position = new Vector2(.8f, 24f); over.Puck.Velocity = new Vector2(0f, 30f);
            over.Puck.Height = 2.6f; over.Puck.NoControlTime = 1f;
            for (int i = 0; i < 8; i++) over.Step(default(HockeyInput), 1f / 60f);
            check(over.BlueScore == 0 && over.OrangeScore == 0, "puck crossing goal plane above crossbar is not awarded a goal");

            var outOfPlay = ContactFixture(932);
            outOfPlay.Puck.Position = new Vector2(13.7f, 0f); outOfPlay.Puck.Velocity = new Vector2(25f, 0f);
            outOfPlay.Puck.Height = 3.8f;
            for (int i = 0; i < 6; i++) outOfPlay.Step(default(HockeyInput), 1f / 60f);
            check(outOfPlay.Stats.OutOfPlay == 1 && outOfPlay.Phase == MatchPhase.Faceoff && !outOfPlay.Puck.OutOfPlay,
                "puck over protective glass receives visible whistle and physical-state reset");
        }

        private static void ValidateOneTimers(Action<bool, string> check)
        {
            var match = ContactFixture(1001);
            match.Player.Position = new Vector2(0f, 12f);
            SkaterState passer = match.Skaters[1];
            passer.Position = new Vector2(-7f, 12f); passer.Facing = (float)Math.PI * .5f;
            match.Puck.Position = new Vector2(-5.93f, 12f); match.Puck.Velocity = Vector2.Zero;
            check(PuckPhysics.Strike(passer, match.Puck, new HockeyInput { Aim = Vector2.UnitX, Pass = true, ShotPower = 1f }),
                "one-timer fixture begins with an actual teammate stick pass");
            HockeyInput ready = new HockeyInput { Aim = Vector2.UnitY, OneTimerHeld = true, ShotPower = .8f,
                HasShotTarget = true, ShotTarget = new Vector2(5f, 20f), TargetHeight = 1.3f };
            for (int i = 0; i < 65 && match.Stats.OneTimers[0] == 0; i++) match.Step(ready, 1f / 120f);
            check(match.Stats.OneTimers[0] == 1 && match.Stats.ShotTypes[0, (int)HockeyShotKind.OneTimer] == 1,
                "pre-held stick redirects arriving physical teammate pass into one one-timer");
            check(match.Stats.OneTimersBySkater[match.Player.Id] == 1 && match.Stats.OneTimersBySkater[1] == 0,
                "human one-timer consumption is attributed to its skater, separately from teammate shots");
            check(match.Puck.VerticalVelocity > 0f, "one-timer applies physical upward shot impulse toward aimed height");
            int shots = match.Stats.Shots[0];
            match.Puck.Position = match.Player.Position + Vector2.UnitY;
            match.Puck.Velocity = -Vector2.UnitY * 8f;
            match.Puck.Height = .05f; match.Puck.VerticalVelocity = 0f;
            match.Puck.NoControlTime = 0f; match.Player.StickCooldown = 0f;
            for (int i = 0; i < 12; i++) match.Step(ready, 1f / 120f);
            check(match.Stats.Shots[0] == shots, "holding after a one-timer never repeats automatically on another close puck");
            ready.OneTimerHeld = false; ready.Shoot = true;
            match.Step(ready, 1f / 120f);
            check(match.Stats.Shots[0] == shots, "release after auto one-timer does not also fire a charged shot");

            var skating = ContactFixture(1002);
            skating.Player.Position = new Vector2(0f, 10f); skating.Player.Velocity = Vector2.UnitY * 5f;
            skating.Puck.Position = new Vector2(0f, 13f); skating.Puck.Velocity = Vector2.Zero;
            HockeyInput skateOnto = new HockeyInput { Aim = Vector2.UnitY, Move = Vector2.UnitY, OneTimerHeld = true,
                HasShotTarget = true, ShotTarget = new Vector2(1f, 25f), TargetHeight = 1.2f, ShotPower = .8f };
            for (int i = 0; i < 90 && skating.Stats.OneTimers[0] == 0; i++) skating.Step(skateOnto, 1f / 120f);
            check(skating.Stats.OneTimers[0] == 1, "skating onto a stationary loose puck with readied stick also produces a one-timer");

            var possessed = ContactFixture(1003);
            possessed.Player.Position = new Vector2(0f, 12f);
            possessed.Puck.Position = possessed.Player.Position + Vector2.UnitY;
            possessed.Puck.Velocity = Vector2.Zero;
            for (int i = 0; i < 30; i++) possessed.Step(skateOnto, 1f / 120f);
            check(possessed.Stats.OneTimers[0] == 0 && possessed.Stats.Shots[0] == 0,
                "holding the button while puck is already at the stick keeps a normal charged shot");

            var bot = ContactFixture(1004);
            SkaterState receiver = bot.Skaters[1]; receiver.Position = new Vector2(0f, 12f);
            passer = bot.Skaters[2]; passer.Position = new Vector2(-7f, 12f); passer.Facing = (float)Math.PI * .5f;
            bot.Puck.Position = new Vector2(-5.93f, 12f); bot.Puck.Velocity = Vector2.Zero;
            PuckPhysics.Strike(passer, bot.Puck, new HockeyInput { Aim = Vector2.UnitX, Pass = true, ShotPower = 1f });
            var pilot = new HockeyAI(1004, receiver.Id);
            HockeyInput decision = pilot.Think(bot, receiver, 1f / 120f);
            check(decision.OneTimerHeld && decision.HasShotTarget && decision.TargetHeight > .3f,
                "AI anticipates teammate pass and readies an aimed airborne one-timer");
            for (int i = 0; i < 90 && bot.Stats.OneTimers[0] == 0; i++) bot.Step(default(HockeyInput), 1f / 120f);
            check(bot.Stats.OneTimers[0] == 1, "AI physically completes a teammate-pass one-timer through the common session rules");
            check(bot.Stats.OneTimersBySkater[receiver.Id] == 1 && bot.Stats.OneTimersBySkater[bot.Player.Id] == 0,
                "teammate one-timer does not count as consumption of the human shoot gesture");
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
            check(first.Puck.Height == second.Puck.Height && first.Puck.VerticalVelocity == second.Puck.VerticalVelocity,
                "identical seeds also reproduce shot lift and airborne rebounds");
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
            int goals = 0, shots = 0, saves = 0, passes = 0, turnovers = 0, oneTimers = 0;
            bool blueWin = false, orangeWin = false;
            int[] seeds = { 1988, 1944, 7, 93, 71319, 2026, 83, 501, 111 };
            for (int s = 0; s < seeds.Length; s++)
            {
                var match = new HockeySession(seeds[s]);
                bool finite = true, containment = true, control = true, roleSeparation = false;
                string containmentDiagnostic = "";
                float maxPlayerSpeed = 0f, peakHeight = 0f;
                for (int frame = 0; frame < 21000 && match.Phase != MatchPhase.Final; frame++)
                {
                    match.StepBots(1f / 60f);
                    finite &= HockeyMath.Finite(match.Puck.Position) && HockeyMath.Finite(match.Puck.Velocity);
                    finite &= HockeyMath.Finite(match.Puck.Height) && HockeyMath.Finite(match.Puck.VerticalVelocity) && match.Puck.Height >= 0f;
                    peakHeight = Math.Max(peakHeight, match.Puck.Height);
                    if (!RinkPhysics.Inside(match.Puck.Position, .1f))
                    {
                        containment = false;
                        if (containmentDiagnostic.Length == 0) containmentDiagnostic = "time=" + match.Time + ", position=" + match.Puck.Position
                            + ", height=" + match.Puck.Height + ", vertical=" + match.Puck.VerticalVelocity + ", out=" + match.Puck.OutOfPlay;
                    }
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
                int totalOneTimers = match.Stats.OneTimers[0] + match.Stats.OneTimers[1];
                check(finite, "seed " + seeds[s] + ": finite state throughout full game");
                check(containment, "seed " + seeds[s] + ": puck remains in rounded rink; " + containmentDiagnostic);
                check(control, "seed " + seeds[s] + ": control never belongs to goalie or invalid id");
                check(match.Phase == MatchPhase.Final, "seed " + seeds[s] + ": full match reaches final score");
                check(totalShots > 3, "seed " + seeds[s] + ": AI creates repeated shot attempts");
                check(peakHeight > .4f, "seed " + seeds[s] + ": AI produces physical airborne shots");
                check(totalPasses > 1, "seed " + seeds[s] + ": AI passes between support players");
                check(totalTurnovers > 1, "seed " + seeds[s] + ": contested possession changes teams");
                check(roleSeparation, "seed " + seeds[s] + ": support roles spread across useful lanes");
                check(match.Stats.StuckResets == 0, "seed " + seeds[s] + ": no stuck-puck watchdog interventions; " + match.LastFrozenDiagnostic);
                check(maxPlayerSpeed <= 18.01f, "seed " + seeds[s] + ": all pilots respect bounded skating velocity");
                int countedShots = 0;
                for (int team = 0; team < 2; team++) for (int kind = 0; kind < 4; kind++) countedShots += match.Stats.ShotTypes[team, kind];
                int countedOneTimers = 0;
                for (int i = 0; i < 6; i++) countedOneTimers += match.Stats.OneTimersBySkater[i];
                check(countedShots == totalShots && totalOneTimers == match.Stats.ShotTypes[0, 3] + match.Stats.ShotTypes[1, 3],
                    "seed " + seeds[s] + ": shot kinds and one-timers correspond to actual successful stick strikes");
                check(countedOneTimers == totalOneTimers, "seed " + seeds[s] + ": one-timer attribution exactly matches team counts");
                float finalClock = match.TimeRemaining;
                Vector2 finalPuck = match.Puck.Position;
                match.StepBots(.25f);
                check(finalClock == match.TimeRemaining && finalPuck == match.Puck.Position, "seed " + seeds[s] + ": final score freezes gameplay until restart");
                blueWin |= match.BlueScore > match.OrangeScore;
                orangeWin |= match.OrangeScore > match.BlueScore;
                goals += match.BlueScore + match.OrangeScore; shots += totalShots; passes += totalPasses; saves += totalSaves; turnovers += totalTurnovers;
                oneTimers += totalOneTimers;
                summary.Append("seed ").Append(seeds[s]).Append(": ").Append(match.BlueScore).Append('-').Append(match.OrangeScore)
                    .Append(", shots ").Append(totalShots).Append(", saves ").Append(totalSaves).Append(", passes ").Append(totalPasses)
                    .Append(", takeaways ").Append(totalTurnovers).Append(", checks ").Append(match.Stats.Checks[0] + match.Stats.Checks[1])
                    .Append(", one-timers ").Append(totalOneTimers).Append(", max height ").Append(peakHeight.ToString("F2")).Append('m')
                    .Append(", frozen-puck resets ").Append(match.Stats.StuckResets).Append(", elapsed ").Append(match.Time.ToString("F1")).Append("s\n");
            }
            check(goals >= 8, "bot series produces earned goals through physical shooting (" + goals + " goals from " + shots + " attempts)");
            check(saves >= 8, "bot series includes physical goalkeeper saves and rebounds (" + saves + " saves)");
            check(shots > goals * 2, "shooting has misses/saves rather than automatic goals");
            check(blueWin && orangeWin, "full bot series exercises both blue and orange wins (overtime and draw have separate clock fixtures)");
            check(oneTimers > 0, "full bot series includes actual physical passing one-timers (" + oneTimers + ")");
            summary.Append("TOTAL goals ").Append(goals).Append(", shots ").Append(shots).Append(", saves ").Append(saves).Append(", passes ").Append(passes).Append(", takeaways ").Append(turnovers).Append(", one-timers ").Append(oneTimers);
            MatchSummary = summary.ToString();
        }
    }
}
