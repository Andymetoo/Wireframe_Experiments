using IceResurfacer.Core;
using TMPro;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    public enum IceServiceField { Hose, Reach, Supply, Flow, Overflow, Prompt, Notice }
    /// <summary>Read-only, independently positioned fragments of the existing service status/prompt.</summary>
    public sealed class IceServiceReadout : MonoBehaviour
    {
        public IceServiceField kind;
        public TMP_Text value;
        public string format = "{0}";
        public void Refresh(IceResurfacerGame game)
        {
            var s = game.Sim; var service = game.WaterService; string text = "";
            switch (kind)
            {
                case IceServiceField.Hose: text = s.HoseState == WaterHoseState.Stowed ? "HOSE STOWED" : s.HoseState == WaterHoseState.Carried ? "NOZZLE IN HAND" : s.HoseState == WaterHoseState.Dropped ? "LOOSE NOZZLE ON FLOOR" : s.HoseState == WaterHoseState.Flood ? "FLOOD WATER CONNECTED" : "WASH WATER CONNECTED"; break;
                case IceServiceField.Reach: if (s.HoseState != WaterHoseState.Stowed) text = service.HoseVisibleLength.ToString("0.0") + " / 11 m" + (service.HoseVisibleLength > 10.5f ? " TAUT" : ""); break;
                case IceServiceField.Supply: text = "SUPPLY " + Mathf.RoundToInt(s.SupplyValve * 100) + "%"; break;
                case IceServiceField.Flow: text = s.ServiceFlowLitersPerSecond.ToString("0.0") + " L/s"; break;
                case IceServiceField.Overflow: if (s.ServiceOverflowLitersPerSecond > .01f) text = "OVERFLOW — CLOSE WALL VALVE"; break;
                case IceServiceField.Prompt: text = service.Prompt; break;
                case IceServiceField.Notice: text = game.Notice; break;
            }
            IceNavigationUI.Text(value, string.IsNullOrEmpty(text) || format == "{0}" ? text : string.Format(format, text));
        }
    }
}
