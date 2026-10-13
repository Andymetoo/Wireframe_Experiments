using System;
using System.Collections.Generic;

namespace Meridian
{
    /// <summary>
    /// Operation Afterburn: four authoritative sorties linked by finite repairs, persistent losses,
    /// and field equipment. A run owns every definition it mutates; ordinary missions are untouched.
    /// </summary>
    public sealed class RogueRun
    {
        public const int SortieCount = 4, MaxInstallations = 5, MaxModLevel = 2;
        public readonly int Seed;
        private readonly List<RogueRoute> routes = new List<RogueRoute>();
        private readonly List<RogueReward> rewards = new List<RogueReward>();
        private readonly List<RogueInstallation> mods = new List<RogueInstallation>();
        private readonly List<RogueAbility> ownedAbilities = new List<RogueAbility>();
        private readonly RogueLoadout loadout = new RogueLoadout();
        private readonly Dictionary<int, AircraftDefinition> wingBaseline = new Dictionary<int, AircraftDefinition>();
        private readonly Dictionary<int, AircraftDefinition> wingDefinitions = new Dictionary<int, AircraftDefinition>();
        private int routeHistory, lastCompletedStage = -1;
        private bool rallyActive;
        private SquadronOrder beforeRally;
        public readonly RogueAbility[] Equipped = { RogueAbility.Overboost, RogueAbility.None };
        public readonly int[] Charges = new int[2];
        public readonly float[] ActiveSeconds = new float[2];
        public readonly float[] WingmanHull = { 100f, 100f };
        public IReadOnlyList<RogueRoute> Routes { get { return routes; } }
        public IReadOnlyList<RogueReward> Rewards { get { return rewards; } }
        public IReadOnlyList<RogueInstallation> Mods { get { return mods; } }
        public IReadOnlyList<RogueAbility> OwnedAbilities { get { return ownedAbilities; } }
        public RoguePhase Phase { get; private set; }
        public int Stage { get; private set; }
        public string StageName { get { return Stage == 0 ? "Opening moves" : Stage == 1 ? "Contested skies" : Stage == 2 ? "Pressure front" : "Final offensive"; } }
        public float Hull { get; private set; } = 100f;
        public int Supplies { get; private set; } = 4;
        public AirMission Mission { get; private set; }
        public RogueRoute SelectedRoute { get; private set; }
        public RogueReward RefitOffer { get; private set; }
        public bool RefitPurchased { get; private set; }
        public string LastMessage { get; private set; }
        public string Summary { get; private set; }
        public int SortiesWon { get; private set; }
        public int TotalKills { get; private set; }
        public int TotalHits { get; private set; }
        public int TotalRounds { get; private set; }
        public float FlightTime { get; private set; }
        public AircraftDefinition AircraftPreview { get { return loadout.Definition; } }
        public float DamageMultiplier { get { return loadout.Damage; } }
        public float FireRateMultiplier { get { return loadout.FireRate; } }
        public float DamageTakenMultiplier { get { return loadout.DamageTaken; } }
        public string BuildIdentity
        {
            get
            {
                if (ModLevel(RogueMod.Cannon) > 0 && ModLevel(RogueMod.Armor) > 0) return "Flying fortress";
                if (ModLevel(RogueMod.Lightframe) > 0 && ModLevel(RogueMod.Supercharger) > 0) return "Knife-edge racer";
                if (ModLevel(RogueMod.Longwing) > 0) return "High-wing duelist";
                if (ModLevel(RogueMod.Cannon) > 0) return "Heavy hitter";
                if (ModLevel(RogueMod.Lightframe) > 0) return "Featherweight";
                if (ModLevel(RogueMod.AmmoBays) > 0) return "Patrol gunship";
                return mods.Count == 0 ? "Field-standard fighter" : "Field-modified Kestrel";
            }
        }

        public RogueRun(int seed)
        {
            Seed = seed; Phase = RoguePhase.Planning;
            ownedAbilities.Add(RogueAbility.Overboost);
            LastMessage = "Four sorties to break the blockade. Hull and lost wingmen carry over; fuel, guns and tactical charges rearm between sorties.";
            Summary = "Operation Afterburn / sortie 1 of 4";
            RebuildEquipment(); GenerateRoutes();
        }

