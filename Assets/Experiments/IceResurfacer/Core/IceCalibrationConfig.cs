using System;

namespace IceResurfacer.Core
{
    public sealed partial class SimulatorConfig
    {
        // Published wheel track plus the 526's LT215/85R16 nominal section. These
        // define the operator's alignment cue, independently of body width.
        public float WheelTrack = 1.37f;
        public float TireWidth = .215f;
        public float ShavingPerimeterExclusion = .1524f;
        // A finite fringe supplied by the same flood liters, not extra water.
        public float WaterFilmSpreadMeters = .12f;
        public float WaterFilmFringeFraction = .05f;
        public float WashPickupLitersPerSecond = 1.45f;
        public float WashPickupContactLength = .32f;
        public float WashPickupContactFore = -.12f;
        public float WashPickupResidualMm = .035f;
        public float FloodContactLength = .27f;
        public float FloodContactFore = -.44f;
        public float MinimumGougeDepthMm = .024f;
        public float TypicalGougeDepthMm = .060f;
        public float DeepGougeDepthMm = .145f;
        public float GougeRoughnessDepthMm = .080f;
        public float BrushPivotX = -.90f;
        public float BrushPivotZ = .50f;
        public float BrushArmLength = .38f;
        public float BrushRadius = .25f;
        public float BrushRetractedDegrees = 85f;

        void ValidateIceCalibration()
        {
            float[] values={WheelTrack,TireWidth,ShavingPerimeterExclusion,
                WaterFilmSpreadMeters,WaterFilmFringeFraction,WashPickupLitersPerSecond,
                WashPickupContactLength,WashPickupContactFore,WashPickupResidualMm,FloodContactLength,FloodContactFore,
                MinimumGougeDepthMm,TypicalGougeDepthMm,DeepGougeDepthMm,GougeRoughnessDepthMm,BrushPivotX,BrushPivotZ,
                BrushArmLength,BrushRadius,BrushRetractedDegrees};
            foreach(float value in values)
                if(float.IsNaN(value)||float.IsInfinity(value))
                    throw new ArgumentException("Ice calibration requires finite numbers.");
            if(WheelTrack<=0||TireWidth<=0||WheelTrack+TireWidth>VehicleHalfWidth*2||
                ShavingPerimeterExclusion<0||ShavingPerimeterExclusion>.5f||
                WaterFilmSpreadMeters<0||WaterFilmSpreadMeters>.3f||
                WaterFilmFringeFraction<0||WaterFilmFringeFraction>.3f||
                WashPickupLitersPerSecond<=0||WashPickupContactLength<=0||WashPickupContactLength>ConditionerLength||
                FloodContactLength<=0||FloodContactLength>ConditionerLength||Math.Abs(FloodContactFore)>ConditionerLength||
                Math.Abs(WashPickupContactFore)+WashPickupContactLength*.5f>ConditionerLength*.5f||
                WashPickupResidualMm<0||WashPickupResidualMm>GoodFloodDepthMm||
                MinimumGougeDepthMm<0||TypicalGougeDepthMm<=MinimumGougeDepthMm||DeepGougeDepthMm<TypicalGougeDepthMm||GougeRoughnessDepthMm<=0||
                BrushArmLength<=0||BrushRadius<=0||BrushRetractedDegrees<0||BrushRetractedDegrees>100)
                throw new ArgumentException("Invalid tire, film, pickup, perimeter or brush calibration.");
        }
    }
}
