using System;
using UnityEngine;
using UnityEngine.InputSystem;
using NVector3 = System.Numerics.Vector3;

namespace Meridian
{
    /// <summary>Scene-owned controls, fixed-step clock and interface. No persistent game state.</summary>
    public sealed class AirCombatGame : MonoBehaviour
    {
        public AirMission Mission { get; private set; }
        public AirRenderer View { get; private set; }
        public bool Briefing { get; private set; } = true;
        public bool Paused { get; private set; }
        public bool DiagnosticControl { get; set; }
        public bool ShowLead { get; set; } = true;
        public float Sensitivity = .8f;
        public bool MouseStick;
        public string SeedText = "1944";
        MissionKind selectedKind = MissionKind.Duel;
        float accumulator, throttle = .78f, hitFlash;
        int previousHits;
        int targetIndex;
        bool help, invertMouse;
        string seedError = "";
        GUIStyle body, small, title, button, number;
        readonly Color ink = new Color(.88f,.90f,.78f), amber = new Color(1,.73f,.30f), green = new Color(.59f,.89f,.63f);
        Rect screenRect;

        void Awake()
        {
            Time.timeScale=1; Application.targetFrameRate=60;
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            var root = new GameObject("Meridian presentation"); root.transform.SetParent(transform);
            View=root.AddComponent<AirRenderer>();
            Mission=new AirMission(1944,selectedKind); View.Build(Mission);
        }

        public void StartSortie(int seed, MissionKind kind)
        {
            selectedKind=kind;SeedText=seed.ToString();seedError="";
            Mission=new AirMission(seed,kind);View.Build(Mission);
            Briefing=false;Paused=false;help=false;accumulator=0;throttle=.78f;targetIndex=0;previousHits=0;hitFlash=0;
            ShowLead=kind==MissionKind.Duel;SetCursor();
        }
        public void SetPaused(bool value) { Paused=value;accumulator=0;SetCursor(); }
        public void StepForDiagnostics(FlightInput input,float dt) { Mission.Step(input,dt); View.Sync(Mission,dt); }
        public void ShowBriefing() { Briefing=true;Paused=false;help=false;SetCursor(); }
        void SetCursor()
        {
            bool flying=!Briefing&&!Paused&&Mission.Outcome==MissionOutcome.Flying;
            Cursor.visible=!flying || MouseStick;
            Cursor.lockState=flying ? CursorLockMode.Confined : CursorLockMode.None;
        }
        void OnDestroy() { Cursor.visible=true;Cursor.lockState=CursorLockMode.None; }

        void Update()
        {
            var k=Keyboard.current;
            if(k!=null)
            {
                if(k.escapeKey.wasPressedThisFrame)
                {
                    if(Briefing)VectorCabinet.GameCollection.ReturnToMenu();
                    else { help=false;SetPaused(!Paused); }
                }
                if(k.hKey.wasPressedThisFrame&&!Briefing) { help=!help;SetPaused(help); }
                if(k.cKey.wasPressedThisFrame)View.SetCameraMode(1-View.CameraMode);
                if(k.gKey.wasPressedThisFrame)ShowLead=!ShowLead;
                if(k.tabKey.wasPressedThisFrame)targetIndex++;
                if(k.mKey.wasPressedThisFrame) { MouseStick=!MouseStick;if(MouseStick&&Mouse.current!=null)Mouse.current.WarpCursorPosition(new Vector2(Screen.width*.5f,Screen.height*.5f));SetCursor(); }
                if(Briefing&&k.enterKey.wasPressedThisFrame)Launch();
            }
            if(!Briefing&&!Paused&&Mission.Outcome==MissionOutcome.Flying&&!DiagnosticControl)
            {
                FlightInput input=ReadControls(Mathf.Min(Time.unscaledDeltaTime,.1f));
                accumulator+=Mathf.Min(Time.unscaledDeltaTime,.1f);
                while(accumulator>=1f/60f)
                {
                    Mission.Step(input,1f/60f);accumulator-=1f/60f;
                }
                if(Mission.Outcome!=MissionOutcome.Flying)SetCursor();
            }
            if(Mission.Hits>previousHits)hitFlash=.35f;
            previousHits=Mission.Hits;hitFlash=Mathf.Max(0,hitFlash-Time.unscaledDeltaTime);
            View.Sync(Mission,Paused?0:Time.unscaledDeltaTime);
        }

