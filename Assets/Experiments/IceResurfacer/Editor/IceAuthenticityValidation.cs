using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Editor
{
    /// <summary>Real keyboard/mouse routing into the live game, followed by fixed-step mechanics.</summary>
    internal static class IceAuthenticityValidation
    {
        static IceResurfacerGame game;
        static Keyboard keyboard;
        static Mouse mouse;
        static MethodInfo update, render;
        static readonly List<string> observations = new List<string>();
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Authenticity: " + message);
            observations.Add("PASS " + message);
        }
        static void Input(Key[] keys = null, ushort buttons = 0, Vector2 position = default)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys ?? new Key[0]));
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = buttons, position = position });
            InputSystem.Update(); update.Invoke(game, null);
        }
        static void Advance(float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / .02f); i++)
            {
                game.Sim.Step(.02f);
                render.Invoke(game, new object[] { .02f });
            }
        }
        static void Place(float x, float z, float heading = 0)
        {
            var s = game.Sim;
            s.Vehicle.X = x; s.Vehicle.Z = z; s.Vehicle.Heading = heading; s.Vehicle.Speed = 0;
            s.Conditioner.Heading = heading;
            s.Conditioner.X = x - Mathf.Sin(heading) * 1.1f;
            s.Conditioner.Z = z - Mathf.Cos(heading) * 1.1f;
            render.Invoke(game, new object[] { 0f });
        }
        static void Stop()
        {
            game.Sim.Controls.FloodFlow = game.Sim.Controls.WashValve = 0;
            game.Sim.Controls.WashPump = false;
            Input(new[] { Key.LeftCtrl }); Advance(2);
            Check(Mathf.Abs(game.Sim.Vehicle.Speed) < .001f, "CTRL stops through the real keyboard path");
            Input();
        }
        static void Resume(string operation)
        {
            var s = game.Sim; float z = s.Vehicle.Z;
            Input(new[] { Key.W });
            Check(s.Controls.Throttle == 1 && !s.Controls.Brake, operation + ": W reaches drive and releases brake");
            Advance(1);
            Check(s.Vehicle.Z > z + .06f && s.Vehicle.Speed > .1f,
                operation + ": actual forward travel resumes (" + s.DriveInhibitReason + ")");
        }
        static Vector2 LeverPointer()
        {
            Vector3 point = game.World.ConditionerLever.TransformPoint(0, .22f, 0);
            Vector3 local = game.DriverCamera.transform.parent.InverseTransformDirection(point - game.DriverCamera.transform.position);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(-local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            IceCameraValidation.Aim(yaw, pitch, false);
            return game.DriverCamera.WorldToScreenPoint(point);
        }
        static Vector2 PullAxis()
        {
            Vector3 pivot = game.World.ConditionerLever.position;
            Vector3 a = game.DriverCamera.WorldToScreenPoint(pivot);
            Vector3 b = game.DriverCamera.WorldToScreenPoint(pivot - game.World.Machine.forward * .15f);
            return new Vector2(b.x - a.x, b.y - a.y).normalized;
        }
        static Vector2 GrabLower()
        {
            Vector2 pointer = LeverPointer();
            Input(); Input(null, 1, pointer);
            Check(game.PhysicalControls.IsDragging && game.PhysicalControls.ActiveControl.Contains("Conditioner"),
                "LMB ray picks the real conditioner lever knob");
            pointer -= PullAxis() * Mathf.Max(50, Screen.height * .08f);
            Input(null, 1, pointer);
            Check(game.Sim.Controls.ConditionerHydraulic < -.9f, "physical push commands conditioner LOWER");
            return pointer;
        }
        public static void Run(IceResurfacerGame target, Keyboard keys, Mouse pointer)
        {
            game = target; keyboard = keys; mouse = pointer; observations.Clear();
            update = typeof(IceResurfacerGame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            render = typeof(IceResurfacerGame).GetMethod("UpdateMachine", BindingFlags.Instance | BindingFlags.NonPublic);
            bool enabled = game.enabled; game.enabled = false;
            try
            {
                game.NewSession(true); game.Dashboard.Compact = true;
                Input(new[] { Key.S, Key.LeftShift }); Advance(25); Stop();
                Check(game.Sim.HasEnteredRink, "fixture genuinely drives from service bay onto ice before interaction");
                // Input-recovery matrix uses a clear straight lane after the real reverse departure.
                Place(0,-15);
                Action[] actions = {
                    () => Input(new[] { Key.Period }),
                    () => { Input(new[] { Key.Digit1 }); Advance(.3f); Input(); },
                    () => Input(new[] { Key.Digit2 }),
                    () => Input(new[] { Key.Digit3 }),
                    () => Input(new[] { Key.Digit4 }),
                    () => { game.Sim.Controls.WashValve = .43f; Input(new[] { Key.Digit5 }); },
                    () => { game.Sim.Controls.FloodFlow = .37f; Input(); },
                    () => Input(new[] { Key.Digit6 }),
                    () => { game.Sim.SetTankHydraulic(1); Advance(.2f); game.Sim.SetTankHydraulic(0); },
                    () => { Input(new[] { Key.Space }); Advance(.2f); Input(); }
                };
                string[] names = { "blade adjustment", "conditioner hold/release", "horizontal auger", "vertical auger",
                    "board brush", "wash valve/pump", "flood water", "snow breaker", "free on-ice dump", "standing/sitting" };
                for (int i = 0; i < actions.Length; i++)
                {
                    Stop(); actions[i](); Input(); Resume(names[i]);
                }
                Input(new[]{Key.Digit7,Key.W});Advance(.3f);
                Check(game.Sim.Controls.TankHydraulic==1&&game.Sim.TankRaised&&game.Sim.Vehicle.Speed>0,"7 raises tank while W continues Free Simulation travel");
                Input();Check(game.Sim.Controls.TankHydraulic==0,"7 release centres dump command");
                float loweringSeconds=game.Sim.TankLift*game.Sim.Config.TankLiftSeconds+.2f;
                Input(new[]{Key.Digit7,Key.LeftShift});Advance(loweringSeconds);Input();
                Check(!game.Sim.TankRaised,"Shift+7 lowers the same physical tank");
                float flood=game.Sim.Controls.FloodFlow,wash=game.Sim.Controls.WashValve;
                Input(new[]{Key.RightBracket});
                Check(game.Sim.Controls.FloodFlow>flood&&game.Sim.Controls.WashValve==wash,"right bracket adjusts only flood water");
                Input();flood=game.Sim.Controls.FloodFlow;
                Input(new[]{Key.LeftShift,Key.RightBracket});
                Check(game.Sim.Controls.WashValve>wash&&game.Sim.Controls.FloodFlow==flood,"Shift+right bracket adjusts only wash water");
                Input();bool fps=game.PerformanceVisible;
                Input(new[]{Key.F4});Check(game.PerformanceVisible!=fps,"F4 toggles performance readout");Input();
                Input(new[]{Key.F2});Check(game.Surface.DebugMode==1,"F2 exposes actual shaving mask");Input();
                Input(new[]{Key.F2});Check(game.Surface.DebugMode==2,"F2 exposes affected cut/wash/flood cells");Input();
                Input(new[]{Key.F2});Check(game.Surface.DebugMode==0,"F2 returns to normal ice rendering");Input();
                Stop();
                Vector2 dragged = GrabLower(); Advance(.2f);
                Input(new[] { Key.W }, 1, dragged); Advance(.4f);
                Check(game.Sim.Controls.Throttle == 1 && game.Sim.Vehicle.Speed > .1f,
                    "W continues driving during an actively captured mechanical drag");
                Input(null, 0, new Vector2(-100, -100));
                Check(!game.PhysicalControls.IsDragging && game.Sim.Controls.ConditionerHydraulic == 0,
                    "polled LMB release outside the view clears drag and centres spool");
                Stop(); Resume("mouse drag/release outside window"); Stop();

                dragged = GrabLower();
                Input(new[] { Key.Escape }, 1, dragged);
                Check(game.Paused && game.Sim.Controls.ConditionerHydraulic == 0 && !game.PhysicalControls.IsDragging,
                    "pause while dragging releases capture and hydraulic demand");
                Input(); Input(new[] { Key.Escape }); Input(); Resume("pause/resume during drag"); Stop();
                Input(new[] { Key.F1 }); Check(game.HelpVisible, "F1 opens guide");
                Input(); Input(new[] { Key.F1 }); Input(); Resume("guide close"); Stop();

                dragged = GrabLower(); Input(null, 2, dragged);
                Check(game.Looking && !game.PhysicalControls.IsDragging && game.Sim.Controls.ConditionerHydraulic == 0,
                    "RMB releases mechanical capture and keeps drive input independent");
                Input(); Resume("RMB release"); Stop();
                dragged = GrabLower();
                typeof(IceResurfacerGame).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(game, new object[] { false });
                Check(!game.PhysicalControls.IsDragging && game.Sim.Controls.ConditionerHydraulic == 0,
                    "focus loss releases spring-centred controls even if MouseUp was lost");
                Input(); Resume("focus-loss control cleanup"); Stop();
                Input(new[] { Key.S }); Advance(1);
                Check(game.Sim.Vehicle.Speed < -.1f, "S reverses after a stopped equipment interaction");
                Input(new[] { Key.W }); Advance(2);
                Check(game.Sim.Vehicle.Speed > .1f, "W brakes through reverse and resumes forward"); Stop();

                // Explicit boundary fixtures isolate wall recovery; no claimed
                // manually driven route is inferred from these placements.
                Place(-11.865f, -12); Resume("85 mm board-margin stop"); Stop();
                game.Sim.SetConditionerHydraulic(-1); Advance(.3f); game.Sim.SetConditionerHydraulic(0);
                Resume("near-board conditioner interaction"); Stop();
                Place(0, 27.45f); Input(new[] { Key.W }); Advance(2);
                Check(game.Sim.BoardContact && game.Sim.DriveInhibitReason.Contains("Reverse"),
                    "direct board contact explains reverse-clear recovery");
                Input(new[] { Key.S }); Advance(1);
                Check(game.Sim.Vehicle.Speed < -.1f, "reverse clears direct board contact without a state reset");
                CaptureShakeout();
                File.WriteAllLines("Assets/Experiments/IceResurfacer/Validation~/Authenticity-results.txt", observations);
            }
            finally
            {
                game.ReleaseControls();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.Update();
                game.enabled = enabled;
            }
        }
        static void CaptureShakeout()
        {
            game.NewSession(true);
            var layout=game.Sim.Layout;layout.ToWorld(layout.PitSide,layout.PitOutward-3.85f,out float px,out float pz);
            Place(px,pz,layout.OutwardHeading+.06f); Input(new[] { Key.LeftCtrl });
            var s = game.Sim;
            typeof(Simulation).GetProperty("SnowTankM3").SetValue(s, .8f);
            observations.Add("Shakeout rendering fixture seeds 0.8 m3 tank stock and pit-aligned pose only. Subsequent transfers use production hydraulics and conserved DumpSnowRate; core tests collect their stock by cutting.");
            s.SetTankHydraulic(1); Advance(8); s.SetTankHydraulic(0);
            Check(s.ResidualSnowM3 > .019f && s.SnowTankM3 < .021f, "stationary raised bin retains its adhered residual snow");
            float before = s.SnowTankM3;
            s.SetTankHydraulic(-1); Advance(.36f); s.SetTankHydraulic(1); Advance(.16f);
            Check(s.TankShakeoutCount == 1 && s.DumpSnowRate > 0 && s.SnowTankM3 < before,
                "small measured lowering/raising movement releases residual snow through the dump outlet");
            Check(Mathf.Abs(s.SnowTankM3 + s.DumpedSnowM3 - .8f) < .00001f,
                "runtime shakeout conserves tank plus pit volume");
            foreach (var particles in game.GetComponentsInChildren<ParticleSystem>())
                if (particles.name.Contains("unloading"))
                {
                    particles.Simulate(.16f, true, false);
                    Check(particles.particleCount > 0, "residual shakeout emits visible pooled falling snow");
                }
            var cameraObject = new GameObject("Shakeout validation camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.fieldOfView = 53; camera.nearClipPlane = .05f; camera.farClipPlane = 150;
                camera.transform.position = game.World.Machine.TransformPoint(3.8f,3.2f,3.5f);
                camera.transform.LookAt(game.World.Machine.TransformPoint(0,1.1f,2.8f));
                IceResurfacerEditor.Capture(camera, "Authenticity-dump-shakeout.png", 1920, 1080);
            }
            finally { UnityEngine.Object.DestroyImmediate(cameraObject); }
            s.SetTankHydraulic(0);
        }
    }
}
