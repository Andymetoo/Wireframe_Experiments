using System;
using System.Collections.Generic;
using System.Linq;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// Persistent machine water fill ports: the one-time migration into AuroraR54.prefab, and the
    /// reachability analysis behind the fill-port Inspector and Scene gizmos. The analysis places the
    /// machine at its parking pose in the service bay and uses the water service's own reach, hose
    /// length, routing and footprint, so a warning here is what Play would do.
    /// </summary>
    public static class IceFillPorts
    {
        public const string PrefabPath="Assets/Experiments/IceResurfacer/Machine/AuroraR54.prefab";
        const string MaterialFolder="Assets/Experiments/IceResurfacer/Machine/Materials/";
        public const string GroupName="Water Fill Connections";

        // ---- Analysis ---------------------------------------------------------------------------

        public sealed class Report
        {
            public Vector3 SocketLocal, OverflowLocal, StandLocal, AnchorLocal;
            public float Reach=float.PositiveInfinity, RouteLength=float.PositiveInfinity, Straight;
            public Vector3[] Route=new Vector3[16];public int RoutePoints;
            public IceWaterService.Footprint Footprint;
            public string NozzleObstruction, OverflowObstruction;
            public float NozzleOutward;
            public readonly List<KeyValuePair<MessageType,string>> Findings=new List<KeyValuePair<MessageType,string>>();
            public bool Usable { get { return !Findings.Exists(f=>f.Key==MessageType.Error); } }
        }

        static SimulatorConfig Config(Component part,out IceResurfacerGame game)
        {
            var rig=part.GetComponentInParent<IceMachineRig>();
            var context=IceCalibrationContext.Resolve(part,rig!=null?rig.BakedConfigJson:null);
            game=context.Writable?context.Game:null;
            return context.Config;
        }

        /// <summary>Reach, hose and clearance at the service-bay parking pose. Positions are machine space.</summary>
        public static Report Analyze(IceWaterFillPort port)
        {
            var r=new Report();
            var rig=port.GetComponentInParent<IceMachineRig>();
            if(rig==null){r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Error,"Not part of the authored machine (no IceMachineRig above it)."));return r;}
            if(!port.IsComplete){r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Error,"Assign the hose and overflow sockets."));return r;}
            Transform machine=rig.Get("Machine"),conditioner=rig.Get("Conditioner");
            var c=Config(port,out var game);var layout=new ServiceLayout(c);
            layout.ParkingPose(out float px,out float pz,out float heading);
            Matrix4x4 parked=Matrix4x4.TRS(new Vector3(px,0,pz),Quaternion.Euler(0,heading*Mathf.Rad2Deg,0),Vector3.one);
            r.SocketLocal=machine.InverseTransformPoint(port.hoseSocket.position);
            r.OverflowLocal=machine.InverseTransformPoint(port.overflowSocket.position);
            r.Footprint=IceWaterService.Footprint.Enclosing(machine,machine,conditioner);
            Vector3 socket=parked.MultiplyPoint3x4(r.SocketLocal);

            // Operator: the nearest floor spot outside the machine's keep-out box, eyes/hands at 1.15 m.
            for(float x=-r.Footprint.HalfWidth-.6f;x<=r.Footprint.HalfWidth+.6f;x+=.05f)
                for(float z=-r.Footprint.Rear-.6f;z<=r.Footprint.Front+.6f;z+=.05f)
                {
                    var local=new Vector3(x,0,z);
                    if(r.Footprint.Contains(local))continue;
                    Vector3 g=parked.MultiplyPoint3x4(local);
                    if(!layout.ContainsFloor(g.x,g.z,.22f))continue;
                    float d=Vector3.Distance(g+Vector3.up*1.15f,socket);
                    if(d<r.Reach){r.Reach=d;r.StandLocal=local;}
                }
            if(r.Reach>Simulation.ServiceReachMeters)
                r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Error,"Out of reach: from the closest floor spot beside the parked machine the socket is "+
                    (float.IsPositiveInfinity(r.Reach)?"unreachable":r.Reach.ToString("0.00")+" m")+" from the operator's hands (reach "+Simulation.ServiceReachMeters.ToString("0.0")+" m). Move the socket outward or lower."));
            else r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Info,"Operator reach "+r.Reach.ToString("0.00")+" of "+Simulation.ServiceReachMeters.ToString("0.0")+" m from the floor beside the machine."));

            // Hose: the same route and the same breakaway rule as Play.
            Matrix4x4 station=IceWaterService.GeneratedStation(c);
            if(game!=null){var authored=IceArenaRig.FindPart(game,IceArenaRole.WaterStation);if(authored!=null)station=game.transform.worldToLocalMatrix*authored.localToWorldMatrix;}
            IceWaterService.HoseAnchor(station,c,out Vector3 anchor,out Vector3 groundStart);
            r.AnchorLocal=parked.inverse.MultiplyPoint3x4(anchor);
            r.RoutePoints=IceWaterService.Route(parked,anchor,groundStart,socket,true,r.Footprint,r.Route,out r.RouteLength);
            for(int i=0;i<r.RoutePoints;i++)r.Route[i]=parked.inverse.MultiplyPoint3x4(r.Route[i]);
            layout.SupplyAnchor(out float ax,out float az);
            r.Straight=new Vector2(socket.x-ax,socket.z-az).magnitude+.9f;
            if(float.IsPositiveInfinity(r.RouteLength))r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Error,"No hose route around the machine reaches this socket."));
            else if(r.RouteLength>Simulation.ServiceHoseLengthMeters||r.Straight>Simulation.ServiceHoseLengthMeters)
                r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Error,"Hose too short: routed "+r.RouteLength.ToString("0.0")+" m (limit "+Simulation.ServiceHoseLengthMeters.ToString("0")+" m) - the coupling would break away when parked."));
            else r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Info,"Hose routed "+r.RouteLength.ToString("0.0")+" of "+Simulation.ServiceHoseLengthMeters.ToString("0")+" m from the wall reel ("+(Simulation.ServiceHoseLengthMeters-r.RouteLength).ToString("0.0")+" m slack)."));

            // Nozzle direction: it should point away from the machine side the hose drops to.
            Vector3 drop=IceWaterService.HoseDrop(r.SocketLocal,r.Footprint);
            Vector3 outward=drop-new Vector3(r.SocketLocal.x,0,r.SocketLocal.z);outward.y=0;
            Vector3 axis=machine.InverseTransformDirection(port.hoseSocket.right);
            r.NozzleOutward=outward.sqrMagnitude>1e-6f?Vector3.Dot(axis.normalized,outward.normalized):1;
            if(r.NozzleOutward<.2f)r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Warning,"The coupled nozzle (socket +X, red) points "+(r.NozzleOutward<-.2f?"into":"along")+" the machine; the hose drops on the other side. Rotate the socket so red points outward."));

            // Clearance against the machine's own meshes.
            var colliders=new List<MeshCollider>();
            try
            {
                foreach(var mr in machine.GetComponentsInChildren<MeshRenderer>(true).Concat(conditioner!=null?conditioner.GetComponentsInChildren<MeshRenderer>(true):new MeshRenderer[0]))
                {
                    if(mr.transform.IsChildOf(port.transform)||!mr.enabled)continue;
                    var f=mr.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null||mr.GetComponent<TextMesh>()!=null)continue;
                    var mc=mr.gameObject.AddComponent<MeshCollider>();mc.hideFlags=HideFlags.HideAndDontSave;mc.sharedMesh=f.sharedMesh;colliders.Add(mc);
                }
                Physics.SyncTransforms();
                Vector3 s=port.hoseSocket.position,dir=port.hoseSocket.right;
                // The game's reach is distance only; warn when the hand would have to pass through the machine.
                Vector3 hand=machine.TransformPoint(r.StandLocal)+machine.up*1.15f,toSocket=s-hand;
                if(!float.IsPositiveInfinity(r.Reach)&&Physics.Raycast(hand,toSocket.normalized,out var block,Mathf.Max(0,toSocket.magnitude-.04f)))
                    r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Warning,"The operator would reach through "+block.transform.name+" to get to this socket. Play allows it (reach is distance only), but it is not a believable place to couple a hose."));
                if(Physics.Raycast(s,dir,out var hit,.25f))r.NozzleObstruction=hit.transform.name;
                else if(Physics.CheckSphere(s+dir*.12f,.03f))r.NozzleObstruction="a nearby part";
                if(r.NozzleObstruction!=null)r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Warning,"The coupled nozzle (0.25 m along red) would pass through "+r.NozzleObstruction+"."));
                if(Physics.Raycast(port.overflowSocket.position,Vector3.down,out var spill,3f)&&spill.distance<port.overflowSocket.position.y-machine.position.y-.05f)
                {r.OverflowObstruction=spill.transform.name;r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Info,"Overflow falls onto "+spill.transform.name+" before the floor."));}
            }
            finally{foreach(var mc in colliders)UnityEngine.Object.DestroyImmediate(mc);}
            if(r.OverflowLocal.y>r.SocketLocal.y+.05f)r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Warning,"The overflow socket is above the hose socket; overflow normally leaves below the filler."));
            if(Vector3.Distance(r.OverflowLocal,r.SocketLocal)>.8f)r.Findings.Add(new KeyValuePair<MessageType,string>(MessageType.Warning,"The overflow socket is "+Vector3.Distance(r.OverflowLocal,r.SocketLocal).ToString("0.00")+" m from the hose socket."));
            return r;
        }

        // ---- Migration --------------------------------------------------------------------------

        public static string Status(out bool present)
        {
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var ports=root.GetComponentsInChildren<IceWaterFillPort>(true);var gauges=root.GetComponentsInChildren<IceWaterLevelGauge>(true);
                present=ports.Length>0||gauges.Length>0;
                return present?ports.Length+" fill port(s) and "+gauges.Length+" gauge(s) are already part of the prefab.":"The fill ports and gauges are generated at Play; they are not in the prefab yet.";
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        /// <summary>Adds the fill ports and gauges to the existing machine prefab at exactly the place
        /// and shape Play generates today. Existing fittings (with the components, or generated-named
        /// objects you added by hand) are adopted, never duplicated. Backed up first.</summary>
        public static string Migrate()
        {
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            var log=new List<string>();
            try
            {
                var rig=root.GetComponentInChildren<IceMachineRig>(true);
                var machine=rig!=null?rig.Get("Machine"):null;
                if(machine==null)throw new InvalidOperationException("AuroraR54.prefab has no bound machine root.");
                var ports=root.GetComponentsInChildren<IceWaterFillPort>(true).ToList();
                var gauges=root.GetComponentsInChildren<IceWaterLevelGauge>(true).ToList();
                bool Has(WaterFillPort kind){return ports.Exists(p=>p.port==kind);}
                bool HasGauge(WaterFillPort kind){return gauges.Exists(g=>g.tank==kind);}
                if(Has(WaterFillPort.Flood)&&Has(WaterFillPort.Wash)&&HasGauge(WaterFillPort.Flood)&&HasGauge(WaterFillPort.Wash))return "The prefab already has both fill ports and both gauges; nothing changed.";

                var newMaterials=new List<string>();
                Material Mat(string name,Color color,float metallic,float smooth)
                {
                    string path=MaterialFolder+name+".mat";
                    var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
                    var template=AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+"Aurora midnight blue.mat");
                    var m=new Material(template){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
                    newMaterials.Add(path);
                    return m;
                }
                Material Existing(string name){var m=AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+name+".mat");if(m==null)throw new InvalidOperationException("Machine material missing: "+name);return m;}
                var steel=Existing("Brushed stainless steel");var rubber=Existing("Tire and hydraulic rubber");
                var blue=Mat("Flood water inlet - blue",new Color(.075f,.36f,.62f),.3f,.52f);
                var teal=Mat("Wash inlet - teal",new Color(.08f,.57f,.49f),.3f,.52f);
                var sample=root.GetComponentInChildren<TextMesh>(true);
                Font font=sample!=null?sample.font:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Material letters=sample!=null?sample.GetComponent<MeshRenderer>().sharedMaterial:font.material;

                string backup=IceAuthoredRecovery.BackUp("Before persistent water fill ports",new[]{PrefabPath});
                log.Add("Backup: "+backup);
                foreach(string path in newMaterials){AssetDatabase.CreateAsset(AssetDatabase.LoadAssetAtPath<Material>(path)??NewMaterial(path,blue,teal),path);log.Add("Created "+path);}

                var group=machine.Find(GroupName);
                if(group==null){group=new GameObject(GroupName).transform;group.SetParent(machine,false);log.Add("Added "+GroupName);}
                var b=new Parts(font,letters);
                foreach(var kind in new[]{WaterFillPort.Flood,WaterFillPort.Wash})
                {
                    string label=kind==WaterFillPort.Flood?"FLOOD WATER":"WASH WATER";
                    float z=kind==WaterFillPort.Flood?Simulation.FloodFillLocalZ:Simulation.WashFillLocalZ;
                    float height=kind==WaterFillPort.Flood?Simulation.FloodFillHeight:Simulation.WashFillHeight;
                    var material=kind==WaterFillPort.Flood?AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+"Flood water inlet - blue.mat"):AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+"Wash inlet - teal.mat");
                    if(!Has(kind))
                    {
                        // A fitting you added by hand with the generated name is adopted, not duplicated.
                        var manual=machine.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==label+" fill coupling");
                        var port=(manual!=null?manual.gameObject:null)?.AddComponent<IceWaterFillPort>();
                        if(port==null)
                        {
                            var p=new GameObject(kind==WaterFillPort.Flood?"Flood Fill Port":"Wash Fill Port").transform;
                            p.SetParent(group,false);p.localPosition=new Vector3(Simulation.FillPortLocalX,height,z);
                            port=p.gameObject.AddComponent<IceWaterFillPort>();
                            var visual=new GameObject("Visual Assembly").transform;visual.SetParent(p,false);
                            b.Box("Fill-port mounting plate",visual,new Vector3(-.015f,0,0),new Vector3(.025f,.26f,.32f),material);
                            b.Cylinder("Fill neck",visual,new Vector3(.035f,0,0),.11f,.09f,steel,Vector3.right);
                            b.Cylinder("Fill inlet dark aperture",visual,new Vector3(.084f,0,0),.074f,.006f,rubber,Vector3.right);
                            for(int i=-1;i<=1;i+=2)b.Box("Bayonet inlet lug",visual,new Vector3(.07f,0,i*.059f),new Vector3(.027f,.027f,.035f),steel);
                            b.Beam("Overflow standpipe",visual,new Vector3(-.01f,-.07f,.13f),new Vector3(.11f,-.25f,.13f),.027f,steel);
                            b.Label(label,visual,new Vector3(.025f,.22f,0),.053f,Quaternion.Euler(0,90,0));
                            b.Label("E  INTERACT",visual,new Vector3(.025f,.15f,0),.034f,Quaternion.Euler(0,90,0));
                            port.visual=visual;
                            log.Add("Added "+p.name+" at machine "+p.localPosition.ToString("F3"));
                        }
                        else log.Add("Adopted your existing "+manual.name+" as the "+label+" fill port");
                        port.port=kind;
                        port.hoseSocket=Socket(port.transform,label+" hose attachment","Hose Connection Socket",new Vector3(.14f,0,0));
                        port.overflowSocket=Socket(port.transform,label+" overflow discharge","Overflow Discharge Socket",new Vector3(.11f,-.25f,.13f));
                    }
                    if(!HasGauge(kind))
                    {
                        var g=new GameObject(kind==WaterFillPort.Flood?"Flood Tank Level Gauge":"Wash Tank Level Gauge").transform;
                        g.SetParent(group,false);g.localPosition=new Vector3(Simulation.FillPortLocalX+.01f,kind==WaterFillPort.Flood?.80f:1.06f,z);
                        var gauge=g.gameObject.AddComponent<IceWaterLevelGauge>();gauge.tank=kind;
                        b.Box("Level gauge backing",g,Vector3.zero,new Vector3(.018f,.31f,.075f),rubber);
                        gauge.indicator=b.Box("Water level indicator (full)",g,Vector3.zero,new Vector3(.024f,.27f,.043f),material);
                        gauge.indicator.gameObject.AddComponent<IceKeepUnbatched>();
                        for(int i=0;i<5;i++)b.Box("Level graduation",g,new Vector3(.02f,-.135f+i*.0675f,.042f),new Vector3(.008f,.008f,.029f),steel);
                        log.Add("Added "+g.name+" at machine "+g.localPosition.ToString("F3"));
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                AssetDatabase.SaveAssets();
                log.Add("Saved "+PrefabPath+" (GUID unchanged). Play now binds these instead of generating its own.");
                return string.Join("\n",log);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        static Material NewMaterial(string path,Material blue,Material teal){ return path.Contains("Flood")?blue:teal; }

        static Transform Socket(Transform port,string generatedName,string name,Vector3 position)
        {
            var existing=port.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==generatedName||t.name==name);
            if(existing!=null)return existing;
            var s=new GameObject(name).transform;s.SetParent(port,false);s.localPosition=position;return s;
        }

        sealed class Parts
        {
            readonly Font font;readonly Material letters;
            public Parts(Font font,Material letters){this.font=font;this.letters=letters;}
            public Transform Box(string name,Transform parent,Vector3 position,Vector3 size,Material material){return Primitive(PrimitiveType.Cube,name,parent,position,Quaternion.identity,size,material);}
            public Transform Cylinder(string name,Transform parent,Vector3 position,float diameter,float length,Material material,Vector3 axis)
            {return Primitive(PrimitiveType.Cylinder,name,parent,position,Quaternion.FromToRotation(Vector3.up,axis),new Vector3(diameter,length*.5f,diameter),material);}
            public void Beam(string name,Transform parent,Vector3 a,Vector3 b,float diameter,Material material){Cylinder(name,parent,(a+b)*.5f,diameter,(b-a).magnitude,material,(b-a).normalized);}
            public void Label(string text,Transform parent,Vector3 position,float size,Quaternion rotation)
            {
                var go=new GameObject(text);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation*Quaternion.Euler(0,180,0);
                var t=go.AddComponent<TextMesh>();t.font=font;t.fontSize=64;t.characterSize=size*10f/64f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.text=text;t.color=Color.white;
                go.GetComponent<MeshRenderer>().sharedMaterial=letters;
            }
            static Transform Primitive(PrimitiveType type,string name,Transform parent,Vector3 position,Quaternion rotation,Vector3 scale,Material material)
            {
                var go=GameObject.CreatePrimitive(type);go.name=name;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;go.transform.localScale=scale;
                go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
            }
        }

        [MenuItem("Experiments/Ice Resurfacer/Authored Scene/Add Persistent Water Fill Ports to Machine...")]
        static void Menu()
        {
            string status=Status(out bool present);
            if(present){EditorUtility.DisplayDialog("Water fill ports",status,"OK");return;}
            if(!EditorUtility.DisplayDialog("Add persistent water fill ports",status+"\n\nAdd the FLOOD WATER and WASH WATER fill ports and both tank level gauges to Machine/AuroraR54.prefab, "+
                "exactly where Play generates them today? Nothing else in the machine changes. The prefab is backed up first.","Add","Cancel"))return;
            try{Debug.Log(Migrate());}
            catch(Exception e){EditorUtility.DisplayDialog("Not added",e.Message,"OK");}
        }
    }

    [CustomEditor(typeof(IceWaterFillPort))]
    public sealed class IceWaterFillPortEditor : UnityEditor.Editor
    {
        IceFillPorts.Report report;double analysed;

        IceFillPorts.Report Current()
        {
            if(report==null||EditorApplication.timeSinceStartup-analysed>.5)
            {report=IceFillPorts.Analyze((IceWaterFillPort)target);analysed=EditorApplication.timeSinceStartup;}
            return report;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if(Application.isPlaying){EditorGUILayout.HelpBox("Play uses these sockets live.",MessageType.None);return;}
            var r=Current();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("SERVICE CHECK (machine parked in the bay)",EditorStyles.boldLabel);
            foreach(var f in r.Findings)EditorGUILayout.HelpBox(f.Value,f.Key);
            EditorGUILayout.LabelField("Socket (machine space)",r.SocketLocal.ToString("F3"));
            EditorGUILayout.LabelField("Overflow (machine space)",r.OverflowLocal.ToString("F3"));
            EditorGUILayout.HelpBox("Scene view: cyan sphere = socket and click target; red = coupled nozzle; blue = overflow; green = where the operator stands; yellow = hose route from the wall reel (at the parked pose); grey = machine keep-out box.",MessageType.None);
        }

        void OnSceneGUI()
        {
            var port=(IceWaterFillPort)target;
            if(!port.IsComplete||Application.isPlaying)return;
            var rig=port.GetComponentInParent<IceMachineRig>();if(rig==null)return;
            Transform machine=rig.Get("Machine");var r=Current();
            Vector3 W(Vector3 local)=>machine.TransformPoint(local);
            Vector3 s=port.hoseSocket.position;
            Handles.color=Color.cyan;Handles.SphereHandleCap(0,s,Quaternion.identity,.05f,EventType.Repaint);
            Handles.DrawWireDisc(s,SceneView.currentDrawingSceneView!=null?SceneView.currentDrawingSceneView.camera.transform.forward:Vector3.up,.38f);
            Handles.Label(s+Vector3.up*.1f,port.Label+" socket");
            Handles.color=Color.red;Handles.ArrowHandleCap(0,s,Quaternion.LookRotation(port.hoseSocket.right),.25f,EventType.Repaint);
            Handles.color=new Color(.3f,.5f,1);Handles.ArrowHandleCap(0,port.overflowSocket.position,Quaternion.LookRotation(Vector3.down),.25f,EventType.Repaint);
            Handles.color=r.Reach<=Simulation.ServiceReachMeters?Color.green:Color.red;
            Vector3 hand=W(r.StandLocal)+machine.up*1.15f;
            Handles.DrawDottedLine(W(r.StandLocal),hand,3);Handles.DrawLine(hand,s,2);
            Handles.Label((hand+s)*.5f,"reach "+r.Reach.ToString("0.00")+" m");
            Handles.color=r.RouteLength<=Simulation.ServiceHoseLengthMeters?Color.yellow:Color.red;
            for(int i=1;i<r.RoutePoints;i++)Handles.DrawLine(W(r.Route[i-1]),W(r.Route[i]),2);
            if(r.RoutePoints>0)Handles.Label(W(r.Route[0])+Vector3.up*.2f,"hose "+r.RouteLength.ToString("0.0")+" / "+Simulation.ServiceHoseLengthMeters.ToString("0")+" m");
            var f=r.Footprint;Handles.color=new Color(.7f,.7f,.7f,.6f);
            Handles.DrawPolyLine(W(new Vector3(-f.HalfWidth,0,-f.Rear)),W(new Vector3(f.HalfWidth,0,-f.Rear)),W(new Vector3(f.HalfWidth,0,f.Front)),W(new Vector3(-f.HalfWidth,0,f.Front)),W(new Vector3(-f.HalfWidth,0,-f.Rear)));
        }
    }

    [CustomEditor(typeof(IceWaterLevelGauge))]
    public sealed class IceWaterLevelGaugeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var g=(IceWaterLevelGauge)target;
            if(!g.IsComplete)EditorGUILayout.HelpBox("Assign the indicator (a child of this gauge). Its authored size is the full-tank level.",MessageType.Error);
            else if(g.indicator.GetComponentInParent<IceKeepUnbatched>(true)==null)
            {
                EditorGUILayout.HelpBox("The indicator is not excluded from runtime batching, so it could not move in Play.",MessageType.Warning);
                if(GUILayout.Button("Keep the indicator unbatched")){Undo.AddComponent<IceKeepUnbatched>(g.indicator.gameObject);}
            }
            else EditorGUILayout.HelpBox("Play scales the indicator from its base: authored size = full tank.",MessageType.None);
        }
    }
}
