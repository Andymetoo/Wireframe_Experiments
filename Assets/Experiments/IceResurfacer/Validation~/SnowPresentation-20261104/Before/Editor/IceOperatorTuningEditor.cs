using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;

namespace IceResurfacer.Editor
{
    [CustomEditor(typeof(IceOperatorTuning))]
    public sealed class IceOperatorTuningEditor : UnityEditor.Editor
    {
        void Field(SerializedProperty parent,string name,string label,string tip)
        {
            float min,max;
            switch(name)
            {
                case "FloodFlowMultiplier":min=.1f;max=2;break;
                case "ShavingsExpansionMultiplier":min=1;max=4;break;
                case "LooseSnowPickupDepthMm":min=1;max=50;break;
                case "LooseSnowIntakeHeightMm":min=10;max=100;break;
                case "ConditionerRetentionM3":min=.005f;max=.2f;break;
                case "ConditionerSpillFillFraction":min=.05f;max=1;break;
                case "WashRecoveryFraction":min=0;max=1;break;
                default:min=.1f;max=4;break;
            }
            EditorGUILayout.Slider(parent.FindPropertyRelative(name),min,max,new GUIContent(label,tip));
        }
        void Meter(string label,double value,string units,string format="F3")
        {EditorGUILayout.LabelField(label,value.ToString(format)+" "+units);}
        void Preset(IceOperatorTuning t,string label,System.Action action)
        {if(GUILayout.Button(label)){Undo.RecordObject(t,label);action();EditorUtility.SetDirty(t);}}
        public override bool RequiresConstantRepaint()=>Application.isPlaying;
        public override void OnInspectorGUI()
        {
            var t=(IceOperatorTuning)target;var game=t.GetComponent<IceResurfacerGame>();
            serializedObject.Update();
            EditorGUILayout.HelpBox("Scene settings. Save edits outside Play Mode to keep them. Play edits are live experiments and revert on exit. Disable this component to restore the scene calibration; tanks and existing snow are preserved.",MessageType.Info);
            var p=serializedObject.FindProperty("simulation");
            EditorGUILayout.LabelField("SIMULATION / conserved transfers",EditorStyles.boldLabel);
            Field(p,"FloodFlowMultiplier","Flood flow multiplier (x)","0.4 gives 2.5 times the tank duration. Valve position still sets a fraction of this maximum; capacity is fixed. Range 0.1–2.");
            Field(p,"ShavingsExpansionMultiplier","New shavings expansion (x)","Multiplies loose bulk volume made from newly shaved solid ice. Existing loose snow and tank stock are never multiplied. Experimental bulk density calibration, range 1–4.");
            Field(p,"LooseSnowPickupDepthMm","Deep-bank plowing (mm/pass)","Saved pickup quota. Governs banks taller than the intake; normal banks are swept whole. All pickup enters the finite trough; excessive input can spill again. Range 1–50 mm.");
            Field(p,"LooseSnowIntakeHeightMm","Loose intake height (mm)","Physical height of an ordinary bank swept up on fully lowered moving contact. Default 50 mm. Taller banks use the deep-bank quota. Throughput and retention still limit transport. Range 10–100 mm.");
            Field(p,"HorizontalConveyorMultiplier","Horizontal throughput (x)","Scales the authored conveyor m³/s limit. Existing stopped, jam and throat backpressure behavior remains. Range 0.1–4.");
            Field(p,"VerticalConveyorMultiplier","Vertical throughput (x)","Scales tank delivery capacity; crowding, full/raised bin rejection and losses still apply. Range 0.1–4.");
            Field(p,"ConditionerRetentionM3","Nominal trough volume (m³)","Saved retention calibration, before the usable-fill fraction. Existing values are preserved. Range 0.005–0.2 m³.");
            Field(p,"ConditionerSpillFillFraction","Usable fill before spill (0–1)","Physical fraction below the side lips. Default 0.17 × 0.08 m³ = 13.6 L. Higher delays overflow; lower starts it sooner. Snow slumps from finite stock after reaching the lip. 1 restores the old nominal threshold.");
            Field(p,"WashRecoveryFraction","Wash recovery efficiency (0–1)","Fraction of fresh wash eligible for direct return; also limits old surface pickup using remaining pump capacity. No artificial drain. The net result depends on flow, priming, surface water and travel.");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PRESENTATION / no material created",EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tankVisualFullness"),new GUIContent("Bin visual fullness (x)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("overflowParticleDensity"),new GUIContent("Overflow particle density (x)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("overflowClumpSize"),new GUIContent("Overflow clump size (x)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("windrowRoughness"),new GUIContent("Windrow facets (0–1)"));
            serializedObject.ApplyModifiedProperties();
            if(!string.IsNullOrEmpty(t.lastError))EditorGUILayout.HelpBox(t.lastError,MessageType.Error);
            SimulatorConfig c;
            try{c=game.Sim!=null?game.Sim.Config:t.CreateRuntimeConfig(game.calibration);}
            catch(System.ArgumentException e){EditorGUILayout.HelpBox(e.Message,MessageType.Error);return;}
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("EFFECTIVE SETTINGS / actual units",EditorStyles.boldLabel);
            Meter("Flood at full valve",c.FloodMaxLitersPerSecond,"L/s");
            Meter("Full flood tank duration",c.FloodCapacityLiters/c.FloodMaxLitersPerSecond/60,"min");
            Meter("New snow / shaved solid ice",c.SnowExpansionRatio,"m³ loose / m³ solid");
            Meter("Horizontal capacity",c.HorizontalCapacityM3PerSecond,"m³/s","F4");
            Meter("Vertical capacity",c.VerticalCapacityM3PerSecond,"m³/s","F4");
            Meter("Usable snow retention",c.ConditionerUsableSnowCapacityM3*1000,"L");
            EditorGUILayout.LabelField("Fixed tank capacities",c.FloodCapacityLiters+" L flood / "+c.WashCapacityLiters+" L wash / "+c.SnowCapacityM3+" m³ snow");
            if(game.Sim!=null)
            {
                var s=game.Sim;
                EditorGUILayout.Space();EditorGUILayout.LabelField("MEASURED / simulation time, 0.25 s samples",EditorStyles.boldLabel);
                Meter("Actual flood draw",t.measuredFloodLps,"L/s");
                Meter("Actual wash supply / treatment",t.measuredWashSupplyLps,"L/s");
                Meter("Actual combined wash return",t.measuredWashReturnLps,"L/s");
                Meter("Actual solid shaving",t.measuredShavedSolidM3ps,"m³/s","F6");
                Meter("Expanded new shavings",t.measuredExpandedM3ps,"m³/s","F6");
                Meter("Existing loose snow pickup",t.measuredLoosePickupM3ps,"m³/s","F6");
                Meter("Actual tank delivery",t.measuredTankDeliveryM3ps,"m³/s","F6");
                Meter("Current tank stock",s.SnowTankM3,"m³");
                Meter("Total solid shaved",s.TotalShavedIceM3,"m³","F6");
                Meter("Total expanded new snow",s.TotalExpandedShavingsM3,"m³","F6");
                Meter("Total delivered (may include recollection)",s.TotalSnowCollectedM3,"m³","F6");
                Meter("Total overflow transfers (may repeat)",s.SpilledSnowM3,"m³","F6");
                Meter("Current overflow",s.EscapingSnowRate,"m³/s","F6");
                Meter("Trough stock",s.HorizontalSnowM3,"m³");
                Meter("Vertical stock",s.VerticalSnowM3,"m³");
                Meter("Wash missing from full",c.WashCapacityLiters-s.WashWaterLiters,"L");
                EditorGUILayout.LabelField("Transport condition",s.SnowCondition.ToString());
            }
            EditorGUILayout.Space();EditorGUILayout.LabelField("INDEPENDENT EXPERIMENTS",EditorStyles.boldLabel);
            Preset(t,"Flood: 2.5 times longer (0.4x)",()=>t.simulation.FloodFlowMultiplier=.4f);
            Preset(t,"New shavings: 2.5x loose volume",()=>t.simulation.ShavingsExpansionMultiplier=2.5f);
            Preset(t,"Bin appearance only: 2.5x fullness",()=>t.tankVisualFullness=2.5f);
            Preset(t,"Restore scene calibration and 1x presentation",()=>{t.simulation=OperatorTuningSettings.From(game.calibration);t.tankVisualFullness=t.overflowParticleDensity=t.overflowClumpSize=1;t.windrowRoughness=.7f;});
        }
    }
}
