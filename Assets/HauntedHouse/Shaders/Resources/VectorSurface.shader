Shader "HollowHouse/VectorSurface"
{
    Properties
    {
        _Exposure ("Lantern exposure", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "HiddenLineDepth"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float distance : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float _Exposure;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                output.distance = distance(positionWS, _WorldSpaceCameraPos);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half fade = saturate(1.1 - input.distance / 38.0);
                return half4(input.color.rgb * max(0.35, _Exposure) * fade, 1);
            }
            ENDHLSL
        }
    }
}
