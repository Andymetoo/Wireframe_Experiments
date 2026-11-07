using System;
using System.Collections.Generic;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Board brush rig Inspector: draft mechanism, cosmetic offsets, an Edit Mode deployment
    /// preview (0-100%), Scene-view handles and gizmos, and the explicit
    /// "Apply Brush Geometry to Simulation". Preview poses are never saved: every scene or
    /// prefab save, Play Mode entry and Reset Preview returns the brush to its stowed pose.
    /// </summary>
    [CustomEditor(typeof(IceBoardBrushRig))]
    public sealed class IceBoardBrushRigEditor : UnityEditor.Editor
    {
        const string MotorName="Brush motor housing", GuardName="Brush plate";
        static readonly Dictionary<int,float> preview=new Dictionary<int,float>();
        // 0 = STOWED, 1 = WORKING. The saved endpoints live on the component, not here.
        static readonly Dictionary<int,int> endpointEditing=new Dictionary<int,int>();
        static bool previewSimulation;
        static bool ShowGizmos { get { return EditorPrefs.GetBool("IceBoardBrushRig.Gizmos",true); } set { EditorPrefs.SetBool("IceBoardBrushRig.Gizmos",value); } }

        [InitializeOnLoadMethod]
        static void HookSaves()
        {
            EditorSceneManager.sceneSaving+=(scene,path)=>{ foreach(var root in scene.GetRootGameObjects())Stow(root); };
            UnityEditor.SceneManagement.PrefabStage.prefabSaving+=root=>Stow(root);
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.ExitingEditMode)return;
                for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                    foreach(var root in UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).GetRootGameObjects())Stow(root);
            };
            Undo.undoRedoPerformed+=()=>{ foreach(var rig in UnityEngine.Object.FindObjectsByType<IceBoardBrushRig>(FindObjectsInactive.Include,FindObjectsSortMode.None))Repose(rig); };
            AssemblyReloadEvents.beforeAssemblyReload+=()=>
            {
                foreach(var rig in Resources.FindObjectsOfTypeAll<IceBoardBrushRig>())
                {
                    if(EditorUtility.IsPersistent(rig)||!rig.gameObject.scene.IsValid())continue;
                    endpointEditing.Remove(rig.GetInstanceID());preview.Remove(rig.GetInstanceID());Repose(rig);
                }
            };
        }

        static void Stow(GameObject root)
        {
            if(Application.isPlaying)return;
            foreach(var rig in root.GetComponentsInChildren<IceBoardBrushRig>(true)) { endpointEditing.Remove(rig.GetInstanceID()); preview.Remove(rig.GetInstanceID()); Repose(rig); }
        }

        public static float PreviewDeployment(IceBoardBrushRig rig)
        {
            return preview.TryGetValue(rig.GetInstanceID(),out float d)?d:0;
        }

        static IceCalibrationContext.Context Context(IceBoardBrushRig rig)
        {
            var machineRig=rig.GetComponentInParent<IceMachineRig>();
            return IceCalibrationContext.Resolve(rig,machineRig!=null?machineRig.BakedConfigJson:null);
        }

        /// <summary>The scene calibration the brush is measured against (also in Prefab Mode); game is set only where Apply may write.</summary>
        static SimulatorConfig Simulation(IceBoardBrushRig rig,out IceResurfacerGame game)
        {
            var context=Context(rig);
            game=context.Writable?context.Game:null;
            return context.Config;
        }

        static IceBoardBrushRig.Mechanism Shown(IceBoardBrushRig rig)
        {
            if(rig.useAuthoredEndpoints&&rig.stowedEndpointSaved&&rig.workingEndpointSaved&&rig.TryEndpointMechanism(out var authored,out _))return authored;
            return previewSimulation?IceBoardBrushRig.Mechanism.From(Simulation(rig,out _)):rig.mechanism;
        }

        /// <summary>Poses the brush at its preview deployment (stowed when not previewing).
        /// The stowed pose is recorded on prefab instances so it is what gets saved.</summary>
        public static void Repose(IceBoardBrushRig rig)
        {
            if(rig==null||!rig.IsComplete||Application.isPlaying)return;
            float d=PreviewDeployment(rig);
            bool approved=rig.useAuthoredEndpoints&&rig.stowedEndpointSaved&&rig.workingEndpointSaved&&rig.TryEndpointMechanism(out _,out _);
            if(endpointEditing.ContainsKey(rig.GetInstanceID())||approved)rig.PoseEndpointPreview(d);
            else { rig.RestoreEndpointPreviewRotation();rig.PosePreview(Shown(rig),d); }
            if(d==0&&PrefabUtility.IsPartOfPrefabInstance(rig))
                foreach(var t in new[]{rig.transform,rig.head,rig.link,rig.roller})
                {
                    if(SnapToSource(t))PrefabUtility.RevertObjectOverride(t,InteractionMode.AutomatedAction);
                    else PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                }
        }

        // A re-posed part that equals its prefab pose up to float noise takes that exact pose and carries no
        // scene override (its pose is the prefab's); only real differences are recorded as overrides.
        static bool SnapToSource(Transform t)
        {
            var source=PrefabUtility.GetCorrespondingObjectFromSource(t);
            if(source==null)return false;
            bool p=(t.localPosition-source.localPosition).sqrMagnitude<1e-10f,r=Quaternion.Angle(t.localRotation,source.localRotation)<.001f,s=(t.localScale-source.localScale).sqrMagnitude<1e-10f;
            if(p)t.localPosition=source.localPosition;
            if(r)t.localRotation=source.localRotation;
            if(s)t.localScale=source.localScale;
            return p&&r&&s;
        }

        void OnEnable()
        {
            var rig=(IceBoardBrushRig)target;
            if(rig!=null&&!Application.isPlaying)Repose(rig);
        }

        void OnDisable()
        {
            // Leaving the rig always returns the saved state to the stowed pose.
            var rig=target as IceBoardBrushRig;
            if(rig!=null&&!Application.isPlaying)
            {
                bool editing=endpointEditing.Remove(rig.GetInstanceID());
                if(editing||PreviewDeployment(rig)!=0){preview.Remove(rig.GetInstanceID());Repose(rig);}
            }
        }

        public override void OnInspectorGUI()
        {
            var rig=(IceBoardBrushRig)target;
            if(!rig.IsComplete){EditorGUILayout.HelpBox("Machine, head, link and roller must be assigned (head under this arm).",MessageType.Error);DrawDefaultInspector();return;}
            var context=Context(rig);
            var config=context.Config;var game=context.Writable?context.Game:null;
            AdoptManualMove(rig);
            serializedObject.Update();
            IceCalibrationContext.DrawLabel(context);

            EndpointInspector(rig);
            EditorGUILayout.LabelField("BOARD CONTACT SPRING",EditorStyles.boldLabel);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("complianceTravel"),new GUIContent("Compliance travel (m)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("springReturn"),new GUIContent("Spring return (rad/s)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("springDamping"),new GUIContent("Damping ratio"));
            serializedObject.ApplyModifiedProperties();
            if(endpointEditing.ContainsKey(rig.GetInstanceID()))return;

            EditorGUILayout.LabelField("MECHANISM / where snow is swept (SWEEP handles; draft until applied)",EditorStyles.boldLabel);
            var m=serializedObject.FindProperty("mechanism");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m.FindPropertyRelative("PivotX"),new GUIContent("Pivot lateral X (m)","Machine space; negative is the left side."));
            EditorGUILayout.PropertyField(m.FindPropertyRelative("PivotZ"),new GUIContent("Pivot forward Z (m)","From the rear axle; positive is forward."));
            EditorGUILayout.PropertyField(m.FindPropertyRelative("ArmLength"),new GUIContent("Arm length (m)","Pivot to brush disc centre."));
            EditorGUILayout.PropertyField(m.FindPropertyRelative("Radius"),new GUIContent("Brush contact radius (m)"));
            if(rig.useAuthoredEndpoints&&rig.TryEndpointMechanism(out var endpointMechanism,out _))
            {
                using(new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.FloatField("Working angle (from endpoint)",endpointMechanism.DeployedDegrees);
                    EditorGUILayout.FloatField("Stowed angle (from endpoint)",endpointMechanism.RetractedDegrees);
                }
            }
            else
            {
                EditorGUILayout.PropertyField(m.FindPropertyRelative("DeployedDegrees"),new GUIContent("Working angle (deg)","0 = arm straight out to the left; positive swings the head rearward."));
                EditorGUILayout.PropertyField(m.FindPropertyRelative("RetractedDegrees"),new GUIContent("Stowed angle (deg)"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("COSMETIC / never affects contact (COSMETIC handles)",EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pivotHeight"),new GUIContent("Pivot height (m)"));
            if(rig.useAuthoredEndpoints)EditorGUILayout.LabelField("Head heights / tilt", "Edit the STOWED / WORKING endpoints above");
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("deployedHeadHeight"),new GUIContent("Head height, working (m)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("retractedHeadHeight"),new GUIContent("Head height, stowed (m)"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("linkPivotOffset"),new GUIContent("Link attach on arm"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("linkHeadOffset"),new GUIContent("Link attach on head"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("linkThickness"),new GUIContent("Link thickness (m)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rollerOffset"),new GUIContent("Contact roller offset"));
            bool changed=EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            HeadProxies(rig);
            if(changed)Repose(rig);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PREVIEW / Edit Mode only, never saved",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUI.BeginChangeCheck();
                float d=EditorGUILayout.Slider("Deployment %",PreviewDeployment(rig)*100,0,100)/100;
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("Stowed"))d=0;
                    if(GUILayout.Button("25%"))d=.25f;
                    if(GUILayout.Button("50%"))d=.5f;
                    if(GUILayout.Button("75%"))d=.75f;
                    if(GUILayout.Button("Working"))d=1;
                }
                bool sim=previewSimulation;
                if(rig.useAuthoredEndpoints)EditorGUILayout.LabelField("Preview geometry","Saved endpoints");
                else sim=EditorGUILayout.Popup("Preview geometry",previewSimulation?1:0,new[]{"Draft (this rig)","Simulation (scene calibration)"})==1;
                if(EditorGUI.EndChangeCheck()) { previewSimulation=sim; SetPreview(rig,d); }
                if(GUILayout.Button("Reset Preview (stowed)")) { previewSimulation=false; SetPreview(rig,0); }
            }
            ShowGizmos=EditorGUILayout.Toggle("Scene gizmos",ShowGizmos);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("SIMULATION AGREEMENT",EditorStyles.boldLabel);
            var simulation=IceBoardBrushRig.Mechanism.From(config);
            if(rig.useAuthoredEndpoints)EditorGUILayout.HelpBox("Play derives the brush geometry from the saved endpoints. The scene calibration is preserved; snow follows the same simulated disc shown by this rig.",MessageType.Info);
            else if(rig.mechanism.Matches(simulation))EditorGUILayout.HelpBox("Draft matches the simulation. Play sweeps snow exactly under this articulation.",MessageType.Info);
            else EditorGUILayout.HelpBox("Not applied: "+string.Join(", ",Differences(rig.mechanism,simulation))+
                ". Play poses the brush, and sweeps snow, from the scene calibration (magenta in the Scene view), not this draft.",MessageType.Warning);
            foreach(var line in Diagnose(rig,config,Shown(rig)))EditorGUILayout.HelpBox(line.Value,line.Key);
            float visual=VisualRadius(rig);
            using(new EditorGUI.DisabledScope(Application.isPlaying))
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUI.DisabledScope(Mathf.Abs(visual-rig.mechanism.Radius)<.0005f))
                {
                    if(GUILayout.Button("Contact radius = bristles ("+visual.ToString("F3")+" m)"))
                    { Undo.RecordObject(rig,"Brush contact radius to bristles");rig.mechanism.Radius=Mathf.Round(visual*1000)/1000;Repose(rig); }
                    if(GUILayout.Button("Scale bristles to contact radius"))
                    {
                        Undo.RecordObject(rig.head,"Scale brush head to contact radius");
                        rig.head.localScale*=rig.mechanism.Radius/Mathf.Max(.001f,visual);
                        if(PrefabUtility.IsPartOfPrefabInstance(rig.head))PrefabUtility.RecordPrefabInstancePropertyModifications(rig.head);
                    }
                }
            }
            using(new EditorGUI.DisabledScope(game==null||Application.isPlaying||rig.useAuthoredEndpoints||rig.mechanism.Matches(simulation)))
            {
                if(GUILayout.Button("Apply Brush Geometry to Simulation..."))
                {
                    string summary="Copy the brush mechanism into the scene calibration?\n\n"+string.Join("\n",Differences(rig.mechanism,simulation))+
                        "\n\nThis changes where the simulated brush sweeps snow. Undo is available (Edit > Undo), and the previous geometry is kept for "+
                        "\"Restore Previous Simulation Geometry\". Brush fields are not GPS compatibility fields: routes stay usable.";
                    if(EditorUtility.DisplayDialog("Apply Brush Geometry to Simulation",summary,"Apply","Cancel")&&!ApplyToSimulation(rig,game,out string error))
                        EditorUtility.DisplayDialog("Brush geometry not applied",error,"OK");
                }
                if(GUILayout.Button("Revert Draft to Simulation"))
                {
                    Undo.RecordObject(rig,"Revert board brush draft");
                    rig.mechanism=simulation;
                    Repose(rig);
                }
            }
            var previous=default(IceBoardBrushRig.Mechanism);
            bool history=game!=null&&Previous(game,out previous)&&!previous.Matches(simulation);
            using(new EditorGUI.DisabledScope(!history||Application.isPlaying))
                if(GUILayout.Button("Restore Previous Simulation Geometry..."+(history?" ("+string.Join(", ",Differences(previous,simulation))+")":"")))
                {
                    Previous(game,out previous);
                    if(EditorUtility.DisplayDialog("Restore previous brush geometry","Write the brush geometry that was in the scene calibration before the last Apply back into it, and into this draft?\n\n"+
                        string.Join("\n",Differences(previous,simulation)),"Restore","Cancel"))
                    {
                        Undo.RecordObject(rig,"Restore previous brush geometry");rig.mechanism=previous;
                        if(!ApplyToSimulation(rig,game,out string error))EditorUtility.DisplayDialog("Brush geometry not restored",error,"OK");
                    }
                }
            if(game==null)EditorGUILayout.HelpBox("Read-only here: open the authored scene and select the brush arm to apply geometry to its calibration.",MessageType.None);
        }

        static void Persist(IceBoardBrushRig rig)
        {
            EditorUtility.SetDirty(rig);
            if(PrefabUtility.IsPartOfPrefabInstance(rig))PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
        }

        static void EndpointInspector(IceBoardBrushRig rig)
        {
            EditorGUILayout.LabelField("AUTHOR STOWED / WORKING ENDPOINTS",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Edit an endpoint, move/rotate its brush head with the Scene handles, then Save that endpoint. Save both and enable Use saved endpoints in Play. The current rig supplies the starting poses. The arm keeps one pivot and one length.",MessageType.Info);
            using(new EditorGUI.DisabledScope(Application.isPlaying))
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("Edit STOWED"))BeginEndpoint(rig,0);
                    if(GUILayout.Button("Edit WORKING"))BeginEndpoint(rig,1);
                }
                if(!rig.endpointsInitialized)return;
                bool valid=rig.TryEndpointMechanism(out _,out string error);
                if(!valid)EditorGUILayout.HelpBox(error,MessageType.Warning);
                if(rig.useAuthoredEndpoints&&(!valid||!rig.stowedEndpointSaved||!rig.workingEndpointSaved))
                    EditorGUILayout.HelpBox("Endpoint edits are still a draft. Play retains the scene geometry until both valid endpoints are saved.",MessageType.Warning);
                if(endpointEditing.TryGetValue(rig.GetInstanceID(),out int endpoint))
                {
                    var pose=endpoint==0?rig.stowedEndpoint:rig.workingEndpoint;
                    EditorGUILayout.LabelField(endpoint==0?"Editing STOWED":"Editing WORKING",EditorStyles.boldLabel);
                    EditorGUI.BeginChangeCheck();
                    pose.position=EditorGUILayout.Vector3Field("Head position (machine m)",pose.position);
                    pose.rotation=Quaternion.Euler(EditorGUILayout.Vector3Field("Head rotation (machine deg)",pose.rotation.eulerAngles));
                    if(EditorGUI.EndChangeCheck())StoreEndpoint(rig,endpoint,pose);
                    EditorGUI.BeginChangeCheck();
                    float length=EditorGUILayout.FloatField("Arm length (m)",rig.mechanism.ArmLength);
                    Vector3 pivot=EditorGUILayout.Vector3Field("Pivot (machine m)",new Vector3(rig.mechanism.PivotX,rig.pivotHeight,rig.mechanism.PivotZ));
                    if(EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(rig,"Adjust endpoint arm geometry");
                        rig.mechanism.ArmLength=Mathf.Max(.05f,length);rig.mechanism.PivotX=pivot.x;rig.mechanism.PivotZ=pivot.z;rig.pivotHeight=pivot.y;
                        rig.stowedEndpointSaved=rig.workingEndpointSaved=false;Persist(rig);Repose(rig);
                    }
                    if(GUILayout.Button("Project both endpoints onto arm circle"))
                    {
                        Undo.RecordObject(rig,"Project board brush endpoints");
                        rig.stowedEndpoint=ProjectEndpoint(rig,rig.stowedEndpoint);rig.workingEndpoint=ProjectEndpoint(rig,rig.workingEndpoint);
                        rig.stowedEndpointSaved=rig.workingEndpointSaved=false;Persist(rig);Repose(rig);
                    }
                    bool endpointFits=BoardBrushArticulation.Endpoint(rig.mechanism.PivotX,rig.mechanism.PivotZ,rig.mechanism.ArmLength,pose.position.x,pose.position.z,out float angle,out _)
                        &&(endpoint==0?angle>=0&&angle<=100:angle>=-45&&angle<=100);
                    using(new EditorGUI.DisabledScope(!endpointFits))
                        if(GUILayout.Button(endpoint==0?"Save STOWED endpoint":"Save WORKING endpoint"))
                        {
                            Undo.RecordObject(rig,"Save board brush endpoint");
                            // Remove sub-millimetre handle drift so the authored centre is exactly physical.
                            if(endpoint==0){rig.stowedEndpoint=ProjectEndpoint(rig,rig.stowedEndpoint);rig.stowedEndpointSaved=true;}
                            else {rig.workingEndpoint=ProjectEndpoint(rig,rig.workingEndpoint);rig.workingEndpointSaved=true;}
                            endpointEditing.Remove(rig.GetInstanceID());Persist(rig);Repose(rig);
                        }
                    if(GUILayout.Button("Close endpoint editing (keep draft)")){endpointEditing.Remove(rig.GetInstanceID());SetPreview(rig,0);}
                }
                EditorGUILayout.LabelField("Saved", "STOWED: "+(rig.stowedEndpointSaved?"yes":"draft")+"  /  WORKING: "+(rig.workingEndpointSaved?"yes":"draft"));
                using(new EditorGUI.DisabledScope(!rig.useAuthoredEndpoints&&(!valid||!rig.stowedEndpointSaved||!rig.workingEndpointSaved)))
                {
                    EditorGUI.BeginChangeCheck();
                    bool active=EditorGUILayout.Toggle("Use saved endpoints in Play",rig.useAuthoredEndpoints);
                    if(EditorGUI.EndChangeCheck()){Undo.RecordObject(rig,"Enable authored brush endpoints");rig.useAuthoredEndpoints=active;Persist(rig);Repose(rig);}
                }
            }
        }

        static void BeginEndpoint(IceBoardBrushRig rig,int endpoint)
        {
            Undo.RecordObject(rig,"Begin board brush endpoint authoring");rig.InitializeEndpoints();
            endpointEditing[rig.GetInstanceID()]=endpoint;previewSimulation=false;Persist(rig);SetPreview(rig,endpoint);
        }

        static IceBoardBrushRig.Endpoint ProjectEndpoint(IceBoardBrushRig rig,IceBoardBrushRig.Endpoint pose)
        {
            Vector2 delta=new Vector2(pose.position.x-rig.mechanism.PivotX,pose.position.z-rig.mechanism.PivotZ);
            if(delta.sqrMagnitude<.000001f)delta=Vector2.left;
            delta=delta.normalized*rig.mechanism.ArmLength;
            pose.position.x=rig.mechanism.PivotX+delta.x;pose.position.z=rig.mechanism.PivotZ+delta.y;
            return pose;
        }

        static void StoreEndpoint(IceBoardBrushRig rig,int endpoint,IceBoardBrushRig.Endpoint pose)
        {
            Undo.RecordObject(rig,"Move board brush endpoint");
            if(endpoint==0){rig.stowedEndpoint=pose;rig.stowedEndpointSaved=false;}
            else {rig.workingEndpoint=pose;rig.workingEndpointSaved=false;}
            Persist(rig);Repose(rig);
        }

        static string HistoryKey(IceResurfacerGame game){ return "IceBoardBrushRig.Previous."+game.gameObject.scene.path; }
        static bool Previous(IceResurfacerGame game,out IceBoardBrushRig.Mechanism mechanism)
        {
            mechanism=default;
            string json=EditorPrefs.GetString(HistoryKey(game),"");
            if(json=="")return false;
            mechanism=JsonUtility.FromJson<IceBoardBrushRig.Mechanism>(json);
            return true;
        }

        public static void SetPreview(IceBoardBrushRig rig,float d)
        {
            if(d<=0)preview.Remove(rig.GetInstanceID());else preview[rig.GetInstanceID()]=Mathf.Clamp01(d);
            Repose(rig);
            SceneView.RepaintAll();
        }

        /// <summary>Head tilt/scale and motor/guard offsets edit those transforms directly (with Undo).</summary>
        static void HeadProxies(IceBoardBrushRig rig)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 tilt=rig.head.localEulerAngles;
            if(!rig.useAuthoredEndpoints)tilt=EditorGUILayout.Vector3Field("Head tilt (deg)",tilt);
            float scale=EditorGUILayout.FloatField("Head scale",rig.head.localScale.x);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(rig.head,"Board brush head");
                rig.head.localEulerAngles=tilt; rig.head.localScale=Vector3.one*Mathf.Max(.05f,scale);
                if(PrefabUtility.IsPartOfPrefabInstance(rig.head))PrefabUtility.RecordPrefabInstancePropertyModifications(rig.head);
            }
            foreach(var part in rig.nonSpinning)
            {
                if(part==null)continue;
                EditorGUI.BeginChangeCheck();
                Vector3 offset=EditorGUILayout.Vector3Field(part.name+" offset",part.localPosition);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(part,"Board brush "+part.name);
                    part.localPosition=offset;
                    if(PrefabUtility.IsPartOfPrefabInstance(part))PrefabUtility.RecordPrefabInstancePropertyModifications(part);
                }
            }
            EditorGUILayout.LabelField("Visual bristle radius",VisualRadius(rig).ToString("F3")+" m (contact "+rig.mechanism.Radius.ToString("F3")+" m)");
        }

        // Dragging the arm with the Move tool moves the pivot rather than fighting the rig.
        static void AdoptManualMove(IceBoardBrushRig rig)
        {
            if(Application.isPlaying||!rig.IsComplete||endpointEditing.ContainsKey(rig.GetInstanceID()))return;
            var shown=Shown(rig);
            Vector3 actual=rig.machine.InverseTransformPoint(rig.transform.position);
            if(Vector3.Distance(actual,new Vector3(shown.PivotX,rig.pivotHeight,shown.PivotZ))<.0002f)return;
            Undo.RecordObject(rig,"Move board brush pivot");
            rig.mechanism.PivotX=actual.x; rig.mechanism.PivotZ=actual.z; rig.pivotHeight=actual.y;
            previewSimulation=false;
            Repose(rig);
        }

        public static List<string> Differences(IceBoardBrushRig.Mechanism draft,IceBoardBrushRig.Mechanism sim)
        {
            var list=new List<string>();
            void Add(string label,float a,float b,string unit,float tolerance){ if(Mathf.Abs(a-b)>tolerance)list.Add(label+" "+b.ToString("0.###")+" -> "+a.ToString("0.###")+unit); }
            Add("pivot X",draft.PivotX,sim.PivotX," m",.0005f); Add("pivot Z",draft.PivotZ,sim.PivotZ," m",.0005f);
            Add("arm length",draft.ArmLength,sim.ArmLength," m",.0005f); Add("radius",draft.Radius,sim.Radius," m",.0005f);
            Add("working angle",draft.DeployedDegrees,sim.DeployedDegrees," deg",.01f); Add("stowed angle",draft.RetractedDegrees,sim.RetractedDegrees," deg",.01f);
            return list;
        }

        /// <summary>Validates on a copy, then writes the six brush fields with Undo.</summary>
        public static bool ApplyToSimulation(IceBoardBrushRig rig,IceResurfacerGame game,out string error)
        {
            error=null;
            var copy=JsonUtility.FromJson<SimulatorConfig>(JsonUtility.ToJson(game.calibration));
            Write(copy,rig.mechanism);
            try { copy.Validate(); }
            catch(Exception e) { error="The scene calibration would be invalid: "+e.Message; return false; }
            var before=IceBoardBrushRig.Mechanism.From(game.calibration);
            Undo.RecordObject(game,"Apply brush geometry to simulation");
            Write(game.calibration,rig.mechanism);
            if(!before.Matches(rig.mechanism))EditorPrefs.SetString(HistoryKey(game),JsonUtility.ToJson(before));
            Debug.Log("Board brush geometry applied to "+game.gameObject.scene.name+": "+string.Join(", ",Differences(rig.mechanism,before))+". Previous: "+JsonUtility.ToJson(before),game);
            EditorUtility.SetDirty(game);
            if(PrefabUtility.IsPartOfPrefabInstance(game))PrefabUtility.RecordPrefabInstancePropertyModifications(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            Repose(rig);
            return true;
        }

        static void Write(SimulatorConfig c,IceBoardBrushRig.Mechanism m)
        {
            c.BrushPivotX=m.PivotX; c.BrushPivotZ=m.PivotZ; c.BrushArmLength=m.ArmLength; c.BrushRadius=m.Radius;
            c.BrushDeployedDegrees=m.DeployedDegrees; c.BrushRetractedDegrees=m.RetractedDegrees;
        }

        /// <summary>Reach and fit checks for a mechanism (machine space). Used by the Inspector and validation.</summary>
        public static List<KeyValuePair<MessageType,string>> Diagnose(IceBoardBrushRig rig,SimulatorConfig config,IceBoardBrushRig.Mechanism m)
        {
            var list=new List<KeyValuePair<MessageType,string>>();
            // How close the machine can get to boards on the brush side: its outermost collision point there.
            float side=CollisionSide(config,m.PivotX<0?-1:1,out string sidePart);
            Vector3 working=IceBoardBrushRig.ContactLocal(m,1),stowed=IceBoardBrushRig.ContactLocal(m,0);
            float reach=Mathf.Abs(working.x)+m.Radius-side;
            if(reach<=0)list.Add(new KeyValuePair<MessageType,string>(MessageType.Error,"Working brush edge is "+(-reach*1000).ToString("F0")+
                " mm inside the "+sidePart+" (|x| "+side.ToString("F3")+"): driving along the boards, the machine touches them before the brush. Snow at the kickplate cannot be swept."));
            else list.Add(new KeyValuePair<MessageType,string>(reach<.03f?MessageType.Warning:MessageType.Info,"Working brush edge reaches "+(reach*1000).ToString("F0")+
                " mm past the "+sidePart+" (|x| "+side.ToString("F3")+"), so with the machine along the boards the bristles press into the kickplate"+(reach<.03f?" only lightly.":".")));
            float travel=rig!=null?rig.complianceTravel:config.BrushComplianceTravel;
            if(reach>travel)list.Add(new KeyValuePair<MessageType,string>(MessageType.Warning,"At the closest parallel-board position the brush needs "+(reach*1000).ToString("F0")+
                " mm of compliance, exceeding its "+(travel*1000).ToString("F0")+" mm travel. Leave more board clearance, reduce working reach, or explicitly increase compliance travel. The simulation will not invent extra travel."));
            float stowOut=-(stowed.x-m.Radius)-side;
            if(stowOut>0)list.Add(new KeyValuePair<MessageType,string>(MessageType.Info,"Stowed brush still sticks out "+stowOut.ToString("F2")+" m beyond the body side."));
            if(working.z<-config.BodyRear||working.z>config.BodyFront)list.Add(new KeyValuePair<MessageType,string>(MessageType.Warning,"Working brush centre is outside the body's length."));
            if(rig!=null)
            {
                float visual=VisualRadius(rig);
                if(Mathf.Abs(visual-m.Radius)>.02f)list.Add(new KeyValuePair<MessageType,string>(MessageType.Warning,"Visible bristles ("+visual.ToString("F3")+
                    " m) differ from the contact radius ("+m.Radius.ToString("F3")+" m): snow is cleared where no bristles show, or vice versa. "+
                    "Match Head scale to the radius, or set the radius to the visible size."));
            }
            return list;
        }

        /// <summary>Outermost collision point on one side (-1 left, +1 right): outline, conditioner collider, guide roller.</summary>
        public static float CollisionSide(SimulatorConfig c,int sign,out string part)
        {
            float side=0;part="body side";
            if(c.HasCollisionOutline){for(int i=0;i<c.CollisionOutline.Length;i+=2)if(Mathf.Sign(c.CollisionOutline[i])==sign)side=Mathf.Max(side,Mathf.Abs(c.CollisionOutline[i]));part="drawn body";}
            else side=c.VehicleHalfWidth;
            if(c.ConditionerCollisionHalfWidth>side){side=c.ConditionerCollisionHalfWidth;part="conditioner side plate";}
            if(Mathf.Sign(c.GuideRollerX)==sign&&Mathf.Abs(c.GuideRollerX)+c.GuideRollerRadius>side){side=Mathf.Abs(c.GuideRollerX)+c.GuideRollerRadius;part="guide roller";}
            return side;
        }

        /// <summary>Horizontal extent of the spinning head (excluding motor and guard), machine units.</summary>
        public static float VisualRadius(IceBoardBrushRig rig)
        {
            float radius=0;
            var still=new HashSet<Transform>(rig.nonSpinning);
            foreach(var renderer in rig.head.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(still.Contains(renderer.transform)||Array.Exists(rig.nonSpinning,t=>t!=null&&renderer.transform.IsChildOf(t)))continue;
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter==null||filter.sharedMesh==null)continue;
                var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 p=rig.head.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    radius=Mathf.Max(radius,new Vector2(p.x,p.z).magnitude*rig.head.lossyScale.x);
                }
            }
            return radius;
        }

        // Scene handles. SWEEP (round dots, green / grey / cyan / yellow) change where snow is swept and
        // stay a draft until Apply. COSMETIC (square, orange) only move drawn parts: heights, link ends,
        // the kickplate roller carriage. Every drag is undoable and re-poses the rig immediately.
        static readonly Color Cosmetic=new Color(1f,.55f,.1f);

        void OnSceneGUI()
        {
            var rig=(IceBoardBrushRig)target;
            if(!rig.IsComplete||Application.isPlaying)return;
            if(endpointEditing.TryGetValue(rig.GetInstanceID(),out int endpoint))
            {
                var pose=endpoint==0?rig.stowedEndpoint:rig.workingEndpoint;
                Vector3 position=rig.machine.TransformPoint(pose.position);
                Quaternion rotation=rig.machine.rotation*pose.rotation;
                Handles.Label(position+rig.machine.up*.3f,(endpoint==0?"STOWED":"WORKING")+" endpoint: move / rotate, then Save in Inspector");
                Handles.color=endpoint==0?Color.gray:Color.green;
                Handles.DrawWireDisc(position,rig.machine.up,rig.mechanism.Radius);
                EditorGUI.BeginChangeCheck();
                Vector3 moved=Handles.PositionHandle(position,rig.machine.rotation);
                Quaternion turned=Handles.RotationHandle(rotation,position);
                if(EditorGUI.EndChangeCheck())
                {
                    pose.position=rig.machine.InverseTransformPoint(moved);pose.rotation=Quaternion.Inverse(rig.machine.rotation)*turned;
                    StoreEndpoint(rig,endpoint,pose);
                }
                return;
            }
            AdoptManualMove(rig);
            var m=Shown(rig);
            Transform machine=rig.machine;
            Vector3 P(Vector3 local)=>machine.TransformPoint(local);
            Vector3 pivot=P(new Vector3(m.PivotX,rig.pivotHeight,m.PivotZ));
            Vector3 up=machine.up;
            var config=Simulation(rig,out _);
            Vector3 work=IceBoardBrushRig.ContactLocal(m,1),stow=IceBoardBrushRig.ContactLocal(m,0);
            if(ShowGizmos)
            {
                // Travel arc at pivot height, working (green), stowed (grey) and contact disc outlines on the ice.
                Vector3 now=IceBoardBrushRig.ContactLocal(m,PreviewDeployment(rig));
                Handles.color=new Color(1,.8f,.2f,.8f);
                Vector3 from=P(new Vector3(work.x,rig.pivotHeight,work.z))-pivot;
                Handles.DrawWireArc(pivot,-up,from.normalized,m.RetractedDegrees-m.DeployedDegrees,m.ArmLength);
                Handles.color=Color.green; Handles.DrawLine(pivot,P(new Vector3(work.x,rig.pivotHeight,work.z)),2); Handles.DrawWireDisc(P(work),up,m.Radius);
                Handles.color=Color.gray; Handles.DrawLine(pivot,P(new Vector3(stow.x,rig.pivotHeight,stow.z)),2); Handles.DrawWireDisc(P(stow),up,m.Radius);
                Handles.color=Color.yellow; Handles.DrawWireDisc(P(now),up,m.Radius*.98f);
                // Kickplate contact: the disc rim straight out along the arm.
                Vector3 outward=(work-new Vector3(m.PivotX,0,m.PivotZ)).normalized;
                Handles.color=Color.red; Handles.SphereHandleCap(0,P(work+outward*m.Radius),Quaternion.identity,.04f,EventType.Repaint);
                Handles.Label(P(work+outward*m.Radius)+up*.08f,"kickplate contact");
                // Closest approach to the boards on this side: the outermost collision point (outline, conditioner, roller).
                Handles.color=new Color(.3f,.7f,1,.6f);
                float side=CollisionSide(config,m.PivotX<0?-1:1,out string sidePart)*(m.PivotX<0?-1:1);
                Handles.DrawDottedLine(P(new Vector3(side,0,-1.6f)),P(new Vector3(side,0,config.BodyFront)),4);
                Handles.Label(P(new Vector3(side,.05f,config.BodyFront)),"boards can come no closer ("+sidePart+")");
                // Simulation ghost when the draft is not applied.
                var sim=IceBoardBrushRig.Mechanism.From(config);
                if(!rig.useAuthoredEndpoints&&!m.Matches(sim))
                {
                    Handles.color=new Color(1,.3f,1,.7f);
                    Vector3 simPivot=P(new Vector3(sim.PivotX,rig.pivotHeight,sim.PivotZ));
                    Handles.DrawDottedLine(simPivot,P(IceBoardBrushRig.ContactLocal(sim,1)+Vector3.up*rig.pivotHeight),3);
                    Handles.DrawWireDisc(P(IceBoardBrushRig.ContactLocal(sim,1)),up,sim.Radius);
                    Handles.Label(simPivot+up*.12f,"simulation (what Play sweeps until you Apply)");
                }
            }
            // Active endpoints own angles/heights. Edit them through the explicit endpoint
            // handles instead of allowing legacy handles to write unused draft angles.
            if(rig.useAuthoredEndpoints)return;
            // SWEEP: pivot, working and stowed ends, reach along the working arm, contact radius.
            PivotHandle(rig);
            EndHandle(rig,1,"Board brush working position");
            EndHandle(rig,0,"Board brush stowed position");
            ReachHandle(rig);
            RadiusHandle(rig);
            // COSMETIC: heights, roller carriage, link ends.
            CosmeticHandles(rig);
        }

        static void Label(Vector3 at,Vector3 up,string text,Color color)
        {
            var style=new GUIStyle(EditorStyles.miniBoldLabel){normal={textColor=color}};
            Handles.Label(at+up*.06f,text,style);
        }

        static void PivotHandle(IceBoardBrushRig rig)
        {
            var m=rig.mechanism;Transform machine=rig.machine;
            Vector3 world=machine.TransformPoint(m.PivotX,rig.pivotHeight,m.PivotZ);
            Handles.color=Color.white;
            EditorGUI.BeginChangeCheck();
            Vector3 moved=Handles.Slider2D(world,machine.up,machine.right,machine.forward,HandleUtility.GetHandleSize(world)*.09f,Handles.SphereHandleCap,Vector2.zero);
            Label(world,machine.up,"SWEEP pivot",Color.white);
            if(!EditorGUI.EndChangeCheck())return;
            Vector3 p=machine.InverseTransformPoint(moved);
            Undo.RecordObject(rig,"Move board brush pivot");
            rig.mechanism.PivotX=p.x;rig.mechanism.PivotZ=p.z;previewSimulation=false;
            Repose(rig);
        }

        static void ReachHandle(IceBoardBrushRig rig)
        {
            var m=rig.mechanism;Transform machine=rig.machine;
            Vector3 pivot=new Vector3(m.PivotX,0,m.PivotZ),work=IceBoardBrushRig.ContactLocal(m,1);
            Vector3 along=(work-pivot).normalized;
            Vector3 world=machine.TransformPoint(new Vector3(work.x,rig.pivotHeight,work.z)-along*m.ArmLength*.35f);
            Handles.color=Color.cyan;
            EditorGUI.BeginChangeCheck();
            Vector3 moved=Handles.Slider(world,machine.TransformDirection(along),HandleUtility.GetHandleSize(world)*.07f,Handles.DotHandleCap,0);
            Label(world,machine.up,"SWEEP reach (arm length)",Color.cyan);
            if(!EditorGUI.EndChangeCheck())return;
            float delta=Vector3.Dot(machine.InverseTransformPoint(moved)-machine.InverseTransformPoint(world),along);
            Undo.RecordObject(rig,"Board brush reach");
            rig.mechanism.ArmLength=Mathf.Max(.05f,m.ArmLength+delta);
            Repose(rig);
        }

        static void RadiusHandle(IceBoardBrushRig rig)
        {
            var m=rig.mechanism;Transform machine=rig.machine;
            Vector3 work=IceBoardBrushRig.ContactLocal(m,1),pivot=new Vector3(m.PivotX,0,m.PivotZ);
            Vector3 sideways=Vector3.Cross(Vector3.up,(work-pivot).normalized);
            Vector3 world=machine.TransformPoint(work+sideways*m.Radius);
            Handles.color=Color.yellow;
            EditorGUI.BeginChangeCheck();
            Vector3 moved=Handles.Slider(world,machine.TransformDirection(sideways),HandleUtility.GetHandleSize(world)*.06f,Handles.DotHandleCap,0);
            Label(world,machine.up,"SWEEP contact radius",Color.yellow);
            if(!EditorGUI.EndChangeCheck())return;
            Undo.RecordObject(rig,"Board brush contact radius");
            rig.mechanism.Radius=Mathf.Max(.05f,Vector3.Dot(machine.InverseTransformPoint(moved)-work,sideways));
            Repose(rig);
        }

        static void CosmeticHandles(IceBoardBrushRig rig)
        {
            var m=rig.mechanism;Transform machine=rig.machine;Vector3 up=machine.up;
            float size(Vector3 at)=>HandleUtility.GetHandleSize(at)*.05f;
            Handles.color=Cosmetic;
            // Pivot height.
            Vector3 pivot=machine.TransformPoint(m.PivotX,rig.pivotHeight,m.PivotZ)+up*.12f;
            EditorGUI.BeginChangeCheck();
            Vector3 p=Handles.Slider(pivot,up,size(pivot),Handles.CubeHandleCap,0);
            Label(pivot,up,"COSMETIC pivot height",Cosmetic);
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(rig,"Board brush pivot height");rig.pivotHeight+=Vector3.Dot(p-pivot,up);Repose(rig);}
            // Head heights at the working and stowed ends.
            foreach(float deployment in new[]{1f,0f})
            {
                Vector3 c=IceBoardBrushRig.ContactLocal(m,deployment);
                Vector3 at=machine.TransformPoint(c.x,rig.HeadHeight(deployment),c.z);
                EditorGUI.BeginChangeCheck();
                Vector3 q=Handles.Slider(at,up,size(at),Handles.CubeHandleCap,0);
                Label(at,up,"COSMETIC head height ("+(deployment>0?"working":"stowed")+")",Cosmetic);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(rig,"Board brush head height");
                    float h=machine.InverseTransformPoint(q).y;
                    if(deployment>0)rig.deployedHeadHeight=h;else rig.retractedHeadHeight=h;
                    Repose(rig);
                }
            }
            // Kickplate roller carriage and the link ends: free moves, machine-oriented offsets from the head / arm.
            Vector3 roller=rig.roller.position;
            EditorGUI.BeginChangeCheck();
            Vector3 r=Handles.FreeMoveHandle(roller,size(roller),Vector3.zero,Handles.RectangleHandleCap);
            Label(roller,up,"COSMETIC roller carriage",Cosmetic);
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(rig,"Board brush roller offset");rig.rollerOffset+=Quaternion.Inverse(machine.rotation)*(r-roller);Repose(rig);}
            Vector3 linkHead=rig.head.position+machine.rotation*rig.linkHeadOffset;
            EditorGUI.BeginChangeCheck();
            Vector3 lh=Handles.FreeMoveHandle(linkHead,size(linkHead),Vector3.zero,Handles.RectangleHandleCap);
            Label(linkHead,up,"COSMETIC link on head",Cosmetic);
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(rig,"Board brush link end");rig.linkHeadOffset+=Quaternion.Inverse(machine.rotation)*(lh-linkHead);Repose(rig);}
            Vector3 linkArm=rig.transform.TransformPoint(rig.linkPivotOffset);
            EditorGUI.BeginChangeCheck();
            Vector3 la=Handles.FreeMoveHandle(linkArm,size(linkArm),Vector3.zero,Handles.RectangleHandleCap);
            Label(linkArm,-up,"COSMETIC link on arm",Cosmetic);
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(rig,"Board brush link end");rig.linkPivotOffset=rig.transform.InverseTransformPoint(la);Repose(rig);}
        }

        static void EndHandle(IceBoardBrushRig rig,float deployment,string label)
        {
            var m=rig.mechanism;
            Transform machine=rig.machine;
            Vector3 local=IceBoardBrushRig.ContactLocal(m,deployment);
            Vector3 world=machine.TransformPoint(new Vector3(local.x,rig.pivotHeight,local.z));
            Handles.color=deployment>0?Color.green:Color.gray;
            EditorGUI.BeginChangeCheck();
            Vector3 moved=Handles.Slider2D(world,machine.up,machine.right,machine.forward,HandleUtility.GetHandleSize(world)*.08f,Handles.DotHandleCap,Vector2.zero);
            Label(world,machine.up,deployment>0?"SWEEP working end":"SWEEP stowed end",deployment>0?Color.green:Color.gray);
            if(!EditorGUI.EndChangeCheck())return;
            Vector3 p=machine.InverseTransformPoint(moved);
            float dx=m.PivotX-p.x,dz=m.PivotZ-p.z;
            Undo.RecordObject(rig,label);
            rig.mechanism.ArmLength=Mathf.Max(.05f,new Vector2(dx,dz).magnitude);
            float degrees=Mathf.Atan2(dz,dx)*Mathf.Rad2Deg;
            if(deployment>0)rig.mechanism.DeployedDegrees=Mathf.Clamp(degrees,-45,rig.mechanism.RetractedDegrees);
            else rig.mechanism.RetractedDegrees=Mathf.Clamp(degrees,rig.mechanism.DeployedDegrees,100);
            Repose(rig);
        }

        // ---- Setup --------------------------------------------------------------------------

        /// <summary>Adds the rig to the bound brush arm and imports the current authored pose,
        /// so nothing visibly moves. Returns a summary of what was imported.</summary>
        public static IceBoardBrushRig AddRig(IceWorld w,SimulatorConfig config,out string summary)
        {
            var arm=w.BrushArm;
            var rig=arm.GetComponent<IceBoardBrushRig>();
            if(rig!=null){summary="The brush arm already has a rig.";return rig;}
            rig=Undo.AddComponent<IceBoardBrushRig>(arm.gameObject);
            rig.machine=w.Machine; rig.head=w.Brush; rig.link=w.BrushLink; rig.roller=w.BrushRoller;
            var still=new List<Transform>();
            foreach(Transform child in w.Brush)
                if(child.name==MotorName||child.name==GuardName)
                {
                    still.Add(child);
                    if(child.GetComponent<IceKeepUnbatched>()==null)Undo.AddComponent<IceKeepUnbatched>(child.gameObject);
                }
            rig.nonSpinning=still.ToArray();
            Vector3 pivot=w.Machine.InverseTransformPoint(arm.position);
            Vector3 head=w.Machine.InverseTransformPoint(w.Brush.position);
            float yaw=Mathf.DeltaAngle(0,(Quaternion.Inverse(w.Machine.rotation)*arm.rotation).eulerAngles.y);
            float degrees=-yaw;
            var m=IceBoardBrushRig.Mechanism.From(config);
            m.PivotX=pivot.x; m.PivotZ=pivot.z;
            m.ArmLength=new Vector2(head.x-pivot.x,head.z-pivot.z).magnitude;
            rig.pivotHeight=pivot.y;
            bool stowed=Mathf.Abs(degrees-config.BrushRetractedDegrees)<=Mathf.Abs(degrees-config.BrushDeployedDegrees);
            if(stowed){ m.RetractedDegrees=Mathf.Clamp(degrees,m.DeployedDegrees,100); rig.retractedHeadHeight=head.y; }
            else { m.DeployedDegrees=Mathf.Clamp(degrees,-45,m.RetractedDegrees); rig.deployedHeadHeight=head.y; }
            rig.mechanism=m;
            summary="Imported from the current arm: pivot ("+pivot.x.ToString("F3")+", "+pivot.z.ToString("F3")+") at height "+pivot.y.ToString("F3")+
                " m, arm length "+m.ArmLength.ToString("F3")+" m, "+(stowed?"stowed":"working")+" angle "+degrees.ToString("F1")+" deg, head height "+
                head.y.ToString("F3")+" m. Other values from the scene calibration. Draft vs simulation: "+
                (m.Matches(IceBoardBrushRig.Mechanism.From(config))?"matches.":string.Join(", ",Differences(m,IceBoardBrushRig.Mechanism.From(config)))+" (not applied).");
            Repose(rig);
            return rig;
        }
    }
}
