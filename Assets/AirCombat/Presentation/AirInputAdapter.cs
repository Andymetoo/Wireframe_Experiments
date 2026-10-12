using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Meridian
{
    /// <summary>Physical devices end here. The mission only receives FlightInput and discrete orders.</summary>
    public struct AirControlIntent
    {
        public FlightInput Flight;
        public Vector2 LookRate, LookDelta;
        public bool LevelAssist, CenterLook, Pause, Back, Submit, QuickLaunch, Notes, Camera, Lead, MouseMode, NextTarget;
        public int MenuMove, MissionChoice;
        public bool HasOrder;
        public SquadronOrder Order;
    }

    [Serializable]
    public sealed class AirInputSettings
    {
        public float Sensitivity = .8f, Deadzone = .14f, LookSensitivity = 75f;
        public bool MouseStick, InvertMouse, InvertPitch, InvertRoll, InvertYaw, InvertLook;
        // Input System control paths, e.g. <Joystick>/stick/x. Empty means unbound.
        public string StickRoll = "", StickPitch = "", StickYaw = "", StickThrottle = "", StickFire = "";
        public bool ThrottleSigned = true;
    }

    public sealed class AirInputAdapter : IDisposable
    {
        public readonly AirInputSettings Settings = new AirInputSettings();
        public float Throttle { get; private set; } = .78f;
        public string ActiveDevice { get; private set; } = "KEYBOARD / MOUSE";
        public string BindingError { get; private set; } = "";
        public int DisabledBindingCount { get; private set; }
        InputAction rollAxis, pitchAxis, yawAxis, throttleAxis, fireButton;
        bool disposed, mouseLookHeld;
        float mouseRecenterTime;

        public void Reset(float throttle = .78f) { Throttle = Mathf.Clamp01(throttle);mouseLookHeld=false;mouseRecenterTime=0; }
        static float Held(bool value) { return value ? 1f : 0f; }
        public static float Axis(float value, float deadzone)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0;
            deadzone = Mathf.Clamp(deadzone, 0, .45f);
            return Mathf.Sign(value) * Mathf.Clamp01((Mathf.Abs(value) - deadzone) / (1f - deadzone));
        }
        static float MouseAxis(float value) { return Axis(value, .08f); }

        public AirControlIntent Read(float dt, bool flying)
        { return Read(dt, flying, Keyboard.current, Mouse.current, Gamepad.current); }

        // Explicit device overload lets runtime diagnostics use temporary devices without disabling hardware.
        public AirControlIntent Read(float dt, bool flying, Keyboard keyboard, Mouse mouse, Gamepad pad)
        {
            var r = new AirControlIntent { MissionChoice = -1 };
            var k = keyboard; var m = mouse;
            mouseRecenterTime=Mathf.Max(0,mouseRecenterTime-Mathf.Max(0,dt));
            float roll = 0, pitch = 0, yaw = 0, throttleRate = 0;
            if (k != null && k.added && k.enabled)
            {
                roll = Held(k.dKey.isPressed || k.rightArrowKey.isPressed) - Held(k.aKey.isPressed || k.leftArrowKey.isPressed);
                pitch = Held(k.sKey.isPressed || k.downArrowKey.isPressed) - Held(k.wKey.isPressed || k.upArrowKey.isPressed);
                yaw = (Held(k.eKey.isPressed) - Held(k.qKey.isPressed)) * .65f;
                throttleRate = Held(k.leftShiftKey.isPressed || k.rightShiftKey.isPressed || k.rKey.isPressed)
                    - Held(k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed || k.fKey.isPressed);
                r.Flight.Fire = k.spaceKey.isPressed; r.LevelAssist = k.zKey.isPressed;
                r.Pause = k.escapeKey.wasPressedThisFrame; r.Back = r.Pause;
                r.Submit = k.enterKey.wasPressedThisFrame; r.Notes = k.hKey.wasPressedThisFrame;
                r.QuickLaunch = r.Submit;
                r.Camera = k.cKey.wasPressedThisFrame; r.Lead = k.gKey.wasPressedThisFrame;
                r.NextTarget = k.tabKey.wasPressedThisFrame; r.MouseMode = k.mKey.wasPressedThisFrame;
                r.CenterLook = k.vKey.wasPressedThisFrame;
                r.MenuMove = (k.downArrowKey.wasPressedThisFrame ? 1 : 0) - (k.upArrowKey.wasPressedThisFrame ? 1 : 0);
                for (int i = 0; i < 5; i++) if (k[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                {
                    r.MissionChoice = i;
                    if (i < 4) { r.HasOrder = true; r.Order = (SquadronOrder)i; }
                }
                if (roll != 0 || pitch != 0 || yaw != 0 || r.Flight.Fire || throttleRate != 0) ActiveDevice = "KEYBOARD / MOUSE";
            }
            roll *= Settings.Sensitivity; pitch *= Settings.Sensitivity;
            if (m != null && m.added && m.enabled)
            {
                r.Flight.Fire |= m.leftButton.isPressed;
                r.CenterLook |= m.middleButton.wasPressedThisFrame;
                bool looking=m.rightButton.isPressed;
                bool releasedLook=mouseLookHeld&&!looking;
                mouseLookHeld=looking;
                if(Settings.MouseStick&&releasedLook&&flying)
                {
                    m.WarpCursorPosition(new Vector2(Screen.width*.5f,Screen.height*.5f));
                    // Cursor-warp events may reach the device on the following update.
                    mouseRecenterTime=.05f;
                }
                if (Settings.MouseStick&&!looking&&!releasedLook&&mouseRecenterTime<=0)
                {
                    Vector2 offset = m.position.ReadValue() - new Vector2(Screen.width * .5f, Screen.height * .5f);
                    roll += MouseAxis(offset.x / Mathf.Max(1, Screen.height * .24f)) * .85f * Settings.Sensitivity;
                    pitch += MouseAxis(offset.y / Mathf.Max(1, Screen.height * .24f)) * (Settings.InvertMouse ? -1 : 1) * .65f * Settings.Sensitivity;
                }
                if (looking) r.LookDelta = Vector2.Scale(m.delta.ReadValue(), new Vector2(.12f, Settings.InvertLook ? .12f : -.12f));
            }
            if (pad != null && pad.added && pad.enabled)
            {
                Vector2 stick = pad.leftStick.ReadUnprocessedValue();
                float px = Axis(stick.x, Settings.Deadzone), py = Axis(stick.y, Settings.Deadzone);
                float rudder = Axis(pad.rightTrigger.ReadUnprocessedValue() - pad.leftTrigger.ReadUnprocessedValue(), Settings.Deadzone);
                roll += px * Settings.Sensitivity * (Settings.InvertRoll ? -1 : 1);
                pitch += -py * Settings.Sensitivity * (Settings.InvertPitch ? -1 : 1);
                yaw += rudder * .65f * (Settings.InvertYaw ? -1 : 1);
                Vector2 look = pad.rightStick.ReadUnprocessedValue();
                r.LookRate = new Vector2(Axis(look.x, Settings.Deadzone), Axis(look.y, Settings.Deadzone) * (Settings.InvertLook ? 1 : -1)) * Settings.LookSensitivity;
                r.CenterLook |= pad.rightStickButton.wasPressedThisFrame;
                r.Flight.Fire |= pad.rightShoulder.isPressed;
                r.LevelAssist |= pad.leftShoulder.isPressed;
                bool orders = pad.buttonNorth.isPressed;
                if (!orders) throttleRate += Held(pad.dpad.up.isPressed) - Held(pad.dpad.down.isPressed);
                else if (flying)
                {
                    if (pad.dpad.up.wasPressedThisFrame) { r.HasOrder = true; r.Order = SquadronOrder.FormUp; }
                    if (pad.dpad.right.wasPressedThisFrame) { r.HasOrder = true; r.Order = SquadronOrder.Engage; }
                    if (pad.dpad.left.wasPressedThisFrame) { r.HasOrder = true; r.Order = SquadronOrder.CoverMe; }
                    if (pad.dpad.down.wasPressedThisFrame) { r.HasOrder = true; r.Order = SquadronOrder.Regroup; }
                }
                r.NextTarget |= pad.buttonWest.wasPressedThisFrame;
                r.Camera |= pad.buttonEast.wasPressedThisFrame;
                r.Lead |= pad.buttonSouth.wasPressedThisFrame;
                r.Pause |= pad.startButton.wasPressedThisFrame;
                r.Back |= pad.buttonEast.wasPressedThisFrame;
                r.Submit |= pad.buttonSouth.wasPressedThisFrame;
                r.MenuMove += (pad.dpad.down.wasPressedThisFrame ? 1 : 0) - (pad.dpad.up.wasPressedThisFrame ? 1 : 0);
                if (Mathf.Abs(px) + Mathf.Abs(py) + Mathf.Abs(rudder) + r.LookRate.sqrMagnitude > .02f || pad.rightShoulder.isPressed || r.Pause || r.Submit || r.MenuMove != 0)
                    ActiveDevice = "GAMEPAD";
            }
            if (!disposed)
            {
                float rx = ReadBound(rollAxis), py = ReadBound(pitchAxis), rz = ReadBound(yawAxis);
                roll += Axis(rx, Settings.Deadzone) * Settings.Sensitivity * (Settings.InvertRoll ? -1 : 1);
                pitch += -Axis(py, Settings.Deadzone) * Settings.Sensitivity * (Settings.InvertPitch ? -1 : 1);
                yaw += Axis(rz, Settings.Deadzone) * .65f * (Settings.InvertYaw ? -1 : 1);
                if (fireButton != null) r.Flight.Fire |= fireButton.IsPressed();
                if (Mathf.Abs(rx) + Mathf.Abs(py) + Mathf.Abs(rz) > .08f || (fireButton != null && fireButton.IsPressed())) ActiveDevice = "BOUND FLIGHT STICK";
                // Only a resolved, connected control may override throttle. Unplugging preserves the last setting.
                if (flying && throttleAxis != null && throttleAxis.enabled && throttleAxis.controls.Count > 0 && throttleAxis.controls[0].device.added)
                {
                    float value = ReadBound(throttleAxis);
                    if(throttleAxis.enabled)Throttle = Mathf.Clamp01(Settings.ThrottleSigned ? (value + 1f) * .5f : value);
                }
            }
            if (flying) Throttle = Mathf.Clamp01(Throttle + Mathf.Clamp(dt, 0, .1f) * .32f * Mathf.Clamp(throttleRate, -1, 1));
            r.Flight.Roll = Mathf.Clamp(roll, -1, 1); r.Flight.Pitch = Mathf.Clamp(pitch, -1, 1);
            r.Flight.Yaw = Mathf.Clamp(yaw, -1, 1); r.Flight.Throttle = Throttle;
            return r;
        }

        float ReadBound(InputAction action)
        {
            if(action==null||!action.enabled)return 0;
            try{float value=action.ReadValue<float>();return float.IsNaN(value)||float.IsInfinity(value)?0:value;}
            catch(InvalidOperationException)
            {
                BindingError="Bind a single axis (for example /stick/x), not a complete stick.";
                action.Disable();DisabledBindingCount++;return 0;
            }
        }
        InputAction Bind(string name, string path, bool button = false)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var action = new InputAction(name, button ? InputActionType.Button : InputActionType.Value, path, expectedControlType: button ? "Button" : "Axis");
            try { action.Enable(); return action; }
            catch { action.Dispose(); throw; }
        }
        public bool ApplyFlightStickBindings()
        {
            ReleaseBindings(); BindingError = ""; DisabledBindingCount=0; disposed = false;
            try
            {
                rollAxis = Bind("Roll", Settings.StickRoll); pitchAxis = Bind("Pitch", Settings.StickPitch);
                yawAxis = Bind("Yaw", Settings.StickYaw); throttleAxis = Bind("Throttle", Settings.StickThrottle);
                fireButton = Bind("Guns", Settings.StickFire, true); return true;
            }
            catch (Exception ex) { ReleaseBindings(); BindingError = "Binding rejected: " + ex.Message; return false; }
        }
        void ReleaseBindings()
        {
            rollAxis?.Dispose(); pitchAxis?.Dispose(); yawAxis?.Dispose(); throttleAxis?.Dispose(); fireButton?.Dispose();
            rollAxis = pitchAxis = yawAxis = throttleAxis = fireButton = null;
        }
        public void Dispose() { ReleaseBindings(); disposed = true; }
    }
}
