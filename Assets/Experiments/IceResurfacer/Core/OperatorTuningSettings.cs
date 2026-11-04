using System;

namespace IceResurfacer.Core
{
    /// <summary>Scene opt-in material calibration. Never changes tank capacities or stored material.</summary>
    [Serializable]
    public sealed class OperatorTuningSettings
    {
        public float FloodFlowMultiplier=1;
        public float ShavingsExpansionMultiplier=1;
        public float LooseSnowPickupDepthMm=6;
        public float LooseSnowIntakeHeightMm=50;
        public float HorizontalConveyorMultiplier=1;
        public float VerticalConveyorMultiplier=1;
        public float ConditionerRetentionM3=.08f;
        public float ConditionerSpillFillFraction=.17f;
        public float WashRecoveryFraction=.82f;

        public void Validate()
        {
            Bound(FloodFlowMultiplier,.1f,2,nameof(FloodFlowMultiplier));
            Bound(ShavingsExpansionMultiplier,1,4,nameof(ShavingsExpansionMultiplier));
            Bound(LooseSnowPickupDepthMm,1,50,nameof(LooseSnowPickupDepthMm));
            Bound(LooseSnowIntakeHeightMm,10,100,nameof(LooseSnowIntakeHeightMm));
            Bound(HorizontalConveyorMultiplier,.1f,4,nameof(HorizontalConveyorMultiplier));
            Bound(VerticalConveyorMultiplier,.1f,4,nameof(VerticalConveyorMultiplier));
            Bound(ConditionerRetentionM3,.005f,.2f,nameof(ConditionerRetentionM3));
            Bound(ConditionerSpillFillFraction,.05f,1,nameof(ConditionerSpillFillFraction));
            Bound(WashRecoveryFraction,0,1,nameof(WashRecoveryFraction));
        }
        static void Bound(float value,float min,float max,string name)
        {if(float.IsNaN(value)||float.IsInfinity(value)||value<min||value>max)throw new ArgumentOutOfRangeException(name);}
        public void Apply(SimulatorConfig target,SimulatorConfig authored)
        {
            Validate();
            target.FloodMaxLitersPerSecond=authored.FloodMaxLitersPerSecond*FloodFlowMultiplier;
            target.SnowExpansionRatio=authored.SnowExpansionRatio*ShavingsExpansionMultiplier;
            target.LooseSnowPickupDepthMm=LooseSnowPickupDepthMm;
            target.LooseSnowIntakeHeightMm=LooseSnowIntakeHeightMm;
            target.HorizontalCapacityM3PerSecond=authored.HorizontalCapacityM3PerSecond*HorizontalConveyorMultiplier;
            target.VerticalCapacityM3PerSecond=authored.VerticalCapacityM3PerSecond*VerticalConveyorMultiplier;
            target.ConditionerSnowCapacityM3=ConditionerRetentionM3;
            target.ConditionerSpillFillFraction=ConditionerSpillFillFraction;
            target.WashRecoveryFraction=WashRecoveryFraction;
        }
        public static void Restore(SimulatorConfig target,SimulatorConfig authored)
        {
            target.FloodMaxLitersPerSecond=authored.FloodMaxLitersPerSecond;
            target.SnowExpansionRatio=authored.SnowExpansionRatio;
            target.LooseSnowPickupDepthMm=authored.LooseSnowPickupDepthMm;
            target.LooseSnowIntakeHeightMm=authored.LooseSnowIntakeHeightMm;
            target.HorizontalCapacityM3PerSecond=authored.HorizontalCapacityM3PerSecond;
            target.VerticalCapacityM3PerSecond=authored.VerticalCapacityM3PerSecond;
            target.ConditionerSnowCapacityM3=authored.ConditionerSnowCapacityM3;
            target.ConditionerSpillFillFraction=authored.ConditionerSpillFillFraction;
            target.WashRecoveryFraction=authored.WashRecoveryFraction;
        }
        public static OperatorTuningSettings From(SimulatorConfig c)=>new OperatorTuningSettings
        {LooseSnowPickupDepthMm=c.LooseSnowPickupDepthMm,LooseSnowIntakeHeightMm=c.LooseSnowIntakeHeightMm,ConditionerRetentionM3=c.ConditionerSnowCapacityM3,ConditionerSpillFillFraction=c.ConditionerSpillFillFraction,WashRecoveryFraction=c.WashRecoveryFraction};
    }
}