        public int ModLevel(RogueMod mod)
        {
            for (int i = 0; i < mods.Count; i++) if (mods[i].Mod == mod) return mods[i].Level;
            return 0;
        }

        public bool RepairHull()
        {
            if (Phase != RoguePhase.Planning || Supplies < 2 || Hull >= 100f) return false;
            Supplies -= 2; Hull = Math.Min(100f, Hull + 35f);
            LastMessage = "Ground crew restored 35 hull (maximum 100). Two supply crates spent."; return true;
        }

        public bool BuyRefit()
        {
            if (Phase != RoguePhase.Planning || RefitOffer == null || RefitPurchased || Supplies < 3
                || !InstallMod(RefitOffer.Mod)) return false;
            Supplies -= 3; RefitPurchased = true; RebuildEquipment();
            LastMessage = RefitOffer.Title + " installed for three supply crates. Repairs and replacement pilots use the same stores.";
            return true;
        }

        public bool RepairWingman(int index)
        {
            if (Phase != RoguePhase.Planning || index < 0 || index >= 2 || Supplies < 1
                || WingmanHull[index] <= 0f || WingmanHull[index] >= 100f) return false;
            Supplies--; WingmanHull[index] = Math.Min(100f, WingmanHull[index] + 40f);
            LastMessage = "Wing aircraft repaired by 40 hull. One supply crate spent."; return true;
        }

        public bool RecruitWingman(int index)
        {
            if (Phase != RoguePhase.Planning || index < 0 || index >= 2 || Supplies < 3 || WingmanHull[index] > 0f) return false;
            Supplies -= 3; WingmanHull[index] = 100f;
            LastMessage = "Replacement pilot and aircraft assigned. Three supply crates spent."; return true;
        }

        public bool EquipAbility(int slot, RogueAbility ability)
        {
            if (Phase != RoguePhase.Planning || slot < 0 || slot >= 2 || !ValidAbility(ability)
                || (ability != RogueAbility.None && !ownedAbilities.Contains(ability))) return false;
            int other = 1 - slot;
            if (ability != RogueAbility.None && Equipped[other] == ability)
            { RogueAbility previous = Equipped[slot]; Equipped[other] = previous; }
            Equipped[slot] = ability;
            LastMessage = "Tactical slot " + (slot + 1) + ": " + RogueCatalog.AbilityName(ability); return true;
        }

        public AirMission LaunchRoute(int index)
        {
            if (Phase != RoguePhase.Planning || index < 0 || index >= routes.Count || Hull <= 0f) return null;
            SelectedRoute = routes[index];
            routeHistory = unchecked(routeHistory * 31 + index + 1);
            Mission = new AirMission(SelectedRoute.Seed, SelectedRoute.Kind,
                new AirEncounterOptions { Pressure = SelectedRoute.EncounterPressure });
            Mission.Player.Health = Hull;
            Array.Clear(ActiveSeconds, 0, ActiveSeconds.Length);
            rallyActive = false;
            RebuildEquipment(); loadout.ApplyTo(Mission.Player);
            Mission.Player.Ammo = loadout.Definition.Ammunition;
            wingBaseline.Clear(); wingDefinitions.Clear();
            for (int i = Mission.Wingmen.Count - 1; i >= 0; i--)
            {
                AircraftState wingman = Mission.Wingmen[i]; int slot = wingman.Id - 1;
                if (slot < 0 || slot >= WingmanHull.Length) continue;
                if (WingmanHull[slot] <= 0f)
                {
                    // Losses do not respawn at the next launch or generate a fake crash at spawn.
                    Mission.Wingmen.RemoveAt(i); Mission.Aircraft.Remove(wingman); continue;
                }
                wingman.Health = WingmanHull[slot];
                wingBaseline.Add(wingman.Id, RogueCatalog.Clone(wingman.Definition));
                wingDefinitions.Add(wingman.Id, RogueCatalog.Clone(wingman.Definition));
                wingman.Definition = wingDefinitions[wingman.Id];
            }
            for (int i = 0; i < 2; i++)
                Charges[i] = Equipped[i] == RogueAbility.None ? 0 : Equipped[i] == RogueAbility.Rally ? 1
                    : Equipped[i] == RogueAbility.Overboost ? 2 + ModLevel(RogueMod.BoostInjector) : 2;
            Phase = RoguePhase.Flying;
            LastMessage = SelectedRoute.Title + ": " + Mission.Objective;
            Summary = "Sortie " + (Stage + 1) + " / " + SortieCount + " — " + SelectedRoute.Title;
            return Mission;
        }

