using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Board brush rig and conditioner alignment, isolated validation project only.
    /// SetUp: measure the user's conditioner, add the brush rig (no visible move), sweep the
    /// Edit Mode preview, prove saves stow the brush, apply a test geometry with Undo, save.
    /// Verify (fresh process): persistence, then Play: no snapping, visible brush on the
    /// simulated contact through deployment and retraction, links attached, conditioner state.
    /// </summary>
    [InitializeOnLoad]
    public static class IceBrushConditionerValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        const string ResultsPath=Root+"Validation~/BrushConditioner-results.txt";
        const string Output="BrushConditioner/";
        const string ExpectPath=Root+"Validation~/"+Output+"expected.json";
        static readonly float[] Sweep={0,.25f,.5f,.75f,1};

        // A geometry that clearly differs from both the default and the imported pose.
        static IceBoardBrushRig.Mechanism TestMechanism(float radius)
        { return new IceBoardBrushRig.Mechanism{PivotX=-.75f,PivotZ=1.16f,ArmLength=.45f,Radius=radius,DeployedDegrees=5,RetractedDegrees=80}; }

        [Serializable] sealed class Expected
        {
            public IceBoardBrushRig.Mechanism mechanism; public float pivotHeight;
            public Vector3 armPosition, headPosition; public Quaternion armRotation;
            public string prefabHash, sceneHash;
        }

        public static void SetUp()
        {
            Run(() =>
            {
                RequireIsolated();Start("Set up (copy of the user's current authored scene)");
                var rig=OpenRig();var w=rig.Collect();
                var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();var config=game.calibration;
                string prefabHash=Hash(IceMachineBaker.PrefabPath);

                Start("Conditioner alignment (as authored)");
                var report=IceConditionerAlignment.Measure(w,config);
                foreach(string line in report.Lines)Info(line);
                foreach(var anchor in IceMachineRig.MechanicalAnchors(w,config))
                {
                    float error=Vector3.Distance(anchor.Actual,anchor.Expected);
                    Info("anchor "+anchor.Label+": "+(error<.001f?"matches config":(error*1000).ToString("F1")+" mm from config, actual "+V(anchor.Actual)+" expected "+V(anchor.Expected)));
                }
                CaptureConditioner("Authored",w);
                Undo.IncrementCurrentGroup();
                float moved=IceConditionerAlignment.SeatBlade(w,config);
                Check(Mathf.Abs(IceConditionerAlignment.Measure(w,config).BladeAboveIce)<.0002f,"Seat Blade Edge on Ice puts the blade edge on the ice ("+(moved*1000).ToString("+0.0;-0.0")+" mm)");
                Undo.PerformUndo();
                Check(Mathf.Abs(IceConditionerAlignment.Measure(w,config).BladeAboveIce-report.BladeAboveIce)<.00001f,"Undo restores the authored blade (the copy keeps the user's state)");

                Start("Board brush rig: add and import");
                Transform arm=w.BrushArm,machine=w.Machine;
                Check(arm.GetComponent<IceBoardBrushRig>()==null,"no rig yet on the user's brush arm");
                Vector3 armBefore=machine.InverseTransformPoint(arm.position),headBefore=machine.InverseTransformPoint(w.Brush.position);
                Quaternion armRotBefore=Quaternion.Inverse(machine.rotation)*arm.rotation;
                var brush=IceBoardBrushRigEditor.AddRig(w,config,out string summary);
                Info(summary);
                // The saved rest pose is stowed (as Play starts). An arm authored in its working pose
                // is shown stowed; the 100% preview must then reproduce the authored pose exactly.
                bool importedWorking=summary.Contains("working angle");
                if(importedWorking)IceBoardBrushRigEditor.SetPreview(brush,1);
                Check(Vector3.Distance(machine.InverseTransformPoint(arm.position),armBefore)<.0001f&&
                    Quaternion.Angle(Quaternion.Inverse(machine.rotation)*arm.rotation,armRotBefore)<.05f,"adding the rig keeps the arm exactly where it was authored");
                Check(Vector3.Distance(machine.InverseTransformPoint(w.Brush.position),headBefore)<.001f,
                    "adding the rig keeps the brush head where it was authored ("+(Vector3.Distance(machine.InverseTransformPoint(w.Brush.position),headBefore)*1000).ToString("F2")+" mm)"+
                    (importedWorking?" (authored working pose, checked at 100% preview)":""));
                if(importedWorking)IceBoardBrushRigEditor.SetPreview(brush,0);
                Check(brush.nonSpinning.Length==2&&Array.TrueForAll(brush.nonSpinning,t=>t.GetComponent<IceKeepUnbatched>()!=null),"motor housing and guard plate are held still and kept out of runtime batching");
                foreach(var line in IceBoardBrushRigEditor.Diagnose(brush,config,brush.mechanism))Info("imported geometry: "+line.Value);
                PreviewSweep(brush,brush.mechanism,"Imported");

                Start("Preview is never saved");
                IceBoardBrushRigEditor.SetPreview(brush,.75f);
                Check(Vector3.Distance(machine.InverseTransformPoint(w.Brush.position),Expect(brush,brush.mechanism,.75f))<.0001f,"75% preview poses the head on the articulated contact");
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Check(IceBoardBrushRigEditor.PreviewDeployment(brush)==0&&Vector3.Distance(machine.InverseTransformPoint(w.Brush.position),Expect(brush,brush.mechanism,0))<.0001f,
                    "saving the scene during a preview returns the brush to its stowed pose first");

                Start("Authoring a new articulation and applying it to the simulation");
                Undo.IncrementCurrentGroup();
                var before=IceBoardBrushRig.Mechanism.From(config);
                Undo.RecordObject(brush,"Test brush geometry");
                brush.mechanism=TestMechanism(IceBoardBrushRigEditor.VisualRadius(brush));brush.pivotHeight=.40f;
                IceBoardBrushRigEditor.Repose(brush);
                foreach(var line in IceBoardBrushRigEditor.Diagnose(brush,config,brush.mechanism))
                { Info("test geometry: "+line.Value); Check(line.Key!=MessageType.Error,"test geometry passes the reach checks"); }
                PreviewSweep(brush,brush.mechanism,"Test");
                Check(!IceBoardBrushRig.Mechanism.From(config).Matches(brush.mechanism),"the draft is not physical until applied (simulation still "+string.Join(", ",IceBoardBrushRigEditor.Differences(brush.mechanism,before))+")");
                var invalid=brush.mechanism;
                Undo.IncrementCurrentGroup();
                brush.mechanism.DeployedDegrees=90;brush.mechanism.RetractedDegrees=60;
                Check(!IceBoardBrushRigEditor.ApplyToSimulation(brush,game,out string rejected)&&IceBoardBrushRig.Mechanism.From(config).Matches(before),"an invalid geometry is rejected and the calibration is untouched ("+rejected+")");
                brush.mechanism=invalid;
                Undo.IncrementCurrentGroup();
                Check(IceBoardBrushRigEditor.ApplyToSimulation(brush,game,out _)&&IceBoardBrushRig.Mechanism.From(config).Matches(brush.mechanism),"Apply Brush Geometry to Simulation writes all six fields to the scene calibration");
                config.Validate();
                Undo.PerformUndo();
                Check(IceBoardBrushRig.Mechanism.From(game.calibration).Matches(before),"Undo restores the previous calibration");
                Undo.PerformRedo();
                Check(IceBoardBrushRig.Mechanism.From(game.calibration).Matches(brush.mechanism),"Redo re-applies it");
                IceBoardBrushRigEditor.SetPreview(brush,0);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Check(Hash(IceMachineBaker.PrefabPath)==prefabHash,"the prefab asset is untouched (everything above lives in the scene)");
                var expected=new Expected{mechanism=brush.mechanism,pivotHeight=brush.pivotHeight,armPosition=machine.InverseTransformPoint(arm.position),
                    armRotation=Quaternion.Inverse(machine.rotation)*arm.rotation,headPosition=machine.InverseTransformPoint(w.Brush.position),
                    prefabHash=prefabHash,sceneHash=Hash(IceMachineBaker.AuthoredScenePath)};
                File.WriteAllText(ExpectPath,JsonUtility.ToJson(expected,true));
                Exit(0);
            });
        }

        static Vector3 Expect(IceBoardBrushRig rig,IceBoardBrushRig.Mechanism m,float d)
        {
            var p=IceBoardBrushRig.ContactLocal(m,d);p.y=rig.HeadHeight(d);return p;
        }

        static void PreviewSweep(IceBoardBrushRig rig,IceBoardBrushRig.Mechanism m,string label)
        {
            foreach(float d in Sweep)
            {
                IceBoardBrushRigEditor.SetPreview(rig,d);
                Articulated(rig,m,d,"Edit Mode "+label+" "+(d*100).ToString("F0")+"%");
                CaptureBrush(label+"-"+(d*100).ToString("F0"),rig);
            }
            IceBoardBrushRigEditor.SetPreview(rig,0);
        }

        /// <summary>Arm angle, head on the contact, link ends on the arm and head, roller on the head.</summary>
        static void Articulated(IceBoardBrushRig rig,IceBoardBrushRig.Mechanism m,float d,string label,Vector3? contactWorld=null)
        {
            Transform machine=rig.machine;
            float yaw=-Mathf.DeltaAngle(0,(Quaternion.Inverse(machine.rotation)*rig.transform.rotation).eulerAngles.y);
            float degrees=BoardBrushArticulation.ArmDegrees(m.DeployedDegrees,m.RetractedDegrees,d);
            Vector3 head=machine.InverseTransformPoint(rig.head.position);
            Vector3 contact=contactWorld.HasValue?machine.InverseTransformPoint(contactWorld.Value):IceBoardBrushRig.ContactLocal(m,d);
            Vector3 from=rig.transform.TransformPoint(rig.linkPivotOffset),to=rig.head.position+machine.rotation*rig.linkHeadOffset;
            Vector3 half=rig.link.up*rig.link.lossyScale.y;
            float linkError=Mathf.Min(Vector3.Distance(rig.link.position-half,from)+Vector3.Distance(rig.link.position+half,to),
                Vector3.Distance(rig.link.position+half,from)+Vector3.Distance(rig.link.position-half,to));
            float rollerError=Vector3.Distance(rig.roller.position,rig.head.position+machine.rotation*rig.rollerOffset);
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw,degrees))<.01f&&new Vector2(head.x-contact.x,head.z-contact.z).magnitude<.0001f&&
                Mathf.Abs(head.y-rig.HeadHeight(d))<.0001f&&linkError<.001f&&rollerError<.0001f,
                label+": arm "+yaw.ToString("F2")+" deg, head on contact, link attached ("+(linkError*1000).ToString("F2")+" mm), roller attached");
        }

        // ---- Fresh process -------------------------------------------------------------

        public static void Verify()
        {
            Run(() =>
            {
                RequireIsolated();Start("Verify (fresh process)");
                var expected=JsonUtility.FromJson<Expected>(File.ReadAllText(ExpectPath));
                var rig=OpenRig();var w=rig.Collect();
                var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
                var brush=w.BrushArm.GetComponent<IceBoardBrushRig>();
                Check(brush!=null&&brush.IsComplete,"the brush rig survived a Unity restart with all parts bound");
                Check(brush.mechanism.Matches(expected.mechanism,.00001f)&&Mathf.Abs(brush.pivotHeight-expected.pivotHeight)<.00001f,"rig settings persisted");
                Check(IceBoardBrushRig.Mechanism.From(game.calibration).Matches(expected.mechanism,.00001f),"applied calibration persisted (pivot, length, radius, working and stowed angles)");
                Transform machine=w.Machine;
                Check(Vector3.Distance(machine.InverseTransformPoint(brush.transform.position),expected.armPosition)<.0001f&&
                    Vector3.Distance(machine.InverseTransformPoint(brush.head.position),expected.headPosition)<.0001f,"saved Edit Mode pose is the stowed pose");
                Check(Hash(IceMachineBaker.PrefabPath)==expected.prefabHash,"prefab asset unchanged");
                Check(!EditorSceneManager.GetActiveScene().isDirty,"opening the scene does not dirty it");
                SessionState.SetString(StageKey,"play");frame=0;
                EditorApplication.update-=Tick;EditorApplication.update+=Tick;
                EditorApplication.EnterPlaymode();
            });
        }

        const string StageKey="Aurora.BrushConditioner.Stage";
        static int frame;
        static IceBrushConditionerValidation()
        {
            if(Application.isBatchMode&&SessionState.GetString(StageKey,"")!="")
            { EditorApplication.update-=Tick;EditorApplication.update+=Tick; }
        }

        static void Tick()
        {
            string stage=SessionState.GetString(StageKey,"");
            Run(() =>
            {
                if(stage=="play")
                {
                    if(!EditorApplication.isPlaying||++frame<30)return;
                    var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
                    if(game==null||game.World==null)return;
                    PlayChecks(game);
                    SessionState.SetString(StageKey,"exit");
                    EditorApplication.ExitPlaymode();
                }
                else if(stage=="exit"&&!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
                    var expected=JsonUtility.FromJson<Expected>(File.ReadAllText(ExpectPath));
                    Check(Hash(IceMachineBaker.PrefabPath)==expected.prefabHash&&Hash(IceMachineBaker.AuthoredScenePath)==expected.sceneHash,"prefab and scene files unchanged by Play");
                    Exit(0);
                }
            });
        }

        static void PlayChecks(IceResurfacerGame game)
        {
            Start("Play (authored scene)");
            var expected=JsonUtility.FromJson<Expected>(File.ReadAllText(ExpectPath));
            var w=game.World;var s=game.Sim;var config=s.Config;
            var brush=w.BrushArm.GetComponent<IceBoardBrushRig>();
            Check(brush!=null,"Play uses the authored brush rig");
            var render=typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic);
            bool enabled=game.enabled;game.enabled=false;
            void Render(float dt){render.Invoke(game,new object[]{dt});}
            try
            {
                Transform machine=w.Machine;
                Render(0);
                Check(s.BrushDeployment==0&&Vector3.Distance(machine.InverseTransformPoint(brush.transform.position),expected.armPosition)<.0005f&&
                    Quaternion.Angle(Quaternion.Inverse(machine.rotation)*brush.transform.rotation,expected.armRotation)<.05f&&
                    Vector3.Distance(machine.InverseTransformPoint(brush.head.position),expected.headPosition)<.0005f,"no snapping: Play starts exactly at the authored stowed pose");

                // Drive along the left boards with the brush on, then retract.
                float x=-config.RinkWidth*.5f+config.VehicleHalfWidth+.065f,z=-8;
                s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=0;s.Vehicle.Speed=0;s.Vehicle.SteeringAngle=0;
                s.Conditioner.Heading=0;s.Conditioner.X=x;s.Conditioner.Z=z-(config.HitchBehindAxle+config.DrawbarLength);
                s.Controls.Brake=false;s.Controls.Steer=0;s.Controls.Throttle=.35f;s.Controls.BoardBrush=true;
                var still=new Quaternion[brush.nonSpinning.Length];
                for(int i=0;i<still.Length;i++)still[i]=Quaternion.Inverse(brush.transform.rotation)*brush.nonSpinning[i].rotation;
                Quaternion headStart=Quaternion.Inverse(brush.transform.rotation)*brush.head.rotation;
                Render(0);
                float lastDeployment=0,maxJump=0,maxError=0,stillError=0;Vector3 last=brush.head.position;bool reversed=false;
                int swept=0,checkedCells=0;bool contact=false;
                for(int step=0;step<250;step++)
                {
                    Vector3 under=brush.head.position;float snowBefore=SnowAt(s,under);
                    s.Step(.02f);Render(.02f);
                    var contactWorld=new Vector3(s.BrushWorldX,0,s.BrushWorldZ);
                    maxError=Mathf.Max(maxError,new Vector2(brush.head.position.x-s.BrushWorldX,brush.head.position.z-s.BrushWorldZ).magnitude);
                    Vector3 local=machine.InverseTransformPoint(brush.head.position);
                    if(step%25==0)Articulated(brush,IceBoardBrushRig.Mechanism.From(config),s.BrushDeployment,"Play deploying step "+step+" ("+(s.BrushDeployment*100).ToString("F0")+"%)",contactWorld);
                    reversed|=s.BrushDeployment<lastDeployment-1e-6f;
                    lastDeployment=s.BrushDeployment;
                    if(step>0)maxJump=Mathf.Max(maxJump,Vector3.Distance(brush.head.position,last)-s.Vehicle.Speed*.02f);last=brush.head.position;
                    for(int i=0;i<still.Length;i++)stillError=Mathf.Max(stillError,Quaternion.Angle(Quaternion.Inverse(brush.transform.rotation)*brush.nonSpinning[i].rotation,still[i]));
                    contact|=s.BrushContact;
                    if(s.BrushContact&&snowBefore>0){checkedCells++;if(SnowAt(s,under)<snowBefore)swept++;}
                }
                Check(s.BrushDeployment>.999f&&!reversed,"brush fully deploys without ever reversing");
                Check(maxError<.0001f,"visible brush stays on the simulated contact (max "+(maxError*1000).ToString("F3")+" mm)");
                Check(maxJump<.05f,"articulation is smooth (largest step beyond vehicle travel "+(maxJump*1000).ToString("F1")+" mm per 20 ms)");
                Check(stillError<.1f,"motor housing and guard plate hold still while the bristles spin ("+stillError.ToString("F3")+" deg)");
                Check(Quaternion.Angle(Quaternion.Inverse(brush.transform.rotation)*brush.head.rotation,headStart)>1,"bristles spin");
                Check(contact&&s.BrushSweptSnowM3>0,"working brush reaches the kickplate and sweeps snow ("+(s.BrushSweptSnowM3*1000).ToString("F2")+" L)");
                Check(checkedCells>0&&swept>=checkedCells*0.9f,"snow is cleared under the visible brush ("+swept+"/"+checkedCells+" frames)");
                float board=s.Ice.BoardDistance(s.BrushWorldX,s.BrushWorldZ,out _,out _);
                Info("working contact: disc centre "+(-board).ToString("F3")+" m from the boards (radius "+config.BrushRadius.ToString("F3")+")");
                CapturePlay("Play-Brush-Working",w,machine.TransformPoint(-1.0f,.3f,1.0f));
                s.Controls.BoardBrush=false;
                for(int step=0;step<150;step++)
                {
                    s.Step(.02f);Render(.02f);
                    if(step%25==0)Articulated(brush,IceBoardBrushRig.Mechanism.From(config),s.BrushDeployment,"Play retracting step "+step+" ("+(s.BrushDeployment*100).ToString("F0")+"%)",new Vector3(s.BrushWorldX,0,s.BrushWorldZ));
                }
                Check(s.BrushDeployment<.001f&&Vector3.Distance(machine.InverseTransformPoint(brush.transform.position),expected.armPosition)<.0005f&&
                    Quaternion.Angle(Quaternion.Inverse(machine.rotation)*brush.transform.rotation,expected.armRotation)<.05f,"retracts to the authored stowed pose");

                Start("Conditioner in Play");
                s.Controls.Throttle=0;s.Vehicle.Speed=0;
                float simZ=-(config.HitchBehindAxle+config.DrawbarLength);
                foreach(float lift in new[]{0f,1f})
                {
                    s.Conditioner.Lift=lift;Render(0);
                    Vector3 root=machine.InverseTransformPoint(w.Conditioner.position);
                    Check(Mathf.Abs(root.z-simZ)<.0005f&&Mathf.Abs(root.y-lift*IceConditionerAlignment.LiftTravel)<.0005f,
                        "lift "+lift+": the visible conditioner is at the simulated hitch (z "+root.z.ToString("F3")+", y "+root.y.ToString("F3")+"); an Edit Mode offset does not survive Play");
                    for(int i=0;i<2;i++)
                    {
                        Vector3 tip=w.ConditionerCylinderRods[i].position+w.ConditionerCylinderRods[i].up*w.ConditionerCylinderRods[i].lossyScale.y;
                        float error=Vector3.Distance(tip,w.ConditionerCylinderClevis[i].position);
                        Check(error<.001f,"lift "+lift+": lift cylinder "+i+" rod ends on its clevis ("+(error*1000).ToString("F2")+" mm), length "+
                            Vector3.Distance(w.ConditionerCylinderBase[i].position,w.ConditionerCylinderClevis[i].position).ToString("F3")+" m");
                    }
                    var drawbar=(Transform)typeof(IceResurfacerGame).GetField("drawbar",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
                    Vector3 a=machine.TransformPoint(0,.52f,-config.HitchBehindAxle),b=w.Conditioner.TransformPoint(0,.37f,.26f),half=drawbar.up*drawbar.localScale.y;
                    Check(Vector3.Distance(drawbar.position-half,a)<.001f&&Vector3.Distance(drawbar.position+half,b)<.001f,"lift "+lift+": drawbar spans both hitch points ("+(2*drawbar.localScale.y).ToString("F3")+" m)");
                    var blade=w.CuttingBlade.GetComponent<Renderer>().bounds;
                    if(lift==0)Info("lowered, depth "+s.ActualBladeDepthMm.ToString("F2")+" mm: visible blade edge "+((blade.min.y-IceConditionerAlignment.IceTop)*1000).ToString("F1")+" mm above the ice top in Play");
                    CapturePlay("Play-Conditioner-Lift"+lift,w,machine.TransformPoint(0,.4f,simZ));
                }
            }
            finally { game.NewSession(true);game.enabled=enabled; }
        }

        static float SnowAt(Simulation s,Vector3 p)
        {
            var ice=s.Ice;
            int col=(int)Math.Floor((p.x-ice.MinX)/ice.CellSize),row=(int)Math.Floor((p.z-ice.MinZ)/ice.CellSize);
            if(col<0||row<0||col>=ice.Columns||row>=ice.Rows)return 0;
            return ice.Cells[ice.Index(col,row)].SnowMm;
        }

        // ---- Screenshots -----------------------------------------------------------------

        static void CaptureBrush(string label,IceBoardBrushRig rig)
        {
            Transform machine=rig.machine;
            Vector3 centre=machine.TransformPoint(rig.mechanism.PivotX-.25f,.2f,rig.mechanism.PivotZ-.2f);
            Shoot(label+"-Top",machine,centre+machine.up*4,Quaternion.LookRotation(-machine.up,machine.forward),true,.9f);
            Shoot(label+"-Side",machine,centre-machine.right*5+machine.up*.3f,Quaternion.LookRotation(machine.right,machine.up),true,.7f);
        }

        static void CaptureConditioner(string label,IceWorld w)
        {
            Transform machine=w.Machine;
            Vector3 centre=machine.TransformPoint(0,.6f,-.7f);
            Shoot("Conditioner-"+label+"-Side",machine,centre-machine.right*6,Quaternion.LookRotation(machine.right,machine.up),true,1.1f);
        }

        static void CapturePlay(string label,IceWorld w,Vector3 focus)
        {
            Transform machine=w.Machine;
            Shoot(label+"-Side",machine,focus-machine.right*6+machine.up*.2f,Quaternion.LookRotation(machine.right,machine.up),true,1.0f,false);
            Shoot(label+"-Top",machine,focus+machine.up*5,Quaternion.LookRotation(-machine.up,machine.forward),true,1.4f,false);
        }

        static void Shoot(string label,Transform machine,Vector3 position,Quaternion rotation,bool ortho,float size,bool hideArena=true)
        {
            var arena=UnityEngine.Object.FindAnyObjectByType<IceArenaRig>(FindObjectsInactive.Include);
            bool arenaActive=arena!=null&&arena.gameObject.activeSelf;
            if(hideArena&&arena!=null)arena.gameObject.SetActive(false);
            var node=new GameObject("Validation capture camera"){hideFlags=HideFlags.HideAndDontSave};
            var lightNode=new GameObject("Validation capture light"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var light=lightNode.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;
                lightNode.transform.rotation=machine.rotation*Quaternion.Euler(50,70,0);
                var camera=node.AddComponent<Camera>();camera.nearClipPlane=.02f;camera.farClipPlane=60;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.42f,.45f,.5f);
                camera.orthographic=ortho;camera.orthographicSize=size;
                node.transform.SetPositionAndRotation(position,rotation);
                Directory.CreateDirectory(Root+"Validation~/"+Output);
                for(int i=0;i<2;i++)IceResurfacerEditor.Capture(camera,Output+"warm-up.png",320,180);
                File.Delete(Root+"Validation~/"+Output+"warm-up.png");
                IceResurfacerEditor.Capture(camera,Output+label+".png",1200,800);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(node);UnityEngine.Object.DestroyImmediate(lightNode);
                if(arena!=null)arena.gameObject.SetActive(arenaActive);
            }
        }

        // ---- Helpers ---------------------------------------------------------------------

        static IceMachineRig OpenRig()
        {
            EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
            var rig=UnityEngine.Object.FindAnyObjectByType<IceMachineRig>(FindObjectsInactive.Include);
            if(rig==null)throw new InvalidOperationException("No authored machine in "+IceMachineBaker.AuthoredScenePath);
            return rig;
        }
        static string V(Vector3 v) { return "("+v.x.ToString("F3")+","+v.y.ToString("F3")+","+v.z.ToString("F3")+")"; }
        static string Hash(string path)
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        static void RequireIsolated()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))
                throw new InvalidOperationException("Brush/conditioner validation writes assets; run it only in the isolated StageUnityValidation.ps1 project.");
        }
        static void Start(string step) { Directory.CreateDirectory(Root+"Validation~");File.AppendAllText(ResultsPath,"== "+step+" / "+DateTime.Now.ToString("s")+"\n"); }
        static void Pass(string message) { File.AppendAllText(ResultsPath,"PASS "+message+"\n"); }
        static void Info(string message) { File.AppendAllText(ResultsPath,"INFO "+message+"\n"); }
        static void Check(bool valid,string message)
        {
            if(!valid)throw new InvalidOperationException("Brush/conditioner: "+message);
            Pass(message);
        }
        static void Run(Action body)
        {
            try { body(); }
            catch(Exception e)
            {
                Debug.LogException(e);SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
                try { File.AppendAllText(ResultsPath,"FAIL "+e.Message+"\n"); } catch { }
                Exit(1);
            }
        }
        static void Exit(int code) { Debug.Log("AURORA BRUSH CONDITIONER VALIDATION exit="+code);EditorApplication.Exit(code); }
    }
}
