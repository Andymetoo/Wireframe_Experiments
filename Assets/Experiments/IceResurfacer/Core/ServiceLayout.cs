using System;

namespace IceResurfacer.Core
{
    /// <summary>One coordinate authority for the southeast diagonal gate and backstage room.
    /// Local Side points northeast; Outward points away from the ice toward the snow pit.</summary>
    public sealed class ServiceLayout
    {
        public const float Axis = .7071067811865475f;
        public readonly float GateX, GateZ, GateHalfWidth, RoomHalfWidth = 6f, RoomLength;
        public readonly float RoomStart = 1.6f, NeckStart = -1.1f;
        public readonly float PitSide = 0f, PitOutward, PitHalfWidth = 2f, PitHalfLength = 1.2f;
        public readonly float ParkSide = 0f, ParkOutward = 5.8f;
        // Machine faces into the room. Its fixed right-side fillers face the
        // negative-Side wall, leaving a straight reverse route onto the ice.
        public const float SupplySide = -5.45f, SupplyOutward = 6f;
        public const float HolderSide = -5.40f, HolderOutward = 5.52f;
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
        { return ContainsFloor(x,z,margin) && !ContainsPit(x,z,.12f); }
        public bool InsideRoom(float x,float z)
        { ToLocal(x,z,out float side,out float outward); return Math.Abs(side)<RoomHalfWidth && outward>RoomStart+.1f && outward<RoomLength; }
        public void ParkingPose(out float x,out float z,out float heading)
        { ToWorld(ParkSide,ParkOutward,out x,out z); heading=OutwardHeading; }
        public void SupplyAnchor(out float x,out float z) { ToWorld(SupplySide,SupplyOutward,out x,out z); }
        public void HoseHolder(out float x,out float z) { ToWorld(HolderSide,HolderOutward,out x,out z); }
        public void ContactNormal(float x,float z,out float nx,out float nz)
        {
            ToLocal(x,z,out float side,out float outward);
            float sideClear=(outward<RoomStart?GateHalfWidth:RoomHalfWidth)-Math.Abs(side);
            float endClear=RoomLength-outward;
            float ns=0,no=0;
            if(sideClear<endClear)ns=-Math.Sign(side);else no=-1;
            if(Math.Abs(side-PitSide)<PitHalfWidth+2&&Math.Abs(outward-PitOutward)<PitHalfLength+3)
            {
                float dx=side-PitSide,dz=outward-PitOutward;
                if(Math.Abs(dx)-PitHalfWidth>Math.Abs(dz)-PitHalfLength){ns=Math.Sign(dx);no=0;}
                else{ns=0;no=Math.Sign(dz);}
            }
            nx=Axis*(ns+no);nz=Axis*(ns-no);
        }
    }
}
