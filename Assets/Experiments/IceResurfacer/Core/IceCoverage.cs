namespace IceResurfacer.Core
{
    public enum IceTreatmentState
    {
        OutsideIce, Uncut, CutButDry, WetButUncut, SnowCovered,
        Overwatered, Resurfaced, ShavingExcluded
    }

    /// <summary>Explains the existing treatment rules without changing scoring or cell state.</summary>
    public static class IceCoverage
    {
        public const float RequiredFloodMm = .08f;
        public const float MaximumLooseSnowMm = .25f;
        public const float ExcessWaterMm = .05f;

        public static float RequiredCutMm(SimulatorConfig config) { return .08f * config.BladeBiteFraction; }

        public static IceTreatmentState Classify(IceCell cell, SimulatorConfig config)
        {
            if (!cell.IsIce) return IceTreatmentState.OutsideIce;
            if (cell.OverwaterMm > ExcessWaterMm) return IceTreatmentState.Overwatered;
            if (cell.SnowMm >= MaximumLooseSnowMm) return IceTreatmentState.SnowCovered;
            if (!cell.Shavable) return IceTreatmentState.ShavingExcluded;
            if (cell.Covered) return IceTreatmentState.Resurfaced;
            bool cut = cell.CutDepthMm > RequiredCutMm(config);
            bool wet = cell.FloodMm > RequiredFloodMm;
            if (cut && !wet) return IceTreatmentState.CutButDry;
            if (!cut && wet) return IceTreatmentState.WetButUncut;
            return IceTreatmentState.Uncut;
        }

        public static string Label(IceTreatmentState state)
        {
            switch (state)
            {
                case IceTreatmentState.Uncut: return "Uncut";
                case IceTreatmentState.CutButDry: return "Cut / dry";
                case IceTreatmentState.WetButUncut: return "Wet / uncut";
                case IceTreatmentState.SnowCovered: return "Loose snow";
                case IceTreatmentState.Overwatered: return "Too wet";
                case IceTreatmentState.Resurfaced: return "Resurfaced";
                case IceTreatmentState.ShavingExcluded: return "Perimeter / shaving excluded";
                default: return "Outside ice";
            }
        }
    }
}
