using System;
using System.Collections.Generic;

namespace HollowHouse
{
    /// <summary>
    /// The complete action-driven investigation, independent of Unity and presentation.
    /// Required objects are never consumed; danger can always be answered without a resource.
    /// </summary>
    public sealed class GameSession
    {
        private enum ActionKind { Move, Inspect, Door, Meter, Speak, Lantern, Wait, Ward, Relic, Attune, Release }
        private static readonly GridPos[] Steps =
        { new GridPos(0, 1), new GridPos(1, 0), new GridPos(0, -1), new GridPos(-1, 0) };

        public Scenario Scenario { get; private set; }
        public GridPos Position { get; private set; }
        public Facing Facing { get; private set; }
        public int Turn { get; private set; }
        public int Health { get; private set; }
        public int Fear { get; private set; }
        public int Salt { get; private set; }
        public int Charges { get; private set; }
        public int GhostRoom { get; private set; }
        public int GhostPressure { get; private set; }
        public int ThreatCountdown { get; private set; }
        public bool Lantern { get; private set; }
        public bool[] DoorsOpen { get; private set; }
        public HashSet<int> VisitedRooms { get; private set; }
        public HashSet<int> SearchedRooms { get; private set; }
        public HashSet<int> SealedRooms { get; private set; }
        public List<Evidence> Journal { get; private set; }
        public List<string> Log { get; private set; }
        public int CarriedRelic { get; private set; }
        public int PlacedRelic { get; private set; }
        public int PlacedRoom { get; private set; }
        public int RitualProgress { get; private set; }
        public Ending Ending { get; private set; }
        public bool SpiritAnswered { get; private set; }

        private int wardTurns;
        private int noisyStreak;
        private Facing warnedDirection;
        private Facing manifestationDirection;

        public int CurrentRoom
        {
            get { Room room = Scenario.RoomAt(Position); return room == null ? -1 : room.Id; }
        }
        public bool IsSanctuary { get { return CurrentRoom == 0; } }
        public bool GhostVisible
        {
            get { return Ending == Ending.Investigating && !IsSanctuary && Scenario.RoomDistance(CurrentRoom, GhostRoom) <= 1; }
        }
        public int WardTurns { get { return wardTurns; } }
        public Facing ThreatDirection
        {
            get
            {
                if (ThreatCountdown > 0) return warnedDirection;
                GridPos delta = Scenario.Rooms[GhostRoom].Center - Position;
                if (delta.X == 0 && delta.Y == 0) return manifestationDirection;
                if (Math.Abs(delta.X) > Math.Abs(delta.Y)) return delta.X > 0 ? Facing.East : Facing.West;
                return delta.Y > 0 ? Facing.North : Facing.South;
            }
        }
        public int DoorAhead
        {
            get
            {
                Cell cell;
                if (Scenario.Cells.TryGetValue(Position + Steps[(int)Facing], out cell) && cell.DoorId >= 0)
                    return cell.DoorId;
                return Scenario.Cells.TryGetValue(Position, out cell) ? cell.DoorId : -1;
            }
        }
        public bool RitualReady
        {
            get
            {
                if (CurrentRoom != Scenario.OriginRoom || PlacedRoom != Scenario.OriginRoom || PlacedRelic != Scenario.AnchorRelic)
                    return false;
                if (Scenario.Archetype == Archetype.Mourner) return !Lantern && RitualProgress >= 3;
                if (Scenario.Archetype == Archetype.Watcher)
                    return AllRelaysSealed() && Facing == Scenario.RitualFacing;
                return RitualProgress == Scenario.RelayRooms.Length && SpiritAnswered;
            }
        }
        public string ObjectiveHint
        {
            get
            {
                if (Ending == Ending.Won) return "The house is still. The haunting is resolved.";
                if (Ending == Ending.Lost) return "The house has taken another name.";
                if (Ending == Ending.Escaped) return "You survived. The house is still waiting.";
                if (ThreatCountdown > 0) return CounterInstruction();
                if (SearchedRooms.Count < 2) return "Inspect rooms. Read your journal. Find what happened here.";
                if (CarriedRelic < 0 && PlacedRelic < 0) return "Cross-reference the material and inscription before taking an anchor.";
                return "Apply the rite in your journal. The location and the haunting's rules both matter.";
            }
        }

