using System;

namespace IceResurfacer.Core
{
    public sealed partial class SimulatorConfig
    {
        // Plan-view collision outline of the chassis: machine-space (x, z) pairs of one closed polygon,
        // all heights (the boards and glass are a full-height wall). Empty keeps the original
        // VehicleHalfWidth x (-BodyRear .. BodyFront) rectangle sampled on its 3x3 grid.
        public float[] CollisionOutline = new float[0];
        // Conditioner side plates as a collider, about the simulated conditioner centre (half width,
        // extents forward/rearward along its heading). A zero half width keeps the original behaviour,
        // in which the trailing conditioner is not a collider.
        public float ConditionerCollisionHalfWidth = 0f;
        public float ConditionerCollisionFront = .40f;
        public float ConditionerCollisionRear = .40f;
        // Board contact response (dynamics, not geometry): how fast the boards can deflect the nose about
        // the rear axle (front tyres scrub) and push the rear sideways (rear tyres scrub); anything beyond
        // these is blocked and shortens the step. Rolling and sliding friction scale the normal reaction.
        public float ContactPivotRate = .35f;
        public float ContactScrubSpeed = .45f;
        public float GuideRollerFriction = .03f;
        public float BodyContactFriction = .25f;

        public bool HasCollisionOutline { get { return CollisionOutline!=null&&CollisionOutline.Length>=6; } }

        /// <summary>Largest |x| of the collision outline and conditioner collider (the legacy rectangle when unset).</summary>
        public float CollisionHalfWidth
        {
            get
            {
                float width=HasCollisionOutline?0:VehicleHalfWidth;
                if(HasCollisionOutline)for(int i=0;i<CollisionOutline.Length;i+=2)width=Math.Max(width,Math.Abs(CollisionOutline[i]));
                return Math.Max(width,ConditionerCollisionHalfWidth);
            }
        }

        void ValidateCollisionOutline()
        {
            float[] values={ConditionerCollisionHalfWidth,ConditionerCollisionFront,ConditionerCollisionRear,ContactPivotRate,ContactScrubSpeed,GuideRollerFriction,BodyContactFriction};
            foreach(float value in values)
                if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Collision outline and contact calibration must be finite.");
            if(ConditionerCollisionHalfWidth<0||ConditionerCollisionHalfWidth>1.6f||ConditionerCollisionFront<0||ConditionerCollisionFront>1.5f||
               ConditionerCollisionRear<0||ConditionerCollisionRear>1.5f||ContactPivotRate<=0||ContactPivotRate>3||ContactScrubSpeed<0||ContactScrubSpeed>3||
               GuideRollerFriction<0||GuideRollerFriction>1||BodyContactFriction<0||BodyContactFriction>1)
                throw new ArgumentException("Invalid conditioner collider or board contact calibration.");
            var o=CollisionOutline;
            if(o==null||o.Length==0)return;
            if(o.Length%2!=0||o.Length<6||o.Length>256)throw new ArgumentException("The collision outline needs 3 to 128 (x, z) vertices.");
            double area=0;
            for(int i=0;i<o.Length;i+=2)
            {
                if(float.IsNaN(o[i])||float.IsInfinity(o[i])||float.IsNaN(o[i+1])||float.IsInfinity(o[i+1]))throw new ArgumentException("The collision outline must be finite.");
                if(Math.Abs(o[i])>1.6f||o[i+1]< -3f||o[i+1]>4f)throw new ArgumentException("The collision outline exceeds the supported machine envelope.");
                int j=(i+2)%o.Length;area+=o[i]*o[j+1]-o[j]*o[i+1];
            }
            if(Math.Abs(area)*.5<.5)throw new ArgumentException("The collision outline encloses less than 0.5 m2.");
            if(!OutlineContains(o,0,0))throw new ArgumentException("The collision outline must enclose the rear axle.");
            if(CollisionHalfWidth>=GateHalfWidth)throw new ArgumentException("The collision outline is wider than the service gate.");
        }

        /// <summary>Even-odd point-in-polygon test in machine space.</summary>
        public static bool OutlineContains(float[] o,float x,float z)
        {
            bool inside=false;
            for(int i=0,j=o.Length-2;i<o.Length;j=i,i+=2)
                if((o[i+1]>z)!=(o[j+1]>z)&&x<(o[j]-o[i])*(z-o[i+1])/(o[j+1]-o[i+1])+o[i])inside=!inside;
            return inside;
        }
    }
}
