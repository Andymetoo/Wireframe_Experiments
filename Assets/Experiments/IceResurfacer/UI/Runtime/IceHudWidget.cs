using IceResurfacer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    public enum IceHudWidgetKind { Identity, Speed, Load, Flood, Wash, Snow, Conditioner, Blade }

    /// <summary>
    /// Binds simulation values to authored elements. Positions, typography, artwork,
    /// colors, and static labels remain entirely under the author's control.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IceHudWidget : MonoBehaviour
    {
        public IceHudWidgetKind kind;
        public TMP_Text label;
        public TMP_Text value;
        public TMP_Text detail;
        public Image fill;
        public Button action;
        [Tooltip("Editable prefix before the live elapsed time on the Identity widget.")]
        public string identityPrefix = "NORTHLINE  /  ";

        IceAuthoredHud owner;
        bool reportedInvalid;

        public bool ValidateBinding(out string error)
        {
            if (value == null) { error = "Assign the live Value text."; return false; }
            if ((kind == IceHudWidgetKind.Speed || kind == IceHudWidgetKind.Conditioner || kind == IceHudWidgetKind.Blade) && detail == null)
            { error = "Assign the Detail text for the units, direction, lift percentage, or blade contact information."; return false; }
            if ((kind == IceHudWidgetKind.Load || kind == IceHudWidgetKind.Flood ||
                 kind == IceHudWidgetKind.Wash || kind == IceHudWidgetKind.Snow) && fill == null)
            { error = "Assign the meter Fill image."; return false; }
            if (kind == IceHudWidgetKind.Load && action == null)
            { error = "Assign the machine-load Action button for diagnostics."; return false; }
            error = null;
            return true;
        }

        internal void Bind(IceAuthoredHud hud)
        {
            if (action != null) action.onClick.RemoveListener(InvokeAction);
            owner = hud;
            if (kind == IceHudWidgetKind.Load && action != null) action.onClick.AddListener(InvokeAction);
        }

        internal void Unbind(IceAuthoredHud hud)
        {
            if (owner != hud) return;
            if (action != null) action.onClick.RemoveListener(InvokeAction);
            owner = null;
        }

        void InvokeAction()
        {
            if (kind == IceHudWidgetKind.Load && owner != null && owner.CanInteract)
                owner.game.ToggleDiagnostics();
        }

        public void Refresh(Simulation simulation)
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (!ValidateBinding(out string error))
            {
                Unavailable(error);
                return;
            }
            if (simulation == null)
            {
                Unavailable("No live simulation is bound.");
                return;
            }
            if (kind == IceHudWidgetKind.Flood && simulation.Config.FloodCapacityLiters <= 0 ||
                kind == IceHudWidgetKind.Wash && simulation.Config.WashCapacityLiters <= 0 ||
                kind == IceHudWidgetKind.Snow && simulation.Config.SnowCapacityM3 <= 0)
            {
                Unavailable("Tank capacity must be greater than zero.");
                return;
            }
            reportedInvalid = false;
            var s = simulation;
            switch (kind)
            {
                case IceHudWidgetKind.Identity:
                    Set(value, identityPrefix + ((int)s.Time / 60).ToString("00") + ":" + ((int)s.Time % 60).ToString("00"));
                    break;
                case IceHudWidgetKind.Speed:
                    Set(value, (Mathf.Abs(s.Vehicle.Speed) * 3.6f).ToString("0.0"));
                    Set(detail, s.Vehicle.Speed < -.02f ? "km/h REVERSE" : "km/h FORWARD");
                    break;
                case IceHudWidgetKind.Load:
                    Set(value, (s.MachineLoad * 100).ToString("0") + "%");
                    fill.fillAmount = Mathf.Clamp01(s.MachineLoad);
                    Set(detail, s.MachineLoad > .85f ? "HIGH LOAD / Click or F3" : "Click / F3 diagnostics");
                    break;
                case IceHudWidgetKind.Flood:
                    Meter(s.FloodWaterLiters, s.Config.FloodCapacityLiters, " L");
                    break;
                case IceHudWidgetKind.Wash:
                    Meter(s.WashWaterLiters, s.Config.WashCapacityLiters, " L");
                    break;
                case IceHudWidgetKind.Snow:
                    float snow = s.SnowTankM3 / s.Config.SnowCapacityM3;
                    Set(value, (snow * 100).ToString("0") + "% / " + s.SnowTankM3.ToString("0.00") + " m3");
                    Set(detail, snow > .85f ? "NEAR CAPACITY" : "");
                    fill.fillAmount = Mathf.Clamp01(snow);
                    break;
                case IceHudWidgetKind.Conditioner:
                    string conditioner = IceDashboard.ConditionerStatus(s);
                    const string prefix = "CONDITIONER ";
                    Set(value, conditioner.StartsWith(prefix) ? conditioner.Substring(prefix.Length) : conditioner);
                    Set(detail, (s.Conditioner.Lift * 100).ToString("0") + "% raised");
                    break;
                case IceHudWidgetKind.Blade:
                    Set(value, s.Controls.BladeDepthMm.ToString("0.00") + " mm SET");
                    Set(detail, IceDashboard.BladeContactMm(s).ToString("0.000") + " mm CONTACT");
                    break;
            }
        }

        void Meter(float amount, float capacity, string unit)
        {
            if (capacity <= 0) { Unavailable("Tank capacity must be greater than zero."); return; }
            Set(value, amount.ToString("0") + unit);
            fill.fillAmount = Mathf.Clamp01(amount / capacity);
        }

        void Unavailable(string error)
        {
            Set(value, "UNAVAILABLE");
            Set(detail, "Check HUD binding");
            if (fill != null) fill.fillAmount = 0;
            if (!reportedInvalid)
            {
                Debug.LogError("Aurora UI: " + kind + " widget on '" + name + "': " + error, this);
                reportedInvalid = true;
            }
        }

        static void Set(TMP_Text target, string content)
        {
            if (target != null && target.text != content) target.text = content;
        }
    }
}
