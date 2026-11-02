#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    public sealed partial class IceLayoutPlayerProbe
    {
        // Explicit development-player suite only. It writes disposable validation
        // libraries and never saves scenes, prefabs, source recordings or settings.
        IEnumerator ValidateCalibrationCompatibility()
        {
            game.NewSession(true);var g=game.RouteGuide;g.Refresh();g.UseOriginalReference();
            string evidence=Path.Combine(outputDirectory,"CalibrationCompatibility");
            string oldPath=Path.Combine(evidence,"Inputs","Repaired.guide.json");
            string correctedPath=File.ReadAllText(Path.Combine(evidence,"Corrected-guide-path.txt")).Trim();
            g.ConfigureValidationStorage(Path.Combine(outputDirectory,"CC-Native-"+(game.GetComponent<IceMachineRig>()!=null?"A":"O")));
            Directory.CreateDirectory(g.GuideDirectory);
            string oldCopy=Path.Combine(g.GuideDirectory,"Earlier-repaired.guide.json"),newCopy=Path.Combine(g.GuideDirectory,"Current-calibration.guide.json");
            File.Copy(oldPath,oldCopy,true);File.Copy(correctedPath,newCopy,true);
            g.Manager.Refresh();Check(g.Manager.Select(oldCopy),"native Route Manager selects the earlier repaired guide");
            if(!g.PanelOpen)g.ToggleRecordingPanel();
            yield return ClickAt(RouteManagerPanel.ControlRect("UseGps").center);
            Check(g.SelectedGuide==null&&g.TrainingStatus.Contains("ConditionerYawLimitRadians: guide=0.0349065848, session=0"),"native mismatch identifies legacy yaw and exact guide/session values");
            yield return Capture("Calibration-mismatch-"+currentResolution+".png","actual Route Manager rejection of earlier repaired guide");
            Check(g.Manager.Select(newCopy),"native Route Manager selects the new calibrated guide");
            yield return ClickAt(RouteManagerPanel.ControlRect("UseGps").center);
            Check(g.SelectedGuide!=null&&g.ActiveFit.Ready&&g.ActiveFit.Compatible(game.Sim.Config),"native Use as GPS accepts the corrected guide in the current scene");
            yield return Capture("Calibration-ready-"+currentResolution+".png","new guide accepted without replacing the earlier guide or raw route");
            yield return ClickAt(RouteManagerPanel.ControlRect("Drive").center);
            if(!g.Visible)g.Toggle();var raw=TrainingGuideStorage.Raw(g.SelectedGuide);double maxLead=0,previous=0;bool ordered=true;
            foreach(var p in raw.samples)
            {
                g.Progress.Observe(p.vehicleX,p.vehicleZ,p.vehicleHeadingRadians,p.speedMetersPerSecond);
                ordered&=g.Progress.Distance>=previous&&g.Progress.Distance<=p.distanceMeters+.25;previous=g.Progress.Distance;maxLead=Math.Max(maxLead,g.Progress.Distance-p.distanceMeters);
            }
            Check(ordered&&Math.Abs(g.Progress.Distance-g.ActiveFit.Length)<.000001&&g.Progress.Leg==g.ActiveFit.legs.Count-1,"native GPS replays all 5130 raw observations / 132 contexts in sequence; max lead "+maxLead.ToString("F6")+" m");
            int leg=0;
            for(int i=0;i<g.ActiveFit.legs.Count;i++)
            {
                var l=g.ActiveFit.legs[i];var p=g.ActiveFit.At(l.start+1);
                if(!l.turn&&l.direction>0&&l.end-l.start>5&&Mathf.Abs(p.x)<11&&Mathf.Abs(p.z)<24){leg=i;break;}
            }
            SetGuidePose(leg,g.ActiveFit.legs[leg].start+1);game.SelectCameraView("Forward");game.TickInspectionCamera(2);g.Refresh();
            Check(g.LookEnd-g.Progress.Distance<=12.192001&&g.DisplayPointCount>0,"corrected guide retains limited ground/minimap look-ahead");
            yield return Capture("Calibration-GPS-"+currentResolution+".png","corrected real route / explicitly seeded seated render pose, not a manual drive");
            game.TogglePause();yield return null;var slider=IceDashboard.ControlRect("SpeedSlider");
            yield return ClickAt(new Vector2(slider.x+3,slider.center.y));
            Check(Mathf.Abs(game.SelectedForwardSpeedMph-3)<.001f,"native ESC slider retains 3 MPH minimum");
            yield return ClickAt(new Vector2(slider.xMax-3,slider.center.y));
            Check(Mathf.Abs(game.SelectedForwardSpeedMph-8)<.001f,"native ESC slider retains 8 MPH maximum");
            yield return ClickAt(new Vector2(slider.center.x,slider.center.y));
            Check(Mathf.Abs(game.SelectedForwardSpeedMph-5.5f)<.001f,"native ESC slider retains quarter-MPH 5.50 setting");
            yield return ClickAt(new Vector2(800,567));
            Check(Mathf.Abs(game.SelectedForwardSpeedMph-5.5f)<.001f&&PlayerPrefs.HasKey(IceOperatorPreferences.SpeedKey),"clicking removed reset-button area cannot clear the selected speed or PlayerPrefs");
            Check(game.calibration.ForwardSpeed==2.65f,"internal original maximum remains 2.65 m/s");
            yield return Capture("Calibration-ESC-"+currentResolution+".png","ESC slider retained / reset button removed");
            game.TogglePause();game.NewSession(true);g.Refresh();
            Check(Mathf.Abs(game.SelectedForwardSpeedMph-5.5f)<.001f&&g.ActiveFit.Compatible(game.Sim.Config),"saved speed and compatible GPS survive a new session");
            g.UseOriginalReference();Check(g.SelectedGuide==null&&g.ActiveRouteName=="Original reference","original built-in reference remains available");
        }
    }
}
#endif
