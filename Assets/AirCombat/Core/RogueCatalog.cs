using System;

namespace Meridian
{
    public enum RoguePhase { Planning, Flying, Reward, Victory, Defeat }
    public enum RogueMod
    {
        Armor, Supercharger, Cannon, Lightframe, Longwing, Streamline,
        AmmoBays, HarmonizedGuns, HotBelt, BoostInjector, ControlCables, FieldMechanic
    }
    public enum RogueAbility { None, Overboost, ArmorPiercing, CombatFlaps, Rally }
    public enum RogueRewardKind { Modification, Ability, FieldRepair }

    public sealed class RogueRoute
    {
        public string Title, Description;
        public MissionKind Kind;
        public int Seed, Danger, SupplyReward;
        public int EncounterPressure;
    }

    public sealed class RogueReward
    {
        public string Title, Description;
        public RogueRewardKind Kind;
        public RogueMod Mod;
        public RogueAbility Ability;
    }

    public sealed class RogueInstallation
    {
        public RogueMod Mod;
        public int Level;
        public string Name { get { return RogueCatalog.ModName(Mod); } }
    }

    /// <summary>Run equipment is source data, separate from the standard aircraft definitions.</summary>
    public static class RogueCatalog
    {
        public static string ModName(RogueMod mod)
        {
            switch (mod)
            {
                case RogueMod.Armor: return "Armored bathtub";
                case RogueMod.Supercharger: return "Two-stage supercharger";
                case RogueMod.Cannon: return "20 mm conversion";
                case RogueMod.Lightframe: return "Stripped airframe";
                case RogueMod.Longwing: return "Extended wings";
                case RogueMod.Streamline: return "Racing fairings";
                case RogueMod.AmmoBays: return "Deep ammunition bays";
                case RogueMod.HarmonizedGuns: return "Bench-harmonized guns";
                case RogueMod.HotBelt: return "High-velocity feed";
                case RogueMod.BoostInjector: return "Water injection kit";
                case RogueMod.ControlCables: return "Competition controls";
                default: return "Flying workshop";
            }
        }

        public static string ModDescription(RogueMod mod)
        {
            switch (mod)
            {
                case RogueMod.Armor: return "Gun damage received -28%; mass +16%; roll response -12%. Heavy but forgiving.";
                case RogueMod.Supercharger: return "Engine power +22%; thrust +12%; drag +4%. Better climb and pursuit; watch dive speed.";
                case RogueMod.Cannon: return "Round damage +65%; rate -38%; ammunition -18%; +220 kg. Short, deliberate bursts.";
                case RogueMod.Lightframe: return "Mass -12%; roll +20%; pitch +12%; gun damage received +28%; safe dive speed -4%.";
                case RogueMod.Longwing: return "Wing area +20%; induced drag -15%; stall speed -7%; roll -22%; parasite drag +8%.";
                case RogueMod.Streamline: return "Parasite drag -25%; safe dive speed +9%; wing area -7%; stall speed +4%.";
                case RogueMod.AmmoBays: return "Ammunition +55%; mass +10%; drag +5%. Sustained fire costs climb and acceleration.";
                case RogueMod.HarmonizedGuns: return "Dispersion -55%; round damage -10%. Precision belts trade impact for consistency.";
                case RogueMod.HotBelt: return "Muzzle speed +20%; firing rate +15%; ammunition -20%; mass +6%. Less lead, shorter endurance.";
                case RogueMod.BoostInjector: return "Overboost +5 seconds and +1 charge per sortie; +100 kg and +2% drag. Wear per use is unchanged.";
                case RogueMod.ControlCables: return "Roll and pitch response +25%; drag +4%; safe dive speed -4%. Fast hands still need energy.";
                default: return "After each victory, repair 12 hull on your aircraft and surviving wingmen; +180 kg. Crew cannot revive losses.";
            }
        }

        public static string AbilityName(RogueAbility ability)
        {
            switch (ability)
            {
                case RogueAbility.Overboost: return "Emergency boost";
                case RogueAbility.ArmorPiercing: return "AP belt";
                case RogueAbility.CombatFlaps: return "Combat flaps";
                case RogueAbility.Rally: return "Flight rally";
                default: return "Empty slot";
            }
        }

        public static string AbilityDescription(RogueAbility ability)
        {
            switch (ability)
            {
                case RogueAbility.Overboost: return "15s: power +45%, thrust +25%. Costs 3 hull each use; unavailable at 15 hull or less. 2 uses/sortie.";
                case RogueAbility.ArmorPiercing: return "9s: round damage +60%, firing rate +35%. Burns ammunition faster. 2 uses/sortie.";
                case RogueAbility.CombatFlaps: return "12s: lift area +24%, stall speed -12%, pitch +15%; drag +55%, safe speed -20%. 2 uses/sortie.";
                case RogueAbility.Rally: return "20s: wingmen Cover Me, +15% engine power, -40% dispersion. Surviving pilots only. 1 use/sortie.";
                default: return "Acquire an ability from a sortie reward. No in-flight card menus.";
            }
        }

        public static AircraftDefinition Clone(AircraftDefinition d)
        {
            return new AircraftDefinition {
                Name = d.Name, Mass = d.Mass, WingArea = d.WingArea, Wingspan = d.Wingspan, Length = d.Length,
                MaxThrust = d.MaxThrust, MaxPower = d.MaxPower, BaseDrag = d.BaseDrag, InducedDrag = d.InducedDrag,
                LiftAtZero = d.LiftAtZero, LiftSlope = d.LiftSlope, StallAngle = d.StallAngle, StallSpeed = d.StallSpeed,
                CruiseSpeed = d.CruiseSpeed, MaxSpeed = d.MaxSpeed, MaxRollRate = d.MaxRollRate,
                MaxPitchRate = d.MaxPitchRate, MaxYawRate = d.MaxYawRate, Ammunition = d.Ammunition
            };
        }

        public static void Copy(AircraftDefinition from, AircraftDefinition to)
        {
            to.Name = from.Name; to.Mass = from.Mass; to.WingArea = from.WingArea;
            to.Wingspan = from.Wingspan; to.Length = from.Length;
            to.MaxThrust = from.MaxThrust; to.MaxPower = from.MaxPower;
            to.BaseDrag = from.BaseDrag; to.InducedDrag = from.InducedDrag;
            to.LiftAtZero = from.LiftAtZero; to.LiftSlope = from.LiftSlope;
            to.StallAngle = from.StallAngle; to.StallSpeed = from.StallSpeed;
            to.CruiseSpeed = from.CruiseSpeed; to.MaxSpeed = from.MaxSpeed;
            to.MaxRollRate = from.MaxRollRate; to.MaxPitchRate = from.MaxPitchRate;
            to.MaxYawRate = from.MaxYawRate; to.Ammunition = from.Ammunition;
        }
    }
}
