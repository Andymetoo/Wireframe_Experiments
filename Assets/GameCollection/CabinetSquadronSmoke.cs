using System;
using System.Collections;
using Meridian;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NVector3 = System.Numerics.Vector3;
using NQuaternion = System.Numerics.Quaternion;

namespace VectorCabinet
{
    public sealed partial class CabinetSmoke
    {
        IEnumerator ExerciseSquadron(AirCombatGame air)
        {
            air.DiagnosticControl = true;
            air.StartSortie(81, MissionKind.Interception);
            yield return AirInputValidation.Run(Check);
            Check(air.Mission.Wingmen.Count == 2 && air.Mission.CurrentOrder == SquadronOrder.FormUp,
                "Tactical sortie starts with two mortal wingmen and a fresh Form Up order");
            yield return ExerciseSquadronKeys(air);

            // Geometry-only camera fixtures. These NEVER count as evidence of mission winnability.
            air.StartSortie(81, MissionKind.BomberEscort);
            var bomber = air.Mission.Bombers[0];
            air.Mission.Player.Position = bomber.Position - bomber.Forward * 57 + bomber.Right * 23 + NVector3.UnitY * 9;
            PointDiagnosticAircraft(air, bomber.Position);
            air.View.SetCameraMode(0); air.View.Sync(air.Mission, 0);
            Note("VISUAL FIXTURE ONLY: player positioned near a bomber to inspect procedural geometry; simulation frozen.");
            yield return Capture("28-squadron-bomber-geometry-fixture");

            air.StartSortie(81, MissionKind.GroundAttack);
            var ground = air.Mission.GroundTargets[0];
            air.Mission.Player.Position = ground.Position + new NVector3(45, 34, -58);
            PointDiagnosticAircraft(air, ground.Position + NVector3.UnitY * 3);
            air.View.SetCameraMode(0); air.View.Sync(air.Mission, 0);
            Note("VISUAL FIXTURE ONLY: player/camera near convoy geometry; no physics or damage overrides used by the later mission tests.");
            yield return Capture("29-squadron-ground-geometry-fixture");

            MissionKind[] kinds = { MissionKind.Interception, MissionKind.BomberEscort, MissionKind.GroundAttack };
            foreach (MissionKind kind in kinds)
            {
                int seed = kind == MissionKind.Interception ? 1944 : 81;
                air.StartSortie(seed, kind);
                air.View.SetCameraMode(1);
                Check(air.Mission.Time == 0 && air.Mission.Outcome == MissionOutcome.Flying
                    && air.Mission.Wingmen.Count == 2 && air.Mission.CurrentOrder == SquadronOrder.FormUp,
                    kind + ": fresh seeded sortie clears previous damage, outcome, clock and squadron order");
                yield return Capture("30-" + kind + "-start");
                int maximumTicks = 60 * 720;
                bool capturedCombat = false, finite = true;
                for (int tick = 0; tick < maximumTicks && air.Mission.Outcome == MissionOutcome.Flying; tick++)
                {
                    // The actual mission AI writes ordinary inputs into the existing flight and weapons systems.
                    // No positions, health, ammunition, targets, projectiles or outcomes are changed here.
                    air.Mission.StepBots(1f / 60f);
                    if (tick % 30 == 0)
                    {
                        foreach (var aircraft in air.Mission.Aircraft)
                            finite &= FlightDynamics.Finite(aircraft.Position) && FlightDynamics.Finite(aircraft.Rotation)
                                && FlightDynamics.Finite(aircraft.Velocity);
                        air.View.Sync(air.Mission, .5f);
                        yield return null;
                    }
                    if (!capturedCombat && air.Mission.TotalRoundsFired > 12 && air.Mission.Time > 8
                        && air.Mission.Outcome == MissionOutcome.Flying)
                    {
                        capturedCombat = true;
                        air.View.SetCameraMode(0); air.View.Sync(air.Mission, 0);
                        yield return Capture("31-" + kind + "-live-combat");
                    }
                }
                air.View.Sync(air.Mission, 0);
                Check(finite, kind + ": every actual aircraft state stays finite throughout the rendered mission");
                Check(air.Mission.Outcome != MissionOutcome.Flying, kind + ": bounded actual mission reaches a terminal outcome");
                Check(air.Mission.Outcome == MissionOutcome.Victory && air.Mission.Player.Alive,
                    kind + ": unmodified seed " + seed + " is won by bounded pilot controls through actual flight, projectiles and mission objectives");
                Check(air.Mission.TotalRoundsFired > 0 && capturedCombat,
                    kind + ": participating pilots fire travelling rounds and live combat is captured");
                if (kind == MissionKind.Interception)
                    Check(air.Mission.ObjectiveProgress >= 1 && air.Mission.EnemyEscaped == 0,
                        "Interception victory stops both raid aircraft before the defended boundary");
                else if (kind == MissionKind.BomberEscort)
                    Check(air.Mission.BombersArrived >= air.Mission.RequiredBombers,
                        "Bomber escort victory requires actual route arrivals by the protected aircraft");
                else
                    Check(air.Mission.GroundObjectivesDestroyed == air.Mission.RequiredGroundTargets && air.Mission.Hits > 0,
                        "Ground attack victory destroys every required truck through actual ballistic impacts");
                Note("Rendered actual mission: " + kind + " seed " + seed + "; elapsed " + air.Mission.Time.ToString("0.00")
                    + "; outcome " + air.Mission.Outcome + "; all rounds " + air.Mission.TotalRoundsFired
                    + "; player HP " + air.Mission.Player.Health.ToString("0.0") + "; " + air.Mission.DebriefDetails.Replace('\n', ';'));
                yield return Capture("32-" + kind + "-debrief");
            }
            air.StartSortie(81, MissionKind.Interception);
            Check(air.Mission.WingmenAlive == 2 && air.Mission.CurrentOrder == SquadronOrder.FormUp && air.Mission.Time == 0,
                "Restarting after three tactical missions restores both wingmen and clears old mission commands");
            air.ShowBriefing();
            Check(air.Briefing && !air.Paused, "Tactical missions return cleanly to the shared flight briefing");
            CheckScene(GameCollection.AirScene);
        }

