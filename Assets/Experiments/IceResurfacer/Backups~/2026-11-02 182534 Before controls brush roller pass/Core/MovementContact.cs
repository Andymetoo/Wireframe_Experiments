using System;

namespace IceResurfacer.Core
{
    public enum MovementObstacle { None, PitRim, ServiceWall, RinkBoards, TankGlass, TankWall }
    public struct MovementContact
    {
        public MovementObstacle Obstacle;
        public float VehicleX,VehicleZ,Heading,PointX,PointZ,NormalX,NormalZ;
        public float PointY;
        public bool GuideRoller;
        public string Reason { get { return Obstacle==MovementObstacle.PitRim?"Snow-pit rim":Obstacle==MovementObstacle.ServiceWall?"Service-room wall / doorway":Obstacle==MovementObstacle.RinkBoards?"Rink boards":Obstacle==MovementObstacle.TankGlass?"Tilted tank / door at glass":Obstacle==MovementObstacle.TankWall?"Tilted tank / door at wall":"Clear"; } }
    }
    public sealed partial class Simulation
    {
        public MovementContact LastMovementContact { get; private set; }
        public float LastMovementContactTime { get; private set; }
        public int ContactEventSequence { get; private set; }
        public float ContactNormalSpeed { get; private set; }
        public float ContactTangentialSpeed { get; private set; }
        public float ContactSeverity { get; private set; }
        public MovementContact AudibleContact { get; private set; }
        public float AudibleContactSeverity { get; private set; }
        public float AudibleContactNormalSpeed { get; private set; }
        float nextContactEventTime;

        // Contact-point velocity includes chassis rotation. The inward normal
        // separates a fast glancing brush from a slower square impact. No mass
        // or impulse is invented for this deterministic kinematic vehicle.
        void RecordContact(MovementContact hit,float vx,float vz,float angularVelocity)
        {
            float rx=hit.PointX-Vehicle.X,rz=hit.PointZ-Vehicle.Z;
            vx+=angularVelocity*rz;vz-=angularVelocity*rx;
            ContactNormalSpeed=Math.Max(0,-(vx*hit.NormalX+vz*hit.NormalZ));
            ContactTangentialSpeed=Math.Abs(vx*hit.NormalZ-vz*hit.NormalX);
            ContactSeverity=Clamp(ContactNormalSpeed/1.8f,0,1);
            if(hit.Obstacle==MovementObstacle.None||ContactNormalSpeed<.035f||Time<nextContactEventTime)return;
            AudibleContact=hit;AudibleContactSeverity=ContactSeverity;AudibleContactNormalSpeed=ContactNormalSpeed;ContactEventSequence++;
            nextContactEventTime=Time+(hit.GuideRoller?.28f:.40f);
        }

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
            float rx=x+cs*Config.GuideRollerX+sn*Config.GuideRollerZ;
            float rz=z-sn*Config.GuideRollerX+cs*Config.GuideRollerZ;
            for(int i=0;i<8;i++)
            {
                float a=i*(float)Math.PI*.25f;
                if(!ProbePoint(rx+(float)Math.Cos(a)*Config.GuideRollerRadius,rz+(float)Math.Sin(a)*Config.GuideRollerRadius,x,z,heading,out contact)){contact.GuideRoller=true;contact.PointY=.26f;return false;}
            }
            Ice.BoardDistance(rx,rz,out float nx,out float nz);
            bool clear=ProbePoint(rx-nx*Config.GuideRollerRadius,rz-nz*Config.GuideRollerRadius,x,z,heading,out contact);
            if(!clear){contact.GuideRoller=true;contact.PointY=.26f;}return clear;
        }
        bool ProbePoint(float px,float pz,float x,float z,float heading,out MovementContact contact)
        {
            contact=default;
            if(Navigable(px,pz))return true;
            Layout.ToLocal(px,pz,out float side,out float outward);
            bool service=(outward>=Layout.RoomStart-.02f||!Ice.IsInside(x,z))&&outward>Layout.NeckStart&&Math.Abs(side)<Layout.RoomHalfWidth+3&&outward<Layout.RoomLength+3;
            MovementObstacle obstacle=service?MovementObstacle.ServiceWall:MovementObstacle.RinkBoards;
            float nx,nz;
            if(service)Layout.ContactNormal(px,pz,out nx,out nz);
            else Ice.BoardDistance(px,pz,out nx,out nz);
            contact=new MovementContact{Obstacle=obstacle,VehicleX=x,VehicleZ=z,Heading=heading,PointX=px,PointZ=pz,NormalX=nx,NormalZ=nz};
            return false;
        }
    }
}
