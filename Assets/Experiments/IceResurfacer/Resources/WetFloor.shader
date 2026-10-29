Shader "IceResurfacer/WetFloor"
{
    Properties
    {
        _BaseColor("Wet concrete",Color)=(0.12,0.15,0.17,0.58)
        [HideInInspector] _Gate("Gate origin",Vector)=(0,0,0,0)
        [HideInInspector] _Room("Room footprint",Vector)=(0,0,0,0)
        [HideInInspector] _Pit("Pit footprint",Vector)=(0,0,0,0)
        [HideInInspector] _Rink("Rink footprint",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Finite floor water"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _Gate,_Room,_Pit,_Rink;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; half4 color:COLOR; };
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
                o.uv=v.uv;o.color=v.color;return o;
            }
            half4 frag(Varyings v):SV_Target
            {
                float2 delta=v.positionWS.xz-_Gate.xy;
                float2 room=float2(delta.x+delta.y,delta.x-delta.y)*.70710678;
                bool onFloor=(abs(room.x)<=_Room.x&&room.y>=_Room.y&&room.y<=_Room.z)||(abs(room.x)<=_Room.w&&room.y>=-1.1&&room.y<=_Room.y);
                bool pit=abs(room.x-_Pit.x)<=_Pit.z&&abs(room.y-_Pit.y)<=_Pit.w;
                float2 iceCorner=max(0,abs(v.positionWS.xz)-(_Rink.xy-_Rink.z));
                bool ice=dot(iceCorner,iceCorner)<=_Rink.z*_Rink.z+.000001;
                clip(onFloor&&!pit&&!ice?1:-1);
                float2 p=v.uv*2-1;
                float irregular=.055*sin(v.positionWS.x*8.3+v.positionWS.z*5.7)+.035*sin(v.positionWS.x*17.1-v.positionWS.z*11.3);
                half edge=1-smoothstep(.62,1.0,length(p)+irregular);
                half alpha=edge*_BaseColor.a*v.color.a;clip(alpha-.004);
                float ripple=.003*sin(v.positionWS.x*19+v.positionWS.z*13);
                InputData lighting=(InputData)0;
                lighting.positionWS=v.positionWS;lighting.normalWS=normalize(float3(ripple,1,-ripple));
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.positionWS);
                lighting.shadowCoord=TransformWorldToShadowCoord(v.positionWS);
                lighting.bakedGI=SampleSH(lighting.normalWS);lighting.shadowMask=half4(1,1,1,1);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=_BaseColor.rgb;surface.metallic=0;surface.specular=half3(.12,.12,.12);
                surface.smoothness=.73+edge*.10;surface.normalTS=half3(0,0,1);surface.occlusion=1;surface.alpha=alpha;
                half4 result=UniversalFragmentPBR(lighting,surface);result.a=alpha;return result;
            }
            ENDHLSL
        }
    }
}
