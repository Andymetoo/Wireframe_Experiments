using System;
using System.Collections.Generic;
using System.Text;

namespace HollowHouse
{
    /// <summary>
    /// Executable, presentation-independent playthroughs. The solving driver reads collected
    /// evidence and inspected objects; hidden solution fields are used only for assertions.
    /// No state setter, teleport, infinite resource, or disabled ghost is used by the solver.
    /// </summary>
    public static class SessionValidation
    {
        private sealed class Deductions
        {
            public Archetype Nature;
            public int Anchor;
            public int Origin;
            public int[] Stations;
            public Facing Bearing;
        }

        public static List<string> ValidateSeed(int seed)
        {
            var errors = new List<string>();
            try
            {
                ValidateInvestigation(seed, errors);
                ValidateResourcesAndEscape(seed, errors);
                ValidateDanger(seed, errors);
                ValidateDeterminism(seed, errors);
            }
            catch (Exception exception)
            {
                errors.Add("Seed " + seed + ": validator exception: " + exception.Message);
            }
            return errors;
        }

        private static void ValidateInvestigation(int seed, List<string> errors)
        {
            Scenario scenario = ScenarioGenerator.Generate(seed);
            var game = new GameSession(scenario);
            SafeAfter(game);
            for (int room = 0; room < scenario.Rooms.Count; room++)
            {
                Travel(game, room);
                game.Inspect();
                SafeAfter(game);
            }
            Require(game.SearchedRooms.Count == scenario.Rooms.Count, "Not every room could be searched.");
            Deductions facts = Infer(game);
            Require(facts.Anchor == scenario.AnchorRelic, "Observed metal and engraving identify the wrong anchor.");
            Require(facts.Origin == scenario.OriginRoom, "The last account does not uniquely reveal the origin.");
            Require(facts.Nature == scenario.Archetype, "The behavioral record contradicts the generated nature.");

            // A physical error must be recoverable, and a plausible decoy must not resolve a case.
            int decoy = (facts.Anchor + 1) % scenario.Relics.Count;
            Travel(game, scenario.Relics[decoy].RoomId);
            Require(game.TakeRelic(), "Could not pick up the decoy.");
            SafeAfter(game);
            PrepareStations(game, facts, true);
            Travel(game, facts.Origin);
            Require(game.PlaceRelic(), "Could not place the decoy at the origin.");
            SafeAfter(game);
            FinishConditions(game, facts);
            game.Release();
            Require(game.Ending == Ending.Investigating, "A decoy incorrectly won the case.");
            SafeAfter(game);

            Require(game.TakeRelic(), "Placed decoy could not be recovered.");
            SafeAfter(game);
            Travel(game, scenario.Relics[facts.Anchor].RoomId);
            Require(game.TakeRelic(), "Could not exchange decoy for the original.");
            Require(game.RelicAvailable(decoy), "Swapping lost the original location of the decoy.");
            SafeAfter(game);

            // Correct object, wrong place: choosing PLACE must not automatically choose the answer.
            Travel(game, 0);
            Require(game.PlaceRelic(), "Could not make a reversible misplaced offering.");
            SafeAfter(game);
            FinishConditions(game, facts);
            game.Release();
            Require(game.Ending == Ending.Investigating, "Offering at the wrong location incorrectly won.");
            SafeAfter(game);
            Require(game.TakeRelic(), "Misplaced offering could not be recovered.");
            SafeAfter(game);

            Travel(game, facts.Origin);
            Require(game.PlaceRelic(), "Could not place the authentic offering at the deduced origin.");
            SafeAfter(game);
            ProbeFinalConditions(game, facts);
            FinishConditions(game, facts);
            Require(game.RitualReady, "The documented ritual could not become ready.");
            Require(game.Release(), "A ready release was rejected.");
            Require(game.Ending == Ending.Won, "Evidence-based playthrough did not win.");
            Require(game.Health == 3, "A careful playthrough took an unavoidable wound.");
            Require(game.Salt == 4, "A careful playthrough needed consumable protection.");
            CheckTerminal(game, errors, "victory");
        }

