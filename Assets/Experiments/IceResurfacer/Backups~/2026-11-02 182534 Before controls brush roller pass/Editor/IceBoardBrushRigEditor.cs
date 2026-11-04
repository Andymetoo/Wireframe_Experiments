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
        }

        static void Stow(GameObject root)
        {
            if(Application.isPlaying)return;
            foreach(var rig in root.GetComponentsInChildren<IceBoardBrushRig>(true)) { preview.Remove(rig.GetInstanceID()); Repose(rig); }
        }

        public static float PreviewDeployment(IceBoardBrushRig rig)
        {
            return preview.TryGetValue(rig.GetInstanceID(),out float d)?d:0;
        }

        static SimulatorConfig Simulation(IceBoardBrushRig rig,out IceResurfacerGame game)
        {
            game=rig.GetComponentInParent<IceResurfacerGame>();
            if(game!=null)return game.calibration;
            var machineRig=rig.GetComponentInParent<IceMachineRig>();
            return machineRig!=null&&!string.IsNullOrEmpty(machineRig.BakedConfigJson)?JsonUtility.FromJson<SimulatorConfig>(machineRig.BakedConfigJson):new SimulatorConfig();
        }

        static IceBoardBrushRig.Mechanism Shown(IceBoardBrushRig rig)
        {
            return previewSimulation?IceBoardBrushRig.Mechanism.From(Simulation(rig,out _)):rig.mechanism;
        }

        /// <summary>Poses the brush at its preview deployment (stowed when not previewing).
        /// The stowed pose is recorded on prefab instances so it is what gets saved.</summary>
        public static void Repose(IceBoardBrushRig rig)
        {
            if(rig==null||!rig.IsComplete||Application.isPlaying)return;
            float d=PreviewDeployment(rig);
            rig.PosePreview(Shown(rig),d);
            if(d==0&&PrefabUtility.IsPartOfPrefabInstance(rig))
                foreach(var t in new[]{rig.transform,rig.head,rig.link,rig.roller})PrefabUtility.RecordPrefabInstancePropertyModifications(t);
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
            if(rig!=null&&!Application.isPlaying&&PreviewDeployment(rig)!=0){preview.Remove(rig.GetInstanceID());Repose(rig);}
        }

        public override void OnInspectorGUI()
        {
            var rig=(IceBoardBrushRig)target;
            if(!rig.IsComplete){EditorGUILayout.HelpBox("Machine, head, link and roller must be assigned (head under this arm).",MessageType.Error);DrawDefaultInspector();return;}
            var config=Simulation(rig,out var game);
            AdoptManualMove(rig);
            serializedObject.Update();

            EditorGUILayout.LabelField("MECHANISM / simulation contact (draft until applied)",EditorStyles.boldLabel);
            var m=serializedObject.FindProperty("mechanism");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m.FindPropertyRelative("PivotX"),new GUIContent("Pivot lateral X (m)","Machine space; negative is the left side."));
            EditorGUILayout.PropertyField(m.FindPropertyRelative("PivotZ"),new GUIContent("Pivot forward Z (m)","From the rear axle; positive is forward."));
            EditorGUILayout.PropertyField(m.FindPropertyRelative("ArmLength"),new GUIContent("Arm length (m)","Pivot to brush disc centre."));
            EditorGUILayout.PropertyField(m.FindPropertyRelative("Radius"),new GUIContent("Brush contact radius (m)"));
            EditorGUILayout.PropertyField(m.FindPropertyRelative("DeployedDegrees"),new GUIContent("Working angle (deg)","0 = arm straight out to the left; positive swings the head rearward."));
            EditorGUILayout.PropertyField(m.FindPropertyRelative("RetractedDegrees"),new GUIContent("Stowed angle (deg)"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("COSMETIC / never affects contact",EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pivotHeight"),new GUIContent("Pivot height (m)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("deployedHeadHeight"),new GUIContent("Head height, working (m)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("retractedHeadHeight"),new GUIContent("Head height, stowed (m)"));
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
                bool sim=EditorGUILayout.Popup("Preview geometry",previewSimulation?1:0,new[]{"Draft (this rig)","Simulation (scene calibration)"})==1;
                if(EditorGUI.EndChangeCheck()) { previewSimulation=sim; SetPreview(rig,d); }
                if(GUILayout.Button("Reset Preview (stowed)")) { previewSimulation=false; SetPreview(rig,0); }
            }
            ShowGizmos=EditorGUILayout.Toggle("Scene gizmos",ShowGizmos);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("SIMULATION AGREEMENT",EditorStyles.boldLabel);
            var simulation=IceBoardBrushRig.Mechanism.From(config);
            if(rig.mechanism.Matches(simulation))EditorGUILayout.HelpBox("Draft matches the simulation. Play shows exactly this articulation.",MessageType.Info);
            else EditorGUILayout.HelpBox("Not applied: "+string.Join(", ",Differences(rig.mechanism,simulation))+
                ". Play poses the brush from the scene calibration (simulation), not this draft.",MessageType.Warning);
            foreach(var line in Diagnose(rig,config,rig.mechanism))EditorGUILayout.HelpBox(line.Value,line.Key);
            using(new EditorGUI.DisabledScope(game==null||Application.isPlaying||rig.mechanism.Matches(simulation)))
            {
                if(GUILayout.Button("Apply Brush Geometry to Simulation..."))
                {
                    string summary="Copy the brush mechanism into the scene calibration?\n\n"+string.Join("\n",Differences(rig.mechanism,simulation))+
                        "\n\nThis changes where the simulated brush sweeps snow. Undo is available (Edit > Undo).";
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
            if(game==null)EditorGUILayout.HelpBox("Prefab Mode: open the authored scene to apply geometry to its calibration.",MessageType.None);
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
            Vector3 tilt=EditorGUILayout.Vector3Field("Head tilt (deg)",rig.head.localEulerAngles);
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
            if(Application.isPlaying||!rig.IsComplete)return;
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
            Undo.RecordObject(game,"Apply brush geometry to simulation");
            Write(game.calibration,rig.mechanism);
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
            float side=config.VehicleHalfWidth;
            Vector3 working=IceBoardBrushRig.ContactLocal(m,1),stowed=IceBoardBrushRig.ContactLocal(m,0);
            float reach=-(working.x-m.Radius)-side;
            if(reach<=0)list.Add(new KeyValuePair<MessageType,string>(MessageType.Error,"Working brush edge is "+(-reach).ToString("F2")+
                " m inside the body side (x = -"+side.ToString("F3")+"): the body would reach the boards before the brush."));
            else list.Add(new KeyValuePair<MessageType,string>(reach<.08f?MessageType.Warning:MessageType.Info,"Working brush edge reaches "+reach.ToString("F2")+
                " m beyond the body side"+(reach<.08f?" (tight: the body nearly touches the boards before the brush does).":".")));
            float stowOut=-(stowed.x-m.Radius)-side;
            if(stowed.x>-.8f)list.Add(new KeyValuePair<MessageType,string>(MessageType.Warning,"Stowed brush centre is at x = "+stowed.x.ToString("F3")+
                ": the layout validation expects it left of x = -0.800 (outboard, clear of the chassis)."));
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

        void OnSceneGUI()
        {
            var rig=(IceBoardBrushRig)target;
            if(!rig.IsComplete||Application.isPlaying)return;
            AdoptManualMove(rig);
            var m=rig.mechanism;
            Transform machine=rig.machine;
            Vector3 P(Vector3 local)=>machine.TransformPoint(local);
            Vector3 pivot=P(new Vector3(m.PivotX,rig.pivotHeight,m.PivotZ));
            Vector3 up=machine.up;
            if(ShowGizmos)
            {
                // Travel arc at pivot height, working (green), stowed (grey) and contact disc outlines on the ice.
                Vector3 work=IceBoardBrushRig.ContactLocal(m,1),stow=IceBoardBrushRig.ContactLocal(m,0),now=IceBoardBrushRig.ContactLocal(m,PreviewDeployment(rig));
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
                // Body side reference line.
                Handles.color=new Color(.3f,.7f,1,.6f);
                var config=Simulation(rig,out _);
                Handles.DrawDottedLine(P(new Vector3(-config.VehicleHalfWidth,0,-config.BodyRear)),P(new Vector3(-config.VehicleHalfWidth,0,config.BodyFront)),4);
                // Simulation ghost when the draft is not applied.
                var sim=IceBoardBrushRig.Mechanism.From(config);
                if(!m.Matches(sim))
                {
                    Handles.color=new Color(1,.3f,1,.7f);
                    Vector3 simPivot=P(new Vector3(sim.PivotX,rig.pivotHeight,sim.PivotZ));
                    Handles.DrawDottedLine(simPivot,P(IceBoardBrushRig.ContactLocal(sim,1)+Vector3.up*rig.pivotHeight),3);
                    Handles.DrawWireDisc(P(IceBoardBrushRig.ContactLocal(sim,1)),up,sim.Radius);
                    Handles.Label(simPivot+up*.12f,"simulation (Play)");
                }
                Handles.color=Color.white; Handles.SphereHandleCap(0,pivot,Quaternion.identity,.05f,EventType.Repaint);
                Handles.Label(pivot+up*.1f,"brush pivot");
            }
            // Drag the working and stowed head points: sets that angle and the shared arm length.
            EndHandle(rig,1,"Board brush working position");
            EndHandle(rig,0,"Board brush stowed position");
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
