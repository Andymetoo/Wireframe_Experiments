using System;
using System.Collections.Generic;
using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using SimulationMode = IceResurfacer.Core.SimulationMode;

namespace IceResurfacer.Presentation
{
    /// <summary>Immediate-mode operator instruments. Only writes operator commands; never edits ice or scoring.</summary>
    public sealed class IceDashboard : IDisposable
    {
        public const float LayoutWidth = 1600f, LayoutHeight = 900f;
        public bool AuditLayout { get; set; }
        public bool Compact { get; set; } = true;
        public bool HistoryVisible { get; private set; }
        public bool MessagesVisible { get; private set; } = true;
        public int InspectedSurveyCell { get; private set; } = -1;
        public readonly List<string> LayoutIssues = new List<string>();
        static readonly Color Navy = new Color(.035f, .068f, .09f, .96f);
        static readonly Color Modal = new Color(.035f, .068f, .09f, 1);
        static readonly Color Inset = new Color(.025f, .044f, .058f, 1);
        static readonly Color Slate = new Color(.13f, .20f, .24f, 1);
        static readonly Color Muted = new Color(.72f, .80f, .83f, 1);
        static readonly Color White = new Color(.91f, .95f, .95f, 1);
        static readonly Color Mint = new Color(.44f, .90f, .76f, 1);
        static readonly Color Amber = new Color(1f, .70f, .32f, 1);
        static readonly Color Red = new Color(1f, .39f, .31f, 1);
        GUIStyle text, small, heading, title, number, button;
        Texture2D disc, ring;
        float dialLastAngle;
        string hoverHint;
        int springControl;
        bool pointerCaptured;
        bool releaseCapture;
        readonly List<string> messageHistory = new List<string>(32);
        string lastNotice = "";
        float noticeAt;
        Simulation messageSession;
        Vector2 messageScroll;
        public static readonly Rect SurveyMapRect = new Rect(1228,72,142,272.79f);
        static bool Inspecting(IceResurfacerGame game) => !string.IsNullOrEmpty(game.HeldQuicklook);

        public void ReleaseInteractions() { springControl=0; pointerCaptured=false; releaseCapture=true; }
        public void CheckPointerRelease(IceResurfacerGame game)
        {
            // Polling protects against a lost native MouseUp. Only release our own
            // momentary lever; keyboard and physical interactions have separate owners.
            if(!pointerCaptured||Mouse.current!=null&&Mouse.current.added&&Mouse.current.leftButton.isPressed)return;
            if(springControl==501)game.Sim.SetConditionerHydraulic(0);
            if(springControl==505)game.Sim.SetTankHydraulic(0);
            ReleaseInteractions();
        }
        public bool PointerOverInterface(IceResurfacerGame game,Vector2 screenPoint)
        {
            float scale=Mathf.Min(Screen.width/LayoutWidth,Screen.height/LayoutHeight);
            Vector2 p=(new Vector2(screenPoint.x,Screen.height-screenPoint.y)-new Vector2((Screen.width-LayoutWidth*scale)*.5f,(Screen.height-LayoutHeight*scale)*.5f))/scale;
            if(!game.Started||game.Paused||game.HelpVisible||game.Sim.Finished||game.RouteGuide!=null&&game.RouteGuide.PanelOpen)return true;
            if(game.RouteGuide!=null&&game.RouteGuide.Visible&&(!Inspecting(game)||game.ChaseActive)&&!game.Sim.OnFoot&&new Rect(398,145,492,104).Contains(p))return true;
            if(new Rect(28,24,1164,108).Contains(p))return true;
            if(game.DiagnosticsVisible&&(Compact?new Rect(28,132,492,242).Contains(p):
                new Rect(545,228,755,390).Contains(p)||new Rect(28,520,492,100).Contains(p)))return true;
            if(game.Sim.OnFoot)return p.y>700;
            if(HistoryVisible&&new Rect(520,160,650,570).Contains(p))return true;
            if(new Rect(398,849,1174,43).Contains(p))return true;
            return !Inspecting(game)&&new Rect(1210,24,362,Compact?432:814).Contains(p);
        }

