using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    // Original synthesized loops; no recordings, imported IP, or audio downloads.
    public sealed class IceMachineAudio : MonoBehaviour
    {
        readonly AudioSource[] layers=new AudioSource[6];
        readonly float[] target=new float[6];
        public void Initialize()
        {
            string[] names={"Engine / combustion and hydrostat","Blade / shaved ice","Conveyors / twin augers","Hydraulic lift","Wash and flood water","Enclosed arena / ventilation"};
            for(int k=0;k<layers.Length;k++)
            {
                var source=gameObject.AddComponent<AudioSource>(); layers[k]=source;
                source.playOnAwake=false; source.loop=true; source.spatialBlend=0; source.volume=0;
                source.clip=CreateLoop(names[k],k); source.Play();
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
                    default: signal=low*.65f+.025f*Mathf.Sin(t*2*Mathf.PI*60); break;
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
            target[0]=.48f;
            target[1]=Mathf.Clamp01(speed)*c.BladeDepthMm*(1-sim.Conditioner.Lift)*.65f;
            target[2]=((c.HorizontalAuger?.16f:0)+(c.VerticalAuger?.18f:0))*(1+sim.ConveyorCongestion*.65f);
            target[3]=hydraulic?.3f:0;
            target[4]=c.FloodFlow*.17f+(c.WashPump?.1f:0);
            target[5]=.15f;
            for(int i=0;i<layers.Length;i++)layers[i].volume=Mathf.Lerp(layers[i].volume,target[i]*factor,1-Mathf.Exp(-6*Time.unscaledDeltaTime));
            layers[0].pitch=.72f+speed*.12f+sim.MachineLoad*.23f;
            layers[2].pitch=sim.ConveyorJammed?.38f:1-sim.MachineLoad*.18f;
        }
        void OnDestroy() { foreach(var source in layers)if(source!=null&&source.clip!=null)Destroy(source.clip); }
    }
}
