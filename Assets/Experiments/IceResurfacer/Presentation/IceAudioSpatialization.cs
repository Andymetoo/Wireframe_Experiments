using UnityEngine;

namespace IceResurfacer.Presentation
{
    // Shared world-meter falloff for physical sounds, independent of camera mode.
    public static class IceAudioSpatialization
    {
        public const float NearDistance=2.5f, FarDistance=120f;
        static readonly AnimationCurve falloff=BuildFalloff();

        static AnimationCurve BuildFalloff()
        {
            float[] distances={0,2.5f,3,4,5,7.5f,10,15,20,30,40,60,80,100,120};
            var keys=new Keyframe[distances.Length];
            for(int i=0;i<keys.Length;i++)
            {
                float distance=distances[i];
                float gain=distance<=NearDistance?1:distance<=80?NearDistance/distance:distance==100?.01f:0;
                keys[i]=new Keyframe(distance/FarDistance,gain);
            }
            // Linear segments cannot overshoot or briefly grow louder with distance.
            for(int i=0;i<keys.Length;i++)
            {
                float incoming=i>0?(keys[i].value-keys[i-1].value)/(keys[i].time-keys[i-1].time):0;
                float outgoing=i+1<keys.Length?(keys[i+1].value-keys[i].value)/(keys[i+1].time-keys[i].time):0;
                keys[i].inTangent=incoming;keys[i].outTangent=outgoing;
            }
            return new AnimationCurve(keys){preWrapMode=WrapMode.ClampForever,postWrapMode=WrapMode.ClampForever};
        }

        public static float GainAt(float distance) {return falloff.Evaluate(Mathf.Max(0,distance)/FarDistance);}

        public static AudioSource Create(string name,Transform parent,Vector3 localPosition,AudioClip clip,bool loop,bool physical=true)
        {
            var node=new GameObject(name);node.transform.SetParent(parent,false);node.transform.localPosition=localPosition;
            var source=node.AddComponent<AudioSource>();
            source.playOnAwake=false;source.loop=loop;source.clip=clip;source.volume=0;
            source.spatialBlend=physical?1:0;source.dopplerLevel=0;source.panStereo=0;source.spread=0;
            source.minDistance=NearDistance;source.maxDistance=FarDistance;
            source.rolloffMode=AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff,falloff);
            // The arena has no authored acoustic zones. Keep dry distance/panning
            // measurable instead of adding a listener-independent wet audio bed.
            source.reverbZoneMix=0;
            if(loop)source.Play();
            return source;
        }
    }
}
