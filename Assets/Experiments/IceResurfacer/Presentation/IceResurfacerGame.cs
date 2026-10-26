using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace IceResurfacer.Presentation
{
    [DisallowMultipleComponent]
    public sealed class IceResurfacerGame : MonoBehaviour
    {
        public Material litTemplate;
        public Material iceTemplate;
        public SimulatorConfig calibration = new SimulatorConfig();
        public Simulation Sim { get; private set; }
        public IceWorld World { get; private set; }
        public IceSurfaceView Surface { get; private set; }
        public bool Started { get; private set; }
        public bool Paused { get; private set; }
        public bool HelpVisible { get; private set; }
        public bool Looking { get; private set; }
        public Camera DriverCamera { get; private set; }
        public RunReport Report { get; private set; }
        public string Notice { get; set; } = "";
        IceDashboard dashboard;
        public IceDashboard Dashboard { get { return dashboard; } }
        IceMachineAudio machineAudio;
        ParticleSystem snow, water;
        Transform drawbar;
        float accumulator, surveyTimer, yaw, pitch=5, wheelRoll, lastLift, lastTankLift, breakerAnimation;
        Quaternion tankRest, bladeRest, valveRest, floodRest, liftRest, steeringRest, horizontalLeverRest, verticalLeverRest;
        Quaternion acceleratorRest, brakeRest;
        Vector3 breakerRest;
        Quaternion[] frontRest, rearRest;
        int observedMistakes;
        Color oldAmbient, oldFog;
        AmbientMode oldAmbientMode;
        float oldAmbientIntensity, oldFogDensity;
        bool oldFogEnabled, oldCursorVisible;
        CursorLockMode oldCursorLock;

        void Awake()
        {
            if (litTemplate == null || iceTemplate == null)
            { Debug.LogError("Ice Resurfacer needs its owned Lit and SpatialIce materials. Open its saved launch scene.", this); enabled=false; return; }
            oldAmbient=RenderSettings.ambientLight; oldAmbientMode=RenderSettings.ambientMode;
            oldAmbientIntensity=RenderSettings.ambientIntensity;
            oldFog=RenderSettings.fogColor; oldFogEnabled=RenderSettings.fog; oldFogDensity=RenderSettings.fogDensity;
            oldCursorVisible=Cursor.visible; oldCursorLock=Cursor.lockState;
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.43f,.51f,.6f); RenderSettings.ambientIntensity=1;
            RenderSettings.fog=true; RenderSettings.fogColor=new Color(.10f,.15f,.20f); RenderSettings.fogDensity=.0035f;
            World=IceWorldBuilder.Build(transform,litTemplate,calibration);
            var hitch=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hitch.name="Articulating conditioner drawbar"; hitch.transform.SetParent(transform,false);
            Destroy(hitch.GetComponent<Collider>()); drawbar=hitch.transform;
            var hitchMaterial=new Material(litTemplate) { name="IceResurfacer / drawbar steel" };
            hitchMaterial.SetColor("_BaseColor",new Color(.35f,.43f,.46f)); hitchMaterial.SetFloat("_Metallic",.8f);
            hitch.GetComponent<Renderer>().sharedMaterial=hitchMaterial; World.Materials.Add(hitchMaterial);
            var cameraObject=new GameObject("Seated operator / independent head look");
            cameraObject.transform.SetParent(World.CameraMount,false);
            DriverCamera=cameraObject.AddComponent<Camera>();
            // The previous wide 76-degree lens reduced the apparent size of the authentic
            // 77-inch cut. A 64-degree seated lens projects it about 25% wider at equal distance.
            DriverCamera.fieldOfView=64; DriverCamera.nearClipPlane=.035f; DriverCamera.farClipPlane=180;
            DriverCamera.clearFlags=CameraClearFlags.SolidColor;
            DriverCamera.backgroundColor=new Color(.09f,.13f,.18f);
            cameraObject.AddComponent<AudioListener>();
            tankRest=World.TankPivot.localRotation; bladeRest=World.BladeWheel.localRotation;
            valveRest=World.WashValve.localRotation; floodRest=World.FloodLever.localRotation;
            liftRest=World.ConditionerLever.localRotation; steeringRest=World.SteeringWheel.localRotation;
            horizontalLeverRest=World.HorizontalAugerLever.localRotation;
            verticalLeverRest=World.VerticalAugerLever.localRotation;
            breakerRest=World.SnowBreakerPlunger.localPosition;
            acceleratorRest=World.AcceleratorPedal.localRotation; brakeRest=World.BrakePedal.localRotation;
            frontRest=Capture(World.FrontWheels); rearRest=Capture(World.RearWheels);
            machineAudio=gameObject.AddComponent<IceMachineAudio>(); machineAudio.Initialize();
            snow=Particles("Escaping shaved snow",new Color(.91f,.96f,1),.065f);
            water=Particles("Water at distribution towel",new Color(.25f,.62f,.76f,.65f),.018f);
            var waterShape=water.shape; waterShape.scale=new Vector3(calibration.ConditionerWidth,.05f,.15f);
            dashboard=new IceDashboard();
            NewSession(false,false);
        }
        static Quaternion[] Capture(Transform[] targets)
        {
            var result=new Quaternion[targets.Length];
            for(int i=0;i<result.Length;i++) result[i]=targets[i].localRotation;
            return result;
        }
        ParticleSystem Particles(string label,Color color,float size)
        {
            var go=new GameObject(label); go.transform.SetParent(transform,false);
            var ps=go.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main; main.loop=true; main.playOnAwake=false; main.maxParticles=360;
            main.startLifetime=.8f; main.startSpeed=.7f; main.startSize=size; main.startColor=color;
            main.gravityModifier=.7f; main.simulationSpace=ParticleSystemSimulationSpace.World;
            var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(1.6f,.05f,.15f);
            var emission=ps.emission; emission.rateOverTime=0;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=new Material(litTemplate) { name=label+" material", color=color };
            renderer.shadowCastingMode=ShadowCastingMode.Off; ps.Play(); return ps;
        }
        public void NewSession(bool practice,bool start=true)
        {
            Surface?.Dispose(); Sim=new Simulation(calibration,practice,546);
            Surface=new IceSurfaceView(Sim.Ice,World.IceMesh,iceTemplate);
            Started=start; Paused=false; HelpVisible=false; Looking=false;
            accumulator=0; surveyTimer=0; yaw=0; pitch=5; Notice=""; observedMistakes=0; breakerAnimation=0;
            Report=Sim.GetReport(); Surface.Refresh(Sim); UpdateMachine(0);
            DriverCamera.transform.localPosition=Vector3.zero;
            DriverCamera.transform.localRotation=Quaternion.Euler(5,0,0);
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        }
        public void TogglePause() { if(Started&&!Sim.Finished) { Paused=!Paused; ReleaseLook(); } }
        public void ToggleHelp() { HelpVisible=!HelpVisible; }
        public void Finish()
        {
            if(Sim.TryFinish()) { Report=Sim.GetReport(); ReleaseLook(); }
            else Notice=Sim.LastMessage;
        }
        public void Service()
        {
            Notice=Sim.Refill()?"Water replenished and pickup pump serviced.":Sim.LastMessage;
        }
        public void OperateSnowBreaker()
        {
            if(!Started||Paused||HelpVisible||Sim.Finished||breakerAnimation>0)return;
            if(Sim.StrokeSnowBreaker())breakerAnimation=.60f;
            else Notice=Sim.LastMessage;
        }
        void Update()
        {
            if(Sim==null) return;
            var keys=Keyboard.current;
            if(keys!=null)
            {
                if(keys.escapeKey.wasPressedThisFrame) TogglePause();
                if(keys.f1Key.wasPressedThisFrame) ToggleHelp();
            }
            bool running=Started&&!Paused&&!HelpVisible&&!Sim.Finished;
            var c=Sim.Controls;
            c.Throttle=running&&keys!=null?(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0):0;
            if(keys!=null&&(keys.leftShiftKey.isPressed||keys.rightShiftKey.isPressed))c.Throttle*=.42f;
            c.Steer=running&&keys!=null?(keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0):0;
            c.Brake=running&&keys!=null&&keys.spaceKey.isPressed;
            if(running&&keys!=null)
            {
                if(keys.digit1Key.wasPressedThisFrame)c.ConditionerTarget=c.ConditionerTarget>.5f?0:1;
                if(keys.digit2Key.wasPressedThisFrame)c.HorizontalAuger=!c.HorizontalAuger;
                if(keys.digit3Key.wasPressedThisFrame)c.VerticalAuger=!c.VerticalAuger;
                if(keys.digit4Key.wasPressedThisFrame)c.BoardBrush=!c.BoardBrush;
                if(keys.digit5Key.wasPressedThisFrame)c.WashPump=!c.WashPump;
                if(keys.digit6Key.wasPressedThisFrame)OperateSnowBreaker();
                if(keys.qKey.isPressed)c.BladeDepthMm=Mathf.Max(0,c.BladeDepthMm-Time.deltaTime*.15f);
                if(keys.eKey.isPressed)c.BladeDepthMm=Mathf.Min(.8f,c.BladeDepthMm+Time.deltaTime*.15f);
            }
            UpdateLook(running);
            if(running)
            {
                accumulator+=Mathf.Min(Time.deltaTime,.12f);
                while(accumulator>=.02f) { Sim.Step(.02f); accumulator-=.02f; }
                if(Sim.Mistakes>observedMistakes) { Notice=Sim.LastMessage; observedMistakes=Sim.Mistakes; }
                surveyTimer-=Time.deltaTime;
                if(surveyTimer<=0) { Surface.Refresh(Sim); Report=Sim.GetReport(); surveyTimer=.16f; }
            }
            UpdateMachine(running?Time.deltaTime:0);
            machineAudio.SetState(Sim,running,Mathf.Abs(Sim.Conditioner.Lift-lastLift)>.0001f||Mathf.Abs(Sim.TankLift-lastTankLift)>.0001f);
            lastLift=Sim.Conditioner.Lift;
            lastTankLift=Sim.TankLift;
        }
        void UpdateLook(bool running)
        {
            var mouse=Mouse.current;
            bool held=running&&mouse!=null&&mouse.rightButton.isPressed;
            if(held)
            {
                if(!Looking)
                {
                    yaw=Mathf.DeltaAngle(0,DriverCamera.transform.localEulerAngles.y);
                    pitch=Mathf.DeltaAngle(0,DriverCamera.transform.localEulerAngles.x);
                    Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false;
                }
                Vector2 delta=mouse.delta.ReadValue();
                yaw=Mathf.Clamp(yaw+delta.x*.11f,-170,170);
                pitch=Mathf.Clamp(pitch-delta.y*.11f,-55,80);
            }
            else
            {
                if(Looking) ReleaseLook();
                // Return the target immediately; smooth only the actual head. A second
                // target filter would let the view keep turning after RMB is released.
                yaw=0; pitch=5;
            }
            Looking=held;
            float shoulder=Mathf.SmoothStep(0,1,Mathf.InverseLerp(90,135,Mathf.Abs(yaw)));
            Vector3 lean=new Vector3((yaw>0?.52f:-.30f)*shoulder,.03f*shoulder,0);
            DriverCamera.transform.localPosition=Vector3.Lerp(DriverCamera.transform.localPosition,lean,1-Mathf.Exp(-10*Time.deltaTime));
            DriverCamera.transform.localRotation=Quaternion.Slerp(DriverCamera.transform.localRotation,
                Quaternion.Euler(pitch,yaw,0),1-Mathf.Exp(-(held?18:7)*Time.deltaTime));
        }
        void ReleaseLook() { Looking=false; Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
        void UpdateMachine(float dt)
        {
            var v=Sim.Vehicle; var c=Sim.Controls;
            World.Machine.SetPositionAndRotation(new Vector3(v.X,0,v.Z),Quaternion.Euler(0,v.Heading*Mathf.Rad2Deg,0));
            World.Conditioner.SetPositionAndRotation(new Vector3(Sim.Conditioner.X,Sim.Conditioner.Lift*.34f,Sim.Conditioner.Z),
                Quaternion.Euler(0,Sim.Conditioner.Heading*Mathf.Rad2Deg,0));
            Vector3 hitchA=World.Machine.TransformPoint(0,.52f,-.45f);
            Vector3 hitchB=World.Conditioner.TransformPoint(0,.37f,.26f);
            drawbar.position=(hitchA+hitchB)*.5f; drawbar.rotation=Quaternion.FromToRotation(Vector3.up,hitchB-hitchA);
            drawbar.localScale=new Vector3(.075f,(hitchB-hitchA).magnitude*.5f,.075f);
            wheelRoll+=v.Speed*dt/0.39f*Mathf.Rad2Deg;
            for(int i=0;i<World.FrontWheels.Length;i++)
                World.FrontWheels[i].localRotation=Quaternion.Euler(0,v.SteeringAngle*Mathf.Rad2Deg,0)*frontRest[i]*Quaternion.Euler(wheelRoll,0,0);
            for(int i=0;i<World.RearWheels.Length;i++)World.RearWheels[i].localRotation=rearRest[i]*Quaternion.Euler(wheelRoll,0,0);
            World.SteeringWheel.localRotation=steeringRest*Quaternion.Euler(0,v.SteeringAngle*Mathf.Rad2Deg*5,0);
            World.TankPivot.localRotation=tankRest*Quaternion.Euler(Sim.TankLift*58,0,0);
            World.BladeWheel.localRotation=bladeRest*Quaternion.Euler(0,c.BladeDepthMm*720,0);
            World.WashValve.localRotation=valveRest*Quaternion.Euler(0,c.WashValve*270,0);
            World.FloodLever.localRotation=floodRest*Quaternion.Euler(c.FloodFlow*65-25,0,0);
            World.ConditionerLever.localRotation=liftRest*Quaternion.Euler(c.ConditionerTarget*50-25,0,0);
            World.HorizontalAugerLever.localRotation=horizontalLeverRest*Quaternion.Euler(c.HorizontalAuger?-24:18,0,0);
            World.VerticalAugerLever.localRotation=verticalLeverRest*Quaternion.Euler(c.VerticalAuger?-24:18,0,0);
            World.AcceleratorPedal.localRotation=acceleratorRest*Quaternion.Euler(Mathf.Abs(c.Throttle)*16,0,0);
            World.BrakePedal.localRotation=brakeRest*Quaternion.Euler(c.Brake?18:0,0,0);
            breakerAnimation=Mathf.Max(0,breakerAnimation-dt);
            float stroke=breakerAnimation>.35f?( .60f-breakerAnimation)/.25f:breakerAnimation/.35f;
            World.SnowBreakerPlunger.localPosition=breakerRest-Vector3.up*(Mathf.Clamp01(stroke)*.18f);
            if(c.HorizontalAuger)World.HorizontalAuger.Rotate(Vector3.right,dt*390,Space.Self);
            if(c.VerticalAuger)World.VerticalAuger.Rotate(Vector3.up,dt*540,Space.Self);
            if(World.BrushArm!=null)
                World.BrushArm.localRotation=Quaternion.Euler(0,0,c.BoardBrush?0:-33);
            if(c.BoardBrush)World.Brush.Rotate(Vector3.up,dt*540,Space.Self);
            if(World.TankSnow!=null)
                World.TankSnow.localScale=new Vector3(1,Mathf.Max(.01f,Sim.SnowTankM3/3.54f),1);
            snow.transform.position=World.SnowOutlet.position;
            snow.transform.rotation=World.Conditioner.rotation;
            var emission=snow.emission;
            emission.rateOverTime=dt>0?Mathf.Clamp(Sim.EscapingSnowRate*18000,0,260):0;
            water.transform.position=World.WaterOutlet.position;
            water.transform.rotation=World.Conditioner.rotation*Quaternion.Euler(90,0,0);
            var waterEmission=water.emission;
            waterEmission.rateOverTime=dt>0&&Sim.FloodWaterLiters>0?c.FloodFlow*90:0;
        }
        void OnGUI()
        {
            if(Sim==null)return;
            dashboard.Draw(this);
        }
        void OnApplicationFocus(bool focus) { if(!Application.isBatchMode&&!focus&&Started&&!Sim.Finished) { Paused=true; ReleaseLook(); } }
        void OnDestroy()
        {
            Surface?.Dispose(); dashboard?.Dispose();
            if(World==null)return;
            RenderSettings.ambientMode=oldAmbientMode; RenderSettings.ambientLight=oldAmbient;
            RenderSettings.ambientIntensity=oldAmbientIntensity; RenderSettings.fogColor=oldFog;
            RenderSettings.fog=oldFogEnabled; RenderSettings.fogDensity=oldFogDensity;
            Cursor.lockState=oldCursorLock; Cursor.visible=oldCursorVisible;
            // Own generated geometry/materials: clean them when replacing or unloading the scene.
            foreach(var m in World.Meshes)Destroy(m);
            foreach(var m in World.Materials)Destroy(m);
            if(snow!=null)Destroy(snow.GetComponent<ParticleSystemRenderer>().sharedMaterial);
            if(water!=null)Destroy(water.GetComponent<ParticleSystemRenderer>().sharedMaterial);
        }
    }
}