        void Initialize()
        {
            if (text != null) return;
            text = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true, richText = false, padding = new RectOffset(0, 0, 0, 0) };
            text.normal.textColor = White;
            small = new GUIStyle(text) { fontSize = 16 };
            heading = new GUIStyle(text) { fontSize = 17, fontStyle = FontStyle.Bold };
            title = new GUIStyle(text) { fontSize = 31, fontStyle = FontStyle.Bold };
            number = new GUIStyle(text) { fontSize = 34, fontStyle = FontStyle.Bold };
            button = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter, fontSize = 17, fontStyle = FontStyle.Bold, wordWrap = false, padding = new RectOffset(8, 8, 3, 3) };
            button.normal.background = null;
            button.hover.background = null;
            button.active.background = null;
            button.focused.background = null;
            button.hover.textColor = White;
            button.active.textColor = White;
            disc = Circle(false); ring = Circle(true);
        }

        static Texture2D Circle(bool hollow)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = hollow ? "IceResurfacer / instrument ring" : "IceResurfacer / instrument disc", hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(48, 48));
                float alpha = Mathf.Clamp01(47 - distance);
                if (hollow) alpha *= Mathf.Clamp01(distance - 38);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }

        public static Vector2 CanvasToViewport(Vector2 point, float width, float height)
        {
            float scale = Mathf.Min(width / LayoutWidth, height / LayoutHeight);
            return new Vector2((width - LayoutWidth * scale) * .5f, (height - LayoutHeight * scale) * .5f) + point * scale;
        }

        /// <summary>Actual clickable bounds in the reference canvas, also used by explicit validation.</summary>
        public static Rect ControlRect(string name)
        {
            switch (name)
            {
                case "MachineLoad": return new Rect(460,32,145,60);
                case "VerticalAuger": return new Rect(1228,446,68,94);
                case "Conditioner": return new Rect(1310,446,68,94);
                case "HorizontalAuger": return new Rect(1392,446,68,94);
                case "TankLift": return new Rect(1474,446,68,94);
                case "Flood": return new Rect(1228,587,88,88);
                case "WashValve": return new Rect(1342,587,88,88);
                case "Blade": return new Rect(1461,590,80,80);
                case "BoardBrush": return new Rect(1228,716,154,34);
                case "WashPump": return new Rect(1396,716,158,34);
                case "SnowBreaker": return new Rect(1228,758,154,34);
                case "Service": return new Rect(1396,758,158,34);
                case "Finish": return new Rect(1228,800,154,34);
                case "NewSession": return new Rect(1396,800,158,34);
                case "Guide": return new Rect(1364,851,93,37);
                case "Pause": return new Rect(1465,851,107,37);
                case "Console": return new Rect(1228,382,326,34);
                case "History": return new Rect(1228,851,128,37);
                case "RouteRecorder": return new Rect(1018,851,188,37);
                default: throw new ArgumentException("Unknown Aurora instrument: " + name);
            }
        }

        public void Draw(IceResurfacerGame game) { Draw(game, Screen.width, Screen.height); }

        public void Draw(IceResurfacerGame game, float width, float height)
        {
            Initialize();
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            int oldDepth = GUI.depth;
            try
            {
                float scale = Mathf.Min(width / LayoutWidth, height / LayoutHeight);
                GUI.matrix = Matrix4x4.TRS(new Vector3((width - LayoutWidth * scale) * .5f, (height - LayoutHeight * scale) * .5f), Quaternion.identity, new Vector3(scale, scale, 1));
                GUI.color = Color.white; GUI.depth = -10; GUI.enabled = game.RouteGuide==null||!game.RouteGuide.PanelOpen;
                if(releaseCapture) { GUIUtility.hotControl=0;releaseCapture=false; }
                hoverHint = game.PhysicalControls!=null?game.PhysicalControls.Hint:"";
                LayoutIssues.Clear();
                if(Event.current.type==EventType.Repaint)game.RouteGuide?.DrawSilhouette(width,height);
                Header(game);
                if(!game.Sim.OnFoot&&!Inspecting(game))Survey(game);
                if(game.Sim.OnFoot)ServicePanel(game);
                else if(Compact)CompactRack(game);
                else Rack(game);
                if (game.Started && !game.Sim.Finished && !game.HelpVisible && !game.Paused)
                {
                    if(!game.Sim.OnFoot)Guidance(game);
                    DriveWarning(game);
                    if(game.DiagnosticsVisible)Diagnostics(game);
                    if(!game.Sim.OnFoot&&(!Inspecting(game)||game.ChaseActive)&&game.RouteGuide!=null&&game.RouteGuide.Visible)TrainingCue(game);
                    if(game.RouteGuide!=null&&game.RouteGuide.Recorder.Recording)
                    {Box(new Rect(398,258,620,48),new Color(.38f,.025f,.08f,.98f));Label(new Rect(414,264,588,36),"● RECORDING  /  NUMPAD 9 STOPS & SAVES",text,White);}
                }
                if(!game.Sim.OnFoot)StatusStrip(game);
                if (game.Sim.Finished && !game.HelpVisible) Results(game);
                else if (!game.Started && !game.HelpVisible) Welcome(game);
                else if (game.Paused && !game.HelpVisible) Pause(game);
                if (game.HelpVisible) Help(game);
                if (!string.IsNullOrEmpty(hoverHint) && !game.HelpVisible && game.Started && !game.Paused && !game.Sim.Finished)
                {
                    Box(new Rect(398,764,806,76), Navy);
                    Label(new Rect(414,774,774,58), hoverHint, small, White);
                }
                if(game.PerformanceVisible)
                {
                    Box(new Rect(28,865,340,27),Navy);
                    Label(new Rect(38,868,320,23),game.Performance.Summary,small,Muted);
                }
                GUI.enabled=true;game.RouteGuide?.DrawRecordingPanel();
            }
            finally
            {
                GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.enabled = oldEnabled; GUI.depth = oldDepth;
            }
        }

        public static string ConditionerStatus(Simulation s)
        {
            float lift=s.Conditioner.Lift;
            if(lift<=.005f)return "CONDITIONER DOWN";
            if(lift>=.995f)return "CONDITIONER UP";
            float demand=s.Controls.HydraulicHoldControls?s.Controls.ConditionerHydraulic:s.Controls.ConditionerTarget-lift;
            if(demand>.001f)return "CONDITIONER RAISING";
            if(demand<-.001f)return "CONDITIONER LOWERING";
            return "CONDITIONER PARTIALLY LOWERED";
        }
        void Header(IceResurfacerGame game)
        {
            var s=game.Sim;
            Box(new Rect(28,24,1164,108),Navy);Box(new Rect(28,24,5,108),Amber);
            Label(new Rect(48,36,290,39),"AURORA / R-54",title,White);
            Label(new Rect(49,77,285,23),"NORTHLINE / "+Duration(s.Time),small,Muted);
            Label(new Rect(338,35,116,44),(Mathf.Abs(s.Vehicle.Speed)*3.6f).ToString("0.0"),number,Mint);
            Label(new Rect(338,78,125,22),s.Vehicle.Speed<-.02f?"km/h REVERSE":"km/h FORWARD",small,Muted);
            Meter(new Rect(470,36,125,49),"MACHINE LOAD",s.MachineLoad,(s.MachineLoad*100).ToString("0")+"%",s.MachineLoad>.85f?Amber:Mint);
            Rect loadRect=ControlRect("MachineLoad");
            if(loadRect.Contains(Event.current.mousePosition))hoverHint="Estimated machinery demand; higher load reduces acceleration. Click or F3 for the breakdown.";
            if(game.Started&&!game.Paused&&!game.HelpVisible&&!s.Finished&&!game.Looking&&GUI.Button(loadRect,GUIContent.none,GUIStyle.none))game.ToggleDiagnostics();
            Meter(new Rect(620,36,157,49),"FLOOD WATER",s.FloodWaterLiters/s.Config.FloodCapacityLiters,s.FloodWaterLiters.ToString("0")+" L",Mint);
            Meter(new Rect(800,36,157,49),"WASH WATER",s.WashWaterLiters/s.Config.WashCapacityLiters,s.WashWaterLiters.ToString("0")+" L",Mint);
            float snow=s.SnowTankM3/s.Config.SnowCapacityM3;
            Meter(new Rect(980,36,188,49),"SNOW TANK",snow,(snow*100).ToString("0")+"% / "+s.SnowTankM3.ToString("0.00")+" m3",snow>.85f?Amber:Mint);
            string status=ConditionerStatus(s);
            Label(new Rect(48,106,432,23),status,heading,s.Conditioner.Lift<.005f?Mint:Amber);
            Box(new Rect(490,112,102,8),Slate);Box(new Rect(490,112,102*(1-s.Conditioner.Lift),8),Mint);
            float contactBite=s.ActualBladeDepthMm*Mathf.Clamp01(1-s.Conditioner.Lift/.25f);
            Label(new Rect(620,105,550,24),"BLADE "+s.Controls.BladeDepthMm.ToString("0.00")+" mm SET  /  "+contactBite.ToString("0.000")+" mm CONTACT",small,White);
        }

        void Survey(IceResurfacerGame game)
        {
            var s = game.Sim; var r = game.Report;
            Box(new Rect(1210,24,362,342),Navy);
            Label(new Rect(1228,40,326,23), "SURFACE SURVEY", heading, White);
            Rect map = SurveyMapRect;
            Box(map, Inset);
            if (game.Surface != null && game.Surface.MapTexture != null)
            {
                Vector2 top=MapPoint(map,-s.Config.RinkWidth*.5f,s.Config.RinkLength*.5f,s.Config);
                Vector2 bottom=MapPoint(map,s.Config.RinkWidth*.5f,-s.Config.RinkLength*.5f,s.Config);
                GUI.DrawTexture(new Rect(top,bottom-top), game.Surface.DebugMode==0?game.Surface.OverviewTexture:game.Surface.MapTexture, ScaleMode.StretchToFill, true);
            }
            var layout=s.Layout;
            layout.ToWorld(-layout.RoomHalfWidth,layout.RoomStart,out float ax,out float az);
            layout.ToWorld(layout.RoomHalfWidth,layout.RoomStart,out float bx,out float bz);
            layout.ToWorld(layout.RoomHalfWidth,layout.RoomLength,out float cx,out float cz);
            layout.ToWorld(-layout.RoomHalfWidth,layout.RoomLength,out float dx,out float dz);
            Vector2 a=MapPoint(map,ax,az,s.Config),b=MapPoint(map,bx,bz,s.Config),cc=MapPoint(map,cx,cz,s.Config),d=MapPoint(map,dx,dz,s.Config);
            Line(a,b,2,Muted);Line(b,cc,2,Muted);Line(cc,d,2,Muted);Line(d,a,2,Muted);
            Vector2 gate=MapPoint(map,layout.GateX,layout.GateZ,s.Config);
            Box(new Rect(gate.x-3,gate.y-3,6,6),Mint);
            if(game.RouteGuide!=null&&game.RouteGuide.Visible)DrawRouteMap(game,map);
            Vector2 p = MapPoint(map, s.Vehicle.X, s.Vehicle.Z, s.Config);
            p.x = Mathf.Clamp(p.x, map.x + 3, map.xMax - 3);
            p.y = Mathf.Clamp(p.y, map.y + 8, map.yMax - 8);
            Vector2 forward = new Vector2(Mathf.Sin(s.Vehicle.Heading), -Mathf.Cos(s.Vehicle.Heading));
            Line(p - forward * 5, p + forward * 7, 3, White);
            Line(p + forward * 7, p + new Vector2(-forward.y, forward.x) * 5, 2, White);
            Line(p + forward * 7, p - new Vector2(-forward.y, forward.x) * 5, 2, White);
            Vector2 tail = MapPoint(map, s.Conditioner.X, s.Conditioner.Z, s.Config);
            if (map.Contains(tail))
            {
                Vector2 right = new Vector2(Mathf.Cos(s.Conditioner.Heading), Mathf.Sin(s.Conditioner.Heading));
                Line(tail - right * 5.3f, tail + right * 5.3f, 3, Amber);
            }
            Label(new Rect(1388,79,164,23),"TREATMENT",heading,Mint);
            Label(new Rect(1388,108,164,40),(r.Coverage*100).ToString("0.0")+"%",number,Mint);
            Label(new Rect(1388,149,164,24),"Finish "+(r.FinishQuality*100).ToString("0")+"%",small,White);
            if(game.Surface!=null&&game.Surface.DebugMode==0)
            {
                MapKey(1388,184,IceTreatmentState.Uncut);MapKey(1388,208,IceTreatmentState.CutButDry);
                MapKey(1388,232,IceTreatmentState.WetButUncut);MapKey(1388,256,IceTreatmentState.SnowCovered);
                MapKey(1388,280,IceTreatmentState.Overwatered);MapKey(1388,304,IceTreatmentState.Resurfaced);
            }
            else Label(new Rect(1388,184,164,130),game.Surface!=null&&game.Surface.DebugMode==1?
                "F2 / SHAVING MASK\nOrange: excluded rim\nGreen: treatment":"F2 / MATERIAL\nRed cut\nGreen wash\nBlue flood",small,Muted);
            Label(new Rect(1388,337,164,23),game.RouteGuide!=null&&game.RouteGuide.Visible&&game.RouteGuide.Progress!=null?"GPS LEG "+(game.RouteGuide.Progress.Leg+1)+" / 40 ft":"Hover: exact cells",small,game.RouteGuide!=null&&game.RouteGuide.Visible?game.RouteGuide.GpsColor:Muted);
            SurveyDetail(game,map);
        }

        void MapKey(float x,float y,IceTreatmentState state)
        {
            Box(new Rect(x,y+5,8,8),IceSurfaceView.TreatmentColor(state));
            Label(new Rect(x+12,y,150,20),IceCoverage.Label(state),small,Muted);
        }

        void SurveyDetail(IceResurfacerGame game,Rect map)
        {
            InspectedSurveyCell=-1;
            if(game.HelpVisible||game.Paused||!game.Started||game.Sim.Finished||game.Looking||!map.Contains(Event.current.mousePosition))return;
            var s=game.Sim;var field=s.Ice;Vector2 mouse=Event.current.mousePosition;
            float x=-s.Config.RinkWidth*.5f+(mouse.x-map.x)/map.width*(s.Config.RinkWidth+12);
            float z=s.Config.RinkLength*.5f-(mouse.y-map.y)/map.height*(s.Config.RinkLength+13);
            int index=field.IndexAt(x,z);if(index<0||!field.Cells[index].IsIce)return;
            InspectedSurveyCell=index;
            var cell=field.Cells[index];var state=IceCoverage.Classify(cell,s.Config);
            hoverHint=IceCoverage.Label(state)+" | cut "+cell.CutDepthMm.ToString("0.0000")+" mm (>"+IceCoverage.RequiredCutMm(s.Config).ToString("0.0000")+
                ") | water "+cell.FloodMm.ToString("0.00")+" mm (>0.08) | snow "+cell.SnowMm.ToString("0.00")+" mm (<0.25) | groove "+cell.GougeDepthMm.ToString("0.00")+" mm";
            const int span=16;
            int col=index%field.Columns,row=index/field.Columns;
            int left=Mathf.Clamp(col-span/2,0,field.Columns-span),bottom=Mathf.Clamp(row-span/2,0,field.Rows-span);
            Box(new Rect(970,154,224,276),Modal);
            Label(new Rect(982,164,200,24),"PATCH DETAIL",heading,Mint);
            Rect zoom=new Rect(982,194,200,200);
            GUI.DrawTextureWithTexCoords(zoom,game.Surface.MapTexture,new Rect((float)left/field.Columns,(float)bottom/field.Rows,(float)span/field.Columns,(float)span/field.Rows));
            float side=zoom.width/span;
            Rect selected=new Rect(zoom.x+(col-left)*side,zoom.y+(span-1-(row-bottom))*side,side,side);
            Line(new Vector2(selected.x,selected.y),new Vector2(selected.xMax,selected.y),1,White);
            Line(new Vector2(selected.x,selected.yMax),new Vector2(selected.xMax,selected.yMax),1,White);
            Line(new Vector2(selected.x,selected.y),new Vector2(selected.x,selected.yMax),1,White);
            Line(new Vector2(selected.xMax,selected.y),new Vector2(selected.xMax,selected.yMax),1,White);
            Label(new Rect(982,404,200,21),(span*field.CellSize).ToString("0.0")+" m patch / "+(field.CellSize*100).ToString("0")+" cm samples",small,Muted);
        }

        static Vector2 MapPoint(Rect rect, float x, float z, SimulatorConfig config)
        {
            float minX=-config.RinkWidth*.5f,maxX=config.RinkWidth*.5f+12;
            float minZ=-config.RinkLength*.5f-13,maxZ=config.RinkLength*.5f;
            return new Vector2(rect.x+(x-minX)/(maxX-minX)*rect.width,rect.y+(maxZ-z)/(maxZ-minZ)*rect.height);
        }

        void DrawRouteMap(IceResurfacerGame game,Rect map)
        {
            var guide=game.RouteGuide;if(guide.ActiveFit==null||guide.Progress==null)return;
            for(int i=1;i<guide.MapPointCount;i+=2)
            {
                var a=guide.MapPointAt(i-1);var b=guide.MapPointAt(Mathf.Min(i+1,guide.MapPointCount-1));
                var from=MapPoint(map,a.x,a.z,game.Sim.Config);var to=MapPoint(map,b.x,b.z,game.Sim.Config);
                Line(from,to,3.6f,new Color(.06f,0,.06f,.95f));Line(from,to,1.8f,guide.GpsColor);
            }
            int turn=guide.Progress.UpcomingTurn(IceRouteGuide.LookAheadMeters);
            if(turn>=0&&guide.ActiveFit.samples[turn].distance<=guide.LookEnd)
            {var p=guide.ActiveFit.samples[turn];var at=MapPoint(map,p.x,p.z,game.Sim.Config);Box(new Rect(at.x-2.5f,at.y-2.5f,5,5),Amber);}
            if(guide.MapPointCount>2)
            {var p=guide.MapPointAt(Mathf.Min(guide.MapPointCount-1,6));var q=guide.MapPointAt(Mathf.Min(guide.MapPointCount-1,8));Vector2 at=MapPoint(map,p.x,p.z,game.Sim.Config),forward=(MapPoint(map,q.x,q.z,game.Sim.Config)-at).normalized,side=new Vector2(-forward.y,forward.x);Line(at-forward*4+side*3,at,2,guide.GpsColor);Line(at-forward*4-side*3,at,2,guide.GpsColor);}
            DrawFootprintMap(map,game.Sim.Conditioner.X,game.Sim.Conditioner.Z,game.Sim.Conditioner.Heading,game.Sim.Config.ConditionerWidth,game.Sim.Config.ConditionerLength,0,game.Sim.Config,Mint);
            DrawFootprintMap(map,game.Sim.Conditioner.X,game.Sim.Conditioner.Z,game.Sim.Conditioner.Heading,game.Sim.Config.WaterDistributionWidth+2*game.Sim.Config.WaterFilmSpreadMeters,game.Sim.Config.FloodContactLength,game.Sim.Config.FloodContactFore,game.Sim.Config,new Color(.35f,.75f,1,.9f));
        }
        void DrawFootprintMap(Rect map,float x,float z,float heading,float width,float length,float fore,SimulatorConfig c,Color tint)
        {
            float sn=Mathf.Sin(heading),cs=Mathf.Cos(heading);Vector2 last=default;
            for(int i=0;i<5;i++)
            {int j=i%4;float side=(j==0||j==3?-.5f:.5f)*width,along=(j<2?-.5f:.5f)*length+fore;
                var p=MapPoint(map,x+cs*side+sn*along,z-sn*side+cs*along,c);if(i>0)Line(last,p,1,tint);last=p;}
        }
        void TrainingCue(IceResurfacerGame game)
        {
            var guide=game.RouteGuide;var fit=guide.ActiveFit;var progress=guide.Progress;if(fit==null||progress==null)return;
            Rect rect=new Rect(398,145,492,104);Box(rect,Navy);
            int turn=progress.UpcomingTurn(IceRouteGuide.LookAheadMeters);string instruction=turn<0?"FOLLOW CURRENT PASS":
                (fit.samples[turn].curvature>0?"CURVE RIGHT":"CURVE LEFT")+"  /  "+Mathf.Max(0,(float)(fit.samples[turn].distance-progress.Distance)).ToString("F0")+" m";
            if(progress.OffRoute)instruction="JOIN ACTIVE ROUTE  /  NUM 8";
            float pace=1.5f;int a=fit.IndexAt(progress.Distance),b=fit.IndexAt(Math.Min(fit.Length,progress.Distance+6));
            for(int i=a;i<=b;i++)pace=Mathf.Min(pace,fit.samples[i].safeSpeed);
            if(!progress.OffRoute)instruction+="  /  "+(pace<.12f?"CREEP":(pace*3.6f).ToString("F1")+" km/h");
            string source=guide.ActiveRouteName.Length>29?guide.ActiveRouteName.Substring(0,27)+"…":guide.ActiveRouteName;
            Label(new Rect(414,154,460,25),source+" / LEG "+(progress.Leg+1)+"  "+(fit.legs[progress.Leg].direction<0?"REVERSE":"FORWARD"),small,Muted);
            Label(new Rect(414,181,460,29),instruction,heading,turn>=0?Amber:Mint);
            var intended=fit.At(progress.Distance);float dx=game.Sim.Conditioner.X-intended.conditionerX,dz=game.Sim.Conditioner.Z-intended.conditionerZ;
            Label(new Rect(414,216,460,25),"Offset "+progress.LateralError.ToString("F2")+" m  /  conditioner "+Mathf.Sqrt(dx*dx+dz*dz).ToString("F2")+" m  /  advisory",small,Muted);
        }

        void CompactRack(IceResurfacerGame game)
        {
            if(Inspecting(game))return;
            var c=game.Sim.Controls;
            Box(new Rect(1210,378,362,78),Navy);
            if(Button(ControlRect("Console"),"OPEN CONSOLE [TAB]",Slate)){Compact=false;game.ReleaseControls();}
            Label(new Rect(1228,426,326,23),"AUGERS "+(c.VerticalAuger?"V ON":"V OFF")+" / "+(c.HorizontalAuger?"H ON":"H OFF")+"   FLOOD "+(c.FloodFlow*100).ToString("0")+"%",small,Muted);
        }
        void StatusStrip(IceResurfacerGame game)
        {
            Box(new Rect(398,849,806,43),Navy);
            string line=game.RouteGuide!=null&&game.RouteGuide.Recorder.Recording?"RECORDING / "+game.RouteGuide.Recorder.Data.distanceMeters.ToString("F1")+" m  /  NUM 9 SAVE":
                game.RouteGuide!=null&&game.RouteGuide.Visible?"NUM 7 GPS / "+game.RouteGuide.Phase:
                "Q edge / E controls / C chase / L Shift rear | NUM 7 GPS";
            Label(new Rect(414,860,594,23),line,small,Muted);
            if(Button(ControlRect("RouteRecorder"),"ROUTES NUM 8",Slate))game.RouteGuide?.ToggleRecordingPanel();
            if(Button(ControlRect("History"),"MESSAGES",Slate))HistoryVisible=!HistoryVisible;
            if(Button(ControlRect("Guide"),"HELP F1",Slate))game.ToggleHelp();
            if(Button(ControlRect("Pause"),game.Paused?"RESUME":"PAUSE",Slate))game.TogglePause();
        }

        void ServicePanel(IceResurfacerGame game)
        {
            Box(new Rect(220,690,1160,191),Navy);
            Label(new Rect(242,706,1116,45),"WATER SERVICE  /  "+game.WaterService.Status,heading,Mint);
            Label(new Rect(242,757,1116,30),game.WaterService.Prompt,text,White);
            Label(new Rect(242,794,1116,38),game.Notice,small,Amber);
            Label(new Rect(242,839,1116,25),"WASD walk   Mouse look   E interact   F board near the steps   ESC releases cursor",small,Muted);
        }
        void DriveWarning(IceResurfacerGame game)
        {
            string reason=game.Sim.DriveInhibitReason;
            if(Mathf.Abs(game.RawThrottle)<.01f||game.Sim.OnFoot||string.IsNullOrEmpty(reason))return;
            Box(new Rect(398,714,806,58),new Color(.26f,.10f,.065f,.98f));
            Label(new Rect(414,723,774,42),reason,small,Amber);
        }
        void Diagnostics(IceResurfacerGame game)
        {
            var s=game.Sim;var c=s.Controls;
            if(Compact)
            {
                // Leave the middle of the scene clear for the actual F3 outlines.
                // TAB restores the full machinery/performance diagnostic panel.
                var compactHit=s.LastMovementContact;
                Box(new Rect(28,132,492,242),Modal);
                Label(new Rect(44,144,460,24),"CONTACT OVERLAY  [F3]",heading,Mint);
                string detail="Speed "+s.Vehicle.Speed.ToString("0.000")+" m/s  /  command "+c.Throttle.ToString("0.00")+"  /  accepted "+s.EffectiveDriveDemand.ToString("0.00")+
                    "\n"+(compactHit.Obstacle==MovementObstacle.None?"No rejected movement this session.":compactHit.Reason+(s.BoardContact?" / contact now":" / last contact"))+
                    "\nRejected x/z "+compactHit.VehicleX.ToString("0.00")+", "+compactHit.VehicleZ.ToString("0.00")+"  heading "+(compactHit.Heading*Mathf.Rad2Deg).ToString("0.0")+
                    "\nPoint x/y/z "+compactHit.PointX.ToString("0.00")+", "+compactHit.PointY.ToString("0.00")+", "+compactHit.PointZ.ToString("0.00")+
                    "\nNormal x/z "+compactHit.NormalX.ToString("0.00")+", "+compactHit.NormalZ.ToString("0.00")+
                    "\nGreen: body  /  red: rejected pose"+
                    "\nCyan: traversable grate  /  amber: snow receiver"+
                    "\nImpact normal "+s.ContactNormalSpeed.ToString("0.00")+" m/s / severity "+s.ContactSeverity.ToString("0.00");
                Label(new Rect(44,180,460,180),detail,small,White);
                return;
            }
            Box(new Rect(545,228,755,390),Modal);
            Label(new Rect(565,241,715,26),"MACHINE DIAGNOSTICS  [F3]  /  SURFACE MASK [F2]",heading,Mint);
            Label(new Rect(565,278,715,52),"Machine Load estimates mechanical demand, not engine power. Cutting, augers and packed snow add demand; higher load slows acceleration and changes the drive sound.",small,Muted);
            string reason=!game.Started?"Start a run":game.Paused?"Paused":game.HelpVisible?"Guide open":s.Finished?"Run finished":s.DriveInhibitReason;
            string body="Keyboard W/S: "+game.RawThrottle.ToString("0.00")+"   Pedal: "+c.Throttle.ToString("0.00")+"   Accepted: "+s.EffectiveDriveDemand.ToString("0.00")+
                "\nDirection: "+(c.Throttle>0?"forward":c.Throttle<0?"reverse":"neutral")+"   Velocity: "+s.Vehicle.Speed.ToString("0.000")+" m/s   Brake: "+(c.Brake?"CTRL held":"released")+
                "\nTank: "+(s.TankLift*100).ToString("0.0")+"%   Hose: "+s.HoseState+"   Load: "+s.MachineLoad.ToString("0.00")+
                "\nBoard/wall contact: "+s.BoardContact+"   Roller: "+s.GuideRollerContact+"   Normal "+s.ContactNormalSpeed.ToString("0.00")+" m/s / severity "+s.ContactSeverity.ToString("0.00")+
                "\nFocus: "+game.HasFocus+"   RMB: "+game.Looking+"   HUD capture: "+(springControl!=0)+
                "\nGrip: "+s.TractionGrip.ToString("0.00")+"   Slip: "+s.TractionSlip.ToString("0.00")+"   Lateral: "+s.LateralVelocity.ToString("0.00")+" m/s"+
                "\nDemand: idle 13% + cutting "+(s.LoadCutting*100).ToString("0")+"% + augers/snow "+(s.LoadConveyors*100).ToString("0")+"% + congestion "+(s.LoadCongestion*100).ToString("0")+"%"+
                "\nLoad response: "+(Mathf.Max(.2f,1-s.MachineLoad*.35f)*100).ToString("0")+"% acceleration; above 100% demand warns of overload."+
                "\n"+game.Performance.Details+
                "\nTravel: "+(string.IsNullOrEmpty(reason)?"permitted":reason);
            Label(new Rect(565,337,715,269),body,small,White);
            var hit=s.LastMovementContact;
            Box(new Rect(28,520,492,100),Modal);
            Label(new Rect(44,529,460,22),"CONTACT FOOTPRINTS  /  WORLD OVERLAY",heading,Mint);
            string contact=hit.Obstacle==MovementObstacle.None?"No rejected movement recorded this session.":
                hit.Reason+" / "+(s.BoardContact?"contact now":"last contact")+
                "\nRejected x/z "+hit.VehicleX.ToString("0.00")+", "+hit.VehicleZ.ToString("0.00")+"  heading "+(hit.Heading*Mathf.Rad2Deg).ToString("0.0")+
                "\nPoint x/y/z "+hit.PointX.ToString("0.00")+", "+hit.PointY.ToString("0.00")+", "+hit.PointZ.ToString("0.00")+"  normal "+hit.NormalX.ToString("0.00")+", "+hit.NormalZ.ToString("0.00");
            Label(new Rect(44,554,460,60),contact,small,White);
        }

        void Guidance(IceResurfacerGame game)
        {
            var s=game.Sim;
            if(messageSession!=s){messageSession=s;lastNotice="";messageHistory.Clear();HistoryVisible=false;messageScroll=Vector2.zero;}
            if(string.IsNullOrEmpty(game.Notice))lastNotice="";
            if(!string.IsNullOrEmpty(game.Notice)&&game.Notice!=lastNotice)
            {
                lastNotice=game.Notice;noticeAt=Time.unscaledTime;
                if(messageHistory.Count==32)messageHistory.RemoveAt(0);
                messageHistory.Add(Duration(s.Time)+"  "+lastNotice);
            }
            bool serious=s.ConveyorJammed||s.SnowTankM3>=s.Config.SnowCapacityM3*.995f||s.PumpDamage>.05f;
            string alert=s.ConveyorJammed?"AUGER JAM — retract blade; stop conveyors, clear and reset [J]":
                s.SnowTankM3>=s.Config.SnowCapacityM3*.995f?"SNOW TANK FULL — retract blade and unload":
                s.PumpDamage>.05f?"WASH PUMP WEAR — inspect pump status [F3]":"";
            float age=Time.unscaledTime-noticeAt;
            if(serious||MessagesVisible&&!string.IsNullOrEmpty(game.Notice)&&age<8)
            {
                string message=serious?alert:game.Notice;
                if(message.Length>142)message=message.Substring(0,139)+"...";
                Color tint=GUI.color;GUI.color=new Color(1,1,1,serious?1:Mathf.Clamp01(8-age));
                Box(new Rect(398,782,806,58),Navy);
                Label(new Rect(414,791,774,42),message,small,serious?Amber:White);GUI.color=tint;
            }
            if(!HistoryVisible)return;
            Box(new Rect(520,160,650,570),Modal);
            Label(new Rect(540,179,440,28),"OPERATOR MESSAGES",heading,Mint);
            if(Button(new Rect(1050,174,100,34),"CLOSE",Slate))HistoryVisible=false;
            if(Button(new Rect(540,220,610,35),MessagesVisible?"ORDINARY NOTIFICATIONS: ON":"ORDINARY NOTIFICATIONS: OFF",Slate))MessagesVisible=!MessagesVisible;
            Label(new Rect(540,268,610,48),s.GuidedTraining?NextAction(s):"Free simulation. Equipment consequences and scoring follow the actual surface. F1 opens the operating guide.",small,Muted);
            int count=messageHistory.Count;
            if(count==0)Label(new Rect(540,328,610,40),"No messages this session.",small,Muted);
            messageScroll=GUI.BeginScrollView(new Rect(540,328,610,380),messageScroll,new Rect(0,0,586,Mathf.Max(379,count*77)));
            for(int i=0;i<count;i++)Label(new Rect(0,i*77,584,72),messageHistory[messageHistory.Count-1-i],small,White);
            GUI.EndScrollView();
        }

        static string SnowStatus(Simulation s)
        {
            var state=s.SnowCondition;
            if((state&SnowTransportCondition.Jammed)!=0)return "AUGER JAM / Snow is backing up. Retract the blade; stop both augers and clear the obstruction before J resets.";
            if((state&SnowTransportCondition.RaisedBin)!=0&&s.VerticalSpillRate>0)return "BIN RAISED / The fixed upper chute is missing the tank. Snow lands beside the machine.";
            if((state&SnowTransportCondition.TankFull)!=0)return "TANK FULL / Snow backs up through the upper chute and conditioner. Retract the blade and unload.";
            if((state&SnowTransportCondition.HorizontalStopped)!=0)return "HORIZONTAL AUGER OFF / Trough "+(s.ConditionerSnowFill*100).ToString("0")+"%. Snow accumulates, then escapes both conditioner sides.";
            if((state&SnowTransportCondition.VerticalStopped)!=0)return "VERTICAL AUGER OFF / The transfer throat is backing up. The horizontal auger cannot deliver snow to the tank.";
            return "CONVEYOR CONGESTION / Snow arrival exceeds transport. Reduce cutting depth or ground speed; deposited snow can be collected again.";
        }

        static string NextAction(Simulation s)
        {
            var c = s.Controls;
            if (s.TankLift > .8f && s.SnowTankM3 > .0001f) return s.DumpSnowRate>0?"UNLOADING  /  Snow is falling into the pit. Hold position.":"RESIDUAL SNOW  /  Briefly lower a few degrees, then raise to release clumps. Repeat, then lower fully.";
            if (s.TankRaised) return "STOW TANK  /  Hold the dump lever LOWER until the tank is fully down, then release.";
            if (s.InServiceBay && !s.HasEnteredRink) return "ENTER THE RINK  /  S backs through the southeast gate; hold C for chase view. Keep the conditioner raised until fully on ice.";
            if (s.InServiceBay && s.SnowTankM3 > .005f && !s.InDumpZone) return "APPROACH THE DUMP PIT  /  Drive toward the far end, centred and facing the pit. Stop at the yellow line.";
            if (s.InDumpZone && s.SnowTankM3 > .005f && Mathf.Abs(s.Vehicle.Speed) > .035f) return "STOP TO DUMP  /  Hold CTRL until stationary, then stow equipment and raise the snow-tank lever.";
            if (s.InDumpZone && s.SnowTankM3 > .005f) return "EMPTY THE TANK  /  Hold RAISE. To release residue, briefly lower and raise again. Hold LOWER to stow.";
            if (s.InServiceBay && s.EquipmentSafe) return "READY TO RETURN  /  Finish this run, or refill water and continue working on missed strips.";
            if (s.InServiceBay) return "STOW EQUIPMENT  /  Blade out, augers and water off, brush stowed and conditioner fully raised.";
            s.Layout.ToLocal(s.Vehicle.X,s.Vehicle.Z,out float gateSide,out float gateOutward);
            if (s.HasEnteredRink && gateOutward > -8 && gateOutward < 1 && Mathf.Abs(gateSide) < s.Config.GateHalfWidth + 1 && Mathf.Cos(s.Vehicle.Heading-s.Layout.OutwardHeading) > .5f && c.BladeDepthMm < .02f && c.FloodFlow < .02f)
                return "EXIT SEQUENCE  /  Stop on ice and clear conveyors. Shut augers and water, stow brush, then lift fully before the threshold.";
            if (c.ConditionerTarget > .05f && s.Conditioner.Lift > .25f) return "LOWER CONDITIONER  /  Blade out. Creep forward, lower fully, then engage both augers.";
            if (!c.HorizontalAuger || !c.VerticalAuger) return "CONVEYORS  /  Engage both augers before cutting. Let them clear before the exit sequence.";
            if (s.ConveyorJammed) return "AUGER JAM  /  Stop, blade out, BOTH augers OFF. Empty the tank; J resets after safe inspection.";
            if (s.SnowTankM3 > s.Config.SnowCapacityM3 * .85f) return "TANK NEAR CAPACITY  /  Retract blade, clear conveyors and return to the service bay.";
            if (c.BladeDepthMm < .02f) return "SET THE CUT  /  Turn the blade wheel clockwise. Start near 0.35 mm; use a lighter cut in creases.";
            if (c.WashValve > .02f && !c.WashPump && s.WashPrimeSeconds < 10) return "PRIMING WASH  /  " + Mathf.CeilToInt(10 - s.WashPrimeSeconds) + " s remaining. Continue forward before starting the pickup pump.";
            if (c.WashValve > .02f && !c.WashPump) return "WASH PRIMED  /  Enable the pickup pump to recover wash water through the filter.";
            if (c.FloodFlow < .02f) return "APPLY FINISH WATER  /  Open the flood lever while moving. Reduce flow through overlaps and turns.";
            return "WORK THE SHEET  /  Follow the wet edge with the outer tire. Hover the survey to inspect unfinished cells. F1 opens the guide.";
        }

        void Rack(IceResurfacerGame game)
        {
            if(Inspecting(game))return;
            var s=game.Sim;var c=s.Controls;
            Box(new Rect(1210,378,362,462),Navy);
            if(Button(ControlRect("Console"),"CLOSE CONSOLE [TAB]",Slate)){Compact=true;game.ReleaseControls();}
            GUI.enabled=game.Started&&!game.Paused&&!s.Finished&&!game.Looking&&!game.HelpVisible;
            CenterLabel(new Rect(1222,421,80,23),"VERT [3]",small,Muted);
            CenterLabel(new Rect(1304,421,80,23),"COND [1]",small,Muted);
            CenterLabel(new Rect(1386,421,80,23),"HORIZ [2]",small,Muted);
            CenterLabel(new Rect(1468,421,80,23),"DUMP [7]",small,Muted);
            c.VerticalAuger=MaintainedLever(506,ControlRect("VerticalAuger"),c.VerticalAuger,"Vertical auger: click to latch ON or OFF. [3]");
            float conditioner=HydraulicLever(501,ControlRect("Conditioner"),c.ConditionerHydraulic,"Hold top RAISE / bottom LOWER; release holds actual height. 1 lowers; Right Shift+1 raises.");
            if(conditioner!=c.ConditionerHydraulic)s.SetConditionerHydraulic(conditioner);
            c.HorizontalAuger=MaintainedLever(507,ControlRect("HorizontalAuger"),c.HorizontalAuger,"Horizontal auger: click to latch ON or OFF. [2]");
            float tank=HydraulicLever(505,ControlRect("TankLift"),c.TankHydraulic,"Hold top RAISE / bottom LOWER; release holds attained tank position. 7 raises; Right Shift+7 lowers.");
            if(tank!=c.TankHydraulic)s.SetTankHydraulic(tank);
            CenterLabel(new Rect(1222,543,80,22),c.VerticalAuger?"ON":"OFF",small,c.VerticalAuger?Mint:Muted);
            CenterLabel(new Rect(1304,543,80,22),(s.Conditioner.Lift*100).ToString("0")+"% UP",small,Mint);
            CenterLabel(new Rect(1386,543,80,22),c.HorizontalAuger?"ON":"OFF",small,c.HorizontalAuger?Mint:Muted);
            CenterLabel(new Rect(1468,543,80,22),(s.TankLift*100).ToString("0")+"% UP",small,Mint);
            Box(new Rect(1228,569,326,1),Slate);
            CenterLabel(new Rect(1220,574,110,23),"FLOOD",heading,Muted);
            CenterLabel(new Rect(1334,574,110,23),"WASH",heading,Muted);
            CenterLabel(new Rect(1454,574,94,23),"BLADE",heading,Muted);
            c.FloodFlow=WaterHandle(504,ControlRect("Flood"),c.FloodFlow,"Flood water: drag the L handle through its quarter turn. [ / ] adjust. Close before stopping.");
            c.WashValve=WaterHandle(503,ControlRect("WashValve"),c.WashValve,"Wash valve: independent quarter turn. Right Shift + [ / ] adjust. Prime 10 seconds before pickup.");
            c.BladeDepthMm=Dial(502,ControlRect("Blade"),c.BladeDepthMm,s.Config.MaxBladeDepthMm,"Blade screw: 3.5 turns, clockwise deeper. Comma / period fine adjustment.",s.Config.BladeWheelTurns*360);
            CenterLabel(new Rect(1220,680,110,23),(c.FloodFlow*100).ToString("0")+"%",small,Mint);
            CenterLabel(new Rect(1334,680,110,23),(c.WashValve*100).ToString("0")+"%",small,Mint);
            CenterLabel(new Rect(1444,680,112,23),c.BladeDepthMm.ToString("0.00")+" mm",small,Mint);
            c.BoardBrush=Toggle(ControlRect("BoardBrush"),"BRUSH [4]",c.BoardBrush,"Board brush follows its actual left-side contact patch.");
            c.WashPump=Toggle(ControlRect("WashPump"),"PUMP [5]",c.WashPump,"Pickup recovers liquid excess. Prime wash for 10 seconds; running dry causes wear.");
            if(Button(ControlRect("SnowBreaker"),"BREAKER [6]",Slate))game.OperateSnowBreaker();
            if(Button(ControlRect("Service"),"REFILL HELP",Slate))game.Service();
            if(Button(ControlRect("Finish"),"FINISH RUN",new Color(.14f,.37f,.31f)))game.Finish();
            if(Button(ControlRect("NewSession"),"NEW SESSION",Slate))game.NewSession(false,false);
            GUI.enabled=true;
        }
        bool MaintainedLever(int hint,Rect rect,bool value,string help)
        {
            if(GUI.enabled&&rect.Contains(Event.current.mousePosition))hoverHint=help;
            Box(rect,Inset);Box(new Rect(rect.center.x-3,rect.y+12,6,rect.height-24),Slate);
            float y=value?rect.y+22:rect.yMax-22;
            Box(new Rect(rect.center.x-21,y-8,42,16),value?Mint:Muted);
            return GUI.Button(rect,GUIContent.none,GUIStyle.none)?!value:value;
        }

        float HydraulicLever(int hint,Rect rect,float value,string help)
        {
            int id=GUIUtility.GetControlID(hint,FocusType.Passive);
            Event e=Event.current;Rect hit=rect;
            if(GUI.enabled&&hit.Contains(e.mousePosition))hoverHint=help;
            if(GUI.enabled&&e.type==EventType.MouseDown&&e.button==0&&hit.Contains(e.mousePosition))
            { GUIUtility.hotControl=id;pointerCaptured=true;springControl=hint;value=HydraulicDemand(hit,e.mousePosition);e.Use(); }
            if(GUIUtility.hotControl==id)
            {
                if(GUI.enabled&&e.type==EventType.MouseDrag){value=HydraulicDemand(hit,e.mousePosition);e.Use();}
                if(e.type==EventType.MouseUp||!GUI.enabled)
                {value=0;springControl=0;pointerCaptured=false;GUIUtility.hotControl=0;if(e.type==EventType.MouseUp)e.Use();}
            }
            Box(rect,Inset);Box(new Rect(rect.center.x-3,rect.y+12,6,rect.height-24),Slate);
            float y=rect.center.y-value*(rect.height*.30f);
            Box(new Rect(rect.center.x-21,y-8,42,16),springControl==hint?Mint:Amber);
            CenterLabel(new Rect(rect.x,rect.y-1,rect.width,20),"+",small,Muted);
            CenterLabel(new Rect(rect.x,rect.yMax-20,rect.width,20),"-",small,Muted);
            return value;
        }
        static float HydraulicDemand(Rect r,Vector2 p) { return p.y<r.center.y-12?1:p.y>r.center.y+12?-1:0; }

        float WaterHandle(int hint,Rect rect,float value,string help)
        {
            int id=GUIUtility.GetControlID(hint,FocusType.Passive);Event e=Event.current;
            Vector2 centre=rect.center;float angle=Mathf.Atan2(e.mousePosition.y-centre.y,e.mousePosition.x-centre.x)*Mathf.Rad2Deg;
            if(GUI.enabled&&rect.Contains(e.mousePosition))
            {
                hoverHint=help;
                if(e.type==EventType.MouseDown&&e.button==0){GUIUtility.hotControl=id;pointerCaptured=true;dialLastAngle=angle;e.Use();}
                if(e.type==EventType.ScrollWheel){value=Mathf.Clamp01(value-e.delta.y*.025f);e.Use();}
            }
            if(GUIUtility.hotControl==id)
            {
                if(GUI.enabled&&e.type==EventType.MouseDrag)
                {if(Vector2.Distance(e.mousePosition,centre)>12)value=Mathf.Clamp01(value-Mathf.DeltaAngle(dialLastAngle,angle)/90);dialLastAngle=angle;e.Use();}
                if(e.type==EventType.MouseUp||!GUI.enabled){GUIUtility.hotControl=0;pointerCaptured=false;if(e.type==EventType.MouseUp)e.Use();}
            }
            // The same quarter turn as the physical stem, with a right-angle grip.
            Texture(new Rect(centre.x-14,centre.y-14,28,28),ring,Muted);
            for(int i=0;i<9;i++)
            {
                float a=(180-i*10)*Mathf.Deg2Rad,b=(170-i*10)*Mathf.Deg2Rad;
                Line(centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*39,centre+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*39,2,Slate);
            }
            float turn=(180-value*90)*Mathf.Deg2Rad;
            Vector2 arm=new Vector2(Mathf.Cos(turn),Mathf.Sin(turn)),tip=centre+arm*31;
            Line(centre,tip,7,Muted);Line(tip,tip+new Vector2(-arm.y,arm.x)*16,10,GUIUtility.hotControl==id?Mint:Amber);


            return value;
        }

        float Dial(int hint, Rect rect, float value, float maximum, string help,float travelDegrees=300)
        {
            int id = GUIUtility.GetControlID(hint, FocusType.Passive);
            Event e = Event.current;
            Vector2 center = rect.center;
            float angle = Mathf.Atan2(e.mousePosition.y - center.y, e.mousePosition.x - center.x) * Mathf.Rad2Deg;
            if (GUI.enabled && rect.Contains(e.mousePosition))
            {
                hoverHint = help;
                if (e.type == EventType.ScrollWheel) { value = Mathf.Clamp(value - e.delta.y * maximum * 7.5f/travelDegrees, 0, maximum); e.Use(); }
                if (e.type == EventType.MouseDown && e.button == 0) { GUIUtility.hotControl = id; pointerCaptured=true; dialLastAngle = angle; e.Use(); }
            }
            if (GUIUtility.hotControl == id)
            {
                if (GUI.enabled && e.type == EventType.MouseDrag)
                {
                    if (Vector2.Distance(e.mousePosition, center) > 12)
                        value = Mathf.Clamp(value + Mathf.DeltaAngle(dialLastAngle, angle) / travelDegrees * maximum, 0, maximum);
                    dialLastAngle = angle; e.Use();
                }
                if (e.type == EventType.MouseUp || !GUI.enabled) { GUIUtility.hotControl = 0; pointerCaptured=false; if (e.type == EventType.MouseUp) e.Use(); }
            }
            Texture(rect, disc, Inset);
            Texture(rect, ring, GUI.enabled ? Amber : Muted);
            float rotation = value / maximum * travelDegrees - 120;
            for (int i = 0; i < 3; i++)
            {
                float a = (rotation + i * 120) * Mathf.Deg2Rad;
                Line(center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rect.width * .40f, 5, GUI.enabled ? Amber : Muted);
            }
            Texture(new Rect(center.x - 12, center.y - 12, 24, 24), disc, Slate);
            float pointer = rotation * Mathf.Deg2Rad;
            Vector2 tip = center + new Vector2(Mathf.Cos(pointer), Mathf.Sin(pointer)) * rect.width * .38f;
            Texture(new Rect(tip.x - 5, tip.y - 5, 10, 10), disc, White);
            return value;
        }

        bool Toggle(Rect rect, string label, bool value, string help)
        {
            if (GUI.enabled && rect.Contains(Event.current.mousePosition)) hoverHint = help;
            Box(rect, value ? new Color(.12f, .31f, .27f) : Inset);
            Box(new Rect(rect.x + 7, rect.y + 10, 5, rect.height - 20), value ? Mint : Slate);
            var previousWrap = button.wordWrap;
            button.wordWrap = true;
            Rect contentRect = new Rect(rect.x + 13, rect.y, rect.width - 15, rect.height);
            if (AuditLayout && Event.current.type == EventType.Repaint)
            {
                float required = button.CalcHeight(new GUIContent(label), contentRect.width);
                if (required > contentRect.height + 1f)
                    LayoutIssues.Add(label + " switch requires " + required.ToString("0.0") + " px, has " + contentRect.height.ToString("0.0") + " px");
            }
            bool clicked = GUI.Button(contentRect, label, button);
            button.wordWrap = previousWrap;
            return clicked ? !value : value;
        }

        void Welcome(IceResurfacerGame game)
        {
            Box(new Rect(360, 165, 835, 440), Modal);
            Box(new Rect(360, 165, 6, 440), Amber);
            Label(new Rect(396, 216, 730, 25), "NORTHLINE  /  BETWEEN SESSIONS", heading, Mint);
            Label(new Rect(394, 256, 740, 47), "A clean sheet is in your hands.", title, White);
            Label(new Rect(396, 319, 740, 76), "Free Simulation lets you operate every control, step off anywhere and deal with the consequences. Choose scored or unscored. Training assistance adds explicit safety restrictions.", text, Muted);
            Label(new Rect(396, 405, 740, 34), "WASD drive  /  RMB look  /  HOLD Q edge, E controls, C chase  /  X forward", heading, White);
            if (Button(new Rect(397, 457, 365, 49), "FREE SIMULATION / SCORED", new Color(.18f, .40f, .33f))) game.NewSession(false);
            if (Button(new Rect(780, 457, 365, 49), "FREE SIMULATION / PRACTICE", Slate)) game.NewSession(true);
            if (Button(new Rect(397, 522, 365, 49), "GUIDED TRAINING / ASSISTED", Slate)) game.NewSession(true,true,SimulationMode.GuidedTraining);
            if (Button(new Rect(780, 522, 365, 49), "READ THE OPERATOR GUIDE", Slate)) game.ToggleHelp();
        }

        void Pause(IceResurfacerGame game)
        {
            Box(new Rect(430, 210, 700, 420), Modal);
            Label(new Rect(468, 264, 624, 44), "Operator break", title, White);
            Label(new Rect(470, 322, 618, 45), "The machine and ice simulation are paused.", text, Muted);
            if (Button(new Rect(470, 391, 180, 49), "RESUME", new Color(.18f, .40f, .33f))) game.TogglePause();
            if (Button(new Rect(670, 391, 190, 49), "OPERATOR GUIDE", Slate)) game.ToggleHelp();
            if (Button(new Rect(870, 391, 220, 49), "NEW SESSION", Slate)) game.NewSession(false, false);
            Label(new Rect(470, 460, 620, 24), "Seated eye height adjustment: "+(game.operatorEyeHeight*100).ToString("+0;-0;0")+" cm", small, Muted);
            game.operatorEyeHeight=GUI.HorizontalSlider(new Rect(470,493,620,18),game.operatorEyeHeight,-.08f,.12f);
            if(Button(new Rect(470,512,620,45),"REFILL PRACTICE  /  START WITH PARTLY EMPTY TANKS",Slate))game.StartServicePractice();
            Label(new Rect(470,581,620,24),"ESC resumes. Free Simulation preserves your machinery settings.",small,Muted);
        }

        void Help(IceResurfacerGame game)
        {
            Box(new Rect(180, 125, 1240, 555), Modal);
            Box(new Rect(180, 125, 1240, 3), Amber);
            Label(new Rect(214, 150, 950, 44), "Operator's quick guide", title, White);
            if (Button(new Rect(1254, 151, 132, 36), "CLOSE  F1", Slate)) game.ToggleHelp();
            Label(new Rect(216, 218, 550, 25), "01  PREPARE & RESURFACE", heading, Mint);
            Label(new Rect(216, 255, 550, 280), "Back onto ice with S; hold C for chase view. Lower the conditioner, run both augers and start near 0.35 mm. Open wash, prime 10 seconds, then start pickup.\n\nFollow the previous wet edge with the outer tire. Hover the survey for exact cells; F2 shows treatment. Pickup recovers liquid; deep grooves need more cutting.\n\nMachine Load: 13% idle plus cutting, augers, snow and congestion. Higher load slows acceleration. Click the meter or F3 for the breakdown.", text, White);
            Box(new Rect(795, 220, 1, 314), Slate);
            Label(new Rect(826, 218, 550, 25), "02  RETURN & EMPTY", heading, Mint);
            Label(new Rect(826, 255, 550, 280), "Stow equipment for safe travel through the threshold. Dump toward the pit at the rear of the service room; snow lands where the outlet sends it. Brief lower / raise movements shake out residue.\n\nF steps off. Mouse looks freely on foot; WASD walks. E takes, connects and returns the hose and operates supply. The wall station is right of the parked machine.\n\nClose supply, uncouple, return the nozzle and F to board. Driving beyond hose reach breaks the coupling and spills water.", text, White);
            Label(new Rect(216, 554, 1170, 24), "1 / Right Shift+1 lower / raise conditioner   2/3 augers   4 brush   5 pump   6 plunger   7 / Right Shift+7 raise / lower tank", small, Muted);
            Label(new Rect(216, 581, 1170, 24), "[ / ] flood   Right Shift+[ / ] wash   , / . fine blade   J jam reset   F mount / dismount   TAB compact", small, Muted);
            Label(new Rect(216, 608, 1170, 24), "HOLD Q edge / E controls / C chase / LEFT SHIFT rear; release restores view.  X forward  R stand  SPACE rise  RMB look", small, Muted);
            Label(new Rect(216, 635, 1170, 24), "W/S drive   A/D steer   R stand   RMB look   CTRL brake   NUM 7 GPS / 8 routes / 9 record & save   ESC pause", small, Muted);
        }

        void Results(IceResurfacerGame game)
        {
            var r = game.Report; var s = game.Sim;
            Box(new Rect(365, 162, 835, 477), Modal);
            Label(new Rect(399, 186, 755, 24), r.Practice ? "PRACTICE REVIEW  /  NO SCORE RECORDED" : r.Complete ? "FULL RUN COMPLETE" : "SESSION COMPLETE  /  PARTIAL SHEET", heading, Mint);
            Label(new Rect(397, 223, 530, 45), r.Practice ? "Your surface, reviewed." : r.Score.ToString("0.0") + "  /  100", title, White);
            Label(new Rect(970, 232, 190, 24), Duration(r.ElapsedSeconds), heading, Muted);
            ReportLine(399,294,"COVERAGE",(r.Coverage*100).ToString("0.0")+"%",r.Practice?"":r.CoveragePoints.ToString("0.0")+" / 40");
            ReportLine(399,329,"SURFACE QUALITY",(r.FinishQuality*100).ToString("0.0")+"%",r.Practice?"":r.FinishPoints.ToString("0.0")+" / 30");
            ReportLine(399,364,"WATER DISTRIBUTION",(r.WaterDistribution*100).ToString("0.0")+"%",r.Practice?"":r.WaterPoints.ToString("0.0")+" / 10");
            ReportLine(399,399,"MISSED PATCHES",(r.MissedFraction*100).ToString("0.0")+"%","");
            ReportLine(790,294,"EXCESS WATER",(r.OverwaterFraction*100).ToString("0.0")+"%",r.Practice?"":r.OverwaterPoints.ToString("0.0")+" / 10");
            ReportLine(790,329,"WATER EFFICIENCY",(r.OperationalEfficiency*100).ToString("0.0")+"%",r.Practice?"":r.EfficiencyPoints.ToString("0.0")+" / 5");
            ReportLine(790,364,"TANK / SAFE RETURN",r.TankDumps+" dumps",r.Practice?"":r.HandlingPoints.ToString("0.0")+" / 5");
            ReportLine(790,399,"EQUIPMENT MISUSE",r.Mistakes+" faults",r.Practice?"":"-"+r.MisusePenalty.ToString("0.0"));
            Label(new Rect(400,445,750,54),"Shaving mask excludes the outer 15.24 cm. Water and finish still include the whole sheet. Flood reached "+(r.WaterCoverage*100).ToString("0.0")+"%; shaved "+(r.ShavingCoverage*100).ToString("0.0")+"% of reachable ice.",small,Muted);
            Label(new Rect(400,505,748,32),r.Complete?"Complete sheet. Inspect the survey for finish differences.":"Partial sheet: completion requires 95% combined treatment of reachable ice.",small,Muted);
            if(Button(new Rect(401,564,229,46),"NEW SCORED RUN",new Color(.18f,.40f,.33f)))game.NewSession(false);
            if(Button(new Rect(651,564,229,46),"NEW PRACTICE",Slate))game.NewSession(true);
            if(Button(new Rect(902,564,261,46),"OPERATOR GUIDE",Slate))game.ToggleHelp();
        }

        void ReportLine(float x, float y, string label, string value, string points)
        {
            Label(new Rect(x, y, 182, 24), label, small, Muted);
            Label(new Rect(x + 184, y - 1, 110, 24), value, heading, White);
            Label(new Rect(x + 302, y, 82, 24), points, small, Mint);
        }
        static string Duration(float seconds) { return ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00"); }
        void Meter(Rect rect, string caption, float value, string reading, Color color)
        {
            Label(new Rect(rect.x, rect.y, rect.width, 19), caption, small, Muted);
            Label(new Rect(rect.x, rect.y + 20, rect.width, 21), reading, heading, color);
            Box(new Rect(rect.x, rect.y + 47, rect.width, 4), Slate);
            Box(new Rect(rect.x, rect.y + 47, rect.width * Mathf.Clamp01(value), 4), color);
        }
        bool Button(Rect rect, string caption, Color color)
        {
            if (AuditLayout && Event.current.type == EventType.Repaint)
            {
                Vector2 required = button.CalcSize(new GUIContent(caption));
                if (required.x > rect.width + 1f || required.y > rect.height + 1f)
                    LayoutIssues.Add(caption + " button requires " + required + ", has " + rect.size);
            }
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Box(rect, hover ? Color.Lerp(color, White, .13f) : color);
            return GUI.Button(rect, caption, button);
        }
        void Label(Rect rect, string caption, GUIStyle style, Color color)
        {
            if (AuditLayout && Event.current.type == EventType.Repaint)
            {
                float required = style.CalcHeight(new GUIContent(caption), rect.width);
                if (required > rect.height + 1f)
                    LayoutIssues.Add(caption + " requires " + required.ToString("0.0") + " px, has " + rect.height.ToString("0.0") + " px");
            }
            Color previous = style.normal.textColor; style.normal.textColor = color;
            GUI.Label(rect, caption, style); style.normal.textColor = previous;
        }
        void CenterLabel(Rect rect, string caption, GUIStyle style, Color color)
        {
            var previous = style.alignment; style.alignment = TextAnchor.UpperCenter;
            Label(rect, caption, style, color); style.alignment = previous;
        }
        static void Box(Rect rect, Color color) { Texture(rect, Texture2D.whiteTexture, color); }
        static void Texture(Rect rect, Texture texture, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, texture); GUI.color = previous;
        }
        static void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            Matrix4x4 previous = GUI.matrix;
            float degrees = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3(a.x, a.y, 0), Quaternion.Euler(0, 0, degrees), Vector3.one);
            Box(new Rect(0, -width * .5f, Vector2.Distance(a, b), width), color);
            GUI.matrix = previous;
        }
        public void Dispose()
        {
            if (disc != null) UnityEngine.Object.Destroy(disc);
            if (ring != null) UnityEngine.Object.Destroy(ring);
        }
    }
}
