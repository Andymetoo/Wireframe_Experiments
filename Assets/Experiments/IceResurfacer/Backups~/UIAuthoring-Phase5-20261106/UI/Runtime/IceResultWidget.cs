using IceResurfacer.Core;
using TMPro;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    public enum IceResultKind
    {
        Heading, Score, Duration, Coverage, FinishQuality, WaterDistribution, Missed,
        ExcessWater, Efficiency, Handling, Misuse, ShavingCoverage, WaterCoverage,
        SelectedSpeed, OriginalSpeed, Completion, Mode
    }
    /// <summary>Explicit read-only RunReport formatting. No scoring/calibration logic lives in the UI.</summary>
    public sealed class IceResultWidget : MonoBehaviour
    {
        public IceResultKind kind;
        public TMP_Text value, points;
        public string valueFormat = "{0:0.0}%";
        public string pointsFormat = "{0:0.0}";
        public float multiplier = 100;
        [TextArea] public string practiceText = "Practice session report";
        [TextArea] public string completeText = "Complete sheet. Inspect the survey for finish differences.";
        [TextArea] public string partialText = "Partial sheet: completion requires 95% combined treatment of reachable ice.";
        [TextArea] public string practiceHeading = "PRACTICE REVIEW / NO SCORE RECORDED";
        [TextArea] public string completeHeading = "FULL RUN COMPLETE";
        [TextArea] public string partialHeading = "SESSION COMPLETE / PARTIAL SHEET";
        public void Refresh(in RunReport r, bool guided)
        {
            float number = 0, score = 0; bool hasPoints = false; string content = null;
            switch (kind)
            {
                case IceResultKind.Heading: content = r.Practice ? practiceHeading : r.Complete ? completeHeading : partialHeading; break;
                case IceResultKind.Score: content = r.Practice ? practiceText : Format(valueFormat, r.Score * multiplier); break;
                case IceResultKind.Duration: content = ((int)r.ElapsedSeconds / 60).ToString("00") + ":" + ((int)r.ElapsedSeconds % 60).ToString("00"); break;
                case IceResultKind.Coverage: number = r.Coverage; score = r.CoveragePoints; hasPoints = true; break;
                case IceResultKind.FinishQuality: number = r.FinishQuality; score = r.FinishPoints; hasPoints = true; break;
                case IceResultKind.WaterDistribution: number = r.WaterDistribution; score = r.WaterPoints; hasPoints = true; break;
                case IceResultKind.Missed: number = r.MissedFraction; break;
                case IceResultKind.ExcessWater: number = r.OverwaterFraction; score = r.OverwaterPoints; hasPoints = true; break;
                case IceResultKind.Efficiency: number = r.OperationalEfficiency; score = r.EfficiencyPoints; hasPoints = true; break;
                case IceResultKind.Handling: number = r.TankDumps; score = r.HandlingPoints; hasPoints = true; break;
                case IceResultKind.Misuse: number = r.Mistakes; score = r.MisusePenalty; hasPoints = true; break;
                case IceResultKind.ShavingCoverage: number = r.ShavingCoverage; break;
                case IceResultKind.WaterCoverage: number = r.WaterCoverage; break;
                case IceResultKind.SelectedSpeed: number = r.SelectedMaxForwardSpeedMph; break;
                case IceResultKind.OriginalSpeed: number = r.OriginalMaxForwardSpeedMph; break;
                case IceResultKind.Completion: content = r.Complete ? completeText : partialText; break;
                case IceResultKind.Mode: content = guided ? "ASSISTED TRAINING / UNSCORED" : r.Practice ? "PRACTICE / FREE SIMULATION" : "SCORED / FREE SIMULATION"; break;
            }
            IceNavigationUI.Text(value, content ?? Format(valueFormat, number * multiplier));
            IceNavigationUI.Text(points, hasPoints && !r.Practice ? Format(pointsFormat, score) : "");
        }
        static string Format(string format, float number)
        { try { return string.Format(format, number); } catch (System.FormatException) { return number.ToString("F2"); } }
    }
}
