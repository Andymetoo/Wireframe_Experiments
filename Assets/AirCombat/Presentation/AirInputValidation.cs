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
            Keyboard keyboard = null;
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

                    Set(pad, new GamepadState().WithButton(GamepadButton.North));
                    Set(pad, new GamepadState().WithButton(GamepadButton.North).WithButton(GamepadButton.LeftShoulder));
                    var ability = adapter.Read(.1f, true, null, null, pad, true);
                    check(ability.Ability1 && !ability.Ability2 && !ability.LevelAssist,
                        "Roguelite gamepad: Y then LB activates the first ability without also leveling the aircraft");
                    Set(pad, new GamepadState().WithButton(GamepadButton.North).WithButton(GamepadButton.LeftShoulder));
                    check(!adapter.Read(.1f, true, null, null, pad, true).Ability1,
                        "Roguelite gamepad: holding an ability chord does not consume repeated charges");
                    Set(pad, new GamepadState().WithButton(GamepadButton.North));
                    Set(pad, new GamepadState().WithButton(GamepadButton.North).WithButton(GamepadButton.RightShoulder));
                    ability = adapter.Read(.1f, true, null, null, pad, true);
                    check(ability.Ability2 && !ability.Ability1 && !ability.Flight.Fire,
                        "Roguelite gamepad: Y then RB activates the second ability without firing guns");
                    var standardChord = adapter.Read(.1f, true, null, null, pad);
                    check(standardChord.Flight.Fire && !standardChord.Ability2,
                        "Standard gamepad: roguelite ability chords do not replace existing flight controls");
                    Set(pad, new GamepadState().WithButton(GamepadButton.RightShoulder).WithButton(GamepadButton.LeftShoulder));
                    var unmodified = adapter.Read(.1f, true, null, null, pad, true);
                    check(unmodified.Flight.Fire && unmodified.LevelAssist && !unmodified.Ability1 && !unmodified.Ability2,
                        "Roguelite gamepad: shoulders retain guns and level assist unless Y is held");

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

                    adapter.Settings.MouseStick = false;
                    bool uniform = InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
                    bool windows = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
                    float notch = !uniform && windows ? 120 : 1;
                    check(Mathf.Abs(AirInputAdapter.WheelThrottleDelta(120, false, true) - .04f) < .00001f
                        && Mathf.Abs(AirInputAdapter.WheelThrottleDelta(1, true, true) - .04f) < .00001f
                        && Mathf.Abs(AirInputAdapter.WheelThrottleDelta(1, false, false) - .04f) < .00001f,
                        "Mouse wheel: uniform and legacy platform ranges resolve to the same four-percent notch");
                    check(AirInputAdapter.WheelThrottleDelta(float.NaN, true, true) == 0
                        && AirInputAdapter.WheelThrottleDelta(float.PositiveInfinity, true, true) == 0,
                        "Mouse wheel: non-finite deltas are ignored");
                    adapter.Reset(.5f);
                    Set(mouse, new MouseState { scroll = new Vector2(0, notch) });
                    var up = adapter.Read(1f / 15, true, null, mouse, null);
                    check(Mathf.Abs(adapter.Throttle - .54f) < .0001f && up.Flight.Throttle == adapter.Throttle,
                        "Mouse wheel: upward scrolling changes the authoritative flight throttle by one precise increment");
                    Set(mouse, new MouseState());
                    adapter.Read(1f / 15, true, null, mouse, null);
                    check(Mathf.Abs(adapter.Throttle - .54f) < .0001f,
                        "Mouse wheel: an idle update does not repeat the previous scroll delta");
                    Set(mouse, new MouseState { scroll = new Vector2(0, -notch) });
                    adapter.Read(1f / 60, true, null, mouse, null);
                    check(Mathf.Abs(adapter.Throttle - .5f) < .0001f,
                        "Mouse wheel: downward scrolling decreases the same throttle state");
                    adapter.Reset(.5f);
                    for (int i = 0; i < 4; i++)
                    {
                        Set(mouse, new MouseState { scroll = new Vector2(0, notch * .25f) });
                        adapter.Read(1f / 144, true, null, mouse, null);
                    }
                    check(Mathf.Abs(adapter.Throttle - .54f) < .0001f,
                        "Mouse wheel: four high-resolution quarter-notches retain the same total precision as one notch");
                    adapter.Reset(.5f);
                    Set(mouse, new MouseState { scroll = new Vector2(0, notch) });
                    var fastWheel = adapter.Read(1f / 240, true, null, mouse, null);
                    check(Mathf.Abs(fastWheel.Flight.Throttle - up.Flight.Throttle) < .0001f,
                        "Mouse wheel: identical event deltas at 15 and 240 FPS produce identical throttle changes");
                    adapter.Reset(.98f);
                    Set(mouse, new MouseState { scroll = new Vector2(0, notch) });
                    adapter.Read(.1f, true, null, mouse, null);
                    check(adapter.Throttle == 1, "Mouse wheel: upward adjustment clamps at full throttle");
                    adapter.Reset(.02f);
                    Set(mouse, new MouseState { scroll = new Vector2(0, -notch) });
                    adapter.Read(.1f, true, null, mouse, null);
                    check(adapter.Throttle == 0, "Mouse wheel: downward adjustment clamps at idle");
                    adapter.Reset(.4f);
                    Set(mouse, new MouseState { scroll = new Vector2(0, notch * 3) });
                    adapter.Read(.1f, false, null, mouse, null);
                    check(adapter.Throttle == .4f, "Mouse wheel: scrolling menus, settings or cards leaves throttle unchanged");
                    Set(mouse, new MouseState());
                    adapter.Read(.1f, true, null, mouse, null);
                    check(adapter.Throttle == .4f, "Mouse wheel: menu scroll events are discarded, not deferred until flight resumes");
                    adapter.Reset();
                    Set(mouse, new MouseState());
                    adapter.Read(.1f, true, null, mouse, null);
                    check(Mathf.Abs(adapter.Throttle - .78f) < .0001f, "Mouse wheel: sortie reset restores the initial throttle without stale scrolling");

                    pad = InputSystem.AddDevice<Gamepad>("Meridian throttle coexistence pad");
                    adapter.Settings.StickRoll = "";
                    adapter.Settings.StickThrottle = pad.rightTrigger.path;
                    adapter.Settings.ThrottleSigned = false;
                    adapter.ApplyFlightStickBindings();
                    Set(pad, new GamepadState { rightTrigger = .4f });
                    adapter.Read(.1f, true, null, null, null);
                    check(Mathf.Abs(adapter.Throttle - .4f) < .001f, "Bound throttle: a resolved lever controls the authoritative throttle");
                    Set(mouse, new MouseState { scroll = new Vector2(0, notch) });
                    adapter.Read(.1f, true, null, mouse, null);
                    Set(mouse, new MouseState());
                    adapter.Read(.1f, true, null, mouse, null);
                    check(Mathf.Abs(adapter.Throttle - .44f) < .001f,
                        "Bound throttle: a stationary lever does not undo a mouse-wheel adjustment on the next frame");
                    Set(pad, new GamepadState { rightTrigger = .6f });
                    adapter.Read(.1f, true, null, null, null);
                    check(Mathf.Abs(adapter.Throttle - .6f) < .001f,
                        "Bound throttle: moving the lever takes command again after wheel input");

                    keyboard = InputSystem.AddDevice<Keyboard>("Meridian ability validation keyboard");
                    Set(keyboard, new KeyboardState(Key.Digit5));
                    var keyAbility = adapter.Read(.1f, true, keyboard, null, null, true);
                    check(keyAbility.Ability1 && !keyAbility.Ability2,
                        "Roguelite keyboard: 5 activates ability slot one");
                    Set(keyboard, new KeyboardState(Key.Digit5));
                    check(!adapter.Read(.1f, true, keyboard, null, null, true).Ability1,
                        "Roguelite keyboard: a held ability key does not repeat activation");
                    Set(keyboard, new KeyboardState(Key.Digit6));
                    keyAbility = adapter.Read(.1f, true, keyboard, null, null, true);
                    check(keyAbility.Ability2 && keyAbility.OpenRogue,
                        "Roguelite keyboard: 6 is the second flight slot and the separate briefing selection intention");
                    var menuAbility = adapter.Read(.1f, false, keyboard, null, null);
                    check(!menuAbility.Ability1 && !menuAbility.Ability2,
                        "Roguelite keyboard: ordinary menus and standard sorties cannot activate tactical abilities");

                    Set(keyboard, new KeyboardState(Key.T));
                    keyAbility = adapter.Read(.1f, true, keyboard, null, null, true);
                    check(keyAbility.Ability1 && !keyAbility.Ability2,
                        "Roguelite keyboard: nearby T key activates slot one without replacing 5");
                    Set(keyboard, new KeyboardState(Key.Y));
                    keyAbility = adapter.Read(.1f, true, keyboard, null, null, true);
                    check(keyAbility.Ability2 && !keyAbility.Ability1,
                        "Roguelite keyboard: nearby Y key activates slot two without replacing 6");
                    Set(keyboard, new KeyboardState(Key.T, Key.Y));
                    var standardKeys = adapter.Read(.1f, true, keyboard, null, null);
                    check(!standardKeys.Ability1 && !standardKeys.Ability2,
                        "Standard flight: T and Y cannot activate roguelite abilities");
                    Set(keyboard, new KeyboardState(Key.Digit7));
                    var newMission = adapter.Read(.1f, false, keyboard, null, null);
                    check(newMission.MissionChoice == 5 && !newMission.OpenRogue && !newMission.HasOrder,
                        "Standard briefing: 7 selects the ace encounter without issuing a flight order");
                    Set(keyboard, new KeyboardState(Key.Digit8));
                    newMission = adapter.Read(.1f, false, keyboard, null, null);
                    check(newMission.MissionChoice == 6 && !newMission.OpenRogue && !newMission.HasOrder,
                        "Standard briefing: 8 selects the larger engagement without replacing the Afterburn shortcut");
                    Set(keyboard, new KeyboardState(Key.Digit6));
                    newMission = adapter.Read(.1f, false, keyboard, null, null);
                    check(newMission.OpenRogue && newMission.MissionChoice == -1,
                        "Standard briefing: 6 still opens Afterburn rather than selecting a standard mission");
                    Set(keyboard, new KeyboardState(Key.V));
                    bool keyboardCenter = adapter.Read(.1f, true, keyboard, null, null).CenterLook;
                    Set(mouse, new MouseState { position = centre }.WithButton(MouseButton.Middle));
                    bool mouseCenter = adapter.Read(.1f, true, null, mouse, null).CenterLook;
                    Set(pad, new GamepadState().WithButton(GamepadButton.RightStick));
                    bool padCenter = adapter.Read(.1f, true, null, null, pad).CenterLook;
                    check(keyboardCenter && mouseCenter && padCenter,
                        "Center view: V, middle mouse and right-stick click retain their common explicit center intention");

                    adapter.Settings.MouseStick = true;
                    Set(keyboard, new KeyboardState(Key.S));
                    Set(mouse, new MouseState { position = centre + new Vector2(200, 120), delta = new Vector2(30, -25) }
                        .WithButton(MouseButton.Right).WithButton(MouseButton.Left));
                    var freeLook = adapter.Read(.1f, true, keyboard, mouse, null);
                    check(freeLook.FreeLookHeld && !freeLook.RearViewHeld && freeLook.LookDelta.x > 0 && freeLook.LookDelta.y > 0,
                        "Free-look: RMB delivers independent left/right and vertical head motion");
                    check(!freeLook.Flight.Fire && freeLook.Flight.Roll == 0 && Mathf.Abs(freeLook.Flight.Pitch - adapter.Settings.Sensitivity) < .001f,
                        "Free-look: mouse guns and mouse steering are withheld while keyboard flight controls remain usable");
                    Set(keyboard, new KeyboardState(Key.Space));
                    freeLook = adapter.Read(.1f, true, keyboard, mouse, null);
                    check(freeLook.Flight.Fire,
                        "Free-look: deliberate keyboard gunfire remains available while mouse gunfire is inhibited");
                    Set(keyboard, new KeyboardState());
                    Set(mouse, new MouseState { position = centre + new Vector2(200, 120) }.WithButton(MouseButton.Left));
                    var lookRelease = adapter.Read(1f / 60, true, keyboard, mouse, null);
                    check(lookRelease.LookReleased && !lookRelease.FreeLookHeld && lookRelease.LookDelta == Vector2.zero
                        && lookRelease.Flight.Roll == 0 && lookRelease.Flight.Pitch == 0,
                        "Free-look: RMB release requests forward return and safely recenters normal mouse steering");
                    check(!lookRelease.Flight.Fire,
                        "Free-look: holding LMB through RMB release cannot accidentally open fire");
                    check(!adapter.Read(1f / 60, true, keyboard, mouse, null).LookReleased,
                        "Free-look: return-to-forward is a single release edge");
                    Set(mouse, new MouseState { position = centre });
                    adapter.Read(.1f, true, keyboard, mouse, null);
                    Set(mouse, new MouseState { position = centre + new Vector2(150, 0) }.WithButton(MouseButton.Left));
                    var resumedMouse = adapter.Read(.1f, true, keyboard, mouse, null);
                    check(resumedMouse.Flight.Fire && resumedMouse.Flight.Roll > 0,
                        "Free-look: a fresh click and deliberate cursor displacement restore normal mouse gunfire and steering");

                    Set(keyboard, new KeyboardState(Key.X));
                    Set(mouse, new MouseState { position = centre + new Vector2(150, 90), delta = new Vector2(-500, 200) }
                        .WithButton(MouseButton.Right).WithButton(MouseButton.Left));
                    var rear = adapter.Read(.1f, true, keyboard, mouse, null);
                    check(rear.RearViewHeld && rear.FreeLookHeld && rear.LookDelta == Vector2.zero
                        && rear.Flight.Roll == 0 && rear.Flight.Pitch == 0 && !rear.Flight.Fire,
                        "Rear view: hold X takes precedence over RMB motion, mouse steering and mouse firing");
                    var pausedLook = adapter.Read(.1f, false, keyboard, mouse, null);
                    check(!pausedLook.RearViewHeld && !pausedLook.FreeLookHeld && pausedLook.LookReleased
                        && pausedLook.LookDelta == Vector2.zero && pausedLook.LookRate == Vector2.zero,
                        "Menus: look holds are cleared and release is reported when flight input becomes inactive");
                    var diagnosticLook = adapter.Read(.1f, false, keyboard, mouse, null, false, true);
                    check(diagnosticLook.RearViewHeld && diagnosticLook.FreeLookHeld,
                        "Diagnostics: explicit view activity supports camera checks without enabling wheel throttle");
                    Set(keyboard, new KeyboardState());
                    Set(mouse, new MouseState { position = centre });
                    adapter.Read(.1f, true, keyboard, mouse, null);
                    adapter.Reset();
                    var resetView = adapter.Read(.1f, true, keyboard, mouse, null);
                    check(!resetView.FreeLookHeld && !resetView.RearViewHeld && !resetView.LookReleased,
                        "View controls: sortie restart does not leave a stuck look hold or release edge");
                    ValidateViewState(check);
                }
                finally
                {
                    if (pad != null && pad.added)
                    { Set(pad, new GamepadState()); InputSystem.RemoveDevice(pad); }
                    if(mouse!=null&&mouse.added){Set(mouse,new MouseState());InputSystem.RemoveDevice(mouse);}
                    if(keyboard!=null&&keyboard.added){Set(keyboard,new KeyboardState());InputSystem.RemoveDevice(keyboard);}
                }
            }
            yield break;
        }

        static void ValidateViewState(Action<bool, string> check)
        {
            var view = new AirViewControls();
            view.Step(new AirControlIntent { FreeLookHeld = true, LookDelta = new Vector2(500, -500) }, 1f / 60);
            check(view.Yaw == 165 && view.Pitch == -80,
                "Head motion: right/up limits expose the rear hemisphere without unrestricted rotation");
            view.Step(new AirControlIntent { FreeLookHeld = true, LookDelta = new Vector2(-1000, 1000) }, 1f / 60);
            check(view.Yaw == -165 && view.Pitch == 70,
                "Head motion: left/down limits remain bounded like a pilot turning their head");
            view.Step(new AirControlIntent { LookReleased = true }, 1f / 60);
            check(view.Returning && view.Yaw > -165 && view.Yaw < 0,
                "Head motion: releasing RMB smoothly starts the forward return");
            for (int i = 0; i < 25; i++) view.Step(default(AirControlIntent), 1f / 60);
            check(view.Yaw == 0 && view.Pitch == 0 && !view.Returning,
                "Head motion: release reliably settles exactly forward without residual drift");
            var rear = new AirControlIntent { RearViewHeld = true, FreeLookHeld = true, LookDelta = new Vector2(-900, 900) };
            view.Step(rear, .1f); view.Step(rear, .1f);
            check(view.Yaw == 165 && view.Pitch == -5,
                "Head motion: quick-six reaches its rear shoulder view within 0.2 seconds and overrides free-look");
            view.Step(new AirControlIntent { FreeLookHeld = true, LookDelta = new Vector2(-900, 900) }, 1f / 60);
            check(view.Returning && view.Yaw > 0 && view.Yaw < 165,
                "Head motion: releasing X returns forward even if RMB remains held, without applying stale rear-view deltas");
            for (int i = 0; i < 25; i++) view.Step(new AirControlIntent { FreeLookHeld = true }, 1f / 60);
            view.Step(new AirControlIntent { FreeLookHeld = true, LookDelta = new Vector2(-35, 12) }, 1f / 60);
            check(view.Yaw == -35 && view.Pitch == 12,
                "Head motion: RMB resumes normal bounded free-look after the quick-six return");
            view.Step(rear, .1f); view.Step(rear, .1f);
            check(view.Yaw == -165,
                "Head motion: quick-six uses the shoulder the pilot was already looking over");
            view.Step(rear, .1f, false);
            check(view.Yaw == 0 && view.Pitch == 0 && !view.Returning,
                "Head motion: pause or menu exit clears camera offsets and transition state");
            view.Step(new AirControlIntent { FreeLookHeld = true, LookDelta = new Vector2(100, 40) }, 1f / 60);
            view.Step(new AirControlIntent { CenterLook = true, FreeLookHeld = true, LookDelta = new Vector2(900, 900) }, .001f);
            check(view.Yaw == 0 && view.Pitch == 0 && !view.Returning,
                "Head motion: explicit center is immediate and ignores concurrent look deltas instead of easing");

            var slow = new AirViewControls(); var fast = new AirViewControls();
            slow.Step(rear, .1f); slow.Step(rear, .1f);
            fast.Step(rear, .1f); fast.Step(rear, .1f);
            for (int i = 0; i < 4; i++) slow.Step(default(AirControlIntent), 1f / 15);
            for (int i = 0; i < 64; i++) fast.Step(default(AirControlIntent), 1f / 240);
            check(Mathf.Abs(slow.Yaw - fast.Yaw) < .002f && slow.Yaw < 1,
                "Head motion: forward return is consistent at 15 and 240 FPS and nearly centered after a quarter-second");
            fast.Reset();
            check(fast.Yaw == 0 && fast.Pitch == 0 && !fast.Returning,
                "Head motion: a fresh sortie has no inherited rear-view state");
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
        static void Set(Keyboard keyboard, KeyboardState state)
        {
            InputSystem.QueueStateEvent(keyboard, state);
            InputSystem.Update();
        }
    }
}
