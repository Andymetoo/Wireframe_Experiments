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
    /// <summary>Real input and cockpit integration checks, restricted to the marked disposable validation project.</summary>
    public static class IceUIInputValidation
    {
        public static void Run(IceAuthoredHud hud)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            bool phase1 = project.Replace('\\', '/').Contains("/Validation/UIAuthoring-Phase1/") && File.Exists(Path.Combine(project, "UIAuthoringValidationProject.txt"));
            bool phase4 = project.Replace('\\', '/').Contains("/Validation/UIAuthoring-Phase4/") && File.Exists(Path.Combine(project, "UIAuthoringPhase4ValidationProject.txt"));
            if (!phase1 && !phase4)
                throw new InvalidOperationException("UI input acceptance runs only in the marked disposable UIAuthoring-Phase1 project.");
            if (hud == null || !Application.IsPlaying(hud.gameObject) || !hud.IsAvailable || !hud.Visible)
                throw new InvalidOperationException("UI input acceptance requires the active authored HUD in a running Play session.");
            var game = hud.game;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.added) throw new InvalidOperationException("The parent validation must supply an injected Mouse.");
            var module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            var keyboardBefore = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>("Aurora UI keyboard acceptance");
            keyboard.MakeCurrent();
            var checks = new List<string>();
            string output = Path.Combine(project, "ValidationResults", "UIInput-results.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            void Check(bool pass, string message)
            {
                if (!pass) throw new InvalidOperationException("UI input acceptance: " + message);
                checks.Add("PASS " + message);
            }

            var load = hud.widgets.Single(widget => widget.kind == IceHudWidgetKind.Load);
            var loadRect = (RectTransform)load.transform;
            Vector3 loadPosition = loadRect.anchoredPosition3D;
            var camera = game.DriverCamera.transform;
            Vector3 cameraPosition = camera.localPosition;
            Quaternion cameraRotation = camera.localRotation;
            string cameraView = game.CameraView;
            bool diagnosticBefore = game.DiagnosticsVisible;
            bool hiddenBefore = game.HudHidden;
            CursorLockMode cursorBefore = Cursor.lockState;
            bool cursorVisibleBefore = Cursor.visible;
            Vector2 mouseBefore = mouse.position.ReadValue();
            var controls = game.Sim.Controls;
            float floodBefore = controls.FloodFlow, washBefore = controls.WashValve, bladeBefore = controls.BladeDepthMm;
            float conditionerBefore = controls.ConditionerHydraulic, tankBefore = controls.TankHydraulic;
            bool horizontalBefore = controls.HorizontalAuger, verticalBefore = controls.VerticalAuger;
            var update = typeof(IceResurfacerGame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            var look = typeof(IceResurfacerGame).GetMethod("UpdateLook", BindingFlags.Instance | BindingFlags.NonPublic);
            var render = typeof(IceResurfacerGame).GetMethod("UpdateMachine", BindingFlags.Instance | BindingFlags.NonPublic);
            void Keys(params Key[] held)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
                InputSystem.Update();
            }
            void Pointer(Vector2 position, bool left = false, bool right = false, bool physical = true, Vector2 delta = default)
            {
                var state = new MouseState { position = position, delta = delta };
                state = state.WithButton(MouseButton.Left, left).WithButton(MouseButton.Right, right);
                InputSystem.QueueStateEvent(mouse, state);
                InputSystem.Update();
                // Query machinery first to prove protection is independent of UI module update order.
                if (physical)
                    game.PhysicalControls.UpdateInput(true, game.Dashboard.PointerOverInterface(game, position));
                module.Process();
            }
            void Look() => look.Invoke(game, new object[] { true, .02f });
            Vector2 Screen(Vector3 position) => game.DriverCamera.WorldToScreenPoint(position);
            try
            {
                Keys();
                game.ReleaseControls();
                if (game.DiagnosticsVisible) game.ToggleDiagnostics();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                hud.RefreshNow();
                game.SelectCameraView("Controls");
                game.TickControlsAnchor(1);
                Vector3 grip = IcePhysicalControls.GripPositions(game.World, game.Sim.Config)[1];
                camera.LookAt(grip, Vector3.up);
                Vector2 cockpitPoint = Screen(grip);
                Check(game.PhysicalControls.GetHitControl(cockpitPoint) == "Conditioner hydraulic lever",
                    "Camera ray reaches the actual conditioner lever at the test screen position.");
                Check(!game.Dashboard.PointerOverInterface(game, cockpitPoint),
                    "Physical lever test position is clear of authored and legacy HUD regions.");

                var uiCamera = hud.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hud.canvas.worldCamera;
                Check(RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)loadRect.parent, cockpitPoint, uiCamera, out Vector3 target),
                    "Projected cockpit grip maps into the authored UI parent.");
                loadRect.position += target - loadRect.TransformPoint(loadRect.rect.center);
                Canvas.ForceUpdateCanvases();
                Check(hud.ContainsPointer(cockpitPoint),
                    "Relocated machine-load widget raycasts directly over the physical lever.");
                Pointer(cockpitPoint);
                bool beforeClick = game.DiagnosticsVisible;
                Pointer(cockpitPoint, true);
                Check(!game.PhysicalControls.IsDragging && Mathf.Abs(controls.ConditionerHydraulic) < .0001f,
                    "UI press over a real hydraulic grip cannot acquire or command the physical lever.");
                Pointer(cockpitPoint);
                Check(game.DiagnosticsVisible != beforeClick,
                    "Injected UI press/release over the real cockpit grip invokes diagnostics.");
                Check(!game.PhysicalControls.IsDragging && Mathf.Abs(controls.ConditionerHydraulic) < .0001f &&
                    controls.FloodFlow == floodBefore && controls.WashValve == washBefore &&
                    controls.BladeDepthMm == bladeBefore && controls.HorizontalAuger == horizontalBefore &&
                    controls.VerticalAuger == verticalBefore,
                    "The overlapping UI click leaves hydraulic, valve, blade and auger commands unchanged.");
                if (game.DiagnosticsVisible) game.ToggleDiagnostics();
                loadRect.anchoredPosition3D = loadPosition;
                Canvas.ForceUpdateCanvases();
                Check(!game.Dashboard.PointerOverInterface(game, cockpitPoint),
                    "Moving the UI away immediately releases the physical cockpit screen position.");

                Pointer(cockpitPoint);
                Pointer(cockpitPoint, true);
                Check(game.PhysicalControls.IsDragging && game.PhysicalControls.ActiveControl == "Conditioner hydraulic lever",
                    "Mouse press in open space acquires the real hydraulic grip.");
                var lever = game.World.ConditionerLever;
                Vector2 pull = (Screen(lever.position - lever.parent.forward * .15f) - Screen(lever.position)).normalized;
                if (pull.sqrMagnitude < .01f) pull = Vector2.down;
                Vector2 pulledPoint = cockpitPoint + pull * 80;
                Pointer(pulledPoint, true);
                Check(controls.ConditionerHydraulic < -.5f,
                    "Physical backward lever drag still commands lowering with authored UI enabled.");
                Check(RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)loadRect.parent, pulledPoint, uiCamera, out target),
                    "An authored panel can be placed across an already active cockpit drag.");
                loadRect.position += target - loadRect.TransformPoint(loadRect.rect.center);
                Canvas.ForceUpdateCanvases();
                Pointer(pulledPoint, true);
                Check(game.PhysicalControls.IsDragging && controls.ConditionerHydraulic < -.5f,
                    "A physical drag acquired in open space keeps ownership when it crosses an authored panel.");
                Pointer(pulledPoint);
                Check(!game.PhysicalControls.IsDragging && Mathf.Abs(controls.ConditionerHydraulic) < .0001f,
                    "Releasing the physical drag neutralizes hydraulic demand.");
                loadRect.anchoredPosition3D = loadPosition;
                Canvas.ForceUpdateCanvases();
                render.Invoke(game, new object[] { 0f });

                controls.FloodFlow = .35f;
                render.Invoke(game, new object[] { 0f });
                var valve = game.World.FloodLever;
                Vector3 valveGrip = IcePhysicalControls.GripPositions(game.World, game.Sim.Config)[5];
                camera.LookAt(valveGrip, Vector3.up);
                Vector2 valvePoint = Screen(valveGrip);
                Check(game.PhysicalControls.GetHitControl(valvePoint) == "Flood water valve" &&
                    !game.Dashboard.PointerOverInterface(game, valvePoint),
                    "The real flood-water valve is reachable through open screen space.");
                Pointer(valvePoint);
                Pointer(valvePoint, true);
                Vector3 turnedGrip = valve.position + Quaternion.AngleAxis(-25, valve.up) * (valveGrip - valve.position);
                Pointer(Screen(turnedGrip), true);
                Check(controls.FloodFlow > .37f,
                    "Physical valve turning still changes the authoritative flood command with authored UI enabled.");
                Pointer(Screen(turnedGrip));
                Check(!game.PhysicalControls.IsDragging, "Releasing the physical valve clears its drag.");
                controls.FloodFlow = floodBefore;
                render.Invoke(game, new object[] { 0f });

                // These keys travel through the game's real Update path, not ToggleVideoMode directly.
                Keys(Key.Numpad1);
                update.Invoke(game, null);
                hud.RefreshNow();
                Check(game.HudHidden && !hud.Visible && !hud.ContainsPointer(cockpitPoint),
                    "Injected Numpad 1 hides authored drawing and pointer capture through game.Update.");
                Keys(); update.Invoke(game, null);
                Keys(Key.Numpad1); update.Invoke(game, null);
                Keys(); update.Invoke(game, null);
                hud.RefreshNow();
                Check(!game.HudHidden && hud.Visible,
                    "A second injected Numpad 1 restores the live authored HUD.");

                // RMB must retain its existing look ownership even if initiated over a widget.
                Vector2 uiPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, loadRect.TransformPoint(loadRect.rect.center));
                Pointer(uiPoint, false, true, false);
                Look();
                checks.Add("INFO RMB state: injectedMouseIsCurrent=" + (Mouse.current == mouse) + ", rightPressed=" + mouse.rightButton.isPressed +
                    ", Looking=" + game.Looking + ", nativeCursorLock=" + Cursor.lockState + ", CanInteract=" + hud.CanInteract + ".");
                Check(Mouse.current == mouse && mouse.rightButton.isPressed && game.Looking && !hud.CanInteract,
                    "RMB over a UI widget acquires mouse look and disables authored UI interaction.");
                if (Cursor.lockState == CursorLockMode.Locked)
                    Check(true, "Native cursor lock acquired during RMB look.");
                else if (Application.isBatchMode)
                    checks.Add("NOT VERIFIED Native cursor lock: hidden batchmode Editor reported " + Cursor.lockState +
                        "; visible Editor or standalone hardware verification is still required.");
                else
                    Check(false, "Native cursor lock acquired during RMB look.");
                Pointer(uiPoint, false, true, false); Look();
                Quaternion beforeLook = camera.rotation;
                Pointer(uiPoint, false, true, false, new Vector2(36, 12)); Look();
                Check(Quaternion.Angle(beforeLook, camera.rotation) > .01f,
                    "Injected relative mouse movement still changes the camera during RMB look.");
                Pointer(uiPoint, false, false, false); Look();
                hud.RefreshNow();
                Check(!game.Looking && Cursor.lockState == CursorLockMode.None && hud.CanInteract,
                    "Releasing RMB restores unlocked UI interaction.");
                checks.Add("PASS All checks used injected Input System events and actual runtime handlers; no native desktop click claim is made.");
            }
            catch (Exception error)
            {
                checks.Add("FAIL " + error);
                throw;
            }
            finally
            {
                loadRect.anchoredPosition3D = loadPosition;
                game.ReleaseControls();
                Keys();
                Pointer(mouseBefore, false, false, false);
                Look();
                if (game.HudHidden != hiddenBefore) game.ToggleVideoMode();
                if (game.DiagnosticsVisible != diagnosticBefore) game.ToggleDiagnostics();
                game.SelectCameraView(cameraView);
                camera.localPosition = cameraPosition;
                camera.localRotation = cameraRotation;
                controls.FloodFlow = floodBefore;
                controls.WashValve = washBefore;
                controls.BladeDepthMm = bladeBefore;
                controls.ConditionerHydraulic = conditionerBefore;
                controls.TankHydraulic = tankBefore;
                controls.HorizontalAuger = horizontalBefore;
                controls.VerticalAuger = verticalBefore;
                render.Invoke(game, new object[] { 0f });
                Cursor.lockState = cursorBefore;
                Cursor.visible = cursorVisibleBefore;
                InputSystem.RemoveDevice(keyboard);
                if (keyboardBefore != null && keyboardBefore.added) keyboardBefore.MakeCurrent();
                hud.RefreshNow();
                Canvas.ForceUpdateCanvases();
                File.WriteAllLines(output, checks);
            }
        }
    }
}
