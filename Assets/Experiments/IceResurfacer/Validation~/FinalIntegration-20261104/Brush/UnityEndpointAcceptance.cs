// Native acceptance hook, intentionally outside imported Assets code.
// Copy this file into the isolated project's Editor folder, then run
// -executeMethod IceResurfacer.Editor.UnityEndpointAcceptance.RunBatch
// -iceBrushEndpointAcceptance. Never run against the authored working project.
using System;
using System.Linq;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IceResurfacer.Editor
{
    public static class UnityEndpointAcceptance
    {
        const string Source="Assets/Experiments/IceResurfacer/Machine/AuroraR54.prefab";
        const string Folder="Assets/BrushEndpointAcceptanceScratch";
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        public static void RunBatch()
        {
            Check(Environment.GetCommandLineArgs().Contains("-iceBrushEndpointAcceptance"),"Explicit isolated validation flag required.");
            Check(Application.dataPath.IndexOf("Validation",StringComparison.OrdinalIgnoreCase)>=0,"Use the isolated validation project, not the authored working project.");
            Check(!AssetDatabase.IsValidFolder(Folder),"Scratch folder already exists; refusing to overwrite it.");
            Scene scene=default;
            try
            {
                AssetDatabase.CreateFolder("Assets","BrushEndpointAcceptanceScratch");
                scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source),scene);
                var rig=root.GetComponentInChildren<IceBoardBrushRig>(true);
                Check(rig!=null&&rig.IsComplete,"Authored rig bindings incomplete.");
                var original=rig.mechanism;
                rig.InitializeEndpoints();
                Check(rig.TryEndpointMechanism(out var initial,out string error),error);
                Check(initial.Matches(original),"Initialization changed current geometry.");
                var legacyTilt=rig.head.localRotation;
                var stowedBefore=rig.stowedEndpoint;
                var begin=typeof(IceBoardBrushRigEditor).GetMethod("BeginEndpoint",BindingFlags.NonPublic|BindingFlags.Static);
                var store=typeof(IceBoardBrushRigEditor).GetMethod("StoreEndpoint",BindingFlags.NonPublic|BindingFlags.Static);
                begin.Invoke(null,new object[]{rig,0});
                var draftStowed=rig.stowedEndpoint;draftStowed.position.y+=.04f;draftStowed.rotation=Quaternion.Euler(20,-70,15);
                store.Invoke(null,new object[]{rig,0,draftStowed});
                Check(Quaternion.Angle(rig.head.localRotation,legacyTilt)>1,"Fixture did not change the preview head tilt.");
                // Destroying the actual custom Inspector invokes its normal OnDisable path.
                var inspector=UnityEditor.Editor.CreateEditor(rig,typeof(IceBoardBrushRigEditor));
                UnityEngine.Object.DestroyImmediate(inspector);
                Check(Quaternion.Angle(rig.head.localRotation,legacyTilt)<.001f,"Leaving STOWED editing leaked a draft head rotation.");
                Check(Mathf.Abs(rig.machine.InverseTransformPoint(rig.head.position).y-rig.retractedHeadHeight)<.00001f,"Leaving STOWED editing leaked its preview height.");
                rig.stowedEndpoint=stowedBefore;
                var changed=rig.workingEndpoint;
                BoardBrushArticulation.Contact(original.PivotX,original.PivotZ,original.ArmLength,-10*Mathf.Deg2Rad,out float x,out float z);
                changed.position=new Vector3(x,.17f,z);changed.rotation=Quaternion.Euler(5,10,3);
                var before=rig.workingEndpoint;
                // This is the same edit path used by the Inspector and Scene handles.
                store.Invoke(null,new object[]{rig,1,changed});Undo.FlushUndoRecordObjects();
                Check((rig.workingEndpoint.position-changed.position).sqrMagnitude<1e-10f,"Scene edit not recorded.");
                Undo.PerformUndo();Check((rig.workingEndpoint.position-before.position).sqrMagnitude<1e-10f,"Undo failed.");
                Undo.PerformRedo();Check((rig.workingEndpoint.position-changed.position).sqrMagnitude<1e-10f,"Redo failed.");
                rig.stowedEndpointSaved=rig.workingEndpointSaved=true;rig.useAuthoredEndpoints=true;
                var config=new SimulatorConfig();float flood=config.FloodMaxLitersPerSecond;
                Check(rig.ApplyAuthoredConfiguration(config,out error),error);config.Validate();
                Check(Mathf.Abs(config.BrushDeployedDegrees+10)<.001f&&config.FloodMaxLitersPerSecond==flood,"Endpoint geometry/config isolation failed.");
                foreach(float deployment in new[]{0f,.25f,.5f,.75f,1f})
                {
                    IceBoardBrushRigEditor.SetPreview(rig,deployment);
                    BoardBrushArticulation.Contact(config,deployment,out float px,out float pz);
                    Vector3 actual=rig.machine.InverseTransformPoint(rig.head.position);
                    Check(Vector2.Distance(new Vector2(actual.x,actual.z),new Vector2(px,pz))<.0001f,"Intermediate endpoint preview differs from physical articulation.");
                }
                // Scene save must reset transient preview without losing approved endpoints.
                IceBoardBrushRigEditor.SetPreview(rig,.5f);
                Check(EditorSceneManager.SaveScene(scene,Folder+"/Acceptance.unity"),"Scene save failed.");
                Check(IceBoardBrushRigEditor.PreviewDeployment(rig)==0,"Scene save persisted intermediate preview.");
                var saved=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Acceptance.prefab");
                Check(saved!=null,"Prefab save failed.");
                EditorSceneManager.CloseScene(scene,true);
                scene=EditorSceneManager.OpenScene(Folder+"/Acceptance.unity",OpenSceneMode.Additive);
                var reloaded=scene.GetRootGameObjects()[0].GetComponentInChildren<IceBoardBrushRig>(true);
                Check(reloaded.useAuthoredEndpoints&&reloaded.stowedEndpointSaved&&reloaded.workingEndpointSaved,"Endpoint approval did not survive scene reload.");
                Check((reloaded.workingEndpoint.position-changed.position).sqrMagnitude<1e-10f,"Scene endpoint changed after reload.");
                var savedRig=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Acceptance.prefab").GetComponentInChildren<IceBoardBrushRig>(true);
                Check(savedRig.useAuthoredEndpoints&&(savedRig.workingEndpoint.position-changed.position).sqrMagnitude<1e-10f,"Prefab endpoint failed serialization.");
                Debug.Log("PASS native board brush endpoint import, Undo/Redo, config isolation, five previews, scene save/reload and prefab serialization.");
            }
            finally
            {
                if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
                if(AssetDatabase.IsValidFolder(Folder))AssetDatabase.DeleteAsset(Folder);
            }
        }
    }
}
