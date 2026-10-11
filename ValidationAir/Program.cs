using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Meridian;

internal static class Program
{
    private static int checks, failures;
    private static int Main(string[] args)
    {
        if (Array.IndexOf(args, "--trace") >= 0) { Trace(Array.IndexOf(args, "--duel") >= 0, Array.IndexOf(args, "--straight") >= 0); return 0; }
        if (Array.IndexOf(args, "--ace") >= 0) { Ace(Array.IndexOf(args, "--escort") >= 0); return 0; }
        var timer = Stopwatch.StartNew();
        Console.WriteLine("MERIDIAN 1944 - deterministic flight and combat validation");
        AirValidation.Run(Check);
        Console.WriteLine(AirValidation.SortieSummary);
        Console.WriteLine("Checks: " + checks + "; failures: " + failures + "; seconds: " + timer.Elapsed.TotalSeconds.ToString("F2"));
        Console.WriteLine("Scope: numerical flight, AI, ballistics, mission transitions. Human handling/visual playtesting remains separate.");
        return failures == 0 ? 0 : 1;
    }
    private static void Check(bool condition, string message)
    { checks++; if (!condition) { failures++; Console.WriteLine("FAIL " + message); } }

    private static void Trace(bool duel, bool straight)
    {
        var mission = new AirMission(1944, duel ? MissionKind.Duel : MissionKind.ConvoyCover);
        var playerAI = new PilotAI(54321);
        for (int n = 0; n < 60 * 330 && mission.Outcome == MissionOutcome.Flying; n++)
        {
            FlightInput input = straight ? new FlightInput { Throttle = .62f } : playerAI.Update(mission.Player, mission.Aircraft, mission.Terrain, Vector3.Zero, 1f / 60f);
            mission.Step(input, 1f / 60f);
            if (n % 600 != 0) continue;
            Console.WriteLine("t=" + mission.Time.ToString("F0") + " rounds=" + mission.TotalRoundsFired + " hits=" + mission.Hits);
            foreach (AircraftState a in mission.Aircraft)
                Console.WriteLine(a.Callsign + " " + mission.GetPilotState(a.Id) + " alive=" + a.Alive + " speed=" + a.Speed.ToString("F0")
                    + " alt=" + a.Position.Y.ToString("F0") + " hp=" + a.Health.ToString("F0") + " bank="
                    + (Math.Atan2(-a.Right.Y, a.Up.Y) * 180 / Math.PI).ToString("F0") + " pitch=" + (Math.Asin(a.Forward.Y)*180/Math.PI).ToString("F1")
                    + " aoa=" + (a.AngleOfAttack*180/Math.PI).ToString("F1") + " load=" + a.LoadFactor.ToString("F1") + " pos=" + a.Position.ToString());
        }
        Console.WriteLine(mission.Outcome + " " + mission.Summary);
    }

    private static void Ace(bool escort)
    {
        int[] seeds = { 1944, 0, 1, 17, 88, 31337 };
        foreach (int seed in seeds)
        {
            var mission = new AirMission(seed, escort ? MissionKind.ConvoyCover : MissionKind.Duel);
            var pilot = new ValidationPilot(seed);
            for (int tick = 0; tick < 60 * 301 && mission.Outcome == MissionOutcome.Flying; tick++)
                mission.Step(pilot.Update(mission), 1f / 60f);
            Console.WriteLine("seed=" + seed + " " + mission.Outcome + " t=" + mission.Time.ToString("F1") + " shots=" + mission.RoundsFired + " hits=" + mission.Hits
                + " player=" + mission.Player.Health.ToString("F0") + " enemy=" + mission.Aircraft[1].Health.ToString("F0") + " alt=" + mission.Player.Position.Y.ToString("F0"));
        }
    }

}
