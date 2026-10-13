using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace Meridian
{
    /// <summary>Campaign rules fixtures and separately identified controls-only combat trials.
    /// Fixture victories are deliberately never counted as evidence of playable sorties.</summary>
    public static class RogueValidation
    {
        const float Tick = 1f / 60f;
        public static string Summary { get; private set; } = "Roguelite validation has not run.";

        public static void Run(Action<bool, string> check)
        {
            string originalDefinitions = DefinitionsSignature();
            string ordinaryBefore = OrdinaryReplay();
            var report = new StringBuilder("ROGUELITE VALIDATION\nState fixtures and actual combat runs are reported separately.\n");
            ValidateRoutes(check);
            ValidateTransitions(check);
            ValidateEconomyAndCaps(check);
            ValidateAbilities(check);
            ValidateUpgrades(check);
            ValidateModifiedCombat(check);
            ValidateActualRuns(check, report);
            check(DefinitionsSignature() == originalDefinitions,
                "Roguelite: all shared aircraft definition fields remain bit-for-bit unchanged after upgrades and abilities");
            check(OrdinaryReplay() == ordinaryBefore,
                "Roguelite: all five ordinary mission trajectories, health, ammunition and objectives exactly reproduce after campaign use");
            Summary = report.ToString();
        }

        static void ValidateRoutes(Action<bool, string> check)
        {
            var signatures = new HashSet<string>();
            bool deterministic = true, valid = true, distinct = true;
            for (int seed = -50; seed < 150; seed++)
            {
                var a = new RogueRun(seed); var b = new RogueRun(seed);
                string signature = RoutesSignature(a);
                signatures.Add(signature);
                deterministic &= signature == RoutesSignature(b);
                valid &= a.Phase == RoguePhase.Planning && a.Stage == 0 && a.Hull > 0 && a.Supplies >= 0
                    && a.Routes.Count >= 2 && a.Equipped.Length == 2;
                for (int i = 0; i < a.Routes.Count; i++)
                {
                    var route = a.Routes[i];
                    valid &= !string.IsNullOrEmpty(route.Title) && !string.IsNullOrEmpty(route.Description)
                        && route.SupplyReward > 0 && route.Danger >= 0;
                    for (int j = i + 1; j < a.Routes.Count; j++)
                        distinct &= route.Title != a.Routes[j].Title || route.Kind != a.Routes[j].Kind;
                }
            }
            check(deterministic, "Roguelite: 200 seeded planning decks reproduce exactly");
            check(valid && distinct, "Roguelite: 200 fresh runs offer valid, distinct, nonempty route choices and a live aircraft");
            check(signatures.Count >= 100, "Roguelite: 200 signed seeds produce at least 100 distinct route and sortie decks");
            check(RoutesSignature(new RogueRun(int.MinValue)) == RoutesSignature(new RogueRun(int.MinValue)),
                "Roguelite: minimum integer seed is supported deterministically");
        }

        static void ValidateTransitions(Action<bool, string> check)
        {
            var run = new RogueRun(1944);
            check(run.LaunchRoute(-1) == null && run.LaunchRoute(999) == null && run.Phase == RoguePhase.Planning,
                "Roguelite: invalid route indices cannot consume the run or launch a sortie");
            check(!run.ChooseReward(0) && !run.ActivateAbility(0),
                "Roguelite: planning cannot claim unearned rewards or spend airborne equipment");
            bool valid = true, idempotent = true, damageCarries = true, independent = true;
            AirMission lastMission = null;
            for (int stage = 0; stage < RogueRun.SortieCount; stage++)
            {
                float launchHull = run.Hull;
                AirMission mission = run.LaunchRoute(stage % run.Routes.Count);
                valid &= mission != null && run.Phase == RoguePhase.Flying && run.Stage == stage;
                independent &= mission != lastMission;
                damageCarries &= mission.Player.Health == launchHull;
                lastMission = mission;
                valid &= run.LaunchRoute(0) == null && !run.ChooseReward(0);
                mission.Player.Health = Math.Min(mission.Player.Health, 63f - stage * 4f);
                float expectedHull = mission.Player.Health;
                // Explicit objective-state fixture: this checks only campaign synchronization.
                CompleteFixture(mission);
                run.FinishSortie();
                damageCarries &= Math.Abs(run.Hull - expectedHull) < .01f;
                int supplies = run.Supplies; float hull = run.Hull; RoguePhase phase = run.Phase;
                run.FinishSortie(); run.FinishSortie();
                idempotent &= supplies == run.Supplies && hull == run.Hull && phase == run.Phase;
                if (stage == RogueRun.SortieCount - 1)
                {
                    valid &= run.Phase == RoguePhase.Victory && run.LaunchRoute(0) == null && !run.ChooseReward(0);
                    break;
                }
                valid &= run.Phase == RoguePhase.Reward && run.Rewards.Count >= 2;
                valid &= !run.ChooseReward(-1) && !run.ChooseReward(999) && run.Phase == RoguePhase.Reward;
                valid &= run.ChooseReward(0) && run.Phase == RoguePhase.Planning && run.Stage == stage + 1;
                int after = run.Supplies;
                idempotent &= !run.ChooseReward(0) && run.Supplies == after;
            }
            check(valid, "Roguelite: four explicit objective fixtures traverse launch, reward, planning and terminal victory without invalid reentry");
            check(idempotent, "Roguelite: duplicate debriefs and reward submits never grant duplicate supplies or rewards");
            check(damageCarries && independent, "Roguelite: remaining hull carries into distinct fresh mission instances");
            var loss = new RogueRun(81); var failed = loss.LaunchRoute(0);
            failed.Player.Alive = false; failed.Player.Health = 0;
            loss.Step(new FlightInput(), Tick);
            check(loss.Phase == RoguePhase.Defeat && loss.Hull == 0 && loss.LaunchRoute(0) == null && !loss.ChooseReward(0),
                "Roguelite: losing the aircraft terminates the run and cannot collect victory rewards");
            float time = failed.Time; loss.Step(new FlightInput { Fire = true, Throttle = 1 }, 1f);
            check(failed.Time == time && !loss.ActivateAbility(0), "Roguelite: terminal runs freeze simulation and tactical activations");
            var fresh = new RogueRun(81);
            check(fresh.Phase == RoguePhase.Planning && fresh.Hull == 100 && fresh.Stage == 0
                && fresh.Mods.Count == 0 && fresh.Mission == null,
                "Roguelite: retry creates a clean run with no carried destruction, mission or modifications");
        }

        static void ValidateAbilities(Action<bool, string> check)
        {
            var run = new RogueRun(1944); AirMission mission = run.LaunchRoute(0);
            float power = mission.Player.Definition.MaxPower, health = mission.Player.Health;
            int charges = run.Charges[0];
            check(!run.ActivateAbility(-1) && !run.ActivateAbility(2) && !run.ActivateAbility(1),
                "Roguelite: invalid and empty ability slots cannot activate");
            check(run.ActivateAbility(0) && run.Charges[0] == charges - 1 && run.ActiveSeconds[0] > 0
                && mission.Player.Definition.MaxPower > power && mission.Player.Health < health,
                "Roguelite: emergency boost consumes one charge, adds real engine power and incurs disclosed hull wear");
            float remaining = run.ActiveSeconds[0], hull = mission.Player.Health;
            check(!run.ActivateAbility(0) && run.Charges[0] == charges - 1 && run.ActiveSeconds[0] == remaining,
                "Roguelite: held/repeated activation cannot stack an already active ability");
            float elapsed = mission.Time;
            run.Step(new FlightInput(), float.NaN); run.Step(new FlightInput(), -1); run.Step(new FlightInput(), 0);
            check(run.ActiveSeconds[0] == remaining && mission.Player.Health == hull && mission.Time == elapsed,
                "Roguelite: invalid or zero simulation deltas cannot expire an ability or damage an aircraft");
            run.Step(new FlightInput { Throttle = .8f }, 30f);
            check(Math.Abs((remaining - run.ActiveSeconds[0]) - (mission.Time - elapsed)) < .001f,
                "Roguelite: ability duration follows actual bounded simulation time during a long render frame");
            for (int i = 0; i < 20 * 60 && run.Phase == RoguePhase.Flying; i++)
                run.Step(new FlightInput { Throttle = .8f }, Tick);
            check(run.ActiveSeconds[0] == 0 && mission.Player.Definition.MaxPower == power,
                "Roguelite: expired boost restores the exact permanent engine specification");
            check(run.ActivateAbility(0) && run.Charges[0] == 0,
                "Roguelite: the second boost use is available after expiration");
            for (int i = 0; i < 20 * 60 && run.Phase == RoguePhase.Flying; i++)
                run.Step(new FlightInput { Throttle = .8f }, Tick);
            check(!run.ActivateAbility(0) && run.Charges[0] == 0,
                "Roguelite: expended equipment cannot activate without remaining sortie charges");

            foreach (RogueAbility ability in new[] { RogueAbility.ArmorPiercing, RogueAbility.CombatFlaps, RogueAbility.Rally })
            {
                var acquired = FindAbilityReward(ability);
                check(acquired != null, "Roguelite: " + ability + " is actually obtainable from generated mission rewards");
                if (acquired == null) continue;
                check(acquired.EquipAbility(1, ability), "Roguelite: earned " + ability + " can be equipped between sorties");
                int tacticalRoute = 0;
                for (int i = 0; i < acquired.Routes.Count; i++)
                    if (acquired.Routes[i].Kind == MissionKind.BomberEscort || acquired.Routes[i].Kind == MissionKind.Interception)
                    { tacticalRoute = i; break; }
                var sortie = acquired.LaunchRoute(tacticalRoute);
                float wing = sortie.Player.Definition.WingArea, drag = sortie.Player.Definition.BaseDrag;
                float damage = sortie.Player.DamageMultiplier, rate = sortie.Player.FireRateMultiplier;
                bool activated = acquired.ActivateAbility(1);
                check(activated && acquired.ActiveSeconds[1] > 0, "Roguelite: " + ability + " activates from its second cockpit slot");
                check(!acquired.EquipAbility(1, RogueAbility.Overboost), "Roguelite: in-flight loadout cannot be swapped to evade " + ability + " charges");
                if (ability == RogueAbility.ArmorPiercing)
                    check(sortie.Player.DamageMultiplier > damage && sortie.Player.FireRateMultiplier > rate,
                        "Roguelite: AP equipment affects authoritative projectile damage and ammunition expenditure rate");
                if (ability == RogueAbility.CombatFlaps)
                    check(sortie.Player.Definition.WingArea > wing && sortie.Player.Definition.BaseDrag > drag,
                        "Roguelite: combat flaps add aerodynamic lift area and a real drag penalty");
                if (ability == RogueAbility.Rally)
                    check(sortie.Wingmen.Count > 0 && sortie.CurrentOrder == SquadronOrder.CoverMe,
                        "Roguelite: rally commands actual surviving wingmen to cover the player");
                // Explicit fixture end also tests that completing a sortie removes temporary effects.
                CompleteFixture(sortie); acquired.FinishSortie();
                check(acquired.ActiveSeconds[0] == 0 && acquired.ActiveSeconds[1] == 0,
                    "Roguelite: debrief clears every temporary timer after " + ability);
            }
        }

        static void ValidateEconomyAndCaps(Action<bool, string> check)
        {
            var initial = new RogueRun(1944);
            check(!initial.BuyRefit() && !initial.RepairHull() && !initial.RecruitWingman(0)
                && !initial.RepairWingman(-1) && !initial.RecruitWingman(2) && initial.Supplies == 4,
                "Roguelite: unavailable refits, full-health repairs and invalid crew slots never consume supplies");
            bool bounded = true, available = true, paidOnce = true, noSoftlock = true;
            for (int seed = 0; seed < 100; seed++)
            {
                var run = new RogueRun(seed);
                for (int stage = 0; stage < RogueRun.SortieCount - 1; stage++)
                {
                    var mission = run.LaunchRoute((seed + stage) % run.Routes.Count);
                    mission.Player.Health = 48f;
                    if (mission.Wingmen.Count > 0)
                    {
                        mission.Wingmen[0].Alive = false; mission.Wingmen[0].Health = 0;
                        if (mission.Wingmen.Count > 1) mission.Wingmen[1].Health = 31;
                    }
                    CompleteFixture(mission); run.FinishSortie();
                    bool repairOffered = false;
                    foreach (var reward in run.Rewards)
                    {
                        if (reward.Kind == RogueRewardKind.FieldRepair) repairOffered = true;
                        if (reward.Kind == RogueRewardKind.Modification)
                            available &= run.ModLevel(reward.Mod) < RogueRun.MaxModLevel
                                && (run.ModLevel(reward.Mod) > 0 || run.Mods.Count < RogueRun.MaxInstallations);
                    }
                    available &= repairOffered && run.ChooseReward(0);
                    if (run.RefitOffer != null && run.Supplies >= 3)
                    {
                        int before = run.Supplies; RogueMod mod = run.RefitOffer.Mod; int level = run.ModLevel(mod);
                        bool bought = run.BuyRefit();
                        paidOnce &= bought && run.Supplies == before - 3 && run.RefitPurchased && run.ModLevel(mod) == level + 1;
                        paidOnce &= !run.BuyRefit() && run.Supplies == before - 3;
                    }
                    while (run.RepairHull()) { }
                    foreach (var installation in run.Mods) bounded &= installation.Level > 0 && installation.Level <= RogueRun.MaxModLevel;
                    bounded &= run.Mods.Count <= RogueRun.MaxInstallations && run.Supplies >= 0 && run.Hull > 0 && run.Hull <= 100;
                    // Route launches never cost supplies, including after choosing equipment over repairs.
                    noSoftlock &= run.Phase == RoguePhase.Planning && run.Routes.Count >= 2;
                }
            }
            check(available, "Roguelite: 300 generated reward decks omit capped equipment and always provide a repair alternative");
            check(paidOnce, "Roguelite: paid refits spend exactly three crates once per planning visit and cannot be rerolled or duplicated");
            check(bounded && noSoftlock, "Roguelite: equipment and repair competition respects installation/level caps and never blocks a surviving aircraft from its next route");

            var crew = new RogueRun(1944); var escort = crew.LaunchRoute(2);
            escort.Wingmen[0].Alive = false; escort.Wingmen[0].Health = 0;
            escort.Wingmen[1].Health = 31; escort.Player.Health = 41;
            CompleteFixture(escort); crew.FinishSortie();
            crew.ChooseReward(0);
            check(crew.WingmanHull[0] == 0 && crew.WingmanHull[1] == 31 && !crew.RepairWingman(0),
                "Roguelite: dead pilots remain lost and cannot be revived by a cheap repair");
            int supplies = crew.Supplies;
            check(crew.RepairWingman(1) && crew.WingmanHull[1] == 71 && crew.Supplies == supplies - 1,
                "Roguelite: surviving wingman repair restores forty hull for one crate");
            var next = crew.LaunchRoute(2);
            bool missing = true; foreach (var wingman in next.Wingmen) if (wingman.Id == 1) missing = false;
            check(missing && next.Wingmen.Count == 1 && next.Wingmen[0].Health == 71,
                "Roguelite: the next squadron sortie omits a lost pilot and carries repaired survivor health");
        }

        static void ValidateUpgrades(Action<bool, string> check)
        {
            var loadout = new RogueLoadout();
            var empty = new List<RogueInstallation>();
            var equipped = new[] { RogueAbility.None, RogueAbility.None }; var inactive = new float[2];
            loadout.Rebuild(empty, equipped, inactive);
            string baseline = DefinitionSignature(loadout.Definition);
            check(!ReferenceEquals(loadout.Definition, AircraftDefinition.AlliedFighter),
                "Roguelite: modified aircraft own a private definition instead of mutating the standard fighter");
            bool finite = true, stable = true, physical = true;
            foreach (RogueMod mod in Enum.GetValues(typeof(RogueMod)))
            {
                var mods = new List<RogueInstallation> { new RogueInstallation { Mod = mod, Level = 2 } };
                loadout.Rebuild(mods, equipped, inactive);
                string expected = DefinitionSignature(loadout.Definition);
                float damage = loadout.Damage, fireRate = loadout.FireRate, spread = loadout.Spread;
                for (int i = 0; i < 100; i++) loadout.Rebuild(mods, equipped, inactive);
                stable &= expected == DefinitionSignature(loadout.Definition) && damage == loadout.Damage
                    && fireRate == loadout.FireRate && spread == loadout.Spread;
                finite &= SafeDefinition(loadout.Definition) && loadout.Damage > 0 && loadout.FireRate > 0
                    && loadout.Spread > 0 && loadout.DamageTaken > 0;
                if (mod == RogueMod.Armor) physical &= loadout.Definition.Mass > AircraftDefinition.AlliedFighter.Mass
                    && loadout.DamageTaken < 1 && loadout.Definition.MaxRollRate < AircraftDefinition.AlliedFighter.MaxRollRate;
                if (mod == RogueMod.Cannon) physical &= loadout.Damage > 2 && loadout.FireRate < .5f
                    && loadout.Definition.Ammunition < 1200 && loadout.Definition.Mass > AircraftDefinition.AlliedFighter.Mass;
                if (mod == RogueMod.Lightframe) physical &= loadout.Definition.Mass < AircraftDefinition.AlliedFighter.Mass
                    && loadout.DamageTaken > 1 && loadout.Definition.MaxRollRate > AircraftDefinition.AlliedFighter.MaxRollRate;
                if (mod == RogueMod.Longwing) physical &= loadout.Definition.WingArea > AircraftDefinition.AlliedFighter.WingArea
                    && loadout.Definition.StallSpeed < AircraftDefinition.AlliedFighter.StallSpeed
                    && loadout.Definition.MaxRollRate < AircraftDefinition.AlliedFighter.MaxRollRate;
            }
            check(stable, "Roguelite: rebuilding every double-installed modification 100 times never compounds previous effects");
            check(finite && physical, "Roguelite: modification tradeoffs produce finite aircraft, with distinct armor, cannon, lightframe and lift identities");
            loadout.Rebuild(empty, equipped, inactive);
            check(DefinitionSignature(loadout.Definition) == baseline && loadout.Damage == 1 && loadout.FireRate == 1
                && loadout.Spread == 1 && loadout.DamageTaken == 1,
                "Roguelite: removing all installations exactly restores private baseline performance");
            bool combinations = true;
            foreach (RogueMod first in Enum.GetValues(typeof(RogueMod)))
                foreach (RogueMod second in Enum.GetValues(typeof(RogueMod)))
                {
                    loadout.Rebuild(new List<RogueInstallation> {
                        new RogueInstallation { Mod = first, Level = 2 }, new RogueInstallation { Mod = second, Level = 2 }
                    }, new[] { RogueAbility.Overboost, RogueAbility.CombatFlaps }, new[] { 10f, 10f });
                    var plane = new AircraftState { Definition = loadout.Definition, Position = new Vector3(0, 2200, 0),
                        Velocity = Vector3.UnitZ * 112f, Health = 100, Ammo = loadout.Definition.Ammunition };
                    for (int tick = 0; tick < 180; tick++)
                        FlightDynamics.Step(plane, new FlightInput { Throttle = .9f, Pitch = .12f, Roll = .18f }, Tick, null);
                    combinations &= plane.Alive && FlightDynamics.Finite(plane.Position) && FlightDynamics.Finite(plane.Velocity)
                        && FlightDynamics.Finite(plane.Rotation) && SafeDefinition(plane.Definition);
                }
            check(combinations, "Roguelite: 144 paired double-level builds plus simultaneous boost/flaps remain finite in authoritative aerodynamic flight");
        }
        static void ValidateActualRuns(Action<bool, string> check, StringBuilder report)
        {
            report.AppendLine("ACTUAL CAMPAIGNS: legal route, reward and repair choices; bounded pilot controls; no health, position, ammunition or objective overrides.");
            int wins = 0, sorties = 0, completed = 0;
            foreach (int seed in new[] { 1944, 81, 31337, 0, 17 })
            {
                var run = new RogueRun(seed); bool finite = true;
                int transitions = 0;
                while ((run.Phase == RoguePhase.Planning || run.Phase == RoguePhase.Reward) && transitions++ < 12)
                {
                    if (run.Phase == RoguePhase.Reward)
                    {
                        int choice = 0;
                        if (run.Hull < 65)
                            for (int i = 0; i < run.Rewards.Count; i++)
                                if (run.Rewards[i].Kind == RogueRewardKind.FieldRepair) choice = i;
                        run.ChooseReward(choice);
                    }
                    while (run.Hull < 85 && run.RepairHull()) { }
                    for (int wing = 0; wing < 2; wing++)
                    {
                        if (run.WingmanHull[wing] <= 0) run.RecruitWingman(wing);
                        else if (run.WingmanHull[wing] < 65) run.RepairWingman(wing);
                    }
                    // A coherent fighter build: opening patrol, engine salvage from interception,
                    // then a strike kit (even seeds) or early ace, culminating in the final ace.
                    int routeIndex = run.Stage == 0 ? 0 : run.Stage == 1 ? 1
                        : run.Stage == 2 ? (seed & 1) == 0 ? 0 : 2 : 2;
                    AirMission mission = run.LaunchRoute(routeIndex);
                    if (mission == null) { finite = false; break; }
                    sorties++;
                    var pilot = new ValidationPilot(mission.Seed);
                    var tacticalPilot = new TacticalValidationPilot(mission.Seed);
                    var encounterPilot = new EncounterValidationPilot(mission.Seed);
                    int initialAmmo = mission.Player.Ammo;
                    for (int tick = 0; tick < 60 * 721 && run.Phase == RoguePhase.Flying; tick++)
                    {
                        if (mission.Kind == MissionKind.AceHunt || mission.Kind == MissionKind.FleetBattle)
                            run.Step(encounterPilot.Update(mission), Tick);
                        else if (mission.Kind == MissionKind.BomberEscort || mission.Kind == MissionKind.Interception)
                            run.Step(tacticalPilot.Update(mission), Tick);
                        else if (mission.Tactical) run.StepBots(Tick);
                        else run.Step(pilot.Update(mission), Tick);
                        if (tick % 60 != 0) continue;
                        foreach (var aircraft in mission.Aircraft)
                            finite &= FlightDynamics.Finite(aircraft.Position) && FlightDynamics.Finite(aircraft.Rotation)
                                && FlightDynamics.Finite(aircraft.Velocity) && aircraft.Ammo >= 0 && aircraft.Health >= 0;
                        finite &= mission.Player.Ammo <= initialAmmo && mission.Projectiles.Count < 400;
                    }
                    if (mission.Outcome == MissionOutcome.Victory) completed++;
                    report.Append("Run ").Append(seed).Append(" stage ").Append(run.Stage + 1).Append(' ')
                        .Append(mission.Kind).Append(" seed=").Append(mission.Seed).Append(' ').Append(mission.Outcome)
                        .Append(" t=").Append(mission.Time.ToString("F1", CultureInfo.InvariantCulture))
                        .Append(" hull=").Append(run.Hull.ToString("F1", CultureInfo.InvariantCulture))
                        .Append(" player rounds=").Append(mission.RoundsFired).Append(" hits=").Append(mission.Hits)
                        .Append(" wingmen=").Append(mission.WingmenAlive).AppendLine();
                }
                if (run.Phase == RoguePhase.Victory) wins++;
                check(finite, "Roguelite: actual campaign " + seed + " keeps aircraft, damage, ammunition and projectiles finite and bounded");
                check(run.Phase == RoguePhase.Victory || run.Phase == RoguePhase.Defeat,
                    "Roguelite: actual campaign " + seed + " reaches a terminal run outcome through real sorties");
                report.Append("Campaign ").Append(seed).Append(" result ").Append(run.Phase).Append(" | ").Append(run.Summary).AppendLine();
            }
            check(completed > 0, "Roguelite: run equipment and carried consequences allow actual combat victories");
            check(wins > 0, "Roguelite: at least one complete four-sortie campaign is earned through authoritative combat without outcome overrides");
            report.Append("Earned campaign victories: ").Append(wins).Append("/5; sortie victories ").Append(completed).Append('/').Append(sorties).AppendLine();
        }

        static RogueRun FindAbilityReward(RogueAbility ability)
        {
            for (int seed = 0; seed < 80; seed++)
            {
                var run = new RogueRun(seed);
                for (int stage = 0; stage < RogueRun.SortieCount - 1; stage++)
                {
                    int route = ability == RogueAbility.Rally ? run.Routes.Count - 1 : 0;
                    CompleteFixture(run.LaunchRoute(route)); run.FinishSortie();
                    for (int i = 0; i < run.Rewards.Count; i++)
                        if (run.Rewards[i].Kind == RogueRewardKind.Ability && run.Rewards[i].Ability == ability)
                        { run.ChooseReward(i); return run; }
                    run.ChooseReward(0);
                }
            }
            return null;
        }

        static void ValidateModifiedCombat(Action<bool, string> check)
        {
            var loadout = new RogueLoadout();
            loadout.Rebuild(new List<RogueInstallation> { new RogueInstallation { Mod = RogueMod.Cannon, Level = 1 } },
                new[] { RogueAbility.ArmorPiercing, RogueAbility.None }, new[] { 9f, 0f });
            var shooter = new AircraftState { Id = 0, Team = 0, Role = AircraftRole.Player,
                Position = new Vector3(0, 1800, 0), Velocity = Vector3.UnitZ * 112f, Ammo = 100 };
            loadout.ApplyTo(shooter);
            var rounds = new List<Projectile>(); var guns = new Weapons(81);
            guns.Fire(shooter, true, Tick, rounds);
            float projectileDamage = rounds[0].Damage;
            check(Math.Abs(projectileDamage - Weapons.RoundDamage * 1.65f * 1.6f) < .001f && shooter.Ammo == 99,
                "Roguelite: cannon and AP synergy is carried by a real fired projectile while consuming ammunition");
            loadout.Rebuild(new List<RogueInstallation>(), new[] { RogueAbility.None, RogueAbility.None }, new float[2]);
            loadout.ApplyTo(shooter);
            check(rounds[0].Damage == projectileDamage && shooter.DamageMultiplier == 1,
                "Roguelite: rounds already in flight retain their launch damage after temporary equipment expires");

            var armored = new AircraftState { Id = 4, Team = 1, Position = new Vector3(0, 1800, 150), Health = 100,
                DamageTakenMultiplier = .72f, Definition = AircraftDefinition.AxisFighter };
            // Collision fixture uses one launched projectile relocated onto a known swept collision segment.
            rounds[0].Position = armored.Position - Vector3.UnitZ * 4;
            rounds[0].Velocity = Vector3.UnitZ * 760;
            guns.Step(Tick, rounds, new List<AircraftState> { armored }, new Dictionary<int, Vector3> { { armored.Id, armored.Position } },
                new TerrainField(81), new List<CombatEvent>());
            check(rounds.Count == 0 && Math.Abs(armored.Health - (100 - projectileDamage * .72f)) < .001f,
                "Roguelite: swept physical hits apply equipment resistance without granting immunity");

            var standard = new AircraftState { Position = new Vector3(0, 2400, 0), Velocity = Vector3.UnitZ * 112f,
                Definition = RogueCatalog.Clone(AircraftDefinition.AlliedFighter) };
            var boosted = new AircraftState { Position = standard.Position, Velocity = standard.Velocity };
            loadout.Rebuild(new List<RogueInstallation>(), new[] { RogueAbility.Overboost, RogueAbility.None }, new[] { 15f, 0f });
            loadout.ApplyTo(boosted);
            for (int i = 0; i < 300; i++)
            {
                FlightDynamics.Step(standard, new FlightInput { Throttle = 1 }, Tick, null);
                FlightDynamics.Step(boosted, new FlightInput { Throttle = 1 }, Tick, null);
            }
            check(boosted.Speed > standard.Speed + 3f && boosted.Position.Z > standard.Position.Z,
                "Roguelite: emergency engine power produces measurable acceleration and distance through existing aerodynamic forces");
        }

        static bool SafeDefinition(AircraftDefinition d)
        {
            return d.Mass > 500 && d.Mass < 15000 && d.WingArea > 8 && d.WingArea < 100
                && d.MaxPower > 0 && d.MaxThrust > 0 && d.BaseDrag > 0 && d.StallSpeed > 20
                && d.StallSpeed < d.MaxSpeed && d.MaxRollRate > 0 && d.MaxPitchRate > 0 && d.Ammunition > 0;
        }

        public static void CompleteFixture(AirMission mission)
        {
            if (mission.Kind == MissionKind.FleetBattle)
            {
                // Explicit objective fixture only: advance the schedule, then defeat spawned forces.
                // This is never used by the controls-only campaign trials below.
                typeof(AirMission).GetProperty("Time").SetValue(mission, 200f);
                mission.Step(new FlightInput { Throttle = .78f }, Tick);
                foreach (AircraftState aircraft in mission.Aircraft) if (aircraft.Team != mission.Player.Team) aircraft.Alive = false;
            }
            if (mission.Kind == MissionKind.BomberEscort)
            {
                Vector3 direction = Vector3.Normalize(mission.RouteDestination - mission.RouteStart);
                for (int i = 0; i < mission.RequiredBombers; i++)
                    mission.Bombers[i].Position = mission.RouteDestination + direction * (100 + i * 300);
            }
            else if (mission.Kind == MissionKind.GroundAttack)
            { foreach (GroundTarget target in mission.GroundTargets) if (target.IsObjective) target.Alive = false; }
            else if (mission.Kind == MissionKind.Interception)
            { foreach (AircraftState raider in mission.Raiders) raider.Alive = false; }
            else if (mission.Kind != MissionKind.FleetBattle)
            { foreach (AircraftState aircraft in mission.Aircraft) if (aircraft.Team != mission.Player.Team) aircraft.Alive = false; }
            mission.Step(new FlightInput { Throttle = .78f }, Tick);
        }

        static string RoutesSignature(RogueRun run)
        {
            var text = new StringBuilder();
            foreach (var route in run.Routes)
                text.Append(route.Title).Append('|').Append(route.Description).Append('|').Append(route.Kind)
                    .Append('|').Append(route.Seed).Append('|').Append(route.Danger).Append('|').Append(route.SupplyReward).Append(';');
            return text.ToString();
        }

        static string DefinitionsSignature()
        {
            return DefinitionSignature(AircraftDefinition.AlliedFighter) + DefinitionSignature(AircraftDefinition.AxisFighter)
                + DefinitionSignature(AircraftDefinition.Bomber) + DefinitionSignature(AircraftDefinition.Courier);
        }

        static string DefinitionSignature(AircraftDefinition definition)
        {
            var text = new StringBuilder();
            foreach (FieldInfo field in typeof(AircraftDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = field.GetValue(definition);
                text.Append(field.Name).Append('=').Append(value is float ? ((float)value).ToString("R", CultureInfo.InvariantCulture) : value).Append(';');
            }
            return text.ToString();
        }

        static string OrdinaryReplay()
        {
            var text = new StringBuilder();
            foreach (MissionKind kind in Enum.GetValues(typeof(MissionKind)))
            {
                var mission = new AirMission(31337, kind);
                for (int i = 0; i < 1200; i++)
                {
                    mission.StepBots(Tick);
                    if (i % 60 != 0) continue;
                    text.Append(kind).Append(':').Append(mission.Time).Append(':').Append(mission.Outcome)
                        .Append(':').Append(mission.TotalRoundsFired).Append(':').Append(mission.ObjectiveStatus);
                    foreach (AircraftState aircraft in mission.Aircraft)
                        text.Append('|').Append(aircraft.Position).Append('|').Append(aircraft.Velocity).Append('|')
                            .Append(aircraft.Rotation).Append('|').Append(aircraft.Health).Append('|').Append(aircraft.Ammo);
                }
            }
            return text.ToString();
        }
    }
}
