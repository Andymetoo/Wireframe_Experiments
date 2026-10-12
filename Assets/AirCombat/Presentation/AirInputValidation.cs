using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Meridian
{
    /// <summary>Opt-in runtime checks against Unity's real device state and binding resolution.</summary>
    public static class AirInputValidation
    {
        public static IEnumerator Run(Action<bool, string> check)
        {
            Gamepad pad = null;
            Mouse mouse = null;
            using (var adapter = new AirInputAdapter())
            {
                try
                {
                    pad = InputSystem.AddDevice<Gamepad>("Meridian virtual validation pad");
                    Set(pad, new GamepadState());
                    var neutral = adapter.Read(1f / 60, true, null, null, pad);
                    check(neutral.Flight.Pitch == 0 && neutral.Flight.Roll == 0 && neutral.Flight.Yaw == 0 && !neutral.Flight.Fire,
                        "Virtual gamepad: neutral stick/triggers/buttons produce neutral flight controls");
                    check(Mathf.Abs(neutral.Flight.Throttle - .78f) < .001f, "Virtual gamepad: neutral input preserves commanded throttle");

                    Set(pad, new GamepadState { leftStick = new Vector2(.65f, -.45f), rightTrigger = .6f, rightStick = new Vector2(.4f, .5f) }.WithButton(GamepadButton.RightShoulder));
                    var flight = adapter.Read(1f / 60, true, null, null, pad);
                    check(flight.Flight.Roll > .4f && flight.Flight.Pitch > .25f && flight.Flight.Yaw > .3f && flight.Flight.Fire,
                        "Virtual gamepad: simultaneous roll, pull-up, rudder and guns survive device-to-intent conversion");
                    check(flight.LookRate.x > 0 && flight.LookRate.y < 0, "Virtual gamepad: right stick looks right/up independently of flight controls");
                    float expected = AirInputAdapter.Axis(.65f, adapter.Settings.Deadzone) * adapter.Settings.Sensitivity;
                    check(Mathf.Abs(flight.Flight.Roll - expected) < .005f, "Virtual gamepad: configured deadzone is applied once to raw analog input");
                    adapter.Settings.InvertPitch = adapter.Settings.InvertRoll = adapter.Settings.InvertYaw = true;
                    var inverted = adapter.Read(1f / 60, true, null, null, pad);
                    check(Mathf.Abs(inverted.Flight.Roll + flight.Flight.Roll) < .001f && Mathf.Abs(inverted.Flight.Pitch + flight.Flight.Pitch) < .001f
                        && Mathf.Abs(inverted.Flight.Yaw + flight.Flight.Yaw) < .001f, "Virtual gamepad: independent pitch/roll/rudder inversion reverses axes");
                    adapter.Settings.InvertPitch = adapter.Settings.InvertRoll = adapter.Settings.InvertYaw = false;
                    Set(pad, new GamepadState { leftStick = new Vector2(.08f, -.08f), rightTrigger = .05f });
                    var drift = adapter.Read(1f / 60, true, null, null, pad);
                    check(drift.Flight.Roll == 0 && drift.Flight.Pitch == 0 && drift.Flight.Yaw == 0, "Virtual gamepad: resting stick drift and trigger noise stay inside the deadzone");

                    Set(pad, new GamepadState().WithButton(GamepadButton.DpadUp));
                    float before = adapter.Throttle;
                    adapter.Read(.1f, true, null, null, pad);
                    check(adapter.Throttle > before, "Virtual gamepad: D-pad up increases throttle");
                    before = adapter.Throttle;
                    adapter.Read(.1f, false, null, null, pad);
                    check(adapter.Throttle == before, "Virtual gamepad: menu navigation never changes flight throttle");
                    Set(pad, new GamepadState());
                    Set(pad, new GamepadState().WithButton(GamepadButton.North).WithButton(GamepadButton.DpadRight));
                    var order = adapter.Read(.1f, true, null, null, pad);
                    check(order.HasOrder && order.Order == SquadronOrder.Engage && adapter.Throttle == before,
                        "Virtual gamepad: Y plus D-pad issues Engage without moving throttle");
                    Set(pad, new GamepadState());
                    Set(pad, new GamepadState().WithButton(GamepadButton.West).WithButton(GamepadButton.Start));
                    var buttons = adapter.Read(.1f, true, null, null, pad);
                    check(buttons.NextTarget && buttons.Pause, "Virtual gamepad: target and pause button edges map to device-independent intentions");
                    Set(pad, new GamepadState());
                    Set(pad, new GamepadState().WithButton(GamepadButton.DpadDown).WithButton(GamepadButton.South));
                    var menu = adapter.Read(.1f, false, null, null, pad);
                    check(menu.MenuMove == 1 && menu.Submit, "Virtual gamepad: menu navigation and submit are usable without a mouse");

                    // A concrete control path tests the same extension point used by optional flight sticks.
                    adapter.Settings.StickRoll = pad.rightStick.x.path;
                    check(adapter.ApplyFlightStickBindings(), "Input System: a runtime axis control path binds successfully");
                    Set(pad, new GamepadState { rightStick = new Vector2(.7f, 0) });
                    var bound = adapter.Read(.1f, true, null, null, null);
                    check(bound.Flight.Roll > .45f, "Input System: bound axis drives the same roll intention with no Gamepad shortcut");

                    adapter.Settings.StickRoll=pad.rightStick.path;
                    adapter.ApplyFlightStickBindings();
                    Set(pad,new GamepadState{rightStick=new Vector2(.7f,.3f)});
                    var malformed=adapter.Read(.1f,true,null,null,null);
                    check(malformed.Flight.Roll==0&&adapter.DisabledBindingCount==1&&adapter.BindingError.Length>0,
                        "Input System: a Vector2 path in an axis slot is safely disabled with a readable binding error");
                    for(int i=0;i<60;i++)adapter.Read(1f/60,true,null,null,null);
                    check(adapter.DisabledBindingCount==1,"Input System: invalid axis binding is disabled once, without repeated per-frame exceptions");
                    adapter.Settings.StickRoll="";adapter.Settings.StickThrottle=pad.leftStick.path;
                    adapter.ApplyFlightStickBindings();
                    Set(pad,new GamepadState{leftStick=new Vector2(.3f,.6f)});
                    adapter.Read(.1f,true,null,null,null);
                    for(int i=0;i<5;i++)adapter.Read(.1f,true,null,null,null);
                    check(adapter.DisabledBindingCount==1&&Mathf.Abs(adapter.Throttle-before)<.001f,
                        "Input System: invalid throttle binding preserves the last throttle and is not retried");
                    adapter.Settings.StickThrottle="";adapter.Settings.StickRoll=pad.rightStick.x.path;
                    adapter.ApplyFlightStickBindings();
                    Set(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
                    InputSystem.RemoveDevice(pad);
                    var removed = adapter.Read(.1f, true, null, null, pad);
                    check(removed.Flight.Roll == 0 && removed.Flight.Pitch == 0 && removed.Flight.Yaw == 0 && !removed.Flight.Fire,
                        "Virtual gamepad: disconnection clears held controls and bound axes without stuck guns");
                    check(Mathf.Abs(removed.Flight.Throttle - before) < .001f, "Virtual gamepad: disconnection safely retains the last throttle setting");
                    check(AirInputAdapter.Axis(float.NaN, .14f) == 0 && AirInputAdapter.Axis(float.PositiveInfinity, .14f) == 0,
                        "Analog adapter rejects non-finite axis values");

                    mouse=InputSystem.AddDevice<Mouse>("Meridian virtual validation mouse");
                    adapter.Settings.MouseStick=true;
                    Vector2 centre=new Vector2(Screen.width*.5f,Screen.height*.5f);
                    Set(mouse,new MouseState{position=centre+new Vector2(180,90),delta=new Vector2(20,-10)}.WithButton(MouseButton.Right));
                    var looking=adapter.Read(1f/60,true,null,mouse,null);
                    check(looking.Flight.Roll==0&&looking.Flight.Pitch==0&&looking.LookDelta.sqrMagnitude>0,
                        "Mouse stick: holding RMB moves the eyes while withholding mouse flight-axis deflections");
                    Set(mouse,new MouseState{position=centre+new Vector2(180,90)});
                    var released=adapter.Read(1f/60,true,null,mouse,null);
                    check(released.Flight.Roll==0&&released.Flight.Pitch==0,
                        "Mouse stick: releasing freelook suppresses stale cursor deflection during recentering");
                    Set(mouse,new MouseState{position=centre});
                    var centered=adapter.Read(.1f,true,null,mouse,null);
                    check(centered.Flight.Roll==0&&centered.Flight.Pitch==0,
                        "Mouse stick: centered cursor resumes neutral flight after freelook");
                }
                finally
                {
                    if (pad != null && pad.added)
                    { Set(pad, new GamepadState()); InputSystem.RemoveDevice(pad); }
                    if(mouse!=null&&mouse.added){Set(mouse,new MouseState());InputSystem.RemoveDevice(mouse);}
                }
            }
            yield break;
        }

        static void Set(Gamepad pad, GamepadState state)
        {
            InputSystem.QueueStateEvent(pad, state);
            // Synchronous updates keep synthetic buttons out of gameplay Update. This helper is opt-in only.
            InputSystem.Update();
        }
        static void Set(Mouse mouse,MouseState state)
        {
            InputSystem.QueueStateEvent(mouse,state);
            InputSystem.Update();
        }
    }
}
