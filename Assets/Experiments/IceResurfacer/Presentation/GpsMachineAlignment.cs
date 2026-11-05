using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>Machine-relative axle sight line, continued only across its visible silhouette.</summary>
    public sealed class GpsMachineAlignment
    {
        public const int Intervals=64;
        public int LastVisibleSegments {get;private set;}
        public int LastRayQueries {get;private set;}
        public void Draw(IceResurfacerGame game,RouteSilhouetteContinuation visibility,float width,float height)
        {Visit(game,visibility,width,height,true);}
        public void Measure(IceResurfacerGame game,RouteSilhouetteContinuation visibility,float width,float height)
        {Visit(game,visibility,width,height,false);}
        void Visit(IceResurfacerGame game,RouteSilhouetteContinuation visibility,float width,float height,bool draw)
        {
            LastVisibleSegments=LastRayQueries=0;
            if(game==null||game.RouteGuide==null||!game.RouteGuide.GuidanceDisplayed||game.ChaseActive||visibility==null)return;
            var camera=game.DriverCamera;var machine=game.World.Machine;
            float length=game.Sim.Config.BodyFront+GpsGuidanceGeometry.HeadingBeyondFrontMeters;
            float scale=Mathf.Min(width/1600,height/900);
            Vector2 offset=new Vector2((width-1600*scale)*.5f,(height-900*scale)*.5f);
            bool previous=false;Vector2 last=default;
            for(int i=0;i<=Intervals;i++)
            {
                // This is the same rear-axle reference plane as the route. A stripe
                // painted on top of the bin would appear offset from the track as
                // the operator moved their head. The local sight line stays true.
                Vector3 point=machine.TransformPoint(0,.052f,length*i/Intervals);
                Vector3 screen=camera.WorldToViewportPoint(point);
                bool inView=screen.z>.1f&&screen.x>0&&screen.x<1&&screen.y>0&&screen.y<1;
                bool visible=false;
                if(inView){LastRayQueries++;visible=visibility.MachineOccludes(camera,point);}
                Vector2 at=(new Vector2(screen.x*width,(1-screen.y)*height)-offset)/scale;
                if(visible&&previous&&Vector2.Distance(last,at)<40)
                {
                    if(draw)
                    {
                        Stroke(last,at,4.5f,new Color(.09f,.075f,.015f,.9f));
                        Stroke(last,at,2.1f,new Color(1,.83f,.035f,1));
                    }
                    LastVisibleSegments++;
                }
                last=at;previous=visible;
            }
        }
        static void Stroke(Vector2 a,Vector2 b,float width,Color color)
        {
            Matrix4x4 matrix=GUI.matrix;Color prior=GUI.color;
            GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
            GUI.color=color;GUI.DrawTexture(new Rect(0,-width*.5f,Vector2.Distance(a,b),width),Texture2D.whiteTexture);
            GUI.color=prior;GUI.matrix=matrix;
        }
    }
}
