using IceResurfacer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace IceResurfacer.Presentation
{
    // Sampled visuals of simulation transfers, never a second material simulation.
    // Two reusable vertex buffers and four bounded particle pools; no frame objects.
    public sealed class IceSnowView
    {
        readonly IceWorld world;
        readonly Surface tank, pit;
        readonly ParticleSystem discharge, overflow, conditioner, dump;
        readonly ParticleSystem[] pools;
        float tankValue=-1,pitValue=-1,tankTilt=-1;

        sealed class Surface
        {
            public readonly Mesh Mesh;
            public readonly MeshRenderer Renderer;
            readonly Vector3[] vertices;
            readonly float[] profile;
            readonly float length;
            const int X=12,Z=16;
            public Surface(Transform target,IceWorld world,float width,float length,bool pile)
            {
                this.length=length;
                Mesh=new Mesh { name=target.name+" / reusable surface" }; Mesh.MarkDynamic();
                world.Meshes.Add(Mesh);
                Renderer=target.GetComponent<MeshRenderer>();
                target.GetComponent<MeshFilter>().sharedMesh=Mesh;
                vertices=new Vector3[(X+1)*(Z+1)];profile=new float[vertices.Length];
                var indices=new int[X*Z*6];
                float mean=0;
                for(int z=0;z<=Z;z++)for(int x=0;x<=X;x++)
                {
                    int i=z*(X+1)+x;float u=x/(float)X,v=z/(float)Z;
                    vertices[i]=new Vector3((u-.5f)*width,0,(v-.5f)*length);
                    profile[i]=pile?.08f+Mathf.Sin(u*Mathf.PI)*Mathf.Sin(v*Mathf.PI):Mathf.Sin(x*1.14f+z*.37f)*.55f+Mathf.Cos(z*.83f-x*.52f)*.45f;
                    mean+=profile[i];
                    if(x<X&&z<Z)
                    {
                        int k=(z*X+x)*6;
                        indices[k]=i;indices[k+1]=i+X+1;indices[k+2]=i+1;
                        indices[k+3]=i+1;indices[k+4]=i+X+1;indices[k+5]=i+X+2;
                    }
                }
                // Equal-area triangles: normalize their integrated profile, not
                // just the vertex average, so height corresponds to bulk volume.
                mean=0;for(int i=0;i<indices.Length;i++)mean+=profile[indices[i]];
                mean/=indices.Length;
                for(int i=0;i<profile.Length;i++)profile[i]=pile?profile[i]/mean:profile[i]-mean;
                Mesh.vertices=vertices;Mesh.triangles=indices;
                Set(0,0,pile);
            }
            public void Set(float height,float roughness,bool pile)
            {
                Renderer.enabled=height>.00001f;
                for(int i=0;i<vertices.Length;i++)vertices[i].y=pile?height*profile[i]:height+profile[i]*roughness;
                Mesh.vertices=vertices;Mesh.RecalculateNormals();Mesh.RecalculateBounds();
            }
            public void SetTank(float height,float roughness,float tilt)
            {
                Renderer.enabled=height>.00001f;
                // Linear fore/aft weighting integrates to zero over the same triangles.
                // Contents migrate toward the opening as the bin pitches; the mesh's
                // integrated bulk volume remains the same Core-owned stored quantity.
                for(int i=0;i<vertices.Length;i++)
                    vertices[i].y=height*(1+vertices[i].z/length*1.6f*tilt)+profile[i]*roughness;
                Mesh.vertices=vertices;Mesh.RecalculateNormals();Mesh.RecalculateBounds();
            }
        }

        public IceSnowView(IceWorld world,Transform root,Material template)
        {
            this.world=world;
            tank=new Surface(world.TankSnow,world,1.488f,2.026f,false);
            pit=new Surface(world.PitSnow,world,3.5f,1.6f,true);
            discharge=Pool("Snow conveyed into inspection bin",root,template,.035f,.55f);
            overflow=Pool("Congested vertical discharge spilling sideways",root,template,.06f,1.8f);
            conditioner=Pool("Conditioner snow spill",root,template,.055f,.6f);
            dump=Pool("Snow unloading through forward door",root,template,.10f,1.5f);
            pools=new[]{discharge,overflow,conditioner,dump};
        }
        ParticleSystem Pool(string name,Transform root,Material template,float size,float lifetime)
        {
            var obj=new GameObject(name);obj.transform.SetParent(root,false);
            var ps=obj.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=true;main.maxParticles=360;
            main.startLifetime=lifetime;main.startSize=size;main.startSpeed=0;
            main.startColor=new Color(.91f,.96f,1);main.gravityModifier=1;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var shape=ps.shape;shape.enabled=false;
            var emission=ps.emission;emission.rateOverTime=0;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;
            var material=new Material(template) { name=name+" / snow material" };
            material.SetColor("_BaseColor",new Color(.91f,.96f,1));world.Materials.Add(material);
            renderer.sharedMaterial=material;ps.Play();return ps;
        }
        static void Rate(ParticleSystem ps,float rate,Vector3 position,Vector3 velocity)
        {
            ps.transform.position=position;
            var emission=ps.emission;emission.rateOverTime=Mathf.Clamp(rate*14000,0,330);
            var motion=ps.velocityOverLifetime;motion.enabled=true;motion.space=ParticleSystemSimulationSpace.World;
            motion.x=velocity.x;motion.y=velocity.y;motion.z=velocity.z;
        }
        public void Reset()
        {
            foreach(var ps in pools)ps.Clear();
            tankValue=-1;pitValue=-1;tankTilt=-1;
        }
        public void Update(Simulation sim,float dt)
        {
            float fill=Mathf.Clamp01(sim.SnowTankM3/sim.Config.SnowCapacityM3);
            if(Mathf.Abs(fill-tankValue)>.00001f||Mathf.Abs(sim.TankLift-tankTilt)>.0001f)
            {
                // The tank stores loose-snow equivalents (3.54 m3) in the
                // roughly 2.83 m3 physical shell via 1.25:1 compaction.
                float height=fill*2.83f/(1.488f*2.026f);
                tank.SetTank(height,Mathf.Min(height*.15f,.035f)*(1-fill),Mathf.Clamp01(sim.TankLift)*(1-fill));
                tankValue=fill;tankTilt=sim.TankLift;
            }
            if(sim.DumpedSnowM3!=pitValue)
            { pit.Set(sim.DumpedSnowM3/(3.5f*1.6f),0,true);pitValue=sim.DumpedSnowM3; }
            bool active=dt>0;
            Rate(discharge,active?sim.DischargeSnowRate:0,world.TankDischarge.position,world.Machine.forward*.45f+Vector3.down*.25f);
            // A box spray emits on both sides; the ice receives the same transfer
            // volume through Simulation.SpillAtDischarge, independently of particles.
            var overflowShape=overflow.shape;overflowShape.enabled=true;overflowShape.shapeType=ParticleSystemShapeType.Box;
            overflowShape.scale=new Vector3(1.9f,.12f,.2f);
            overflow.transform.rotation=world.Machine.rotation;
            Rate(overflow,active?sim.VerticalSpillRate:0,world.Machine.TransformPoint(0,1.96f,.20f),world.Machine.right*.25f);
            Rate(conditioner,active?Mathf.Max(0,sim.EscapingSnowRate-sim.VerticalSpillRate):0,world.SnowOutlet.position,Vector3.up*.3f);
            Vector3 toPit=world.PitSnow.position-world.DumpOutlet.position;toPit.y=0;
            // Choose horizontal velocity from the actual fall time to the pit floor.
            // A fixed multiplier sent particles beyond the pit at some lift angles.
            float fallTime=Mathf.Sqrt(2*Mathf.Max(.08f,world.DumpOutlet.position.y-world.PitSnow.position.y)/9.81f);
            Rate(dump,active?sim.DumpSnowRate:0,world.DumpOutlet.position,toPit/Mathf.Max(.2f,fallTime));
            var dumpMain=dump.main;dumpMain.startSize=sim.ResidualSnowM3>0&&sim.SnowTankM3<=sim.ResidualSnowM3+.001f?.075f:.10f;
            dumpMain.startLifetime=Mathf.Clamp(fallTime,.2f,1.5f);
            // Paused particle pools must not continue shedding material or falling.
            foreach(var ps in pools)
            {
                if(active&&ps.isPaused)ps.Play();
                else if(!active&&!ps.isPaused)ps.Pause();
            }
        }
    }
}
