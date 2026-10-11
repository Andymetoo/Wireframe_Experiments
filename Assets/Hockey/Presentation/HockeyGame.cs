using System;
using UnityEngine;
using UnityEngine.InputSystem;
using NVector2 = System.Numerics.Vector2;

namespace ColdSnap
{
    /// <summary>Scene-owned input, fixed-step clock, camera intent and match interface.</summary>
    public sealed class HockeyGame : MonoBehaviour
    {
        public HockeySession Session { get; private set; }
        public HockeyRenderer View { get; private set; }
        public bool Briefing { get; private set; } = true;
        public bool Paused { get; private set; }
        public bool DiagnosticControl { get; set; }
        public float LookYaw { get; private set; }
        public float LookPitch { get; private set; } = 28;
        public int CameraMode { get; private set; }
        public string SeedText = "1988";
        public float Sensitivity = .12f;
        bool practice, showMap=true, invertMouse, help, charging;
        float accumulator, charge, controlsReady, shotPower;
        bool pendingShot,pendingPass,pendingCheck,pendingCall;
        MatchPhase previousPhase;
        string inputError="";
        GUIStyle body,small,title,button,number;
        Rect screenRect;
        readonly Color white=new Color(.86f,.94f,.95f),blue=new Color(.30f,.76f,1f),orange=new Color(1,.54f,.24f),mint=new Color(.50f,1,.75f);

