using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IceResurfacer.Presentation
{
    /// <summary>
    /// Picks the moving mechanical parts in their own local space. No hidden UI slider,
    /// input action map change, Rigidbody or event-system focus is involved. The only
    /// stored interaction state is the current mouse gesture; all settings live in Core.
    /// </summary>
    public sealed class IcePhysicalControls
    {
        enum Control { None, Conditioner, Horizontal, Vertical, Dump, Flood, Wash, Blade, Breaker }
        readonly IceResurfacerGame game;
        readonly IceWorld world;
        Control active, hover;
        Mouse ownerMouse;
        Vector2 startPointer, pullAxis;
        float lastAngle;
        bool angleValid;
        readonly Renderer[][] highlightRenderers=new Renderer[9][];
        readonly MaterialPropertyBlock[][] savedBlocks=new MaterialPropertyBlock[9][];
        readonly MaterialPropertyBlock tintBlock=new MaterialPropertyBlock();
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
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
            for(int i=1;i<=8;i++)
            {
                // Runtime batches are confined to each moving part. Fixed plates,
                // neighbors and disabled source meshes never join its highlight.
                highlightRenderers[i]=System.Array.FindAll(TransformFor((Control)i).GetComponentsInChildren<Renderer>(),r=>r.enabled);
                savedBlocks[i]=new MaterialPropertyBlock[highlightRenderers[i].Length];
                for(int j=0;j<savedBlocks[i].Length;j++)savedBlocks[i][j]=new MaterialPropertyBlock();
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
            active=Control.None;hover=Control.None;ownerMouse=null;angleValid=false;Hint="";
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
                active=hover;ownerMouse=mouse;startPointer=pointer;
                Highlight(active,true);
                Transform control=TransformFor(active);
                Vector3 pivot=control.position;
                Vector3 a=game.DriverCamera.WorldToScreenPoint(pivot);
                Vector3 b=game.DriverCamera.WorldToScreenPoint(pivot-world.Machine.forward*.15f);
                pullAxis=new Vector2(b.x-a.x,b.y-a.y).normalized;
                if(pullAxis.sqrMagnitude<.01f)pullAxis=Vector2.down;
                angleValid=PlaneAngle(active,ray,out lastAngle);
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
            else if(active==Control.Flood||active==Control.Wash)
            {
                if(PlaneAngle(active,ray,out float angle))
                {
                    if(angleValid)
                    {
                        float current=active==Control.Flood?c.FloodFlow:c.WashValve;
                        float flow=ValveFlowFromAngle(current*90+Mathf.DeltaAngle(lastAngle,angle));
                        if(active==Control.Flood)c.FloodFlow=flow;else c.WashValve=flow;
                    }
                    lastAngle=angle;angleValid=true;
                }
            }
            else if(active==Control.Blade&&PlaneAngle(active,ray,out float angle))
            {
                if(angleValid)ChangeBlade(-Mathf.DeltaAngle(lastAngle,angle)/360f);
                lastAngle=angle;angleValid=true;
            }
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

        bool PlaneAngle(Control control,Ray ray,out float angle)
        {
            Transform t=TransformFor(control);
            // Work in the FIXED bearing frame; using the turning handle's frame
            // would feed its previous rotation back into the commanded setting.
            Transform frame=t.parent;
            Vector3 center=t.position;
            float height=control==Control.Blade?.064f:game.Sim.Config.WaterValveStemHeight;
            var plane=new Plane(frame.up,center+frame.up*height);
            angle=0;
            if(!plane.Raycast(ray,out float distance)||distance>2.5f)return false;
            Vector3 p=frame.InverseTransformDirection(ray.GetPoint(distance)-center);
            if(p.x*p.x+p.z*p.z<.0003f)return false;
            angle=Mathf.Atan2(-p.z,-p.x)*Mathf.Rad2Deg;
            return true;
        }

        Control Pick(Ray ray)
        {
            Control best=Control.None;float nearest=1.7f;
            for(int i=1;i<=(int)Control.Breaker;i++)
            {
                var kind=(Control)i;Transform t=TransformFor(kind);
                Ray local=new Ray(t.InverseTransformPoint(ray.origin),t.InverseTransformDirection(ray.direction));
                if(kind==Control.Conditioner||kind==Control.Horizontal||kind==Control.Vertical||kind==Control.Dump)
                {
                    // A broad rectangular hit region on a transverse bank can
                    // steal the ray aimed at the next, slightly farther knob.
                    // Pick the actual round grip with 5 mm of usable tolerance.
                    Vector3 to=local.origin-new Vector3(0,.225f,0);
                    float b=Vector3.Dot(to,local.direction),c=to.sqrMagnitude-.040f*.040f;
                    float determinant=b*b-c;
                    if(determinant>=0)
                    {
                        float gripDistance=-b-Mathf.Sqrt(determinant);
                        if(gripDistance>=0&&gripDistance<nearest){nearest=gripDistance;best=kind;}
                    }
                    continue;
                }
                Vector3 center;Vector3 size;
                if(kind==Control.Flood||kind==Control.Wash)
                {
                    // The grip spans -.115 to -.055 m. A box spanning the whole
                    // elbow also catches empty space above a nearer tall valve.
                    center=new Vector3(-.085f,game.Sim.Config.WaterValveStemHeight,0);
                    size=new Vector3(.10f,.044f,.044f);
                }
                else if(kind==Control.Blade)
                { center=new Vector3(0,.02f,0);size=new Vector3(.31f,.13f,.31f); }
                else if(kind==Control.Breaker)
                { center=new Vector3(0,.22f,0);size=new Vector3(.26f,.095f,.095f); }
                else { center=new Vector3(0,.20f,0);size=new Vector3(.10f,.14f,.10f); }
                if(new Bounds(center,size).IntersectRay(local,out float distance)&&distance<nearest)
                { nearest=distance;best=kind; }
            }
            return best;
        }

        Transform TransformFor(Control control)
        {
            switch(control)
            {
                case Control.Conditioner:return world.ConditionerLever;
                case Control.Horizontal:return world.HorizontalAugerLever;
                case Control.Vertical:return world.VerticalAugerLever;
                case Control.Dump:return world.DumpLever;
                case Control.Flood:return world.FloodLever;
                case Control.Wash:return world.WashValve;
                case Control.Blade:return world.BladeWheel;
                default:return world.SnowBreakerPlunger;
            }
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
            world.ConditionerLever.localRotation=Quaternion.Euler(24*c.ConditionerHydraulic,0,0);
            world.DumpLever.localRotation=Quaternion.Euler(24*c.TankHydraulic,0,0);
            world.HorizontalAugerLever.localRotation=Quaternion.Euler(c.HorizontalAuger?24:0,0,0);
            world.VerticalAugerLever.localRotation=Quaternion.Euler(c.VerticalAuger?24:0,0,0);
            world.WashValve.localRotation=Quaternion.Euler(0,-90*c.WashValve,0);
            world.FloodLever.localRotation=Quaternion.Euler(0,-90*c.FloodFlow,0);
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                Cylinder(world.TankCylinderBarrels[i],world.TankCylinderRods[i],
                    world.Machine.TransformPoint(side*.69f,.55f,.72f),world.TankPivot.TransformPoint(game.Sim.Config.TankLateralOffset+side*.69f,game.Sim.Config.TankGeometryY(.40f),-.74f),.095f,.043f,.64f);
                Cylinder(world.ConditionerCylinderBarrels[i],world.ConditionerCylinderRods[i],
                    world.Machine.TransformPoint(side*.55f,.76f,-.57f),world.Conditioner.TransformPoint(side*.55f,.43f,.30f),.074f,.035f,.42f);
            }
        }

        static void Cylinder(Transform barrel,Transform rod,Vector3 anchor,Vector3 clevis,float bore,float shaft,float barrelLength)
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
