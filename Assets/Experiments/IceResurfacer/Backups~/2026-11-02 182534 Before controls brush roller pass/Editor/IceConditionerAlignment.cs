using System;
using System.Collections.Generic;
using System.IO;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Measures the authored conditioner against the simulated one. At runtime the conditioner
    /// root is placed every frame at the simulated hitch position (HitchBehindAxle+DrawbarLength
    /// behind the rear axle, height Lift*0.34), so an Edit Mode move of that root never reaches
    /// Play. Everything is measured in machine space with the machine heading straight.
    /// </summary>
    public static class IceConditionerAlignment
    {
        public const float LiftTravel=.34f, IceTop=.005f;
        static readonly Vector3 HitchOnMachine=new Vector3(0,.52f,0), HitchOnConditioner=new Vector3(0,.37f,.26f);

        public sealed class Report
        {
            public Vector3 SimulatedOrigin, AuthoredOrigin, PrefabOrigin, Offset;
            public float EditLift, AuthoredYaw;
            public bool RootOverridden, HasPrefabSource;
            public Bounds Blade, Towel, Housing, Assembly, Pickup;
            public float BladeAboveIce, BladeAboveIceAtMaxDepth, TowelAboveIce, HousingAboveIce;
            public float CutFront, CutBack, FloodFront, FloodBack, PickupFront, PickupBack, OutletZ;
            public float TireRearZ, TireInnerX, TireOuterX, TireTopY, HousingTireGapSim, HousingTireGapAuthored, AssemblyTireGapSim, AssemblyTireGapAuthored;
            public string AssemblyCulprit="";
            public float[] CylinderLowered=new float[2], CylinderRaised=new float[2], CylinderAuthored=new float[2];
            public float CylinderBarrel=.42f;
            public float DrawbarLowered, DrawbarRaised, DrawbarAuthored;
            public float HitchTotal, RequiredHitchTotal, SuggestedDrawbar;
            public readonly List<string> Lines=new List<string>();
        }

        public static Vector3 SimulatedOrigin(SimulatorConfig c,float lift)
        {
            return new Vector3(0,lift*LiftTravel,-(c.HitchBehindAxle+c.DrawbarLength));
        }

        /// <summary>Renderer bounds of a subtree in the given frame (mesh-bounds corners).</summary>
        public static Bounds LocalBounds(IEnumerable<Renderer> renderers,Transform frame)
        {
            bool any=false; var result=new Bounds();
            foreach(var renderer in renderers)
            {
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter==null||filter.sharedMesh==null||!renderer.gameObject.activeInHierarchy)continue;
                var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 p=frame.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if(!any){result=new Bounds(p,Vector3.zero);any=true;}else result.Encapsulate(p);
                }
            }
            return result;
        }

        static Bounds Of(Transform part,Transform frame)
        {
            return part==null?new Bounds():LocalBounds(part.GetComponentsInChildren<Renderer>(true),frame);
        }

        static Transform Find(Transform root,string name)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;
            return null;
        }

        public static Report Measure(IceWorld w,SimulatorConfig c)
        {
            var r=new Report();
            Transform machine=w.Machine,cond=w.Conditioner;
            r.AuthoredOrigin=machine.InverseTransformPoint(cond.position);
            r.AuthoredYaw=Mathf.DeltaAngle(0,(Quaternion.Inverse(machine.rotation)*cond.rotation).eulerAngles.y);
            r.EditLift=Mathf.Clamp01(r.AuthoredOrigin.y/LiftTravel);
            r.SimulatedOrigin=SimulatedOrigin(c,r.EditLift);
            r.Offset=r.AuthoredOrigin-r.SimulatedOrigin;
            r.HitchTotal=c.HitchBehindAxle+c.DrawbarLength;
            r.RequiredHitchTotal=-r.AuthoredOrigin.z;
            r.SuggestedDrawbar=r.RequiredHitchTotal-c.HitchBehindAxle;
            var source=PrefabUtility.GetCorrespondingObjectFromSource(cond);
            if(source!=null)
            {
                r.HasPrefabSource=true; r.PrefabOrigin=source.localPosition;
                r.RootOverridden=(source.localPosition-cond.localPosition).sqrMagnitude>1e-10f;
            }

            // Parts in conditioner space; runtime places that frame at SimulatedOrigin(lift).
            r.Blade=Of(w.CuttingBlade,cond); r.Towel=Of(w.DistributionTowel,cond); r.Housing=Of(w.ConditionerHousing,cond);
            var assembly=new List<Renderer>(cond.GetComponentsInChildren<Renderer>(true));
            r.Assembly=LocalBounds(assembly,cond);
            var pickup=Find(cond,"Wash pickup squeegee contact");
            r.Pickup=Of(pickup,cond);
            float lowered=SimulatedOrigin(c,0).y;
            r.BladeAboveIce=lowered+r.Blade.min.y-IceTop;
            r.BladeAboveIceAtMaxDepth=r.BladeAboveIce-c.MaxBladeDepthMm*.001f;
            r.TowelAboveIce=lowered+r.Towel.min.y-IceTop;
            r.HousingAboveIce=lowered+r.Housing.min.y-IceTop;
            r.CutFront=c.ConditionerLength*.5f; r.CutBack=-c.ConditionerLength*.5f;
            r.FloodFront=c.FloodContactFore+c.FloodContactLength*.5f; r.FloodBack=c.FloodContactFore-c.FloodContactLength*.5f;
            r.PickupFront=c.WashPickupContactFore+c.WashPickupContactLength*.5f; r.PickupBack=c.WashPickupContactFore-c.WashPickupContactLength*.5f;
            r.OutletZ=w.WaterOutlet!=null?cond.InverseTransformPoint(w.WaterOutlet.position).z:float.NaN;

            // Rear tyres (machine space) against the conditioner front.
            r.TireRearZ=float.PositiveInfinity; r.TireInnerX=float.PositiveInfinity; r.TireOuterX=0; r.TireTopY=0;
            foreach(var wheel in w.RearWheels)
            {
                var b=Of(wheel,machine);
                r.TireRearZ=Mathf.Min(r.TireRearZ,b.min.z);
                r.TireInnerX=Mathf.Min(r.TireInnerX,Mathf.Min(Mathf.Abs(b.min.x),Mathf.Abs(b.max.x)));
                r.TireOuterX=Mathf.Max(r.TireOuterX,Mathf.Max(Mathf.Abs(b.min.x),Mathf.Abs(b.max.x))); r.TireTopY=Mathf.Max(r.TireTopY,b.max.y);
            }
            float simZ=SimulatedOrigin(c,0).z,authZ=r.AuthoredOrigin.z;
            r.HousingTireGapSim=Overlaps(r.Housing,r.TireInnerX)?r.TireRearZ-(simZ+r.Housing.max.z):float.PositiveInfinity;
            r.HousingTireGapAuthored=Overlaps(r.Housing,r.TireInnerX)?r.TireRearZ-(authZ+r.Housing.max.z):float.PositiveInfinity;
            // Per part: only pieces that share the tyres' lateral band and height can touch them.
            r.AssemblyTireGapSim=r.AssemblyTireGapAuthored=float.PositiveInfinity;
            foreach(var renderer in assembly)
            {
                var b=LocalBounds(new[]{renderer},cond);
                if(b.size==Vector3.zero||b.max.x< -r.TireOuterX||b.min.x>r.TireOuterX||(b.min.x> -r.TireInnerX&&b.max.x<r.TireInnerX))continue;
                if(b.min.y>r.TireTopY)continue;
                float gap=r.TireRearZ-(simZ+b.max.z);
                if(gap<r.AssemblyTireGapSim){r.AssemblyTireGapSim=gap;r.AssemblyCulprit=renderer.name;}
                r.AssemblyTireGapAuthored=Mathf.Min(r.AssemblyTireGapAuthored,r.TireRearZ-(authZ+b.max.z));
            }

            for(int i=0;i<2;i++)
            {
                Vector3 pin=machine.InverseTransformPoint(w.ConditionerCylinderBase[i].position);
                Vector3 clevis=cond.InverseTransformPoint(w.ConditionerCylinderClevis[i].position);
                r.CylinderLowered[i]=Vector3.Distance(pin,SimulatedOrigin(c,0)+clevis);
                r.CylinderRaised[i]=Vector3.Distance(pin,SimulatedOrigin(c,1)+clevis);
                r.CylinderAuthored[i]=Vector3.Distance(pin,machine.InverseTransformPoint(w.ConditionerCylinderClevis[i].position));
            }
            Vector3 hitchA=HitchOnMachine+new Vector3(0,0,-c.HitchBehindAxle);
            r.DrawbarLowered=Vector3.Distance(hitchA,SimulatedOrigin(c,0)+HitchOnConditioner);
            r.DrawbarRaised=Vector3.Distance(hitchA,SimulatedOrigin(c,1)+HitchOnConditioner);
            r.DrawbarAuthored=Vector3.Distance(hitchA,r.AuthoredOrigin+HitchOnConditioner);
            Describe(r,c);
            return r;
        }

        static bool Overlaps(Bounds b,float tireInnerX){ return b.max.x>tireInnerX||b.min.x< -tireInnerX; }

        static string M(float v){ return v.ToString("+0.000;-0.000")+" m"; }
        static string Mm(float v){ return (v*1000).ToString("+0;-0")+" mm"; }

        static void Describe(Report r,SimulatorConfig c)
        {
            var l=r.Lines;
            l.Add("ROOT  'Trailing conditioner / articulated hitch' (runtime-driven: placed at the simulated hitch every frame)");
            l.Add("  Authored (machine space)      x "+M(r.AuthoredOrigin.x)+"  y "+M(r.AuthoredOrigin.y)+"  z "+M(r.AuthoredOrigin.z)+"  yaw "+r.AuthoredYaw.ToString("F2")+" deg");
            l.Add("  Simulated (same lift "+(r.EditLift*100).ToString("F0")+"%)       x "+M(r.SimulatedOrigin.x)+"  y "+M(r.SimulatedOrigin.y)+"  z "+M(r.SimulatedOrigin.z));
            l.Add("  Visual minus simulation       forward "+M(r.Offset.z)+" ("+(r.Offset.z/.3048f).ToString("F2")+" ft), up "+M(r.Offset.y)+", lateral "+M(r.Offset.x));
            if(r.HasPrefabSource)l.Add("  Prefab value z "+M(r.PrefabOrigin.z)+(r.RootOverridden?"; the authored value is a scene override on the prefab instance":"; not overridden"));
            l.Add("  In Play the root snaps to the simulated position, so this offset does not survive Play Mode.");
            l.Add("BLADE  (simulated cut band: conditioner z "+M(r.CutBack)+" .. "+M(r.CutFront)+", width "+c.ConditionerWidth.ToString("F3")+" m)");
            l.Add("  Visible blade z "+M(r.Blade.min.z)+" .. "+M(r.Blade.max.z)+", width "+r.Blade.size.x.ToString("F3")+" m");
            l.Add("  Lowered, depth 0: blade edge "+Mm(r.BladeAboveIce)+" above the ice top; at max depth "+c.MaxBladeDepthMm.ToString("F1")+" mm: "+Mm(r.BladeAboveIceAtMaxDepth));
            l.Add("TOWEL  (simulated flood band z "+M(r.FloodBack)+" .. "+M(r.FloodFront)+", width "+c.WaterDistributionWidth.ToString("F3")+" m)");
            l.Add("  Visible towel z "+M(r.Towel.min.z)+" .. "+M(r.Towel.max.z)+", width "+r.Towel.size.x.ToString("F3")+" m, lowered "+Mm(r.TowelAboveIce)+" above the ice; emitter z "+M(r.OutletZ));
            l.Add("PICKUP (simulated wash pickup z "+M(r.PickupBack)+" .. "+M(r.PickupFront)+"); visible squeegee z "+M(r.Pickup.min.z)+" .. "+M(r.Pickup.max.z));
            l.Add("HOUSING  z "+M(r.Housing.min.z)+" .. "+M(r.Housing.max.z)+", lowered underside "+Mm(r.HousingAboveIce)+" above the ice");
            l.Add("  Rear tyre back edge z "+M(r.TireRearZ)+" (inner x "+r.TireInnerX.ToString("F3")+"); housing gap: simulated "+Gap(r.HousingTireGapSim)+", authored "+Gap(r.HousingTireGapAuthored));
            l.Add("  Closest conditioner part beside the tyres ("+r.AssemblyCulprit+"): simulated "+Gap(r.AssemblyTireGapSim)+", authored "+Gap(r.AssemblyTireGapAuthored));
            for(int i=0;i<2;i++)
                l.Add("CYLINDER "+(i==0?"L":"R")+"  pin-to-clevis lowered "+r.CylinderLowered[i].ToString("F3")+" m, raised "+r.CylinderRaised[i].ToString("F3")+
                    " m, authored "+r.CylinderAuthored[i].ToString("F3")+" m (barrel "+r.CylinderBarrel.ToString("F2")+" m; the rod always ends on the clevis)");
            l.Add("DRAWBAR  hitch-to-hitch lowered "+r.DrawbarLowered.ToString("F3")+" m, raised "+r.DrawbarRaised.ToString("F3")+" m, authored "+r.DrawbarAuthored.ToString("F3")+" m (runtime-stretched, always connected)");
            l.Add("HITCH  simulated "+r.HitchTotal.ToString("F3")+" m behind the axle; the authored root implies "+r.RequiredHitchTotal.ToString("F3")+
                " m (DrawbarLength "+c.DrawbarLength.ToString("F3")+" -> "+r.SuggestedDrawbar.ToString("F3")+" to make it physical).");
        }

        static string Gap(float g){ return float.IsPositiveInfinity(g)?"no lateral overlap":(g>=0?g.ToString("F3")+" m clear":(-g).ToString("F3")+" m OVERLAP"); }

        // ---- Explicit actions (Undo) --------------------------------------------------------

        /// <summary>Cosmetic: lowers or raises the blade so its edge meets the ice at depth 0.
        /// The cut itself is planar in the simulation; only the drawn blade moves.</summary>
        public static float SeatBlade(IceWorld w,SimulatorConfig c)
        {
            var r=Measure(w,c);
            float delta=-r.BladeAboveIce;
            Undo.RecordObject(w.CuttingBlade,"Seat conditioner blade on ice");
            w.CuttingBlade.localPosition+=Vector3.up*delta;
            if(PrefabUtility.IsPartOfPrefabInstance(w.CuttingBlade))PrefabUtility.RecordPrefabInstancePropertyModifications(w.CuttingBlade);
            return delta;
        }

        /// <summary>Returns the Edit Mode root to the simulated position at its current lift.</summary>
        public static void SnapRootToSimulation(IceWorld w,SimulatorConfig c)
        {
            var r=Measure(w,c);
            Undo.RecordObject(w.Conditioner,"Snap conditioner to simulation");
            w.Conditioner.SetPositionAndRotation(w.Machine.TransformPoint(r.SimulatedOrigin),w.Machine.rotation);
            if(PrefabUtility.IsPartOfPrefabInstance(w.Conditioner))PrefabUtility.RecordPrefabInstancePropertyModifications(w.Conditioner);
        }

        /// <summary>Saved routes and guides whose recorded hitch matches the current calibration,
        /// i.e. that a hitch change would make incompatible.</summary>
        public static List<string> RoutesAffectedByHitch(SimulatorConfig c)
        {
            var affected=new List<string>();
            string root=RouteRecordingStorage.DefaultDirectory;
            foreach(string folder in new[]{root,Path.Combine(root,"Guides")})
            {
                if(!Directory.Exists(folder))continue;
                foreach(string path in Directory.GetFiles(folder,"*.json"))
                {
                    SimulatorConfig recorded=null;
                    try { recorded=folder==root?RouteRecordingStorage.Read(path).configuration:TrainingGuideStorage.Read(path).configuration; }
                    catch { continue; }
                    if(recorded!=null&&Mathf.Abs(recorded.HitchBehindAxle-c.HitchBehindAxle)<=.0001f&&
                        Mathf.Abs(recorded.DrawbarLength-c.DrawbarLength)<=.0001f)
                        affected.Add(Path.GetFileName(path));
                }
            }
            return affected;
        }

        /// <summary>Mechanical calibration: moves the simulated conditioner (cut, water, pickup)
        /// to the authored root by changing DrawbarLength. Validated, Undo-able, never automatic.</summary>
        public static bool ApplyHitch(IceResurfacerGame game,float drawbar,out string error)
        {
            error=null;
            var copy=JsonUtility.FromJson<SimulatorConfig>(JsonUtility.ToJson(game.calibration));
            copy.DrawbarLength=drawbar;
            try { copy.Validate(); }
            catch(Exception e) { error="The scene calibration would be invalid: "+e.Message; return false; }
            Undo.RecordObject(game,"Apply conditioner hitch calibration");
            game.calibration.DrawbarLength=drawbar;
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            return true;
        }
    }

    /// <summary>Conditioner alignment: measurements, Scene-view gizmos of the simulated
    /// footprints, and explicit cosmetic / mechanical actions.</summary>
    public sealed class IceConditionerAlignmentWindow : EditorWindow
    {
        float lift;
        bool gizmos=true;
        Vector2 scroll;

        [MenuItem("Experiments/Ice Resurfacer/Authored Scene/Conditioner Alignment...")]
        public static void Open() { GetWindow<IceConditionerAlignmentWindow>("Conditioner Alignment").Show(); }

        void OnEnable() { SceneView.duringSceneGui+=DrawScene; }
        void OnDisable() { SceneView.duringSceneGui-=DrawScene; }

        static bool Find(out IceWorld w,out SimulatorConfig c,out IceResurfacerGame game)
        {
            w=null; c=null; game=null;
            var rig=UnityEngine.Object.FindFirstObjectByType<IceMachineRig>(FindObjectsInactive.Include);
            if(rig==null)return false;
            try { w=rig.Collect(); } catch { return false; }
            game=rig.GetComponentInParent<IceResurfacerGame>();
            c=game!=null?game.calibration:(string.IsNullOrEmpty(rig.BakedConfigJson)?new SimulatorConfig():JsonUtility.FromJson<SimulatorConfig>(rig.BakedConfigJson));
            return true;
        }

        void OnGUI()
        {
            if(!Find(out var w,out var c,out var game)){EditorGUILayout.HelpBox("Open the authored scene (or the machine prefab).",MessageType.Info);return;}
            var r=IceConditionerAlignment.Measure(w,c);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            foreach(string line in r.Lines)EditorGUILayout.LabelField(line,EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
            if(Mathf.Abs(r.Offset.z)>.005f||Mathf.Abs(r.Offset.x)>.005f)
                EditorGUILayout.HelpBox("The visible conditioner is "+Mathf.Abs(r.Offset.z).ToString("F3")+" m "+(r.Offset.z>0?"forward of":"behind")+
                    " the simulated one in Edit Mode. Play places it at the simulated position, so cutting, water and pickup stay where they are drawn in Play.",MessageType.Warning);
            if(r.BladeAboveIce>.003f)EditorGUILayout.HelpBox("The blade edge floats "+(r.BladeAboveIce*1000).ToString("F0")+" mm above the ice while cutting.",MessageType.Warning);

            EditorGUILayout.Space();
            gizmos=EditorGUILayout.Toggle("Scene gizmos (simulated footprints)",gizmos);
            lift=EditorGUILayout.Slider("Gizmo lift %",lift*100,0,100)/100;
            using(new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.LabelField("Cosmetic (no effect on cutting or water)",EditorStyles.boldLabel);
                using(new EditorGUI.DisabledScope(Mathf.Abs(r.BladeAboveIce)<.0005f))
                    if(GUILayout.Button("Seat Blade Edge on Ice"))
                        Debug.Log("Conditioner blade moved "+(IceConditionerAlignment.SeatBlade(w,c)*1000).ToString("+0.0;-0.0")+" mm (cosmetic).");
                using(new EditorGUI.DisabledScope(r.Offset.magnitude<.0005f))
                    if(GUILayout.Button("Snap Visible Conditioner to Simulation Pose")&&
                        EditorUtility.DisplayDialog("Snap conditioner","Move the conditioner root back to the simulated position ("+r.Offset.z.ToString("+0.000;-0.000")+" m)? This discards your manual move (Undo is available).","Snap","Cancel"))
                        IceConditionerAlignment.SnapRootToSimulation(w,c);
                EditorGUILayout.LabelField("Mechanical calibration (changes the physics)",EditorStyles.boldLabel);
                bool collides=r.AssemblyTireGapAuthored<0;
                if(collides)EditorGUILayout.HelpBox("At the authored position the conditioner overlaps the rear tyres by "+(-r.AssemblyTireGapAuthored).ToString("F3")+
                    " m, so it cannot be made physical by changing the drawbar alone.",MessageType.Error);
                using(new EditorGUI.DisabledScope(game==null||collides||Mathf.Abs(r.Offset.z)<.005f))
                    if(GUILayout.Button("Move Simulated Conditioner to Authored Position..."))
                    {
                        var affected=IceConditionerAlignment.RoutesAffectedByHitch(c);
                        string message="DrawbarLength "+c.DrawbarLength.ToString("F3")+" -> "+r.SuggestedDrawbar.ToString("F3")+" m. The cut, water film and pickup move "+
                            r.Offset.z.ToString("+0.000;-0.000")+" m, turning behaviour of the trailing conditioner changes, and "+affected.Count+
                            " saved route(s)/guide(s) recorded with the current hitch will be reported as calibration-incompatible for GPS guidance.\n\nUndo is available.";
                        if(EditorUtility.DisplayDialog("Mechanical calibration change",message,"Apply","Cancel")&&!IceConditionerAlignment.ApplyHitch(game,r.SuggestedDrawbar,out string error))
                            EditorUtility.DisplayDialog("Not applied",error,"OK");
                    }
            }
        }

        void DrawScene(SceneView view)
        {
            if(!gizmos||!Find(out var w,out var c,out _))return;
            Transform machine=w.Machine;
            var r=IceConditionerAlignment.Measure(w,c);
            Vector3 origin=IceConditionerAlignment.SimulatedOrigin(c,lift);
            Vector3 P(float x,float y,float z)=>machine.TransformPoint(origin+new Vector3(x,y,z));
            void Band(float back,float front,float width,Color color,string label)
            {
                float h=width*.5f,y=IceConditionerAlignment.IceTop+.002f;
                Handles.DrawSolidRectangleWithOutline(new[]{P(-h,y,back),P(h,y,back),P(h,y,front),P(-h,y,front)},new Color(color.r,color.g,color.b,.15f),color);
                Handles.color=color; Handles.Label(P(h,y,(front+back)*.5f),label);
            }
            Band(r.CutBack,r.CutFront,c.ConditionerWidth,Color.cyan,"simulated cut");
            Band(r.FloodBack,r.FloodFront,c.WaterDistributionWidth,new Color(.3f,.5f,1),"simulated flood");
            Band(r.PickupBack,r.PickupFront,c.ConditionerWidth,Color.magenta,"simulated pickup");
            Handles.color=Color.yellow;
            Handles.DrawWireCube(machine.TransformPoint(origin+r.Housing.center),r.Housing.size);
            Handles.SphereHandleCap(0,machine.TransformPoint(origin),Quaternion.identity,.06f,EventType.Repaint);
            Handles.Label(machine.TransformPoint(origin)+Vector3.up*.5f,"simulated conditioner ("+(lift*100).ToString("F0")+"% lift)");
            Handles.color=new Color(1,.5f,0);
            Handles.SphereHandleCap(0,w.Conditioner.position,Quaternion.identity,.06f,EventType.Repaint);
            Handles.Label(w.Conditioner.position+Vector3.up*.65f,"visible root, offset "+r.Offset.z.ToString("+0.000;-0.000")+" m");
            Handles.color=Color.white;
            for(int i=0;i<2;i++)
            {
                Vector3 clevis=w.Conditioner.InverseTransformPoint(w.ConditionerCylinderClevis[i].position);
                Handles.DrawLine(w.ConditionerCylinderBase[i].position,machine.TransformPoint(origin+clevis),2);
            }
            Handles.DrawDottedLine(machine.TransformPoint(0,.52f,-c.HitchBehindAxle),machine.TransformPoint(origin+new Vector3(0,.37f,.26f)),4);
        }
    }
}
