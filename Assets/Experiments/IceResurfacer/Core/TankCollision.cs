using System;

namespace IceResurfacer.Core
{
    public sealed partial class Simulation
    {
        // The authored moving shell and opening door, not an expanded chassis.
        // Coordinates match WorldBuilder.Tank; the runtime fixture checks that
        // the transformed door corners agree. Points above 2.55 m clear glass;
        // the low pit rim never obstructs an elevated dumping outlet.
        public bool ProbeTankPose(float x,float z,float heading,float lift,out MovementContact contact)
        {
            contact=default;
            if(lift<=.001f)return true;
            if(Ice.BoardDistance(x,z,out _,out _)<-4.3f)return true;
            float angle=lift*58f*(float)Math.PI/180,ts=(float)Math.Sin(angle),tc=(float)Math.Cos(angle);
            float sn=(float)Math.Sin(heading),cs=(float)Math.Cos(heading);
            float door=-Clamp((lift-.45f)/.4f,0,1)*105f*(float)Math.PI/180;
            float ds=(float)Math.Sin(door),dc=(float)Math.Cos(door);
            // Sample each real surface on a bounded grid. With convex straight
            // board faces the extreme vertices are sufficient; subdivisions
            // also cover the concave rink/service portal and height boundary.
            for(int surface=0;surface<7;surface++)
            for(int row=0;row<=16;row++)for(int col=0;col<=12;col++)
            {
                float u=col/12f,v=row/16f,lx,ly,lz;
                if(surface==0||surface==6) // the two faces of the hinged door
                {
                    lx=.20f-.72f+1.44f*u;float dy=-1.02f+.94f*v;
                    float skin=surface==0?-.0275f:.0275f;
                    ly=1.02f+dc*dy-ds*skin;lz=.002f+ds*dy+dc*skin;
                }
                else if(surface==1) // sloping forward crown
                {
                    lx=(-.44f+.04f*v)+(1.28f-.08f*v)*u;
                    ly=1.025f-.085f*v;lz=-1.25f+1.27f*v;
                }
                else if(surface<4) // tapered lower side skins
                {
                    float side=surface==2?-1:1;
                    lx=.20f+side*(.763f-.043f*v);
                    ly=.025f+(.775f-.05f*v)*u;lz=-2.03f+2.05f*v;
                }
                else // sloped upper shoulders, including the wider lower fold
                {
                    float side=surface==4?-1:1;
                    float lowerX=.763f-.043f*v,upperX=.64f-.04f*v;
                    lx=.20f+side*(lowerX+(upperX-lowerX)*u);
                    float lowerY=.80f-.05f*v,upperY=1.025f-.085f*v;
                    ly=lowerY+(upperY-lowerY)*u;lz=-2.03f+2.05f*v;
                }
                float height=.78f+tc*ly-ts*lz,forward=2.46f+ts*ly+tc*lz;
                // Everything behind this plane remains inside the existing
                // chassis footprint; do not enlarge ordinary driving bounds.
                if(forward<=Config.BodyFront)continue;
                float px=x+cs*lx+sn*forward,pz=z-sn*lx+cs*forward;
                if(!TankPointClear(px,height,pz,x,z,heading,out contact))return false;
            }
            return true;
        }
        bool TankPointClear(float px,float py,float pz,float x,float z,float heading,out MovementContact contact)
        {
            contact=default;
            Layout.ToLocal(px,pz,out float side,out float outward);
            bool service=outward>=Layout.NeckStart&&outward<=Layout.RoomLength+2&&Math.Abs(side)<=Layout.RoomHalfWidth+2;
            if(service)
            {
                // Raised surfaces may project over the pit. Only actual room
                // walls/returns/portal lintel count at this height.
                bool floor=Layout.ContainsFloor(px,pz,.005f);
                bool ice=Ice.IsInside(px,pz);
                bool lintel=outward>=.05f&&outward<=1.55f&&Math.Abs(side)<Layout.GateHalfWidth&&py>=3.87f;
                if((floor||ice)&&!lintel)return true;
                if(py>4.72f)return true;
                if(!floor&&!ice||lintel)
                {
                    Layout.ContactNormal(px,pz,out float nx,out float nz);
                    contact=new MovementContact{Obstacle=MovementObstacle.TankWall,VehicleX=x,VehicleZ=z,Heading=heading,PointX=px,PointY=py,PointZ=pz,NormalX=nx,NormalZ=nz};
                    return false;
                }
            }
            if(py>2.55f||Ice.IsInside(px,pz))return true;
            Ice.BoardDistance(px,pz,out float bx,out float bz);
            contact=new MovementContact{Obstacle=MovementObstacle.TankGlass,VehicleX=x,VehicleZ=z,Heading=heading,PointX=px,PointY=py,PointZ=pz,NormalX=bx,NormalZ=bz};
            return false;
        }
    }
}
