using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IceResurfacer.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace IceResurfacer.UI.Editor
{
    /// <summary>Injected integration checks; never callable against the working authored project.</summary>
    public static class IceMachineryPhysicalValidation
    {
        public static void Run(IceMachineryConsole console)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            bool phase2 = project.Replace('\\', '/').Contains("/Validation/UIAuthoring-Phase2/") && File.Exists(Path.Combine(project, "UIAuthoringPhase2ValidationProject.txt"));
            bool phase4 = project.Replace('\\', '/').Contains("/Validation/UIAuthoring-Phase4/") && File.Exists(Path.Combine(project, "UIAuthoringPhase4ValidationProject.txt"));
            if (!phase2 && !phase4)
                throw new InvalidOperationException("Run machinery input validation only in the marked Phase 2 disposable project.");
            if (console == null || !Application.IsPlaying(console.gameObject) || console.game == null || console.game.Sim == null)
                throw new InvalidOperationException("A live authored machinery console is required.");
            var game = console.game;
            var sim = game.Sim;
            var c = sim.Controls;
            var mouse = Mouse.current;
            var keys = Keyboard.current;
            if (mouse == null || keys == null) throw new InvalidOperationException("The parent runner must provide injected Mouse and Keyboard devices.");
            var module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            var checks = new List<string>();
            string output = Path.Combine(project, "ValidationResults", "MachineryPhysical-results.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            void Check(bool result, string label)
            {
                if (!result) throw new InvalidOperationException("Machinery physical validation: " + label);
                checks.Add("PASS " + label);
            }
            var update = typeof(IceResurfacerGame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            var render = typeof(IceResurfacerGame).GetMethod("UpdateMachine", BindingFlags.Instance | BindingFlags.NonPublic);
            var look = typeof(IceResurfacerGame).GetMethod("UpdateLook", BindingFlags.Instance | BindingFlags.NonPublic);
            var focus = typeof(IceResurfacerGame).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic);
            var camera = game.DriverCamera.transform;
            Vector3 cameraPosition = camera.localPosition;
            Quaternion cameraRotation = camera.localRotation;
            string oldView = game.CameraView;
            float oldLift = sim.Conditioner.Lift, oldTank = sim.TankLift, oldFlood = c.FloodFlow, oldWash = c.WashValve, oldBlade = c.BladeDepthMm;
            bool oldHorizontal = c.HorizontalAuger, oldVertical = c.VerticalAuger, oldBrush = c.BoardBrush, oldPump = c.WashPump;
            bool oldCompact = game.Dashboard.Compact, oldHidden = game.HudHidden, oldFocus = game.HasFocus, oldDiagnostics = game.DiagnosticsVisible;
            var cursorLock = Cursor.lockState;
            bool cursorVisible = Cursor.visible;
            var uiLever = console.controls.Single(control => control.kind == IceConsoleControlKind.Conditioner);
            var uiRect = (RectTransform)uiLever.transform;
            Vector3 uiPosition = uiRect.anchoredPosition3D;
            void Render() => render.Invoke(game, new object[] { 0f });
            Vector2 Screen(Vector3 p) => game.DriverCamera.WorldToScreenPoint(p);
            void Keys(params Key[] held)
            {
                InputSystem.QueueStateEvent(keys, new KeyboardState(held));
                InputSystem.Update();
                update.Invoke(game, null);
                console.RefreshNow();
            }
            void Pointer(Vector2 p, bool down = false, bool right = false, Vector2 delta = default)
            {
                var state = new MouseState { position = p, delta = delta };
                state = state.WithButton(MouseButton.Left, down).WithButton(MouseButton.Right, right);
                InputSystem.QueueStateEvent(mouse, state);
                InputSystem.Update();
                game.PhysicalControls.UpdateInput(!right, game.Dashboard.PointerOverInterface(game, p));
                module.Process();
                console.TickInput();
            }
            void Look() => look.Invoke(game, new object[] { true, .02f });
            try
            {
                focus.Invoke(game, new object[] { true });
                if (game.HudHidden) game.ToggleVideoMode();
                if (game.DiagnosticsVisible) game.ToggleDiagnostics();
                console.SetCompact(false);
                Keys();
                game.ReleaseControls();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                console.RefreshNow();
                Check(console.Visible && console.CanInteract, "Authored expanded console is active for physical/keyboard parity checks.");
                foreach (float height in new[] { 0f, .5f, 1f })
                {
                    sim.Conditioner.Lift = height;
                    Render();
                    game.SelectCameraView("Controls");
                    game.TickControlsAnchor(1);
                    foreach (int index in new[] { 1, 4 })
                    {
                        var lever = IcePhysicalControls.TransformFor(game.World, index);
                        Vector3 grip = IcePhysicalControls.GripPositions(game.World, sim.Config)[index];
                        camera.LookAt(grip, Vector3.up);
                        Vector2 p = Screen(grip);
                        string expected = index == 1 ? "Conditioner hydraulic lever" : "Snow tank hydraulic lever";
                        Check(game.PhysicalControls.GetHitControl(p) == expected && !game.Dashboard.PointerOverInterface(game, p),
                            expected + " remains reachable outside UI at conditioner lift " + height + ".");
                        Pointer(p); Pointer(p, true);
                        Vector2 pull = (Screen(lever.position - lever.parent.forward * .15f) - Screen(lever.position)).normalized;
                        if (pull.sqrMagnitude < .01f) pull = Vector2.down;
                        Pointer(p + pull * 80, true);
                        Check((index == 1 ? c.ConditionerHydraulic : c.TankHydraulic) < -.5f,
                            expected + " backward pull lowers at lift " + height + ".");
                        Pointer(p - pull * 80, true);
                        Check((index == 1 ? c.ConditionerHydraulic : c.TankHydraulic) > .5f,
                            expected + " forward push raises at lift " + height + ".");
                        Pointer(p);
                        Check(!game.PhysicalControls.IsDragging && Mathf.Abs(index == 1 ? c.ConditionerHydraulic : c.TankHydraulic) < .0001f,
                            expected + " releases to neutral at lift " + height + ".");
                        Render();
                    }
                    foreach (int index in new[] { 5, 6 })
                    {
                        if (index == 5) c.FloodFlow = .35f; else c.WashValve = .35f;
                        Render();
                        var valve = IcePhysicalControls.TransformFor(game.World, index);
                        Vector3 grip = IcePhysicalControls.GripPositions(game.World, sim.Config)[index];
                        camera.LookAt(grip, Vector3.up);
                        Vector2 p = Screen(grip);
                        string expected = index == 5 ? "Flood water valve" : "Wash water valve";
                        Check(game.PhysicalControls.GetHitControl(p) == expected && !game.Dashboard.PointerOverInterface(game, p),
                            expected + " remains reachable outside UI at conditioner lift " + height + ".");
                        Pointer(p); Pointer(p, true);
                        Vector3 target = valve.position + Quaternion.AngleAxis(-25, valve.up) * (grip - valve.position);
                        Pointer(Screen(target), true);
                        Check((index == 5 ? c.FloodFlow : c.WashValve) > .37f,
                            expected + " physical opening direction is preserved at lift " + height + ".");
                        Pointer(Screen(target));
                    }
                }
                c.FloodFlow = c.WashValve = 0;
                sim.Conditioner.Lift = .5f;
                Render();
                Vector3 overlapGrip = IcePhysicalControls.GripPositions(game.World, sim.Config)[1];
                camera.LookAt(overlapGrip, Vector3.up);
                Vector2 overlap = Screen(overlapGrip);
                var uiCamera = console.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : console.canvas.worldCamera;
                RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)uiRect.parent, overlap, uiCamera, out Vector3 world);
                uiRect.position += world - uiLever.interactionArea.TransformPoint(uiLever.interactionArea.rect.center);
                Canvas.ForceUpdateCanvases();
                Pointer(overlap); Pointer(overlap, true);
                var area = uiLever.interactionArea;
                Vector2 lower = RectTransformUtility.WorldToScreenPoint(uiCamera,
                    area.TransformPoint(area.rect.center + Vector2.down * area.rect.height * .35f));
                Pointer(lower, true);
                Check(uiLever.IsHolding && !game.PhysicalControls.IsDragging && c.ConditionerHydraulic < -.5f,
                    "Relocated authored lever over the real cockpit grip owns the click and commands lowering without physical acquisition.");
                Pointer(lower);
                Check(!uiLever.IsHolding && c.ConditionerHydraulic == 0, "The relocated authored lever releases its own hydraulic command.");
                uiRect.anchoredPosition3D = uiPosition;
                Canvas.ForceUpdateCanvases();

                Vector2 authoredLower = RectTransformUtility.WorldToScreenPoint(uiCamera,
                    area.TransformPoint(area.rect.center + Vector2.down * area.rect.height * .35f));
                Pointer(authoredLower); Pointer(authoredLower, true);
                Check(uiLever.IsHolding && c.ConditionerHydraulic == -1, "Authored lever acquires lowering before keyboard takeover.");
                Keys(Key.Digit1, Key.RightShift);
                Check(!uiLever.IsHolding && c.ConditionerHydraulic == 1,
                    "Keyboard raising supersedes a held UI lever without UI release neutralizing the keyboard command.");
                console.ReleaseInteractions();
                Check(c.ConditionerHydraulic == 1, "Releasing all console gestures preserves the active keyboard owner.");
                Pointer(authoredLower);
                Check(c.ConditionerHydraulic == 1, "Releasing the original UI mouse press also preserves keyboard raising.");
                Keys();
                Check(c.ConditionerHydraulic == 0, "Releasing the takeover key restores the existing keyboard neutral state.");

                foreach (var entry in new[] { new[] { Key.Digit1 }, new[] { Key.Digit1, Key.RightShift }, new[] { Key.Digit7 }, new[] { Key.Digit7, Key.RightShift } })
                {
                    Keys();
                    Keys(entry);
                    bool conditioner = entry[0] == Key.Digit1;
                    float expected = conditioner ? (entry.Length == 2 ? 1 : -1) : (entry.Length == 2 ? -1 : 1);
                    Check(Mathf.Abs((conditioner ? c.ConditionerHydraulic : c.TankHydraulic) - expected) < .0001f,
                        "Existing keyboard hydraulic direction is preserved for " + string.Join("+", entry) + ".");
                    Keys();
                    Check((conditioner ? c.ConditionerHydraulic : c.TankHydraulic) == 0, "Keyboard hydraulic release remains neutral.");
                }
                bool[] before = { c.HorizontalAuger, c.VerticalAuger, c.BoardBrush, c.WashPump };
                Key[] toggles = { Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
                for (int i = 0; i < toggles.Length; i++)
                {
                    Keys(toggles[i]); Keys();
                    bool state = i == 0 ? c.HorizontalAuger : i == 1 ? c.VerticalAuger : i == 2 ? c.BoardBrush : c.WashPump;
                    Check(state != before[i], "Keyboard " + toggles[i] + " still toggles the authoritative machinery state.");
                    var widget = console.controls.Single(control => control.kind == new[] {
                        IceConsoleControlKind.HorizontalAuger, IceConsoleControlKind.VerticalAuger,
                        IceConsoleControlKind.BoardBrush, IceConsoleControlKind.WashPump }[i]);
                    widget.Refresh(sim);
                    Check(widget.value != null ? widget.value.text == (state ? "ON" : "OFF") :
                        widget.maintainedToggle != null ? widget.maintainedToggle.isOn == state : Mathf.Approximately(widget.indicator.fillAmount, state ? 1 : 0),
                        "Authored maintained indicator follows keyboard changes without duplicate state.");
                }
                c.FloodFlow = c.WashValve = .5f;
                Keys(Key.RightBracket); Keys();
                Check(c.FloodFlow > .5f && Mathf.Approximately(c.WashValve, .5f), "Unshifted bracket keys still adjust only flood flow.");
                float flood = c.FloodFlow;
                Keys(Key.RightShift, Key.RightBracket); Keys();
                Check(c.WashValve > .5f && Mathf.Approximately(c.FloodFlow, flood), "Right Shift plus bracket keys still adjust only wash flow.");
                c.BladeDepthMm = sim.Config.MaxBladeDepthMm * .5f;
                float blade = c.BladeDepthMm;
                Keys(Key.Period); Keys();
                Check(c.BladeDepthMm > blade, "Period still deepens the blade screw.");
                blade = c.BladeDepthMm;
                Keys(Key.Comma); Keys();
                Check(c.BladeDepthMm < blade, "Comma still retracts the blade screw.");
                c.FloodFlow = c.WashValve = 0;
                Keys(Key.Numpad1); Keys();
                Check(game.HudHidden && !console.Visible, "Numpad 1 hides the authored console through the existing keyboard path.");
                Keys(Key.Numpad1); Keys();
                Check(!game.HudHidden && console.Visible, "Numpad 1 restores the authored console.");
                Pointer(overlap, false, true); Look();
                checks.Add("INFO RMB right=" + mouse.rightButton.isPressed + "; Looking=" + game.Looking +
                    "; Cursor=" + Cursor.lockState + "; ConsoleCanInteract=" + console.CanInteract + ".");
                Check(mouse.rightButton.isPressed && game.Looking && !console.CanInteract, "RMB retains look ownership and excludes machinery UI.");
                if (Cursor.lockState != CursorLockMode.Locked)
                    checks.Add("LIMITATION Native cursor lock is not granted by this background Editor; injected look behavior is tested separately.");
                Pointer(overlap, false, true); Look();
                Quaternion lookBefore = camera.rotation;
                Pointer(overlap, false, true, new Vector2(36, 12)); Look();
                Check(Quaternion.Angle(lookBefore, camera.rotation) > .01f, "RMB relative motion still rotates the existing operator camera.");
                Pointer(overlap); Look(); console.RefreshNow();
                Check(!game.Looking && console.CanInteract, "RMB release restores console interaction.");
            }
            catch (Exception error) { checks.Add("FAIL " + error); throw; }
            finally
            {
                uiRect.anchoredPosition3D = uiPosition;
                game.ReleaseControls();
                InputSystem.QueueStateEvent(keys, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                Look();
                if (game.HudHidden != oldHidden) game.ToggleVideoMode();
                if (game.DiagnosticsVisible != oldDiagnostics) game.ToggleDiagnostics();
                console.SetCompact(oldCompact);
                sim.Conditioner.Lift = oldLift;
                // Restore the isolated fixture through its private setter; production authority stays unchanged.
                sim.GetType().GetProperty("TankLift").SetValue(sim, oldTank);
                c.FloodFlow = oldFlood; c.WashValve = oldWash; c.BladeDepthMm = oldBlade;
                c.HorizontalAuger = oldHorizontal; c.VerticalAuger = oldVertical; c.BoardBrush = oldBrush; c.WashPump = oldPump;
                game.SelectCameraView(oldView);
                camera.localPosition = cameraPosition; camera.localRotation = cameraRotation;
                Render();
                focus.Invoke(game, new object[] { oldFocus });
                Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
                console.RefreshNow();
                Canvas.ForceUpdateCanvases();
                File.WriteAllLines(output, checks);
            }
        }
    }
}
