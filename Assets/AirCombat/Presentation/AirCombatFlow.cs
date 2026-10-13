using UnityEngine;

namespace Meridian
{
    public sealed partial class AirCombatGame
    {
        public const float OutcomeHoldSeconds=3f;
        float aftermathRemaining;
        bool outcomeObserved;
        readonly AirViewControls viewControls=new AirViewControls();
        public bool AftermathActive => aftermathRemaining>0;
        public float AftermathRemaining => aftermathRemaining;
        public float ViewYaw => viewControls.Yaw;
        public float ViewPitch => viewControls.Pitch;

        void ResetFlightPresentation()
        {
            aftermathRemaining=0;outcomeObserved=false;
            lookingAround=false;
            viewControls.Reset();previousAircraftHits=previousAircraftKills=0;
            aircraftHitFlash=aircraftKillFlash=0;
        }
        void ObserveOutcome()
        {
            if(Mission==null||Mission.Outcome==MissionOutcome.Flying||outcomeObserved)return;
            outcomeObserved=true;aftermathRemaining=OutcomeHoldSeconds;Paused=false;accumulator=0;
            View.BeginOutcome(Mission);SetCursor();
        }
        public void SkipAftermath()
        {aftermathRemaining=0;View.SetOutcomeElapsed(OutcomeHoldSeconds);Paused=false;menuIndex=0;SetCursor();}
        public void AdvanceAftermathForDiagnostics(float dt)
        {
            ObserveOutcome();
            if(!AftermathActive||Paused||float.IsNaN(dt)||float.IsInfinity(dt))return;
            aftermathRemaining=Mathf.Max(0,aftermathRemaining-Mathf.Max(0,dt));
            View.SetOutcomeElapsed(OutcomeHoldSeconds-aftermathRemaining);
            if(!AftermathActive){menuIndex=0;SetCursor();}
        }
        void HandleAftermath(AirControlIntent intent,float dt)
        {
            if(stickBindings){if(intent.Back||intent.Pause)stickBindings=false;return;}
            if(intent.Pause||intent.Notes){SetPaused(!Paused);return;}
            if(Paused)
            {
                if(intent.Back){SetPaused(false);return;}
                if(intent.MenuMove!=0)menuIndex=(menuIndex+intent.MenuMove+3)%3;
                if(intent.Submit)
                {
                    if(menuIndex==0)SetPaused(false);
                    else if(menuIndex==1)RestartFlightMode();
                    else VectorCabinet.GameCollection.ReturnToMenu();
                }
                return;
            }
            if(intent.Submit){SkipAftermath();return;}
            AdvanceAftermathForDiagnostics(dt);
        }
        void AftermathHud()
        {
            bool won=Mission.Outcome==MissionOutcome.Victory;
            WorldLabel(384,535,650,49,won?"OBJECTIVE COMPLETE":"SORTIE LOST",number,won?green:amber);
            WorldLabel(384,577,650,46,Mission.Summary,body);
            WorldLabel(384,626,650,26,"Aftermath  "+aftermathRemaining.ToString("0.0")+"s   /   Enter or gamepad A: continue   /   Esc: pause",small);
        }
    }
}
