using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HollowHouse
{
    /// <summary>
    /// Replaceable presentation for the simulation. Every visible object is a code-built mesh.
    /// Solid, depth-writing walls and furniture hide lines behind them, including other rooms.
    /// </summary>
    public sealed class HouseRenderer : MonoBehaviour
    {
        public const float CellSize = 2.6f;
        const float WallHeight = 3.35f;
        static readonly Color Architecture = new Color(.29f, .58f, .60f);
        static readonly Color Trim = new Color(.45f, .67f, .64f);
        static readonly Color Faint = new Color(.075f, .17f, .19f);
        static readonly Color Furniture = new Color(.37f, .46f, .43f);
        static readonly Color Gold = new Color(.91f, .65f, .28f);
        static readonly Color Black = new Color(.004f, .008f, .012f);
        static readonly Color Red = new Color(.91f, .23f, .24f);
        static readonly GridPos[] Directions =
        { new GridPos(0, 1), new GridPos(1, 0), new GridPos(0, -1), new GridPos(-1, 0) };

        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly Dictionary<int, Transform> doorPanels = new Dictionary<int, Transform>();
        readonly Dictionary<int, Transform> evidenceMarkers = new Dictionary<int, Transform>();
        readonly Dictionary<int, GameObject> wards = new Dictionary<int, GameObject>();
        readonly Dictionary<int, Transform> relics = new Dictionary<int, Transform>();
        readonly List<Transform> ghostRings = new List<Transform>();
        Material surfaceMaterial, lineMaterial, ghostMaterial;
        Transform geometryRoot, ghostRoot, ritualRoot, placedRelicRoot;
        GameObject[] ritualSegments;
        Scenario scenario;
        Camera viewCamera;
        Vector3 viewFrom, viewTarget;
        Quaternion rotationFrom, rotationTarget;
        GridPos lastPosition;
        Facing lastFacing;
        float viewChangedAt;
        bool viewInitialized;

        public Camera ViewCamera { get { return viewCamera; } }
        public Vector3 World(GridPos position) { return new Vector3(position.X * CellSize, 0, position.Y * CellSize); }

        public void Build(Scenario generatedScenario)
        {
            ClearGeometry();
            scenario = generatedScenario;
            surfaceMaterial = CreateMaterial("HollowHouse/VectorSurface");
            lineMaterial = CreateMaterial("HollowHouse/VectorLine");
            ghostMaterial = CreateMaterial("HollowHouse/VectorLine");
            geometryRoot = new GameObject("Generated house — opaque hidden-line geometry").transform;
            geometryRoot.SetParent(transform, false);
            if (viewCamera == null)
            {
                var cameraObject = new GameObject("Investigator camera");
                cameraObject.transform.SetParent(transform, false);
                viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.tag = "MainCamera";
                viewCamera.clearFlags = CameraClearFlags.SolidColor;
                viewCamera.backgroundColor = new Color(.001f, .003f, .007f);
                viewCamera.nearClipPlane = .07f;
                viewCamera.farClipPlane = 72f;
                viewCamera.fieldOfView = 68f;
                viewCamera.allowHDR = false;
                viewCamera.allowMSAA = true;
            }

            var surfaces = new Geometry();
            var lines = new Geometry();
            foreach (Cell cell in scenario.Cells.Values)
            {
                Vector3 center = World(cell.Position);
                float half = CellSize * .5f;
                surfaces.Quad(center + new Vector3(-half, -.025f, -half), center + new Vector3(-half, -.025f, half),
                    center + new Vector3(half, -.025f, half), center + new Vector3(half, -.025f, -half), Black);
                surfaces.Quad(center + new Vector3(-half, WallHeight, -half), center + new Vector3(half, WallHeight, -half),
                    center + new Vector3(half, WallHeight, half), center + new Vector3(-half, WallHeight, half), Black);
                // Small floor ticks show discrete movement without drawing a grid through the house.
                lines.Edge(center + new Vector3(-.07f, .006f, 0), center + new Vector3(.07f, .006f, 0), Faint, .009f);
                lines.Edge(center + new Vector3(0, .006f, -.07f), center + new Vector3(0, .006f, .07f), Faint, .009f);
                for (int direction = 0; direction < 4; direction++)
                {
                    if (scenario.Cells.ContainsKey(cell.Position + Directions[direction])) continue;
                    Quaternion rotation = Quaternion.Euler(0, direction * 90, 0);
                    Vector3 a = center + rotation * new Vector3(-half, 0, half);
                    Vector3 b = center + rotation * new Vector3(half, 0, half);
                    surfaces.Quad(a, b, b + Vector3.up * WallHeight, a + Vector3.up * WallHeight, Black);
                    Vector3 inward = rotation * Vector3.back * .016f;
                    lines.Edge(a + inward + Vector3.up * .12f, b + inward + Vector3.up * .12f, Architecture, .012f);
                    lines.Edge(a + inward + Vector3.up * 2.92f, b + inward + Vector3.up * 2.92f, Architecture, .011f);
                    lines.Edge(a + inward, a + inward + Vector3.up * WallHeight, Faint, .009f);
                    lines.Edge(a + inward + Vector3.up * 3.2f, b + inward + Vector3.up * 3.2f, Trim, .012f);
                    Vector3 panelA = a + (b - a).normalized * .13f + inward;
                    Vector3 panelB = b - (b - a).normalized * .13f + inward;
                    lines.Edge(panelA + Vector3.up * .42f, panelB + Vector3.up * .42f, Faint, .008f);
                    lines.Edge(panelA + Vector3.up * .42f, panelA + Vector3.up * 2.6f, Faint, .008f);
                    lines.Edge(panelB + Vector3.up * .42f, panelB + Vector3.up * 2.6f, Faint, .008f);
                    lines.Edge(panelA + Vector3.up * 2.6f, panelB + Vector3.up * 2.6f, Faint, .008f);
                }
            }
            foreach (Room room in scenario.Rooms) BuildRoom(room, surfaces, lines);
            MakeMesh("Architecture / depth", surfaces, surfaceMaterial, geometryRoot);
            MakeMesh("Architecture / vectors", lines, lineMaterial, geometryRoot);
            foreach (Door door in scenario.Doors) BuildDoor(door);
            BuildGhost();
            BuildRitual();
            BuildPlacedRelic();
            viewInitialized = false;
            SetView(scenario.Spawn, Facing.North, true);
        }

        public void SetView(GridPos position, Facing facing, bool immediate = false)
        {
            if (viewCamera == null) return;
            if (viewInitialized && position == lastPosition && facing == lastFacing && !immediate) return;
            viewFrom = viewCamera.transform.position;
            rotationFrom = viewCamera.transform.rotation;
            viewTarget = World(position) + Vector3.up * 1.62f;
            rotationTarget = Quaternion.Euler(0, (int)facing * 90, 0);
            viewChangedAt = Time.unscaledTime;
            lastPosition = position;
            lastFacing = facing;
            if (immediate || !viewInitialized)
            {
                viewFrom = viewTarget;
                rotationFrom = rotationTarget;
                viewCamera.transform.SetPositionAndRotation(viewTarget, rotationTarget);
            }
            viewInitialized = true;
        }

        public void Sync(GameSession session, float time)
        {
            if (scenario == null || viewCamera == null) return;
            SetView(session.Position, session.Facing);
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - viewChangedAt) / .19f));
            Vector3 cameraPosition = Vector3.Lerp(viewFrom, viewTarget, t);
            cameraPosition.y += Mathf.Sin(t * Mathf.PI) * .045f;
            Quaternion cameraRotation = Quaternion.Slerp(rotationFrom, rotationTarget, t);
            if (session.ThreatCountdown > 0)
                cameraRotation *= Quaternion.Euler(0, 0, Mathf.Sin(time * 9) * .3f);
            viewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            float exposure = session.Lantern ? 1f : .43f;
            if (session.ThreatCountdown > 0) exposure *= .84f + .16f * Mathf.Sin(time * 11.1f);
            lineMaterial.SetFloat("_Exposure", exposure);
            surfaceMaterial.SetFloat("_Exposure", exposure);
            foreach (var pair in doorPanels)
            {
                bool open = pair.Key < session.DoorsOpen.Length && session.DoorsOpen[pair.Key];
                pair.Value.localRotation = Quaternion.Slerp(pair.Value.localRotation,
                    Quaternion.Euler(0, open ? -94 : 0, 0), Mathf.Clamp01(Time.unscaledDeltaTime * 14));
            }
            foreach (var pair in evidenceMarkers)
            {
                bool searched = session.SearchedRooms.Contains(pair.Key);
                pair.Value.localScale = Vector3.one * (searched ? .45f : 1f);
                pair.Value.localRotation = Quaternion.Euler(0, time * (searched ? 6 : 17), 0);
            }
            foreach (var pair in wards) pair.Value.SetActive(session.SealedRooms.Contains(pair.Key));
            foreach (var pair in relics)
            {
                pair.Value.gameObject.SetActive(session.RelicAvailable(pair.Key));
                pair.Value.localRotation = Quaternion.Euler(0, 18 + Mathf.Sin(time * .55f) * 5, 0);
            }
            bool placed = session.PlacedRelic >= 0 && session.PlacedRoom >= 0 && session.PlacedRoom < scenario.Rooms.Count;
            placedRelicRoot.gameObject.SetActive(placed);
            if (placed) placedRelicRoot.position = World(scenario.Rooms[session.PlacedRoom].Center) + Vector3.up * .28f;
            bool knowsOrigin = false;
            for (int i = 0; i < session.Journal.Count; i++)
                if (session.Journal[i].FactKey == "origin") { knowsOrigin = true; break; }
            ritualRoot.gameObject.SetActive(knowsOrigin && session.VisitedRooms.Contains(scenario.OriginRoom));
            for (int i = 0; i < ritualSegments.Length; i++)
                ritualSegments[i].SetActive(session.RitualProgress > i);

            ghostRoot.gameObject.SetActive((session.GhostVisible || session.ThreatCountdown > 0) && session.GhostRoom >= 0 && session.GhostRoom < scenario.Rooms.Count);
            if (ghostRoot.gameObject.activeSelf)
            {
                Vector3 roomCenter = World(scenario.Rooms[session.GhostRoom].Center);
                // Between the walking lanes, never inside the player or a wall.
                ghostRoot.position = roomCenter + new Vector3(-1.3f, .28f + Mathf.Sin(time * 1.7f) * .14f, 1.3f);
                ghostRoot.localScale = Vector3.one;
                if (session.ThreatCountdown > 0 || scenario.Archetype == Archetype.Watcher)
                {
                    // A manifestation projects at the same bearing the rules report.
                    // The Watcher always uses this rule, even before an attack warning:
                    // looking directly at its visible apparition must count as watching it.
                    Vector3 bearing = Quaternion.Euler(0, (int)session.ThreatDirection * 90, 0) * Vector3.forward;
                    bool wallAhead = !scenario.Cells.ContainsKey(session.Position + Directions[(int)session.ThreatDirection]);
                    float distance = wallAhead ? .74f : 1.05f;
                    float scale = wallAhead ? .48f : .73f;
                    float height = wallAhead ? .79f : .40f;
                    // Even the largest rotated orbit remains inside the current tile before a wall:
                    // .74 + (.92 * .48) < the 1.3m half-cell. Side clearance is smaller still.
                    ghostRoot.position = World(session.Position) + bearing * distance + Vector3.up * (height + Mathf.Sin(time * 1.7f) * .08f);
                    ghostRoot.localScale = Vector3.one * scale;
                }
                Vector3 look = viewCamera.transform.position - ghostRoot.position;
                look.y = 0;
                if (look.sqrMagnitude > .001f) ghostRoot.rotation = Quaternion.LookRotation(look);
                for (int i = 0; i < ghostRings.Count; i++)
                    ghostRings[i].localRotation = Quaternion.Euler(18 * Mathf.Sin(time * .7f + i), time * (i % 2 == 0 ? 21 : -17) + i * 37, i * 31);
                ghostMaterial.SetFloat("_Exposure", .75f + .22f * Mathf.Sin(time * 3.3f));
            }
        }

        void BuildRoom(Room room, Geometry surfaces, Geometry lines)
        {
            Vector3 c = World(room.Center);
            // The compass in every room is a navigation affordance, not a clue to the true site.
            Ring(lines, c + Vector3.up * .008f, 1.03f, 16, Faint, .009f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 direction = Quaternion.Euler(0, i * 90, 0) * Vector3.forward;
                lines.Edge(c + direction * 1.08f + Vector3.up * .009f, c + direction * 1.25f + Vector3.up * .009f, Trim * .6f, .011f);
            }
            // Compass north is distinguished by a double mark.
            lines.Edge(c + new Vector3(-.08f, .01f, 1.27f), c + new Vector3(.08f, .01f, 1.27f), Gold * .65f, .01f);
            Vector3 nook = c + new Vector3(-1.3f, 0, -1.3f);
            switch (room.Kind)
            {
                case RoomKind.Vestibule:
                    Clock(c + new Vector3(-3.47f, 0, -2.55f), surfaces, lines);
                    Console(c + new Vector3(-2.55f, 0, -3.5f), surfaces, lines, 1.7f);
                    Mirror(c + new Vector3(-2.55f, 1.85f, -3.85f), lines, .68f, .9f);
                    break;
                case RoomKind.Parlour:
                    Piano(c + new Vector3(-2.55f, 0, -3.42f), surfaces, lines);
                    Chair(nook, surfaces, lines);
                    Lamp(c + new Vector3(-3.37f, 0, 3.37f), lines);
                    break;
                case RoomKind.Nursery:
                    Crib(nook, surfaces, lines);
                    Mobile(nook + Vector3.up * 2.45f, lines);
                    Console(c + new Vector3(-2.55f, 0, -3.5f), surfaces, lines, 1.4f);
                    break;
                case RoomKind.Study:
                    Bookshelf(c + new Vector3(-2.6f, 0, -3.55f), surfaces, lines, room.Id);
                    Bookshelf(c + new Vector3(2.6f, 0, -3.55f), surfaces, lines, room.Id + 4);
                    Table(nook, surfaces, lines, new Vector2(1.45f, 1.1f));
                    break;
                case RoomKind.Kitchen:
                    Stove(c + new Vector3(-2.55f, 0, -3.47f), surfaces, lines);
                    Console(c + new Vector3(-3.47f, 0, 2.55f), surfaces, lines, .55f);
                    HangingPans(c + new Vector3(-2.55f, 2.35f, -3.70f), lines);
                    break;
                case RoomKind.DiningRoom:
                    Table(nook, surfaces, lines, new Vector2(1.45f, 1.45f));
                    Candle(nook + new Vector3(0, .86f, 0), lines);
                    Console(c + new Vector3(-2.55f, 0, -3.5f), surfaces, lines, 2.1f);
                    break;
                case RoomKind.Conservatory:
                    Plant(nook, surfaces, lines);
                    Plant(c + new Vector3(3.33f, 0, -3.28f), surfaces, lines);
                    Trellis(c + new Vector3(-2.55f, 0, -3.84f), lines);
                    break;
                case RoomKind.Chapel:
                    Console(c + new Vector3(-2.55f, 0, -3.48f), surfaces, lines, 2.2f);
                    lines.Edge(c + new Vector3(-2.55f, 1.45f, -3.8f), c + new Vector3(-2.55f, 2.82f, -3.8f), Trim, .033f);
                    lines.Edge(c + new Vector3(-2.95f, 2.35f, -3.8f), c + new Vector3(-2.15f, 2.35f, -3.8f), Trim, .033f);
                    Candle(c + new Vector3(-3.35f, .96f, -3.48f), lines);
                    Candle(c + new Vector3(-1.75f, .96f, -3.48f), lines);
                    Chair(nook, surfaces, lines);
                    break;
                case RoomKind.Bedchamber:
                    Bed(c + new Vector3(-3.37f, 0, -2.55f), surfaces, lines);
                    Mirror(c + new Vector3(-2.55f, 1.9f, -3.85f), lines, .8f, 1.2f);
                    Console(c + new Vector3(-2.55f, 0, -3.5f), surfaces, lines, 1.5f);
                    break;
                case RoomKind.Cellar:
                    Barrel(nook, surfaces, lines);
                    Barrel(c + new Vector3(-3.3f, 0, 3.3f), surfaces, lines);
                    Bookshelf(c + new Vector3(-2.6f, 0, -3.55f), surfaces, lines, 2);
                    break;
            }

            // A document station is present for every room's searchable evidence.
            Vector3 station = c + new Vector3(1.3f, 0, 1.3f);
            Table(station, surfaces, lines, new Vector2(.92f, .75f));
            Vector3 paper = station + new Vector3(0, .872f, 0);
            lines.Rect(paper, Vector3.right * .2f, Vector3.forward * .25f, Gold * .8f, .012f);
            for (int i = 0; i < 3; i++) lines.Edge(paper + new Vector3(-.12f, .003f, -.12f + i * .09f), paper + new Vector3(.1f, .003f, -.12f + i * .09f), Gold * .65f, .008f);
            if (room.Evidence.Count > 0)
            {
                var markerLines = new Geometry();
                Diamond(markerLines, Vector3.zero, .085f, .15f, Gold, .011f);
                Transform marker = MakeMesh("Unread testimony / room " + room.Id, markerLines, lineMaterial, geometryRoot).transform;
                marker.position = station + Vector3.up * 1.24f;
                evidenceMarkers.Add(room.Id, marker);
            }
            if (room.RelicIndex >= 0)
            {
                Transform relic = new GameObject("Relic " + room.RelicIndex).transform;
                relic.SetParent(geometryRoot, false);
                relic.position = c + new Vector3(1.3f, 1f, -1.3f);
                Table(c + new Vector3(1.3f, 0, -1.3f), surfaces, lines, new Vector2(.64f, .64f));
                var relicShape = new Geometry();
                Relic relicInfo = scenario.Relics[room.RelicIndex];
                Color relicColor = relicInfo.Material == "silver" ? new Color(.70f, .79f, .79f)
                    : relicInfo.Material == "iron" ? new Color(.46f, .51f, .56f)
                    : relicInfo.Material == "copper" ? new Color(.81f, .47f, .31f) : Gold;
                DrawRelic(relicShape, relicInfo.Name, relicColor);
                MakeMesh("Relic vectors", relicShape, lineMaterial, relic);
                relics.Add(room.RelicIndex, relic);
            }
            var wardGeometry = new Geometry();
            Ring(wardGeometry, Vector3.up * .028f, .64f, 24, Gold, .021f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2 / 3;
                float b = (i + 1) * Mathf.PI * 2 / 3;
                wardGeometry.Edge(new Vector3(Mathf.Sin(a) * .62f, .03f, Mathf.Cos(a) * .62f),
                    new Vector3(Mathf.Sin(b) * .62f, .03f, Mathf.Cos(b) * .62f), Gold, .013f);
            }
            GameObject ward = MakeMesh("Player seal / room " + room.Id, wardGeometry, lineMaterial, geometryRoot);
            ward.transform.position = c;
            ward.SetActive(false);
            wards.Add(room.Id, ward);
        }

        void BuildDoor(Door door)
        {
            Transform doorway = new GameObject("Door " + door.Id).transform;
            doorway.SetParent(geometryRoot, false);
            doorway.position = World(door.Position);
            Room a = scenario.Rooms[door.A], b = scenario.Rooms[door.B];
            doorway.rotation = Quaternion.Euler(0, a.X == b.X ? 0 : 90, 0);
            var surface = new Geometry();
            var line = new Geometry();
            Box(surface, line, new Vector3(-1.23f, 1.52f, 0), new Vector3(.14f, 3.04f, .20f), Trim);
            Box(surface, line, new Vector3(1.23f, 1.52f, 0), new Vector3(.14f, 3.04f, .20f), Trim);
            Box(surface, line, new Vector3(0, 3.03f, 0), new Vector3(2.6f, .16f, .2f), Trim);
            surface.Box(new Vector3(0, 3.23f, 0), new Vector3(2.6f, .24f, .10f), Black);
            line.Edge(new Vector3(-1.17f, .014f, -.14f), new Vector3(1.17f, .014f, -.14f), Gold * .55f, .014f);
            line.Edge(new Vector3(-1.17f, .014f, .14f), new Vector3(1.17f, .014f, .14f), Gold * .55f, .014f);
            MakeMesh("Doorframe depth", surface, surfaceMaterial, doorway);
            MakeMesh("Doorframe vectors", line, lineMaterial, doorway);
            Transform hinge = new GameObject("Hinged panel").transform;
            hinge.SetParent(doorway, false);
            hinge.localPosition = new Vector3(-1.15f, 0, 0);
            surface = new Geometry(); line = new Geometry();
            Box(surface, line, new Vector3(1.15f, 1.44f, 0), new Vector3(2.3f, 2.86f, .10f), Architecture);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * .06f;
                line.Rect(new Vector3(1.15f, 1.96f, z), Vector3.right * .89f, Vector3.up * .65f, Faint * 1.9f, .01f);
                line.Rect(new Vector3(1.15f, .68f, z), Vector3.right * .89f, Vector3.up * .44f, Faint * 1.9f, .01f);
                Diamond(line, new Vector3(2.03f, 1.25f, z + side * .025f), .047f, .075f, Gold, .012f);
            }
            MakeMesh("Closed door depth", surface, surfaceMaterial, hinge);
            MakeMesh("Door vectors", line, lineMaterial, hinge);
            doorPanels.Add(door.Id, hinge);
        }

        void BuildGhost()
        {
            ghostRoot = new GameObject("Manifestation").transform;
            ghostRoot.SetParent(geometryRoot, false);
            var shape = new Geometry();
            // An impossible hollow face, suspended ribs and disconnected hands.
            Diamond(shape, new Vector3(0, 1.55f, 0), .32f, .57f, Red, .021f);
            shape.Edge(new Vector3(-.23f, 1.61f, .09f), new Vector3(-.07f, 1.59f, .09f), Gold, .021f);
            shape.Edge(new Vector3(.07f, 1.59f, .09f), new Vector3(.23f, 1.61f, .09f), Gold, .021f);
            shape.Edge(new Vector3(-.07f, 1.23f, .05f), new Vector3(.07f, 1.20f, .05f), Red, .013f);
            for (int i = 0; i < 5; i++)
            {
                float y = .28f + i * .17f;
                float width = .16f + i * .065f;
                shape.Edge(new Vector3(-width, y, 0), new Vector3(0, y - .13f, .14f), Red * .7f, .012f);
                shape.Edge(new Vector3(0, y - .13f, .14f), new Vector3(width, y, 0), Red * .7f, .012f);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                shape.Edge(new Vector3(side * .35f, 1.04f, 0), new Vector3(side * .59f, .63f, .12f), Red, .018f);
                for (int finger = 0; finger < 3; finger++)
                    shape.Edge(new Vector3(side * .59f, .63f, .12f), new Vector3(side * (.58f + finger * .07f), .32f - finger * .04f, .17f), Red * .8f, .012f);
            }
            MakeMesh("The hollow witness", shape, ghostMaterial, ghostRoot);
            for (int i = 0; i < 3; i++)
            {
                var ring = new Geometry();
                Ring(ring, Vector3.zero, .64f + i * .14f, 5 + i * 2, Red * (.5f + i * .13f), .011f);
                Transform ringObject = MakeMesh("Broken orbit " + i, ring, ghostMaterial, ghostRoot).transform;
                ringObject.localPosition = Vector3.up * (1.0f + i * .18f);
                ghostRings.Add(ringObject);
            }
            ghostRoot.gameObject.SetActive(false);
        }

        void BuildRitual()
        {
            ritualRoot = new GameObject("Discovered resolution site").transform;
            ritualRoot.SetParent(geometryRoot, false);
            ritualRoot.position = World(scenario.Rooms[scenario.OriginRoom].Center);
            var guide = new Geometry();
            Ring(guide, Vector3.up * .014f, 1.40f, 32, Gold * .40f, .008f);
            MakeMesh("Ritual guide", guide, lineMaterial, ritualRoot);
            ritualSegments = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var segment = new Geometry();
                Ring(segment, Vector3.up * (.022f + i * .008f), .78f + i * .2f, 3 + i, Gold, .026f);
                ritualSegments[i] = MakeMesh("Completed verse " + i, segment, lineMaterial, ritualRoot);
                ritualSegments[i].SetActive(false);
            }
            ritualRoot.gameObject.SetActive(false);
        }

        void BuildPlacedRelic()
        {
            placedRelicRoot = new GameObject("Placed offering").transform;
            placedRelicRoot.SetParent(geometryRoot, false);
            var shape = new Geometry();
            Diamond(shape, Vector3.zero, .20f, .24f, Gold, .018f);
            Ring(shape, new Vector3(0, -.21f, 0), .3f, 8, Gold * .7f, .018f);
            MakeMesh("Offering vectors", shape, lineMaterial, placedRelicRoot);
            placedRelicRoot.gameObject.SetActive(false);
        }

        static void Box(Geometry surfaces, Geometry lines, Vector3 center, Vector3 size, Color color)
        { surfaces.Box(center, size, Black); lines.BoxEdges(center, size, color, .012f); }

        static void Table(Vector3 p, Geometry surfaces, Geometry lines, Vector2 size)
        {
            Box(surfaces, lines, p + Vector3.up * .81f, new Vector3(size.x, .09f, size.y), Furniture);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Box(surfaces, lines, p + new Vector3(x * (size.x * .5f - .09f), .39f, z * (size.y * .5f - .09f)), new Vector3(.065f, .78f, .065f), Furniture * .8f);
        }

        static void Console(Vector3 p, Geometry surfaces, Geometry lines, float width)
        {
            Box(surfaces, lines, p + Vector3.up * .48f, new Vector3(width, .96f, .55f), Furniture);
            for (int i = 0; i < 3; i++)
            {
                lines.Rect(p + new Vector3(0, .25f + .25f * i, .285f), Vector3.right * (width * .44f), Vector3.up * .1f, Faint * 1.8f, .009f);
                lines.Edge(p + new Vector3(-.08f, .25f + .25f * i, .3f), p + new Vector3(.08f, .25f + .25f * i, .3f), Gold * .5f, .014f);
            }
        }

        static void Bookshelf(Vector3 p, Geometry surfaces, Geometry lines, int variant)
        {
            Box(surfaces, lines, p + Vector3.up * 1.2f, new Vector3(1.9f, 2.4f, .44f), Furniture);
            for (int shelf = 0; shelf < 4; shelf++)
            {
                float y = .16f + shelf * .54f;
                lines.Edge(p + new Vector3(-.90f, y, .235f), p + new Vector3(.9f, y, .235f), Furniture, .018f);
                for (int book = 0; book < 9; book++)
                {
                    float height = .26f + ((book * 7 + shelf * 3 + variant) % 5) * .039f;
                    float x = -.82f + book * .19f;
                    lines.Rect(p + new Vector3(x, y + height * .5f + .015f, .245f), Vector3.right * .058f, Vector3.up * height * .5f, book % 4 == 0 ? Gold * .42f : Furniture * .65f, .008f);
                }
            }
        }

        static void Chair(Vector3 p, Geometry surfaces, Geometry lines)
        {
            Box(surfaces, lines, p + Vector3.up * .45f, new Vector3(.72f, .1f, .72f), Furniture);
            Box(surfaces, lines, p + new Vector3(0, .92f, -.31f), new Vector3(.72f, .86f, .08f), Furniture);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    lines.Edge(p + new Vector3(x * .3f, 0, z * .3f), p + new Vector3(x * .3f, .45f, z * .3f), Furniture, .029f);
        }

        static void Piano(Vector3 p, Geometry surfaces, Geometry lines)
        {
            Box(surfaces, lines, p + Vector3.up * .77f, new Vector3(2.05f, 1.54f, .55f), Furniture);
            Box(surfaces, lines, p + new Vector3(0, .94f, .35f), new Vector3(2.03f, .1f, .32f), Trim);
            for (int i = 0; i < 24; i++)
            {
                float x = -.94f + i * .08f;
                lines.Edge(p + new Vector3(x, 1.001f, .23f), p + new Vector3(x, 1.001f, .49f), i % 3 == 0 ? Gold * .6f : Furniture, .008f);
            }
            lines.Rect(p + new Vector3(0, 1.29f, .286f), Vector3.right * .79f, Vector3.up * .13f, Faint * 1.5f, .012f);
        }

        static void Crib(Vector3 p, Geometry surfaces, Geometry lines)
        {
            Box(surfaces, lines, p + Vector3.up * .37f, new Vector3(1.45f, .17f, 1.05f), Furniture);
            lines.BoxEdges(p + Vector3.up * .7f, new Vector3(1.49f, .84f, 1.09f), Furniture, .018f);
            for (int i = 0; i < 8; i++)
            {
                float x = -.68f + i * .195f;
                for (int z = -1; z <= 1; z += 2)
                    lines.Edge(p + new Vector3(x, .42f, z * .54f), p + new Vector3(x, 1.10f, z * .54f), Furniture * .85f, .014f);
            }
        }

        static void Mobile(Vector3 p, Geometry lines)
        {
            lines.Edge(p + Vector3.up * .6f, p, Furniture, .009f);
            lines.Edge(p + Vector3.left * .48f, p + Vector3.right * .48f, Trim, .012f);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 end = p + new Vector3(i * .42f, -.25f - (i == 0 ? .16f : 0), 0);
                lines.Edge(p + Vector3.right * i * .42f, end, Furniture, .006f);
                Diamond(lines, end - Vector3.up * .075f, .055f, .1f, Gold * .55f, .009f);
            }
        }

        static void Bed(Vector3 p, Geometry surfaces, Geometry lines)
        {
            Box(surfaces, lines, p + Vector3.up * .44f, new Vector3(.80f, .23f, 2.2f), Furniture);
            Box(surfaces, lines, p + new Vector3(0, .68f, -1.02f), new Vector3(.87f, 1.0f, .08f), Furniture);
            Box(surfaces, lines, p + new Vector3(0, .60f, -.67f), new Vector3(.63f, .12f, .45f), Trim);
            lines.Rect(p + new Vector3(0, .563f, .25f), Vector3.right * .34f, Vector3.forward * .68f, Trim * .7f, .01f);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    lines.Edge(p + new Vector3(x * .32f, 0, z * .93f), p + new Vector3(x * .32f, .4f, z * .93f), Furniture, .028f);
        }

        static void Clock(Vector3 p, Geometry surfaces, Geometry lines)
        {
            Box(surfaces, lines, p + Vector3.up * 1.17f, new Vector3(.52f, 2.34f, .62f), Furniture);
            Vector3 dial = p + new Vector3(.269f, 1.92f, 0);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6, b = (i + 1) * Mathf.PI / 6;
                lines.Edge(dial + new Vector3(0, Mathf.Cos(a) * .21f, Mathf.Sin(a) * .21f), dial + new Vector3(0, Mathf.Cos(b) * .21f, Mathf.Sin(b) * .21f), Gold * .7f, .01f);
            }
            lines.Edge(dial, dial + new Vector3(0, .16f, -.02f), Gold, .015f);
            lines.Edge(dial, dial + new Vector3(0, -.05f, .12f), Gold, .015f);
            lines.Edge(p + new Vector3(.273f, 1.54f, 0), p + new Vector3(.273f, .64f, .035f), Trim, .014f);
            Diamond(lines, p + new Vector3(.29f, .60f, 0), .09f, .16f, Gold * .7f, .016f);
        }

        static void Stove(Vector3 p, Geometry surfaces, Geometry lines)
        {
            Box(surfaces, lines, p + Vector3.up * .47f, new Vector3(1.73f, .94f, .62f), Furniture);
            lines.Rect(p + new Vector3(0, .48f, .323f), Vector3.right * .57f, Vector3.up * .29f, Trim, .014f);
            for (int i = -1; i <= 1; i += 2)
                Ring(lines, p + new Vector3(i * .43f, .954f, 0), .20f, 12, Furniture, .016f);
            Box(surfaces, lines, p + new Vector3(.56f, 1.82f, -.12f), new Vector3(.27f, 1.72f, .27f), Faint * 1.7f);
        }

        static void HangingPans(Vector3 p, Geometry lines)
        {
            lines.Edge(p + Vector3.left * 1.1f, p + Vector3.right * 1.1f, Furniture, .022f);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 center = p + new Vector3(i * .64f, -.38f, .08f);
                lines.Edge(center + Vector3.up * .19f, center + Vector3.up * .40f, Trim, .018f);
                for (int j = 0; j < 10; j++)
                {
                    float a = j * Mathf.PI * 2 / 10, b = (j + 1) * Mathf.PI * 2 / 10;
                    lines.Edge(center + new Vector3(Mathf.Sin(a) * .17f, Mathf.Cos(a) * .17f, 0), center + new Vector3(Mathf.Sin(b) * .17f, Mathf.Cos(b) * .17f, 0), Furniture, .012f);
                }
            }
        }

        static void Lamp(Vector3 p, Geometry lines)
        {
            Ring(lines, p + Vector3.up * .025f, .24f, 8, Furniture, .02f);
            lines.Edge(p, p + Vector3.up * 1.9f, Furniture, .018f);
            Ring(lines, p + Vector3.up * 1.78f, .35f, 8, Gold * .55f, .016f);
            Ring(lines, p + Vector3.up * 2.22f, .16f, 8, Gold * .55f, .016f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                Vector3 dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                lines.Edge(p + dir * .35f + Vector3.up * 1.78f, p + dir * .16f + Vector3.up * 2.22f, Furniture, .01f);
            }
        }

        static void Mirror(Vector3 p, Geometry lines, float width, float height)
        {
            lines.Rect(p, Vector3.right * width * .5f, Vector3.up * height * .5f, Trim, .022f);
            lines.Rect(p + Vector3.forward * .003f, Vector3.right * (width * .5f - .06f), Vector3.up * (height * .5f - .06f), Furniture, .01f);
            // Geometric glints, without a texture or a misleading view into the room beyond.
            lines.Edge(p + new Vector3(-width * .26f, -height * .19f, .01f), p + new Vector3(width * .15f, height * .26f, .01f), Faint, .009f);
        }

        static void Candle(Vector3 p, Geometry lines)
        {
            Ring(lines, p + Vector3.up * .015f, .12f, 8, Gold * .6f, .013f);
            lines.Edge(p, p + Vector3.up * .27f, Trim, .027f);
            Diamond(lines, p + Vector3.up * .32f, .025f, .06f, Gold, .009f);
        }

        static void Plant(Vector3 p, Geometry surfaces, Geometry lines)
        {
            Box(surfaces, lines, p + Vector3.up * .26f, new Vector3(.52f, .52f, .52f), Furniture);
            lines.Edge(p + Vector3.up * .4f, p + new Vector3(.05f, 1.65f, 0), Furniture, .02f);
            for (int i = 0; i < 7; i++)
            {
                float a = i * 2.4f;
                float y = .6f + i * .135f;
                Vector3 stem = p + Vector3.up * y;
                Vector3 tip = stem + new Vector3(Mathf.Sin(a) * .48f, .34f, Mathf.Cos(a) * .48f);
                Vector3 side = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a)) * .11f;
                Vector3 middle = (stem + tip) * .5f;
                lines.Edge(stem, middle + side, Furniture, .011f);
                lines.Edge(middle + side, tip, Furniture, .011f);
                lines.Edge(tip, middle - side, Furniture, .011f);
                lines.Edge(middle - side, stem, Furniture, .011f);
                lines.Edge(stem, tip, Faint * 2f, .008f);
            }
        }

        static void Trellis(Vector3 p, Geometry lines)
        {
            lines.Rect(p + Vector3.up * 1.65f, Vector3.right * 1.07f, Vector3.up * 1.15f, Furniture, .018f);
            for (int i = -3; i <= 3; i++)
            {
                float x = i * .34f;
                lines.Edge(p + new Vector3(x, .53f, .007f), p + new Vector3(x, 2.78f, .007f), Faint * 1.7f, .009f);
            }
            for (int i = 0; i < 5; i++) lines.Edge(p + new Vector3(-1.06f, .65f + i * .47f, .01f), p + new Vector3(1.06f, .65f + i * .47f, .01f), Faint * 1.7f, .009f);
        }

        static void Barrel(Vector3 p, Geometry surfaces, Geometry lines)
        {
            // A low-poly closed cylinder keeps the far-side staves correctly hidden.
            const int count = 10;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count, b = (i + 1) * Mathf.PI * 2 / count;
                Vector3 da = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)), db = new Vector3(Mathf.Sin(b), 0, Mathf.Cos(b));
                surfaces.Quad(p + da * .39f, p + db * .39f, p + db * .39f + Vector3.up * 1.05f, p + da * .39f + Vector3.up * 1.05f, Black);
                surfaces.Triangle(p + Vector3.up * 1.05f, p + da * .39f + Vector3.up * 1.05f, p + db * .39f + Vector3.up * 1.05f, Black);
                lines.Edge(p + da * .4f, p + da * .4f + Vector3.up * 1.05f, Furniture, .01f);
            }
            Ring(lines, p + Vector3.up * .04f, .4f, count, Furniture, .018f);
            Ring(lines, p + Vector3.up * .28f, .41f, count, Trim * .7f, .02f);
            Ring(lines, p + Vector3.up * .79f, .41f, count, Trim * .7f, .02f);
            Ring(lines, p + Vector3.up * 1.06f, .4f, count, Furniture, .018f);
        }

        static void DrawRelic(Geometry lines, string relicName, Color color)
        {
            switch (relicName)
            {
                case "Mourning brooch":
                    Diamond(lines, Vector3.zero, .13f, .19f, color, .018f);
                    Ring(lines, Vector3.up * .03f, .17f, 8, color * .7f, .013f);
                    break;
                case "Pocket watch":
                    Ring(lines, Vector3.zero, .17f, 16, color, .018f);
                    Ring(lines, new Vector3(0, .04f, 0), .17f, 16, color, .013f);
                    lines.Edge(new Vector3(0, .045f, 0), new Vector3(.11f, .045f, .03f), color, .013f);
                    lines.Edge(new Vector3(0, .045f, 0), new Vector3(0, .045f, .12f), color, .013f);
                    break;
                default:
                    Ring(lines, Vector3.zero, .15f, 12, color, .025f);
                    lines.Edge(new Vector3(0, 0, -.15f), new Vector3(0, .24f, -.15f), color, .024f);
                    lines.Edge(new Vector3(0, .2f, -.15f), new Vector3(.09f, .2f, -.15f), color, .018f);
                    break;
            }
        }

        static void Diamond(Geometry lines, Vector3 center, float width, float height, Color color, float thickness)
        {
            Vector3 top = center + Vector3.up * height, bottom = center - Vector3.up * height;
            Vector3[] middle = { center + Vector3.left * width, center + Vector3.forward * width,
                center + Vector3.right * width, center + Vector3.back * width };
            for (int i = 0; i < 4; i++)
            {
                lines.Edge(top, middle[i], color, thickness);
                lines.Edge(bottom, middle[i], color, thickness);
                lines.Edge(middle[i], middle[(i + 1) % 4], color, thickness);
            }
        }

        static void Ring(Geometry lines, Vector3 center, float radius, int segments, Color color, float thickness)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                lines.Edge(center + new Vector3(Mathf.Sin(a) * radius, 0, Mathf.Cos(a) * radius),
                    center + new Vector3(Mathf.Sin(b) * radius, 0, Mathf.Cos(b) * radius), color, thickness);
            }
        }

        Material CreateMaterial(string shaderName)
        {
            // Resources retains these otherwise code-only shader references in standalone builds.
            string resourceName = shaderName.Substring(shaderName.LastIndexOf('/') + 1);
            Shader shader = Resources.Load<Shader>(resourceName);
            if (shader == null) shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("Required procedural renderer shader missing: " + shaderName);
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            var material = new Material(shader) { name = shaderName + " (runtime)" };
            return material;
        }

        GameObject MakeMesh(string objectName, Geometry geometry, Material material, Transform parent)
        {
            var obj = new GameObject(objectName);
            obj.transform.SetParent(parent, false);
            Mesh mesh = geometry.ToMesh(objectName);
            ownedMeshes.Add(mesh);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return obj;
        }

        void ClearGeometry()
        {
            if (geometryRoot != null) { geometryRoot.gameObject.SetActive(false); DestroyOwned(geometryRoot.gameObject); }
            foreach (Mesh mesh in ownedMeshes) DestroyOwned(mesh);
            ownedMeshes.Clear();
            DestroyOwned(surfaceMaterial); DestroyOwned(lineMaterial); DestroyOwned(ghostMaterial);
            doorPanels.Clear(); evidenceMarkers.Clear(); wards.Clear(); relics.Clear(); ghostRings.Clear();
        }

        void OnDestroy() { ClearGeometry(); }
        static void DestroyOwned(Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }

        /// <summary>Small mesh builder: triangles for solid depth and thin square rods for vectors.</summary>
        sealed class Geometry
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Color> colors = new List<Color>();
            readonly List<int> triangles = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int i = 0; i < 4; i++) colors.Add(color);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                colors.Add(color); colors.Add(color); colors.Add(color);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }
            public void Edge(Vector3 a, Vector3 b, Color color, float thickness)
            {
                Vector3 direction = b - a;
                if (direction.sqrMagnitude < .0000001f) return;
                direction.Normalize();
                Vector3 helper = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > .9f ? Vector3.forward : Vector3.up;
                Vector3 u = Vector3.Cross(direction, helper).normalized * thickness * .5f;
                Vector3 v = Vector3.Cross(direction, u).normalized * thickness * .5f;
                Quad(a - u - v, a + u - v, b + u - v, b - u - v, color);
                Quad(a + u - v, a + u + v, b + u + v, b + u - v, color);
                Quad(a + u + v, a - u + v, b - u + v, b + u + v, color);
                Quad(a - u + v, a - u - v, b - u - v, b - u + v, color);
            }
            public void Rect(Vector3 center, Vector3 horizontal, Vector3 vertical, Color color, float thickness)
            {
                Edge(center - horizontal - vertical, center + horizontal - vertical, color, thickness);
                Edge(center + horizontal - vertical, center + horizontal + vertical, color, thickness);
                Edge(center + horizontal + vertical, center - horizontal + vertical, color, thickness);
                Edge(center - horizontal + vertical, center - horizontal - vertical, color, thickness);
            }
            public void Box(Vector3 center, Vector3 size, Color color)
            {
                Vector3 h = size * .5f;
                Vector3 a = center + new Vector3(-h.x, -h.y, -h.z), b = center + new Vector3(h.x, -h.y, -h.z);
                Vector3 c = center + new Vector3(h.x, -h.y, h.z), d = center + new Vector3(-h.x, -h.y, h.z);
                Vector3 y = Vector3.up * size.y;
                Quad(a, b, c, d, color); Quad(a + y, d + y, c + y, b + y, color);
                Quad(a, a + y, b + y, b, color); Quad(b, b + y, c + y, c, color);
                Quad(c, c + y, d + y, d, color); Quad(d, d + y, a + y, a, color);
            }
            public void BoxEdges(Vector3 center, Vector3 size, Color color, float thickness)
            {
                Vector3 h = size * .5f;
                Rect(center - Vector3.up * h.y, Vector3.right * h.x, Vector3.forward * h.z, color, thickness);
                Rect(center + Vector3.up * h.y, Vector3.right * h.x, Vector3.forward * h.z, color, thickness);
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                        Edge(center + new Vector3(x * h.x, -h.y, z * h.z), center + new Vector3(x * h.x, h.y, z * h.z), color, thickness);
            }
            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
