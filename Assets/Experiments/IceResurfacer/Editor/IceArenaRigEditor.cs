using System.Collections.Generic;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Geometry-versus-simulation report for the authored arena. Cosmetic parts are free;
    /// physical parts must agree with SimulatorConfig / ServiceLayout. Writes only on buttons.
    /// </summary>
    [CustomEditor(typeof(IceArenaRig))]
    public sealed class IceArenaRigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var rig=(IceArenaRig)target;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("batchAtRuntime"));
            serializedObject.ApplyModifiedProperties();
            var context=IceCalibrationContext.Resolve(rig,rig.BakedConfigJson);
            var config=context.Config;
            IceCalibrationContext.DrawLabel(context);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PHYSICAL RINK / SimulatorConfig and ServiceLayout are authoritative",EditorStyles.boldLabel);
            bool drift=false;
            // In Play the parts are merged by runtime batching; show what was measured when bound.
            var findings=Application.isPlaying&&rig.BoundFindings!=null?rig.BoundFindings:IceArenaDrift.Evaluate(rig,config);
            if(Application.isPlaying&&rig.BoundFindings!=null)EditorGUILayout.HelpBox("Play Mode: measured when the arena was bound, before runtime batching.",MessageType.None);
            foreach(var finding in findings)
            {
                drift|=finding.Drifted;
                EditorGUILayout.LabelField(finding.Label,finding.Drifted?finding.Detail:"matches the simulation",EditorStyles.wordWrappedMiniLabel);
            }
            var changed=rig.PhysicalFieldsChangedSinceBake(config);
            if(changed.Count>0)
                EditorGUILayout.HelpBox("Calibration changed since the arena was generated: "+string.Join(", ",changed)+". Collision follows the calibration; "+
                    "regenerate the affected sections (Advanced Recovery) so the visible rink matches.",MessageType.Warning);
            if(drift)
                EditorGUILayout.HelpBox("Visible physical geometry disagrees with the collision boundaries above: the machine would hit walls or boards "+
                    "that are not where they look. Restore the generated poses, or change the calibration and regenerate the section.",MessageType.Warning);
            var moved=new List<IceArenaPart>();
            foreach(var part in rig.GetComponentsInChildren<IceArenaPart>(true))if((part.IsPhysical||HoldsPhysical(part))&&!part.AtBakedPose)moved.Add(part);
            using(new EditorGUI.DisabledScope(moved.Count==0||Application.isPlaying))
                if(GUILayout.Button("Restore generated poses of moved physical parts ("+moved.Count+")"))
                {
                    var targets=new List<Object>();foreach(var part in moved)targets.Add(part.transform);
                    Undo.RecordObjects(targets.ToArray(),"Restore arena physical poses");
                    foreach(var part in moved)part.RestoreBakedPose();
                }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("SECTIONS / each is its own prefab",EditorStyles.boldLabel);
            foreach(var section in rig.Sections())
                using(new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(section.name,EditorStyles.wordWrappedMiniLabel);
                    var source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(section.gameObject);
                    using(new EditorGUI.DisabledScope(source==null))
                        if(GUILayout.Button("Open prefab",GUILayout.Width(90)))AssetDatabase.OpenAsset(source);
                }
            EditorGUILayout.HelpBox("Free to edit: stands, roof, trusses, light fixtures and Lights, banners, signs, scoreboard, goals, trim and materials. "+
                "Physical (keep in place): ice sheet, boards/kickplate/rail/glass bands, gate frame, service floor, walls and pit, the water station frame. "+
                "The ice sheet's material is replaced by the live ice at Play; edit the SpatialIce template to change how ice looks.",MessageType.None);
        }
        // A section root carrying physical parts (the rink) moves them too; cosmetic sections are free.
        static bool HoldsPhysical(IceArenaPart part)
        {
            if(!part.sectionRoot)return false;
            foreach(var child in part.GetComponentsInChildren<IceArenaPart>(true))if(child!=part&&child.IsPhysical)return true;
            return false;
        }
    }
}
