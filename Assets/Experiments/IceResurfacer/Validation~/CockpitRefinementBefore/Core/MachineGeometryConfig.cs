using System;

namespace IceResurfacer.Core
{
    public sealed partial class SimulatorConfig
    {
        // Authored dimensions, not parent scale: moving parts and collision use
        // these same coordinates. Original y samples are measured from the hinge.
        public float TankRoofHeight = .875f;
        public float TankLateralOffset = 0f;
        public float ConveyorHeightOffset = 0f;
        public float CockpitOffsetX = -.06f;
        public float CockpitOffsetZ = 0f;
        public float TankGeometryX(float originalX) { return originalX - .20f + TankLateralOffset; }
        public float TankGeometryY(float originalY) { return .085f + (originalY - .085f) * (TankRoofHeight - .085f) / .94f; }
        public float TankHeightRatio { get { return (TankRoofHeight - .085f) / .94f; } }
        public float ConveyorOutletX { get { return .45f; } }
        public float ConveyorOutletY { get { return 1.48f + ConveyorHeightOffset; } }
        public float ConveyorOutletZ { get { return .47f; } }
        public float TankSnowInteriorWidth { get { return 1.488f; } }
        public float TankSnowFrontWidth { get { return 1.40f; } }
        public float TankSnowMeanWidth { get { return (TankSnowInteriorWidth+TankSnowFrontWidth)*.5f; } }
        public float TankSnowInteriorLength { get { return 1.96f; } }
        public float TankSnowCenterZ { get { return -1.01f; } }
        public float TankSnowMaxDepth { get { return TankGeometryY(.94f) - .085f - .025f; } }
        public float TankSnowShoulderDepth { get { return TankGeometryY(.75f) - .085f; } }
        public float TankSnowTopWidth { get { return 1.20f; } }
        public float TankSnowPhysicalCapacityM3 { get { return TankSnowInteriorLength *
            (TankSnowMeanWidth * TankSnowShoulderDepth + (TankSnowMeanWidth + TankSnowTopWidth) * .5f *
            (TankSnowMaxDepth - TankSnowShoulderDepth)); } }
        public float TankSnowWidthAt(float depth,float normalizedZ)
        {
            float z=Math.Max(0,Math.Min(1,normalizedZ)),h=Math.Max(0,Math.Min(TankSnowMaxDepth,depth));
            float width=TankSnowInteriorWidth+(TankSnowFrontWidth-TankSnowInteriorWidth)*z;
            float shoulder=Math.Max(0,h-TankSnowShoulderDepth)/(TankSnowMaxDepth-TankSnowShoulderDepth);
            return width+(TankSnowTopWidth-width)*shoulder;
        }
        public float TankSnowSectionArea(float depth,float normalizedZ)
        {
            float z=Math.Max(0,Math.Min(1,normalizedZ)),h=Math.Max(0,Math.Min(TankSnowMaxDepth,depth));
            float width=TankSnowInteriorWidth+(TankSnowFrontWidth-TankSnowInteriorWidth)*z;
            float shoulder=Math.Max(0,h-TankSnowShoulderDepth);
            return width*h-(width-TankSnowTopWidth)*shoulder*shoulder/(2*(TankSnowMaxDepth-TankSnowShoulderDepth));
        }

        void ValidateMachineGeometry()
        {
            float[] values = { TankRoofHeight, TankLateralOffset, ConveyorHeightOffset, CockpitOffsetX, CockpitOffsetZ };
            foreach(float value in values)
                if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Machine geometry must be finite.");
            if(TankRoofHeight < .80f || TankRoofHeight > 1.10f || Math.Abs(TankLateralOffset) > .18f ||
               Math.Abs(ConveyorHeightOffset) > .08f || Math.Abs(CockpitOffsetX) > .08f || Math.Abs(CockpitOffsetZ) > .10f ||
               ConveyorOutletY+.14f > .78f+TankGeometryY(1.02f) || ConveyorOutletX+.14f > TankLateralOffset+.64f)
                throw new ArgumentException("Machine geometry exceeds the supported mechanical clearances.");
        }
    }
}
