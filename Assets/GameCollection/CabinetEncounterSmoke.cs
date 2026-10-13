using System;
using System.Collections;
using Meridian;
using UnityEngine;
using UnityEngine.InputSystem;
using NVector3 = System.Numerics.Vector3;

namespace VectorCabinet
{
    public sealed partial class CabinetSmoke
    {
        IEnumerator ExerciseExpandedMissions(AirCombatGame air)
        {
            air.DiagnosticControl = true;
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current;
            testKeyboard = InputSystem.AddDevice<Keyboard>("Expanded encounters diagnostic keyboard");
            testMouse = InputSystem.AddDevice<Mouse>("Expanded encounters diagnostic mouse");
            testKeyboard.MakeCurrent(); testMouse.MakeCurrent();
            try
            {
                foreach (MissionKind kind in new[] { MissionKind.AceHunt, MissionKind.FleetBattle })
                {
                    air.ShowStandardMissions(); air.SeedText = "1944";
                    yield return SquadronKey(kind == MissionKind.AceHunt ? Key.Digit7 : Key.Digit8);
                    yield return Capture("62-" + kind + "-briefing");
                    Check(air.DescriptionOverflowCount == 0, kind + ": 17-pixel mission description fits its briefing panel");
                    yield return SquadronKey(Key.Enter);
                    Check(air.Mission.Kind == kind && air.Mission.Seed == 1944 && !air.Briefing && air.Campaign == null,
                        kind + ": physical mission shortcut and Enter launch a separate standard encounter");
                    Check(air.ShowLead && air.Mission.WingmenAlive == 2 && air.Mission.Time == 0,
                        kind + ": fresh encounter restores lead assistance and two unmodified wingmen");
                    air.View.SetCameraMode(0); air.View.Sync(air.Mission, 0);
                    yield return Capture("63-" + kind + "-initial-objectives");

                    // Entire sortie is earned with bounded controls or the real mission pilots.
                    // No aircraft state, health, ammunition, projectiles or objective overrides.
                    var pilot = new TacticalValidationPilot(1944);
                    int peakAlive = 0, peakAircraft = 0, peakProjectiles = 0;
                    float contact = -1, firstRound = -1;
                    bool finite = true, capturedContact = false, capturedReinforcements = false;
                    for (int tick = 0; tick < 601 * 60 && air.Mission.Outcome == MissionOutcome.Flying; tick++)
                    {
                        if (kind == MissionKind.AceHunt) air.Mission.StepBots(1f / 60f);
                        else air.Mission.Step(pilot.Update(air.Mission), 1f / 60f);
                        int alive = 0;
                        foreach (AircraftState aircraft in air.Mission.Aircraft)
                        {
                            finite &= FlightDynamics.Finite(aircraft.Position) && FlightDynamics.Finite(aircraft.Rotation)
                                && FlightDynamics.Finite(aircraft.Velocity) && aircraft.Ammo >= 0;
                            if (!aircraft.Alive) continue;
                            alive++;
                            if (aircraft.Team != 0 && contact < 0 && NVector3.DistanceSquared(aircraft.Position, air.Mission.Player.Position) < 2500 * 2500)
                                contact = air.Mission.Time;
                        }
                        peakAlive = Math.Max(peakAlive, alive); peakAircraft = Math.Max(peakAircraft, air.Mission.Aircraft.Count);
                        peakProjectiles = Math.Max(peakProjectiles, air.Mission.Projectiles.Count);
                        if (air.Mission.TotalRoundsFired > 0 && firstRound < 0) firstRound = air.Mission.Time;
                        if (tick % 30 != 0) continue;
                        air.View.Sync(air.Mission, .5f); yield return null;
                        if (!capturedContact && air.Mission.TotalRoundsFired > 10 && air.Mission.Outcome == MissionOutcome.Flying)
                        { capturedContact = true; yield return Capture("64-" + kind + "-live-combat"); }
                        if (kind == MissionKind.FleetBattle && !capturedReinforcements && air.Mission.EnemyFlightsEngaged > 1)
                        { capturedReinforcements = true; yield return Capture("65-FleetBattle-reinforcements-engaged"); }
                    }
                    Check(finite && peakAircraft <= 12 && peakProjectiles < 400,
                        kind + ": actual rendered engagement preserves finite flight and bounded aircraft/projectile counts");
                    Check(air.Mission.Outcome == MissionOutcome.Victory && air.Mission.Player.Alive,
                        kind + ": stock seed 1944 reaches an earned victory through actual flight and travelling rounds");
                    Check(contact >= 0 && firstRound >= 0 && air.Mission.TotalRoundsFired > 20 && capturedContact,
                        kind + ": real pilots establish contact, maneuver and exchange actual gunfire");
                    if (kind == MissionKind.AceHunt)
                    {
                        bool aceAlive = false;
                        foreach (AircraftState aircraft in air.Mission.Aircraft)
                            if (aircraft.Id == air.Mission.AceId) aceAlive = aircraft.Alive;
                        Check(!aceAlive, "Ace Hunt victory defeats the designated pilot rather than requiring an unrelated escort kill");
                    }
                    else
                        Check(air.Mission.Kills >= air.Mission.RequiredFighterLosses
                            && air.Mission.EnemyFlightsEngaged == air.Mission.TotalEnemyFlights && capturedReinforcements && peakAlive >= 8,
                            "Channel battle victory earns its loss quota after both enemy flights physically enter the contested sector");
                    Note("ACTUAL EXPANDED ENCOUNTER: " + kind + " seed=1944; outcome=" + air.Mission.Outcome
                        + "; contact=" + contact.ToString("F1") + "s; first-round=" + firstRound.ToString("F1")
                        + "s; duration=" + air.Mission.Time.ToString("F1") + "s; kills=" + air.Mission.Kills
                        + "; player-hits=" + air.Mission.Hits + "; wingmen=" + air.Mission.WingmenAlive
                        + "; peak-concurrent=" + peakAlive + "; total-aircraft=" + peakAircraft + "; peak-rounds=" + peakProjectiles
                        + "; flights=" + air.Mission.EnemyFlightsEngaged + "/" + air.Mission.TotalEnemyFlights
                        + "; cause=" + air.Mission.Summary);
                    FinishDiagnosticAftermath(air); air.View.Sync(air.Mission, 0);
                    yield return Capture("66-" + kind + "-earned-victory");

                    // A deliberately reckless pilot supplies legal controls; terrain/flight simulation
                    // determines defeat. This is not an injected death or claimed combat performance.
                    air.StartSortie(1944, kind);
                    for (int tick = 0; tick < 180 * 60 && air.Mission.Outcome == MissionOutcome.Flying; tick++)
                    {
                        air.Mission.Step(new FlightInput { Throttle = 0, Pitch = -1, Roll = .15f }, 1f / 60f);
                        if (tick % 60 == 0) { air.View.Sync(air.Mission, 1); yield return null; }
                    }
                    Check(air.Mission.Outcome == MissionOutcome.Defeat && !air.Mission.Player.Alive,
                        kind + ": deliberately unsafe bounded flying produces an actual failure without state overrides");
                    Note("ACTUAL UNSAFE-CONTROL FAILURE: " + kind + "; t=" + air.Mission.Time.ToString("F1")
                        + "; outcome=" + air.Mission.Outcome + "; cause=" + air.Mission.Summary);
                    FinishDiagnosticAftermath(air); air.View.Sync(air.Mission, 0);
                    yield return Capture("67-" + kind + "-actual-defeat");
                    air.StartSortie(1944, kind);
                    Check(air.Mission.Time == 0 && air.Mission.Outcome == MissionOutcome.Flying
                        && air.Mission.WingmenAlive == 2 && !air.AftermathActive && air.View.VisibleWreckCount == 0,
                        kind + ": replay clears outcome presentation, wrecks, damage and elapsed mission time");
                }
                air.ShowStandardMissions(); CheckScene(GameCollection.AirScene);
            }
            finally { CleanupKeyboard(); CleanupMouse(); air.DiagnosticControl = true; }
        }
    }
}
