using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HollowHouse;

internal static class Program
{
    private static int checks;
    private static int failures;

    private static int Main(string[] args)
    {
        bool structuralOnly = Array.IndexOf(args, "--structural-only") >= 0;
        var clock = Stopwatch.StartNew();
        Console.WriteLine("THE HOUSE THAT LISTENS — reproducible core validation");
        Console.WriteLine("Framework: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        int[] archetypes = new int[3];
        int[] seeds = new int[10000];
        for (int i = 0; i < seeds.Length - 4; i++) seeds[i] = i;
        seeds[9996] = -1;
        seeds[9997] = unchecked((int)0x9E3779B9u);
        seeds[9998] = int.MinValue;
        seeds[9999] = int.MaxValue;
        for (int i = 0; i < seeds.Length; i++)
        {
            int seed = seeds[i];
            try
            {
                Scenario scenario = ScenarioGenerator.Generate(seed);
                archetypes[(int)scenario.Archetype]++;
                CheckErrors("structure seed " + seed, ScenarioValidator.Validate(scenario));
                Check(Fingerprint(scenario) == Fingerprint(ScenarioGenerator.Generate(seed)), "generation determinism seed " + seed);
            }
            catch (Exception exception) { Check(false, "generation seed " + seed + ": " + exception); }
        }
        Console.WriteLine("Structural scenarios: " + seeds.Length + " (seeds 0–9995, -1, -1640531527, Int32.MinValue, Int32.MaxValue).");
        Console.WriteLine("Archetypes: Mourner " + archetypes[0] + ", Watcher " + archetypes[1] + ", Echo " + archetypes[2] + ".");
        Console.WriteLine("Generation determinism comparisons: " + seeds.Length + ".");
        ValidateRejections();

        int sessionCases = 0;
        if (!structuralOnly)
        {
            for (int i = 0; i < 1000; i++)
            {
                int seed = i == 998 ? int.MinValue : i == 999 ? int.MaxValue : i;
                try
                {
                    CheckErrors("session seed " + seed, SessionValidation.ValidateSeed(seed));
                    sessionCases++;
                }
                catch (Exception exception) { Check(false, "session seed " + seed + ": " + exception); }
            }
        }
        Console.WriteLine("Session scenarios: " + sessionCases + (structuralOnly ? " (intentionally skipped by --structural-only)." : " (seeds 0–997 and signed integer boundaries)."));
        Console.WriteLine("Assertions: " + checks + "; failures: " + failures + ".");
        Console.WriteLine("Elapsed: " + clock.Elapsed.TotalSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " seconds.");
        Console.WriteLine("Scope: generated geometry, evidence and core simulation. Does not validate Unity rendering, UI, subjective difficulty, or visual quality.");
        return failures == 0 ? 0 : 1;
    }

    private static void ValidateRejections()
    {
        Reject("missing material evidence", scenario => RemoveFact(scenario, "anchor.material"));
        Reject("contradictory inscription evidence", scenario => FindFact(scenario, "anchor.inscription").Text = "An unrelated crest.");
        Reject("wrong authentic candidate", scenario => scenario.AnchorRelic = (scenario.AnchorRelic + 1) % 3);
        Reject("blocked door cell", scenario => scenario.Cells.Remove(scenario.Doors[0].Position));
        Reject("impossible room role overlap", scenario => scenario.RelayRooms[0] = scenario.OriginRoom);
        Reject("false refrain order", scenario => FindFact(scenario, "sequence").Text = "No surviving order.");
        Reject("missing original relic descriptor", scenario => RemoveFact(scenario, "relic." + scenario.AnchorRelic));
        Reject("ghost inside sanctuary", scenario => scenario.InitialGhostRoom = 0);
        Console.WriteLine("Deliberately broken scenarios rejected: 8 checks.");
    }

    private static void Reject(string name, Action<Scenario> mutate)
    {
        Scenario scenario = ScenarioGenerator.Generate(71319, Archetype.Echo);
        mutate(scenario);
        Check(ScenarioValidator.Validate(scenario).Count > 0, "validator accepts " + name);
    }

    private static void RemoveFact(Scenario scenario, string key)
    { foreach (Room room in scenario.Rooms) room.Evidence.RemoveAll(evidence => evidence.FactKey == key); }

    private static Evidence FindFact(Scenario scenario, string key)
    {
        foreach (Room room in scenario.Rooms)
            foreach (Evidence evidence in room.Evidence)
                if (evidence.FactKey == key) return evidence;
        throw new InvalidOperationException("Missing fixture fact " + key);
    }

    private static void CheckErrors(string name, IEnumerable<string> errors)
    {
        var messages = new List<string>(errors);
        Check(messages.Count == 0, name + (messages.Count == 0 ? "" : ": " + string.Join(" | ", messages)));
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (condition) return;
        failures++;
        if (failures <= 40) Console.WriteLine("FAIL: " + message);
        else if (failures == 41) Console.WriteLine("Further failures suppressed.");
    }

    private static string Fingerprint(Scenario scenario)
    {
        var output = new StringBuilder();
        output.Append(scenario.Seed).Append('|').Append(scenario.Identity).Append('|').Append(scenario.History).Append('|').Append(scenario.Archetype)
            .Append('|').Append(scenario.AnchorRelic).Append('|').Append(scenario.OriginRoom).Append('|').Append(scenario.RitualFacing)
            .Append('|').Append(scenario.Spawn).Append('|').Append(scenario.InitialGhostRoom).Append('|').Append(scenario.TargetMaterial).Append('|').Append(scenario.TargetInscription);
        foreach (int relay in scenario.RelayRooms) output.Append('|').Append(relay);
        foreach (string word in scenario.TrueWords) output.Append('|').Append(word);
        foreach (Room room in scenario.Rooms)
        {
            output.Append('|').Append(room.Id).Append('|').Append(room.Name).Append('|').Append(room.X).Append('|').Append(room.Y).Append('|').Append(room.Kind).Append('|').Append(room.RelicIndex);
            foreach (Evidence evidence in room.Evidence) output.Append('|').Append(evidence.FactKey).Append('|').Append(evidence.Title).Append('|').Append(evidence.Text);
        }
        foreach (Door door in scenario.Doors) output.Append('|').Append(door.Id).Append('|').Append(door.Position).Append('|').Append(door.A).Append('|').Append(door.B);
        foreach (Relic relic in scenario.Relics) output.Append('|').Append(relic.Name).Append('|').Append(relic.Material).Append('|').Append(relic.Inscription).Append('|').Append(relic.RoomId);
        var cells = new List<GridPos>(scenario.Cells.Keys);
        cells.Sort((a, b) => a.X == b.X ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        foreach (GridPos position in cells)
        { Cell cell = scenario.Cells[position]; output.Append('|').Append(position).Append('|').Append(cell.RoomId).Append('|').Append(cell.DoorId); }
        return output.ToString();
    }
}