        void Awake()
        {
            Time.timeScale=1;Application.targetFrameRate=60;
            var rendererObject=new GameObject("Cold Snap presentation");rendererObject.transform.SetParent(transform);
            View=rendererObject.AddComponent<HockeyRenderer>();
            Session=new HockeySession(1988);View.Build(Session);previousPhase=Session.Phase;
            View.SetView(LookYaw,LookPitch,CameraMode);SetCursor();
        }
        public void StartMatch(int seed,bool openIce=false)
        {
            Session=new HockeySession(seed,openIce);practice=openIce;SeedText=seed.ToString();
            View.Build(Session);Briefing=false;Paused=false;help=false;accumulator=0;
            LookYaw=Session.Player.Facing*Mathf.Rad2Deg;LookPitch=28;CameraMode=0;
            previousPhase=Session.Phase;controlsReady=Time.unscaledTime+.25f;ResetActions();SetCursor();
            View.SetView(LookYaw,LookPitch,CameraMode);View.Sync(Session,0);
        }
        public void SetPaused(bool value){Paused=value;accumulator=0;ResetActions();SetCursor();}
        public void ShowBriefing(){Briefing=true;Paused=false;help=false;ResetActions();SetCursor();}
        public void SetLook(float yaw,float pitch){LookYaw=yaw;LookPitch=Mathf.Clamp(pitch,-15,65);View.SetView(LookYaw,LookPitch,CameraMode);}
        public void SetCamera(int mode){CameraMode=mode==0?0:1;View.SetView(LookYaw,LookPitch,CameraMode);}
        public void StepForDiagnostics(HockeyInput input,float dt,bool bots=false)
        {
            if(bots)Session.StepBots(dt);else Session.Step(input,dt);
            if(bots)LookYaw=Session.Player.Facing*Mathf.Rad2Deg;
            if(Session.Phase==MatchPhase.Final)SetCursor();
            View.SetView(LookYaw,LookPitch,CameraMode);View.Sync(Session,dt);
        }
        void ResetActions(){pendingShot=pendingPass=pendingCheck=pendingCall=charging=false;charge=0;}
        void SetCursor()
        {
            bool active=!Briefing&&!Paused&&Session.Phase!=MatchPhase.Final;
            Cursor.lockState=active?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!active;
        }
        void OnDestroy(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void OnApplicationFocus(bool focus){if(!focus&&!Briefing&&!DiagnosticControl)SetPaused(true);}

        void Update()
        {
            var k=Keyboard.current;
            if(k!=null)
            {
                if(k.escapeKey.wasPressedThisFrame)
                {
                    if(Briefing){VectorCabinet.GameCollection.ReturnToMenu();return;}
                    help=false;SetPaused(!Paused);
                }
                if(k.hKey.wasPressedThisFrame&&!Briefing){help=!help;SetPaused(help);}
                if(k.tabKey.wasPressedThisFrame)showMap=!showMap;
                if(k.cKey.wasPressedThisFrame)SetCamera(1-CameraMode);
                if(Briefing&&k.enterKey.wasPressedThisFrame)Launch();
            }
            if(!Briefing&&!Paused&&Session.Phase!=MatchPhase.Final&&!DiagnosticControl)
            {
                float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
                HockeyInput input=ReadControls(dt);
                accumulator+=dt;
                while(accumulator>=1f/60f)
                {
                    input.Shoot=pendingShot;input.Pass=pendingPass;input.Check=pendingCheck;input.Call=pendingCall;input.ShotPower=input.Pass?1f:shotPower;
                    if(input.Pass)
                    {
                        var receiver=PassTarget();
                        if(receiver!=null)
                        {
                            float travel=NVector2.Distance(receiver.Position,Session.Player.Position)/18f;
                            input.Aim=HockeyMath.Unit(receiver.Position+receiver.Velocity*travel*.75f-Session.Player.Position,input.Aim);
                        }
                    }
                    Session.Step(input,1f/60f);
                    pendingShot=pendingPass=pendingCheck=pendingCall=false;
                    accumulator-=1f/60f;
                }
                if(Session.Phase==MatchPhase.Final)SetCursor();
            }
            if(Session.Phase!=previousPhase)
            {
                if(Session.Phase==MatchPhase.Faceoff){LookYaw=Session.Player.Facing*Mathf.Rad2Deg;LookPitch=28;ResetActions();}
                previousPhase=Session.Phase;
            }
            View.SetView(LookYaw,LookPitch,CameraMode);View.Sync(Session,Paused?0:Time.unscaledDeltaTime);
        }
        HockeyInput ReadControls(float dt)
        {
            var k=Keyboard.current;var m=Mouse.current;
            if(m!=null&&Time.unscaledTime>controlsReady)
            {
                Vector2 delta=m.delta.ReadValue();LookYaw+=Mathf.Clamp(delta.x,-500,500)*Sensitivity;
                LookPitch+=Mathf.Clamp(delta.y,-500,500)*Sensitivity*(invertMouse?1:-1);
                if(m.leftButton.wasPressedThisFrame){charging=true;charge=0;}
                if(charging&&m.leftButton.isPressed)charge=Mathf.Min(1,charge+dt/1.05f);
                if(charging&&m.leftButton.wasReleasedThisFrame){pendingShot=true;shotPower=.25f+.75f*charge;charging=false;charge=0;}
                if(m.rightButton.wasPressedThisFrame)pendingPass=true;
            }
            Vector2 movement=Vector2.zero;bool brake=false,sprint=false;
            if(k!=null)
            {
                LookYaw+=((k.rightArrowKey.isPressed?1:0)-(k.leftArrowKey.isPressed?1:0))*dt*95;
                LookPitch+=((k.downArrowKey.isPressed?1:0)-(k.upArrowKey.isPressed?1:0))*dt*55;
                movement=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                brake=k.spaceKey.isPressed;sprint=k.leftShiftKey.isPressed||k.rightShiftKey.isPressed;
                if(k.fKey.wasPressedThisFrame){pendingShot=true;shotPower=.58f;}
                pendingPass|=k.qKey.wasPressedThisFrame;pendingCheck|=k.eKey.wasPressedThisFrame;pendingCall|=k.rKey.wasPressedThisFrame;
            }
            LookYaw=Mathf.Repeat(LookYaw+180,360)-180;LookPitch=Mathf.Clamp(LookPitch,-15,65);
            float yaw=LookYaw*Mathf.Deg2Rad;NVector2 forward=new NVector2(Mathf.Sin(yaw),Mathf.Cos(yaw)),right=new NVector2(forward.Y,-forward.X);
            if(movement.sqrMagnitude>1)movement.Normalize();
            return new HockeyInput{Move=right*movement.x+forward*movement.y,Aim=forward,Brake=brake,Sprint=sprint};
        }
        void Launch(){int seed;if(int.TryParse(SeedText,out seed))StartMatch(seed,practice);else inputError="Use a whole-number match seed.";}
        public SkaterState PassTarget()
        {
            float a=LookYaw*Mathf.Deg2Rad;var forward=new NVector2(Mathf.Sin(a),Mathf.Cos(a));
            SkaterState result=null;float best=float.MaxValue;
            foreach(var skater in Session.Skaters)
            {
                if(skater==Session.Player||skater.Goalie||skater.Team!=0)continue;
                var delta=skater.Position-Session.Player.Position;float distance=delta.Length();
                if(distance<1.4f||distance>18)continue;float dot=NVector2.Dot(delta/distance,forward);
                if(dot<.9063f)continue;float score=(1-dot)*60+distance*.06f;
                if(score<best){best=score;result=skater;}
            }
            return result;
        }
        void Styles()
        {
            if(body!=null)return;
            body=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true,normal={textColor=white}};
            small=new GUIStyle(body){fontSize=14};title=new GUIStyle(body){fontSize=43,fontStyle=FontStyle.Bold};
            number=new GUIStyle(body){fontSize=25,fontStyle=FontStyle.Bold};
            button=new GUIStyle(body){alignment=TextAnchor.MiddleCenter,fontSize=17};
        }
        void Fill(float x,float y,float w,float h,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=old;}
        void Label(float x,float y,float w,float h,string text,GUIStyle style=null,Color? c=null)
        {Color old=GUI.color;if(c.HasValue)GUI.color=c.Value;GUI.Label(new Rect(x,y,w,h),text,style??body);GUI.color=old;}
        bool Button(float x,float y,float w,string text,bool selected=false)
        {
            var r=new Rect(x,y,w,44);Fill(x,y,w,44,r.Contains(Event.current.mousePosition)?new Color(.12f,.25f,.30f):new Color(.055f,.12f,.16f));
            Fill(x,y,selected?4:1,44,selected?blue:new Color(.23f,.39f,.44f));return GUI.Button(r,text,button);
        }
        void Line(Vector2 a,Vector2 b,Color c,float width=1.5f)
        {
            var mat=GUI.matrix;GUI.matrix=mat*Matrix4x4.TRS(a,Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
            Fill(0,-width/2,Vector2.Distance(a,b),width,c);GUI.matrix=mat;
        }
        void Panel(float x,float y,float w,float h){Fill(x,y,w,h,new Color(.018f,.035f,.052f,.97f));Fill(x,y,w,2,blue);}
        void OnGUI()
        {
            if(Session==null||View==null)return;Styles();
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
            screenRect=new Rect((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2,1280*scale,800*scale);
            GUI.matrix=Matrix4x4.TRS(new Vector3(screenRect.x,screenRect.y,0),Quaternion.identity,Vector3.one*scale);
            if(Briefing)BriefingScreen();
            else {Hud();if(Session.Phase==MatchPhase.Final)FinalScreen();else if(Paused)PauseScreen();}
            GUI.matrix=Matrix4x4.identity;
        }
        void BriefingScreen()
        {
            Fill(0,0,1280,800,new Color(.018f,.035f,.052f,.60f));Panel(60,43,1160,715);
            Label(94,65,1030,26,"THE VECTOR CABINET / AFTER HOURS AT THE CIVIC RINK",small,blue);
            Label(89,111,1000,62,"COLD SNAP",title);
            Label(94,183,1077,45,"Three skaters. One goalie. Find the pass that opens the ice.");
            if(Button(94,248,520,"THREE-MINUTE MATCH",!practice))practice=false;
            if(Button(640,248,520,"OPEN ICE / PRACTICE",practice))practice=true;
            Label(94,315,508,127,practice?
                "Learn your edges with the clock stopped. Opposing skaters stay in place; goalies and your two teammates remain active. Request passes and try shots. Return here to start a match.":
                "You are Blue #9, attacking the orange goal. Your teammates support and cover behind you. Three minutes, then one minute of sudden death if tied. No icing, offsides or penalties.");
            Label(640,315,520,127,"WASD pushes in the direction you look. Release to coast; turning your head does not erase momentum. Shift adds speed, Space digs in the edges to brake. Aim the stick with the mouse.");
            Label(94,466,525,101,"LEFT MOUSE  hold / release shot    F  quick shot\nRIGHT MOUSE / Q  pass toward a teammate\nE  check nearby opponent    R  request a pass\nC  camera    Tab  rink map    Esc / H  pause / help",small);
            Label(640,466,522,101,"The puck is loose until your stick is close enough to guide it. Shots stay on the ice; aim for a corner. A nearby teammate in your aim cone receives a gently assisted pass. Go for rebounds.",small);
            Label(94,593,106,26,"MATCH SEED",small,blue);SeedText=GUI.TextField(new Rect(213,586,171,35),SeedText,12);
            Label(416,593,729,32,inputError.Length>0?inputError:"Reproduce the same starting players and AI decisions.",small);
            if(Button(94,657,520,"TAKE THE ICE  [ENTER]",true))Launch();
            if(Button(640,657,520,"RETURN TO CABINET"))VectorCabinet.GameCollection.ReturnToMenu();
        }
        Vector2 Project(NVector2 point,float height,out bool front)
        {
            Vector3 p=View.ViewCamera.WorldToScreenPoint(new Vector3(point.X,height,point.Y));front=p.z>0;
            return new Vector2((p.x-screenRect.x)/screenRect.width*1280,(Screen.height-p.y-screenRect.y)/screenRect.height*800);
        }
        void Diamond(Vector2 p,float r,Color c)
        {Line(p+Vector2.up*r,p+Vector2.right*r,c,2);Line(p+Vector2.right*r,p+Vector2.down*r,c,2);Line(p+Vector2.down*r,p+Vector2.left*r,c,2);Line(p+Vector2.left*r,p+Vector2.up*r,c,2);}
        void Hud()
        {
            var player=Session.Player;var puck=Session.Puck;
            Fill(0,0,1280,72,new Color(.018f,.035f,.052f,.94f));
            Label(25,18,230,35,"COLD SNAP",number,white);
            Label(315,12,210,42,"BLUE  "+Session.BlueScore,number,blue);
            Label(542,12,190,43,Session.Practice?"OPEN ICE":$"{(int)Mathf.Ceil(Session.TimeRemaining)/60:0}:{(int)Mathf.Ceil(Session.TimeRemaining)%60:00}",number);
            Label(736,12,230,43,Session.OrangeScore+"  ORANGE",number,orange);
            Label(1011,14,247,45,"H  NOTES / ESC  PAUSE\n"+(Session.Overtime?"SUDDEN DEATH":"BLUE ATTACKS ORANGE"),small);
            if(Session.Phase==MatchPhase.Faceoff)
            {
                Panel(418,148,444,94);Label(439,164,404,37,"FACEOFF  /  "+Mathf.CeilToInt(Session.PhaseTime),number);
                Label(439,205,404,26,"Push forward when the puck drops.",small);
            }
            else if(Session.Phase==MatchPhase.Goal)
            {
                Panel(340,135,600,132);Label(369,149,542,58,Session.LastGoalTeam==0?"BLUE GOAL":"ORANGE GOAL",title,Session.LastGoalTeam==0?blue:orange);
                Label(371,217,540,32,"Back to center. The next play starts here.",body);
            }
            bool front;Vector2 p=Project(puck.Position,.12f,out front);
            float distance=NVector2.Distance(player.Position,puck.Position);
            if(front&&p.x>26&&p.x<1250&&p.y>84&&p.y<681)
            {
                if(distance>2.1f)Diamond(p,7,new Color(.05f,.10f,.13f,.9f));
                if(distance>5){Fill(p.x+11,p.y-12,111,25,new Color(.018f,.035f,.052f,.80f));Label(p.x+16,p.y-13,110,24,"PUCK "+distance.ToString("0")+"m",small,white);}
            }
            else
            {
                NVector2 delta=puck.Position-player.Position;float yaw=LookYaw*Mathf.Deg2Rad;
                float side=delta.X*Mathf.Cos(yaw)-delta.Y*Mathf.Sin(yaw),ahead=delta.X*Mathf.Sin(yaw)+delta.Y*Mathf.Cos(yaw);
                string direction=ahead<0?"BEHIND / ":"";direction+=side<0?"LEFT":"RIGHT";
                Panel(436,642,410,35);Label(451,648,383,25,"PUCK  "+direction+"  "+distance.ToString("0.0")+"m",small);
            }
            var passTarget=PassTarget();
            foreach(var s in Session.Skaters)
            {
                if(s.Team!=0||s.Goalie||s==player)continue;
                Vector2 marker=Project(s.Position,2.05f,out front);
                if(front&&marker.x>20&&marker.x<1050&&marker.y>87&&marker.y<590)
                {
                    Fill(marker.x-4,marker.y-3,8,4,blue);
                    Fill(marker.x+7,marker.y-14,176,25,new Color(.018f,.035f,.052f,.80f));
                    Label(marker.x+12,marker.y-15,180,25,(s==passTarget?"PASS  ":"")+(s.Id==1?"#7  WING":"#12  ANCHOR"),small,blue);
                }
            }
            // Stick aim projected onto the ice makes shot direction clear without hiding the puck.
            float a=LookYaw*Mathf.Deg2Rad;var aim=new NVector2(Mathf.Sin(a),Mathf.Cos(a));
            var aimPoint=player.Position+aim*4.5f;Vector2 aimScreen=Project(aimPoint,.06f,out front);
            if(front&&aimScreen.y<683&&aimScreen.y>85)
            {Line(aimScreen+new Vector2(-8,0),aimScreen+new Vector2(8,0),new Color(.03f,.24f,.31f,.75f));Line(aimScreen+new Vector2(0,-5),aimScreen+new Vector2(0,5),new Color(.03f,.24f,.31f,.75f));}
            if(showMap)RinkMap();
            Fill(0,704,1280,96,new Color(.018f,.035f,.052f,.96f));
            Label(24,717,170,23,"SKATING / km/h",small);Label(24,747,167,37,(player.Speed*3.6f).ToString("0.0"),number,blue);
            Label(205,717,515,31,player.Stun>0?"OFF BALANCE":puck.ControllerId==player.Id?"ON YOUR STICK":puck.ControllerId>=0?(Session.Skaters[puck.ControllerId].Team==0?"BLUE POSSESSION / R CALL":"DEFEND / E CHECK"):"LOOSE PUCK / CLOSE TO COLLECT",body,player.Stun>0?orange:mint);
            Label(205,756,730,27,"LMB SHOT   RMB PASS   SPACE BRAKE   E CHECK   R CALL",small);
            Label(961,717,290,25,"SHOT POWER",small);
            Fill(963,755,286,9,new Color(.11f,.21f,.26f));Fill(963,755,286*(charging?charge:0),9,orange);
            if(Session.MessageTime>0){Panel(229,92,789,35);Label(243,97,765,27,Session.LastMessage,small);}
            if(Session.PassCallTime>0)Label(25,92,191,28,"PASS REQUESTED",small,blue);
            // Relative velocity shows momentum independently of mouse look.
            NVector2 velocity=player.Velocity;Vector2 v=new Vector2(velocity.X*Mathf.Cos(a)-velocity.Y*Mathf.Sin(a),-(velocity.X*Mathf.Sin(a)+velocity.Y*Mathf.Cos(a)));
            Vector2 origin=new Vector2(91,642);Fill(56,607,70,70,new Color(.018f,.035f,.052f,.62f));
            Line(origin,origin+Vector2.ClampMagnitude(v*3,29),blue,3);Label(24,676,185,21,"GLIDE DIRECTION",small,new Color(.08f,.20f,.26f));
        }
        void RinkMap()
        {
            const float x=1091,y=104,w=160,h=328;
            Fill(x,y,w,h,new Color(.018f,.035f,.052f,.82f));
            Line(new Vector2(x+8,y+h/2),new Vector2(x+w-8,y+h/2),new Color(.64f,.32f,.34f));
            Line(new Vector2(x+8,y+103),new Vector2(x+w-8,y+103),blue);Line(new Vector2(x+8,y+h-103),new Vector2(x+w-8,y+h-103),blue);
            Fill(x+w/2-9,y+17,18,3,orange);Fill(x+w/2-9,y+h-20,18,3,blue);
            foreach(var s in Session.Skaters)
            {
                Vector2 p=new Vector2(x+w*.5f+s.Position.X/28*(w-12),y+h*.5f-s.Position.Y/58*(h-12));
                float r=s==Session.Player?4:3;Fill(p.x-r,p.y-r,r*2,r*2,s.Team==0?blue:orange);
                if(s==Session.Player){Vector2 dir=new Vector2(Mathf.Sin(LookYaw*Mathf.Deg2Rad),-Mathf.Cos(LookYaw*Mathf.Deg2Rad));Line(p,p+dir*10,white,2);}
            }
            var puck=Session.Puck.Position;Fill(x+w*.5f+puck.X/28*(w-12)-2,y+h*.5f-puck.Y/58*(h-12)-2,4,4,Color.white);
            Label(x,y+h+5,w,23,"TAB  RINK MAP",small,new Color(.06f,.20f,.27f));
        }
        void PauseScreen()
        {
            Panel(184,151,912,493);Label(215,173,846,62,help?"READ THE ICE":"SHIFT PAUSED",title);
            Label(216,252,844,121,"WASD pushes relative to your view. Release to glide. Space brakes and Shift adds speed. Mouse look remains independent of momentum; arrow keys also turn the view.\n\nThe stick guides a nearby puck with limited force. Hold and release left mouse for a shot, or tap F. Right mouse / Q passes. Get close and face the puck first.");
            Label(216,400,844,67,"E checks within reach. R requests a teammate pass; move into a clear lane.\nBlue always attacks the orange end. C switches camera. Tab toggles the rink map.",small);
            Label(216,492,212,27,"MOUSE SENSITIVITY",small);Sensitivity=GUI.HorizontalSlider(new Rect(432,503,258,24),Sensitivity,.04f,.30f);
            invertMouse=GUI.Toggle(new Rect(750,492,255,28),invertMouse," Invert vertical look");
            if(Button(216,560,251,"RESUME")){help=false;SetPaused(false);}
            if(Button(487,560,251,"RESTART MATCH"))StartMatch(Session.Seed,Session.Practice);
            if(Button(758,560,306,"RETURN TO CABINET"))VectorCabinet.GameCollection.ReturnToMenu();
        }
        void FinalScreen()
        {
            Panel(235,151,810,503);Label(271,173,737,61,Session.BlueScore>Session.OrangeScore?"BLUE WINS":Session.BlueScore<Session.OrangeScore?"ORANGE WINS":"HONOURS EVEN",title);
            Label(272,252,735,57,$"BLUE  {Session.BlueScore}  —  {Session.OrangeScore}  ORANGE",number);
            Label(272,320,735,113,$"Shots: {Session.Stats.Shots[0]} / {Session.Stats.Shots[1]}     Saves: {Session.Stats.Saves[0]} / {Session.Stats.Saves[1]}\nPasses: {Session.Stats.Passes[0]} / {Session.Stats.Passes[1]}     Turnovers won: {Session.Stats.Turnovers[0]} / {Session.Stats.Turnovers[1]}\nMatch seed: {Session.Seed}");
            if(Button(272,470,340,"REMATCH"))StartMatch(Session.Seed,false);
            if(Button(632,470,372,"NEW MATCH BRIEFING")){SeedText=unchecked(Session.Seed+7919).ToString();ShowBriefing();}
            if(Button(272,559,732,"RETURN TO CABINET"))VectorCabinet.GameCollection.ReturnToMenu();
        }
    }
}