        public GameSession(Scenario scenario)
        {
            if (scenario == null) throw new ArgumentNullException("scenario");
            Scenario = scenario;
            Position = scenario.Spawn;
            Facing = Facing.North;
            Health = 3;
            Salt = 4;
            Charges = 12;
            GhostRoom = scenario.InitialGhostRoom;
            Lantern = true;
            DoorsOpen = new bool[scenario.Doors.Count];
            VisitedRooms = new HashSet<int>();
            SearchedRooms = new HashSet<int>();
            SealedRooms = new HashSet<int>();
            Journal = new List<Evidence>();
            Log = new List<string>();
            CarriedRelic = -1;
            PlacedRelic = -1;
            PlacedRoom = -1;
            Ending = Ending.Investigating;
            manifestationDirection = (Facing)(Math.Abs((long)scenario.Seed) % 4);
            VisitedRooms.Add(CurrentRoom);
            Say("CASE " + scenario.Seed + ". The front door closes behind you. The vestibule is a sanctuary.");
            Say("Inspect each room for records and objects. Your journal preserves every clue. Turning and reading do not advance time.");
            Say("Three wounds end the investigation. Threats warn before striking. Salt buys time; understanding buys safety.");
        }

        public bool Move(int relativeDirection)
        {
            if (!Active()) return false;
            int direction = (((int)Facing + relativeDirection) % 4 + 4) % 4;
            GridPos target = Position + Steps[direction];
            Cell cell;
            if (!Scenario.Cells.TryGetValue(target, out cell)) return Reject("A solid wall. Turn or try another step.");
            if (cell.DoorId >= 0 && !DoorsOpen[cell.DoorId]) return Reject("The door is closed. Face it and open it first.");
            Cell current = Scenario.Cells[Position];
            if (current.DoorId >= 0 && !DoorsOpen[current.DoorId]) return Reject("Open this door before stepping through.");
            int oldRoom = CurrentRoom;
            Position = target;
            VisitedRooms.Add(CurrentRoom);
            if (oldRoom != CurrentRoom)
            {
                Say("Entered " + Scenario.Rooms[CurrentRoom].Name + ".");
                if (CurrentRoom == 0) Say("The vestibule's iron threshold holds. Wait here to steady yourself.");
            }
            Advance(ActionKind.Move);
            return true;
        }

        public void TurnFacing(int quarterTurns)
        {
            if (!Active()) return;
            Facing = (Facing)((((int)Facing + quarterTurns) % 4 + 4) % 4);
        }

        public bool Inspect()
        {
            if (!Active()) return false;
            Room room = Scenario.RoomAt(Position);
            if (room == null) return false;
            if (!SearchedRooms.Add(room.Id))
            {
                Say("You already recorded the evidence in " + room.Name + ". Read it in your journal.");
                DescribeRelic(room.Id);
                return false;
            }
            for (int i = 0; i < room.Evidence.Count; i++)
            {
                Journal.Add(room.Evidence[i]);
                Say(room.Evidence[i].Title + ": " + room.Evidence[i].Text);
            }
            if (room.Evidence.Count == 0) Say("Only old dust and disturbed outlines. No written evidence here.");
            DescribeRelic(room.Id);
            Advance(ActionKind.Inspect);
            return true;
        }

        public bool InteractDoor()
        {
            if (!Active()) return false;
            int door = DoorAhead;
            if (door < 0) return Reject("No door within reach. Move to the doorway and face it.");
            if (Scenario.Cells[Position].DoorId == door && DoorsOpen[door])
                return Reject("You are standing in the doorway. Step clear before closing it.");
            DoorsOpen[door] = !DoorsOpen[door];
            Say(DoorsOpen[door] ? "The door opens. Its frame trembles." : "The door closes. The haunting passes through walls, but you can still choose your route.");
            Advance(ActionKind.Door);
            return true;
        }

