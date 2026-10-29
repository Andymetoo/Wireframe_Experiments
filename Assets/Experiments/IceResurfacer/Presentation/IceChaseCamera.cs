using UnityEngine;

namespace IceResurfacer.Presentation
{
    // Presentation only. The existing camera follows a body-relative target; reversing
    // never flips the view. The procedural arena has no runtime physics colliders,
    // so its shared floor outline is the wall authority, including the diagonal portal.
    public sealed class IceChaseCamera
    {
        readonly IceResurfacerGame game;
        Vector3 position;
        float orbitYaw, orbitPitch;
        public bool Active { get; private set; }
        public IceChaseCamera(IceResurfacerGame owner) { game=owner; }
        public void Enter()
        {
            if(Active)return;
            Active=true; orbitYaw=orbitPitch=0;
            position=game.DriverCamera.transform.position;
            game.DriverCamera.transform.SetParent(game.transform,true);
        }
        public void Leave()
        {
            if(!Active)return;
            Active=false;game.DriverCamera.transform.SetParent(game.World.CameraMount,true);
        }
        public void Orbit(Vector2 delta)
        { orbitYaw=Mathf.Clamp(orbitYaw+delta.x*.11f,-60,60);orbitPitch=Mathf.Clamp(orbitPitch-delta.y*.08f,-12,20); }
        public bool Clear(Vector3 point)
        {
            float ceiling=game.Sim.Layout.IsPortal(point.x,point.z)?3.65f:4.25f;
            return point.y>.25f&&point.y<ceiling&&
                (game.Sim.Ice.IsInside(point.x,point.z,.20f)||game.Sim.Layout.ContainsFloor(point.x,point.z,.20f));
        }
        Vector3 Clip(Vector3 focus,Vector3 candidate)
        {
            Vector3 last=focus;
            int steps=Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(focus,candidate)/.12f),1,96);
            for(int i=1;i<=steps;i++)
            {
                Vector3 point=Vector3.Lerp(focus,candidate,i/(float)steps);
                if(!Clear(point))
                {
                    Vector3 blocked=point;
                    for(int j=0;j<7;j++) { Vector3 middle=(last+blocked)*.5f;if(Clear(middle))last=middle;else blocked=middle; }
                    // Retraction at a nearby wall must look over the seat/body,
                    // rather than placing the external lens inside the machine.
                    last.y=Mathf.Max(last.y,2.65f);
                    return last;
                }
                last=point;
            }
            return candidate;
        }
        public void Tick(float dt)
        {
            if(!Active)return;
            Transform machine=game.World.Machine;
            Vector3 focus=machine.TransformPoint(0,1.05f,.55f);
            Vector3 offset=Quaternion.Euler(orbitPitch,orbitYaw,0)*new Vector3(-.65f,2.25f,-6.65f);
            Vector3 desired=Clip(focus,focus+machine.TransformDirection(offset));
            // Smooth ordinary travel, but constrain the smoothed path immediately
            // when a turn would otherwise carry the camera through a board or wall.
            position=Clip(focus,Vector3.Lerp(position,desired,1-Mathf.Exp(-8*dt)));
            Transform camera=game.DriverCamera.transform;
            camera.position=position;
            Quaternion rotation=Quaternion.LookRotation(focus-position,Vector3.up);
            camera.rotation=Quaternion.Slerp(camera.rotation,rotation,1-Mathf.Exp(-10*dt));
        }
    }
}
