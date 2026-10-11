using System;
using System.Collections.Generic;

namespace HollowHouse
{
    /// <summary>
    /// Validates geometry, puzzle prerequisites, and the information that distinguishes a
    /// solution. This does not pretend to measure fun or replace simulation/play testing.
    /// </summary>
    public static class ScenarioValidator
    {
        public static List<string> Validate(Scenario scenario)
        {
            var errors = new List<string>();
            if (scenario == null) { errors.Add("Scenario is null."); return errors; }
            if (scenario.Rooms == null || scenario.Cells == null || scenario.Doors == null || scenario.Relics == null)
            { errors.Add("A scenario collection is null."); return errors; }
            if (scenario.Rooms.Count < 8 || scenario.Rooms.Count > 12) errors.Add("House must contain 8–12 rooms.");
            if (!Enum.IsDefined(typeof(Archetype), scenario.Archetype)) errors.Add("Unknown haunting archetype.");
            if (!Enum.IsDefined(typeof(Facing), scenario.RitualFacing)) errors.Add("Invalid ritual facing.");
            if (string.IsNullOrWhiteSpace(scenario.Identity) || string.IsNullOrWhiteSpace(scenario.History)) errors.Add("Missing identity or history.");
            if (scenario.Relics.Count != 3) errors.Add("The intersection puzzle requires three candidate keepsakes.");

            var roomNames = new HashSet<string>(StringComparer.Ordinal);
            var roomCenters = new HashSet<GridPos>();
            var facts = new Dictionary<string, List<Evidence>>(StringComparer.Ordinal);
            int evidenceRooms = 0;
            bool invalidRoom = false;
            for (int i = 0; i < scenario.Rooms.Count; i++)
            {
                Room room = scenario.Rooms[i];
                if (room == null) { errors.Add("Room " + i + " is null."); invalidRoom = true; continue; }
                if (room.Id != i) errors.Add("Room IDs must match list indices.");
                if (string.IsNullOrWhiteSpace(room.Name) || !roomNames.Add(room.Name)) errors.Add("Room labels must be present and unique.");
                if (!roomCenters.Add(room.Center)) errors.Add("Two rooms share a center.");
                if (room.X % 4 != 0 || room.Y % 4 != 0) errors.Add("Room center falls outside the room lattice.");
                if (room.Evidence == null) { errors.Add("Null evidence collection in room " + i + "."); continue; }
                if (room.Evidence.Count > 0) evidenceRooms++;
                foreach (Evidence clue in room.Evidence)
                {
                    if (clue == null || string.IsNullOrWhiteSpace(clue.FactKey) || string.IsNullOrWhiteSpace(clue.Text) || string.IsNullOrWhiteSpace(clue.Title))
                    { errors.Add("Empty evidence in room " + i + "."); continue; }
                    List<Evidence> entries;
                    if (!facts.TryGetValue(clue.FactKey, out entries)) { entries = new List<Evidence>(); facts.Add(clue.FactKey, entries); }
                    entries.Add(clue);
                }
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                    {
                        Cell cell;
                        if (!scenario.Cells.TryGetValue(new GridPos(room.X + x, room.Y + y), out cell) || cell == null || cell.RoomId != i || cell.DoorId != -1)
                            errors.Add("Room " + i + " has an invalid floor cell.");
                    }
            }
            if (invalidRoom) return errors;
            if (evidenceRooms < 6) errors.Add("Evidence must be distributed through at least six rooms.");

            var doorCells = new HashSet<GridPos>();
            var doorPairs = new HashSet<string>();
            for (int i = 0; i < scenario.Doors.Count; i++)
            {
                Door door = scenario.Doors[i];
                if (door == null) { errors.Add("Null door."); continue; }
                if (door.Id != i) errors.Add("Door IDs must match list indices.");
                if (!ValidRoom(scenario, door.A) || !ValidRoom(scenario, door.B) || door.A == door.B)
                { errors.Add("Door references invalid rooms."); continue; }
                Room a = scenario.Rooms[door.A], b = scenario.Rooms[door.B];
                if (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) != 4) errors.Add("Door joins nonadjacent rooms.");
                if (door.Position != new GridPos((a.X + b.X) / 2, (a.Y + b.Y) / 2)) errors.Add("Door is not at its rooms' midpoint.");
                if (!doorCells.Add(door.Position)) errors.Add("Duplicate door cell.");
                string pair = Math.Min(door.A, door.B) + ":" + Math.Max(door.A, door.B);
                if (!doorPairs.Add(pair)) errors.Add("Duplicate room connection.");
                Cell doorCell;
                if (!scenario.Cells.TryGetValue(door.Position, out doorCell) || doorCell == null || doorCell.DoorId != i || (doorCell.RoomId != door.A && doorCell.RoomId != door.B))
                    errors.Add("Door has no corresponding walk cell.");
            }

