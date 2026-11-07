using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    public enum IceMenuSettingKind { ForwardSpeed, SeatedEye }
    public sealed class IceMenuSetting : MonoBehaviour, IPointerDownHandler
    {
        public IceMenuScreens owner;
        public IceMenuScreen screen;
        public IceMenuSettingKind kind;
        public Slider slider;
        public TMP_Text value, detail;
        [Tooltip("Standard .NET numeric formatting. Changes presentation only.")]
        public string valueFormat = "{0:F2} mph";
        public string detailFormat = "{0:F2} km/h / saved between sessions";
        public float displayMultiplier = 1, detailMultiplier = 1.609344f;
        [TextArea] public string authoredEyeMessage = "The authored Driver Eye Anchor controls seated height. This fallback offset does not move that anchor.";
        [TextArea] public string fallbackEyeMessage = "Fallback seated offset / -8 to +12 cm";
        bool pointerCanceled;
        float observedValue = float.NaN;
        bool observedAnchor;
        public void Bind(IceMenuScreens presenter)
        {
            Unbind(); owner = presenter; pointerCanceled = true; observedValue = float.NaN;
            if (slider == null) return;
            // Data constraints, never RectTransform or artwork changes.
            slider.minValue = kind == IceMenuSettingKind.ForwardSpeed ? 3 : -.08f;
            slider.maxValue = kind == IceMenuSettingKind.ForwardSpeed ? 8 : .12f;
            slider.wholeNumbers = false; slider.onValueChanged.AddListener(Changed);
        }
        public void Unbind() { if (slider != null) slider.onValueChanged.RemoveListener(Changed); }
        void Changed(float next)
        {
            if (owner == null) return;
            if (!owner.CanUse(this)) { Refresh(true); return; }
            var mouse = Mouse.current;
            if (pointerCanceled && mouse != null && mouse.leftButton.isPressed && !mouse.leftButton.wasPressedThisFrame)
            { Refresh(true); return; }
            if (kind == IceMenuSettingKind.ForwardSpeed)
            {
                float snapped = IceOperatorPreferences.SnapMph(next);
                if (!Mathf.Approximately(owner.game.SelectedForwardSpeedMph, snapped)) owner.game.SetOperatorSpeedMph(snapped);
            }
            else owner.game.operatorEyeHeight = Mathf.Clamp(next, -.08f, .12f);
            Refresh(true);
        }
        public void OnPointerDown(PointerEventData e)
        { if (e.button == PointerEventData.InputButton.Left && owner != null && owner.CanUse(this)) pointerCanceled = false; }
        public void CancelPointer() { pointerCanceled = true; }
        void OnDisable() => CancelPointer();
        public void Refresh(bool force = false)
        {
            if (owner == null || owner.game == null || slider == null) return;
            float next = kind == IceMenuSettingKind.ForwardSpeed ? owner.game.SelectedForwardSpeedMph : owner.game.operatorEyeHeight;
            bool anchor = owner.game.UsesAuthoredDriverEye;
            if (!force && next == observedValue && anchor == observedAnchor && slider.value == next) return;
            observedValue = next; observedAnchor = anchor;
            if (slider.value != next) slider.SetValueWithoutNotify(next);
            IceNavigationUI.Text(value, Format(valueFormat, next * displayMultiplier));
            IceNavigationUI.Text(detail, kind == IceMenuSettingKind.ForwardSpeed ? Format(detailFormat, next * detailMultiplier) : anchor ? authoredEyeMessage : fallbackEyeMessage);
        }
        static string Format(string format, float number)
        { try { return string.Format(format, number); } catch (System.FormatException) { return number.ToString("F2"); } }
        void OnDestroy() => Unbind();
    }
}
