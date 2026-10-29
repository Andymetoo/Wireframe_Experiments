using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    // Original synthesized loops; no recordings, imported IP, or audio downloads.
    public sealed class IceMachineAudio : MonoBehaviour
    {
        readonly AudioSource[] layers=new AudioSource[10];
        readonly float[] target=new float[10];
        AudioSource mechanism;
        AudioClip spoolClick,breakerStroke;
        readonly AudioSource[] impacts=new AudioSource[3];
        AudioClip rollerImpact,boardImpact,wallImpact;
        int observedContact,nextImpact;
        bool observedHorizontal,observedVertical;
        int observedBreaker;
        float lastSpeed,previousHydraulic;
        Simulation previousSimulation;
        public void Initialize()
        {
            string[] names={"Engine / combustion","Blade / shaved ice","Horizontal auger / gearbox","Hydraulic cylinders / pressure","Wash and flood water","Enclosed arena / ventilation","Vertical elevator / gearbox","Hydrostatic pump / traction and deceleration","Board guide roller / contact","Snow / bin discharge and shakeout"};
            for(int k=0;k<layers.Length;k++)
            {
                var node=k==8?new GameObject("Guide roller contact audio"):gameObject;
                if(k==8)node.transform.SetParent(transform,false);
                var source=node.AddComponent<AudioSource>(); layers[k]=source;
                source.playOnAwake=false; source.loop=true; source.spatialBlend=k==8?1:0; source.volume=0;
                if(k==8){source.rolloffMode=AudioRolloffMode.Logarithmic;source.minDistance=2;source.maxDistance=22;source.dopplerLevel=0;}
                source.clip=CreateLoop(names[k],k); source.Play();
            }
            mechanism=gameObject.AddComponent<AudioSource>();mechanism.playOnAwake=false;mechanism.spatialBlend=0;
            spoolClick=CreateMechanism("Hydraulic spool detent",false);
            breakerStroke=CreateMechanism("Manual breaker downstroke and spring return",true);
            rollerImpact=CreateImpact("Guide roller / rubber rattle",0);
            boardImpact=CreateImpact("Board / dull polyethylene and body vibration",1);
            wallImpact=CreateImpact("Service wall / dull chassis clunk",2);
            for(int i=0;i<impacts.Length;i++)
            {
                var node=new GameObject("Physical contact voice "+i);node.transform.SetParent(transform,false);
                var source=node.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;
                source.rolloffMode=AudioRolloffMode.Logarithmic;source.minDistance=2;source.maxDistance=26;source.dopplerLevel=0;impacts[i]=source;
            }
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
            var c=sim.Controls; float speed=Mathf.Abs(sim.Vehicle.Speed), factor=running?1:0;
            if(previousSimulation!=sim)
            {
                previousSimulation=sim;observedHorizontal=c.HorizontalAuger;observedVertical=c.VerticalAuger;
                observedBreaker=sim.SnowBreakerStrokes;lastSpeed=speed;previousHydraulic=0;
                observedContact=sim.ContactEventSequence;
            }
            float hydraulicDemand=Mathf.Abs(c.ConditionerHydraulic)+Mathf.Abs(c.TankHydraulic);
            if(running)
            {
                if(observedHorizontal!=c.HorizontalAuger||observedVertical!=c.VerticalAuger||
                   (hydraulicDemand>.1f&&previousHydraulic<=.1f))mechanism.PlayOneShot(spoolClick,.17f);
                if(observedBreaker!=sim.SnowBreakerStrokes)mechanism.PlayOneShot(breakerStroke,.28f);
                if(observedContact!=sim.ContactEventSequence)
                {
                    var hit=sim.AudibleContact;var source=impacts[nextImpact++%impacts.Length];
                    source.transform.position=new Vector3(hit.PointX,Mathf.Max(.24f,hit.PointY),hit.PointZ);
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
            target[0]=.34f+Mathf.Clamp01(Mathf.Abs(c.Throttle))*.08f+Mathf.Clamp01(sim.MachineLoad)*.04f;
            target[1]=Mathf.Clamp01(speed)*Mathf.Clamp01(sim.ActualBladeDepthMm/.10f)*(1-sim.Conditioner.Lift)*.24f;
            target[2]=(c.HorizontalAuger?.14f:0)*(1+congestion*.5f);
            target[3]=hydraulic?.22f:0;
            target[4]=(sim.FloodWaterLiters>0?c.FloodFlow*.12f:0)+(sim.WashWaterLiters>0?c.WashValve*.09f:0)+(c.WashPump?.06f:0);
            target[5]=.10f;
            target[6]=(c.VerticalAuger?.15f:0)*(1+congestion*.6f);
            target[7]=Mathf.Clamp01(speed/3)*.14f+Mathf.Abs(c.Throttle)*Mathf.Clamp01(sim.MachineLoad)*.08f+deceleration*.08f;
            target[8]=sim.GuideRollerContact?Mathf.Clamp01(speed)*.09f:0;
            float sn=Mathf.Sin(sim.Vehicle.Heading),cs=Mathf.Cos(sim.Vehicle.Heading);
            layers[8].transform.position=new Vector3(sim.Vehicle.X+cs*sim.Config.GuideRollerX+sn*sim.Config.GuideRollerZ,.26f,sim.Vehicle.Z-sn*sim.Config.GuideRollerX+cs*sim.Config.GuideRollerZ);
            target[9]=Mathf.Clamp01((sim.DumpSnowRate+sim.DischargeSnowRate+sim.EscapingSnowRate)*10)*.27f+congestion*Mathf.Clamp01(speed)*.06f;
            for(int i=0;i<layers.Length;i++)layers[i].volume=Mathf.Lerp(layers[i].volume,target[i]*factor,1-Mathf.Exp(-6*Time.unscaledDeltaTime));
            layers[0].pitch=.76f+Mathf.Abs(c.Throttle)*.12f+speed*.075f+Mathf.Clamp01(sim.MachineLoad)*.08f;
            layers[1].pitch=.8f+Mathf.Clamp01(speed/2)*.35f;
            layers[2].pitch=sim.ConveyorJammed?.38f:1-congestion*.23f;
            layers[6].pitch=sim.ConveyorJammed?.34f:1-congestion*.29f;
            layers[7].pitch=.72f+speed*.15f+Mathf.Clamp01(sim.MachineLoad)*.12f;
            layers[8].pitch=.8f+sim.TractionSlip*.5f;
            layers[3].pitch=.91f+Mathf.Clamp01(hydraulicDemand)*.11f;
            lastSpeed=speed;
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
            foreach(var source in layers)if(source!=null&&source.clip!=null)Destroy(source.clip);
            if(spoolClick!=null)Destroy(spoolClick);if(breakerStroke!=null)Destroy(breakerStroke);
            if(rollerImpact!=null)Destroy(rollerImpact);if(boardImpact!=null)Destroy(boardImpact);if(wallImpact!=null)Destroy(wallImpact);
        }
    }
}
