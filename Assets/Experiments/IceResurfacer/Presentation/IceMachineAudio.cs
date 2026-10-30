using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    // Original synthesized loops; no recordings, imported IP, or audio downloads.
    public sealed class IceMachineAudio : MonoBehaviour
    {
        enum Layer { Engine,Blade,Horizontal,ConditionerLeft,ConditionerRight,TankLeft,TankRight,Flood,Wash,Pickup,Arena,Vertical,Drive,Roller,Discharge,Dump,Upper,EscapeLeft,EscapeRight }
        readonly AudioSource[] layers=new AudioSource[19];
        readonly float[] target=new float[19];
        readonly AudioClip[] clips=new AudioClip[10];
        AudioSource mechanism,breakerMechanism;
        AudioClip spoolClick,breakerStroke;
        readonly AudioSource[] impacts=new AudioSource[3];
        AudioClip rollerImpact,boardImpact,wallImpact;
        int observedContact,nextImpact;
        bool observedHorizontal,observedVertical;
        int observedBreaker;
        float lastSpeed,previousHydraulic,previousLift,previousTank;
        Simulation previousSimulation;
        IceWorld world;
        AudioSource Sound(Layer layer) {return layers[(int)layer];}
        void Target(Layer layer,float level) {target[(int)layer]=level;}
        public void Initialize()
        {
            world=GetComponent<IceResurfacerGame>().World;
            string[] names={"Engine / combustion","Blade / shaved ice","Horizontal auger / gearbox","Hydraulic cylinders / pressure","Wash and flood water","Enclosed arena / ventilation","Vertical elevator / gearbox","Hydrostatic pump / traction and deceleration","Board guide roller / contact","Snow / bin discharge and shakeout"};
            for(int i=0;i<clips.Length;i++)clips[i]=CreateLoop(names[i],i);
            Loop(Layer.Engine,0,Find("Shaped engine cover / right of operator"));
            Loop(Layer.Blade,1,world.CuttingBlade,new Vector3(0,-.5f,0));
            Loop(Layer.Horizontal,2,world.HorizontalAuger);
            Loop(Layer.ConditionerLeft,3,world.ConditionerCylinderBarrels[0]);
            Loop(Layer.ConditionerRight,3,world.ConditionerCylinderBarrels[1]);
            Loop(Layer.TankLeft,3,world.TankCylinderBarrels[0]);
            Loop(Layer.TankRight,3,world.TankCylinderBarrels[1]);
            Loop(Layer.Flood,4,world.WaterOutlet);
            Loop(Layer.Wash,4,Find("Wash water manifold"));
            Loop(Layer.Pickup,4,Find("Wash pickup squeegee contact"));
            Loop(Layer.Arena,5,transform,Vector3.zero,false);
            Loop(Layer.Vertical,6,world.VerticalAuger,new Vector3(0,.21f,0));
            Loop(Layer.Drive,7,Find("Central steel chassis"));
            Loop(Layer.Roller,8,transform);
            Loop(Layer.Discharge,9,world.TankDischarge);
            Loop(Layer.Dump,9,transform);
            Loop(Layer.Upper,9,transform);
            Loop(Layer.EscapeLeft,9,transform);
            Loop(Layer.EscapeRight,9,transform);
            mechanism=IceAudioSpatialization.Create("Machine audio / Hydraulic spool detent",world.ConditionerLever.parent,new Vector3(0,.08f,0),null,false);
            breakerMechanism=IceAudioSpatialization.Create("Machine audio / Manual snow breaker",world.SnowBreakerPlunger,new Vector3(0,.22f,0),null,false);
            mechanism.volume=breakerMechanism.volume=1;
            spoolClick=CreateMechanism("Hydraulic spool detent",false);
            breakerStroke=CreateMechanism("Manual breaker downstroke and spring return",true);
            rollerImpact=CreateImpact("Guide roller / rubber rattle",0);
            boardImpact=CreateImpact("Board / dull polyethylene and body vibration",1);
            wallImpact=CreateImpact("Service wall / dull chassis clunk",2);
            for(int i=0;i<impacts.Length;i++)
            {
                impacts[i]=IceAudioSpatialization.Create("Physical contact voice "+i,transform,Vector3.zero,null,false);
                impacts[i].volume=1;
            }
        }
        void Loop(Layer layer,int clip,Transform parent,Vector3 position=default,bool physical=true)
        {layers[(int)layer]=IceAudioSpatialization.Create("Machine audio / "+layer,parent,position,clips[clip],true,physical);}
        Transform Find(string name)
        {
            foreach(var part in transform.GetComponentsInChildren<Transform>())if(part.name==name)return part;
            throw new System.InvalidOperationException("Missing physical audio anchor: "+name);
        }
        static AudioClip CreateLoop(string name,int layer)
        {
            const int rate=22050, count=rate*3;
            var samples=new float[count]; var random=new System.Random(546+layer); float low=0;
            for(int i=0;i<count;i++)
            {
                float t=(float)i/rate, n=(float)random.NextDouble()*2-1;
                low=low*.94f+n*.06f;
                float signal;
                switch(layer)
                {
                    case 0: signal=.29f*Mathf.Sin(t*2*Mathf.PI*40)+.18f*Mathf.Sin(t*2*Mathf.PI*80)+.08f*Mathf.Sin(t*2*Mathf.PI*120)+low*.45f; break;
                    case 1: signal=(n*.36f+low*.35f)*(.7f+.3f*Mathf.Sin(t*2*Mathf.PI*17)); break;
                    case 2: signal=.12f*Mathf.Sin(t*2*Mathf.PI*210)+.07f*Mathf.Sin(t*2*Mathf.PI*420)+low*.6f; break;
                    case 3: signal=.12f*Mathf.Sin(t*2*Mathf.PI*330)+low*.5f; break;
                    case 4: signal=low*1.5f+n*.08f; break;
                    case 5: signal=low*.65f+.025f*Mathf.Sin(t*2*Mathf.PI*60); break;
                    case 6: signal=.09f*Mathf.Sin(t*2*Mathf.PI*174)+.06f*Mathf.Sin(t*2*Mathf.PI*348)+low*.55f;break;
                    case 7: signal=.10f*Mathf.Sin(t*2*Mathf.PI*290)+.045f*Mathf.Sin(t*2*Mathf.PI*580)+low*.23f;break;
                    case 8: signal=low*.65f+Mathf.Sin(t*2*Mathf.PI*35)*.035f;break;
                    default: signal=low*.9f+n*.08f*(.5f+.5f*Mathf.Sin(t*2*Mathf.PI*11));break;
                }
                // Match loop ends without a click; long noise layers have gentle zero edges.
                float fade=Mathf.Min(1,Mathf.Min(i,count-1-i)/220f);
                samples[i]=signal*fade;
            }
            var clip=AudioClip.Create(name,count,1,rate,false); clip.SetData(samples,0); return clip;
        }
        public void SetState(Simulation sim,bool running,bool hydraulic)
        {
            var c=sim.Controls;float speed=Mathf.Abs(sim.Vehicle.Speed),factor=running?1:0;
            if(previousSimulation!=sim)
            {
                previousSimulation=sim;observedHorizontal=c.HorizontalAuger;observedVertical=c.VerticalAuger;
                observedBreaker=sim.SnowBreakerStrokes;lastSpeed=speed;previousHydraulic=0;
                observedContact=sim.ContactEventSequence;previousLift=sim.Conditioner.Lift;previousTank=sim.TankLift;
                foreach(var impact in impacts)impact.Stop();
                mechanism.Stop();breakerMechanism.Stop();
            }
            float hydraulicDemand=Mathf.Abs(c.ConditionerHydraulic)+Mathf.Abs(c.TankHydraulic);
            if(running)
            {
                if(observedHorizontal!=c.HorizontalAuger||observedVertical!=c.VerticalAuger||
                   (hydraulicDemand>.1f&&previousHydraulic<=.1f))
                {mechanism.Stop();mechanism.PlayOneShot(spoolClick,.17f);}
                if(observedBreaker!=sim.SnowBreakerStrokes)
                {breakerMechanism.Stop();breakerMechanism.PlayOneShot(breakerStroke,.28f);}
                if(observedContact!=sim.ContactEventSequence)
                {
                    var hit=sim.AudibleContact;var source=impacts[nextImpact++%impacts.Length];
                    // Each bounded voice retains its world contact position for the tail.
                    source.Stop();source.transform.position=new Vector3(hit.PointX,hit.PointY,hit.PointZ);
                    bool roller=hit.GuideRoller;
                    bool wall=hit.Obstacle==MovementObstacle.ServiceWall||hit.Obstacle==MovementObstacle.TankWall;
                    source.pitch=(roller?1.10f:.96f)-sim.AudibleContactSeverity*.15f;
                    float level=roller?.045f+sim.AudibleContactSeverity*.18f:.09f+Mathf.Sqrt(sim.AudibleContactSeverity)*.55f;
                    source.PlayOneShot(roller?rollerImpact:wall?wallImpact:boardImpact,level);
                }
            }
            observedContact=sim.ContactEventSequence;
            observedHorizontal=c.HorizontalAuger;observedVertical=c.VerticalAuger;
            observedBreaker=sim.SnowBreakerStrokes;previousHydraulic=hydraulicDemand;
            float congestion=Mathf.Clamp01(sim.ConveyorCongestion);
            float deceleration=Mathf.Clamp01((lastSpeed-speed)/Mathf.Max(.001f,Time.unscaledDeltaTime));
            Target(Layer.Engine,.34f+Mathf.Clamp01(Mathf.Abs(c.Throttle))*.08f+Mathf.Clamp01(sim.MachineLoad)*.04f);
            Target(Layer.Blade,Mathf.Clamp01(speed)*Mathf.Clamp01(sim.ActualBladeDepthMm/.10f)*(1-sim.Conditioner.Lift)*.24f);
            Target(Layer.Horizontal,(c.HorizontalAuger?.14f:0)*(1+congestion*.5f));
            bool conditionerMoving=Mathf.Abs(sim.Conditioner.Lift-previousLift)>.0001f;
            bool tankMoving=Mathf.Abs(sim.TankLift-previousTank)>.0001f;
            if(hydraulic&&!conditionerMoving&&!tankMoving)
            {conditionerMoving=Mathf.Abs(c.ConditionerHydraulic)>.1f;tankMoving=Mathf.Abs(c.TankHydraulic)>.1f;}
            int groups=(conditionerMoving?1:0)+(tankMoving?1:0);
            float cylinder=hydraulic&&groups>0?.22f/groups*.5f:0;
            Target(Layer.ConditionerLeft,conditionerMoving?cylinder:0);Target(Layer.ConditionerRight,conditionerMoving?cylinder:0);
            Target(Layer.TankLeft,tankMoving?cylinder:0);Target(Layer.TankRight,tankMoving?cylinder:0);
            Target(Layer.Flood,sim.FloodWaterLiters>0?c.FloodFlow*.12f:0);
            Target(Layer.Wash,sim.WashWaterLiters>0?c.WashValve*.09f:0);
            Target(Layer.Pickup,c.WashPump?.06f:0);
            Target(Layer.Arena,.10f);
            Target(Layer.Vertical,(c.VerticalAuger?.15f:0)*(1+congestion*.6f));
            Target(Layer.Drive,Mathf.Clamp01(speed/3)*.14f+Mathf.Abs(c.Throttle)*Mathf.Clamp01(sim.MachineLoad)*.08f+deceleration*.08f);
            Target(Layer.Roller,sim.GuideRollerContact?Mathf.Clamp01(speed)*.09f:0);
            float sn=Mathf.Sin(sim.Vehicle.Heading),cs=Mathf.Cos(sim.Vehicle.Heading);
            Sound(Layer.Roller).transform.position=new Vector3(sim.Vehicle.X+cs*sim.Config.GuideRollerX+sn*sim.Config.GuideRollerZ,.26f,sim.Vehicle.Z-sn*sim.Config.GuideRollerX+cs*sim.Config.GuideRollerZ);
            float snowLevel=Mathf.Clamp01((sim.DumpSnowRate+sim.DischargeSnowRate+sim.EscapingSnowRate)*10)*.27f;
            float snowRates=sim.DischargeSnowRate+sim.DumpSnowRate+sim.VerticalSpillRate+sim.ConditionerLeftEscapeRate+sim.ConditionerRightEscapeRate;
            float perRate=snowRates>0?snowLevel/snowRates:0;
            Target(Layer.Discharge,perRate*sim.DischargeSnowRate+congestion*Mathf.Clamp01(speed)*.06f);
            Target(Layer.Dump,perRate*sim.DumpSnowRate);Target(Layer.Upper,perRate*sim.VerticalSpillRate);
            Target(Layer.EscapeLeft,perRate*sim.ConditionerLeftEscapeRate);Target(Layer.EscapeRight,perRate*sim.ConditionerRightEscapeRate);
            Sound(Layer.Dump).transform.position=sim.DumpSnowRate>0?new Vector3(sim.DumpOriginX,sim.DumpOriginY,sim.DumpOriginZ):world.DumpOutlet.position;
            Sound(Layer.Upper).transform.position=sim.VerticalSpillRate>0?new Vector3(sim.UpperSnowOriginX,sim.UpperSnowOriginY,sim.UpperSnowOriginZ):world.TankDischarge.position;
            float exitY=world.Conditioner.TransformPoint(0,.16f,0).y;
            Sound(Layer.EscapeLeft).transform.position=new Vector3(sim.ConditionerLeftExitX,exitY,sim.ConditionerLeftExitZ);
            Sound(Layer.EscapeRight).transform.position=new Vector3(sim.ConditionerRightExitX,exitY,sim.ConditionerRightExitZ);
            for(int i=0;i<layers.Length;i++)layers[i].volume=Mathf.Lerp(layers[i].volume,target[i]*factor,1-Mathf.Exp(-6*Time.unscaledDeltaTime));
            Sound(Layer.Engine).pitch=.76f+Mathf.Abs(c.Throttle)*.12f+speed*.075f+Mathf.Clamp01(sim.MachineLoad)*.08f;
            Sound(Layer.Blade).pitch=.8f+Mathf.Clamp01(speed/2)*.35f;
            Sound(Layer.Horizontal).pitch=sim.ConveyorJammed?.38f:1-congestion*.23f;
            Sound(Layer.Vertical).pitch=sim.ConveyorJammed?.34f:1-congestion*.29f;
            Sound(Layer.Drive).pitch=.72f+speed*.15f+Mathf.Clamp01(sim.MachineLoad)*.12f;
            Sound(Layer.Roller).pitch=.8f+sim.TractionSlip*.5f;
            for(int i=(int)Layer.ConditionerLeft;i<=(int)Layer.TankRight;i++)layers[i].pitch=.91f+Mathf.Clamp01(hydraulicDemand)*.11f;
            lastSpeed=speed;previousLift=sim.Conditioner.Lift;previousTank=sim.TankLift;
        }
        static AudioClip CreateMechanism(string name,bool breaker)
        {
            const int rate=22050;int count=(int)(rate*(breaker?.58f:.09f));var data=new float[count];
            var random=new System.Random(breaker?854:783);float low=0;
            for(int i=0;i<count;i++)
            {
                float t=(float)i/rate,n=(float)random.NextDouble()*2-1;low=.82f*low+.18f*n;
                float envelope=breaker?Mathf.Exp(-t*18)+.38f*Mathf.Exp(-Mathf.Abs(t-.31f)*28):Mathf.Exp(-t*55);
                data[i]=(low*.7f+Mathf.Sin(t*2*Mathf.PI*(breaker?95:360))*.2f)*envelope;
            }
            var clip=AudioClip.Create(name,count,1,rate,false);clip.SetData(data,0);return clip;
        }
        static AudioClip CreateImpact(string name,int kind)
        {
            const int rate=22050;int count=(int)(rate*(kind==0?.22f:.46f));var data=new float[count];
            var random=new System.Random(943+kind);float low=0;
            for(int i=0;i<count;i++)
            {
                float t=(float)i/rate,n=(float)random.NextDouble()*2-1;low=.90f*low+.10f*n;
                float frequency=kind==0?135:kind==1?63:49;
                float attack=1-Mathf.Exp(-t*900),decay=Mathf.Exp(-t*(kind==0?24:13));
                float secondary=.22f*Mathf.Exp(-Mathf.Abs(t-.085f)*48)*Mathf.Sin(t*2*Mathf.PI*102);
                data[i]=attack*((Mathf.Sin(t*2*Mathf.PI*frequency)*.45f+low*.6f)*decay+secondary);
            }
            var clip=AudioClip.Create(name,count,1,rate,false);clip.SetData(data,0);return clip;
        }
        void OnDestroy()
        {
            foreach(var clip in clips)if(clip!=null)Destroy(clip);
            if(spoolClick!=null)Destroy(spoolClick);if(breakerStroke!=null)Destroy(breakerStroke);
            if(rollerImpact!=null)Destroy(rollerImpact);if(boardImpact!=null)Destroy(boardImpact);if(wallImpact!=null)Destroy(wallImpact);
        }
    }
}
