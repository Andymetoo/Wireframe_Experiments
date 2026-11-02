using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Phase 1 proof for the authored machine, run only in the isolated validation project:
    ///   Bake -> Verify (edit mode, bake equivalence, Play binding/animation, asset safety)
    ///   WriteRearrangement -> VerifyRearrangement (fresh process: persistence, no snap-back,
    ///   full mouse operation of moved controls, framing, audio; then restores the prefab).
    /// </summary>
    [InitializeOnLoad]
    public static class IceAuthoredMachineValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        const string StageKey="Aurora.AuthoredMachine.Stage", HashKey="Aurora.AuthoredMachine.Hashes";
        const string ResultsPath=Root+"Validation~/AuthoredMachine-results.txt";
        const string ExpectedPath=Root+"Validation~/AuthoredMachine-rearranged.json";
        const string CyanMaterial=IceMachineBaker.MaterialFolder+"/Aurora glacier enamel.mat";
        static readonly Vector3 BankMove=new Vector3(-.12f,0,.20f), BladeMove=new Vector3(0,.15f,-.15f);
        const float BankYaw=20, BladeTilt=15, CoverStretch=1.1f;
        static int frame;
        static Keyboard keyboard; static Mouse mouse;
        static InputSettings originalInput, validationInput;

        [Serializable] sealed class Pose { public Vector3 position; public Quaternion rotation; public Vector3 scale; }
        [Serializable] sealed class Rearrangement
        {
            public Pose bankLocal, bladeLocal, coverLocal;   // originals, for restore
            public Pose bankMachine, bladeMachine;           // expected after the move (machine space)
            public Vector3 coverScale; public Color cyanOriginal, cyanAuthored;
        }

        static IceAuthoredMachineValidation()
        {
            if(Application.isBatchMode&&SessionState.GetString(StageKey,"")!="")
            { EditorApplication.update-=Tick;EditorApplication.update+=Tick; }
        }

        // ---- Entry points (-executeMethod) ------------------------------------------------

        public static void Bake()
        {
            Run(() =>
            {
                RequireIsolated();
                Start("Bake");
                // Each validation bake starts from a fresh copy of the launch scene (isolated copy only).
                AssetDatabase.DeleteAsset(IceMachineBaker.AuthoredScenePath);
                AssetDatabase.DeleteAsset(IceMachineBaker.MachineFolder);
                IceMachineBaker.OpenOrCreateAuthoredScene();
                var game=Game();
                Check(JsonUtility.ToJson(game.calibration)==LaunchCalibration(),"experimental scene carries the launch scene's exact calibration");
                IceMachineBaker.SetUpFirstMachine(game);
                Pass("baked "+IceMachineBaker.PrefabPath+", meshes and materials");
                Exit(0);
            });
        }

        public static void Verify()
        {
            Run(() =>
            {
                RequireIsolated();
                Start("Verify");
                EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
                var game=Game();var rig=Rig(game);
                Check(JsonUtility.ToJson(game.calibration)==LaunchCalibration(),"simulation calibration identical to the launch scene");
                CheckEditMode(game,rig);
                CheckEquivalence(game,rig);
                CheckMaterialsClean("before Play");
                SessionState.SetString(HashKey,JsonUtility.ToJson(new HashList(AssetHashes())));
                Enter("verify-play");
            });
        }

        public static void WriteRearrangement()
        {
            Run(() =>
            {
                RequireIsolated();
                Start("WriteRearrangement");
                var root=PrefabUtility.LoadPrefabContents(IceMachineBaker.PrefabPath);
                try
                {
                    var w=root.GetComponent<IceMachineRig>().Collect();
                    Transform bank=w.ConditionerLever.parent, blade=w.BladeWheel.parent, cover=w.EngineCover;
                    var record=new Rearrangement { bankLocal=Local(bank), bladeLocal=Local(blade), coverLocal=Local(cover) };
                    // Large, deliberate moves of complete mounting assemblies.
                    bank.localPosition+=BankMove; bank.localRotation=Quaternion.Euler(0,BankYaw,0)*bank.localRotation;
                    blade.localPosition+=BladeMove; blade.localRotation=Quaternion.Euler(BladeTilt,0,0)*blade.localRotation;
                    cover.localScale=Vector3.Scale(cover.localScale,new Vector3(1,CoverStretch,1));
                    record.bankMachine=MachinePose(w,bank); record.bladeMachine=MachinePose(w,blade); record.coverScale=cover.localScale;
                    PrefabUtility.SaveAsPrefabAsset(root,IceMachineBaker.PrefabPath,out bool saved);
                    Check(saved,"rearranged prefab saved through the normal prefab path");
                    var cyan=AssetDatabase.LoadAssetAtPath<Material>(CyanMaterial);
                    record.cyanOriginal=cyan.GetColor("_BaseColor"); record.cyanAuthored=new Color(.70f,.20f,.55f,1);
                    cyan.SetColor("_BaseColor",record.cyanAuthored); EditorUtility.SetDirty(cyan); AssetDatabase.SaveAssets();
                    File.WriteAllText(ExpectedPath,JsonUtility.ToJson(record,true));
                    Pass("moved hydraulic bank "+BankMove+" / "+BankYaw+" deg yaw, blade handwheel "+BladeMove+" / "+BladeTilt+" deg tilt, stretched engine cover, recolored glacier enamel");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                Exit(0);
            });
        }

        public static void VerifyRearrangement()
        {
            Run(() =>
            {
                RequireIsolated();
                Start("VerifyRearrangement");
                var record=Record();
                EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
                var game=Game();var rig=Rig(game);var w=rig.Collect();
                // Fresh editor process: the values come only from disk.
                Near(MachinePose(w,w.ConditionerLever.parent),record.bankMachine,"reopened scene: hydraulic lever bank keeps its authored pose");
                Near(MachinePose(w,w.BladeWheel.parent),record.bladeMachine,"reopened scene: blade handwheel assembly keeps its authored pose");
                Check(Vector3.Distance(w.EngineCover.localScale,record.coverScale)<1e-5f,"reopened scene: engine cover keeps its Inspector scale");
                var cyan=AssetDatabase.LoadAssetAtPath<Material>(CyanMaterial);
                Check(cyan.GetColor("_BaseColor")==record.cyanAuthored,"reopened project: material color edit persisted in its asset");
                var moved=rig.RearrangedControls();
                Check(moved.Contains("Hydraulic lever bank")&&moved.Contains("Blade handwheel assembly"),"rig reports the rearranged mounts: "+string.Join(", ",moved));
                Check(IceMachineRigEditor.ControlAdvisories(w,game.calibration).Count>=0,"operator-control advisory evaluates: "+string.Join(" | ",IceMachineRigEditor.ControlAdvisories(w,game.calibration)));
                foreach(var anchor in IceMachineRig.MechanicalAnchors(w,game.calibration))
                    Check(Vector3.Distance(anchor.Actual,anchor.Expected)<.0001f,"physics anchor still matches config: "+anchor.Label);
                SessionState.SetString(HashKey,JsonUtility.ToJson(new HashList(AssetHashes())));
                Enter("rearranged-play");
            });
        }

        // ---- Edit mode -------------------------------------------------------------------

        static void CheckEditMode(IceResurfacerGame game,IceMachineRig rig)
        {
            Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rig.gameObject)==IceMachineBaker.PrefabPath,"machine is a saved instance of "+IceMachineBaker.PrefabPath);
            int machines=Count("AURORA R-54 / rear axle origin"),rigs=UnityEngine.Object.FindObjectsByType<IceMachineRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            Check(machines==1&&rigs==1,"saved scene holds exactly one machine, the prefab instance (machines="+machines+", rigs="+rigs+")");
            var renderers=rig.GetComponentsInChildren<MeshRenderer>(true);
            int enabled=0,meshes=0,builtIn=0;
            foreach(var renderer in renderers)
            {
                if(renderer.enabled)enabled++;
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter!=null&&filter.sharedMesh!=null)
                {
                    var mesh=filter.sharedMesh;
                    Check(EditorUtility.IsPersistent(mesh),"mesh is a saved asset: "+renderer.name);
                    Check(mesh.isReadable,"mesh is readable for silhouettes/validation: "+renderer.name);
                    if(AssetDatabase.GetAssetPath(mesh)==IceMachineBaker.MeshAssetPath)meshes++;else builtIn++;
                }
                foreach(var material in renderer.sharedMaterials)
                    Check(material!=null&&EditorUtility.IsPersistent(material),"material is a saved asset: "+renderer.name);
            }
            Check(enabled>100,"Edit Mode shows "+enabled+" individually selectable machine renderers ("+meshes+" baked meshes, "+builtIn+" built-in primitives)");
            foreach(var t in rig.GetComponentsInChildren<Transform>(true))
                Check((t.hideFlags&(HideFlags.DontSaveInEditor|HideFlags.HideInHierarchy))==0,"saved and visible in Hierarchy: "+t.name);
            var w=rig.Collect();
            foreach(string name in new[]{"Chassis and bodywork","Wheels","Hydraulic equipment","Compact vertical snow conveyor / rear tank inlet"})
                Check(Find(w.Machine,name)!=null&&Find(w.Machine,name).GetComponentsInChildren<MeshRenderer>().Length>0,"selectable subassembly: "+name);
            foreach(var part in new[]{w.TankPivot,w.OperatorStation,w.SteeringColumn,w.ConditionerLever.parent,w.FloodValveMount.parent,w.BladeWheel.parent,w.SnowBreakerPlunger.parent,w.BrushArm,w.Conditioner})
                Check(part.GetComponentsInChildren<MeshRenderer>().Length>0,"selectable subassembly: "+part.name);
            foreach(var anchor in IceMachineRig.MechanicalAnchors(w,game.calibration))
                Check(Vector3.Distance(anchor.Actual,anchor.Expected)<.0001f,"physics anchor matches config: "+anchor.Label);
            foreach(var width in IceMachineRigEditor.Widths(w,game.calibration))
                Check(Mathf.Abs(width.Value)<.0001f,width.Key+" matches the simulated footprint");
            Check(rig.RearrangedControls().Count==0,"fresh bake reports no rearranged controls");
        }

        static void CheckEquivalence(IceResurfacerGame game,IceMachineRig rig)
        {
            var config=JsonUtility.FromJson<SimulatorConfig>(rig.BakedConfigJson);
            var container=new GameObject("Validation only / generated reference machine");
            container.hideFlags=HideFlags.DontSave;
            IceWorld reference=null;
            try
            {
                reference=IceWorldBuilder.BuildMachinePreview(container.transform,game.litTemplate,config);
                reference.Conditioner.localPosition=new Vector3(0,.34f,-config.HitchBehindAxle-config.DrawbarLength);
                IcePhysicalControls.PoseCylinders(reference);
                var authored=rig.Collect();
                int compared=0;
                foreach(var field in IceMachineRig.MachineTransformFields())
                { Same(field.Name,(Transform)field.GetValue(reference),(Transform)field.GetValue(authored),reference,authored);compared++; }
                foreach(var field in IceMachineRig.MachineTransformArrayFields())
                {
                    var r=(Transform[])field.GetValue(reference);var a=(Transform[])field.GetValue(authored);
                    Check(r.Length==a.Length,"same element count: "+field.Name);
                    for(int i=0;i<r.Length;i++){Same(field.Name+"["+i+"]",r[i],a[i],reference,authored);compared++;}
                }
                Pass("bake equivalence: "+compared+" bound transforms match the generated machine (<=0.1 mm, <=0.01 deg, scale)");
                var expected=Totals(container.transform);var actual=Totals(rig.transform);
                foreach(var entry in expected)
                    Check(actual.TryGetValue(entry.Key,out var count)&&count==entry.Value,"same "+entry.Key+" geometry ("+entry.Value+")");
                Check(actual.Count==expected.Count,"no extra or missing materials ("+actual.Count+")");
                var assets=new Dictionary<string,Material>();
                foreach(var renderer in rig.GetComponentsInChildren<MeshRenderer>(true))
                    if(renderer.sharedMaterial!=null&&renderer.GetComponent<TextMesh>()==null)assets[renderer.sharedMaterial.name]=renderer.sharedMaterial;
                foreach(var renderer in container.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var source=renderer.sharedMaterial;
                    if(source==null||renderer.GetComponent<TextMesh>()!=null)continue;
                    string label=source.name.Replace("IceResurfacer / ","");
                    Check(assets.TryGetValue(label,out var asset),"persisted material exists: "+label);
                    var a=new HashSet<string>(asset.shaderKeywords);var r=new HashSet<string>(source.shaderKeywords);
                    Check(a.SetEquals(r)&&asset.shader==source.shader&&asset.renderQueue==source.renderQueue&&
                        asset.GetColor("_BaseColor")==source.GetColor("_BaseColor")&&asset.GetColor("_EmissionColor")==source.GetColor("_EmissionColor")&&
                        asset.GetFloat("_Metallic")==source.GetFloat("_Metallic")&&asset.GetFloat("_Smoothness")==source.GetFloat("_Smoothness"),
                        "persisted material renders like the builder's: "+label+" [keywords "+string.Join(",",a)+" vs "+string.Join(",",r)+"]");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(container);
                if(reference!=null)
                {
                    foreach(var mesh in reference.Meshes)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
                    foreach(var material in reference.Materials)if(material!=null)UnityEngine.Object.DestroyImmediate(material);
                }
            }
        }
        static void Same(string name,Transform r,Transform a,IceWorld rw,IceWorld aw)
        {
            Transform rf=Frame(r,rw),af=Frame(a,aw);
            Vector3 dp=rf.InverseTransformPoint(r.position)-af.InverseTransformPoint(a.position);
            float angle=Quaternion.Angle(Quaternion.Inverse(rf.rotation)*r.rotation,Quaternion.Inverse(af.rotation)*a.rotation);
            float scale=Vector3.Distance(r.lossyScale,a.lossyScale);
            if(dp.magnitude>.0001f||angle>.01f||scale>.0001f)
                throw new InvalidOperationException("Authored machine: "+name+" differs from generated machine (dp="+dp.magnitude+" m, angle="+angle+", scale="+scale+")");
        }
        static Transform Frame(Transform t,IceWorld w)
        {
            if(t==w.Machine||t==w.Conditioner)return t.parent;
            return t.IsChildOf(w.Conditioner)?w.Conditioner:w.Machine;
        }
        static Dictionary<string,int> Totals(Transform root)
        {
            var totals=new Dictionary<string,int>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                if(filter.sharedMesh==null||renderer==null||renderer.GetComponent<TextMesh>()!=null||renderer.sharedMaterial==null)continue;
                string key=renderer.sharedMaterial.name.Replace("IceResurfacer / ","");
                totals.TryGetValue(key,out int count);totals[key]=count+filter.sharedMesh.vertexCount;
            }
            return totals;
        }

        // ---- Play Mode -------------------------------------------------------------------

        static void Tick()
        {
            string stage=SessionState.GetString(StageKey,"");
            try
            {
                if(stage.EndsWith("-play"))
                {
                    if(!EditorApplication.isPlaying)return;
                    var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
                    if(game==null||game.Sim==null) { if(++frame>400)throw new InvalidOperationException("Authored scene did not initialize in Play Mode");return; }
                    frame++;
                    if(frame==30)AddInput();
                    if(frame<45)return;
                    if(stage=="verify-play")VerifyPlay(game);else VerifyRearrangedPlay(game);
                    RestoreInput();
                    SessionState.SetString(StageKey,stage.Replace("-play","-exit"));
                    EditorApplication.ExitPlaymode();
                }
                else if(stage.EndsWith("-exit"))
                {
                    if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                    CheckAssetsAfterPlay();
                    if(stage=="rearranged-exit")Restore();
                    SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
                    Pass("COMPLETE");Exit(0);
                }
            }
            catch(Exception e) { Fail(e); }
        }

        static void VerifyPlay(IceResurfacerGame game)
        {
            var rig=Rig(game);var w=game.World;var bound=rig.Collect();
            Check(w.Machine==bound.Machine&&w.Conditioner==bound.Conditioner,"Play binds the saved prefab instance (no regenerated machine)");
            foreach(var field in IceMachineRig.MachineTransformFields())
                Check((Transform)field.GetValue(w)==(Transform)field.GetValue(bound),"runtime IceWorld."+field.Name+" is the authored part");
            int machines=Count("AURORA R-54 / rear axle origin"),conditioners=Count("Trailing conditioner / articulated hitch");
            Check(machines==1&&conditioners==1,"exactly one machine and one conditioner exist in Play (machines="+machines+", conditioners="+conditioners+": "+Paths("Trailing conditioner / articulated hitch")+")");
            Check(UnityEngine.Object.FindObjectsByType<IceMachineRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"one authored rig");
            Check(w.IceMesh!=null&&w.ArenaLights!=null&&w.ArenaLights.Length>0&&w.PitSnow!=null,"generated arena still present around the authored machine");
            int batched=0;foreach(Transform child in w.Machine)if(child.name.StartsWith("Batched "))batched++;
            int live=0;foreach(var r in w.Machine.parent.GetComponentsInChildren<MeshRenderer>())if(r.enabled)live++;
            Check(batched>5,"runtime material batching ran on the Play instance ("+batched+" machine batches, "+live+" live machine renderers)");
            foreach(var mesh in w.Meshes)Check(mesh==null||!EditorUtility.IsPersistent(mesh),"runtime destroy list holds no persistent mesh: "+(mesh!=null?mesh.name:""));
            foreach(var material in w.Materials)Check(material==null||!EditorUtility.IsPersistent(material),"runtime destroy list holds no persistent material: "+(material!=null?material.name:""));
            // Prefab labels only; IceWaterService adds its own runtime port labels to the machine.
            int labels=0;
            foreach(var text in rig.GetComponentsInChildren<TextMesh>())
            {
                var material=text.GetComponent<MeshRenderer>().sharedMaterial;
                Check(material!=null&&!EditorUtility.IsPersistent(material),"label uses a runtime depth-tested material, not an asset: "+text.text);
                if(material==w.Lettering)labels++;
            }
            Check(labels>10,labels+" prefab labels swapped to the runtime lettering material");
            Check(game.GetComponentsInChildren<AudioSource>().Length==27,"27 audio sources, as in the launch scene");
            Animate(game);
            new MechanicalControls(game).Run("authored bake");
        }

        static void VerifyRearrangedPlay(IceResurfacerGame game)
        {
            var record=Record();var rig=Rig(game);var w=game.World;
            Check(w.Machine==rig.Collect().Machine,"Play uses the saved rearranged prefab");
            Transform bank=w.ConditionerLever.parent,blade=w.BladeWheel.parent;
            Near(MachinePose(w,bank),record.bankMachine,"Play start: hydraulic bank at authored pose");
            Near(MachinePose(w,blade),record.bladeMachine,"Play start: blade handwheel at authored pose");
            Check(Vector3.Distance(w.EngineCover.localScale,record.coverScale)<1e-5f,"Play: engine cover keeps Inspector scale");
            bool wasEnabled=game.enabled;game.enabled=false;
            try
            {
                game.NewSession(true);var s=game.Sim;var c=s.Controls;var render=Renderer(game);
                for(int i=0;i<200;i++)
                {
                    c.Throttle=i<100?.5f:-.3f;c.Steer=i%60<30?1:-1;
                    s.SetConditionerHydraulic(i%50<25?1:-1);s.SetTankHydraulic(i%80<40?1:-1);
                    c.HorizontalAuger=c.VerticalAuger=i%40<20;c.WashValve=c.FloodFlow=(i%30)/30f;c.BladeDepthMm=(i%25)/25f*s.Config.MaxBladeDepthMm;
                    s.Step(.02f);render(.02f);
                }
                s.SetConditionerHydraulic(0);s.SetTankHydraulic(0);
                Near(MachinePose(w,bank),record.bankMachine,"after 200 driven steps: hydraulic bank never snaps back");
                Near(MachinePose(w,blade),record.bladeMachine,"after 200 driven steps: blade assembly never snaps back");
            }
            finally { game.NewSession(true);game.enabled=wasEnabled; }
            new MechanicalControls(game).Run("rearranged controls");
            Near(MachinePose(w,bank),record.bankMachine,"after mouse operation: hydraulic bank still at authored pose");
            Near(MachinePose(w,blade),record.bladeMachine,"after mouse operation: blade assembly still at authored pose");
        }

        /// <summary>Real Input System mouse gestures on the authored controls, then framing and audio.</summary>
        sealed class MechanicalControls
        {
            readonly IceResurfacerGame game;
            public MechanicalControls(IceResurfacerGame owner) { game=owner; }
            public void Run(string label)
            {
                IcePhysicalValidation.Run(game,keyboard,mouse);
                Pass(label+": all 8 physical controls pick, highlight, drag in their own frames, latch, spring back and turn (IcePhysicalValidation)");
                var w=game.World;bool wasEnabled=game.enabled;game.enabled=false;
                try
                {
                    game.NewSession(true);Renderer(game)(0);
                    game.SelectCameraView("Controls");
                    var look=typeof(IceResurfacerGame).GetMethod("UpdateLook",BindingFlags.Instance|BindingFlags.NonPublic);
                    for(int i=0;i<90;i++)look.Invoke(game,new object[]{true,.02f});
                    // The preset aims at the authored grips; its fixed 61-degree lens cannot widen,
                    // so a layout wider than the lens is reported as a limit, not hidden.
                    var camera=game.DriverCamera;float minimum=180,maximum=-180,sumX=0;var outside=new List<string>();
                    var grips=IceMachineRigEditor.Grips(w,game.calibration);
                    foreach(var grip in grips)
                    {
                        Vector3 v=camera.WorldToViewportPoint(grip.Value);sumX+=v.x;
                        Vector3 local=camera.transform.InverseTransformPoint(grip.Value);
                        float bearing=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;minimum=Mathf.Min(minimum,bearing);maximum=Mathf.Max(maximum,bearing);
                        if(!(v.z>0&&v.x>0&&v.x<1&&v.y>0&&v.y<1))outside.Add(grip.Key+" "+v.ToString("F2"));
                    }
                    float lens=Camera.VerticalToHorizontalFieldOfView(camera.fieldOfView,camera.aspect),span=maximum-minimum,centre=sumX/grips.Count;
                    Check(Mathf.Abs((minimum+maximum)*.5f)<lens*.25f&&centre>.25f&&centre<.75f,
                        label+": E/Controls view aims at the authored controls (bearing span "+span.ToString("F0")+" deg centred at "+((minimum+maximum)*.5f).ToString("F1")+" deg; mean viewport x "+centre.ToString("F2")+")");
                    if(outside.Count==0)Pass(label+": E/Controls view frames all 8 grips within the "+lens.ToString("F0")+" deg lens");
                    else
                    {
                        Check(span>lens*.9f,label+": grips outside the view although the layout fits the lens: "+string.Join(", ",outside));
                        File.AppendAllText(ResultsPath,"LIMIT "+label+": grips span "+span.ToString("F0")+" deg against the fixed "+lens.ToString("F0")+
                            " deg Controls lens, aimed "+((minimum+maximum)*.5f).ToString("F1")+" deg off their centre; outside the frame: "+string.Join(", ",outside)+"\n");
                    }
                    game.SelectCameraView("Forward");
                    AudioSource detent=null;
                    foreach(var source in game.GetComponentsInChildren<AudioSource>())if(source.name=="Machine audio / Hydraulic spool detent")detent=source;
                    Check(detent!=null&&Vector3.Distance(detent.transform.position,w.ConditionerLever.parent.TransformPoint(0,.08f,0))<.0001f,label+": spool detent audio sits at the lever bank");
                    // Each lever's drag axis is its own bank's forward; the animated rotation is rest-relative.
                    var c=game.Sim.Controls;c.ConditionerHydraulic=1;game.PhysicalControls.Animate();
                    Check(Quaternion.Angle(Quaternion.Inverse(w.ConditionerLever.parent.rotation)*w.ConditionerLever.rotation,Quaternion.Euler(24,0,0))<.05f,label+": conditioner lever tilts 24 deg about its authored bank axis");
                    c.ConditionerHydraulic=0;game.PhysicalControls.Animate();
                }
                finally { game.NewSession(true);game.enabled=wasEnabled; }
            }
        }

        static void Animate(IceResurfacerGame game)
        {
            bool wasEnabled=game.enabled;game.enabled=false;
            try
            {
                game.NewSession(true);var s=game.Sim;var c=s.Controls;var w=game.World;var render=Renderer(game);
                render(0);
                var watched=new List<Transform>{w.SteeringWheel,w.FrontWheels[0],w.FrontWheels[1],w.RearWheels[0],w.RearWheels[1],w.TankPivot,w.TankDoor,
                    w.BladeWheel,w.CuttingBlade,w.Conditioner,w.BrushArm,w.BrushLink,w.GuideRoller,w.HorizontalAuger,w.VerticalAuger,
                    w.ConditionerLever,w.HorizontalAugerLever,w.VerticalAugerLever,w.DumpLever,w.FloodLever,w.WashValve,w.SnowBreakerPlunger,
                    w.AcceleratorPedal,w.BrakePedal};
                foreach(var group in new[]{w.TankCylinderBarrels,w.TankCylinderRods,w.ConditionerCylinderBarrels,w.ConditionerCylinderRods})watched.AddRange(group);
                var before=new Matrix4x4[watched.Count];
                for(int i=0;i<watched.Count;i++)before[i]=watched[i].localToWorldMatrix;
                s.Vehicle.SteeringAngle=s.Config.MaxSteeringRadians*.8f;s.Vehicle.Speed=1;
                Set(s,"TankLift",.9f);Set(s,"BrushDeployment",.4f);Set(s,"GuideRollerTravel",.37f);
                s.Conditioner.Lift=.4f;c.BladeDepthMm=s.Config.MaxBladeDepthMm*.8f;
                c.ConditionerHydraulic=1;c.TankHydraulic=-1;c.HorizontalAuger=c.VerticalAuger=true;c.WashValve=c.FloodFlow=.6f;
                c.Throttle=1;c.Brake=true;
                typeof(IceResurfacerGame).GetField("breakerAnimation",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,.45f);
                render(.1f);
                for(int i=0;i<watched.Count;i++)
                    Check(!Equal(before[i],watched[i].localToWorldMatrix),"animated in Play: "+watched[i].name);
                Pass("steering, wheels, tank and door, conditioner, blade wheel and edge, brush, guide roller, augers, levers, valves, pedals, plunger and 8 cylinder parts animate");
                for(int i=0;i<2;i++)
                {
                    CylinderEnds(w.TankCylinderBarrels[i],w.TankCylinderRods[i],w.TankCylinderBase[i],w.TankCylinderClevis[i],"tank "+i);
                    CylinderEnds(w.ConditionerCylinderBarrels[i],w.ConditionerCylinderRods[i],w.ConditionerCylinderBase[i],w.ConditionerCylinderClevis[i],"conditioner "+i);
                }
            }
            finally { game.NewSession(true);game.enabled=wasEnabled; }
        }
        static void CylinderEnds(Transform barrel,Transform rod,Transform basePin,Transform clevisPin,string label)
        {
            // Unit cylinder: local y = +-1 are the ends.
            Check(Vector3.Distance(barrel.TransformPoint(0,-1,0),basePin.position)<.001f,label+" cylinder barrel starts on its authored base clevis");
            Check(Vector3.Distance(rod.TransformPoint(0,1,0),clevisPin.position)<.001f,label+" cylinder rod ends on its authored moving clevis");
        }
        static bool Equal(Matrix4x4 a,Matrix4x4 b)
        {
            for(int i=0;i<16;i++)if(Mathf.Abs(a[i]-b[i])>1e-5f)return false;
            return true;
        }
        static void Set(Simulation s,string property,float value)
        {
            var info=typeof(Simulation).GetProperty(property);
            if(info!=null&&info.CanWrite){info.SetValue(s,value);return;}
            foreach(var part in typeof(Simulation).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            {
                var nested=part.FieldType.GetProperty(property);
                if(nested!=null&&nested.CanWrite&&part.GetValue(s)!=null){nested.SetValue(part.GetValue(s),value);return;}
            }
            throw new InvalidOperationException("Cannot set Simulation."+property);
        }

        // ---- Assets ----------------------------------------------------------------------

        [Serializable] sealed class HashList { public List<string> entries=new List<string>(); public HashList(){} public HashList(List<string> e){entries=e;} }
        static List<string> AssetHashes()
        {
            var list=new List<string>();
            using(var sha=SHA256.Create())
                foreach(var file in Directory.GetFiles(IceMachineBaker.MachineFolder,"*",SearchOption.AllDirectories))
                {
                    if(file.EndsWith(".meta"))continue;
                    list.Add(file.Replace('\\','/')+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))));
                }
            list.Sort();
            return list;
        }
        static void CheckAssetsAfterPlay()
        {
            var before=JsonUtility.FromJson<HashList>(SessionState.GetString(HashKey,"{}")).entries;
            var after=AssetHashes();
            Check(before.Count>0&&before.Count==after.Count,"same "+after.Count+" machine asset files after Play");
            for(int i=0;i<after.Count;i++)Check(before[i]==after[i],"unchanged by Play: "+after[i].Split(' ')[0]);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(IceMachineBaker.PrefabPath);
            Check(prefab!=null&&!EditorUtility.IsDirty(prefab),"prefab asset intact and not dirtied by Play");
            foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(IceMachineBaker.MeshAssetPath))
                Check(asset!=null&&!EditorUtility.IsDirty(asset),"mesh asset survives Play: "+(asset!=null?asset.name:"(destroyed)"));
            CheckMaterialsClean("after Play");
            var scene=SceneManager.GetActiveScene();
            Check(scene.path==IceMachineBaker.AuthoredScenePath&&!scene.isDirty,"experimental scene not modified by Play");
            var rig=Rig(Game());
            foreach(var filter in rig.GetComponentsInChildren<MeshFilter>(true))
                if(filter.GetComponent<MeshRenderer>()!=null&&filter.name!="Uneven snow surface / actual tank contents")
                    Check(filter.sharedMesh!=null,"Edit Mode mesh reference intact after Play: "+filter.name);
        }

        static void CheckMaterialsClean(string when)
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{IceMachineBaker.MaterialFolder}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Check(material!=null&&!EditorUtility.IsDirty(material),"material asset clean "+when+": "+(material!=null?material.name+
                    " [keywords "+string.Join(",",material.shaderKeywords)+"; GI "+material.globalIlluminationFlags+"]":guid));
            }
        }
        static void Restore()
        {
            var record=Record();
            var root=PrefabUtility.LoadPrefabContents(IceMachineBaker.PrefabPath);
            try
            {
                var w=root.GetComponent<IceMachineRig>().Collect();
                Apply(w.ConditionerLever.parent,record.bankLocal);Apply(w.BladeWheel.parent,record.bladeLocal);Apply(w.EngineCover,record.coverLocal);
                PrefabUtility.SaveAsPrefabAsset(root,IceMachineBaker.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var cyan=AssetDatabase.LoadAssetAtPath<Material>(CyanMaterial);
            cyan.SetColor("_BaseColor",record.cyanOriginal);EditorUtility.SetDirty(cyan);AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
            Check(Rig(Game()).RearrangedControls().Count==0,"restored prefab: controls back at bake poses");
        }

        // ---- Helpers ---------------------------------------------------------------------

        static Pose Local(Transform t) { return new Pose { position=t.localPosition, rotation=t.localRotation, scale=t.localScale }; }
        static void Apply(Transform t,Pose p) { t.localPosition=p.position;t.localRotation=p.rotation;t.localScale=p.scale; }
        static Pose MachinePose(IceWorld w,Transform t)
        { return new Pose { position=w.Machine.InverseTransformPoint(t.position), rotation=Quaternion.Inverse(w.Machine.rotation)*t.rotation, scale=t.lossyScale }; }
        static void Near(Pose actual,Pose expected,string message)
        {
            Check(Vector3.Distance(actual.position,expected.position)<.0001f&&Quaternion.Angle(actual.rotation,expected.rotation)<.01f,
                message+" (error "+(Vector3.Distance(actual.position,expected.position)*1000).ToString("F3")+" mm, "+Quaternion.Angle(actual.rotation,expected.rotation).ToString("F3")+" deg)");
        }
        static Rearrangement Record()
        {
            if(!File.Exists(ExpectedPath))throw new InvalidOperationException("Run WriteRearrangement first.");
            return JsonUtility.FromJson<Rearrangement>(File.ReadAllText(ExpectedPath));
        }
        static Action<float> Renderer(IceResurfacerGame game)
        { return (Action<float>)Delegate.CreateDelegate(typeof(Action<float>),game,typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic)); }
        static IceResurfacerGame Game()
        {
            var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
            if(game==null)throw new InvalidOperationException("No IceResurfacerGame in "+SceneManager.GetActiveScene().path);
            return game;
        }
        static IceMachineRig Rig(IceResurfacerGame game)
        {
            var rig=game.GetComponentInChildren<IceMachineRig>(true);
            if(rig==null)throw new InvalidOperationException("No authored machine under the game root; run Bake first.");
            return rig;
        }
        static Transform Find(Transform root,string name)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;
            return null;
        }
        static string Paths(string name)
        {
            var paths=new List<string>();
            foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(t.name==name)paths.Add((t.parent!=null?t.parent.name+"/":"")+t.name+" @"+t.gameObject.scene.name);
            return string.Join("; ",paths);
        }
        static int Count(string name)
        {
            int n=0;
            foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(t.name==name)n++;
            return n;
        }
        static string LaunchCalibration()
        {
            var active=SceneManager.GetActiveScene();
            var launch=EditorSceneManager.OpenScene(IceResurfacerEditor.LaunchScenePath,OpenSceneMode.Additive);
            try
            {
                foreach(var root in launch.GetRootGameObjects())
                {
                    var game=root.GetComponentInChildren<IceResurfacerGame>(true);
                    if(game!=null)return JsonUtility.ToJson(game.calibration);
                }
                throw new InvalidOperationException("Launch scene has no IceResurfacerGame");
            }
            finally { EditorSceneManager.CloseScene(launch,true);SceneManager.SetActiveScene(active); }
        }
        static void AddInput()
        {
            originalInput=InputSystem.settings;
            validationInput=UnityEngine.Object.Instantiate(originalInput);
            validationInput.hideFlags=HideFlags.HideAndDontSave;
            validationInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            validationInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings=validationInput;
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
        }
        static void RestoreInput()
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(originalInput!=null)InputSystem.settings=originalInput;
            if(validationInput!=null)UnityEngine.Object.Destroy(validationInput);
            keyboard=null;mouse=null;originalInput=validationInput=null;
        }
        static void RequireIsolated()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))
                throw new InvalidOperationException("Authored-machine validation writes assets; run it only in the isolated StageUnityValidation.ps1 project.");
        }
        static void Enter(string stage)
        {
            frame=0;SessionState.SetString(StageKey,stage);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            EditorApplication.EnterPlaymode();
        }
        static void Start(string step) { Directory.CreateDirectory(Root+"Validation~");File.AppendAllText(ResultsPath,"== "+step+" / "+DateTime.Now.ToString("s")+"\n"); }
        static void Pass(string message) { File.AppendAllText(ResultsPath,"PASS "+message+"\n"); }
        static void Check(bool valid,string message)
        {
            if(!valid)throw new InvalidOperationException("Authored machine: "+message);
            Pass(message);
        }
        static void Run(Action body)
        {
            try { body(); }
            catch(Exception e) { Fail(e); }
        }
        static void Fail(Exception e)
        {
            Debug.LogException(e);
            try { File.AppendAllText(ResultsPath,"FAIL "+e.Message+"\n"); } catch { }
            RestoreInput();
            SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
            Exit(1);
        }
        static void Exit(int code) { Debug.Log("AURORA AUTHORED MACHINE VALIDATION exit="+code);EditorApplication.Exit(code); }
    }
}