        private static Deductions Infer(GameSession game)
        {
            string invoice = EvidenceText(game, "anchor.material");
            string register = EvidenceText(game, "anchor.inscription");
            string origin = EvidenceText(game, "origin");
            string behavior = EvidenceText(game, "behavior");
            var result = new Deductions();
            int matches = 0;
            for (int i = 0; i < game.Scenario.Relics.Count; i++)
            {
                Relic candidate = game.Scenario.Relics[i];
                Require(game.SearchedRooms.Contains(candidate.RoomId), "Solver tried to infer an uninspected object.");
                if (invoice.Contains("solid " + candidate.Material + ".") &&
                    register.Contains("engraved with " + candidate.Inscription + "."))
                { result.Anchor = i; matches++; }
            }
            Require(matches == 1, "Written evidence does not uniquely distinguish an anchor.");
            var originRooms = MentionedRooms(game.Scenario, origin);
            Require(originRooms.Count == 1, "The origin account must name exactly one room.");
            result.Origin = originRooms[0];
            result.Nature = behavior.Contains("MOURNER") ? Archetype.Mourner :
                behavior.Contains("WATCHER") ? Archetype.Watcher : behavior.Contains("ECHO") ? Archetype.Echo :
                throw new InvalidOperationException("Behavior evidence does not identify a nature.");
            if (result.Nature == Archetype.Watcher)
            {
                result.Stations = MentionedRooms(game.Scenario, EvidenceText(game, "relays")).ToArray();
                Require(result.Stations.Length == 2, "Witness statement does not identify two stations.");
                string facing = EvidenceText(game, "facing");
                int count = 0;
                for (int d = 0; d < 4; d++)
                    if (facing.Contains(((Facing)d).ToString().ToUpperInvariant())) { result.Bearing = (Facing)d; count++; }
                Require(count == 1, "Death register does not identify one ritual bearing.");
            }
            else if (result.Nature == Archetype.Echo)
            {
                result.Stations = MentionedRooms(game.Scenario, EvidenceText(game, "sequence")).ToArray();
                Require(result.Stations.Length == 3, "The score does not identify three ordered stations.");
            }
            else result.Stations = new int[0];
            return result;
        }

        private static string EvidenceText(GameSession game, string key)
        {
            for (int i = 0; i < game.Journal.Count; i++)
                if (game.Journal[i].FactKey == key) return game.Journal[i].Text;
            throw new InvalidOperationException("Missing discoverable evidence: " + key);
        }

        private static List<int> MentionedRooms(Scenario scenario, string text)
        {
            var found = new List<int>();
            foreach (Room room in scenario.Rooms) if (text.Contains(room.Name)) found.Add(room.Id);
            found.Sort((a, b) => text.IndexOf(scenario.Rooms[a].Name, StringComparison.Ordinal)
                .CompareTo(text.IndexOf(scenario.Rooms[b].Name, StringComparison.Ordinal)));
            return found;
        }

        private static void PrepareStations(GameSession game, Deductions facts, bool testMistake)
        {
            if (facts.Nature == Archetype.Mourner) return;
            if (testMistake)
            {
                Travel(game, 0);
                game.Attune();
                SafeAfter(game);
                Require(!game.SealedRooms.Contains(0), "An unrelated room became a station.");
                Require(game.RitualProgress == 0, "A wrong first station advanced the sequence.");
                if (facts.Nature == Archetype.Echo)
                {
                    Travel(game, facts.Stations[0]);
                    game.Attune();
                    SafeAfter(game);
                    Travel(game, facts.Stations[2]);
                    game.Attune();
                    SafeAfter(game);
                    Require(game.RitualProgress == 0, "A wrong later station did not reset the cadence.");
                }
            }
            foreach (int room in facts.Stations)
            {
                Travel(game, room);
                Require(game.Attune(), "A documented station refused attunement.");
                SafeAfter(game);
            }
            Require(game.RitualProgress == facts.Stations.Length, "The documented station sequence did not complete.");
        }

