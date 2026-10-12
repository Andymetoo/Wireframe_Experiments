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
        MissionKind selectedKind = MissionKind.Interception;
        readonly AirInputAdapter inputAdapter = new AirInputAdapter();
        public AirInputAdapter Controls { get { return inputAdapter; } }
        public int SelectedTargetId { get { return Mission.SelectedTargetId; } }
        float accumulator, hitFlash, lookYaw, lookPitch;
        int previousHits;
        int menuIndex = 5;
        bool help, invertMouse, stickBindings;
        bool editingSeed;
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
            if(Mission.SelectedTargetId<0)Mission.SelectNextTarget();
            Briefing=false;Paused=false;help=false;stickBindings=false;editingSeed=false;accumulator=0;previousHits=0;hitFlash=0;lookYaw=lookPitch=0;menuIndex=0;
            inputAdapter.Reset();
            ShowLead=kind==MissionKind.Duel;SetCursor();
        }
        public void SetPaused(bool value) { Paused=value;accumulator=0;menuIndex=0;if(!value)stickBindings=false;SetCursor(); }
        public void StepForDiagnostics(FlightInput input,float dt) { Mission.Step(input,dt); View.Sync(Mission,dt); }
        public void ShowBriefing() { Briefing=true;Paused=false;help=false;menuIndex=5;SetCursor(); }
        void SetCursor()
        {
            bool flying=!Briefing&&!Paused&&Mission.Outcome==MissionOutcome.Flying;
            Cursor.visible=!flying || MouseStick;
            Cursor.lockState=flying ? CursorLockMode.Confined : CursorLockMode.None;
        }
        void OnDestroy() { inputAdapter.Dispose();Cursor.visible=true;Cursor.lockState=CursorLockMode.None; }

        void Update()
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            inputAdapter.Settings.Sensitivity=Sensitivity;inputAdapter.Settings.MouseStick=MouseStick;inputAdapter.Settings.InvertMouse=invertMouse;
            bool wasFlying=!Briefing&&!Paused&&Mission.Outcome==MissionOutcome.Flying;
            AirControlIntent intent=inputAdapter.Read(dt,wasFlying&&!DiagnosticControl);
            if(Briefing||Paused||Mission.Outcome!=MissionOutcome.Flying)HandleMenu(intent);
            else
            {
                if(intent.Pause){help=false;SetPaused(true);}
                if(intent.Notes){help=true;SetPaused(true);}
                if(intent.Camera)View.SetCameraMode(1-View.CameraMode);
                if(intent.Lead)ShowLead=!ShowLead;
                if(intent.NextTarget)Mission.SelectNextTarget();
                if(!SelectedTargetAlive())Mission.SelectNextTarget();
                if(intent.HasOrder)Mission.IssueOrder(intent.Order,Mission.SelectedTargetId);
                if(intent.MouseMode){MouseStick=!MouseStick;if(MouseStick&&Mouse.current!=null)Mouse.current.WarpCursorPosition(new Vector2(Screen.width*.5f,Screen.height*.5f));SetCursor();}
                if(intent.CenterLook){lookYaw=lookPitch=0;}
                lookYaw=Mathf.Clamp(lookYaw+intent.LookRate.x*dt+intent.LookDelta.x,-130,130);
                lookPitch=Mathf.Clamp(lookPitch+intent.LookRate.y*dt+intent.LookDelta.y,-65,65);
                View.SetLook(lookYaw,lookPitch);
            }
            if(!Briefing&&!Paused&&Mission.Outcome==MissionOutcome.Flying&&!DiagnosticControl)
            {
                FlightInput input=ReadControls(intent);
                accumulator+=dt;
                while(accumulator>=1f/60f)
                {
                    Mission.Step(input,1f/60f);accumulator-=1f/60f;
                }
                if(Mission.Outcome!=MissionOutcome.Flying){menuIndex=0;SetCursor();}
            }
            if(Mission.Hits>previousHits)hitFlash=.35f;
            previousHits=Mission.Hits;hitFlash=Mathf.Max(0,hitFlash-Time.unscaledDeltaTime);
            View.Sync(Mission,Paused?0:Time.unscaledDeltaTime);
        }

        FlightInput ReadControls(AirControlIntent intent)
        {
            FlightInput input=intent.Flight;
            if(intent.LevelAssist)
            {
                input.Roll=Mathf.Clamp(Mission.Player.Right.Y*3f,-.7f,.7f);
                if(Mathf.Abs(input.Pitch)<.1f)input.Pitch=-Mission.Player.Forward.Y*.8f;
            }
            return input;
        }
        void HandleMenu(AirControlIntent intent)
        {
            if(stickBindings){if(intent.Back||intent.Pause)stickBindings=false;return;}
            if(intent.Back){if(Briefing||Mission.Outcome!=MissionOutcome.Flying)VectorCabinet.GameCollection.ReturnToMenu();else{help=false;SetPaused(false);}return;}
            if((intent.Pause||intent.Notes)&&!Briefing&&Mission.Outcome==MissionOutcome.Flying){help=false;SetPaused(false);return;}
            int count=Briefing?7:3;
            if(intent.MenuMove!=0&&!(Briefing&&editingSeed))menuIndex=(menuIndex+intent.MenuMove+count)%count;
            if(Briefing&&!editingSeed&&intent.MissionChoice>=0){selectedKind=MissionAt(intent.MissionChoice);menuIndex=intent.MissionChoice;}
            if(!intent.Submit)return;
            if(Briefing)
            {
                // Enter keeps its original quick-launch behavior; a gamepad selects the focused row.
                if(intent.QuickLaunch){Launch();return;}
                if(menuIndex<5)selectedKind=MissionAt(menuIndex);else if(menuIndex==5)Launch();else VectorCabinet.GameCollection.ReturnToMenu();
            }
            else if(Mission.Outcome!=MissionOutcome.Flying)
            {
                if(menuIndex==0)StartSortie(Mission.Seed,Mission.Kind);
                else if(menuIndex==1){SeedText=unchecked(Mission.Seed+7919).ToString();ShowBriefing();}
                else VectorCabinet.GameCollection.ReturnToMenu();
            }
            else if(menuIndex==0){help=false;SetPaused(false);}
            else if(menuIndex==1)StartSortie(Mission.Seed,Mission.Kind);
            else VectorCabinet.GameCollection.ReturnToMenu();
        }
        static MissionKind MissionAt(int index)
        {return index==0?MissionKind.Interception:index==1?MissionKind.BomberEscort:index==2?MissionKind.GroundAttack:index==3?MissionKind.Duel:MissionKind.ConvoyCover;}
        static string MissionName(MissionKind kind)
        {return kind==MissionKind.Interception?"FIGHTER INTERCEPTION":kind==MissionKind.BomberEscort?"BOMBER ESCORT":kind==MissionKind.GroundAttack?"GROUND ATTACK":kind==MissionKind.Duel?"TRAINING DUEL":"COURIER COVER";}
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
            Panel(44,27,1192,745);
            Label(78,45,1060,25,"THE VECTOR CABINET   /   SQUADRON OPERATIONS   /   SUMMER 1944",small,amber);
            Label(75,77,1080,62,"MERIDIAN 1944",title);
            Label(79,140,1060,30,"Your flight. Your decisions. Every aircraft can be lost.");
            for(int i=0;i<5;i++)
            {
                MissionKind kind=MissionAt(i);
                if(Button(79,190+i*51,405,(menuIndex==i?"> ":"")+(i+1)+"  "+MissionName(kind),selectedKind==kind)){selectedKind=kind;menuIndex=i;}
            }
            Label(514,190,670,34,MissionName(selectedKind),number,amber);
            Label(514,234,665,108,BriefingText(selectedKind));
            Label(514,351,665,91,selectedKind==MissionKind.Duel||selectedKind==MissionKind.ConvoyCover?
                "Original patrols preserved. Bank, pull, lead the target and use short bursts inside 300–600 m. Your opponent flies the same aerodynamic model.":
                "Two wingmen fly with you. They can be separated, damaged or shot down. Orders guide their intentions; immediate danger takes priority. Stay aware of your objective.",small);
            Label(79,462,530,124,"W / S   nose down / up     A / D   roll\nQ / E   rudder     Shift / Ctrl   throttle\nSpace / LMB   guns     Z   level assist     Tab   target\n1   form up     2   engage     3   cover me     4   regroup\nC   view     G   lead cue     M   mouse stick     RMB   look",small);
            Label(653,462,524,124,"GAMEPAD: left stick fly; triggers rudder; RB guns\nD-pad up / down throttle; LB level assist; right stick look\nX target; B view; A lead cue; right-stick click center look\nHold Y + D-pad: up form / right engage / left cover / down regroup\nStart pause. Menus: D-pad navigate, A select, B back.",small);
            Label(79,612,108,25,"SORTIE SEED",small,amber);
            GUI.SetNextControlName("MeridianSortieSeed");
            SeedText=GUI.TextField(new Rect(195,607,174,32),SeedText,12);
            editingSeed=GUI.GetNameOfFocusedControl()=="MeridianSortieSeed";
            Label(391,610,787,39,seedError.Length>0?seedError:"After launch, Esc / Start opens controls, stick bindings and axis inversion.",small);
            if(Button(79,681,535,(menuIndex==5?"> ":"")+"TAKE THE AIR   [ENTER / A]",menuIndex==5))Launch();
            if(Button(645,681,535,(menuIndex==6?"> ":"")+"RETURN TO CABINET",menuIndex==6))VectorCabinet.GameCollection.ReturnToMenu();
        }

        static string BriefingText(MissionKind kind)
        {
            switch(kind)
            {
                case MissionKind.Interception:return "Destroy both raiders before either crosses the defended boundary. Escort fighters are secondary targets. Identify Raid 1 and Raid 2, designate a target with Tab, and send your wingmen to engage before the raiders get through.";
                case MissionKind.BomberEscort:return "Protect the bomber formation on its route to friendly airspace. At least two bombers must arrive. Killing every fighter does not complete the escort: the bombers must make it home. Break off distant pursuits when the formation needs you.";
                case MissionKind.GroundAttack:return "Destroy all four supply trucks and survive. Two gun positions and hostile fighters defend the target; destroying them is optional. Set up a shallow strafing pass, aim at the marked vehicles and pull out early. Your wingmen can keep the fighters occupied.";
                case MissionKind.ConvoyCover:return "Protect the unarmed Heron courier for five minutes, or destroy both interceptors. They divide their attention between you and the courier. Stay close enough to intervene.";
                default:return "Begin airborne in a Kestrel Mk.IX and defeat one Falke fighter. A lead marker helps you learn gunnery. Your armed opponent uses the same flight model. No wingmen: this is the original one-on-one duel.";
            }
        }

        AircraftState Target()
        {
            foreach(var a in Mission.Aircraft)if(a.Id==Mission.SelectedTargetId&&a.Alive)return a;
            return null;
        }
        bool SelectedTargetAlive()
        {
            foreach(var a in Mission.Aircraft)if(a.Id==Mission.SelectedTargetId&&a.Alive)return true;
            foreach(var t in Mission.GroundTargets)if(t.Id==Mission.SelectedTargetId&&t.Alive)return true;
            return false;
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
            Label(224,12,736,28,MissionName(Mission.Kind),body);
            Label(224,44,782,24,Mission.ObjectiveStatus,small);
            Label(1017,14,235,26,"H  NOTES    ESC  PAUSE",small);
            Label(1017,43,235,25,MouseStick?"MOUSE STICK":inputAdapter.ActiveDevice,small);
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
                bool contact=distance<5500&&Visible(target.Position);
                if(contact&&ahead&&pt.x>22&&pt.x<1258&&pt.y>105&&pt.y<680)
                {
                    Bracket(pt,amber,17);Label(pt.x+23,pt.y-21,220,24,target.Callsign+"  "+distance.ToString("0")+"m",small,amber);
                    if(ShowLead&&distance<1400)
                    {
                        Vector2 lead=Project(Weapons.ComputeLead(p,target),out ahead);
                        if(ahead&&lead.x>0&&lead.x<1280&&lead.y>80&&lead.y<700)
                        { Bracket(lead,green,5);Line(pt,lead,new Color(.63f,.8f,.62f,.4f),1); }
                    }
                }
                else if(contact)
                {
                    var local=System.Numerics.Vector3.Transform(target.Position-p.Position,System.Numerics.Quaternion.Inverse(p.Rotation));
                    string bearing=local.Z<0?"BEHIND / ":"";
                    bearing+=local.X<0?"LEFT":"RIGHT";bearing+=local.Y>150?" / HIGH":local.Y < -150?" / LOW":" / LEVEL";
                    Label(414,97,620,27,target.Callsign+"  "+distance.ToString("0")+"m  "+bearing,small,amber);
                }
                Label(22,103,250,26,"TARGET  "+target.Callsign,small,amber);
                Label(22,131,280,26,contact?$"{distance:0}m   {target.Health:0}%":"Contact obscured / beyond visual range",small);
            }
            else GroundTargetHud();
            SquadronHud();
            ObjectiveHud();
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
                if(d.LengthSquared()>4000f*4000f||!Visible(a.Position))continue;
                Vector2 offset=new Vector2(d.X*Mathf.Cos(heading)-d.Z*Mathf.Sin(heading),-(d.X*Mathf.Sin(heading)+d.Z*Mathf.Cos(heading)))/4000*68;
                offset=Vector2.ClampMagnitude(offset,68);
                Fill(center.x+offset.x-3,center.y+offset.y-3,6,6,a.Team==Mission.Player.Team?green:amber);
            }
            Label(1092,235,170,22,"VISUAL / 4 km",small);
        }
        void GroundTargetHud()
        {
            foreach(var target in Mission.GroundTargets)
            {
                if(target.Id!=Mission.SelectedTargetId||!target.Alive)continue;
                float distance=NVector3.Distance(target.Position,Mission.Player.Position);
                Label(22,103,285,26,"TARGET  "+target.Name,small,amber);
                Label(22,131,285,26,$"{distance:0}m   {target.Health/target.MaxHealth*100:0}%",small);
                bool ahead;Vector2 point=Project(target.Position,out ahead);
                if(ahead&&point.x>20&&point.x<1250&&point.y>100&&point.y<680&&Visible(target.Position+NVector3.UnitY*5))
                {Bracket(point,amber,17);Label(point.x+22,point.y-18,260,40,target.Name+" / "+distance.ToString("0")+"m",small,amber);}
                else Label(390,98,630,25,"GROUND OBJECTIVE  /  "+Bearing(target.Position)+"  /  "+distance.ToString("0")+"m",small,amber);
                break;
            }
        }
        string Bearing(NVector3 position)
        {
            var local=NVector3.Transform(position-Mission.Player.Position,System.Numerics.Quaternion.Inverse(Mission.Player.Rotation));
            return (local.Z<0?"BEHIND / ":"")+(local.X<0?"LEFT":"RIGHT")+(local.Y>150?" / HIGH":local.Y < -150?" / LOW":" / LEVEL");
        }
        void SquadronHud()
        {
            if(Mission.Wingmen.Count==0)return;
            Fill(16,172,298,121,new Color(.02f,.05f,.05f,.66f));
            Label(25,180,283,23,"FLIGHT / "+OrderName(Mission.CurrentOrder),small,green);
            for(int i=0;i<Mission.Wingmen.Count;i++)
            {
                var a=Mission.Wingmen[i];float distance=NVector3.Distance(a.Position,Mission.Player.Position);
                Label(25,207+i*36,282,39,a.Callsign+"  "+(a.Alive?$"{a.Health:0}%  {distance/1000:0.0}km  "+Mission.GetPilotState(a.Id):"LOST"),small,a.Alive?ink:amber);
                if(!a.Alive||distance>4500||!Visible(a.Position))continue;
                bool ahead;Vector2 pt=Project(a.Position,out ahead);
                if(ahead&&pt.x>325&&pt.x<1020&&pt.y>115&&pt.y<590)
                {Line(pt+new Vector2(-6,10),pt+new Vector2(0,15),green);Line(pt+new Vector2(6,10),pt+new Vector2(0,15),green);Label(pt.x+11,pt.y+7,190,24,a.Callsign,small,green);}
            }
            Label(23,302,380,43,"1 FORM  2 ENGAGE  3 COVER  4 REGROUP\nTab designates the next target",small,green);
        }
        static string OrderName(SquadronOrder order)
        {return order==SquadronOrder.FormUp?"FORM UP":order==SquadronOrder.CoverMe?"COVER ME":order==SquadronOrder.Regroup?"REGROUP":"ENGAGE";}
        void ObjectiveHud()
        {
            if(Mission.Kind==MissionKind.Duel||Mission.Kind==MissionKind.ConvoyCover)return;
            Fill(1080,279,180,7,new Color(.15f,.23f,.21f));Fill(1080,279,180*Mathf.Clamp01(Mission.ObjectiveProgress),7,green);
            if(Mission.Bombers.Count>0)
            {
                Label(1087,300,183,23,"PROTECTED FORMATION",small,green);
                bool threatened=false;
                for(int i=0;i<Mission.Bombers.Count;i++)
                {
                    var a=Mission.Bombers[i];Label(1087,330+i*25,182,24,a.Callsign+"  "+(a.Alive?a.Health.ToString("0")+" HP":"LOST"),small,a.Alive?ink:amber);
                    if(!a.Alive)continue;
                    foreach(var enemy in Mission.Aircraft)
                    {
                        if(!enemy.Alive||enemy.Team==a.Team)continue;
                        var offset=a.Position-enemy.Position;float d2=offset.LengthSquared();
                        if(d2>1&&d2<1200*1200&&NVector3.Dot(enemy.Forward,NVector3.Normalize(offset))>.8f)threatened=true;
                    }
                }
                if(threatened)Label(420,580,690,34,"FORMATION THREATENED — BREAK OFF AND COVER",small,amber);
            }
            bool ahead;Vector2 point=Project(Mission.ObjectivePosition,out ahead);
            float distance=NVector3.Distance(Mission.ObjectivePosition,Mission.Player.Position);
            if(ahead&&point.x>330&&point.x<1010&&point.y>145&&point.y<590)
            {Bracket(point,new Color(.5f,.8f,1f,.65f),10);Label(point.x+15,point.y+4,250,24,"OBJECTIVE  "+(distance/1000).ToString("0.0")+"km",small,new Color(.5f,.8f,1));}
        }
        void PauseScreen()
        {
            Panel(142,99,996,606);
            Label(177,119,916,62,help?"PILOT'S NOTES":"SORTIE PAUSED",title);
            if(stickBindings){BindingPanel();return;}
            Label(178,190,922,100,"Bank with A / D, then pull S to curve the flight path. W lowers the nose. Below 110 knots, lower the nose and add power. Shift / Ctrl (or R / F) throttle; Space / LMB guns; Z level assist.\nTab target; 1 form up, 2 engage, 3 cover me, 4 regroup. Orders never override survival.",body);
            Label(178,300,921,84,"Gamepad: left stick fly; triggers rudder; RB guns; LB level. D-pad up / down throttle. X target; B view; A lead cue. Hold Y + D-pad: up form, right engage, left cover, down regroup.\nRight stick / held RMB looks around. V / middle mouse / right-stick click centers view. C view; G lead cue; M mouse stick. In menus use D-pad / A / B. Esc or Start resumes.",small);
            Label(178,398,204,26,"STICK SENSITIVITY",small);
            Sensitivity=GUI.HorizontalSlider(new Rect(389,409,222,22),Sensitivity,.35f,1.2f);
            Label(638,398,176,26,"ANALOG DEADZONE",small);
            inputAdapter.Settings.Deadzone=GUI.HorizontalSlider(new Rect(828,409,236,22),inputAdapter.Settings.Deadzone,.02f,.35f);
            Label(178,445,199,26,"LOOK SENSITIVITY",small);
            inputAdapter.Settings.LookSensitivity=GUI.HorizontalSlider(new Rect(389,456,222,22),inputAdapter.Settings.LookSensitivity,25,130);
            invertMouse=GUI.Toggle(new Rect(650,445,258,26),invertMouse," Invert mouse-stick pitch");
            var settings=inputAdapter.Settings;
            settings.InvertPitch=GUI.Toggle(new Rect(178,490,204,26),settings.InvertPitch," Invert analog pitch");
            settings.InvertRoll=GUI.Toggle(new Rect(407,490,203,26),settings.InvertRoll," Invert analog roll");
            settings.InvertYaw=GUI.Toggle(new Rect(638,490,203,26),settings.InvertYaw," Invert analog rudder");
            settings.InvertLook=GUI.Toggle(new Rect(861,490,213,26),settings.InvertLook," Invert look pitch");
            if(Button(178,540,909,"FLIGHT-STICK BINDINGS / OPTIONAL INPUT SYSTEM CONTROL PATHS"))stickBindings=true;
            if(Button(178,624,276,(menuIndex==0?"> ":"")+"RESUME",menuIndex==0)){help=false;SetPaused(false);}
            if(Button(466,624,300,(menuIndex==1?"> ":"")+"RESTART SORTIE",menuIndex==1))StartSortie(Mission.Seed,Mission.Kind);
            if(Button(778,624,309,(menuIndex==2?"> ":"")+"RETURN TO CABINET",menuIndex==2))VectorCabinet.GameCollection.ReturnToMenu();
        }
        void BindingPanel()
        {
            var s=inputAdapter.Settings;
            Label(178,191,919,74,"Optional: enter Input System axis paths for your connected device. Example: <Joystick>/stick/x. Leave unneeded fields empty. Axis defaults: positive pitch lowers the nose; invert above if needed. Hardware layouts vary and require testing.",small);
            s.StickRoll=BindingField(280,"ROLL",s.StickRoll);s.StickPitch=BindingField(321,"PITCH",s.StickPitch);
            s.StickYaw=BindingField(362,"RUDDER",s.StickYaw);s.StickThrottle=BindingField(403,"THROTTLE",s.StickThrottle);
            s.StickFire=BindingField(444,"GUNS",s.StickFire);
            s.ThrottleSigned=GUI.Toggle(new Rect(390,490,590,27),s.ThrottleSigned," Throttle axis spans -1 to +1 (off = 0 to +1)");
            Label(178,527,910,54,inputAdapter.BindingError.Length>0?inputAdapter.BindingError:"Bindings belong only to this Air Combat scene; leaving the game releases them. Empty bindings preserve keyboard / mouse and gamepad controls.",small);
            if(Button(178,621,441,"APPLY BINDINGS"))inputAdapter.ApplyFlightStickBindings();
            if(Button(646,621,441,"BACK TO CONTROLS"))stickBindings=false;
        }
        string BindingField(float y,string label,string value)
        {Label(178,y,201,30,label,small);return GUI.TextField(new Rect(390,y,697,30),value,128);}
        void Debrief()
        {
            Panel(222,137,836,526);
            Label(256,157,770,61,Mission.Outcome==MissionOutcome.Victory?"SORTIE COMPLETE":"SORTIE LOST",title);
            Label(258,230,756,69,Mission.Summary,body);
            Label(258,310,756,58,$"Time {(int)Mission.Time/60:00}:{(int)Mission.Time%60:00}     Victories {Mission.Kills}     Score {Mission.Score}\nRounds fired {Mission.RoundsFired}     Hits {Mission.Hits}     Seed {Mission.Seed}",body);
            Label(258,382,756,108,Mission.DebriefDetails,small,green);
            if(Button(258,516,354,(menuIndex==0?"> ":"")+"FLY THIS SORTIE AGAIN",menuIndex==0))StartSortie(Mission.Seed,Mission.Kind);
            if(Button(636,516,382,(menuIndex==1?"> ":"")+"NEW BRIEFING",menuIndex==1)) { SeedText=unchecked(Mission.Seed+7919).ToString();ShowBriefing(); }
            if(Button(258,587,760,(menuIndex==2?"> ":"")+"RETURN TO CABINET",menuIndex==2))VectorCabinet.GameCollection.ReturnToMenu();
        }
    }
}
