using System;
using System.Collections.Generic;
using IceResurfacer.Core;
using IceResurfacer.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IceResurfacer.Editor
{
    /// <summary>
    /// One place for every simulation-critical assembly of the authored machine: where the visible
    /// part is, where the simulation puts it, the millimetre difference, and an explicit transfer of
    /// the approved visual geometry into the scene calibration. Transfers validate on a copy first,
    /// are undoable, and never move cosmetic geometry. Nothing here runs automatically.
    /// </summary>
    public static class IceMechanicalAlignment
    {
        public const float Tolerance=.001f;
        public enum Kind { Matches, Mechanical, Blocked, Info }

        public sealed class Item
        {
            public string Label, Detail="";
            public Vector3 Visual, Simulated;            // machine space
            public float ErrorMm;
            public Kind Kind;
            public string Blocker;                       // why a transfer is refused
            public Func<SimulatorConfig,string> Transfer; // writes the calibration copy; null when nothing to transfer
            public Func<IceWorld,string> VisualFix;     // optional explicit prefab-side mechanical fix; returns what it did
            public string VisualFixLabel;
        }

        static float Mm(Vector3 a,Vector3 b){ return Vector3.Distance(a,b)*1000; }
        static Vector3 M(IceWorld w,Transform t){ return w.Machine.InverseTransformPoint(t.position); }

        public static List<Item> Measure(IceWorld w,SimulatorConfig c)
        {
            var items=new List<Item>();
            Transform machine=w.Machine;

            // Wheels: drive geometry is calibration-owned and must match exactly.
            for(int side=0;side<2;side++)
            {
                float x=(side==0?-1:1)*c.WheelTrack*.5f;
                Add(items,"Front wheel pivot "+(side==0?"L":"R"),M(w,w.FrontWheels[side]),new Vector3(x,.39f,c.Wheelbase),null,
                    "Driving geometry (Wheelbase/WheelTrack) is not transferred from visuals.");
                Add(items,"Rear wheel hub "+(side==0?"L":"R"),M(w,w.RearWheels[side]),new Vector3(x,.39f,0),null,
                    "Driving geometry (Wheelbase/WheelTrack) is not transferred from visuals.");
            }

            // Conditioner hitch (root at the authored lift); cylinders keep their chassis-relative geometry.
            Vector3 cond=M(w,w.Conditioner);
            float simZ=-(c.HitchBehindAxle+c.DrawbarLength);
            float lift=Mathf.Clamp01(cond.y/IceConditionerAlignment.LiftTravel);
            var hitch=Add(items,"Conditioner hitch (root)",cond,new Vector3(0,lift*IceConditionerAlignment.LiftTravel,simZ),cfg=>
            {
                cfg.DrawbarLength=-cond.z-cfg.HitchBehindAxle;
                return "DrawbarLength "+c.DrawbarLength.ToString("F3")+" -> "+cfg.DrawbarLength.ToString("F3")+" m (GPS compatibility field)";
            },null);
            float yaw=Mathf.DeltaAngle(0,(Quaternion.Inverse(machine.rotation)*w.Conditioner.rotation).eulerAngles.y);
            if(Mathf.Abs(cond.x)>.002f||Mathf.Abs(yaw)>.05f)Block(hitch,"the conditioner root is offset sideways or yawed; the simulation keeps it centred and square.");
            else if(hitch.Kind!=Kind.Matches)
            {
                var r=IceConditionerAlignment.Measure(w,c);
                if(r.HousingTireGapAuthored<.05f)Block(hitch,"the housing would be "+r.HousingTireGapAuthored.ToString("F3")+" m from the rear tyres (needs 0.05 m).");
                float delta=cond.z-simZ;
                hitch.Detail="Visible conditioner is "+(delta*1000).ToString("+0;-0")+" mm from the simulated cut, flood and pickup. Transfer moves the simulation to it, "+
                    "and the lift-cylinder clevises by "+(-delta*1000).ToString("+0;-0")+" mm on the housing so the cylinders keep their stroke.";
                hitch.VisualFix=world=>{ShiftConditionerClevises(world,-delta);return "clevis z "+(-delta).ToString("+0.000;-0.000")+" m";};
                hitch.VisualFixLabel="moves both conditioner lift clevises "+(-delta*1000).ToString("+0;-0")+" mm along the housing";
            }

            // Cutting blade seat (cosmetic pose of a rest-relative part; the cut is planar in the simulation).
            var report=IceConditionerAlignment.Measure(w,c);
            var blade=new Item{Label="Cutting blade edge at depth 0",Visual=new Vector3(0,report.BladeAboveIce,0),Simulated=Vector3.zero,ErrorMm=Mathf.Abs(report.BladeAboveIce)*1000};
            blade.Kind=blade.ErrorMm<=Tolerance*1000?Kind.Matches:Kind.Mechanical;
            blade.Detail="Lowered blade edge "+(report.BladeAboveIce*1000).ToString("+0.0;-0.0")+" mm relative to the ice top.";
            if(blade.Kind==Kind.Mechanical){blade.VisualFix=world=>{float moved=IceConditionerAlignment.SeatBlade(world,c);CommitToPrefab(world.CuttingBlade);return "blade y "+(moved*1000).ToString("+0.00;-0.00")+" mm";};blade.VisualFixLabel="seats the blade edge on the ice ("+(-report.BladeAboveIce*1000).ToString("+0.0;-0.0")+" mm)";}
            items.Add(blade);
            // Lengths at the authored root position (what Play will show once the hitch is physical).
            bool collapsed=Mathf.Min(report.CylinderAuthored[0],report.CylinderAuthored[1])<.15f;
            items.Add(new Item{Label="Conditioner lift cylinders",Kind=collapsed?Kind.Mechanical:Kind.Info,
                Detail="Pin-to-clevis at the simulated hitch: lowered "+report.CylinderLowered[0].ToString("F3")+" m, raised "+report.CylinderRaised[0].ToString("F3")+
                    " m; at the authored root: "+report.CylinderAuthored[0].ToString("F3")+" m"+(collapsed?" (COLLAPSED: the clevis meets the chassis pin)":"")});

            // Cosmetic lift-linkage bars ride with the conditioner; they must not pass through the rear tyres.
            float linkageShift=LinkageIntoTyres(w,c,out float linkageDepth);
            var linkage=new Item{Label="Conditioner lift linkage vs rear tyres",ErrorMm=linkageDepth*1000,Kind=linkageShift>0?Kind.Mechanical:Kind.Matches};
            if(linkageShift>0)
            {
                linkage.Detail="Through the lift range the linkage bars enter the rear tyre sidewalls by "+(linkageDepth*1000).ToString("F0")+" mm.";
                linkage.VisualFix=world=>{ShiftLinkage(world,linkageShift);return "bars "+(linkageShift*1000).ToString("F0")+" mm inboard";};
                linkage.VisualFixLabel="moves both lift-linkage bars "+(linkageShift*1000).ToString("F0")+" mm inboard so they clear the tyres by 10 mm";
            }
            items.Add(linkage);

            // Snow tank hinge and scale.
            Vector3 hinge=M(w,w.TankPivot);
            Vector3 scale=w.TankPivot.localScale;
            var tank=Add(items,"Snow tank dump hinge",hinge,new Vector3(0,c.TankHingeHeight,c.TankHingeZ),cfg=>
            {
                cfg.TankHingeHeight=hinge.y;cfg.TankHingeZ=hinge.z;
                return "TankHingeHeight/Z ("+c.TankHingeHeight.ToString("F3")+", "+c.TankHingeZ.ToString("F3")+") -> ("+hinge.y.ToString("F3")+", "+hinge.z.ToString("F3")+")";
            },null);
            if(Mathf.Abs(hinge.x)>.002f)Block(tank,"the hinge is off the machine centreline; the tank's lateral offset is TankLateralOffset (shell), not the hinge.");
            var tankScale=Add(items,"Snow tank scale (per axis, x1000)",scale*1000,new Vector3(c.TankScaleX,c.TankScaleY,c.TankScaleZ)*1000,cfg=>
            {
                cfg.TankScaleX=scale.x;cfg.TankScaleY=scale.y;cfg.TankScaleZ=scale.z;
                return "TankScale ("+c.TankScaleX.ToString("F3")+", "+c.TankScaleY.ToString("F3")+", "+c.TankScaleZ.ToString("F3")+") -> ("+
                    scale.x.ToString("F3")+", "+scale.y.ToString("F3")+", "+scale.z.ToString("F3")+"); collision shell and dump outlet only, snow capacity unchanged";
            },null);
            tankScale.ErrorMm=(scale-new Vector3(c.TankScaleX,c.TankScaleY,c.TankScaleZ)).magnitude*1000;
            tankScale.Kind=tankScale.ErrorMm<.5f?Kind.Matches:Kind.Mechanical;
            if(w.BodyTank.localScale!=Vector3.one||machine.lossyScale!=Vector3.one)Block(tankScale,"scale only the dump hinge (Snow tank forward dump hinge), not the shell or the machine.");

            // Conveyor discharge.
            Vector3 discharge=M(w,w.TankDischarge);
            var conveyor=Add(items,"Conveyor discharge into tank",discharge,new Vector3(c.ConveyorOutletX,c.ConveyorOutletY,c.ConveyorOutletZ),cfg=>
            {
                cfg.ConveyorDischargeX=discharge.x;cfg.ConveyorDischargeHeight=discharge.y-cfg.ConveyorHeightOffset;cfg.ConveyorDischargeZ=discharge.z;
                return "ConveyorDischarge ("+c.ConveyorOutletX.ToString("F3")+", "+c.ConveyorOutletY.ToString("F3")+", "+c.ConveyorOutletZ.ToString("F3")+") -> ("+
                    discharge.x.ToString("F3")+", "+discharge.y.ToString("F3")+", "+discharge.z.ToString("F3")+")";
            },null);
            string inlet=InletProblem(w);
            if(inlet!=null)
            {
                conveyor.Detail=inlet;if(conveyor.Kind==Kind.Matches)conveyor.Kind=Kind.Mechanical;
                conveyor.VisualFix=world=>{ if(!AlignDeliveryThroat(world,out string why))throw new InvalidOperationException("Delivery throat not aligned: "+why);return why; };
                conveyor.VisualFixLabel="re-aims the short delivery throat from the discharge casing into the tank's rear-wall opening and puts the discharge point at its mouth";
            }

            // Board brush (owned by its rig).
            var brushRig=w.BrushArm!=null?w.BrushArm.GetComponent<IceBoardBrushRig>():null;
            if(brushRig==null)
            {
                var brush=Add(items,"Board brush pivot",M(w,w.BrushArm),new Vector3(c.BrushPivotX,.38f,c.BrushPivotZ),null,null);
                if(brush.Kind!=Kind.Matches)brush.Detail="Add the Board Brush Rig: it imports this pose and applies pivot, arm, angles and radius together.";
            }
            else
            {
                var sim=IceBoardBrushRig.Mechanism.From(c);
                var brush=Add(items,"Board brush mechanism",new Vector3(brushRig.mechanism.PivotX,brushRig.pivotHeight,brushRig.mechanism.PivotZ),new Vector3(sim.PivotX,brushRig.pivotHeight,sim.PivotZ),null,null);
                bool same=brushRig.mechanism.Matches(sim);
                brush.Kind=same?Kind.Matches:Kind.Mechanical;
                if(!same){brush.ErrorMm=Mathf.Max(brush.ErrorMm,.5f);brush.Detail="Rig draft not applied: "+string.Join(", ",IceBoardBrushRigEditor.Differences(brushRig.mechanism,sim))+". Use the rig's Apply.";}
                float visual=IceBoardBrushRigEditor.VisualRadius(brushRig);
                var radius=Add(items,"Board brush radius (visible vs contact, mm)",new Vector3(visual*1000,0,0),new Vector3(c.BrushRadius*1000,0,0),null,null);
                radius.ErrorMm=Mathf.Abs(visual-c.BrushRadius)*1000;radius.Kind=radius.ErrorMm<=5?Kind.Matches:Kind.Mechanical;
                if(radius.Kind!=Kind.Matches)radius.Detail="Snow is cleared over a "+(c.BrushRadius*1000).ToString("F0")+" mm disc but the bristles show "+(visual*1000).ToString("F0")+" mm.";
            }

            // Collision outline: the plan view the simulation collides with, against the machine as drawn.
            var fit=IceCollisionOutlineFit.Measure(w,c);
            float[] simulated=c.HasCollisionOutline?c.CollisionOutline:new[]{-c.VehicleHalfWidth,-c.BodyRear,-c.VehicleHalfWidth,c.BodyFront,c.VehicleHalfWidth,c.BodyFront,c.VehicleHalfWidth,-c.BodyRear};
            float outlineError=IceCollisionOutlineFit.Difference(fit.Outline,simulated,out float outlineZ);
            float conditionerError=Mathf.Max(Mathf.Abs(fit.ConditionerHalfWidth-c.ConditionerCollisionHalfWidth),
                c.ConditionerCollisionHalfWidth>0?Mathf.Max(Mathf.Abs(fit.ConditionerFront-c.ConditionerCollisionFront),Mathf.Abs(fit.ConditionerRear-c.ConditionerCollisionRear)):0);
            var outline=new Item{Label="Collision outline (plan view)",ErrorMm=Mathf.Max(outlineError,conditionerError)*1000};
            outline.Kind=outline.ErrorMm<=5?Kind.Matches:Kind.Mechanical;
            if(outline.Kind!=Kind.Matches)
            {
                outline.Detail=(c.HasCollisionOutline?"The collision outline":"The simulated body (one "+(2*c.VehicleHalfWidth).ToString("F2")+" m wide rectangle)")+
                    " differs from the machine as drawn by up to "+(outlineError*1000).ToString("F0")+" mm (at z "+outlineZ.ToString("F2")+" m)"+
                    (c.ConditionerCollisionHalfWidth>0?"":"; the conditioner (half width "+fit.ConditionerHalfWidth.ToString("F3")+" m) is not a collider")+
                    ". Transfer fits a "+fit.Vertices+"-vertex outline to "+fit.Renderers+" parts (widest "+fit.MaxHalfWidth.ToString("F3")+" m) and a conditioner collider; "+
                    "it changes where the machine can drive (GPS compatibility).";
                outline.Transfer=cfg=>
                {
                    cfg.CollisionOutline=(float[])fit.Outline.Clone();
                    cfg.ConditionerCollisionHalfWidth=fit.ConditionerHalfWidth;cfg.ConditionerCollisionFront=fit.ConditionerFront;cfg.ConditionerCollisionRear=fit.ConditionerRear;
                    return "CollisionOutline "+(c.HasCollisionOutline?(c.CollisionOutline.Length/2)+" vertices":"legacy rectangle")+" -> "+fit.Vertices+" vertices; conditioner collider half width "+
                        c.ConditionerCollisionHalfWidth.ToString("F3")+" -> "+fit.ConditionerHalfWidth.ToString("F3")+" (front "+fit.ConditionerFront.ToString("F3")+", rear "+fit.ConditionerRear.ToString("F3")+") (GPS compatibility fields)";
                };
            }
            items.Add(outline);

            // Board guide roller: its contact only happens if it reaches past the body beside it.
            Vector3 roller=M(w,w.GuideRoller);
            float rollerRadius=GuideRollerRadius(w);
            var guide=Add(items,"Board guide roller",roller,new Vector3(c.GuideRollerX,roller.y,c.GuideRollerZ),cfg=>
            {
                cfg.GuideRollerX=roller.x;cfg.GuideRollerZ=roller.z;cfg.GuideRollerRadius=rollerRadius;
                return "GuideRoller ("+c.GuideRollerX.ToString("F3")+", "+c.GuideRollerZ.ToString("F3")+", r "+c.GuideRollerRadius.ToString("F3")+") -> ("+
                    roller.x.ToString("F3")+", "+roller.z.ToString("F3")+", r "+rollerRadius.ToString("F3")+") (GPS compatibility fields)";
            },null);
            // Against the outline that will be in force after Transfer (the fitted one).
            IceCollisionOutlineFit.Extent(fit.Outline,roller.z,out float sideLeft,out float sideRight);
            float bodySide=roller.x<0?(float.IsNaN(sideLeft)?0:-sideLeft):(float.IsNaN(sideRight)?0:sideRight);
            float reach=Mathf.Abs(roller.x)+rollerRadius-bodySide;
            if(guide.Kind!=Kind.Matches)
            {
                guide.Detail="Simulated roller contact is "+guide.ErrorMm.ToString("F0")+" mm from the visible roller.";
                if(reach<=0)Block(guide,"its outer edge is "+(-reach*1000).ToString("F0")+" mm inside the drawn body beside it (z "+roller.z.ToString("F2")+"), so the body would always reach the boards first. Extend the roller bracket outboard.");
            }
            float widest=Mathf.Max(fit.MaxHalfWidth,fit.ConditionerHalfWidth);
            if(Mathf.Abs(roller.x)+rollerRadius<widest-.001f)
                items.Add(new Item{Label="Guide roller vs widest part",Kind=Kind.Info,Detail="The roller reaches "+(Mathf.Abs(roller.x)+rollerRadius).ToString("F3")+" m from the centreline, "+
                    ((widest-Mathf.Abs(roller.x)-rollerRadius)*1000).ToString("F0")+" mm short of the "+(fit.ConditionerHalfWidth>=fit.MaxHalfWidth?"conditioner side plates":"widest body part")+" ("+widest.ToString("F3")+" m). "+
                    "Running parallel to the boards that part touches first; the roller leads only with the nose angled toward the boards (about "+
                    (Mathf.Atan2(widest-Mathf.Abs(roller.x)-rollerRadius,roller.z+(c.HitchBehindAxle+c.DrawbarLength-fit.ConditionerFront))*Mathf.Rad2Deg).ToString("F1")+" deg or more). \"Move Guide Roller Outboard...\" moves its assembly (a visual change to your part); Transfer then makes it physical."});
            return items;
        }

        static Item Add(List<Item> items,string label,Vector3 visual,Vector3 simulated,Func<SimulatorConfig,string> transfer,string infoOnly)
        {
            var item=new Item{Label=label,Visual=visual,Simulated=simulated,ErrorMm=Mm(visual,simulated),Transfer=transfer};
            item.Kind=item.ErrorMm<=Tolerance*1000?Kind.Matches:transfer==null&&infoOnly!=null?Kind.Blocked:Kind.Mechanical;
            if(infoOnly!=null&&item.Kind!=Kind.Matches){item.Blocker=infoOnly;item.Transfer=null;}
            if(infoOnly!=null&&item.Kind==Kind.Matches)item.Detail="";
            items.Add(item);
            return item;
        }
        static void Block(Item item,string why){ item.Kind=Kind.Blocked;item.Blocker=why; }

        public static float GuideRollerRadius(IceWorld w)
        {
            var b=IceConditionerAlignment.LocalBounds(w.GuideRoller.GetComponentsInChildren<Renderer>(true),w.GuideRoller);
            // To 0.1 mm: mesh bounds carry float noise that would otherwise read as a calibration change.
            return Mathf.Round(Mathf.Max(b.max.x,-b.min.x,b.max.z,-b.min.z)*10000)/10000;
        }

        /// <summary>Null when the discharge sits in an open part of the tank's rear wall (within 6 cm of
        /// the wall plane); otherwise what is wrong. Uses temporary mesh colliders on the tank only.</summary>
        public static string InletProblem(IceWorld w)
        {
            var colliders=new List<MeshCollider>();
            try
            {
                foreach(var r in w.TankPivot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(!r.gameObject.activeInHierarchy||r.transform.IsChildOf(w.TankSnow))continue;
                    var f=r.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null)continue;
                    var mc=r.gameObject.AddComponent<MeshCollider>();mc.hideFlags=HideFlags.HideAndDontSave;mc.sharedMesh=f.sharedMesh;colliders.Add(mc);
                }
                Physics.SyncTransforms();
                // The machine's own physics scene: Prefab Mode and prefab previews are separate worlds.
                var physics=w.Machine.gameObject.scene.GetPhysicsScene();
                Transform m=w.Machine;Vector3 p=w.TankDischarge.position,forward=m.forward;
                // Is the discharge looking through an opening? Rays along the throat at its centre and rim.
                int blocked=0;
                foreach(var o in new[]{Vector3.zero,m.up*.06f,-m.up*.06f,m.right*.06f,-m.right*.06f})
                    if(physics.Raycast(p+o-forward*.10f,forward,out var hit,.25f)&&Vector3.Dot(hit.point-p,forward)<.12f)blocked++;
                if(blocked>0)return "The discharge faces solid tank wall ("+blocked+"/5 sample rays blocked within 12 cm): snow would pass through a panel.";
                // The wall around the opening: an outer ring of rays finds its plane.
                float wall=float.PositiveInfinity;
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI*.25f;Vector3 o=(m.right*Mathf.Cos(a)+m.up*Mathf.Sin(a))*.24f;
                    if(physics.Raycast(p+o-forward*.3f,forward,out var ring,.8f))wall=Mathf.Min(wall,Vector3.Dot(ring.point-p,forward));
                }
                if(float.IsPositiveInfinity(wall))return "No tank wall around the discharge.";
                if(wall>.03f)return "The discharge ends "+(wall*1000).ToString("F0")+" mm short of the tank wall: snow would fall in the open gap.";
                return null;
            }
            finally { foreach(var mc in colliders)UnityEngine.Object.DestroyImmediate(mc); }
        }

        static List<Transform> Linkage(IceWorld w)
        {
            var bars=new List<Transform>();
            foreach(Transform t in w.Conditioner)if(t.name=="Lift linkage"&&t.gameObject.activeInHierarchy)bars.Add(t);
            return bars;
        }

        /// <summary>Inward shift (m) that keeps every linkage bar 10 mm inside the rear tyres' inner
        /// faces wherever a bar passes within a tyre's radius, over lift 0..1 at the simulated hitch.
        /// depth is the deepest lateral intrusion found.</summary>
        static float LinkageIntoTyres(IceWorld w,SimulatorConfig c,out float depth)
        {
            depth=0;float shift=0;
            var bars=Linkage(w);if(bars.Count==0)return 0;
            Transform m=w.Machine;
            Vector3 root=m.InverseTransformPoint(w.Conditioner.position);
            foreach(var wheel in w.RearWheels)
            {
                var b=IceConditionerAlignment.LocalBounds(wheel.GetComponentsInChildren<Renderer>(true),wheel);
                float radius=Mathf.Max(b.max.y,-b.min.y,b.max.z,-b.min.z);
                var mb=IceConditionerAlignment.LocalBounds(wheel.GetComponentsInChildren<Renderer>(true),m);
                float inner=Mathf.Min(Mathf.Abs(mb.min.x),Mathf.Abs(mb.max.x));
                Vector3 hub=m.InverseTransformPoint(wheel.position);
                foreach(var bar in bars)
                {
                    float r=bar.lossyScale.x*.5f;
                    Vector3 local=w.Conditioner.InverseTransformPoint(bar.position),axis=w.Conditioner.InverseTransformDirection(bar.up)*bar.lossyScale.y;
                    if(Mathf.Sign(local.x)!=Mathf.Sign(hub.x))continue;
                    for(int lift=0;lift<=10;lift++)for(int i=0;i<=20;i++)
                    {
                        // At the authored root position: that is where Transfer makes the conditioner physical.
                        Vector3 p=local+axis*(i/10f-1)+new Vector3(0,lift*.1f*IceConditionerAlignment.LiftTravel,root.z);
                        if(new Vector2(p.y-hub.y,p.z-hub.z).magnitude>radius+r+.01f)continue;
                        float outer=Mathf.Abs(p.x)+r;
                        depth=Mathf.Max(depth,outer-inner);
                        shift=Mathf.Max(shift,outer-(inner-.01f));
                    }
                }
            }
            return depth>0?Mathf.Ceil(shift*1000)/1000:0;
        }

        static void ShiftLinkage(IceWorld w,float shift)
        {
            foreach(var bar in Linkage(w))
            {
                Undo.RecordObject(bar,"Move conditioner lift linkage inboard");
                var p=bar.localPosition;p.x-=Mathf.Sign(p.x)*shift;bar.localPosition=p;
                CommitToPrefab(bar);
            }
        }

        static Transform Child(Transform root,string name)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;
            return null;
        }

        /// <summary>
        /// Re-aims "Short rear-wall delivery throat" from inside the discharge casing to the tank's
        /// rear-wall opening, ending 5 mm behind the wall so the rising tank never clips it, and moves
        /// the discharge point to its mouth. Uses the lowest climb angle that seats the whole throat
        /// section inside the opening with 12 mm margins. Silhouette is otherwise unchanged.
        /// </summary>
        public static bool AlignDeliveryThroat(IceWorld w,out string summary)
        {
            summary="";
            Transform m=w.Machine,conveyor=w.VerticalAuger.parent;
            var throat=Child(conveyor,"Short rear-wall delivery throat")??Child(m,"Short rear-wall delivery throat");
            var casing=Child(conveyor,"Compact discharge casing");
            if(throat==null||casing==null){summary="the delivery throat or discharge casing is missing";return false;}
            Vector3 c=m.InverseTransformPoint(casing.position);
            float casingHalf=casing.lossyScale.y,r=throat.lossyScale.x*.5f;
            var colliders=new List<MeshCollider>();
            try
            {
                foreach(var mr in w.TankPivot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(!mr.gameObject.activeInHierarchy||mr.transform.IsChildOf(w.TankSnow))continue;
                    var f=mr.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null)continue;
                    var mc=mr.gameObject.AddComponent<MeshCollider>();mc.hideFlags=HideFlags.HideAndDontSave;mc.sharedMesh=f.sharedMesh;colliders.Add(mc);
                }
                Physics.SyncTransforms();
                var physics=m.gameObject.scene.GetPhysicsScene();
                // Wall plane and the open band straight ahead of the casing.
                float wall=float.PositiveInfinity,openLow=float.NaN,openHigh=float.NaN,best=float.PositiveInfinity,runLow=float.NaN;
                for(float y=c.y-.3f;y<=c.y+.8f;y+=.005f)
                {
                    bool hit=physics.Raycast(m.TransformPoint(c.x,y,c.z),m.forward,out var h,3f);
                    float z=hit?m.InverseTransformPoint(h.point).z:float.PositiveInfinity;
                    if(hit&&z<wall+.05f)wall=Mathf.Min(wall,z);
                }
                if(float.IsPositiveInfinity(wall)){summary="no tank wall ahead of the conveyor";return false;}
                // A hole has wall below and above it (open air over the roof does not count);
                // use the one nearest the casing that the throat can pass through.
                for(float y=c.y-.3f;y<=c.y+.8f;y+=.005f)
                {
                    bool open=!physics.Raycast(m.TransformPoint(c.x,y,c.z),m.forward,out var h,3f)||m.InverseTransformPoint(h.point).z>wall+.05f;
                    if(open&&float.IsNaN(runLow))runLow=y;
                    if(!open&&!float.IsNaN(runLow))
                    {
                        float high=y-.005f,distance=Mathf.Abs((runLow+high)*.5f-c.y);
                        if(runLow>c.y-.3f&&high-runLow>=2*r+.024f&&distance<best){best=distance;openLow=runLow;openHigh=high;}
                        runLow=float.NaN;
                    }
                }
                if(float.IsNaN(openLow)){summary="no rear-wall opening ahead of the conveyor is large enough for the "+(2*r*1000).ToString("F0")+" mm throat";return false;}
                // The wall face at the opening itself (lower lips of a tapered wall can sit further back).
                float face=float.PositiveInfinity;
                foreach(float y in new[]{openLow-.01f,openHigh+.01f})
                    if(physics.Raycast(m.TransformPoint(c.x,y,c.z),m.forward,out var lip,3f))face=Mathf.Min(face,m.InverseTransformPoint(lip.point).z);
                if(!float.IsPositiveInfinity(face))wall=face;
                // Lateral fit at the opening's middle.
                float midY=(openLow+openHigh)*.5f;
                foreach(float side in new[]{-1f,1f})
                    if(physics.Raycast(m.TransformPoint(c.x+side*(r+.005f),midY,c.z),m.forward,out var lateral,3f)&&m.InverseTransformPoint(lateral.point).z<wall+.05f)
                    {summary="the opening is narrower than the throat at x = "+(c.x+side*(r+.005f)).ToString("F3");return false;}
                float tipZ=wall-.005f,run=tipZ-c.z;
                for(float degrees=0;degrees<=45;degrees+=.25f)
                {
                    float t=Mathf.Tan(degrees*Mathf.Deg2Rad),cos=Mathf.Cos(degrees*Mathf.Deg2Rad);
                    // The throat's end cap stays hidden inside the casing.
                    float yA=Mathf.Clamp(c.y,c.y-casingHalf+r*cos,c.y+casingHalf-r*cos);
                    float atWall=yA+(wall-c.z)*t,half=r/cos;
                    if(atWall-half<openLow+.012f||atWall+half>openHigh-.012f)continue;
                    var a=new Vector3(c.x,yA,c.z);var b=new Vector3(c.x,yA+run*t,tipZ);
                    Undo.RecordObjects(new UnityEngine.Object[]{throat,w.TankDischarge},"Align delivery throat to tank inlet");
                    throat.SetPositionAndRotation(m.TransformPoint((a+b)*.5f),m.rotation*Quaternion.FromToRotation(Vector3.up,b-a));
                    var s=throat.localScale;s.y=(b-a).magnitude*.5f/(throat.parent!=null?throat.parent.lossyScale.y:1);throat.localScale=s;
                    w.TankDischarge.position=m.TransformPoint(b);
                    CommitToPrefab(throat);CommitToPrefab(w.TankDischarge);
                    summary="throat climbs "+degrees.ToString("F2")+" deg from y "+yA.ToString("F3")+" to "+b.y.ToString("F3")+", mouth at z "+tipZ.ToString("F4")+
                        " (wall "+wall.ToString("F4")+"), opening y "+openLow.ToString("F3")+".."+openHigh.ToString("F3")+", section at wall "+(atWall-half).ToString("F3")+".."+(atWall+half).ToString("F3");
                    return true;
                }
                summary="no climb angle up to 45 deg fits the throat inside the opening ("+openLow.ToString("F3")+".."+openHigh.ToString("F3")+")";
                return false;
            }
            finally { foreach(var mc in colliders)UnityEngine.Object.DestroyImmediate(mc); }
        }

        /// <summary>
        /// The roller's outer edge must lead the widest part on its side (the conditioner side plates on the
        /// authored machine) for the roller, not that part, to bear on the boards in a straight run. Returns how
        /// far the roller assembly has to move outboard for the given lead (0 when it already leads by it).
        /// </summary>
        public static float GuideRollerShortfall(IceWorld w,SimulatorConfig c,float lead,out float widest,out string part)
        {
            var fit=IceCollisionOutlineFit.Measure(w,c);
            Vector3 roller=M(w,w.GuideRoller);float side=Mathf.Sign(roller.x);
            widest=0;part="body";
            for(int i=0;i<fit.Outline.Length;i+=2)if(Mathf.Sign(fit.Outline[i])==side)widest=Mathf.Max(widest,Mathf.Abs(fit.Outline[i]));
            if(fit.ConditionerHalfWidth>widest){widest=fit.ConditionerHalfWidth;part="conditioner side plates";}
            return Mathf.Max(0,widest+lead-(Mathf.Abs(roller.x)+GuideRollerRadius(w)));
        }

        /// <summary>
        /// Moves the guide roller assembly (its group: wheel, cap, bracket) straight outboard so the roller's
        /// outer edge leads the widest part on its side by lead, and lengthens "Guide roller bracket" so its
        /// inboard end stays where it meets the body. Nothing else moves; one Undo step; written to the prefab.
        /// Transfer Authored Geometry then makes the new roller position physical.
        /// </summary>
        public static bool MoveGuideRollerOutboard(IceWorld w,SimulatorConfig c,float lead,out string summary)
        {
            summary="";
            Transform m=w.Machine,group=w.GuideRoller.parent;
            if(group==null||group==m){summary="the roller has no assembly of its own to move";return false;}
            float move=GuideRollerShortfall(w,c,lead,out float widest,out string part);
            if(move<.0005f){summary="the roller already leads the "+part+" by "+(lead*1000).ToString("F0")+" mm or more";return false;}
            float side=Mathf.Sign(M(w,w.GuideRoller).x);
            var bracket=Child(group,"Guide roller bracket");
            Vector3 inner=Vector3.zero,outer=Vector3.zero;
            if(bracket!=null)
            {
                Vector3 half=bracket.up*bracket.lossyScale.y,a=bracket.position-half,b=bracket.position+half;
                bool aInner=Mathf.Abs(m.InverseTransformPoint(a).x)<Mathf.Abs(m.InverseTransformPoint(b).x);
                inner=aInner?a:b;outer=aInner?b:a;
            }
            Undo.RecordObjects(bracket!=null?new UnityEngine.Object[]{group,bracket}:new UnityEngine.Object[]{group},"Move guide roller outboard");
            Vector3 offset=m.right*side*move;
            group.position+=offset;
            if(bracket!=null)
            {
                outer+=offset;Vector3 axis=outer-inner;
                bool upOutboard=Vector3.Dot(bracket.up,m.right*side)>0;
                bracket.SetPositionAndRotation((inner+outer)*.5f,Quaternion.FromToRotation(Vector3.up,upOutboard?axis:-axis));
                var scale=bracket.localScale;scale.y=axis.magnitude*.5f/(bracket.parent!=null?bracket.parent.lossyScale.y:1);bracket.localScale=scale;
            }
            CommitToPrefab(group);if(bracket!=null)CommitToPrefab(bracket);
            Vector3 roller=M(w,w.GuideRoller);float radius=GuideRollerRadius(w);
            summary="roller assembly moved "+(move*1000).ToString("F0")+" mm outboard: centre x "+roller.x.ToString("F3")+", outer edge |x| "+(Mathf.Abs(roller.x)+radius).ToString("F3")+
                " leads the "+part+" ("+widest.ToString("F3")+") by "+((Mathf.Abs(roller.x)+radius-widest)*1000).ToString("F0")+" mm"+
                (bracket!=null?"; bracket lengthened to "+(2*bracket.lossyScale.y).ToString("F3")+" m, still meeting the body at x "+m.InverseTransformPoint(inner).x.ToString("F3"):"");
            return true;
        }

        // ---- Explicit actions (Undo) ------------------------------------------------------------

        /// <summary>The Inspector's Transfer: prefab-side mechanical fixes first (they can move the
        /// discharge), then a fresh measurement transferred into the calibration. One Undo group;
        /// any failure rolls everything back. Blocked items are skipped and reported.</summary>
        public static bool Reconcile(IceWorld w,IceResurfacerGame game,out List<string> changes,out string error)
        {
            changes=new List<string>();error=null;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Transfer authored machine geometry");
            try
            {
                foreach(var item in Measure(w,game.calibration))
                    if(item.VisualFix!=null)changes.Add("PREFAB "+item.Label+": "+item.VisualFixLabel+" ("+item.VisualFix(w)+")");
                var fresh=Measure(w,game.calibration).FindAll(i=>i.Kind!=Kind.Blocked);
                if(!TransferToSimulation(game,fresh,out var transferred,out error)){Undo.RevertAllDownToGroup(group);changes.Clear();return false;}
                changes.AddRange(transferred);
                foreach(var item in Measure(w,game.calibration))
                    if(item.Kind==Kind.Blocked)changes.Add("SKIPPED "+item.Label+": "+item.Blocker);
                Undo.CollapseUndoOperations(group);
                return true;
            }
            catch(Exception e){Undo.RevertAllDownToGroup(group);changes.Clear();error=e.Message;return false;}
        }

        /// <summary>Validates every transfer on a copy of the calibration, then writes them with Undo.</summary>
        public static bool TransferToSimulation(IceResurfacerGame game,IEnumerable<Item> items,out List<string> changes,out string error)
        {
            changes=new List<string>();error=null;
            var copy=JsonUtility.FromJson<SimulatorConfig>(JsonUtility.ToJson(game.calibration));
            foreach(var item in items)
            {
                if(item.Kind==Kind.Blocked){error=item.Label+": "+item.Blocker;return false;}
                if(item.Transfer!=null&&item.Kind==Kind.Mechanical)changes.Add(item.Label+": "+item.Transfer(copy));
            }
            try { copy.Validate(); }
            catch(Exception e) { error="The scene calibration would be invalid: "+e.Message;changes.Clear();return false; }
            Undo.RecordObject(game,"Transfer authored machine geometry to simulation");
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(copy),game.calibration);
            EditorUtility.SetDirty(game);
            if(PrefabUtility.IsPartOfPrefabInstance(game))PrefabUtility.RecordPrefabInstancePropertyModifications(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            return true;
        }

        /// <summary>Keeps the lift cylinders' stroke when the hitch moves: the conditioner-side clevis
        /// pins move along the housing by the opposite of the hitch change.</summary>
        public static void ShiftConditionerClevises(IceWorld w,float dz)
        {
            foreach(var clevis in w.ConditionerCylinderClevis)
            {
                Undo.RecordObject(clevis,"Move conditioner lift clevis");
                clevis.localPosition+=Vector3.forward*dz;
                CommitToPrefab(clevis);
            }
        }

        /// <summary>Writes an instance edit straight into the machine prefab (not a scene override).</summary>
        public static void CommitToPrefab(UnityEngine.Object part)
        {
            if(!PrefabUtility.IsPartOfPrefabInstance(part))return;
            PrefabUtility.RecordPrefabInstancePropertyModifications(part);
            string path=AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(part));
            PrefabUtility.ApplyObjectOverride(part,path,InteractionMode.UserAction);
        }

        // ---- Edit Mode lift / tilt preview (never saved) ----------------------------------------
        // The same pose Play draws (IceConditionerMotion): root at the simulated hitch and lift, the lift
        // linkage and hoses spanning their pins, cylinders on their clevises; controls mounted on the
        // conditioner ride with it. Nothing is recorded for Undo; every pose is restored before a save,
        // Play, a recompile, an Undo/Redo, or a manual edit of a previewed part (which is not applied).

        static readonly Dictionary<Transform,(Vector3 p,Quaternion r,Vector3 s)> saved=new Dictionary<Transform,(Vector3,Quaternion,Vector3)>();
        static List<IceConditionerLinkage.Span> previewSpans;
        static IceWorld previewWorld;
        static SimulatorConfig previewConfig;
        // An edit made to a posed part may have recorded a prefab override on it; only such new pose overrides
        // on previewed parts are removed again (the user's other overrides are never touched).
        static GameObject previewInstance;
        static PropertyModification[] previewModifications;
        public static float PreviewLift=-1, PreviewTankTilt=-1;
        public static readonly float[] PreviewSteps={0,.25f,.5f,.75f,1};
        public static bool Previewing { get { return saved.Count>0; } }

        [InitializeOnLoadMethod]
        static void HookSaves()
        {
            EditorSceneManager.sceneSaving+=(scene,path)=>EndPreview();
            UnityEditor.SceneManagement.PrefabStage.prefabSaving+=root=>EndPreview();
            EditorApplication.playModeStateChanged+=state=>{ if(state==PlayModeStateChange.ExitingEditMode)EndPreview(); };
            AssemblyReloadEvents.beforeAssemblyReload+=EndPreview;
            Undo.undoRedoPerformed+=EndPreview;
            Undo.postprocessModifications+=DropPreviewEdits;
        }

        static UndoPropertyModification[] DropPreviewEdits(UndoPropertyModification[] modifications)
        {
            if(saved.Count==0)return modifications;
            var kept=new List<UndoPropertyModification>();bool dropped=false;
            foreach(var m in modifications)
            {
                var target=m.currentValue!=null?m.currentValue.target as Transform:null;
                if(target!=null&&saved.ContainsKey(target)){dropped=true;continue;}
                kept.Add(m);
            }
            if(dropped)
            {
                Debug.LogWarning("Lift preview ended: a previewed part was edited while posed. The edit was not applied; the authored pose is restored. Repeat it after the preview.");
                EditorApplication.delayCall+=EndPreview;
            }
            return kept.ToArray();
        }

        /// <summary>Poses the conditioner at the simulated hitch and lift (0 lowered .. 1 raised) and the tank
        /// at a tilt (0..1 of the 58 degree dump), exactly as Play does.</summary>
        public static void Preview(IceWorld w,SimulatorConfig c,float lift,float tankTilt)
        {
            if(Application.isPlaying)return;
            if(saved.Count==0)
            {
                previewSpans=IceConditionerLinkage.Capture(w);
                previewInstance=PrefabUtility.GetNearestPrefabInstanceRoot(w.Machine);
                previewModifications=previewInstance!=null?PrefabUtility.GetPropertyModifications(previewInstance):null;
            }
            w.ConditionerSpans=previewSpans;
            var parts=new List<Transform>{w.Conditioner,w.TankPivot,w.TankDoor};
            foreach(var g in new[]{w.TankCylinderBarrels,w.TankCylinderRods,w.ConditionerCylinderBarrels,w.ConditionerCylinderRods})parts.AddRange(g);
            foreach(var span in previewSpans)parts.Add(span.Part);
            foreach(var t in parts)if(t!=null&&!saved.ContainsKey(t))saved[t]=(t.localPosition,t.localRotation,t.localScale);
            PreviewLift=lift;PreviewTankTilt=tankTilt;previewWorld=w;previewConfig=c;
            IceConditionerMotion.PoseLocal(w,c,lift);
            var tankRest=saved[w.TankPivot].r;
            w.TankPivot.localRotation=tankRest*Quaternion.Euler(tankTilt*58,0,0);
            w.TankDoor.localRotation=Quaternion.Euler(-Mathf.Clamp01((tankTilt-.45f)/.4f)*105,0,0);
            IcePhysicalControls.PoseCylinders(w);
            SceneView.duringSceneGui-=DrawPreview;SceneView.duringSceneGui+=DrawPreview;
            SceneView.RepaintAll();
        }

        public static void EndPreview()
        {
            if(saved.Count==0)return;
            foreach(var pair in saved)if(pair.Key!=null){pair.Key.localPosition=pair.Value.p;pair.Key.localRotation=pair.Value.r;pair.Key.localScale=pair.Value.s;}
            RemovePreviewOverrides();
            saved.Clear();previewSpans=null;previewWorld=null;previewConfig=null;previewInstance=null;previewModifications=null;PreviewLift=PreviewTankTilt=-1;
            SceneView.duringSceneGui-=DrawPreview;
            SceneView.RepaintAll();
        }

        static void RemovePreviewOverrides()
        {
            if(previewInstance==null||previewModifications==null)return;
            var current=PrefabUtility.GetPropertyModifications(previewInstance);
            if(current==null)return;
            string Key(PropertyModification m){ return (m.target!=null?m.target.GetInstanceID():0)+"/"+m.propertyPath; }
            var before=new HashSet<string>();foreach(var m in previewModifications)before.Add(Key(m));
            var posed=new HashSet<UnityEngine.Object>();
            foreach(var t in saved.Keys)if(t!=null){var source=PrefabUtility.GetCorrespondingObjectFromSource(t);if(source!=null)posed.Add(source);}
            var kept=new List<PropertyModification>();
            foreach(var m in current)
            {
                bool pose=m.propertyPath.StartsWith("m_LocalPosition")||m.propertyPath.StartsWith("m_LocalRotation")||m.propertyPath.StartsWith("m_LocalScale")||m.propertyPath.StartsWith("m_LocalEulerAnglesHint");
                if(pose&&m.target!=null&&posed.Contains(m.target)&&!before.Contains(Key(m)))continue;
                kept.Add(m);
            }
            if(kept.Count!=current.Length)PrefabUtility.SetPropertyModifications(previewInstance,kept.ToArray());
        }

        /// <summary>Play generates the drawbar at runtime; the preview draws where it will be.</summary>
        static void DrawPreview(SceneView view)
        {
            var w=previewWorld;var c=previewConfig;
            if(w==null||c==null||w.Machine==null||w.Conditioner==null){SceneView.duringSceneGui-=DrawPreview;return;}
            Vector3 a=w.Machine.TransformPoint(0,.52f,-c.HitchBehindAxle),b=w.Conditioner.TransformPoint(0,.37f,.26f);
            Handles.color=new Color(.55f,.65f,.7f,.9f);
            Handles.DrawLine(a,b,6);
            Handles.Label((a+b)*.5f+w.Machine.up*.08f,"drawbar (generated in Play)  lift "+(PreviewLift*100).ToString("F0")+"%");
        }

        // ---- Scene overlay -----------------------------------------------------------------------

        public static void DrawOverlay(IceWorld w,List<Item> items)
        {
            Transform m=w.Machine;
            foreach(var item in items)
            {
                if(item.Label.Contains("(")&&!item.Label.StartsWith("Conditioner")&&!item.Label.StartsWith("Front")&&!item.Label.StartsWith("Rear"))continue;
                if(item.Label.StartsWith("Cutting")||item.Label.StartsWith("Lift"))continue;
                Vector3 v=m.TransformPoint(item.Visual),s=m.TransformPoint(item.Simulated);
                Color color=item.Kind==Kind.Matches?Color.green:item.Kind==Kind.Blocked?Color.red:new Color(1,.6f,0);
                Handles.color=Color.cyan;Handles.SphereHandleCap(0,s,Quaternion.identity,.05f,EventType.Repaint);
                Handles.color=color;Handles.SphereHandleCap(0,v,Quaternion.identity,.05f,EventType.Repaint);
                if(item.Kind!=Kind.Matches)
                {
                    Handles.DrawDottedLine(v,s,3);
                    Handles.Label((v+s)*.5f+m.up*.06f,item.Label+"  "+item.ErrorMm.ToString("F0")+" mm");
                }
            }
        }
    }
}
