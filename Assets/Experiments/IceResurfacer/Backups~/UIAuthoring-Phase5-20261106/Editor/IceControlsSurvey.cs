using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Read-only survey of the authored Aurora (isolated validation project only): Inspector calibration
    /// context, conditioner-mounted controls (hierarchy, batching, picking, dragging at every lift),
    /// Edit Mode preview versus Play, linkage/hose ends, the authored plan outline and the guide roller.
    /// Never saves an asset. Prefix (-surveyPrefix Before|After) names results and screenshots.
    /// </summary>
    [InitializeOnLoad]
    public static class IceControlsSurvey
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        const string Out=Root+"Validation~/ControlsBrushRoller/";
        const string StageKey="Aurora.ControlsSurvey.Stage", PrefixKey="Aurora.ControlsSurvey.Prefix";
        static string Prefix=>SessionState.GetString(PrefixKey,"Before");
        static string ResultsPath=>Out+"survey-"+Prefix+".txt";
        static readonly float[] Lifts={0f,.25f,.5f,.75f,1f};
        static readonly string[] ControlNames={"","Conditioner hydraulic lever","Horizontal auger lever","Vertical auger lever","Snow tank hydraulic lever","Flood water valve","Wash water valve","Blade handwheel","Snow-breaker plunger"};
        static int frame,errors;

        public static void Survey()
        {
            SessionState.SetString(PrefixKey,Arg("-surveyPrefix","Before"));
            Directory.CreateDirectory(Out);File.WriteAllText(ResultsPath,"");errors=0;
            Run(()=>
            {
                RequireIsolated();
                EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
                var rig=UnityEngine.Object.FindAnyObjectByType<IceMachineRig>(FindObjectsInactive.Include);
                var game=rig.GetComponentInParent<IceResurfacerGame>();var w=rig.Collect();
                string prefabHash=Hash(IceMachineBaker.PrefabPath),sceneHash=Hash(IceMachineBaker.AuthoredScenePath);
                Context(rig,game,w);
                Hierarchy(rig,w,game.calibration);
                Section("EDIT MODE PREVIEW (scene calibration)");
                var edit=PreviewPoses(w,game.calibration,"scene");
                File.WriteAllText(Out+"preview-"+Prefix+".json",JsonUtility.ToJson(new PoseTable{rows=edit},true));
                EditShots(w,game.calibration);
                Spans(w,game.calibration);
                Outline(rig,w,game.calibration);
                Check(Hash(IceMachineBaker.PrefabPath)==prefabHash&&Hash(IceMachineBaker.AuthoredScenePath)==sceneHash,"survey did not modify the prefab or the scene");
                Section("PLAY");
                SessionState.SetString(StageKey,"play");frame=0;
                Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
                EditorApplication.update-=Tick;EditorApplication.update+=Tick;
                EditorApplication.EnterPlaymode();
            });
        }

        // ---- Calibration context --------------------------------------------------------------

        static void Context(IceMachineRig rig,IceResurfacerGame game,IceWorld w)
        {
            Section("CALIBRATION CONTEXT");
            var baked=JsonUtility.FromJson<SimulatorConfig>(rig.BakedConfigJson);
            Table("scene instance vs authored scene calibration",IceMechanicalAlignment.Measure(w,game.calibration));
            var contents=PrefabUtility.LoadPrefabContents(IceMachineBaker.PrefabPath);
            try
            {
                var prefabRig=contents.GetComponent<IceMachineRig>();var pw=prefabRig.Collect();
                Info("prefab root has IceResurfacerGame parent: "+(prefabRig.GetComponentInParent<IceResurfacerGame>()!=null)+" (Prefab Mode falls back to the bake-time configuration)");
                Table("PREFAB MODE as shipped (bake-time configuration)",IceMechanicalAlignment.Measure(pw,baked));
                Table("prefab vs authored scene calibration",IceMechanicalAlignment.Measure(pw,game.calibration));
                var brush=pw.BrushArm.GetComponent<IceBoardBrushRig>();
                Info("brush rig draft vs scene calibration: "+(brush.mechanism.Matches(IceBoardBrushRig.Mechanism.From(game.calibration))?"MATCHES":string.Join(", ",IceBoardBrushRigEditor.Differences(brush.mechanism,IceBoardBrushRig.Mechanism.From(game.calibration)))));
                Info("brush rig draft vs bake-time configuration: "+(brush.mechanism.Matches(IceBoardBrushRig.Mechanism.From(baked))?"MATCHES":string.Join(", ",IceBoardBrushRigEditor.Differences(brush.mechanism,IceBoardBrushRig.Mechanism.From(baked)))));
                Info("brush visual bristle radius "+IceBoardBrushRigEditor.VisualRadius(brush).ToString("F4")+" m; scene BrushRadius "+game.calibration.BrushRadius.ToString("F4")+"; baked "+baked.BrushRadius.ToString("F4"));
                Info("conveyor inlet check in the prefab preview scene: "+(IceMechanicalAlignment.InletProblem(pw)??"ok"));
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);}
            Info("conveyor inlet check in the authored scene: "+(IceMechanicalAlignment.InletProblem(w)??"ok"));
            var mods=PrefabUtility.GetPropertyModifications(rig.gameObject);
            var source=PrefabUtility.GetCorrespondingObjectFromSource(rig.gameObject);
            var below=mods.Where(m=>m.target!=source&&m.target!=source.transform).ToList();
            Info("scene overrides below the instance root: "+below.Count+(below.Count>0?" ("+string.Join("; ",below.Take(20).Select(m=>(m.target!=null?m.target.name:"<missing>")+"."+m.propertyPath))+")":""));
            Info("added objects "+PrefabUtility.GetAddedGameObjects(rig.gameObject).Count+", added components "+PrefabUtility.GetAddedComponents(rig.gameObject).Count+", removed components "+PrefabUtility.GetRemovedComponents(rig.gameObject).Count);
            foreach(var f in typeof(SimulatorConfig).GetFields(BindingFlags.Public|BindingFlags.Instance))
                if(!Equals(f.GetValue(baked),f.GetValue(game.calibration)))Info("CALIBRATION scene vs bake | "+f.Name+" | bake "+f.GetValue(baked)+" | scene "+f.GetValue(game.calibration));
        }

        static void Table(string title,List<IceMechanicalAlignment.Item> items)
        {
            Info("-- "+title);
            foreach(var i in items)
                Info("   "+i.Label+" | "+i.Kind+" | "+(i.Kind==IceMechanicalAlignment.Kind.Info?"":i.ErrorMm.ToString("F1")+" mm")+(i.Detail!=""?" | "+i.Detail:"")+(i.Blocker!=null?" | "+i.Blocker:""));
        }

        // ---- Controls hierarchy and grips -------------------------------------------------------

        static Transform For(IceWorld w,int i)
        {
            switch(i){case 1:return w.ConditionerLever;case 2:return w.HorizontalAugerLever;case 3:return w.VerticalAugerLever;case 4:return w.DumpLever;
                case 5:return w.FloodLever;case 6:return w.WashValve;case 7:return w.BladeWheel;default:return w.SnowBreakerPlunger;}
        }
        static readonly string[] GripNames={"","Black phenolic hydraulic grip","Black phenolic hydraulic grip","Black phenolic hydraulic grip","Black phenolic hydraulic grip","Valve grip","Valve grip","Handwheel crank","Snow breaker T handle"};
        static Transform Grip(IceWorld w,int i)
        {
            foreach(var t in For(w,i).GetComponentsInChildren<Transform>(true))if(t.name==GripNames[i])return t;
            return For(w,i);
        }
        static string PathOf(Transform t,Transform root){ string p=t.name; while(t.parent!=null&&t.parent!=root){t=t.parent;p=t.name+"/"+p;} return p; }

        static void Hierarchy(IceMachineRig rig,IceWorld w,SimulatorConfig c)
        {
            Section("CONTROLS / hierarchy and visible grips (Edit Mode, authored pose)");
            Transform m=w.Machine;
            for(int i=1;i<=8;i++)
            {
                var t=For(w,i);var g=Grip(w,i);
                Info(ControlNames[i]+" | "+PathOf(t,rig.transform)+" | under conditioner "+t.IsChildOf(w.Conditioner)+" | grip '"+g.name+"' control-local "+V(t.InverseTransformPoint(g.position),4)+
                    " | machine "+V(m.InverseTransformPoint(g.position),4)+" | control lossy scale "+V(t.lossyScale,4));
            }
            Info("calibration WaterValveStemHeight "+c.WaterValveStemHeight.ToString("F3")+" (picker's valve grip height in the handle frame)");
            Info("seated eye (camera mount) machine "+V(m.InverseTransformPoint(w.CameraMount.position),4)+" | seat "+V(m.InverseTransformPoint(w.Seat.position),4));
        }

        // ---- Preview poses ----------------------------------------------------------------------

        [Serializable] public sealed class PoseRow { public string part; public float lift; public Vector3 machine; }
        [Serializable] public sealed class PoseTable { public List<PoseRow> rows=new List<PoseRow>(); }

        static List<(string,Transform)> Tracked(IceWorld w)
        {
            var list=new List<(string,Transform)>();
            for(int i=1;i<=8;i++)list.Add((ControlNames[i]+" grip",Grip(w,i)));
            list.Add(("Conditioner root",w.Conditioner));
            list.Add(("Blade handwheel assembly",w.BladeWheel.parent));
            list.Add(("Flood valve bearing",w.FloodValveMount));list.Add(("Wash valve bearing",w.WashValveMount));
            for(int i=0;i<2;i++){list.Add(("Conditioner cylinder rod "+i,w.ConditionerCylinderRods[i]));list.Add(("Conditioner clevis "+i,w.ConditionerCylinderClevis[i]));}
            list.Add(("Brush arm",w.BrushArm));list.Add(("Brush head",w.Brush));
            return list;
        }

        static List<PoseRow> PreviewPoses(IceWorld w,SimulatorConfig c,string label)
        {
            var rows=new List<PoseRow>();
            try
            {
                foreach(float lift in Lifts)
                {
                    IceMechanicalAlignment.Preview(w,c,lift,0);
                    foreach(var (name,t) in Tracked(w))rows.Add(new PoseRow{part=name,lift=lift,machine=w.Machine.InverseTransformPoint(t.position)});
                }
            }
            finally{IceMechanicalAlignment.EndPreview();}
            foreach(var r in rows.Where(r=>r.part.EndsWith(" grip")&&(r.part.StartsWith("Flood")||r.part.StartsWith("Wash")||r.part.StartsWith("Blade"))))
                Info("PREVIEW "+label+" | "+r.part+" | lift "+(r.lift*100).ToString("F0")+"% | machine "+V(r.machine,4));
            return rows;
        }

        // ---- Linkage, hoses and other members that span chassis and conditioner -----------------

        static IEnumerable<Transform> SpanCandidates(IceWorld w)
        {
            foreach(var t in w.Conditioner.GetComponentsInChildren<Transform>(true))
                if(t.name=="Lift linkage"||t.name.StartsWith("Hydraulic flexible hose")||t.name.Contains("rubber line"))yield return t;
        }

        static void Spans(IceWorld w,SimulatorConfig c)
        {
            Section("SPANNING MEMBERS (ends in machine space; chassis parts nearest the upper end)");
            Transform m=w.Machine;
            var chassis=w.Machine.GetComponentsInChildren<MeshRenderer>(true).Where(r=>!r.transform.IsChildOf(w.Conditioner)).ToList();
            try
            {
                foreach(float lift in new[]{1f,.5f,0f})
                {
                    IceMechanicalAlignment.Preview(w,c,lift,0);
                    foreach(var t in SpanCandidates(w))
                    {
                        Vector3 a=m.InverseTransformPoint(t.position-t.up*t.lossyScale.y),b=m.InverseTransformPoint(t.position+t.up*t.lossyScale.y);
                        Vector3 top=a.y>b.y?a:b;
                        string nearest="";float best=float.PositiveInfinity;
                        foreach(var r in chassis)
                        {
                            float d=Vector3.Distance(r.bounds.ClosestPoint(m.TransformPoint(top)),m.TransformPoint(top));
                            if(d<best){best=d;nearest=r.name;}
                        }
                        Info("SPAN lift "+(lift*100).ToString("F0")+"% | "+PathOf(t,w.Conditioner)+" | ends "+V(a,3)+" .. "+V(b,3)+" | upper end "+(best*1000).ToString("F0")+" mm from chassis part '"+nearest+"'");
                    }
                }
            }
            finally{IceMechanicalAlignment.EndPreview();}
        }

        // ---- Plan-view outline of the authored machine ------------------------------------------

        static void Outline(IceMachineRig rig,IceWorld w,SimulatorConfig c)
        {
            Section("PLAN OUTLINE (machine space, all heights; 5 cm stations)");
            Transform m=w.Machine;
            var brush=w.BrushArm;var roller=w.GuideRoller.parent!=null&&w.GuideRoller.parent!=m?w.GuideRoller.parent:w.GuideRoller;
            var bins=new SortedDictionary<int,(float min,float max,string minPart,string maxPart)>();
            var condBins=new SortedDictionary<int,(float min,float max)>();
            foreach(var r in rig.GetComponentsInChildren<MeshRenderer>(false))
            {
                if(r.GetComponent<TextMesh>()!=null)continue;
                var f=r.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null)continue;
                bool conditioner=r.transform.IsChildOf(w.Conditioner);
                if(r.transform.IsChildOf(brush)||r.transform.IsChildOf(roller))continue;
                var mesh=f.sharedMesh;Vector3[] vertices=mesh.isReadable?mesh.vertices:Corners(mesh.bounds);
                foreach(var v in vertices)
                {
                    Vector3 p=m.InverseTransformPoint(r.transform.TransformPoint(v));
                    int k=Mathf.FloorToInt(p.z/.05f);
                    if(conditioner)
                    {
                        condBins.TryGetValue(k,out var e);if(!condBins.ContainsKey(k))e=(p.x,p.x);
                        condBins[k]=(Mathf.Min(e.min,p.x),Mathf.Max(e.max,p.x));continue;
                    }
                    if(!bins.TryGetValue(k,out var b))b=(p.x,p.x,r.name,r.name);
                    if(p.x<b.min){b.min=p.x;b.minPart=r.name;}if(p.x>b.max){b.max=p.x;b.maxPart=r.name;}
                    bins[k]=b;
                }
            }
            foreach(var pair in bins)
                Info("OUTLINE z "+(pair.Key*.05f).ToString("F2")+".."+((pair.Key+1)*.05f).ToString("F2")+" | x "+pair.Value.min.ToString("F3")+" .. "+pair.Value.max.ToString("F3")+
                    " | left '"+pair.Value.minPart+"' right '"+pair.Value.maxPart+"'");
            foreach(var pair in condBins)
                Info("OUTLINE conditioner (authored lift) z "+(pair.Key*.05f).ToString("F2")+" | x "+pair.Value.min.ToString("F3")+" .. "+pair.Value.max.ToString("F3"));
            float radius=IceMechanicalAlignment.GuideRollerRadius(w);
            Info("GUIDE ROLLER visible centre "+V(m.InverseTransformPoint(w.GuideRoller.position),4)+" radius "+radius.ToString("F4")+" | simulated ("+c.GuideRollerX+", "+c.GuideRollerZ+") r "+c.GuideRollerRadius);
            Info("SIMULATED BODY rectangle x +-"+c.VehicleHalfWidth+" z "+(-c.BodyRear)+" .. "+c.BodyFront);
        }
        static Vector3[] Corners(Bounds b){var list=new Vector3[8];for(int i=0;i<8;i++)list[i]=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));return list;}

        // ---- Edit Mode screenshots --------------------------------------------------------------

        static void EditShots(IceWorld w,SimulatorConfig c)
        {
            Transform m=w.Machine;
            try
            {
                foreach(float lift in new[]{0f,.5f,1f})
                {
                    IceMechanicalAlignment.Preview(w,c,lift,0);
                    Shoot(Prefix+"-Edit-Preview-Lift"+(lift*100).ToString("F0")+"-Side",m,m.TransformPoint(5,1.3f,-.7f),m.TransformPoint(0,1.3f,-.7f),1.25f);
                }
            }
            finally{IceMechanicalAlignment.EndPreview();}
        }

        // ---- Play ---------------------------------------------------------------------------------

        static IceControlsSurvey()
        {
            if(Application.isBatchMode&&SessionState.GetString(StageKey,"")!="")
            { EditorApplication.update-=Tick;EditorApplication.update+=Tick; }
        }
        static void Log(string message,string stack,LogType type){ if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)errors++; }

        static void Tick()
        {
            string stage=SessionState.GetString(StageKey,"");
            Run(()=>
            {
                if(stage=="play")
                {
                    if(!EditorApplication.isPlaying||++frame<30)return;
                    var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
                    if(game==null||game.World==null)return;
                    Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
                    PlaySurvey(game);
                    Info("Unity runtime errors or exceptions in Play: "+errors);
                    SessionState.SetString(StageKey,"exit");
                    EditorApplication.ExitPlaymode();
                }
                else if(stage=="exit"&&!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
                    Exit(0);
                }
            });
        }

        static void Render(IceResurfacerGame game,float dt)
        { typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{dt}); }

        static void Park(Simulation s,float x,float z,float heading)
        {
            s.Vehicle.X=x;s.Vehicle.Z=z;s.Vehicle.Heading=heading;s.Vehicle.Speed=0;s.Vehicle.SteeringAngle=0;
            float behind=s.Config.HitchBehindAxle+s.Config.DrawbarLength;
            s.Conditioner.Heading=heading;s.Conditioner.X=x-Mathf.Sin(heading)*behind;s.Conditioner.Z=z-Mathf.Cos(heading)*behind;
            s.Controls.HydraulicHoldControls=false;s.Controls.Throttle=0;s.Controls.Steer=0;s.Controls.Brake=false;
        }

        static void PlaySurvey(IceResurfacerGame game)
        {
            bool enabled=game.enabled;game.enabled=false;
            Keyboard keys=null;Mouse mouse=null;
            try
            {
                // A batch-mode Game view never has focus: route the virtual devices to it regardless.
                var validationInput=UnityEngine.Object.Instantiate(InputSystem.settings);validationInput.hideFlags=HideFlags.HideAndDontSave;
                validationInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                validationInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=validationInput;
                keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
                game.NewSession(true);var s=game.Sim;var w=game.World;Transform m=w.Machine;
                Park(s,0,-5,0);s.Step(.02f);
                // Preview vs Play.
                Section("PLAY POSES vs EDIT MODE PREVIEW (same lift)");
                var edit=JsonUtility.FromJson<PoseTable>(File.ReadAllText(Out+"preview-"+Prefix+".json")).rows;
                foreach(float lift in Lifts)
                {
                    s.Conditioner.Lift=lift;Render(game,0);
                    foreach(var (name,t) in Tracked(w))
                    {
                        var e=edit.FirstOrDefault(r=>r.part==name&&Mathf.Abs(r.lift-lift)<1e-4f);if(e==null)continue;
                        float d=Vector3.Distance(e.machine,m.InverseTransformPoint(t.position))*1000;
                        if(d>.5f||name.EndsWith(" grip"))Info("POSE lift "+(lift*100).ToString("F0")+"% | "+name+" | play-edit "+d.ToString("F1")+" mm");
                    }
                }
                // Batching: is each control still its own moving visual?
                Section("PLAY BATCHING / visible renderers that move with each control");
                var controls=game.PhysicalControls;
                var highlight=(Renderer[][])typeof(IcePhysicalControls).GetField("highlightRenderers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controls);
                s.Conditioner.Lift=1;Render(game,0);
                for(int i=1;i<=8;i++)
                {
                    var t=For(w,i);var enabledRenderers=t.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToList();
                    int vertices=enabledRenderers.Sum(r=>{var f=r.GetComponent<MeshFilter>();return f!=null&&f.sharedMesh!=null?f.sharedMesh.vertexCount:0;});
                    Info("BATCH "+ControlNames[i]+" | enabled renderers under the control "+enabledRenderers.Count+" ("+vertices+" vertices) | highlight renderers "+highlight[i].Length+" (alive "+highlight[i].Count(r=>r!=null&&r.enabled)+")");
                }
                foreach(Transform child in w.Conditioner)
                    if(child.name.StartsWith("Batched "))Info("BATCH conditioner static batch '"+child.name+"' "+child.GetComponent<MeshFilter>().sharedMesh.vertexCount+" vertices");
                // Picking and dragging at each lift, aiming at the visible grip.
                Section("PICKING (camera at the seated eye, aimed at each visible grip; virtual mouse through IcePhysicalControls.UpdateInput)");
                var cam=game.DriverCamera;
                void Pointer(Vector2 p,bool down=false){InputSystem.QueueStateEvent(mouse,new MouseState{position=p,buttons=(ushort)(down?1:0)});InputSystem.Update();controls.UpdateInput(true,false);}
                InputSystem.QueueStateEvent(keys,new KeyboardState());InputSystem.Update();
                foreach(float lift in new[]{0f,.5f,1f})
                {
                    s.Conditioner.Lift=lift;Render(game,0);
                    for(int i=1;i<=8;i++)
                    {
                        Vector3 grip=Grip(w,i).position;
                        cam.transform.localPosition=new Vector3(0,game.operatorEyeHeight,0);
                        cam.transform.rotation=Quaternion.LookRotation(grip-cam.transform.position,m.up);
                        Vector2 screen=cam.WorldToScreenPoint(grip);
                        string hit=controls.GetHitControl(screen);
                        Pointer(screen);
                        Info("PICK lift "+(lift*100).ToString("F0")+"% | aim "+ControlNames[i]+" | picked '"+hit+"' | hover '"+controls.HighlightedControl+"' | distance "+Vector3.Distance(cam.transform.position,grip).ToString("F2")+" m");
                    }
                    Pointer(Vector2.zero);
                    if(lift!=.5f)DriverShot(game,"Controls-Seated-Lift"+(lift*100).ToString("F0"));
                }
                // Valve drag: turn the visible grip 45 degrees about its stem with the pointer.
                Section("DRAG (pointer follows the visible grip turning 45 deg about its own axis)");
                foreach(float lift in new[]{0f,.5f,1f})
                foreach(int i in new[]{5,6,7})
                {
                    game.NewSession(true);s=game.Sim;w=game.World;m=w.Machine;Park(s,0,-5,0);s.Step(.02f);
                    s.Conditioner.Lift=lift;Render(game,0);
                    var t=For(w,i);Transform axisFrame=t.parent;Vector3 pivot=t.position;Vector3 grip=Grip(w,i).position;
                    cam.transform.localPosition=new Vector3(0,game.operatorEyeHeight,0);
                    cam.transform.rotation=Quaternion.LookRotation(grip-cam.transform.position,m.up);
                    float before=i==5?s.Controls.FloodFlow:i==6?s.Controls.WashValve:s.Controls.BladeDepthMm;
                    Pointer(cam.WorldToScreenPoint(grip));Pointer(cam.WorldToScreenPoint(grip),true);
                    string grabbed=controls.ActiveControl;
                    // Valves open toward 6 o'clock (negative yaw about the bearing's up); the handwheel deepens clockwise from above.
                    for(int k=1;k<=15;k++)
                    {
                        Quaternion turn=Quaternion.AngleAxis((i==7?3:-3)*k,axisFrame.up);
                        Vector3 p=pivot+turn*(grip-pivot);
                        Pointer(cam.WorldToScreenPoint(p),true);
                    }
                    float after=i==5?s.Controls.FloodFlow:i==6?s.Controls.WashValve:s.Controls.BladeDepthMm;
                    Pointer(cam.WorldToScreenPoint(grip),false);
                    Info("DRAG lift "+(lift*100).ToString("F0")+"% | "+ControlNames[i]+" | grabbed '"+grabbed+"' | setting "+before.ToString("F3")+" -> "+after.ToString("F3")+
                        (i==7?" mm (expected about "+(45f/360f/s.Config.BladeWheelTurns*s.Config.MaxBladeDepthMm).ToString("F3")+" mm)":" (expected 0.500)"));
                }
                // Parent motion while holding: pointer still, conditioner lifts.
                Section("HOLD WHILE THE CONDITIONER MOVES (pointer still)");
                foreach(int i in new[]{5,6,7})
                {
                    game.NewSession(true);s=game.Sim;w=game.World;m=w.Machine;Park(s,0,-5,0);s.Step(.02f);
                    s.Conditioner.Lift=0;s.Controls.ConditionerTarget=0;Render(game,0);
                    if(i==5)s.Controls.FloodFlow=.5f;else if(i==6)s.Controls.WashValve=.5f;else s.Controls.BladeDepthMm=.4f;
                    Render(game,0);
                    var grip=Grip(w,i).position;
                    cam.transform.localPosition=new Vector3(0,game.operatorEyeHeight,0);
                    cam.transform.rotation=Quaternion.LookRotation(grip-cam.transform.position,m.up);
                    Vector2 screen=cam.WorldToScreenPoint(grip);
                    Pointer(screen);Pointer(screen,true);string grabbed=controls.ActiveControl;
                    float before=i==5?s.Controls.FloodFlow:i==6?s.Controls.WashValve:s.Controls.BladeDepthMm;
                    for(int k=1;k<=25;k++){s.Conditioner.Lift=k/25f;Render(game,0);Pointer(screen,true);}
                    float after=i==5?s.Controls.FloodFlow:i==6?s.Controls.WashValve:s.Controls.BladeDepthMm;
                    Pointer(screen,false);
                    Info("HOLD | "+ControlNames[i]+" | grabbed '"+grabbed+"' | setting "+before.ToString("F3")+" -> "+after.ToString("F3")+" while the conditioner rose 0 -> 100% (pointer still)");
                }
                // E (Controls) view.
                Section("E CONTROLS VIEW");
                game.NewSession(true);s=game.Sim;w=game.World;m=w.Machine;Park(s,0,-5,0);s.Step(.02f);
                foreach(float lift in new[]{0f,1f})
                {
                    s.Conditioner.Lift=lift;Render(game,0);
                    game.SelectCameraView("Controls");
                    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    float yaw=(float)typeof(IceResurfacerGame).GetField("targetYaw",flags).GetValue(game),pitch=(float)typeof(IceResurfacerGame).GetField("targetPitch",flags).GetValue(game);
                    Vector3 head=(Vector3)typeof(IceResurfacerGame).GetMethod("HeadOffset",flags).Invoke(game,new object[]{yaw});
                    cam.transform.localPosition=head;cam.transform.localRotation=Quaternion.Euler(pitch,yaw,0);
                    var inView=new List<string>();var picked=new List<string>();
                    for(int i=1;i<=8;i++)
                    {
                        Vector3 v=cam.WorldToViewportPoint(Grip(w,i).position);
                        if(v.z>0&&v.x>0&&v.x<1&&v.y>0&&v.y<1)inView.Add(ControlNames[i]);
                        string hit=controls.GetHitControl(cam.WorldToScreenPoint(Grip(w,i).position));
                        picked.Add(ControlNames[i]+"->"+(hit==""?"none":hit));
                    }
                    Info("E view lift "+(lift*100).ToString("F0")+"% | yaw "+yaw.ToString("F1")+" pitch "+pitch.ToString("F1")+" | grips in view: "+string.Join(", ",inView)+" | picks: "+string.Join("; ",picked));
                    DriverShot(game,"Controls-EView-Lift"+(lift*100).ToString("F0"));
                    game.SelectCameraView("Forward");
                }
            }
            finally
            {
                if(keys!=null)InputSystem.RemoveDevice(keys);if(mouse!=null)InputSystem.RemoveDevice(mouse);
                game.NewSession(true);game.enabled=enabled;
            }
        }

        static void DriverShot(IceResurfacerGame game,string label)
        {
            Directory.CreateDirectory(Out+"Shots");
            IceResurfacerEditor.Capture(game.DriverCamera,"ControlsBrushRoller/Shots/warm.png",320,180);
            IceResurfacerEditor.Capture(game.DriverCamera,"ControlsBrushRoller/Shots/"+Prefix+"-"+label+".png",1280,800);
            File.Delete(Out+"Shots/warm.png");
        }

        static void Shoot(string label,Transform m,Vector3 from,Vector3 at,float size)
        {
            var arena=UnityEngine.Object.FindAnyObjectByType<IceArenaRig>(FindObjectsInactive.Include);
            bool active=arena!=null&&arena.gameObject.activeSelf;
            if(arena!=null)arena.gameObject.SetActive(false);
            var node=new GameObject("Survey camera"){hideFlags=HideFlags.HideAndDontSave};var lightNode=new GameObject("Survey light"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var light=lightNode.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;lightNode.transform.rotation=m.rotation*Quaternion.Euler(40,60,0);
                var cam=node.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.02f;cam.farClipPlane=60;
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.42f,.45f,.5f);
                node.transform.SetPositionAndRotation(from,Quaternion.LookRotation(at-from,m.up));
                Directory.CreateDirectory(Out+"Shots");
                IceResurfacerEditor.Capture(cam,"ControlsBrushRoller/Shots/warm.png",320,180);
                IceResurfacerEditor.Capture(cam,"ControlsBrushRoller/Shots/"+label+".png",1200,900);
                File.Delete(Out+"Shots/warm.png");
            }
            finally{UnityEngine.Object.DestroyImmediate(node);UnityEngine.Object.DestroyImmediate(lightNode);if(arena!=null)arena.gameObject.SetActive(active);}
        }

        // ---- Helpers ------------------------------------------------------------------------------

        static string Arg(string name,string fallback)
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]==name)return args[i+1];
            return fallback;
        }
        static string V(Vector3 v,int d=3){ string f="F"+d; return "("+v.x.ToString(f,System.Globalization.CultureInfo.InvariantCulture)+","+v.y.ToString(f,System.Globalization.CultureInfo.InvariantCulture)+","+v.z.ToString(f,System.Globalization.CultureInfo.InvariantCulture)+")"; }
        static string Hash(string path){ using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
        static void RequireIsolated()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))
                throw new InvalidOperationException("Run the controls survey only in the isolated StageUnityValidation.ps1 project.");
        }
        static void Section(string title){ File.AppendAllText(ResultsPath,"== "+title+"\n"); }
        static void Info(string message){ File.AppendAllText(ResultsPath,"INFO "+message+"\n"); }
        static void Check(bool valid,string message)
        {
            if(!valid)throw new InvalidOperationException("Survey: "+message);
            File.AppendAllText(ResultsPath,"PASS "+message+"\n");
        }
        static void Run(Action body)
        {
            try { body(); }
            catch(Exception e)
            {
                Debug.LogException(e);SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
                try { File.AppendAllText(ResultsPath,"FAIL "+e.Message+"\n"+e.StackTrace+"\n"); } catch { }
                if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
                Exit(1);
            }
        }
        static void Exit(int code){ Debug.Log("AURORA CONTROLS SURVEY exit="+code);EditorApplication.Exit(code); }
    }
}