        IEnumerator ExerciseSquadronKeys(AirCombatGame air)
        {
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current;
            testKeyboard = InputSystem.AddDevice<Keyboard>("Squadron diagnostic keyboard");
            testMouse = InputSystem.AddDevice<Mouse>("Squadron diagnostic mouse");
            testKeyboard.MakeCurrent(); testMouse.MakeCurrent();
            try
            {
                var expected = new[] { SquadronOrder.FormUp, SquadronOrder.Engage, SquadronOrder.CoverMe, SquadronOrder.Regroup };
                var keys = new[] { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
                for (int i = 0; i < keys.Length; i++)
                {
                    yield return SquadronKey(keys[i]);
                    Check(air.Mission.CurrentOrder == expected[i], "Physical keyboard " + (i + 1) + " issues " + expected[i] + " through normal Update");
                }
                int target = air.SelectedTargetId;
                yield return SquadronKey(Key.Tab);
                Check(air.SelectedTargetId != target && air.SelectedTargetId >= 0, "Tab selects another real hostile target without changing the Regroup order");
                Check(air.Mission.CurrentOrder == SquadronOrder.Regroup, "Target browsing preserves the last explicit wingman order");
                air.View.SetCameraMode(0);
                yield return null;
                var rotation = air.View.ViewCamera.transform.rotation;
                InputSystem.QueueStateEvent(testMouse, new MouseState().WithButton(MouseButton.Right));
                InputSystem.QueueDeltaStateEvent(testMouse.delta, new Vector2(200, -40));
                yield return null; yield return null;
                Check(Quaternion.Angle(rotation, air.View.ViewCamera.transform.rotation) > 8,
                    "Held right mouse and mouse delta look around without rotating the aircraft");
                InputSystem.QueueStateEvent(testMouse, new MouseState());
                yield return null; yield return null;
                yield return SquadronKey(Key.V);
                Check(Quaternion.Angle(rotation, air.View.ViewCamera.transform.rotation) < 1,
                    "V recenters the view after independent freelook");
                yield return SquadronKey(Key.Escape);
                Check(air.Paused && Cursor.lockState == CursorLockMode.None,
                    "Tactical pause releases cursor capture and exposes squadron/controller instructions");
                yield return Capture("27-squadron-controller-settings");
                yield return SquadronKey(Key.Enter);
                Check(!air.Paused, "Menu submit resumes the focused tactical sortie action");
                yield return SquadronKey(Key.Escape);
                yield return SquadronKey(Key.DownArrow);
                yield return SquadronKey(Key.Enter);
                Check(!air.Paused && air.Mission.Time == 0 && air.Mission.CurrentOrder == SquadronOrder.FormUp,
                    "Pause menu keyboard navigation and submit restart the mission with cleared orders");
            }
            finally { CleanupKeyboard(); CleanupMouse(); air.DiagnosticControl = true; }
        }

        IEnumerator SquadronKey(Key key)
        {
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null; yield return null;
        }

        static void PointDiagnosticAircraft(AirCombatGame air, NVector3 target)
        {
            Vector3 direction = AirRenderer.World(target - air.Mission.Player.Position);
            Quaternion view = Quaternion.LookRotation(direction, Vector3.up);
            air.Mission.Player.Rotation = new NQuaternion(view.x, view.y, view.z, view.w);
        }
    }
}