        FlightInput ReadControls(float dt)
        {
            var k=Keyboard.current;var m=Mouse.current;
            FlightInput input=new FlightInput { Throttle=throttle };
            if(k!=null)
            {
                float roll=(Held(k.dKey.isPressed||k.rightArrowKey.isPressed)-Held(k.aKey.isPressed||k.leftArrowKey.isPressed));
                float pitch=(Held(k.sKey.isPressed||k.downArrowKey.isPressed)-Held(k.wKey.isPressed||k.upArrowKey.isPressed));
                input.Roll=roll*Sensitivity;input.Pitch=pitch*Sensitivity;
                input.Yaw=(Held(k.eKey.isPressed)-Held(k.qKey.isPressed))*.65f;
                throttle=Mathf.Clamp01(throttle+dt*.32f*(Held(k.leftShiftKey.isPressed||k.rightShiftKey.isPressed||k.rKey.isPressed)-Held(k.leftCtrlKey.isPressed||k.rightCtrlKey.isPressed||k.fKey.isPressed)));
                input.Fire=k.spaceKey.isPressed;
                if(k.zKey.isPressed)
                {
                    // A pilot aid that moves the controls. The aircraft still has angular inertia.
                    input.Roll=Mathf.Clamp(Mission.Player.Right.Y*3f,-.7f,.7f);
                    if(Mathf.Abs(pitch)<.1f) input.Pitch=-Mission.Player.Forward.Y*.8f;
                }
            }
            if(m!=null)
            {
                input.Fire|=m.leftButton.isPressed;
                if(MouseStick)
                {
                    Vector2 offset=m.position.ReadValue()-new Vector2(Screen.width*.5f,Screen.height*.5f);
                    input.Roll+=DeadZone(offset.x/(Screen.height*.24f))*.85f*Sensitivity;
                    input.Pitch+=DeadZone(offset.y/(Screen.height*.24f))*(invertMouse?-1:1)*.65f*Sensitivity;
                }
            }
            input.Pitch=Mathf.Clamp(input.Pitch,-1,1);input.Roll=Mathf.Clamp(input.Roll,-1,1);input.Throttle=throttle;
            return input;
        }
        static float Held(bool value) { return value?1:0; }
        static float DeadZone(float value) { return Mathf.Sign(value)*Mathf.Clamp01((Mathf.Abs(value)-.08f)/.92f); }
        void Launch() { int seed;if(int.TryParse(SeedText,out seed))StartSortie(seed,selectedKind);else seedError="Use a whole-number seed."; }