        public bool ActivateAbility(int slot)
        {
            if (Phase != RoguePhase.Flying || Mission == null || Mission.Outcome != MissionOutcome.Flying
                || !Mission.Player.Alive || slot < 0 || slot >= 2) return false;
            RogueAbility ability = Equipped[slot];
            if (ability == RogueAbility.None) { LastMessage = "Empty tactical slot. Equip a card between sorties."; return false; }
            if (ActiveSeconds[slot] > 0f) { LastMessage = RogueCatalog.AbilityName(ability) + " is already active."; return false; }
            if (Charges[slot] <= 0) { LastMessage = RogueCatalog.AbilityName(ability) + " has no uses left this sortie."; return false; }
            if (ability == RogueAbility.Overboost && Mission.Player.Health <= 15f)
            { LastMessage = "Boost inhibited: more than 15 hull required."; return false; }
            if (ability == RogueAbility.Rally && Mission.WingmenAlive == 0)
            { LastMessage = "No surviving wingmen in this sortie to rally."; return false; }
            Charges[slot]--;
            switch (ability)
            {
                case RogueAbility.Overboost:
                    ActiveSeconds[slot] = 15f + 5f * ModLevel(RogueMod.BoostInjector);
                    Mission.Player.Health -= 3f; Hull = Mission.Player.Health; break;
                case RogueAbility.ArmorPiercing: ActiveSeconds[slot] = 9f; break;
                case RogueAbility.CombatFlaps: ActiveSeconds[slot] = 12f; break;
                case RogueAbility.Rally:
                    ActiveSeconds[slot] = 20f; beforeRally = Mission.CurrentOrder;
                    Mission.IssueOrder(SquadronOrder.CoverMe); rallyActive = true; break;
            }
            RebuildEquipment();
            LastMessage = RogueCatalog.AbilityName(ability) + " active. " + Charges[slot] + " uses remain this sortie.";
            return true;
        }

        public void Step(FlightInput input, float dt) { Advance(input, dt, false); }
        public void StepBots(float dt) { Advance(default(FlightInput), dt, true); }

