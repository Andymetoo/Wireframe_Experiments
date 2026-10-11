using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HollowHouse;
using Meridian;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using NVector3 = System.Numerics.Vector3;

namespace VectorCabinet
{
    /// <summary>
    /// Opt-in rendered integration checks. Nothing is created during ordinary play.
    /// Uses public action APIs and temporary keyboard events; ergonomics still require human playtesting.
    /// </summary>
    public sealed class CabinetSmoke : MonoBehaviour
    {
        string output;
        int assertions, failures, runtimeErrors;
        float started;
        bool finished;
        Keyboard testKeyboard, previousKeyboard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            string destination = null;
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("VectorCabinet.Smoke", false))
            {
                UnityEditor.SessionState.SetBool("VectorCabinet.Smoke", false);
                destination = Path.GetFullPath("Logs/AirCombat");
            }
#endif
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-cabinetSmoke") destination = Path.GetFullPath(args[i + 1]);
            if (destination == null || FindAnyObjectByType<CabinetSmoke>() != null) return;
            var runner = new GameObject("Opt-in cabinet integration checks").AddComponent<CabinetSmoke>();
            DontDestroyOnLoad(runner.gameObject);
            runner.output = destination;
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "runtime-smoke.txt"),
                "RUNNING: rendered scene integration checks\n" +
                "Checks include temporary keyboard-device events, public gameplay APIs and diagnostic fixed steps. Human ergonomics remain untested.\n");
            File.WriteAllText(Path.Combine(destination, "runtime-errors.txt"), "");
            runner.started = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            Application.logMessageReceived += runner.Record;
            runner.StartCoroutine(runner.GuardedRun());
        }

        void Update()
        {
            if (!finished && output != null && Time.realtimeSinceStartup - started > 240)
            {
                Check(false, "Integration runner exceeded four minutes of wall time");
                Finish();
            }
        }

        IEnumerator GuardedRun()
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                IEnumerator current = stack.Peek();
                bool next = false;
                object yielded = null;
                Exception failure = null;
                try { next = current.MoveNext(); if (next) yielded = current.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    Check(false, "Runner exception: " + failure);
                    Finish();
                    yield break;
                }
                if (!next) { stack.Pop(); continue; }
                IEnumerator nested = yielded as IEnumerator;
                if (nested != null) stack.Push(nested);
                else yield return yielded;
            }
            if (!finished) Finish();
        }

        void Record(string message, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            runtimeErrors++;
            File.AppendAllText(Path.Combine(output, "runtime-errors.txt"), message + "\n" + trace + "\n");
        }
        void Check(bool passed, string description)
        {
            assertions++;
            if (!passed) failures++;
            File.AppendAllText(Path.Combine(output, "runtime-smoke.txt"), (passed ? "PASS " : "FAIL ") + description + "\n");
        }
        void Note(string value) { File.AppendAllText(Path.Combine(output, "runtime-smoke.txt"), value + "\n"); }

        void CheckScene(string expected)
        {
            Check(SceneManager.GetActiveScene().name == expected, "Active scene is " + expected);
            Check(Count<CabinetLauncher>() == (expected == GameCollection.LauncherScene ? 1 : 0), expected + ": launcher lifetime isolated");
            Check(Count<HauntedHouseGame>() == (expected == GameCollection.HouseScene ? 1 : 0), expected + ": house input/simulation root isolated");
            Check(Count<AirCombatGame>() == (expected == GameCollection.AirScene ? 1 : 0), expected + ": air input/simulation root isolated");
            Check(Count<HouseRenderer>() == (expected == GameCollection.HouseScene ? 1 : 0), expected + ": house presentation lifetime isolated");
            Check(Count<AirRenderer>() == (expected == GameCollection.AirScene ? 1 : 0), expected + ": air presentation lifetime isolated");
            Check(Count<Camera>() == 1, expected + ": exactly one enabled camera");
            Check(Count<AudioListener>() <= 1, expected + ": no duplicate audio listeners");
            Check(Mathf.Approximately(Time.timeScale, 1), expected + ": simulation timescale restored");
        }
        static int Count<T>() where T : Behaviour
        {
            int result = 0;
            foreach (T instance in FindObjectsByType<T>(FindObjectsInactive.Exclude))
                if (instance.isActiveAndEnabled) result++;
            return result;
        }
        IEnumerator Open(string scene)
        {
            GameCollection.Open(scene);
            yield return null;
            yield return null;
            CheckScene(scene);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.20f);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(output, name + ".png");
            DateTime requestedAt = DateTime.UtcNow;
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.20f);
            Check(File.Exists(path) && new FileInfo(path).Length > 2000 && File.GetLastWriteTimeUtc(path) >= requestedAt.AddSeconds(-1),
                "Rendered screenshot saved: " + name);
        }

        IEnumerator Run()
        {
            yield return null;
            if (SceneManager.GetActiveScene().name != GameCollection.LauncherScene)
                yield return Open(GameCollection.LauncherScene);
            else CheckScene(GameCollection.LauncherScene);
            yield return Capture("01-cabinet");

            yield return Open(GameCollection.HouseScene);
            HauntedHouseGame house = FindAnyObjectByType<HauntedHouseGame>();
            if (house == null) throw new InvalidOperationException("House composition root missing");
            Check(house.AtTitle, "Haunted House opens at its original title");
            yield return Capture("02-house-title");
            house.NewCase(128);
            Check(!house.AtTitle && house.Session.Ending == Ending.Investigating, "Haunted House starts a reproducible active investigation");
            house.Act(() => house.Session.Inspect());
            Check(house.Session.Journal.Count > 0 && house.Session.Turn > 0, "Examining the vestibule records evidence and advances the investigation");
            yield return WalkHouse(house, 1);
            house.Act(() => house.Session.Inspect());
            Check(house.Session.CurrentRoom == 1 && house.Session.SearchedRooms.Contains(1), "Grid movement, doors, room inspection and evidence remain functional");
            yield return Capture("03-house-room");
            house.OpenPanel("journal");
            yield return Capture("04-house-journal");
            house.OpenPanel("pause");
            int pausedTurn = house.Session.Turn;
            yield return new WaitForSecondsRealtime(.3f);
            Check(house.Panel == "pause" && house.Session.Turn == pausedTurn, "Haunted House pause preserves action-driven state");
            house.NewCase(128);
            Check(house.Session.Turn == 0 && house.Session.Health == 3 && house.Session.Journal.Count == 0,
                "Haunted House restart resets the case");
            yield return Open(GameCollection.LauncherScene);
            yield return Capture("05-cabinet-after-house");

            yield return Open(GameCollection.AirScene);
            AirCombatGame air = FindAnyObjectByType<AirCombatGame>();
            if (air == null) throw new InvalidOperationException("Air composition root missing");
            air.DiagnosticControl = true;
            Check(air.Briefing && air.Mission != null, "Air Combat opens at the operations briefing");
            yield return Capture("06-air-briefing");
            yield return ExerciseInputAdapter(air);
            air.StartSortie(1944, MissionKind.Duel);
            air.View.SetCameraMode(0);
            yield return null;
            yield return null;
            Check(!air.Briefing && air.Mission.EnemyRemaining == 1 && air.Mission.Player.Alive, "Training duel starts with one opponent and a live player");
            CheckScene(GameCollection.AirScene);
            yield return Capture("07-air-cockpit");
            NVector3 start = air.Mission.Player.Position;
            for (int frame = 0; frame < 60 * 8; frame++)
            {
                float t = frame / 60f;
                air.StepForDiagnostics(new FlightInput { Throttle = .8f, Roll = t < .65f ? .65f : 0, Pitch = .15f }, 1f / 60f);
                if (frame % 12 == 0) yield return null;
            }
            Check(air.Mission.Player.Alive && NVector3.Distance(start, air.Mission.Player.Position) > 700,
                "Eight seconds of manual flight controls advance a live aircraft through the world");
            Check(air.Mission.Player.Right.Y < -.20f, "Right roll input banks the rendered/simulated airframe");
            Note("Manual flight sample: speed " + air.Mission.Player.Speed.ToString("0.0") + " m/s; altitude " + air.Mission.Player.Position.Y.ToString("0.0") + " m");
            yield return Capture("08-air-bank");
            air.View.SetCameraMode(1);
            yield return Capture("09-air-chase");
            Check(air.View.CameraMode == 1, "Chase camera switch preserves the same mission");
            air.SetPaused(true);
            air.DiagnosticControl = false;
            float pauseTime = air.Mission.Time;
            NVector3 pausePosition = air.Mission.Player.Position;
            yield return Capture("10-air-pause");
            Check(air.Paused && air.Mission.Time == pauseTime && air.Mission.Player.Position == pausePosition,
                "Air pause prevents normal Update from advancing the simulation");
            air.DiagnosticControl = true;
            air.StartSortie(1944, MissionKind.Duel);
            yield return null;
            yield return null;
            Check(air.Mission.Time == 0 && air.Mission.Player.Health == 100 && !air.Paused,
                "Air restart replaces the sortie and resets damage, time and pause state");
            CheckScene(GameCollection.AirScene);
            // This independent pilot writes only ordinary bounded control inputs. No transforms,
            // hit points, ammunition, opposing pilots or initial mission conditions are altered.
            var diagnosticPilot = new ValidationPilot(1944);
            air.View.SetCameraMode(0);
            bool capturedTracers = false, capturedImpact = false;
            for (int frame = 0; frame < 60 * 300 && air.Mission.Outcome == MissionOutcome.Flying; frame++)
            {
                FlightInput control = diagnosticPilot.Update(air.Mission);
                air.StepForDiagnostics(control, 1f / 60f);
                if (frame == 60 * 12) yield return Capture("11-air-contact");
                if (!capturedTracers && air.Mission.RoundsFired >= 3 && air.Mission.Projectiles.Count >= 2)
                {
                    capturedTracers = true;
                    // DiagnosticControl holds simulation time still while the render is captured.
                    yield return Capture("11a-air-live-tracers");
                    Note("Tracer capture: time " + air.Mission.Time.ToString("0.00") + "; player rounds " + air.Mission.RoundsFired);
                }
                if (!capturedImpact && air.Mission.Hits > 0 && air.Mission.Outcome == MissionOutcome.Flying)
                {
                    capturedImpact = true;
                    yield return Capture("11b-air-live-impact");
                    Note("Impact capture: time " + air.Mission.Time.ToString("0.00") + "; player hits " + air.Mission.Hits);
                }
                if (frame % 18 == 0) yield return null;
            }
            Check(FlightDynamics.Finite(air.Mission.Player.Position) && FlightDynamics.Finite(air.Mission.Player.Rotation),
                "A complete rendered diagnostic dogfight remains finite");
            Check(air.Mission.Outcome == MissionOutcome.Victory && air.Mission.Hits >= 8 && air.Mission.Player.Alive,
                "Unmodified default seed 1944 is won through actual ballistic combat using bounded player controls");
            Check(capturedTracers && capturedImpact, "Full-strength dogfight records travelling rounds and nonterminal impact feedback");
            Note("Full-strength default dogfight: elapsed " + air.Mission.Time.ToString("0.00") + "; outcome " + air.Mission.Outcome
                + "; player rounds " + air.Mission.RoundsFired + "; all rounds " + air.Mission.TotalRoundsFired
                + "; player hits " + air.Mission.Hits + "; player health " + air.Mission.Player.Health.ToString("0.0"));
            yield return Capture("12-air-dogfight");

            // A controlled short-range fixture tests real projectile hits and the victory UI.
            // Low target health is deliberate; full-strength combat balance belongs to the core soak and human tests.
            air.StartSortie(1944, MissionKind.Duel);
            AircraftState enemy = air.Mission.Aircraft[1];
            enemy.Position = air.Mission.Player.Position + air.Mission.Player.Forward * 220;
            enemy.Rotation = air.Mission.Player.Rotation;
            enemy.Velocity = air.Mission.Player.Velocity;
            enemy.Health = 8;
            air.View.SetCameraMode(0);
            for (int frame = 0; frame < 240 && air.Mission.Outcome == MissionOutcome.Flying; frame++)
            {
                air.StepForDiagnostics(new FlightInput { Throttle = .78f, Fire = true }, 1f / 60f);
                if (frame % 6 == 0) yield return null;
            }
            Check(air.Mission.Hits > 0 && air.Mission.RoundsFired > 0 && air.Mission.Outcome == MissionOutcome.Victory,
                "Controlled gunnery fixture: travelling projectiles destroy a damaged target and trigger victory");
            yield return Capture("13-air-victory-fixture");
            air.StartSortie(991, MissionKind.Duel);
            air.Mission.Player.Position = new NVector3(0, air.Mission.Terrain.Height(0, 0) + 1, 0);
            air.StepForDiagnostics(new FlightInput { Throttle = .7f }, 1f / 60f);
            Check(air.Mission.Outcome == MissionOutcome.Defeat && !air.Mission.Player.Alive,
                "Terrain collision fixture triggers defeat");
            yield return Capture("14-air-defeat-fixture");
            air.StartSortie(71319, MissionKind.ConvoyCover);
            yield return null;
            yield return null;
            Check(air.Mission.Courier != null && air.Mission.Courier.Alive && air.Mission.EnemyRemaining == 2 && air.Mission.Time == 0,
                "Restart after defeat can begin courier cover with two interceptors");
            air.ShowBriefing();
            Check(air.Briefing && !air.Paused, "Operations briefing can be reopened from a sortie");
            yield return Capture("15-courier-briefing");
            yield return Open(GameCollection.LauncherScene);
            yield return Capture("16-cabinet-after-flight");
            yield return Open(GameCollection.HouseScene);
            house = FindAnyObjectByType<HauntedHouseGame>();
            Check(house != null && house.AtTitle && house.Session.Turn == 0,
                "Haunted House relaunches fresh after Air Combat");
            house.NewCase(128);
            house.Act(() => house.Session.Inspect());
            Check(house.Session.Journal.Count > 0, "Investigation still accepts evidence actions after switching from flight");
            yield return Capture("17-house-after-flight");
            yield return Open(GameCollection.LauncherScene);
            yield return Capture("18-cabinet-final");
        }

        IEnumerator WalkHouse(HauntedHouseGame house, int room)
        {
            GameSession session = house.Session;
            var path = session.Scenario.FindPath(session.Position, session.Scenario.Rooms[room].Center);
            for (int i = 1; i < path.Count; i++)
            {
                HouseCounter(house);
                GridPos delta = path[i] - session.Position;
                int direction = delta.X > 0 ? 1 : delta.X < 0 ? 3 : delta.Y > 0 ? 0 : 2;
                session.TurnFacing(direction - (int)session.Facing);
                Cell cell = session.Scenario.Cells[path[i]];
                if (cell.DoorId >= 0 && !session.DoorsOpen[cell.DoorId])
                {
                    session.InteractDoor(); HouseCounter(house);
                    session.TurnFacing(direction - (int)session.Facing);
                }
                session.Move(0);
                house.House.SetView(session.Position, session.Facing);
                yield return null;
            }
            HouseCounter(house);
        }

        IEnumerator ExerciseInputAdapter(AirCombatGame air)
        {
            // A temporary device exercises the same ReadControls / Update path as physical keys.
            // It is always removed; it never replaces or alters a connected hardware keyboard.
            previousKeyboard = Keyboard.current;
            testKeyboard = InputSystem.AddDevice<Keyboard>("Cabinet smoke keyboard");
            testKeyboard.MakeCurrent();
            try
            {
                air.StartSortie(1944, MissionKind.Duel);
                air.View.SetCameraMode(0);
                air.DiagnosticControl = false;
                int initialAmmo = air.Mission.Player.Ammo;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.D, Key.Space));
                yield return new WaitForSecondsRealtime(.45f);
                Check(air.Mission.Time > .15f && air.Mission.Player.Right.Y < -.05f,
                    "Temporary D key events pass through Update and ReadControls to bank the aircraft");
                Check(air.Mission.Player.Ammo < initialAmmo && air.Mission.RoundsFired > 0,
                    "Temporary Space key events fire physical rounds through the normal input adapter");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.C));
                yield return new WaitForSecondsRealtime(.12f);
                Check(air.View.CameraMode == 1, "Temporary C key press switches the player camera");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
                yield return new WaitForSecondsRealtime(.12f);
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                float pausedTime = air.Mission.Time;
                yield return new WaitForSecondsRealtime(.25f);
                Check(air.Paused && air.Mission.Time == pausedTime, "Temporary Escape key press pauses the normal simulation clock");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
                yield return new WaitForSecondsRealtime(.12f);
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                Check(!air.Paused && air.Mission.Time > pausedTime, "A second Escape press resumes the sortie");
            }
            finally
            {
                air.DiagnosticControl = true;
                CleanupKeyboard();
            }
        }
        void CleanupKeyboard()
        {
            if (testKeyboard != null)
            {
                if (testKeyboard.added) InputSystem.RemoveDevice(testKeyboard);
                testKeyboard = null;
            }
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            previousKeyboard = null;
        }
        static void HouseCounter(HauntedHouseGame house)
        {
            GameSession session = house.Session;
            if (session.ThreatCountdown <= 0) return;
            if (session.Scenario.Archetype == Archetype.Mourner && session.Lantern) session.ToggleLantern();
            if (session.Scenario.Archetype == Archetype.Watcher) session.TurnFacing((int)session.ThreatDirection - (int)session.Facing);
            session.Wait();
            house.House.SetView(session.Position, session.Facing);
        }
        void Finish()
        {
            if (finished) return;
            finished = true;
            CleanupKeyboard();
            Application.logMessageReceived -= Record;
            Note("Runtime errors: " + runtimeErrors);
            Note("Checks: " + assertions + "; failures: " + failures);
            Note(failures == 0 && runtimeErrors == 0 ? "COMPLETE: PASS" : "COMPLETE: FAIL");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(failures == 0 && runtimeErrors == 0 ? 0 : 1);
#endif
        }
        void OnDestroy() { CleanupKeyboard(); Application.logMessageReceived -= Record; }
    }
}
