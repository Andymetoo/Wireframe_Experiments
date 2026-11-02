using System;
using System.Collections.Generic;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Ownership report for the authored machine: physics anchors follow SimulatorConfig;
    /// operator controls and cosmetics are authored. Writes only on explicit buttons.
    /// </summary>
    [CustomEditor(typeof(IceMachineRig))]
    public sealed class IceMachineRigEditor : UnityEditor.Editor
    {
        public const float DriftTolerance=.001f, ReachWarning=.85f, GripSpacingWarning=.06f;
        bool showParts;

        public override void OnInspectorGUI()
        {
            var rig=(IceMachineRig)target;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("batchAtRuntime"));
            serializedObject.ApplyModifiedProperties();
            IceWorld w;
            try { w=rig.Collect(); }
            catch(Exception e) { EditorGUILayout.HelpBox(e.Message,MessageType.Error);DrawParts();return; }
            var game=rig.GetComponentInParent<IceResurfacerGame>();
            var config=game!=null?game.calibration:(string.IsNullOrEmpty(rig.BakedConfigJson)?new SimulatorConfig():JsonUtility.FromJson<SimulatorConfig>(rig.BakedConfigJson));
            if(game==null)EditorGUILayout.HelpBox("Prefab view: comparing against the configuration captured at bake time.",MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PHYSICS ANCHORS / SimulatorConfig is authoritative",EditorStyles.boldLabel);
            bool drift=false;
            foreach(var anchor in IceMachineRig.MechanicalAnchors(w,config))
            {
                float error=Vector3.Distance(anchor.Actual,anchor.Expected);
                drift|=error>DriftTolerance;
                EditorGUILayout.LabelField(anchor.Label,error>DriftTolerance?(error*1000).ToString("F1")+" mm from config":"matches config");
            }
            foreach(var width in Widths(w,config))
            {
                drift|=Mathf.Abs(width.Value)>DriftTolerance;
                EditorGUILayout.LabelField(width.Key,Mathf.Abs(width.Value)>DriftTolerance?(width.Value*1000).ToString("+0.0;-0.0")+" mm from config":"matches config");
            }
            if(drift)EditorGUILayout.HelpBox("These parts disagree with the simulation's configuration, so physics and visuals would differ. "+
                "Change the value in the scene calibration (Machine Geometry Editor) and apply, rather than moving the anchor.",MessageType.Warning);
            using(new EditorGUI.DisabledScope(!drift||game==null||Application.isPlaying))
                if(GUILayout.Button("Apply config to physics anchors"))ApplyConfig(w,config);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("BOARD BRUSH / articulation",EditorStyles.boldLabel);
            var brushRig=w.BrushArm!=null?w.BrushArm.GetComponent<IceBoardBrushRig>():null;
            using(new EditorGUI.DisabledScope(Application.isPlaying||w.BrushArm==null))
            {
                if(brushRig==null&&GUILayout.Button("Add Board Brush Rig (keeps the current brush pose)"))
                {
                    brushRig=IceBoardBrushRigEditor.AddRig(w,config,out string summary);
                    Debug.Log("Board brush rig added. "+summary,brushRig);
                    Selection.activeObject=brushRig.gameObject;
                }
                else if(brushRig!=null&&GUILayout.Button("Select Board Brush Rig"))Selection.activeObject=brushRig.gameObject;
            }
            if(GUILayout.Button("Conditioner Alignment..."))IceConditionerAlignmentWindow.Open();

            var changed=rig.ShapeFieldsChangedSinceBake(config);
            if(changed.Count>0)EditorGUILayout.HelpBox("Changed since bake, affecting mesh shape: "+string.Join(", ",changed)+
                ". The machine is never regenerated automatically: use Advanced Recovery > Generate Fresh Machine Copy for a new baseline "+
                "(your prefab is untouched), or Replace or Restore Authored Assets to replace it behind a backup.",MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("OPERATOR CONTROLS / authored here",EditorStyles.boldLabel);
            var moved=rig.RearrangedControls();
            EditorGUILayout.LabelField("Moved since bake",moved.Count==0?"none":string.Join(", ",moved),EditorStyles.wordWrappedLabel);
            foreach(string advice in ControlAdvisories(w,config))EditorGUILayout.HelpBox(advice,MessageType.Info);
            EditorGUILayout.HelpBox("Move or rotate whole mounts (lever bank, valve manifold, handwheel assembly, snow breaker, steering column). "+
                "Runtime animates only lever/valve rotation, handwheel turns and plunger stroke, on top of the authored rest pose.",MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("RUNTIME-DRIVEN PIVOTS / edit their children",EditorStyles.boldLabel);
            var names=new List<string>();
            foreach(var pivot in IceMachineRig.RuntimeDrivenPivots(w))names.Add(pivot.name);
            EditorGUILayout.LabelField(string.Join("; ",names),EditorStyles.wordWrappedMiniLabel);
            if(Selection.activeTransform!=null&&IceMachineRig.RuntimeDrivenPivots(w).Contains(Selection.activeTransform))
                EditorGUILayout.HelpBox("The selected transform is overwritten every frame in Play Mode.",MessageType.Warning);
            using(new EditorGUI.DisabledScope(Application.isPlaying))
                if(GUILayout.Button("Re-pose hydraulic cylinders on their clevis pins"))
                {
                    var parts=new List<UnityEngine.Object>();
                    foreach(var group in new[]{w.TankCylinderBarrels,w.TankCylinderRods,w.ConditionerCylinderBarrels,w.ConditionerCylinderRods})parts.AddRange(group);
                    Undo.RecordObjects(parts.ToArray(),"Re-pose Aurora cylinders");
                    IcePhysicalControls.PoseCylinders(w);
                }
            DrawParts();
        }

        void DrawParts()
        {
            showParts=EditorGUILayout.Foldout(showParts,"Bound parts (IceWorld contract)");
            if(!showParts)return;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("parts"),true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("partArrays"),true);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Rendered working widths minus the simulated ones (metres).</summary>
        public static List<KeyValuePair<string,float>> Widths(IceWorld w,SimulatorConfig config)
        {
            var list=new List<KeyValuePair<string,float>>();
            if(w.CuttingBladeMesh!=null)list.Add(new KeyValuePair<string,float>("Cutting blade width",w.CuttingBladeMesh.bounds.size.x*w.CuttingBlade.lossyScale.x-config.ConditionerWidth));
            if(w.DistributionTowelMesh!=null)list.Add(new KeyValuePair<string,float>("Water towel width",w.DistributionTowelMesh.bounds.size.x*w.DistributionTowel.lossyScale.x-config.WaterDistributionWidth));
            return list;
        }

        /// <summary>Where each control is grabbed, in world space (same points the picker targets).</summary>
        public static List<KeyValuePair<string,Vector3>> Grips(IceWorld w,SimulatorConfig config)
        {
            return new List<KeyValuePair<string,Vector3>>
            {
                new KeyValuePair<string,Vector3>("Conditioner lever",w.ConditionerLever.TransformPoint(0,.225f,0)),
                new KeyValuePair<string,Vector3>("Horizontal auger lever",w.HorizontalAugerLever.TransformPoint(0,.225f,0)),
                new KeyValuePair<string,Vector3>("Vertical auger lever",w.VerticalAugerLever.TransformPoint(0,.225f,0)),
                new KeyValuePair<string,Vector3>("Dump lever",w.DumpLever.TransformPoint(0,.225f,0)),
                new KeyValuePair<string,Vector3>("Flood valve",w.FloodLever.TransformPoint(-.09f,config.WaterValveStemHeight,0)),
                new KeyValuePair<string,Vector3>("Wash valve",w.WashValve.TransformPoint(-.09f,config.WaterValveStemHeight,0)),
                new KeyValuePair<string,Vector3>("Blade handwheel",w.BladeWheel.TransformPoint(.11f,.045f,0)),
                new KeyValuePair<string,Vector3>("Snow breaker",w.SnowBreakerPlunger.TransformPoint(0,.22f,0)),
            };
        }

        /// <summary>Non-blocking ergonomics: reach, crowding and cab envelope.</summary>
        public static List<string> ControlAdvisories(IceWorld w,SimulatorConfig config)
        {
            var advice=new List<string>();
            var grips=Grips(w,config);
            Vector3 eye=w.CameraMount.position;
            foreach(var grip in grips)
            {
                float reach=Vector3.Distance(eye,grip.Value);
                if(reach>ReachWarning)advice.Add(grip.Key+" is "+reach.ToString("F2")+" m from the seated eye (over "+ReachWarning+" m).");
                Vector3 local=w.Machine.InverseTransformPoint(grip.Value);
                if(Mathf.Abs(local.x)>1.38f||local.z< -1.88f||local.z>2.84f)advice.Add(grip.Key+" is outside the machine body envelope.");
            }
            for(int i=0;i<grips.Count;i++)for(int j=i+1;j<grips.Count;j++)
                if(Vector3.Distance(grips[i].Value,grips[j].Value)<GripSpacingWarning)
                    advice.Add(grips[i].Key+" and "+grips[j].Key+" grips are closer than "+(GripSpacingWarning*100)+" cm; clicks may select the wrong one.");
            return advice;
        }

        static void ApplyConfig(IceWorld w,SimulatorConfig config)
        {
            var anchors=IceMachineRig.MechanicalAnchors(w,config);
            var parts=new List<UnityEngine.Object>();
            foreach(var anchor in anchors)parts.Add(anchor.Part);
            // A brush rig re-poses the arm from its draft, so config wins there too.
            var brushRig=w.BrushArm!=null?w.BrushArm.GetComponent<IceBoardBrushRig>():null;
            if(brushRig!=null)parts.Add(brushRig);
            Undo.RecordObjects(parts.ToArray(),"Apply Aurora config anchors");
            if(brushRig!=null)brushRig.mechanism=IceBoardBrushRig.Mechanism.From(config);
            foreach(var anchor in anchors)anchor.Part.position=anchor.Frame.TransformPoint(anchor.Expected);
            if(brushRig!=null)IceBoardBrushRigEditor.Repose(brushRig);
        }
    }
}
