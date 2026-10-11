using System;
using System.Collections.Generic;

namespace HollowHouse
{
    /// <summary>Generates truth first, then places its supporting objects and corroborating accounts.</summary>
    public static class ScenarioGenerator
    {
        private static readonly string[] RoomNames =
        { "Vestibule", "Parlour", "Nursery", "Study", "Kitchen", "Dining Room", "Conservatory", "Chapel", "Bedchamber", "Cellar" };
        private static readonly string[] GivenNames = { "Ada", "Edmund", "Mara", "Silas", "Iris", "Theodore", "Evelyn", "Jonas" };
        private static readonly string[] Surnames = { "Voss", "Vale", "Ashby", "Mercer", "Holloway", "Wren", "Blackwell", "Morrow" };
        private static readonly string[] Materials = { "silver", "brass", "iron", "copper" };
        private static readonly string[] Marks = { "a closed eye", "a broken circle", "three falling stars", "a moth over a flame", "a key beneath a wave", "two intertwined branches" };
        private static readonly string[] WordPool = { "ASH", "HOME", "DAWN", "HUSH", "MOTH", "STAY", "TIDE", "REST" };
        private static readonly GridPos[] Directions =
        { new GridPos(0, 1), new GridPos(1, 0), new GridPos(0, -1), new GridPos(-1, 0) };

        // An explicit generator keeps seeds reproducible across runtime/framework versions.
        private sealed class Dice
        {
            private uint state;
            public Dice(int seed) { state = unchecked((uint)seed) ^ 0x9E3779B9u; if (state == 0) state = 0xA341316Cu; }
            public int Next(int maximum)
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                return (int)(state % (uint)maximum);
            }
            public void Shuffle<T>(IList<T> values)
            {
                for (int i = values.Count - 1; i > 0; i--)
                { int j = Next(i + 1); T swap = values[i]; values[i] = values[j]; values[j] = swap; }
            }
        }

        private struct Frontier
        {
            public int Parent;
            public GridPos Position;
            public Frontier(int parent, GridPos position) { Parent = parent; Position = position; }
        }

        public static Scenario Generate(int seed) { return Generate(seed, null); }

        public static Scenario Generate(int seed, Archetype? forcedArchetype)
        {
            var dice = new Dice(seed);
            var scenario = new Scenario
            {
                Seed = seed,
                Identity = GivenNames[dice.Next(GivenNames.Length)] + " " + Surnames[dice.Next(Surnames.Length)],
                Archetype = forcedArchetype ?? (Archetype)dice.Next(3),
                RitualFacing = (Facing)dice.Next(4),
                Spawn = new GridPos(0, -1)
            };
            BuildHouse(scenario, dice);
            AssignTruth(scenario, dice);
            PlaceEvidence(scenario, dice);
            return scenario;
        }

        private static void BuildHouse(Scenario scenario, Dice dice)
        {
            var roomKinds = new List<RoomKind>();
            for (int i = 1; i < RoomNames.Length; i++) roomKinds.Add((RoomKind)i);
            dice.Shuffle(roomKinds);
            var occupied = new Dictionary<GridPos, int>();
            var lattice = new List<GridPos> { new GridPos(0, 0) };
            occupied.Add(lattice[0], 0);
            scenario.Rooms.Add(CreateRoom(0, RoomKind.Vestibule, lattice[0]));
            while (scenario.Rooms.Count < RoomNames.Length)
            {
                var frontier = new List<Frontier>();
                for (int r = 0; r < lattice.Count; r++)
                {
                    foreach (GridPos direction in Directions)
                    {
                        GridPos next = lattice[r] + direction;
                        if (Math.Abs(next.X) > 3 || Math.Abs(next.Y) > 3 || occupied.ContainsKey(next)) continue;
                        frontier.Add(new Frontier(r, next));
                    }
                }
                Frontier chosen = frontier[dice.Next(frontier.Count)];
                int id = scenario.Rooms.Count;
                lattice.Add(chosen.Position);
                occupied.Add(chosen.Position, id);
                scenario.Rooms.Add(CreateRoom(id, roomKinds[id - 1], chosen.Position));
                AddDoor(scenario, chosen.Parent, id);
            }

            // A few loops create escape routes, while the spanning tree guarantees reachability.
            for (int a = 0; a < lattice.Count; a++)
            {
                for (int b = a + 1; b < lattice.Count; b++)
                {
                    if (Math.Abs(lattice[a].X - lattice[b].X) + Math.Abs(lattice[a].Y - lattice[b].Y) != 1) continue;
                    bool exists = false;
                    foreach (Door door in scenario.Doors)
                        if ((door.A == a && door.B == b) || (door.A == b && door.B == a)) exists = true;
                    if (!exists && dice.Next(3) == 0) AddDoor(scenario, a, b);
                }
            }

            foreach (Room room in scenario.Rooms)
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                    {
                        GridPos position = new GridPos(room.X + x, room.Y + y);
                        scenario.Cells.Add(position, new Cell { Position = position, RoomId = room.Id });
                    }
            foreach (Door door in scenario.Doors)
                scenario.Cells.Add(door.Position, new Cell { Position = door.Position, RoomId = door.A, DoorId = door.Id });
        }

