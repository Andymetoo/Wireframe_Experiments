using System;
using System.Collections;
using Meridian;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NVector3 = System.Numerics.Vector3;

namespace VectorCabinet
{
    public sealed partial class CabinetSmoke
    {
        IEnumerator ExerciseRoguelite(AirCombatGame air)
        {
            air.DiagnosticControl = true;
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current;
            testKeyboard = InputSystem.AddDevice<Keyboard>("Afterburn diagnostic keyboard");
            testMouse = InputSystem.AddDevice<Mouse>("Afterburn diagnostic mouse");
            testKeyboard.MakeCurrent(); testMouse.MakeCurrent();
            try
            {
                air.ShowStandardMissions(); air.SeedText = "1944";
                yield return Capture("40-standard-and-roguelite-selection");
                yield return SquadronKey(Key.Digit6);
                Check(air.Campaign != null && air.Campaign.Phase == RoguePhase.Planning,
                    "Roguelite: physical briefing key 6 enters a separately owned run planning screen");
                Check(air.Campaign.Stage == 0 && air.Campaign.Mission == null && air.Campaign.Routes.Count >= 2,
                    "Roguelite: new operation offers routes before launching any campaign simulation");
                yield return Capture("41-afterburn-operations");
                float throttle = air.Controls.Throttle;
                yield return RogueWheel(3);
                Check(air.Controls.Throttle == throttle && air.Campaign.Mission == null,
                    "Roguelite: scrolling the operations screen cannot move throttle or start simulation");

                air.LaunchRogueRoute(0);
                Check(air.Mission == air.Campaign.Mission && air.Campaign.Phase == RoguePhase.Flying,
                    "Roguelite: route launch uses the authoritative flight mission and renderer");
                yield return ExerciseWheelInFlight(air, "roguelite");
                air.DiagnosticControl = false;
                int uses = air.Campaign.Charges[0];
                yield return SquadronKey(Key.Digit5);
                Check(air.Campaign.Charges[0] == uses - 1 && air.Campaign.ActiveSeconds[0] > 0,
                    "Roguelite: physical key 5 activates the first tactical ability once during real Update");
                yield return Capture("42-afterburn-live-ability");
                air.SetPaused(true);
                float effect = air.Campaign.ActiveSeconds[0], time = air.Mission.Time;
                yield return RogueWheel(4); yield return SquadronKey(Key.Digit5);
                Check(air.Campaign.ActiveSeconds[0] == effect && air.Mission.Time == time
                    && air.Campaign.Charges[0] == uses - 1,
                    "Roguelite: pause freezes ability lifetime, mission time and activation edges");
                yield return Capture("43-afterburn-pause");
                air.SetPaused(false); air.DiagnosticControl = true;

                // Complete campaign with legal route/equipment/repair choices and ordinary pilot controls.
                // No fixture mutation is used until this actual run has reached its own outcome.
                air.StartRogueRun(81);
                bool finite = true; int sorties = 0;
                while ((air.Campaign.Phase == RoguePhase.Planning || air.Campaign.Phase == RoguePhase.Reward) && sorties < 4)
                {
                    if (air.Campaign.Phase == RoguePhase.Reward)
                    {
                        yield return Capture("44-afterburn-earned-reward-" + sorties);
                        Check(DescriptionsFit(air),
                            "Roguelite: earned stage " + sorties + " reward descriptions fit the enlarged-font cards");
                        int reward = 0;
                        if (air.Campaign.Hull < 65)
                            for (int i = 0; i < air.Campaign.Rewards.Count; i++)
                                if (air.Campaign.Rewards[i].Kind == RogueRewardKind.FieldRepair) reward = i;
                        air.ChooseRogueReward(reward);
                    }
                    while (air.Campaign.Hull < 85 && air.Campaign.RepairHull()) { }
                    for (int wing = 0; wing < 2; wing++)
                    {
                        if (air.Campaign.WingmanHull[wing] <= 0) air.Campaign.RecruitWingman(wing);
                        else if (air.Campaign.WingmanHull[wing] < 65) air.Campaign.RepairWingman(wing);
                    }
                    yield return Capture("44-afterburn-earned-planning-" + (air.Campaign.Stage + 1));
                    Check(DescriptionsFit(air),
                        "Roguelite: all three routes at stage " + (air.Campaign.Stage + 1) + " fit their enlarged-font descriptions");
                    int[] routePolicy = { 0, 1, 2, 1 };
                    air.LaunchRogueRoute(routePolicy[air.Campaign.Stage]);
                    var pilot = new ValidationPilot(air.Mission.Seed);
                    var tacticalPilot = new TacticalValidationPilot(air.Mission.Seed);
                    var encounterPilot = new EncounterValidationPilot(air.Mission.Seed); sorties++;
                    bool capturedFinalFlights=false; int peakAircraft=0;
                    for (int tick = 0; tick < 60 * 721 && air.Campaign.Phase == RoguePhase.Flying; tick++)
                    {
                        if (air.Mission.Kind == MissionKind.AceHunt)
                            air.StepForDiagnostics(encounterPilot.Update(air.Mission), 1f / 60f);
                        else if (air.Mission.Kind == MissionKind.Interception || air.Mission.Kind == MissionKind.BomberEscort || air.Mission.Kind == MissionKind.FleetBattle)
                            air.StepForDiagnostics(tacticalPilot.Update(air.Mission), 1f / 60f);
                        else if (air.Mission.Tactical) air.StepRogueBotsForDiagnostics(1f / 60f);
                        else air.StepForDiagnostics(pilot.Update(air.Mission), 1f / 60f);
                        if (tick % 30 != 0) continue;
                        finite &= FlightDynamics.Finite(air.Mission.Player.Position) && FlightDynamics.Finite(air.Mission.Player.Velocity);
                        peakAircraft=Math.Max(peakAircraft,air.Mission.Aircraft.Count);
                        yield return null;
                        if(!capturedFinalFlights&&air.Mission.Kind==MissionKind.FleetBattle&&air.Mission.EnemyFlightsEngaged==3)
                        {capturedFinalFlights=true;yield return Capture("44-afterburn-final-three-flights");}
                    }
                    Check(finite && air.Campaign.Phase != RoguePhase.Flying,
                        "Roguelite: rendered seeded sortie " + sorties + " reaches a finite real combat outcome without objective overrides");
                    Check(air.Mission.RoundsFired > 0 && air.Mission.Hits > 0,
                        "Roguelite: rendered sortie " + sorties + " fires actual ammunition and lands travelling projectiles");
                    if(air.Mission.Kind==MissionKind.FleetBattle)
                        Check(capturedFinalFlights&&peakAircraft==12&&air.Mission.Kills>=4,
                            "Roguelite: final larger battle renders twelve aircraft and earns four losses after all three flights enter the sector");
                    Note("ACTUAL ROGUELITE SORTIE: run 81 / stage " + sorties + " / mission " + air.Mission.Seed + " " + air.Mission.Kind
                        + "; " + air.Mission.Outcome + "; t=" + air.Mission.Time.ToString("F1")
                        + "; rounds=" + air.Mission.RoundsFired + "; hits=" + air.Mission.Hits + "; hull=" + air.Campaign.Hull.ToString("F1"));
                    FinishDiagnosticAftermath(air);
                }
                Check(air.Campaign.Phase == RoguePhase.Victory && sorties == 4,
                    "Roguelite: all four rendered sorties complete an earned campaign through real flight, guns, repairs and objective progress");
                yield return Capture("44-afterburn-earned-operation-result");

                // Explicit outcome fixtures exercise every later UI and reset independently of pilot skill.
                air.StartRogueRun(81);
                for (int stage = 0; stage < RogueRun.SortieCount; stage++)
                {
                    air.LaunchRogueRoute(stage % air.Campaign.Routes.Count);
                    RogueValidation.CompleteFixture(air.Mission); air.Campaign.FinishSortie();
                    FinishDiagnosticAftermath(air);
                    air.View.Sync(air.Mission, 0);
                    Note("UI FIXTURE ONLY: stage " + (stage + 1) + " objective state completed to inspect reward/final menus; not combat evidence.");
                    if (stage == RogueRun.SortieCount - 1) break;
                    Check(air.Campaign.Phase == RoguePhase.Reward && air.Campaign.Rewards.Count >= 3,
                        "Roguelite: completed sortie " + (stage + 1) + " exposes earned equipment and repair choices");
                    if (stage == 0) yield return Capture("45-afterburn-reward-choices-fixture");
                    air.ChooseRogueReward(0);
                    Check(air.Campaign.Phase == RoguePhase.Planning && air.Campaign.Stage == stage + 1,
                        "Roguelite: reward advances the operation exactly one sortie");
                    if (stage == 0) yield return Capture("46-afterburn-equipped-hangar-fixture");
                }
                Check(air.Campaign.Phase == RoguePhase.Victory, "Roguelite: fourth explicit objective fixture exposes the completed operation screen");
                yield return Capture("47-afterburn-victory-fixture");
                air.StartRogueRun(81); air.LaunchRogueRoute(0);
                air.Mission.Player.Alive = false; air.Mission.Player.Health = 0;
                air.StepForDiagnostics(new FlightInput(), 1f / 60f);
                FinishDiagnosticAftermath(air);
                Check(air.Campaign.Phase == RoguePhase.Defeat,
                    "Roguelite: player-loss fixture exposes the failed-run screen and blocks further sorties");
                yield return Capture("48-afterburn-defeat-fixture");
                air.StartRogueRun(81);
                Check(air.Campaign.Stage == 0 && air.Campaign.Mods.Count == 0 && air.Campaign.Hull == 100,
                    "Roguelite: retry clears previous aircraft modifications, destruction and campaign progress");
                air.ShowStandardMissions();
                Check(air.Campaign == null && air.Briefing,
                    "Roguelite: return to standard missions discards campaign state and restores ordinary briefing");
                air.StartSortie(81, MissionKind.Interception);
                Check(air.Campaign == null && ReferenceEquals(air.Mission.Player.Definition, AircraftDefinition.AlliedFighter)
                    && air.Mission.Player.DamageMultiplier == 1 && air.Mission.Player.FireRateMultiplier == 1
                    && air.Mission.Player.DamageTakenMultiplier == 1,
                    "Roguelite: the next standard sortie has no leftover aircraft, weapon or resistance modifiers");
                yield return ExerciseWheelInFlight(air, "standard mission after campaign");
                air.DiagnosticControl = true;
                yield return ExerciseReadableFlightLabels(air);
                air.ShowStandardMissions();
                CheckScene(GameCollection.AirScene);
            }
            finally { CleanupKeyboard(); CleanupMouse(); air.DiagnosticControl = true; }
        }

