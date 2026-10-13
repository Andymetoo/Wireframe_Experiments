using UnityEngine;
using NVector3=System.Numerics.Vector3;

namespace Meridian
{
    public sealed partial class AirCombatGame
    {
        readonly Rect[] objectiveLabelRects=new Rect[24];
        int objectiveLabelCount;
        public int ActiveGroundObjectiveMarkers
        {
            get {int n=0;if(Mission!=null)foreach(var g in Mission.GroundTargets)if(g.Alive&&g.IsObjective)n++;return n;}
        }
        void Diamond(Vector2 p,Color color,float radius=6)
        {
            Line(p+Vector2.up*radius,p+Vector2.right*radius,color,1.8f);
            Line(p+Vector2.right*radius,p+Vector2.down*radius,color,1.8f);
            Line(p+Vector2.down*radius,p+Vector2.left*radius,color,1.8f);
            Line(p+Vector2.left*radius,p+Vector2.up*radius,color,1.8f);
        }
        void MissionObjectiveMarkers()
        {
            objectiveLabelCount=0;
            int index=0;
            foreach(var target in Mission.GroundTargets)
            {
                if(!target.IsObjective)continue;index++;
                if(!target.Alive)continue;
                ObjectiveMarker(target.Position,"V"+index,target.Id==Mission.SelectedTargetId,amber);
            }
            if(Mission.Kind==MissionKind.Interception)
                foreach(var raider in Mission.Raiders)
                    if(raider.Alive)ObjectiveMarker(raider.Position,raider.Callsign,raider.Id==Mission.SelectedTargetId,amber);
            if(Mission.Kind==MissionKind.AceHunt)
                foreach(var plane in Mission.Aircraft)
                    if(plane.Alive&&plane.Id==Mission.AceId)ObjectiveMarker(plane.Position,"ACE / Voss",plane.Id==Mission.SelectedTargetId,amber);
            if(Mission.Kind==MissionKind.FleetBattle&&NVector3.DistanceSquared(Mission.Player.Position,Mission.ObjectivePosition)>3500f*3500f)
                ObjectiveMarker(Mission.ObjectivePosition,"SECTOR",false,green);
        }
        bool HasObjectiveAircraftMarker(int id)
        {
            if(Mission.Kind==MissionKind.AceHunt&&id==Mission.AceId)return true;
            if(Mission.Kind==MissionKind.Interception)
                foreach(var plane in Mission.Raiders)if(plane.Id==id)return true;
            return false;
        }
        void ObjectiveMarker(NVector3 position,string id,bool selected,Color color)
        {
            float distance=NVector3.Distance(position,Mission.Player.Position);
            bool ahead;Vector2 projected=Project(position,out ahead),point=projected;
            bool clear=Visible(position+NVector3.UnitY*6);
            bool onscreen=ahead&&clear&&point.x>337&&point.x<1022&&point.y>138&&point.y<548;
            string suffix="";
            if(!onscreen)
            {
                Vector3 local=View.ViewCamera.transform.InverseTransformPoint(AirRenderer.World(position));
                Vector2 direction=new Vector2(local.x,-local.y);
                if(local.z<0)direction.y=Mathf.Abs(direction.y)+300;
                if(direction.sqrMagnitude<1)direction=Vector2.down;
                direction.Normalize();
                Vector2 center=new Vector2(676,344);
                float edge=Mathf.Min(336/Mathf.Max(.001f,Mathf.Abs(direction.x)),190/Mathf.Max(.001f,Mathf.Abs(direction.y)));
                point=center+direction*edge;
                Vector2 across=new Vector2(-direction.y,direction.x);
                Line(point-direction*9+across*5,point,color,2);
                Line(point-direction*9-across*5,point,color,2);
                suffix=!clear?" / RIDGE":local.z<0?" / BEHIND":"";
            }
            else Diamond(point,color,selected?9:5);
            string text=id+" "+(distance<1000?distance.ToString("0")+"m":(distance/1000).ToString("0.0")+"km")+suffix;
            worldLabelContent.text=text;
            float width=Mathf.Min(205,small.CalcSize(worldLabelContent).x+7);
            var rect=new Rect(Mathf.Clamp(point.x+12,338,1020-width),Mathf.Clamp(point.y-10,125,556),width,21);
            // All known objectives retain a marker. Separate close labels instead of hiding vehicles.
            float preferredY=rect.y;
            for(int attempt=0;attempt<17;attempt++)
            {
                int step=(attempt+1)/2;
                rect.y=Mathf.Clamp(preferredY+(attempt%2==1?-step:step)*23,125,556);
                bool overlap=false;
                for(int i=0;i<objectiveLabelCount;i++)if(objectiveLabelRects[i].Overlaps(rect)){overlap=true;break;}
                if(!overlap)break;
            }
            if(objectiveLabelCount<objectiveLabelRects.Length)objectiveLabelRects[objectiveLabelCount++]=rect;
            if(Vector2.Distance(rect.position,point)>36)Line(point,new Vector2(rect.x,rect.y+9),new Color(color.r,color.g,color.b,.55f),1);
            WorldLabel(rect.x,rect.y,rect.width,rect.height,text,small,color);
        }
        void ObjectiveRadar(Vector2 center,float heading)
        {
            foreach(var target in Mission.GroundTargets)
            {
                if(!target.IsObjective||!target.Alive)continue;
                Vector2 offset=RadarOffset(target.Position,center,heading,out bool outside);
                Diamond(offset,amber,outside?3:4);
            }
            if(Mission.Kind==MissionKind.Interception)
                foreach(var raider in Mission.Raiders)if(raider.Alive)
                {Vector2 point=RadarOffset(raider.Position,center,heading,out bool outside);Diamond(point,amber,outside?4:5);}
            if(Mission.Kind==MissionKind.AceHunt)
                foreach(var plane in Mission.Aircraft)if(plane.Alive&&plane.Id==Mission.AceId)
                {Vector2 point=RadarOffset(plane.Position,center,heading,out bool outside);Diamond(point,amber,outside?4:5);}
            if(Mission.Kind==MissionKind.FleetBattle)
            {Vector2 point=RadarOffset(Mission.ObjectivePosition,center,heading,out bool outside);Diamond(point,green,4);}
            if(Mission.Bombers.Count>0)
            {Vector2 point=RadarOffset(Mission.ObjectivePosition,center,heading,out bool outside);Diamond(point,new Color(.66f,.87f,1),4);}
        }
        Vector2 RadarOffset(NVector3 position,Vector2 center,float heading,out bool outside)
        {
            var d=position-Mission.Player.Position;
            Vector2 offset=new Vector2(d.X*Mathf.Cos(heading)-d.Z*Mathf.Sin(heading),-(d.X*Mathf.Sin(heading)+d.Z*Mathf.Cos(heading)))/4000*68;
            outside=offset.sqrMagnitude>68*68;
            return center+Vector2.ClampMagnitude(offset,68);
        }
    }
}