        public bool ReadMeter()
        {
            if (!Active()) return false;
            if (Charges == 0) return Reject("The field meter is exhausted. Written evidence still contains everything needed for the rite.");
            Charges--;
            int distance = Scenario.RoomDistance(CurrentRoom, GhostRoom);
            Say(distance == 0 ? "METER: the signal is inside this room." : distance == 1 ? "METER: a strong moving signal beyond a nearby wall." : "METER: the moving signal is distant.");
            if (CurrentRoom == Scenario.OriginRoom)
                Say("METER: a second, stationary signal under the floor. This room remembers the first event.");
            else Say("METER: no stationary origin signal here.");
            int relic = RelicInRoom(CurrentRoom);
            if (relic >= 0)
                DescribeMeterRelic(relic);
            else if (CarriedRelic >= 0)
                DescribeMeterRelic(CarriedRelic);
            Advance(ActionKind.Meter);
            return true;
        }

        public bool Communicate()
        {
            if (!Active()) return false;
            if (Scenario.Archetype == Archetype.Echo && CurrentRoom == Scenario.OriginRoom &&
                PlacedRoom == Scenario.OriginRoom && PlacedRelic == Scenario.AnchorRelic &&
                RitualProgress == Scenario.RelayRooms.Length)
            {
                SpiritAnswered = true;
                Say("You speak the recovered cadence: " + string.Join(" / ", Scenario.TrueWords) + ". A second voice moves your shadow's mouth. The last answer is RELEASE.");
            }
            else if (CurrentRoom == 0)
                Say("The threshold refuses an answer. The dead cannot cross this iron line.");
            else if (Scenario.Archetype == Archetype.Mourner)
                Say("A fingertip traces: 'The light makes me remember. Let the dark be still.'");
            else if (Scenario.Archetype == Archetype.Watcher)
                Say("Four corners turn toward you: 'If you look away, I become the distance between us.'");
            else if (RitualProgress < Scenario.RelayRooms.Length)
                Say("The room repeats your last movement. Between repetitions: 'Put our rooms in their remembered order. Leave silence between your noises.'");
            else Say("Your recovered cadence has no answering pulse here. The original object and the place of the first event must meet.");
            Advance(ActionKind.Speak);
            return true;
        }

        public bool ToggleLantern()
        {
            if (!Active()) return false;
            Lantern = !Lantern;
            Say(Lantern ? "Lantern raised. The house resolves into pale edges." : "Lantern shuttered. Nearby walls remain visible; distant lines fade.");
            Advance(ActionKind.Lantern);
            return true;
        }

        public bool Wait()
        {
            if (!Active()) return false;
            if (IsSanctuary) Say("You breathe behind the iron threshold. Fear recedes.");
            else if (ThreatCountdown == 0 && GhostPressure == 0) Say("You hold still. The house takes its turn.");
            Advance(ActionKind.Wait);
            return true;
        }

        public bool Ward()
        {
            if (!Active()) return false;
            if (Salt <= 0) return Reject("No salt remains. Use the haunting's protective rule or retreat to the vestibule.");
            Salt--;
            wardTurns = 4; // This action plus the next three actions.
            ThreatCountdown = 0;
            GhostPressure = 0;
            Fear = Math.Max(0, Fear - 12);
            Say("Salt draws a boundary around your shadow. Three further actions are protected.");
            Advance(ActionKind.Ward);
            return true;
        }

        public bool RelicAvailable(int index)
        {
            return index >= 0 && index < Scenario.Relics.Count && index != CarriedRelic && index != PlacedRelic;
        }

        public bool TakeRelic()
        {
            if (!Active()) return false;
            int relic = RelicInRoom(CurrentRoom);
            if (relic < 0) return Reject("There is no resting relic in this room.");
            if (CarriedRelic >= 0)
                Say("You release " + Scenario.Relics[CarriedRelic].Name + "; the house returns it to its original table.");
            if (relic == PlacedRelic) { PlacedRelic = -1; PlacedRoom = -1; }
            CarriedRelic = relic;
            SpiritAnswered = false;
            if (Scenario.Archetype == Archetype.Mourner) RitualProgress = 0;
            Relic item = Scenario.Relics[relic];
            Say("Carrying " + item.Name + ": " + item.Material + ", inscribed '" + item.Inscription + "'.");
            Advance(ActionKind.Relic);
            return true;
        }