            foreach (KeyValuePair<GridPos, Cell> pair in scenario.Cells)
            {
                Cell cell = pair.Value;
                if (cell == null) { errors.Add("Null walk cell."); continue; }
                if (pair.Key != cell.Position) errors.Add("Cell key differs from position.");
                if (!ValidRoom(scenario, cell.RoomId)) errors.Add("Cell has invalid room ID.");
                if (cell.DoorId < -1 || cell.DoorId >= scenario.Doors.Count) errors.Add("Cell has invalid door ID.");
                if (cell.DoorId >= 0 && !doorCells.Contains(pair.Key)) errors.Add("Door cell does not have a door record.");
            }

            var reached = new HashSet<GridPos>();
            var queue = new Queue<GridPos>();
            if (!scenario.Cells.ContainsKey(scenario.Spawn)) errors.Add("Spawn is not walkable.");
            else { queue.Enqueue(scenario.Spawn); reached.Add(scenario.Spawn); }
            while (queue.Count > 0)
            {
                GridPos current = queue.Dequeue();
                foreach (GridPos next in scenario.Neighbors(current))
                    if (reached.Add(next)) queue.Enqueue(next);
            }
            if (reached.Count != scenario.Cells.Count) errors.Add("Some house cells are inaccessible from the entrance.");
            foreach (Room room in scenario.Rooms)
                if (!reached.Contains(room.Center)) errors.Add("Evidence/objectives in room " + room.Id + " are inaccessible.");
            Room spawnRoom = scenario.RoomAt(scenario.Spawn);
            if (spawnRoom == null || spawnRoom.Id != 0 || spawnRoom.Kind != RoomKind.Vestibule) errors.Add("Spawn must lie inside the sanctuary.");

            if (!ValidRoom(scenario, scenario.OriginRoom) || scenario.OriginRoom == 0) errors.Add("The origin must be a real non-sanctuary room.");
            if (!ValidRoom(scenario, scenario.InitialGhostRoom) || scenario.InitialGhostRoom == 0) errors.Add("Ghost must begin outside the sanctuary.");
            int expectedRelays = scenario.Archetype == Archetype.Echo ? 3 : 2;
            if (scenario.RelayRooms == null || scenario.RelayRooms.Length != expectedRelays) errors.Add("Incorrect number of relay rooms.");
            var roles = new HashSet<int> { 0, scenario.OriginRoom };
            if (scenario.RelayRooms != null)
                foreach (int relay in scenario.RelayRooms)
                    if (!ValidRoom(scenario, relay) || !roles.Add(relay)) errors.Add("Relay roles must occupy distinct non-origin rooms.");

