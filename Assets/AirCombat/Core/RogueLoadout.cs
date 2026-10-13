using System;
using System.Collections.Generic;

namespace Meridian
{
    /// <summary>Always rebuilt from an immutable baseline. Effects never multiply their own previous result.</summary>
    public sealed class RogueLoadout
    {
        private readonly AircraftDefinition baseline;
        public readonly AircraftDefinition Definition;
        public float Damage = 1f, FireRate = 1f, Spread = 1f, MuzzleSpeed = 1f, DamageTaken = 1f;

        public RogueLoadout()
        {
            baseline = RogueCatalog.Clone(AircraftDefinition.AlliedFighter);
            baseline.Ammunition = 1200;
            Definition = RogueCatalog.Clone(baseline);
        }

        public void Rebuild(IList<RogueInstallation> installed, RogueAbility[] equipped, float[] activeSeconds)
        {
            RogueCatalog.Copy(baseline, Definition);
            Damage = FireRate = Spread = MuzzleSpeed = DamageTaken = 1f;
            for (int i = 0; i < installed.Count; i++)
                for (int n = 0; n < installed[i].Level; n++) Apply(installed[i].Mod);
            for (int i = 0; i < equipped.Length; i++)
                if (activeSeconds[i] > 0f)
                    switch (equipped[i])
                    {
                        case RogueAbility.Overboost: Definition.MaxPower *= 1.45f; Definition.MaxThrust *= 1.25f; break;
                        case RogueAbility.ArmorPiercing: Damage *= 1.6f; FireRate *= 1.35f; break;
                        case RogueAbility.CombatFlaps:
                            Definition.WingArea *= 1.24f; Definition.StallSpeed *= .88f;
                            Definition.MaxPitchRate *= 1.15f; Definition.BaseDrag *= 1.55f; Definition.MaxSpeed *= .8f; break;
                    }
            Definition.Name = installed.Count == 0 ? "Kestrel / field standard" : "Kestrel / " + installed.Count + " field kits";
        }

        private void Apply(RogueMod mod)
        {
            AircraftDefinition d = Definition;
            switch (mod)
            {
                case RogueMod.Armor: DamageTaken *= .72f; d.Mass *= 1.16f; d.MaxRollRate *= .88f; break;
                case RogueMod.Supercharger: d.MaxPower *= 1.22f; d.MaxThrust *= 1.12f; d.BaseDrag *= 1.04f; break;
                case RogueMod.Cannon:
                    Damage *= 1.65f; FireRate *= .62f; d.Ammunition = (int)(d.Ammunition * .82f);
                    d.Mass += 220f; break;
                case RogueMod.Lightframe:
                    d.Mass *= .88f; d.MaxRollRate *= 1.20f; d.MaxPitchRate *= 1.12f;
                    DamageTaken *= 1.28f; d.MaxSpeed *= .96f; break;
                case RogueMod.Longwing:
                    d.WingArea *= 1.20f; d.Wingspan *= 1.08f; d.InducedDrag *= .85f;
                    d.StallSpeed *= .93f; d.MaxRollRate *= .78f; d.BaseDrag *= 1.08f; break;
                case RogueMod.Streamline:
                    d.BaseDrag *= .75f; d.MaxSpeed *= 1.09f; d.WingArea *= .93f; d.StallSpeed *= 1.04f; break;
                case RogueMod.AmmoBays:
                    d.Ammunition = (int)(d.Ammunition * 1.55f); d.Mass *= 1.10f; d.BaseDrag *= 1.05f; break;
                case RogueMod.HarmonizedGuns: Spread *= .45f; Damage *= .90f; break;
                case RogueMod.HotBelt:
                    MuzzleSpeed *= 1.20f; FireRate *= 1.15f; d.Ammunition = (int)(d.Ammunition * .80f); d.Mass *= 1.06f; break;
                case RogueMod.BoostInjector: d.Mass += 100f; d.BaseDrag *= 1.02f; break;
                case RogueMod.ControlCables:
                    d.MaxPitchRate *= 1.25f; d.MaxRollRate *= 1.25f; d.BaseDrag *= 1.04f; d.MaxSpeed *= .96f; break;
                case RogueMod.FieldMechanic: d.Mass += 180f; break;
            }
        }

        public void ApplyTo(AircraftState aircraft)
        {
            aircraft.Definition = Definition;
            aircraft.DamageMultiplier = Damage;
            aircraft.FireRateMultiplier = FireRate;
            aircraft.SpreadMultiplier = Spread;
            aircraft.MuzzleSpeedMultiplier = MuzzleSpeed;
            aircraft.DamageTakenMultiplier = DamageTaken;
        }
    }
}
