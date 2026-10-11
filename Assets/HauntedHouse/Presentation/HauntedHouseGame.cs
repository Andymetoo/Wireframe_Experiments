using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HollowHouse
{
    /// <summary>Composition root and input adapter. All game rules live in GameSession.</summary>
    public sealed class HauntedHouseGame : MonoBehaviour
    {
        public GameSession Session { get; private set; }
        public HouseRenderer House { get; private set; }
        public bool AtTitle { get; private set; } = true;
        public string Panel { get; private set; } = "";
        public string SeedText = "71319";
        public string InputError = "";
        public float LastActionTime { get; private set; }
        float nextInput;
        Vector2 scroll;
        int journalTab;
        GUIStyle small, body, heading, title, button, muted, gold, danger;
        readonly Color ink = new Color(.025f, .043f, .050f, .98f);
        readonly Color line = new Color(.18f, .37f, .39f);
        readonly Color paper = new Color(.75f, .86f, .83f);
        readonly Color amber = new Color(.89f, .67f, .36f);
        const float Width = 1280, Height = 800;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            House = new GameObject("Procedural house").AddComponent<HouseRenderer>();
            NewCase(71319, false);
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-caseSeed" && i + 1 < args.Length && int.TryParse(args[i + 1], out int seed))
                    NewCase(seed, true);
        }

        public void NewCase(int seed, bool begin = true)
        {
            Session = new GameSession(ScenarioGenerator.Generate(seed));
            SeedText = seed.ToString();
            House.Build(Session.Scenario);
            House.SetView(Session.Position, Session.Facing, true);
            AtTitle = !begin;
            Panel = "";
            scroll = Vector2.zero;
            nextInput = Time.unscaledTime + .25f;
        }

        public void Act(Action action)
        {
            if (AtTitle || Panel != "" || Session.Ending != Ending.Investigating) return;
            action();
            LastActionTime = Time.unscaledTime;
            House.SetView(Session.Position, Session.Facing);
            nextInput = Time.unscaledTime + .16f;
        }

        public void OpenPanel(string panel) { Panel = panel; scroll = Vector2.zero; }
        public void ClosePanel() { Panel = ""; nextInput = Time.unscaledTime + .15f; }

        void Update()
        {
            House.Sync(Session, Time.unscaledTime);
            float scale = Mathf.Min(Screen.width / Width, Screen.height / Height);
            float left = (Screen.width - Width * scale) * .5f;
            float bottom = (Screen.height - Height * scale) * .5f;
            House.ViewCamera.rect = AtTitle ? new Rect(0,0,1,1) : new Rect(left / Screen.width,
                (bottom + 206 * scale) / Screen.height,951 * scale / Screen.width,518 * scale / Screen.height);
            var k = Keyboard.current;
            if (k == null) return;
            if (AtTitle) { if (k.enterKey.wasPressedThisFrame) BeginTypedSeed(); else if(k.escapeKey.wasPressedThisFrame) VectorCabinet.GameCollection.ReturnToMenu(); return; }
            if (k.escapeKey.wasPressedThisFrame) { if (Panel == "") OpenPanel("pause"); else ClosePanel(); return; }
            if (k.jKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame) { if (Panel == "journal") ClosePanel(); else OpenPanel("journal"); return; }
            if (k.mKey.wasPressedThisFrame) { if (Panel == "map") ClosePanel(); else OpenPanel("map"); return; }
            if (k.hKey.wasPressedThisFrame || k.f1Key.wasPressedThisFrame) { if (Panel == "help") ClosePanel(); else OpenPanel("help"); return; }
            if (Panel != "") { if (k.enterKey.wasPressedThisFrame) ClosePanel(); return; }
            if (Session.Ending != Ending.Investigating) { if (k.nKey.wasPressedThisFrame) NewCase(NewSeed()); return; }
            if (Time.unscaledTime < nextInput) return;
            if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) Act(() => Session.Move(0));
            else if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) Act(() => Session.Move(2));
            else if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) Act(() => Session.TurnFacing(-1));
            else if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) Act(() => Session.TurnFacing(1));
            else if (k.qKey.wasPressedThisFrame) Act(() => Session.Move(3));
            else if (k.eKey.wasPressedThisFrame) Interact();
            else if (k.cKey.wasPressedThisFrame) Act(() => Session.Move(1));
            else if (k.fKey.wasPressedThisFrame) Inspect();
            else if (k.tKey.wasPressedThisFrame) Act(() => Session.ReadMeter());
            else if (k.vKey.wasPressedThisFrame) Act(() => Session.Communicate());
            else if (k.lKey.wasPressedThisFrame) Act(() => Session.ToggleLantern());
            else if (k.spaceKey.wasPressedThisFrame) Act(() => Session.Wait());
            else if (k.xKey.wasPressedThisFrame) Act(() => Session.Ward());
            else if (k.gKey.wasPressedThisFrame) Act(() => Session.TakeRelic());
            else if (k.pKey.wasPressedThisFrame) Act(() => Session.PlaceRelic());
            else if (k.rKey.wasPressedThisFrame) Act(() => Session.Attune());
            else if (k.bKey.wasPressedThisFrame) Act(() => Session.Release());
        }

        bool DoorAhead()
        {
            int f = (int)Session.Facing;
            var step = new GridPos(f == 1 ? 1 : f == 3 ? -1 : 0, f == 0 ? 1 : f == 2 ? -1 : 0);
            var p = new GridPos(Session.Position.X + step.X, Session.Position.Y + step.Y);
            return Session.Scenario.Cells.TryGetValue(p, out Cell cell) && cell.DoorId >= 0;
        }
        void Interact() { if (DoorAhead()) Act(() => Session.InteractDoor()); else Inspect(); }
        void Inspect()
        {
            Act(() => Session.Inspect());
            if (Session.Ending == Ending.Investigating) OpenPanel("evidence");
        }
        void BeginTypedSeed()
        {
            if (string.IsNullOrWhiteSpace(SeedText)) { NewCase(NewSeed()); return; }
            if (int.TryParse(SeedText, out int seed)) { InputError = ""; NewCase(seed); }
            else InputError = "Use a whole number, or leave the case number empty.";
        }
        static int NewSeed() => unchecked(Environment.TickCount * 397 ^ DateTime.UtcNow.Millisecond);

        void Styles()
        {
            if (body != null) return;
            body = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, richText = false, normal = { textColor = paper } };
            small = new GUIStyle(body) { fontSize = 13 };
            muted = new GUIStyle(body) { fontSize = 14, normal = { textColor = new Color(.40f, .61f, .61f) } };
            heading = new GUIStyle(body) { fontSize = 25, fontStyle = FontStyle.Bold };
            title = new GUIStyle(body) { fontSize = 55, fontStyle = FontStyle.Bold };
            gold = new GUIStyle(body) { normal = { textColor = amber } };
            danger = new GUIStyle(body) { normal = { textColor = new Color(1f, .40f, .36f) } };
            button = new GUIStyle(body) { alignment = TextAnchor.MiddleLeft, fontSize = 15, padding = new RectOffset(12, 8, 2, 2) };
        }
        void Box(Rect r, Color color) { var old = GUI.color; GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old; }
        void Rule(float x, float y, float w) => Box(new Rect(x, y, w, 1), line);
        void Label(float x, float y, float w, float h, string text, GUIStyle style = null) => GUI.Label(new Rect(x,y,w,h), text, style ?? body);
        bool Button(float x, float y, float w, float h, string text)
        {
            var r = new Rect(x,y,w,h);
            bool hover = r.Contains(Event.current.mousePosition);
            Box(r, hover ? new Color(.10f,.23f,.24f) : new Color(.05f,.105f,.12f));
            Box(new Rect(x,y,2,h), hover ? amber : line);
            return GUI.Button(r, text, button);
        }

        void OnGUI()
        {
            if (Session == null) return;
            Styles();
            float scale = Mathf.Min(Screen.width / Width, Screen.height / Height);
            float ox = (Screen.width - Width * scale) * .5f, oy = (Screen.height - Height * scale) * .5f;
            GUI.matrix = Matrix4x4.identity;
            // Partial camera viewports do not clear the surrounding backbuffer. Paint real bars
            // so a previous title frame cannot remain visible at wider or taller aspect ratios.
            Box(new Rect(0,0,ox,Screen.height),Color.black);
            Box(new Rect(Screen.width-ox,0,ox,Screen.height),Color.black);
            Box(new Rect(0,0,Screen.width,oy),Color.black);
            Box(new Rect(0,Screen.height-oy,Screen.width,oy),Color.black);
            GUI.matrix = Matrix4x4.TRS(new Vector3(ox,oy,0), Quaternion.identity, Vector3.one * scale);
            if (AtTitle) { TitleScreen(); return; }
            DrawHud();
            if (Session.Ending != Ending.Investigating && Panel == "") EndScreen();
            else if (Panel != "") DrawPanel();
        }

        void TitleScreen()
        {
            Box(new Rect(0,0,1280,800), new Color(.005f,.012f,.018f,.78f));
            Box(new Rect(65,65,680,670), ink);
            if(Button(835,100,330,42,"ALL GAMES  [ESC]")) VectorCabinet.GameCollection.ReturnToMenu();
            Label(105,95,580,25,"BUREAU OF IMPOSSIBLE STRUCTURES  /  FIELD RECORD 09", muted);
            Rule(105,135,590);
            Label(100,167,620,142,"THE HOUSE\nTHAT LISTENS", title);
            Label(106,330,565,80,"A silent investigation in ten rooms.\nSomeone is still keeping the house in order.", gold);
            Label(106,427,560,91,"Read the house. Test its rules. Find what binds the dead.\nThen put things right before it learns your habits.");
            Label(106,528,180,25,"CASE NUMBER", muted);
            SeedText = GUI.TextField(new Rect(285,523,245,34),SeedText,12);
            if (Button(106,578,424,49,"ENTER THE HOUSE                                      [ENTER]")) BeginTypedSeed();
            if (Button(540,578,153,49,"NEW CASE")) NewCase(NewSeed());
            Label(106,645,580,54,InputError != "" ? InputError : "W / S step   ·   A / D turn   ·   E examine or open\nThe house moves only when you act. Reading is safe.",small);
            Label(835,576,370,90,"NO WEAPONS.\nNO VOICES.\nPAY ATTENTION.", heading);
            Label(835,700,370,40,"Geometry, darkness, and a little unfinished business.",muted);
        }

        void DrawHud()
        {
            var room = Session.Scenario.RoomAt(Session.Position);
            Box(new Rect(0,0,1280,76),ink);
            Label(26,15,700,28,room == null ? "BETWEEN ROOMS" : room.Name.ToUpperInvariant(),heading);
            Label(27,47,880,22,$"CASE {Session.Scenario.Seed}   /   {Session.Facing.ToString().ToUpperInvariant()}   /   ACTION {Session.Turn:000}   /   {(Session.Lantern ? "LANTERN LIT" : "LANTERN DARK")}",muted);
            Label(967,15,290,24,$"BODY  {new string('|',Math.Max(0,Session.Health))}   /   DREAD {Session.Fear:00}",Session.Health < 2 ? danger : gold);
            Label(967,46,290,24,$"SALT {Session.Salt}     METER {Session.Charges}",muted);
            Rule(0,75,1280);
            Box(new Rect(951,76,329,642),ink);
            Box(new Rect(951,76,1,642),line);
            Label(975,94,280,22,"FIELD ACTIONS",muted);
            ActionButton(127,DoorAhead() ? "E   Open / close door" : "E   Examine this room",Interact);
            ActionButton(167,"T   Read resonance meter",()=>Act(()=>Session.ReadMeter()));
            ActionButton(207,"V   Ask the house",()=>Act(()=>Session.Communicate()));
            ActionButton(247,"L   "+(Session.Lantern ? "Extinguish lantern" : "Light lantern"),()=>Act(()=>Session.ToggleLantern()));
            ActionButton(287,"SPACE   Wait / hold still",()=>Act(()=>Session.Wait()));
            ActionButton(327,"X   Lay protective salt",()=>Act(()=>Session.Ward()));
            Rule(975,377,280);
            Label(975,392,280,22,"OBJECTS & OBSERVANCES",muted);
            ActionButton(426,"G   Take / exchange keepsake",()=>Act(()=>Session.TakeRelic()));
            ActionButton(466,"P   Place carried keepsake",()=>Act(()=>Session.PlaceRelic()));
            ActionButton(506,"R   Mark / attune this room",()=>Act(()=>Session.Attune()));
            ActionButton(546,"B   Attempt release",()=>Act(()=>Session.Release()));
            Rule(975,598,280);
            string carried = Session.CarriedRelic >= 0 ? Session.Scenario.Relics[Session.CarriedRelic].Name : "Empty hands";
            Label(975,613,280,23,"CARRYING",muted);
            Label(975,641,280,55,carried,gold);

            Box(new Rect(0,594,951,124),new Color(.016f,.030f,.039f,.93f));
            Rule(25,594,898);
            int count = Session.Log.Count;
            if(count > 0) Label(26,609,895,55,Session.Log[count-1],Session.ThreatCountdown > 0 ? danger : body);
            if(count > 1) Label(26,670,895,36,Session.Log[count-2],muted);
            if (Session.ThreatCountdown > 0)
            {
                Box(new Rect(20,95,600,45),new Color(.19f,.035f,.033f,.94f));
                Label(34,104,575,32,$"MANIFESTATION  /  {Session.ThreatCountdown} actions before contact",danger);
            }
            Label(468,323,30,24,"+",muted);
            Box(new Rect(0,718,1280,82),ink);
            Rule(0,718,1280);
            Label(25,735,850,25,"W / S  STEP     A / D  TURN     Q / C  SIDESTEP     F  EXAMINE",small);
            Label(25,766,850,22,Session.IsSanctuary ? "THE VESTIBULE IS SAFE. Wait here to settle your nerves." : "Observe before acting. The dead obey rules; so must you.",muted);
            if(Button(900,735,103,40,"J  Journal")) OpenPanel("journal");
            if(Button(1013,735,103,40,"M  Map")) OpenPanel("map");
            if(Button(1126,735,129,40,"H  Field guide")) OpenPanel("help");
        }
        void ActionButton(float y,string text,Action action)
        {
            bool previous = GUI.enabled;
            GUI.enabled = Panel == "" && Session.Ending == Ending.Investigating;
            if(Button(975,y,280,34,text)) action();
            GUI.enabled = previous;
        }

        void DrawPanel()
        {
            Box(new Rect(0,0,1280,800),new Color(0,0,0,.80f));
            Box(new Rect(90,52,1100,696),ink);
            Label(122,78,875,42,Panel == "evidence" ? "ROOM EXAMINATION" : Panel == "journal" ? "INVESTIGATOR'S NOTEBOOK" : Panel == "map" ? "SURVEY OF THE HOUSE" : Panel == "pause" ? "HOLD YOUR BREATH" : "FIELD GUIDE",heading);
            if(Button(1020,77,137,36,"CLOSE  [ESC]")) ClosePanel();
            Rule(122,130,1034);
            Label(122,141,990,25,"TIME IS HELD. Reading, looking at the map, and turning do not advance the haunting.",muted);
            if (Panel == "map") { DrawMap(); return; }
            if (Panel == "pause") { PausePanel(); return; }
            if (Panel == "help") { Guide(); return; }
            if (Panel == "journal")
            {
                if(Button(122,181,240,34,"RECOVERED ACCOUNTS")) {journalTab=0;scroll=Vector2.zero;}
                if(Button(374,181,240,34,"FIELD OBSERVATIONS")) {journalTab=1;scroll=Vector2.zero;}
            }
            float start = Panel == "journal" ? 230 : 184;
            List<Evidence> entries = Panel == "evidence" ? Session.Scenario.RoomAt(Session.Position)?.Evidence : Session.Journal;
            if (Panel == "journal" && journalTab == 1)
            {
                float logHeight=15;
                foreach(string entry in Session.Log) logHeight+=body.CalcHeight(new GUIContent(entry),977)+22;
                scroll = GUI.BeginScrollView(new Rect(122,start,1034,692-start),scroll,new Rect(0,0,996,logHeight));
                float logY=4;
                for(int i=0;i<Session.Log.Count;i++)
                {
                    string entry=Session.Log[Session.Log.Count-1-i];
                    float height=body.CalcHeight(new GUIContent(entry),977);
                    Label(4,logY,977,height+4,entry);logY+=height+22;
                }
                GUI.EndScrollView(); return;
            }
            float total = 15;
            if(entries != null) foreach(var e in entries) total += 91 + body.CalcHeight(new GUIContent(e.Text),960);
            scroll = GUI.BeginScrollView(new Rect(122,start,1034,692-start),scroll,new Rect(0,0,996,total));
            float y = 4;
            if(entries == null || entries.Count==0) Label(4,y,968,80,"No written account recovered here. Examine rooms with E or F; use the meter and spirit board to test what the documents suggest.",muted);
            else foreach(var e in entries)
            {
                Label(4,y,960,37,e.Title.ToUpperInvariant(),gold); y+=43;
                float h=body.CalcHeight(new GUIContent(e.Text),960);
                Label(4,y,960,h+4,e.Text); y+=h+23;
                Rule(4,y,966); y+=25;
            }
            GUI.EndScrollView();
        }

        void Guide()
        {
            string text =
                "YOUR ASSIGNMENT\nFind the authentic keepsake among three similar objects. Compare the material and inscription in separate accounts. Learn where it belongs and what kind of observance this haunting needs. You must perform the procedure in the house.\n\n"+
                "EXPLORE & REMEMBER\nW/S move one tile; A/D turn 90 degrees; Q/C sidestep. E opens the door directly ahead, or examines the room. F always examines. Examinations work anywhere inside a room and cost one action. All recovered accounts are copied into J, your notebook. M maps only explored rooms.\n\n"+
                "TEST THE HOUSE\nT spends one meter charge to measure the local influence. V asks the house a question; it answers from its history and present state. Watch the feedback after changing L, your lantern. Space waits one action. No real-time countdown runs while you think.\n\n"+
                "SURVIVE\nManifestations warn before they strike. Learn a protective rule from records, the meter, or the spirit board. X uses a salt charge for emergency protection. The entrance vestibule is a permanent refuge: wait there to reduce dread. Supplies are finite, but no consumable is needed for the solution. Careless actions can injure or kill you.\n\n"+
                "PUT THINGS RIGHT\nG takes the keepsake in this room. Exchanging objects returns the previous one safely. P places what you carry. R marks or attunes a room; some observances need several rooms in a particular order. V can complete a spoken response once the echoes are restored. B attempts release. Use the actual records to discover which actions, places, light, and direction matter in this case. Wrong attempts can provoke the haunting, but do not destroy the solution.\n\n"+
                "LEAVE A RECORD\nEsc pauses. You may retreat from the vestibule. A case number reproduces the same house and mystery; NEW CASE generates another. The game is intentionally silent.";
            float h=body.CalcHeight(new GUIContent(text),978);
            scroll=GUI.BeginScrollView(new Rect(122,187,1034,514),scroll,new Rect(0,0,998,h+15));
            Label(4,0,978,h+10,text); GUI.EndScrollView();
        }

        void PausePanel()
        {
            Label(125,205,970,76,"The house waits for your next action.\nYour current investigation stays in memory while this screen is open.");
            if(Button(125,317,455,48,"RESUME INVESTIGATION")) ClosePanel();
            if(Button(125,385,455,48,"READ THE FIELD GUIDE")) OpenPanel("help");
            if (Session.IsSanctuary && Session.Ending == Ending.Investigating)
                if(Button(125,453,455,48,"WITHDRAW THROUGH THE FRONT DOOR")) {ClosePanel();Act(()=>Session.Escape());}
            if(Button(650,317,455,48,"RESTART THIS CASE")) NewCase(Session.Scenario.Seed);
            if(Button(650,385,455,48,"RETURN TO TITLE")) {AtTitle=true;Panel="";}
            if(Button(125,525,455,48,"RETURN TO CABINET")) VectorCabinet.GameCollection.ReturnToMenu();
            if(!Application.isEditor && Button(650,453,455,48,"EXIT GAME")) Application.Quit();
            Label(650,525,445,95,"Restarting, returning to the title, or exiting discards the current attempt. The case number always reconstructs the original mystery.",muted);
        }

        void DrawMap()
        {
            var s=Session.Scenario;
            int minX=int.MaxValue,maxX=int.MinValue,minY=int.MaxValue,maxY=int.MinValue;
            foreach(var r in s.Rooms) { if(!Session.VisitedRooms.Contains(r.Id)) continue; minX=Math.Min(minX,r.X);maxX=Math.Max(maxX,r.X);minY=Math.Min(minY,r.Y);maxY=Math.Max(maxY,r.Y); }
            float spacingX=Mathf.Min(160,860f/Math.Max(1,(maxX-minX)/4+1));
            float spacingY=Mathf.Min(90,420f/Math.Max(1,(maxY-minY)/4+1));
            Func<Room,Vector2> at=r=>new Vector2(220+(r.X-minX)/4*spacingX,240+(maxY-r.Y)/4*spacingY);
            float nodeWidth=Mathf.Min(145,spacingX-12), nodeHeight=Mathf.Min(75,spacingY-8);
            foreach(var d in s.Doors)
            {
                var a=s.Rooms[d.A];var b=s.Rooms[d.B];
                if(!Session.VisitedRooms.Contains(a.Id) || !Session.VisitedRooms.Contains(b.Id)) continue;
                var p=at(a);var q=at(b);
                Box(new Rect(Mathf.Min(p.x,q.x),Mathf.Min(p.y,q.y),Mathf.Abs(p.x-q.x)+2,Mathf.Abs(p.y-q.y)+2),line);
            }
            foreach(var r in s.Rooms)
            {
                if(!Session.VisitedRooms.Contains(r.Id)) continue;
                var p=at(r);bool current=s.RoomAt(Session.Position)?.Id==r.Id;
                float x=p.x-nodeWidth*.5f,y=p.y-nodeHeight*.5f;
                Box(new Rect(x,y,nodeWidth,nodeHeight),current ? new Color(.22f,.30f,.22f) : new Color(.055f,.12f,.13f));
                Box(new Rect(x,y,nodeWidth,2),current ? amber : line);
                Label(x+5,y+5,nodeWidth-9,nodeHeight-22,r.Name,small);
                Label(x+5,y+nodeHeight-23,nodeWidth-9,23,current ? FacingArrow() : Session.SearchedRooms.Contains(r.Id) ? "READ" : "...",muted);
            }
            Label(122,678,1000,33,"Only rooms you have entered are mapped. Gold marks your position. READ marks an examined room.",muted);
        }
        string FacingArrow() => new[]{"N ^","E >","S v","W <"}[(int)Session.Facing];

        void EndScreen()
        {
            Box(new Rect(0,0,1280,800),new Color(0,0,0,.79f));
            Box(new Rect(195,152,890,503),ink);
            bool won=Session.Ending==Ending.Won;
            Label(235,185,800,38,"CASE "+Session.Scenario.Seed+" / "+(won ? "CLOSED" : "UNRESOLVED"),muted);
            Label(229,241,820,80,won ? "THE HOUSE EXHALES" : Session.Ending==Ending.Lost ? "ANOTHER NAME IN THE HOUSE" : "YOU RETURN ALONE",heading);
            Label(235,315,790,104,won ? Session.Scenario.Identity+" is no longer held here. The lines become still. For the first time, the house is only a house." : Session.Ending==Ending.Lost ? "The record ends here. The rules were discoverable; the house gave its warnings. Your case number can reconstruct this investigation." : "The threshold releases you. The haunting remains, but so does your chance to understand it.");
            Label(235,435,790,38,$"{Session.Turn} actions   /   {Session.SearchedRooms.Count} rooms examined   /   {Session.Journal.Count} records recovered",gold);
            if(Button(235,514,375,48,"ANOTHER INVESTIGATION  [N]")) NewCase(NewSeed());
            if(Button(630,514,375,48,"RETRY CASE "+Session.Scenario.Seed)) NewCase(Session.Scenario.Seed);
            if(Button(235,584,375,36,"REVIEW YOUR NOTEBOOK")) OpenPanel("journal");
            if(Button(630,584,375,36,"RETURN TO CABINET")) VectorCabinet.GameCollection.ReturnToMenu();
        }
    }
}