        public bool PlaceRelic()
        {
            if (!Active()) return false;
            if (CarriedRelic < 0) return Reject("Your hands are empty. Take an object before placing it.");
            if (PlacedRelic >= 0)
                Say("The previously placed object returns to its original table.");
            PlacedRelic = CarriedRelic;
            PlacedRoom = CurrentRoom;
            CarriedRelic = -1;
            SpiritAnswered = false;
            if (Scenario.Archetype == Archetype.Mourner) RitualProgress = 0;
            Say("Placed " + Scenario.Relics[PlacedRelic].Name + " in " + Scenario.Rooms[PlacedRoom].Name + ".");
            Advance(ActionKind.Relic);
            return true;
        }

        public bool Attune()
        {
            if (!Active()) return false;
            if (Scenario.Archetype == Archetype.Mourner)
                return Reject("No geometry will console this presence. Its rite requires the anchor and stillness.");
            if (Scenario.Archetype == Archetype.Watcher)
            {
                if (SealedRooms.Contains(CurrentRoom)) return Reject("This room's reflected angle is already sealed.");
                bool relay = Array.IndexOf(Scenario.RelayRooms, CurrentRoom) >= 0;
                if (relay)
                {
                    SealedRooms.Add(CurrentRoom);
                    RitualProgress = SealedRooms.Count;
                    Say("You turn the reflected angle inward. A seal holds in " + Scenario.Rooms[CurrentRoom].Name + ".");
                }
                else Say("Your chalk lines refuse to meet. This room is not one of the reflected angles named in the record.");
            }
            else
            {
                if (RitualProgress == Scenario.RelayRooms.Length)
                    return Reject("The full cadence is held. Bring the original anchor to the first event and communicate.");
                if (CurrentRoom == Scenario.RelayRooms[RitualProgress])
                {
                    SealedRooms.Add(CurrentRoom);
                    Say("The room retains '" + Scenario.TrueWords[RitualProgress] + "'. Cadence " + (RitualProgress + 1) + "/" + Scenario.RelayRooms.Length + ".");
                    RitualProgress++;
                }
                else
                {
                    RitualProgress = CurrentRoom == Scenario.RelayRooms[0] ? 1 : 0;
                    SealedRooms.Clear();
                    if (RitualProgress == 1) SealedRooms.Add(CurrentRoom);
                    SpiritAnswered = false;
                    Say("The cadence breaks. " + (RitualProgress == 1 ? "The first room begins it again." : "Begin again at the first room named in the sequence."));
                }
            }
            Advance(ActionKind.Attune);
            return true;
        }

        public bool Release()
        {
            if (!Active()) return false;
            if (!RitualReady)
            {
                Say("The house does not release its hold. Check the anchor, the place of the first event, and every condition of the rite.");
                Advance(ActionKind.Release);
                return true;
            }
            Ending = Ending.Won;
            Turn++;
            ThreatCountdown = 0;
            GhostPressure = 0;
            Fear = 0;
            Say("The outline of " + Scenario.Identity + " becomes almost human. Then every line opens. The house releases its dead.");
            Say("CASE CLOSED in " + Turn + " actions. " + Health + " wounds remaining. Seed " + Scenario.Seed + ".");
            return true;
        }

        public bool Escape()
        {
            if (!Active()) return false;
            if (!IsSanctuary) return Reject("The front door is in the vestibule. Return there to abandon this case alive.");
            Ending = Ending.Escaped;
            ThreatCountdown = 0;
            Say("You step beyond the iron threshold. You survived; the haunting remains unresolved. Seed " + Scenario.Seed + ".");
            return true;
        }

        private bool Active() { return Ending == Ending.Investigating; }
        private bool Reject(string text) { Say(text); return false; }
        private void Say(string text)
        {
            Log.Add(text);
            if (Log.Count > 80) Log.RemoveAt(0);
        }

        private int RelicInRoom(int roomId)
        {
            if (PlacedRelic >= 0 && PlacedRoom == roomId) return PlacedRelic;
            for (int i = 0; i < Scenario.Relics.Count; i++)
                if (Scenario.Relics[i].RoomId == roomId && RelicAvailable(i)) return i;
            return -1;
        }