        void Styles()
        {
            if(body!=null)return;
            body=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true,normal={textColor=ink}};
            small=new GUIStyle(body){fontSize=14};
            title=new GUIStyle(body){fontSize=43,fontStyle=FontStyle.Bold};
            number=new GUIStyle(body){fontSize=24,fontStyle=FontStyle.Bold};
            button=new GUIStyle(body){fontSize=17,alignment=TextAnchor.MiddleCenter};
        }
        void Fill(float x,float y,float w,float h,Color color)
        {var old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=old;}
        void Label(float x,float y,float w,float h,string text,GUIStyle style=null,Color? color=null)
        {var old=GUI.color;if(color.HasValue)GUI.color=color.Value;GUI.Label(new Rect(x,y,w,h),text,style??body);GUI.color=old;}
        bool Button(float x,float y,float w,string text,bool active=false)
        {
            Rect r=new Rect(x,y,w,43);
            Fill(x,y,w,43,r.Contains(Event.current.mousePosition)?new Color(.27f,.32f,.25f):new Color(.12f,.17f,.15f));
            Fill(x,y,active?4:1,43,active?amber:new Color(.4f,.49f,.4f));
            return GUI.Button(r,text,button);
        }
        void Line(Vector2 a,Vector2 b,Color c,float width=1.5f)
        {
            var mat=GUI.matrix;GUI.matrix=mat*Matrix4x4.TRS(a,Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
            float length=Vector2.Distance(a,b);
            Fill(0,-width/2-.7f,length,width+1.4f,new Color(.018f,.025f,.021f,c.a*.75f));
            Fill(0,-width/2,length,width,c);GUI.matrix=mat;
        }
        void OnGUI()
        {
            if(Mission==null||View==null)return;Styles();
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
            screenRect=new Rect((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2,1280*scale,800*scale);
            GUI.matrix=Matrix4x4.TRS(new Vector3(screenRect.x,screenRect.y,0),Quaternion.identity,Vector3.one*scale);
            if(Briefing)BriefingScreen();
            else
            {
                Hud();
                if(Mission.Outcome!=MissionOutcome.Flying)Debrief();
                else if(Paused)PauseScreen();
            }
            GUI.matrix=Matrix4x4.identity;
        }
        void Panel(float x,float y,float w,float h)
        {Fill(x,y,w,h,new Color(.028f,.047f,.043f,.96f));Fill(x,y,w,2,amber);}
        void BriefingScreen()
        {
            Fill(0,0,1280,800,new Color(.025f,.042f,.040f,.70f));
            Panel(54,43,1172,715);
            Label(87,65,1000,25,"THE VECTOR CABINET   /   OPERATIONS ROOM   /   SUMMER 1944",small,amber);
            Label(84,105,1070,68,"MERIDIAN 1944",title);
            Label(88,180,1055,51,"A fictional Channel patrol. A real exchange of height, speed and opportunity.");
            if(Button(88,246,528,"TRAINING DUEL  /  one opponent",selectedKind==MissionKind.Duel))selectedKind=MissionKind.Duel;
            if(Button(640,246,528,"COURIER COVER  /  two interceptors",selectedKind==MissionKind.ConvoyCover))selectedKind=MissionKind.ConvoyCover;
            Label(88,311,514,113,selectedKind==MissionKind.Duel?
                "You begin airborne in a Kestrel Mk.IX. Defeat one Falke fighter. A lead marker helps you learn gunnery. Your opponent is armed and uses the same flight model.":
                "Protect the unarmed Heron courier for five minutes, or destroy both interceptors. They divide their attention between you and the courier. Stay close enough to intervene.");
            Label(640,311,527,113,"Bank with A / D, then pull with S to turn. W lowers the nose. More bank needs more elevator. Climbing spends speed; diving earns it. Release the stick to settle the aircraft.");
            Label(88,440,518,134,"W / S   nose down / up     A / D   roll\nQ / E   rudder     Shift / Ctrl   throttle\nSpace / left mouse   guns     Z   level assist\nC   cockpit / chase     Tab   next target",small);
            Label(640,440,519,134,"G   lead cue     M   mouse stick     H   flight notes\nEsc   pause / controls / return to cabinet\n\nClose to 300–600 m. Lead the target and use short bursts. You have limited ammunition.",small);
            Label(88,587,105,28,"SORTIE SEED",small,amber);
            SeedText=GUI.TextField(new Rect(204,582,180,34),SeedText,12);
            Label(408,587,725,30,seedError.Length>0?seedError:"Same seed, same island, initial contacts and pilots.",small);
            if(Button(88,653,528,"TAKE THE AIR   [ENTER]",true))Launch();
            if(Button(640,653,528,"RETURN TO CABINET"))VectorCabinet.GameCollection.ReturnToMenu();
        }

        AircraftState Target()
        {
            int count=0;foreach(var a in Mission.Aircraft)if(a.Team!=Mission.Player.Team&&a.Alive)count++;
            if(count==0)return null;int wanted=targetIndex%count;
            foreach(var a in Mission.Aircraft)if(a.Team!=Mission.Player.Team&&a.Alive) { if(wanted--==0)return a; }
            return null;
        }
        Vector2 Project(NVector3 point,out bool ahead)
        {
            var p=View.ViewCamera.WorldToScreenPoint(AirRenderer.World(point));ahead=p.z>0;
            return new Vector2((p.x-screenRect.x)/screenRect.width*1280,(Screen.height-p.y-screenRect.y)/screenRect.height*800);
        }
        void Bracket(Vector2 p,Color color,float radius)
        {
            float r=radius;float k=r*.4f;
            Line(p+new Vector2(-r,-r),p+new Vector2(-r+k,-r),color);Line(p+new Vector2(-r,-r),p+new Vector2(-r,-r+k),color);
            Line(p+new Vector2(r,-r),p+new Vector2(r-k,-r),color);Line(p+new Vector2(r,-r),p+new Vector2(r,-r+k),color);
            Line(p+new Vector2(-r,r),p+new Vector2(-r+k,r),color);Line(p+new Vector2(-r,r),p+new Vector2(-r,r-k),color);
            Line(p+new Vector2(r,r),p+new Vector2(r-k,r),color);Line(p+new Vector2(r,r),p+new Vector2(r,r-k),color);
        }
        bool Visible(NVector3 point)
        {
            var start=Mission.Player.Position;
            for(int i=1;i<16;i++) { var p=NVector3.Lerp(start,point,i/16f);if(p.Y<Mission.Terrain.Height(p.X,p.Z)+4)return false; }
            return true;
        }
        void Hud()
        {
            var p=Mission.Player;
            float knots=p.Speed*1.94384f, alt=p.Position.Y*3.28084f, agl=p.Position.Y-Mission.Terrain.Height(p.Position.X,p.Position.Z);
            float heading=(Mathf.Atan2(p.Forward.X,p.Forward.Z)*Mathf.Rad2Deg+360)%360;
            Fill(0,0,1280,78,new Color(.028f,.047f,.043f,.90f));
            Label(24,10,190,28,"MERIDIAN / "+Mission.Seed,small,amber);
            Label(24,37,180,30,$"{(int)Mission.Time/60:00}:{(int)Mission.Time%60:00}   KILLS {Mission.Kills}",small);
            Label(224,12,736,28,Mission.Kind==MissionKind.Duel?"DUEL  /  Defeat the Falke":"COVER  /  Keep the Heron alive for five minutes",body);
            Label(224,44,736,24,$"{Mission.EnemyRemaining} HOSTILE   •   "+(Mission.Courier!=null?$"HERON {Mathf.CeilToInt(Mission.Courier.Health/180*100)}%   •   HOME IN {Mathf.Max(0,300-Mission.Time):0}s":"Guns effective inside 800 m"),small);
            Label(1017,14,235,26,"H  NOTES    ESC  PAUSE",small);
            Label(1017,43,235,25,(MouseStick?"MOUSE STICK":"KEYBOARD STICK")+" / C VIEW",small);
            Fill(0,715,1280,85,new Color(.028f,.047f,.043f,.95f));
            Instrument(24,"SPEED / KNOTS",knots.ToString("0"),knots<115?amber:ink);
            Instrument(207,"ALT / FEET",alt.ToString("0"),ink);
            Instrument(391,"HEADING",heading.ToString("000")+"°",ink);
            Instrument(563,"THROTTLE",(p.Throttle*100).ToString("0")+"%",ink);
            Instrument(745,"AIRFRAME",Mathf.Max(0,p.Health).ToString("0")+"%",p.Health<40?amber:ink);
            Instrument(940,"ROUNDS",p.Ammo.ToString(),p.Ammo<150?amber:ink);
            Instrument(1112,"G LOAD",p.LoadFactor.ToString("0.0"),ink);
            bool ahead;Vector2 sight=Project(p.Position+p.Forward*1200,out ahead);
            if(ahead) { Line(sight+new Vector2(-15,0),sight+new Vector2(-5,0),green);Line(sight+new Vector2(5,0),sight+new Vector2(15,0),green);Line(sight+new Vector2(0,-15),sight+new Vector2(0,-5),green);Fill(sight.x-1,sight.y-1,2,2,green); }
            var target=Target();
            if(target!=null)
            {
                float distance=NVector3.Distance(target.Position,p.Position);
                Vector2 pt=Project(target.Position,out ahead);
                if(ahead&&pt.x>22&&pt.x<1258&&pt.y>105&&pt.y<680&&Visible(target.Position))
                {
                    Bracket(pt,amber,17);Label(pt.x+23,pt.y-21,220,24,target.Callsign+"  "+distance.ToString("0")+"m",small,amber);
                    if(ShowLead&&distance<1400)
                    {
                        Vector2 lead=Project(Weapons.ComputeLead(p,target),out ahead);
                        if(ahead&&lead.x>0&&lead.x<1280&&lead.y>80&&lead.y<700)
                        { Bracket(lead,green,5);Line(pt,lead,new Color(.63f,.8f,.62f,.4f),1); }
                    }
                }
                else
                {
                    var local=System.Numerics.Vector3.Transform(target.Position-p.Position,System.Numerics.Quaternion.Inverse(p.Rotation));
                    string bearing=local.Z<0?"BEHIND / ":"";
                    bearing+=local.X<0?"LEFT":"RIGHT";bearing+=local.Y>150?" / HIGH":local.Y < -150?" / LOW":" / LEVEL";
                    Label(414,97,620,27,target.Callsign+"  "+distance.ToString("0")+"m  "+bearing,small,amber);
                }
                Label(22,103,250,26,"TARGET  "+target.Callsign,small,amber);
                Label(22,131,250,26,$"{distance:0}m   {target.Health:0}%",small);
            }
            Radar();
            string warning=p.StallFactor>.2f?"STALL — LOWER NOSE, ADD POWER":p.Speed>p.Definition.MaxSpeed?"OVERSPEED — REDUCE POWER, EASE OUT":agl<140?"TERRAIN — WINGS LEVEL, PULL UP":p.Health<35?"AIRFRAME DAMAGED — FLY SMOOTHLY":"";
            if(warning.Length>0)Label(335,628,860,37,warning,number,amber);
            if(Mission.MessageTime>0)Label(289,668,794,31,Mission.LastMessage,body,green);
            if(hitFlash>0)Label(sight.x+27,sight.y+20,120,26,"HIT",small,green);
            if(ShowLead)Label(23,679,250,22,"G  LEAD CUE ON",small,green);
            if(MouseStick)
            {
                Vector2 mp=Mouse.current!=null?Mouse.current.position.ReadValue():Vector2.zero;
                var m=new Vector2((mp.x-screenRect.x)/screenRect.width*1280,(Screen.height-mp.y-screenRect.y)/screenRect.height*800);
                Bracket(m,new Color(.86f,.89f,.74f,.6f),4);
            }
        }
        void Instrument(float x,string label,string value,Color color)
        {Label(x,728,180,23,label,small);Label(x,753,180,35,value,number,color);}
        void Radar()
        {
            Vector2 center=new Vector2(1170,172);Fill(1080,91,180,171,new Color(.02f,.05f,.05f,.7f));
            Line(center+Vector2.left*70,center+Vector2.right*70,new Color(.22f,.37f,.33f));Line(center+Vector2.up*60,center+Vector2.down*60,new Color(.22f,.37f,.33f));
            Line(center+new Vector2(-5,7),center+new Vector2(0,-5),green);Line(center+new Vector2(5,7),center+new Vector2(0,-5),green);
            float heading=Mathf.Atan2(Mission.Player.Forward.X,Mission.Player.Forward.Z);
            foreach(var a in Mission.Aircraft)
            {
                if(a.IsPlayer||!a.Alive)continue;var d=a.Position-Mission.Player.Position;
                Vector2 offset=new Vector2(d.X*Mathf.Cos(heading)-d.Z*Mathf.Sin(heading),-(d.X*Mathf.Sin(heading)+d.Z*Mathf.Cos(heading)))/4000*68;
                offset=Vector2.ClampMagnitude(offset,68);
                Fill(center.x+offset.x-3,center.y+offset.y-3,6,6,a.Team==Mission.Player.Team?green:amber);
            }
            Label(1092,235,170,22,"CONTACTS / 4 km",small);
        }
        void PauseScreen()
        {
            Panel(180,136,920,517);
            Label(215,159,836,62,help?"PILOT'S NOTES":"SORTIE PAUSED",title);
            Label(215,239,835,155,"Bank with A / D, then pull S to curve the flight path. W lowers the nose. Q / E trims the nose sideways. Z gently levels the wings. A steep bank without elevator loses height.\n\nBelow about 110 knots, ease the nose down and add power. In a dive, watch speed and pull out smoothly. Damage reduces lift, power and control authority.",body);
            Label(215,409,831,65,"Shift / Ctrl (or R / F) throttle • Space / left mouse guns • C view • Tab target\nM mouse stick (cursor above center pulls up) • G lead cue • H notes • Esc resume",small);
            Label(215,489,190,27,"STICK SENSITIVITY",small);
            Sensitivity=GUI.HorizontalSlider(new Rect(410,500,250,22),Sensitivity,.35f,1.2f);
            invertMouse=GUI.Toggle(new Rect(710,489,260,28),invertMouse," Invert mouse pitch");
            if(Button(215,556,260,"RESUME")){help=false;SetPaused(false);}
            if(Button(495,556,260,"RESTART SORTIE"))StartSortie(Mission.Seed,Mission.Kind);
            if(Button(775,556,290,"RETURN TO CABINET"))VectorCabinet.GameCollection.ReturnToMenu();
        }
        void Debrief()
        {
            Panel(244,176,792,457);
            Label(278,201,730,61,Mission.Outcome==MissionOutcome.Victory?"SORTIE COMPLETE":"SORTIE LOST",title);
            Label(280,280,712,75,Mission.Summary,body);
            Label(280,364,712,63,$"Time {(int)Mission.Time/60:00}:{(int)Mission.Time%60:00}     Victories {Mission.Kills}     Score {Mission.Score}\nRounds fired {Mission.RoundsFired}     Hits {Mission.Hits}     Seed {Mission.Seed}",body);
            if(Button(280,451,328,"FLY THIS SORTIE AGAIN"))StartSortie(Mission.Seed,Mission.Kind);
            if(Button(632,451,366,"NEW BRIEFING")) { SeedText=unchecked(Mission.Seed+7919).ToString();ShowBriefing(); }
            if(Button(280,535,718,"RETURN TO CABINET"))VectorCabinet.GameCollection.ReturnToMenu();
        }
    }
}