        private static void FinishConditions(GameSession game, Deductions facts)
        {
            if (facts.Nature == Archetype.Mourner)
            {
                if (game.Lantern) game.ToggleLantern();
                game.Wait(); game.Wait(); game.Wait();
            }
            else if (facts.Nature == Archetype.Watcher)
                game.TurnFacing((int)facts.Bearing - (int)game.Facing);
            else
            {
                game.Communicate();
                SafeAfter(game);
            }
        }

        private static void ProbeFinalConditions(GameSession game, Deductions facts)
        {
            if (facts.Nature == Archetype.Mourner)
            {
                game.Wait();
                Require(game.RitualProgress == 2 && !game.RitualReady, "Two breaths incorrectly completed a three-breath vigil.");
                game.Communicate();
                Require(game.RitualProgress == 0, "Speech failed to interrupt a vigil.");
            }
            else if (facts.Nature == Archetype.Watcher)
                game.TurnFacing(((int)facts.Bearing + 2) % 4 - (int)game.Facing);
            else Require(!game.SpiritAnswered, "The Echo answered without communication over the authentic offering.");
            Require(!game.RitualReady, "An incomplete final condition incorrectly made a ritual ready.");
            game.Release();
            Require(game.Ending == Ending.Investigating, "Ignoring a final ritual condition incorrectly won.");
            SafeAfter(game);
        }

        private static void Travel(GameSession game, int room)
        {
            List<GridPos> path = game.Scenario.FindPath(game.Position, game.Scenario.Rooms[room].Center);
            Require(path.Count > 0, "No traversable route to room " + room + ".");
            for (int i = 1; i < path.Count; i++)
            {
                GridPos difference = path[i] - game.Position;
                Facing direction = difference.X > 0 ? Facing.East : difference.X < 0 ? Facing.West : difference.Y > 0 ? Facing.North : Facing.South;
                game.TurnFacing((int)direction - (int)game.Facing);
                Cell cell = game.Scenario.Cells[path[i]];
                if (cell.DoorId >= 0 && !game.DoorsOpen[cell.DoorId])
                {
                    Require(game.InteractDoor(), "A route's closed door could not be opened.");
                    SafeAfter(game);
                    game.TurnFacing((int)direction - (int)game.Facing);
                }
                Require(game.Move(0), "A connected route rejected a valid step.");
                if (cell.DoorId >= 0)
                {
                    int before = game.Turn;
                    Require(!game.InteractDoor() && game.DoorsOpen[cell.DoorId] && game.Turn == before,
                        "An occupied doorway could close through the player or consume a turn.");
                }
                SafeAfter(game);
            }
            Require(game.CurrentRoom == room, "Traversal arrived in the wrong room.");
        }

        private static void SafeAfter(GameSession game)
        {
            Require(game.Ending == Ending.Investigating, "Safe route unexpectedly ended.");
            // The foyer manual teaches all three responses before the nature is known.
            // Combine them conservatively, without reading the hidden archetype.
            if (game.Lantern) game.ToggleLantern();
            if (game.GhostVisible || game.ThreatCountdown > 0)
                game.TurnFacing((int)game.ThreatDirection - (int)game.Facing);
            game.Wait();
            Require(game.Health == 3, "A documented protective action failed to prevent injury.");
        }

        private static void ValidateResourcesAndEscape(int seed, List<string> errors)
        {
            var game = new GameSession(ScenarioGenerator.Generate(seed));
            int turn = game.Turn;
            Require(!game.TakeRelic(), "An empty room produced an object.");
            Require(!game.PlaceRelic(), "Empty hands placed an object.");
            Require(game.Turn == turn, "Invalid actions consumed a turn.");
            for (int i = 0; i < 12; i++) Require(game.ReadMeter(), "Meter ended before its twelfth charge.");
            turn = game.Turn;
            Require(game.Charges == 0 && !game.ReadMeter() && game.Turn == turn, "Meter underflowed or exhausted use consumed time.");
            for (int i = 0; i < 4; i++) Require(game.Ward(), "Salt ended before its fourth use.");
            turn = game.Turn;
            Require(game.Salt == 0 && !game.Ward() && game.Turn == turn, "Salt underflowed or empty use consumed time.");
            Require(game.Health == 3, "The foyer sanctuary allowed damage.");
            Require(game.Escape() && game.Ending == Ending.Escaped, "The foyer exit failed.");
            CheckTerminal(game, errors, "escape");
        }

