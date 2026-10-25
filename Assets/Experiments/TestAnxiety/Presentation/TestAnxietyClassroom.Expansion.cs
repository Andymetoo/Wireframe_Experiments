using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TestAnxiety
{
    public sealed partial class TestAnxietyClassroom
    {
        const string StandardBoardText = "STANDARDIZED OBSERVATION ASSESSMENT\n\nRead each question carefully.\nUse a No. 2 pencil. Fill the circle completely.\n\nLeave your booklet on the desk when finished.";

        public Transform ArchiveFocus { get; private set; }
        public Transform ArchiveView { get; private set; }
        public Transform FinalExitFocus { get; private set; }
        public Transform LightSwitchFocus { get; private set; }
        public Transform DeskSeatView { get; private set; }
        public Transform DeskSeatFocus { get; private set; }
        public Transform ProctorView { get; private set; }
        public Transform[] DocumentTargets { get; private set; } = new Transform[0];
        public string[] DocumentTitles { get; private set; } = new string[0];
        public string[] DocumentBodies { get; private set; } = new string[0];
        public bool ArchiveOpen { get { return archiveOpen; } }

        readonly Dictionary<int, Transform> numberedDesks = new Dictionary<int, Transform>();
        readonly Dictionary<int, Transform> numberedChairs = new Dictionary<int, Transform>();
        readonly List<Light> annexLights = new List<Light>();
        Transform entranceDoor, archiveLedger, clockMinute, clockHour, powerSwitch;
        Vector3 clockOrigin;
        TextMesh candidateName, candidateFrontName, candidateBackName, archiveStatus;
        Light deskLamp, emergencyFill;
        Material lampDiffuser;
        bool archiveOpen, attention, powerCut;
        float blackoutStrength, doorAngle, lastExpansionTime;
        int finalRecordChoice = -1;

        void BuildExpansion()
        {
            BuildRecordsArchitecture();
            BuildRecordsFurniture();
            BuildRecordsLighting();
            BuildDocuments();
            Box("Desk 014 / upright candidate card", new Vector3(0, .919f, .267f), new Vector3(.88f, .25f, .014f), blueInk, false, ExamDesk);
            candidateFrontName = Text("Candidate card / student face", "CANDIDATE 014", new Vector3(0, .919f, .256f), .095f, new Color(.88f, .87f, .78f), Vector3.zero, TextAnchor.MiddleCenter, ExamDesk);
            candidateBackName = Text("Candidate card / examiner face", "CANDIDATE 014", new Vector3(0, .919f, .278f), .095f, new Color(.88f, .87f, .78f), new Vector3(0, 180, 0), TextAnchor.MiddleCenter, ExamDesk);
            ArchiveFocus = Marker("Records / ledger interaction", new Vector3(-11, .83f, -1.30f));
            ArchiveView = Marker("Records / close-up camera", new Vector3(-11, 1.55f, -2.10f));
            ArchiveView.rotation = Quaternion.LookRotation(ArchiveFocus.position - ArchiveView.position, Vector3.up);
            FinalExitFocus = Marker("Records / outside door interaction", new Vector3(-12.85f, 1.18f, -4.65f));
            LightSwitchFocus = Marker("Classroom / power isolation", new Vector3(-4.90f, 1.28f, -3.62f));
            DeskSeatView = Marker("Desk 014 / seated eye position", new Vector3(0, 1.18f, -2.65f));
            DeskSeatView.localRotation = Quaternion.identity;
            DeskSeatFocus = ExamFocus;
            ProctorView = Marker("Examiner / final view", new Vector3(-.55f, 1.68f, 4.0f));
            ProctorView.rotation = Quaternion.LookRotation(ExamFocus.position - ProctorView.position, Vector3.up);
        }

        /// <summary>The hinged slab opens west, away from a player waiting inside the classroom.</summary>
        public void SetArchiveOpen(bool value)
        {
            archiveOpen = value;
            if (!Application.isPlaying && entranceDoor != null)
            {
                doorAngle = value ? -90 : 0;
                entranceDoor.localRotation = Quaternion.Euler(0, doorAngle, 0);
            }
        }

        public void SetArchiveCollected(bool value)
        {
            if (archiveLedger != null) archiveLedger.gameObject.SetActive(!value);
            if (archiveStatus != null) archiveStatus.text = value ? "REGISTER CHECKED OUT\nReturn before the next session." : "ATTENDANCE REGISTER\nROOM 107  /  OCTOBER 2000";
        }

        /// <summary>Zero restores the room. Only the three scripted vacancy choices are accepted.</summary>
        public void SetVacantDesk(int deskNumber)
        {
            if (deskNumber != 0 && deskNumber != 3 && deskNumber != 6 && deskNumber != 8) return;
            foreach (var entry in numberedDesks) if (entry.Value != null) entry.Value.gameObject.SetActive(entry.Key != deskNumber);
            foreach (var entry in numberedChairs) if (entry.Value != null) entry.Value.gameObject.SetActive(entry.Key != deskNumber);
        }

        public void SetAttention(bool value)
        {
            attention = value;
            foreach (var entry in numberedChairs)
            {
                Transform seat = entry.Value;
                if (seat == null) continue;
                Vector3 direction = ExamDesk.position - seat.position;
                direction.y = 0;
                seat.rotation = value && direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction, Vector3.up) : room.rotation;
            }
            RefreshBoard();
        }

        public void SetClockLate(bool value)
        {
            MoveClockHand(clockMinute, clockOrigin + Vector3.back * .049f,
                clockOrigin + (value ? new Vector3(0, .222f, -.049f) : new Vector3(.222f, 0, -.049f)), .006f);
            MoveClockHand(clockHour, clockOrigin + Vector3.back * .055f,
                clockOrigin + (value ? new Vector3(-.14f, 0, -.055f) : new Vector3(.14f, -.0184f, -.055f)), .009f);
        }

        public void SetBlackout(float strength) { blackoutStrength = Mathf.Clamp01(strength); }

        public void SetPowerCut(bool value)
        {
            powerCut = value;
            if (powerSwitch != null) powerSwitch.localRotation = Quaternion.Euler(0, 0, value ? 24 : 0);
            RefreshBoard();
        }

        public void SetFinalRecord(int choice)
        {
            finalRecordChoice = choice;
            if (choice == 0) SetCandidateName("MARA BELL");
            else if (choice == 1) SetCandidateName("CANDIDATE 014");
            RefreshBoard();
        }

        public void SetCandidateName(string value)
        {
            if (candidateName != null) candidateName.text = value ?? "";
            if (candidateFrontName != null) candidateFrontName.text = value ?? "";
            if (candidateBackName != null) candidateBackName.text = value ?? "";
        }

        void RefreshBoard()
        {
            if (boardMessage == null) return;
            if (powerCut) boardMessage.text = "";
            else if (finalRecordChoice == 0) boardMessage.text = "CANDIDATE 014\nRELEASED\n\nReturn the register to its proper place.";
            else if (finalRecordChoice == 1) boardMessage.text = "MARA BELL\nRELEASED\n\nThe next candidate may take their seat.";
            else if (finalRecordChoice >= 2) boardMessage.text = "RECORD NOT ACCEPTED\n\nMANUAL ISOLATION REQUIRED\n\nThe switch is beside the classroom entrance.";
            else if (finalAtmosphere) boardMessage.text = "ASSESSMENT RECEIVED\n\nConsult the attendance record before leaving.";
            else if (attention) boardMessage.text = "STANDARDIZED OBSERVATION ASSESSMENT\n\nKeep your eyes on your own paper.\n\nThe remaining time is not your concern.";
            else boardMessage.text = StandardBoardText;
        }

        void TickExpansion(float time)
        {
            float dt = Mathf.Clamp(time - lastExpansionTime, 0, .1f);
            lastExpansionTime = time;
            doorAngle = Mathf.MoveTowards(doorAngle, archiveOpen ? -90 : 0, dt * 105);
            if (entranceDoor != null) entranceDoor.localRotation = Quaternion.Euler(0, doorAngle, 0);
            float dim = Mathf.Lerp(1, .12f, blackoutStrength);
            foreach (Light light in annexLights) if (light != null) light.intensity = (powerCut ? .025f : .46f) * dim;
            if (deskLamp != null) deskLamp.intensity = powerCut ? 0 : .72f * dim;
            if (emergencyFill != null) emergencyFill.intensity = powerCut ? .28f : .12f;
            if (tube != null) tube.SetColor("_EmissionColor", new Color(1.4f, 1.6f, 1.3f) * (powerCut ? .015f : dim));
            if (lampDiffuser != null) lampDiffuser.SetColor("_EmissionColor", new Color(.70f, .54f, .26f) * (powerCut ? 0 : dim));
        }

        void OnEntranceDoor(Transform item) { item.SetParent(entranceDoor, true); }

        void MoveClockHand(Transform hand, Vector3 start, Vector3 end, float radius)
        {
            if (hand == null) return;
            Vector3 delta = end - start;
            hand.localPosition = (start + end) * .5f;
            hand.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            hand.localScale = new Vector3(radius * 2, delta.magnitude * .5f, radius * 2);
        }

        void BuildRecordsArchitecture()
        {
            // Floor slabs overlap the classroom by 15cm; there is no threshold gap or raised step.
            Box("Records corridor / continuous floor", new Vector3(-6.50f, -.09f, -4.65f), new Vector3(3.20f, .18f, 1.50f), tileB, true);
            Box("Records office / continuous floor", new Vector3(-10.50f, -.09f, -3), new Vector3(5.20f, .18f, 6.20f), tileB, true);
            for (int x = 0; x < 5; x++) for (int z = 0; z < 6; z++)
                Box("Records / vinyl tile", new Vector3(-12.5f + x, .003f, -5.5f + z), new Vector3(.993f, .006f, .993f), (x + z) % 3 == 0 ? tileB : tileA);
            for (int x = 0; x < 3; x++)
                Box("Records corridor / vinyl tile", new Vector3(-7.5f + x, .003f, -4.65f), new Vector3(.993f, .006f, 1.326f), tileC);
            Wall("Records corridor / north wall", new Vector3(-6.50f, 0, -3.90f), 3.16f, .16f);
            Wall("Records corridor / south wall", new Vector3(-6.50f, 0, -5.40f), 3.16f, .16f);
            Wall("Records office / north wall", new Vector3(-10.50f, 0, .08f), 5.16f, .16f);
            Wall("Records office / south wall", new Vector3(-10.50f, 0, -6.08f), 5.16f, .16f);
            Wall("Records office / east south pier", new Vector3(-7.92f, 0, -5.66f), .16f, .68f);
            Wall("Records office / east north wall", new Vector3(-7.92f, 0, -1.99f), .16f, 3.98f);
            Box("Records office / passage header", new Vector3(-7.92f, 2.81f, -4.65f), new Vector3(.16f, .90f, 1.34f), plaster, true);
            Box("Records corridor / ceiling", new Vector3(-6.5f, 3.32f, -4.65f), new Vector3(3.16f, .14f, 1.50f), ceiling, true);
            Box("Records office / ceiling", new Vector3(-10.50f, 3.32f, -3), new Vector3(5.16f, .14f, 6.16f), ceiling, true);
            for (int x = 0; x <= 5; x++)
                Box("Records / ceiling rail", new Vector3(-13 + x, 3.235f, -3), new Vector3(.018f, .025f, 6), trim);
            for (int z = 0; z <= 6; z++)
                Box("Records / ceiling rail", new Vector3(-10.5f, 3.235f, -6 + z), new Vector3(5, .025f, .018f), trim);

            // A west window and exit door occupy actual breaks in the west wall.
            Wall("Records / west south pier", new Vector3(-13.08f, 0, -5.66f), .16f, .68f);
            Wall("Records / west middle pier", new Vector3(-13.08f, 0, -3.64f), .16f, .68f);
            Wall("Records / west north pier", new Vector3(-13.08f, 0, -.55f), .16f, 1.10f);
            Box("Records / exit header", new Vector3(-13.08f, 2.81f, -4.65f), new Vector3(.16f, .90f, 1.34f), plaster, true);
            Box("Records / window sill wall", new Vector3(-13.08f, .525f, -2.2f), new Vector3(.16f, 1.05f, 2.2f), lowerWall, true);
            Box("Records / window header", new Vector3(-13.08f, 2.99f, -2.2f), new Vector3(.16f, .52f, 2.2f), plaster, true);
            Transform pane = Box("Records / overcast west window", new Vector3(-13.22f, 1.89f, -2.2f), new Vector3(.025f, 1.68f, 2.2f), glass, true);
            pane.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Records / window jamb", new Vector3(-12.98f, 1.89f, -2.2f + side * 1.07f), new Vector3(.13f, 1.78f, .07f), trim);
                Box("Records / window rail", new Vector3(-12.98f, 1.89f + side * .81f, -2.2f), new Vector3(.13f, .07f, 2.2f), trim);
            }
            Box("Records / window center mullion", new Vector3(-12.98f, 1.89f, -2.2f), new Vector3(.13f, 1.65f, .055f), steel);
            Box("Records / deep window sill", new Vector3(-12.86f, 1.075f, -2.2f), new Vector3(.35f, .07f, 2.3f), trim);
            Box("Records / radiator body", new Vector3(-12.77f, .37f, -2.2f), new Vector3(.22f, .55f, 1.65f), trim, true);
            for (int i = 0; i < 12; i++)
                Box("Records / radiator rib", new Vector3(-12.63f, .37f, -2.95f + i * .135f), new Vector3(.08f, .54f, .045f), steel);
            RecordsExitDoor();
            RecordsPartition();
            Text("Records corridor / room plate", "RECORDS", new Vector3(-7.82f, 2.59f, -4.65f), .12f, new Color(.18f, .22f, .20f), new Vector3(0, -90, 0), TextAnchor.MiddleCenter);
        }

        void RecordsExitDoor()
        {
            Box("Records / outside door", new Vector3(-13.025f, 1.16f, -4.65f), new Vector3(.07f, 2.32f, 1.20f), lowerWall, true);
            for (int side = -1; side <= 1; side += 2)
                Box("Records exit / jamb", new Vector3(-12.96f, 1.18f, -4.65f + side * .66f), new Vector3(.16f, 2.36f, .095f), trim);
            Box("Records exit / lintel", new Vector3(-12.96f, 2.36f, -4.65f), new Vector3(.16f, .10f, 1.42f), trim);
            Box("Records exit / push bar", new Vector3(-12.88f, 1.08f, -4.65f), new Vector3(.08f, .055f, .86f), steel);
            Box("Records exit / kick plate", new Vector3(-12.97f, .22f, -4.65f), new Vector3(.03f, .32f, 1.04f), steel);
            Box("Records exit / sign stock", new Vector3(-12.96f, 1.72f, -4.65f), new Vector3(.03f, .35f, .65f), paper);
            Text("Records exit / sign", "EXIT\nPUSH TO OPEN", new Vector3(-12.935f, 1.72f, -4.65f), .09f, new Color(.25f, .26f, .22f), new Vector3(0, -90, 0), TextAnchor.MiddleCenter);
            Box("Records exit / emergency fitting", new Vector3(-12.92f, 2.73f, -4.65f), new Vector3(.18f, .20f, .55f), trim);
            Sphere("Records exit / emergency lens", new Vector3(-12.81f, 2.73f, -4.65f), new Vector3(.055f, .11f, .23f), tube);
        }

        void RecordsPartition()
        {
            Material glazing = Surface("Records office internal glazing", new Color(.40f, .52f, .48f, .16f), plaster.shader, .6f);
            glazing.SetFloat("_Surface", 1); glazing.SetFloat("_Blend", 0); glazing.SetFloat("_ZWrite", 0);
            glazing.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); glazing.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            glazing.SetFloat("_Cull", 0); glazing.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glazing.SetOverrideTag("RenderType", "Transparent"); glazing.renderQueue = (int)RenderQueue.Transparent;
            // The 1.45m opening on the right admits the player without a second interaction.
            Box("Records partition / lower panel", new Vector3(-11.575f, .52f, -3.12f), new Vector3(2.85f, 1.04f, .08f), lowerWall, true);
            Box("Records partition / upper glass", new Vector3(-11.575f, 1.91f, -3.12f), new Vector3(2.85f, 1.72f, .014f), glazing, true).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box("Records partition / right pier", new Vector3(-8.35f, 1.40f, -3.12f), new Vector3(.70f, 2.8f, .08f), lowerWall, true);
            Box("Records partition / transom", new Vector3(-10.50f, 2.82f, -3.12f), new Vector3(5.0f, .12f, .10f), trim);
            for (int i = 0; i < 3; i++)
                Box("Records partition / mullion", new Vector3(-12.96f + i * 1.405f, 1.91f, -3.12f), new Vector3(.055f, 1.80f, .055f), trim);
            Box("Records partition / sill", new Vector3(-11.575f, 1.055f, -3.12f), new Vector3(2.89f, .055f, .13f), trim);
            Text("Records / glass lettering", "ATTENDANCE  /  107", new Vector3(-11.575f, 1.98f, -3.136f), .09f, new Color(.24f, .31f, .27f), Vector3.zero, TextAnchor.MiddleCenter);
        }

        void BuildRecordsFurniture()
        {
            Table("Records / clerk's desk", new Vector3(-11, 0, -1.30f), new Vector2(1.90f, .90f), .80f, true);
            StudentChair(new Vector3(-12.27f, 0, -1.35f), Quaternion.Euler(0, 90, 0), "Records / clerk's empty chair");
            for (int i = 0; i < 3; i++) FilingCabinet(new Vector3(-12.22f + i * .76f, 0, -.40f), i);
            // Flat labelled files provide the room's detail without filling every surface with primitives.
            Box("Records / shelving case", new Vector3(-8.42f, 1.02f, -1.35f), new Vector3(.62f, 2.04f, 1.80f), lowerWall, true);
            for (int level = 0; level < 3; level++)
            {
                Box("Records / shelf", new Vector3(-8.77f, .45f + level * .57f, -1.35f), new Vector3(.10f, .035f, 1.65f), steel);
                for (int row = 0; row < 3; row++)
                {
                    float z = -1.93f + row * .57f;
                    Box("Records / archive box", new Vector3(-8.78f, .66f + level * .57f, z), new Vector3(.20f, .38f, .47f), wood);
                    Text("Records / box label", (1998 + level) + "\n" + (row == 0 ? "A - H" : row == 1 ? "I - P" : "Q - Z"), new Vector3(-8.89f, .66f + level * .57f, z), .045f, new Color(.13f, .13f, .10f), new Vector3(0, 90, 0), TextAnchor.MiddleCenter);
                }
            }
            archiveLedger = Child("Records / collectible attendance register", room);
            archiveLedger.localPosition = new Vector3(-10.91f, .812f, -1.30f);
            Box("Register / folder cover", Vector3.zero, new Vector3(.48f, .014f, .37f), blueInk, false, archiveLedger);
            Box("Register / paper", new Vector3(0, .012f, -.005f), new Vector3(.45f, .006f, .34f), paper, false, archiveLedger);
            Text("Register / heading", "ROOM 107  /  ATTENDANCE\n17 OCTOBER 2000", new Vector3(0, .016f, .105f), .022f, new Color(.13f, .18f, .17f), new Vector3(90, 0, 0), TextAnchor.MiddleCenter, archiveLedger);
            Text("Register / retained entry", "SEAT     NAME             STATUS\n\n06       MARA BELL       EXCUSED\n\n014      -----------------------", new Vector3(0, .016f, -.035f), .019f, new Color(.18f, .19f, .15f), new Vector3(90, 0, 0), TextAnchor.MiddleCenter, archiveLedger);
            Box("Records / rubber stamp base", new Vector3(-10.41f, .84f, -1.10f), new Vector3(.15f, .07f, .08f), darkSteel);
            Cylinder("Records / rubber stamp handle", new Vector3(-10.41f, .91f, -1.10f), .028f, .10f, wood);
            Box("Records / ink pad", new Vector3(-10.42f, .819f, -1.38f), new Vector3(.16f, .035f, .11f), darkSteel);
            for (int i = 0; i < 4; i++)
                Box("Records / pending folders", new Vector3(-11.64f + .012f * i, .817f + .013f * i, -1.43f), new Vector3(.35f, .011f, .27f), i % 2 == 0 ? paper : wood);
            Box("Records / status frame", new Vector3(-11.0f, 2.24f, -.028f), new Vector3(1.86f, .44f, .025f), wood);
            Box("Records / status stock", new Vector3(-11.0f, 2.24f, -.045f), new Vector3(1.78f, .37f, .008f), paper);
            archiveStatus = Text("Records / status text", "ATTENDANCE REGISTER\nROOM 107  /  OCTOBER 2000", new Vector3(-11.0f, 2.24f, -.052f), .083f, new Color(.18f, .23f, .20f), Vector3.zero, TextAnchor.MiddleCenter);
            Notice("Records / retention policy", "KEEP THE ORIGINAL", "Working copies may be removed.\n\nThe original attendance entry\nmust remain legible.", new Vector3(-9.13f, 1.70f, -.026f), new Vector2(.88f, .97f), Vector3.zero, blueInk);
        }

        void FilingCabinet(Vector3 at, int index)
        {
            Transform root = Child("Records / filing cabinet " + (index + 1), room); root.localPosition = at;
            Box("Cabinet / body", new Vector3(0, .86f, 0), new Vector3(.65f, 1.72f, .56f), lowerWall, true, root);
            for (int drawer = 0; drawer < 4; drawer++)
            {
                float y = .24f + drawer * .39f;
                Box("Cabinet / drawer face", new Vector3(0, y, -.295f), new Vector3(.60f, .36f, .025f), trim, false, root);
                Box("Cabinet / handle", new Vector3(0, y + .07f, -.322f), new Vector3(.15f, .025f, .035f), steel, false, root);
                Box("Cabinet / label stock", new Vector3(0, y - .065f, -.313f), new Vector3(.22f, .065f, .004f), paper, false, root);
                Text("Cabinet / date label", (1998 + index) + " / " + (drawer + 1), new Vector3(0, y - .065f, -.318f), .032f, new Color(.18f, .20f, .17f), Vector3.zero, TextAnchor.MiddleCenter, root);
            }
        }

        void BuildRecordsLighting()
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = i == 0 ? new Vector3(-6.5f, 3.10f, -4.65f) : new Vector3(-10.35f, 3.10f, i == 1 ? -4.6f : -1.50f);
                Box("Records / fluorescent housing", at, new Vector3(1.14f, .10f, .37f), trim);
                Box("Records / diffuser", at + Vector3.down * .063f, new Vector3(1.03f, .025f, .29f), tube);
                Transform point = Child("Records / fluorescent fill", room); point.localPosition = at + Vector3.down * .24f;
                Light light = point.gameObject.AddComponent<Light>(); light.type = LightType.Point; light.range = 5;
                light.color = new Color(.84f, .88f, .77f); light.intensity = .46f; light.shadows = LightShadows.None;
                annexLights.Add(light);
            }
            lampDiffuser = Surface("Clerk lamp diffuser", new Color(.63f, .58f, .37f), plaster.shader, .18f);
            lampDiffuser.EnableKeyword("_EMISSION"); lampDiffuser.SetColor("_EmissionColor", new Color(.70f, .54f, .26f));
            Cylinder("Records / lamp base", new Vector3(-11.61f, .82f, -1.05f), .085f, .035f, darkSteel);
            Rod("Records / lamp stem", new Vector3(-11.61f, .84f, -1.05f), new Vector3(-11.61f, 1.15f, -1.05f), .010f, brass);
            Box("Records / green lamp shade", new Vector3(-11.61f, 1.19f, -1.05f), new Vector3(.36f, .11f, .19f), lowerWall);
            Box("Records / warm lamp lens", new Vector3(-11.61f, 1.13f, -1.05f), new Vector3(.30f, .012f, .13f), lampDiffuser);
            Transform bulb = Child("Records / desk lamp pool", room); bulb.localPosition = new Vector3(-11.55f, 1.08f, -1.15f);
            deskLamp = bulb.gameObject.AddComponent<Light>(); deskLamp.type = LightType.Point; deskLamp.range = 2.4f;
            deskLamp.intensity = .72f; deskLamp.color = new Color(1, .79f, .48f); deskLamp.shadows = LightShadows.None;
            Transform emergency = Child("Records / maintained emergency fill", room); emergency.localPosition = new Vector3(-11.5f, 2.55f, -4.65f);
            emergencyFill = emergency.gameObject.AddComponent<Light>(); emergencyFill.type = LightType.Point;
            emergencyFill.range = 5.8f; emergencyFill.intensity = .12f; emergencyFill.color = new Color(.73f, .78f, .67f);
            emergencyFill.shadows = LightShadows.None;
        }

        void BuildDocuments()
        {
            Box("Teacher / folded parent note", new Vector3(-1.25f, .811f, 4.50f), new Vector3(.21f, .006f, .28f), paper);
            Text("Teacher / parent note heading", "PLEASE CALL\nBEFORE DISMISSAL", new Vector3(-1.25f, .816f, 4.50f), .016f, new Color(.17f, .20f, .18f), new Vector3(90, 0, 0), TextAnchor.MiddleCenter);
            Notice("Records / service card", "SERVICE RECORD", "OPTICAL READER OMR-98\n\nIsolate at classroom switch.\nDo not remove retained originals.", new Vector3(-8.75f, 1.66f, -5.93f), new Vector2(.95f, .85f), new Vector3(0, 180, 0), lowerWall);
            DocumentTargets = new[] {
                Marker("Document / evacuation drill", new Vector3(2.76f, 1.88f, -5.84f)),
                Marker("Document / folded parent note", new Vector3(-1.25f, .82f, 4.50f)),
                Marker("Document / maintenance card", new Vector3(-8.75f, 1.66f, -5.90f))
            };
            DocumentTitles = new[] { "EVACUATION DRILL / ROOM 107", "A FOLDED NOTE", "DISTRICT EQUIPMENT SERVICE CARD" };
            DocumentBodies = new[] {
                "17 OCTOBER 2000\n\nAt the signal, leave bags and papers on the desks. Walk through the records passage to the outside door. The teacher will bring the attendance register.\n\nCount students before leaving the classroom. Count again outside. If the totals differ, do not correct the register until the missing student has been located.\n\nPlease do not practice the alarm during examinations.",
                "Mrs. Keene,\n\nMara says she has been moved from seat 06 again. She cannot tell me where she sits now, only that she is keeping someone else's place.\n\nIf she finishes early today, please have her wait in the office. I will collect her myself. Please call before marking her absent.\n\nE. Bell\n\nA receptionist has written underneath: NO EARLY COLLECTION RECORDED.",
                "OMR-98 / DISTRICT PROPERTY / ROOM 107\n\nComplaint: reader retains a copy after the sheet is returned.\n\nInspection: feed rollers clear. Lamp and optical head within tolerance. Replaced fuse. Retained image persists after ordinary shutdown.\n\nFor manual isolation, use the classroom wall switch beside the entrance. The records exit remains on the emergency circuit.\n\nDo not alter the original attendance entry to match a returned copy.\n\nService completed: 16 OCT 2000."
            };
        }

        void ClearExpansion()
        {
            numberedDesks.Clear(); numberedChairs.Clear(); annexLights.Clear();
            entranceDoor = archiveLedger = clockMinute = clockHour = powerSwitch = null;
            candidateName = candidateFrontName = candidateBackName = archiveStatus = null; deskLamp = emergencyFill = null; lampDiffuser = null;
            archiveOpen = attention = powerCut = false; blackoutStrength = doorAngle = lastExpansionTime = 0; finalRecordChoice = -1;
            ArchiveFocus = ArchiveView = FinalExitFocus = LightSwitchFocus = DeskSeatView = DeskSeatFocus = ProctorView = null;
            DocumentTargets = new Transform[0]; DocumentTitles = DocumentBodies = new string[0];
        }
    }
}
