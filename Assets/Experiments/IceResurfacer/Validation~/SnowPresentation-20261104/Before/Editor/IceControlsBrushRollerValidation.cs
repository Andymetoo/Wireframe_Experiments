using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Conditioner-mounted controls, Edit Mode lift preview, board brush authoring, collision outline and
    /// board contact. Isolated validation project only (StageUnityValidation.ps1).
    ///   Apply  : the Inspector's Transfer Authored Geometry on the authored scene (collision outline, guide
    ///            roller), new calibrated GPS guide copies; saves the scene only.
    ///   Verify : fresh process; persistence, calibration context, preview vs Play, brush apply round trip,
    ///            Play interaction at every lift, board driving, GPS, screenshots, no mutations.
    /// </summary>
    [InitializeOnLoad]
    public static class IceControlsBrushRollerValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        const string Out=Root+"Validation~/ControlsBrushRoller/";
        const string StageKey="Aurora.ControlsBrushRoller.Stage";
        static string ResultsPath=>Out+"results-"+SessionState.GetString("Aurora.ControlsBrushRoller.Results","run")+".txt";
        static readonly float[] Lifts={0f,.25f,.5f,.75f,1f};
        static int frame,errors,passes;

        [Serializable] sealed class Expected
        {
            public string prefabHash,sceneHash,meshesHash,calibration;
            public List<string> guides=new List<string>();
            public List<PoseRow> preview=new List<PoseRow>();
        }
        [Serializable] sealed class PoseRow { public string part; public float lift; public Vector3 machine; }
        [Serializable] sealed class ControlList { public List<RouteControlPoint> controls; }

        // ---- Apply --------------------------------------------------------------------------------

        public static void Apply()
        {
            Begin("Apply",()=>
            {
                var rig=OpenScene();var game=Game();var w=rig.Collect();
                string prefabHash=Hash(IceMachineBaker.PrefabPath),meshesHash=Hash(Root+"Machine/AuroraR54.Meshes.asset");
                Start("Before: Mechanical Alignment in the authored scene");
                Table(IceMechanicalAlignment.Measure(w,game.calibration));
                var before=JsonUtility.FromJson<SimulatorConfig>(JsonUtility.ToJson(game.calibration));
                Start("Guide roller: Move Guide Roller Outboard (Inspector action, 25 mm lead)");
                string partsBefore=Snapshot(rig.transform);
                float shortfall=IceMechanicalAlignment.GuideRollerShortfall(w,game.calibration,RollerLead,out float widest,out string widestPart);
                Info("roller shortfall for a "+(RollerLead*1000)+" mm lead over the "+widestPart+" ("+widest.ToString("F3")+" m): "+(shortfall*1000).ToString("F1")+" mm");
                Check(IceMechanicalAlignment.MoveGuideRollerOutboard(w,game.calibration,RollerLead,out string moved),"the roller assembly moves outboard: "+moved);
                Info(moved);
                var changedParts=Diff(partsBefore,Snapshot(rig.transform));
                Info("transforms changed by the roller move: "+string.Join(", ",changedParts));
                Check(changedParts.Count==2&&changedParts.Any(n=>n.StartsWith("Front-left board guide"))&&changedParts.Any(n=>n.StartsWith("Guide roller bracket")),
                    "only the roller assembly and its bracket moved (everything else in the machine is untouched)");
                Check(PrefabUtility.GetPropertyModifications(rig.gameObject).Count(m=>!IsRoot(rig,m))==0,"the move is written to the prefab, not left as scene overrides");
                w=rig.Collect();
                Start("Transfer Authored Geometry to Simulation (Inspector action)");
                Check(IceMechanicalAlignment.Reconcile(w,game,out var changes,out string error),"Reconcile succeeds"+(error!=null?": "+error:""));
                foreach(string line in changes)Info(line);
                w=rig.Collect();
                Start("After");
                var after=Table(IceMechanicalAlignment.Measure(w,game.calibration));
                foreach(var item in after)
                    Check(item.Kind==IceMechanicalAlignment.Kind.Matches||item.Kind==IceMechanicalAlignment.Kind.Info,"aligned: "+item.Label+" ("+item.ErrorMm.ToString("F1")+" mm)");
                foreach(var f in typeof(SimulatorConfig).GetFields(BindingFlags.Public|BindingFlags.Instance))
                {
                    object a=f.GetValue(before),b=f.GetValue(game.calibration);
                    bool same=a is float[] x&&b is float[] y?x.SequenceEqual(y):Equals(a,b);
                    if(!same)Info("CALIBRATION | "+f.Name+" | "+Describe(a)+" -> "+Describe(b)+(CompatibilityFields().Contains(f.Name)?" | GPS COMPATIBILITY FIELD":""));
                }
                Check(game.calibration.HasCollisionOutline&&game.calibration.ConditionerCollisionHalfWidth>1f,"collision outline and conditioner collider are in the scene calibration");
                Check(Hash(Root+"Machine/AuroraR54.Meshes.asset")==meshesHash,"the machine meshes are not modified");
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                prefabHash=Hash(IceMachineBaker.PrefabPath);
                RollerHardware(rig.Collect(),game.calibration);
                var expected=new Expected{prefabHash=prefabHash,meshesHash=meshesHash,sceneHash=Hash(IceMachineBaker.AuthoredScenePath),calibration=JsonUtility.ToJson(game.calibration)};
                Start("GPS: new validated copies for the authored calibration (originals untouched)");
                expected.guides=GuideCopies(game.calibration);
                // Edit Mode preview poses, for the Play comparison in Verify.
                w=rig.Collect();
                try
                {
                    foreach(float lift in Lifts)
                    {
                        IceMechanicalAlignment.Preview(w,game.calibration,lift,0);
                        foreach(var (name,t) in Tracked(w))expected.preview.Add(new PoseRow{part=name,lift=lift,machine=w.Machine.InverseTransformPoint(t.position)});
                    }
                }
                finally{IceMechanicalAlignment.EndPreview();}
                Check(Hash(IceMachineBaker.AuthoredScenePath)==expected.sceneHash,"previewing after the save does not dirty the saved scene");
                File.WriteAllText(Out+"expected.json",JsonUtility.ToJson(expected,true));
                Exit(0);
            });
        }

        static List<string> GuideCopies(SimulatorConfig current)
        {
            string root=RouteRecordingStorage.DefaultDirectory,guides=Path.Combine(root,"Guides");
            var originals=new Dictionary<string,string>();
            foreach(var f in Directory.GetFiles(root,"*.json",SearchOption.AllDirectories))originals[f]=Hash(f);
            var made=new List<string>();var routes=new HashSet<string>();
            foreach(string path in Directory.GetFiles(guides,"*.guide.json").Concat(Directory.GetFiles(Path.Combine(guides,"AutoFit"),"*.guide.json")).OrderBy(p=>p))
            {
                var g=TrainingGuideStorage.Read(path);
                var diff=new RouteFit{configuration=g.configuration}.CalibrationDifferences(current);
                Info("GPS before | "+Path.GetFileName(path)+" | "+(diff.Count==0?"COMPATIBLE":"INCOMPATIBLE: "+string.Join("; ",diff)));
                // Only guides that were made for the authored machine (DrawbarLength 0.435) get a copy, once per distinct route.
                if(Math.Abs(g.configuration.DrawbarLength-current.DrawbarLength)>.0001f)continue;
                string route=g.sourceSha256+JsonUtility.ToJson(new ControlList{controls=g.controls});
                if(!routes.Add(route)){Info("GPS skip | "+Path.GetFileName(path)+" | same route and recording as a guide already copied");continue;}
                try
                {
                    var copy=TrainingGuideStorage.RevalidateCopy(g,current,out var fit);
                    const string suffix=" / outline calibration";
                    string name=g.name.Replace(" / authored Aurora calibration","");
                    copy.name=name.Substring(0,Math.Min(name.Length,100-suffix.Length))+suffix;
                    string saved=TrainingGuideStorage.SaveCopy(guides,copy);made.Add(saved);
                    Info("GPS copy | "+Path.GetFileName(saved)+" from "+Path.GetFileName(path)+" | ready="+fit.Ready+" boundary="+fit.boundaryErrors+" curvature="+fit.curvatureErrors+
                        " recorded="+fit.recordedSamples+" length "+fit.Length.ToString("F3")+" m | compatible="+fit.Compatible(current));
                }
                catch(Exception e)
                {
                    Info("GPS NOT revalidated | "+Path.GetFileName(path)+" | "+e.Message);Clearance(g,current);
                    string repaired=RepairedCopy(g,current,guides);
                    if(repaired!=null)made.Add(repaired);
                }
            }
            foreach(var f in originals)Check(File.Exists(f.Key)&&Hash(f.Key)==f.Value,"original unchanged: "+f.Key.Substring(root.Length));
            return made.Select(Path.GetFileName).ToList();
        }

        /// <summary>
        /// The Training Route Editor's own repair, applied minimally to a NEW copy: only the control points of the
        /// failing sections move straight away from the boards, by the smallest of 15/20/30/40/60 mm that makes the
        /// whole guide pass the full clearance, curvature and compatibility checks. The original is never edited.
        /// </summary>
        static string RepairedCopy(TrainingRouteDefinition g,SimulatorConfig current,string guides)
        {
            var probe=TrainingGuideStorage.Decode(TrainingGuideStorage.Encode(g));probe.configuration=TrainingRouteDefinition.CopyConfiguration(current);
            var fit=RouteFitter.Fit(probe,TrainingGuideStorage.Raw(probe));
            var points=new SortedSet<int>();
            foreach(var sample in fit.samples)if(sample.boundaryError){points.Add(sample.control);if(sample.control+1<probe.controls.Count)points.Add(sample.control+1);}
            points.Remove(0);points.Remove(probe.controls.Count-1);
            if(points.Count==0||points.Count>12){Info("GPS repair | "+g.name+" | not attempted ("+points.Count+" control points involved)");return null;}
            var ice=new Simulation(probe.configuration,true).Ice;
            foreach(float offset in new[]{.015f,.02f,.03f,.04f,.06f})
            {
                var copy=TrainingGuideStorage.Decode(TrainingGuideStorage.Encode(g));copy.configuration=TrainingRouteDefinition.CopyConfiguration(current);
                copy.id=Guid.NewGuid().ToString("N");copy.createdUtc=DateTimeOffset.UtcNow.ToString("O");
                var moved=new List<string>();
                foreach(int i in points)
                {
                    var p=copy.controls[i];ice.BoardDistance(p.x,p.z,out float nx,out float nz);
                    RecordedTrackReconstruction.DetachPoint(copy,i);
                    p.x+=nx*offset;p.z+=nz*offset;moved.Add("#"+i+" (seq "+p.sourceSequence+")");
                }
                var f=RouteFitter.Fit(copy,TrainingGuideStorage.Raw(copy));
                Info("GPS repair try | "+offset*1000+" mm | ready="+f.Ready+" boundary="+f.boundaryErrors+" curvature="+f.curvatureErrors);
                if(!f.Ready||!f.validationPerformed||!f.Compatible(current))continue;
                string suffix=" / outline calibration (repaired "+(offset*1000).ToString("F0")+" mm)";
                string name=g.name.Replace(" / authored Aurora calibration","");
                copy.name=name.Substring(0,Math.Min(name.Length,100-suffix.Length))+suffix;
                string saved=TrainingGuideStorage.SaveCopy(guides,copy);
                Info("GPS repaired copy | "+Path.GetFileName(saved)+" | control points "+string.Join(", ",moved)+" moved "+(offset*1000).ToString("F0")+
                    " mm away from the boards; the other "+(copy.controls.Count-moved.Count)+" of "+copy.controls.Count+" points and the embedded recording are unchanged | length "+f.Length.ToString("F3")+
                    " m (raw "+TrainingGuideStorage.Raw(copy).distanceMeters.ToString("F3")+" m), max deviation from the recorded track "+(f.maximumRawDeviation*1000).ToString("F0")+" mm");
                return saved;
            }
            Info("GPS repair | "+g.name+" | no minimal repair passed; repair it in the Training Route Editor");
            return null;
        }

        /// <summary>Which part of the drawn machine a guide's recorded poses push into the boards, and how far.</summary>
        static void Clearance(TrainingRouteDefinition g,SimulatorConfig current)
        {
            var raw=TrainingGuideStorage.Raw(g);var s=new Simulation(TrainingRouteDefinition.CopyConfiguration(current),true);
            int failing=0;float deepest=0;string where="";var parts=new Dictionary<string,int>();
            foreach(var p in raw.samples)
            {
                if(s.ProbeVehiclePose(p.vehicleX,p.vehicleZ,p.vehicleHeadingRadians,out var hit))continue;
                failing++;
                float sn=Mathf.Sin(p.vehicleHeadingRadians),cs=Mathf.Cos(p.vehicleHeadingRadians),depth=0;string part="";
                for(int k=0;k<s.CollisionSampleCount;k++)
                {
                    s.CollisionSample(k,out float lx,out float lz);
                    float x=p.vehicleX+cs*lx+sn*lz,z=p.vehicleZ-sn*lx+cs*lz;
                    if(s.Navigable(x,z))continue;
                    float d=s.Ice.BoardDistance(x,z,out _,out _)+.02f;
                    if(d>depth){depth=d;part=lz< -(current.HitchBehindAxle+current.DrawbarLength)+current.ConditionerCollisionFront+.001f&&Mathf.Abs(lx)>1f?"conditioner side":"body";}
                }
                if(hit.GuideRoller)part="guide roller";
                parts[part]=parts.TryGetValue(part,out int n)?n+1:1;
                if(depth>deepest){deepest=depth;where="sample "+p.sequence+" at ("+p.vehicleX.ToString("F2")+", "+p.vehicleZ.ToString("F2")+")";}
            }
            Info("GPS clearance | "+g.name+" | "+failing+"/"+raw.samples.Count+" recorded poses touch boards/walls with the drawn envelope; deepest "+(deepest*1000).ToString("F0")+
                " mm past the 2 cm driving margin ("+where+"); by part: "+string.Join(", ",parts.Select(k=>k.Key+" x"+k.Value)));
        }

        // ---- Verify -------------------------------------------------------------------------------

        public static void Verify()
        {
            Begin("Verify",()=>
            {
                var expected=JsonUtility.FromJson<Expected>(File.ReadAllText(Out+"expected.json"));
                var rig=OpenScene();var game=Game();var w=rig.Collect();
                Start("Persistence after restarting Unity");
                Check(Hash(IceMachineBaker.PrefabPath)==expected.prefabHash&&Hash(IceMachineBaker.AuthoredScenePath)==expected.sceneHash&&
                    Hash(Root+"Machine/AuroraR54.Meshes.asset")==expected.meshesHash,"prefab, scene and meshes are exactly as saved");
                Check(!EditorSceneManager.GetActiveScene().isDirty,"opening the scene does not dirty it");
                Check(JsonUtility.ToJson(game.calibration)==expected.calibration,"scene calibration (with collision outline) persisted");
                foreach(var item in IceMechanicalAlignment.Measure(w,game.calibration))
                    Info("alignment "+item.Label+": "+item.Kind+" "+item.ErrorMm.ToString("F1")+" mm "+item.Detail);
                RollerHardware(w,game.calibration);
                CalibrationContext(rig,game);
                PreviewChecks(rig,game,expected);
                BrushAuthoring(rig,game);
                Check(Hash(IceMachineBaker.AuthoredScenePath)==expected.sceneHash&&Hash(IceMachineBaker.PrefabPath)==expected.prefabHash,"Edit Mode checks leave the saved scene and prefab byte-identical");
                Play();
            });
        }

        static void CalibrationContext(IceMachineRig rig,IceResurfacerGame game)
        {
            Start("Calibration context (Inspector source)");
            var scene=IceCalibrationContext.Resolve(rig,rig.BakedConfigJson);
            Info("scene: "+scene.Label);
            Check(scene.Source==IceCalibrationContext.Source.AuthoredScene&&scene.Writable&&scene.Config==game.calibration,"scene instance measures against the live authored scene calibration");
            // Prefab Mode equivalents: the prefab as an asset (its stage resolves the same way through the asset path).
            var contents=PrefabUtility.LoadPrefabContents(IceMachineBaker.PrefabPath);
            try
            {
                var prefabRig=contents.GetComponent<IceMachineRig>();
                var context=IceCalibrationContext.Resolve(prefabRig,prefabRig.BakedConfigJson);
                Info("prefab contents: "+context.Label+" ("+context.Source+")");
                // LoadPrefabContents is neither a stage nor an asset: it falls back, and must say so.
                Check(context.Source==IceCalibrationContext.Source.PrefabFallback&&!context.Writable,"detached prefab contents fall back to the bake-time configuration, read-only and labelled");
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);}
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(IceMachineBaker.PrefabPath).GetComponent<IceMachineRig>();
            var fromAsset=IceCalibrationContext.Resolve(asset,asset.BakedConfigJson);
            Info("prefab asset: "+fromAsset.Label+" ("+fromAsset.Source+")");
            Check(fromAsset.Source==IceCalibrationContext.Source.AuthoredSceneForPrefab&&!fromAsset.Writable&&JsonUtility.ToJson(fromAsset.Config)==JsonUtility.ToJson(game.calibration),
                "the prefab asset is measured against the open authored scene's calibration (read-only)");
            var stage=PrefabStageUtility.OpenPrefab(IceMachineBaker.PrefabPath);
            try
            {
                var stageRig=stage.prefabContentsRoot.GetComponent<IceMachineRig>();var sw=stageRig.Collect();
                var context=IceCalibrationContext.Resolve(stageRig,stageRig.BakedConfigJson);
                Info("Prefab Mode: "+context.Label+" ("+context.Source+")");
                Check(context.Trustworthy&&!context.Writable&&JsonUtility.ToJson(context.Config)==JsonUtility.ToJson(game.calibration),"Prefab Mode measures against the authored scene calibration, read-only");
                var items=IceMechanicalAlignment.Measure(sw,context.Config);
                foreach(var i in items)Info("Prefab Mode alignment "+i.Label+": "+i.Kind+" "+i.ErrorMm.ToString("F1")+" mm");
                Check(items.All(i=>i.Kind==IceMechanicalAlignment.Kind.Matches||i.Kind==IceMechanicalAlignment.Kind.Info),"Prefab Mode shows no false-positive discrepancies (hitch, tank, conveyor, brush, outline, roller all match)");
                var brush=sw.BrushArm.GetComponent<IceBoardBrushRig>();
                Check(brush.mechanism.Matches(IceBoardBrushRig.Mechanism.From(context.Config)),"Prefab Mode brush rig: draft matches the scene calibration (no 'not applied' warning)");
                // Lift preview in Prefab Mode: same conditioner pose as the scene (no 215 mm jump).
                IceMechanicalAlignment.Preview(sw,context.Config,1,0);
                float z=sw.Machine.InverseTransformPoint(sw.Conditioner.position).z;
                IceMechanicalAlignment.EndPreview();
                Check(Mathf.Abs(z-(-(game.calibration.HitchBehindAxle+game.calibration.DrawbarLength)))<.0005f,"Prefab Mode lift preview puts the conditioner where Play does (z "+z.ToString("F3")+")");
                Check(!stage.scene.isDirty,"Prefab Mode inspection and preview do not dirty the prefab");
            }
            finally{StageUtility.GoToMainStage();}
        }

        static List<(string,Transform)> Tracked(IceWorld w)
        {
            var list=new List<(string,Transform)>();
            var grips=GripTransforms(w);
            for(int i=1;i<=8;i++)list.Add((Names[i]+" grip",grips[i]));
            list.Add(("Conditioner root",w.Conditioner));list.Add(("Blade handwheel assembly",w.BladeWheel.parent));
            list.Add(("Flood valve bearing",w.FloodValveMount));list.Add(("Wash valve bearing",w.WashValveMount));
            for(int i=0;i<2;i++){list.Add(("Conditioner cylinder rod "+i,w.ConditionerCylinderRods[i]));list.Add(("Conditioner cylinder barrel "+i,w.ConditionerCylinderBarrels[i]));}
            int n=0;foreach(var span in IceConditionerLinkage.Capture(w))list.Add(("Span "+(n++)+" "+span.Part.name,span.Part));
            return list;
        }
        static readonly string[] Names={"","Conditioner hydraulic lever","Horizontal auger lever","Vertical auger lever","Snow tank hydraulic lever","Flood water valve","Wash water valve","Blade handwheel","Snow-breaker plunger"};
        static readonly string[] GripParts={"","Black phenolic hydraulic grip","Black phenolic hydraulic grip","Black phenolic hydraulic grip","Black phenolic hydraulic grip","Valve grip","Valve grip","Handwheel crank","Snow breaker T handle"};
        static Transform For(IceWorld w,int i)
        {
            switch(i){case 1:return w.ConditionerLever;case 2:return w.HorizontalAugerLever;case 3:return w.VerticalAugerLever;case 4:return w.DumpLever;
                case 5:return w.FloodLever;case 6:return w.WashValve;case 7:return w.BladeWheel;default:return w.SnowBreakerPlunger;}
        }
        static Transform[] GripTransforms(IceWorld w)
        {
            var list=new Transform[9];
            for(int i=1;i<=8;i++){list[i]=For(w,i);foreach(var t in For(w,i).GetComponentsInChildren<Transform>(true))if(t.name==GripParts[i]){list[i]=t;break;}}
            return list;
        }

        static void PreviewChecks(IceMachineRig rig,IceResurfacerGame game,Expected expected)
        {
            Start("Edit Mode lift preview: 0/25/50/75/100%, connections, never saved");
            var w=rig.Collect();var c=game.calibration;Transform m=w.Machine;
            string rest=Snapshot(rig.transform);
            var spans=IceConditionerLinkage.Capture(w);
            Check(spans.Count==4,"four members span the lift (2 lift linkage bars, 2 hydraulic hoses): "+string.Join(", ",spans.Select(s=>s.Part.name)));
            try
            {
                foreach(float lift in Lifts)
                {
                    IceMechanicalAlignment.Preview(w,c,lift,0);
                    Vector3 root=m.InverseTransformPoint(w.Conditioner.position);
                    Check(Mathf.Abs(root.y-lift*IceConditionerMotion.LiftTravel)<.0005f&&Mathf.Abs(root.z+c.HitchBehindAxle+c.DrawbarLength)<.0005f,"preview "+(lift*100)+"%: conditioner at the simulated hitch and lift");
                    foreach(var s in spans)
                    {
                        Vector3 half=s.Part.up*s.Part.lossyScale.y,a=s.Part.position-half,b=s.Part.position+half;
                        Vector3 chassis=m.TransformPoint(s.ChassisEnd),cond=w.Conditioner.TransformPoint(s.ConditionerEnd);
                        float gap=Mathf.Max(Mathf.Min(Vector3.Distance(a,chassis),Vector3.Distance(b,chassis)),Mathf.Min(Vector3.Distance(a,cond),Vector3.Distance(b,cond)));
                        Check(gap<.001f,"preview "+(lift*100)+"%: "+s.Part.name+" stays pinned to chassis and conditioner ("+(gap*1000).ToString("F2")+" mm, length "+(2*s.Part.lossyScale.y).ToString("F3")+" m)");
                    }
                    for(int i=0;i<2;i++)
                    {
                        var rod=w.ConditionerCylinderRods[i];Vector3 tip=rod.position+rod.up*rod.lossyScale.y;
                        Check(Vector3.Distance(tip,w.ConditionerCylinderClevis[i].position)<.001f,"preview "+(lift*100)+"%: lift cylinder "+i+" rod on its clevis");
                    }
                    var grips=IcePhysicalControls.GripPositions(w,c);
                    foreach(int i in new[]{5,6,7})
                    {
                        var e=expected.preview.First(r=>r.part==Names[i]+" grip"&&Mathf.Abs(r.lift-lift)<1e-4f);
                        Info("preview "+(lift*100)+"% "+Names[i]+" grip machine "+V(m.InverseTransformPoint(GripTransforms(w)[i].position))+" (saved at Apply "+V(e.machine)+")");
                    }
                    if(lift==0||lift==.5f||lift==1)Shoot("Edit-Preview-Lift"+(lift*100).ToString("F0")+"-Side",m,m.TransformPoint(5,1.3f,-.7f),m.TransformPoint(0,1.3f,-.7f),1.25f);
                }
            }
            finally{IceMechanicalAlignment.EndPreview();}
            Check(Snapshot(rig.transform)==rest,"ending the preview restores every part exactly");
            // Saving during a preview saves the authored pose.
            IceMechanicalAlignment.Preview(w,c,.25f,0);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Check(!IceMechanicalAlignment.Previewing&&Snapshot(rig.transform)==rest,"saving during a preview restores the authored pose first");
            Check(Hash(IceMachineBaker.AuthoredScenePath)==expected.sceneHash,"the scene saved during a preview is byte-identical");
            // Undo/Redo during a preview restores the authored pose (an unrelated undoable change is undone).
            float eye=game.operatorEyeHeight;
            Undo.RecordObject(game,"Validation change");game.operatorEyeHeight=eye+.01f;Undo.FlushUndoRecordObjects();Undo.IncrementCurrentGroup();
            IceMechanicalAlignment.Preview(w,c,.5f,0);
            Undo.PerformUndo();
            Check(!IceMechanicalAlignment.Previewing&&Snapshot(rig.transform)==rest&&Mathf.Abs(game.operatorEyeHeight-eye)<1e-6f,"Undo during a preview undoes the edit and returns to the authored pose");
            // A manual (Inspector) edit of a posed part is not applied.
            IceMechanicalAlignment.Preview(w,c,.75f,0);
            int warnings=0;void Count(string text,string stack,LogType type){if(type==LogType.Warning&&text.StartsWith("Lift preview ended"))warnings++;}
            Application.logMessageReceived+=Count;
            var so=new SerializedObject(w.Conditioner);so.FindProperty("m_LocalPosition").vector3Value+=Vector3.up*.1f;so.ApplyModifiedProperties();
            Application.logMessageReceived-=Count;
            IceMechanicalAlignment.EndPreview();
            Info("Inspector edit during preview: refusal warnings logged "+warnings);
            var overrides=PrefabUtility.GetPropertyModifications(rig.gameObject).Count(m=>m.target==PrefabUtility.GetCorrespondingObjectFromSource(w.Conditioner));
            Check(Snapshot(rig.transform)==rest&&overrides==0,"an Inspector edit of a posed part during the preview is not kept: the authored pose returns and no override is left on it");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Check(Hash(IceMachineBaker.AuthoredScenePath)==expected.sceneHash,"scene is byte-identical after the preview safety checks");
        }

        static void BrushAuthoring(IceMachineRig rig,IceResurfacerGame game)
        {
            Start("Board brush authoring: draft vs simulation, reach, apply and restore");
            var w=rig.Collect();var brush=w.BrushArm.GetComponent<IceBoardBrushRig>();var c=game.calibration;
            var original=IceBoardBrushRig.Mechanism.From(c);
            Check(brush.mechanism.Matches(original),"the authored brush draft equals the scene calibration (the earlier 'not applied' warning was Prefab Mode context)");
            float visual=IceBoardBrushRigEditor.VisualRadius(brush);
            Check(Mathf.Abs(visual-c.BrushRadius)<.001f,"snow-clearing radius "+c.BrushRadius.ToString("F3")+" m equals the visible bristles "+visual.ToString("F3")+" m");
            foreach(var line in IceBoardBrushRigEditor.Diagnose(brush,c,brush.mechanism))Info("brush: "+line.Key+" "+line.Value);
            float side=IceBoardBrushRigEditor.CollisionSide(c,-1,out string part);
            Vector3 work=IceBoardBrushRig.ContactLocal(brush.mechanism,1);
            float reach=Mathf.Abs(work.x)+brush.mechanism.Radius-side;
            Info("brush reach: working disc edge |x| "+(Mathf.Abs(work.x)+brush.mechanism.Radius).ToString("F3")+" m vs outermost left collision point "+side.ToString("F3")+" m ("+part+"): "+(reach*1000).ToString("F0")+" mm");
            Check(reach>0,"the working brush reaches past the outermost collision point on its side, so the bristles can reach the kickplate");
            // Bring the brush 0.10 m inward and apply: the simulation follows the approved contact.
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            Undo.RecordObject(brush,"Move brush inward");
            brush.mechanism.PivotX+=.10f;
            Check(IceBoardBrushRigEditor.ApplyToSimulation(brush,game,out string error),"Apply Brush Geometry to Simulation (inward test)"+(error!=null?": "+error:""));
            Check(Mathf.Abs(c.BrushPivotX-(original.PivotX+.10f))<1e-5f,"the scene calibration now holds the inward pivot");
            var sim=new Simulation(JsonUtility.FromJson<SimulatorConfig>(JsonUtility.ToJson(c)),true);
            sim.Controls.BoardBrush=true;for(int i=0;i<200;i++)sim.Step(.02f);
            BoardBrushArticulation.Contact(sim.Config,1,out float lx,out float lz);
            Check(Mathf.Abs(lx-(work.x+.10f))<1e-4f,"the simulated sweep follows the new contact point ("+lx.ToString("F3")+" m, was "+work.x.ToString("F3")+" m)");
            Undo.RevertAllDownToGroup(group);
            Check(brush.mechanism.Matches(original)&&IceBoardBrushRig.Mechanism.From(c).Matches(original),"Undo reverts both the draft and the scene calibration");
            IceBoardBrushRigEditor.Repose(brush);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Shoot("Brush-Edit-Stowed-Top",w.Machine,w.Machine.TransformPoint(-.8f,4,1.2f),w.Machine.TransformPoint(-.8f,0,1.2f),.9f);
            IceBoardBrushRigEditor.SetPreview(brush,1);
            Shoot("Brush-Edit-Working-Top",w.Machine,w.Machine.TransformPoint(-.8f,4,1.2f),w.Machine.TransformPoint(-.8f,0,1.2f),.9f);
            IceBoardBrushRigEditor.SetPreview(brush,0);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        // ---- Play ---------------------------------------------------------------------------------

        static IceControlsBrushRollerValidation()
        {
            if(Application.isBatchMode&&SessionState.GetString(StageKey,"")!="")
            { EditorApplication.update-=Tick;EditorApplication.update+=Tick; }
        }

        static void Play()
        {
            SessionState.SetString(StageKey,"play");frame=0;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string stack,LogType type){ if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)errors++; }

        static void Tick()
        {
            string stage=SessionState.GetString(StageKey,"");
            Run(()=>
            {
                if(stage=="play")
                {
                    if(!EditorApplication.isPlaying||++frame<30)return;
                    var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
                    if(game==null||game.World==null)return;
                    Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
                    PlayChecks(game);
                    Check(errors==0,"zero Unity runtime errors or exceptions in Play ("+errors+")");
                    SessionState.SetString(StageKey,"exit");
                    EditorApplication.ExitPlaymode();
                }
                else if(stage=="exit"&&!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
                    var expected=JsonUtility.FromJson<Expected>(File.ReadAllText(Out+"expected.json"));
                    Check(Hash(IceMachineBaker.PrefabPath)==expected.prefabHash&&Hash(IceMachineBaker.AuthoredScenePath)==expected.sceneHash&&
                        Hash(Root+"Machine/AuroraR54.Meshes.asset")==expected.meshesHash,"entering and exiting Play leaves the prefab, meshes and scene byte-identical");
                    Info("TOTAL checks passed: "+File.ReadAllLines(ResultsPath).Count(l=>l.StartsWith("PASS ")));
                    Exit(0);
                }
            });
        }

        static void Render(IceResurfacerGame game,float dt)
        { typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{dt}); }

        static void Park(Simulation s,float x,float z,float heading,float speed=0)
        {
            s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=heading;s.Vehicle.Speed=speed;s.Vehicle.SteeringAngle=0;
            float behind=s.Config.HitchBehindAxle+s.Config.DrawbarLength;
            s.Conditioner.Heading=heading;s.Conditioner.X=x-Mathf.Sin(heading)*behind;s.Conditioner.Z=z-Mathf.Cos(heading)*behind;
            s.Controls.HydraulicHoldControls=false;s.Controls.Throttle=0;s.Controls.Steer=0;s.Controls.Brake=false;
        }

        static void PlayChecks(IceResurfacerGame game)
        {
            var expected=JsonUtility.FromJson<Expected>(File.ReadAllText(Out+"expected.json"));
            bool enabled=game.enabled;game.enabled=false;
            Keyboard keys=null;Mouse mouse=null;
            // A batch-mode Game view never has focus: route the virtual devices to it regardless (as RunBatch does).
            var originalInput=InputSystem.settings;var validationInput=UnityEngine.Object.Instantiate(originalInput);
            validationInput.hideFlags=HideFlags.HideAndDontSave;
            validationInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            validationInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings=validationInput;
            try
            {
                keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
                InputSystem.QueueStateEvent(keys,new KeyboardState());InputSystem.Update();
                game.NewSession(true);var s=game.Sim;var w=game.World;Transform m=w.Machine;
                Park(s,0,-5,0);s.Step(.02f);
                Start("Play poses equal the Edit Mode preview at every lift");
                float worst=0;string worstPart="";
                foreach(float lift in Lifts)
                {
                    s.Conditioner.Lift=lift;Render(game,0);
                    foreach(var (name,t) in Tracked(w))
                    {
                        var e=expected.preview.FirstOrDefault(r=>r.part==name&&Mathf.Abs(r.lift-lift)<1e-4f);if(e==null)continue;
                        float d=Vector3.Distance(e.machine,m.InverseTransformPoint(t.position));
                        if(d>worst){worst=d;worstPart=name+" at "+(lift*100)+"%";}
                    }
                }
                Check(worst<.0005f,"every tracked part (8 grips, conditioner, bearings, cylinders, linkage, hoses) is where the preview showed it: worst "+(worst*1000).ToString("F2")+" mm ("+worstPart+")");
                Controls(game,mouse);
                Roller(game);
                BrushInPlay(game);
                GuideCheck(game);
                Captures(game,mouse);
            }
            finally
            {
                if(keys!=null)InputSystem.RemoveDevice(keys);if(mouse!=null)InputSystem.RemoveDevice(mouse);
                InputSystem.settings=originalInput;UnityEngine.Object.DestroyImmediate(validationInput);
                game.NewSession(true);game.enabled=enabled;
            }
        }

        static void Pointer(IceResurfacerGame game,Mouse mouse,Vector2 p,bool down=false)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p,buttons=(ushort)(down?1:0)});
            InputSystem.Update();game.PhysicalControls.UpdateInput(true,false);
        }

        static void Aim(IceResurfacerGame game,Vector3 at)
        {
            var cam=game.DriverCamera;
            cam.transform.localPosition=new Vector3(0,game.operatorEyeHeight,0);
            cam.transform.rotation=Quaternion.LookRotation(at-cam.transform.position,game.World.Machine.up);
        }

        static void Controls(IceResurfacerGame game,Mouse mouse)
        {
            Start("Play: conditioner-mounted controls at 0/25/50/75/100% lift");
            var controls=game.PhysicalControls;var cam=game.DriverCamera;
            var highlight=(Renderer[][])typeof(IcePhysicalControls).GetField("highlightRenderers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controls);
            for(int i=5;i<=7;i++)Check(highlight[i].Length>0&&highlight[i].All(r=>r!=null&&r.enabled),Names[i]+": its own renderers survive runtime batching ("+highlight[i].Length+"), so it can turn and highlight");
            foreach(float lift in Lifts)
            {
                game.NewSession(true);var s=game.Sim;var w=game.World;Park(s,0,-5,0);s.Step(.02f);
                s.Conditioner.Lift=lift;Render(game,0);
                var grips=GripTransforms(w);
                // Hover and pick each control at its visible grip; a ring around it picks nothing else.
                for(int i=1;i<=8;i++)
                {
                    Aim(game,grips[i].position);
                    Vector2 screen=cam.WorldToScreenPoint(grips[i].position);
                    Pointer(game,mouse,screen);
                    Check(controls.HighlightedControl==Names[i],"lift "+(lift*100)+"%: hovering the visible "+Names[i]+" grip highlights it ("+controls.HighlightedControl+")");
                    if(i>=5&&i<=7)
                    {
                        var tint=new MaterialPropertyBlock();highlight[i][0].GetPropertyBlock(tint);
                        Check(tint.HasColor(Shader.PropertyToID("_BaseColor")),"lift "+(lift*100)+"%: "+Names[i]+" renderers are tinted while hovered");
                    }
                    var stolen=new List<string>();
                    for(int k=0;k<12;k++)
                    {
                        float a=k*Mathf.PI/6;Vector2 p=screen+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*6;
                        string hit=controls.GetHitControl(p);if(hit!=""&&hit!=Names[i])stolen.Add(hit);
                    }
                    Check(stolen.Count==0,"lift "+(lift*100)+"%: no other control steals clicks within 6 px of the "+Names[i]+" grip"+(stolen.Count>0?" ("+string.Join(", ",stolen.Distinct())+")":""));
                    Pointer(game,mouse,Vector2.zero);
                }
                // Turn both valves a quarter of their travel and the handwheel an eighth of a turn, at this lift.
                foreach(int i in new[]{5,6,7})
                {
                    // Each control from closed/zero, so an already-turned neighbour is not in front of it.
                    game.NewSession(true);s=game.Sim;w=game.World;Park(s,0,-5,0);s.Step(.02f);s.Conditioner.Lift=lift;Render(game,0);grips=GripTransforms(w);
                    var t=For(w,i);Vector3 pivot=t.position,grip=grips[i].position;Vector3 axis=t.parent.rotation*Vector3.up;
                    Aim(game,grip);
                    float before=Setting(game.Sim,i);
                    Bounds visualBefore=highlight[i][0].bounds;
                    Pointer(game,mouse,cam.WorldToScreenPoint(grip));Pointer(game,mouse,cam.WorldToScreenPoint(grip),true);
                    Check(controls.ActiveControl==Names[i],"lift "+(lift*100)+"%: clicking the visible "+Names[i]+" grabs it (grabbed '"+controls.ActiveControl+"', hover '"+controls.HighlightedControl+"')");
                    // One pointer event per frame, the handle redrawn in between, as in Play.
                    for(int k=1;k<=15;k++){Pointer(game,mouse,cam.WorldToScreenPoint(pivot+Quaternion.AngleAxis((i==7?3:-3)*k,axis)*(grip-pivot)),true);Render(game,0);}
                    float after=Setting(game.Sim,i);
                    Pointer(game,mouse,cam.WorldToScreenPoint(grip));
                    float expected=i==7?45f/360f/game.Sim.Config.BladeWheelTurns*game.Sim.Config.MaxBladeDepthMm:.5f;
                    Check(Mathf.Abs(after-before-expected)<expected*.04f,"lift "+(lift*100)+"%: dragging the "+Names[i]+" grip 45 deg about its own axis sets "+(after-before).ToString("F4")+" (expected "+expected.ToString("F4")+")");
                    Check(Vector3.Distance(highlight[i][0].bounds.center,visualBefore.center)>.002f||i==7,"lift "+(lift*100)+"%: the visible "+Names[i]+" handle turns with it");
                    // A slow frame: the same 45 deg arrives as one pointer jump.
                    before=Setting(game.Sim,i);grip=GripTransforms(w)[i].position;
                    Pointer(game,mouse,cam.WorldToScreenPoint(grip));Pointer(game,mouse,cam.WorldToScreenPoint(grip),true);
                    string held=controls.ActiveControl;Vector3 target=pivot+Quaternion.AngleAxis(i==7?45:-45,axis)*(grip-pivot);
                    Pointer(game,mouse,cam.WorldToScreenPoint(target),true);Render(game,0);
                    after=Setting(game.Sim,i);Pointer(game,mouse,cam.WorldToScreenPoint(grip));
                    float edge=Mathf.Abs(Vector3.Dot(cam.ScreenPointToRay(cam.WorldToScreenPoint(target)).direction,axis));
                    string slow="lift "+(lift*100)+"%: the same 45 deg turn in a single slow frame sets "+(after-before).ToString("F4")+" (expected "+expected.ToString("F4")+"; held '"+held+"', view |cos| "+edge.ToString("F3")+")";
                    // Within about 6 deg of edge-on, both sides of the handle's path lie within a few pixels: one jump
                    // round the end of the stroke cannot be told from a short move. The hand test below follows it.
                    if(edge>=.1f)Check(Mathf.Abs(after-before-expected)<expected*.08f,slow);
                    else Info(slow+" - edge-on view, a single jump round the end of the stroke is ambiguous (not a check)");
                }
                float gripY=game.World.Machine.InverseTransformPoint(grips[5].position).y;
                if(lift==0)baseGripY=gripY;
                Check(Mathf.Abs(gripY-baseGripY-lift*IceConditionerMotion.LiftTravel)<.001f,"lift "+(lift*100)+"%: the valve grip rides "+(lift*IceConditionerMotion.LiftTravel*1000).ToString("F0")+" mm up with the conditioner");
            }
            // Holding a control while the conditioner moves under a still pointer.
            foreach(int i in new[]{5,6,7})
            {
                game.NewSession(true);var s=game.Sim;var w=game.World;Park(s,0,-5,0);s.Step(.02f);
                s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;
                if(i==5)s.Controls.FloodFlow=.5f;else if(i==6)s.Controls.WashValve=.5f;else s.Controls.BladeDepthMm=.4f;
                Render(game,0);
                var grip=GripTransforms(w)[i];Aim(game,grip.position);
                // Grab it where it is visible from the eye: the grip, or (handwheel) any point of its rim.
                Vector2 screen=cam.WorldToScreenPoint(grip.position);
                var candidates=new List<Vector3>{grip.position};
                if(i==7)for(int k=0;k<12;k++){float a=k*Mathf.PI/6;candidates.Add(w.BladeWheel.TransformPoint(Mathf.Cos(a)*.13f,0,Mathf.Sin(a)*.13f));}
                foreach(var point in candidates){Vector2 p=cam.WorldToScreenPoint(point);if(controls.GetHitControl(p)==Names[i]){screen=p;break;}}
                Pointer(game,mouse,screen);Pointer(game,mouse,screen,true);
                Check(controls.ActiveControl==Names[i],"hold: "+Names[i]+" grabbed at 0% lift (grabbed '"+controls.ActiveControl+"', hover '"+controls.HighlightedControl+"', pick '"+controls.GetHitControl(screen)+
                    "', grip machine "+V(w.Machine.InverseTransformPoint(grip.position))+", eye distance "+Vector3.Distance(cam.transform.position,grip.position).ToString("F2")+" m, viewport "+cam.WorldToViewportPoint(grip.position).ToString("F2")+")");
                float before=Setting(s,i);
                for(int k=1;k<=25;k++){s.Conditioner.Lift=k/25f;Render(game,0);Pointer(game,mouse,screen,true);}
                float after=Setting(s,i);
                Pointer(game,mouse,screen);
                Check(Mathf.Abs(after-before)<.0005f,"hold: the "+Names[i]+" setting does not change while the conditioner rises 0->100% under a still pointer ("+before.ToString("F3")+" -> "+after.ToString("F3")+")");
            }
            // A hand on a mouse: the pointer follows the visible grip in 4 deg steps with up to 2 px of jitter, at
            // the lifts where the handles sit well below, and level with, the seated eye.
            Info("driver camera "+cam.pixelWidth+" x "+cam.pixelHeight+" px");
            foreach(float lift in new[]{0f,.5f,1f})
            foreach(int i in new[]{5,6,7})
            {
                game.NewSession(true);var s=game.Sim;var w=game.World;Park(s,0,-5,0);s.Step(.02f);s.Conditioner.Lift=lift;Render(game,0);
                var t=For(w,i);var grip=GripTransforms(w)[i];Vector3 pivot=t.position,arm=grip.position-pivot,axis=t.parent.rotation*Vector3.up;
                Aim(game,grip.position);
                float elevation=Mathf.Asin(Mathf.Abs(Vector3.Dot((grip.position-cam.transform.position).normalized,axis)))*Mathf.Rad2Deg;
                var random=new System.Random(1000+i*10+(int)(lift*4));
                Vector2 At(float degrees){return (Vector2)cam.WorldToScreenPoint(pivot+Quaternion.AngleAxis(degrees,axis)*arm)+new Vector2((float)random.NextDouble()*4-2,(float)random.NextDouble()*4-2);}
                float before=Setting(s,i);float sign=i==7?1:-1;
                Vector2 start=cam.WorldToScreenPoint(grip.position);Pointer(game,mouse,start);Pointer(game,mouse,start,true);
                for(int k=1;k<=12;k++){Pointer(game,mouse,At(sign*4*k),true);Render(game,0);}
                float after=Setting(s,i);
                float expected=i==7?48f/360f/s.Config.BladeWheelTurns*s.Config.MaxBladeDepthMm:48f/90f;
                Check(Mathf.Abs(after-before-expected)<expected*.15f,"lift "+(lift*100)+"%: a jittery hand following the "+Names[i]+" grip 48 deg turns it "+(after-before).ToString("F4")+
                    " (expected "+expected.ToString("F4")+"; view "+elevation.ToString("F1")+" deg off its plane)");
                if(i!=7)
                {
                    // On past the open stop, then all the way back past the closed stop.
                    float open=0;
                    for(int k=13;k<=26;k++){Pointer(game,mouse,At(sign*4*k),true);Render(game,0);open=Mathf.Max(open,Setting(s,i));}
                    for(int k=25;k>=-2;k--){Pointer(game,mouse,At(sign*4*k),true);Render(game,0);}
                    Check(open>.999f&&Setting(s,i)<.001f,"lift "+(lift*100)+"%: the same hand turns the "+Names[i]+" to its open stop ("+open.ToString("F3")+") and back to closed ("+Setting(s,i).ToString("F3")+")");
                }
                Pointer(game,mouse,At(0));
            }
            // Handwheel end stops, conditioner lowered (where depth is set) and raised (wheel near eye height).
            foreach(float lift in new[]{0f,1f})
            {
                game.NewSession(true);var s=game.Sim;var w=game.World;Park(s,0,-5,0);s.Step(.02f);s.Conditioner.Lift=lift;Render(game,0);
                var t=w.BladeWheel;var knob=GripTransforms(w)[7];Vector3 pivot=t.position,arm=knob.position-pivot,axis=t.parent.rotation*Vector3.up;
                Aim(game,knob.position);
                Vector2 Around(float degrees){return cam.WorldToScreenPoint(pivot+Quaternion.AngleAxis(degrees,axis)*arm);}
                Pointer(game,mouse,Around(0));Pointer(game,mouse,Around(0),true);
                for(int k=1;k<=129;k++){Pointer(game,mouse,Around(10*k),true);Render(game,0);}
                float max=s.Controls.BladeDepthMm;
                for(int k=130;k<=135;k++){Pointer(game,mouse,Around(10*k),true);Render(game,0);}
                Check(Mathf.Abs(max-s.Config.MaxBladeDepthMm)<.0001f&&Mathf.Abs(s.Controls.BladeDepthMm-s.Config.MaxBladeDepthMm)<.0001f,
                    "lift "+(lift*100)+"%: turning the handwheel by its knob reaches the 3.5-turn stop ("+max.ToString("F3")+" mm) and holds it when turned further");
                Pointer(game,mouse,Around(1350));
                // Grab it again where it stopped and turn back one full turn.
                var stopped=GripTransforms(w)[7].position;arm=stopped-pivot;
                Pointer(game,mouse,Around(0));Pointer(game,mouse,Around(0),true);
                for(int k=1;k<=36;k++){Pointer(game,mouse,Around(-10*k),true);Render(game,0);}
                Pointer(game,mouse,Around(-360));
                float back=s.Config.MaxBladeDepthMm*(1-1f/s.Config.BladeWheelTurns);
                Check(Mathf.Abs(s.Controls.BladeDepthMm-back)<.02f,"lift "+(lift*100)+"%: one turn back from the stop leaves "+s.Controls.BladeDepthMm.ToString("F3")+" mm (expected "+back.ToString("F3")+")");
            }
            // E (Controls) view: every grip in frame; picks from that eye.
            foreach(float lift in new[]{0f,.5f,1f})
            {
                game.NewSession(true);var s=game.Sim;var w=game.World;Park(s,0,-5,0);s.Step(.02f);
                s.Conditioner.Lift=lift;Render(game,0);
                EView(game);
                var grips=GripTransforms(w);var picks=new List<string>();int inFrame=0;
                for(int i=1;i<=8;i++)
                {
                    Vector3 v=cam.WorldToViewportPoint(grips[i].position);if(v.z>0&&v.x>0&&v.x<1&&v.y>0&&v.y<1)inFrame++;
                    string hit=controls.GetHitControl(cam.WorldToScreenPoint(grips[i].position));
                    picks.Add(Names[i]+"->"+(hit==""?"none":hit));
                }
                Info("E view lift "+(lift*100)+"%: "+inFrame+"/8 grips in frame; picks "+string.Join("; ",picks));
                Check(inFrame==8,"E view at "+(lift*100)+"% lift frames all 8 grips");
            }
        }
        static float baseGripY;
        static float Setting(Simulation s,int i){ return i==5?s.Controls.FloodFlow:i==6?s.Controls.WashValve:s.Controls.BladeDepthMm; }

        static void EView(IceResurfacerGame game)
        {
            game.SelectCameraView("Controls");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            float yaw=(float)typeof(IceResurfacerGame).GetField("targetYaw",flags).GetValue(game),pitch=(float)typeof(IceResurfacerGame).GetField("targetPitch",flags).GetValue(game);
            Vector3 head=(Vector3)typeof(IceResurfacerGame).GetMethod("HeadOffset",flags).Invoke(game,new object[]{yaw});
            game.DriverCamera.transform.localPosition=head;game.DriverCamera.transform.localRotation=Quaternion.Euler(pitch,yaw,0);
        }

        // Board driving with the authored machine in Play (the game's own simulation, rendered each step).
        static void Roller(IceResurfacerGame game)
        {
            Start("Play: guide roller and board contact with the authored machine");
            var c=game.calibration;
            float radius=IceMechanicalAlignment.GuideRollerRadius(game.World);
            Check(Mathf.Abs(radius-c.GuideRollerRadius)<.001f,"the visible roller radius equals the simulated one ("+radius.ToString("F3")+" m)");
            foreach(var (label,angle,steer,throttle) in new[]{("parallel, light contact",0f,-.06f,1f),("parallel, resurfacing speed",0f,-.04f,.6f),("parallel, steering neutral",0f,0f,1f),
                ("2 deg",2f,-.3f,1f),("5 deg",5f,0f,1f),("10 deg",10f,-.3f,.6f),("15 deg",15f,-.3f,1f),
                ("25 deg",25f,0f,1f),("45 deg",45f,0f,1f),("head-on",90f,0f,1f),("full lock into the boards at 5 deg",5f,-1f,1f),("steer away at 5 deg",5f,.3f,1f)})
            {
                game.NewSession(true);var s=game.Sim;var w=game.World;
                // Parallel runs start with the roller just off (steered in) or just on (steering neutral) the board face.
                float heading=-angle*Mathf.Deg2Rad,x=PlaceX(s,-6,heading,angle==0?(steer==0?.0205f:.025f):.40f);
                float back=angle==0?0:Mathf.Min(4,.40f/Mathf.Sin(angle*Mathf.Deg2Rad)+.5f);
                Park(s,x+Mathf.Sin(-heading)*back,-6-Mathf.Cos(heading)*back,heading,throttle*c.ForwardSpeed);
                s.Conditioner.Lift=angle==10?0:1;s.Controls.ConditionerTarget=s.Conditioner.Lift;s.Controls.Throttle=throttle;s.Controls.Steer=steer;
                float startZ=s.Vehicle.Z,minSpeed=float.MaxValue,worstVisible=0;int contacts=0,deadStops=0,rollerSteps=0,touchedSteps=0;string first="";float rollerStart=s.GuideRollerTravel,maxRotationError=0;
                float conditionerClear=float.PositiveInfinity,progressTouching=0;
                for(int i=0;i<250;i++)
                {
                    float before=s.Vehicle.Speed;
                    s.Step(.02f);Render(game,.02f);
                    worstVisible=Mathf.Max(worstVisible,VisiblePenetration(game));
                    if(s.BoardContact){contacts++;if(first=="")first=s.LastMovementContact.GuideRoller?"roller":Mathf.Abs(LocalZ(s))>0&&LocalZ(s)<-.3f?"conditioner":"body";if(Mathf.Abs(before)>.2f&&s.Vehicle.Speed==0)deadStops++;}
                    // The roller on the board face (rolling at the driving margin) counts as touching, pushed or not.
                    else if(first==""&&s.GuideRollerContact)first="roller";
                    if(first!="")minSpeed=Mathf.Min(minSpeed,Mathf.Abs(s.Vehicle.Speed));
                    conditionerClear=Mathf.Min(conditionerClear,ConditionerClearance(s));
                    if(first!=""){touchedSteps++;if(s.GuideRollerContact){rollerSteps++;progressTouching+=Mathf.Abs(s.Vehicle.Speed)*.02f;}}
                    float expectedYaw=-s.GuideRollerTravel/c.GuideRollerRadius*Mathf.Rad2Deg;
                    maxRotationError=Mathf.Max(maxRotationError,Mathf.Abs(Mathf.DeltaAngle(w.GuideRoller.localEulerAngles.y,expectedYaw)));
                }
                float progress=s.Vehicle.Z-startZ;
                Info("DRIVE | "+label+" | throttle "+throttle+" steer "+steer+" lift "+s.Conditioner.Lift+" | progress "+progress.ToString("F2")+" m | speed "+(throttle*c.ForwardSpeed).ToString("F2")+"->"+s.Vehicle.Speed.ToString("F2")+
                    " (min "+(minSpeed==float.MaxValue?"-":minSpeed.ToString("F2"))+") | first contact "+(first==""?"none":first)+" | contact steps "+contacts+" | dead stops "+deadStops+
                    " | roller travel "+(s.GuideRollerTravel-rollerStart).ToString("F2")+" m, bearing "+rollerSteps+"/"+touchedSteps+" steps | conditioner nearest the boards "+(conditionerClear*1000).ToString("F0")+" mm | visible penetration "+(worstVisible*1000).ToString("F1")+" mm");
                Check(worstVisible<.0015f,label+": the visible machine never enters the boards ("+(worstVisible*1000).ToString("F1")+" mm)");
                Check(maxRotationError<.01f,label+": the visible roller turns with its tangential travel");
                if(angle<=15)Check(deadStops==0&&s.Vehicle.Speed>.5f*throttle*c.ForwardSpeed,label+": glancing contact keeps moving along the boards (no dead stop, "+s.Vehicle.Speed.ToString("F2")+" m/s)");
                if(angle==90)Check(s.Vehicle.Speed==0&&s.BoardContact,"head-on: the boards stop the machine");
                if(angle<=15&&steer<=0)
                {
                    Check(first==""||first=="roller",label+": the guide roller makes first contact ("+(first==""?"no contact":first)+")");
                    Check(conditionerClear>.02f+RollerLead*.8f,label+": the conditioner side plate never reaches the boards (nearest "+(conditionerClear*1000).ToString("F0")+" mm)");
                }
                if(angle==0&&steer<=0)
                {
                    float travel=s.GuideRollerTravel-rollerStart;
                    Check(touchedSteps>0&&rollerSteps>=touchedSteps*.9f&&travel>=progressTouching*.95f&&travel>.5f*progress,
                        label+": the roller bears on the boards and turns with the straight run ("+rollerSteps+"/"+touchedSteps+" steps, "+travel.ToString("F2")+" m rolled of "+progress.ToString("F2")+" m)");
                }
                if(label=="parallel, light contact")Capture("Roller-Straight-Board-Top",game,new Vector3(-.8f,5,1.9f),new Vector3(-.8f,0,1.9f),.75f,false);
                if(label=="2 deg")Capture("Roller-Riding-Board-Top",game,new Vector3(-.8f,5,2.2f),new Vector3(-.8f,0,2.2f),.55f,false);
                if(label=="15 deg")Capture("Glancing-Along-Board-Top",game,new Vector3(-.5f,9,.5f),new Vector3(-.5f,0,.5f),3.2f,false);
            }
            // Corner (held light into the boards; and, for the record, held hard into them), reverse recovery, service gate.
            foreach(float steer in new[]{-.15f,-.5f})
            {
                game.NewSession(true);var s=game.Sim;
                float x=PlaceX(s,10,0,.03f);Park(s,x,10,0,c.ForwardSpeed);s.Conditioner.Lift=1;s.Controls.ConditionerTarget=1;s.Controls.Throttle=1;s.Controls.Steer=steer;
                float worstVisible=0,conditionerClear=float.PositiveInfinity;int deadStops=0,conditionerSteps=0,rollerSteps=0,bodySteps=0,atMargin=0;
                for(int i=0;i<450;i++)
                {
                    float before=s.Vehicle.Speed;s.Step(.02f);Render(game,.02f);worstVisible=Mathf.Max(worstVisible,VisiblePenetration(game));
                    // The solver names only the deepest part; a conditioner held at the margin alongside the roller is touching too.
                    float clear=ConditionerClearance(s);conditionerClear=Mathf.Min(conditionerClear,clear);if(clear<.0201f)atMargin++;
                    if(s.BoardContact&&Mathf.Abs(before)>.2f&&s.Vehicle.Speed==0)deadStops++;
                    if(s.GuideRollerContact)rollerSteps++;
                    if(s.BoardContact&&!s.LastMovementContact.GuideRoller){if(LocalZ(s)<-.3f)conditionerSteps++;else bodySteps++;}
                    if(i==260&&steer==-.15f)Capture("Rounding-Corner-Top",game,new Vector3(0,12,0),Vector3.zero,4.5f,false);
                }
                Info("DRIVE | NW corner, steer "+steer.ToString("F2")+" | heading "+(s.Vehicle.Heading*Mathf.Rad2Deg).ToString("F1")+" deg | speed "+s.Vehicle.Speed.ToString("F2")+" | roller travel "+s.GuideRollerTravel.ToString("F2")+" m, bearing "+rollerSteps+"/450 steps | "+
                    "contact steps: conditioner "+conditionerSteps+", body "+bodySteps+" | conditioner nearest the board face "+(conditionerClear*1000).ToString("F2")+" mm (driving margin 20 mm), within 0.1 mm of the margin "+atMargin+" steps | visible penetration "+(worstVisible*1000).ToString("F1")+" mm");
                if(steer!=-.15f){Check(worstVisible<.0015f,"held hard into the boards round the NW corner, the visible machine never enters them ("+(worstVisible*1000).ToString("F1")+" mm)");continue;}
                Check(deadStops==0&&s.Vehicle.Heading>1.2f&&worstVisible<.0015f,"the boards carry the machine round the NW corner without a dead stop or penetration");
                Check(conditionerSteps==0&&bodySteps==0&&atMargin==0,"round the corner the roller alone bears; the conditioner never reaches the boards or the 20 mm driving margin (nearest "+(conditionerClear*1000).ToString("F2")+" mm, contact steps "+conditionerSteps+", at the margin "+atMargin+")");
            }
            {
                game.NewSession(true);var s=game.Sim;
                Park(s,-c.RinkWidth*.5f+4,0,-Mathf.PI*.5f,c.ForwardSpeed);s.Controls.Throttle=1;s.Conditioner.Lift=1;s.Controls.ConditionerTarget=1;
                for(int i=0;i<150;i++){s.Step(.02f);Render(game,.02f);}
                float hx=s.Vehicle.X;s.Controls.Throttle=-1;for(int i=0;i<100;i++){s.Step(.02f);Render(game,.02f);}
                Check(s.Vehicle.X-hx>.5f&&!s.BoardContact,"after a head-on stop, reversing recovers ("+(s.Vehicle.X-hx).ToString("F2")+" m)");
            }
            {
                game.NewSession(true);var s=game.Sim;s.Layout.ParkingPose(out float px,out float pz,out float ph);Park(s,px,pz,ph);
                float worstVisible=0;s.Controls.Throttle=-1;for(int i=0;i<700;i++){s.Step(.02f);Render(game,.02f);worstVisible=Mathf.Max(worstVisible,VisiblePenetration(game));}
                bool onIce=s.Ice.IsInside(s.Vehicle.X,s.Vehicle.Z);
                s.Controls.Throttle=.5f;for(int i=0;i<900;i++){s.Step(.02f);Render(game,.02f);worstVisible=Mathf.Max(worstVisible,VisiblePenetration(game));}
                Check(onIce&&s.InServiceBay&&worstVisible<.0015f,"service gate: reverse out onto the ice and drive back into the bay through the gate, walls solid ("+(worstVisible*1000).ToString("F1")+" mm)");
            }
        }
        /// <summary>How far the conditioner collider is from the board face (m).</summary>
        static float ConditionerClearance(Simulation s)
        {
            var box=s.ConditionerCollisionPolygon();if(box.Length==0)return float.PositiveInfinity;
            float sn=Mathf.Sin(s.Vehicle.Heading),cs=Mathf.Cos(s.Vehicle.Heading),worst=float.NegativeInfinity;
            for(int i=0;i<box.Length;i+=2)
            {
                int j=(i+2)%box.Length;
                for(int k=0;k<10;k++)
                {
                    float lx=box[i]+(box[j]-box[i])*k/10f,lz=box[i+1]+(box[j+1]-box[i+1])*k/10f;
                    float x=s.Vehicle.X+cs*lx+sn*lz,z=s.Vehicle.Z-sn*lx+cs*lz;
                    if(s.Layout.IsPortal(x,z))continue;
                    worst=Mathf.Max(worst,s.Ice.BoardDistance(x,z,out _,out _));
                }
            }
            return -worst;
        }
        static float LocalZ(Simulation s)
        {
            var hit=s.LastMovementContact;float dx=hit.PointX-hit.VehicleX,dz=hit.PointZ-hit.VehicleZ;
            return Mathf.Sin(hit.Heading)*dx+Mathf.Cos(hit.Heading)*dz;
        }
        static float PlaceX(Simulation s,float z,float heading,float gap)
        {
            float x=-s.Config.RinkWidth*.5f+2.5f;
            for(int i=0;i<80;i++)
            {
                float sn=Mathf.Sin(heading),cs=Mathf.Cos(heading),worst=float.MinValue;
                for(int k=0;k<s.CollisionSampleCount;k++){s.CollisionSample(k,out float lx,out float lz);worst=Mathf.Max(worst,s.Ice.BoardDistance(x+cs*lx+sn*lz,z-sn*lx+cs*lz,out _,out _));}
                float rx=x+cs*s.Config.GuideRollerX+sn*s.Config.GuideRollerZ,rz=z-sn*s.Config.GuideRollerX+cs*s.Config.GuideRollerZ;
                worst=Mathf.Max(worst,s.Ice.BoardDistance(rx,rz,out _,out _)+s.Config.GuideRollerRadius);
                x-=(-gap-worst)*.9f;
            }
            return x;
        }

        /// <summary>How far any visible machine mesh (rendered, batched) passes the boards or service walls, in metres.</summary>
        static float VisiblePenetration(IceResurfacerGame game)
        {
            var s=game.Sim;float worst=0;
            foreach(var r in game.World.Machine.parent.GetComponentsInChildren<MeshRenderer>())
            {
                if(!r.enabled||r.transform.IsChildOf(game.World.BrushArm))continue;
                var f=r.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null)continue;
                var b=r.bounds;
                // Cheap reject: the renderer's box is fully on drivable ground.
                bool inside=true;
                for(int i=0;i<4&&inside;i++){float x=(i&1)==0?b.min.x:b.max.x,z=(i&2)==0?b.min.z:b.max.z;inside=s.Ice.IsInside(x,z)||s.Layout.ContainsFloor(x,z);}
                if(inside)continue;
                var mesh=f.sharedMesh;if(!mesh.isReadable)continue;
                var vertices=mesh.vertices;var matrix=r.localToWorldMatrix;
                for(int i=0;i<vertices.Length;i+=1)
                {
                    Vector3 p=matrix.MultiplyPoint3x4(vertices[i]);
                    if(p.y>3.7f||s.Ice.IsInside(p.x,p.z)||s.Layout.ContainsFloor(p.x,p.z))continue;
                    float d=s.Layout.IsPortal(p.x,p.z)?.001f:s.Ice.BoardDistance(p.x,p.z,out _,out _);
                    worst=Mathf.Max(worst,d);
                }
            }
            return worst;
        }

        static void BrushInPlay(IceResurfacerGame game)
        {
            Start("Play: board brush along the boards in normal driving");
            game.NewSession(true);var s=game.Sim;var w=game.World;var c=s.Config;
            var rig=w.BrushArm.GetComponent<IceBoardBrushRig>();
            Check(rig!=null&&rig.IsComplete,"Play uses the authored brush rig");
            float x=PlaceX(s,-8,-3*Mathf.Deg2Rad,.20f);
            Park(s,x,-8,-3*Mathf.Deg2Rad,1.6f);s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;
            s.Controls.BoardBrush=true;s.Controls.Throttle=.6f;s.Controls.Steer=-.08f;
            float maxError=0;int contact=0,swept=0,checkedCells=0;
            for(int i=0;i<300;i++)
            {
                Vector3 under=rig.head.position;float before=SnowAt(s,under);
                s.Step(.02f);Render(game,.02f);
                maxError=Mathf.Max(maxError,new Vector2(rig.head.position.x-s.BrushWorldX,rig.head.position.z-s.BrushWorldZ).magnitude);
                if(s.BrushContact)contact++;
                if(before>0&&s.BrushDeployment>.5f&&Mathf.Abs(s.Vehicle.Speed)>.01f){checkedCells++;if(SnowAt(s,under)<before)swept++;}
                if(i==200)Capture("Brush-Deployed-At-Boards-Top",game,new Vector3(-.8f,4,1.2f),new Vector3(-.8f,0,1.2f),1.2f,false);
            }
            Info("brush along the boards: contact steps "+contact+"/300, swept "+swept+"/"+checkedCells+" checked cells, visible disc vs simulated contact max "+(maxError*1000).ToString("F3")+" mm, swept "+(s.BrushSweptSnowM3*1000).ToString("F2")+" L");
            Check(contact>100,"driving normally along the boards (the guide roller on them), the working brush reaches the kickplate");
            Check(maxError<.0001f,"the visible bristle disc is the simulated sweep disc");
            Check(s.BrushSweptSnowM3>0&&(checkedCells==0||swept>=checkedCells*.9f),"snow is swept under the visible disc");
        }
        static float SnowAt(Simulation s,Vector3 p)
        {
            var ice=s.Ice;int col=(int)Math.Floor((p.x-ice.MinX)/ice.CellSize),row=(int)Math.Floor((p.z-ice.MinZ)/ice.CellSize);
            if(col<0||row<0||col>=ice.Columns||row>=ice.Rows)return 0;
            return ice.Cells[ice.Index(col,row)].SnowMm;
        }

        static void GuideCheck(IceResurfacerGame game)
        {
            Start("GPS: compatibility with the outline calibration");
            var expected=JsonUtility.FromJson<Expected>(File.ReadAllText(Out+"expected.json"));
            string guides=Path.Combine(RouteRecordingStorage.DefaultDirectory,"Guides");
            var owner=game.RouteGuide;owner.Refresh();
            foreach(string old in Directory.GetFiles(guides,"*.guide.json").Where(p=>!expected.guides.Contains(Path.GetFileName(p))))
            {
                bool used=owner.UseSavedRouteAsGps(old);
                Info("GPS old | "+Path.GetFileName(old)+" | "+(used?"ACCEPTED":"rejected")+" | "+owner.TrainingStatus.Replace("\n"," / "));
                Check(!used,"an earlier guide is not silently accepted: "+Path.GetFileName(old));
            }
            foreach(string made in expected.guides)
            {
                string path=Path.Combine(guides,made);
                var g=TrainingGuideStorage.Read(path);var fit=RouteFitter.Fit(g,TrainingGuideStorage.Raw(g));
                bool used=owner.UseSavedRouteAsGps(path);
                Info("GPS new | "+made+" | ready "+fit.Ready+" boundary "+fit.boundaryErrors+" | "+(used?"accepted":"rejected")+" | "+owner.TrainingStatus.Replace("\n"," / "));
            }
            // Physical replay of the essential operator drive against today's envelope.
            string andy=Directory.GetFiles(RouteRecordingStorage.DefaultDirectory,"Andy_Try_1_*.route.json").Single();
            var raw=RouteRecordingStorage.Read(andy);int body=0;var s=game.Sim;var reasons=new Dictionary<string,int>();
            foreach(var p in raw.samples)if(!s.ProbeVehiclePose(p.vehicleX,p.vehicleZ,p.vehicleHeadingRadians,out var hit)){body++;string k=hit.Reason+(hit.GuideRoller?" (roller)":"");reasons[k]=reasons.TryGetValue(k,out int n)?n+1:1;}
            Info("Andy_Try_1 physical replay: "+(raw.samples.Count-body)+"/"+raw.samples.Count+" recorded poses fit the outline envelope"+(body>0?"; contacts: "+string.Join(", ",reasons.Select(r=>r.Key+" x"+r.Value)):""));
        }

        static void Captures(IceResurfacerGame game,Mouse mouse)
        {
            Start("Captures");
            var cam=game.DriverCamera;var controls=game.PhysicalControls;
            foreach(float lift in new[]{0f,.5f,1f})
            {
                game.NewSession(true);var s=game.Sim;var w=game.World;Park(s,0,-5,0);s.Step(.02f);s.Conditioner.Lift=lift;
                s.Controls.FloodFlow=.5f;Render(game,0);
                var grips=GripTransforms(w);
                Aim(game,(grips[5].position+grips[7].position)*.5f);
                Pointer(game,mouse,cam.WorldToScreenPoint(grips[5].position));
                Driver("Controls-Seated-Lift"+(lift*100).ToString("F0")+"-Hover-Flood",cam);
                Pointer(game,mouse,cam.WorldToScreenPoint(grips[7].position));
                Driver("Controls-Seated-Lift"+(lift*100).ToString("F0")+"-Hover-Blade",cam);
                Pointer(game,mouse,Vector2.zero);
                EView(game);Driver("Controls-EView-Lift"+(lift*100).ToString("F0"),cam);
                Capture("Controls-Side-Lift"+(lift*100).ToString("F0"),game,new Vector3(5,1.4f,-.7f),new Vector3(0,1.4f,-.7f),1.3f,true);
            }
        }

        static void Driver(string label,Camera cam)
        {
            Directory.CreateDirectory(Out+"Shots");
            IceResurfacerEditor.Capture(cam,"ControlsBrushRoller/Shots/warm.png",320,180);
            IceResurfacerEditor.Capture(cam,"ControlsBrushRoller/Shots/After-"+label+".png",1280,800);
            File.Delete(Out+"Shots/warm.png");
        }
        static void Capture(string label,IceResurfacerGame game,Vector3 from,Vector3 at,float size,bool hideArena)
        {
            var m=game.World.Machine;Shoot(label,m,m.TransformPoint(from),m.TransformPoint(at),size,hideArena);
        }
        static void Shoot(string label,Transform m,Vector3 from,Vector3 at,float size,bool hideArena=true)
        {
            var arena=UnityEngine.Object.FindAnyObjectByType<IceArenaRig>(FindObjectsInactive.Include);
            bool active=arena!=null&&arena.gameObject.activeSelf;
            if(hideArena&&arena!=null)arena.gameObject.SetActive(false);
            var node=new GameObject("Validation camera"){hideFlags=HideFlags.HideAndDontSave};var lightNode=new GameObject("Validation light"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var light=lightNode.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;lightNode.transform.rotation=m.rotation*Quaternion.Euler(40,60,0);
                var cam=node.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.02f;cam.farClipPlane=60;
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.42f,.45f,.5f);
                Vector3 d=at-from;node.transform.SetPositionAndRotation(from,Quaternion.LookRotation(d,Mathf.Abs(Vector3.Dot(d.normalized,m.up))>.9f?m.forward:m.up));
                Directory.CreateDirectory(Out+"Shots");
                IceResurfacerEditor.Capture(cam,"ControlsBrushRoller/Shots/warm.png",320,180);
                IceResurfacerEditor.Capture(cam,"ControlsBrushRoller/Shots/After-"+label+".png",1200,900);
                File.Delete(Out+"Shots/warm.png");
            }
            finally{UnityEngine.Object.DestroyImmediate(node);UnityEngine.Object.DestroyImmediate(lightNode);if(arena!=null)arena.gameObject.SetActive(active);}
        }

        // ---- Helpers ------------------------------------------------------------------------------

        static List<IceMechanicalAlignment.Item> Table(List<IceMechanicalAlignment.Item> items)
        {
            foreach(var i in items)Info("TABLE | "+i.Label+" | "+i.Kind+" | "+i.ErrorMm.ToString("F1")+" mm | "+i.Detail+(i.Blocker!=null?" | "+i.Blocker:""));
            return items;
        }
        static string[] CompatibilityFields(){ return (string[])typeof(RouteFit).GetField("CompatibilityFields",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null); }
        static string Describe(object v){ return v is float[] a?(a.Length==0?"[]":"["+a.Length/2+" vertices]"):Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture); }
        static IceMachineRig OpenScene()
        {
            RequireIsolated();
            EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
            var rig=UnityEngine.Object.FindAnyObjectByType<IceMachineRig>(FindObjectsInactive.Include);
            if(rig==null)throw new InvalidOperationException("No authored machine in "+IceMachineBaker.AuthoredScenePath);
            return rig;
        }
        static IceResurfacerGame Game(){ return UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>(FindObjectsInactive.Include); }
        const float RollerLead=.025f;
        static bool IsRoot(IceMachineRig rig,PropertyModification m)
        {
            var source=PrefabUtility.GetCorrespondingObjectFromSource(rig.gameObject);
            return m.target==source||m.target==source.transform;
        }
        static List<string> Diff(string a,string b)
        {
            var before=a.Split('\n');var after=b.Split('\n');var changed=new List<string>();
            for(int i=0;i<Math.Min(before.Length,after.Length);i++)if(before[i]!=after[i])changed.Add(after[i].Split('(')[0]);
            return changed;
        }

        /// <summary>The moved roller: leads the conditioner, bracket meets body and roller, front tyre clear through full lock.</summary>
        static void RollerHardware(IceWorld w,SimulatorConfig c)
        {
            Start("Guide roller hardware after the move");
            Transform m=w.Machine;
            Vector3 roller=m.InverseTransformPoint(w.GuideRoller.position);float radius=IceMechanicalAlignment.GuideRollerRadius(w);
            float lead=Mathf.Abs(roller.x)+radius-c.ConditionerCollisionHalfWidth;
            Check(Mathf.Abs(lead-RollerLead)<.0011f,"the roller's outer edge |x| "+(Mathf.Abs(roller.x)+radius).ToString("F3")+" leads the conditioner side plates ("+c.ConditionerCollisionHalfWidth.ToString("F3")+") by "+(lead*1000).ToString("F0")+" mm");
            Check(Mathf.Abs(roller.x-c.GuideRollerX)<.0005f&&Mathf.Abs(roller.z-c.GuideRollerZ)<.0005f&&Mathf.Abs(radius-c.GuideRollerRadius)<.0005f,"the simulated roller is the visible roller ("+roller.x.ToString("F3")+", "+roller.z.ToString("F3")+", r "+radius.ToString("F3")+")");
            Transform bracket=null;foreach(var t in w.GuideRoller.parent.GetComponentsInChildren<Transform>(true))if(t.name=="Guide roller bracket")bracket=t;
            Vector3 half=bracket.up*bracket.lossyScale.y,a=m.InverseTransformPoint(bracket.position-half),b=m.InverseTransformPoint(bracket.position+half);
            Vector3 inner=Mathf.Abs(a.x)<Mathf.Abs(b.x)?a:b,outer=Mathf.Abs(a.x)<Mathf.Abs(b.x)?b:a;
            IceCollisionOutlineFit.Extent(c.CollisionOutline,inner.z,out float bodyLeft,out _);
            Check(Vector2.Distance(new Vector2(outer.x,outer.z),new Vector2(roller.x,roller.z))<.002f,"the bracket ends at the roller axle");
            Check(Mathf.Abs(inner.x)<-bodyLeft-.05f,"the bracket's inboard end is still inside the drawn body (x "+inner.x.ToString("F3")+", body side "+bodyLeft.ToString("F3")+")");
            Info("bracket length "+(2*bracket.lossyScale.y).ToString("F3")+" m (was 0.195 m)");
            // Front-left tyre vs roller and bracket, wheel steered through its full lock.
            var wheel=w.FrontWheels[0];var rest=wheel.localRotation;float clear=float.PositiveInfinity,at=0;
            var parts=new List<(Transform,Vector3[])>();
            foreach(var r in w.GuideRoller.parent.GetComponentsInChildren<MeshRenderer>(true)){var f=r.GetComponent<MeshFilter>();if(f!=null&&f.sharedMesh!=null&&f.sharedMesh.isReadable)parts.Add((r.transform,f.sharedMesh.vertices));}
            var tyre=new List<(Transform,Vector3[])>();
            foreach(var r in wheel.GetComponentsInChildren<MeshRenderer>(true)){var f=r.GetComponent<MeshFilter>();if(f!=null&&f.sharedMesh!=null&&f.sharedMesh.isReadable)tyre.Add((r.transform,f.sharedMesh.vertices));}
            try
            {
                for(float steer=-c.MaxSteeringRadians;steer<=c.MaxSteeringRadians+1e-4f;steer+=c.MaxSteeringRadians/10)
                {
                    wheel.localRotation=Quaternion.Euler(0,steer*Mathf.Rad2Deg,0)*rest;
                    // Tyre as its own mesh hull in the wheel frame: radial and lateral extent.
                    float tyreRadius=0,tyreHalf=0;
                    foreach(var (t,v) in tyre)foreach(var p in v){Vector3 q=wheel.InverseTransformPoint(t.TransformPoint(p));tyreRadius=Mathf.Max(tyreRadius,new Vector2(q.y,q.z).magnitude);tyreHalf=Mathf.Max(tyreHalf,Mathf.Abs(q.x));}
                    foreach(var (t,v) in parts)foreach(var p in v)
                    {
                        Vector3 q=wheel.InverseTransformPoint(t.TransformPoint(p));
                        float d=Mathf.Max(new Vector2(q.y,q.z).magnitude-tyreRadius,Mathf.Abs(q.x)-tyreHalf);
                        if(d<clear){clear=d;at=steer;}
                    }
                }
            }
            finally{wheel.localRotation=rest;}
            Check(clear>.02f,"the front tyre clears the roller and bracket through full steering lock by "+(clear*1000).ToString("F0")+" mm (closest at "+(at*Mathf.Rad2Deg).ToString("F1")+" deg)");
            Shoot("Roller-Mount-Top",m,m.TransformPoint(-.8f,4,2.1f),m.TransformPoint(-.8f,0,2.1f),.6f);
            Shoot("Roller-Mount-Front",m,m.TransformPoint(-.8f,.55f,6),m.TransformPoint(-.8f,.55f,0),.6f);
        }

        static string Snapshot(Transform root)
        {
            var sb=new System.Text.StringBuilder();
            foreach(var t in root.GetComponentsInChildren<Transform>(true))sb.Append(t.name).Append(V(t.localPosition,6)).Append(t.localRotation.ToString("F6")).Append(V(t.localScale,6)).Append('\n');
            return sb.ToString();
        }
        static string V(Vector3 v,int d=4){ string f="F"+d; return "("+v.x.ToString(f,System.Globalization.CultureInfo.InvariantCulture)+","+v.y.ToString(f,System.Globalization.CultureInfo.InvariantCulture)+","+v.z.ToString(f,System.Globalization.CultureInfo.InvariantCulture)+")"; }
        static string Hash(string path){ using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
        static void RequireIsolated()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))
                throw new InvalidOperationException("This validation writes assets; run it only in the isolated StageUnityValidation.ps1 project.");
        }
        static void Begin(string name,Action body)
        {
            SessionState.SetString("Aurora.ControlsBrushRoller.Results",name);
            Directory.CreateDirectory(Out);File.WriteAllText(ResultsPath,"");errors=0;passes=0;
            Run(body);
        }
        static void Start(string step){ File.AppendAllText(ResultsPath,"== "+step+" / "+DateTime.Now.ToString("s")+"\n"); }
        static void Info(string message){ File.AppendAllText(ResultsPath,"INFO "+message+"\n"); }
        static void Check(bool valid,string message)
        {
            if(!valid)throw new InvalidOperationException("Controls/brush/roller: "+message);
            passes++;File.AppendAllText(ResultsPath,"PASS "+message+"\n");
        }
        static void Run(Action body)
        {
            try { body(); }
            catch(Exception e)
            {
                Debug.LogException(e);SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
                try { File.AppendAllText(ResultsPath,"FAIL "+e.Message+"\n"+e.StackTrace+"\n"); } catch { }
                if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
                Exit(1);
            }
        }
        static void Exit(int code){ Debug.Log("AURORA CONTROLS BRUSH ROLLER exit="+code);EditorApplication.Exit(code); }
    }
}
