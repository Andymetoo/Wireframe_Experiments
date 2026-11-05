using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>Read-only survey of the authored machine (isolated project only).</summary>
    public static class IceMechanicalSurvey
    {
        const string Out="Assets/Experiments/IceResurfacer/Validation~/MechanicalReconciliation/";

        public static void Survey()
        {
            int code=0;
            try
            {
                if(!Application.dataPath.Replace('\\','/').Contains("/Validation~/UnityProject/Assets"))throw new InvalidOperationException("isolated only");
                Directory.CreateDirectory(Out);
                EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
                var rig=UnityEngine.Object.FindAnyObjectByType<IceMachineRig>(FindObjectsInactive.Include);
                var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>(FindObjectsInactive.Include);
                var w=rig.Collect();var c=game.calibration;
                var sb=new StringBuilder();
                sb.AppendLine("calibration "+JsonUtility.ToJson(c));
                sb.AppendLine("rig root "+Path(rig.transform,null)+" machine="+Path(w.Machine,rig.transform.root));
                sb.AppendLine("rig world pos "+V(rig.transform.position)+" rot "+V(rig.transform.eulerAngles)+" scale "+V(rig.transform.lossyScale));
                sb.AppendLine("machine world pos "+V(w.Machine.position)+" rot "+V(w.Machine.eulerAngles));
                // Overrides
                sb.AppendLine("== scene overrides on machine instance");
                foreach(var mod in PrefabUtility.GetPropertyModifications(rig.gameObject)??new PropertyModification[0])
                {
                    var t=mod.target as Component; var g=mod.target as GameObject;
                    string who=t!=null?Path(t.transform,null)+" ["+t.GetType().Name+"]":g!=null?Path(g.transform,null)+" [GameObject]":mod.target?.name;
                    sb.AppendLine("  "+who+" ."+mod.propertyPath+" = "+mod.value+(mod.objectReference!=null?" ref "+mod.objectReference.name:""));
                }
                foreach(var added in PrefabUtility.GetAddedComponents(rig.gameObject))sb.AppendLine("  ADDED COMPONENT "+added.instanceComponent.GetType().Name+" on "+Path(added.instanceComponent.transform,null));
                foreach(var added in PrefabUtility.GetAddedGameObjects(rig.gameObject))sb.AppendLine("  ADDED GO "+Path(added.instanceGameObject.transform,null));
                foreach(var removed in PrefabUtility.GetRemovedComponents(rig.gameObject))sb.AppendLine("  REMOVED COMPONENT "+removed.assetComponent.GetType().Name);
                // Scene-vs-prefab difference in machine space for every transform
                sb.AppendLine("== scene instance vs prefab asset (machine-space), differences > 0.1 mm / 0.05 deg / scale 1e-4");
                var asset=PrefabUtility.LoadPrefabContents(IceMachineBaker.PrefabPath);
                var assetRig=asset.GetComponentInChildren<IceMachineRig>(true);var aw=assetRig.Collect();
                int diffs=0;
                foreach(var t in rig.GetComponentsInChildren<Transform>(true))
                {
                    if(t==rig.transform)continue;
                    var a=PrefabUtility.GetCorrespondingObjectFromSource(t);
                    if(a==null){sb.AppendLine("  only in scene: "+Path(t,rig.transform));continue;}
                    bool pos=(t.localPosition-a.localPosition).magnitude>.0001f, rot=Quaternion.Angle(t.localRotation,a.localRotation)>.05f;
                    bool scale=(t.localScale-a.localScale).magnitude>1e-4f, act=t.gameObject.activeSelf!=a.gameObject.activeSelf;
                    if(pos||rot||scale||act)
                    { diffs++; sb.AppendLine("  "+Path(t,rig.transform)+(pos?" localPos "+V(a.localPosition,4)+" -> "+V(t.localPosition,4)+" d="+((t.localPosition-a.localPosition).magnitude*1000).ToString("F1")+"mm":"")+
                        (rot?" rot "+Quaternion.Angle(t.localRotation,a.localRotation).ToString("F2")+"deg":"")+(scale?" scale "+V(a.localScale,4)+"->"+V(t.localScale,4):"")+(act?" active "+a.gameObject.activeSelf+"->"+t.gameObject.activeSelf:"")); }
                }
                var rootSource=PrefabUtility.GetCorrespondingObjectFromSource(rig.transform);
                sb.AppendLine("  instance root localPos "+V(rig.transform.localPosition,4)+" (prefab root "+V(rootSource.localPosition,4)+")");
                sb.AppendLine("  total differing transforms: "+diffs);
                // Baseline dumps
                File.WriteAllText(Out+"baseline-prefab.tsv",Dump(asset.transform,aw.Machine));
                File.WriteAllText(Out+"baseline-scene.tsv",Dump(rig.transform,w.Machine));
                // Anchors (scene and asset)
                foreach(var pair in new[]{("SCENE",w),("PREFAB",aw)})
                {
                    sb.AppendLine("== anchors "+pair.Item1);
                    foreach(var an in IceMachineRig.MechanicalAnchors(pair.Item2,c))
                        sb.AppendLine("  "+an.Label+": actual "+V(an.Actual)+" expected "+V(an.Expected)+" err "+(Vector3.Distance(an.Actual,an.Expected)*1000).ToString("F1")+" mm");
                    Parts(sb,pair.Item2,c);
                }
                sb.AppendLine("== conditioner alignment (scene)");
                foreach(var l in IceConditionerAlignment.Measure(w,c).Lines)sb.AppendLine("  "+l);
                sb.AppendLine("== conditioner alignment (prefab)");
                foreach(var l in IceConditionerAlignment.Measure(aw,c).Lines)sb.AppendLine("  "+l);
                var brush=w.BrushArm.GetComponent<IceBoardBrushRig>();
                sb.AppendLine("== brush rig scene: "+(brush==null?"NONE":JsonUtility.ToJson(brush)));
                var abrush=aw.BrushArm.GetComponent<IceBoardBrushRig>();
                sb.AppendLine("== brush rig prefab: "+(abrush==null?"NONE":JsonUtility.ToJson(abrush)));
                PrefabUtility.UnloadPrefabContents(asset);
                Profile(sb,w,c);
                File.WriteAllText(Out+"survey.txt",sb.ToString());
                Shots("Before",w);
            }
            catch(Exception e){Debug.LogException(e);File.AppendAllText(Out+"survey.txt","FAIL "+e+"\n");code=1;}
            EditorApplication.Exit(code);
        }

        static void Parts(StringBuilder sb,IceWorld w,SimulatorConfig c)
        {
            Transform m=w.Machine;
            void P(string label,Transform t,Transform frame=null)
            {
                if(t==null){sb.AppendLine("  "+label+": null");return;}
                frame=frame??m;
                var r=t.GetComponentsInChildren<Renderer>(true);
                var b=IceConditionerAlignment.LocalBounds(r,frame);
                sb.AppendLine("  "+label+" ["+t.name+"] origin "+V(frame.InverseTransformPoint(t.position))+" lossy "+V(t.lossyScale)+" localScale "+V(t.localScale)+
                    " rotM "+V((Quaternion.Inverse(m.rotation)*t.rotation).eulerAngles)+" bounds "+V(b.min)+".."+V(b.max)+" parent "+(t.parent!=null?t.parent.name:"-"));
            }
            P("Machine",w.Machine,m);P("Conditioner",w.Conditioner);P("TankPivot",w.TankPivot);P("BodyTank",w.BodyTank);P("BodyTank(in pivot)",w.BodyTank,w.TankPivot);
            P("TankSnow",w.TankSnow);P("TankSnow(in pivot)",w.TankSnow,w.TankPivot);P("TankDoor",w.TankDoor);P("TankDoor(in pivot)",w.TankDoor,w.TankPivot);
            P("DumpOutlet",w.DumpOutlet);P("DumpOutlet(in pivot)",w.DumpOutlet,w.TankPivot);P("TankInspection",w.TankInspection);
            P("TankDischarge",w.TankDischarge);P("SnowOutlet",w.SnowOutlet);P("VerticalAuger",w.VerticalAuger);P("HorizontalAuger",w.HorizontalAuger);
            P("VerticalAuger.parent",w.VerticalAuger.parent);
            P("GuideRoller",w.GuideRoller);P("GuideRoller.parent",w.GuideRoller.parent);
            P("BrushArm",w.BrushArm);P("Brush",w.Brush);P("BrushLink",w.BrushLink);P("BrushRoller",w.BrushRoller);
            P("CuttingBlade",w.CuttingBlade,w.Conditioner);P("Towel",w.DistributionTowel,w.Conditioner);P("Housing",w.ConditionerHousing,w.Conditioner);
            P("WaterOutlet",w.WaterOutlet,w.Conditioner);P("WashPickup",w.WashPickup,w.Conditioner);P("ConditionerLeftEdge",w.ConditionerLeftEdge,w.Conditioner);
            P("Chassis",w.Chassis);P("EngineCover",w.EngineCover);
            for(int i=0;i<2;i++)
            {
                P("FrontWheel"+i,w.FrontWheels[i]);P("RearWheel"+i,w.RearWheels[i]);
                P("TankCylBase"+i,w.TankCylinderBase[i]);P("TankCylClevis"+i,w.TankCylinderClevis[i]);P("TankCylClevis(in pivot)"+i,w.TankCylinderClevis[i],w.TankPivot);
                P("CondCylBase"+i,w.ConditionerCylinderBase[i]);P("CondCylClevis"+i,w.ConditionerCylinderClevis[i]);P("CondCylClevis(in cond)"+i,w.ConditionerCylinderClevis[i],w.Conditioner);
            }
            var all=IceConditionerAlignment.LocalBounds(m.GetComponentsInChildren<Renderer>(true),m);
            sb.AppendLine("  machine total renderer bounds "+V(all.min)+".."+V(all.max));
            var tankAll=IceConditionerAlignment.LocalBounds(w.TankPivot.GetComponentsInChildren<Renderer>(true),m);
            sb.AppendLine("  tank-pivot subtree bounds (machine) "+V(tankAll.min)+".."+V(tankAll.max));
            var tankP=IceConditionerAlignment.LocalBounds(w.TankPivot.GetComponentsInChildren<Renderer>(true),w.TankPivot);
            sb.AppendLine("  tank-pivot subtree bounds (pivot) "+V(tankP.min)+".."+V(tankP.max));
            // Body envelope excluding tank subtree, conditioner and brush
            var body=new List<Renderer>();
            foreach(var r in m.GetComponentsInChildren<Renderer>(true))
                if(!r.transform.IsChildOf(w.Conditioner)&&!r.transform.IsChildOf(w.BrushArm)&&!r.transform.IsChildOf(w.TankPivot))body.Add(r);
            var bb=IceConditionerAlignment.LocalBounds(body,m);
            sb.AppendLine("  chassis/body bounds excluding tank/conditioner/brush "+V(bb.min)+".."+V(bb.max)+"  sim: x ±"+c.VehicleHalfWidth+" z "+(-c.BodyRear)+".."+c.BodyFront);
        }

        /// <summary>Left/right extent of the rigid body (not tank, conditioner, brush, guide roller) per 10 cm of z, below 1.2 m.</summary>
        static void Profile(StringBuilder sb,IceWorld w,SimulatorConfig c)
        {
            Transform m=w.Machine;
            sb.AppendLine("== lateral body profile (machine space, renderers below y 1.2 m; excluding tank, conditioner, brush, guide roller)");
            var mins=new float[40];var maxs=new float[40];var who=new string[40];
            for(int i=0;i<40;i++){mins[i]=0;maxs[i]=0;who[i]="";}
            foreach(var r in m.GetComponentsInChildren<Renderer>(true))
            {
                if(r.transform.IsChildOf(w.Conditioner)||r.transform.IsChildOf(w.BrushArm)||r.transform.IsChildOf(w.TankPivot)||r.transform.IsChildOf(w.GuideRoller.parent))continue;
                var b=IceConditionerAlignment.LocalBounds(new[]{r},m);
                if(b.size==Vector3.zero||b.min.y>1.2f)continue;
                for(int i=0;i<40;i++)
                {
                    float z0=-1.4f+i*.1f,z1=z0+.1f;
                    if(b.max.z<z0||b.min.z>z1)continue;
                    if(b.min.x<mins[i]){mins[i]=b.min.x;who[i]=r.name;}
                    maxs[i]=Mathf.Max(maxs[i],b.max.x);
                }
            }
            for(int i=0;i<40;i++)sb.AppendLine("  z "+(-1.4f+i*.1f).ToString("F1")+": x "+mins[i].ToString("F3")+" .. "+maxs[i].ToString("F3")+"  leftmost "+who[i]);
        }

        public static void Focus()
        {
            int code=0;
            try
            {
                EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
                var rig=UnityEngine.Object.FindAnyObjectByType<IceMachineRig>(FindObjectsInactive.Include);
                var w=rig.Collect();Transform m=w.Machine;
                Vector3 P(float x,float y,float z){return m.TransformPoint(new Vector3(x,y,z));}
                var conveyor=w.VerticalAuger.parent;
                var keep=new[]{w.TankPivot,conveyor};
                var hidden=new List<Renderer>();
                foreach(var r in rig.GetComponentsInChildren<Renderer>(true))
                {
                    bool k=false;foreach(var t in keep)k|=r.transform.IsChildOf(t);
                    if(!k&&r.enabled){r.enabled=false;hidden.Add(r);}
                }
                var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.transform.position=w.TankDischarge.position;marker.transform.localScale=Vector3.one*.06f;
                var mat=new Material(w.TankDoor.GetComponentInChildren<Renderer>().sharedMaterial);mat.color=Color.red;marker.GetComponent<Renderer>().sharedMaterial=mat;
                var views=new List<(string,Vector3,Vector3,float)>
                {
                    ("Focus-TankRear-FromBehind",P(0,1.3f,-6),P(0,1.3f,0),1.1f),
                    ("Focus-TankRear-FromInside",P(0,1.3f,2.0f),P(0,1.3f,-2),1.1f),
                    ("Focus-Inlet-Side",P(-5,1.3f,0),P(0,1.3f,0),.6f),
                    ("Focus-Inlet-Top",P(0,5,0),P(0,0,0),.6f),
                };
                RenderViews(m,views);
                UnityEngine.Object.DestroyImmediate(marker);
                // Inlet opening: rays from inside the tank toward the rear wall.
                var colliders=new List<MeshCollider>();
                foreach(var r in w.TankPivot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(r.transform.IsChildOf(w.TankSnow)||r.transform.IsChildOf(w.TankInspection))continue;
                    var f=r.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null)continue;
                    var mc=r.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=f.sharedMesh;colliders.Add(mc);
                }
                Physics.SyncTransforms();
                var sb=new StringBuilder("== rear wall open cells (machine x,y), ray from z 1.0 toward -z; first hit z\n");
                float minX=9,maxX=-9,minY=9,maxY=-9;int open=0;
                for(float y=.8f;y<=2.0f;y+=.01f)
                {
                    var line=new StringBuilder();
                    for(float x=-.85f;x<=.85f;x+=.01f)
                    {
                        Vector3 o=P(x,y,1.0f);
                        bool hit=Physics.Raycast(o,-m.forward,out var h,1.3f);
                        float hz=hit?m.InverseTransformPoint(h.point).z:-9;
                        bool isOpen=!hit||hz< -.05f;
                        line.Append(isOpen?'.':'#');
                        if(isOpen&&Mathf.Abs(x)<.5f&&y>1.0f&&y<1.95f){open++;minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
                    }
                    sb.AppendLine(y.ToString("F2")+" "+line);
                }
                sb.AppendLine("opening bounds (|x|<.5, 1<y<1.95): x "+minX.ToString("F3")+".."+maxX.ToString("F3")+" y "+minY.ToString("F3")+".."+maxY.ToString("F3")+" cells "+open);
                // Wall plane z at the opening centre
                if(Physics.Raycast(P(0,1.3f,1.0f),-m.forward,out var wall,2f))sb.AppendLine("rear wall inner face z at (0,1.3): "+m.InverseTransformPoint(wall.point).z.ToString("F4"));
                if(Physics.Raycast(P(0,1.3f,-1.0f),m.forward,out var wallOut,2f))sb.AppendLine("rear wall outer face z at (0,1.3): "+m.InverseTransformPoint(wallOut.point).z.ToString("F4"));
                File.WriteAllText(Out+"inlet.txt",sb.ToString());
                foreach(var mc in colliders)UnityEngine.Object.DestroyImmediate(mc);
                foreach(var r in hidden)r.enabled=true;
            }
            catch(Exception e){Debug.LogException(e);code=1;}
            EditorApplication.Exit(code);
        }

        /// <summary>Mesh-level interference of every moving assembly against the rigid machine, through
        /// its range of motion (convex hull of each moving part vs the exact static meshes).</summary>
        public static void Clearance()
        {
            int code=0;var sb=new StringBuilder();
            try
            {
                EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
                var rig=UnityEngine.Object.FindAnyObjectByType<IceMachineRig>(FindObjectsInactive.Include);
                var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>(FindObjectsInactive.Include);
                var w=rig.Collect();var c=game.calibration;
                sb.Append(ClearanceReport(w,c));
                File.WriteAllText(Out+"clearance.txt",sb.ToString());
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText(Out+"clearance.txt",sb+"\nFAIL "+e);code=1;}
            EditorApplication.Exit(code);
        }

        public static string ClearanceReport(IceWorld w,SimulatorConfig c)
        {
            var sb=new StringBuilder();
            Transform m=w.Machine;
            var moving=new Dictionary<string,Transform>{{"conditioner",w.Conditioner},{"brush",w.BrushArm},{"tank",w.TankPivot}};
            var cylinders=new HashSet<Transform>();
            foreach(var g in new[]{w.TankCylinderBarrels,w.TankCylinderRods,w.ConditionerCylinderBarrels,w.ConditionerCylinderRods})foreach(var t in g)cylinders.Add(t);
            var added=new List<Component>();
            var staticColliders=new List<(MeshCollider col,Renderer r)>();
            var movingColliders=new Dictionary<string,List<(MeshCollider col,Renderer r)>>();
            foreach(var key in moving.Keys)movingColliders[key]=new List<(MeshCollider,Renderer)>();
            try
            {
                foreach(var r in m.root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(!r.gameObject.activeInHierarchy||!r.transform.IsChildOf(rig(w)))continue;
                    var f=r.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null)continue;
                    string owner=null;foreach(var pair in moving)if(r.transform.IsChildOf(pair.Value))owner=pair.Key;
                    bool cylinder=false;foreach(var t in cylinders)cylinder|=r.transform.IsChildOf(t);
                    if(cylinder||r.transform.IsChildOf(w.TankSnow))continue;
                    var mc=r.gameObject.AddComponent<MeshCollider>();mc.hideFlags=HideFlags.HideAndDontSave;mc.sharedMesh=f.sharedMesh;
                    if(owner!=null){mc.convex=true;movingColliders[owner].Add((mc,r));}else staticColliders.Add((mc,r));
                    added.Add(mc);
                }
                var brushRig=w.BrushArm.GetComponent<IceBoardBrushRig>();
                foreach(var key in moving.Keys)
                {
                    foreach(float t in new[]{0f,.25f,.5f,.75f,1f})
                    {
                        if(key=="conditioner")IceMechanicalAlignment.Preview(w,c,t,0);
                        else if(key=="tank")IceMechanicalAlignment.Preview(w,c,1,t);
                        else if(brushRig!=null)IceBoardBrushRigEditor.SetPreview(brushRig,t);
                        Physics.SyncTransforms();
                        var worst=new Dictionary<string,float>();
                        foreach(var a in movingColliders[key])foreach(var b in staticColliders)
                        {
                            if(!a.r.bounds.Intersects(b.r.bounds))continue;
                            if(Physics.ComputePenetration(a.col,a.col.transform.position,a.col.transform.rotation,b.col,b.col.transform.position,b.col.transform.rotation,out _,out float depth)&&depth>.002f)
                            {
                                string pair=a.r.name+"  x  "+b.r.name;
                                if(!worst.ContainsKey(pair)||worst[pair]<depth)worst[pair]=depth;
                            }
                        }
                        foreach(var pair in worst.OrderByDescending(p=>p.Value))sb.AppendLine(key+" "+(t*100).ToString("F0")+"% | "+(pair.Value*1000).ToString("F0")+" mm | "+pair.Key);
                        IceMechanicalAlignment.EndPreview();if(brushRig!=null)IceBoardBrushRigEditor.SetPreview(brushRig,0);
                    }
                }
            }
            finally{foreach(var a in added)UnityEngine.Object.DestroyImmediate(a);IceMechanicalAlignment.EndPreview();}
            return sb.ToString();
        }
        static Transform rig(IceWorld w){ var t=w.Machine;while(t.parent!=null&&t.GetComponent<IceMachineRig>()==null)t=t.parent;return t; }

        public static void Controls()
        {
            int code=0;var sb=new StringBuilder();
            try
            {
                EditorSceneManager.OpenScene(IceMachineBaker.AuthoredScenePath);
                var rig=UnityEngine.Object.FindAnyObjectByType<IceMachineRig>(FindObjectsInactive.Include);
                var game=UnityEngine.Object.FindAnyObjectByType<IceResurfacerGame>(FindObjectsInactive.Include);
                var w=rig.Collect();var c=game.calibration;Transform m=w.Machine;
                foreach(var t in new[]{w.FloodValveMount,w.WashValveMount,w.FloodLever,w.WashValve})
                    sb.AppendLine(t.name+" parent "+t.parent.name+" local "+V(t.localPosition,4)+" machine "+V(m.InverseTransformPoint(t.position),4)+" rotM "+V((Quaternion.Inverse(m.rotation)*t.rotation).eulerAngles,1));
                // Handle grips through a quarter turn (the runtime turns the handle about its local up).
                foreach(var handle in new[]{w.FloodLever,w.WashValve})
                    for(int a=0;a<=90;a+=15)
                        sb.AppendLine("  "+handle.name+" "+a+" deg grip machine "+V(m.InverseTransformPoint(handle.parent.TransformPoint(handle.localPosition+handle.localRotation*Quaternion.Euler(0,-a,0)*new Vector3(-.09f,c.WaterValveStemHeight,0))),4));
                float closest=float.PositiveInfinity;
                for(int a=0;a<=90;a+=5)for(int b=0;b<=90;b+=5)
                {
                    Vector3 Grip(Transform h,int deg)=>h.parent.TransformPoint(h.localPosition+h.localRotation*Quaternion.Euler(0,-deg,0)*new Vector3(-.09f,c.WaterValveStemHeight,0));
                    closest=Mathf.Min(closest,Vector3.Distance(Grip(w.FloodLever,a),Grip(w.WashValve,b)));
                }
                sb.AppendLine("closest grip-to-grip over both quarter turns: "+(closest*1000).ToString("F0")+" mm");
                var foot=Array.Find(w.SteeringColumn.GetComponentsInChildren<Transform>(),t=>t.name=="Steering column foot");
                var floor=Array.Find(w.OperatorStation.GetComponentsInChildren<Transform>(),t=>t.name=="Left operator floor");
                var fb=IceConditionerAlignment.LocalBounds(foot.GetComponentsInChildren<Renderer>(),m);var lb=IceConditionerAlignment.LocalBounds(floor.GetComponents<Renderer>(),m);
                sb.AppendLine("steering foot bounds "+V(fb.min,4)+".."+V(fb.max,4));
                sb.AppendLine("left floor bounds "+V(lb.min,4)+".."+V(lb.max,4));
                sb.AppendLine("foot bottom minus floor top: "+((fb.min.y-lb.max.y)*1000).ToString("F1")+" mm; foot inside floor footprint x "+(fb.center.x>lb.min.x&&fb.center.x<lb.max.x)+" z "+(fb.center.z>lb.min.z&&fb.center.z<lb.max.z));
                foreach(var lever in new[]{w.ConditionerLever,w.HorizontalAugerLever,w.VerticalAugerLever,w.DumpLever})
                {
                    Vector3 grip=lever.TransformPoint(0,.225f,0);
                    sb.AppendLine(lever.name+" lossy "+V(lever.lossyScale,4)+" grip machine "+V(m.InverseTransformPoint(grip),4)+" eye distance "+Vector3.Distance(w.CameraMount.position,grip).ToString("F3")+
                        " local distance "+lever.InverseTransformPoint(w.CameraMount.position).magnitude.ToString("F3")+" parent chain scale "+V(lever.parent.lossyScale,4));
                }
                // Sightline from the seated eye to each water grip, in the handwheel's own frame.
                Vector3 eye=w.CameraMount.TransformPoint(0,game.operatorEyeHeight,0);
                foreach(var handle in new[]{w.WashValve,w.FloodLever})
                {
                    Vector3 grip=handle.TransformPoint(-.09f,c.WaterValveStemHeight,0);
                    sb.AppendLine("sightline to "+handle.name+" ("+Vector3.Distance(eye,grip).ToString("F3")+" m), in handwheel frame (lossy "+V(w.BladeWheel.lossyScale,3)+"):");
                    for(int i=0;i<=20;i++)
                    {
                        Vector3 p=Vector3.Lerp(eye,grip,i/20f),l=w.BladeWheel.InverseTransformPoint(p);
                        if(Mathf.Abs(l.x)<.3f&&Mathf.Abs(l.z)<.3f&&Mathf.Abs(l.y)<.2f)
                            sb.AppendLine("   t "+(i/20f).ToString("F2")+" local "+V(l,3)+" radial "+new Vector2(l.x,l.z).magnitude.ToString("F3"));
                    }
                }
                var colliders=new List<MeshCollider>();
                foreach(var r in rig.GetComponentsInChildren<MeshRenderer>())
                {var f=r.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null)continue;var mc=r.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=f.sharedMesh;colliders.Add(mc);}
                Physics.SyncTransforms();
                foreach(var offset in new[]{new Vector3(0,.06f,0),new Vector3(0,.06f,-.18f),new Vector3(.52f,.09f,-.18f)})
                {
                    Vector3 from=w.CameraMount.TransformPoint(offset);var names=new List<string>();int clear=0;
                    for(int i=0;i<8;i++)
                    {
                        float angle=i*Mathf.PI/4;Vector3 rim=w.BladeWheel.TransformPoint(Mathf.Cos(angle)*.13f,.012f,Mathf.Sin(angle)*.13f);
                        if(Physics.Raycast(from,(rim-from).normalized,out var hit,3)){if(hit.transform.IsChildOf(w.BladeWheel))clear++;else names.Add(i+":"+hit.transform.name+"("+(hit.transform.parent!=null?hit.transform.parent.name:"")+")");}
                    }
                    sb.AppendLine("E eye offset "+V(offset,2)+": rim clear "+clear+"/8; blockers "+string.Join(", ",names));
                }
                foreach(var mc in colliders)UnityEngine.Object.DestroyImmediate(mc);
                var wheelBounds=IceConditionerAlignment.LocalBounds(w.BladeWheel.GetComponentsInChildren<Renderer>(),w.BladeWheel);
                sb.AppendLine("handwheel visible bounds (own frame) "+V(wheelBounds.min,3)+".."+V(wheelBounds.max,3));
                File.WriteAllText(Out+"controls.txt",sb.ToString());
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText(Out+"controls.txt",sb+"\nFAIL "+e);code=1;}
            EditorApplication.Exit(code);
        }

        static void RenderViews(Transform m,List<(string,Vector3,Vector3,float)> views)
        {
            var node=new GameObject("Survey camera"){hideFlags=HideFlags.HideAndDontSave};
            var lightNode=new GameObject("Survey light"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var light=lightNode.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;
                lightNode.transform.rotation=m.rotation*Quaternion.Euler(40,60,0);
                var cam=node.AddComponent<Camera>();cam.nearClipPlane=.02f;cam.farClipPlane=60;cam.orthographic=true;
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.42f,.45f,.5f);
                Directory.CreateDirectory(Out+"Shots");
                foreach(var v in views)
                {
                    node.transform.position=v.Item2;
                    Vector3 d=v.Item3-v.Item2;
                    node.transform.rotation=Quaternion.LookRotation(d,Mathf.Abs(Vector3.Dot(d.normalized,m.up))>.9f?m.forward:m.up);
                    cam.orthographicSize=v.Item4;
                    for(int i=0;i<2;i++)IceResurfacerEditor.Capture(cam,"MechanicalReconciliation/Shots/warm.png",320,180);
                    IceResurfacerEditor.Capture(cam,"MechanicalReconciliation/Shots/"+v.Item1+".png",1200,800);
                }
                File.Delete(Out+"Shots/warm.png");
            }
            finally { UnityEngine.Object.DestroyImmediate(node);UnityEngine.Object.DestroyImmediate(lightNode); }
        }

        internal static void Shots(string prefix,IceWorld w)
        {
            Transform m=w.Machine;
            Vector3 P(float x,float y,float z){return m.TransformPoint(new Vector3(x,y,z));}
            var views=new List<(string,Vector3,Vector3,float)>
            {
                ("Side-Left",P(-8,1.0f,.6f),P(0,1.0f,.6f),2.2f),
                ("Side-Right",P(8,1.0f,.6f),P(0,1.0f,.6f),2.2f),
                ("Top",P(0,9,.6f),P(0,0,.6f),2.4f),
                ("Rear-Conditioner",P(0,1.0f,-8),P(0,1.0f,0),1.6f),
                ("Front",P(0,1.0f,9),P(0,1.0f,0),1.6f),
                ("Conveyor-Discharge-Side",P(4,1.3f,-.1f),P(0,1.3f,-.1f),.75f),
                ("Conveyor-Discharge-Top",P(0,5,0),P(0,1,0),.9f),
                ("Brush-Top",P(-.7f,5,1.1f),P(-.7f,0,1.1f),.8f),
                ("GuideRoller-Front",P(-.85f,.6f,6),P(-.85f,.6f,0),.6f),
                ("Conditioner-Side",P(-6,.6f,-.9f),P(0,.6f,-.9f),1.0f),
            };
            var arena=UnityEngine.Object.FindAnyObjectByType<IceArenaRig>(FindObjectsInactive.Include);
            bool arenaActive=arena!=null&&arena.gameObject.activeSelf;
            if(arena!=null)arena.gameObject.SetActive(false);
            var node=new GameObject("Survey camera"){hideFlags=HideFlags.HideAndDontSave};
            var lightNode=new GameObject("Survey light"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var light=lightNode.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;
                lightNode.transform.rotation=m.rotation*Quaternion.Euler(40,60,0);
                var cam=node.AddComponent<Camera>();cam.nearClipPlane=.02f;cam.farClipPlane=60;cam.orthographic=true;
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.42f,.45f,.5f);
                Directory.CreateDirectory(Out+"Shots");
                foreach(var v in views)
                {
                    node.transform.position=v.Item2;
                    Vector3 d=v.Item3-v.Item2;
                    node.transform.rotation=Quaternion.LookRotation(d,Mathf.Abs(Vector3.Dot(d.normalized,m.up))>.9f?m.forward:m.up);
                    cam.orthographicSize=v.Item4;
                    for(int i=0;i<2;i++)IceResurfacerEditor.Capture(cam,"MechanicalReconciliation/Shots/warm.png",320,180);
                    IceResurfacerEditor.Capture(cam,"MechanicalReconciliation/Shots/"+prefix+"-"+v.Item1+".png",1200,800);
                }
                File.Delete(Out+"Shots/warm.png");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(node);UnityEngine.Object.DestroyImmediate(lightNode);
                if(arena!=null)arena.gameObject.SetActive(arenaActive);
            }
        }

        static string Dump(Transform root,Transform machine)
        {
            var sb=new StringBuilder("path\tactive\tlocalPos\tlocalRot\tlocalScale\tmachinePos\tmesh\tmaterials\n");
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                var f=t.GetComponent<MeshFilter>();var r=t.GetComponent<Renderer>();
                string mats="";if(r!=null)foreach(var mat in r.sharedMaterials)mats+=(mat!=null?mat.name:"null")+";";
                sb.Append(Path(t,root)).Append('\t').Append(t.gameObject.activeSelf).Append('\t').Append(V(t.localPosition,5)).Append('\t').Append(Q(t.localRotation)).Append('\t')
                  .Append(V(t.localScale,5)).Append('\t').Append(V(machine.InverseTransformPoint(t.position),5)).Append('\t').Append(f!=null&&f.sharedMesh!=null?f.sharedMesh.name:"").Append('\t').Append(mats).Append('\n');
            }
            return sb.ToString();
        }
        static string Path(Transform t,Transform root){ string p=t.name+"#"+t.GetSiblingIndex(); while(t.parent!=null&&t.parent!=root){t=t.parent;p=t.name+"#"+t.GetSiblingIndex()+"/"+p;} return p; }
        static string V(Vector3 v,int d=3){ string f="F"+d; return "("+v.x.ToString(f)+","+v.y.ToString(f)+","+v.z.ToString(f)+")"; }
        static string Q(Quaternion q){ return "("+q.x.ToString("F5")+","+q.y.ToString("F5")+","+q.z.ToString("F5")+","+q.w.ToString("F5")+")"; }
    }
}
