using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TestAnxiety
{
    /// <summary>Scene-owned, conventional solid-mesh classroom. All dimensions are metres.</summary>
    public sealed partial class TestAnxietyClassroom : MonoBehaviour
    {
        public Transform ExamFocus { get; private set; }
        public Transform ExamView { get; private set; }
        public Transform ExamDesk { get; private set; }
        public Transform AnswerSheet { get; private set; }
        public Transform MachineAnchor { get; private set; }
        public Transform RearObservation { get; private set; }
        public Transform ExitFocus { get; private set; }
        public Transform Shirt { get; private set; }
        public Vector3 SpawnPosition { get { return new Vector3(1.4f, 0, -4.7f); } }
        public List<Light> Fluorescents { get; } = new List<Light>();

        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        Transform room;
        Font font;
        Material plaster, lowerWall, trim, steel, darkSteel, wood, chair, paper, ink, blueInk;
        Material chalkboard, chalk, cork, glass, tube, brass, pencilPaint, red, blueCloth, ceiling, tileA, tileB, tileC;
        Light daylight;
        TextMesh boardMessage;
        bool finalAtmosphere;

        public void Build(Material litTemplate = null)
        {
            Clear();
            room = Child("Room 107 / furnished classroom", transform);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Shader shader = litTemplate != null ? litTemplate.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("TEST ANXIETY requires the installed Universal Render Pipeline/Lit shader.");
            plaster = Surface("Aged warm plaster", new Color(.71f, .70f, .63f), shader, .12f, .018f);
            lowerWall = Surface("Institutional sage paint", new Color(.37f, .43f, .40f), shader, .23f, .015f);
            trim = Surface("Painted cream trim", new Color(.61f, .61f, .54f), shader, .33f);
            steel = Surface("Dull tubular steel", new Color(.40f, .43f, .42f), shader, .58f, 0, .65f);
            darkSteel = Surface("Black rubber and painted metal", new Color(.095f, .105f, .10f), shader, .30f);
            wood = Surface("Worn honey laminate", new Color(.46f, .32f, .19f), shader, .32f, .035f);
            chair = Surface("Faded burgundy polypropylene", new Color(.29f, .115f, .095f), shader, .35f, .012f);
            paper = Surface("Uncoated examination stock", new Color(.87f, .85f, .76f), shader, .06f, .008f);
            ink = Surface("Graphite", new Color(.11f, .12f, .12f), shader, .10f);
            blueInk = Surface("Institutional blue", new Color(.14f, .23f, .27f), shader, .20f);
            chalkboard = Surface("Green slate chalkboard", new Color(.075f, .16f, .13f), shader, .10f, .012f);
            chalk = Surface("Chalk", new Color(.76f, .78f, .68f), shader, .05f);
            cork = Surface("Cork noticeboard", new Color(.41f, .27f, .15f), shader, .05f, .075f);
            glass = Surface("Overcast window glass", new Color(.43f, .52f, .52f), shader, .63f);
            glass.EnableKeyword("_EMISSION"); glass.SetColor("_EmissionColor", new Color(.20f, .25f, .25f));
            tube = Surface("Fluorescent diffuser", new Color(.81f, .88f, .79f), shader, .25f);
            tube.EnableKeyword("_EMISSION"); tube.SetColor("_EmissionColor", new Color(1.4f, 1.6f, 1.3f));
            brass = Surface("Tarnished brass", new Color(.45f, .35f, .17f), shader, .53f, 0, .65f);
            pencilPaint = Surface("No. 2 yellow pencil lacquer", new Color(.78f, .57f, .12f), shader, .38f);
            red = Surface("School safety red", new Color(.40f, .065f, .045f), shader, .31f);
            blueCloth = Surface("Blue cotton shirt", new Color(.10f, .22f, .38f), shader, .03f, .025f);
            ceiling = Surface("Acoustic ceiling tile", new Color(.66f, .67f, .61f), shader, .03f, .025f);
            tileA = Surface("Waxed vinyl / cream", new Color(.51f, .49f, .40f), shader, .36f, .035f);
            tileB = Surface("Waxed vinyl / worn cream", new Color(.48f, .465f, .38f), shader, .32f, .035f);
            tileC = Surface("Waxed vinyl / green border", new Color(.25f, .31f, .28f), shader, .30f, .025f);
            BuildArchitecture();
            BuildFrontWall();
            BuildFurniture();
            BuildDetails();
            BuildLighting();
            BuildShirt();
            ExamFocus = Marker("Examination / look target", new Vector3(0, .815f, -2.03f));
            ExamView = Marker("Examination / close-up camera", new Vector3(0, 1.57f, -2.83f));
            ExamView.rotation = Quaternion.LookRotation(ExamFocus.position - ExamView.position, Vector3.up);
            MachineAnchor = Marker("Scanner / mounting point", new Vector3(3.5f, .81f, 4.5f));
            RearObservation = Marker("Empty seat / observation target", new Vector3(0, .80f, -3.30f));
            ExitFocus = Marker("Exit / interaction target", new Vector3(-4.88f, 1.18f, -4.65f));
            BuildExpansion();
        }

        public void RevealShirt() { if (Shirt != null) Shirt.gameObject.SetActive(true); }

        public void SetFinalAtmosphere(bool value)
        {
            finalAtmosphere = value;
            RefreshBoard();
        }

        public void Tick(float time, float tension)
        {
            // A restrained irregular dip, never a full-room strobe.
            float dip = Mathf.Pow(Mathf.Max(0, Mathf.Sin(time * 2.17f) * Mathf.Sin(time * 13.13f)), 18);
            for (int i = 0; i < Fluorescents.Count; i++)
            {
                if (Fluorescents[i] == null) continue;
                float baseIntensity = (powerCut ? .025f : finalAtmosphere ? .30f : .66f) * Mathf.Lerp(1, .12f, blackoutStrength);
                Fluorescents[i].intensity = baseIntensity * (1 - (i == 4 ? .18f + tension * .23f : .06f) * dip);
            }
            if (daylight != null) daylight.intensity = (powerCut ? .8f : finalAtmosphere ? .30f : .72f) * Mathf.Lerp(1, .18f, blackoutStrength);
            TickExpansion(time);
        }

        void BuildArchitecture()
        {
            Box("Continuous floor collider", new Vector3(0, -.09f, 0), new Vector3(10.2f, .18f, 12.2f), tileB, true);
            for (int x = 0; x < 10; x++) for (int z = 0; z < 12; z++)
            {
                bool border = x == 0 || x == 9 || z == 0 || z == 11;
                Material tile = border ? tileC : ((x * 7 + z * 11) % 4 == 0 ? tileB : tileA);
                Box("Vinyl tile", new Vector3(x - 4.5f, .003f, z - 5.5f), new Vector3(.993f, .006f, .993f), tile);
            }
            Wall("Front", new Vector3(0, 0, 6.08f), 10.2f, .16f);
            Wall("Rear", new Vector3(0, 0, -6.08f), 10.2f, .16f);
            Wall("Right", new Vector3(5.08f, 0, 0), .16f, 12.2f);

            // Left wall is physically open at the windows; opaque courtyard backdrops sit outside.
            float[] edges = { -6.08f, -5.32f, -3.98f, -2.75f, -.85f, -.15f, 1.75f, 2.45f, 4.35f, 6.08f };
            for (int i = 0; i < edges.Length - 1; i++)
            {
                float center = (edges[i] + edges[i + 1]) * .5f, length = edges[i + 1] - edges[i];
                bool door = i == 1, window = i == 3 || i == 5 || i == 7;
                if (window)
                {
                    Box("Window sill wall", new Vector3(-5.08f, .525f, center), new Vector3(.16f, 1.05f, length), lowerWall, true);
                    Box("Window header", new Vector3(-5.08f, 2.99f, center), new Vector3(.16f, .52f, length), plaster, true);
                    Window(center);
                }
                else if (door)
                    Box("Door header", new Vector3(-5.08f, 2.81f, center), new Vector3(.16f, .90f, length), plaster, true);
                else Wall("Left wall pier", new Vector3(-5.08f, 0, center), .16f, length);
            }
            Box("Ceiling slab", new Vector3(0, 3.32f, 0), new Vector3(10.2f, .14f, 12.2f), trim, true);
            for (int x = 0; x < 8; x++) for (int z = 0; z < 10; z++)
                Box("Suspended acoustic tile", new Vector3(-4.375f + x * 1.25f, 3.238f, -5.4f + z * 1.2f), new Vector3(1.226f, .024f, 1.176f), ceiling);
            for (int x = 0; x <= 8; x++)
                Box("Ceiling T rail", new Vector3(-5 + x * 1.25f, 3.221f, 0), new Vector3(.016f, .025f, 12), trim);
            for (int z = 0; z <= 10; z++)
                Box("Ceiling T rail", new Vector3(0, 3.221f, -6 + z * 1.2f), new Vector3(10, .025f, .016f), trim);
            Door();
        }

        void Wall(string name, Vector3 at, float width, float depth)
        {
            Box(name + " / upper plaster", at + Vector3.up * 2.115f, new Vector3(width, 2.27f, depth), plaster, true);
            Box(name + " / painted dado", at + Vector3.up * .49f, new Vector3(width, .98f, depth), lowerWall, true);
            Box(name + " / rubber base", at + Vector3.up * .055f, new Vector3(width + .018f, .11f, depth + .018f), darkSteel);
            Box(name + " / chair rail", at + Vector3.up * 1.015f, new Vector3(width + .027f, .065f, depth + .027f), trim);
        }

        void Window(float z)
        {
            Transform pane = Box("Window / overcast courtyard", new Vector3(-5.24f, 1.89f, z), new Vector3(.025f, 1.68f, 1.90f), glass, true);
            pane.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Window / jamb", new Vector3(-4.98f, 1.89f, z + side * .94f), new Vector3(.16f, 1.78f, .075f), trim);
                Box("Window / frame", new Vector3(-4.98f, 1.89f + side * .81f, z), new Vector3(.16f, .075f, 1.95f), trim);
            }
            Box("Window / center mullion", new Vector3(-4.98f, 1.89f, z), new Vector3(.11f, 1.60f, .045f), steel);
            Box("Window / sash", new Vector3(-4.97f, 1.90f, z), new Vector3(.12f, .04f, 1.84f), steel);
            Box("Window / deep sill", new Vector3(-4.88f, 1.075f, z), new Vector3(.42f, .075f, 2.06f), trim);
            // Partly raised venetian blind slats, with two suspension cords.
            for (int i = 0; i < 5; i++)
            {
                Transform slat = Box("Window / blind slat", new Vector3(-4.86f, 2.66f - i * .047f, z), new Vector3(.13f, .012f, 1.82f), trim);
                slat.localRotation = Quaternion.Euler(0, 0, -18);
            }
            for (int side = -1; side <= 1; side += 2)
                Box("Window / blind cord", new Vector3(-4.79f, 2.47f, z + side * .65f), new Vector3(.008f, .42f, .008f), darkSteel);
            Box("Window / latch", new Vector3(-4.89f, 1.91f, z + .07f), new Vector3(.06f, .035f, .08f), darkSteel);
        }

        void Door()
        {
            entranceDoor = Child("Entrance / working door hinge", room);
            entranceDoor.localPosition = new Vector3(-5.025f, 0, -5.25f);
            OnEntranceDoor(Box("Entrance / closed oak door", new Vector3(-5.025f, 1.16f, -4.65f), new Vector3(.07f, 2.32f, 1.20f), wood, true));
            Box("Entrance / jamb north", new Vector3(-4.96f, 1.18f, -3.99f), new Vector3(.16f, 2.36f, .095f), trim);
            Box("Entrance / jamb south", new Vector3(-4.96f, 1.18f, -5.31f), new Vector3(.16f, 2.36f, .095f), trim);
            Box("Entrance / lintel", new Vector3(-4.96f, 2.36f, -4.65f), new Vector3(.16f, .10f, 1.42f), trim);
            OnEntranceDoor(Box("Entrance / kick plate", new Vector3(-4.98f, .23f, -4.65f), new Vector3(.022f, .30f, 1.06f), steel));
            OnEntranceDoor(Box("Entrance / wired glass surround", new Vector3(-4.98f, 1.72f, -4.65f), new Vector3(.035f, .70f, .50f), darkSteel));
            Transform doorPane = Box("Entrance / wired glass", new Vector3(-4.953f, 1.72f, -4.65f), new Vector3(.018f, .61f, .41f), glass);
            doorPane.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            OnEntranceDoor(doorPane);
            for (int i = 0; i < 4; i++)
                OnEntranceDoor(Box("Entrance / glass wire", new Vector3(-4.939f, 1.72f, -4.81f + i * .11f), new Vector3(.006f, .61f, .005f), steel));
            OnEntranceDoor(Box("Entrance / latch plate", new Vector3(-4.96f, 1.05f, -4.19f), new Vector3(.035f, .22f, .08f), brass));
            OnEntranceDoor(Sphere("Entrance / round handle", new Vector3(-4.88f, 1.05f, -4.19f), Vector3.one * .085f, brass));
            OnEntranceDoor(Text("Entrance / room number", "107", new Vector3(-4.923f, 2.17f, -4.65f), .13f, new Color(.13f, .12f, .10f), new Vector3(0, -90, 0), TextAnchor.MiddleCenter).transform);
            Box("Entrance / exit sign", new Vector3(-4.94f, 2.66f, -4.65f), new Vector3(.09f, .22f, .63f), paper);
            Text("Entrance / EXIT", "EXIT", new Vector3(-4.881f, 2.66f, -4.65f), .11f, new Color(.45f, .10f, .06f), new Vector3(0, -90, 0), TextAnchor.MiddleCenter);
        }

        void BuildFrontWall()
        {
            Box("Chalkboard / wooden surround", new Vector3(-.55f, 1.95f, 5.92f), new Vector3(5.55f, 1.62f, .12f), wood);
            Box("Chalkboard / slate", new Vector3(-.55f, 1.95f, 5.845f), new Vector3(5.40f, 1.46f, .035f), chalkboard);
            Box("Chalkboard / chalk tray", new Vector3(-.55f, 1.19f, 5.75f), new Vector3(5.58f, .06f, .25f), steel);
            boardMessage = Text("Chalkboard / instructions", StandardBoardText, new Vector3(-2.95f, 2.48f, 5.818f), .12f, new Color(.72f, .77f, .67f), Vector3.zero);
            Text("Chalkboard / date", "TUESDAY\nOCTOBER 17, 2000", new Vector3(1.20f, 2.44f, 5.816f), .065f, new Color(.63f, .71f, .62f), Vector3.zero);
            Box("Observation symbol / plaque", new Vector3(-.55f, 2.98f, 5.94f), new Vector3(.58f, .34f, .03f), paper);
            Cylinder("Observation symbol / CIRCLE", new Vector3(-.55f, 2.98f, 5.915f), .115f, .012f, blueInk, new Vector3(90, 0, 0));
            for (int i = 0; i < 4; i++)
                Rod("Chalkboard / loose chalk", new Vector3(-1.6f + i * .13f, 1.24f, 5.72f), new Vector3(-1.53f + i * .13f, 1.24f, 5.72f), .008f, chalk);
            Box("Chalkboard / eraser", new Vector3(1.45f, 1.25f, 5.73f), new Vector3(.15f, .04f, .07f), darkSteel);
            Clock(new Vector3(3.10f, 2.62f, 5.82f));
            Notice("Quiet poster", "QUIET", "TESTING IN PROGRESS\n\nDo your own work.\nTrust your first answer.", new Vector3(4.35f, 2.50f, 5.88f), new Vector2(.80f, .92f), Vector3.zero, blueInk);
        }

        void Clock(Vector3 at)
        {
            clockOrigin = at;
            Cylinder("Clock / metal rim", at, .315f, .045f, darkSteel, new Vector3(90, 0, 0));
            Cylinder("Clock / aged dial", at + Vector3.back * .027f, .292f, .012f, paper, new Vector3(90, 0, 0));
            for (int i = 0; i < 60; i++)
            {
                float a = i * Mathf.PI / 30;
                Vector3 p = at + new Vector3(Mathf.Sin(a) * .265f, Mathf.Cos(a) * .265f, -.037f);
                Transform tick = Box("Clock / tick", p, new Vector3(i % 5 == 0 ? .012f : .005f, i % 5 == 0 ? .032f : .014f, .003f), ink);
                tick.localRotation = Quaternion.Euler(0, 0, -i * 6);
            }
            for (int i = 1; i <= 12; i++)
            {
                float a = i * Mathf.PI / 6;
                Text("Clock / numeral", i.ToString(), at + new Vector3(Mathf.Sin(a) * .213f, Mathf.Cos(a) * .213f, -.041f), .065f, new Color(.12f, .13f, .12f), Vector3.zero, TextAnchor.MiddleCenter);
            }
            // Frozen at 3:15: the minute hand points at 3, the shorter hour hand a little below it.
            clockMinute = Rod("Clock / minute hand", at + Vector3.back * .049f, at + new Vector3(.222f, 0, -.049f), .006f, ink);
            clockHour = Rod("Clock / hour hand", at + Vector3.back * .055f, at + new Vector3(.140f, -.0184f, -.055f), .009f, ink);
            Sphere("Clock / pin", at + Vector3.back * .064f, Vector3.one * .025f, brass);
        }

        void BuildFurniture()
        {
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
                Desk(new Vector3(x * 3, 0, z * 2), x == 0 && z == -1, (z + 1) * 3 + x + 2);
            // A second empty seat makes the question's rearward reference visually legible.
            StudentChair(new Vector3(0, 0, -3.30f), Quaternion.identity, "Unoccupied rear chair");
            Table("Teacher's desk", new Vector3(-.55f, 0, 4.53f), new Vector2(1.85f, .82f), .77f, true);
            Table("Optical scanner table", new Vector3(3.50f, 0, 4.5f), new Vector2(1.64f, .94f), .80f, false);
            StudentChair(new Vector3(-.55f, 0, 5.20f), Quaternion.Euler(0, 180, 0), "Teacher's chair");
            for (int i = 0; i < 3; i++)
            {
                Material cover = i == 0 ? blueInk : i == 1 ? chair : lowerWall;
                Box("Teacher / stacked textbook", new Vector3(-.93f + i * .018f, .81f + i * .045f, 4.55f), new Vector3(.34f, .040f, .25f), cover);
                Box("Teacher / textbook pages", new Vector3(-.93f + i * .018f, .81f + i * .045f, 4.539f), new Vector3(.325f, .025f, .235f), paper);
            }
            Cylinder("Teacher / pencil cup", new Vector3(.0f, .86f, 4.61f), .052f, .15f, darkSteel);
            for (int i = 0; i < 5; i++)
                Rod("Teacher / pencils", new Vector3(-.03f + i * .013f, .82f, 4.61f), new Vector3(-.04f + i * .017f, 1.04f, 4.59f + (i % 2) * .03f), .004f, pencilPaint);
            Box("Teacher / closed grade book", new Vector3(-.25f, .792f, 4.35f), new Vector3(.28f, .035f, .38f), darkSteel);
            Text("Teacher / grade book label", "CLASS RECORD", new Vector3(-.25f, .813f, 4.35f), .016f, new Color(.73f, .71f, .58f), new Vector3(90, 0, 0), TextAnchor.MiddleCenter);
            Cylinder("Teacher / mug", new Vector3(.18f, .86f, 4.40f), .040f, .11f, paper);
            Cylinder("Teacher / cold coffee", new Vector3(.18f, .918f, 4.40f), .033f, .003f, wood);
            Box("Scanner / instruction placard", new Vector3(3.50f, 1.76f, 5.90f), new Vector3(1.45f, .45f, .025f), paper);
            Text("Scanner / instructions", "AUTOMATIC EXAMINER\nComplete all items. Bring your answer sheet here.", new Vector3(3.50f, 1.77f, 5.88f), .05f, new Color(.16f, .19f, .17f), Vector3.zero, TextAnchor.MiddleCenter);
        }

        void Desk(Vector3 p, bool examination, int number)
        {
            Transform root = Child(examination ? "YOUR DESK / examination station" : "Student desk " + number.ToString("00"), room);
            root.localPosition = p;
            Box("Desk / black moulded edge", new Vector3(0, .738f, 0), new Vector3(1.04f, .055f, .64f), darkSteel, true, root);
            Box("Desk / laminate worktop", new Vector3(0, .771f, 0), new Vector3(1.018f, .018f, .618f), wood, false, root);
            Box("Desk / under-desk book tray", new Vector3(0, .59f, .045f), new Vector3(.73f, .045f, .43f), lowerWall, false, root);
            Box("Desk / tray back", new Vector3(0, .645f, .25f), new Vector3(.75f, .12f, .025f), lowerWall, false, root);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            {
                Rod("Desk / chrome leg", new Vector3(x * .45f, .72f, z * .25f), new Vector3(x * .48f, .04f, z * .29f), .018f, steel, root);
                Cylinder("Desk / rubber foot", new Vector3(x * .48f, .027f, z * .29f), .025f, .054f, darkSteel, Vector3.zero, root);
            }
            Text("Desk / number", examination ? "014" : number.ToString("00"), new Vector3(-.43f, .742f, -.327f), .065f, new Color(.72f, .72f, .60f), Vector3.zero, TextAnchor.MiddleCenter, root);
            if (!examination)
            {
                numberedDesks[number] = root;
                numberedChairs[number] = StudentChair(p + Vector3.back * .68f, Quaternion.identity, "Student chair " + number.ToString("00"));
            }
            else
            {
                ExamDesk = root;
                StudentChair(p + new Vector3(-.62f, 0, -.76f), Quaternion.Euler(0, -18, 0), "Your pulled-out chair");
                ExamPapers(root);
            }
        }

        Transform StudentChair(Vector3 p, Quaternion rotation, string name)
        {
            Transform root = Child(name, room); root.localPosition = p; root.localRotation = rotation;
            // A circular footprint does not expand when the scripted chairs turn in the dark.
            // This keeps a nearby standing controller outside the same volume before and after.
            CapsuleCollider collision = root.gameObject.AddComponent<CapsuleCollider>();
            collision.center = new Vector3(0, .47f, 0); collision.radius = .34f; collision.height = .94f;
            Box("Chair / contoured seat", new Vector3(0, .435f, 0), new Vector3(.43f, .055f, .42f), chair, false, root);
            Box("Chair / seat rim", new Vector3(0, .405f, .01f), new Vector3(.40f, .025f, .37f), darkSteel, false, root);
            Transform back = Box("Chair / curved backrest", new Vector3(0, .755f, -.186f), new Vector3(.43f, .29f, .047f), chair, false, root);
            back.localRotation = Quaternion.Euler(-8, 0, 0);
            for (int x = -1; x <= 1; x += 2)
            {
                Rod("Chair / back support", new Vector3(x * .16f, .35f, -.13f), new Vector3(x * .16f, .85f, -.21f), .013f, steel, root);
                Rod("Chair / front leg", new Vector3(x * .16f, .42f, .15f), new Vector3(x * .22f, .025f, .22f), .014f, steel, root);
                Rod("Chair / rear leg", new Vector3(x * .16f, .42f, -.13f), new Vector3(x * .22f, .025f, -.24f), .014f, steel, root);
                Rod("Chair / side brace", new Vector3(x * .18f, .23f, -.18f), new Vector3(x * .18f, .23f, .18f), .010f, steel, root);
            }
            return root;
        }

        void Table(string name, Vector3 p, Vector2 size, float top, bool drawers)
        {
            Transform root = Child(name, room); root.localPosition = p;
            Box("Table / laminate top", new Vector3(0, top - .03f, 0), new Vector3(size.x, .06f, size.y), wood, true, root);
            Box("Table / metal apron", new Vector3(0, top - .12f, .03f), new Vector3(size.x - .08f, .15f, size.y - .08f), lowerWall, true, root);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                Box("Table / steel leg", new Vector3(x * (size.x * .5f - .10f), (top - .1f) * .5f, z * (size.y * .5f - .10f)), new Vector3(.055f, top - .1f, .055f), steel, false, root);
            if (drawers)
            {
                Box("Teacher / drawer pedestal", new Vector3(-.60f, .43f, 0), new Vector3(.50f, .54f, .66f), lowerWall, true, root);
                for (int i = 0; i < 3; i++)
                {
                    Box("Teacher / drawer face", new Vector3(-.60f, .27f + i * .17f, -.345f), new Vector3(.46f, .15f, .025f), trim, false, root);
                    Box("Teacher / drawer pull", new Vector3(-.60f, .29f + i * .17f, -.372f), new Vector3(.14f, .025f, .035f), steel, false, root);
                }
                Box("Teacher / modesty panel", new Vector3(.10f, .45f, .28f), new Vector3(1.1f, .49f, .04f), lowerWall, false, root);
            }
        }

        void ExamPapers(Transform root)
        {
            Box("Booklet / navy backing", new Vector3(-.22f, .789f, .015f), new Vector3(.38f, .012f, .43f), blueInk, false, root);
            Box("Booklet / left leaf", new Vector3(-.32f, .798f, .012f), new Vector3(.181f, .005f, .405f), paper, false, root);
            Box("Booklet / right leaf", new Vector3(-.13f, .800f, .012f), new Vector3(.181f, .005f, .405f), paper, false, root);
            Text("Booklet / cover typography", "OBSERVATION\nASSESSMENT\n\nFORM 107-B\n\nRead before marking.", new Vector3(-.22f, .805f, .04f), .015f, new Color(.14f, .19f, .20f), new Vector3(90, 0, 0), TextAnchor.MiddleCenter, root);
            Box("Your desk / candidate nameplate", new Vector3(0, .808f, .267f), new Vector3(.56f, .018f, .073f), blueInk, false, root);
            candidateName = Text("Your desk / candidate name", "CANDIDATE 014", new Vector3(0, .819f, .267f), .06f, new Color(.88f, .87f, .78f), new Vector3(90, 0, 0), TextAnchor.MiddleCenter, root);
            AnswerSheet = Child("Answer sheet / physical original", root);
            AnswerSheet.localPosition = new Vector3(.22f, .795f, -.012f);
            Box("Answer sheet / stock", Vector3.zero, new Vector3(.215f, .005f, .34f), paper, false, AnswerSheet);
            Text("Answer sheet / heading", "ANSWER SHEET\nUSE NO. 2 PENCIL", new Vector3(0, .004f, .119f), .012f, new Color(.16f, .20f, .20f), new Vector3(90, 0, 0), TextAnchor.MiddleCenter, AnswerSheet);
            for (int i = 0; i < 9; i++)
            {
                Text("Answer sheet / row", (i + 1) + "    A   B   C   D", new Vector3(0, .004f, .065f - i * .023f), .011f, new Color(.19f, .22f, .21f), new Vector3(90, 0, 0), TextAnchor.MiddleCenter, AnswerSheet);
                Box("Answer sheet / optical registration", new Vector3(-.095f, .004f, .065f - i * .023f), new Vector3(.009f, .001f, .007f), ink, false, AnswerSheet);
            }
            Rod("Pencil / yellow body", new Vector3(.390f, .803f, -.14f), new Vector3(.435f, .803f, .10f), .0045f, pencilPaint, root);
            Rod("Pencil / graphite point", new Vector3(.388f, .803f, -.151f), new Vector3(.390f, .803f, -.14f), .0025f, ink, root);
            Rod("Pencil / pink eraser", new Vector3(.435f, .803f, .10f), new Vector3(.438f, .803f, .118f), .005f, chair, root);
        }

        void BuildDetails()
        {
            // Rear bulletin board: paperwork in an orderly, recognizably municipal palette.
            Box("Rear noticeboard / frame", new Vector3(2.8f, 1.85f, -5.95f), new Vector3(2.8f, 1.35f, .08f), wood);
            Box("Rear noticeboard / cork", new Vector3(2.8f, 1.85f, -5.896f), new Vector3(2.66f, 1.21f, .02f), cork);
            Notice("Attendance notice", "ATTENDANCE", "EVERY DAY COUNTS\n\nPlease report absences\nto the main office.", new Vector3(3.55f, 1.88f, -5.868f), new Vector2(.63f, .86f), new Vector3(0, 180, 0), blueInk);
            Notice("Emergency notice", "FIRE DRILL", "LEAVE ALL BELONGINGS\n\nWalk. Do not run.\nFollow your instructor.", new Vector3(2.76f, 1.88f, -5.868f), new Vector2(.63f, .86f), new Vector3(0, 180, 0), red);
            Notice("Lunch menu", "LUNCH MENU", "MON  Pizza\nTUE  Chicken sandwich\nWED  Spaghetti\nTHU  Soup & crackers\nFRI  Manager's choice", new Vector3(1.97f, 1.88f, -5.868f), new Vector2(.63f, .86f), new Vector3(0, 180, 0), lowerWall);
            Notice("Right wall / rules", "RESPECT", "YOURSELF\nOTHERS\nTHIS CLASSROOM\n\nWe are all being observed.", new Vector3(4.94f, 1.96f, .15f), new Vector2(.80f, 1.14f), new Vector3(0, 90, 0), blueInk);
            Notice("Right wall / mathematics", "SHOW YOUR WORK", "1. Read the question.\n2. Check your answer.\n3. Do not erase the truth.", new Vector3(4.94f, 2.02f, 2.45f), new Vector2(.95f, .80f), new Vector3(0, 90, 0), lowerWall);
            // Built-in low cupboard along the rear right, kept clear of the central route.
            Box("Storage / cabinet carcass", new Vector3(3.45f, .45f, -5.52f), new Vector3(2.20f, .90f, .64f), lowerWall, true);
            Box("Storage / laminate counter", new Vector3(3.45f, .918f, -5.52f), new Vector3(2.25f, .05f, .68f), wood);
            for (int i = 0; i < 4; i++)
            {
                float x = 2.63f + i * .545f;
                Box("Storage / cupboard door", new Vector3(x, .47f, -5.185f), new Vector3(.52f, .78f, .025f), trim);
                Box("Storage / handle", new Vector3(x + .17f, .58f, -5.158f), new Vector3(.022f, .13f, .032f), steel);
            }
            for (int i = 0; i < 5; i++)
                Box("Storage / exercise books", new Vector3(2.8f + i * .012f, .97f + i * .029f, -5.46f), new Vector3(.36f, .025f, .25f), i % 2 == 0 ? blueInk : chair);
            Box("Storage / cardboard archive", new Vector3(4.07f, 1.09f, -5.53f), new Vector3(.44f, .29f, .40f), wood);
            Box("Storage / archive label", new Vector3(4.07f, 1.10f, -5.322f), new Vector3(.27f, .10f, .006f), paper);
            Text("Storage / archive title", "1999 RESULTS", new Vector3(4.07f, 1.10f, -5.316f), .017f, new Color(.18f, .16f, .12f), new Vector3(0, 180, 0), TextAnchor.MiddleCenter);
            // Wastebasket with an open rim and a paper ball.
            Cylinder("Wastebasket / base", new Vector3(-3.93f, .05f, 4.95f), .18f, .07f, darkSteel);
            Cylinder("Wastebasket / body", new Vector3(-3.93f, .21f, 4.95f), .18f, .34f, lowerWall);
            Cylinder("Wastebasket / dark opening", new Vector3(-3.93f, .385f, 4.95f), .164f, .006f, darkSteel);
            Sphere("Wastebasket / crumpled paper", new Vector3(-3.96f, .39f, 4.98f), new Vector3(.11f, .08f, .13f), paper);
            // Radiators beneath the windows, with individually modelled ribs.
            for (int k = 0; k < 3; k++)
            {
                float z = -1.8f + 2.6f * k;
                Box("Radiator / collision body", new Vector3(-4.75f, .37f, z), new Vector3(.25f, .56f, 1.44f), trim, true);
                for (int j = 0; j < 13; j++)
                    Box("Radiator / cast rib", new Vector3(-4.59f, .37f, z - .66f + j * .11f), new Vector3(.10f, .55f, .045f), steel);
                Rod("Radiator / supply pipe", new Vector3(-4.77f, .08f, z + .81f), new Vector3(-4.77f, .48f, z + .81f), .018f, steel);
            }
            for (int z = -4; z <= 4; z += 4)
            {
                Box("Electrical / outlet plate", new Vector3(4.982f, .35f, z), new Vector3(.025f, .11f, .075f), trim);
                for (int i = -1; i <= 1; i += 2)
                    Box("Electrical / socket", new Vector3(4.965f, .35f + i * .025f, z), new Vector3(.008f, .013f, .023f), darkSteel);
            }
            Box("PA speaker / enclosure", new Vector3(-3.80f, 2.72f, 5.87f), new Vector3(.42f, .32f, .14f), trim);
            for (int i = 0; i < 7; i++)
                Box("PA speaker / grille", new Vector3(-3.80f, 2.62f + i * .031f, 5.79f), new Vector3(.32f, .009f, .012f), darkSteel);
            Box("Door / light switch plate", new Vector3(-4.97f, 1.28f, -3.62f), new Vector3(.025f, .12f, .075f), trim);
            powerSwitch = Box("Door / light switch", new Vector3(-4.947f, 1.28f, -3.62f), new Vector3(.020f, .04f, .024f), paper);
        }

        void Notice(string name, string title, string body, Vector3 at, Vector2 size, Vector3 euler, Material band)
        {
            Transform root = Child(name, room); root.localPosition = at; root.localRotation = Quaternion.Euler(euler);
            Box("Notice / stock", Vector3.zero, new Vector3(size.x, size.y, .009f), paper, false, root);
            Box("Notice / heading band", new Vector3(0, size.y * .5f - .11f, -.008f), new Vector3(size.x - .05f, .17f, .004f), band, false, root);
            Text("Notice / heading", title, new Vector3(0, size.y * .5f - .11f, -.012f), Math.Min(.13f, (size.x - .09f) / (title.Length * .68f)), new Color(.88f, .86f, .76f), Vector3.zero, TextAnchor.MiddleCenter, root);
            Text("Notice / body", body, new Vector3(0, -.06f, -.012f), .037f, new Color(.18f, .21f, .19f), Vector3.zero, TextAnchor.MiddleCenter, root);
            for (int side = -1; side <= 1; side += 2)
                Sphere("Notice / thumbtack", new Vector3(side * (size.x * .5f - .035f), size.y * .5f - .035f, -.017f), Vector3.one * .018f, red, root);
        }

        void BuildLighting()
        {
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z++)
            {
                Vector3 at = new Vector3(x * 2.3f, 3.16f, z * 3.7f);
                Box("Fluorescent / enamel housing", at, new Vector3(.68f, .10f, 1.32f), trim);
                Box("Fluorescent / recessed reflector", at + Vector3.down * .055f, new Vector3(.58f, .016f, 1.22f), steel);
                for (int side = -1; side <= 1; side += 2)
                    Rod("Fluorescent / luminous tube", at + new Vector3(side * .17f, -.076f, -.56f), at + new Vector3(side * .17f, -.076f, .56f), .022f, tube);
                for (int j = -2; j <= 2; j++)
                    Box("Fluorescent / protective louvre", at + new Vector3(0, -.09f, j * .23f), new Vector3(.61f, .025f, .015f), trim);
                Transform lightTransform = Child("Fluorescent / area fill", room); lightTransform.localPosition = at + Vector3.down * .24f;
                Light light = lightTransform.gameObject.AddComponent<Light>();
                light.type = LightType.Point; light.range = 6.5f; light.intensity = .66f;
                light.color = new Color(.82f, .91f, .79f);
                light.shadows = x == 1 && z != 0 ? LightShadows.Soft : LightShadows.None;
                light.shadowStrength = .58f; light.shadowBias = .025f; light.shadowNormalBias = .18f;
                Fluorescents.Add(light);
            }
            Transform sun = Child("Overcast afternoon / directional window light", room);
            sun.rotation = Quaternion.Euler(29, 108, 0);
            daylight = sun.gameObject.AddComponent<Light>(); daylight.type = LightType.Directional;
            daylight.intensity = .72f; daylight.color = new Color(.71f, .77f, .80f);
            daylight.shadows = LightShadows.Soft; daylight.shadowStrength = .72f;
            daylight.shadowBias = .035f; daylight.shadowNormalBias = .25f;
        }

        void BuildShirt()
        {
            Box("Rear wall / coat hooks", new Vector3(-.10f, 1.94f, -5.94f), new Vector3(1.48f, .11f, .06f), wood);
            for (int i = -1; i <= 1; i++)
            {
                Rod("Rear wall / hook", new Vector3(i * .52f - .1f, 1.94f, -5.90f), new Vector3(i * .52f - .1f, 1.90f, -5.83f), .009f, brass);
                Sphere("Rear wall / hook tip", new Vector3(i * .52f - .1f, 1.916f, -5.825f), Vector3.one * .025f, brass);
            }
            Shirt = Child("Blue shirt / appears only after observation", room);
            Shirt.localPosition = new Vector3(-.1f, 1.63f, -5.81f);
            // A closed, low-poly cloth volume with a distinct T-shirt silhouette.
            Vector2[] silhouette = {
                new Vector2(-.17f,.27f), new Vector2(-.29f,.23f), new Vector2(-.41f,.10f),
                new Vector2(-.30f,.015f), new Vector2(-.215f,.095f), new Vector2(-.205f,-.34f),
                new Vector2(.205f,-.34f), new Vector2(.215f,.095f), new Vector2(.30f,.015f),
                new Vector2(.41f,.10f), new Vector2(.29f,.23f), new Vector2(.17f,.27f),
                new Vector2(.105f,.20f), new Vector2(-.105f,.20f)
            };
            int n = silhouette.Length;
            var vertices = new Vector3[n * 2 + 2];
            vertices[n * 2] = new Vector3(0, -.02f, -.012f); vertices[n * 2 + 1] = new Vector3(0, -.02f, .065f);
            for (int i = 0; i < n; i++)
            {
                vertices[i] = new Vector3(silhouette[i].x, silhouette[i].y, 0);
                vertices[n + i] = new Vector3(silhouette[i].x, silhouette[i].y, .055f + .012f * Mathf.Sin(i * 2.2f));
            }
            var tris = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tris.Add(n * 2); tris.Add(j); tris.Add(i);
                tris.Add(n * 2 + 1); tris.Add(n + i); tris.Add(n + j);
                tris.Add(i); tris.Add(j); tris.Add(n + j);
                tris.Add(i); tris.Add(n + j); tris.Add(n + i);
            }
            Mesh mesh = new Mesh { name = "Solid blue cotton shirt" };
            mesh.vertices = vertices; mesh.triangles = tris.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh);
            Shirt.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            Shirt.gameObject.AddComponent<MeshRenderer>().sharedMaterial = blueCloth;
            // Subtle raised seams and a folded hem remain blue, not a vector outline.
            Box("Shirt / hem", new Vector3(0, -.324f, .058f), new Vector3(.40f, .018f, .012f), blueCloth, false, Shirt);
            Box("Shirt / chest pocket", new Vector3(-.10f, .065f, .068f), new Vector3(.095f, .105f, .011f), blueCloth, false, Shirt);
            Rod("Shirt / hanger left", new Vector3(0, .31f, .014f), new Vector3(-.17f, .245f, .014f), .006f, steel, Shirt);
            Rod("Shirt / hanger right", new Vector3(0, .31f, .014f), new Vector3(.17f, .245f, .014f), .006f, steel, Shirt);
            Shirt.gameObject.SetActive(false);
        }

        Material Surface(string name, Color color, Shader shader, float smoothness, float variation = 0, float metallic = 0)
        {
            Material result = new Material(shader) { name = "Test Anxiety / " + name };
            result.SetColor("_BaseColor", color); result.SetFloat("_Smoothness", smoothness); result.SetFloat("_Metallic", metallic);
            if (variation > 0)
            {
                const int size = 64; var data = new Color[size * size];
                var random = new System.Random(713 + owned.Count * 109);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float grain = 1 + ((float)random.NextDouble() - .5f) * variation * 5;
                    data[y * size + x] = new Color(grain, grain, grain, 1);
                }
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = name + " / procedural grain", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                texture.SetPixels(data); texture.Apply(false, true); owned.Add(texture);
                result.SetTexture("_BaseMap", texture); result.SetTextureScale("_BaseMap", new Vector2(2, 2));
            }
            owned.Add(result); return result;
        }

        Transform Child(string name, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }

        Transform Marker(string name, Vector3 position)
        {
            Transform result = Child(name, room); result.localPosition = position; return result;
        }

        Transform Primitive(string name, PrimitiveType type, Vector3 position, Vector3 size, Material material, bool collision, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent != null ? parent : room, false);
            go.transform.localPosition = position; go.transform.localScale = size;
            Renderer renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            Collider collider = go.GetComponent<Collider>();
            if (!collision && collider != null) { collider.enabled = false; Dispose(collider); }
            return go.transform;
        }

        Transform Box(string name, Vector3 position, Vector3 size, Material material, bool collision = false, Transform parent = null)
        { return Primitive(name, PrimitiveType.Cube, position, size, material, collision, parent); }

        Transform Sphere(string name, Vector3 position, Vector3 size, Material material, Transform parent = null)
        { return Primitive(name, PrimitiveType.Sphere, position, size, material, false, parent); }

        Transform Cylinder(string name, Vector3 position, float radius, float height, Material material, Vector3 euler = default(Vector3), Transform parent = null)
        {
            Transform result = Primitive(name, PrimitiveType.Cylinder, position, new Vector3(radius * 2, height * .5f, radius * 2), material, false, parent);
            result.localRotation = Quaternion.Euler(euler); return result;
        }

        Transform Rod(string name, Vector3 start, Vector3 end, float radius, Material material, Transform parent = null)
        {
            Vector3 delta = end - start;
            Transform result = Cylinder(name, (start + end) * .5f, radius, delta.magnitude, material, Vector3.zero, parent);
            result.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized); return result;
        }

        TextMesh Text(string name, string value, Vector3 position, float worldEmSize, Color color, Vector3 euler, TextAnchor anchor = TextAnchor.UpperLeft, Transform parent = null)
        {
            Transform t = Child(name, parent != null ? parent : room); t.localPosition = position; t.localRotation = Quaternion.Euler(euler);
            TextMesh text = t.gameObject.AddComponent<TextMesh>(); text.font = font; text.text = value;
            // Font resolution and physical size are independent: TextMesh scales pixel metrics by 0.1.
            text.fontSize = 64; text.characterSize = worldEmSize * 10f / text.fontSize; text.anchor = anchor;
            text.alignment = anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : TextAlignment.Left;
            text.lineSpacing = 1.2f; text.color = color;
            MeshRenderer renderer = t.GetComponent<MeshRenderer>(); renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return text;
        }

        void Clear()
        {
            if (room != null) { room.gameObject.SetActive(false); Dispose(room.gameObject); }
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Dispose(owned[i]);
            owned.Clear(); Fluorescents.Clear(); finalAtmosphere = false; room = null;
            ClearExpansion();
        }

        static void Dispose(UnityEngine.Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }

        void OnDestroy() { Clear(); }
    }
}
