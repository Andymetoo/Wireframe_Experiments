using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace IceResurfacer.Presentation
{
    /// <summary>Opt-in F3 geometry evidence. Fixed line renderers; no pose correction.</summary>
    public sealed class IceCollisionOverlay:MonoBehaviour
    {
        IceResurfacerGame game;
        readonly LineRenderer[] lines=new LineRenderer[6];
        readonly Material[] materials=new Material[6];
        public void Initialize(IceResurfacerGame owner)
        {
            game=owner;
            string[] names={"F3 accepted body / green","F3 rejected body / red","F3 rendered pit / cyan","F3 collision pit / amber","F3 actual contact normal / magenta","F3 contact point / magenta"};
            Color[] colors={new Color(.2f,1,.4f,.9f),new Color(1,.15f,.12f,.9f),new Color(.1f,.8f,1,.9f),new Color(1,.7f,.05f,.9f),new Color(1,.2f,1,.9f),new Color(1,.2f,1,.9f)};
            Shader shader=Resources.Load<Shader>("CollisionOverlay");
            for(int i=0;i<lines.Length;i++)
            {
                var node=new GameObject(names[i]);node.transform.SetParent(transform,false);
                materials[i]=new Material(shader){name=names[i]};materials[i].SetColor("_BaseColor",colors[i]);
                var line=node.AddComponent<LineRenderer>();line.sharedMaterial=materials[i];line.useWorldSpace=true;
                line.startWidth=line.endWidth=i==2?.028f:.017f;line.positionCount=i<4?5:i==4?4:5;
                line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;lines[i]=line;
            }
        }
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            if(game==null||game.Sim==null)return;
            bool visible=game.DiagnosticsVisible;var s=game.Sim;var hit=s.LastMovementContact;
            for(int i=0;i<lines.Length;i++)lines[i].enabled=visible&&(i!=1&&i<4||hit.Obstacle!=MovementObstacle.None);
            if(!visible)return;
            Body(lines[0],s.Vehicle.X,s.Vehicle.Z,s.Vehicle.Heading,.20f);
            Body(lines[1],hit.VehicleX,hit.VehicleZ,hit.Heading,.25f);
            Pit(lines[2],0,.135f);Pit(lines[3],ServiceLayout.PitCollisionClearance,.18f);
            Vector3 contact=new Vector3(hit.PointX,Mathf.Max(.32f,hit.PointY),hit.PointZ),normal=new Vector3(hit.NormalX,0,hit.NormalZ);
            Vector3 end=contact+normal*.85f,tangent=new Vector3(-normal.z,0,normal.x)*.12f;
            lines[4].SetPosition(0,contact);lines[4].SetPosition(1,end);lines[4].SetPosition(2,end-normal*.20f+tangent);lines[4].SetPosition(3,end);
            for(int i=0;i<5;i++){float a=i*Mathf.PI*.5f;lines[5].SetPosition(i,contact+new Vector3(Mathf.Cos(a)*.09f,0,Mathf.Sin(a)*.09f));}
        }
        void Body(LineRenderer line,float x,float z,float heading,float y)
        {
            var c=game.Sim.Config;float sn=Mathf.Sin(heading),cs=Mathf.Cos(heading);
            for(int i=0;i<5;i++)
            {
                int corner=i%4;float lx=corner==0||corner==3?-c.VehicleHalfWidth:c.VehicleHalfWidth,lz=corner<2?-c.BodyRear:c.BodyFront;
                line.SetPosition(i,new Vector3(x+cs*lx+sn*lz,y,z-sn*lx+cs*lz));
            }
        }
        void Pit(LineRenderer line,float margin,float y)
        {
            var b=game.Sim.Layout;
            for(int i=0;i<5;i++)
            {
                int corner=i%4;float side=b.PitSide+(corner==0||corner==3?-1:1)*(b.PitHalfWidth+margin);
                float outward=b.PitOutward+(corner<2?-1:1)*(b.PitHalfLength+margin);
                b.ToWorld(side,outward,out float x,out float z);line.SetPosition(i,new Vector3(x,y,z));
            }
        }
        void OnDestroy(){foreach(var material in materials)if(material!=null)Destroy(material);}
    }
}