        IEnumerator ExerciseWheelInFlight(AirCombatGame air, string context)
        {
            air.DiagnosticControl = false; air.Controls.Reset(.5f);
            yield return null;
            yield return RogueWheel(1);
            Check(Mathf.Abs(air.Controls.Throttle - .54f) < .002f,
                "Mouse wheel in " + context + ": one upward event raises authoritative throttle four percent through normal Update");
            yield return RogueWheel(-.5f);
            Check(Mathf.Abs(air.Controls.Throttle - .52f) < .002f,
                "Mouse wheel in " + context + ": half downward event preserves gradual fractional adjustment");
            air.SetPaused(true); float throttle = air.Controls.Throttle, time = air.Mission.Time;
            yield return RogueWheel(3);
            Check(air.Controls.Throttle == throttle && air.Mission.Time == time,
                "Mouse wheel in " + context + ": paused settings consume scroll without changing flight state");
            air.SetPaused(false); yield return null; yield return null;
            Check(air.Controls.Throttle == throttle,
                "Mouse wheel in " + context + ": no stale menu scroll applies when flight resumes");
            air.DiagnosticControl = true;
        }

        IEnumerator RogueWheel(float notches)
        {
            bool uniform = InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
            bool windows = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
            float units = !uniform && windows ? 120 : 1;
            InputSystem.QueueStateEvent(testMouse, new MouseState { scroll = new Vector2(0, notches * units) });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(testMouse, new MouseState());
            yield return null; yield return null;
        }

