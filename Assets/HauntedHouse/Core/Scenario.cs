using System;
using System.Collections.Generic;

namespace HollowHouse
{
    [Serializable]
    public struct GridPos : IEquatable<GridPos>
    {
        public int X;
        public int Y;
        public GridPos(int x, int y) { X = x; Y = y; }
        public bool Equals(GridPos other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is GridPos && Equals((GridPos)obj); }
        public override int GetHashCode() { unchecked { return (X * 397) ^ Y; } }
        public override string ToString() { return X + "," + Y; }
        public static bool operator ==(GridPos a, GridPos b) { return a.Equals(b); }
        public static bool operator !=(GridPos a, GridPos b) { return !a.Equals(b); }
        public static GridPos operator +(GridPos a, GridPos b) { return new GridPos(a.X + b.X, a.Y + b.Y); }
        public static GridPos operator -(GridPos a, GridPos b) { return new GridPos(a.X - b.X, a.Y - b.Y); }
    }

    public enum Facing { North, East, South, West }
    public enum Archetype { Mourner, Watcher, Echo }
    public enum Ending { Investigating, Won, Lost, Escaped }
    public enum RoomKind { Vestibule, Parlour, Nursery, Study, Kitchen, DiningRoom, Conservatory, Chapel, Bedchamber, Cellar }

    [Serializable]
    public sealed class Evidence
    {
        public string Title;
        public string Text;
        // A semantic key lets validation and future clue presentations share the same truth.
        public string FactKey;
        public Evidence() { }
        public Evidence(string title, string text, string factKey)
        { Title = title; Text = text; FactKey = factKey; }
    }

    [Serializable]
    public sealed class Room
    {
        public int Id;
        public string Name;
        public int X;
        public int Y;
        public RoomKind Kind;
        public List<Evidence> Evidence = new List<Evidence>();
        public int RelicIndex = -1;
        public GridPos Center { get { return new GridPos(X, Y); } }
    }

    [Serializable]
    public sealed class Cell
    {
        public GridPos Position;
        public int RoomId;
        public int DoorId = -1;
    }

    [Serializable]
    public sealed class Door
    {
        public int Id;
        public GridPos Position;
        public int A;
        public int B;
    }

    [Serializable]
    public sealed class Relic
    {
        public string Name;
        public string Material;
        public string Inscription;
        public int RoomId;
    }

    /// <summary>
    /// Immutable during play by convention. The generated truth is separate from observations
    /// and from GameSession's changing state; a renderer can be replaced without changing it.
    /// </summary>
    [Serializable]
    public sealed class Scenario
    {
        public int Seed;
        public string Identity;
        public string History;
        public Archetype Archetype;
        public List<Room> Rooms = new List<Room>();
        public Dictionary<GridPos, Cell> Cells = new Dictionary<GridPos, Cell>();
        public List<Door> Doors = new List<Door>();
        public List<Relic> Relics = new List<Relic>();
        public int AnchorRelic;
        public int OriginRoom;
        public int[] RelayRooms;
        public Facing RitualFacing;
        public string[] TrueWords;
        public GridPos Spawn;
        public int InitialGhostRoom;
        public string TargetMaterial;
        public string TargetInscription;

        private static readonly GridPos[] Steps =
        { new GridPos(0, 1), new GridPos(1, 0), new GridPos(0, -1), new GridPos(-1, 0) };

        public Room RoomAt(GridPos position)
        {
            Cell cell;
            return Cells.TryGetValue(position, out cell) && cell.RoomId >= 0 && cell.RoomId < Rooms.Count
                ? Rooms[cell.RoomId] : null;
        }

        public IEnumerable<GridPos> Neighbors(GridPos position)
        {
            for (int i = 0; i < Steps.Length; i++)
            {
                GridPos next = position + Steps[i];
                if (Cells.ContainsKey(next)) yield return next;
            }
        }

        public IEnumerable<int> AdjacentRooms(int roomId)
        {
            for (int i = 0; i < Doors.Count; i++)
            {
                Door door = Doors[i];
                if (door.A == roomId) yield return door.B;
                else if (door.B == roomId) yield return door.A;
            }
        }

        public List<GridPos> FindPath(GridPos from, GridPos to)
        {
            var result = new List<GridPos>();
            if (!Cells.ContainsKey(from) || !Cells.ContainsKey(to)) return result;
            var parent = new Dictionary<GridPos, GridPos>();
            var queue = new Queue<GridPos>();
            queue.Enqueue(from);
            parent.Add(from, from);
            while (queue.Count != 0)
            {
                GridPos current = queue.Dequeue();
                if (current == to)
                {
                    while (current != from)
                    { result.Add(current); current = parent[current]; }
                    result.Add(from);
                    result.Reverse();
                    return result;
                }
                foreach (GridPos next in Neighbors(current))
                {
                    if (parent.ContainsKey(next)) continue;
                    parent.Add(next, current);
                    queue.Enqueue(next);
                }
            }
            return result;
        }

        public int Distance(GridPos from, GridPos to)
        {
            List<GridPos> path = FindPath(from, to);
            return path.Count == 0 ? int.MaxValue : path.Count - 1;
        }

        public int RoomDistance(int from, int to)
        {
            if (from < 0 || from >= Rooms.Count || to < 0 || to >= Rooms.Count) return int.MaxValue;
            var distance = new int[Rooms.Count];
            for (int i = 0; i < distance.Length; i++) distance[i] = -1;
            var queue = new Queue<int>();
            queue.Enqueue(from);
            distance[from] = 0;
            while (queue.Count != 0)
            {
                int room = queue.Dequeue();
                if (room == to) return distance[room];
                foreach (int next in AdjacentRooms(room))
                {
                    if (distance[next] != -1) continue;
                    distance[next] = distance[room] + 1;
                    queue.Enqueue(next);
                }
            }
            return int.MaxValue;
        }
    }
}
