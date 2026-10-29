using System;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>Explicit, disposable unbatched preview of the one runtime builder.</summary>
    public sealed class IceMachineGeometryEditor : EditorWindow
    {
        IceResurfacerGame source;
        GameObject preview;
        IceWorld world;
        Vector2 scroll;
        string problem;

        [MenuItem("Experiments/Ice Resurfacer/Machine Geometry Editor")]
        public static void Open()
        {
            var window=GetWindow<IceMachineGeometryEditor>("Aurora geometry");
            window.minSize=new Vector2(360,460);
            window.source=Selection.activeGameObject!=null?Selection.activeGameObject.GetComponentInParent<IceResurfacerGame>():null;
            if(window.source==null)window.source=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
            window.Show();
        }
        void OnEnable() { EditorApplication.playModeStateChanged+=PlayState; }
        void OnDisable() { EditorApplication.playModeStateChanged-=PlayState;ClearPreview(); }
        void PlayState(PlayModeStateChange state)
        { if(state==PlayModeStateChange.ExitingEditMode)ClearPreview(); }

        void OnGUI()
        {
            EditorGUILayout.LabelField("AURORA / GEOMETRY AUTHORING",EditorStyles.boldLabel);
            source=(IceResurfacerGame)EditorGUILayout.ObjectField("Launch scene machine",source,typeof(IceResurfacerGame),true);
            EditorGUILayout.HelpBox("Edit the scene's shared geometry parameters, then rebuild the selectable preview. Play uses the same builder with batching. Save the launch scene normally to retain these values.",MessageType.Info);
            if(source==null)
            { EditorGUILayout.LabelField("Open the IceResurfacer launch scene and select its machine root.",EditorStyles.wordWrappedLabel);return; }
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorGUILayout.HelpBox("Stop Play Mode to author geometry. This tool does not stop or switch the Editor for you.",MessageType.Info);return; }
            scroll=EditorGUILayout.BeginScrollView(scroll);
            var serialized=new SerializedObject(source);
            serialized.Update();
            var config=serialized.FindProperty("calibration");
            Field(config,"TankRoofHeight","Tank roof above hinge (m)");
            Field(config,"TankLateralOffset","Tank center offset (m)");
            Field(config,"ConveyorHeightOffset","Conveyor height offset (m)");
            Field(config,"WheelTrack","Wheel center track (m)");
            Field(config,"BrushPivotX","Brush mount lateral (m)");
            Field(config,"BrushPivotZ","Brush mount forward (m)");
            Field(config,"BrushArmLength","Brush extension (m)");
            Field(config,"CockpitOffsetX","Operator station lateral (m)");
            Field(config,"CockpitOffsetZ","Operator station forward (m)");
            Field(config,"SteeringWheelZ","Steering hub forward (m)");
            Field(config,"SteeringWheelHeight","Steering hub height (m)");
            Field(config,"SteeringWheelTilt","Steering wheel tilt from horizontal (deg)");
            Field(config,"BladeWheelX","Blade spindle lateral (m)");
            Field(config,"BladeWheelZ","Blade spindle forward (m)");
            Field(config,"BladeWheelHeight","Blade handwheel height (m)");
            Field(config,"WaterValveStemHeight","Water handle stem height (m)");
            Field(config,"SnowBreakerX","Snow breaker lateral (m)");
            Field(config,"SnowBreakerZ","Snow breaker forward (m)");
            serialized.ApplyModifiedProperties();
            try { source.calibration.Validate();problem=null; }
            catch(Exception e) { problem=e.Message; }
            if(problem!=null)EditorGUILayout.HelpBox(problem,MessageType.Error);
            EditorGUILayout.HelpBox("Wheel track and brush changes also update the simulator's contacts and alignment references. Tank geometry also updates the raised-shell collision samples, inlet opening and snow fill. These are mechanical parameters, so review clearances after large edits.",MessageType.None);
            EditorGUILayout.LabelField("Physical tank fill envelope",source.calibration.TankSnowPhysicalCapacityM3.ToString("F3")+" m³");
            EditorGUILayout.LabelField("Snow stock capacity",source.calibration.SnowCapacityM3.ToString("F2")+" m³ loose equivalent");
            using(new EditorGUI.DisabledScope(problem!=null))
                if(GUILayout.Button("Rebuild selectable machine preview",GUILayout.Height(30)))Rebuild();
            using(new EditorGUI.DisabledScope(preview==null))
            {
                if(GUILayout.Button("Frame preview in Scene view"))
                { Selection.activeGameObject=preview;SceneView.lastActiveSceneView?.FrameSelected(); }
                if(GUILayout.Button("Select snow tank"))Selection.activeTransform=world.TankPivot;
                if(GUILayout.Button("Select physical hydraulic controls"))Selection.activeTransform=world.ConditionerLever.parent;
                if(GUILayout.Button("Remove preview"))ClearPreview();
            }
            EditorGUILayout.HelpBox("The preview is temporary and never saved into the launch scene or run in Play Mode. Its individual meshes are inspectable; manual preview-transform edits are discarded on rebuild. Use the parameters above for persistent changes. No separate prefab copy can drift from the runtime machine.",MessageType.None);
            EditorGUILayout.EndScrollView();
        }
        static void Field(SerializedProperty config,string name,string caption)
        { EditorGUILayout.PropertyField(config.FindPropertyRelative(name),new GUIContent(caption)); }
        void Rebuild()
        {
            ClearPreview();
            preview=new GameObject("AURORA EDITOR PREVIEW / disposable selectable assemblies");
            preview.hideFlags=HideFlags.DontSaveInEditor|HideFlags.DontSaveInBuild;
            preview.transform.position=source.transform.position;
            try
            {
                world=IceWorldBuilder.BuildMachinePreview(preview.transform,source.litTemplate,source.calibration);
                world.Conditioner.localPosition=new Vector3(0,.34f,-source.calibration.HitchBehindAxle-source.calibration.DrawbarLength);
                // Runtime-owned cylinder transforms normally follow the hydraulics.
                // Give the static preview its real lowered anchors explicitly.
                for(int i=0;i<2;i++)
                {
                    float side=i==0?-1:1;
                    PreviewCylinder(world.TankCylinderBarrels[i],world.TankCylinderRods[i],
                        world.Machine.TransformPoint(side*.69f,.55f,.72f),world.TankPivot.TransformPoint(source.calibration.TankLateralOffset+side*.69f,source.calibration.TankGeometryY(.40f),-.74f),.095f,.043f,.64f);
                    PreviewCylinder(world.ConditionerCylinderBarrels[i],world.ConditionerCylinderRods[i],
                        world.Machine.TransformPoint(side*.55f,.76f,-.57f),world.Conditioner.TransformPoint(side*.55f,.43f,.30f),.074f,.035f,.42f);
                }
                foreach(var transform in preview.GetComponentsInChildren<Transform>(true))
                    transform.gameObject.hideFlags=HideFlags.DontSaveInEditor|HideFlags.DontSaveInBuild;
                Selection.activeGameObject=preview;SceneView.lastActiveSceneView?.FrameSelected();
            }
            catch(Exception e) { ClearPreview();problem=e.Message; }
        }
        static void PreviewCylinder(Transform barrel,Transform rod,Vector3 anchor,Vector3 clevis,float bore,float shaft,float barrelLength)
        {
            Vector3 axis=clevis-anchor;float length=axis.magnitude;axis/=Mathf.Max(.001f,length);
            float body=Mathf.Min(barrelLength,length*.78f),start=body*.78f;
            barrel.SetPositionAndRotation(anchor+axis*body*.5f,Quaternion.FromToRotation(Vector3.up,axis));barrel.localScale=new Vector3(bore,body*.5f,bore);
            rod.SetPositionAndRotation(anchor+axis*(start+length)*.5f,barrel.rotation);rod.localScale=new Vector3(shaft,(length-start)*.5f,shaft);
        }
        void ClearPreview()
        {
            if(preview!=null)DestroyImmediate(preview);
            if(world!=null)
            {
                foreach(var mesh in world.Meshes)if(mesh!=null)DestroyImmediate(mesh);
                foreach(var material in world.Materials)if(material!=null)DestroyImmediate(material);
            }
            preview=null;world=null;
        }
    }
}
