using System.Collections.Generic;
using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// Picks the moving mechanical parts in their own local space. No hidden UI slider,
    /// input action map change, Rigidbody or event-system focus is involved. The only
    /// stored interaction state is the current mouse gesture; all settings live in Core.
    /// Grips are measured from the authored parts, so a control mounted anywhere (including
    /// on the moving conditioner) is picked, highlighted and turned where it is drawn.
    /// </summary>
    public sealed class IcePhysicalControls
    {
        enum Control { None, Conditioner, Horizontal, Vertical, Dump, Flood, Wash, Blade, Breaker }
        public const int ControlCount=8;

        /// <summary>Where a control is grabbed, in the control's own (possibly scaled) frame.</summary>
        public struct Grip
        {
            public Vector3 Center, Size;   // box (valves, breaker); Center is also the lever ball and blade hub
            public float Radius;           // lever ball; blade disc
            public float Thickness;        // blade disc
            public Vector3 Knob;           // blade spinner knob
            public float KnobRadius;
            public Vector3 Tolerance;      // forgiving margin added to the drawn grip (box size, or x = radius)
            public bool Measured;          // false: the generated layout's constants were used
        }
        /// <summary>A part of the seat that hides controls behind it (mesh bounds in the part's frame).</summary>
        public struct Occluder { public Transform Part; public Bounds Local; }

        readonly IceResurfacerGame game;
        readonly IceWorld world;
        readonly Grip[] grips;
        Control active, hover;
        Mouse ownerMouse;
        Vector2 startPointer, lastPointer, pullAxis;
        // Edge-on turning: where the hand is relative to the grip's path, kept between frames, and the way
        // the control was last turning (0 after a stop).
        Vector2 pathOffset; float lastTurn;
        readonly Renderer[][] highlightRenderers=new Renderer[9][];
        readonly MaterialPropertyBlock[][] savedBlocks=new MaterialPropertyBlock[9][];
        readonly MaterialPropertyBlock tintBlock=new MaterialPropertyBlock();
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        // Authored rest orientation of each moving control; animation is applied on top of it.
        readonly Quaternion[] restRotations=new Quaternion[9];
        Control highlighted;
        bool highlightedDrag;
        public string Hint { get; private set; } = "";
        public string ActiveControl { get { return Name(active); } }
        public string HighlightedControl { get { return Name(highlighted); } }
        public bool IsDragging { get { return active!=Control.None; } }
        public string GetHitControl(Vector2 screenPosition)
        { return Name(Pick(game.DriverCamera.ScreenPointToRay(screenPosition))); }

        public IcePhysicalControls(IceResurfacerGame owner)
        {
            game=owner;world=owner.World;
            grips=world.ControlGrips??MeasureGrips(world,owner.calibration);
            for(int i=1;i<=8;i++)
            {
                // Runtime batches are confined to each moving part. Fixed plates,
                // neighbors and disabled source meshes never join its highlight.
                highlightRenderers[i]=System.Array.FindAll(TransformFor((Control)i).GetComponentsInChildren<Renderer>(),r=>r.enabled);
                savedBlocks[i]=new MaterialPropertyBlock[highlightRenderers[i].Length];
                for(int j=0;j<savedBlocks[i].Length;j++)savedBlocks[i][j]=new MaterialPropertyBlock();
                restRotations[i]=TransformFor((Control)i).localRotation;
            }
        }

        void Highlight(Control control,bool dragging)
        {
            if(game.HudHidden){control=Control.None;dragging=false;}
            if(highlighted==control&&highlightedDrag==dragging)return;
            if(highlighted!=Control.None)
            {
                var renderers=highlightRenderers[(int)highlighted];var blocks=savedBlocks[(int)highlighted];
                for(int i=0;i<renderers.Length;i++)renderers[i].SetPropertyBlock(blocks[i].isEmpty?null:blocks[i]);
            }
            highlighted=control;highlightedDrag=dragging;
            if(control==Control.None)return;
            var selected=highlightRenderers[(int)control];var saved=savedBlocks[(int)control];
            for(int i=0;i<selected.Length;i++)
            {
                var renderer=selected[i];var material=renderer.sharedMaterial;
                renderer.GetPropertyBlock(saved[i]);renderer.GetPropertyBlock(tintBlock);
                if(material!=null&&material.HasProperty(BaseColor))
                {
                    Color original=saved[i].HasColor(BaseColor)?saved[i].GetColor(BaseColor):material.GetColor(BaseColor);
                    Color tint=Color.Lerp(original,new Color(1f,.72f,.30f,original.a),dragging?.42f:.25f);
                    tintBlock.SetColor(BaseColor,tint);renderer.SetPropertyBlock(tintBlock);
                }
            }
        }
        public void RefreshVisibility(){if(game.HudHidden)Highlight(Control.None,false);}

        public void Release()
        {
            if(game.Sim!=null)
            {
                if(active==Control.Conditioner)game.Sim.Controls.ConditionerHydraulic=0;
                if(active==Control.Dump)game.Sim.Controls.TankHydraulic=0;
            }
            active=Control.None;hover=Control.None;ownerMouse=null;Hint="";
            Highlight(Control.None,false);
        }

        public void UpdateInput(bool enabled,bool pointerOverHud)
        {
            var mouse=Mouse.current;
            if(!enabled||mouse==null||!mouse.added||game.Sim==null||
               (active!=Control.None&&(mouse!=ownerMouse||!mouse.leftButton.isPressed)))Release();
            if(!enabled||mouse==null||!mouse.added||game.Sim==null)return;
            var pointer=mouse.position.ReadValue();
            Ray ray=game.DriverCamera.ScreenPointToRay(pointer);
            if(active==Control.None)
            {
                hover=pointerOverHud?Control.None:Pick(ray);
                Hint=Guidance(hover);
                Highlight(hover,false);
                if(hover==Control.None)return;
                if(hover==Control.Blade&&Mathf.Abs(mouse.scroll.ReadValue().y)>.1f)
                    ChangeBlade(mouse.scroll.ReadValue().y/120f*.025f);
                if(!mouse.leftButton.wasPressedThisFrame)return;
                active=hover;ownerMouse=mouse;startPointer=lastPointer=pointer;pathOffset=Vector2.zero;lastTurn=0;
                Highlight(active,true);
                Transform control=TransformFor(active);
                Vector3 pivot=control.position;
                Vector3 a=game.DriverCamera.WorldToScreenPoint(pivot);
                // Pull toward the seat along the lever's own fixed bank, wherever it is mounted.
                Vector3 b=game.DriverCamera.WorldToScreenPoint(pivot-control.parent.forward*.15f);
                pullAxis=new Vector2(b.x-a.x,b.y-a.y).normalized;
                if(pullAxis.sqrMagnitude<.01f)pullAxis=Vector2.down;
                switch(active)
                {
                    case Control.Horizontal: game.Sim.Controls.HorizontalAuger=!game.Sim.Controls.HorizontalAuger;break;
                    case Control.Vertical: game.Sim.Controls.VerticalAuger=!game.Sim.Controls.VerticalAuger;break;
                    case Control.Breaker: game.OperateSnowBreaker();break;
                }
            }
            var c=game.Sim.Controls;
            float pull=Vector2.Dot(pointer-startPointer,pullAxis);
            float demand=Mathf.Abs(pull)<8?0:Mathf.Clamp(pull/Mathf.Max(22,Screen.height*.035f),-1,1);
            // Pull is toward the seat (-machine forward). Core demand is positive
            // for raising, so both hydraulic spools lower on a backward pull.
            if(active==Control.Conditioner)c.ConditionerHydraulic=-demand;
            else if(active==Control.Dump)c.TankHydraulic=-demand;
            else if(active==Control.Flood||active==Control.Wash||active==Control.Blade)
            {
                // Both pointer positions are projected through the control as it is drawn now, so
                // the setting follows the hand only: a conditioner (or machine) moving under a held,
                // still pointer turns nothing.
                if(TurnDelta(active,lastPointer,pointer,out float delta))
                {
                    float turned;
                    if(active==Control.Blade)
                    {
                        float depth=c.BladeDepthMm;ChangeBlade(-delta/360f);
                        turned=(depth-c.BladeDepthMm)/game.Sim.Config.MaxBladeDepthMm*game.Sim.Config.BladeWheelTurns*360f;
                    }
                    else
                    {
                        float current=active==Control.Flood?c.FloodFlow:c.WashValve;
                        float flow=ValveFlowFromAngle(current*90+delta);
                        if(active==Control.Flood)c.FloodFlow=flow;else c.WashValve=flow;
                        turned=(flow-current)*90;
                    }
                    if(Mathf.Abs(turned)<.5f*Mathf.Abs(delta))lastTurn=0;
                }
            }
            lastPointer=pointer;
            Hint=Guidance(active);
        }

        // Zero is the arm at 9 o'clock, positive 90 is at 6 o'clock as seen
        // looking down on the fixed manifold. Values outside the quadrant hit stops.
        public static float ValveFlowFromAngle(float degrees)
        {
            return Mathf.Clamp01(degrees/90f);
        }

        void ChangeBlade(float turns)
        {
            var c=game.Sim.Controls;var config=game.Sim.Config;
            c.BladeDepthMm=Mathf.Clamp(c.BladeDepthMm+turns/config.BladeWheelTurns*config.MaxBladeDepthMm,0,config.MaxBladeDepthMm);
        }

        bool TurnDelta(Control control,Vector2 from,Vector2 to,out float delta)
        {
            delta=0;
            if((to-from).sqrMagnitude<.01f)return false;
            var camera=game.DriverCamera;
            Transform t=TransformFor(control);
            Vector3 axis=t.parent.rotation*restRotations[(int)control]*Vector3.up;
            Ray ray=camera.ScreenPointToRay(to);
            // Seen well off its plane, the pointer's angle about the stem is exact.
            if(Mathf.Abs(Vector3.Dot(ray.direction,axis))>=GrazingView&&
               PlaneAngle(control,camera.ScreenPointToRay(from),out float before)&&PlaneAngle(control,ray,out float after))
            { delta=Mathf.DeltaAngle(before,after); pathOffset=Vector2.zero; if(Mathf.Abs(delta)>.05f)lastTurn=Mathf.Sign(delta); return true; }
            // Seen nearly edge-on (a handle at eye height), that projection is ill-conditioned: a pixel can swing
            // it through the stem. Follow the grip's own visible path instead (a thin ellipse): turn it to the
            // point of that path nearest the hand, within 45 deg a frame. Where both sides of the ellipse pass
            // near the hand (round the ends of the stroke), the handle keeps turning the way it was going unless
            // the other side is clearly nearer, and never takes a much longer turn for a few pixels. What the hand
            // is off the path stays between frames; what lies past an end stop does not.
            var g=grips[(int)control];
            Vector3 pivot=t.position,arm=t.TransformPoint(control==Control.Blade?g.Knob:g.Center)-pivot;
            Vector2 At(float turned){return camera.WorldToScreenPoint(pivot+Quaternion.AngleAxis(-turned,axis)*arm);}
            if(Mathf.Max((At(1)-At(0)).sqrMagnitude,(At(91)-At(90)).sqrMagnitude,(At(-89)-At(-90)).sqrMagnitude)<.0025f)return false;
            Vector2 hand=At(0)+pathOffset+(to-from);
            float Miss(float turned){return (At(turned)-hand).sqrMagnitude;}
            // Every local nearest point along the 1-deg scan, refined to 1/16 deg.
            float best=float.MaxValue,before2=float.MaxValue,before1=Miss(-45);
            for(int a=-44;a<=46;a++)
            {
                float miss=a<=45?Miss(a):float.MaxValue;
                if(before1<=before2&&before1<=miss)
                {
                    float at=a-1,nearest=before1;
                    for(float step=.5f;step>.05f;step*=.5f)
                    for(int dir=1;dir>=-1;dir-=2)
                    {
                        float next=Mathf.Clamp(at+dir*step,-45,45),m=Miss(next);
                        if(m<nearest){nearest=m;at=next;}
                    }
                    float cost=nearest+LongerTurn*at*at+(at*lastTurn<-.05f?Reverse:0);
                    if(cost<best){best=cost;delta=at;}
                }
                before2=before1;before1=miss;
            }
            pathOffset=hand-At(delta);
            if(Mathf.Abs(delta)>.05f)lastTurn=Mathf.Sign(delta);
            return true;
        }
        // |cos| between the view ray and the turning axis below which the plane projection is not used (about 15 deg).
        const float GrazingView=.26f;
        // px^2 per deg^2: the far side of the ellipse must be about 6 px nearer the hand to take a 45 deg turn to it.
        const float LongerTurn=.018f;
        // px^2: turning back must be about 3 px nearer the hand than carrying on (a hand jitters by about 2).
        const float Reverse=9f;

        bool PlaneAngle(Control control,Ray ray,out float angle)
        {
            Transform t=TransformFor(control);
            // Work in the FIXED bearing frame at the control's rest orientation; using the turning
            // handle's frame would feed its previous rotation back into the commanded setting.
            Quaternion rest=t.parent.rotation*restRotations[(int)control];
            Vector3 axis=rest*Vector3.up,center=t.position;
            var grip=grips[(int)control];
            // The plane through the part the hand turns: the valve grip, or the handwheel's spinner knob
            // (scaled with the control), so pointer travel round it maps 1:1 onto the turn.
            float local=control==Control.Blade?grip.Knob.y:grip.Center.y;
            float height=Vector3.Dot(t.TransformVector(new Vector3(0,local,0)),axis);
            var plane=new Plane(axis,center+axis*height);
            angle=0;
            if(!plane.Raycast(ray,out float distance)||distance>2.5f)return false;
            Vector3 p=Quaternion.Inverse(rest)*(ray.GetPoint(distance)-center);
            if(p.x*p.x+p.z*p.z<.0003f)return false;
            angle=Mathf.Atan2(-p.z,-p.x)*Mathf.Rad2Deg;
            return true;
        }

        Control Pick(Ray ray)
        {
            // Hit regions are in each control's own (possibly scaled) space; reach and ordering compare
            // world distances, so an authored, rescaled mount stays pickable. A control drawn under the
            // pointer always wins; the forgiving margins only apply where no drawn grip is hit, so one
            // control's margin can never steal a click from another control's visible part.
            var best=Pick(ray,false);
            return best!=Control.None?best:Pick(ray,true);
        }

        Control Pick(Ray ray,bool tolerant)
        {
            Control best=Control.None;float nearest=1.7f;
            for(int i=1;i<=(int)Control.Breaker;i++)
            {
                var kind=(Control)i;
                float hit=HitDistance(kind,ray,tolerant);
                if(hit<nearest&&!Occluded(ray,hit)){nearest=hit;best=kind;}
            }
            return best;
        }

        /// <summary>World distance along the ray to the control's grip (drawn, or with its margin), or +infinity.</summary>
        float HitDistance(Control kind,Ray ray,bool tolerant)
        {
            Transform t=TransformFor(kind);var g=grips[(int)kind];
            if(!tolerant)
            {
                g.Size-=g.Tolerance;
                if(kind==Control.Blade){g.Radius-=g.Tolerance.x;g.KnobRadius-=g.Tolerance.y;}
                else g.Radius-=g.Tolerance.x;
            }
            Ray local=new Ray(t.InverseTransformPoint(ray.origin),t.InverseTransformDirection(ray.direction));
            float World(float along){ return Vector3.Distance(ray.origin,t.TransformPoint(local.GetPoint(along))); }
            float best=float.PositiveInfinity;
            if(kind==Control.Conditioner||kind==Control.Horizontal||kind==Control.Vertical||kind==Control.Dump)
            {
                // A broad rectangular hit region on a transverse bank can steal the ray aimed at
                // the next, slightly farther knob. Pick the actual round grip with 5 mm tolerance.
                if(Sphere(local,g.Center,g.Radius,out float along))best=World(along);
                return best;
            }
            if(kind==Control.Blade)
            {
                // Rim, spokes and hub as a thin disc, plus the spinner knob: a thick box over
                // the wheel also caught rays aimed at a valve grip mounted just above the rim.
                if(new Bounds(g.Center,new Vector3(g.Radius*2,g.Thickness,g.Radius*2)).IntersectRay(local,out float face))
                {
                    Vector3 p=local.GetPoint(face)-g.Center;
                    if(new Vector2(p.x,p.z).magnitude<=g.Radius)best=Mathf.Min(best,World(face));
                }
                if(Sphere(local,g.Knob,g.KnobRadius,out float along))best=Mathf.Min(best,World(along));
                return best;
            }
            // Valves: the grip spans only its own length; a box over the whole elbow also catches
            // empty space above a nearer tall valve. Breaker: its T handle.
            if(new Bounds(g.Center,g.Size).IntersectRay(local,out float distance))best=World(distance);
            return best;
        }

        static bool Sphere(Ray local,Vector3 center,float radius,out float along)
        {
            Vector3 to=local.origin-center;
            float b=Vector3.Dot(to,local.direction),c=to.sqrMagnitude-radius*radius,determinant=b*b-c;
            along=determinant>=0?-b-Mathf.Sqrt(determinant):-1;
            return along>=0;
        }

        /// <summary>The seat in front of a control hides it; a ray starting inside a seat part does not.</summary>
        bool Occluded(Ray ray,float distance)
        {
            if(world.ControlOccluders==null)return false;
            foreach(var o in world.ControlOccluders)
            {
                if(o.Part==null||!o.Part.gameObject.activeInHierarchy)continue;
                Ray local=new Ray(o.Part.InverseTransformPoint(ray.origin),o.Part.InverseTransformDirection(ray.direction));
                if(o.Local.Contains(local.origin)||!o.Local.IntersectRay(local,out float along))continue;
                if(Vector3.Distance(ray.origin,o.Part.TransformPoint(local.GetPoint(along)))<distance-.01f)return true;
            }
            return false;
        }

        Transform TransformFor(Control control) { return TransformFor(world,(int)control); }
        static Transform TransformFor(IceWorld w,int control)
        {
            switch((Control)control)
            {
                case Control.Conditioner:return w.ConditionerLever;
                case Control.Horizontal:return w.HorizontalAugerLever;
                case Control.Vertical:return w.VerticalAugerLever;
                case Control.Dump:return w.DumpLever;
                case Control.Flood:return w.FloodLever;
                case Control.Wash:return w.WashValve;
                case Control.Blade:return w.BladeWheel;
                default:return w.SnowBreakerPlunger;
            }
        }

        // ---- Grip geometry, measured once from the unbatched parts --------------------------------

        static readonly string[] GripParts={"","Black phenolic hydraulic grip","Black phenolic hydraulic grip","Black phenolic hydraulic grip","Black phenolic hydraulic grip","Valve grip","Valve grip","Blade handwheel rim","Snow breaker T handle"};

        /// <summary>
        /// Each control's grip in its own frame, from the named grip part when present (so lengthened
        /// valve stems, rescaled banks and controls moved onto the conditioner are picked where drawn),
        /// otherwise the generated layout's constants. Hit tolerances match the original regions.
        /// </summary>
        public static Grip[] MeasureGrips(IceWorld w,SimulatorConfig config)
        {
            var grips=new Grip[9];
            float stem=config!=null?config.WaterValveStemHeight:.24f;
            for(int i=1;i<=8;i++)
            {
                Transform t=TransformFor(w,i);
                bool found=PartBounds(t,GripParts[i],out Bounds b);
                switch((Control)i)
                {
                    case Control.Flood:case Control.Wash:
                        grips[i]=found?new Grip{Center=b.center,Size=b.size+new Vector3(.04f,.01f,.01f),Tolerance=new Vector3(.04f,.01f,.01f),Measured=true}
                            :new Grip{Center=new Vector3(-.085f,stem,0),Size=new Vector3(.10f,.044f,.044f),Tolerance=new Vector3(.04f,.01f,.01f)};
                        break;
                    case Control.Blade:
                        float rim=found?Mathf.Max(b.max.x,-b.min.x,b.max.z,-b.min.z):.148f;
                        var crank=Child(t,"Handwheel crank");
                        grips[i]=new Grip{Center=Vector3.zero,Radius=rim+.017f,Thickness=.07f,Knob=crank!=null?t.InverseTransformPoint(crank.position):new Vector3(.11f,.045f,0),KnobRadius=.045f,
                            Tolerance=new Vector3(.017f,.02f,0),Measured=found};
                        break;
                    case Control.Breaker:
                        grips[i]=found?new Grip{Center=b.center,Size=b.size+new Vector3(.05f,.057f,.057f),Tolerance=new Vector3(.05f,.057f,.057f),Measured=true}
                            :new Grip{Center=new Vector3(0,.22f,0),Size=new Vector3(.26f,.095f,.095f),Tolerance=new Vector3(.05f,.057f,.057f)};
                        break;
                    default:
                        grips[i]=found?new Grip{Center=b.center,Radius=Mathf.Max(b.extents.x,b.extents.y,b.extents.z)+.005f,Tolerance=new Vector3(.005f,0,0),Measured=true}
                            :new Grip{Center=new Vector3(0,.225f,0),Radius=.040f,Tolerance=new Vector3(.005f,0,0)};
                        break;
                }
            }
            return grips;
        }

        /// <summary>World position of each control's grip (index 1..8), as picking targets it.</summary>
        public static Vector3[] GripPositions(IceWorld w,SimulatorConfig config)
        {
            var grips=w.ControlGrips??MeasureGrips(w,config);
            var points=new Vector3[9];
            for(int i=1;i<=8;i++)points[i]=TransformFor(w,i).TransformPoint(i==7?grips[i].Knob:grips[i].Center);
            return points;
        }
        /// <summary>The handwheel rim radius in the wheel's frame (for framing the whole wheel).</summary>
        public static float BladeRimRadius(IceWorld w,SimulatorConfig config)
        {
            var grips=w.ControlGrips??MeasureGrips(w,config);
            return grips[7].Radius-.017f;
        }

        public static List<Occluder> MeasureOccluders(IceWorld w)
        {
            var list=new List<Occluder>();
            if(w.Seat==null)return list;
            foreach(var renderer in w.Seat.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter==null||filter.sharedMesh==null||renderer.GetComponent<TextMesh>()!=null)continue;
                list.Add(new Occluder{Part=renderer.transform,Local=filter.sharedMesh.bounds});
            }
            return list;
        }

        static bool PartBounds(Transform control,string name,out Bounds bounds)
        {
            bounds=default;
            var part=Child(control,name);
            var filter=part!=null?part.GetComponent<MeshFilter>():null;
            if(filter==null||filter.sharedMesh==null)return false;
            var b=filter.sharedMesh.bounds;bool any=false;
            for(int i=0;i<8;i++)
            {
                Vector3 corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 p=control.InverseTransformPoint(part.TransformPoint(corner));
                if(!any){bounds=new Bounds(p,Vector3.zero);any=true;}else bounds.Encapsulate(p);
            }
            return true;
        }
        static Transform Child(Transform root,string name)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;
            return null;
        }

        static string Name(Control control)
        {
            switch(control)
            {
                case Control.Conditioner:return "Conditioner hydraulic lever";
                case Control.Horizontal:return "Horizontal auger lever";
                case Control.Vertical:return "Vertical auger lever";
                case Control.Dump:return "Snow tank hydraulic lever";
                case Control.Flood:return "Flood water valve";
                case Control.Wash:return "Wash water valve";
                case Control.Blade:return "Blade handwheel";
                case Control.Breaker:return "Snow-breaker plunger";
                default:return "";
            }
        }

        static string Guidance(Control control)
        {
            switch(control)
            {
                case Control.Conditioner:case Control.Dump:
                    return Name(control)+" / LMB pull BACK toward seat to LOWER; push FORWARD to RAISE. Release to hold.";
                case Control.Horizontal:case Control.Vertical:return Name(control)+" / click to latch RUN or OFF.";
                case Control.Flood:case Control.Wash:return Name(control)+" / drag handle: 9 o'clock CLOSED to 6 o'clock OPEN.";
                case Control.Blade:return "Blade handwheel / rotate or scroll for fine adjustment; 3.5 turns stop to stop.";
                case Control.Breaker:return "Snow-breaker plunger / click for one complete down-and-return stroke.";
                default:return "";
            }
        }

        public void Animate()
        {
            if(game.Sim==null)return;
            var c=game.Sim.Controls;
            world.ConditionerLever.localRotation=restRotations[(int)Control.Conditioner]*Quaternion.Euler(24*c.ConditionerHydraulic,0,0);
            world.DumpLever.localRotation=restRotations[(int)Control.Dump]*Quaternion.Euler(24*c.TankHydraulic,0,0);
            world.HorizontalAugerLever.localRotation=restRotations[(int)Control.Horizontal]*Quaternion.Euler(c.HorizontalAuger?24:0,0,0);
            world.VerticalAugerLever.localRotation=restRotations[(int)Control.Vertical]*Quaternion.Euler(c.VerticalAuger?24:0,0,0);
            world.WashValve.localRotation=restRotations[(int)Control.Wash]*Quaternion.Euler(0,-90*c.WashValve,0);
            world.FloodLever.localRotation=restRotations[(int)Control.Flood]*Quaternion.Euler(0,-90*c.FloodFlow,0);
            PoseCylinders(world);
        }

        /// <summary>Hydraulic barrels and rods span their clevis pins, wherever those are authored.</summary>
        public static void PoseCylinders(IceWorld world)
        {
            for(int i=0;i<2;i++)
            {
                Cylinder(world.TankCylinderBarrels[i],world.TankCylinderRods[i],Pin(world.TankCylinderBase[i]),Pin(world.TankCylinderClevis[i]),.095f,.043f,.64f);
                Cylinder(world.ConditionerCylinderBarrels[i],world.ConditionerCylinderRods[i],Pin(world.ConditionerCylinderBase[i]),Pin(world.ConditionerCylinderClevis[i]),.074f,.035f,.42f);
            }
        }
        static Vector3 Pin(Transform pin) { return pin.parent.TransformPoint(pin.localPosition); }

        public static void Cylinder(Transform barrel,Transform rod,Vector3 anchor,Vector3 clevis,float bore,float shaft,float barrelLength)
        {
            Vector3 axis=clevis-anchor;float length=axis.magnitude;
            if(length<.001f)return;
            axis/=length;
            float body=Mathf.Min(barrelLength,length*.78f);
            barrel.SetPositionAndRotation(anchor+axis*body*.5f,Quaternion.FromToRotation(Vector3.up,axis));
            barrel.localScale=new Vector3(bore,body*.5f,bore);
            float pistonStart=body*.78f;
            rod.SetPositionAndRotation(anchor+axis*(pistonStart+length)*.5f,barrel.rotation);
            rod.localScale=new Vector3(shaft,(length-pistonStart)*.5f,shaft);
        }
    }
}
