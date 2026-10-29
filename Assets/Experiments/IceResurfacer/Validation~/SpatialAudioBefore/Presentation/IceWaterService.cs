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
        enum Target { None, Holder, Supply, Flood, Wash, Step, LooseNozzle }
        IceResurfacerGame game;
        Transform root, nozzle, supplyHandle, floodInlet, washInlet, overflowOutlet;
        Transform floodGauge, washGauge;
        Transform holderHighlight, station;
        LineRenderer hose, runoff;
        ParticleSystem spray;
        AudioSource filling, overflowing, coupling;
        AudioClip fillClip, overflowClip, couplingClip;
        Material steel, rubber, blue, teal, paint, waterMaterial, floorWaterMaterial, textMaterial;
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
        float footYaw, footPitch, levelUpdate;
        bool footLook;
        int lookWarmup;
        Target selected;
        string prompt = "", movementStatus = "";
        float movementStatusTime;
        Target outOfReachTarget;
        float targetDistance;
        float hoseLength;
        Vector3 holderPosition, supplyPosition, anchorPosition, drainPosition;
        float groundUpdate;
        int groundRevision=-1;
        Mesh groundWaterMesh, groundSnowMesh;
        Vector3[] groundWaterVertices,groundSnowVertices;
        float[] groundSnowSamples;
        Color32[] groundWaterColors;

        public string Prompt { get { return prompt; } }
        public bool OnFoot { get { return game != null && game.Sim != null && game.Sim.OnFoot; } }
        public Transform FloodInlet { get { return floodInlet; } }
        public Transform WashInlet { get { return washInlet; } }
        public Transform HoseNozzle { get { return nozzle; } }
        public Transform SupplyHandle { get { return supplyHandle; } }
        public ParticleSystem OverflowParticles { get { return spray; } }
        public Vector3 FootPosition { get { return footPosition; } }
        public float HoseVisibleLength { get { return hoseLength; } }
        public string MovementStatus { get { return movementStatus; } }
        public Vector3 HolderPosition { get { return holderPosition; } }
        public Vector3 SupplyPosition { get { return supplyPosition; } }
        public Vector3 FloorDrainPosition { get { return drainPosition; } }
        public Vector3 BoardingPoint { get { return StepPoint(); } }
        public bool HolderHighlighted { get { return holderHighlight!=null && holderHighlight.gameObject.activeSelf; } }
        public bool FootLookActive { get { return footLook; } }
        public string Status
        {
            get
            {
                if (game == null || game.Sim == null) return "";
                var s = game.Sim;
                string state = s.HoseState == WaterHoseState.Stowed ? "HOSE STOWED" : s.HoseState == WaterHoseState.Carried ? "NOZZLE IN HAND" : s.HoseState == WaterHoseState.Dropped ? "LOOSE NOZZLE ON FLOOR" :
                    s.HoseState == WaterHoseState.Flood ? "FLOOD WATER CONNECTED" : "WASH WATER CONNECTED";
                string reach=s.HoseState==WaterHoseState.Stowed?"":"  "+hoseLength.ToString("0.0")+" / 11 m"+(hoseLength>10.5f?" TAUT":"");
                return state + reach + "   |   SUPPLY " + Mathf.RoundToInt(s.SupplyValve*100) + "%   |   " + s.ServiceFlowLitersPerSecond.ToString("0.0") + " L/s" +
                    (s.ServiceOverflowLitersPerSecond > .01f ? "   |   OVERFLOW — CLOSE WALL VALVE" : "");
            }
        }

        public void Initialize(IceResurfacerGame owner)
        {
            game = owner;
            root = Group("Interactive water service / 11 m hose", transform, Vector3.zero);
            steel = Mat("Water station / brushed steel",new Color(.42f,.50f,.53f),.82f,.68f);
            rubber = Mat("Water station / reinforced hose",new Color(.055f,.075f,.072f),0,.22f);
            blue = Mat("Flood water inlet / blue",new Color(.075f,.36f,.62f),.3f,.52f);
            teal = Mat("Wash inlet / teal",new Color(.08f,.57f,.49f),.3f,.52f);
            paint = Mat("Water station / cream enamel",new Color(.73f,.76f,.70f),.2f,.35f);
            waterMaterial = Mat("Water station / wet water",new Color(.20f,.48f,.57f),.15f,.94f);
            floorWaterMaterial=new Material(Resources.Load<Shader>("WetFloor")){name="Finite shallow water / darkened concrete"};
            var floorLayout=new ServiceLayout(game.calibration);
            floorWaterMaterial.SetVector("_Gate",new Vector4(floorLayout.GateX,floorLayout.GateZ,0,0));
            floorWaterMaterial.SetVector("_Room",new Vector4(floorLayout.RoomHalfWidth,floorLayout.RoomStart,floorLayout.RoomLength,floorLayout.GateHalfWidth));
            floorWaterMaterial.SetVector("_Pit",new Vector4(floorLayout.PitSide,floorLayout.PitOutward,floorLayout.PitHalfWidth,floorLayout.PitHalfLength));
            floorWaterMaterial.SetVector("_Rink",new Vector4(game.calibration.RinkWidth*.5f,game.calibration.RinkLength*.5f,game.calibration.CornerRadius,0));
            ownedMaterials.Add(floorWaterMaterial);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var shader = Shader.Find("IceResurfacer/WorldText");
            textMaterial = new Material(shader != null ? shader : font.material.shader) { name="Water service lettering",mainTexture=font.material.mainTexture };
            ownedMaterials.Add(textMaterial);
            root.gameObject.AddComponent<IceWorldTextAtlas>().Initialize(font,textMaterial);
            BuildStation(); BuildPorts(); BuildEffects(); BuildAudio();
            root.gameObject.AddComponent<IceCollisionOverlay>().Initialize(game);
        }

        public void ResetSession()
        {
            if (game == null) return;
            if(groundWaterMesh==null)BuildGroundMaterials();
            EndFootLook();
            game.DriverCamera.transform.SetParent(game.World.CameraMount,false);
            game.DriverCamera.transform.localPosition=Vector3.zero;
            game.DriverCamera.transform.localRotation=Quaternion.Euler(5,0,0);
            prompt="";movementStatus="";movementStatusTime=0;levelUpdate=0;groundRevision=-1;groundUpdate=0;
            if(holderHighlight!=null)holderHighlight.gameObject.SetActive(false);
            if(spray!=null)spray.Clear();
        }

        public void HandleInput(bool running,float dt)
        {
            if(game==null||game.Sim==null)return;
            var keys=Keyboard.current; var mouse=Mouse.current; var s=game.Sim;
            if(!running){ EndFootLook(); prompt="";if(holderHighlight!=null)holderHighlight.gameObject.SetActive(false); return; }
            if(keys!=null&&keys.fKey.wasPressedThisFrame)
            {
                if(!s.OnFoot)
                {
                    if(s.TryDismount())BeginWalk();
                    game.Notice=s.LastMessage;
                }
                else
                {
                    float distance=BoardingDistance();
                    if(s.TryBoard(distance))EndWalk();
                    game.Notice=s.LastMessage;
                }
            }
            if(!s.OnFoot)
            {
                prompt="F  DISMOUNT";
                return;
            }
            UpdateFootLook(mouse,Mathf.Min(dt,.05f));
            movementStatusTime=Mathf.Max(0,movementStatusTime-dt);
            if(movementStatusTime==0)movementStatus="";
            if(keys!=null)
            {
                Vector3 move=new Vector3((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),0,(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0));
                move=Quaternion.Euler(0,footYaw,0)*Vector3.ClampMagnitude(move,1)*Mathf.Min(dt,.05f)*1.8f;
                MoveFoot(move);
            }
            ApplyFootCamera();
            selected=FindTarget(mouse);
            holderHighlight.gameObject.SetActive(selected==Target.Holder&&s.HoseState==WaterHoseState.Carried);
            prompt=PromptFor(selected);
            if(movementStatusTime>0)prompt=movementStatus;
            bool interact=keys!=null&&keys.eKey.wasPressedThisFrame;
            if(mouse!=null&&mouse.leftButton.wasPressedThisFrame)interact=true;
            if(interact&&selected!=Target.None)Interact(selected);
        }

        void BeginWalk()
        {
            var camera=game.DriverCamera.transform;
            seatedPosition=camera.localPosition; seatedRotation=camera.localRotation;
            footPosition=FindExitPosition();
            footYaw=game.World.Machine.eulerAngles.y-90; footPitch=5;
            camera.SetParent(transform,true);
            ApplyFootCamera();
            footLook=false;
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
        float BoardingDistance()
        {
            Vector3 rear=game.World.Machine.TransformPoint(new Vector3(-.65f,0,-2.12f));rear.y=0;
            return Mathf.Min(Vector3.Distance(footPosition,StepPoint()),Vector3.Distance(footPosition,rear));
        }
        void UpdateFootLook(Mouse mouse,float dt)
        {
            bool held=mouse!=null;
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
            if(movement.sqrMagnitude<.000001f)return;
            Vector3 before=footPosition;
            Vector3 next=footPosition+movement;
            if(Walkable(next,true,true))footPosition=next;
            else
            {
                next=footPosition+new Vector3(movement.x,0,0);if(Walkable(next))footPosition=next;
                next=footPosition+new Vector3(0,0,movement.z);if(Walkable(next))footPosition=next;
            }
            if((footPosition-before).sqrMagnitude>=movement.sqrMagnitude*.80f){movementStatus="";movementStatusTime=0;}
        }
        bool Blocked(string reason,bool show)
        {if(show){movementStatus=reason;movementStatusTime=1.4f;}return false;}
        bool Walkable(Vector3 position,bool checkHose=true,bool showBlock=false)
        {
            var s=game.Sim;
            if(!s.Ice.IsInside(position.x,position.z,.22f)&&!s.Layout.ContainsFloor(position.x,position.z,.22f))return Blocked("Wall or boards ahead. Follow the clear aisle.",showBlock);
            Vector3 local=game.World.Machine.InverseTransformPoint(position);
            if(Mathf.Abs(local.x)<1.38f&&local.z> -1.88f&&local.z<2.84f)return Blocked("Machine ahead. Use the clear aisle beside the fill couplings.",showBlock);
            if(checkHose&&game.Sim.HoseState==WaterHoseState.Carried)
            {
                Vector3 end=position+Vector3.up*1.68f+Quaternion.Euler(footPitch,footYaw,0)*new Vector3(.27f,-.56f,.44f);
                // Turning the head can move the held nozzle beyond the limit.
                // Always allow a shorter route back toward slack; otherwise an
                // overextended hand creates a trap even while walking inward.
                if(BuildRoute(end,out float length)>0&&length>Simulation.ServiceHoseLengthMeters&&length>=hoseLength-.0005f)
                {return Blocked("Hose taut at 11 m. Walk toward the wall reel or return the nozzle before moving farther.",showBlock);}
            }
            return true;
        }

        Vector3 FindExitPosition()
        {
            // Prefer the left step, then the aisle behind or the opposite side.
            // Every candidate uses the same ground and obstruction checks as walking.
            Vector3[] local={new Vector3(-1.67f,0,-.45f),new Vector3(-1.67f,0,-1.65f),new Vector3(1.67f,0,-.45f),new Vector3(0,0,-2.25f),new Vector3(-1.67f,0,2.5f)};
            for(int i=0;i<local.Length;i++){Vector3 p=game.World.Machine.TransformPoint(local[i]);p.y=0;if(Walkable(p,false))return p;}
            Vector3 origin=game.World.Machine.position;origin.y=0;
            for(float radius=1.6f;radius<=4.4f;radius+=.35f)
                for(int n=0;n<24;n++){float a=n*Mathf.PI/12;Vector3 p=origin+new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius);if(Walkable(p,false))return p;}
            // Only a deliberately invalid injected pose can exhaust these samples.
            return StepPoint();
        }

        Target FindTarget(Mouse mouse)
        {
            Ray ray=game.DriverCamera.ScreenPointToRay(footLook||mouse==null?new Vector3(Screen.width*.5f,Screen.height*.5f):new Vector3(mouse.position.ReadValue().x,mouse.position.ReadValue().y));
            Target best=Target.None;float score=float.MaxValue;
            outOfReachTarget=Target.None;targetDistance=0;float distantScore=float.MaxValue;
            for(int i=1;i<=6;i++)
            {
                Target t=(Target)i;Vector3 p=TargetPosition(t);Vector3 to=p-ray.origin;
                if(t==Target.LooseNozzle&&game.Sim.HoseState!=WaterHoseState.Dropped)continue;
                float distance=Vector3.Distance(footPosition+Vector3.up*1.15f,p);
                float reach=t==Target.Holder?Simulation.ServiceHolderReachMeters:Simulation.ServiceReachMeters;
                if(distance>reach+2)continue;
                float along=Vector3.Dot(to,ray.direction);if(along<0)continue;
                float miss=(to-ray.direction*along).magnitude;
                float radius=t==Target.Holder&&game.Sim.HoseState==WaterHoseState.Carried?.86f:t==Target.Step?.65f:t==Target.LooseNozzle?.60f:.38f;
                float aim=miss/radius;
                if(distance>reach)
                {
                    if(aim<1&&aim<distantScore){outOfReachTarget=t;targetDistance=distance;distantScore=aim;}
                    continue;
                }
                if(aim<1&&aim<score){best=t;score=aim;}
            }
            return best;
        }
        Vector3 TargetPosition(Target target)
        {
            switch(target){case Target.Holder:return holderPosition;case Target.Supply:return supplyPosition;case Target.Flood:return floodInlet.position;case Target.Wash:return washInlet.position;case Target.Step:return StepPoint()+Vector3.up*.45f;case Target.LooseNozzle:return new Vector3(game.Sim.HoseLooseX,.09f,game.Sim.HoseLooseZ);default:return Vector3.zero;}
        }
        string PromptFor(Target target)
        {
            var s=game.Sim;
            if(target==Target.None&&outOfReachTarget!=Target.None)
            {
                string fixture=outOfReachTarget==Target.Holder?"hose holder":outOfReachTarget==Target.Supply?"wall valve":outOfReachTarget==Target.Flood?"FLOOD WATER inlet":outOfReachTarget==Target.Wash?"WASH WATER inlet":outOfReachTarget==Target.Step?"left boarding step":"loose nozzle";
                float reach=outOfReachTarget==Target.Holder?Simulation.ServiceHolderReachMeters:Simulation.ServiceReachMeters;
                return "Approach "+fixture+" - "+targetDistance.ToString("0.0")+" m away; reach "+reach.ToString("0.0")+" m";
            }
            switch(target)
            {
                case Target.Holder:return s.HoseState==WaterHoseState.Stowed?"E — Take hose":s.HoseState==WaterHoseState.Carried?"E — Return hose":s.HoseState==WaterHoseState.Dropped?"Retrieve the loose nozzle from the floor":"Hose is connected at the machine inlet";
                case Target.Supply:return s.SupplyValve>.01f?"E / CLICK  Close wall supply valve":"E / CLICK  Open wall supply valve";
                case Target.Flood:return s.HoseState==WaterHoseState.Flood?"E / CLICK  Disconnect FLOOD WATER hose":"E / CLICK  Connect FLOOD WATER — "+s.FloodWaterLiters.ToString("0")+" / 738 L";
                case Target.Wash:return s.HoseState==WaterHoseState.Wash?"E / CLICK  Disconnect WASH WATER hose":"E / CLICK  Connect WASH WATER — "+s.WashWaterLiters.ToString("0")+" / 310 L";
                case Target.Step:return "F  Board the left operator station";
                case Target.LooseNozzle:return "E — Pick up loose hose nozzle";
                default:return "WASD walk  •  Mouse look  •  E interact  •  F board at left step";
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
                case Target.Step:if(s.TryBoard(BoardingDistance())){EndWalk();success=true;}break;
                case Target.LooseNozzle:success=s.PickUpDroppedHose(distance);break;
            }
            if(success){coupling.pitch=target==Target.Supply?.8f:1.15f;coupling.PlayOneShot(couplingClip,.26f);}
            game.Notice=s.LastMessage;
        }

        public void UpdateViewAndEffects(bool running,float dt)
        {
            if(game==null||game.Sim==null)return;
            var s=game.Sim;
            if(s.OnFoot)
            {
                // The vehicle can coast after a moving exit. Resolve its actual
                // footprint against the walker without changing any machine state.
                if(!Walkable(footPosition,false))footPosition=FindExitPosition();
                ApplyFootCamera();
            }
            supplyHandle.localRotation=Quaternion.Euler(s.SupplyValve*90,0,0);
            Transform inlet=s.HoseState==WaterHoseState.Wash?washInlet:floodInlet;
            Vector3 endpoint=s.HoseConnected?inlet.position:s.HoseState==WaterHoseState.Carried?
                game.DriverCamera.transform.TransformPoint(new Vector3(.27f,-.56f,.44f)):s.HoseState==WaterHoseState.Dropped?new Vector3(s.HoseLooseX,.09f,s.HoseLooseZ):holderPosition;
            if(s.HoseState==WaterHoseState.Carried)s.SetCarriedHosePosition(endpoint.x,endpoint.z);
            nozzle.position=endpoint;
            nozzle.rotation=s.HoseConnected?inlet.rotation:Quaternion.Euler(0,0,-28);
            hose.enabled=true;
            if(s.HoseState!=WaterHoseState.Stowed)
            {
                int count=BuildRoute(endpoint,out hoseLength);hose.positionCount=count;
                for(int i=0;i<count;i++)hose.SetPosition(i,route[i]);
                if(running&&s.CheckHoseReach(hoseLength))game.Notice=s.LastMessage;
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
            }
            filling.volume=Mathf.Lerp(filling.volume,running&&s.HoseConnected?s.ServiceFlowLitersPerSecond/12*.20f:0,1-Mathf.Exp(-7*Time.unscaledDeltaTime));
            overflowing.volume=Mathf.Lerp(overflowing.volume,spill/12*.24f,1-Mathf.Exp(-7*Time.unscaledDeltaTime));
            filling.pitch=.8f+.4f*(1-(s.HoseState==WaterHoseState.Wash?s.WashWaterLiters/s.Config.WashCapacityLiters:s.FloodWaterLiters/s.Config.FloodCapacityLiters));
            UpdateGauge(floodGauge,s.FloodWaterLiters/s.Config.FloodCapacityLiters);
            UpdateGauge(washGauge,s.WashWaterLiters/s.Config.WashCapacityLiters);
            levelUpdate-=dt;
            if(levelUpdate<=0){levelUpdate=.2f;levels.text="FLOOD WATER  "+s.FloodWaterLiters.ToString("0")+" / 738 L\nWASH WATER  "+s.WashWaterLiters.ToString("0")+" / 310 L\n"+(s.ServiceOverflowLitersPerSecond>.01f?"OVERFLOW — CLOSE SUPPLY":"SUPPLY  "+s.ServiceFlowLitersPerSecond.ToString("0.0")+" L/s");}
            UpdateGroundMaterials(dt);
        }

        // A short visibility graph routes the floor hose around the machine footprint.
        // Six fixed nodes and reusable buffers avoid runtime object/particle accumulation.
        int BuildRoute(Vector3 endpoint,out float length)
        {
            Transform machine=game.World.Machine;
            Vector3 groundStart=anchorPosition-station.right*.12f;groundStart.y=.03f;
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
            var layout=new ServiceLayout(game.calibration);
            // Rotate the complete fixture about its anchor depth to the opposite
            // wall. Positive scales preserve lettering, pipe and valve geometry.
            float wallInset=6-layout.RoomHalfWidth;
            layout.ToWorld(wallInset,Simulation.ServiceAnchorOutward*2,out float sx,out float sz);
            station=Group("Southeast service wall / right of nose-in parked machine",root,new Vector3(sx,0,sz));
            station.localRotation=Quaternion.Euler(0,135,0);
            Vector3 holder=new Vector3(5.40f,1.15f,-6.48f),supply=new Vector3(5.38f,1.25f,-5.28f),anchor=new Vector3(wallInset-Simulation.ServiceAnchorSide,1.26f,-Simulation.ServiceAnchorOutward);
            holderPosition=station.TransformPoint(holder);supplyPosition=station.TransformPoint(supply);anchorPosition=station.TransformPoint(anchor);
            Vector3 drain=new Vector3(4.6f,.025f,-7.35f);drainPosition=station.TransformPoint(drain);
            Box("Water station backing",station,new Vector3(5.85f,1.52f,-6.05f),new Vector3(.06f,1.40f,2.25f),paint);
            Beam("Wall supply riser",station,new Vector3(5.70f,.18f,-5.28f),new Vector3(5.70f,2.40f,-5.28f),.052f,steel);
            Beam("Supply branch",station,new Vector3(5.70f,1.25f,-5.28f),supply,.052f,steel);
            Cylinder("Supply valve gland",station,supply,.11f,.07f,steel,Vector3.right);
            supplyHandle=Group("Wall supply valve 90 degree pivot",station,supply+Vector3.left*.055f);
            Beam("Wall valve handle",supplyHandle,Vector3.zero,new Vector3(0,0,-.17f),.033f,blue);
            Beam("Wall valve handle return",supplyHandle,new Vector3(0,0,-.17f),new Vector3(-.07f,0,-.17f),.033f,blue);
            Beam("Supply to hose reel",station,new Vector3(5.66f,1.25f,-5.28f),anchor,.04f,steel);
            for(int i=0;i<3;i++)
            {
                Cylinder("Pipe wall clamp",station,new Vector3(5.72f,.35f+i*.9f,-5.28f),.074f,.035f,rubber,Vector3.up);
                Beam("Pipe clamp bracket",station,new Vector3(5.72f,.35f+i*.9f,-5.28f),new Vector3(5.83f,.35f+i*.9f,-5.28f),.025f,steel);
            }
            Box("Nozzle holder",station,holder+Vector3.right*.16f,new Vector3(.14f,.21f,.15f),steel);
            Material highlight=Mat("Nozzle return highlight",new Color(1,.72f,.16f),0,.5f);
            highlight.EnableKeyword("_EMISSION");highlight.SetColor("_EmissionColor",new Color(.45f,.25f,.025f));
            holderHighlight=Group("Forgiving nozzle holder highlight",station,holder+Vector3.right*.08f);
            for(int side=-1;side<=1;side+=2)
            {
                Beam("Holder highlight vertical",holderHighlight,new Vector3(0,-.23f,side*.25f),new Vector3(0,.23f,side*.25f),.028f,highlight);
                Beam("Holder highlight horizontal",holderHighlight,new Vector3(0,side*.23f,-.25f),new Vector3(0,side*.23f,.25f),.028f,highlight);
            }
            holderHighlight.gameObject.SetActive(false);
            nozzle=Group("Service nozzle / carried and coupled",root,holderPosition);
            Cylinder("Hose bayonet coupling",nozzle,Vector3.zero,.075f,.13f,steel,Vector3.right);
            Cylinder("Nozzle reinforced grip",nozzle,new Vector3(.10f,0,0),.058f,.13f,rubber,Vector3.right);
            for(int i=-1;i<=1;i+=2)Box("Coupling lug",nozzle,new Vector3(0,0,i*.043f),new Vector3(.035f,.027f,.025f),steel);
            Label("WATER SERVICE",station,new Vector3(5.79f,2.16f,-6),.105f,Color.white,Quaternion.Euler(0,-90,0));
            Label("SUPPLY\nE  OPEN / CLOSE",station,new Vector3(5.78f,1.72f,-5.28f),.065f,Color.white,Quaternion.Euler(0,-90,0));
            Label("NOZZLE\nE  RETURN HOSE",station,new Vector3(5.78f,.82f,-6.48f),.060f,Color.white,Quaternion.Euler(0,-90,0));
            levels=Label("FLOOD WATER  738 / 738 L\nWASH WATER  310 / 310 L",station,new Vector3(5.78f,2.05f,-7.50f),.070f,Color.white,Quaternion.Euler(0,-90,0));
            Box("Tank level repeater panel",station,new Vector3(5.81f,2.05f,-7.50f),new Vector3(.035f,.63f,1.65f),rubber);
            Box("Service floor drain recess",station,drain,new Vector3(.85f,.025f,.65f),rubber);
            for(int i=0;i<9;i++)Box("Drain grate bar",station,drain+new Vector3(-.36f+i*.09f,.016f,0),new Vector3(.027f,.014f,.60f),steel);
        }
        void BuildPorts()
        {
            floodInlet=Port("FLOOD WATER",new Vector3(Simulation.FillPortLocalX,Simulation.FloodFillHeight,Simulation.FloodFillLocalZ),blue);
            washInlet=Port("WASH WATER",new Vector3(Simulation.FillPortLocalX,Simulation.WashFillHeight,Simulation.WashFillLocalZ),teal);
            floodGauge=Gauge(new Vector3(Simulation.FillPortLocalX+.01f,.80f,Simulation.FloodFillLocalZ),blue);
            washGauge=Gauge(new Vector3(Simulation.FillPortLocalX+.01f,1.06f,Simulation.WashFillLocalZ),teal);
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
            Label("E  INTERACT",inlet,new Vector3(.025f,.15f,0),.034f,Color.white,Quaternion.Euler(0,90,0));
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
        void BuildGroundMaterials()
        {
            var field=game.Sim.Environment;
            int count=field.Cells.Length;
            groundWaterVertices=new Vector3[count*4];groundSnowVertices=new Vector3[count*9];groundWaterColors=new Color32[count*4];
            groundSnowSamples=new float[count];
            Vector2[] waterUV=new Vector2[count*4];
            int[] waterIndices=new int[count*6],snowIndices=new int[count*24];
            for(int i=0;i<count;i++)
            {
                int w=i*4,t=i*6;
                waterIndices[t]=w;waterIndices[t+1]=w+2;waterIndices[t+2]=w+1;waterIndices[t+3]=w;waterIndices[t+4]=w+3;waterIndices[t+5]=w+2;
                waterUV[w]=Vector2.zero;waterUV[w+1]=Vector2.right;waterUV[w+2]=Vector2.one;waterUV[w+3]=Vector2.up;
                for(int z=0;z<2;z++)for(int x=0;x<2;x++)
                {
                    int a=i*9+z*3+x,b=a+3,k=i*24+(z*2+x)*6;
                    snowIndices[k]=a;snowIndices[k+1]=b;snowIndices[k+2]=a+1;
                    snowIndices[k+3]=a+1;snowIndices[k+4]=b;snowIndices[k+5]=b+1;
                }
            }
            groundWaterMesh=GroundMesh("Spatial floor runoff / conserved liters",groundWaterVertices,waterIndices,floorWaterMaterial);groundWaterMesh.uv=waterUV;
            groundSnowMesh=GroundMesh("Spatial dumped floor snow / conserved volume",groundSnowVertices,snowIndices,Mat("Floor snow deposits",new Color(.82f,.88f,.90f),0,.14f));
        }
        Mesh GroundMesh(string name,Vector3[] vertices,int[] triangles,Material material)
        {
            Transform node=Group(name,root,Vector3.zero);
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.MarkDynamic();mesh.vertices=vertices;mesh.triangles=triangles;
            Vector3[] normals=new Vector3[vertices.Length];for(int i=0;i<normals.Length;i++)normals[i]=Vector3.up;mesh.normals=normals;
            mesh.bounds=new Bounds(new Vector3(0,1,-10),new Vector3(120,6,150));
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            return mesh;
        }
        void UpdateGroundMaterials(float dt)
        {
            groundUpdate-=dt;if(groundUpdate>0)return;groundUpdate=.2f;
            var field=game.Sim.Environment;if(groundRevision==field.Revision)return;groundRevision=field.Revision;
            bool snowChanged=false;
            for(int i=0;i<field.Cells.Length;i++)
            {
                float x=field.CellX(i%field.Columns),z=field.CellZ(i/field.Columns);
                var cell=field.Cells[i];bool floor=game.Sim.Layout.ContainsFloor(x,z)&&!game.Sim.Ice.IsInside(x,z);
                bool waterFloor=game.Sim.Layout.ContainsFloor(cell.WaterX,cell.WaterZ)&&!game.Sim.Layout.ContainsPit(cell.WaterX,cell.WaterZ)&&!game.Sim.Ice.IsInside(cell.WaterX,cell.WaterZ);
                float radius=waterFloor?Mathf.Min(.70f,Mathf.Sqrt(cell.WaterLiters*.20f)):0;
                float y=radius>.005f?.002f:-.25f;int w=i*4;
                Vector3 centre=new Vector3(cell.WaterX,y,cell.WaterZ);
                byte opacity=(byte)Mathf.RoundToInt(Mathf.Clamp01(.25f+cell.WaterLiters*.3f)*255);
                for(int j=0;j<4;j++)
                {
                    Vector3 point=centre+new Vector3(j==0||j==3?-radius:radius,0,j<2?-radius:radius);
                    if(radius>0)point=ClipFloorWater(centre,point);
                    groundWaterVertices[w+j]=point;groundWaterColors[w+j]=new Color32(255,255,255,opacity);
                }
                float snow=floor?cell.SnowM3:0;
                if(groundSnowSamples[i]!=snow){groundSnowSamples[i]=snow;snowChanged=true;}
            }
            groundWaterMesh.SetVertices(groundWaterVertices);groundWaterMesh.SetColors(groundWaterColors);
            if(snowChanged)UpdateFloorSnow(field);
        }
        void UpdateFloorSnow(EnvironmentField field)
        {
            // One continuous half-metre height mesh replaces per-cell pyramids.
            // Bilinear bulk-density reconstruction blends neighbouring deposits;
            // normalization preserves the visible integrated ground volume.
            double target=0,integral=0;
            for(int i=0;i<field.Cells.Length;i++)
            {
                target+=groundSnowSamples[i];
                float cx=field.CellX(i%field.Columns),cz=field.CellZ(i/field.Columns);
                for(int z=0;z<3;z++)for(int x=0;x<3;x++)
                {
                    float px=cx+(x-1)*.5f,pz=cz+(z-1)*.5f;
                    float h=FloorSnowDepth(field,px,pz);
                    if(!game.Sim.Layout.ContainsFloor(px,pz)||game.Sim.Ice.IsInside(px,pz))h=0;
                    h*=1+.055f*Mathf.Sin(px*7.7f+pz*4.3f)+.035f*Mathf.Cos(pz*9.1f-px*3.9f);
                    groundSnowVertices[i*9+z*3+x]=new Vector3(px,h,pz);
                }
                for(int z=0;z<2;z++)for(int x=0;x<2;x++)
                {
                    int a=i*9+z*3+x,b=a+3;
                    integral+=(groundSnowVertices[a].y+groundSnowVertices[b].y+groundSnowVertices[a+1].y)/24.0;
                    integral+=(groundSnowVertices[a+1].y+groundSnowVertices[b].y+groundSnowVertices[b+1].y)/24.0;
                }
            }
            float normalization=integral>0?(float)(target/integral):0;
            for(int i=0;i<field.Cells.Length;i++)
            {
                bool visible=false;for(int v=0;v<9;v++)if(groundSnowVertices[i*9+v].y>.000001f){visible=true;break;}
                for(int v=0;v<9;v++)
                {var p=groundSnowVertices[i*9+v];p.y=visible?.0005f+p.y*normalization:-.25f;groundSnowVertices[i*9+v]=p;}
            }
            groundSnowMesh.SetVertices(groundSnowVertices);groundSnowMesh.RecalculateNormals();groundSnowMesh.RecalculateBounds();
        }
        float FloorSnowDepth(EnvironmentField field,float x,float z)
        {
            float gx=(x-field.MinX)/field.CellSize-.5f,gz=(z-field.MinZ)/field.CellSize-.5f;
            int col=Mathf.FloorToInt(gx),row=Mathf.FloorToInt(gz);float fx=gx-col,fz=gz-row,total=0;
            for(int dz=0;dz<2;dz++)for(int dx=0;dx<2;dx++)
            {
                int c=col+dx,r=row+dz;if(c<0||c>=field.Columns||r<0||r>=field.Rows)continue;
                total+=groundSnowSamples[r*field.Columns+c]*(dx==0?1-fx:fx)*(dz==0?1-fz:fz);
            }
            return total/(field.CellSize*field.CellSize);
        }
        Vector3 ClipFloorWater(Vector3 centre,Vector3 point)
        {
            var s=game.Sim;
            if(s.Layout.ContainsFloor(point.x,point.z,.02f)&&!s.Layout.ContainsPit(point.x,point.z)&&!s.Ice.IsInside(point.x,point.z))return point;
            float low=0,high=1;
            for(int n=0;n<7;n++)
            {
                float middle=(low+high)*.5f;Vector3 p=Vector3.Lerp(centre,point,middle);
                if(s.Layout.ContainsFloor(p.x,p.z,.02f)&&!s.Layout.ContainsPit(p.x,p.z)&&!s.Ice.IsInside(p.x,p.z))low=middle;else high=middle;
            }
            return Vector3.Lerp(centre,point,low);
        }
        void BuildEffects()
        {
            hose=Line("Live service hose",rubber,.035f,16);hose.numCornerVertices=5;hose.numCapVertices=4;
            runoff=Line("Water falling at actual overflow source",waterMaterial,.025f,2);runoff.numCornerVertices=2;
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
        void OnDestroy(){foreach(var material in ownedMaterials)if(material!=null)Destroy(material);if(fillClip!=null)Destroy(fillClip);if(overflowClip!=null)Destroy(overflowClip);if(couplingClip!=null)Destroy(couplingClip);if(groundWaterMesh!=null)Destroy(groundWaterMesh);if(groundSnowMesh!=null)Destroy(groundSnowMesh);}
    }
}
