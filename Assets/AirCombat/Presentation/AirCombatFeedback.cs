using UnityEngine;

namespace Meridian
{
    public sealed partial class AirCombatGame
    {
        int previousAircraftHits,previousAircraftKills;
        float aircraftHitFlash,aircraftKillFlash;
        public bool AircraftHitVisible => aircraftHitFlash>0;
        public bool AircraftKillVisible => aircraftKillFlash>0;

        void UpdateAircraftFeedback(float dt)
        {
            if(Mission.PlayerAircraftHits>previousAircraftHits)aircraftHitFlash=.28f;
            if(Mission.PlayerAircraftKills>previousAircraftKills)aircraftKillFlash=.85f;
            previousAircraftHits=Mission.PlayerAircraftHits;previousAircraftKills=Mission.PlayerAircraftKills;
            if(!Paused){aircraftHitFlash=Mathf.Max(0,aircraftHitFlash-dt);aircraftKillFlash=Mathf.Max(0,aircraftKillFlash-dt);}
        }
        void Gunsight(Vector2 center)
        {
            Color white=new Color(.95f,1f,.88f);
            Line(center+new Vector2(-19,0),center+new Vector2(-7,0),white,2.3f);
            Line(center+new Vector2(7,0),center+new Vector2(19,0),white,2.3f);
            Line(center+new Vector2(0,-19),center+new Vector2(0,-7),white,2.3f);
            Fill(center.x-2.5f,center.y-2.5f,5,5,Color.black);
            Fill(center.x-1,center.y-1,2,2,white);
            if(aircraftHitFlash<=0&&aircraftKillFlash<=0)return;
            Color confirmation=aircraftKillFlash>0?amber:Color.white;
            float outer=aircraftKillFlash>0?14:12;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
                Line(center+new Vector2(x*7,y*7),center+new Vector2(x*outer,y*outer),confirmation,2.4f);
            if(aircraftKillFlash>0)WorldLabel(center.x+28,center.y+21,180,23,"AIRCRAFT DESTROYED",small,amber);
        }
    }
}