        private void DescribeRelic(int roomId)
        {
            int index = RelicInRoom(roomId);
            if (index < 0) return;
            Relic relic = Scenario.Relics[index];
            Say("An object rests here: " + relic.Name + ", made of " + relic.Material + ", inscribed '" + relic.Inscription + "'. You can take it.");
        }

        private void DescribeMeterRelic(int index)
        {
            Relic relic = Scenario.Relics[index];
            Say("METER: " + relic.Name + (relic.Material == Scenario.TargetMaterial
                ? " retains a pulse in its metal. A copy in the same metal also resonates: check the engraving against written evidence."
                : " is inert. This metal cannot carry the original haunting."));
        }

        private bool AllRelaysSealed()
        {
            for (int i = 0; i < Scenario.RelayRooms.Length; i++)
                if (!SealedRooms.Contains(Scenario.RelayRooms[i])) return false;
            return true;
        }

        private string CounterInstruction()
        {
            if (Scenario.Archetype == Archetype.Mourner) return "THREAT: shutter the lantern, then WAIT in darkness. Salt also protects.";
            if (Scenario.Archetype == Archetype.Watcher) return "THREAT: face " + ThreatDirection.ToString().ToUpperInvariant() + ", then WAIT. Turning costs no time.";
            return "THREAT: WAIT. Silence interrupts the repeated noise. Salt also protects.";
        }

        private bool IsNoisy(ActionKind kind)
        {
            return kind == ActionKind.Door || kind == ActionKind.Speak || kind == ActionKind.Relic ||
                kind == ActionKind.Attune || kind == ActionKind.Release || kind == ActionKind.Inspect;
        }

        private void Advance(ActionKind action)
        {
            Turn++;
            bool noisy = IsNoisy(action);
            noisyStreak = noisy ? noisyStreak + 1 : 0;
            UpdateRitual(action);

            if (IsSanctuary)
            {
                ThreatCountdown = 0;
                GhostPressure = 0;
                Fear = Math.Max(0, Fear - (action == ActionKind.Wait ? 14 : 4));
                if (wardTurns > 0) wardTurns--;
                MoveGhost(noisy);
                return;
            }

            bool protective = action == ActionKind.Wait &&
                ((Scenario.Archetype == Archetype.Mourner && !Lantern) ||
                 (Scenario.Archetype == Archetype.Watcher && Facing == ThreatDirection) ||
                  Scenario.Archetype == Archetype.Echo);
            if (protective || wardTurns > 0)
            {
                if (ThreatCountdown > 0 || GhostPressure > 0)
                    Say(protective ? "You obey the rule. The threatening lines loosen their grip." : "The presence stops at the salt boundary.");
                ThreatCountdown = 0;
                GhostPressure = 0;
                Fear = Math.Max(0, Fear - (protective ? 5 : 2));
                if (wardTurns > 0) wardTurns--;
                MoveGhost(noisy);
                return;
            }

            if (ThreatCountdown > 0)
            {
                ThreatCountdown--;
                if (ThreatCountdown == 0) Strike();
                else Say("The outline contracts. ONE action remains. " + CounterInstruction());
                return;
            }

            int distance = Scenario.RoomDistance(CurrentRoom, GhostRoom);
            int provocation = 0;
            if (distance <= 1)
            {
                if (Scenario.Archetype == Archetype.Mourner && Lantern)
                    provocation = CarriedRelic >= 0 || noisy ? 2 : action == ActionKind.Move ? 1 : 0;
                else if (Scenario.Archetype == Archetype.Watcher && Facing != ThreatDirection)
                    provocation = noisy || action == ActionKind.Move ? 2 : 1;
                else if (Scenario.Archetype == Archetype.Echo && noisyStreak >= 2)
                    provocation = 3;
            }
            // Repeated noisy experiments attract an Echo even from a distant room.
            else if (Scenario.Archetype == Archetype.Echo && noisyStreak >= 3) provocation = 2;

            if (provocation > 0)
            {
                GhostPressure = Math.Min(8, GhostPressure + provocation);
                Fear = Math.Min(100, Fear + provocation);
                if (GhostPressure >= 3)
                {
                    if (Scenario.Archetype == Archetype.Mourner) Say("The lantern catches a hunched outline. Light and disturbed objects are feeding its grief.");
                    else if (Scenario.Archetype == Archetype.Watcher) Say("An angle moves where you are not looking. Its bearing is " + ThreatDirection.ToString().ToUpperInvariant() + ".");
                    else Say("Your noises return twice. A third repetition begins to take your shape. Leave a silent wait between noisy actions.");
                }
            }
            else
            {
                GhostPressure = Math.Max(0, GhostPressure - 1);
                if (action == ActionKind.Wait) Fear = Math.Max(0, Fear - 2);
            }

            int threshold = Fear >= 75 ? 4 : 6;
            if (GhostPressure >= threshold)
            {
                warnedDirection = ThreatDirection;
                ThreatCountdown = 2;
                Say("A hostile shape locks onto you. TWO actions before it strikes. " + CounterInstruction());
            }
            else MoveGhost(noisy);
        }