        IEnumerator ExerciseReadableFlightLabels(AirCombatGame air)
        {
            int width = Screen.width, height = Screen.height;
            foreach (bool rogue in new[] { false, true })
            {
                if (rogue) { air.StartRogueRun(81); air.LaunchRogueRoute(1); }
                else air.StartSortie(81, MissionKind.BomberEscort);
                air.DiagnosticControl = true; air.View.SetCameraMode(0);
                AircraftState target = null;
                foreach (var aircraft in air.Mission.Aircraft) if (aircraft.Team != 0) { target = aircraft; break; }
                if (target == null) continue;
                foreach (bool sky in new[] { true, false })
                {
                    air.Mission.Player.Position = target.Position + new NVector3(0, sky ? -180 : 650, -750);
                    PointDiagnosticAircraft(air, target.Position); air.View.Sync(air.Mission, 0);
                    Screen.SetResolution(sky ? 1280 : 1600, sky ? 800 : 900, false);
                    yield return null; yield return null;
                    Note("LABEL VISUAL FIXTURE ONLY: " + (rogue ? "roguelite" : "standard")
                        + " camera positioned toward " + (sky ? "bright sky" : "dark terrain")
                        + "; resolution " + Screen.width + "x" + Screen.height + "; no target visibility rules changed.");
                    yield return Capture("49-labels-" + (rogue ? "roguelite-" : "standard-") + (sky ? "sky-1280" : "terrain-1600"));
                }
            }
            Screen.SetResolution(width, height, false); yield return null; yield return null;
        }
    }
}
