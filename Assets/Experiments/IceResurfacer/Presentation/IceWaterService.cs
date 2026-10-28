using System.Collections.Generic;
using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace IceResurfacer.Presentation
{
    /// <summary>Grounded service walk, physical coupling interactions and bounded procedural hose.</summary>
    public sealed class IceWaterService : MonoBehaviour
    {
        enum Target { None, Holder, Supply, Flood, Wash, Step }
        IceResurfacerGame game;
        Transform root, nozzle, supplyHandle, floodInlet, washInlet, overflowOutlet;
        Transform floodGauge, washGauge, spillSheet;
        LineRenderer hose, runoff;
        ParticleSystem spray;
        AudioSource filling, overflowing, coupling;
        AudioClip fillClip, overflowClip, couplingClip;
        Material steel, rubber, blue, teal, paint, waterMaterial, textMaterial;
        Font font;
        TextMesh levels;
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly Vector3[] route = new Vector3[16];
        readonly Vector2[] nodes = new Vector2[6];
        readonly float[] distances = new float[6];
        readonly bool[] visited = new bool[6];
        readonly int[] previous = new int[6], reversePath = new int[6];
        Vector3 footPosition, seatedPosition;
        Quaternion seatedRotation;
        Vector2 savedCursor;
        float footYaw, footPitch, spillWetness, levelUpdate;
        bool footLook;
        int lookWarmup;
        Target selected;
        string prompt = "";
        float hoseLength;
        Vector3 holderPosition = new Vector3(4.04f,1.15f,-35.98f);
        Vector3 supplyPosition = new Vector3(4.02f,1.25f,-34.78f);
        Vector3 anchorPosition = new Vector3(4.10f,1.26f,-35.50f);
        readonly Vector3 drainPosition = new Vector3(3.26f,.025f,-36.85f);

        public string Prompt { get { return prompt; } }
        public bool OnFoot { get { return game != null && game.Sim != null && game.Sim.OnFoot; } }
        public Transform FloodInlet { get { return floodInlet; } }
        public Transform WashInlet { get { return washInlet; } }
        public Transform HoseNozzle { get { return nozzle; } }
        public Transform SupplyHandle { get { return supplyHandle; } }
        public ParticleSystem OverflowParticles { get { return spray; } }
        public Vector3 FootPosition { get { return footPosition; } }
        public float HoseVisibleLength { get { return hoseLength; } }
        public string Status
        {
            get
            {
                if (game == null || game.Sim == null) return "";
                var s = game.Sim;
                string state = s.HoseState == WaterHoseState.Stowed ? "HOSE STOWED" : s.HoseState == WaterHoseState.Carried ? "NOZZLE IN HAND" :
                    s.HoseState == WaterHoseState.Flood ? "ICE WATER CONNECTED" : "WASH WATER CONNECTED";
                return state + "   |   SUPPLY " + Mathf.RoundToInt(s.SupplyValve*100) + "%   |   " + s.ServiceFlowLitersPerSecond.ToString("0.0") + " L/s" +
                    (s.ServiceOverflowLitersPerSecond > .01f ? "   |   OVERFLOW — CLOSE WALL VALVE" : "");
            }
        }

        public void Initialize(IceResurfacerGame owner)
        {
            game = owner;
            root = Group("Interactive water service / 11 m hose", transform, Vector3.zero);
            steel = Mat("Water station / brushed steel",new Color(.42f,.50f,.53f),.82f,.68f);
            rubber = Mat("Water station / reinforced hose",new Color(.055f,.075f,.072f),0,.22f);
            blue = Mat("Ice water inlet / blue",new Color(.075f,.36f,.62f),.3f,.52f);
            teal = Mat("Wash inlet / teal",new Color(.08f,.57f,.49f),.3f,.52f);
            paint = Mat("Water station / cream enamel",new Color(.73f,.76f,.70f),.2f,.35f);
            waterMaterial = Mat("Water station / wet water",new Color(.20f,.48f,.57f),.15f,.94f);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var shader = Shader.Find("IceResurfacer/WorldText");
            textMaterial = new Material(shader != null ? shader : font.material.shader) { name="Water service lettering",mainTexture=font.material.mainTexture };
            ownedMaterials.Add(textMaterial);
            root.gameObject.AddComponent<IceWorldTextAtlas>().Initialize(font,textMaterial);
            BuildStation(); BuildPorts(); BuildEffects(); BuildAudio();
        }

        public void ResetSession()
        {
            if (game == null) return;
            EndFootLook();
            game.DriverCamera.transform.SetParent(game.World.CameraMount,false);
            game.DriverCamera.transform.localPosition=Vector3.zero;
            game.DriverCamera.transform.localRotation=Quaternion.Euler(5,0,0);
            prompt=""; spillWetness=0; levelUpdate=0;
            if(spray!=null)spray.Clear();
        }

        public void HandleInput(bool running,float dt)
        {
            if(game==null||game.Sim==null)return;
            var keys=Keyboard.current; var mouse=Mouse.current; var s=game.Sim;
            if(!running){ EndFootLook(); prompt=""; return; }
            if(keys!=null&&keys.fKey.wasPressedThisFrame)
            {
                if(!s.OnFoot)
                {
                    if(s.TryDismount())BeginWalk();
                    game.Notice=s.LastMessage;
                }
                else
                {
                    float distance=Vector3.Distance(footPosition,StepPoint());
                    if(s.TryBoard(distance))EndWalk();
                    game.Notice=s.LastMessage;
                }
            }
            if(!s.OnFoot)
            {
                prompt=s.InServiceBay?"F  SERVICE / DISMOUNT — stop and stow equipment":"";
                return;
            }
            UpdateFootLook(mouse,Mathf.Min(dt,.05f));
            if(keys!=null)
            {
                Vector3 move=new Vector3((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),0,(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0));
                move=Quaternion.Euler(0,footYaw,0)*Vector3.ClampMagnitude(move,1)*Mathf.Min(dt,.05f)*1.8f;
                MoveFoot(move);
            }
            ApplyFootCamera();
            selected=FindTarget(mouse);
            prompt=PromptFor(selected);
            bool interact=keys!=null&&keys.eKey.wasPressedThisFrame;
            if(mouse!=null&&!mouse.rightButton.isPressed&&mouse.leftButton.wasPressedThisFrame&&mouse.position.ReadValue().y>Screen.height*.24f)interact=true;
            if(interact&&selected!=Target.None)Interact(selected);
        }

        void BeginWalk()
        {
            var camera=game.DriverCamera.transform;
            seatedPosition=camera.localPosition; seatedRotation=camera.localRotation;
            footPosition=StepPoint();
            footYaw=game.World.Machine.eulerAngles.y+90; footPitch=5;
            camera.SetParent(transform,true);
            ApplyFootCamera();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void EndWalk()
        {
            EndFootLook();
            game.DriverCamera.transform.SetParent(game.World.CameraMount,false);
            game.DriverCamera.transform.localPosition=seatedPosition;
            game.DriverCamera.transform.localRotation=seatedRotation;
        }
        Vector3 StepPoint()
        {
            Vector3 point=game.World.Machine.TransformPoint(new Vector3(-1.67f,0,-.45f));point.y=0;return point;
        }
        void UpdateFootLook(Mouse mouse,float dt)
        {
            bool held=mouse!=null&&mouse.rightButton.isPressed;
            if(held)
            {
                if(!footLook){savedCursor=mouse.position.ReadValue();lookWarmup=2;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
                Vector2 delta=lookWarmup>0?Vector2.zero:mouse.delta.ReadValue();
                if(lookWarmup>0)lookWarmup--;
                footYaw+=delta.x*.11f;footPitch=Mathf.Clamp(footPitch-delta.y*.11f,-60,75);
            }
            else if(footLook)EndFootLook();
            footLook=held;
        }
        void EndFootLook()
        {
            if(!footLook)return;
            footLook=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(Mouse.current!=null)Mouse.current.WarpCursorPosition(savedCursor);
        }
        void ApplyFootCamera()
        {
            game.DriverCamera.transform.SetPositionAndRotation(footPosition+Vector3.up*1.68f,Quaternion.Euler(footPitch,footYaw,0));
        }
        void MoveFoot(Vector3 movement)
        {
            Vector3 next=footPosition+movement;
            if(Walkable(next))footPosition=next;
            else
            {
                next=footPosition+new Vector3(movement.x,0,0);if(Walkable(next))footPosition=next;
                next=footPosition+new Vector3(0,0,movement.z);if(Walkable(next))footPosition=next;
            }
        }
        bool Walkable(Vector3 position)
        {
            if(Mathf.Abs(position.x)>4.12f||position.z< -40.70f||position.z> -32.45f)return false;
            Vector3 local=game.World.Machine.InverseTransformPoint(position);
            if(Mathf.Abs(local.x)<1.38f&&local.z> -1.88f&&local.z<2.84f)return false;
            if(game.Sim.HoseState==WaterHoseState.Carried)
            {
                Vector3 end=position+Vector3.up*1.68f+Quaternion.Euler(footPitch,footYaw,0)*new Vector3(.27f,-.56f,.44f);
                if(BuildRoute(end,out float length)>0&&length>Simulation.ServiceHoseLengthMeters)
                {game.Notice="Hose at its 11 m reach. Walk back toward the water station.";return false;}
            }
            return true;
        }

        Target FindTarget(Mouse mouse)
        {
            Ray ray=game.DriverCamera.ScreenPointToRay(footLook||mouse==null?new Vector3(Screen.width*.5f,Screen.height*.5f):new Vector3(mouse.position.ReadValue().x,mouse.position.ReadValue().y));
            Target best=Target.None;float score=float.MaxValue;
            for(int i=1;i<=5;i++)
            {
                Target t=(Target)i;Vector3 p=TargetPosition(t);Vector3 to=p-ray.origin;
                float distance=Vector3.Distance(footPosition+Vector3.up*1.15f,p);
                if(distance>Simulation.ServiceReachMeters)continue;
                float along=Vector3.Dot(to,ray.direction);if(along<0)continue;
                float miss=(to-ray.direction*along).magnitude;
                float radius=t==Target.Step?.65f:.33f;
                if(miss<radius&&miss<score){best=t;score=miss;}
            }
            return best;
        }
        Vector3 TargetPosition(Target target)
        {
            switch(target){case Target.Holder:return holderPosition;case Target.Supply:return supplyPosition;case Target.Flood:return floodInlet.position;case Target.Wash:return washInlet.position;case Target.Step:return StepPoint()+Vector3.up*.45f;default:return Vector3.zero;}
        }
        string PromptFor(Target target)
        {
            var s=game.Sim;
            switch(target)
            {
                case Target.Holder:return s.HoseState==WaterHoseState.Stowed?"E / CLICK  Take hose nozzle":s.HoseState==WaterHoseState.Carried?"E / CLICK  Return hose nozzle":"Hose is connected at the machine inlet";
                case Target.Supply:return s.SupplyValve>.01f?"E / CLICK  Close wall supply valve":"E / CLICK  Open wall supply valve";
                case Target.Flood:return s.HoseState==WaterHoseState.Flood?"E / CLICK  Disconnect ICE WATER hose":"E / CLICK  Connect ICE WATER — "+s.FloodWaterLiters.ToString("0")+" / 738 L";
                case Target.Wash:return s.HoseState==WaterHoseState.Wash?"E / CLICK  Disconnect WASH WATER hose":"E / CLICK  Connect WASH WATER — "+s.WashWaterLiters.ToString("0")+" / 310 L";
                case Target.Step:return "F  Board the left operator station";
                default:return "WASD walk  •  RMB look  •  Aim at hose, valve or inlet and press E  •  F board at left step";
            }
        }
        void Interact(Target target)
        {
            var s=game.Sim;bool success=false;
            float distance=Vector3.Distance(footPosition+Vector3.up*1.15f,TargetPosition(target));
            switch(target)
            {
                case Target.Holder:success=s.HoseState==WaterHoseState.Carried?s.ReturnHose(distance):s.TakeHose(distance);break;
                case Target.Supply:success=s.SetSupplyValve(s.SupplyValve>.01f?0:1,distance);break;
                case Target.Flood:case Target.Wash:
                    bool same=(target==Target.Flood&&s.HoseState==WaterHoseState.Flood)||(target==Target.Wash&&s.HoseState==WaterHoseState.Wash);
                    BuildRoute(TargetPosition(target),out float length);
                    success=same?s.DisconnectHose(distance):s.ConnectHose(target==Target.Flood?WaterFillPort.Flood:WaterFillPort.Wash,length,distance);break;
                case Target.Step:if(s.TryBoard(Vector3.Distance(footPosition,StepPoint()))){EndWalk();success=true;}break;
            }
            if(success){coupling.pitch=target==Target.Supply?.8f:1.15f;coupling.PlayOneShot(couplingClip,.26f);}
            game.Notice=s.LastMessage;
        }

        public void UpdateViewAndEffects(bool running,float dt)
        {
            if(game==null||game.Sim==null)return;
            var s=game.Sim;
            if(s.OnFoot)ApplyFootCamera();
            supplyHandle.localRotation=Quaternion.Euler(s.SupplyValve*90,0,0);
            Transform inlet=s.HoseState==WaterHoseState.Wash?washInlet:floodInlet;
            Vector3 endpoint=s.HoseConnected?inlet.position:s.HoseState==WaterHoseState.Carried?
                game.DriverCamera.transform.TransformPoint(new Vector3(.27f,-.56f,.44f)):holderPosition;
            nozzle.position=endpoint;
            nozzle.rotation=s.HoseConnected?inlet.rotation:Quaternion.Euler(0,0,-28);
            hose.enabled=true;
            if(s.HoseState!=WaterHoseState.Stowed)
            {
                int count=BuildRoute(endpoint,out hoseLength);hose.positionCount=count;
                for(int i=0;i<count;i++)hose.SetPosition(i,route[i]);
            }
            else
            {
                // The fixed wall reel stores unused length. Its hanging tail remains
                // visibly joined to the nozzle instead of disappearing when stowed.
                hose.positionCount=18;hose.SetPosition(0,anchorPosition);hoseLength=0;
                Vector3 last=anchorPosition;
                for(int i=1;i<17;i++)
                {
                    float t=(i-1)/15f;
                    Vector3 point=Vector3.Lerp(anchorPosition,holderPosition,t)-Vector3.up*(Mathf.Sin(t*Mathf.PI)*.60f);
                    hose.SetPosition(i,point);hoseLength+=Vector3.Distance(last,point);last=point;
                }
                hose.SetPosition(17,holderPosition);
            }
            float spill=running?(s.HoseConnected?s.ServiceOverflowLitersPerSecond:s.ServiceFlowLitersPerSecond):0;
            Vector3 spillOrigin=s.HoseConnected?inlet.position-game.World.Machine.right*.03f-Vector3.up*.25f+game.World.Machine.forward*.13f:endpoint;
            overflowOutlet.position=spillOrigin;
            var emission=spray.emission;emission.rateOverTime=spill*10;
            runoff.enabled=spill>.01f;
            if(runoff.enabled)
            {
                Vector3 landing=new Vector3(spillOrigin.x,.035f,spillOrigin.z);
                runoff.SetPosition(0,spillOrigin);runoff.SetPosition(1,landing);
                runoff.SetPosition(2,Vector3.Lerp(landing,drainPosition,.65f)+Vector3.up*.003f);runoff.SetPosition(3,drainPosition);
            }
            spillWetness=Mathf.MoveTowards(spillWetness,spill>.01f?1:0,dt*(spill>.01f?.18f:.015f));
            spillSheet.gameObject.SetActive(spillWetness>.001f);spillSheet.localScale=new Vector3(.8f+spillWetness*.8f,.003f,1+spillWetness*.8f);
            filling.volume=Mathf.Lerp(filling.volume,running&&s.HoseConnected?s.ServiceFlowLitersPerSecond/12*.20f:0,1-Mathf.Exp(-7*Time.unscaledDeltaTime));
            overflowing.volume=Mathf.Lerp(overflowing.volume,spill/12*.24f,1-Mathf.Exp(-7*Time.unscaledDeltaTime));
            filling.pitch=.8f+.4f*(1-(s.HoseState==WaterHoseState.Wash?s.WashWaterLiters/s.Config.WashCapacityLiters:s.FloodWaterLiters/s.Config.FloodCapacityLiters));
            UpdateGauge(floodGauge,s.FloodWaterLiters/s.Config.FloodCapacityLiters);
            UpdateGauge(washGauge,s.WashWaterLiters/s.Config.WashCapacityLiters);
            levelUpdate-=dt;
            if(levelUpdate<=0){levelUpdate=.2f;levels.text="ICE WATER  "+s.FloodWaterLiters.ToString("0")+" / 738 L\nWASH WATER  "+s.WashWaterLiters.ToString("0")+" / 310 L\n"+(s.ServiceOverflowLitersPerSecond>.01f?"OVERFLOW — CLOSE SUPPLY":"SUPPLY  "+s.ServiceFlowLitersPerSecond.ToString("0.0")+" L/s");}
        }

        // A short visibility graph routes the floor hose around the machine footprint.
        // Six fixed nodes and reusable buffers avoid runtime object/particle accumulation.
        int BuildRoute(Vector3 endpoint,out float length)
        {
            Transform machine=game.World.Machine;
            Vector3 groundStart=new Vector3(4.02f,.03f,anchorPosition.z);
            Vector3 localEnd=machine.InverseTransformPoint(endpoint);
            Vector3 groundEnd=endpoint;
            if(game.Sim!=null&&game.Sim.HoseConnected)groundEnd=machine.TransformPoint(new Vector3(1.53f,0,localEnd.z));
            else if(Mathf.Abs(localEnd.x)<1.48f&&localEnd.z> -1.98f&&localEnd.z<2.95f)
                groundEnd=machine.TransformPoint(new Vector3(localEnd.x>=0?1.53f:-1.53f,0,localEnd.z));
            groundEnd.y=.03f;
            Vector3 a=machine.InverseTransformPoint(groundStart),b=machine.InverseTransformPoint(groundEnd);
            nodes[0]=new Vector2(a.x,a.z);nodes[1]=new Vector2(b.x,b.z);
            nodes[2]=new Vector2(-1.52f,-2.02f);nodes[3]=new Vector2(1.52f,-2.02f);
            nodes[4]=new Vector2(-1.52f,2.98f);nodes[5]=new Vector2(1.52f,2.98f);
            for(int i=0;i<6;i++){distances[i]=float.PositiveInfinity;visited[i]=false;previous[i]=-1;}
            distances[0]=0;
            for(int pass=0;pass<6;pass++)
            {
                int current=-1;float best=float.PositiveInfinity;
                for(int i=0;i<6;i++)if(!visited[i]&&distances[i]<best){best=distances[i];current=i;}
                if(current<0||current==1)break;visited[current]=true;
                for(int i=0;i<6;i++)
                {
                    if(i==current||visited[i]||CrossesMachine(nodes[current],nodes[i]))continue;
                    float candidate=distances[current]+Vector2.Distance(nodes[current],nodes[i]);
                    if(candidate<distances[i]){distances[i]=candidate;previous[i]=current;}
                }
            }
            int count=0;route[count++]=anchorPosition;route[count++]=groundStart;
            int n=0,index=1;
            while(index>0&&n<6){reversePath[n++]=index;index=previous[index];}
            if(index<0){route[count++]=groundEnd;length=float.PositiveInfinity;route[count++]=endpoint;return count;}
            for(int i=n-1;i>=0;i--){Vector2 p=nodes[reversePath[i]];Vector3 p3=machine.TransformPoint(new Vector3(p.x,0,p.y));p3.y=.03f;route[count++]=p3;}
            route[count++]=endpoint;length=0;
            for(int i=1;i<count;i++)length+=Vector3.Distance(route[i-1],route[i]);
            return count;
        }
        static bool CrossesMachine(Vector2 a,Vector2 b)
        {
            float t0=0,t1=1;Vector2 d=b-a;
            return Slab(a.x,d.x,-1.39f,1.39f,ref t0,ref t1)&&Slab(a.y,d.y,-1.89f,2.85f,ref t0,ref t1)&&t1>t0;
        }
        static bool Slab(float origin,float direction,float min,float max,ref float low,ref float high)
        {
            if(Mathf.Abs(direction)<.00001f)return origin>min&&origin<max;
            float a=(min-origin)/direction,b=(max-origin)/direction;if(a>b){float swap=a;a=b;b=swap;}
            low=Mathf.Max(low,a);high=Mathf.Min(high,b);return high>=low;
        }

        void BuildStation()
        {
            Box("Water station backing",root,new Vector3(4.40f,1.52f,-35.55f),new Vector3(.06f,1.40f,2.25f),paint);
            Beam("Wall supply riser",root,new Vector3(4.25f,.18f,-34.78f),new Vector3(4.25f,2.40f,-34.78f),.052f,steel);
            Beam("Supply branch",root,new Vector3(4.25f,1.25f,-34.78f),supplyPosition,.052f,steel);
            Cylinder("Supply valve gland",root,supplyPosition,.11f,.07f,steel,Vector3.right);
            supplyHandle=Group("Wall supply valve 90 degree pivot",root,supplyPosition+Vector3.left*.055f);
            Beam("Wall valve handle",supplyHandle,Vector3.zero,new Vector3(0,0,-.17f),.033f,blue);
            Beam("Wall valve handle return",supplyHandle,new Vector3(0,0,-.17f),new Vector3(-.07f,0,-.17f),.033f,blue);
            Beam("Supply to hose reel",root,new Vector3(4.21f,1.25f,-34.78f),anchorPosition,.04f,steel);
            for(int i=0;i<3;i++)
            {
                Cylinder("Pipe wall clamp",root,new Vector3(4.27f,.35f+i*.9f,-34.78f),.074f,.035f,rubber,Vector3.up);
                Beam("Pipe clamp bracket",root,new Vector3(4.27f,.35f+i*.9f,-34.78f),new Vector3(4.38f,.35f+i*.9f,-34.78f),.025f,steel);
            }
            Box("Nozzle holder",root,holderPosition+Vector3.right*.16f,new Vector3(.14f,.21f,.15f),steel);
            nozzle=Group("Service nozzle / carried and coupled",root,holderPosition);
            Cylinder("Hose bayonet coupling",nozzle,Vector3.zero,.075f,.13f,steel,Vector3.right);
            Cylinder("Nozzle reinforced grip",nozzle,new Vector3(.10f,0,0),.058f,.13f,rubber,Vector3.right);
            for(int i=-1;i<=1;i+=2)Box("Coupling lug",nozzle,new Vector3(0,0,i*.043f),new Vector3(.035f,.027f,.025f),steel);
            Label("WATER SERVICE",root,new Vector3(4.34f,2.16f,-35.50f),.105f,Color.white,Quaternion.Euler(0,-90,0));
            Label("SUPPLY\nE  OPEN / CLOSE",root,new Vector3(4.33f,1.72f,-34.78f),.065f,Color.white,Quaternion.Euler(0,-90,0));
            Label("NOZZLE\n11 m HOSE",root,new Vector3(4.33f,.82f,-35.98f),.060f,Color.white,Quaternion.Euler(0,-90,0));
            levels=Label("ICE WATER  738 / 738 L\nWASH WATER  310 / 310 L",root,new Vector3(4.33f,2.05f,-37.0f),.070f,Color.white,Quaternion.Euler(0,-90,0));
            Box("Tank level repeater panel",root,new Vector3(4.36f,2.05f,-37),new Vector3(.035f,.63f,1.65f),rubber);
            Box("Service floor drain recess",root,drainPosition,new Vector3(.85f,.025f,.65f),rubber);
            for(int i=0;i<9;i++)Box("Drain grate bar",root,drainPosition+new Vector3(-.36f+i*.09f,.016f,0),new Vector3(.027f,.014f,.60f),steel);
            spillSheet=Box("Persistent service drain wetness",root,drainPosition+new Vector3(-.25f,-.005f,.20f),new Vector3(1,.003f,1),waterMaterial);
        }
        void BuildPorts()
        {
            floodInlet=Port("ICE WATER",new Vector3(1.105f,1.10f,1.36f),blue);
            washInlet=Port("WASH WATER",new Vector3(1.105f,1.10f,.44f),teal);
            floodGauge=Gauge(new Vector3(1.115f,.78f,1.36f),blue);
            washGauge=Gauge(new Vector3(1.115f,.78f,.44f),teal);
        }
        Transform Port(string name,Vector3 position,Material material)
        {
            Transform inlet=Group(name+" fill coupling",game.World.Machine,position);
            Box("Fill-port mounting plate",inlet,new Vector3(-.015f,0,0),new Vector3(.025f,.26f,.32f),material);
            Cylinder("Fill neck",inlet,new Vector3(.035f,0,0),.11f,.09f,steel,Vector3.right);
            Cylinder("Fill inlet dark aperture",inlet,new Vector3(.084f,0,0),.074f,.006f,rubber,Vector3.right);
            for(int i=-1;i<=1;i+=2)Box("Bayonet inlet lug",inlet,new Vector3(.07f,0,i*.059f),new Vector3(.027f,.027f,.035f),steel);
            Beam("Overflow standpipe",inlet,new Vector3(-.01f,-.07f,.13f),new Vector3(.11f,-.25f,.13f),.027f,steel);
            Label(name,inlet,new Vector3(.025f,.22f,0),.053f,Color.white,Quaternion.Euler(0,90,0));
            Label("E  CONNECT",inlet,new Vector3(.025f,.15f,0),.034f,Color.white,Quaternion.Euler(0,90,0));
            return Group(name+" hose attachment",inlet,new Vector3(.14f,0,0));
        }
        Transform Gauge(Vector3 position,Material material)
        {
            Transform frame=Group("Tank sight level tube",game.World.Machine,position);
            Box("Level gauge backing",frame,Vector3.zero,new Vector3(.018f,.31f,.075f),rubber);
            Transform fill=Box("Measured tank water level",frame,Vector3.zero,new Vector3(.024f,.27f,.043f),material);
            for(int i=0;i<5;i++)Box("Level graduation",frame,new Vector3(.02f,-.135f+i*.0675f,.042f),new Vector3(.008f,.008f,.029f),steel);
            return fill;
        }
        static void UpdateGauge(Transform gauge,float fraction)
        {
            fraction=Mathf.Clamp01(fraction);gauge.localScale=new Vector3(.024f,Mathf.Max(.002f,.27f*fraction),.043f);gauge.localPosition=new Vector3(0,-.135f+.135f*fraction,0);
        }
        void BuildEffects()
        {
            hose=Line("Live service hose",rubber,.035f,16);hose.numCornerVertices=5;hose.numCapVertices=4;
            runoff=Line("Water overflow to bay drain",waterMaterial,.045f,4);runoff.numCornerVertices=4;
            overflowOutlet=Group("Tank overflow water outlet",root,Vector3.zero);
            spray=overflowOutlet.gameObject.AddComponent<ParticleSystem>();spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=spray.main;main.playOnAwake=false;main.loop=true;main.maxParticles=160;main.startLifetime=.55f;main.startSpeed=.16f;main.startSize=.025f;main.startColor=new Color(.40f,.68f,.75f);main.gravityModifier=1;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=spray.emission;emission.rateOverTime=0;var shape=spray.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.03f;
            var renderer=spray.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=waterMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
            // Lit billboard particles show their opaque rectangular quad. Reuse one
            // built-in sphere mesh for the bounded droplet pool instead: round water
            // beads, no alpha texture dependency and no per-frame mesh creation.
            var dropletTemplate=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dropletTemplate.name="Water droplet mesh initialization";dropletTemplate.SetActive(false);
            renderer.renderMode=ParticleSystemRenderMode.Mesh;
            renderer.mesh=dropletTemplate.GetComponent<MeshFilter>().sharedMesh;
            Destroy(dropletTemplate);
            spray.Play();
        }
        void BuildAudio()
        {
            fillClip=Noise("Water entering metal tank",0);overflowClip=Noise("Water overflow and drain splash",1);couplingClip=Noise("Mechanical hose coupling",2);
            filling=Source(fillClip,true);overflowing=Source(overflowClip,true);coupling=Source(null,false);
        }
        AudioSource Source(AudioClip clip,bool loop)
        {
            var source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=loop;source.volume=loop?0:.5f;source.spatialBlend=0;source.clip=clip;if(loop)source.Play();return source;
        }
        static AudioClip Noise(string name,int kind)
        {
            const int rate=22050;int count=kind==2?rate/5:rate*2;var samples=new float[count];var random=new System.Random(1600+kind);float low=0;
            for(int i=0;i<count;i++)
            {
                float n=(float)random.NextDouble()*2-1,t=(float)i/rate;low=low*.91f+n*.09f;
                float value=kind==0?low*.8f+n*.03f:kind==1?low*.6f+n*.15f:Mathf.Exp(-t*35)*(n*.35f+Mathf.Sin(t*2*Mathf.PI*770)*.25f);
                samples[i]=value*Mathf.Min(1,Mathf.Min(i,count-1-i)/80f);
            }
            var clip=AudioClip.Create(name,count,1,rate,false);clip.SetData(samples,0);return clip;
        }
        Material Mat(string name,Color color,float metallic,float smooth)
        {
            var material=new Material(game.litTemplate){name=name};material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smooth);ownedMaterials.Add(material);return material;
        }
        static Transform Group(string name,Transform parent,Vector3 position)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;}
        Transform Box(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;}
        Transform Cylinder(string name,Transform parent,Vector3 position,float diameter,float length,Material material,Vector3 axis)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=new Vector3(diameter,length*.5f,diameter);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,axis);Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;}
        void Beam(string name,Transform parent,Vector3 start,Vector3 end,float diameter,Material material)
        {Cylinder(name,parent,(start+end)*.5f,diameter,(end-start).magnitude,material,(end-start).normalized);}
        TextMesh Label(string text,Transform parent,Vector3 position,float size,Color color,Quaternion rotation)
        {var go=Group(text,parent,position);go.localRotation=rotation*Quaternion.Euler(0,180,0);var label=go.gameObject.AddComponent<TextMesh>();label.font=font;label.fontSize=64;label.characterSize=size*10f/64f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.text=text;label.color=color;label.GetComponent<MeshRenderer>().sharedMaterial=textMaterial;return label;}
        LineRenderer Line(string name,Material material,float width,int count)
        {var go=Group(name,root,Vector3.zero);var line=go.gameObject.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.startWidth=line.endWidth=width;line.positionCount=count;line.shadowCastingMode=ShadowCastingMode.On;line.receiveShadows=true;return line;}
        void OnDisable(){EndFootLook();}
        void OnDestroy(){foreach(var material in ownedMaterials)if(material!=null)Destroy(material);if(fillClip!=null)Destroy(fillClip);if(overflowClip!=null)Destroy(overflowClip);if(couplingClip!=null)Destroy(couplingClip);}
    }
}
