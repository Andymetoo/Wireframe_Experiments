using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Mechanical reconciliation of the user's authored Aurora, isolated validation project only.
    ///   Capture  (any state): Play-state screenshots and measurements at fixed machine-relative cameras.
    ///   Apply    : cleans the redundant scene overrides, then runs the same Inspector actions a user
    ///              would (Transfer Authored Geometry, Add Board Brush Rig, Apply Brush Geometry), saves.
    ///   Verify   : fresh process; persistence, proportions, Play mechanics, GPS, no Play mutations.
    /// </summary>
    [InitializeOnLoad]
    public static class IceMechanicalReconciliationValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        const string Out=Root+"Validation~/MechanicalReconciliation/";
        const string StageKey="Aurora.Reconcile.Stage", PrefixKey="Aurora.Reconcile.Prefix";
        const string ResultsKey="Aurora.Reconcile.Results";
        static string ResultsPath=>Out+"results-"+SessionState.GetString(ResultsKey,"run")+".txt";
        static int frame,errors;

        [Serializable] sealed class Expected
        {
            public string prefabHash, sceneHash, calibration, meshesHash;
            public float authoredConditionerZ;
            public List<string> changedPaths=new List<string>();
        }

        // ---- Entry points -------------------------------------------------------------------

        public static void Capture() { Begin("Capture"+Arg("-reconcilePrefix","Before"),()=>{ OpenScene(); Play("capture"); }); }

        public static void Apply()
        {
            Begin("Apply",()=>
            {
                var rig=OpenScene();var game=Game();var w=rig.Collect();
                Start("Before (as authored)");
                Table(w,game.calibration);
                Start("Scene instance clean-up (prefab is authoritative)");
                CleanInstance(rig);
                Start("Transfer Authored Geometry to Simulation (Inspector action)");
                string calibrationBefore=JsonUtility.ToJson(game.calibration);
                Check(IceMechanicalAlignment.Reconcile(w,game,out var changes,out string error),"Reconcile succeeds"+(error!=null?": "+error:""));
                foreach(string line in changes)Info(line);
                Start("Board brush rig (Inspector: Add Board Brush Rig, set radius to the bristles, Apply)");
                Check(w.BrushArm.GetComponents<IceBoardBrushRig>().Length==0,"no brush rig installed yet (none duplicated)");
                var brush=IceBoardBrushRigEditor.AddRig(w,game.calibration,out string summary);Info(summary);
                foreach(var added in PrefabUtility.GetAddedComponents(rig.gameObject))
                    PrefabUtility.ApplyAddedComponent(added.instanceComponent,IceMachineBaker.PrefabPath,InteractionMode.UserAction);
                Undo.RecordObject(brush,"Brush radius to visible bristles");
                float visual=IceBoardBrushRigEditor.VisualRadius(brush);
                brush.mechanism.Radius=Mathf.Round(visual*1000)/1000;
                IceBoardBrushRigEditor.Repose(brush);
                foreach(var part in new UnityEngine.Object[]{brush,brush.transform,brush.head,brush.link,brush.roller})IceMechanicalAlignment.CommitToPrefab(part);
                foreach(var line in IceBoardBrushRigEditor.Diagnose(brush,game.calibration,brush.mechanism))
                { Info("brush: "+line.Value); Check(line.Key!=MessageType.Error,"brush geometry has no reach error"); }
                Check(IceBoardBrushRigEditor.ApplyToSimulation(brush,game,out error),"Apply Brush Geometry to Simulation"+(error!=null?": "+error:""));
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                Start("After");
                w=rig.Collect();
                var after=Table(w,game.calibration);
                foreach(var item in after)
                    if(item.Label!="Board guide roller")Check(item.Kind==IceMechanicalAlignment.Kind.Matches||item.Kind==IceMechanicalAlignment.Kind.Info,"aligned: "+item.Label+" ("+item.ErrorMm.ToString("F1")+" mm)");
                CalibrationDiff(calibrationBefore,JsonUtility.ToJson(game.calibration));
                var expected=new Expected{prefabHash=Hash(IceMachineBaker.PrefabPath),sceneHash=Hash(IceMachineBaker.AuthoredScenePath),meshesHash=Hash(Root+"Machine/AuroraR54.Meshes.asset"),
                    calibration=JsonUtility.ToJson(game.calibration),authoredConditionerZ=w.Machine.InverseTransformPoint(w.Conditioner.position).z};
                expected.changedPaths=Proportions(rig,w);
                File.WriteAllText(Out+"expected.json",JsonUtility.ToJson(expected,true));
                Exit(0);
            });
        }

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
                Check(JsonUtility.ToJson(game.calibration)==expected.calibration,"scene calibration persisted");
                Check(PrefabUtility.GetAddedGameObjects(rig.gameObject).Count==0&&PrefabUtility.GetAddedComponents(rig.gameObject).Count==0&&
                    PrefabUtility.GetRemovedComponents(rig.gameObject).Count==0,"scene adds or removes nothing from the prefab");
                // The saved file itself: no removed-object entries (live or stale) on the machine instance.
                string sceneText=File.ReadAllText(IceMachineBaker.AuthoredScenePath);
                string machineGuid=AssetDatabase.AssetPathToGUID(IceMachineBaker.PrefabPath);
                int instance=sceneText.IndexOf("m_SourcePrefab: {fileID: 100100000, guid: "+machineGuid,StringComparison.Ordinal);
                int removedAt=instance<0?-1:sceneText.LastIndexOf("m_RemovedGameObjects:",instance,StringComparison.Ordinal);
                Check(removedAt>=0&&sceneText.Substring(removedAt,"m_RemovedGameObjects: []".Length)=="m_RemovedGameObjects: []","the saved scene removes no prefab objects (no live or stale removal entries)");
                int overrides=PrefabUtility.GetPropertyModifications(rig.gameObject).Count(m=>!IsRootModification(rig,m));
                Check(overrides==0,"no scene overrides below the instance root ("+overrides+")");
                var brushes=rig.GetComponentsInChildren<IceBoardBrushRig>(true);
                Check(brushes.Length==1&&brushes[0].IsComplete&&brushes[0].transform==w.BrushArm,"exactly one complete brush rig, on the bound arm");
                Check(brushes[0].mechanism.Matches(IceBoardBrushRig.Mechanism.From(game.calibration),.00001f),"brush rig matches the scene calibration");
                // Measured on the unbatched authored head (Play combines the bristle meshes).
                float bristles=IceBoardBrushRigEditor.VisualRadius(brushes[0]);
                Check(Mathf.Abs(bristles-game.calibration.BrushRadius)<.001f,"clearing radius "+game.calibration.BrushRadius.ToString("F3")+" m equals the visible bristle radius "+bristles.ToString("F3")+" m");
                foreach(var item in IceMechanicalAlignment.Measure(w,game.calibration))
                    Info("alignment "+item.Label+": "+item.Kind+" "+item.ErrorMm.ToString("F1")+" mm "+item.Detail+(item.Blocker!=null?" / "+item.Blocker:""));
                Start("Finished visual proportions");
                var changed=Proportions(rig,w);
                Check(changed.SequenceEqual(expected.changedPaths),"the same "+changed.Count+" deliberate mechanical transforms differ from the original; nothing else");
                Start("Edit Mode preview is never saved");
                var snapshot=Snapshot(rig.transform);
                IceMechanicalAlignment.Preview(w,game.calibration,0,0);
                Check(Mathf.Abs(w.Machine.InverseTransformPoint(w.Conditioner.position).y)<.0005f,"lowered preview puts the conditioner on the ice");
                IceMechanicalAlignment.Preview(w,game.calibration,1,1);
                Check(Quaternion.Angle(w.TankPivot.localRotation,Quaternion.identity)>57,"tank preview tilts the tank");
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Check(!IceMechanicalAlignment.Previewing&&Snapshot(rig.transform)==snapshot,"saving during a preview restores every part first");
                Check(Hash(IceMachineBaker.AuthoredScenePath)==expected.sceneHash,"the saved scene is byte-identical");
                Play("verify");
            });
        }

        public static void LaunchCompat()
        {
            // Launch scene: unchanged calibration keeps every saved route compatible there.
            Begin("Launch",()=>
            {
                RequireIsolated();
                EditorSceneManager.OpenScene(IceResurfacerEditor.LaunchScenePath);
                var game=Game();var c=game.calibration;c.Validate();
                Start("Launch scene calibration and saved routes");
                Check(UnityEngine.Object.FindAnyObjectByType<IceMachineRig>(FindObjectsInactive.Include)==null,"launch scene still builds the procedural machine at runtime");
                var defaults=new SimulatorConfig();
                foreach(string f in new[]{"TankHingeHeight","TankHingeZ","TankScaleX","TankScaleY","TankScaleZ","ConveyorDischargeX","ConveyorDischargeHeight","ConveyorDischargeZ","DrawbarLength","BrushRadius","GuideRollerX"})
                {
                    var field=typeof(SimulatorConfig).GetField(f);
                    Check((float)field.GetValue(c)==(float)field.GetValue(defaults),"launch scene "+f+" = default "+field.GetValue(defaults));
                }
                Routes(c,"launch");
                Exit(0);
            });
        }

        // ---- Scene clean-up -----------------------------------------------------------------

        static bool IsRootModification(IceMachineRig rig,PropertyModification m)
        {
            var source=PrefabUtility.GetCorrespondingObjectFromSource(rig.gameObject);
            return m.target==source||m.target==source.transform;
        }

        /// <summary>Removes scene additions that duplicate prefab objects exactly, and property
        /// overrides equal to the prefab value. Anything that really differs is kept and reported.</summary>
        static void CleanInstance(IceMachineRig rig)
        {
            string before=Snapshot(rig.transform);
            int removed=0;
            foreach(var added in PrefabUtility.GetAddedGameObjects(rig.gameObject))
            {
                var go=added.instanceGameObject;
                Transform twin=null;
                foreach(Transform sibling in go.transform.parent)
                    if(sibling!=go.transform&&sibling.name==go.name&&PrefabUtility.GetCorrespondingObjectFromSource(sibling)!=null&&Same(sibling,go.transform))twin=sibling;
                if(twin==null){Info("KEPT added object (no identical prefab twin): "+go.name);continue;}
                Info("removed duplicate scene copy: "+PathOf(go.transform,rig.transform)+" (identical prefab object "+PathOf(twin,rig.transform)+")");
                Undo.DestroyObjectImmediate(go);removed++;
            }
            var keep=new List<PropertyModification>();int redundant=0,stale=0;
            foreach(var m in PrefabUtility.GetPropertyModifications(rig.gameObject))
            {
                // A modification whose prefab target no longer exists has no effect.
                if(m.target==null){stale++;continue;}
                if(IsRootModification(rig,m)||!Redundant(m)){keep.Add(m);if(!IsRootModification(rig,m))Info("KEPT override "+m.target.name+"."+m.propertyPath+"="+m.value);continue;}
                redundant++;
            }
            PrefabUtility.SetPropertyModifications(rig.gameObject,keep.ToArray());
            Check(Snapshot(rig.transform)==before,"clean-up changes no visible transform, mesh or material (co-located duplicates counted once)");
            Info("removed "+removed+" duplicate objects, "+redundant+" redundant overrides and "+stale+" stale overrides (target no longer in the prefab); kept "+(keep.Count)+" root/real overrides");
        }

        static bool Same(Transform a,Transform b)
        {
            if((a.localPosition-b.localPosition).sqrMagnitude>1e-12f||Quaternion.Angle(a.localRotation,b.localRotation)>.001f||(a.localScale-b.localScale).sqrMagnitude>1e-12f)return false;
            var fa=a.GetComponent<MeshFilter>();var fb=b.GetComponent<MeshFilter>();
            var ra=a.GetComponent<MeshRenderer>();var rb=b.GetComponent<MeshRenderer>();
            if((fa==null)!=(fb==null)||fa!=null&&fa.sharedMesh!=fb.sharedMesh)return false;
            if((ra==null)!=(rb==null)||ra!=null&&!ra.sharedMaterials.SequenceEqual(rb.sharedMaterials))return false;
            return a.childCount==b.childCount&&a.gameObject.activeSelf==b.gameObject.activeSelf;
        }

        static bool Redundant(PropertyModification m)
        {
            if(m.target==null)return false;
            var p=new SerializedObject(m.target).FindProperty(m.propertyPath);
            if(p==null)return false;
            switch(p.propertyType)
            {
                case SerializedPropertyType.Float: return float.TryParse(m.value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float f)&&Mathf.Abs(f-p.floatValue)<=1e-6f*Mathf.Max(1,Mathf.Abs(f));
                case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: case SerializedPropertyType.LayerMask: return m.value==p.longValue.ToString();
                case SerializedPropertyType.Boolean: return m.value==(p.boolValue?"1":"0");
                case SerializedPropertyType.String: return m.value==p.stringValue;
                case SerializedPropertyType.ObjectReference: return m.objectReference==p.objectReferenceValue;
                default: return false;
            }
        }

        // ---- Measurements -------------------------------------------------------------------

        static List<IceMechanicalAlignment.Item> Table(IceWorld w,SimulatorConfig c)
        {
            var items=IceMechanicalAlignment.Measure(w,c);
            foreach(var i in items)
                Info("TABLE | "+i.Label+" | visual "+V(i.Visual)+" | simulated "+V(i.Simulated)+" | "+i.ErrorMm.ToString("F1")+" mm | "+i.Kind+" | "+i.Detail+(i.Blocker!=null?" | "+i.Blocker:""));
            var r=IceConditionerAlignment.Measure(w,c);
            foreach(string line in r.Lines)Info("CONDITIONER | "+line);
            return items;
        }

        static void CalibrationDiff(string before,string after)
        {
            var a=JsonUtility.FromJson<SimulatorConfig>(before);var b=JsonUtility.FromJson<SimulatorConfig>(after);
            foreach(var f in typeof(SimulatorConfig).GetFields(BindingFlags.Public|BindingFlags.Instance))
                if(!Equals(f.GetValue(a),f.GetValue(b)))Info("CALIBRATION | "+f.Name+" | "+f.GetValue(a)+" -> "+f.GetValue(b)+
                    (Array.IndexOf(CompatibilityFields(),f.Name)>=0?" | GPS COMPATIBILITY FIELD":""));
        }
        static string[] CompatibilityFields(){ return (string[])typeof(RouteFit).GetField("CompatibilityFields",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null); }

        /// <summary>Every transform, mesh and material of the instance versus the pre-reconciliation
        /// baseline dump. Returns the paths that differ (and logs how).</summary>
        static List<string> Proportions(IceMachineRig rig,IceWorld w)
        {
            var baseline=new Dictionary<string,string[]>();
            // Paths are relative to the machine root (the root row itself is skipped: its scene pose is not part of the model).
            foreach(var line in File.ReadAllLines(Out+"baseline-prefab.tsv").Skip(1)){var c=line.Split('\t');if(c[0].Contains("/")||c.Length>1&&!c[0].StartsWith("AuroraR54#"))baseline[c[0]]=c;}
            var now=new Dictionary<string,string[]>();
            foreach(var line in Dump(rig.transform,w.Machine).Split('\n').Skip(1)){if(line.Length==0)continue;var c=line.Split('\t');if(c[0]!="")now[c[0]]=c;}
            var changed=new List<string>();
            foreach(var pair in baseline)
            {
                if(!now.TryGetValue(pair.Key,out var c)){changed.Add(pair.Key);Info("PROPORTION missing "+pair.Key);continue;}
                var b=pair.Value;
                bool materials=b[6]!=c[6]||b[7]!=c[7];
                if(materials)Info("PROPORTION MESH/MATERIAL "+pair.Key+" "+b[6]+"/"+b[7]+" -> "+c[6]+"/"+c[7]);
                float moved=Vector3.Distance(P(b[5]),P(c[5]));
                if(b[1]!=c[1]||b[2]!=c[2]||b[3]!=c[3]||b[4]!=c[4]||materials)
                {changed.Add(pair.Key);Info("PROPORTION "+pair.Key+" | origin moved "+(moved*1000).ToString("F1")+" mm | localPos "+b[2]+" -> "+c[2]+" | rot "+b[3]+" -> "+c[3]+" | scale "+b[4]+" -> "+c[4]);}
            }
            foreach(var key in now.Keys)if(!baseline.ContainsKey(key)){changed.Add(key);Info("PROPORTION added "+key);}
            changed.Sort(StringComparer.Ordinal);
            return changed;
        }
        static Vector3 P(string s){var v=s.Trim('(',')').Split(',');return new Vector3(float.Parse(v[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(v[1],System.Globalization.CultureInfo.InvariantCulture),float.Parse(v[2],System.Globalization.CultureInfo.InvariantCulture));}

        static string Dump(Transform root,Transform machine)
        {
            var sb=new StringBuilder("path\tactive\tlocalPos\tlocalRot\tlocalScale\tmachinePos\tmesh\tmaterials\n");
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t==root)continue;
                var f=t.GetComponent<MeshFilter>();var r=t.GetComponent<Renderer>();
                string mats="";if(r!=null)foreach(var mat in r.sharedMaterials)mats+=(mat!=null?mat.name:"null")+";";
                sb.Append(PathOf(t,root)).Append('\t').Append(t.gameObject.activeSelf).Append('\t').Append(V(t.localPosition,5)).Append('\t').Append(Q(t.localRotation)).Append('\t')
                  .Append(V(t.localScale,5)).Append('\t').Append(V(machine.InverseTransformPoint(t.position),5)).Append('\t').Append(f!=null&&f.sharedMesh!=null?f.sharedMesh.name:"").Append('\t').Append(mats).Append('\n');
            }
            return sb.ToString();
        }
        static string Snapshot(Transform root)
        {
            var sb=new StringBuilder();
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                var f=t.GetComponent<MeshFilter>();var r=t.GetComponent<Renderer>();
                sb.Append(t.name).Append(V(t.position,5)).Append(Q(t.rotation)).Append(V(t.lossyScale,5)).Append(t.gameObject.activeInHierarchy).Append(f!=null&&f.sharedMesh!=null?f.sharedMesh.name:"");
                if(r!=null)foreach(var m in r.sharedMaterials)sb.Append(m!=null?m.name:"null");
                sb.Append('\n');
            }
            // Duplicate co-located copies are counted once: their removal is not a visible change.
            return string.Join("\n",sb.ToString().Split('\n').Distinct().OrderBy(s=>s,StringComparer.Ordinal));
        }

        // ---- Play ---------------------------------------------------------------------------

        static IceMechanicalReconciliationValidation()
        {
            if(Application.isBatchMode&&SessionState.GetString(StageKey,"")!="")
            { EditorApplication.update-=Tick;EditorApplication.update+=Tick; }
        }

        static void Play(string stage)
        {
            SessionState.SetString(StageKey,stage);frame=0;
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
                if(stage=="capture"||stage=="verify")
                {
                    if(!EditorApplication.isPlaying||++frame<30)return;
                    var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
                    if(game==null||game.World==null)return;
                    Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
                    if(stage=="verify")PlayChecks(game);
                    PlayCaptures(game,SessionState.GetString(PrefixKey,"After"));
                    if(stage=="verify"){Routes(game.Sim.Config,"authored",game);}
                    Check(errors==0,"zero Unity runtime errors or exceptions in Play ("+errors+")");
                    SessionState.SetString(StageKey,stage+"-exit");
                    EditorApplication.ExitPlaymode();
                }
                else if(stage.EndsWith("-exit")&&!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
                    if(stage=="verify-exit")
                    {
                        var expected=JsonUtility.FromJson<Expected>(File.ReadAllText(Out+"expected.json"));
                        Check(Hash(IceMachineBaker.PrefabPath)==expected.prefabHash&&Hash(IceMachineBaker.AuthoredScenePath)==expected.sceneHash&&
                            Hash(Root+"Machine/AuroraR54.Meshes.asset")==expected.meshesHash,"entering and exiting Play leaves the prefab, meshes and scene byte-identical");
                    }
                    Exit(0);
                }
            });
        }

        static void Render(IceResurfacerGame game,float dt)
        { typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{dt}); }

        static void Park(Simulation s,float x,float z,float heading)
        {
            s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=heading;s.Vehicle.Speed=0;s.Vehicle.SteeringAngle=0;
            float behind=s.Config.HitchBehindAxle+s.Config.DrawbarLength;
            s.Conditioner.Heading=heading;s.Conditioner.X=x-Mathf.Sin(heading)*behind;s.Conditioner.Z=z-Mathf.Cos(heading)*behind;
            s.Controls.HydraulicHoldControls=false;s.Controls.Throttle=0;s.Controls.Steer=0;s.Controls.Brake=false;
        }

        static void PlayChecks(IceResurfacerGame game)
        {
            var expected=JsonUtility.FromJson<Expected>(File.ReadAllText(Out+"expected.json"));
            bool enabled=game.enabled;game.enabled=false;
            try
            {
                game.NewSession(true);var s=game.Sim;var w=game.World;var c=s.Config;Transform m=w.Machine;
                Start("Play: conditioner lowered / raised, blade, cylinders, drawbar");
                Park(s,0,-5,0);
                foreach(float lift in new[]{0f,.5f,1f})
                {
                    s.Conditioner.Lift=lift;s.Controls.BladeDepthMm=0;Render(game,0);
                    Vector3 root=m.InverseTransformPoint(w.Conditioner.position);
                    Check(Mathf.Abs(root.z-expected.authoredConditionerZ)<.0005f&&Mathf.Abs(root.y-lift*.34f)<.0005f&&Mathf.Abs(root.x)<.0005f,
                        "lift "+lift+": visible conditioner at the authored position z "+root.z.ToString("F3")+" (simulation "+(-(c.HitchBehindAxle+c.DrawbarLength)).ToString("F3")+")");
                    Vector3 sim=new Vector3(s.Conditioner.X,0,s.Conditioner.Z),visible=w.Conditioner.position;visible.y=0;
                    Check(Vector3.Distance(sim,visible)<.0005f,"lift "+lift+": simulated cut/flood/pickup centre is under the visible conditioner ("+(Vector3.Distance(sim,visible)*1000).ToString("F2")+" mm)");
                    for(int i=0;i<2;i++)
                    {
                        var rod=w.ConditionerCylinderRods[i];Vector3 tip=rod.position+rod.up*rod.lossyScale.y;
                        float length=Vector3.Distance(w.ConditionerCylinderBase[i].position,w.ConditionerCylinderClevis[i].position);
                        Check(Vector3.Distance(tip,w.ConditionerCylinderClevis[i].position)<.001f&&length>.2f,"lift "+lift+": lift cylinder "+i+" rod on its clevis, length "+length.ToString("F3")+" m");
                        Info("cylinder "+i+" lift "+lift+" length "+length.ToString("F3"));
                    }
                    var drawbar=(Transform)typeof(IceResurfacerGame).GetField("drawbar",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
                    Vector3 a=m.TransformPoint(0,.52f,-c.HitchBehindAxle),b=w.Conditioner.TransformPoint(0,.37f,.26f),half=drawbar.up*drawbar.localScale.y;
                    Check(Vector3.Distance(drawbar.position-half,a)<.001f&&Vector3.Distance(drawbar.position+half,b)<.001f,"lift "+lift+": drawbar spans both hitch points ("+(2*drawbar.localScale.y).ToString("F3")+" m)");
                    if(lift==0)
                    {
                        float edge=w.CuttingBlade.GetComponent<Renderer>().bounds.min.y-IceConditionerAlignment.IceTop;
                        Check(Mathf.Abs(edge)<.0005f,"lowered, depth 0: blade edge on the ice top ("+(edge*1000).ToString("+0.00;-0.00")+" mm)");
                        var towel=Witness(w.DistributionTowel,w.DistributionTowelMesh,m).min.y-IceConditionerAlignment.IceTop;
                        Check(Mathf.Abs(towel)<.0015f,"lowered: towel on the ice ("+(towel*1000).ToString("+0.0;-0.0")+" mm)");
                    }
                }
                // Blade depth follows the simulated bite below the ice top.
                s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;s.Controls.BladeDepthMm=c.MaxBladeDepthMm;
                for(int i=0;i<200;i++)s.Step(.02f);Render(game,0);
                float bite=w.CuttingBlade.GetComponent<Renderer>().bounds.min.y-IceConditionerAlignment.IceTop;
                Check(Mathf.Abs(bite+s.ActualBladeDepthMm*.001f)<.0002f,"blade edge sits "+(bite*1000).ToString("F2")+" mm at actual depth "+s.ActualBladeDepthMm.ToString("F2")+" mm");
                // Cut cells lie under the visible blade/housing.
                game.NewSession(true);s=game.Sim;w=game.World;m=w.Machine;
                Park(s,0,-6,0);s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;s.Controls.BladeDepthMm=.35f;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;
                for(int i=0;i<120;i++)s.Step(.02f);
                s.Controls.Throttle=.3f;for(int i=0;i<150;i++)s.Step(.02f);s.Controls.Throttle=0;s.Vehicle.Speed=0;
                for(int i=0;i<25;i++)s.Step(.02f);
                Render(game,0);
                var ice=s.Ice;float frontCut=float.NegativeInfinity;float minX=9,maxX=-9;
                for(int row=0;row<ice.Rows;row++)for(int col=0;col<ice.Columns;col++)
                {
                    var cell=ice.Cells[ice.Index(col,row)];if(cell.CutDepthMm<=0)continue;
                    Vector3 local=m.InverseTransformPoint(new Vector3(ice.CellX(col),0,ice.CellZ(row)));
                    if(Mathf.Abs(local.x)<1.5f&&local.z>-3&&local.z<1){frontCut=Mathf.Max(frontCut,local.z);minX=Mathf.Min(minX,local.x);maxX=Mathf.Max(maxX,local.x);}
                }
                var housing=Witness(w.ConditionerHousing,w.ConditionerHousingMesh,m);
                Info("cut cells: front edge z "+frontCut.ToString("F3")+", x "+minX.ToString("F3")+".."+maxX.ToString("F3")+"; visible housing z "+housing.min.z.ToString("F3")+".."+housing.max.z.ToString("F3"));
                Check(frontCut<=housing.max.z+ice.CellSize&&frontCut>=housing.min.z,"the front of the shaved band ends under the visible conditioner housing");
                Check(maxX-minX<=c.ConditionerWidth+ice.CellSize&&maxX-minX>=c.ConditionerWidth-2*ice.CellSize,"shaved band width equals the unchanged 1.956 m working width");

                Start("Play: board brush 0/25/50/75/100%, snow cleared under the visible disc");
                BrushChecks(game);

                Start("Play: snow tank lift, door, cylinders and dump outlet");
                game.NewSession(true);s=game.Sim;w=game.World;m=w.Machine;
                Park(s,0,0,0);s.Conditioner.Lift=1;s.Controls.ConditionerTarget=1;
                foreach(float lift in new[]{0f,.25f,.5f,.75f,1f})
                {
                    s.Controls.TankLiftTarget=lift;s.Step(.02f);for(int i=0;i<400&&Mathf.Abs(s.TankLift-lift)>.0001f;i++)s.Step(.02f);
                    Render(game,0);
                    Check(Mathf.Abs(s.TankLift-lift)<.001f,"tank reaches "+(lift*100)+"%");
                    Vector3 outlet=new Vector3(s.DumpOriginX,s.DumpOriginY,s.DumpOriginZ);
                    float dumpError=Vector3.Distance(outlet,w.DumpOutlet.position);
                    Check(dumpError<.002f,"lift "+lift+": simulated dump origin is the visible outlet ("+(dumpError*1000).ToString("F2")+" mm)");
                    c.TankPointToMachine(c.TankLateralOffset,c.TankGeometryY(1.02f),.002f,lift*58*Mathf.Deg2Rad,out float dx,out float dy,out float dz);
                    float doorError=Vector3.Distance(m.TransformPoint(dx,dy,dz),w.TankDoor.position);
                    Check(doorError<.002f,"lift "+lift+": collision shell door hinge is the visible door hinge ("+(doorError*1000).ToString("F2")+" mm)");
                    for(int i=0;i<2;i++)
                    {
                        var rod=w.TankCylinderRods[i];Vector3 tip=rod.position+rod.up*rod.lossyScale.y;
                        Check(Vector3.Distance(tip,w.TankCylinderClevis[i].position)<.001f,"lift "+lift+": tank cylinder "+i+" rod on its clevis (length "+
                            Vector3.Distance(w.TankCylinderBase[i].position,w.TankCylinderClevis[i].position).ToString("F3")+" m)");
                        float tyre=SegmentToTyre(w.TankCylinderBase[i].position,w.TankCylinderClevis[i].position,w.FrontWheels[i],.41f,.0475f);
                        Check(tyre>0,"lift "+lift+": tank cylinder "+i+" clears the front tyre ("+(tyre*1000).ToString("F0")+" mm)");
                    }
                    if(lift>=.75f)Check(w.TankDoor.localEulerAngles.x>180,"door opens");
                    if(lift==0||lift==1)Capture("Tank-"+(lift*100),m,m.TransformPoint(-7,1.6f,1.2f),m.TransformPoint(0,1.6f,1.2f),2.2f);
                }
                s.Controls.TankLiftTarget=0;for(int i=0;i<400&&s.TankLift>0;i++)s.Step(.02f);

                Start("Play: snow through intake, conveyor, tank, overflow and dump");
                SnowChecks(game);

                Start("Play: water distribution and wash pickup follow the visible conditioner");
                game.NewSession(true);s=game.Sim;w=game.World;m=w.Machine;Park(s,0,-6,0);s.Conditioner.Lift=0;Render(game,0);
                Vector3 condLocal(Transform t)=>Quaternion.Inverse(Quaternion.Euler(0,s.Conditioner.Heading*Mathf.Rad2Deg,0))*(t.position-new Vector3(s.Conditioner.X,t.position.y,s.Conditioner.Z));
                Check(Mathf.Abs(condLocal(w.DistributionTowel).z-c.FloodContactFore)<.002f,"towel centre at the simulated flood band (fore "+condLocal(w.DistributionTowel).z.ToString("F3")+")");
                Check(Mathf.Abs(condLocal(w.WashPickup).z-c.WashPickupContactFore)<.002f,"squeegee centre at the simulated wash pickup (fore "+condLocal(w.WashPickup).z.ToString("F3")+")");
                Check(Mathf.Abs(condLocal(w.WaterOutlet).z-c.FloodContactFore)<.02f,"flood emitter over the flood band");

                Start("Play: board guide roller at straight boards, corner and service gate");
                GuideRollerChecks(game);
            }
            finally { game.NewSession(true);game.enabled=enabled; }
        }

        /// <summary>Bounds of a part through its geometry witness mesh (its renderer may be batched in Play).</summary>
        static Bounds Witness(Transform part,Mesh mesh,Transform frame)
        {
            var b=mesh.bounds;var result=new Bounds(frame.InverseTransformPoint(part.TransformPoint(b.center)),Vector3.zero);
            for(int i=0;i<8;i++)result.Encapsulate(frame.InverseTransformPoint(part.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))));
            return result;
        }

        static float SegmentToTyre(Vector3 a,Vector3 b,Transform wheel,float radius,float rodRadius)
        {
            // Tyre as a disc in the wheel's local YZ plane, width along X.
            Transform m=wheel.parent;float best=float.PositiveInfinity;
            for(int i=0;i<=40;i++)
            {
                Vector3 p=wheel.InverseTransformPoint(Vector3.Lerp(a,b,i/40f));
                float radial=new Vector2(p.y,p.z).magnitude-radius,lateral=Mathf.Abs(p.x)-.14f;
                best=Mathf.Min(best,Mathf.Max(radial,lateral)-rodRadius);
            }
            return best;
        }

        static void BrushChecks(IceResurfacerGame game)
        {
            game.NewSession(true);var s=game.Sim;var w=game.World;var c=s.Config;Transform m=w.Machine;
            var rig=w.BrushArm.GetComponent<IceBoardBrushRig>();
            Check(rig!=null&&rig.IsComplete,"Play uses the authored brush rig");
            var mech=IceBoardBrushRig.Mechanism.From(c);
            // Body 65 mm from the left boards: the simulated guide roller (40 mm outboard of the body) rides 25 mm off.
            float x=-c.RinkWidth*.5f+c.VehicleHalfWidth+.065f,z=-8;
            // One step refreshes the simulated brush contact after the teleport.
            Park(s,x,z,0);s.Step(.02f);Render(game,0);
            Vector3 stowArm=m.InverseTransformPoint(rig.transform.position),stowHead=m.InverseTransformPoint(rig.head.position);
            var expectedHead=IceBoardBrushRig.ContactLocal(mech,0);
            Check(new Vector2(stowHead.x-expectedHead.x,stowHead.z-expectedHead.z).magnitude<.0005f,"stowed in Play exactly at the authored stowed head (no snapping)");
            s.Controls.BoardBrush=true;s.Controls.Throttle=.3f;
            float last=0,maxError=0,maxJump=0;Vector3 lastHead=rig.head.position;bool reversed=false;int swept=0,checkedCells=0;
            var marks=new HashSet<int>();bool contactSeen=false;
            for(int step=0;step<260;step++)
            {
                Vector3 under=rig.head.position;float before=SnowAt(s,under);
                s.Step(.02f);Render(game,.02f);
                maxError=Mathf.Max(maxError,new Vector2(rig.head.position.x-s.BrushWorldX,rig.head.position.z-s.BrushWorldZ).magnitude);
                if(step>0)maxJump=Mathf.Max(maxJump,Vector3.Distance(rig.head.position,lastHead)-Mathf.Abs(s.Vehicle.Speed)*.02f);lastHead=rig.head.position;
                reversed|=s.BrushDeployment<last-1e-6f;last=s.BrushDeployment;
                foreach(float mark in new[]{0f,.25f,.5f,.75f,1f})
                {
                    int key=(int)(mark*100);
                    if(!marks.Contains(key)&&s.BrushDeployment>=mark-1e-4f)
                    {
                        marks.Add(key);
                        float yaw=-Mathf.DeltaAngle(0,(Quaternion.Inverse(m.rotation)*rig.transform.rotation).eulerAngles.y);
                        float degrees=BoardBrushArticulation.ArmDegrees(c,s.BrushDeployment);
                        Vector3 from=rig.transform.TransformPoint(rig.linkPivotOffset),to=rig.head.position+m.rotation*rig.linkHeadOffset,half=rig.link.up*rig.link.lossyScale.y;
                        float link=Mathf.Min(Vector3.Distance(rig.link.position-half,from)+Vector3.Distance(rig.link.position+half,to),Vector3.Distance(rig.link.position+half,from)+Vector3.Distance(rig.link.position-half,to));
                        Check(Mathf.Abs(Mathf.DeltaAngle(yaw,degrees))<.01f&&link<.001f&&Vector3.Distance(rig.roller.position,rig.head.position+m.rotation*rig.rollerOffset)<.0001f,
                            "deployment "+key+"%: arm "+yaw.ToString("F1")+" deg, link attached ("+(link*1000).ToString("F2")+" mm), roller on the head");
                        CaptureBrush("Brush-"+key,m,rig);
                    }
                }
                // Sweeping (not only kickplate contact) runs above half deployment.
                if(before>0&&s.BrushDeployment>.5f&&Mathf.Abs(s.Vehicle.Speed)>.01f){checkedCells++;if(SnowAt(s,under)<before)swept++;}
                contactSeen|=s.BrushContact;
            }
            Check(contactSeen,"the working brush reaches the kickplate when the machine runs along the boards");
            Check(s.BrushDeployment>.999f&&!reversed,"brush deploys fully without reversing");
            Check(maxError<.0001f,"visible disc stays on the simulated contact (max "+(maxError*1000).ToString("F3")+" mm)");
            Check(maxJump<.05f,"articulation is smooth (largest step beyond travel "+(maxJump*1000).ToString("F1")+" mm per 20 ms)");
            Check(s.BrushSweptSnowM3>0,"working brush sweeps snow ("+(s.BrushSweptSnowM3*1000).ToString("F2")+" L)");
            Check(checkedCells>0&&swept>=checkedCells*.9f,"snow cleared under the visible disc ("+swept+"/"+checkedCells+" frames)");
            float board=s.Ice.BoardDistance(s.BrushWorldX,s.BrushWorldZ,out _,out _);
            Info("working brush: disc centre "+(-board).ToString("F3")+" m from the boards, contact="+s.BrushContact+", radius "+c.BrushRadius.ToString("F3"));
            s.Controls.BoardBrush=false;s.Controls.Throttle=0;s.Vehicle.Speed=0;
            for(int i=0;i<160;i++){s.Step(.02f);Render(game,.02f);}
            Check(s.BrushDeployment<.001f&&Vector3.Distance(m.InverseTransformPoint(rig.transform.position),stowArm)<.0005f,"retracts to the authored stowed pose");
        }

        static float SnowAt(Simulation s,Vector3 p)
        {
            var ice=s.Ice;int col=(int)Math.Floor((p.x-ice.MinX)/ice.CellSize),row=(int)Math.Floor((p.z-ice.MinZ)/ice.CellSize);
            if(col<0||row<0||col>=ice.Columns||row>=ice.Rows)return 0;
            return ice.Cells[ice.Index(col,row)].SnowMm;
        }

        static void SnowChecks(IceResurfacerGame game)
        {
            game.NewSession(true);var s=game.Sim;var w=game.World;var c=s.Config;Transform m=w.Machine;
            Park(s,0,-22,0);s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;s.Controls.BladeDepthMm=.6f;s.Controls.HorizontalAuger=s.Controls.VerticalAuger=true;
            for(int i=0;i<100;i++)s.Step(.02f);
            s.Controls.Throttle=.3f;
            ParticleSystem discharge=null;
            foreach(var ps in game.GetComponentsInChildren<ParticleSystem>(true))if(ps.name.Contains("conveyed into inspection bin"))discharge=ps;
            bool emitted=false;float maxError=0;
            for(int i=0;i<600;i++)
            {
                s.Step(.02f);Render(game,.02f);
                if(s.DischargeSnowRate>0){emitted=true;maxError=Mathf.Max(maxError,Vector3.Distance(discharge.transform.position,w.TankDischarge.position));}
            }
            Vector3 sim=m.TransformPoint(c.ConveyorOutletX,c.ConveyorOutletY,c.ConveyorOutletZ);
            Check(Vector3.Distance(sim,w.TankDischarge.position)<.001f,"simulated discharge coordinate is the visible discharge ("+(Vector3.Distance(sim,w.TankDischarge.position)*1000).ToString("F2")+" mm)");
            Check(emitted&&maxError<.001f&&s.SnowTankM3>0,"conveyed snow is emitted at the visible discharge and fills the tank ("+(s.SnowTankM3*1000).ToString("F1")+" L)");
            Check(IceMechanicalAlignment.InletProblem(w)==null,"the discharge looks through the tank's rear-wall opening (no panel in the way, no open gap)");
            Capture("Conveyor-Discharge-Side",m,m.TransformPoint(4,1.5f,-.1f),m.TransformPoint(0,1.5f,-.1f),.75f);
            // Tank raised while conveying: missed-bin spill leaves from the same visible chute.
            s.Controls.TankLiftTarget=.3f;for(int i=0;i<300;i++){s.Step(.02f);Render(game,.02f);}
            s.Controls.Throttle=0;s.Vehicle.Speed=0;
            float spill=Vector3.Distance(new Vector3(s.UpperSnowOriginX,s.UpperSnowOriginY,s.UpperSnowOriginZ),w.TankDischarge.position);
            Check(s.SpilledSnowM3>0&&spill<.001f,"raised bin: conveyed snow spills from the visible chute ("+(spill*1000).ToString("F2")+" mm, "+(s.SpilledSnowM3*1000).ToString("F1")+" L)");
            s.Controls.HorizontalAuger=s.Controls.VerticalAuger=false;s.Controls.BladeDepthMm=0;s.Controls.TankLiftTarget=0;
            for(int i=0;i<300;i++)s.Step(.02f);
            // Fill and level mapping: capacity is calibration, the fill follows the enlarged shell.
            Check(Mathf.Abs(c.SnowCapacityM3-3.54f)<1e-5f,"snow capacity unchanged (3.54 m3)");
            var snow=w.TankSnow.GetComponent<MeshFilter>().sharedMesh;
            Info("tank fill "+(s.SnowTankM3/c.SnowCapacityM3*100).ToString("F1")+"%, visible surface height in shell "+snow.bounds.max.y.ToString("F3")+" (tank-local, before scale)");
            Capture("Tank-Fill",m,m.TransformPoint(0,4,1.2f),m.TransformPoint(0,0,1.2f),1.4f);
        }

        static void GuideRollerChecks(IceResurfacerGame game)
        {
            foreach(var scenario in new[]{("straight left boards",0f),("rounded corner",1f),("service gate approach",2f)})
            {
                game.NewSession(true);var s=game.Sim;var w=game.World;var c=s.Config;Transform m=w.Machine;
                if(scenario.Item2==0){Park(s,-c.RinkWidth*.5f+c.VehicleHalfWidth+.30f,-5,-.06f);}
                else if(scenario.Item2==1){float r=c.CornerRadius;Park(s,-c.RinkWidth*.5f+r-(r-c.VehicleHalfWidth-.3f)*.7071f,c.RinkLength*.5f-r+(r-c.VehicleHalfWidth-.3f)*.7071f-2.4f,-.75f);}
                else {s.Layout.ParkingPose(out float px,out float pz,out float ph);Park(s,px,pz,ph);}
                s.Conditioner.Lift=1;s.Controls.ConditionerTarget=1;
                // Rink cases steer into the left boards; the gate case reverses out of the nose-in bay past the gate edge.
                s.Controls.Throttle=scenario.Item2==2?-.6f:.35f;s.Controls.Steer=scenario.Item2==2?.35f:-.25f;
                bool contact=false;float minGap=float.PositiveInfinity,gapAtContact=float.NaN;int penetrating=0;
                float radius=IceMechanicalAlignment.GuideRollerRadius(w);
                for(int i=0;i<500;i++)
                {
                    s.Step(.02f);Render(game,.02f);
                    Vector3 roller=w.GuideRoller.position;
                    // Penetration: any point of the visible tyre rim off drivable ice or service floor.
                    for(int k=0;k<12;k++){float a=k*Mathf.PI/6;if(!s.Navigable(roller.x+Mathf.Cos(a)*radius,roller.z+Mathf.Sin(a)*radius)){penetrating++;break;}}
                    if(!s.Ice.IsInside(roller.x,roller.z)||s.Layout.IsPortal(roller.x,roller.z))continue;
                    float d=-s.Ice.BoardDistance(roller.x,roller.z,out _,out _)-radius;
                    minGap=Mathf.Min(minGap,d);
                    if((s.GuideRollerContact||s.BoardContact)&&!contact){contact=true;gapAtContact=d;}
                }
                Check(penetrating==0,scenario.Item1+": the visible roller never enters the boards or walls ("+penetrating+" frames)");
                Info("GUIDE ROLLER | "+scenario.Item1+" | board/roller contact "+contact+" | visible roller tyre "+(float.IsNaN(gapAtContact)?"n/a":(gapAtContact*1000).ToString("F0")+" mm")+
                    " from the boards at first simulated contact | closest "+(minGap*1000).ToString("F0")+" mm");
                if(scenario.Item2==0)Capture("GuideRoller-AtBoards",m,m.TransformPoint(-.85f,.6f,6),m.TransformPoint(-.85f,.6f,0),.9f,false);
            }
        }

        // ---- GPS ----------------------------------------------------------------------------

        static void Routes(SimulatorConfig current,string scene,IceResurfacerGame game=null)
        {
            Start("GPS routes and guides vs the "+scene+" scene calibration");
            string root=RouteRecordingStorage.DefaultDirectory,guides=Path.Combine(root,"Guides");
            var before=new Dictionary<string,string>();
            foreach(var f in Directory.GetFiles(root,"*.json",SearchOption.AllDirectories))before[f]=Hash(f);
            foreach(string path in Directory.GetFiles(root,"*.route.json").Concat(Directory.GetFiles(guides,"*.guide.json")).Concat(Directory.GetFiles(Path.Combine(guides,"AutoFit"),"*.guide.json")))
            {
                try
                {
                    SimulatorConfig recorded;string label;double length;
                    if(path.EndsWith(".route.json")){var raw=RouteRecordingStorage.Read(path);recorded=raw.configuration;length=raw.distanceMeters;label="route";}
                    else{var g=TrainingGuideStorage.Read(path);recorded=g.configuration;length=TrainingGuideStorage.Raw(g).distanceMeters;label="guide";}
                    var diff=new RouteFit{configuration=recorded}.CalibrationDifferences(current);
                    Info("GPS | "+scene+" | "+label+" | "+Path.GetFileName(path)+" | "+length.ToString("F3")+" m | "+(diff.Count==0?"COMPATIBLE":"INCOMPATIBLE: "+string.Join("; ",diff)));
                }
                catch(Exception e){Info("GPS | "+scene+" | "+Path.GetFileName(path)+" | unreadable: "+e.Message);}
            }
            if(game!=null)
            {
                // New calibrated copies, never in place. Andy_Try_1 is the essential operator route.
                string andy=Directory.GetFiles(root,"Andy_Try_1_*.route.json").Single();
                var andyRaw=RouteRecordingStorage.Read(andy);
                Check(Math.Abs(andyRaw.distanceMeters-1003.234)<.001,"Andy_Try_1 raw recording is the 1,003.234 m operator drive ("+andyRaw.distanceMeters.ToString("F6")+" m)");
                var latest=Directory.GetFiles(guides,"Andy_Try_1___recorded_track__*.guide.json").OrderBy(p=>p).Last();
                var original=TrainingGuideStorage.Read(latest);string originalJson=TrainingGuideStorage.Encode(original);
                var copy=TrainingGuideStorage.RevalidateCopy(original,current,out var fit);Label(copy,original);
                string saved=TrainingGuideStorage.SaveCopy(guides,copy);
                Info("GPS | new calibrated guide "+Path.GetFileName(saved)+" from "+Path.GetFileName(latest));
                var reread=TrainingGuideStorage.Read(saved);var refit=RouteFitter.Fit(reread,TrainingGuideStorage.Raw(reread));
                Check(reread.id!=original.id&&reread.rawRecordingJson==original.rawRecordingJson&&reread.sourceSha256==original.sourceSha256,"new guide identity keeps the exact embedded recording and checksum");
                Check(refit.Ready&&refit.validationPerformed&&refit.boundaryErrors==0&&refit.curvatureErrors==0&&refit.Compatible(current),
                    "Andy_Try_1 guide revalidates against the reconciled machine: body, recorded and CURRENT attached conditioner clearance, curvature, strict compatibility");
                Check(Math.Abs(refit.Length-andyRaw.distanceMeters)<.000001&&refit.maximumRawDeviation==0,"guide keeps "+refit.Length.ToString("F3")+" m and zero raw-trajectory displacement");
                bool controls=reread.controls.Count==original.controls.Count;
                for(int i=0;controls&&i<original.controls.Count;i++)controls=JsonUtility.ToJson(reread.controls[i])==JsonUtility.ToJson(original.controls[i]);
                Check(controls,"all "+original.controls.Count+" control points unchanged");
                Check(TrainingGuideStorage.Encode(original)==originalJson,"the earlier guide is not mutated");
                Info("GPS | clearance: min body "+FieldOr(refit,"minimumBodyClearance")+" min conditioner "+FieldOr(refit,"minimumConditionerClearance"));
                // Old guide is still rejected (no silent compatibility).
                var owner=game.RouteGuide;owner.Refresh();
                Check(!owner.UseSavedRouteAsGps(latest)&&owner.TrainingStatus.Contains("DrawbarLength"),"the pre-reconciliation guide is rejected with its DrawbarLength difference: "+owner.TrainingStatus.Replace("\n"," / "));
                Check(owner.UseSavedRouteAsGps(saved),"the new calibrated guide is accepted as GPS: "+owner.TrainingStatus);
                // Deterministic guidance replay over every recorded pose.
                double prior=0;bool ordered=true;int priorLeg=0;
                foreach(var p in andyRaw.samples)
                {
                    owner.Progress.Observe(p.vehicleX,p.vehicleZ,p.vehicleHeadingRadians,p.speedMetersPerSecond);
                    ordered&=owner.Progress.Distance>=prior&&owner.Progress.Leg>=priorLeg&&owner.Progress.Distance<=p.distanceMeters+.25;
                    prior=owner.Progress.Distance;priorLeg=owner.Progress.Leg;
                }
                Check(ordered&&Math.Abs(owner.Progress.Distance-refit.Length)<.000001,"guidance replay of all "+andyRaw.samples.Count+" recorded poses advances in order to the end");
                // Physical replay: every recorded vehicle pose against today's body envelope.
                var s=game.Sim;int body=0;
                foreach(var p in andyRaw.samples)if(!s.ProbeVehiclePose(p.vehicleX,p.vehicleZ,p.vehicleHeadingRadians,out _))body++;
                Check(body==0,"physical replay: all "+andyRaw.samples.Count+" recorded vehicle poses fit the boards and service walls ("+body+" contacts)");
                // Calibrated copies of every other saved guide, where the full checks allow it.
                foreach(string other in Directory.GetFiles(guides,"*.guide.json").Concat(Directory.GetFiles(Path.Combine(guides,"AutoFit"),"*.guide.json")).Where(p=>before.ContainsKey(p)&&p!=latest))
                {
                    try
                    {
                        var g=TrainingGuideStorage.Read(other);var calibrated=TrainingGuideStorage.RevalidateCopy(g,current,out var f);Label(calibrated,g);
                        string path=TrainingGuideStorage.SaveCopy(guides,calibrated);
                        Info("GPS | new calibrated guide "+Path.GetFileName(path)+" from "+Path.GetFileName(other)+" | ready="+f.Ready+" boundary="+f.boundaryErrors+" curvature="+f.curvatureErrors+" length "+f.Length.ToString("F3")+" m");
                    }
                    catch(Exception e){Info("GPS | NOT revalidated "+Path.GetFileName(other)+": "+e.Message);}
                }
                foreach(var f in before)Check(File.Exists(f.Key)&&Hash(f.Key)==f.Value,"unchanged: "+f.Key.Substring(root.Length));
            }
        }
        /// <summary>Names a revalidated copy after its source and the scene it was validated for.</summary>
        static void Label(TrainingRouteDefinition copy,TrainingRouteDefinition source)
        {
            const string suffix=" / authored Aurora calibration";
            string name=source.name.EndsWith(" / current calibration")?source.name.Substring(0,source.name.Length-22):source.name;
            copy.name=name.Substring(0,Math.Min(name.Length,100-suffix.Length))+suffix;
        }
        static string FieldOr(RouteFit fit,string name){ var f=typeof(RouteFit).GetField(name);return f!=null?Convert.ToString(f.GetValue(fit)):"n/a"; }

        // ---- Captures -------------------------------------------------------------------------

        static void PlayCaptures(IceResurfacerGame game,string prefix)
        {
            Start("Captures "+prefix);
            bool enabled=game.enabled;game.enabled=false;
            try
            {
                game.NewSession(true);var s=game.Sim;var w=game.World;Transform m=w.Machine;
                Park(s,0,-3,0);
                s.Conditioner.Lift=0;Render(game,0);
                Shoot(prefix+"-Play-Side-Lowered",m,m.TransformPoint(-8,1,.6f),m.TransformPoint(0,1,.6f),2.2f);
                Shoot(prefix+"-Play-Rear-Conditioner",m,m.TransformPoint(0,1,-8),m.TransformPoint(0,1,0),1.6f);
                Shoot(prefix+"-Play-Conditioner-Side",m,m.TransformPoint(-6,.6f,-.9f),m.TransformPoint(0,.6f,-.9f),1.0f);
                var blade=w.CuttingBlade.GetComponent<Renderer>().bounds;
                Info(prefix+" lowered: root z "+m.InverseTransformPoint(w.Conditioner.position).z.ToString("F3")+", blade edge "+((blade.min.y-IceConditionerAlignment.IceTop)*1000).ToString("F1")+" mm above ice, cylinder "+
                    Vector3.Distance(w.ConditionerCylinderBase[0].position,w.ConditionerCylinderClevis[0].position).ToString("F3")+" m");
                s.Conditioner.Lift=1;Render(game,0);
                Shoot(prefix+"-Play-Conditioner-Raised-Side",m,m.TransformPoint(-6,.6f,-.9f),m.TransformPoint(0,.6f,-.9f),1.0f);
                Info(prefix+" raised: cylinder "+Vector3.Distance(w.ConditionerCylinderBase[0].position,w.ConditionerCylinderClevis[0].position).ToString("F3")+" m");
                // Brush deployed.
                s.Controls.BoardBrush=true;for(int i=0;i<120;i++){s.Step(.02f);Render(game,.02f);}
                Shoot(prefix+"-Play-Brush-Deployed-Top",m,m.TransformPoint(-.7f,5,1.1f),m.TransformPoint(-.7f,0,1.1f),.8f);
                Shoot(prefix+"-Play-Brush-Deployed-Front",m,m.TransformPoint(-.8f,.4f,6),m.TransformPoint(-.8f,.4f,0),.8f);
                s.Controls.BoardBrush=false;for(int i=0;i<120;i++){s.Step(.02f);Render(game,.02f);}
                // Tank raised.
                s.Controls.TankLiftTarget=1;for(int i=0;i<400;i++)s.Step(.02f);Render(game,0);
                Shoot(prefix+"-Play-Tank-Raised-Side",m,m.TransformPoint(-8,1.6f,1.2f),m.TransformPoint(0,1.6f,1.2f),2.2f);
                s.Controls.TankLiftTarget=0;for(int i=0;i<400;i++)s.Step(.02f);Render(game,0);
                Shoot(prefix+"-Play-Conveyor-Discharge-Side",m,m.TransformPoint(4,1.5f,-.1f),m.TransformPoint(0,1.5f,-.1f),.75f);
                Shoot(prefix+"-Play-Conveyor-Discharge-Top",m,m.TransformPoint(0,5,0),m.TransformPoint(0,1,0),.9f);
                Performance(game);
            }
            finally { game.NewSession(true);game.enabled=enabled; }
        }

        /// <summary>Per-frame machine cost (simulation step + machine presentation) with everything
        /// working, and memory. Editor batch measurement: compare before/after, not absolute frame time.</summary>
        static void Performance(IceResurfacerGame game)
        {
            game.NewSession(true);var s=game.Sim;
            Park(s,0,-20,0);s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;s.Controls.BladeDepthMm=.35f;
            s.Controls.HorizontalAuger=s.Controls.VerticalAuger=s.Controls.BoardBrush=s.Controls.WashPump=true;s.Controls.FloodFlow=.3f;s.Controls.WashValve=.25f;s.Controls.Throttle=.3f;
            for(int i=0;i<60;i++){s.Step(.02f);Render(game,.02f);}
            var times=new List<double>();var watch=new System.Diagnostics.Stopwatch();
            long before=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            for(int i=0;i<600;i++){watch.Restart();s.Step(.02f);Render(game,.02f);watch.Stop();times.Add(watch.Elapsed.TotalMilliseconds);}
            long after=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            times.Sort();
            int renderers=game.GetComponentsInChildren<Renderer>().Count(r=>r.enabled);
            Info("PERF | machine step+presentation over 600 frames: mean "+times.Average().ToString("F3")+" ms, p95 "+times[(int)(times.Count*.95f)].ToString("F3")+" ms, max "+times.Last().ToString("F3")+
                " ms | managed+native allocated "+(after/1048576.0).ToString("F1")+" MB (delta "+((after-before)/1024.0).ToString("F0")+" KB) | enabled renderers "+renderers+
                " | mono heap "+(UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()/1048576.0).ToString("F1")+" MB");
            s.Controls.Throttle=0;
        }

        static void CaptureBrush(string label,Transform m,IceBoardBrushRig rig)
        {
            Vector3 centre=m.TransformPoint(rig.mechanism.PivotX-.25f,.2f,rig.mechanism.PivotZ-.2f);
            Shoot(SessionState.GetString(PrefixKey,"After")+"-"+label+"-Top",m,centre+m.up*4,centre,.9f);
        }
        static void Capture(string label,Transform m,Vector3 from,Vector3 at,float size,bool hideArena=true)
        { Shoot(SessionState.GetString(PrefixKey,"After")+"-"+label,m,from,at,size,hideArena); }

        static void Shoot(string label,Transform m,Vector3 from,Vector3 at,float size,bool hideArena=true)
        {
            var arena=UnityEngine.Object.FindAnyObjectByType<IceArenaRig>(FindObjectsInactive.Include);
            bool active=arena!=null&&arena.gameObject.activeSelf;
            if(hideArena&&arena!=null)arena.gameObject.SetActive(false);
            var node=new GameObject("Reconcile camera"){hideFlags=HideFlags.HideAndDontSave};var lightNode=new GameObject("Reconcile light"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var light=lightNode.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;lightNode.transform.rotation=m.rotation*Quaternion.Euler(40,60,0);
                var cam=node.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.02f;cam.farClipPlane=60;
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.42f,.45f,.5f);
                Vector3 d=at-from;node.transform.SetPositionAndRotation(from,Quaternion.LookRotation(d,Mathf.Abs(Vector3.Dot(d.normalized,m.up))>.9f?m.forward:m.up));
                Directory.CreateDirectory(Out+"Shots");
                for(int i=0;i<2;i++)IceResurfacerEditor.Capture(cam,"MechanicalReconciliation/Shots/warm.png",320,180);
                IceResurfacerEditor.Capture(cam,"MechanicalReconciliation/Shots/"+label+".png",1200,800);
                File.Delete(Out+"Shots/warm.png");
            }
            finally{UnityEngine.Object.DestroyImmediate(node);UnityEngine.Object.DestroyImmediate(lightNode);if(arena!=null)arena.gameObject.SetActive(active);}
        }

        // ---- Helpers ------------------------------------------------------------------------

        static IceMachineRig OpenScene()
        {
            RequireIsolated();
            EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
            var rig=UnityEngine.Object.FindAnyObjectByType<IceMachineRig>(FindObjectsInactive.Include);
            if(rig==null)throw new InvalidOperationException("No authored machine in "+IceMachineBaker.AuthoredScenePath);
            return rig;
        }
        static IceResurfacerGame Game(){ return UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>(FindObjectsInactive.Include); }
        static string Arg(string name,string fallback)
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]==name)return args[i+1];
            return fallback;
        }
        static string PathOf(Transform t,Transform root){ string p=t.name+"#"+t.GetSiblingIndex(); while(t.parent!=null&&t.parent!=root){t=t.parent;p=t.name+"#"+t.GetSiblingIndex()+"/"+p;} return p; }
        static string V(Vector3 v,int d=3){ string f="F"+d; return "("+v.x.ToString(f,System.Globalization.CultureInfo.InvariantCulture)+","+v.y.ToString(f,System.Globalization.CultureInfo.InvariantCulture)+","+v.z.ToString(f,System.Globalization.CultureInfo.InvariantCulture)+")"; }
        static string Q(Quaternion q){ return "("+q.x.ToString("F5")+","+q.y.ToString("F5")+","+q.z.ToString("F5")+","+q.w.ToString("F5")+")"; }
        static string Hash(string path)
        { using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
        static void RequireIsolated()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))
                throw new InvalidOperationException("Mechanical reconciliation validation writes assets; run it only in the isolated StageUnityValidation.ps1 project.");
        }
        static void Begin(string prefix,Action body)
        {
            // Screenshots share Before/After prefixes; each entry point keeps its own results file.
            SessionState.SetString(PrefixKey,prefix.StartsWith("Capture")?prefix.Substring(7):prefix=="Verify"?"After":prefix);
            SessionState.SetString(ResultsKey,prefix);
            Directory.CreateDirectory(Out);File.WriteAllText(ResultsPath,"");errors=0;
            Run(body);
        }
        static void Start(string step){ File.AppendAllText(ResultsPath,"== "+step+" / "+DateTime.Now.ToString("s")+"\n"); }
        static void Info(string message){ File.AppendAllText(ResultsPath,"INFO "+message+"\n"); }
        static void Check(bool valid,string message)
        {
            if(!valid)throw new InvalidOperationException("Reconciliation: "+message);
            File.AppendAllText(ResultsPath,"PASS "+message+"\n");
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
        static void Exit(int code){ Debug.Log("AURORA RECONCILIATION exit="+code);EditorApplication.Exit(code); }
    }
}
