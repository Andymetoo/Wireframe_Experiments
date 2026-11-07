using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Reference-proportions pass, isolated validation project only: survey the authored
    /// machine (machine-space extents of every assembly) and capture side and cockpit views.
    /// </summary>
    [InitializeOnLoad]
    public static class IceMachineProportionsValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        const string ResultsPath=Root+"Validation~/MachineProportions-results.txt";
        const string Output="MachineProportions/";

        public static void Survey()
        {
            Run(() =>
            {
                RequireIsolated();Start("Survey");
                var rig=OpenRig();
                var machine=rig.Get("Machine");
                File.WriteAllText(Root+"Validation~/"+Output.TrimEnd('/')+"-survey.txt",Tree(machine,rig.Get("Conditioner")));
                Capture("Survey",rig);
                Exit(0);
            });
        }

        /// <summary>Before views, the real menu operation (backup + prefab edit), after views.</summary>
        public static void Apply()
        {
            Run(() =>
            {
                RequireIsolated();Start("Apply (proportions pass on the copy of the user's prefab)");
                var rig=OpenRig();
                Check(rig.RearrangedControls().Count==0,"before: every control mount is at its generated pose");
                Measure("before",rig);
                Capture("Before",rig);
                string prefabHash=Hash(IceMachineBaker.PrefabPath);
                string report=IceMachineProportionPass.Apply(out string backup);
                foreach(string line in report.Split('\n'))if(line.Trim().Length>0)Info(line.TrimEnd());
                Check(Hash(IceAuthoredRecovery.BackupCopyPath(backup,IceMachineBaker.PrefabPath))==prefabHash,"backup holds the exact pre-edit prefab ("+backup+")");
                Check(File.Exists(IceAuthoredRecovery.BackupCopyPath(backup,IceMachineBaker.PrefabPath+".meta"))&&
                    File.Exists(IceAuthoredRecovery.BackupCopyPath(backup,IceMachineBaker.AuthoredScenePath)),"backup includes the prefab .meta and the authored scene");
                Check(Hash(IceMachineBaker.PrefabPath)!=prefabHash,"prefab asset was edited in place");
                Check(!report.Contains("Skipped"),"every planned change applied (nothing was already moved)");
                rig=OpenRig();
                Measure("after",rig);
                Anchors(rig);
                Clearance(rig);
                var contents=PrefabUtility.LoadPrefabContents(IceMachineBaker.PrefabPath);
                try { IceMachineProportionPass.Edit(contents,out var again,out var skipped);Check(again.Count==0,"running the pass a second time changes nothing ("+skipped.Count+" parts skipped as already moved)"); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                Capture("After",rig);
                File.WriteAllText(Root+"Validation~/"+Output+"prefab-sha256.txt",Hash(IceMachineBaker.PrefabPath));
                Exit(0);
            });
        }

        /// <summary>Fresh process: the edit persisted, anchors and clearances hold.</summary>
        public static void Verify()
        {
            Run(() =>
            {
                RequireIsolated();Start("Verify (fresh process)");
                var rig=OpenRig();
                var machine=rig.Get("Machine");
                Near(machine.InverseTransformPoint(rig.Get("OperatorStation").position),new Vector3(-.61f,.13f,-.64f),"operator station persisted at its new pose");
                Near(machine.InverseTransformPoint(rig.Get("BladeWheel").position),new Vector3(-.11f,1.43f,-.66f),"blade handwheel persisted at the seat's front-right");
                var moved=rig.RearrangedControls();
                Check(moved.Contains("Operator station deck")&&moved.Contains("Hydraulic lever bank")&&moved.Contains("Blade handwheel assembly")&&moved.Contains("Water valve manifold"),
                    "rig reports the rearranged mounts: "+string.Join(", ",moved));
                Anchors(rig);
                Clearance(rig);
                Check(!EditorSceneManager.GetActiveScene().isDirty,"authored scene not dirtied by opening");
                Capture("Verify",rig);
                SessionState.SetString(StageKey,"play");frame=0;
                EditorApplication.update-=Tick;EditorApplication.update+=Tick;
                EditorApplication.EnterPlaymode();
            });
        }

        const string StageKey="Aurora.MachineProportions.Stage";
        static int frame;
        static IceMachineProportionsValidation()
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
                    Check(Hash(IceMachineBaker.PrefabPath)==File.ReadAllText(Root+"Validation~/"+Output+"prefab-sha256.txt"),"prefab asset unchanged by Play");
                    Exit(0);
                }
            });
        }

        static void PlayChecks(IceResurfacerGame game)
        {
            Start("Play (authored scene)");
            var rig=game.GetComponentInChildren<IceMachineRig>(true);
            var w=game.World;
            Check(rig!=null&&w.Machine==rig.Get("Machine")&&w.OperatorStation==rig.Get("OperatorStation"),"Play binds the edited prefab instance (World.Machine and the station are the authored transforms)");
            int machines=0;
            foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name=="AURORA R-54 / rear axle origin")machines++;
            Check(machines==1,"exactly one machine exists in Play (no generated duplicate)");
            Near(w.Machine.InverseTransformPoint(w.OperatorStation.position),new Vector3(-.61f,.13f,-.64f),"operator station keeps its edited pose in Play");
            Near(w.Machine.InverseTransformPoint(w.BladeWheel.parent.position),new Vector3(-.11f,1.43f,-.66f),"blade handwheel assembly keeps its edited pose in Play");
            Check(game.DriverCamera.transform.IsChildOf(w.CameraMount)&&w.Machine.InverseTransformPoint(w.CameraMount.position).y>2f,
                "seated camera rides the moved seat (eye "+w.Machine.InverseTransformPoint(w.CameraMount.position).y.ToString("F2")+" m)");
            Check(w.ConditionerLever.IsChildOf(rig.Get("OperatorStation"))&&w.FloodLever.IsChildOf(rig.Get("OperatorStation")),"levers and valves are the moved authored controls");
            QuicklookSweep(game);
        }

        // Diagnostic: where the Q (conditioner) shoulder view puts the outer conditioner edge and the
        // board brush for different station positions. Play instance only; the asset is untouched.
        static void QuicklookSweep(IceResurfacerGame game)
        {
            var w=game.World;var look=typeof(IceResurfacerGame).GetMethod("UpdateLook",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Vector3 authored=w.OperatorStation.localPosition;
            try
            {
                foreach(var shift in new[]{new Vector2(0,0),new Vector2(.13f,-.10f),new Vector2(.13f,-.15f),new Vector2(.13f,-.20f),new Vector2(.13f,-.25f),new Vector2(.13f,-.30f),new Vector2(0,-.30f),new Vector2(.13f,-.40f)})
                {
                    w.OperatorStation.localPosition=new Vector3(-.61f,shift.x,-.49f+shift.y);
                    game.SelectCameraView("Conditioner");
                    for(int i=0;i<90;i++)look.Invoke(game,new object[]{true,.02f});
                    var cam=game.DriverCamera;
                    Vector3 edge=w.Conditioner.TransformPoint(-game.Sim.Config.WaterDistributionWidth*.5f-.007f,.075f,-.1f);
                    Vector3 e=cam.WorldToViewportPoint(edge),b=cam.WorldToViewportPoint(w.Brush.position);
                    bool both=e.z>0&&e.x>.02f&&e.x<.98f&&e.y>.02f&&e.y<.98f&&b.z>0&&b.x>.02f&&b.x<.98f&&b.y>.02f&&b.y<.98f;
                    Info("Q sweep station up "+shift.x.ToString("F2")+" back "+(-shift.y).ToString("F2")+": edge "+e.ToString("F2")+" brush "+b.ToString("F2")+(both?" BOTH IN FRAME":" brush or edge outside"));
                    game.SelectCameraView("Forward");
                    for(int i=0;i<90;i++)look.Invoke(game,new object[]{true,.02f});
                }
            }
            finally { w.OperatorStation.localPosition=authored; }
        }

        // ---- Checks ----------------------------------------------------------------------

        static void Measure(string label,IceMachineRig rig)
        {
            var m=rig.Get("Machine");
            Vector3 P(string field){return m.InverseTransformPoint(rig.Get(field).position);}
            Vector3 seat=P("Seat"),eye=P("CameraMount"),wheel=P("SteeringWheel"),blade=P("BladeWheel");
            Vector3 conditioner=m.InverseTransformPoint(rig.Get("Conditioner").position);
            Info(label+": seat cushion "+V(seat)+", eye "+V(eye)+", steering hub "+V(wheel)+", blade wheel "+V(blade));
            Info(label+": seat is "+(conditioner.z-seat.z).ToString("+0.00;-0.00")+" m from the conditioner centre (z "+conditioner.z.ToString("F2")+
                "); seat-to-steering "+(wheel.z-seat.z).ToString("F2")+" m; eye height "+eye.y.ToString("F2")+" m");
        }

        static void Anchors(IceMachineRig rig)
        {
            var world=rig.Collect();
            var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
            foreach(var anchor in IceMachineRig.MechanicalAnchors(world,game.calibration))
                Check(Vector3.Distance(anchor.Actual,anchor.Expected)<.001f,"mechanical anchor unchanged: "+anchor.Label+" ("+(Vector3.Distance(anchor.Actual,anchor.Expected)*1000).ToString("F2")+" mm)");
            Check(rig.ShapeFieldsChangedSinceBake(game.calibration).Count==0,"no shape-defining calibration changed (no rebake needed)");
        }

        // Vertical clearance between the moved station/platform and the conditioner, raised and lowered.
        static void Clearance(IceMachineRig rig)
        {
            var machine=rig.Get("Machine");var conditioner=rig.Get("Conditioner");
            var rest=conditioner.localPosition;
            var above=new List<Transform>();
            foreach(var t in rig.Get("OperatorStation").GetComponentsInChildren<Transform>())
                if(t.name=="Left operator floor"||t.name=="Blade adjustment spindle"||t.name=="Blade spindle lower collar")above.Add(t);
            foreach(var t in machine.GetComponentsInChildren<Transform>())if(t.name=="Right equipment deck")above.Add(t);
            try
            {
                foreach(float lift in new[]{0f,1f})
                {
                    var p=machine.TransformPoint(new Vector3(0,lift*.34f,-1.10f));
                    conditioner.position=p;   // straight-line runtime pose: Lift*.34 m, hitch + drawbar behind the axle
                    foreach(var part in above)
                    {
                        Extent(machine,part,out Bounds b);
                        float highest=float.NegativeInfinity;string culprit="";
                        foreach(var filter in conditioner.GetComponentsInChildren<MeshFilter>())
                        {
                            if(filter.sharedMesh==null)continue;
                            foreach(var v in filter.sharedMesh.vertices)
                            {
                                var q=machine.InverseTransformPoint(filter.transform.TransformPoint(v));
                                if(q.x>b.min.x&&q.x<b.max.x&&q.z>b.min.z&&q.z<b.max.z&&q.y>highest){highest=q.y;culprit=filter.name;}
                            }
                        }
                        float gap=b.min.y-highest;
                        if(float.IsNegativeInfinity(highest))Pass(part.name+" has nothing of the conditioner beneath it (lift "+lift+")");
                        else if(gap>=-.035f)Pass(part.name+" clears the conditioner by "+(gap*1000).ToString("F0")+" mm at lift "+lift+" (highest: "+culprit+")");
                        else Info("OVERLAP "+part.name+" by "+(-gap*1000).ToString("F0")+" mm at lift "+lift+" with "+culprit);
                    }
                }
            }
            finally { conditioner.localPosition=rest; }
        }

        // ---- Survey ----------------------------------------------------------------------

        static string Tree(Transform machine,Transform conditioner)
        {
            var text=new StringBuilder();
            text.AppendLine("Machine-space extents (x right, y up, z forward; origin rear axle). min .. max");
            void Walk(Transform t,int depth)
            {
                if(!Extent(machine,t,out Bounds b))return;
                text.Append(new string(' ',depth*2)).Append(t.name)
                    .Append("  pos ").Append(V(machine.InverseTransformPoint(t.position)))
                    .Append("  scale ").Append(V(t.localScale))
                    .Append("  rot ").Append(V(t.localEulerAngles))
                    .Append("  [").Append(V(b.min)).Append(" .. ").Append(V(b.max)).AppendLine("]");
                if(depth<3)foreach(Transform child in t)Walk(child,depth+1);
            }
            Walk(machine,0);
            Walk(conditioner,0);
            return text.ToString();
        }

        static bool Extent(Transform frame,Transform part,out Bounds bounds)
        {
            bounds=default;bool any=false;
            foreach(var filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null)continue;
                var local=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var p=frame.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if(!any){bounds=new Bounds(p,Vector3.zero);any=true;}else bounds.Encapsulate(p);
                }
            }
            return any;
        }

        static string V(Vector3 v) { return "("+v.x.ToString("F2")+","+v.y.ToString("F2")+","+v.z.ToString("F2")+")"; }

        // ---- Screenshots -----------------------------------------------------------------

        internal static void Capture(string label,IceMachineRig rig)
        {
            var machine=rig.Get("Machine");
            var arena=UnityEngine.Object.FindAnyObjectByType<IceArenaRig>(FindObjectsInactive.Include);
            bool arenaActive=arena!=null&&arena.gameObject.activeSelf;
            if(arena!=null)arena.gameObject.SetActive(false);
            Vector3 P(float x,float y,float z){return machine.TransformPoint(new Vector3(x,y,z));}
            var node=new GameObject("Validation capture camera"){hideFlags=HideFlags.HideAndDontSave};
            var lightNode=new GameObject("Validation capture light"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                // Key light from the viewer's side (the machine's left), so the left elevation is lit.
                var light=lightNode.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;
                lightNode.transform.rotation=machine.rotation*Quaternion.Euler(35,70,0);
                var camera=node.AddComponent<Camera>();camera.nearClipPlane=.02f;camera.farClipPlane=60;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.42f,.45f,.5f);
                Directory.CreateDirectory(Root+"Validation~/"+Output);
                // The first render after loading uses placeholder shader variants; discard it.
                node.transform.position=P(-8,1.05f,.55f);node.transform.LookAt(P(0,1,.5f));
                for(int i=0;i<3;i++)IceResurfacerEditor.Capture(camera,Output+"warm-up.png",320,180);
                File.Delete(Root+"Validation~/"+Output+"warm-up.png");
                // Left side elevation, front to the left as in the reference photograph.
                camera.orthographic=true;camera.orthographicSize=1.55f;
                node.transform.position=P(-8,1.05f,.55f);node.transform.rotation=machine.rotation*Quaternion.LookRotation(Vector3.right);
                IceResurfacerEditor.Capture(camera,Output+label+"-Side.png",1600,900);
                camera.orthographic=false;camera.fieldOfView=40;
                node.transform.position=P(-4.2f,2.2f,3.6f);node.transform.LookAt(P(0,.95f,.6f));
                IceResurfacerEditor.Capture(camera,Output+label+"-Front-quarter.png",1600,900);
                node.transform.position=P(-3.6f,2.3f,-3.2f);node.transform.LookAt(P(0,1.05f,.2f));
                IceResurfacerEditor.Capture(camera,Output+label+"-Rear-quarter.png",1600,900);
                // Over the operator's left shoulder, looking down at the controls.
                camera.fieldOfView=55;
                node.transform.position=P(-1.05f,2.55f,-1.15f);node.transform.LookAt(P(-.05f,1.35f,.15f));
                IceResurfacerEditor.Capture(camera,Output+label+"-Cockpit.png",1600,900);
                var eye=rig.Get("CameraMount");
                if(eye!=null)
                {
                    camera.fieldOfView=70;
                    node.transform.position=eye.position;node.transform.rotation=machine.rotation*Quaternion.Euler(28,22,0);
                    IceResurfacerEditor.Capture(camera,Output+label+"-Seated.png",1600,900);
                }
                Pass("Edit Mode screenshots ("+label+") in Validation~/"+Output);
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
        static void Near(Vector3 actual,Vector3 expected,string message)
        { Check(Vector3.Distance(actual,expected)<.0005f,message+" (error "+(Vector3.Distance(actual,expected)*1000).ToString("F2")+" mm)"); }
        static string Hash(string path)
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        static void RequireIsolated()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))
                throw new InvalidOperationException("Proportions validation writes assets; run it only in the isolated StageUnityValidation.ps1 project.");
        }
        static void Start(string step) { Directory.CreateDirectory(Root+"Validation~");File.AppendAllText(ResultsPath,"== "+step+" / "+DateTime.Now.ToString("s")+"\n"); }
        static void Pass(string message) { File.AppendAllText(ResultsPath,"PASS "+message+"\n"); }
        static void Info(string message) { File.AppendAllText(ResultsPath,"INFO "+message+"\n"); }
        static void Check(bool valid,string message)
        {
            if(!valid)throw new InvalidOperationException("Machine proportions: "+message);
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
        static void Exit(int code) { Debug.Log("AURORA MACHINE PROPORTIONS VALIDATION exit="+code);EditorApplication.Exit(code); }
    }
}
