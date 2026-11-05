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

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Rink refinement and persistent water fill ports, isolated validation project only.
    ///   Apply       : runs both migrations (as the menu items do) and records the result.
    ///   Verify      : fresh process; persistence, Edit Mode, analysis, then Play (no duplicates,
    ///                 targeting, flood + wash coupling, overflow, gauges, conservation, no mutation).
    ///   MovePorts   : deliberately moves the ports in the prefab (flood assembly, wash socket alone).
    ///   VerifyMoved : fresh process; Play couples, fills and overflows at the moved sockets.
    /// </summary>
    [InitializeOnLoad]
    public static class IceRinkFillPortValidation
    {
        const string Root="Assets/Experiments/IceResurfacer/";
        const string Out=IceRinkSurvey.Out;
        const string StageKey="Aurora.RinkPorts.Stage",ResultsKey="Aurora.RinkPorts.Results";
        static string ResultsPath=>Out+"results-"+SessionState.GetString(ResultsKey,"run")+".txt";
        static readonly string[] Watched={IceRinkRefinement.SectionPath,IceRinkRefinement.MeshesPath,IceFillPorts.PrefabPath,Root+"Machine/AuroraR54.Meshes.asset",
            Root+"Arena/NorthlineArena.prefab",Root+"Arena/Sections/Southeast diagonal portal - backstage maintenance room.prefab",IceMachineBaker.AuthoredScenePath};
        static int frame,errors;

        [Serializable] sealed class Expected { public List<string> paths=new List<string>(),hashes=new List<string>(); public Vector3 floodSocket,washSocket,floodOverflow,washOverflow; }

        public static void Apply()
        {
            Begin("Apply",()=>
            {
                OpenScene();var game=Game();
                Start("Machine before migration");
                var before=MachineDump();
                Info("machine transforms: "+before.Count);
                Start("Rink boards and glass (Authored Scene > Refine Rink Boards and Glass)");
                foreach(var line in IceRinkRefinement.Apply(game.calibration).Split('\n'))Info(line);
                Start("Water fill ports (Authored Scene > Add Persistent Water Fill Ports to Machine)");
                foreach(var line in IceFillPorts.Migrate().Split('\n'))Info(line);
                Check(IceFillPorts.Migrate().Contains("nothing changed"),"running the migration again changes nothing (no duplicates)");
                Check(IceRinkRefinement.Apply(game.calibration).Contains("nothing changed"),"running the rink refinement again changes nothing");
                OpenScene();
                var after=MachineDump();
                int same=0;
                foreach(var pair in before)
                {
                    Check(after.TryGetValue(pair.Key,out string now),"existing machine part still present: "+pair.Key);
                    Check(now==pair.Value,"machine part unchanged: "+pair.Key);same++;
                }
                var added=after.Keys.Except(before.Keys).ToList();
                Check(added.All(k=>k.Contains(IceFillPorts.GroupName)),"every added transform is inside "+IceFillPorts.GroupName+" ("+added.Count+")");
                Info("all "+same+" existing machine transforms, meshes and materials unchanged; "+added.Count+" added");
                var e=new Expected();
                foreach(var p in Watched){e.paths.Add(p);e.hashes.Add(Hash(p));}
                File.WriteAllText(Out+"expected.json",JsonUtility.ToJson(e,true));
                Exit(0);
            });
        }

        public static void MovePorts()
        {
            Begin("MovePorts",()=>
            {
                var root=PrefabUtility.LoadPrefabContents(IceFillPorts.PrefabPath);
                try
                {
                    var ports=root.GetComponentsInChildren<IceWaterFillPort>(true);
                    var flood=ports.Single(p=>p.port==WaterFillPort.Flood);var wash=ports.Single(p=>p.port==WaterFillPort.Wash);
                    Start("Deliberately moving the fill ports in the prefab");
                    flood.transform.localPosition+=new Vector3(0,.10f,.35f);
                    flood.transform.localRotation=Quaternion.Euler(0,-12,0);
                    wash.hoseSocket.localPosition+=new Vector3(.02f,-.06f,-.08f);wash.hoseSocket.localRotation=Quaternion.Euler(0,0,-20);
                    wash.overflowSocket.localPosition+=new Vector3(0,-.05f,.06f);
                    var gauge=root.GetComponentsInChildren<IceWaterLevelGauge>(true).Single(g=>g.tank==WaterFillPort.Wash);gauge.transform.localPosition+=new Vector3(0,.12f,-.20f);
                    Info("flood port assembly +10 cm up, +35 cm forward, yawed 12 deg; wash hose socket moved alone (+2,-6,-8 cm, rolled 20 deg); wash overflow moved; wash gauge moved");
                    PrefabUtility.SaveAsPrefabAsset(root,IceFillPorts.PrefabPath);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                AssetDatabase.SaveAssets();
                Exit(0);
            });
        }

        /// <summary>Read-only: the generated ports as they are today (Edit Mode shows none; Play couples).</summary>
        public static void CaptureBefore()
        {
            Begin("CaptureBefore",()=>
            {
                var rig=OpenScene();
                Capture("Before-Edit-FillPorts-Prefab",rig.Get("Machine"),new Vector3(2.4f,1.4f,-.3f),new Vector3(1.05f,1.1f,-.25f),40);
                Play("before");
            });
        }

        static void HoseShots(IceResurfacerGame game,string prefix)
        {
            bool enabled=game.enabled;game.enabled=false;
            try
            {
                foreach(var port in new[]{WaterFillPort.Flood,WaterFillPort.Wash})
                {
                    game.NewSession(true);var s=game.Sim;var service=game.WaterService;Transform m=game.World.Machine;
                    Transform socket=port==WaterFillPort.Flood?service.FloodInlet:service.WashInlet;
                    Check(s.TryDismount()&&s.TakeHose(0)&&s.ConnectHose(port,6,0),"hose coupled to the "+port+" port");
                    service.UpdateViewAndEffects(true,.02f);
                    Vector3 local=m.InverseTransformPoint(socket.position);
                    Info(prefix+" "+port+" socket machine "+local.ToString("F3")+", hose "+service.HoseVisibleLength.ToString("0.00")+" m");
                    Capture(prefix+"-Play-Hose-"+port,m,new Vector3(3.6f,1.7f,local.z-.6f),local,42,false);
                }
            }
            finally{game.NewSession(true);game.enabled=enabled;}
        }

        /// <summary>The existing arena equivalence check: the migrated authored arena against a fresh
        /// procedural build, object by object (name, order, pose, mesh, material).</summary>
        public static void Equivalence()
        {
            Begin("Equivalence",()=>
            {
                OpenScene();var game=Game();
                Start("Migrated arena prefab vs the updated procedural builder");
                // A temporary, unsaved instance of the arena prefab itself: your scene overrides (the banner text) are not part of it.
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Arena/NorthlineArena.prefab");
                var temp=(GameObject)PrefabUtility.InstantiatePrefab(asset,game.transform);
                try
                {
                    var method=typeof(IceAuthoredArenaValidation).GetMethod("CheckEquivalence",BindingFlags.Static|BindingFlags.NonPublic);
                    try{method.Invoke(null,new object[]{game,temp.GetComponent<IceArenaRig>()});}
                    catch(TargetInvocationException e){throw e.InnerException;}
                }
                finally{UnityEngine.Object.DestroyImmediate(temp);}
                Check(true,"arena equivalence passed (details in Validation~/AuthoredArena-results.txt)");
                Exit(0);
            });
        }

        /// <summary>Informational: the raised-tank glass contact still uses the simulated 2.55 m glass
        /// (collision unchanged). How far does the visible tank pass the board line inside the taller glass?</summary>
        public static void TankGlass(){ Begin("TankGlass",()=>{ OpenScene();Play("tank"); }); }

        static void TankGlassScenario(IceResurfacerGame game)
        {
            bool enabled=game.enabled;game.enabled=false;
            var render=typeof(IceResurfacerGame).GetMethod("UpdateMachine",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                var c=game.calibration;var layout=new ServiceLayout(c);var profile=new IceRinkBoards.GlassProfile(c,IceRinkBoards.Boundary(c),layout);
                foreach(var scenario in new[]{("east side glass",new Vector3(10.1f,0,0),Mathf.PI*.5f),("north end glass",new Vector3(0,0,25.6f),0f)})
                {
                    game.NewSession(true);var s=game.Sim;
                    s.Vehicle.X=scenario.Item2.x;s.Vehicle.Z=scenario.Item2.z;s.Vehicle.Heading=scenario.Item3;s.Vehicle.Speed=0;
                    s.SetTankHydraulic(1);for(int i=0;i<350;i++){s.Step(.02f);}
                    render.Invoke(game,new object[]{0f});
                    float worst=0,worstHigh=0;
                    foreach(var f in game.World.TankPivot.GetComponentsInChildren<MeshFilter>())
                    {
                        var r=f.GetComponent<MeshRenderer>();if(r==null||!r.enabled||f.sharedMesh==null)continue;
                        foreach(var v in f.sharedMesh.vertices)
                        {
                            Vector3 p=f.transform.TransformPoint(v)-game.transform.position;
                            float beyond=s.Ice.BoardDistance(p.x,p.z,out _,out _);
                            if(beyond<=0)continue;
                            if(p.y<=2.55f)worst=Mathf.Max(worst,beyond);
                            else if(p.y<profile.Top(p))worstHigh=Mathf.Max(worstHigh,beyond);
                        }
                    }
                    Info("TANK GLASS | "+scenario.Item1+" | stopped at lift "+(s.TankLift*100).ToString("0")+"% ("+s.LastMovementContact.Obstacle+" at y "+s.LastMovementContact.PointY.ToString("0.00")+
                        " m) | visible tank beyond the board line: below 2.55 m "+(worst*1000).ToString("0")+" mm, between 2.55 m and the new glass top "+(worstHigh*1000).ToString("0")+" mm");
                }
            }
            finally{game.NewSession(true);game.enabled=enabled;}
        }

        public static void Verify(){ Begin("Verify",()=>{ EditChecks(false);Play("verify"); }); }
        public static void VerifyMoved(){ Begin("VerifyMoved",()=>{ EditChecks(true);Play("moved"); }); }

        static void EditChecks(bool moved)
        {
            var rig=OpenScene();var game=Game();
            Start("Edit Mode after restarting Unity"+(moved?" (ports moved in the prefab)":""));
            if(!moved)
            {
                var e=JsonUtility.FromJson<Expected>(File.ReadAllText(Out+"expected.json"));
                for(int i=0;i<e.paths.Count;i++)Check(Hash(e.paths[i])==e.hashes[i],"persisted unchanged: "+Path.GetFileName(e.paths[i]));
            }
            Check(!EditorSceneManager.GetActiveScene().isDirty,"opening the scene does not dirty it");
            var ports=rig.GetComponentsInChildren<IceWaterFillPort>(true);var gauges=rig.GetComponentsInChildren<IceWaterLevelGauge>(true);
            Check(ports.Length==2&&ports.Count(p=>p.port==WaterFillPort.Flood)==1&&ports.All(p=>p.IsComplete),"exactly one complete flood and one complete wash fill port in the prefab");
            Check(gauges.Length==2&&gauges.All(g=>g.IsComplete&&g.indicator.GetComponentInParent<IceKeepUnbatched>()!=null),"two complete gauges, indicators kept unbatched");
            foreach(var p in ports)
            {
                Check(p.GetComponentsInChildren<MeshRenderer>().Length>=6&&p.GetComponentsInChildren<MeshRenderer>().All(r=>r.enabled&&r.gameObject.activeInHierarchy),p.Label+" port is visible in Edit Mode");
                Check(PrefabUtility.GetCorrespondingObjectFromSource(p.gameObject)!=null,p.Label+" port lives in the prefab, not as a scene addition");
                var r=IceFillPorts.Analyze(p);
                foreach(var f in r.Findings)Info("analysis "+p.Label+": "+f.Key+" "+f.Value);
                Check(r.Usable,p.Label+" port is reachable and within hose length at the parked pose (reach "+r.Reach.ToString("0.00")+" m, hose "+r.RouteLength.ToString("0.0")+" m)");
            }
            Check(PrefabUtility.GetAddedGameObjects(rig.gameObject).Count==0&&PrefabUtility.GetAddedComponents(rig.gameObject).Count==0,"no scene additions on the machine instance");
            if(!moved)
            {
                // Labelled negative fixtures (not saved): the Inspector must warn, not snap back.
                var p=ports.Single(x=>x.port==WaterFillPort.Flood);Transform m=rig.Get("Machine");
                Vector3 pos=p.hoseSocket.position;Quaternion rot=p.hoseSocket.rotation;
                try
                {
                    p.hoseSocket.position=m.TransformPoint(0,3.2f,1.0f);
                    var high=IceFillPorts.Analyze(p);
                    Check(!high.Usable&&high.Findings.Any(f=>f.Key==MessageType.Error&&f.Value.StartsWith("Out of reach")),"a socket raised 1.3 m above the tank roof is an error: out of reach ("+high.Reach.ToString("0.00")+" m)");
                    p.hoseSocket.position=m.TransformPoint(0,1.2f,1.0f);
                    var buried=IceFillPorts.Analyze(p);
                    Check(buried.Findings.Any(f=>f.Key==MessageType.Warning&&f.Value.Contains("reach through")),"a socket buried in the middle of the machine is warned about: the hand passes through "+
                        string.Join("",buried.Findings.Where(f=>f.Value.Contains("reach through")).Select(f=>f.Value.Split(new[]{" to get"},StringSplitOptions.None)[0].Replace("The operator would reach through ",""))));
                    p.hoseSocket.SetPositionAndRotation(pos,rot*Quaternion.Euler(0,180,0));
                    var inward=IceFillPorts.Analyze(p);
                    Check(inward.Findings.Any(f=>f.Key==MessageType.Warning&&f.Value.Contains("points into")),"a socket turned to face the machine is warned about (nozzle points into the body)");
                }
                finally{p.hoseSocket.SetPositionAndRotation(pos,rot);}
                Check(p.hoseSocket.position==pos&&IceFillPorts.Analyze(p).Usable,"the analysis never moves the socket; restoring it makes the port usable again");
            }
            var arena=UnityEngine.Object.FindAnyObjectByType<IceArenaRig>(FindObjectsInactive.Include);
            var drift=IceArenaDrift.Evaluate(arena,game.calibration);
            foreach(var f in drift.Where(f=>f.Drifted))Info("DRIFT "+f.Label+": "+f.Detail);
            Check(drift.All(f=>!f.Drifted),"arena drift report: all "+drift.Count+" physical checks within 2 mm");
            if(!moved)Capture("Edit-FillPorts-Prefab",rig.Get("Machine"),new Vector3(2.4f,1.4f,-.3f),new Vector3(1.05f,1.1f,-.25f),40);
        }

        // ---- Play -------------------------------------------------------------------------------

        static IceRinkFillPortValidation()
        {
            if(Application.isBatchMode&&SessionState.GetString(StageKey,"")!="")
            { EditorApplication.update-=Tick;EditorApplication.update+=Tick; }
        }
        static void Play(string stage)
        {
            SessionState.SetString(StageKey,stage);frame=0;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string stack,LogType type){ if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)errors++; }
        static void Tick()
        {
            string stage=SessionState.GetString(StageKey,"");
            Run(()=>
            {
                if(stage=="tank")
                {
                    if(!EditorApplication.isPlaying||++frame<30)return;
                    var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
                    if(game==null||game.World==null)return;
                    TankGlassScenario(game);
                    SessionState.SetString(StageKey,"tank-exit");EditorApplication.ExitPlaymode();
                }
                else if(stage=="verify"||stage=="moved"||stage=="before")
                {
                    if(!EditorApplication.isPlaying||++frame<30)return;
                    var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>();
                    if(game==null||game.World==null)return;
                    Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
                    if(stage=="before")HoseShots(game,"Before");
                    else{PlayChecks(game,stage=="moved");HoseShots(game,stage=="moved"?"Moved":"After");}
                    Check(errors==0,"zero Unity runtime errors or exceptions in Play ("+errors+")");
                    SessionState.SetString(StageKey,stage+"-exit");
                    EditorApplication.ExitPlaymode();
                }
                else if(stage.EndsWith("-exit")&&!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
                    if(stage=="verify-exit")
                    {
                        var e=JsonUtility.FromJson<Expected>(File.ReadAllText(Out+"expected.json"));
                        for(int i=0;i<e.paths.Count;i++)Check(Hash(e.paths[i])==e.hashes[i],"Play left unchanged: "+Path.GetFileName(e.paths[i]));
                    }
                    Exit(0);
                }
            });
        }

        static void PlayChecks(IceResurfacerGame game,bool moved)
        {
            bool enabled=game.enabled;game.enabled=false;
            try
            {
                var w=game.World;var service=game.WaterService;Transform m=w.Machine;
                Start("Play: one set of fittings, bound to the prefab"+(moved?" (moved ports)":""));
                var ports=m.GetComponentsInChildren<IceWaterFillPort>(true);
                Check(ports.Length==2,"exactly two fill ports under the machine in Play");
                Check(!game.GetComponentsInChildren<Transform>(true).Any(t=>t.name.EndsWith(" fill coupling")||t.name=="Tank sight level tube"),"no generated fill couplings or gauges (nothing duplicated)");
                var flood=ports.Single(p=>p.port==WaterFillPort.Flood);var wash=ports.Single(p=>p.port==WaterFillPort.Wash);
                Check(service.FloodInlet==flood.hoseSocket&&service.WashInlet==wash.hoseSocket,"the water service couples at the authored sockets");
                int nozzles=game.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Service nozzle / carried and coupled");
                Check(nozzles==1,"exactly one carried nozzle ("+nozzles+")");
                foreach(var port in new[]{flood,wash})PortChecks(game,port,moved);
                // (Drift is measured in Edit Mode: Play batches the arena's meshes, so mesh checks there read empty geometry.)
                Start("Play: the rink");
                var s0=game.Sim;
                Check(s0.Ice.IsInside(0,0)&&Mathf.Abs(s0.Ice.BoardDistance(-s0.Config.RinkWidth*.5f,0,out _,out _))<.001f,"collision boards unchanged: the simulated board line is still at the 26 m x 60 m rink edge");
            }
            finally{game.NewSession(true);game.enabled=enabled;}
        }

        static void PortChecks(IceResurfacerGame game,IceWaterFillPort port,bool moved)
        {
            var service=game.WaterService;var w=game.World;Transform m=w.Machine;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            Start("Play: "+port.Label+" port");
            game.NewSession(true);var s=game.Sim;
            Check(s.ConfigureWaterForServiceTest(.40f,.40f),"tanks set partly full for the fill test");
            float floodBefore=s.FloodWaterLiters,washBefore=s.WashWaterLiters;
            Check(s.TryDismount(),"operator steps off in the bay");
            typeof(IceWaterService).GetMethod("BeginWalk",flags).Invoke(service,null);
            // Stand beside the socket, outside the machine, and look at it: E/click targets this port.
            Vector3 socketLocal=m.InverseTransformPoint(port.hoseSocket.position);
            Vector3 stand=m.TransformPoint(new Vector3(socketLocal.x>=0?1.75f:-1.75f,0,socketLocal.z));stand.y=0;
            typeof(IceWaterService).GetField("footPosition",flags).SetValue(service,stand);
            var cam=game.DriverCamera.transform;cam.position=stand+Vector3.up*1.68f;cam.LookAt(port.hoseSocket.position);
            object target=typeof(IceWaterService).GetMethod("FindTarget",flags).Invoke(service,new object[]{null});
            Check(target.ToString()==(port.port==WaterFillPort.Flood?"Flood":"Wash"),"E/click aimed at the socket selects "+port.Label+" ("+target+")");
            float reach=Vector3.Distance(stand+Vector3.up*1.15f,port.hoseSocket.position);
            Check(reach<=Simulation.ServiceReachMeters,"within operator reach from beside the machine ("+reach.ToString("0.00")+" m)");
            Check(s.TakeHose(0),"nozzle taken from the holder");
            typeof(IceWaterService).GetMethod("Interact",flags).Invoke(service,new object[]{target});
            Check(s.HoseState==(port.port==WaterFillPort.Flood?WaterHoseState.Flood:WaterHoseState.Wash),"Interact couples the hose to "+port.Label+": "+s.LastMessage);
            service.UpdateViewAndEffects(true,.02f);
            var nozzle=service.HoseNozzle;
            Check(Vector3.Distance(nozzle.position,port.hoseSocket.position)<.0005f&&Quaternion.Angle(nozzle.rotation,port.hoseSocket.rotation)<.05f,"coupled nozzle sits on the socket in the socket's orientation");
            Check(service.HoseVisibleLength<=Simulation.ServiceHoseLengthMeters,"routed hose "+service.HoseVisibleLength.ToString("0.00")+" m within the 11 m hose");
            var hose=game.GetComponentsInChildren<LineRenderer>(true).Single(l=>l.name=="Live service hose");
            Check(Vector3.Distance(hose.GetPosition(hose.positionCount-1),port.hoseSocket.position)<.0005f,"the live hose ends at the socket");
            Check(s.SetSupplyValve(1,0),"supply opened");
            var gauge=w.Machine.GetComponentsInChildren<IceWaterLevelGauge>(true).Single(g=>g.tank==port.port);
            float gaugeBefore=gauge.indicator.localScale.y;
            for(int i=0;i<200;i++){s.Step(.02f);service.UpdateViewAndEffects(true,.02f);}
            float level=port.port==WaterFillPort.Flood?s.FloodWaterLiters:s.WashWaterLiters,before=port.port==WaterFillPort.Flood?floodBefore:washBefore;
            Check(level>before+20,port.Label+" tank fills ("+before.ToString("0")+" -> "+level.ToString("0")+" L)");
            Check(gauge.indicator.localScale.y>gaugeBefore,"the "+port.Label+" gauge rises");
            float fraction=level/(port.port==WaterFillPort.Flood?s.Config.FloodCapacityLiters:s.Config.WashCapacityLiters);
            Check(Mathf.Abs(gauge.indicator.localScale.y/ FullScale(gauge) - fraction)<.002f,"gauge height matches the tank ("+(fraction*100).ToString("0")+"%)");
            for(int i=0;i<4000&&s.ServiceOverflowLitersPerSecond<=0;i++){s.Step(.02f);}
            service.UpdateViewAndEffects(true,.02f);
            Check(s.ServiceOverflowLitersPerSecond>0,"filling to capacity overflows");
            var outlet=(Transform)typeof(IceWaterService).GetField("overflowOutlet",flags).GetValue(service);
            Check(Vector3.Distance(outlet.position,port.overflowSocket.position)<.0005f,"overflow water leaves at the authored overflow socket");
            double balance=s.TotalServiceSuppliedLiters-s.TotalServiceStoredLiters-s.TotalServiceDrainLiters;
            // Tank litres are single precision (unchanged core): each 20 ms step can round the tank level by at
            // most one float ulp, which max(0, supplied - stored) does not count. Allow exactly that bound.
            float capacity=port.port==WaterFillPort.Flood?s.Config.FloodCapacityLiters:s.Config.WashCapacityLiters;
            double steps=s.TotalServiceSuppliedLiters/(Simulation.ServiceMaximumFlowLitersPerSecond*.02);
            double bound=Math.Max(1e-4,steps*capacity*Math.Pow(2,-23));
            Check(Math.Abs(balance)<=bound,"water conserved: supplied "+s.TotalServiceSuppliedLiters.ToString("0.0")+" L = stored + drained/overflowed (residual "+
                balance.ToString("0.0000")+" L; single-precision bound "+bound.ToString("0.0000")+" L)");
            Capture((moved?"Moved-":"After-")+"Play-Overflow-"+(port.port==WaterFillPort.Flood?"Flood":"Wash"),m,new Vector3(3.6f,1.7f,socketLocal.z-.6f),socketLocal,42,false);
            Check(s.SetSupplyValve(0,0)&&s.DisconnectHose(0),"supply closed and hose disconnected");
            // Breakaway uses the socket: drive away and the nozzle drops toward the wall reel.
            Check(s.ReturnHose(0)||s.HoseState==WaterHoseState.Carried,"nozzle in hand after disconnecting");
        }

        static readonly Dictionary<IceWaterLevelGauge,float> fulls=new Dictionary<IceWaterLevelGauge,float>();
        static float FullScale(IceWaterLevelGauge g)
        {
            if(!fulls.TryGetValue(g,out float f)){var source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(g.indicator);f=source!=null?source.localScale.y:.27f;fulls[g]=f;}
            return f;
        }

        // ---- Helpers ----------------------------------------------------------------------------

        static Dictionary<string,string> MachineDump()
        {
            var rig=UnityEngine.Object.FindAnyObjectByType<IceMachineRig>(FindObjectsInactive.Include);
            var d=new Dictionary<string,string>();
            foreach(var t in rig.GetComponentsInChildren<Transform>(true))
            {
                string p=t.name;var x=t;while(x.parent!=null&&x!=rig.transform){x=x.parent;p=x.name+"/"+p;}
                p+="#"+t.GetSiblingIndexPath();
                var f=t.GetComponent<MeshFilter>();var r=t.GetComponent<Renderer>();
                d[p]=t.localPosition.ToString("F5")+t.localRotation.ToString("F5")+t.localScale.ToString("F5")+t.gameObject.activeSelf+(f!=null&&f.sharedMesh!=null?f.sharedMesh.name:"")+(r!=null?string.Join(",",r.sharedMaterials.Select(mm=>mm!=null?mm.name:"null")):"");
            }
            return d;
        }

        static void Capture(string label,Transform machine,Vector3 fromLocal,Vector3 atLocal,float fov,bool hideArena=false)
        {
            var node=new GameObject("Fill port capture"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var cam=node.AddComponent<Camera>();cam.fieldOfView=fov;cam.nearClipPlane=.03f;cam.farClipPlane=100;
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.09f,.13f,.18f);
                node.transform.position=machine.TransformPoint(fromLocal);node.transform.LookAt(machine.TransformPoint(atLocal));
                Directory.CreateDirectory(Out+"Shots");
                for(int i=0;i<2;i++)IceResurfacerEditor.Capture(cam,"RinkAndFillPorts/Shots/warm.png",320,180);
                IceResurfacerEditor.Capture(cam,"RinkAndFillPorts/Shots/"+label+".png",1600,900);
                File.Delete(Out+"Shots/warm.png");
            }
            finally{UnityEngine.Object.DestroyImmediate(node);}
        }

        static IceMachineRig OpenScene()
        {
            RequireIsolated();
            EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
            return UnityEngine.Object.FindAnyObjectByType<IceMachineRig>(FindObjectsInactive.Include);
        }
        static IceResurfacerGame Game(){ return UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>(FindObjectsInactive.Include); }
        static string Hash(string path){ using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
        static void RequireIsolated()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))
                throw new InvalidOperationException("This validation writes assets; run it only in the isolated StageUnityValidation.ps1 project.");
        }
        static void Begin(string label,Action body)
        {
            SessionState.SetString(ResultsKey,label);
            Directory.CreateDirectory(Out);File.WriteAllText(ResultsPath,"");errors=0;
            Run(body);
        }
        static void Start(string step){ File.AppendAllText(ResultsPath,"== "+step+" / "+DateTime.Now.ToString("s")+"\n"); }
        static void Info(string message){ File.AppendAllText(ResultsPath,"INFO "+message+"\n"); }
        static void Check(bool valid,string message)
        {
            if(!valid)throw new InvalidOperationException("Rink/fill ports: "+message);
            File.AppendAllText(ResultsPath,"PASS "+message+"\n");
        }
        static void Run(Action body)
        {
            try{body();}
            catch(Exception e)
            {
                Debug.LogException(e);SessionState.SetString(StageKey,"");EditorApplication.update-=Tick;
                try{File.AppendAllText(ResultsPath,"FAIL "+e.Message+"\n"+e.StackTrace+"\n");}catch{}
                if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
                Exit(1);
            }
        }
        static void Exit(int code){ Debug.Log("AURORA RINK/FILL PORTS exit="+code);EditorApplication.Exit(code); }
    }

    static class SiblingPath
    {
        public static string GetSiblingIndexPath(this Transform t)
        {
            var sb=new StringBuilder();
            while(t!=null){sb.Insert(0,t.GetSiblingIndex()+".");t=t.parent;}
            return sb.ToString();
        }
    }
}