        private static void ValidateDanger(int seed, List<string> errors)
        {
            var game = new GameSession(ScenarioGenerator.Generate(seed));
            Travel(game, game.Scenario.InitialGhostRoom);
            if (!game.Lantern) game.ToggleLantern();
            int firstWarningTurn = -1;
            int previousHealth = game.Health;
            for (int i = 0; i < 350 && game.Ending == Ending.Investigating; i++)
            {
                // Deliberately violate every nature's rule. Repeated speech attracts the ghost.
                game.TurnFacing(((int)game.ThreatDirection + 2) % 4 - (int)game.Facing);
                game.Communicate();
                if (game.ThreatCountdown == 2) firstWarningTurn = game.Turn;
                if (game.Health < previousHealth)
                {
                    Require(firstWarningTurn >= 0 && game.Turn - firstWarningTurn >= 2, "An injury occurred without two warned actions.");
                    previousHealth = game.Health;
                    firstWarningTurn = -1;
                }
            }
            Require(game.Ending == Ending.Lost && game.Health == 0, "Repeatedly ignoring the ghost never caused defeat.");
            CheckTerminal(game, errors, "defeat");
        }

        private static void ValidateDeterminism(int seed, List<string> errors)
        {
            var first = new GameSession(ScenarioGenerator.Generate(seed));
            var second = new GameSession(ScenarioGenerator.Generate(seed));
            for (int i = 0; i < 72; i++)
            {
                ReplayAction(first, i);
                ReplayAction(second, i);
                if (Snapshot(first) != Snapshot(second))
                { errors.Add("Seed " + seed + ": identical actions diverged at replay action " + i + "."); return; }
            }
        }

        private static void ReplayAction(GameSession game, int step)
        {
            switch (step % 9)
            {
                case 0: game.TurnFacing(1); break;
                case 1: game.Move(0); break;
                case 2: game.InteractDoor(); break;
                case 3: game.Inspect(); break;
                case 4: game.ToggleLantern(); break;
                case 5: game.ReadMeter(); break;
                case 6: game.Communicate(); break;
                case 7: game.Attune(); break;
                default: game.Wait(); break;
            }
        }

        private static void CheckTerminal(GameSession game, List<string> errors, string description)
        {
            string before = Snapshot(game);
            game.Move(0); game.TurnFacing(1); game.Inspect(); game.InteractDoor(); game.ReadMeter();
            game.Communicate(); game.ToggleLantern(); game.Wait(); game.Ward(); game.TakeRelic();
            game.PlaceRelic(); game.Attune(); game.Release(); game.Escape();
            if (before != Snapshot(game)) errors.Add("Seed " + game.Scenario.Seed + ": actions changed state after " + description + ".");
        }

        private static string Snapshot(GameSession game)
        {
            var result = new StringBuilder();
            result.Append(game.Position).Append('|').Append(game.Facing).Append('|').Append(game.Turn)
                .Append('|').Append(game.Health).Append('|').Append(game.Fear).Append('|').Append(game.Salt)
                .Append('|').Append(game.Charges).Append('|').Append(game.GhostRoom).Append('|').Append(game.GhostPressure)
                .Append('|').Append(game.ThreatCountdown).Append('|').Append(game.Lantern).Append('|').Append(game.CarriedRelic)
                .Append('|').Append(game.PlacedRelic).Append('|').Append(game.PlacedRoom).Append('|').Append(game.RitualProgress)
                .Append('|').Append(game.Ending).Append('|').Append(game.SpiritAnswered).Append('|').Append(game.WardTurns);
            foreach (bool open in game.DoorsOpen) result.Append(open ? '1' : '0');
            foreach (string entry in game.Log) result.Append('|').Append(entry);
            foreach (Evidence entry in game.Journal) result.Append('|').Append(entry.FactKey);
            foreach (Room room in game.Scenario.Rooms)
                result.Append('|').Append(game.VisitedRooms.Contains(room.Id)).Append(game.SearchedRooms.Contains(room.Id)).Append(game.SealedRooms.Contains(room.Id));
            return result.ToString();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
