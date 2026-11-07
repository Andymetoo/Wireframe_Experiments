using IceResurfacer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    public enum IceConsoleControlKind
    {
        OpenConsole, CloseConsole, VerticalAuger, Conditioner, HorizontalAuger, TankLift,
        FloodValve, WashValve, Blade, BoardBrush, WashPump, SnowBreaker, Service, Finish,
        NewSession, BladeStatus, ConfirmAction, CancelAction
    }

    /// <summary>
    /// Reusable, explicitly bound console control. Stores only pointer ownership;
    /// commands and readings are always taken from the existing simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IceConsoleControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public IceMachineryConsole owner;
        public IceConsoleControlKind kind;
        public RectTransform interactionArea;
        public TMP_Text value;
        public TMP_Text detail;
        public Image indicator;
        public Button action;
        public Toggle maintainedToggle;
        public Slider slider;
        [Tooltip("Optional standard Selectable for hover, press and disabled styling of a custom lever or dial.")]
        public Selectable selectable;
        [Header("Optional live mechanism artwork (panel layout is never changed)")]
        public bool animateGraphic = true;
        public RectTransform movingGraphic;
        public Vector2 negativeOffset = new Vector2(0, -24);
        public Vector2 positiveOffset = new Vector2(0, 24);
        public float closedAngle;
        public float openAngle = 90;
        [Range(0, .45f)] public float neutralHalfHeight = 12f / 94f;
        [Range(0, .45f)] public float rotaryDeadRadius = .15f;
        [Tooltip("Legacy IMGUI wheel units per conventional mouse notch. Three preserves the old console's scroll steps.")]
        public float scrollUnitsPerTick = 3;
        [Header("Optional blade schematic artwork")]
        public RectTransform schematicBody;
        public RectTransform schematicBlade;
        public RectTransform schematicSetMarker;
        public Vector2 schematicLiftOffset = new Vector2(0, 12);
        public Vector2 schematicContactOffset = new Vector2(0, -12);
        public Vector2 schematicOffIceOffset = new Vector2(0, 9);
        public Vector2 schematicRetractedOffset = new Vector2(0, 9);
        public Vector2 schematicSetOffset = new Vector2(0, -12);

        IceMachineryConsole boundOwner;
        Simulation gestureSimulation;
        Mouse gestureMouse;
        Mouse actionMouse;
        int gesturePointer;
        bool pointerHeld, gestureCanceled, hydraulicOwned, capturedArt, reportedInvalid;
        bool actionPressActive, actionPressCanceled;
        int canceledActionReleaseFrame = -1;
        float lastAngle, lastHydraulic;
        Camera gestureCamera;
        Vector3 artPosition, bodyPosition, bladePosition, markerPosition;
        Quaternion artRotation;

        public bool IsHolding => pointerHeld;
        public bool IsLocallyInteractable =>
            (selectable == null || selectable.isActiveAndEnabled && selectable.IsInteractable()) &&
            (action == null || action.isActiveAndEnabled && action.IsInteractable()) &&
            (maintainedToggle == null || maintainedToggle.isActiveAndEnabled && maintainedToggle.IsInteractable()) &&
            (slider == null || slider.isActiveAndEnabled && slider.IsInteractable());
        bool Hydraulic => kind == IceConsoleControlKind.Conditioner || kind == IceConsoleControlKind.TankLift;
        bool Rotary => kind == IceConsoleControlKind.FloodValve || kind == IceConsoleControlKind.WashValve || kind == IceConsoleControlKind.Blade;
        bool Maintained => kind == IceConsoleControlKind.VerticalAuger || kind == IceConsoleControlKind.HorizontalAuger ||
            kind == IceConsoleControlKind.BoardBrush || kind == IceConsoleControlKind.WashPump;

        public bool ValidateBinding(out string error)
        {
            if (owner == null) { error = "Assign the console Owner on " + name + "."; return false; }
            if ((Hydraulic || Rotary) && interactionArea == null)
            { error = "Assign the authored Interaction Area on " + name + "."; return false; }
            if ((Hydraulic || Rotary || kind == IceConsoleControlKind.BladeStatus) && value == null)
            { error = "Assign live Value text on " + name + "."; return false; }
            if (Maintained && value == null && maintainedToggle == null && (indicator == null || indicator.type != Image.Type.Filled))
            { error = "Assign state text, a Toggle, or a Filled state indicator on " + name + "."; return false; }
            if (kind == IceConsoleControlKind.BladeStatus && detail == null)
            { error = "Assign live Detail text on " + name + "."; return false; }
            if (kind != IceConsoleControlKind.BladeStatus && !Hydraulic && !Rotary &&
                action == null && (!Maintained || maintainedToggle == null))
            { error = "Assign the Button or maintained Toggle on " + name + "."; return false; }
            if (action != null && maintainedToggle != null)
            { error = "Use one Button or one Toggle per control, not both, on " + name + "."; return false; }
            if (slider != null && !Rotary)
            { error = "Slider bindings are supported for flood, wash and blade only on " + name + "."; return false; }
            error = null;
            return true;
        }

        internal void Bind(IceMachineryConsole console)
        {
            if (boundOwner != null) Unbind(boundOwner);
            boundOwner = console;
            if (action != null) action.onClick.AddListener(Click);
            if (maintainedToggle != null) maintainedToggle.onValueChanged.AddListener(ToggleChanged);
            if (slider != null) slider.onValueChanged.AddListener(SliderChanged);
            CaptureArtwork();
        }
        internal void Unbind(IceMachineryConsole console)
        {
            if (boundOwner != console) return;
            CancelInteraction();
            if (action != null) action.onClick.RemoveListener(Click);
            if (maintainedToggle != null) maintainedToggle.onValueChanged.RemoveListener(ToggleChanged);
            if (slider != null) slider.onValueChanged.RemoveListener(SliderChanged);
            boundOwner = null;
        }
        void CaptureArtwork()
        {
            if (capturedArt || !Application.IsPlaying(gameObject)) return;
            if (movingGraphic != null) { artPosition = movingGraphic.localPosition; artRotation = movingGraphic.localRotation; }
            if (schematicBody != null) bodyPosition = schematicBody.localPosition;
            if (schematicBlade != null) bladePosition = schematicBlade.localPosition;
            if (schematicSetMarker != null) markerPosition = schematicSetMarker.localPosition;
            capturedArt = true;
        }
        bool MayDispatchAction => canceledActionReleaseFrame != Time.frameCount &&
            !(actionPressActive && actionPressCanceled && actionMouse != null && actionMouse.leftButton.isPressed);
        void Click() { if (MayDispatchAction && owner != null) owner.Activate(this); }
        void ToggleChanged(bool enabledState)
        {
            if (MayDispatchAction && owner != null && owner.CanUse(this)) owner.Activate(this, enabledState);
            else if (owner != null && owner.game != null) Refresh(owner.game.Sim);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (!Hydraulic && !Rotary)
            {
                // CanvasGroup visibility does not cancel the UI module's stored Button press.
                // Remember cancellation until its release callback, so reopening a panel cannot fire an old click.
                actionPressActive = action != null || maintainedToggle != null;
                actionPressCanceled = !ReadyForGesture();
                canceledActionReleaseFrame = -1;
                actionMouse = Mouse.current;
                gesturePointer = eventData.pointerId;
                return;
            }
            if (!ReadyForGesture() || !Inside(eventData.position, eventData.pressEventCamera)) return;
            CancelInteraction();
            gestureCanceled = false;
            pointerHeld = true;
            gesturePointer = eventData.pointerId;
            gestureMouse = Mouse.current;
            gestureSimulation = owner.game.Sim;
            gestureCamera = eventData.pressEventCamera;
            lastAngle = Angle(eventData.position, gestureCamera, out _);
            if (Hydraulic) WriteHydraulic(HydraulicDemand(eventData.position, gestureCamera));
            else if (slider != null) SliderChanged(slider.value);
            eventData.Use();
        }
        public void OnInitializePotentialDrag(PointerEventData eventData) { if (Hydraulic || Rotary) eventData.useDragThreshold = false; }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (actionPressActive && gesturePointer == eventData.pointerId) actionPressCanceled = true;
            OnDrag(eventData);
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (!pointerHeld || gesturePointer != eventData.pointerId) return;
            if (!ReadyForGesture() || gestureSimulation != owner.game.Sim || !Inside(eventData.position, gestureCamera))
            { CancelInteraction(); return; }
            if (Hydraulic) WriteHydraulic(HydraulicDemand(eventData.position, gestureCamera));
            else if (slider == null)
            {
                float angle = Angle(eventData.position, gestureCamera, out bool beyondDeadZone);
                if (beyondDeadZone)
                {
                    float turn = Mathf.DeltaAngle(lastAngle, angle);
                    // Local UI Y points up: CCW opens valves; CW deepens the blade screw.
                    if (kind == IceConsoleControlKind.Blade)
                        ChangeScalar(-turn / Mathf.Max(.01f, gestureSimulation.Config.BladeWheelTurns * 360));
                    else ChangeScalar(turn / 90);
                }
                lastAngle = angle;
            }
            eventData.Use();
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (actionPressActive && gesturePointer == eventData.pointerId && eventData.button == PointerEventData.InputButton.Left)
            {
                // InputSystemUIInputModule invokes pointer-up immediately before Button/Toggle click.
                if (actionPressCanceled) canceledActionReleaseFrame = Time.frameCount;
                actionPressActive = false;
                actionPressCanceled = false;
                actionMouse = null;
            }
            if (pointerHeld && gesturePointer == eventData.pointerId) CancelInteraction();
            if (eventData.button == PointerEventData.InputButton.Left) gestureCanceled = false;
        }
        public void OnEndDrag(PointerEventData eventData) { if (gesturePointer == eventData.pointerId) CancelInteraction(); }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (actionPressActive && gesturePointer == eventData.pointerId) actionPressCanceled = true;
            if (pointerHeld && gesturePointer == eventData.pointerId) CancelInteraction();
        }
        public void OnScroll(PointerEventData eventData)
        {
            if (!Rotary || !ReadyForGesture() || !Inside(eventData.position, eventData.enterEventCamera)) return;
            var module = EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
            float perTick = module != null ? module.scrollDeltaPerTick : 1;
            if (Mathf.Abs(perTick) < .0001f) return;
            float legacyUnits = eventData.scrollDelta.y / Mathf.Abs(perTick) * scrollUnitsPerTick;
            var sim = owner.game.Sim;
            if (kind == IceConsoleControlKind.Blade) ChangeScalar(legacyUnits * 7.5f / Mathf.Max(.01f, sim.Config.BladeWheelTurns * 360));
            else ChangeScalar(legacyUnits * .025f);
            eventData.Use();
        }
        bool ReadyForGesture() => Application.IsPlaying(gameObject) && owner != null && owner.CanUse(this) &&
            !KeyboardOwnsCommand() && (owner.game.PhysicalControls == null || !owner.game.PhysicalControls.IsDragging);
        bool Inside(Vector2 point, Camera camera) => interactionArea != null &&
            RectTransformUtility.RectangleContainsScreenPoint(interactionArea, point, camera);
        float HydraulicDemand(Vector2 point, Camera camera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(interactionArea, point, camera, out Vector2 local);
            float delta = local.y - interactionArea.rect.center.y;
            float dead = interactionArea.rect.height * neutralHalfHeight;
            return delta > dead ? 1 : delta < -dead ? -1 : 0;
        }
        float Angle(Vector2 point, Camera camera, out bool beyondDeadZone)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(interactionArea, point, camera, out Vector2 local);
            Vector2 vector = local - interactionArea.rect.center;
            beyondDeadZone = vector.magnitude > Mathf.Min(interactionArea.rect.width, interactionArea.rect.height) * rotaryDeadRadius;
            return Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
        }

        internal void PollInteraction()
        {
            if (!pointerHeld) return;
            if (!ReadyForGesture() || gestureSimulation != owner.game.Sim || gestureMouse == null ||
                !gestureMouse.added || gestureMouse != Mouse.current || !gestureMouse.leftButton.isPressed ||
                !Inside(gestureMouse.position.ReadValue(), gestureCamera))
            { CancelInteraction(); return; }
            // A different writer wins. Never reassert stale UI demand over another input owner.
            if (hydraulicOwned && !Mathf.Approximately(HydraulicValue(gestureSimulation), lastHydraulic))
                CancelInteraction();
        }
        public void CancelInteraction()
        {
            if (actionPressActive) actionPressCanceled = true;
            if (hydraulicOwned && gestureSimulation != null)
            {
                bool physicalOwns = owner != null && owner.game != null && owner.game.PhysicalControls != null &&
                    owner.game.PhysicalControls.IsDragging;
                if (!KeyboardOwnsCommand() && !physicalOwns && Mathf.Approximately(HydraulicValue(gestureSimulation), lastHydraulic))
                {
                    if (kind == IceConsoleControlKind.Conditioner) gestureSimulation.SetConditionerHydraulic(0);
                    else gestureSimulation.SetTankHydraulic(0);
                }
            }
            hydraulicOwned = false;
            pointerHeld = false;
            gestureCanceled = true;
            gestureSimulation = null;
            gestureMouse = null;
        }
        float HydraulicValue(Simulation sim) => kind == IceConsoleControlKind.Conditioner ?
            sim.Controls.ConditionerHydraulic : sim.Controls.TankHydraulic;
        void WriteHydraulic(float demand)
        {
            if (gestureSimulation == null) return;
            if (kind == IceConsoleControlKind.Conditioner) gestureSimulation.SetConditionerHydraulic(demand);
            else gestureSimulation.SetTankHydraulic(demand);
            lastHydraulic = demand;
            hydraulicOwned = true;
            Refresh(gestureSimulation);
        }
        bool KeyboardOwnsCommand()
        {
            var keys = Keyboard.current;
            if (keys == null) return false;
            switch (kind)
            {
                case IceConsoleControlKind.Conditioner: return keys.digit1Key.isPressed;
                case IceConsoleControlKind.TankLift: return keys.digit7Key.isPressed;
                case IceConsoleControlKind.FloodValve: return !keys.rightShiftKey.isPressed && (keys.leftBracketKey.isPressed || keys.rightBracketKey.isPressed);
                case IceConsoleControlKind.WashValve: return keys.rightShiftKey.isPressed && (keys.leftBracketKey.isPressed || keys.rightBracketKey.isPressed);
                case IceConsoleControlKind.Blade: return keys.commaKey.isPressed || keys.periodKey.isPressed;
                default: return false;
            }
        }
        void ChangeScalar(float fractionDelta)
        {
            if (!ReadyForGesture()) return;
            var sim = owner.game.Sim;
            switch (kind)
            {
                case IceConsoleControlKind.FloodValve: sim.Controls.FloodFlow = Mathf.Clamp01(sim.Controls.FloodFlow + fractionDelta); break;
                case IceConsoleControlKind.WashValve: sim.Controls.WashValve = Mathf.Clamp01(sim.Controls.WashValve + fractionDelta); break;
                case IceConsoleControlKind.Blade:
                    sim.Controls.BladeDepthMm = Mathf.Clamp(sim.Controls.BladeDepthMm + fractionDelta * sim.Config.MaxBladeDepthMm, 0, sim.Config.MaxBladeDepthMm);
                    break;
            }
            Refresh(sim);
        }
        void SliderChanged(float sliderValue)
        {
            if (slider == null || gestureCanceled || !ReadyForGesture()) return;
            var mouse = Mouse.current;
            if (!pointerHeld && (mouse == null || !mouse.leftButton.isPressed || !Inside(mouse.position.ReadValue(), null))) return;
            var sim = owner.game.Sim;
            float current = kind == IceConsoleControlKind.FloodValve ? sim.Controls.FloodFlow :
                kind == IceConsoleControlKind.WashValve ? sim.Controls.WashValve : sim.Controls.BladeDepthMm / sim.Config.MaxBladeDepthMm;
            ChangeScalar(slider.normalizedValue - current);
        }

        public void Refresh(Simulation sim)
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (!ValidateBinding(out string error) || sim == null)
            {
                Set(value, "UNAVAILABLE");
                if (!reportedInvalid) Debug.LogError("Aurora console: " + (error ?? "live simulation missing."), this);
                reportedInvalid = true;
                return;
            }
            reportedInvalid = false;
            CaptureArtwork();
            var commands = sim.Controls;
            bool state = kind == IceConsoleControlKind.VerticalAuger ? commands.VerticalAuger :
                kind == IceConsoleControlKind.HorizontalAuger ? commands.HorizontalAuger :
                kind == IceConsoleControlKind.BoardBrush ? commands.BoardBrush : commands.WashPump;
            float fraction = 0, demand = 0;
            if (Maintained)
            {
                fraction = state ? 1 : 0;
                Set(value, state ? "ON" : "OFF");
                if (maintainedToggle != null) maintainedToggle.SetIsOnWithoutNotify(state);
            }
            else switch (kind)
            {
                case IceConsoleControlKind.Conditioner:
                    fraction = sim.Conditioner.Lift; demand = commands.ConditionerHydraulic;
                    Set(value, (fraction * 100).ToString("0") + "% UP");
                    Set(detail, IceDashboard.ConditionerStatus(sim));
                    break;
                case IceConsoleControlKind.TankLift:
                    fraction = sim.TankLift; demand = commands.TankHydraulic;
                    Set(value, (fraction * 100).ToString("0") + "% UP");
                    Set(detail, demand > .001f ? "RAISING" : demand < -.001f ? "LOWERING" : "HOLDING");
                    break;
                case IceConsoleControlKind.FloodValve:
                    fraction = commands.FloodFlow; Set(value, (fraction * 100).ToString("0") + "%"); break;
                case IceConsoleControlKind.WashValve:
                    fraction = commands.WashValve; Set(value, (fraction * 100).ToString("0") + "%"); break;
                case IceConsoleControlKind.Blade:
                    fraction = commands.BladeDepthMm / Mathf.Max(.0001f, sim.Config.MaxBladeDepthMm);
                    Set(value, commands.BladeDepthMm.ToString("0.00") + " mm");
                    Set(detail, (fraction * sim.Config.BladeWheelTurns).ToString("0.00") + " turns / " + IceDashboard.BladeContactMm(sim).ToString("0.000") + " mm CONTACT");
                    break;
                case IceConsoleControlKind.BladeStatus:
                    fraction = IceDashboard.BladeContactMm(sim) / Mathf.Max(.0001f, sim.Config.MaxBladeDepthMm);
                    Set(value, IceDashboard.BladeEngagement(sim));
                    Set(detail, "SET " + commands.BladeDepthMm.ToString("F2") + " mm\nCONTACT " + IceDashboard.BladeContactMm(sim).ToString("F3") + " mm");
                    break;
            }
            if (indicator != null) indicator.fillAmount = Mathf.Clamp01(fraction);
            if (slider != null) slider.SetValueWithoutNotify(Mathf.Lerp(slider.minValue, slider.maxValue, Mathf.Clamp01(fraction)));
            if (!animateGraphic) return;
            if (movingGraphic != null)
            {
                if (Hydraulic) movingGraphic.localPosition = artPosition + (Vector3)(demand < 0 ? negativeOffset * -demand : positiveOffset * demand);
                else if (Maintained) movingGraphic.localPosition = artPosition + (Vector3)(state ? positiveOffset : negativeOffset);
                else if (Rotary) movingGraphic.localRotation = artRotation * Quaternion.Euler(0, 0,
                    kind == IceConsoleControlKind.Blade ? closedAngle - fraction * sim.Config.BladeWheelTurns * 360 : Mathf.Lerp(closedAngle, openAngle, fraction));
            }
            if (kind == IceConsoleControlKind.BladeStatus)
            {
                if (schematicBody != null) schematicBody.localPosition = bodyPosition + (Vector3)(schematicLiftOffset * sim.Conditioner.Lift);
                if (schematicBlade != null) schematicBlade.localPosition = bladePosition + (Vector3)(fraction > 0 ?
                    schematicContactOffset * fraction : commands.BladeDepthMm <= .001f ? schematicRetractedOffset :
                    schematicOffIceOffset + schematicLiftOffset * sim.Conditioner.Lift);
                if (schematicSetMarker != null) schematicSetMarker.localPosition = markerPosition +
                    (Vector3)(schematicSetOffset * Mathf.Clamp01(commands.BladeDepthMm / Mathf.Max(.0001f, sim.Config.MaxBladeDepthMm)));
            }
        }
        static void Set(TMP_Text target, string content) { if (target != null && target.text != content) target.text = content; }
        void OnDisable() { CancelInteraction(); }
    }
}
