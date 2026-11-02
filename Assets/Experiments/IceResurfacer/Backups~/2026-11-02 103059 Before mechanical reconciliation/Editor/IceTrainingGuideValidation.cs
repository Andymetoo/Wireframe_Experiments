using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Editor
{
    public static class IceTrainingGuideValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/Validation~/TrainingGuide/";
        static readonly List<string> results=new List<string>();
        static void Check(bool pass,string text){if(!pass)throw new InvalidOperationException("Training guide: "+text);results.Add("PASS "+text);}
        internal static void Run(IceResurfacerGame game,Keyboard keyboard,Mouse mouse)
        {
            results.Clear();Directory.CreateDirectory(Root);bool enabled=game.enabled;game.enabled=false;
            var guide=game.RouteGuide;var window=ScriptableObject.CreateInstance<TrainingRouteEditorWindow>();
            var input=typeof(IceRouteGuide).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            void KeyInput(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();input.Invoke(guide,null);}
            try
            {
                if(guide.Visible)guide.Toggle();game.NewSession(true);guide.Refresh();guide.UseOriginalReference();
                string path="Assets/Experiments/IceResurfacer/Validation~/RouteRecording/Deterministic-fixture.route.json";
                string original=File.ReadAllText(path);var raw=RouteRecordingStorage.Decode(original);window.Create(raw);
                var d=window.Document;d.id="99449d33ce864967aa8228ed92c279b6";d.createdUtc="2026-10-30T00:00:00Z";
                d.name="TEST FIXTURE / perimeter and inward loops";window.Rebuild();
                Check(window.Fit.Ready,"Editor creates a constrained guide from an existing version-1 .route.json");
                Check(window.ControlCentersAreSelectable(),"every visible control center selects the point rather than a nearby short tangent handle");
                Check(raw.provenance.Contains("NOT the operator"),"fixture is explicitly labeled; no historical operator pattern is invented");
                float required=d.configuration.Wheelbase/Mathf.Tan(d.configuration.MaxSteeringRadians);
                Check(window.Fit.minimumRadius>=required/1.001f&&window.Fit.recordedRadiusWarnings==0&&window.Fit.boundaryErrors==0,"observed minimum radius "+window.Fit.minimumRadius.ToString("F6")+" m matches "+required.ToString("F6")+" m within the fitter's existing 0.1% numerical tolerance; body/tank/conditioner clearance pass");
                int controls=d.controls.Count;var p=d.controls[10].Copy();string embedded=d.rawRecordingJson;
                window.MovePoint(10,p.x+.05f,p.z);Check(d.controls[10].x!=p.x,"control position can be edited");window.UndoEdit();d=window.Document;
                Check(d.controls[10].x==p.x,"Editor undo restores a moved point");window.UndoEdit(true);window.UndoEdit();
                window.SetHandles(10,p.headingRadians+.002f,p.incoming*1.01f,p.outgoing);Check(window.Document.controls[10].incoming!=p.incoming,"tangent/heading edits rebuild the curve");window.UndoEdit();
                window.InsertPoint();Check(window.Document.controls.Count==controls+1,"Editor inserts a cubic control point");window.RemovePoint();Check(window.Document.controls.Count==controls,"Editor removes an intermediate control point");window.UndoEdit();window.UndoEdit();
                window.MarkPoint(10,RouteMarker.Turn|RouteMarker.Pass,"Validation pass transition");bool marked=false;foreach(var leg in window.Fit.legs)marked|=leg.label=="Validation pass transition";
                Check(marked,"turn/pass marker creates a named rejoin context");window.UndoEdit();
                Check(window.Document.rawRecordingJson==embedded&&File.ReadAllText(path)==original,"editing, fit, insertion, deletion and undo preserve original raw bytes");
                string saved=window.SaveCopy(Root+"Library"),next=window.SaveCopy(Root+"Library");Check(saved!=next&&File.Exists(saved),"corrected guides save as independent copies");
                var loaded=TrainingGuideStorage.Read(saved);Check(TrainingGuideStorage.Raw(loaded).samples.Count==raw.samples.Count&&loaded.rawRecordingJson==embedded,"guide round-trip keeps every original source observation");
                string json=File.ReadAllText(saved);bool rejected=false;try{TrainingGuideStorage.Decode(json.Replace("perimeter and inward loops","altered without checksum"));}catch(FormatException){rejected=true;}
                Check(rejected,"damaged guide checksum is rejected");window.LoadGuide(saved);Check(window.Fit.Ready&&window.Document.controls.Count==controls,"Editor reload reconstructs a physically validated curve");
                File.WriteAllBytes(Root+"Editor-path-preview.png",window.PreviewPng());
                File.WriteAllBytes(Root+"Editor-intended-footprints.png",window.PreviewPng(GuideViewLayer.IntendedFootprints));
                File.WriteAllBytes(Root+"Editor-recorded-blade.png",window.PreviewPng(GuideViewLayer.RecordedBlade));
                File.WriteAllBytes(Root+"Editor-recorded-water.png",window.PreviewPng(GuideViewLayer.RecordedWater));
                File.WriteAllBytes(Root+"Editor-missed-diagnostic.png",window.PreviewPng(GuideViewLayer.Missed));
                File.WriteAllText(Root+"Fixture.guide.json",TrainingGuideStorage.Encode(window.Document));File.WriteAllText(Root+"Fixture.route.json",original);
                File.WriteAllText("Assets/Experiments/IceResurfacer/Resources/TrainingTestGuide.json",TrainingGuideStorage.Encode(window.Document));AssetDatabase.Refresh();
                Check(Resources.Load<TextAsset>("TrainingTestGuide")!=null,"standalone fixture is packaged for explicit test selection");
                Check(guide.SelectedGuide==null&&!guide.Visible,"saving the fixture does not replace or enable the original F5 reference");
                Check(guide.LoadTrainingGuide(saved)&&!guide.Visible,"explicit valid guide load does not auto-enable F5");
                float x=game.Sim.Vehicle.X,z=game.Sim.Vehicle.Z,water=game.Sim.FloodWaterLiters,throttle=game.Sim.Controls.Throttle;
                KeyInput();KeyInput(Key.F5);KeyInput();guide.Refresh();
                Check(guide.Visible&&guide.ActiveFit.Ready&&guide.DisplayPointCount<=120&&guide.MapPointCount<=120,"actual F5 enables bounded cached ground/minimap previews");
                Check(guide.LookEnd-guide.Progress.Distance<=IceRouteGuide.LookAheadMeters+.001,"live preview is limited to current/upcoming context within 28 m");
                guide.RejoinLeg(5);Check(game.Sim.Vehicle.X==x&&game.Sim.Vehicle.Z==z&&game.Sim.FloodWaterLiters==water&&game.Sim.Controls.Throttle==throttle,"selection, F5 and explicit rejoin never operate machinery or move the vehicle");
                var bad=TrainingGuideStorage.Read(saved);bad.controls[10].x=80;var prior=guide.SelectedGuide;
                Check(!guide.SelectTrainingGuide(bad)&&guide.SelectedGuide==prior,"invalid draft is refused while prior guidance/driving remain available");
                var renderer=guide.transform.Find("F5 advisory upcoming route").GetComponent<LineRenderer>();Check(renderer.sharedMaterial.shader.name.Contains("Training")&&!ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader),"dedicated training shader compiles");
                Check(File.ReadAllText("Assets/Experiments/IceResurfacer/Resources/TrainingRoute.shader").Contains("ZTest LEqual"),"world route uses solid-geometry depth occlusion");
                window.Document.name="Changed after selection";Check(guide.SelectedGuide.name!="Changed after selection","live guide is detached from subsequent Editor edits");
                guide.UseOriginalReference();Check(guide.SelectedGuide==null,"original F5 reference is restored after explicit test selection");
                results.Add("Fixture: "+window.Fit.samples.Count+" fitted samples; "+window.Fit.legs.Count+" contexts; "+window.Fit.Length.ToString("F6")+" m; max source-sample deviation "+window.Fit.maximumRawDeviation.ToString("F6")+" m.");
                results.Add("Full-route PNGs are shared authoring-preview texture exports. Native captures separately verify UI and live partial-route rendering.");
            }
            finally
            {
                if(guide.Visible)guide.Toggle();guide.UseOriginalReference();if(guide.PanelOpen)guide.ToggleRecordingPanel();
                UnityEngine.Object.DestroyImmediate(window);game.enabled=enabled;File.WriteAllLines(Root+"Unity-training-guide-results.txt",results);
            }
        }
        public static void VerifyPersisted()
        {
            try
            {
                if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))throw new InvalidOperationException("Use isolated validation project.");
                results.Clear();var guide=TrainingGuideStorage.Read(Root+"Fixture.guide.json");var raw=TrainingGuideStorage.Raw(guide);
                Check(!EditorApplication.isPlaying&&guide.rawRecordingJson==File.ReadAllText(Root+"Fixture.route.json"),"fresh Unity process reloads corrected guide and exact raw source after Play Mode ended");
                var fit=RouteFitter.Fit(guide,raw);Check(fit.Ready&&fit.samples.Count>10000,"fresh process reconstructs valid guide independently of scene objects");
                results.Add("Length="+fit.Length.ToString("F6")+" m; minimum radius="+fit.minimumRadius.ToString("F6")+" m; samples="+fit.samples.Count+"; contexts="+fit.legs.Count);
                File.WriteAllLines(Root+"Fresh-process-persistence.txt",results);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
