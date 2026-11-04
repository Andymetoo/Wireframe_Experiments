using System;
using System.Collections.Generic;

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
        /// <summary>Fraction of the commanded step the boards allowed on the last contact (1 = slid freely).</summary>
        public float LastContactAllowedFraction { get; private set; } = 1;
        float nextContactEventTime;
        // Contact on the previous step: a step that keeps sliding along the boards is not a new impact.
        bool sustainedContact;
        const float SlidingImpactSpeed=.25f;

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
            // Sliding or rolling along the boards is not a new impact; a fresh hit or a real bump is.
            bool sliding=sustainedContact&&ContactNormalSpeed<SlidingImpactSpeed;
            if(hit.Obstacle==MovementObstacle.None||ContactNormalSpeed<.035f||sliding||Time<nextContactEventTime)return;
            AudibleContact=hit;AudibleContactSeverity=ContactSeverity;AudibleContactNormalSpeed=ContactNormalSpeed;ContactEventSequence++;
            nextContactEventTime=Time+(hit.GuideRoller?.28f:.40f);
        }

        // ---- Collision samples (machine space) ---------------------------------------------------
        // The original body is a 3x3 grid on the VehicleHalfWidth rectangle. A calibrated outline is
        // sampled at its vertices and at most every 10 cm along its edges, like the conditioner collider.
        // The rink interior is convex, so vertices bound the boards; edge samples cover the gate jambs.

        const float SampleSpacing=.10f, NoseLever=.50f;
        float[] sampleX,sampleZ;
        int collisionSignature;

        public int CollisionSampleCount { get { EnsureCollisionSamples(); return sampleX.Length; } }
        public void CollisionSample(int index,out float x,out float z) { EnsureCollisionSamples(); x=sampleX[index]; z=sampleZ[index]; }

        /// <summary>Chassis collision polygon (machine-space x, z pairs): the calibrated outline or the legacy rectangle.</summary>
        public float[] ChassisCollisionPolygon()
        {
            if(Config.HasCollisionOutline)return (float[])Config.CollisionOutline.Clone();
            float w=Config.VehicleHalfWidth;
            return new[]{-w,-Config.BodyRear,-w,Config.BodyFront,w,Config.BodyFront,w,-Config.BodyRear};
        }

        /// <summary>Conditioner collider (machine-space x, z pairs) at the simulated hitch; empty when not a collider.</summary>
        public float[] ConditionerCollisionPolygon()
        {
            float w=Config.ConditionerCollisionHalfWidth;
            if(w<=0)return new float[0];
            float centre=-(Config.HitchBehindAxle+Config.DrawbarLength),front=centre+Config.ConditionerCollisionFront,rear=centre-Config.ConditionerCollisionRear;
            return new[]{-w,rear,-w,front,w,front,w,rear};
        }

        void EnsureCollisionSamples()
        {
            int signature=CollisionSignature();
            if(sampleX!=null&&signature==collisionSignature)return;
            collisionSignature=signature;
            var xs=new List<float>();var zs=new List<float>();
            if(!Config.HasCollisionOutline)
                for(int row=0;row<3;row++)for(int col=0;col<3;col++)
                { xs.Add((col-1)*Config.VehicleHalfWidth); zs.Add(-Config.BodyRear+(Config.BodyFront+Config.BodyRear)*row*.5f); }
            else AddLoop(xs,zs,Config.CollisionOutline);
            AddLoop(xs,zs,ConditionerCollisionPolygon());
            sampleX=xs.ToArray();sampleZ=zs.ToArray();
        }

        static void AddLoop(List<float> xs,List<float> zs,float[] loop)
        {
            for(int i=0;i+1<loop.Length;i+=2)
            {
                int j=(i+2)%loop.Length;
                float ax=loop[i],az=loop[i+1],bx=loop[j],bz=loop[j+1];
                int steps=Math.Max(1,(int)Math.Ceiling(Math.Sqrt((bx-ax)*(bx-ax)+(bz-az)*(bz-az))/SampleSpacing));
                for(int k=0;k<steps;k++){ xs.Add(ax+(bx-ax)*k/steps); zs.Add(az+(bz-az)*k/steps); }
            }
        }

        int CollisionSignature()
        {
            unchecked
            {
                var c=Config;int h=17;
                h=h*31+c.VehicleHalfWidth.GetHashCode();h=h*31+c.BodyFront.GetHashCode();h=h*31+c.BodyRear.GetHashCode();
                h=h*31+c.ConditionerCollisionHalfWidth.GetHashCode();h=h*31+c.ConditionerCollisionFront.GetHashCode();h=h*31+c.ConditionerCollisionRear.GetHashCode();
                h=h*31+c.HitchBehindAxle.GetHashCode();h=h*31+c.DrawbarLength.GetHashCode();
                var o=c.CollisionOutline;
                if(o!=null){h=h*31+o.Length;for(int i=0;i<o.Length;i++)h=h*31+o[i].GetHashCode();}
                return h;
            }
        }

        public bool ProbeVehiclePose(float x,float z,float heading,out MovementContact contact)
        {
            contact=default;
            if(TankLift>.001f&&!ProbeTankPose(x,z,heading,TankLift,out contact))return false;
            EnsureCollisionSamples();
            float sn=(float)Math.Sin(heading),cs=(float)Math.Cos(heading);
            for(int i=0;i<sampleX.Length;i++)
            {
                float lx=sampleX[i],lz=sampleZ[i];
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
            bool service=ServiceObstacle(px,pz,x,z);
            MovementObstacle obstacle=service?MovementObstacle.ServiceWall:MovementObstacle.RinkBoards;
            float nx,nz;
            if(service)Layout.ContactNormal(px,pz,out nx,out nz);
            else Ice.BoardDistance(px,pz,out nx,out nz);
            contact=new MovementContact{Obstacle=obstacle,VehicleX=x,VehicleZ=z,Heading=heading,PointX=px,PointZ=pz,NormalX=nx,NormalZ=nz};
            return false;
        }
        bool ServiceObstacle(float px,float pz,float x,float z)
        {
            Layout.ToLocal(px,pz,out float side,out float outward);
            return (outward>=Layout.RoomStart-.02f||!Ice.IsInside(x,z))&&outward>Layout.NeckStart&&Math.Abs(side)<Layout.RoomHalfWidth+3&&outward<Layout.RoomLength+3;
        }

        // ---- Contact response --------------------------------------------------------------------

        struct ContactSample
        {
            public float Depth,NormalX,NormalZ,LocalX,LocalZ,PointX,PointZ;
            public bool Roller,Tank;
            public MovementObstacle Obstacle;
        }

        /// <summary>How far a non-navigable point lies past the navigable boundary, with its inward normal.</summary>
        float PointPenetration(float px,float pz,float x,float z,out float nx,out float nz,out MovementObstacle obstacle)
        {
            nx=nz=0;obstacle=MovementObstacle.None;
            if(Navigable(px,pz))return 0;
            float depth;
            if(ServiceObstacle(px,pz,x,z)){Layout.ContactNormal(px,pz,out nx,out nz);depth=Layout.FloorPenetration(px,pz);obstacle=MovementObstacle.ServiceWall;}
            else{depth=Ice.BoardDistance(px,pz,out nx,out nz)+.02f;obstacle=MovementObstacle.RinkBoards;}
            return Math.Max(depth,.0005f);
        }

        /// <summary>The deepest rejected sample of a pose (exactly the points ProbeVehiclePose tests); false when clear.</summary>
        bool DeepestPenetration(float x,float z,float heading,out ContactSample deepest)
        {
            deepest=default;
            if(TankLift>.001f&&!ProbeTankPose(x,z,heading,TankLift,out var tank))
            { deepest=new ContactSample{Tank=true,Depth=.01f,NormalX=tank.NormalX,NormalZ=tank.NormalZ,PointX=tank.PointX,PointZ=tank.PointZ,Obstacle=tank.Obstacle};return true; }
            EnsureCollisionSamples();
            float sn=(float)Math.Sin(heading),cs=(float)Math.Cos(heading);
            bool any=false;
            for(int i=0;i<sampleX.Length;i++)Consider(x,z,sn,cs,x+cs*sampleX[i]+sn*sampleZ[i],z-sn*sampleX[i]+cs*sampleZ[i],false,ref any,ref deepest);
            float rx=x+cs*Config.GuideRollerX+sn*Config.GuideRollerZ,rz=z-sn*Config.GuideRollerX+cs*Config.GuideRollerZ;
            for(int i=0;i<8;i++){float a=i*(float)Math.PI*.25f;Consider(x,z,sn,cs,rx+(float)Math.Cos(a)*Config.GuideRollerRadius,rz+(float)Math.Sin(a)*Config.GuideRollerRadius,true,ref any,ref deepest);}
            Ice.BoardDistance(rx,rz,out float bx,out float bz);
            Consider(x,z,sn,cs,rx-bx*Config.GuideRollerRadius,rz-bz*Config.GuideRollerRadius,true,ref any,ref deepest);
            return any;
        }
        void Consider(float x,float z,float sn,float cs,float px,float pz,bool roller,ref bool any,ref ContactSample best)
        {
            float d=PointPenetration(px,pz,x,z,out float nx,out float nz,out var obstacle);
            if(d<=0||any&&d<=best.Depth)return;
            float dx=px-x,dz=pz-z;
            best=new ContactSample{Depth=d,NormalX=nx,NormalZ=nz,LocalX=cs*dx-sn*dz,LocalZ=sn*dx+cs*dz,PointX=px,PointZ=pz,Roller=roller,Obstacle=obstacle};any=true;
        }

        /// <summary>
        /// A rejected step against boards or walls. Contacts ahead of the rear axle (guide roller, nose)
        /// pivot the chassis about the rear axle, as the front tyres scrub; contacts at or behind it
        /// (rear corners, conditioner) push the rear sideways along the boards, as the rear tyres scrub.
        /// Only the inward motion is removed. What the tyres cannot absorb within ContactPivotRate and
        /// ContactScrubSpeed is blocked, so the step shortens: a square impact stops, a glance slides.
        /// Every returned pose passes ProbeVehiclePose. Returns the fraction of the commanded step made.
        /// </summary>
        float ResolveContactStep(float dt,float x1,float z1,float h1,out float fx,out float fz,out float fh,out float correction,out bool rollerOnly)
        {
            float x0=Vehicle.X,z0=Vehicle.Z,h0=Vehicle.Heading;
            if(TryResolve(x0,z0,h0,x1,z1,h1,1,dt,out fx,out fz,out fh,out correction,out rollerOnly))return 1;
            fx=x0;fz=z0;fh=h0;correction=0;
            float lo=0,hi=1;
            for(int i=0;i<8;i++)
            {
                float mid=(lo+hi)*.5f;
                if(TryResolve(x0,z0,h0,x1,z1,h1,mid,dt,out float ax,out float az,out float ah,out float ac,out bool ar)){lo=mid;fx=ax;fz=az;fh=ah;correction=ac;rollerOnly=ar;}
                else hi=mid;
            }
            return lo;
        }

        bool TryResolve(float x0,float z0,float h0,float x1,float z1,float h1,float fraction,float dt,
            out float qx,out float qz,out float qh,out float correction,out bool rollerOnly)
        {
            qx=x0+(x1-x0)*fraction;qz=z0+(z1-z0)*fraction;qh=Wrap(h0+Wrap(h1-h0)*fraction);
            correction=0;rollerOnly=true;
            float yawBudget=Config.ContactPivotRate*dt,slipBudget=Config.ContactScrubSpeed*dt,yaw=0,slip=0;
            for(int k=0;k<10;k++)
            {
                if(!DeepestPenetration(qx,qz,qh,out var c))return true;
                if(c.Tank)return false;
                rollerOnly&=c.Roller;
                float sn=(float)Math.Sin(qh),cs=(float)Math.Cos(qh),need=c.Depth+.0003f;
                if(c.LocalZ>NoseLever)
                {
                    // Inward motion of the contact point per radian of yaw about the rear axle.
                    float g=(-sn*c.LocalX+cs*c.LocalZ)*c.NormalX+(-cs*c.LocalX-sn*c.LocalZ)*c.NormalZ;
                    if(Math.Abs(g)<.25f)return false;
                    float dh=need/g;yaw+=Math.Abs(dh);
                    if(yaw>yawBudget)return false;
                    qh=Wrap(qh+dh);
                }
                else
                {
                    // Machine +X (right) along the inward normal: sideways scrub, never a push along the heading.
                    float g=cs*c.NormalX-sn*c.NormalZ;
                    if(Math.Abs(g)<.30f)return false;
                    float d=need/g;slip+=Math.Abs(d);
                    if(slip>slipBudget)return false;
                    qx+=cs*d;qz-=sn*d;
                }
                correction+=need;
            }
            return false;
        }

        /// <summary>The contact at a rejected pose for diagnostics and audio (deepest sample).</summary>
        MovementContact DeepestContact(float x,float z,float heading,MovementContact fallback)
        {
            if(!DeepestPenetration(x,z,heading,out var c))return fallback;
            if(c.Tank)return fallback;
            return new MovementContact{Obstacle=c.Obstacle,VehicleX=x,VehicleZ=z,Heading=heading,PointX=c.PointX,PointZ=c.PointZ,
                NormalX=c.NormalX,NormalZ=c.NormalZ,GuideRoller=c.Roller,PointY=c.Roller?.26f:0};
        }
    }
}
