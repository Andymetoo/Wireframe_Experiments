using System;

namespace IceResurfacer.Core
{
    /// <summary>
    /// The one board-brush articulation. Simulation contact, runtime visuals and the Editor
    /// preview all call this, so the visible arm and the snow-clearing disc cannot disagree.
    /// Machine space: +X right, +Z forward. At 0 degrees the arm points straight out to the
    /// left (-X); positive angles swing the brush head rearward (-Z) toward its stowed pose.
    /// </summary>
    public static class BoardBrushArticulation
    {
        /// <summary>Arm angle for a deployment of 0 (stowed) .. 1 (working).</summary>
        public static float ArmDegrees(float deployedDegrees,float retractedDegrees,float deployment)
        {
            return deployedDegrees+(1-deployment)*(retractedDegrees-deployedDegrees);
        }

        public static float ArmDegrees(SimulatorConfig c,float deployment)
        {
            return ArmDegrees(c.BrushDeployedDegrees,c.BrushRetractedDegrees,deployment);
        }

        public static float ArmRadians(float deployedDegrees,float retractedDegrees,float deployment)
        {
            return ArmDegrees(deployedDegrees,retractedDegrees,deployment)*(float)Math.PI/180;
        }

        public static float ArmRadians(SimulatorConfig c,float deployment)
        {
            return ArmRadians(c.BrushDeployedDegrees,c.BrushRetractedDegrees,deployment);
        }

        /// <summary>Brush disc centre in machine space (before board compression).</summary>
        public static void Contact(float pivotX,float pivotZ,float armLength,float radians,out float x,out float z)
        {
            x=pivotX-armLength*(float)Math.Cos(radians);
            z=pivotZ-armLength*(float)Math.Sin(radians);
        }

        public static void Contact(SimulatorConfig c,float deployment,out float x,out float z)
        {
            Contact(c.BrushPivotX,c.BrushPivotZ,c.BrushArmLength,ArmRadians(c,deployment),out x,out z);
        }
    }
}