            int materialMatches = 0, markMatches = 0, bothMatches = 0, deducedAnchor = -1;
            for (int i = 0; i < scenario.Relics.Count; i++)
            {
                Relic relic = scenario.Relics[i];
                if (relic == null) { errors.Add("Null keepsake."); continue; }
                if (string.IsNullOrWhiteSpace(relic.Name) || string.IsNullOrWhiteSpace(relic.Material) || string.IsNullOrWhiteSpace(relic.Inscription)) errors.Add("Incomplete keepsake description.");
                if (!ValidRoom(scenario, relic.RoomId) || !roles.Add(relic.RoomId)) errors.Add("Keepsakes must be in distinct, accessible, non-ritual rooms.");
                else if (scenario.Rooms[relic.RoomId].RelicIndex != i) errors.Add("Room/keepsake references disagree.");
                bool material = relic.Material == scenario.TargetMaterial;
                bool mark = relic.Inscription == scenario.TargetInscription;
                if (material) materialMatches++;
                if (mark) markMatches++;
                if (material && mark) { bothMatches++; deducedAnchor = i; }
                RequireText(facts, "relic." + i, relic.Name, errors);
                RequireText(facts, "relic." + i, relic.Material, errors);
                RequireText(facts, "relic." + i, relic.Inscription, errors);
            }
            if (materialMatches != 2 || markMatches != 2) errors.Add("Each partial keepsake clue must leave exactly two candidates.");
            if (bothMatches != 1 || deducedAnchor != scenario.AnchorRelic) errors.Add("The two clues do not uniquely identify the true anchor.");
            foreach (Room room in scenario.Rooms)
                if (room.RelicIndex >= 0 && (room.RelicIndex >= scenario.Relics.Count || scenario.Relics[room.RelicIndex] == null || scenario.Relics[room.RelicIndex].RoomId != room.Id))
                    errors.Add("Room references an invalid keepsake.");

            RequireFact(facts, "manual", errors);
            RequireFact(facts, "ritual", errors);
            RequireFact(facts, "recovery", errors);
            RequireText(facts, "behavior", scenario.Archetype.ToString(), errors);
            RequireText(facts, "anchor.material", scenario.TargetMaterial, errors);
            RequireText(facts, "anchor.inscription", scenario.TargetInscription, errors);
            RequireText(facts, "origin", scenario.Identity, errors);
            if (ValidRoom(scenario, scenario.OriginRoom)) RequireText(facts, "origin", scenario.Rooms[scenario.OriginRoom].Name, errors);
            if (scenario.Archetype == Archetype.Mourner)
            { RequireText(facts, "silence", "three", errors); RequireText(facts, "ritual", "WAIT", errors); }
            if (scenario.Archetype == Archetype.Watcher)
            {
                RequireText(facts, "facing", scenario.RitualFacing.ToString(), errors);
                if (scenario.RelayRooms != null)
                    foreach (int relay in scenario.RelayRooms)
                        if (ValidRoom(scenario, relay)) RequireText(facts, "relays", scenario.Rooms[relay].Name, errors);
            }
            if (scenario.TrueWords == null || scenario.TrueWords.Length != 3 || new HashSet<string>(scenario.TrueWords).Count != 3)
                errors.Add("The refrain must contain three distinct words.");
            if (scenario.Archetype == Archetype.Echo)
            {
                int previous = -1;
                string sequence = FactText(facts, "sequence");
                if (scenario.RelayRooms != null)
                    foreach (int relay in scenario.RelayRooms)
                    {
                        if (!ValidRoom(scenario, relay)) continue;
                        int next = sequence.IndexOf(scenario.Rooms[relay].Name, StringComparison.Ordinal);
                        if (next < 0 || next <= previous) errors.Add("The score does not unambiguously give the true station order.");
                        previous = next;
                    }
                if (scenario.TrueWords != null)
                    foreach (string word in scenario.TrueWords)
                    { RequireText(facts, "phrase", word, errors); RequireText(facts, "sequence", word, errors); }
                RequireText(facts, "ritual", "COMMUNICATE", errors);
            }
            return errors;
        }

        private static bool ValidRoom(Scenario scenario, int room) { return room >= 0 && room < scenario.Rooms.Count; }
        private static string FactText(Dictionary<string, List<Evidence>> facts, string key)
        {
            List<Evidence> entries;
            if (!facts.TryGetValue(key, out entries)) return string.Empty;
            var text = new System.Text.StringBuilder();
            foreach (Evidence clue in entries) text.AppendLine(clue.Text);
            return text.ToString();
        }
        private static void RequireFact(Dictionary<string, List<Evidence>> facts, string key, List<string> errors)
        { if (!facts.ContainsKey(key)) errors.Add("Missing discoverable fact: " + key + "."); }
        private static void RequireText(Dictionary<string, List<Evidence>> facts, string key, string expected, List<string> errors)
        {
            if (string.IsNullOrEmpty(expected) || FactText(facts, key).IndexOf(expected, StringComparison.OrdinalIgnoreCase) < 0)
                errors.Add("Evidence for " + key + " does not support the generated truth.");
        }
    }
}