        private void Strike()
        {
            Health--;
            Fear = Math.Min(100, Fear + 22);
            GhostPressure = 0;
            // Injury gives time to understand the error; it cannot produce an immediate second strike.
            wardTurns = 3;
            if (Health <= 0)
            {
                Ending = Ending.Lost;
                Say("The shape folds through you. Your lantern falls without a sound. The house has another name to keep.");
            }
            else
            {
                Say("The presence tears through your outline. One wound. " + Health + " remain. " + CounterInstruction());
                if (Fear >= 75) Say("PANIC: the presence gathers faster. Rest in the vestibule or perform its protective action to recover.");
            }
        }

        private void UpdateRitual(ActionKind action)
        {
            if (Scenario.Archetype != Archetype.Mourner) return;
            bool validPlace = CurrentRoom == Scenario.OriginRoom && PlacedRoom == Scenario.OriginRoom && PlacedRelic == Scenario.AnchorRelic;
            if (!validPlace || Lantern)
            {
                RitualProgress = 0;
                return;
            }
            if (action == ActionKind.Wait)
            {
                RitualProgress = Math.Min(3, RitualProgress + 1);
                Say("The original object grows still in the dark. Vigil " + RitualProgress + "/3." + (RitualProgress == 3 ? " RELEASE can now end the grief." : " Keep waiting."));
            }
            else if (action != ActionKind.Lantern && action != ActionKind.Release)
            {
                if (RitualProgress > 0) Say("Movement breaks the vigil. The three silent waits must be consecutive.");
                RitualProgress = 0;
            }
        }

        private void MoveGhost(bool attracted)
        {
            if (!Active() || Turn % 6 != 0) return;
            int old = GhostRoom;
            var choices = new List<int>();
            foreach (int room in Scenario.AdjacentRooms(GhostRoom))
            {
                if (room != 0)
                {
                    if (!choices.Contains(room)) choices.Add(room);
                }
                else
                {
                    // Influence can reach another wing around the protected threshold;
                    // the manifestation itself can never enter the sanctuary.
                    foreach (int beyond in Scenario.AdjacentRooms(0))
                        if (beyond != GhostRoom && !choices.Contains(beyond)) choices.Add(beyond);
                }
            }
            if (choices.Count == 0) return;
            choices.Sort();
            int pick = (int)((Math.Abs((long)Scenario.Seed) + Turn / 6) % choices.Count);
            int next = choices[pick];
            if (!IsSanctuary && (attracted || CarriedRelic >= 0))
            {
                int best = Scenario.RoomDistance(next, CurrentRoom);
                for (int i = 0; i < choices.Count; i++)
                {
                    int candidate = Scenario.RoomDistance(choices[i], CurrentRoom);
                    if (candidate < best) { best = candidate; next = choices[i]; }
                }
                // A presence already sharing the room lingers when provoked.
                if (GhostRoom == CurrentRoom) next = GhostRoom;
            }
            GhostRoom = next;
            if (old != GhostRoom)
            {
                manifestationDirection = (Facing)((Turn / 6 + GhostRoom) % 4);
                if (Scenario.RoomDistance(CurrentRoom, GhostRoom) <= 1 && !IsSanctuary)
                    Say("The house's outlines lean toward " + ThreatDirection.ToString().ToUpperInvariant() + ". Something has moved nearby.");
            }
        }
    }
}
