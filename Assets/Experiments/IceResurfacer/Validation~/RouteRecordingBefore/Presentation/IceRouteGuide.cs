using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace IceResurfacer.Presentation
{
    /// <summary>Read-only teaching aid: F5 changes drawing only, never drive or scoring.</summary>
    public sealed class IceRouteGuide:MonoBehaviour
    {
        IceResurfacerGame game;
        Simulation session;
        LineRenderer line;
        Material material;
        readonly Vector3[] upcoming=new Vector3[120];
        public ResurfacingRoute Route {get;private set;}
        public bool Visible {get;private set;}
        public bool Available {get;private set;}=true;
        public int NextPoint {get;private set;}
        public string Phase => Route==null?"":NextPoint<Route.WorkStart?"ENTRY / REVERSE THEN TURN":NextPoint<Route.WorkEnd?"OVERLAPPING PASSES":"RETURN TO SERVICE";
        public void Initialize(IceResurfacerGame owner)
        {
            game=owner;
            material=new Material(Resources.Load<Shader>("CollisionOverlay")){name="Aurora optional route guide"};
            material.SetColor("_BaseColor",new Color(.30f,.90f,.75f,.40f));
            var node=new GameObject("F5 advisory upcoming route");node.transform.SetParent(transform,false);
            line=node.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;
            line.startWidth=.075f;line.endWidth=.025f;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;
            Refresh();
        }
        public void Toggle()
        {
            Refresh();
            if(!Available){game.Notice="The reference route is calibrated for Northline's standard rink and machinery geometry. Free driving remains available.";return;}
            Visible=!Visible;Refresh();
        }
        void Update()
        {
            if(game==null)return;
            if(Keyboard.current!=null&&Keyboard.current.f5Key.wasPressedThisFrame)Toggle();
            Refresh();
        }
        public void Refresh()
        {
            if(game==null||game.Sim==null||line==null)return;
            if(session!=game.Sim)
            {
                session=game.Sim;var c=session.Config;
                Available=Mathf.Abs(c.RinkWidth-26)<.001f&&Mathf.Abs(c.RinkLength-60)<.001f&&Mathf.Abs(c.CornerRadius-8.5f)<.001f;
                Route=null;NextPoint=1;
                if(Available)
                {
                    try {Route=new ResurfacingRoute(c);}
                    catch(System.InvalidOperationException){Available=false;}
                }
                if(!Available)Visible=false;
            }
            if(!Available){line.enabled=false;return;}
            line.enabled=Visible&&game.Started&&!session.OnFoot&&!session.Finished;
            // Advance sequentially: crossing a later pass cannot skip the route.
            for(int i=0;game.Started&&i<20&&NextPoint<Route.Points.Count-1;i++)
            {
                var p=Route.Points[NextPoint];float dx=p.X-session.Vehicle.X,dz=p.Z-session.Vehicle.Z;
                if(dx*dx+dz*dz>2.25f)break;NextPoint++;
            }
            if(!line.enabled)return;
            int count=Mathf.Min(upcoming.Length,Route.Points.Count-NextPoint);
            line.positionCount=count;
            for(int i=0;i<count;i++)
            {var p=Route.Points[NextPoint+i];upcoming[i]=new Vector3(p.X,.025f,p.Z);line.SetPosition(i,upcoming[i]);}
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