        private void Advance(FlightInput input, float dt, bool bots)
        {
            if (Phase != RoguePhase.Flying || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            // Same lost-frame bound as AirMission, with effect boundaries integrated exactly.
            float remaining = Math.Min(.25f, dt);
            while (remaining > .000001f && Phase == RoguePhase.Flying)
            {
                float tick = Math.Min(FlightDynamics.FixedStep, remaining);
                for (int i = 0; i < 2; i++) if (ActiveSeconds[i] > .000001f) tick = Math.Min(tick, ActiveSeconds[i]);
                if (bots) Mission.StepBots(tick); else Mission.Step(input, tick);
                bool expired = false;
                for (int i = 0; i < 2; i++)
                {
                    if (ActiveSeconds[i] <= 0f) continue;
                    ActiveSeconds[i] = Math.Max(0f, ActiveSeconds[i] - tick);
                    if (ActiveSeconds[i] < .00001f) { ActiveSeconds[i] = 0f; expired = true; }
                }
                if (expired) RebuildEquipment();
                Hull = Math.Max(0f, Mission.Player.Health);
                remaining -= tick;
                if (Mission.Outcome != MissionOutcome.Flying) FinishSortie();
            }
        }

        private void RebuildEquipment()
        {
            loadout.Rebuild(mods, Equipped, ActiveSeconds);
            if (Mission == null) return;
            loadout.ApplyTo(Mission.Player);
            bool rally = false;
            for (int i = 0; i < 2; i++) if (Equipped[i] == RogueAbility.Rally && ActiveSeconds[i] > 0f) rally = true;
            foreach (AircraftState wingman in Mission.Wingmen)
            {
                AircraftDefinition original, actual;
                if (!wingBaseline.TryGetValue(wingman.Id, out original) || !wingDefinitions.TryGetValue(wingman.Id, out actual)) continue;
                RogueCatalog.Copy(original, actual); wingman.Definition = actual;
                if (rally) actual.MaxPower *= 1.15f;
                wingman.SpreadMultiplier = rally ? .60f : 1f;
            }
            if (rallyActive && !rally)
            {
                // Explicit player orders issued during the effect take precedence over restoration.
                if (Mission.CurrentOrder == SquadronOrder.CoverMe) Mission.IssueOrder(beforeRally);
                rallyActive = false;
            }
        }

        public bool FinishSortie()
        {
            if (Phase != RoguePhase.Flying || Mission == null || Mission.Outcome == MissionOutcome.Flying
                || lastCompletedStage == Stage) return false;
            lastCompletedStage = Stage;
            Hull = Math.Max(0f, Math.Min(100f, Mission.Player.Health));
            foreach (AircraftState wingman in Mission.Wingmen)
            {
                int slot = wingman.Id - 1;
                if (slot >= 0 && slot < 2) WingmanHull[slot] = wingman.Alive ? Math.Max(0f, Math.Min(100f, wingman.Health)) : 0f;
            }
            TotalKills += Mission.Kills; TotalHits += Mission.Hits; TotalRounds += Mission.RoundsFired; FlightTime += Mission.Time;
            Array.Clear(ActiveSeconds, 0, 2); RebuildEquipment();
            if (Mission.Outcome == MissionOutcome.Defeat || Hull <= 0f)
            {
                Phase = RoguePhase.Defeat;
                Summary = "Operation lost after " + SortiesWon + " successful sorties. " + Mission.Summary;
                LastMessage = "The field kits, supplies and aircraft belonged to this run. A fresh operation starts from an ordinary Kestrel.";
                return true;
            }
            SortiesWon++;
            Supplies += SelectedRoute.SupplyReward;
            int repair = ModLevel(RogueMod.FieldMechanic) * 12;
            Hull = Math.Min(100f, Hull + repair);
            for (int i = 0; i < 2; i++) if (WingmanHull[i] > 0f) WingmanHull[i] = Math.Min(100f, WingmanHull[i] + repair);
            if (Stage == SortieCount - 1)
            {
                Phase = RoguePhase.Victory;
                Summary = "Blockade broken. All four sorties completed in your " + BuildIdentity.ToLowerInvariant() + ".";
                LastMessage = "Operation Afterburn complete. " + TotalKills + " aircraft destroyed; " + (int)Hull + " hull brought home.";
            }
            else
            {
                Phase = RoguePhase.Reward; GenerateRewards();
                Summary = "Sortie won. " + SelectedRoute.SupplyReward + " supply crates recovered.";
                LastMessage = "Choose one field kit, tactical ability, or repair. Hangar repairs are separate; your choices persist for this run.";
            }
            return true;
        }

        public bool ChooseReward(int index, int replaceAbilitySlot = 1)
        {
            if (Phase != RoguePhase.Reward || index < 0 || index >= rewards.Count || replaceAbilitySlot < 0 || replaceAbilitySlot >= 2) return false;
            RogueReward reward = rewards[index];
            if (reward.Kind == RogueRewardKind.Modification)
            {
                if (!InstallMod(reward.Mod)) return false;
            }
            else if (reward.Kind == RogueRewardKind.Ability)
            {
                if (ownedAbilities.Contains(reward.Ability) || !ValidAbility(reward.Ability) || reward.Ability == RogueAbility.None) return false;
                ownedAbilities.Add(reward.Ability);
                int slot = Equipped[0] == RogueAbility.None ? 0 : Equipped[1] == RogueAbility.None ? 1 : replaceAbilitySlot;
                Equipped[slot] = reward.Ability;
            }
            else
            {
                Hull = Math.Min(100f, Hull + 40f);
                for (int i = 0; i < 2; i++) if (WingmanHull[i] > 0f) WingmanHull[i] = Math.Min(100f, WingmanHull[i] + 25f);
                Supplies++;
            }
            LastMessage = reward.Title + " secured. Next sortie re-arms fuel, ammunition and tactical charges.";
            Stage++; Phase = RoguePhase.Planning;
            Summary = "Operation Afterburn / sortie " + (Stage + 1) + " of " + SortieCount;
            rewards.Clear(); RebuildEquipment(); GenerateRoutes(); return true;
        }

        public void Abandon()
        {
            if (Phase == RoguePhase.Victory || Phase == RoguePhase.Defeat) return;
            Array.Clear(ActiveSeconds, 0, 2); RebuildEquipment();
            Phase = RoguePhase.Defeat; Summary = "Operation abandoned after " + SortiesWon + " victories.";
            LastMessage = "Start a fresh run to return with a new route and equipment deck.";
        }

        private void GenerateRoutes()
        {
            routes.Clear();
            var random = new Random(unchecked(Seed ^ (Stage + 1) * 0x195ab31 ^ routeHistory * 7919));
            MissionKind[] kinds = Stage == 0 ? new[] { MissionKind.Duel, MissionKind.ConvoyCover, MissionKind.GroundAttack }
                : Stage == 1 ? new[] { MissionKind.BomberEscort, MissionKind.Interception, MissionKind.AceHunt }
                : Stage == 2 ? new[] { MissionKind.GroundAttack, MissionKind.FleetBattle, MissionKind.AceHunt }
                : new[] { MissionKind.BomberEscort, MissionKind.FleetBattle, MissionKind.AceHunt };
            for (int i = 0; i < kinds.Length; i++)
            {
                MissionKind kind = kinds[i]; int danger = i + 1;
                string title = kind == MissionKind.Duel ? "Fighter sweep" : kind == MissionKind.ConvoyCover ? "Courier gamble"
                    : kind == MissionKind.BomberEscort ? "Cloud convoy" : kind == MissionKind.GroundAttack ? "Valley interdiction"
                    : kind == MissionKind.AceHunt ? "The ace's challenge" : kind == MissionKind.FleetBattle ? "Squadron clash" : "Raid breaker";
                int pressure = Stage < 2 ? 0 : kind == MissionKind.AceHunt ? Stage - 1
                    : kind == MissionKind.FleetBattle ? Stage - 2 : 1;
                string detail = kind == MissionKind.Duel ? "One fighter; no wingmen. Opening patrol for testing your handling. Control equipment in salvage."
                    : kind == MissionKind.ConvoyCover ? "Protect Lark from two attackers. Solo escort; positioning matters. Precision equipment in salvage."
                    : kind == MissionKind.BomberEscort ? pressure == 0
                        ? "Deliver two of three bombers. Your first squadron assignment; stay with the formation. Armor in salvage."
                        : "Final escort: deliver two bombers while a fresh enemy flight joins the attack. Protect the formation through the handover."
                    : kind == MissionKind.GroundAttack ? pressure == 0
                        ? "Risky early strike: four trucks, flak and two fighters. Wingmen join; cannon equipment in salvage."
                        : "Hardened strike: four trucks and flak, plus a fresh defending fighter during the attack. Heavy gun salvage."
                    : kind == MissionKind.AceHunt ? pressure == 0
                        ? "Ace Voss and one escort. Stock aircraft, superior energy tactics. Defeat the ace; handling equipment in salvage."
                        : pressure == 1 ? "Ace Voss with two escorts. Coordinate your wingmen and deny his climb advantage. Handling equipment in salvage."
                        : "Final ace: Voss and two escorts start with a height advantage. Beat disciplined energy tactics using your finished aircraft build."
                    : kind == MissionKind.FleetBattle ? pressure == 0
                        ? "Two friendly flights versus five fighters in two waves. Defeat three and face both flights. Ammunition salvage."
                        : "Final air battle: seven fighters in three waves. Defeat four and face every flight. Blue flight supports your squadron."
                    : "Cross onto the raid's diagonal track; stop both leaders before the boundary. Ignore escort distractions. Engine salvage.";
                if (Stage == SortieCount - 1) title = "FINAL / " + title;
                routes.Add(new RogueRoute { Title = title, Description = detail, EncounterPressure = pressure,
                    Kind = kind, Seed = random.Next(), Danger = danger, SupplyReward = danger + (Stage == 0 ? 0 : 1) });
            }
            RefitOffer = null; RefitPurchased = false;
            if (Stage > 0)
            {
                var eligible = new List<RogueMod>();
                foreach (RogueMod mod in Enum.GetValues(typeof(RogueMod)))
                    if (ModLevel(mod) < MaxModLevel && (ModLevel(mod) > 0 || mods.Count < MaxInstallations)) eligible.Add(mod);
                if (eligible.Count > 0)
                {
                    RogueMod mod = eligible[random.Next(eligible.Count)];
                    RefitOffer = new RogueReward { Kind = RogueRewardKind.Modification, Mod = mod,
                        Title = RogueCatalog.ModName(mod) + " / Mk " + (ModLevel(mod) + 1),
                        Description = RogueCatalog.ModDescription(mod) + " Workshop cost: 3 supplies; once per stop." };
                }
            }
        }

        private void GenerateRewards()
        {
            rewards.Clear();
            var random = new Random(unchecked(Seed ^ SelectedRoute.Seed ^ (Stage + 7) * 104729));
            RogueMod focus = SelectedRoute.Kind == MissionKind.Duel ? RogueMod.ControlCables
                : SelectedRoute.Kind == MissionKind.ConvoyCover ? RogueMod.HarmonizedGuns
                : SelectedRoute.Kind == MissionKind.BomberEscort ? RogueMod.Armor
                : SelectedRoute.Kind == MissionKind.GroundAttack ? RogueMod.Cannon
                : SelectedRoute.Kind == MissionKind.AceHunt ? RogueMod.Lightframe
                : SelectedRoute.Kind == MissionKind.FleetBattle ? RogueMod.AmmoBays : RogueMod.Supercharger;
            var pool = new List<RogueMod>();
            foreach (RogueMod mod in Enum.GetValues(typeof(RogueMod)))
                if (ModLevel(mod) < MaxModLevel && (ModLevel(mod) > 0 || mods.Count < MaxInstallations)) pool.Add(mod);
            if (pool.Remove(focus)) AddModReward(focus);
            while (rewards.Count < 2 && pool.Count > 0)
            { int index = random.Next(pool.Count); RogueMod mod = pool[index]; pool.RemoveAt(index); AddModReward(mod); }
            var abilities = new List<RogueAbility>();
            foreach (RogueAbility ability in Enum.GetValues(typeof(RogueAbility)))
                if (ability != RogueAbility.None && !ownedAbilities.Contains(ability)) abilities.Add(ability);
            if (abilities.Count > 0)
            {
                // Solo encounters never force the only card reward to require absent wingmen.
                if (!Mission.Tactical && abilities.Count > 1) abilities.Remove(RogueAbility.Rally);
                RogueAbility ability = abilities[random.Next(abilities.Count)];
                rewards.Add(new RogueReward { Kind = RogueRewardKind.Ability, Ability = ability,
                    Title = RogueCatalog.AbilityName(ability), Description = RogueCatalog.AbilityDescription(ability) });
            }
            else if (pool.Count > 0) AddModReward(pool[random.Next(pool.Count)]);
            rewards.Add(new RogueReward { Kind = RogueRewardKind.FieldRepair, Title = "Repair instead of equipment",
                Description = "Restore 40 hull, 25 hull to surviving wingmen, and gain 1 supply crate. Always available; lost pilots stay lost." });
        }

        private void AddModReward(RogueMod mod)
        {
            rewards.Add(new RogueReward { Kind = RogueRewardKind.Modification, Mod = mod,
                Title = RogueCatalog.ModName(mod) + " / Mk " + (ModLevel(mod) + 1),
                Description = RogueCatalog.ModDescription(mod) + (ModLevel(mod) > 0 ? " Second level compounds these effects." : "") });
        }

        private bool InstallMod(RogueMod mod)
        {
            int level = ModLevel(mod);
            if (level >= MaxModLevel || (level == 0 && mods.Count >= MaxInstallations)) return false;
            foreach (RogueInstallation installed in mods)
                if (installed.Mod == mod) { installed.Level++; return true; }
            mods.Add(new RogueInstallation { Mod = mod, Level = 1 }); return true;
        }

        private static bool ValidAbility(RogueAbility ability)
        { return ability >= RogueAbility.None && ability <= RogueAbility.Rally; }
    }
}
