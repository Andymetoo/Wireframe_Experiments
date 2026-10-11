using System;
using System.Globalization;
using System.Numerics;
using ColdSnap;

internal static class Program
{
    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "--airtrace")
        { AirTrace(args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 7); return 0; }
        if (args.Length > 0 && (args[0] == "trace" || args[0] == "--trace"))
        {
            int seed = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 1988;
            Trace(seed); return 0;
        }
        int passed = 0, failed = 0;
        HockeyValidation.Run((condition, name) => { if (condition) passed++; else { failed++; Console.WriteLine("FAIL: " + name); } });
        Console.WriteLine(HockeyValidation.MatchSummary);
        Console.WriteLine("Cold Snap: " + passed + " passed, " + failed + " failed.");
        Console.WriteLine("Scope: numerical skating/puck physics, seeded team behavior, shot/save/goal events and full match flow. Human control feel and first-person readability require playtesting.");
        return failed == 0 ? 0 : 1;
    }

    private static void Trace(int seed)
    {
        var session = new HockeySession(seed);
        for (int frame = 0; frame < 18000 && session.Phase != MatchPhase.Final; frame++)
        {
            session.StepBots(1f / 60f);
            if (frame % 300 != 0) continue;
            Console.WriteLine("t=" + session.Time.ToString("F1") + " puck=" + session.Puck.Position + " vel=" + session.Puck.Velocity + " control=" + session.Puck.ControllerId + " score=" + session.BlueScore + ":" + session.OrangeScore + " shots=" + session.Stats.Shots[0] + ":" + session.Stats.Shots[1]);
            for (int i = 0; i < 6; i++) Console.WriteLine(i + " " + session.Skaters[i].Position + " " + session.Skaters[i].Velocity + " " + session.Skaters[i].Intent);
        }
    }

    private static void AirTrace(int seed)
    {
        var match = new HockeySession(seed);
        for (int tick = 0; tick < 21000 && match.Phase != MatchPhase.Final; tick++)
        {
            int sequence = match.Puck.ShotSequence;
            float vertical = match.Puck.VerticalVelocity, height = match.Puck.Height;
            Vector2 position = match.Puck.Position, velocity = match.Puck.Velocity;
            match.StepBots(1f / 60f);
            if (match.Puck.ShotSequence == sequence && match.Puck.VerticalVelocity > vertical + 3f && height > .3f)
                Console.WriteLine("Air impact at " + match.Time.ToString("F3") + "s: position " + position + " height " + height
                    + ", horizontal " + velocity + " -> " + match.Puck.Velocity + ", vertical " + vertical + " -> " + match.Puck.VerticalVelocity);
            if (match.Puck.ShotSequence != sequence && !match.Puck.LastStrikeWasPass && match.Puck.VerticalVelocity > 8f)
                Console.WriteLine("High launch at " + match.Time.ToString("F3") + "s: " + match.Puck.LastShotKind + ", position " + match.Puck.Position
                    + ", horizontal " + match.Puck.Velocity + ", vertical " + match.Puck.VerticalVelocity);
        }
    }
}