        private static Room CreateRoom(int id, RoomKind kind, GridPos lattice)
        { return new Room { Id = id, Kind = kind, Name = RoomNames[(int)kind], X = lattice.X * 4, Y = lattice.Y * 4 }; }

        private static void AddDoor(Scenario scenario, int a, int b)
        {
            Room first = scenario.Rooms[a], second = scenario.Rooms[b];
            scenario.Doors.Add(new Door
            { Id = scenario.Doors.Count, A = a, B = b, Position = new GridPos((first.X + second.X) / 2, (first.Y + second.Y) / 2) });
        }

        private static void AssignTruth(Scenario scenario, Dice dice)
        {
            RoomKind[] originKinds = scenario.Archetype == Archetype.Mourner
                ? new[] { RoomKind.Nursery, RoomKind.Bedchamber, RoomKind.Parlour }
                : scenario.Archetype == Archetype.Watcher
                    ? new[] { RoomKind.Study, RoomKind.Chapel, RoomKind.Conservatory }
                    : new[] { RoomKind.DiningRoom, RoomKind.Kitchen, RoomKind.Cellar };
            RoomKind originKind = originKinds[dice.Next(originKinds.Length)];
            foreach (Room room in scenario.Rooms) if (room.Kind == originKind) scenario.OriginRoom = room.Id;

            var available = new List<int>();
            for (int i = 1; i < scenario.Rooms.Count; i++) if (i != scenario.OriginRoom) available.Add(i);
            dice.Shuffle(available);
            int relayCount = scenario.Archetype == Archetype.Echo ? 3 : 2;
            scenario.RelayRooms = available.GetRange(0, relayCount).ToArray();
            available.RemoveRange(0, relayCount);
            scenario.InitialGhostRoom = scenario.OriginRoom;

            int material = dice.Next(Materials.Length);
            int mark = dice.Next(Marks.Length);
            scenario.TargetMaterial = Materials[material];
            scenario.TargetInscription = Marks[mark];
            string otherMaterial = Materials[(material + 1 + dice.Next(Materials.Length - 1)) % Materials.Length];
            string otherMark = Marks[(mark + 1 + dice.Next(Marks.Length - 1)) % Marks.Length];
            var candidates = new List<Relic>
            {
                new Relic { Material = scenario.TargetMaterial, Inscription = scenario.TargetInscription },
                new Relic { Material = scenario.TargetMaterial, Inscription = otherMark },
                new Relic { Material = otherMaterial, Inscription = scenario.TargetInscription }
            };
            dice.Shuffle(candidates);
            string[] names = { "Pocket watch", "Mourning brooch", "Winding key" };
            dice.Shuffle(names);
            for (int i = 0; i < candidates.Count; i++)
            {
                Relic relic = candidates[i];
                relic.Name = names[i];
                relic.RoomId = available[i];
                scenario.Rooms[relic.RoomId].RelicIndex = i;
                scenario.Relics.Add(relic);
                if (relic.Material == scenario.TargetMaterial && relic.Inscription == scenario.TargetInscription) scenario.AnchorRelic = i;
            }

            var words = new List<string>(WordPool);
            dice.Shuffle(words);
            scenario.TrueWords = words.GetRange(0, 3).ToArray();
            string origin = scenario.Rooms[scenario.OriginRoom].Name;
            scenario.History = scenario.Archetype == Archetype.Mourner
                ? scenario.Identity + " kept a vigil in the " + origin + ", waiting for a child who never returned. The final lamp burned until dawn."
                : scenario.Archetype == Archetype.Watcher
                    ? scenario.Identity + " guarded the " + origin + " during the house's last winter. They died facing " + scenario.RitualFacing.ToString().ToLowerInvariant() + ", afraid to turn away."
                    : scenario.Identity + " taught the household a three-part refrain. The last answer was cut short in the " + origin + "; the house has repeated it ever since.";
        }

