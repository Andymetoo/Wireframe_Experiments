using System;
using IceResurfacer.Core;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>Persistent scene settings; transient meters stay in the Inspector.</summary>
    [DisallowMultipleComponent,RequireComponent(typeof(IceResurfacerGame))]
    [AddComponentMenu("Ice Resurfacer/Operator Tuning")]
    public sealed class IceOperatorTuning : MonoBehaviour
    {
        public OperatorTuningSettings simulation=new OperatorTuningSettings();
        [Tooltip("Presentation only. Scales the displayed fraction in the bin; never changes snow stock, gauge or capacity. Zero stock always looks empty.")]
        [Range(.25f,3)] public float tankVisualFullness=1;
        [Tooltip("Presentation only. More samples of actual overflow, within existing bounded particle pools. No overflow means no particles.")]
        [Range(.25f,5)] public float overflowParticleDensity=1;
        [Tooltip("Presentation only. Size of transient overflow clumps; persistent ground snow still uses actual cell depth.")]
        [Range(.5f,2)] public float overflowClumpSize=1;
        [Tooltip("Presentation only. Redistributes each cell's existing snow volume into angular facets; zero is a gentler surface. Does not alter deposits or pickup.")]
        [Range(0,1)] public float windrowRoughness=.7f;

        [NonSerialized] public string lastError;
        [NonSerialized] public float measuredFloodLps,measuredWashSupplyLps,measuredWashReturnLps;
        [NonSerialized] public float measuredShavedSolidM3ps,measuredExpandedM3ps,measuredLoosePickupM3ps,measuredTankDeliveryM3ps;
        Simulation observed;
        float sampledTime;
        double flood,wash,returned,shaved,expanded,loose,tank;
        public float TankVisualMultiplier=>isActiveAndEnabled?tankVisualFullness:1;
        public float OverflowDensityMultiplier=>isActiveAndEnabled?overflowParticleDensity:1;
        public float OverflowSizeMultiplier=>isActiveAndEnabled?overflowClumpSize:1;
        public float WindrowRoughness=>isActiveAndEnabled?windrowRoughness:.7f;

        public SimulatorConfig CreateRuntimeConfig(SimulatorConfig authored)
        {
            var copy=JsonUtility.FromJson<SimulatorConfig>(JsonUtility.ToJson(authored));
            ApplyRuntime(copy,authored);copy.Validate();return copy;
        }
        public void ApplyRuntime(SimulatorConfig target,SimulatorConfig authored)
        {
            // The saved calibration is never a target; disabling is reversible
            // during Play, without refilling tanks or rescaling existing snow.
            if(ReferenceEquals(target,authored))return;
            if(!isActiveAndEnabled){OperatorTuningSettings.Restore(target,authored);lastError=null;return;}
            try{simulation.Apply(target,authored);lastError=null;}
            catch(ArgumentException e){lastError="Invalid tuning: "+e.Message;}
        }
        void Reset()
        {var owner=GetComponent<IceResurfacerGame>();if(owner!=null)simulation=OperatorTuningSettings.From(owner.calibration);}
        void OnDisable()
        {var owner=GetComponent<IceResurfacerGame>();if(owner!=null&&owner.Sim!=null)ApplyRuntime(owner.Sim.Config,owner.calibration);}
        void LateUpdate()
        {var owner=GetComponent<IceResurfacerGame>();if(owner!=null&&owner.Sim!=null)SampleTelemetry(owner.Sim);}
        public void SampleTelemetry(Simulation s)
        {
            if(observed!=s)
            {
                observed=s;sampledTime=0;flood=wash=returned=shaved=expanded=loose=tank=0;
                measuredFloodLps=measuredWashSupplyLps=measuredWashReturnLps=0;
                measuredShavedSolidM3ps=measuredExpandedM3ps=measuredLoosePickupM3ps=measuredTankDeliveryM3ps=0;
            }
            float dt=s.Time-sampledTime;if(dt<.25f)return;
            measuredFloodLps=(float)((s.TotalFloodUsedLiters-flood)/dt);
            measuredWashSupplyLps=(float)((s.TotalWashSuppliedLiters-wash)/dt);
            double totalReturned=s.TotalWashRecirculatedLiters+s.TotalRecoveredSurfaceLiters;
            measuredWashReturnLps=(float)((totalReturned-returned)/dt);
            measuredShavedSolidM3ps=(float)((s.TotalShavedIceM3-shaved)/dt);
            measuredExpandedM3ps=(float)((s.TotalExpandedShavingsM3-expanded)/dt);
            measuredLoosePickupM3ps=(float)((s.TotalLooseSnowPickupM3-loose)/dt);
            measuredTankDeliveryM3ps=(float)((s.TotalSnowCollectedM3-tank)/dt);
            sampledTime=s.Time;flood=s.TotalFloodUsedLiters;wash=s.TotalWashSuppliedLiters;returned=totalReturned;
            shaved=s.TotalShavedIceM3;expanded=s.TotalExpandedShavingsM3;loose=s.TotalLooseSnowPickupM3;tank=s.TotalSnowCollectedM3;
        }
    }
}
