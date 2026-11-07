using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug=UnityEngine.Debug;

namespace IceResurfacer.Editor
{
    // Explicit isolated Play Mode validation; never bakes or saves either scene.
    public static class IceRouteCalibrationValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/Validation~/CalibrationCompatibility/";
        const string Gate="Aurora.RouteCalibration.Validation";
        static readonly List<string> results=new List<string>();
        static int frames,errors;
        static string SceneName=>Path.GetFileNameWithoutExtension(IceResurfacerEditor.ScenePath);
        static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException(message);results.Add("PASS "+message);File.WriteAllLines(Root+SceneName+"-results.txt",results);}
        public static void RunBatch()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))throw new InvalidOperationException("Use the isolated project.");
            Directory.CreateDirectory(Root);EditorSceneManager.OpenScene(IceResurfacerEditor.ScenePath);SessionState.SetBool(Gate,true);Resume();EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Resume()
        {
            if(!SessionState.GetBool(Gate,false))return;
            EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
            if(EditorApplication.isPlaying)State(PlayModeStateChange.EnteredPlayMode);
        }
        static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        static void State(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode){frames=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}}
        static void Tick()
        {
            if(++frames<30)return;EditorApplication.update-=Tick;results.Clear();
            try{Run(UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>());Check(errors==0,"zero Unity runtime errors");SessionState.SetBool(Gate,false);EditorApplication.Exit(0);}
            catch(Exception e){results.Add("FAIL "+e);File.WriteAllLines(Root+SceneName+"-results.txt",results);SessionState.SetBool(Gate,false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Run(IceResurfacerGame game)
        {
            Check(game!=null&&game.Sim!=null,"actual scene enters Play Mode: "+SceneName);
            game.enabled=false;game.NewSession(true);var owner=game.RouteGuide;owner.Refresh();
            string calibration=JsonUtility.ToJson(game.calibration);
            var original=TrainingGuideStorage.Read(Root+"Inputs/Repaired.guide.json");string originalJson=TrainingGuideStorage.Encode(original);
            var raw=TrainingGuideStorage.Raw(original);var oldFit=RouteFitter.Fit(original,raw);
            var compatibilityFields=(string[])typeof(RouteFit).GetField("CompatibilityFields",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            var differences=oldFit.CalibrationDifferences(game.Sim.Config);
            Check(differences.Count==1&&differences[0].StartsWith("ConditionerYawLimitRadians:"),"reproduces the single legacy-yaw calibration mismatch");
            Check(original.configuration.ConditionerYawLimitRadians==.03490658476948738f&&game.Sim.Config.ConditionerYawLimitRadians==0,"guide yaw=0.03490658476948738 rad; active scene yaw=0 rad");
            foreach(string field in compatibilityFields)
            {
                var info=typeof(SimulatorConfig).GetField(field);float before=(float)info.GetValue(original.configuration),current=(float)info.GetValue(game.Sim.Config);
                results.Add("FIELD "+field+" | guide="+before.ToString("G9",System.Globalization.CultureInfo.InvariantCulture)+" | active="+current.ToString("G9",System.Globalization.CultureInfo.InvariantCulture));
            }
            Check(!owner.UseSavedRouteAsGps(Root+"Inputs/Repaired.guide.json")&&owner.TrainingStatus.Contains(differences[0]),"Route Manager rejection contains the exact field and both values");
            File.WriteAllText(Root+SceneName+"-mismatch-message.txt",owner.TrainingStatus);
            Check(owner.SelectedGuide==null&&owner.ActiveRouteName=="Original reference","mismatch preserves original reference and active selection");
            string marker=Root+"Corrected-guide-path.txt",saved;
            if(File.Exists(marker))saved=File.ReadAllText(marker);
            else
            {
                var editor=ScriptableObject.CreateInstance<TrainingRouteEditorWindow>();
                try{editor.LoadGuide(Root+"Inputs/Repaired.guide.json");saved=editor.RevalidateForScene(game.Sim.Config,Root+"Library");}
                finally{UnityEngine.Object.DestroyImmediate(editor);}
                File.WriteAllText(marker,saved);
            }
            var corrected=TrainingGuideStorage.Read(saved);var fit=RouteFitter.Fit(corrected,TrainingGuideStorage.Raw(corrected));
            Check(corrected.id!=original.id&&corrected.rawRecordingJson==original.rawRecordingJson&&corrected.sourceSha256==original.sourceSha256,"new guide identity retains exact embedded original recording and source checksum");
            Check(corrected.configuration.ConditionerYawLimitRadians==0&&fit.Compatible(game.Sim.Config),"separate guide declares current scene calibration and passes strict compatibility");
            Check(fit.Ready&&fit.validationPerformed&&fit.boundaryErrors==0&&fit.curvatureErrors==0&&fit.recordedRadiusWarnings==0,"full route passes body, observed conditioner, current attached conditioner, and radius checks");
            Check(fit.samples.Count==46616&&Math.Abs(fit.Length-raw.distanceMeters)<.000001&&fit.maximumRawDeviation==0,"all 46,616 samples preserve 1003.234406572 m and zero raw-trajectory displacement");
            bool controlsSame=corrected.controls.Count==original.controls.Count;
            for(int i=0;controlsSame&&i<original.controls.Count;i++)controlsSame=JsonUtility.ToJson(corrected.controls[i])==JsonUtility.ToJson(original.controls[i]);
            Check(controlsSame,"all 398 control points, source indices, handles and annotations are unchanged");
            Check(TrainingGuideStorage.Encode(original)==originalJson,"revalidation never mutates the earlier repaired guide in memory");
            // Every safety field remains checked, at the same 0.0001 tolerance.
            foreach(string name in compatibilityFields)
            {
                var altered=TrainingRouteDefinition.CopyConfiguration(game.Sim.Config);var field=typeof(SimulatorConfig).GetField(name);float value=(float)field.GetValue(altered);field.SetValue(altered,value+.01f);
                Check(!fit.Compatible(altered)&&fit.CalibrationDifferences(altered).Count==1&&fit.CalibrationDifferences(altered)[0].StartsWith(name+":"),"strict mismatch diagnostic for "+name);
            }
            var nan=TrainingRouteDefinition.CopyConfiguration(game.Sim.Config);nan.Wheelbase=float.NaN;
            Check(!fit.Compatible(nan)&&!fit.Compatible(null),"non-finite and missing calibration cannot pass compatibility");
            var within=TrainingRouteDefinition.CopyConfiguration(game.Sim.Config);within.Wheelbase+=.00005f;
            Check(fit.Compatible(within),"pre-existing 0.0001 compatibility tolerance is preserved");
            var unsafeConfig=TrainingRouteDefinition.CopyConfiguration(game.Sim.Config);unsafeConfig.BodyFront=8;
            bool rejected=false;try{TrainingGuideStorage.RevalidateCopy(original,unsafeConfig,out _);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"revalidation against unsafe enlarged geometry is refused rather than certified");
            var bad=CurrentConditionerFixture();var badFit=RouteFitter.Fit(bad,TrainingGuideStorage.Raw(bad));
            bool attachedFailure=false;foreach(var p in badFit.samples)attachedFailure|=p.currentConditionerBoundary;
            Check(!badFit.Ready&&attachedFailure,"labeled negative fixture: safe historical conditioner cannot conceal current attached-conditioner board collision");
            // Keep fully qualified catalog paths below Windows MAX_PATH in the
            // doubly nested isolated project (the production library is shorter).
            var directory="Assets/Experiments/IceResurfacer/Validation~/CC-"+(SceneName.Contains("Authored")?"A":"O");owner.ConfigureValidationStorage(directory);Directory.CreateDirectory(owner.GuideDirectory);
            string runtime=Path.GetFullPath(Path.Combine(owner.GuideDirectory,Path.GetFileName(saved)));File.Copy(saved,runtime,true);owner.Manager.Refresh();
            var catalog=new RouteLibraryCatalog();catalog.Refresh(owner.Library,owner.GuideDirectory);
            foreach(var entry in catalog.entries)results.Add("LIBRARY "+entry.path+" valid="+entry.Valid+" error="+entry.error);
            var selectedEntry=catalog.entries.Find(e=>Path.GetFileName(e.path)==Path.GetFileName(saved));
            Check(selectedEntry!=null&&selectedEntry.Valid,"corrected guide appears valid in the live library catalog");runtime=selectedEntry.path;
            Check(owner.Manager.Select(runtime),"Route Manager discovers the same corrected guide in "+SceneName);
            bool accepted=owner.UseSavedRouteAsGps(runtime);Check(accepted,"Route Manager loads the corrected guide: "+owner.TrainingStatus);
            if(!owner.Visible)owner.Toggle();owner.Refresh();
            string vehicle=JsonUtility.ToJson(game.Sim.Vehicle),conditioner=JsonUtility.ToJson(game.Sim.Conditioner),controls=JsonUtility.ToJson(game.Sim.Controls),report=JsonUtility.ToJson(game.Sim.GetReport());
            double maxLead=0,maxAdvance=0,priorDistance=0;int maxSearch=0,priorLeg=0;bool ordered=true;
            var watch=Stopwatch.StartNew();
            foreach(var p in raw.samples)
            {
                owner.Progress.Observe(p.vehicleX,p.vehicleZ,p.vehicleHeadingRadians,p.speedMetersPerSecond);
                maxLead=Math.Max(maxLead,owner.Progress.Distance-p.distanceMeters);maxAdvance=Math.Max(maxAdvance,owner.Progress.Distance-priorDistance);maxSearch=Math.Max(maxSearch,owner.Progress.LastSearchSamples);
                ordered&=owner.Progress.Distance>=priorDistance&&owner.Progress.Leg>=priorLeg&&owner.Progress.Distance<=p.distanceMeters+.25;
                priorDistance=owner.Progress.Distance;priorLeg=owner.Progress.Leg;
                if(p.sequence%200==0)owner.Refresh();
            }
            watch.Stop();owner.Refresh();
            Check(ordered&&Math.Abs(owner.Progress.Distance-fit.Length)<.000001&&owner.Progress.Leg==fit.legs.Count-1,"all 5,130 raw poses traverse all 132 contexts in order without skipping nearby loops");
            Check(vehicle==JsonUtility.ToJson(game.Sim.Vehicle)&&conditioner==JsonUtility.ToJson(game.Sim.Conditioner)&&controls==JsonUtility.ToJson(game.Sim.Controls)&&report==JsonUtility.ToJson(game.Sim.GetReport()),"guidance replay leaves vehicle, conditioner, controls, scoring and simulation state untouched");
            Check(owner.LookEnd-owner.Progress.Distance<=IceRouteGuide.LookAheadMeters+.0001&&owner.DisplayPointCount<=70,"live preview retains the 80-foot / 70-point bound");
            results.Add("REPLAY samples="+raw.samples.Count+" length="+fit.Length.ToString("F9")+" contexts="+fit.legs.Count+" maxLead="+maxLead.ToString("F6")+" maxAdvance="+maxAdvance.ToString("F6")+" maxSearch="+maxSearch+" ms="+watch.Elapsed.TotalMilliseconds.ToString("F3"));
            bool had=PlayerPrefs.HasKey(IceOperatorPreferences.SpeedKey);float previous=PlayerPrefs.GetFloat(IceOperatorPreferences.SpeedKey);
            try
            {
                foreach(float mph in new[]{3f,8f,5.5f})
                {
                    game.SetOperatorSpeedMph(mph);game.NewSession(true);owner.Refresh();
                    Check(Mathf.Abs(game.SelectedForwardSpeedMph-mph)<.0001f&&PlayerPrefs.HasKey(IceOperatorPreferences.SpeedKey)&&game.calibration.ForwardSpeed==2.65f&&owner.ActiveFit.Compatible(game.Sim.Config),"persistent "+mph+" MPH selection retains baseline calibration and compatible GPS");
                }
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var pause=typeof(IceDashboard).GetMethod("Pause",flags);
                Check(pause!=null&&IceDashboard.ControlRect("SpeedSlider").width>0,"ESC slider remains present");
                bool absent=false;try{absent=IceDashboard.ControlRect("SpeedReset")==default;}catch(ArgumentException){absent=true;}
                Check(absent,"removed reset button has no registered UI hitbox");
            }
            finally{if(had)IceOperatorPreferences.SaveMph(previous);else IceOperatorPreferences.ResetSpeed();}
            Check(calibration==JsonUtility.ToJson(game.calibration),"entire serialized scene calibration remains unchanged");
            Check(File.ReadAllText(Root+"Inputs/Raw.route.json")==corrected.rawRecordingJson,"original raw file remains verbatim in the corrected persisted guide");
            Check(owner.UseSavedRouteAsGps(Root+"Inputs/Raw.route.json")&&owner.SelectedGuide.configuration.ConditionerYawLimitRadians==0,"importing old raw data prepares a separately revalidated current-calibration guide");
            Check(TrainingGuideStorage.Raw(owner.SelectedGuide).configuration.ConditionerYawLimitRadians==original.configuration.ConditionerYawLimitRadians,"automatic raw import preserves its historical embedded calibration");
        }
        static TrainingRouteDefinition CurrentConditionerFixture()
        {
            var raw=new RecordedRouteData{schemaVersion=1,id=Guid.NewGuid().ToString("N"),name="SYNTHETIC current-conditioner boundary fixture",createdUtc=DateTimeOffset.UtcNow.ToString("O"),
                provenance="Deterministic negative test; NOT the operator's route.",coordinates=RecordedRouteData.Coordinates,configuration=new SimulatorConfig(),durationSeconds=2,distanceMeters=.5,distanceThresholdMeters=.2f,headingThresholdDegrees=1,complete=true};
            for(int i=0;i<3;i++)raw.samples.Add(new RecordedRouteSample{sequence=i,seconds=i,distanceMeters=i*.25,vehicleX=0,vehicleZ=-29.4f+i*.25f,speedMetersPerSecond=.25f,direction=1,conditionerX=0,conditionerZ=-28+i*.25f});
            return TrainingGuideStorage.Create(raw);
        }
    }
}
