using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ColdSnap;
using HollowHouse;
using Meridian;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using NVector3 = System.Numerics.Vector3;
using NVector2 = System.Numerics.Vector2;

namespace VectorCabinet
{
    /// <summary>
    /// Opt-in rendered integration checks. Nothing is created during ordinary play.
    /// Uses public action APIs and temporary keyboard events; ergonomics still require human playtesting.
    /// </summary>
    public sealed partial class CabinetSmoke : MonoBehaviour
    {
        string output;
        int assertions, failures, runtimeErrors;
        float started;
        bool finished;
        Keyboard testKeyboard, previousKeyboard;
        Mouse testMouse, previousMouse;
        Keyboard idleKeyboard, hardwareKeyboard;
        Mouse idleMouse, hardwareMouse;
        InputSettings originalInputSettings, smokeInputSettings;
        bool originalRunInBackground;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            string destination = null;
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("VectorCabinet.Smoke", false))
            {
                UnityEditor.SessionState.SetBool("VectorCabinet.Smoke", false);
                destination = Path.GetFullPath("Logs/Hockey");
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
            runner.InstallDiagnosticInput();
            Application.logMessageReceived += runner.Record;
            runner.StartCoroutine(runner.GuardedRun());
        }

        void Update()
        {
            if (!finished && output != null && Time.realtimeSinceStartup - started > 480)
            {
                Check(false, "Integration runner exceeded eight minutes of wall time");
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

        void InstallDiagnosticInput()
        {
            // Keep this opt-in run independent of the user's foreground application.
            // The project asset is never edited: only an unsaved clone is installed.
            originalRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            originalInputSettings = InputSystem.settings;
            smokeInputSettings = Instantiate(originalInputSettings);
            smokeInputSettings.name = "Temporary cabinet diagnostic input settings";
            smokeInputSettings.hideFlags = HideFlags.HideAndDontSave;
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = smokeInputSettings;
            hardwareKeyboard = Keyboard.current; hardwareMouse = Mouse.current;
            idleKeyboard = InputSystem.AddDevice<Keyboard>("Cabinet diagnostic idle keyboard");
            idleMouse = InputSystem.AddDevice<Mouse>("Cabinet diagnostic idle mouse");
            idleKeyboard.MakeCurrent(); idleMouse.MakeCurrent();
            InputSystem.onAfterUpdate += PinDiagnosticDevices;
            Note("Diagnostic input: unsaved settings clone, background events enabled, application focused=" + Application.isFocused);
        }

        void PinDiagnosticDevices()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            // Real devices remain connected and untouched. Only these private devices
            // drive tests, even if the user types in another foreground application.
            Keyboard keyboard = testKeyboard != null ? testKeyboard : idleKeyboard;
            Mouse mouse = testMouse != null ? testMouse : idleMouse;
            if (keyboard != null && keyboard.added) keyboard.MakeCurrent();
            if (mouse != null && mouse.added) mouse.MakeCurrent();
        }

        void RestoreDiagnosticInput()
        {
            InputSystem.onAfterUpdate -= PinDiagnosticDevices;
            if (idleKeyboard != null && idleKeyboard.added) InputSystem.RemoveDevice(idleKeyboard);
            if (idleMouse != null && idleMouse.added) InputSystem.RemoveDevice(idleMouse);
            idleKeyboard = null; idleMouse = null;
            if (hardwareKeyboard != null && hardwareKeyboard.added) hardwareKeyboard.MakeCurrent();
            if (hardwareMouse != null && hardwareMouse.added) hardwareMouse.MakeCurrent();
            hardwareKeyboard = null; hardwareMouse = null;
            if (originalInputSettings != null)
            {
                InputSystem.settings = originalInputSettings;
                Application.runInBackground = originalRunInBackground;
                originalInputSettings = null;
            }
            if (smokeInputSettings != null) Destroy(smokeInputSettings);
            smokeInputSettings = null;
        }

        void CheckScene(string expected)
        {
            Check(SceneManager.GetActiveScene().name == expected, "Active scene is " + expected);
            Check(Count<CabinetLauncher>() == (expected == GameCollection.LauncherScene ? 1 : 0), expected + ": launcher lifetime isolated");
            Check(Count<HauntedHouseGame>() == (expected == GameCollection.HouseScene ? 1 : 0), expected + ": house input/simulation root isolated");
            Check(Count<AirCombatGame>() == (expected == GameCollection.AirScene ? 1 : 0), expected + ": air input/simulation root isolated");
            Check(Count<HockeyGame>() == (expected == GameCollection.HockeyScene ? 1 : 0), expected + ": hockey input/simulation root isolated");
            Check(Count<HouseRenderer>() == (expected == GameCollection.HouseScene ? 1 : 0), expected + ": house presentation lifetime isolated");
            Check(Count<AirRenderer>() == (expected == GameCollection.AirScene ? 1 : 0), expected + ": air presentation lifetime isolated");
            Check(Count<HockeyRenderer>() == (expected == GameCollection.HockeyScene ? 1 : 0), expected + ": hockey presentation lifetime isolated");
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
        IEnumerator SelectFromLauncher(Key key, string scene)
        {
            Check(SceneManager.GetActiveScene().name == GameCollection.LauncherScene, "Game selection begins in the shared launcher");
            previousKeyboard = Keyboard.current;
            testKeyboard = InputSystem.AddDevice<Keyboard>("Cabinet selection keyboard");
            testKeyboard.MakeCurrent();
            try
            {
                Note("Launcher input before " + key + ": focused=" + Application.isFocused + "; current keyboard="
                    + (Keyboard.current != null ? Keyboard.current.deviceId.ToString() : "none")
                    + "; synthetic keyboard=" + testKeyboard.deviceId + "; enabled=" + testKeyboard.enabled);
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(key));
                bool observedPress = false;
                // Wait for actual player frames, rather than a wall-clock delay which
                // can expire while the editor routes or defers input in the background.
                for (int frame = 0; frame < 12 && SceneManager.GetActiveScene().name == GameCollection.LauncherScene; frame++)
                {
                    yield return null;
                    observedPress |= testKeyboard[key].wasPressedThisFrame;
                }
                Note("Launcher input after " + key + ": focused=" + Application.isFocused + "; current keyboard="
                    + (Keyboard.current != null ? Keyboard.current.deviceId.ToString() : "none")
                    + "; down=" + testKeyboard[key].isPressed + "; observed edge=" + observedPress
                    + "; scene=" + SceneManager.GetActiveScene().name);
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                yield return null;
                Check(SceneManager.GetActiveScene().name == scene, "Launcher " + key + " input selects " + scene);
                CheckScene(scene);
            }
            finally { CleanupKeyboard(); }
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

            yield return SelectFromLauncher(Key.Digit1, GameCollection.HouseScene);
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

            yield return SelectFromLauncher(Key.Digit2, GameCollection.AirScene);
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
            yield return ExerciseSquadron(air);
            yield return Open(GameCollection.LauncherScene);
            yield return Capture("16-cabinet-after-flight");
            yield return ExerciseHockey();
            yield return Open(GameCollection.HouseScene);
            house = FindAnyObjectByType<HauntedHouseGame>();
            Check(house != null && house.AtTitle && house.Session.Turn == 0,
                "Haunted House relaunches fresh after Air Combat and Hockey");
            house.NewCase(128);
            house.Act(() => house.Session.Inspect());
            Check(house.Session.Journal.Count > 0, "Investigation still accepts evidence actions after switching through flight and hockey");
            yield return Capture("17-house-after-flight");
            yield return Open(GameCollection.LauncherScene);
            yield return Capture("18-cabinet-final");
        }

        IEnumerator ExerciseHockey()
        {
            yield return SelectFromLauncher(Key.Digit3, GameCollection.HockeyScene);
            HockeyGame hockey = FindAnyObjectByType<HockeyGame>();
            if (hockey == null) throw new InvalidOperationException("Hockey composition root missing");
            hockey.DiagnosticControl = true;
            Check(hockey.Briefing && hockey.Session.Skaters.Count == 8, "Hockey opens at its instructions with three skaters and goalie on each team");
            Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Hockey instructions leave the menu cursor available");
            yield return Capture("19-hockey-briefing");
            yield return ExerciseHockeyInput(hockey);

            // Reproducible scoring fixture; ordinary AI and physics run the entire match.
            hockey.StartMatch(501);
            hockey.SetLook(0, 32);
            yield return null;
            yield return null;
            Check(!hockey.Briefing && !hockey.Paused && hockey.CameraMode == 0 && hockey.Session.Phase == MatchPhase.Faceoff,
                "Hockey seeded match starts in genuine first person at a center faceoff");
            CheckScene(GameCollection.HockeyScene);
            yield return Capture("20-hockey-first-person");
            hockey.SetCamera(1);
            yield return Capture("21-hockey-chase");
            Check(hockey.CameraMode == 1 && hockey.Session.Seed == 501, "Hockey chase view preserves the same match state");
            hockey.SetCamera(0);
            hockey.SetPaused(true);
            hockey.DiagnosticControl = false;
            float pausedTime = hockey.Session.Time;
            NVector2 pausedPosition = hockey.Session.Player.Position;
            yield return Capture("22-hockey-pause");
            Check(hockey.Paused && hockey.Session.Time == pausedTime && hockey.Session.Player.Position == pausedPosition,
                "Hockey pause holds normal Update simulation and skating state");
            hockey.DiagnosticControl = true;
            hockey.StartMatch(1988);

            // Complete default match: every skater, including the human slot, uses ordinary
            // bounded AI controls through the unchanged session and physical puck rules.
            bool capturedGoal = false, capturedReset = false, capturedLive = false;
            bool finite = true, contained = true, controlValid = true;
            float clockBeforeGoal = 0;
            for (int frame = 0; frame < 60 * 350 && hockey.Session.Phase != MatchPhase.Final; frame++)
            {
                hockey.StepForDiagnostics(default(HockeyInput), 1f / 60f, true);
                HockeySession match = hockey.Session;
                finite &= HockeyMath.Finite(match.Puck.Position) && HockeyMath.Finite(match.Puck.Velocity)
                    && HockeyMath.Finite(match.Puck.Height) && HockeyMath.Finite(match.Puck.VerticalVelocity);
                contained &= match.Puck.Height > RinkPhysics.GlassHeight || RinkPhysics.Inside(match.Puck.Position, .1f);
                controlValid &= match.Puck.ControllerId >= -1 && match.Puck.ControllerId < 6;
                for (int i = 0; i < match.Skaters.Count; i++)
                    finite &= HockeyMath.Finite(match.Skaters[i].Position) && HockeyMath.Finite(match.Skaters[i].Velocity);
                if (!capturedLive && frame >= 60 * 12 && match.Phase == MatchPhase.Playing)
                {
                    capturedLive = true;
                    yield return Capture("23-hockey-live-play");
                }
                if (!capturedGoal && match.Phase == MatchPhase.Goal)
                {
                    capturedGoal = true; clockBeforeGoal = match.TimeRemaining;
                    Check(match.BlueScore + match.OrangeScore == 1 && match.Stats.Goals[0] + match.Stats.Goals[1] == 1,
                        "An unmodified bot match scores through physical shooting and updates both scoreboard and statistics once");
                    Note("First physical goal: time " + match.Time.ToString("0.00") + "; scorer team " + match.LastGoalTeam);
                    yield return Capture("24-hockey-earned-goal");
                }
                if (capturedGoal && !capturedReset && match.Phase == MatchPhase.Faceoff)
                {
                    capturedReset = true;
                    Check(match.Stats.Faceoffs >= 2 && match.Puck.Position == NVector2.Zero && match.Puck.Velocity == NVector2.Zero,
                        "A scored goal resets the puck and teams at an announced center faceoff");
                    Check(Mathf.Abs(match.TimeRemaining - clockBeforeGoal) < .03f, "Goal celebration does not consume regulation time");
                    yield return Capture("25-hockey-faceoff-reset");
                }
                if (frame % 24 == 0) yield return null;
            }
            HockeySession completed = hockey.Session;
            Check(finite && contained && controlValid, "Complete rendered bot match keeps all physics finite, puck contained and contact control valid");
            Check(completed.Phase == MatchPhase.Final && (completed.TimeRemaining <= .01f || completed.Overtime && completed.BlueScore != completed.OrangeScore),
                "The regulation clock and any overtime reach an unambiguous final score");
            Check(capturedGoal && capturedReset, "Seeded unmodified match demonstrates an earned goal and a subsequent faceoff");
            Check(completed.Stats.Shots[0] + completed.Stats.Shots[1] > 3 && completed.Stats.Passes[0] + completed.Stats.Passes[1] > 1,
                "Rendered match develops repeated shots and teammate passes");
            Check(completed.Stats.Saves[0] + completed.Stats.Saves[1] > 0, "Rendered match includes a physical goalkeeper save");
            Check(completed.Stats.Turnovers[0] + completed.Stats.Turnovers[1] > 1 && completed.Stats.Checks[0] + completed.Stats.Checks[1] > 0,
                "Rendered match includes contested possession and defensive checks");
            Check(completed.Stats.StuckResets == 0, "Complete seeded match needs no frozen-puck watchdog reset");
            Note("Full unmodified hockey match seed " + completed.Seed + ": " + completed.BlueScore + "-" + completed.OrangeScore
                + "; elapsed " + completed.Time.ToString("0.00") + "; shots " + (completed.Stats.Shots[0] + completed.Stats.Shots[1])
                + "; saves " + (completed.Stats.Saves[0] + completed.Stats.Saves[1])
                + "; passes " + (completed.Stats.Passes[0] + completed.Stats.Passes[1])
                + "; takeaways " + (completed.Stats.Turnovers[0] + completed.Stats.Turnovers[1])
                + "; checks " + (completed.Stats.Checks[0] + completed.Stats.Checks[1]) + "; stuck resets " + completed.Stats.StuckResets);
            yield return Capture("26-hockey-final-score");
            float finalTime = completed.Time;
            hockey.StepForDiagnostics(new HockeyInput { Move = NVector2.UnitY, Shoot = true, ShotPower = 1 }, 1f / 60f);
            Check(completed.Time == finalTime && completed.Phase == MatchPhase.Final, "Final score rejects further gameplay actions");
            hockey.StartMatch(1988);
            Check(hockey.Session.BlueScore == 0 && hockey.Session.OrangeScore == 0 && hockey.Session.Time == 0
                && hockey.Session.TimeRemaining == 180 && !hockey.Paused && hockey.Session.Stats.Shots[0] == 0,
                "Hockey replay resets score, statistics, clock and pause state");
            hockey.ShowBriefing();
            Check(hockey.Briefing && !hockey.Paused && Cursor.lockState == CursorLockMode.None && Cursor.visible,
                "Hockey can reopen its instructions and restore the menu cursor");
            yield return Open(GameCollection.LauncherScene);
            Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Leaving hockey releases the captured mouse");
            yield return Capture("27-cabinet-after-hockey");
            yield return Open(GameCollection.AirScene);
            AirCombatGame returnedAir = FindAnyObjectByType<AirCombatGame>();
            returnedAir.DiagnosticControl = true;
            Check(returnedAir.Briefing && returnedAir.Mission.Player.Alive, "Air Combat relaunches independently after Hockey");
            returnedAir.StartSortie(1944, MissionKind.Duel);
            NVector3 returnedStart = returnedAir.Mission.Player.Position;
            for (int i = 0; i < 60; i++) returnedAir.StepForDiagnostics(new FlightInput { Throttle = .8f }, 1f / 60f);
            Check(returnedAir.Mission.Player.Alive && NVector3.Distance(returnedStart, returnedAir.Mission.Player.Position) > 80,
                "Air Combat flight dynamics still operate after hockey teardown");
            returnedAir.ShowBriefing();
            yield return Open(GameCollection.LauncherScene);
        }

        IEnumerator ExerciseHockeyInput(HockeyGame hockey)
        {
            previousKeyboard = Keyboard.current;
            previousMouse = Mouse.current;
            testKeyboard = InputSystem.AddDevice<Keyboard>("Hockey smoke keyboard");
            testMouse = InputSystem.AddDevice<Mouse>("Hockey smoke mouse");
            testKeyboard.MakeCurrent(); testMouse.MakeCurrent();
            try
            {
                hockey.StartMatch(1988, true);
                for (int i = 0; i < 140; i++) hockey.StepForDiagnostics(default(HockeyInput), 1f / 60f);
                // Explicit contact fixtures test adapter mechanics, not match difficulty.
                hockey.Session.Player.Position = new NVector2(-8, -13);
                hockey.Session.Player.Velocity = NVector2.Zero;
                hockey.Session.Puck.Position = new NVector2(9, 12);
                hockey.Session.Puck.Velocity = NVector2.Zero;
                hockey.SetLook(0, 32);
                hockey.DiagnosticControl = false;
                NVector2 start = hockey.Session.Player.Position;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.W, Key.LeftShift));
                yield return new WaitForSecondsRealtime(.7f);
                float speed = hockey.Session.Player.Speed;
                Check(hockey.Session.Player.Position.Y > start.Y + 1 && speed > 3,
                    "Temporary W and Shift input accelerates the skater through normal Update");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(.3f);
                Check(hockey.Session.Player.Speed > speed * .8f && hockey.Session.Player.Position.Y > start.Y + 2,
                    "Releasing physical skating input preserves visible coasting momentum");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Space));
                yield return new WaitForSecondsRealtime(.65f);
                Check(hockey.Session.Player.Speed < speed * .35f, "Temporary Space input digs in edges and brakes");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null; yield return null;
                float yaw = hockey.LookYaw;
                InputSystem.QueueDeltaStateEvent(testMouse.delta, new Vector2(80, 0));
                yield return null; yield return null;
                Check(Mathf.Abs(Mathf.DeltaAngle(yaw, hockey.LookYaw)) > 3, "Temporary mouse movement changes first-person look through the actual input adapter");
                hockey.SetLook(0, 32);

                PrepareHockeyContact(hockey);
                int shots = hockey.Session.Stats.Shots[0];
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.F));
                yield return new WaitForSecondsRealtime(.12f);
                Check(hockey.Session.Stats.Shots[0] == shots + 1 && hockey.Session.Puck.Velocity.Length() > 15,
                    "Contact fixture: F input makes a physical quick shot with travel and recorded attempt");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null; yield return null;
                PrepareHockeyContact(hockey);
                shots = hockey.Session.Stats.Shots[0];
                InputSystem.QueueStateEvent(testMouse, new MouseState().WithButton(MouseButton.Left));
                yield return new WaitForSecondsRealtime(.65f);
                Check(hockey.Session.Stats.Shots[0] == shots && PuckPhysics.CanReach(hockey.Session.Player, hockey.Session.Puck),
                    "Contact fixture: holding the left mouse button charges without firing or losing nearby stick contact");
                InputSystem.QueueStateEvent(testMouse, new MouseState());
                yield return new WaitForSecondsRealtime(.10f);
                Check(hockey.Session.Stats.Shots[0] == shots + 1 && hockey.Session.Puck.Velocity.Length() > 20,
                    "Contact fixture: left mouse release fires one charged physical shot through the normal input adapter");
                yield return new WaitForSecondsRealtime(.15f);
                Check(hockey.Session.Stats.Shots[0] == shots + 1,
                    "A released mouse charge produces one shot rather than repeated firing");
                yield return ExerciseHockeyShots(hockey);
                PrepareHockeyContact(hockey);
                int passes = hockey.Session.Stats.Passes[0];
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Q));
                yield return new WaitForSecondsRealtime(.12f);
                Check(hockey.Session.Stats.Passes[0] == passes + 1 && hockey.Session.Puck.Velocity.Length() > 8,
                    "Contact fixture: Q input sends a lower-power travelling teammate pass");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null; yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.R));
                yield return new WaitForSecondsRealtime(.12f);
                Check(hockey.Session.PassCallTime > 1.5f, "Temporary R input calls for teammate support");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null; yield return null;

                PrepareHockeyContact(hockey);
                SkaterState opponent = hockey.Session.Skaters[3];
                opponent.Position = hockey.Session.Player.Position + NVector2.UnitY * 1.3f;
                opponent.Velocity = NVector2.Zero;
                hockey.Session.Player.CheckCooldown = 0;
                int checks = hockey.Session.Stats.Checks[0];
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.E));
                yield return new WaitForSecondsRealtime(.12f);
                Check(hockey.Session.Stats.Checks[0] == checks + 1 && opponent.Stun > 0,
                    "Contact fixture: E input checks a reachable opponent through normal gameplay");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null; yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.C));
                yield return new WaitForSecondsRealtime(.12f);
                Check(hockey.CameraMode == 1, "Temporary C input changes hockey camera");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null; yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
                yield return new WaitForSecondsRealtime(.12f);
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                float paused = hockey.Session.Time;
                yield return new WaitForSecondsRealtime(.2f);
                Check(hockey.Paused && hockey.Session.Time == paused && Cursor.lockState == CursorLockMode.None,
                    "Escape input pauses hockey and releases mouse capture");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
                yield return new WaitForSecondsRealtime(.12f);
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                Check(!hockey.Paused && hockey.Session.Time > paused, "Second Escape input resumes the hockey simulation");
            }
            finally
            {
                hockey.DiagnosticControl = true;
                CleanupKeyboard(); CleanupMouse();
            }
        }

        static void PrepareHockeyContact(HockeyGame hockey)
        {
            SkaterState player = hockey.Session.Player;
            player.Position = new NVector2(-8, -13); player.Velocity = NVector2.Zero;
            player.Facing = 0; player.Stun = player.Cooldown = player.StickCooldown = 0;
            hockey.SetLook(0, 2);
            hockey.Session.Puck.Position = player.Position + NVector2.UnitY * 1.05f;
            hockey.Session.Puck.Velocity = NVector2.Zero;
            hockey.Session.Puck.Height = RinkPhysics.PuckHalfHeight;
            hockey.Session.Puck.VerticalVelocity = 0;
            hockey.Session.Puck.OutOfPlay = false;
            hockey.Session.Puck.ControllerId = player.Id; hockey.Session.Puck.NoControlTime = 0;
        }

        IEnumerator ExerciseHockeyShots(HockeyGame hockey)
        {
            PrepareHockeyContact(hockey);
            int shots=hockey.Session.Stats.Shots[0];
            InputSystem.QueueStateEvent(testMouse,new MouseState().WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(testMouse,new MouseState());
            yield return new WaitForSecondsRealtime(.10f);
            Check(hockey.Session.Stats.Shots[0]==shots+1&&hockey.Session.Puck.LastShotKind==HockeyShotKind.Snap,
                "Quick LMB click immediately releases a snap without a charge delay");
            Check(hockey.Session.Puck.Height>.15f&&hockey.Session.Puck.VerticalVelocity>0,
                "Human mouse aim creates a physically rising airborne shot");

            PrepareHockeyContact(hockey);
            int passes=hockey.Session.Stats.Passes[0];shots=hockey.Session.Stats.Shots[0];
            InputSystem.QueueStateEvent(testMouse,new MouseState().WithButton(MouseButton.Right));
            yield return new WaitForSecondsRealtime(.10f);
            Check(hockey.Session.Stats.Passes[0]==passes,"RMB hold waits for pass release or wrist-shot chord");
            InputSystem.QueueStateEvent(testMouse,new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left));
            yield return new WaitForSecondsRealtime(.48f);
            Check(hockey.Session.Stats.Passes[0]==passes&&hockey.Session.Stats.Shots[0]==shots,
                "RMB then LMB charges a wrist shot without accidentally passing or shooting");
            InputSystem.QueueStateEvent(testMouse,new MouseState().WithButton(MouseButton.Right));
            yield return new WaitForSecondsRealtime(.10f);
            Check(hockey.Session.Stats.Shots[0]==shots+1&&hockey.Session.Puck.LastShotKind==HockeyShotKind.Wrist,
                "Releasing LMB from the ordered chord fires one physical wrist shot");
            InputSystem.QueueStateEvent(testMouse,new MouseState());
            yield return new WaitForSecondsRealtime(.10f);
            Check(hockey.Session.Stats.Passes[0]==passes,"Ending a wrist-shot chord cannot emit a stray pass");

            PrepareHockeyContact(hockey);
            InputSystem.QueueStateEvent(testMouse,new MouseState().WithButton(MouseButton.Right));
            yield return new WaitForSecondsRealtime(.10f);
            InputSystem.QueueStateEvent(testMouse,new MouseState());
            yield return new WaitForSecondsRealtime(.10f);
            Check(hockey.Session.Stats.Passes[0]==passes+1,"Ordinary RMB click still releases a physical pass");

            PrepareHockeyContact(hockey);
            hockey.Session.Puck.Position=hockey.Session.Player.Position+NVector2.UnitY*4;
            hockey.Session.Puck.Velocity=-NVector2.UnitY*8;
            hockey.Session.Puck.ControllerId=-1;
            hockey.Session.Puck.LastTouchId=1;hockey.Session.Puck.LastTouchTeam=0;
            shots=hockey.Session.Stats.Shots[0];int oneTimers=hockey.Session.Stats.OneTimers[0];
            InputSystem.QueueStateEvent(testMouse,new MouseState().WithButton(MouseButton.Left));
            yield return new WaitForSecondsRealtime(.56f);
            Check(hockey.Session.Stats.OneTimers[0]==oneTimers+1&&hockey.Session.Stats.Shots[0]==shots+1,
                "Holding LMB before an incoming puck reaches the stick automatically makes one contact one-timer");
            InputSystem.QueueStateEvent(testMouse,new MouseState());
            yield return new WaitForSecondsRealtime(.12f);
            Check(hockey.Session.Stats.Shots[0]==shots+1,"LMB release after an automatic one-timer cannot fire a second shot");

            PrepareHockeyContact(hockey);
            hockey.Session.Player.Position=new NVector2(0,10);
            hockey.Session.Puck.Position=hockey.Session.Player.Position+NVector2.UnitY*1.05f;
            hockey.SetLook(0,2);
            hockey.ResolveShotTarget(Vector2.zero,out NVector2 center,out float centerHeight);
            hockey.ResolveShotTarget(new Vector2(-1,1),out NVector2 upperLeft,out float upperHeight);
            hockey.ResolveShotTarget(new Vector2(1,-1),out NVector2 lowerRight,out float lowerHeight);
            Check(Mathf.Abs(center.Y-RinkPhysics.GoalLine)<.001f&&upperLeft.X<center.X&&lowerRight.X>center.X
                &&upperHeight>centerHeight&&lowerHeight<centerHeight&&upperHeight<RinkPhysics.GoalHeight,
                "Reticle ray resolves at net height; WA and SD select upper-left and lower-right impact regions");
            hockey.SetLook(0,35);
            hockey.ResolveShotTarget(Vector2.zero,out NVector2 ice,out float iceHeight);
            Check(ice.Y<15&&iceHeight<.15f,"Looking down targets nearby ice instead of forcing every shot into the goal");
            hockey.SetLook(0,2);
            hockey.DiagnosticControl=true;
            for(int i=1;i<hockey.Session.Skaters.Count;i++)
            {hockey.Session.Skaters[i].Position=new NVector2(-10+i*.3f,-20);hockey.Session.Skaters[i].Velocity=NVector2.Zero;}
            hockey.StepForDiagnostics(new HockeyInput{Aim=NVector2.UnitY,Shoot=true,ShotKind=HockeyShotKind.Wrist,
                ShotPower=1,HasShotTarget=true,ShotTarget=upperLeft,TargetHeight=upperHeight},1f/60f);
            for(int i=0;i<10;i++)hockey.StepForDiagnostics(default(HockeyInput),1f/60f);
            Check(hockey.Session.Puck.Height>.6f,"Rendered near-net fixture lifts a wrist shot toward the selected upper corner");
            yield return Capture("28-hockey-airborne-shot");
            hockey.DiagnosticControl=false;
            PrepareHockeyContact(hockey);
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
        void CleanupMouse()
        {
            if (testMouse != null)
            {
                if (testMouse.added) InputSystem.RemoveDevice(testMouse);
                testMouse = null;
            }
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            previousMouse = null;
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
            CleanupMouse();
            InputSettings expectedSettings = originalInputSettings;
            RestoreDiagnosticInput();
            Check(expectedSettings != null && InputSystem.settings == expectedSettings,
                "Diagnostic input settings and private devices are restored without changing the project asset");
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
        void OnDestroy() { CleanupKeyboard(); CleanupMouse(); RestoreDiagnosticInput(); Application.logMessageReceived -= Record; }
    }
}