        private static void PlaceEvidence(Scenario scenario, Dice dice)
        {
            string origin = scenario.Rooms[scenario.OriginRoom].Name;
            string first = scenario.Rooms[scenario.RelayRooms[0]].Name;
            string second = scenario.Rooms[scenario.RelayRooms[1]].Name;
            var clues = new List<Evidence>
            {
                new Evidence("The jeweller's invoice", "For " + scenario.Identity + ": one personal keepsake, solid " + scenario.TargetMaterial + ". Copies were made later. Material alone proves nothing; compare its engraving to the family register.", "anchor.material"),
                new Evidence("A leaf from the family register", "The keepsake carried by " + scenario.Identity + " was engraved with " + scenario.TargetInscription + ". A relative copied the mark onto a different metal. Find the object matching BOTH this mark and the jeweller's metal.", "anchor.inscription"),
                new Evidence("The last account", scenario.History + " It is the place of the final vigil, not the place where an object was stored, that must receive the true keepsake.", "origin")
            };

            scenario.Rooms[0].Evidence.Add(new Evidence("Investigator's field manual",
                "Find out who remains, identify their authentic keepsake, and complete the unfinished act. Read traces in every room; your journal preserves them. Inspect the three keepsakes before choosing. Two independent clues distinguish the original. You can carry one and put it down anywhere; mistakes can be retrieved.\n\n" +
                "Threats announce themselves before striking. MOURNER: extinguish your lantern, then wait. WATCHER: face the signalled bearing, then wait. ECHO: wait to break the repetition. Salt buys time but is limited. Instruments and communication can expose a presence, at a cost.\n\n" +
                "The Vestibule is a sanctuary: waiting there settles fear, and the front door lets you abandon a case. Injury does not heal. Reading this manual and the journal is safe. If lost, return here and reconsider the evidence.", "manual"));

            if (scenario.Archetype == Archetype.Mourner)
            {
                clues.Add(new Evidence("A visitor's warning", "This is a MOURNER. It mourns what the lamp could not save. Carrying any keepsake in lamplight near it rouses anger; lingering in its light is unwise. When grief gathers, turn the lantern OFF and WAIT. The shadow recedes when its vigil is respected.", "behavior"));
                clues.Add(new Evidence("A letter never sent", "Bring the authentic keepsake back to the place of the final vigil. PLACE it there, extinguish the lantern, then WAIT for THREE consecutive still beats. Moving, speaking, or disturbing the room breaks that silence. Only then RELEASE it. Darkness without the keepsake is not enough.", "ritual"));
                clues.Add(new Evidence("Three ink-black strokes", "The dying hand drew three strokes, each one a breath of silence. The unfinished vigil asks for three WAIT actions with the lamp dark after the true keepsake has been placed. The room must stay undisturbed until RELEASE.", "silence"));
                clues.Add(new Evidence("A child's recollection", "I remember " + scenario.Identity + " in the " + origin + ", holding something made of " + scenario.TargetMaterial + ". I remember the lamp most of all. Nobody ever put it out.", "corroboration"));
                clues.Add(new Evidence("An abandoned experiment", "We set a convincing copy down and performed the vigil. The lines buckled when we tried to release it. Copies imitate either the metal or the mark; only one keepsake joins both. A misplaced offering can always be picked back up.", "recovery"));
                clues.Add(new Evidence("A line under the wallpaper", "A mourner does not need to be hurt. Return what was loved, and give the waiting a quiet end. If the air grows sharp, darkness followed by waiting is safer than fleeing blindly with a lit lamp.", "motive"));
            }
            else if (scenario.Archetype == Archetype.Watcher)
            {
                clues.Add(new Evidence("The watch log", "This is a WATCHER. It fears what happens when a witness turns away. When it announces a bearing, FACE that direction and WAIT until it withdraws. Leaving its manifestation unwatched feeds its anger. You do not need to walk toward it.", "behavior"));
                clues.Add(new Evidence("The custodian's instructions", "Seal the two witness stations with ATTUNE; either order is permitted. Then PLACE the authentic keepsake at the final watch. Face the direction recorded in the death register and RELEASE. A witness seal survives travel and darkness.", "ritual"));
                clues.Add(new Evidence("Two witness statements", "The " + first + " and the " + second + " witnessed the final watch. ATTUNE in each to seal it; other rooms cannot witness the act. Both stations must be sealed before the final release.", "relays"));
                clues.Add(new Evidence("The death register", scenario.Identity + " was found in the " + origin + ", facing " + scenario.RitualFacing.ToString().ToUpperInvariant() + ". The final witness must face the same direction when releasing the keepsake. This is the ritual bearing, which may differ from a manifestation's bearing.", "facing"));
                clues.Add(new Evidence("A failed witness", "We sealed only one station and called it finished. The watch continued. Both " + first + " and " + second + " must bear a seal. We also mistook a copy for the original: compare both metal and engraving.", "recovery"));
                clues.Add(new Evidence("The guard's private note", "I can rest when someone witnesses the thing I feared to look away from. Keep your eyes on the bearing the apparition announces, then wait. Once it recedes, you may turn and continue your work.", "motive"));
            }
            else
            {
                string third = scenario.Rooms[scenario.RelayRooms[2]].Name;
                clues.Add(new Evidence("A phonograph transcript", "This is an ECHO. Repeated disturbances strengthen it: slamming doors, handling objects, attuning and speech can become a dangerous refrain. WAIT to break the rhythm when a repetition gathers. Silence gives it nothing to repeat.", "behavior"));
                clues.Add(new Evidence("The unfinished score", "ATTUNE the three refrain rooms in the score's order. Then PLACE the authentic keepsake where the last answer died. COMMUNICATE there to speak the assembled refrain, then RELEASE. A wrong station breaks the sequence; begin again at its first room. Progress survives travel.", "ritual"));
                clues.Add(new Evidence("The order of the verses", "The refrain begins in the " + first + " (" + scenario.TrueWords[0] + "), passes through the " + second + " (" + scenario.TrueWords[1] + "), and ends in the " + third + " (" + scenario.TrueWords[2] + "). ATTUNE those rooms in that exact order. The household always sang from first to last.", "sequence"));
                clues.Add(new Evidence("A word on each breath", "The completed answer reads " + string.Join(" / ", scenario.TrueWords) + ". Tuning the rooms in order restores the words. Speak the completed answer with COMMUNICATE over the original keepsake in the room where the last answer was cut short.", "phrase"));
                clues.Add(new Evidence("A conductor's correction", "A mistake in the refrain need not end the investigation. Return to the first verse, " + first + ", and ATTUNE again. Tune " + second + " next, then " + third + ". The last answer belongs to the " + origin + ", not to the final verse's room.", "recovery"));
                clues.Add(new Evidence("The singer's farewell", "The house doesn't want another noise. It wants the missing answer, in the right place. The sequence of rooms is a sentence. The keepsake identifies its speaker. The final room gives it meaning.", "motive"));
            }

            var destinations = new List<int>();
            for (int i = 1; i < scenario.Rooms.Count; i++) destinations.Add(i);
            dice.Shuffle(destinations);
            for (int i = 0; i < clues.Count; i++) scenario.Rooms[destinations[i]].Evidence.Add(clues[i]);

            for (int i = 0; i < scenario.Relics.Count; i++)
            {
                Relic relic = scenario.Relics[i];
                scenario.Rooms[relic.RoomId].Evidence.Add(new Evidence("Object on the table: " + relic.Name,
                    "A " + relic.Name.ToLowerInvariant() + ", made of " + relic.Material + ". Its engraving shows " + relic.Inscription +
                    ". Compare BOTH metal and mark with the records. This observation is preserved in your journal even if you leave the object here.", "relic." + i));
            }

            // Local marks support observation; they do not disclose the order on their own.
            if (scenario.Archetype != Archetype.Mourner)
            {
                for (int i = 0; i < scenario.RelayRooms.Length; i++)
                {
                    Room relay = scenario.Rooms[scenario.RelayRooms[i]];
                    relay.Evidence.Add(new Evidence(scenario.Archetype == Archetype.Watcher ? "A witness notch" : "A resonant inscription",
                        scenario.Archetype == Archetype.Watcher
                            ? "A geometric eye has been cut into the wall here. A witness station: ATTUNE can close it. The second station is described in the witness statements."
                            : "A word cut into the wall vibrates when approached: " + scenario.TrueWords[i] + ". This room is one voice of the refrain. Consult the score before you ATTUNE.",
                        "station." + i));
                }
            }
        }
    }
}
