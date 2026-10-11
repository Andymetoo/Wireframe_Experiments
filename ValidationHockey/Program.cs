using System;
using System.Globalization;
using System.Numerics;
using ColdSnap;

internal static class Program
{
    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
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
}
