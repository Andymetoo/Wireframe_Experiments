using System;

namespace IceResurfacer.Core
{
    /// <summary>One coordinate authority for the southeast diagonal gate and backstage room.
    /// Local Side points northeast; Outward points away from the ice toward the snow pit.</summary>
    public sealed class ServiceLayout
    {
        public const float Axis = .7071067811865475f;
        public readonly float GateX, GateZ, GateHalfWidth, RoomHalfWidth = 5.5f, RoomLength;
        // The unloading zone is a load-bearing grate, flush with the floor.
        // ContainsPit identifies the snow receiver, never a movement exclusion.
        public const float PitCollisionClearance = 0f, PitRimWidth = .10f, PitRimHeight = .005f;
        public readonly float RoomStart = 1.6f, NeckStart = -1.1f;
        public readonly float PitSide = 0f, PitOutward, PitHalfWidth = 2f, PitHalfLength = 1.2f;
        public readonly float ParkSide = 0f, ParkOutward = 5.8f;
        // Machine faces into the room. Its fixed right-side fillers face the
        // negative-Side wall, leaving a straight reverse route onto the ice.
        public const float SupplySide = -4.95f, SupplyOutward = 6f;
        public const float HolderSide = -4.90f, HolderOutward = 5.52f;
        public const float DrainSide = -4.10f, DrainOutward = 4.65f;
        public float InwardHeading { get { return -(float)Math.PI * .25f; } }
        public float OutwardHeading { get { return (float)Math.PI * .75f; } }
        public ServiceLayout(SimulatorConfig config)
        {
            GateX = config.RinkWidth * .5f - config.CornerRadius + config.CornerRadius * Axis;
            GateZ = -config.RinkLength * .5f + config.CornerRadius - config.CornerRadius * Axis;
            GateHalfWidth = config.GateHalfWidth;
            RoomLength = config.BayLength;
            PitOutward = RoomLength - 2f;
        }
        public void ToWorld(float side, float outward, out float x, out float z)
        { x = GateX + Axis * (side + outward); z = GateZ + Axis * (side - outward); }
        public void ToLocal(float x, float z, out float side, out float outward)
        { float dx = x - GateX, dz = z - GateZ; side = Axis * (dx + dz); outward = Axis * (dx - dz); }
        public bool IsPortal(float x, float z)
        {
            ToLocal(x,z,out float side,out float outward);
            return Math.Abs(side) <= GateHalfWidth && outward >= NeckStart && outward <= RoomStart + .2f;
        }
        public bool ContainsFloor(float x, float z, float margin = 0)
        {
            ToLocal(x,z,out float side,out float outward);
            bool neck = Math.Abs(side) <= GateHalfWidth - margin && outward >= NeckStart && outward <= RoomStart + margin;
            bool room = Math.Abs(side) <= RoomHalfWidth - margin && outward >= RoomStart + margin && outward <= RoomLength - margin;
            return neck || room;
        }
        public bool ContainsPit(float x, float z, float margin = 0)
        {
            ToLocal(x,z,out float side,out float outward);
            return Math.Abs(side-PitSide) <= PitHalfWidth + margin && Math.Abs(outward-PitOutward) <= PitHalfLength + margin;
        }
        public bool ContainsVehicleFloor(float x,float z,float margin=.02f)
        { return ContainsFloor(x,z,margin); }
        public bool InsideRoom(float x,float z)
        { ToLocal(x,z,out float side,out float outward); return Math.Abs(side)<RoomHalfWidth && outward>RoomStart+.1f && outward<RoomLength; }
        public void ParkingPose(out float x,out float z,out float heading)
        { ToWorld(ParkSide,ParkOutward,out x,out z); heading=OutwardHeading; }
        public void SupplyAnchor(out float x,out float z) { ToWorld(SupplySide,SupplyOutward,out x,out z); }
        public void HoseHolder(out float x,out float z) { ToWorld(HolderSide,HolderOutward,out x,out z); }
        public void FloorDrain(out float x,out float z) { ToWorld(DrainSide,DrainOutward,out x,out z); }
        public void PitNormal(float x,float z,out float nx,out float nz)
        {
            ToLocal(x,z,out float side,out float outward);
            float dx=side-PitSide,dz=outward-PitOutward;
            float ns=0,no=0;
            if(Math.Abs(dx)-PitHalfWidth>Math.Abs(dz)-PitHalfLength)ns=dx<0?-1:1;
            else no=dz<0?-1:1;
            nx=Axis*(ns+no);nz=Axis*(ns-no);
        }
        public bool SegmentIntersectsPit(float ax,float az,float bx,float bz,out float x,out float z)
        {
            ToLocal(ax,az,out float sa,out float oa);ToLocal(bx,bz,out float sb,out float ob);
            float low=0,high=1;
            bool hit=Slab(sa,sb-sa,PitSide-PitHalfWidth-PitCollisionClearance,PitSide+PitHalfWidth+PitCollisionClearance,ref low,ref high)&&
                Slab(oa,ob-oa,PitOutward-PitHalfLength-PitCollisionClearance,PitOutward+PitHalfLength+PitCollisionClearance,ref low,ref high);
            x=ax+(bx-ax)*low;z=az+(bz-az)*low;return hit;
        }
        static bool Slab(float origin,float direction,float min,float max,ref float low,ref float high)
        {
            if(Math.Abs(direction)<.000001f)return origin>=min&&origin<=max;
            float a=(min-origin)/direction,b=(max-origin)/direction;if(a>b){float t=a;a=b;b=t;}
            low=Math.Max(low,a);high=Math.Min(high,b);return high>=low;
        }
        public void ContactNormal(float x,float z,out float nx,out float nz)
        {
            ToLocal(x,z,out float side,out float outward);
            // This receives the rejected footprint point, not the vehicle centre.
            float sideClear=(outward<RoomStart?GateHalfWidth:RoomHalfWidth)-Math.Abs(side);
            float endClear=RoomLength-outward;
            float ns=0,no=0;
            if(outward<RoomStart&&Math.Abs(side)>GateHalfWidth)no=1;
            else if(sideClear<endClear)ns=-Math.Sign(side);else no=-1;
            nx=Axis*(ns+no);nz=Axis*(ns-no);
        }
    }
}
