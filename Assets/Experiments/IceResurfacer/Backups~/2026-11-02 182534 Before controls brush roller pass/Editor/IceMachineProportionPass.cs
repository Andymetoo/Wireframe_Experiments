using System;
using System.Collections.Generic;
using System.Text;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// One bounded, reversible edit of the authored machine prefab toward the reference resurfacer:
    /// the operator station sits higher and further back, over the conditioner, and the right-hand
    /// controls are regrouped around the seat. Only operator-control mounts and cosmetic bodywork are
    /// moved or rescaled; wheels, tank, hinge, conveyor, brush, guide roller and conditioner are not.
    /// Each part is changed only if it is still at its generated pose, so manual edits are never
    /// overwritten. The prefab and authored scene are backed up to Backups~ first.
    /// </summary>
    public static class IceMachineProportionPass
    {
        const string Menu="Experiments/Ice Resurfacer/Authored Scene/Apply Reference Proportions Pass (machine)...";
        public const string BackupLabel="Machine proportions pass";
        const string Deck="Left-hand operator station", Body="Chassis and bodywork", Machine="AURORA R-54 / rear axle origin";
        const string Blade="Complete blade screw and handwheel assembly";

        // Operator station: +0.13 m up (floor clears the fully raised conditioner linkage) and
        // 0.15 m back, so the seat is over the front of the conditioner as on the reference machine.
        static readonly Vector3 StationShift=new Vector3(0,.13f,-.15f);

        public sealed class Change
        {
            public string Label, Parent, Name;
            public Vector3 From, To;                      // local position in Parent
            public Vector3 Turn=Vector3.zero;             // added local Euler rotation
            public Vector3 Scale=Vector3.one;             // multiplied local scale
            public string Why;
            public override string ToString()
            {
                var text=new StringBuilder(Label).Append(": moved ").Append(V(To-From));
                if(Turn!=Vector3.zero)text.Append(", turned ").Append(V(Turn)).Append(" deg");
                if(Scale!=Vector3.one)text.Append(", scaled x").Append(V(Scale));
                return text.Append(" - ").Append(Why).ToString();
            }
        }

        public static List<Change> Changes()
        {
            Vector3 deck=new Vector3(-.61f,0,-.49f);
            var list=new List<Change>
            {
                new Change{Label="Operator station (floor, seat, steering, dash, pedals, all controls, grab rails)",Parent=Machine,Name=Deck,
                    From=deck,To=deck+StationShift,Why="seat over the conditioner, platform above its raised linkage"},
                new Change{Label="Equipment-side rear safety rail",Parent=Body,Name="Equipment-side rear safety rail",
                    From=new Vector3(.89f,1.485f,-.82f),To=new Vector3(.89f,1.485f,-.82f)+StationShift,Why="keeps framing the moved station"},
                new Change{Label="Equipment-side top safety rail",Parent=Body,Name="Equipment-side top safety rail",
                    From=new Vector3(.89f,1.81f,-.47f),To=new Vector3(.89f,1.81f,-.47f)+StationShift,Why="keeps framing the moved station"},
                new Change{Label="Right equipment deck",Parent=Body,Name="Right equipment deck",
                    From=new Vector3(.48f,.83f,-.37f),To=new Vector3(.48f,.96f,-.52f),Scale=new Vector3(1,1,1.3f),
                    Why="carries the right-hand platform back under the moved controls"},
                // Controls, in station coordinates (unchanged by the station shift above).
                new Change{Label="Hydraulic lever bank",Parent=Deck,Name="Transverse four spool hydraulic bank",
                    From=new Vector3(.47f,1.42f,.31f),To=new Vector3(.49f,1.36f,.28f),Turn=new Vector3(0,10,0),Scale=Vector3.one*.9f,
                    Why="lower, slimmer console angled toward the seat, right of the steering wheel"},
                new Change{Label="Water valve manifold",Parent=Deck,Name="Adjacent flood and wash water manifold",
                    From=new Vector3(.65f,1.44f,.04f),To=new Vector3(.70f,1.42f,-.22f),Why="side console beside the seat at armrest height"},
                new Change{Label="Water manifold left bracket",Parent=Deck,Name="Water manifold left bracket",
                    From=new Vector3(.40f,1.28f,.04f),To=new Vector3(.45f,1.26f,-.22f),Why="follows the manifold"},
                new Change{Label="Water manifold right bracket",Parent=Deck,Name="Water manifold right bracket",
                    From=new Vector3(.88f,1.28f,.04f),To=new Vector3(.93f,1.26f,-.22f),Why="follows the manifold"},
                new Change{Label="Blade handwheel assembly",Parent=Deck,Name=Blade,
                    From=new Vector3(.65f,1.56f,-.20f),To=new Vector3(.50f,1.30f,-.02f),
                    Why="low at the seat's front-right corner, as on the reference machine"},
            };
            return list;
        }

        // The blade screw would otherwise reach down through the platform into the conditioner.
        const string Spindle="Blade adjustment spindle", Collar="Blade spindle lower collar";
        const float FloorTop=.918f;   // top of the station's non-slip mat, station coordinates

        // ---- Menu -------------------------------------------------------------------------

        [MenuItem(Menu)]
        static void ApplyFromMenu()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorUtility.DisplayDialog("Ice Resurfacer","Exit Play Mode first.","OK");return;}
            try
            {
                var preview=new StringBuilder();
                foreach(var change in Changes())preview.AppendLine("• "+change);
                if(!EditorUtility.DisplayDialog("Reference proportions pass",
                    "Edits "+IceMachineBaker.PrefabPath+" in place (no rebake, no regeneration):\n\n"+preview+
                    "• Blade screw spindle shortened to stop at the platform\n\n"+
                    "Parts you have already moved are skipped. The prefab and the authored scene are backed up to "+
                    IceAuthoredRecovery.BackupFolder+" first; undo with Advanced Recovery > Replace or Restore Authored Assets > Restore backup.",
                    "Back up, then apply","Cancel"))return;
                var report=Apply(out string backup);
                Debug.Log("AURORA PROPORTIONS PASS\n"+report);
                EditorUtility.DisplayDialog("Reference proportions pass",report+"\nBackup: "+backup,"OK");
            }
            catch(Exception e) { Debug.LogException(e);EditorUtility.DisplayDialog("Ice Resurfacer",e.Message,"OK"); }
        }
        [MenuItem(Menu,true)]
        static bool CanApply() { return !EditorApplication.isPlaying&&AssetDatabase.LoadMainAssetAtPath(IceMachineBaker.PrefabPath)!=null; }

        // ---- Apply ------------------------------------------------------------------------

        /// <summary>Backs up, edits the prefab asset and returns a report of exactly what changed.</summary>
        public static string Apply(out string backup)
        {
            if(PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Close Prefab Mode first. Nothing was changed.");
            var scene=EditorSceneManager.GetSceneByPath(IceMachineBaker.AuthoredScenePath);
            if(scene.IsValid()&&scene.isDirty)throw new InvalidOperationException("Save the authored scene first. Nothing was changed.");

            // Dry run on a throwaway copy of the contents: refuse before backing up if nothing applies.
            var root=PrefabUtility.LoadPrefabContents(IceMachineBaker.PrefabPath);
            List<string> applied,skipped;
            try { Edit(root,out applied,out skipped); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            if(applied.Count==0)throw new InvalidOperationException("Nothing to apply: every part has already been moved from its generated pose.\n"+string.Join("\n",skipped));

            backup=IceAuthoredRecovery.BackUp(BackupLabel,new[]{IceMachineBaker.PrefabPath,IceMachineBaker.AuthoredScenePath});
            root=PrefabUtility.LoadPrefabContents(IceMachineBaker.PrefabPath);
            try
            {
                var rig=root.GetComponent<IceMachineRig>();
                var anchors=AnchorPositions(rig);
                Edit(root,out applied,out skipped);
                var after=AnchorPositions(rig);
                foreach(var anchor in anchors)
                    if(Vector3.Distance(anchor.Value,after[anchor.Key])>1e-5f)
                        throw new InvalidOperationException("Mechanical anchor would move: "+anchor.Key+". Nothing was saved.");
                PrefabUtility.SaveAsPrefabAsset(root,IceMachineBaker.PrefabPath,out bool saved);
                if(!saved)throw new InvalidOperationException("Unity did not save "+IceMachineBaker.PrefabPath+". Nothing was changed.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var report=new StringBuilder("Changed in "+IceMachineBaker.PrefabPath+":\n");
            foreach(string line in applied)report.AppendLine("  "+line);
            if(skipped.Count>0){report.AppendLine("Skipped (already moved by you, left as is):");foreach(string line in skipped)report.AppendLine("  "+line);}
            foreach(string line in SceneOverrides())report.AppendLine("Note: "+line);
            return report.ToString();
        }

        /// <summary>Applies every change whose part is still at its generated pose.</summary>
        public static void Edit(GameObject root,out List<string> applied,out List<string> skipped)
        {
            applied=new List<string>();skipped=new List<string>();
            foreach(var change in Changes())
            {
                var parent=Find(root.transform,change.Parent);
                var part=parent!=null?Child(parent,change.Name):null;
                if(part==null){skipped.Add(change.Label+" (not found: renamed or removed)");continue;}
                if(Vector3.Distance(part.localPosition,change.From)>.001f){skipped.Add(change.Label+" (already at "+V(part.localPosition)+")");continue;}
                part.localPosition=change.To;
                if(change.Turn!=Vector3.zero)part.localRotation=part.localRotation*Quaternion.Euler(change.Turn);
                if(change.Scale!=Vector3.one)part.localScale=Vector3.Scale(part.localScale,change.Scale);
                applied.Add(change.ToString());
                if(change.Name==Blade)ShortenSpindle(part,applied,skipped);
            }
        }

        static void ShortenSpindle(Transform assembly,List<string> applied,List<string> skipped)
        {
            float bottom=FloorTop-assembly.localPosition.y;   // assembly coordinates (unscaled, unrotated)
            var spindle=Child(assembly,Spindle);var collar=Child(assembly,Collar);
            if(spindle==null||collar==null||Mathf.Abs(spindle.localScale.y-.525f)>.002f){skipped.Add("Blade spindle (edited or missing)");return;}
            float top=spindle.localPosition.y+spindle.localScale.y;
            spindle.localPosition=new Vector3(0,(top+bottom)*.5f,0);
            spindle.localScale=new Vector3(spindle.localScale.x,(top-bottom)*.5f,spindle.localScale.z);
            collar.localPosition=new Vector3(0,bottom+.03f,0);
            applied.Add("Blade adjustment spindle: shortened to "+(top-bottom).ToString("F2")+" m and lower collar moved onto the platform; no longer passes through the conditioner");
        }

        static Dictionary<string,Vector3> AnchorPositions(IceMachineRig rig)
        {
            var world=rig.Collect();var positions=new Dictionary<string,Vector3>();
            var config=new IceResurfacer.Core.SimulatorConfig();
            foreach(var anchor in IceMachineRig.MechanicalAnchors(world,config))positions[anchor.Label]=anchor.Actual;
            foreach(var cylinder in new[]{world.TankCylinderBase,world.TankCylinderClevis,world.ConditionerCylinderBase,world.ConditionerCylinderClevis})
                for(int i=0;i<cylinder.Length;i++)positions[cylinder[i].name+" "+i]=world.Machine.InverseTransformPoint(cylinder[i].position);
            positions["Conditioner"]=world.Machine.InverseTransformPoint(world.Conditioner.position);
            return positions;
        }

        /// <summary>Scene-instance overrides on the parts this pass edits (they would mask the prefab change).</summary>
        public static List<string> SceneOverrides()
        {
            var notes=new List<string>();
            var scene=EditorSceneManager.GetSceneByPath(IceMachineBaker.AuthoredScenePath);
            if(!scene.IsValid()||!scene.isLoaded)return notes;
            var names=new HashSet<string>{Spindle,Collar};
            foreach(var change in Changes())names.Add(change.Name);
            foreach(var rig in UnityEngine.Object.FindObjectsByType<IceMachineRig>(FindObjectsInactive.Include))
                foreach(var mod in PrefabUtility.GetPropertyModifications(rig.gameObject)??new PropertyModification[0])
                    if(mod.target is Transform t&&names.Contains(t.name)&&mod.propertyPath.StartsWith("m_Local"))
                        notes.Add("the scene overrides "+t.name+" "+mod.propertyPath+"; that override still wins over this change");
            return notes;
        }

        static Transform Find(Transform root,string name)
        {
            if(root.name==name)return root;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;
            return null;
        }
        static Transform Child(Transform parent,string name)
        {
            foreach(Transform t in parent)if(t.name==name)return t;
            return null;
        }
        static string V(Vector3 v) { return "("+v.x.ToString("+0.00;-0.00;0")+", "+v.y.ToString("+0.00;-0.00;0")+", "+v.z.ToString("+0.00;-0.00;0")+")"; }
    }
}
