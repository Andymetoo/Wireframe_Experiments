using System;

namespace IceResurfacer.Core
{
    public enum MovementObstacle { None, PitRim, ServiceWall, RinkBoards, TankGlass, TankWall }
    public struct MovementContact
    {
        public MovementObstacle Obstacle;
        public float VehicleX,VehicleZ,Heading,PointX,PointZ,NormalX,NormalZ;
        public float PointY;
        public string Reason { get { return Obstacle==MovementObstacle.PitRim?"Snow-pit rim":Obstacle==MovementObstacle.ServiceWall?"Service-room wall / doorway":Obstacle==MovementObstacle.RinkBoards?"Rink boards":Obstacle==MovementObstacle.TankGlass?"Tilted tank / door at glass":Obstacle==MovementObstacle.TankWall?"Tilted tank / door at wall":"Clear"; } }
    }
    public sealed partial class Simulation
    {
        public MovementContact LastMovementContact { get; private set; }
        public float LastMovementContactTime { get; private set; }

        public bool ProbeVehiclePose(float x,float z,float heading,out MovementContact contact)
        {
            contact=default;
            if(TankLift>.001f&&!ProbeTankPose(x,z,heading,TankLift,out contact))return false;
            float sn=(float)Math.Sin(heading),cs=(float)Math.Cos(heading);
            for(int row=0;row<3;row++)for(int col=0;col<3;col++)
            {
                float lx=(col-1)*Config.VehicleHalfWidth;
                float lz=-Config.BodyRear+(Config.BodyFront+Config.BodyRear)*row*.5f;
                if(!ProbePoint(x+cs*lx+sn*lz,z-sn*lx+cs*lz,x,z,heading,out contact))return false;
            }
            Layout.ToLocal(x,z,out float side,out float outward);
            if(Math.Abs(side-Layout.PitSide)<Layout.PitHalfWidth+3&&Math.Abs(outward-Layout.PitOutward)<Layout.PitHalfLength+3)
            {
                // A shallow angled edge can cross a rectangular pit corner
                // between the established nine body samples. Test those four
                // edges exactly without changing the rink/board footprint.
                for(int i=0;i<4;i++)
                {
                    int j=(i+1)%4;
                    float ax=(i==0||i==3)?-Config.VehicleHalfWidth:Config.VehicleHalfWidth,az=i<2?-Config.BodyRear:Config.BodyFront;
                    float bx=(j==0||j==3)?-Config.VehicleHalfWidth:Config.VehicleHalfWidth,bz=j<2?-Config.BodyRear:Config.BodyFront;
                    if(Layout.SegmentIntersectsPit(x+cs*ax+sn*az,z-sn*ax+cs*az,x+cs*bx+sn*bz,z-sn*bx+cs*bz,out float px,out float pz))
                    {
                        Layout.PitNormal(px,pz,out float hitNx,out float hitNz);
                        contact=new MovementContact{Obstacle=MovementObstacle.PitRim,VehicleX=x,VehicleZ=z,Heading=heading,PointX=px,PointZ=pz,NormalX=hitNx,NormalZ=hitNz};
                        return false;
                    }
                }
            }
            float rx=x+cs*Config.GuideRollerX+sn*Config.GuideRollerZ;
            float rz=z-sn*Config.GuideRollerX+cs*Config.GuideRollerZ;
            for(int i=0;i<8;i++)
            {
                float a=i*(float)Math.PI*.25f;
                if(!ProbePoint(rx+(float)Math.Cos(a)*Config.GuideRollerRadius,rz+(float)Math.Sin(a)*Config.GuideRollerRadius,x,z,heading,out contact))return false;
            }
            Ice.BoardDistance(rx,rz,out float nx,out float nz);
            return ProbePoint(rx-nx*Config.GuideRollerRadius,rz-nz*Config.GuideRollerRadius,x,z,heading,out contact);
        }
        bool ProbePoint(float px,float pz,float x,float z,float heading,out MovementContact contact)
        {
            contact=default;
            if(Navigable(px,pz))return true;
            Layout.ToLocal(px,pz,out float side,out float outward);
            bool service=(outward>=Layout.RoomStart-.02f||!Ice.IsInside(x,z))&&outward>Layout.NeckStart&&Math.Abs(side)<Layout.RoomHalfWidth+3&&outward<Layout.RoomLength+3;
            MovementObstacle obstacle=service?(Layout.ContainsPit(px,pz,ServiceLayout.PitCollisionClearance)?MovementObstacle.PitRim:MovementObstacle.ServiceWall):MovementObstacle.RinkBoards;
            float nx,nz;
            if(service)Layout.ContactNormal(px,pz,out nx,out nz);
            else Ice.BoardDistance(px,pz,out nx,out nz);
            contact=new MovementContact{Obstacle=obstacle,VehicleX=x,VehicleZ=z,Heading=heading,PointX=px,PointZ=pz,NormalX=nx,NormalZ=nz};
            return false;
        }
    }
}
